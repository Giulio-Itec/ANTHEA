using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anthea.Projects;

/// <summary>
/// The lock of an open document (docs/sviluppo/progetto-sistema.md, step 2d, 3/11/2026): the file «~$name» beside it, as Office does (the
/// usual synchronisation clients leave these files alone), with the user, the computer and the time of the opening. The file stays open for
/// as long as the document is open and Windows deletes it when it closes, also when ANTHEA ends badly: a lock file that nobody holds open is
/// left over from a power cut or a copy, and is taken over.
/// </summary>
public sealed class DocumentLock : IDisposable
{
    /// <summary>The prefix of the lock file before the name of the document.</summary>
    public const string Prefix = "~$";

    private const int BufferBytes = 4096;
    private const int Attempts = 2;
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly FileStream stream;

    private DocumentLock(string path, FileStream stream, DocumentLockOwner owner)
    {
        Path = path;
        this.stream = stream;
        Owner = owner;
    }

    /// <summary>The lock file.</summary>
    public string Path { get; }

    /// <summary>Who holds the lock: this process.</summary>
    public DocumentLockOwner Owner { get; }

    /// <summary>The lock file of the document at <paramref name="documentPath"/>: the same folder, «~$» before its name.</summary>
    public static string PathFor(string documentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentPath);
        string full = System.IO.Path.GetFullPath(documentPath);
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(full)!, Prefix + System.IO.Path.GetFileName(full));
    }

    /// <summary>
    /// Takes the lock of the document at <paramref name="documentPath"/>: a lock left over (nobody holds its file open) is taken over; a lock
    /// held by another ANTHEA gives its owner. A folder where the lock cannot be written (read-only, missing) gives neither: the document
    /// opens without a lock, as before 3/11/2026.
    /// </summary>
    public static DocumentLockResult Acquire(string documentPath, DocumentLockOwner? owner = null)
    {
        string path = PathFor(documentPath);
        var me = owner ?? DocumentLockOwner.Current();
        // Twice at most: a lock left over is deleted, then created again.
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, BufferBytes, FileOptions.DeleteOnClose);
                try
                {
                    stream.Write(Encoding.UTF8.GetBytes(me.ToJson().ToJsonString(Indented)));
                    stream.Flush(true);
                    try { File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
                return new(new DocumentLock(path, stream, me), null);
            }
            catch (IOException) when (File.Exists(path))
            {
                var holder = Read(path);
                try
                {
                    File.Delete(path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    return new(null, holder ?? DocumentLockOwner.Unknown);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return new(null, null); }
        }
        return new(null, null);
    }

    /// <summary>The owner written in the lock file at <paramref name="path"/>, null when it cannot be read.</summary>
    public static DocumentLockOwner? Read(string path)
    {
        try
        {
            using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return DocumentLockOwner.FromJson(JsonNode.Parse(reader) as JsonObject);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>Releases the lock: Windows deletes its file.</summary>
    public void Dispose() => stream.Dispose();

    internal static string Format(DateTimeOffset time) => time.ToString("O", CultureInfo.InvariantCulture);
}

/// <summary>The outcome of <see cref="DocumentLock.Acquire"/>.</summary>
/// <param name="Lock">The lock taken; null when another ANTHEA holds it or the folder does not allow it.</param>
/// <param name="Holder">Who holds the lock instead, null when the lock was taken or cannot be written.</param>
public sealed record DocumentLockResult(DocumentLock? Lock, DocumentLockOwner? Holder);

/// <summary>Who opened a document: user, computer, time of the opening and process.</summary>
public sealed record DocumentLockOwner(string User, string Computer, DateTimeOffset Since, int Process)
{
    /// <summary>The owner of a lock file that cannot be read.</summary>
    public static DocumentLockOwner Unknown { get; } = new("", "", DateTimeOffset.MinValue, 0);

    /// <summary>This process, now.</summary>
    public static DocumentLockOwner Current() => new(Environment.UserName, Environment.MachineName, DateTimeOffset.Now, Environment.ProcessId);

    /// <summary>True for another ANTHEA of the same user on this computer (another window, or a second start).</summary>
    public bool IsSameUserHere => User.Length > 0 && string.Equals(User, Environment.UserName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Computer, Environment.MachineName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Who holds the document, for the question of the opening: «da Rossi sul computer PC-07 dalle 14:32 del 03/11/2026», «in un'altra
    /// finestra di ANTHEA su questo computer (dalle 14:32 del 03/11/2026)», «da un altro utente» when the lock file cannot be read.
    /// </summary>
    public string Description
    {
        get
        {
            if (User.Length == 0) return "da un altro utente";
            string since = Since == DateTimeOffset.MinValue ? "" : " dalle " + Since.ToLocalTime().ToString("HH:mm 'del' dd/MM/yyyy", CultureInfo.InvariantCulture);
            return IsSameUserHere ? "in un'altra finestra di ANTHEA su questo computer" + (since.Length == 0 ? "" : " (" + since.TrimStart() + ")")
                : "da " + User + " sul computer " + Computer + since;
        }
    }

    internal JsonObject ToJson() => new()
    {
        ["formato"] = "ANTHEA blocco",
        [UserKey] = User,
        [ComputerKey] = Computer,
        [SinceKey] = DocumentLock.Format(Since),
        [ProcessKey] = Process
    };

    internal static DocumentLockOwner? FromJson(JsonObject? json)
    {
        if (json?[UserKey]?.GetValue<string>() is not { Length: > 0 } user) return null;
        var since = DateTimeOffset.TryParse(json[SinceKey]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time) ? time : DateTimeOffset.MinValue;
        int process = json[ProcessKey] is JsonValue value && value.TryGetValue<int>(out int id) ? id : 0;
        return new(user, json[ComputerKey]?.GetValue<string>() ?? "", since, process);
    }

    private const string UserKey = "utente";
    private const string ComputerKey = "computer";
    private const string SinceKey = "aperto";
    private const string ProcessKey = "processo";
}

/// <summary>
/// The file of a document on disk when it was opened or saved last: its length and its time of writing. «Salva» compares it with the file
/// before writing, so that the changes another program or another ANTHEA saved in the meantime are not overwritten without a question.
/// </summary>
public readonly record struct FileStamp(long Length, DateTime WrittenUtc)
{
    /// <summary>The file at <paramref name="path"/> now, null when it does not exist or cannot be read.</summary>
    public static FileStamp? Of(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(info.Length, info.LastWriteTimeUtc) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }
}
