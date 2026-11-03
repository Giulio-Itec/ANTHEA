using System.IO.Compression;
using System.Text.Json.Nodes;
using GPC.Persistence;

namespace ANTHEA.ModelWorkspace;

public sealed record ModelUnavailableResult(string Name, string? Definition, string Reason);
public sealed record ModelCopyRevision(string Revision, string Date, int ResultSets);

/// <summary>ANTHEA document adapter over the shared, runtime-portable block archive. JSON schemas, revisions,
/// inline threshold and retained-copy policy belong here; the binary format and atomic file operations do not.</summary>
public sealed class ModelResultArchive
{
    public const string Extension = ".anthea-risultati";
    public const long InlineResultBytes = 16_777_216;
    public const int RetainedCopies = 5;
    internal const CompressionLevel Compression = CompressionLevel.Fastest;
    internal const byte ResultBlock = 0, CopyBlock = 1;
    internal const int FormatVersion = 1;
    internal sealed record Block(ArchiveBlock Value)
    {
        internal long Length => Value.Length;
        internal byte[] Data => Value.EncodedBytes();
    }
    internal readonly record struct Content(byte[]? Json, string? Failure);
    readonly object gate = new();
    readonly BlockArchive archive;
    HashSet<string> savedRevisions;

    public ModelResultArchive(string? documentPath, JsonObject? document = null)
    {
        archive = new BlockArchive(documentPath is null ? null : PathFor(documentPath), document is null ? null : References(document), "ANTHEA.RISULTATI");
        savedRevisions = document is null ? new(StringComparer.Ordinal) : Revisions(document);
    }
    public static string PathFor(string documentPath) => Path.ChangeExtension(Path.GetFullPath(documentPath), Extension);
    public string? FilePath => archive.FilePath;
    internal static string Address(ReadOnlySpan<byte> json) => ArchiveBlock.Digest(json.ToArray());
    internal static Block Compress(byte kind, byte[] json) => new(ArchiveBlock.Create(kind, json));
    internal static byte[] Inflate(byte[] data, long length) => ArchiveBlock.Inflate(data, length);
    internal void Add(IEnumerable<KeyValuePair<string, Block>> blocks)
    {
        foreach (var (address, block) in blocks)
        {
            if (address != block.Value.Address) throw new InvalidDataException("Indirizzo del blocco non coerente.");
            archive.Add(block.Value);
        }
    }
    internal bool IsSaved(string revision) { lock (gate) return savedRevisions.Contains(revision); }
    internal Content[] Read(IReadOnlyList<string> addresses) => archive.Read(addresses).Select(c => new Content(c.Bytes, c.Failure)).ToArray();
    public int Save(string documentPath, JsonObject document, Action writeDocument)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (gate)
        {
            int missing = archive.Save(PathFor(documentPath), References(document), writeDocument);
            savedRevisions = Revisions(document);
            return missing;
        }
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

    /// <summary>The blocks cited by a document: copies, result sets and previous revisions of every model in the archive, revisions of the
    /// sections included.</summary>
    public static HashSet<string> References(JsonObject document)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);
        void Copy(JsonObject copy)
        {
            if (copy["copia"]?.GetValue<string>() is { } address) references.Add(address);
            foreach (var set in (copy["risultati"] as JsonArray ?? []).OfType<JsonObject>())
                if (set["blocco"]?.GetValue<string>() is { } block) references.Add(block);
        }
        foreach (var model in ModelEntries(document).Where(ProjectModelStore.IsArchived))
        {
            Copy(model);
            foreach (var previous in (model["storico"] as JsonArray ?? []).OfType<JsonObject>()) Copy(previous);
        }
        return references;
    }

    static HashSet<string> Revisions(JsonObject document) => ModelEntries(document).Where(ProjectModelStore.IsArchived)
        .Select(m => m["revisione"]?.GetValue<string>()).OfType<string>().ToHashSet(StringComparer.Ordinal);
}
