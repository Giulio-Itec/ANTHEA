using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using ANTHEA.ModelWorkspace;

namespace Anthea.Projects;

/// <summary>
/// The recovery copy of one open document (step 2e): written again after its changes, deleted when the document is saved or closed. The
/// folder stays locked while it is in use. Thread-safe: a write runs on a thread of the pool, one at a time.
/// </summary>
public sealed class RecoverySlot : IDisposable
{
    private readonly object gate = new();
    private readonly DocumentLock held;
    private readonly HashSet<string> written = new(StringComparer.Ordinal);
    private int archives;
    private bool deleted;

    internal RecoverySlot(string folder, DocumentLock held)
    {
        Folder = folder;
        this.held = held;
    }

    /// <summary>The folder of the copy.</summary>
    public string Folder { get; }

    /// <summary>Copies written.</summary>
    public int Writes { get; private set; }

    /// <summary>
    /// Writes the copy of <paramref name="document"/>: the result sets of the models it cites that are only in memory (not saved, not written
    /// by an earlier copy) in a new archive of the folder, then the document, then <paramref name="info"/>, so that a folder with its
    /// information always has the document and the results it cites. Nothing after <see cref="Delete"/>.
    /// </summary>
    public void Write(FrozenDocument document, RecoveryInfo info, ModelResultArchive? results)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(info);
        lock (gate)
        {
            if (deleted) return;
            var thawed = document.Thaw();
            if (results is not null)
            {
                var added = results.WriteUnsaved(RecoveryStore.ResultsPath(Folder, archives + 1), ModelResultArchive.References(thawed), written);
                if (added.Count > 0) { archives++; written.UnionWith(added); }
            }
            DocumentService.Save(RecoveryStore.DocumentPath(Folder), thawed);
            info.Write(RecoveryStore.InfoPath(Folder));
            Writes++;
        }
    }

    /// <summary>The document was saved, closed or discarded: the copy is deleted (after the write in progress, if any).</summary>
    public void Delete()
    {
        lock (gate)
        {
            if (deleted) return;
            deleted = true;
            held.Dispose();
            RecoveryStore.Delete(Folder);
        }
    }

    /// <summary>As <see cref="Delete"/>: a copy is never left behind on purpose.</summary>
    public void Dispose() => Delete();

    /// <summary>For the checks: what a crash does, the lock released and the copy left on disk.</summary>
    internal void Abandon()
    {
        lock (gate)
        {
            deleted = true;
            held.Dispose();
        }
    }
}

/// <summary>A recovery copy left over by an ANTHEA that ended badly, locked by this process until it is taken up, deleted or released.</summary>
public sealed class RecoveryEntry : IDisposable
{
    private readonly DocumentLock held;

    internal RecoveryEntry(string folder, RecoveryInfo info, DocumentLock held)
    {
        Folder = folder;
        Info = info;
        this.held = held;
    }

    /// <summary>The folder of the copy.</summary>
    public string Folder { get; }

    /// <summary>What the copy is: file, title, time of the copy, kind.</summary>
    public RecoveryInfo Info { get; }

    /// <summary>The document of the copy, read and checked as a file (DocumentService.Open).</summary>
    public JsonObject ReadDocument() => DocumentService.Open(RecoveryStore.DocumentPath(Folder));

    /// <summary>The archives of the result sets the copy wrote, in order.</summary>
    public IReadOnlyList<string> ResultArchives => [.. RecoveryStore.ResultsPaths(Folder)];

    /// <summary>Taken up or discarded: the copy is deleted.</summary>
    public void Delete()
    {
        held.Dispose();
        RecoveryStore.Delete(Folder);
    }

    /// <summary>«Più tardi»: the copy stays, offered again at the next start.</summary>
    public void Dispose() => held.Dispose();
}

/// <summary>What a recovery copy is (the file «recupero.json» of its folder).</summary>
/// <param name="File">The file of the document, null for a document never saved.</param>
/// <param name="Title">The name shown to the user: the name of the file, or «Nuovo progetto» / «Nuovo documento».</param>
/// <param name="Written">When the copy was written.</param>
/// <param name="Stamp">The file on disk when the document was opened or saved last, null when unknown: «Salva» asks before overwriting a
/// file changed after it.</param>
/// <param name="Project">True for a project, false for a single calculation file.</param>
public sealed record RecoveryInfo(string? File, string Title, DateTimeOffset Written, FileStamp? Stamp, bool Project)
{
    private const string Format = "ANTHEA recupero";
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    internal void Write(string path)
    {
        var json = new JsonObject
        {
            ["formato"] = Format,
            ["file"] = File,
            ["titolo"] = Title,
            ["scritto"] = Written.ToString("O", CultureInfo.InvariantCulture),
            ["progetto"] = Project
        };
        if (Stamp is { } stamp)
        {
            json["lunghezza_file"] = stamp.Length;
            json["data_file"] = stamp.WrittenUtc.ToString("O", CultureInfo.InvariantCulture);
        }
        string temporary = path + ".tmp";
        System.IO.File.WriteAllText(temporary, json.ToJsonString(Indented), new UTF8Encoding(false));
        System.IO.File.Move(temporary, path, true);
    }

    internal static RecoveryInfo? Read(string path)
    {
        try
        {
            if (JsonNode.Parse(System.IO.File.ReadAllText(path)) is not JsonObject json || json["formato"]?.GetValue<string>() != Format) return null;
            FileStamp? stamp = json["lunghezza_file"] is JsonValue length && json["data_file"]?.GetValue<string>() is { } date
                && DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
                ? new FileStamp(length.GetValue<long>(), time) : null;
            if (json["scritto"]?.GetValue<string>() is not { } written
                || !DateTimeOffset.TryParse(written, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)) return null;
            return new(json["file"]?.GetValue<string>(), json["titolo"]?.GetValue<string>() ?? "", when, stamp, json["progetto"]?.GetValue<bool>() ?? false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException)
        { return null; }
    }
}
