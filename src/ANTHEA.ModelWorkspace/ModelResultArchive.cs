using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

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
/// File (format 1): a header of <see cref="HeaderBytes"/> bytes (mark, format, number of blocks, position and SHA-256 of the index), the
/// compressed blocks one after the other, the index (address, kind, position, compressed and JSON length of every block). Every save
/// writes a new file beside the old one and replaces it: the blocks already written are copied as they are, the new ones come from memory.
/// The new file holds the blocks of the document being saved and those of the document saved before at the same place, so the document on
/// disk never cites a block that is not in the archive, even when a save stops between the archive and the document; the blocks cited only
/// by the previous document go at the next save.
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

    /// <summary>Compression of the blocks: the fastest deflate (about 5 times smaller than the JSON, measures of 1/11/2026).</summary>
    internal const CompressionLevel Compression = CompressionLevel.Fastest;

    internal const byte ResultBlock = 0;
    internal const byte CopyBlock = 1;
    internal const int FormatVersion = 1;
    const string Mark = "ANTHEA.RISULTATI";
    const int MarkBytes = 16;
    const int FormatAt = 16;
    const int CountAt = 20;
    const int IndexAt = 24;
    const int DigestAt = 32;
    const int HeaderBytes = 64;
    const int HashBytes = 32;
    const int KindAt = 32;
    const int OffsetAt = 33;
    const int StoredAt = 41;
    const int LengthAt = 49;
    const int EntryBytes = 57;
    const int Attempts = 5;
    const int RetryMilliseconds = 120;

    /// <summary>A compressed block and the length of its JSON.</summary>
    internal sealed record Block(byte Kind, long Length, byte[] Data);

    readonly record struct Entry(byte Kind, long Offset, long Stored, long Length);

    /// <summary>The JSON of a block, or the reason it could not be read.</summary>
    internal readonly record struct Content(byte[]? Json, string? Failure);

    readonly object gate = new();
    // Blocks prepared in this session and not yet in the file, and blocks the file dropped (an undo may cite them again).
    readonly Dictionary<string, Block> memory = new(StringComparer.Ordinal);
    string? file;
    Dictionary<string, Entry>? index;
    byte[]? indexDigest;
    HashSet<string> savedReferences;
    HashSet<string> savedRevisions;

    /// <param name="documentPath">The file of the project, null for a project not saved yet.</param>
    /// <param name="document">The document as read from that file (its blocks are in the archive beside it), null for a new project.</param>
    public ModelResultArchive(string? documentPath, JsonObject? document = null)
    {
        file = documentPath is null ? null : PathFor(documentPath);
        savedReferences = document is null ? new(StringComparer.Ordinal) : References(document);
        savedRevisions = document is null ? new(StringComparer.Ordinal) : Revisions(document);
    }

    /// <summary>The archive of the project file <paramref name="documentPath"/>: the same folder and name, extension <see cref="Extension"/>.</summary>
    public static string PathFor(string documentPath) => Path.ChangeExtension(Path.GetFullPath(documentPath), Extension);

    /// <summary>The archive file of the project, null before the first save.</summary>
    public string? FilePath { get { lock (gate) return file; } }

    /// <summary>The address of a JSON: SHA-256, upper-case hexadecimal.</summary>
    internal static string Address(ReadOnlySpan<byte> json) => Convert.ToHexString(SHA256.HashData(json));

    internal static Block Compress(byte kind, byte[] json)
    {
        using var memory = new MemoryStream();
        using (var deflate = new DeflateStream(memory, Compression, leaveOpen: true)) deflate.Write(json);
        return new(kind, json.LongLength, memory.ToArray());
    }

    internal static byte[] Inflate(byte[] data, long length)
    {
        if (length < 0 || length > ModelSnapshotFiles.MaximumPackageBytes) throw new InvalidDataException("Lunghezza del blocco non valida.");
        var json = new byte[length];
        using var deflate = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
        deflate.ReadExactly(json);
        if (deflate.Read(new byte[1]) != 0) throw new InvalidDataException("Blocco più lungo del previsto.");
        return json;
    }

    /// <summary>Blocks of a copy prepared for the document (<see cref="ProjectModelStore.Set(JsonObject, ModelDocumentEntry, string?, ModelResultArchive?)"/>),
    /// kept in memory until the save; blocks already in the file or in memory are not kept twice.</summary>
    internal void Add(IEnumerable<KeyValuePair<string, Block>> blocks)
    {
        lock (gate)
            foreach (var (address, block) in blocks)
                if (index?.ContainsKey(address) != true) memory.TryAdd(address, block);
    }

    /// <summary>True when <paramref name="revision"/> is the revision of a copy of the document saved last (or opened).</summary>
    internal bool IsSaved(string revision) { lock (gate) return savedRevisions.Contains(revision); }

    /// <summary>The JSON of the blocks, each checked against its address: from memory, or read from the file in one pass and inflated in
    /// parallel. A block that cannot be given back has its reason (archive missing or unreadable, block absent or damaged).</summary>
    internal Content[] Read(IReadOnlyList<string> addresses)
    {
        var contents = new Content[addresses.Count];
        var stored = new (byte[] Data, long Length)?[addresses.Count];
        lock (gate)
        {
            var fromFile = new List<(int Position, Entry Entry)>();
            string? failure = null; bool loaded = false;
            for (int i = 0; i < addresses.Count; i++)
            {
                if (memory.TryGetValue(addresses[i], out var block)) { stored[i] = (block.Data, block.Length); continue; }
                if (!loaded) { failure = LoadIndex(reload: true); loaded = true; }
                if (failure is null && index!.TryGetValue(addresses[i], out var entry)) fromFile.Add((i, entry));
                else contents[i] = new(null, failure ?? "blocco assente dall'archivio dei risultati " + Path.GetFileName(file));
            }
            if (fromFile.Count > 0)
            {
                try
                {
                    using var stream = OpenRead(file!);
                    foreach (var (position, entry) in fromFile.OrderBy(f => f.Entry.Offset))
                        stored[position] = (ReadAt(stream, entry.Offset, entry.Stored), entry.Length);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    foreach (var (position, _) in fromFile) contents[position] = new(null, "archivio dei risultati non leggibile: " + error.Message);
                }
            }
        }
        Parallel.For(0, addresses.Count, i =>
        {
            if (stored[i] is not { } block || contents[i].Failure is not null) return;
            try
            {
                var json = Inflate(block.Data, block.Length);
                contents[i] = Address(json) == addresses[i] ? new(json, null) : new(null, "blocco danneggiato (impronta diversa)");
            }
            catch (Exception error) when (error is InvalidDataException or IOException or EndOfStreamException)
            { contents[i] = new(null, "blocco danneggiato (" + error.Message + ")"); }
        });
        return contents;
    }

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
        string target = PathFor(documentPath);
        var references = References(document);
        int missing = 0;
        bool written = false;
        lock (gate)
        {
            bool same = file is not null && string.Equals(file, target, StringComparison.OrdinalIgnoreCase);
            var keep = new SortedSet<string>(references, StringComparer.Ordinal);
            if (same) keep.UnionWith(savedReferences);
            if (keep.Count > 0) { missing = Write(target, keep, same); written = true; }
        }
        writeDocument();
        lock (gate)
        {
            if (!written && references.Count == 0)
            {
                // The saved document cites no block: an archive left beside it is not needed any more.
                try { if (File.Exists(target)) File.Delete(target); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
                file = target; index = null; indexDigest = null;
            }
            savedReferences = references;
            savedRevisions = Revisions(document);
        }
        return missing;
    }

    /// <summary>Writes the archive <paramref name="target"/> with the blocks <paramref name="keep"/> (from memory or from the current file)
    /// in a temporary file beside it, then replaces it. Under the gate.</summary>
    int Write(string target, SortedSet<string> keep, bool same)
    {
        string? source = file;
        if (source is not null && LoadIndex(reload: true) is { } failure)
        {
            // An archive that exists but cannot be read is never replaced while it holds blocks the documents need: they would be lost.
            // A missing one has nothing to lose.
            if (File.Exists(source) && keep.Any(a => !memory.ContainsKey(a)))
                throw new InvalidDataException("Il progetto non è stato salvato, per non perdere i risultati: " + failure + ". Se il file è aperto in un altro " +
                    "programma, chiudilo e salva di nuovo; se è danneggiato, spostalo in un'altra cartella: il progetto si salverà senza quei risultati.");
            source = null;
        }
        // «Salva» without new blocks and without blocks to drop: the archive on disk is already the one to write.
        if (same && source is not null && index!.Count == keep.Count && keep.All(index.ContainsKey)) return 0;
        // None of the blocks is available (a project opened without its archive): no empty archive is written beside it.
        if (!keep.Any(a => memory.ContainsKey(a) || source is not null && index!.ContainsKey(a)))
        {
            if (!same) { file = target; index = null; indexDigest = null; }
            return keep.Count;
        }
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var retired = new Dictionary<string, Block>(StringComparer.Ordinal);
        int missing = 0;
        byte[] digest;
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var input = source is null ? null : OpenRead(source))
            {
                output.Write(new byte[HeaderBytes]);
                foreach (string address in keep)
                {
                    byte[] data; byte kind; long length;
                    if (memory.TryGetValue(address, out var block)) { data = block.Data; kind = block.Kind; length = block.Length; }
                    else if (input is not null && index!.TryGetValue(address, out var entry)) { data = ReadAt(input, entry.Offset, entry.Stored); kind = entry.Kind; length = entry.Length; }
                    else { missing++; continue; }
                    entries[address] = new(kind, output.Position, data.LongLength, length);
                    output.Write(data);
                }
                // The blocks the new file drops stay in memory for the rest of the session: an undo may cite them again.
                if (same && input is not null)
                    foreach (var (address, entry) in index!)
                        if (!keep.Contains(address) && !memory.ContainsKey(address))
                            retired[address] = new(entry.Kind, entry.Length, ReadAt(input, entry.Offset, entry.Stored));
                long indexOffset = output.Position;
                var table = new byte[entries.Count * EntryBytes];
                int at = 0;
                foreach (var (address, entry) in entries)
                {
                    var row = table.AsSpan(at, EntryBytes);
                    Convert.FromHexString(address).CopyTo(row);
                    row[KindAt] = entry.Kind;
                    BinaryPrimitives.WriteInt64LittleEndian(row[OffsetAt..], entry.Offset);
                    BinaryPrimitives.WriteInt64LittleEndian(row[StoredAt..], entry.Stored);
                    BinaryPrimitives.WriteInt64LittleEndian(row[LengthAt..], entry.Length);
                    at += EntryBytes;
                }
                output.Write(table);
                digest = SHA256.HashData(table);
                var header = new byte[HeaderBytes];
                Encoding.ASCII.GetBytes(Mark).CopyTo(header, 0);
                BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(FormatAt), FormatVersion);
                BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(CountAt), entries.Count);
                BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(IndexAt), indexOffset);
                digest.CopyTo(header, DigestAt);
                output.Position = 0; output.Write(header);
                output.Flush(true);
            }
            for (int attempt = 1; ; attempt++)
            {
                try { File.Move(temporary, target, true); break; }
                catch (Exception error) when (attempt < Attempts && error is IOException or UnauthorizedAccessException) { Thread.Sleep(RetryMilliseconds * attempt); }
            }
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        foreach (string address in entries.Keys) memory.Remove(address);
        foreach (var (address, block) in retired) memory[address] = block;
        file = target; index = entries; indexDigest = digest;
        return missing;
    }

    /// <summary>The index of the file, read once (again with <paramref name="reload"/> when the file changed since). Null when usable,
    /// otherwise the reason. Under the gate.</summary>
    string? LoadIndex(bool reload = false)
    {
        if (file is null) return "progetto non ancora salvato: blocco non disponibile";
        if (!File.Exists(file)) { index = null; indexDigest = null; return "archivio dei risultati mancante: " + Path.GetFileName(file); }
        try
        {
            using var stream = OpenRead(file);
            var header = new byte[HeaderBytes];
            stream.ReadExactly(header);
            if (Encoding.ASCII.GetString(header, 0, MarkBytes) != Mark || BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(FormatAt)) != FormatVersion)
                return "archivio dei risultati di formato sconosciuto: " + Path.GetFileName(file);
            var digest = header.AsSpan(DigestAt, HashBytes).ToArray();
            if (index is not null && indexDigest is not null && (!reload || digest.AsSpan().SequenceEqual(indexDigest))) return null;
            int count = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(CountAt));
            long indexOffset = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(IndexAt));
            if (count < 0 || indexOffset < HeaderBytes || indexOffset + (long)count * EntryBytes != stream.Length)
                return "archivio dei risultati danneggiato (indice): " + Path.GetFileName(file);
            var table = ReadAt(stream, indexOffset, (long)count * EntryBytes);
            if (!SHA256.HashData(table).AsSpan().SequenceEqual(digest)) return "archivio dei risultati danneggiato (indice): " + Path.GetFileName(file);
            var entries = new Dictionary<string, Entry>(count, StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                var row = table.AsSpan(i * EntryBytes, EntryBytes);
                var entry = new Entry(row[KindAt], BinaryPrimitives.ReadInt64LittleEndian(row[OffsetAt..]), BinaryPrimitives.ReadInt64LittleEndian(row[StoredAt..]),
                    BinaryPrimitives.ReadInt64LittleEndian(row[LengthAt..]));
                if (entry.Offset < HeaderBytes || entry.Stored < 0 || entry.Offset + entry.Stored > indexOffset)
                    return "archivio dei risultati danneggiato (indice): " + Path.GetFileName(file);
                entries[Convert.ToHexString(row[..HashBytes])] = entry;
            }
            index = entries; indexDigest = digest;
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or EndOfStreamException)
        { return "archivio dei risultati non leggibile: " + error.Message; }
    }

    static FileStream OpenRead(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    static byte[] ReadAt(FileStream stream, long offset, long length)
    {
        if (length < 0 || length > ModelSnapshotFiles.MaximumPackageBytes) throw new InvalidDataException("Lunghezza del blocco non valida.");
        var data = new byte[length];
        stream.Position = offset; stream.ReadExactly(data);
        return data;
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
