using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Testing.Capture;

/// <param name="ServiceabilityEngine">Motore SLE (tensioni e fessurazione della sezione c.a. e dei muri) di '--motore-sle'; null = predefinito
/// dell'adattatore (refactoring F2.7b, commit A4).</param>
public sealed record CaptureOptions(string Root, string Output, string Tag, string? Commit, Regex? Only, bool Trace, string Culture,
    ServiceabilityEngine? ServiceabilityEngine = null);

/// <summary>
/// 'capture &lt;out&gt;': runs the corpus through CalculationService and the engines it does not cover, writes the normalised results
/// (results/, engines/), the text of the headless reports (reports/), the archive round trips (archives/), the fallback trace
/// (fallbacks.json) and the manifest. Raw DOCX and written archives go to raw/, timings to log.txt: both are not compared.
/// An exception of a step is part of the behaviour: it is written in place of the result ("errore_cattura") and the capture goes on.
/// </summary>
public sealed partial class CaptureRunner
{
    readonly CaptureOptions options;
    readonly string output, raw;
    readonly TextNormalizer text;
    readonly StringBuilder log = new();
    readonly JsonArray corpusEntries = [];
    readonly Dictionary<string, int> written = new(StringComparer.Ordinal);
    int errors;

    public CaptureRunner(CaptureOptions options)
    {
        this.options = options;
        output = Path.GetFullPath(options.Output); raw = Path.Combine(output, "raw");
        text = new TextNormalizer([(options.Root, "<RADICE>"), (output, "<USCITA>"), (Path.GetTempPath(), "<TEMP>")]);
    }

    public int Run()
    {
        var watch = Stopwatch.StartNew();
        var started = DateTimeOffset.Now;
        Directory.CreateDirectory(raw);
        var cases = Corpus.Build(options.Root);
        foreach (var item in cases)
        {
            if (options.Only is not null && !options.Only.IsMatch(item.Key)) continue;
            Console.WriteLine("Caso " + item.Key);
            CaptureCase(item);
        }
        if (options.Only is null || options.Only.IsMatch("archives")) Archives();
        int fallbacks = WriteFallbacks();
        var manifest = Manifest.Build(options, output, corpusEntries, written, errors, fallbacks, started, watch.Elapsed);
        // Written as it is (real date, paths): the manifest is information, excluded from the value comparison.
        File.WriteAllText(Path.Combine(output, "manifest.json"), manifest.ToJsonString(new System.Text.Json.JsonSerializerOptions
            { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + Environment.NewLine, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "log.txt"), log.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Cattura completata in {watch.Elapsed.TotalSeconds:F1} s: {string.Join(", ", written.OrderBy(p => p.Key).Select(p => p.Key + " " + p.Value))}; " +
            $"errori registrati {errors}; ripieghi {fallbacks}. Uscita: {output}");
        return 0;
    }

    // ------------------------------------------------------------------ case
    void CaptureCase(CaptureCase item)
    {
        var guids = new GuidMap();
        var entry = new JsonObject { ["modulo"] = item.Module, ["caso"] = item.Id, ["origine"] = item.Origin, ["file"] = item.SourceFile };
        if (item.SourceFile is not null) entry["sha256_file"] = Corpus.Sha256(Path.Combine(options.Root, item.SourceFile));
        corpusEntries.Add(entry);
        if (item.Data is null)
        {
            entry["errore_corpus"] = item.Error;
            Write($"results/{item.Module}/{item.Id}.json", new JsonObject { ["errore_corpus"] = item.Error }, guids);
            errors++;
            return;
        }
        var input = (JsonObject)item.Data.DeepClone();
        CanonicalJson.Register(input, guids);
        entry["sha256_input"] = Sha(Write($"inputs/{item.Module}/{item.Id}.json", input, guids));
        var result = Json($"results/{item.Module}/{item.Id}.json", guids, () => CalculationService.Calculate(item.Module, Clone(item.Data), default, options.ServiceabilityEngine));
        entry["esito"] = result is null ? "errore" : result["errore"] is JsonValue e && e.ToString().Length > 0 ? "errore del calcolo" : "ok";
        Extras(item, guids, result);
    }

    static JsonObject Clone(JsonObject value) => (JsonObject)value.DeepClone();
    static string? Str(JsonNode? node, string key) => node is JsonObject o && o[key] is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.String ? v.GetValue<string>() : null;

    // ------------------------------------------------------------------ steps
    /// <summary>A JSON step: the value is returned for the following steps, its canonical form written to <paramref name="relative"/>.</summary>
    T? Json<T>(string relative, GuidMap guids, Func<T> work) where T : class
    {
        var watch = Stopwatch.StartNew(); T? value = null; JsonNode? node;
        string context = Path.ChangeExtension(relative, null)!.Replace('\\', '/');
        var saved = JsonFallbackTrace.Context.Value; JsonFallbackTrace.Context.Value = context;
        try
        {
            value = work();
            try { node = CanonicalJson.FromObject(value); }
            catch (Exception ex) { node = new JsonObject { ["errore_serializzazione"] = Error(ex) }; errors++; }
        }
        catch (Exception ex) { node = new JsonObject { ["errore_cattura"] = Error(ex) }; errors++; }
        finally { JsonFallbackTrace.Context.Value = saved; }
        RegisterDocumentsFirst(node, guids);
        Write(relative, node, guids);
        Log(relative, watch, value is null ? "errore" : "ok");
        return value;
    }

    /// <summary>A DOCX report: raw bytes in raw/, text per paragraph and cell in <paramref name="relative"/>.</summary>
    void Docx(string relative, GuidMap guids, Func<byte[]> work)
    {
        var watch = Stopwatch.StartNew(); string content; bool ok = false;
        var saved = JsonFallbackTrace.Context.Value; JsonFallbackTrace.Context.Value = Path.ChangeExtension(relative, null)!.Replace('\\', '/');
        try
        {
            var bytes = work();
            string rawPath = Path.Combine(raw, "docx", Path.ChangeExtension(relative, ".docx"));
            Directory.CreateDirectory(Path.GetDirectoryName(rawPath)!); File.WriteAllBytes(rawPath, bytes);
            content = DocxText.Extract(bytes, text, guids); ok = true;
        }
        catch (Exception ex) { content = ErrorText(ex, guids); errors++; }
        finally { JsonFallbackTrace.Context.Value = saved; }
        WriteText(relative, content);
        Log(relative, watch, ok ? "ok" : "errore");
    }

    /// <summary>A text export (CSV): normalised lines in <paramref name="relative"/>.</summary>
    void Text(string relative, GuidMap guids, Func<string> work)
    {
        var watch = Stopwatch.StartNew(); string content; bool ok = false;
        var saved = JsonFallbackTrace.Context.Value; JsonFallbackTrace.Context.Value = Path.ChangeExtension(relative, null)!.Replace('\\', '/');
        try { content = Normalizer.NormalizeLines(work(), text, guids); ok = true; }
        catch (Exception ex) { content = ErrorText(ex, guids); errors++; }
        finally { JsonFallbackTrace.Context.Value = saved; }
        WriteText(relative, content);
        Log(relative, watch, ok ? "ok" : "errore");
    }

    /// <summary>A value computed once for several steps; the exception, if any, is rethrown by every step that uses it.</summary>
    static Lazy<T> Once<T>(string context, Func<T> work) => new(() =>
    {
        var saved = JsonFallbackTrace.Context.Value; JsonFallbackTrace.Context.Value = context;
        try { return work(); } finally { JsonFallbackTrace.Context.Value = saved; }
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    static JsonObject Error(Exception ex) => new()
    {
        ["tipo"] = ex.GetType().FullName,
        ["messaggio"] = ex.Message,
        ["interna"] = ex.InnerException is { } inner ? inner.GetType().FullName + ": " + inner.Message : null
    };

    string ErrorText(Exception ex, GuidMap guids) => "ERRORE " + ex.GetType().FullName + ": " + text.Normalize(ex.Message.Replace("\r", " ").Replace('\n', ' '), guids)
        + (ex.InnerException is { } inner ? " | " + inner.GetType().FullName + ": " + text.Normalize(inner.Message.Replace("\r", " ").Replace('\n', ' '), guids) : "") + "\n";

    /// <summary>GUID numbering from the documents inside a result (input, dati) before the rest, whose insertion order may vary.</summary>
    static void RegisterDocumentsFirst(JsonNode? node, GuidMap guids)
    {
        if (node is JsonObject o)
            foreach (var key in new[] { "input", "Input", "dati", "Data" })
                if (o[key] is JsonNode document) CanonicalJson.Register(document, guids);
        CanonicalJson.Register(node, guids);
    }

    string Write(string relative, JsonNode? node, GuidMap guids)
    {
        string path = Path.Combine(output, relative);
        CanonicalJson.WriteFile(path, node, guids, text);
        Count(relative);
        return path;
    }

    /// <summary>Text output; above <see cref="GzipThreshold"/> characters it is written as <c>relative.gz</c> (gzip, read by 'compare').</summary>
    void WriteText(string relative, string content)
    {
        string path = Path.Combine(output, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = new UTF8Encoding(false).GetBytes(content);
        if (content.Length > GzipThreshold)
        {
            using var file = File.Create(path + ".gz");
            using var zip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionLevel.Optimal);
            zip.Write(bytes);
        }
        else File.WriteAllBytes(path, bytes);
        Count(relative);
    }

    public const int GzipThreshold = 2_000_000;

    void Count(string relative) { string area = relative.Split('/')[0]; written[area] = written.GetValueOrDefault(area) + 1; }

    void Log(string relative, Stopwatch watch, string outcome) =>
        log.Append(relative).Append('\t').Append(outcome).Append('\t').Append(watch.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append(" s\n");

    static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // ------------------------------------------------------------------ archives
    /// <summary>Round trip of every document of supporto/esempi: Archivio.Leggi, Archivio.Scrivi, Archivio.Leggi again.</summary>
    void Archives()
    {
        string examples = Path.Combine(options.Root, "supporto", "esempi");
        if (!Directory.Exists(examples)) return;
        foreach (var file in Directory.EnumerateFiles(examples, "*", SearchOption.AllDirectories)
            .Where(f => Corpus.DocumentExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f, StringComparer.Ordinal))
        {
            var watch = Stopwatch.StartNew(); var guids = new GuidMap();
            string name = Corpus.Id(Path.ChangeExtension(Path.GetRelativePath(examples, file), null)!) + Path.GetExtension(file).ToLowerInvariant();
            string relative = "archives/" + name + ".json";
            var saved = JsonFallbackTrace.Context.Value; JsonFallbackTrace.Context.Value = "archives/" + name;
            JsonObject node;
            try
            {
                var original = JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8));
                var document = Archivio.Leggi(file);
                string target = Path.Combine(raw, "archivi", name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                Archivio.Scrivi(target, (JsonObject)document.DeepClone());
                var stored = JsonNode.Parse(File.ReadAllText(target, Encoding.UTF8));
                var reread = Archivio.Leggi(target);
                var differences = new JsonArray(Differences(document, reread, "$").Take(50).Select(p => (JsonNode)p).ToArray());
                node = new JsonObject
                {
                    ["file"] = Corpus.Relative(options.Root, file), ["sha256_file"] = Corpus.Sha256(file),
                    ["lettura"] = document.DeepClone(), ["scritto"] = stored,
                    ["rilettura_identica"] = differences.Count == 0, ["differenze_rilettura"] = differences,
                    ["scritto_identico_all_originale"] = JsonNode.DeepEquals(original, stored)
                };
            }
            catch (Exception ex) { node = new JsonObject { ["file"] = Corpus.Relative(options.Root, file), ["errore_cattura"] = Error(ex) }; errors++; }
            finally { JsonFallbackTrace.Context.Value = saved; }
            CanonicalJson.Register(node["lettura"], guids); CanonicalJson.Register(node, guids);
            Write(relative, node, guids);
            Log(relative, watch, node.ContainsKey("errore_cattura") ? "errore" : "ok");
        }
    }

    static IEnumerable<string> Differences(JsonNode? a, JsonNode? b, string path)
    {
        if (JsonNode.DeepEquals(a, b)) yield break;
        if (a is JsonObject oa && b is JsonObject ob)
        {
            foreach (var key in oa.Select(p => p.Key).Union(ob.Select(p => p.Key)).OrderBy(k => k, StringComparer.Ordinal))
                foreach (var d in Differences(oa[key], ob[key], path + "." + key)) yield return d;
        }
        else if (a is JsonArray aa && b is JsonArray ab && aa.Count == ab.Count)
        {
            for (int i = 0; i < aa.Count; i++) foreach (var d in Differences(aa[i], ab[i], path + "[" + i + "]")) yield return d;
        }
        else yield return path;
    }

    // ------------------------------------------------------------------ fallbacks
    /// <summary>fallbacks.json: the distinct fallbacks of J.S/J.D/J.B met during the capture, sorted, and grouped by key and caller.</summary>
    int WriteFallbacks()
    {
        string path = Path.Combine(raw, "fallbacks.tsv");
        var node = new JsonObject { ["attivo"] = options.Trace, ["variabile"] = JsonFallbackTrace.Variable };
        int count = 0;
        if (options.Trace)
        {
            // GUIDs (action identifiers used as fallback names) are generated again at every migration: one placeholder for all of them.
            var guid = new Regex(@"\b[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\b");
            var lines = File.Exists(path) ? File.ReadAllLines(path, Encoding.UTF8).Where(l => l.Length > 0).Select(l => guid.Replace(l, "<GUID>"))
                .Distinct(StringComparer.Ordinal).OrderBy(l => l, StringComparer.Ordinal).ToArray() : [];
            count = lines.Length;
            var rows = lines.Select(l => l.Split('\t')).Where(p => p.Length >= 6).ToArray();
            node["voci"] = new JsonArray(rows.Select(p => (JsonNode)new JsonObject
            {
                ["contesto"] = p[0], ["tipo"] = p[1], ["chiave"] = p[2], ["ripiego"] = p[3], ["genitore"] = p[4], ["chiamante"] = p[5]
            }).ToArray());
            node["per_chiave"] = new JsonArray(rows.GroupBy(p => (Kind: p[1], Key: p[2], Fallback: p[3], Caller: p[5]))
                .OrderBy(g => g.Key.Caller, StringComparer.Ordinal).ThenBy(g => g.Key.Key, StringComparer.Ordinal).ThenBy(g => g.Key.Kind, StringComparer.Ordinal).ThenBy(g => g.Key.Fallback, StringComparer.Ordinal)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["tipo"] = g.Key.Kind, ["chiave"] = g.Key.Key, ["ripiego"] = g.Key.Fallback, ["chiamante"] = g.Key.Caller,
                    ["ambiti"] = new JsonArray(g.Select(p => Scope(p[0])).Distinct().OrderBy(m => m, StringComparer.Ordinal).Select(m => (JsonNode)m).ToArray())
                }).ToArray());
        }
        Write("fallbacks.json", node, new GuidMap());
        return count;
    }

    /// <summary>The second segment of the context: the module for results and reports, the engine for engines/.</summary>
    static string Scope(string context)
    {
        var parts = context.Split('/');
        return parts.Length >= 2 ? parts[1] : context;
    }
}
