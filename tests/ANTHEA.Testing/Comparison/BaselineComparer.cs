using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anthea.Testing.Comparison;

/// <summary>One difference between two captures. Admitted differences are reported but do not fail the comparison.</summary>
public sealed record Difference(string File, string Path, string Class, bool Admitted, string? A, string? B, double? Delta = null, double? Allowance = null, string? Note = null);

/// <summary>
/// 'compare &lt;a&gt; &lt;b&gt;': compares two capture folders file by file. JSON by value (numbers with the tolerance of their quantity),
/// CSV by cell, text by line (lines aligned on their skeleton, numbers compared with <see cref="NumberText"/>).
/// Classes: file-aggiunto, file-rimosso, chiave-aggiunta, chiave-rimossa, elemento-aggiunto, elemento-rimosso, riga-aggiunta, riga-rimossa,
/// tipo, valore, testo, numero (not admitted); numero-entro-tolleranza, arrotondamento, volatile (admitted).
/// </summary>
public sealed class BaselineComparer
{
    public const int MaxStored = 20000;
    readonly Tolerances tolerances;
    readonly List<Difference> stored = [];
    readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> volatileReasons = new(StringComparer.Ordinal);
    int compared;

    public BaselineComparer(Tolerances tolerances) => this.tolerances = tolerances;

    public int NotAdmitted { get; private set; }

    void Add(Difference d)
    {
        counts[d.Class] = counts.GetValueOrDefault(d.Class) + 1;
        if (!d.Admitted) NotAdmitted++;
        if (d.Class == "volatile") { volatileReasons[d.Note ?? ""] = volatileReasons.GetValueOrDefault(d.Note ?? "") + 1; return; }
        if (stored.Count < MaxStored) stored.Add(d);
    }

    public static IEnumerable<string> Files(string root) => Directory.Exists(root)
        ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => System.IO.Path.GetRelativePath(root, f).Replace('\\', '/'))
        : [];

    public JsonObject Compare(string a, string b)
    {
        a = System.IO.Path.GetFullPath(a); b = System.IO.Path.GetFullPath(b);
        var left = Files(a).Where(f => !tolerances.IsExcluded(f)).ToHashSet(StringComparer.Ordinal);
        var right = Files(b).Where(f => !tolerances.IsExcluded(f)).ToHashSet(StringComparer.Ordinal);
        foreach (var file in left.Union(right).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (!right.Contains(file)) { Add(new(file, "", "file-rimosso", false, file, null)); continue; }
            if (!left.Contains(file)) { Add(new(file, "", "file-aggiunto", false, null, file)); continue; }
            compared++;
            string pa = System.IO.Path.Combine(a, file), pb = System.IO.Path.Combine(b, file);
            if (File.ReadAllBytes(pa).AsSpan().SequenceEqual(File.ReadAllBytes(pb))) continue;
            string extension = System.IO.Path.GetExtension(file.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? file[..^3] : file).ToLowerInvariant();
            try
            {
                if (extension == ".json") Json(file, JsonNode.Parse(Read(pa)), JsonNode.Parse(Read(pb)), "$", "");
                else if (extension == ".csv") Csv(file, Lines(pa), Lines(pb));
                else Text(file, Lines(pa), Lines(pb));
            }
            catch (JsonException ex) { Add(new(file, "", "testo", false, null, null, Note: "JSON non leggibile: " + ex.Message)); }
        }
        return Report(a, b);
    }

    /// <summary>The text of a capture file; files ending in .gz are decompressed.</summary>
    static string Read(string path)
    {
        if (!path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) return File.ReadAllText(path, Encoding.UTF8);
        using var zip = new System.IO.Compression.GZipStream(File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(zip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    static string[] Lines(string path)
    {
        var text = Read(path).TrimStart('\uFEFF').Replace("\r\n", "\n");
        if (text.EndsWith('\n')) text = text[..^1];
        return text.Length == 0 ? [] : text.Split('\n');
    }

    // ------------------------------------------------------------------ JSON
    void Json(string file, JsonNode? x, JsonNode? y, string path, string key)
    {
        if (tolerances.IsVolatile(key, file + ":" + path) is { } v) { if (!JsonNode.DeepEquals(x, y)) Add(new(file, path, "volatile", true, null, null, Note: v.Reason)); return; }
        var kx = Kind(x); var ky = Kind(y);
        if (kx != ky) { Add(new(file, path, "tipo", false, Short(x), Short(y))); return; }
        switch (x)
        {
            case JsonObject ox:
                var oy = (JsonObject)y!;
                foreach (var name in ox.Select(p => p.Key).Union(oy.Select(p => p.Key)).Distinct().OrderBy(k => k, StringComparer.Ordinal))
                {
                    string child = path + Member(name);
                    bool inX = ox.ContainsKey(name), inY = oy.ContainsKey(name);
                    if (inX && inY) { Json(file, ox[name], oy[name], child, name); continue; }
                    if (tolerances.IsVolatile(name, file + ":" + child) is { } vv) { Add(new(file, child, "volatile", true, null, null, Note: vv.Reason)); continue; }
                    Add(new(file, child, inX ? "chiave-rimossa" : "chiave-aggiunta", false, inX ? Short(ox[name]) : null, inY ? Short(oy[name]) : null));
                }
                break;
            case JsonArray ax:
                var ay = (JsonArray)y!;
                for (int i = 0; i < Math.Max(ax.Count, ay.Count); i++)
                {
                    string child = path + "[" + i + "]";
                    if (i >= ay.Count) Add(new(file, child, "elemento-rimosso", false, Short(ax[i]), null));
                    else if (i >= ax.Count) Add(new(file, child, "elemento-aggiunto", false, null, Short(ay[i])));
                    else Json(file, ax[i], ay[i], child, key);
                }
                break;
            case JsonValue vx:
                var vy = (JsonValue)y!;
                if (kx == JsonValueKind.Number)
                {
                    double a = double.Parse(vx.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture), b = double.Parse(vy.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (a == b) return;
                    var q = tolerances.For(key, file + ":" + path); double delta = Math.Abs(a - b), allowance = q.Allowance(a, b);
                    Add(new(file, path, delta <= allowance ? "numero-entro-tolleranza" : "numero", delta <= allowance, R(a), R(b), delta, allowance, q.Name));
                }
                else if (kx == JsonValueKind.String) Strings(file, path, key, vx.GetValue<string>(), vy.GetValue<string>());
                else if (!JsonNode.DeepEquals(vx, vy)) Add(new(file, path, "valore", false, vx.ToJsonString(), vy.ToJsonString()));
                break;
        }
    }

    void Strings(string file, string path, string key, string a, string b)
    {
        if (a == b) return;
        var (outcome, numbers) = NumberText.Compare(a, b, tolerances.For(key, file + ":" + path));
        AddText(file, path, a, b, outcome, numbers);
    }

    void AddText(string file, string path, string a, string b, NumberText.Outcome outcome, List<NumberText.NumberDifference> numbers)
    {
        switch (outcome)
        {
            case NumberText.Outcome.Equal: return;
            case NumberText.Outcome.Text: Add(new(file, path, "testo", false, a, b)); return;
        }
        foreach (var n in numbers)
        {
            string cls = n.Outcome switch { NumberText.Outcome.WithinTolerance => "numero-entro-tolleranza", NumberText.Outcome.Rounding => "arrotondamento", _ => "numero" };
            Add(new(file, path + "#" + (n.Index + 1), cls, n.Outcome != NumberText.Outcome.Number, n.A.Text, n.B.Text, n.Delta, n.Allowance, Clip(a)));
        }
    }

    static JsonValueKind Kind(JsonNode? n) => n switch { null => JsonValueKind.Null, JsonObject => JsonValueKind.Object, JsonArray => JsonValueKind.Array, JsonValue v => v.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? JsonValueKind.True : v.GetValueKind(), _ => JsonValueKind.Undefined };
    static string Member(string name) => System.Text.RegularExpressions.Regex.IsMatch(name, @"^[\p{L}_][\p{L}\p{N}_]*$") ? "." + name : "[" + JsonSerializer.Serialize(name, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "]";
    static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string? Short(JsonNode? n) => n is null ? "null" : Clip(n.ToJsonString());
    static string Clip(string s) => s.Length <= 300 ? s : s[..300] + "…";

    // ------------------------------------------------------------------ CSV
    void Csv(string file, string[] a, string[] b)
    {
        string[]? header = null;
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            string path = "riga " + (i + 1);
            if (i >= b.Length) { Add(new(file, path, "riga-rimossa", false, Clip(a[i]), null)); continue; }
            if (i >= a.Length) { Add(new(file, path, "riga-aggiunta", false, null, Clip(b[i]))); continue; }
            if (a[i].StartsWith('#') || b[i].StartsWith('#'))
            {
                Line(file, path, a[i], b[i], "");
                continue;
            }
            var ca = Cells(a[i]); var cb = Cells(b[i]);
            if (header is null) { header = ca; if (a[i] != b[i]) Add(new(file, path + " (intestazione)", "testo", false, Clip(a[i]), Clip(b[i]))); continue; }
            if (a[i] == b[i]) continue;
            if (ca.Length != cb.Length) { Add(new(file, path, "testo", false, Clip(a[i]), Clip(b[i]), Note: "numero di colonne diverso")); continue; }
            for (int c = 0; c < ca.Length; c++)
            {
                if (ca[c] == cb[c]) continue;
                string column = c < header.Length ? header[c] : "colonna " + (c + 1);
                if (tolerances.IsVolatile(column, file + ":" + path) is { } v) { Add(new(file, path + " · " + column, "volatile", true, null, null, Note: v.Reason)); continue; }
                var (o, n) = NumberText.Compare(ca[c], cb[c], tolerances.For(column, file + ":" + column));
                AddText(file, path + " · " + column, ca[c], cb[c], o, n);
            }
        }
    }

    /// <summary>Cells separated by ';' (by ',' when the line has no ';'), with double quotes.</summary>
    static string[] Cells(string line)
    {
        char separator = line.Contains(';') ? ';' : ',';
        var cells = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (quoted) { if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; } else if (ch == '"') quoted = false; else cell.Append(ch); }
            else if (ch == '"') quoted = true;
            else if (ch == separator) { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(ch);
        }
        cells.Add(cell.ToString());
        return cells.ToArray();
    }

    // ------------------------------------------------------------------ text
    /// <summary>Lines aligned on their skeleton (longest common subsequence); unmatched lines of the same block are paired as text changes.</summary>
    void Text(string file, string[] a, string[] b)
    {
        var sa = a.Select(NumberText.Skeleton).ToArray(); var sb = b.Select(NumberText.Skeleton).ToArray();
        int start = 0; while (start < a.Length && start < b.Length && sa[start] == sb[start]) start++;
        int endA = a.Length, endB = b.Length; while (endA > start && endB > start && sa[endA - 1] == sb[endB - 1]) { endA--; endB--; }
        var pairs = new List<(int A, int B)>();
        for (int i = 0; i < start; i++) pairs.Add((i, i));
        int n = endA - start, m = endB - start;
        if ((long)n * m <= 10_000_000)
        {
            var lcs = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--) for (int j = m - 1; j >= 0; j--)
                lcs[i, j] = sa[start + i] == sb[start + j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            int x = 0, y = 0;
            while (x < n && y < m)
            {
                if (sa[start + x] == sb[start + y]) { pairs.Add((start + x, start + y)); x++; y++; }
                else if (lcs[x + 1, y] >= lcs[x, y + 1]) x++; else y++;
            }
        }
        for (int i = 0; endA + i < a.Length; i++) pairs.Add((endA + i, endB + i));
        int pa = 0, pb = 0;
        foreach (var (ia, ib) in pairs.Append((a.Length, b.Length)))
        {
            // Unmatched lines between two matched pairs: changed lines first, then additions or removals.
            var removed = Enumerable.Range(pa, ia - pa).ToList(); var added = Enumerable.Range(pb, ib - pb).ToList();
            for (int k = 0; k < Math.Max(removed.Count, added.Count); k++)
            {
                if (k < removed.Count && k < added.Count) Line(file, "riga " + (removed[k] + 1), a[removed[k]], b[added[k]], "");
                else if (k < removed.Count) Add(new(file, "riga " + (removed[k] + 1), "riga-rimossa", false, Clip(a[removed[k]]), null));
                else Add(new(file, "riga " + (added[k] + 1) + " (b)", "riga-aggiunta", false, null, Clip(b[added[k]])));
            }
            if (ia < a.Length && ib < b.Length && a[ia] != b[ib]) Line(file, "riga " + (ia + 1), a[ia], b[ib], "");
            pa = ia + 1; pb = ib + 1;
        }
    }

    /// <summary>Two lines: a JSON tail ("# NAME {...}") is compared as JSON, so volatile keys are honoured; otherwise as printed text.</summary>
    void Line(string file, string path, string a, string b, string key)
    {
        if (a == b) return;
        if (JsonTail(a) is { } x && JsonTail(b) is { } y && x.Prefix == y.Prefix) { Json(file, x.Json, y.Json, path + " $", ""); return; }
        var (o, n) = NumberText.Compare(a, b, tolerances.For(key, file + ":" + path));
        AddText(file, path, a, b, o, n);
    }

    static (string Prefix, JsonNode? Json)? JsonTail(string line)
    {
        int i = line.IndexOfAny(['{', '[']);
        if (i < 0) return null;
        try { return (line[..i], JsonNode.Parse(line[i..])); }
        catch (JsonException) { return null; }
    }

    // ------------------------------------------------------------------ report
    JsonObject Report(string a, string b)
    {
        var manifest = new JsonObject();
        try
        {
            var ma = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(a, "manifest.json"))) as JsonObject;
            var mb = JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(b, "manifest.json"))) as JsonObject;
            foreach (var (label, path) in new[] { ("commit", "anthea.commit"), ("albero_modificato", "anthea.albero_modificato"), ("manifest_librerie", "librerie.manifest_sha256"),
                ("librerie_coerenti", "librerie.coerenti_con_manifest"), ("runtime", "ambiente.runtime"), ("sdk", "ambiente.sdk"), ("cultura", "ambiente.cultura") })
                manifest[label] = new JsonObject { ["a"] = Dig(ma, path)?.DeepClone(), ["b"] = Dig(mb, path)?.DeepClone() };
            var dllA = (Dig(ma, "assembly") as JsonArray ?? []).OfType<JsonObject>().ToDictionary(o => o["nome"]!.GetValue<string>(), o => o["sha256"]?.ToString() ?? "");
            var dllB = (Dig(mb, "assembly") as JsonArray ?? []).OfType<JsonObject>().ToDictionary(o => o["nome"]!.GetValue<string>(), o => o["sha256"]?.ToString() ?? "");
            manifest["assembly_cambiati"] = new JsonArray(dllA.Keys.Union(dllB.Keys).Where(k => dllA.GetValueOrDefault(k) != dllB.GetValueOrDefault(k)).OrderBy(k => k, StringComparer.Ordinal).Select(k => (JsonNode)k).ToArray());
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException) { manifest["nota"] = "manifest non confrontabile: " + ex.Message; }
        var byClass = new JsonObject(); foreach (var (k, v) in counts.OrderBy(p => p.Key, StringComparer.Ordinal)) byClass[k] = v;
        var reasons = new JsonObject(); foreach (var (k, v) in volatileReasons.OrderBy(p => p.Key, StringComparer.Ordinal)) reasons[k] = v;
        var details = new JsonArray(stored.OrderBy(d => d.Admitted).ThenBy(d => d.File, StringComparer.Ordinal).Select(d => (JsonNode)new JsonObject
        {
            ["file"] = d.File, ["percorso"] = d.Path, ["classe"] = d.Class, ["ammessa"] = d.Admitted, ["a"] = d.A, ["b"] = d.B,
            ["delta"] = d.Delta is double x ? JsonValue.Create(x) : null, ["tolleranza"] = d.Allowance is double t ? JsonValue.Create(t) : null, ["nota"] = d.Note
        }).ToArray());
        return new JsonObject
        {
            ["a"] = a, ["b"] = b, ["tolleranze"] = tolerances.Source, ["file_confrontati"] = compared,
            ["esito"] = NotAdmitted == 0 ? "uguali entro le tolleranze" : "differenze non ammesse",
            ["differenze_non_ammesse"] = NotAdmitted, ["per_classe"] = byClass, ["volatili"] = reasons, ["manifest"] = manifest,
            ["dettagli_troncati"] = counts.Values.Sum() - volatileReasons.Values.Sum() > stored.Count, ["differenze"] = details
        };
    }

    static JsonNode? Dig(JsonNode? node, string path)
    {
        foreach (var part in path.Split('.')) node = node is JsonObject o && o.TryGetPropertyValue(part, out var next) ? next : null;
        return node;
    }

    public static void Print(JsonObject report, int max)
    {
        Console.WriteLine($"Confronto {report["a"]} -> {report["b"]}");
        Console.WriteLine($"File confrontati: {report["file_confrontati"]}; esito: {report["esito"]}; differenze non ammesse: {report["differenze_non_ammesse"]}");
        foreach (var (k, v) in report["per_classe"]!.AsObject()) Console.WriteLine($"  {k,-26} {v}");
        foreach (var (k, v) in report["volatili"]!.AsObject()) Console.WriteLine($"  volatile: {v} × {k}");
        foreach (var (k, v) in report["manifest"]!.AsObject())
            if (v is JsonObject pair && !JsonNode.DeepEquals(pair["a"], pair["b"])) Console.WriteLine($"  manifest {k}: {pair["a"]?.ToJsonString()} -> {pair["b"]?.ToJsonString()}");
            else if (v is JsonArray changed && changed.Count > 0) Console.WriteLine($"  manifest {k}: {string.Join(", ", changed)}");
        int shown = 0;
        foreach (var d in report["differenze"]!.AsArray())
        {
            if (shown++ >= max) { Console.WriteLine("  …"); break; }
            Console.WriteLine($"  [{d!["classe"]}{(d["ammessa"]!.GetValue<bool>() ? ", ammessa" : "")}] {d["file"]} {d["percorso"]}: {d["a"]} -> {d["b"]}");
        }
    }
}
