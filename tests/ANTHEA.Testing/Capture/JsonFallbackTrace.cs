// Spostato da src/ANTHEA.Serialization/JsonData.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: la traccia dei ripieghi serve
// solo alle catture. Nel prodotto resta JsonFallbackHook, punto d'osservazione senza logica; la variabile ANTHEA_TRACE_FALLBACKS non esiste più.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;

namespace Anthea.Testing.Capture;

/// <summary>
/// Trace of the fallback values returned by <see cref="J.S"/>, <see cref="J.D"/> and <see cref="J.B"/> when a key is missing or not valid,
/// for the refactoring baselines. It is active only after <see cref="Start"/> (capture without --no-trace, before any calculation): then
/// every distinct (context, kind, key, fallback, parent, caller) is appended to the file as a tab-separated line. The returned values never
/// change; without the trace nothing is recorded.
/// </summary>
public static class JsonFallbackTrace
{
    /// <summary>
    /// Nome della variabile d'ambiente che fino alla decisione D3 attivava la traccia nel prodotto. Il prodotto non la legge più: resta
    /// soltanto come etichetta di fallbacks.json ("variabile"), che il confronto delle catture vuole identico.
    /// </summary>
    public const string Variable = "ANTHEA_TRACE_FALLBACKS";
    /// <summary>Free label of the running calculation (module, case, step), set by the capture tools.</summary>
    public static readonly AsyncLocal<string?> Context = new();
    static string? path;

    /// <summary>Attiva la traccia verso <paramref name="file"/>: registra l'osservatore nel punto d'osservazione del prodotto, per il contesto
    /// corrente (AsyncLocal, decisione E5): lo ereditano i calcoli avviati dopo, non quelli di altri contesti.</summary>
    public static void Start(string file)
    {
        path = Path.GetFullPath(file);
        JsonFallbackHook.Observer = Record;
    }

    // Called only with an active trace: the product boxes the fallback only when an observer is registered.
    static void Record(string kind, JsonNode? parent, string key, object fallback)
    {
        if (path is null) return;
        Recorder.Add(path, kind, parent, key, fallback);
    }

    /// <summary>State of an active trace, created only when the trace starts.</summary>
    static class Recorder
    {
        static readonly ConcurrentDictionary<string, byte> seen = new(StringComparer.Ordinal);
        static readonly object gate = new();
        static readonly Regex Generated = new(@"(?<=__)\d+(?:_\d+)?|(?<=DisplayClass)\d+_\d+|(?<=\|)\d+_\d+");

        static Recorder() { }

        internal static void Add(string path, string kind, JsonNode? parent, string key, object fallback)
        {
            string line = string.Join('\t', Clean(Context.Value ?? ""), kind, Clean(key), Clean(Convert.ToString(fallback, CultureInfo.InvariantCulture) ?? ""),
                parent is null ? "assente" : parent is JsonObject ? "oggetto" : "altro", Caller());
            if (!seen.TryAdd(line, 0)) return;
            lock (gate) File.AppendAllText(path, line + "\n", new UTF8Encoding(false));
        }

        static string Clean(string text) => text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

        /// <summary>The first method outside J, the hook of the product and the trace, with the numbers of the compiler-generated names removed.</summary>
        static string Caller()
        {
            foreach (var frame in new StackTrace(false).GetFrames())
            {
                var method = frame.GetMethod(); var type = method?.DeclaringType;
                if (method is null || type is null || type == typeof(J) || type == typeof(JsonFallbackHook) || type == typeof(JsonFallbackTrace) || type == typeof(Recorder)) continue;
                while (type.DeclaringType is not null && type.Name.StartsWith('<')) type = type.DeclaringType;
                return Generated.Replace(type.FullName + "." + method.Name, "");
            }
            return "";
        }
    }
}
