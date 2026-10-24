using System.IO;
using System.Text;

namespace X.Desktop;

/// <summary>
/// Minimal log of the unhandled errors: time, source and full exception (type, message, stack) appended to
/// %LOCALAPPDATA%\ANTHEA\logs\errori.log. It only adds a trace: the messages shown to the user do not change.
/// </summary>
internal static class ErrorLog
{
    /// <summary>Beyond this size the log is renamed errori.1.log (replacing the previous one) and restarted.</summary>
    internal const long MaxBytes = 1024 * 1024;
    private static readonly object sync = new();
    private static bool attached;

    internal static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "logs", "errori.log");

    /// <summary>Logs the errors that never reach the dispatcher: unobserved tasks and exceptions of other threads.</summary>
    internal static void Attach()
    {
        if (attached) return;
        attached = true;
        TaskScheduler.UnobservedTaskException += (_, e) => Write("Task non osservato", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write(e.IsTerminating ? "Errore non gestito (chiusura dell'applicazione)" : "Errore non gestito", e.ExceptionObject as Exception);
    }

    /// <summary>Appends one entry; never throws, so that the log cannot hide or replace the original error.</summary>
    internal static void Write(string source, Exception? error, string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            var entry = new StringBuilder()
                .Append('[').Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", System.Globalization.CultureInfo.InvariantCulture)).Append("] ")
                .Append(source).Append(" · ANTHEA ").Append(typeof(ErrorLog).Assembly.GetName().Version).AppendLine()
                .AppendLine(error?.ToString() ?? "(eccezione non disponibile)")
                .AppendLine().ToString();
            lock (sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var file = new FileInfo(path);
                if (file.Exists && file.Length > MaxBytes) File.Move(path, Path.ChangeExtension(path, ".1.log"), overwrite: true);
                File.AppendAllText(path, entry, new UTF8Encoding(false));
            }
        }
        catch (Exception) { }
    }
}
