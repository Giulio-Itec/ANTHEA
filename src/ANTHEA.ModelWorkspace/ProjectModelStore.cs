using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ANTHEA.ModelWorkspace;

/// <summary>
/// Decision of the user of 1/11/2026 («Scrittura del risultato fuori dal thread dell'interfaccia»): the entry of a copy in the project
/// document, prepared where the copy is made (a calculation, a saved definition, an import), so that the thread of the interface only
/// inserts it. The copy is validated, its canonical UTF-8 JSON written once: its SHA-256 is the fingerprint of the copy (the bytes
/// <see cref="ModelSnapshot.Fingerprint"/> hashes) and its tree is the one SerializeToNode reads back from the same bytes, so the document
/// and the saved file are the same as before. The copy must not change after the preparation (the copies never change in place).
/// <para>
/// Results archive (decision of 2/11/2026, «Blocchi accanto»): when the JSON of the results of the copy reaches
/// <see cref="ModelResultArchive.InlineResultBytes"/>, the entry is prepared for the archive instead: the copy without results (in the
/// document), every result set as a compressed block addressed by the SHA-256 of its JSON, and the same fingerprint of the whole copy,
/// composed from the parts. Below the limit nothing changes.
/// </para>
/// </summary>
public sealed class ModelDocumentEntry
{
    readonly object gate = new();
    JsonNode? tree;
    readonly byte[]? json;
    readonly ArchivedCopy? archived;

    /// <summary>A copy prepared for the archive: the JSON of the copy without results and its address, the references of its result sets
    /// in their order, the compressed blocks, the moment of the preparation.</summary>
    internal sealed record ArchivedCopy(byte[] Bare, string Copy, ResultReference[] Results, Dictionary<string, ModelResultArchive.Block> Blocks, string Date);

    /// <summary>A result set of a copy in the archive: its name, the definition it was derived from (null for an imported case), the
    /// address of its block and the length of its JSON.</summary>
    internal sealed record ResultReference(string Name, string? Definition, string Block, long Bytes);

    ModelDocumentEntry(ModelSnapshot snapshot, byte[] json)
    {
        Snapshot = snapshot; this.json = json; Revision = Convert.ToHexString(SHA256.HashData(json)); tree = JsonNode.Parse(json);
    }

    ModelDocumentEntry(ModelSnapshot snapshot, string revision, ArchivedCopy archived)
    {
        Snapshot = snapshot; Revision = revision; this.archived = archived; tree = JsonNode.Parse(archived.Bare);
    }

    /// <summary>The copy.</summary>
    public ModelSnapshot Snapshot { get; }

    /// <summary>The fingerprint of the copy, the «revisione» of its entry.</summary>
    public string Revision { get; }

    /// <summary>True when the copy goes to the results archive (its results reach <see cref="ModelResultArchive.InlineResultBytes"/>).</summary>
    public bool IsArchived => archived is not null;

    internal ArchivedCopy? Archived => archived;

    /// <summary>Validates the copy and writes its entry: the work of <see cref="ProjectModelStore.Set(JsonObject, ModelSnapshot, string?)"/>
    /// before the insertion, for a thread other than the one of the interface.</summary>
    public static ModelDocumentEntry Prepare(ModelSnapshot snapshot) => Prepare(snapshot, ModelResultArchive.InlineResultBytes);

    /// <summary>As <see cref="Prepare(ModelSnapshot)"/>, with the limit of the results in the document (the checks use a small one).</summary>
    public static ModelDocumentEntry Prepare(ModelSnapshot snapshot, long inlineResultBytes)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        // 3/11/2026 (limit of the copy chosen by the user, copies of several GiB): every result set is written, addressed and compressed once,
        // also for the copies made from this one (ModelSnapshot.ResultBlocks, records never changed in place), and the JSON of the sets is never
        // all in memory: each thread holds one set. A series published every set again at every publication.
        var results = snapshot.Results; var known = snapshot.ResultBlocks;
        var prepared = new PreparedBlock[results.Length];
        Parallel.For(0, results.Length, i =>
        {
            if (known != null && known.TryGetValue(results[i], out var block)) { prepared[i] = block; return; }
            var json = JsonSerializer.SerializeToUtf8Bytes(results[i]);
            prepared[i] = new(ModelResultArchive.Address(json), ModelResultArchive.Compress(ModelResultArchive.ResultBlock, json));
        });
        var blocks = new Dictionary<ModelResultSet, PreparedBlock>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < results.Length; i++) blocks.TryAdd(results[i], prepared[i]);
        snapshot.ResultBlocks = blocks;
        // Below the limit, the entry of ANTHEA 1.17.0, byte for byte.
        if (prepared.Sum(p => p.Block.Length) < inlineResultBytes) return new(snapshot, JsonSerializer.SerializeToUtf8Bytes(snapshot));
        var bare = ModelCombinations.Copy(snapshot); bare.Results = [];
        var bareJson = JsonSerializer.SerializeToUtf8Bytes(bare);
        var archived = new Dictionary<string, ModelResultArchive.Block>(StringComparer.Ordinal);
        foreach (var block in prepared) archived.TryAdd(block.Address, block.Block);
        string copy = ModelResultArchive.Address(bareJson);
        archived.TryAdd(copy, ModelResultArchive.Compress(ModelResultArchive.CopyBlock, bareJson));
        var references = results.Select((r, i) => new ResultReference(r.Name, r.Combination?.DefinitionId, prepared[i].Address, prepared[i].Block.Length)).ToArray();
        return new(snapshot, WholeRevision(bareJson, prepared.Select(p => p.Block).ToArray()), new(bareJson, copy, references, archived, DateTime.Now.ToString("O", CultureInfo.InvariantCulture)));
    }

    /// <summary>A result set prepared for the archive: the address of its JSON and its compressed block.</summary>
    internal sealed record PreparedBlock(string Address, ModelResultArchive.Block Block);

    /// <summary>Sets inflated at once by <see cref="WholeRevision(byte[], IReadOnlyList{ModelResultArchive.Block})"/>.</summary>
    const int HashWindow = 8;

    /// <summary>As <see cref="WholeRevision(byte[], IReadOnlyList{byte[]})"/> from the compressed blocks of the sets, inflated a few at a time.</summary>
    internal static string WholeRevision(byte[] bareJson, IReadOnlyList<ModelResultArchive.Block> blocks)
    {
        int split = ResultsSplit(bareJson);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(bareJson, 0, split);
        for (int start = 0; start < blocks.Count; start += HashWindow)
        {
            int count = blocks.Count - start < HashWindow ? blocks.Count - start : HashWindow;
            var window = new byte[count][];
            Parallel.For(0, count, k => window[k] = ModelResultArchive.Inflate(blocks[start + k].Data, blocks[start + k].Length));
            for (int k = 0; k < count; k++) { if (start + k > 0) hash.AppendData(","u8); hash.AppendData(window[k]); }
        }
        hash.AppendData(bareJson, split, bareJson.Length - split);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>The fingerprint of the whole copy from the JSON of the copy without results and the JSON of its result sets: the canonical JSON
    /// of the copy is that of the copy without results with the sets, separated by commas, in its empty array «Results» (the first property
    /// with that name: the ones before it are numbers, texts and records of the geometry).</summary>
    internal static string WholeRevision(byte[] bareJson, IReadOnlyList<byte[]> sets)
    {
        int split = ResultsSplit(bareJson);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(bareJson, 0, split);
        for (int i = 0; i < sets.Count; i++) { if (i > 0) hash.AppendData(","u8); hash.AppendData(sets[i]); }
        hash.AppendData(bareJson, split, bareJson.Length - split);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>The position after «"Results":[» in the JSON of a copy without results.</summary>
    internal static int ResultsSplit(byte[] bareJson)
    {
        var marker = "\"Results\":[]"u8;
        int at = bareJson.AsSpan().IndexOf(marker);
        if (at < 0) throw new InvalidDataException("Copia senza l'elenco dei risultati.");
        return at + marker.Length - 1;
    }

    /// <summary>The tree for the document: the prepared one the first time, read again from the same bytes afterwards (a second container).
    /// For a copy prepared for the archive it is the copy without results.</summary>
    internal JsonNode TakeTree()
    {
        lock (gate)
        {
            var taken = tree ?? JsonNode.Parse(json ?? archived!.Bare)!; tree = null; return taken;
        }
    }

    /// <summary>The whole copy in the document, for a container without an archive: from its JSON, or put together again from its blocks.</summary>
    internal JsonNode InlineTree()
    {
        if (archived is null) return TakeTree();
        var sets = archived.Results.Select(r => ModelResultArchive.Inflate(archived.Blocks[r.Block].Data, archived.Blocks[r.Block].Length)).ToArray();
        int split = ResultsSplit(archived.Bare);
        using var whole = new MemoryStream();
        whole.Write(archived.Bare, 0, split);
        for (int i = 0; i < sets.Length; i++) { if (i > 0) whole.Write(","u8); whole.Write(sets[i]); }
        whole.Write(archived.Bare, split, archived.Bare.Length - split);
        return JsonNode.Parse(whole.ToArray())!;
    }
}

public static class ProjectModelStore
{
    public const string Property = "modelli_calcolo";
    public const string Binding = "riferimento_modello";

    /// <summary>Schema of the entry of a copy with its results in the document (every version up to 1.17.0).</summary>
    const int InlineSchema = 1;

    /// <summary>Schema of the entry of a copy with its results in the archive beside the document (2/11/2026).</summary>
    const int ArchivedSchema = 2;

    public static IEnumerable<JsonObject> Models(JsonObject container)
    {
        if (container[Property] is null) return [];
        if (container[Property] is not JsonArray items || items.Any(i => i is not JsonObject)) throw new InvalidDataException("Elenco dei modelli non valido.");
        return items.Cast<JsonObject>();
    }

    /// <summary>True for the entry of a copy whose results are in the archive (schema 2).</summary>
    public static bool IsArchived(JsonObject model) => Schema(model) == ArchivedSchema;

    static int? Schema(JsonObject model) => model["schema"] is JsonValue value && value.TryGetValue(out int schema) ? schema : null;

    public static ModelSnapshot? Read(JsonObject project, string id) => Read(project, id, null);

    /// <summary>The copy <paramref name="id"/> of the container. A copy in the archive takes its results from <paramref name="archive"/>:
    /// a result set the archive cannot give back (archive missing, block damaged) is left out and listed with the reason in
    /// <see cref="ModelSnapshot.UnavailableResults"/>, so the model opens all the same.</summary>
    public static ModelSnapshot? Read(JsonObject project, string id, ModelResultArchive? archive)
    {
        var model = Models(project).SingleOrDefault(m => m["id"]?.GetValue<string>() == id); if (model == null) return null;
        var schema = Schema(model);
        if (schema == ArchivedSchema) return ReadArchived(model, archive);
        if (schema != InlineSchema) throw new InvalidDataException("Versione del collegamento al modello non supportata.");
        // Combinations of the Models module (1/11/2026): the copy read from its UTF-8 JSON (the text ToJsonString wrote, without the UTF-16 copy).
        // The entry written by Set holds the canonical bytes of the copy: when they hash to the revision they are the bytes the revision was
        // computed from, and the canonical serialization of the copy read from them is not written again; any other entry is checked as before.
        var json = JsonSerializer.SerializeToUtf8Bytes(model["snapshot"] ?? throw new InvalidDataException("Modello mancante."));
        var snapshot = ModelSnapshot.FromJson(json);
        string? revision = model["revisione"]?.GetValue<string>();
        if (Convert.ToHexString(SHA256.HashData(json)) != revision && snapshot.Fingerprint() != revision) throw new InvalidDataException("Impronta del modello non corrispondente ai dati.");
        return snapshot;
    }

    static ModelSnapshot ReadArchived(JsonObject model, ModelResultArchive? archive)
    {
        var bare = JsonSerializer.SerializeToUtf8Bytes(model["snapshot"] ?? throw new InvalidDataException("Modello mancante."));
        if (ModelResultArchive.Address(bare) != model["copia"]?.GetValue<string>()) throw new InvalidDataException("Impronta del modello non corrispondente ai dati.");
        return Assemble(bare, References(model), archive, model["revisione"]?.GetValue<string>());
    }

    /// <summary>The copy without results <paramref name="bare"/> with the result sets <paramref name="references"/> from the archive; the
    /// fingerprint of the whole copy is checked when every set is there.</summary>
    static ModelSnapshot Assemble(byte[] bare, ModelDocumentEntry.ResultReference[] references, ModelResultArchive? archive, string? revision)
    {
        var snapshot = ModelSnapshot.FromJson(bare);
        var contents = archive?.Read(references.Select(r => r.Block).ToArray())
            ?? references.Select(_ => new ModelResultArchive.Content(null, "archivio dei risultati non disponibile")).ToArray();
        var sets = new ModelResultSet?[references.Length];
        var failures = new string?[references.Length];
        Parallel.For(0, references.Length, i =>
        {
            if (contents[i].Json is not { } json) { failures[i] = contents[i].Failure; return; }
            try { sets[i] = JsonSerializer.Deserialize<ModelResultSet>(json) ?? throw new JsonException("vuoto"); }
            catch (JsonException error) { failures[i] = "blocco non leggibile (" + error.Message + ")"; }
        });
        var unavailable = references.Select((r, i) => failures[i] is { } failure ? new ModelUnavailableResult(r.Name, r.Definition, failure) : null)
            .OfType<ModelUnavailableResult>().ToList();
        snapshot.Results = sets.OfType<ModelResultSet>().ToArray();
        try { snapshot.Validate(); }
        catch (InvalidDataException) when (unavailable.Count > 0)
        {
            // The sets that remain do not make a valid copy on their own: the copy opens without results.
            unavailable = references.Select(r => new ModelUnavailableResult(r.Name, r.Definition, unavailable[0].Reason)).ToList();
            snapshot.Results = [];
        }
        if (unavailable.Count == 0 && ModelDocumentEntry.WholeRevision(bare, contents.Select(c => c.Json!).ToArray()) != revision)
            throw new InvalidDataException("Impronta del modello non corrispondente ai dati.");
        snapshot.UnavailableResults = unavailable;
        return snapshot;
    }

    static ModelDocumentEntry.ResultReference[] References(JsonObject copy) => (copy["risultati"] as JsonArray ?? []).OfType<JsonObject>()
        .Select(r => new ModelDocumentEntry.ResultReference(r["nome"]?.GetValue<string>() ?? "", r["definizione"]?.GetValue<string>(),
            r["blocco"]?.GetValue<string>() ?? throw new InvalidDataException("Riferimento a un blocco dei risultati non valido."), r["byte"]?.GetValue<long>() ?? 0)).ToArray();

    /// <summary>The previous saved revisions of a copy in the archive, the most recent first (storico).</summary>
    public static IReadOnlyList<ModelCopyRevision> History(JsonObject container, string id)
    {
        var model = Models(container).SingleOrDefault(m => m["id"]?.GetValue<string>() == id);
        if (model is null || !IsArchived(model)) return [];
        return (model["storico"] as JsonArray ?? []).OfType<JsonObject>().Select(h => new ModelCopyRevision(h["revisione"]?.GetValue<string>() ?? "",
            h["data"]?.GetValue<string>() ?? "", (h["risultati"] as JsonArray)?.Count ?? 0)).ToArray();
    }

    /// <summary>A previous saved revision of the copy <paramref name="id"/>, read from the archive (copy and results).</summary>
    /// <exception cref="InvalidDataException">The revision is not in the history or its copy is not in the archive.</exception>
    public static ModelSnapshot ReadRevision(JsonObject container, string id, string revision, ModelResultArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var model = Models(container).SingleOrDefault(m => m["id"]?.GetValue<string>() == id) ?? throw new ArgumentException("Modello non trovato nel contenitore.");
        var item = (model["storico"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(h => h["revisione"]?.GetValue<string>() == revision)
            ?? throw new InvalidDataException("Copia precedente non presente nello storico del modello.");
        var copy = archive.Read([item["copia"]?.GetValue<string>() ?? ""])[0];
        if (copy.Json is null) throw new InvalidDataException("Copia precedente non disponibile: " + copy.Failure + ".");
        return Assemble(copy.Json, References(item), archive, revision);
    }

    /// <summary>The whole copy in the document, as before the results archive (containers without an archive, checks).</summary>
    public static string Set(JsonObject project, ModelSnapshot snapshot, string? id = null) => Set(project, ModelDocumentEntry.Prepare(snapshot, long.MaxValue), id);

    /// <summary>The copy prepared by <see cref="ModelDocumentEntry.Prepare(ModelSnapshot)"/> (off the thread of the interface) becomes the model
    /// <paramref name="id"/> of the container, or a new one: only the entry is inserted, its JSON is already written.</summary>
    public static string Set(JsonObject project, ModelDocumentEntry prepared, string? id = null) => Set(project, prepared, id, null);

    /// <summary>
    /// As <see cref="Set(JsonObject, ModelDocumentEntry, string?)"/> in a project with a results archive: a copy prepared for the archive
    /// enters the document without its results, whose new blocks go to <paramref name="archive"/> (in memory until the save); a copy of the
    /// document saved last that it replaces enters its history, which keeps <see cref="ModelResultArchive.RetainedCopies"/> copies with the
    /// current one. Without an archive the whole copy is in the document, as before.
    /// </summary>
    public static string Set(JsonObject project, ModelDocumentEntry prepared, string? id, ModelResultArchive? archive)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        _ = Models(project); // Reject malformed metadata instead of losing an import silently.
        var models = project[Property] as JsonArray ?? new JsonArray();
        var old = id == null ? null : (Models(project).SingleOrDefault(m => m["id"]?.GetValue<string>() == id) ?? throw new ArgumentException("Modello non trovato nel contenitore."));
        id ??= Guid.NewGuid().ToString("N");
        string name = old?["nome"]?.GetValue<string>() ?? prepared.Snapshot.Name;
        JsonObject entry;
        if (prepared.Archived is { } archived && archive is not null)
        {
            archive.Add(archived.Blocks);
            entry = new JsonObject { ["schema"] = ArchivedSchema, ["id"] = id, ["revisione"] = prepared.Revision, ["nome"] = name, ["data"] = archived.Date,
                ["copia"] = archived.Copy, ["snapshot"] = prepared.TakeTree(), ["risultati"] = new JsonArray(archived.Results.Select(Reference).ToArray()) };
            var history = History(old, prepared.Revision, archive);
            if (history.Count > 0) entry["storico"] = history;
        }
        else entry = new JsonObject { ["schema"] = InlineSchema, ["id"] = id, ["revisione"] = prepared.Revision, ["nome"] = name, ["snapshot"] = prepared.InlineTree() };
        if (old != null) models[models.IndexOf(old)] = entry; else models.Add(entry);
        if (project[Property] == null) project[Property] = models;
        return id;
    }

    static JsonNode Reference(ModelDocumentEntry.ResultReference reference)
    {
        var node = new JsonObject { ["nome"] = reference.Name };
        if (reference.Definition is { } definition) node["definizione"] = definition;
        node["blocco"] = reference.Block; node["byte"] = reference.Bytes;
        return node;
    }

    /// <summary>The history of the entry that replaces <paramref name="old"/>: the old copy first when it is the one of the document saved
    /// last (a copy only calculated in memory is not kept), then its own history, without the new revision, up to the limit.</summary>
    static JsonArray History(JsonObject? old, string revision, ModelResultArchive archive)
    {
        var items = new List<JsonNode>();
        if (old is not null && IsArchived(old))
        {
            string? previous = old["revisione"]?.GetValue<string>();
            if (previous is not null && previous != revision && archive.IsSaved(previous))
                items.Add(new JsonObject { ["revisione"] = previous, ["data"] = old["data"]?.DeepClone(), ["copia"] = old["copia"]?.DeepClone(), ["risultati"] = old["risultati"]?.DeepClone() });
            foreach (var item in (old["storico"] as JsonArray ?? []).OfType<JsonObject>())
                if (item["revisione"]?.GetValue<string>() != revision) items.Add(item.DeepClone());
        }
        return new JsonArray(items.Take(ModelResultArchive.RetainedCopies - 1).ToArray());
    }
    public static void Rename(JsonObject container, string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Inserire un nome per il modello.");
        var model = Models(container).Single(m => m["id"]!.GetValue<string>() == id); model["nome"] = name.Trim();
    }
    public static void Link(JsonObject project, JsonObject sheet, string modelId, int element, string result, string component) =>
        Link(project, sheet, modelId, element, result, component, null);

    /// <summary>As <see cref="Link(JsonObject, JsonObject, string, int, string, string)"/>, for a copy whose results may be in <paramref name="archive"/>.</summary>
    public static void Link(JsonObject project, JsonObject sheet, string modelId, int element, string result, string component, ModelResultArchive? archive)
    {
        var snapshot = Read(project, modelId, archive) ?? throw new InvalidOperationException("Importare prima il modello.");
        if (!DescendantSheets(project).Any(s => ReferenceEquals(s, sheet))) throw new InvalidOperationException("Il foglio appartiene a un altro progetto.");
        if (!snapshot.Elements.Any(e => e.Id == element) || !snapshot.Results.Any(r => r.Name == result && r.Values.Any(v => v.Element == element)) || !ModelSnapshot.Components.Contains(component)) throw new ArgumentException("Riferimento al risultato non valido o elemento senza risultati.");
        var model = Models(project).Single(m => m["id"]!.GetValue<string>() == modelId);
        sheet[Binding] = new JsonObject { ["modello_id"] = modelId, ["revisione"] = model["revisione"]!.DeepClone(), ["elemento"] = element, ["caso"] = result, ["componente"] = component };
    }
    public static IEnumerable<JsonObject> DescendantSheets(JsonObject project)
    {
        foreach (var s in (project["fogli"] as JsonArray ?? []).OfType<JsonObject>()) yield return s;
        foreach (var child in (project["strutture"] as JsonArray ?? []).OfType<JsonObject>()) foreach (var s in DescendantSheets(child)) yield return s;
    }
    public static string LinkStatus(JsonObject project, JsonObject sheet)
    {
        if (sheet[Binding] is not JsonObject link) return "Non collegato";
        var model = Models(project).SingleOrDefault(m => JsonNode.DeepEquals(link["modello_id"], m["id"]));
        return model != null && JsonNode.DeepEquals(link["revisione"], model["revisione"]) ? "Riferimento aggiornato" : "Riferimento da aggiornare";
    }
    public static IEnumerable<JsonObject> Containers(JsonObject container)
    {
        yield return container;
        foreach (var child in (container["strutture"] as JsonArray ?? []).OfType<JsonObject>()) foreach (var node in Containers(child)) yield return node;
    }
    public static JsonObject? FindOwner(JsonObject sheet)
    {
        if (sheet[Binding] is not JsonObject link) return null;
        for (var owner = sheet.Parent?.Parent as JsonObject; owner != null; owner = owner.Parent?.Parent as JsonObject)
            if (Models(owner).Any(m => JsonNode.DeepEquals(m["id"], link["modello_id"]))) return owner;
        return null;
    }
    /// <summary>Give copied models new identities; keep internal links and leave external references explicit.</summary>
    public static void ReidentifyCopy(JsonObject copy)
    {
        var map = new Dictionary<string, string>();
        foreach (var owner in Containers(copy)) foreach (var model in Models(owner))
            {
                string before = model["id"]!.GetValue<string>(), after = Guid.NewGuid().ToString("N"); map.Add(before, after); model["id"] = after;
            }
        foreach (var sheet in DescendantSheets(copy)) if (sheet[Binding] is JsonObject link && map.TryGetValue(link["modello_id"]!.GetValue<string>(), out var id)) link["modello_id"] = id;
    }
}
