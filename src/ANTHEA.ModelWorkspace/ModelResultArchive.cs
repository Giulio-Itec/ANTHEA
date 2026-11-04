using System.Text.Json.Nodes;
using GPC.Persistence;

namespace ANTHEA.ModelWorkspace;

/// <summary>A result set of a copy that the archive could not give back, with the reason (shown by the view and the catalogue).</summary>
/// <param name="Name">The name of the result set.</param>
/// <param name="Definition">The combination it was derived from, null for an imported case.</param>
/// <param name="Reason">Why it is not available.</param>
public sealed record ModelUnavailableResult(string Name, string? Definition, string Reason);

/// <summary>A previous saved revision of a copy kept by the archive (storico): its revision, the moment it was prepared, its result sets.</summary>
public sealed record ModelCopyRevision(string Revision, string Date, int ResultSets);

/// <summary>
/// Results archive of a project (decision of the user of 2/11/2026, «Blocchi accanto»): the result sets of the large copies, out of the
/// project document, in one file beside it. A block is the canonical UTF-8 JSON of a result set (or of a copy without results), compressed
/// with deflate and addressed by the SHA-256 of the JSON, checked whenever it is read; the same block is stored once. The document keeps
/// geometry, tables and definitions of every copy and, for the results, only their names and addresses (<see cref="ModelDocumentEntry"/>).
/// <para>
/// The block store is GPC.Archive (<see cref="BlockArchive"/>, 3/11/2026, review D1: «per il salvataggio usa GPC.Archive», one
/// implementation for ANTHEA, BridGHe and Arachne), format 1 of ANTHEA 1.17.x/1.18.0 byte for byte: mark «ANTHEA.RISULTATI», the compressed
/// blocks, the index. Every save writes a new file beside the old one and replaces it: the blocks already written are copied as they are,
/// the new ones come from memory. The new file holds the blocks of the document being saved and those of the document saved before at the
/// same place, so the document on disk never cites a block that is not in the archive, even when a save stops between the archive and the
/// document; the blocks cited only by the previous document go at the next save. This class keeps what is ANTHEA's: the document schema of
/// the references, the revisions, the thresholds and the retained copies.
/// </para>
/// <para>
/// Failures are per result set: a reference or a block that cannot be read is one unavailable result, never a project that does not open or
/// save. A damaged archive is renamed aside («.danneggiato-…») instead of stopping the save; one in use by another program still stops it.
/// </para>
/// One archive per open project (ProjectSession); thread-safe.
/// </summary>
public sealed class ModelResultArchive
{
    /// <summary>Extension of the archive, beside the project file with the same name (provisional choice 1 of 2/11/2026).</summary>
    public const string Extension = ".anthea-risultati";

    /// <summary>JSON bytes of the results of a copy from which they go to the archive; below, the copy stays in the document exactly as in
    /// ANTHEA 1.17.0 (provisional choice 2 of 2/11/2026: 16 MiB per model).</summary>
    public const long InlineResultBytes = 16_777_216;

    /// <summary>
    /// JSON bytes a copy may take with its combinations when its results go to the archive (request of the user of 3/11/2026, «Sì, opzione 1»,
    /// after «Calcola tutte» stopped at 7 combinations of a MIDAS model of 3447 plates and 49 cases). The limit of
    /// <see cref="ModelSnapshotFiles.MaximumPackageBytes"/> holds for each block, that is for each result set and for the copy without results;
    /// the whole copy stays in memory while the model is open, about half of this JSON. Provisional: the 73 combinations of that model take
    /// about 1.6 GiB, 165 MiB of them the static cases.
    /// </summary>
    public const long ArchivedCopyBytes = 2_147_483_648;

    /// <summary>Bytes of memory of the computer for each byte of the limit of the copy, for the limits it may choose. Measured on a model of 3456
    /// plates and 49 cases: a series of 581 combinations up to the limit of 4 GiB took 7.7 GB of managed memory and a peak of 18 GB of the
    /// process, about 4.4 times the limit; 6 leaves the rest of the computer room. On 64 GB: 2, 4 and 8 GiB.</summary>
    const long MemoryPerLimitByte = 6;

    /// <summary>
    /// 3/11/2026 (request of the user, «riesci a mettere un pannello di gestione limite?», after 1.17.6 stopped at 2 GiB): the limits of the copy
    /// with its combinations the user may choose on a computer with <paramref name="memory"/> bytes, <see cref="ArchivedCopyBytes"/> always,
    /// the larger ones when the memory is at least <see cref="MemoryPerLimitByte"/> times as much.
    /// </summary>
    public static long[] ArchivedCopyLimits(long memory) =>
        [..new long[] { ArchivedCopyBytes, 4_294_967_296, 8_589_934_592, 17_179_869_184 }.Where(limit => limit == ArchivedCopyBytes || limit * MemoryPerLimitByte <= memory)];

    /// <summary>Copies kept for every model: the current one and the previous saved revisions (provisional choice 3 of 2/11/2026).</summary>
    public const int RetainedCopies = 5;

    internal const byte ResultBlock = 0;
    internal const byte CopyBlock = 1;

    /// <summary>Mark of the file (format 1).</summary>
    internal const string Mark = "ANTHEA.RISULTATI";

    /// <summary>The JSON of a block, or the reason it could not be read.</summary>
    internal readonly record struct Content(byte[]? Json, string? Failure);

    readonly object gate = new();
    readonly BlockArchive blocks;
    HashSet<string> savedRevisions;

    /// <param name="documentPath">The file of the project, null for a project not saved yet.</param>
    /// <param name="document">The document as read from that file (its blocks are in the archive beside it), null for a new project; without
    /// it the first save keeps every block already beside the file.</param>
    public ModelResultArchive(string? documentPath, JsonObject? document = null)
    {
        blocks = new BlockArchive(documentPath is null ? null : PathFor(documentPath), document is null ? null : References(document), Mark);
        savedRevisions = document is null ? new(StringComparer.Ordinal) : Revisions(document);
    }

    /// <summary>The archive of the project file <paramref name="documentPath"/>: the same folder and name, extension <see cref="Extension"/>.</summary>
    public static string PathFor(string documentPath) => Path.ChangeExtension(Path.GetFullPath(documentPath), Extension);

    /// <summary>The archive file of the project, null before the first save.</summary>
    public string? FilePath => blocks.FilePath;

    /// <summary>What the last save did: unavailable result sets, a damaged archive set aside.</summary>
    public ArchiveSaveReport? LastSave => blocks.LastSave;

    /// <summary>
    /// What the user must know after the last save (review L4, 4/11/2026: 1.18.0 with GPC.Archive never read <see cref="LastSave"/>): a damaged
    /// archive kept aside under another name, blocks left out because their bytes were damaged, blocks cited by the project that the archive does
    /// not have. Null when there is nothing to say. Shown as a warning by «Salva» and «Salva con nome…» (WorkspaceSession.SaveWarning).
    /// </summary>
    public string? SaveNotice() => Notice(LastSave);

    /// <summary>The text of <see cref="SaveNotice"/> for a report of GPC.Archive; null when it has nothing to report.</summary>
    public static string? Notice(ArchiveSaveReport? report)
    {
        if (report is null) return null;
        int damaged = report.Damaged.Count, missing = Math.Max(0, report.Unavailable - damaged);
        var lines = new List<string>();
        if (report.SetAside is { } aside)
            lines.Add($"L'archivio dei risultati accanto al progetto non era leggibile: non è stato sovrascritto ma conservato come «{Path.GetFileName(aside)}», e il progetto ha ora un archivio nuovo.");
        if (damaged > 0)
            lines.Add(damaged == 1 ? "Un blocco dell'archivio dei risultati era danneggiato e non è stato ricopiato."
                : $"{damaged} blocchi dell'archivio dei risultati erano danneggiati e non sono stati ricopiati.");
        if (missing > 0)
            lines.Add(missing == 1 ? "Un blocco citato dal progetto non è nell'archivio dei risultati."
                : $"{missing} blocchi citati dal progetto non sono nell'archivio dei risultati.");
        if (lines.Count == 0) return null;
        lines.Add(report.Unavailable > 0
            ? "Il progetto è stato salvato. I risultati che mancano sono indicati come non disponibili nei modelli: per riaverli, calcolali di nuovo."
            : "Il progetto è stato salvato.");
        return string.Join("\n", lines);
    }

    /// <summary>The address of a JSON: SHA-256, upper-case hexadecimal (the address of GPC.Archive).</summary>
    internal static string Address(byte[] json) => ArchiveBlock.Digest(json);

    /// <summary>A block of the JSON: hashed once and compressed with the fastest deflate (about 5 times smaller, measures of 1/11/2026).</summary>
    internal static ArchiveBlock Compress(byte kind, byte[] json) => ArchiveBlock.Create(kind, json);

    /// <summary>Blocks of a copy prepared for the document (<see cref="ProjectModelStore.Set(JsonObject, ModelDocumentEntry, string?, ModelResultArchive?)"/>),
    /// kept in memory until the save; a block already in memory is not kept twice.</summary>
    internal void Add(IEnumerable<KeyValuePair<string, ArchiveBlock>> prepared)
    {
        foreach (var (address, block) in prepared)
        {
            if (address != block.Address) throw new InvalidDataException("Indirizzo del blocco non coerente.");
            blocks.Add(block);
        }
    }

    /// <summary>True when <paramref name="revision"/> is the revision of a copy of the document saved last (or opened).</summary>
    internal bool IsSaved(string revision) { lock (gate) return savedRevisions.Contains(revision); }

    /// <summary>The JSON of the blocks, each checked against its address: from memory, or read from the file in one pass and inflated in
    /// parallel. A block that cannot be given back has its reason (archive missing or unreadable, block absent or damaged, invalid reference).</summary>
    internal Content[] Read(IReadOnlyList<string> addresses) => blocks.Read(addresses).Select(c => new Content(c.Bytes, c.Failure)).ToArray();

    /// <summary>
    /// «Salva» and «Salva con nome…» of a project: the archive beside <paramref name="documentPath"/> (when the document or the one saved
    /// there before cites blocks), then the document through <paramref name="writeDocument"/>, then the state «saved». The thread of the
    /// caller; the blocks are already compressed.
    /// </summary>
    /// <returns>The blocks cited by the document that the archive does not have (an archive missing at the opening), 0 normally.</returns>
    public int Save(string documentPath, JsonObject document, Action writeDocument)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writeDocument);
        var references = References(document);
        var revisions = Revisions(document);
        int missing = blocks.Save(PathFor(documentPath), references, writeDocument);
        lock (gate) savedRevisions = revisions;
        return missing;
    }

    /// <summary>The containers of models of a document: the projects, their sections and the sections of the revisions (contexts).</summary>
    internal static IEnumerable<JsonObject> ModelEntries(JsonNode? node)
    {
        if (node is JsonArray array) { foreach (var item in array) foreach (var entry in ModelEntries(item)) yield return entry; yield break; }
        if (node is not JsonObject container) yield break;
        if (container[ProjectModelStore.Property] is JsonArray models) foreach (var model in models.OfType<JsonObject>()) yield return model;
        var below = ModelEntries(container["progetti"]).Concat(ModelEntries(container["strutture"]))
            .Concat(ModelEntries(container["revisioni"])).Concat(ModelEntries(container["contesto"]));
        foreach (var entry in below) yield return entry;
    }

    /// <summary>The text of a property, null when it is absent or not a text (a damaged reference is one unavailable block, not a failure).</summary>
    internal static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>The blocks cited by a document: copies, result sets and previous revisions of every model in the archive, revisions of the
    /// sections included.</summary>
    public static HashSet<string> References(JsonObject document)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);
        void Copy(JsonObject copy)
        {
            if (Text(copy["copia"]) is { } address) references.Add(address);
            foreach (var set in (copy["risultati"] as JsonArray ?? []).OfType<JsonObject>())
                if (Text(set["blocco"]) is { } block) references.Add(block);
        }
        foreach (var model in ModelEntries(document).Where(ProjectModelStore.IsArchived))
        {
            Copy(model);
            foreach (var previous in (model["storico"] as JsonArray ?? []).OfType<JsonObject>()) Copy(previous);
        }
        return references;
    }

    static HashSet<string> Revisions(JsonObject document) => ModelEntries(document).Where(ProjectModelStore.IsArchived)
        .Select(m => Text(m["revisione"])).OfType<string>().ToHashSet(StringComparer.Ordinal);
}
