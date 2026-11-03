using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Projects;

/// <summary>
/// Undo and redo of a project document (proposal F5, sections 2.1 and 3.7; step F5.2), moved without changes from
/// MainWindow.ProjectWorkspace: whole copies of the document, at most <see cref="Capacity"/> to undo; a copy is taken when a
/// recorded change differs from the last confirmed state (the baseline); a new change clears the redo copies; a restore first
/// commits the open sheet, then swaps the document. Nothing is recorded or restored while the project is read-only (an
/// archived revision on screen) or for a document that is not a project. The window keeps the document, the buttons, the
/// shortcuts and the tooltips; it asks this class for the state of the buttons.
/// </summary>
public sealed class ProjectHistory
{
    /// <summary>Copies of the document kept for undo; the oldest is dropped beyond this number.</summary>
    public const int Capacity = 30;

    // «Annulla leggero» (request of the user of 3/11/2026, after the critical analysis of the project module): with a model 500 × 100 in the
    // project, 30 whole copies took 1.1 GB and every recorded change compared the whole document (26–78 ms, up to 0.5 s). The copies of the
    // models («snapshot» of the entries of «modelli_calcolo», also inside the revisions) are now kept once, as UTF-8 JSON keyed by the hash
    // the entry already carries (ProjectModelStore: «copia» for schema 2, «revisione» for schema 1, checked whenever the copy is read), and
    // a step keeps the rest of the document with a placeholder; the comparison takes the hash instead of the copy. The copies are never
    // changed in place (ProjectModelStore.Set builds a new entry), so the same hash is the same copy.
    private sealed record Step(JsonObject Skeleton, string[] Keys);
    private const string FrozenProperty = FrozenDocument.Placeholder;
    private const string SnapshotProperty = "snapshot";
    private readonly Dictionary<string, byte[]> frozen = new(StringComparer.Ordinal);

    private readonly List<Step> undo = [], redo = [];
    private Step? baseline;
    private JsonObject? tracked;

    /// <summary>Copies available to undo.</summary>
    public int UndoCount => undo.Count;

    /// <summary>Copies available to redo.</summary>
    public int RedoCount => redo.Count;

    /// <summary>State of the undo button: never while read-only.</summary>
    public bool CanUndo(bool readOnly) => !readOnly && undo.Count > 0;

    /// <summary>State of the redo button: never while read-only.</summary>
    public bool CanRedo(bool readOnly) => !readOnly && redo.Count > 0;

    /// <summary>True when the history belongs to <paramref name="document"/> (the same object, not equal content).</summary>
    public bool Tracks(JsonObject document) => ReferenceEquals(tracked, document);

    /// <summary>Starts a new history on <paramref name="document"/>: no copies, ids completed, baseline = the document.</summary>
    public void Reset(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        undo.Clear(); redo.Clear(); frozen.Clear(); tracked = document;
        ProjectRevisions.EnsureIds(document); baseline = Freeze(document);
    }

    /// <summary>
    /// Follows a document that replaces the tracked one with the same history (an archived revision deleted): the next
    /// recorded change is compared with the baseline of the previous document.
    /// </summary>
    public void Track(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        tracked = document;
    }

    /// <summary>
    /// Records a change of <paramref name="document"/>: ignored while read-only or outside a project; a document not tracked
    /// yet starts a new history; otherwise, when the document differs from the baseline, the baseline becomes an undo copy,
    /// the redo copies are cleared and the document becomes the baseline.
    /// </summary>
    /// <returns>True when the history changed (new history or new copy): the window refreshes the buttons.</returns>
    public bool Record(JsonObject document, bool readOnly)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (readOnly || document.S("tipo") != "progetti") return false;
        if (!Tracks(document)) { Reset(document); return true; }
        if (baseline is null || Same(document, baseline.Skeleton, null, false)) return false;
        undo.Add(baseline); if (undo.Count > Capacity) undo.RemoveAt(0);
        redo.Clear(); baseline = Freeze(document); Prune();
        return true;
    }

    /// <summary>
    /// Undo (or redo): nothing while read-only; otherwise <paramref name="commit"/> runs first (the open sheet is confirmed
    /// and may record a change, which clears the redo copies), then, if a copy is available, the current document is kept
    /// on the opposite side and the copy becomes the tracked document and the baseline.
    /// </summary>
    /// <param name="redo">False to undo, true to redo.</param>
    /// <param name="readOnly">True while an archived revision is on screen.</param>
    /// <param name="commit">Confirms the open sheet into the current document.</param>
    /// <param name="current">The current document, read after <paramref name="commit"/>.</param>
    /// <returns>The document to show, or null when nothing was restored.</returns>
    public JsonObject? Restore(bool redo, bool readOnly, Action commit, Func<JsonObject> current)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(current);
        if (readOnly) return null;
        commit(); var source = redo ? this.redo : undo; var target = redo ? undo : this.redo;
        if (source.Count == 0) return null;
        target.Add(Freeze(current())); var step = source[^1]; source.RemoveAt(source.Count - 1);
        var restored = (JsonObject)Thaw(step.Skeleton)!;
        tracked = restored; baseline = step; Prune();
        return restored;
    }

    /// <summary>
    /// <paramref name="document"/> frozen for a writer on another thread (docs/sviluppo/progetto-sistema.md, steps 2c and 2e): the baseline
    /// when it is the tracked document unchanged since (the usual case, about a millisecond), otherwise a new freeze. The copies of the models
    /// are shared with the history, not copied: they are never changed in place.
    /// </summary>
    public FrozenDocument Snapshot(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool current = baseline is not null && Tracks(document) && Same(document, baseline.Skeleton, null, false);
        var step = current ? baseline! : Freeze(document);
        var models = step.Keys.ToDictionary(key => key, key => frozen[key], StringComparer.Ordinal);
        var skeleton = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(step.Skeleton);
        if (!current) Prune();
        return new FrozenDocument(skeleton, models);
    }

    /// <summary>The key of the copy of a model entry: the hash of its content the entry carries, null when it has none (then the copy stays in
    /// the step like any other node).</summary>
    private static string? CopyKey(JsonObject entry)
    {
        if (entry[SnapshotProperty] is not JsonObject) return null;
        string field = ANTHEA.ModelWorkspace.ProjectModelStore.IsArchived(entry) ? "copia" : "revisione";
        return entry[field] is JsonValue value && value.TryGetValue(out string? hash) && hash.Length > 0 ? field + ":" + hash : null;
    }

    /// <summary>A step of <paramref name="document"/>: a copy of the document whose copies of the models are placeholders with their key, each
    /// copy written once in <see cref="frozen"/>.</summary>
    private Step Freeze(JsonObject document)
    {
        var keys = new List<string>();
        var skeleton = (JsonObject)Freeze(document, null, false, keys)!;
        return new(skeleton, [.. keys.Distinct(StringComparer.Ordinal)]);
    }

    private JsonNode? Freeze(JsonNode? node, string? property, bool entry, List<string> keys)
    {
        switch (node)
        {
            case JsonObject value:
                var copy = new JsonObject();
                string? key = entry ? CopyKey(value) : null;
                foreach (var (name, child) in value)
                {
                    if (key is not null && name == SnapshotProperty)
                    {
                        if (!frozen.ContainsKey(key)) frozen[key] = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(child);
                        keys.Add(key); copy[name] = new JsonObject { [FrozenProperty] = key };
                    }
                    else copy[name] = Freeze(child, name, false, keys);
                }
                return copy;
            case JsonArray items:
                var array = new JsonArray();
                foreach (var item in items) array.Add(Freeze(item, null, property == ANTHEA.ModelWorkspace.ProjectModelStore.Property, keys));
                return array;
            default:
                return node?.DeepClone();
        }
    }

    /// <summary>True when <paramref name="live"/> is the document of the step <paramref name="step"/>: the same nodes, and for the copies of the
    /// models the same key (JsonNode.DeepEquals elsewhere: properties in any order, values as JSON).</summary>
    private static bool Same(JsonNode? live, JsonNode? step, string? property, bool entry)
    {
        if (live is JsonObject value && step is JsonObject kept)
        {
            if (value.Count != kept.Count) return false;
            string? key = entry ? CopyKey(value) : null;
            foreach (var (name, child) in value)
            {
                if (!kept.TryGetPropertyValue(name, out var other)) return false;
                if (key is not null && name == SnapshotProperty && Placeholder(other) is { } frozenKey) { if (frozenKey != key) return false; continue; }
                if (!Same(child, other, name, false)) return false;
            }
            return true;
        }
        if (live is JsonArray items && step is JsonArray keptItems)
        {
            if (items.Count != keptItems.Count) return false;
            bool entries = property == ANTHEA.ModelWorkspace.ProjectModelStore.Property;
            for (int i = 0; i < items.Count; i++) if (!Same(items[i], keptItems[i], null, entries)) return false;
            return true;
        }
        if (live is JsonObject or JsonArray || step is JsonObject or JsonArray) return false;
        return JsonNode.DeepEquals(live, step);
    }

    /// <summary>The key of a placeholder, null for any other node.</summary>
    private static string? Placeholder(JsonNode? node) =>
        node is JsonObject { Count: 1 } value && value[FrozenProperty] is JsonValue key && key.TryGetValue(out string? text) ? text : null;

    /// <summary>The document of a step: its nodes copied, the copies of the models read again from their JSON.</summary>
    private JsonNode? Thaw(JsonNode? node) => node switch
    {
        JsonObject when Placeholder(node) is { } key && frozen.TryGetValue(key, out var json) => JsonNode.Parse(json),
        JsonObject value => new JsonObject(value.Select(p => KeyValuePair.Create(p.Key, Thaw(p.Value)))),
        JsonArray items => new JsonArray([.. items.Select(Thaw)]),
        _ => node?.DeepClone()
    };

    /// <summary>The copies of the models no step uses any more leave the history.</summary>
    private void Prune()
    {
        var used = undo.Concat(redo).Append(baseline).OfType<Step>().SelectMany(s => s.Keys).ToHashSet(StringComparer.Ordinal);
        foreach (string key in frozen.Keys.Where(k => !used.Contains(k)).ToArray()) frozen.Remove(key);
    }
}
