using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using ANTHEA.ModelWorkspace;

namespace Anthea.Projects;

/// <summary>
/// The recovery copies of the open documents (docs/sviluppo/progetto-sistema.md, step 2e, decision D2 of 3/11/2026): a folder for each
/// document with changes not saved, in %LOCALAPPDATA%\ANTHEA\Recupero, with the document, the result sets of its models not saved yet and
/// what is needed to take it up again. A folder in use is locked (<see cref="DocumentLock"/> on its document): after a crash Windows releases
/// the lock and the folder is left over, offered at the next start.
/// </summary>
/// <param name="root">The folder of the copies; created when the first copy is written.</param>
public sealed class RecoveryStore(string root)
{
    private const string DocumentName = "documento.programma";
    private const string InfoName = "recupero.json";
    private const string ResultsPrefix = "risultati-";

    /// <summary>The copies of the user: %LOCALAPPDATA%\ANTHEA\Recupero.</summary>
    public static RecoveryStore ForUser() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "Recupero"));

    /// <summary>The folder of the copies.</summary>
    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>A new copy for a document with changes not saved, locked until it is disposed or deleted. Null when the folder cannot be
    /// written (the document goes on without a copy).</summary>
    public RecoverySlot? Create()
    {
        try
        {
            string folder = Path.Combine(Root, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            var held = DocumentLock.Acquire(Path.Combine(folder, DocumentName));
            if (held.Lock is null) return null;
            return new RecoverySlot(folder, held.Lock);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// The copies left over by an ANTHEA that ended without closing them (a crash, a power cut): each locked by this process until it is
    /// taken up, deleted or released. A folder without a document (the crash came before the first write) is deleted.
    /// </summary>
    public IReadOnlyList<RecoveryEntry> LeftOver()
    {
        var entries = new List<RecoveryEntry>();
        if (!Directory.Exists(Root)) return entries;
        foreach (string folder in Directory.EnumerateDirectories(Root).Order(StringComparer.Ordinal))
        {
            var held = DocumentLock.Acquire(Path.Combine(folder, DocumentName));
            if (held.Lock is null) continue;
            var info = RecoveryInfo.Read(Path.Combine(folder, InfoName));
            if (info is null || !File.Exists(Path.Combine(folder, DocumentName)))
            {
                held.Lock.Dispose();
                Delete(folder);
                continue;
            }
            entries.Add(new RecoveryEntry(folder, info, held.Lock));
        }
        return entries;
    }

    internal static void Delete(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    internal static string DocumentPath(string folder) => Path.Combine(folder, DocumentName);
    internal static string InfoPath(string folder) => Path.Combine(folder, InfoName);
    internal static string ResultsPath(string folder, int number) => Path.Combine(folder, ResultsPrefix + number.ToString(CultureInfo.InvariantCulture) + ModelResultArchive.Extension);
    internal static IEnumerable<string> ResultsPaths(string folder) => Directory.EnumerateFiles(folder, ResultsPrefix + "*" + ModelResultArchive.Extension).Order(StringComparer.Ordinal);
}
