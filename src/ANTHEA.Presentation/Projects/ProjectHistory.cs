using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Presentation.Projects;

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

    private readonly List<JsonObject> undo = [], redo = [];
    private JsonObject? baseline;
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
        undo.Clear(); redo.Clear(); tracked = document;
        ProjectRevisions.EnsureIds(document); baseline = (JsonObject)document.DeepClone();
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
        if (baseline is null || JsonNode.DeepEquals(baseline, document)) return false;
        undo.Add(baseline); if (undo.Count > Capacity) undo.RemoveAt(0);
        redo.Clear(); baseline = (JsonObject)document.DeepClone();
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
        target.Add((JsonObject)current().DeepClone()); var restored = source[^1]; source.RemoveAt(source.Count - 1);
        tracked = restored; baseline = (JsonObject)restored.DeepClone();
        return restored;
    }
}
