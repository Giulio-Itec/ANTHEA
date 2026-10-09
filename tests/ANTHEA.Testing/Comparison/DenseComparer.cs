using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Anthea.Testing.Comparison;

/// <summary>One difference of a dense comparison: file, data row (1-based index and key), field (CSV column, JSON path or 'testo'), class.</summary>
public sealed record DenseDifference(string File, int RowIndex, string Row, string Field, string Class, bool Admitted, string? A, string? B,
    double? Delta = null, double? Allowance = null, string? Quantity = null);

/// <summary>
/// 'compare-dense &lt;riferimento&gt; &lt;candidato&gt; --confronto &lt;nome&gt;': compares the dense captures of CheckerMigration.Capture with a
/// reference (another capture, or the frozen fixtures of the GPC libraries) file by file. Lines starting with '#' and the JSONL 'header'
/// lines (commit and SHA-256 of ANTHEA.Calculations.dll) are excluded; the random identifiers declared by the comparison are replaced
/// by their order of appearance. CSV by cell (';'), JSONL by JSON value, other text by line. Numbers inside cells and strings are read
/// in the invariant format (the captures use 'R') and compared with the quantity of tolerances.json chosen by the rule that matches
/// '&lt;prefisso&gt;/&lt;file&gt;:&lt;campo&gt;'; the rest of the text must be identical. Every difference not admitted by the tolerances must match
/// an expected difference of the classification (DenseClassification), and every expected difference must be found as declared.
/// Admitted classes: identificativo-casuale (rows), numero-entro-tolleranza. Not admitted: file-aggiunto, file-rimosso, riga-aggiunta,
/// riga-rimossa, intestazione, struttura, testo, numero, tipo, valore, chiave-aggiunta, chiave-rimossa, elemento-aggiunto, elemento-rimosso.
/// </summary>
public sealed class DenseComparer
{
    public const int MaxStored = 2000;
    static readonly Regex GuidPattern = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex Hex32Pattern = new(@"(?<![0-9a-fA-F])[0-9a-f]{32}(?![0-9a-fA-F])", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    // Invariant numbers not glued to letters, digits or dots ('B02', 'R300x500', '§4.1.2' stay text), and the invariant NaN and infinities.
    static readonly Regex NumberPattern = new(@"(?<![\p{L}\p{N}_.])[-+]?(?:\d+(?:\.\d+)?|\.\d+)(?:[eE][-+]?\d+)?(?![\p{L}\p{N}_.])|(?<![\p{L}\p{N}_])-?(?:NaN|Infinity)(?![\p{L}\p{N}_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    // NaN and ±∞ are legitimate values and differences (a relative difference against zero): written as "NaN"/"Infinity", the
    // report never fails on them.
    static readonly JsonSerializerOptions JsonOut = new()
    {
        WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    readonly Tolerances tolerances;
    readonly DenseSet set;
    readonly DenseClassification classification;
    readonly List<DenseDifference> unclassified = [];
    readonly List<string> problems = [];
    readonly List<string> notCompared = [];
    readonly List<string> compared = [];
    readonly JsonArray fileReports = [];
    readonly SortedDictionary<string, int> byClass = new(StringComparer.Ordinal);
    readonly SortedDictionary<string, int> byCategory = new(StringComparer.Ordinal);
    readonly SortedDictionary<string, int> rowsByCategory = new(StringComparer.Ordinal);
    readonly SortedDictionary<string, int> randomReasons = new(StringComparer.Ordinal);
    int filesCompared, rowsCompared, rowsIdentical, rowsRandom, rowsAdmitted, rowsClassified, rowsUnclassified, linesComment, linesHeader;

    public DenseComparer(Tolerances tolerances, DenseClassification classification, string setName)
    {
        this.tolerances = tolerances; this.classification = classification;
        set = classification.Sets.TryGetValue(setName, out var s) ? s
            : throw new ArgumentException($"Confronto '{setName}' non presente in {classification.Source}. Disponibili: {string.Join(", ", classification.Sets.Keys)}.");
    }

    public int UnclassifiedCount { get; private set; }
    public bool Passed => UnclassifiedCount == 0 && problems.Count == 0;

    sealed class FileStats
    {
        public required string Name { get; init; }
        public required string Format { get; init; }
        public int RowsA, RowsB, Compared, Comments, Headers, Identical, Random, Admitted, Classified, Unclassified;
        public readonly SortedDictionary<string, int> Classes = new(StringComparer.Ordinal);
        public readonly SortedDictionary<string, int> Categories = new(StringComparer.Ordinal);
        public readonly SortedDictionary<string, int> CategoryRows = new(StringComparer.Ordinal);
    }

    public JsonObject Compare(string reference, string candidate)
    {
        reference = Path.GetFullPath(reference); candidate = Path.GetFullPath(candidate);
        bool Excluded(string f) => f == "capture-manifest.json" || set.ExcludedFiles.Any(r => r.IsMatch(f));
        var left = Files(reference).Where(f => !Excluded(f)).ToHashSet(StringComparer.Ordinal);
        var right = Files(candidate).Where(f => !Excluded(f)).ToHashSet(StringComparer.Ordinal);
        IEnumerable<string> names;
        if (set.Files.Count > 0)
        {
            names = set.Files.Select(f => f.Name);
            notCompared.AddRange(left.Union(right).Except(names).OrderBy(f => f, StringComparer.Ordinal));
        }
        else if (set.ReferenceFilesOnly)
        {
            names = left;
            notCompared.AddRange(right.Except(left).OrderBy(f => f, StringComparer.Ordinal));
        }
        else names = left.Union(right);
        foreach (var file in names.OrderBy(f => f, StringComparer.Ordinal))
        {
            var spec = set.Spec(file);
            if (!left.Contains(file) || !right.Contains(file))
            {
                if (set.Files.Count > 0) { problems.Add($"{file}: file dichiarato assente nel {(left.Contains(file) ? "candidato" : "riferimento")}."); continue; }
                var d = new DenseDifference(file, 0, "", "", left.Contains(file) ? "file-rimosso" : "file-aggiunto", false, left.Contains(file) ? file : null, right.Contains(file) ? file : null);
                var stats = new FileStats { Name = file, Format = Format(file) };
                Record(stats, [d]); fileReports.Add(Report(stats, spec, null));
                continue;
            }
            filesCompared++; compared.Add(file);
            CompareFile(file, spec, Path.Combine(reference, file), Path.Combine(candidate, file));
        }
        foreach (var e in set.Expected)
        {
            // An expected difference of a file that is not part of this comparison (one mode of the capture) is not applicable.
            if (!compared.Any(e.File.IsMatch)) continue;
            if (e.Matched == 0) problems.Add($"Differenza attesa {e.Id} non trovata.");
            if (e.ExpectedRows is int rows && e.Rows.Count != rows) problems.Add($"Differenza attesa {e.Id}: righe {e.Rows.Count} invece di {rows}.");
            if (e.ExpectedDifferences is int count && e.Matched != count) problems.Add($"Differenza attesa {e.Id}: differenze {e.Matched} invece di {count}.");
        }
        return Report(reference, candidate);
    }

    static IEnumerable<string> Files(string root) => Directory.Exists(root)
        ? Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).OfType<string>()
        : [];

    static string Format(string file)
    {
        string name = file.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? file[..^3] : file;
        return Path.GetExtension(name).ToLowerInvariant() switch { ".csv" => "csv", ".jsonl" => "jsonl", _ => "testo" };
    }

    static string[] Lines(string path)
    {
        string text;
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = new System.IO.Compression.GZipStream(File.OpenRead(path), System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(zip, Encoding.UTF8);
            text = reader.ReadToEnd();
        }
        else text = File.ReadAllText(path, Encoding.UTF8);
        text = text.TrimStart('﻿').Replace("\r\n", "\n");
        if (text.EndsWith('\n')) text = text[..^1];
        return text.Length == 0 ? [] : text.Split('\n');
    }

    /// <summary>Data lines without '#' comments and JSONL headers.</summary>
    static List<string> Data(string[] lines, string format, out int comments, out int headers)
    {
        var data = new List<string>(lines.Length); comments = 0; headers = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith('#')) { comments++; continue; }
            if (format == "jsonl" && line.StartsWith("{\"header\":", StringComparison.Ordinal)) { headers++; continue; }
            data.Add(line);
        }
        return data;
    }

    /// <summary>Random identifiers replaced by &lt;GUID-n&gt; / &lt;ID-n&gt;, numbered by first appearance in the file.</summary>
    static List<string> Normalize(List<string> lines, RandomIdentifiers[] rules)
    {
        if (rules.Length == 0) return lines;
        var guids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        bool guid = rules.Any(r => r.Form == RandomIdForm.Guid), hex = rules.Any(r => r.Form == RandomIdForm.Hex32);
        return lines.Select(line =>
        {
            if (guid) line = GuidPattern.Replace(line, m => "<GUID-" + (guids.TryGetValue(m.Value, out var n) ? n : guids[m.Value] = guids.Count + 1) + ">");
            if (hex) line = Hex32Pattern.Replace(line, m => "<ID-" + (ids.TryGetValue(m.Value, out var n) ? n : ids[m.Value] = ids.Count + 1) + ">");
            return line;
        }).ToList();
    }

    void CompareFile(string file, DenseFileSpec? spec, string pa, string pb)
    {
        string format = Format(file);
        var stats = new FileStats { Name = file, Format = format };
        string? sha = null;
        if (spec?.Sha256 is { Length: > 0 } expectedSha)
        {
            sha = LfSha256(pa);
            if (!sha.Equals(expectedSha, StringComparison.OrdinalIgnoreCase)) problems.Add($"{file}: SHA-256 (fine riga LF) del riferimento {sha} invece di {expectedSha}: riferimento cambiato.");
        }
        var rawA = Data(Lines(pa), format, out int ca, out int ha); var rawB = Data(Lines(pb), format, out int cb, out int hb);
        stats.Comments = ca + cb; stats.Headers = ha + hb;
        var rules = set.RandomIds.Where(r => r.File.IsMatch(file)).ToArray();
        var a = Normalize(rawA, rules); var b = Normalize(rawB, rules);
        string reason = string.Join("; ", rules.Select(r => r.Reason).Distinct());

        string[] names = []; int keyIndex = -1, start = 0;
        if (format == "csv")
        {
            bool header = spec?.Header ?? (a.Count > 0 && a[0].Split(';').All(c => !IsNumber(c)));
            if (header && a.Count > 0)
            {
                names = a[0].Split(';'); start = 1;
                if (b.Count == 0 || a[0] != b[0]) Record(stats, [new(file, 0, "intestazione", "intestazione", "intestazione", false, Cap(a[0]), b.Count > 0 ? Cap(b[0]) : null)]);
            }
            else if (spec?.Columns is { Length: > 0 } columns) names = columns.Split(';');
            string key = spec?.Key ?? "id";
            keyIndex = Array.IndexOf(names, key);
            // Headerless files without column names: key and fields are the 0-based cell indices 'c<i>', as c[i] in the MigrationTests.
            if (keyIndex < 0 && Regex.Match(key, @"^c(\d+)$") is { Success: true } m) keyIndex = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        stats.RowsA = a.Count - start; stats.RowsB = b.Count - start;
        if (spec?.DataRows is int expectedRows && (stats.RowsA != expectedRows || stats.RowsB != expectedRows))
            problems.Add($"{file}: righe di dati {stats.RowsA}/{stats.RowsB} invece di {expectedRows}.");

        for (int i = start; i < Math.Max(a.Count, b.Count); i++)
        {
            int index = i - start + 1;
            if (i >= b.Count || i >= a.Count)
            {
                string line = i >= b.Count ? a[i] : b[i];
                Record(stats, [new(file, index, RowKey(format, line, keyIndex, spec, index), "", i >= b.Count ? "riga-rimossa" : "riga-aggiunta", false,
                    i < a.Count ? Cap(a[i]) : null, i < b.Count ? Cap(b[i]) : null)]);
                continue;
            }
            stats.Compared++;
            if (rawA[i] == rawB[i]) { stats.Identical++; continue; }
            if (a[i] == b[i]) { stats.Random++; randomReasons[reason] = randomReasons.GetValueOrDefault(reason) + 1; continue; }
            string row = RowKey(format, a[i], keyIndex, spec, index);
            var differences = new List<DenseDifference>();
            if (format == "csv") Csv(differences, file, index, row, names, a[i], b[i]);
            else if (format == "jsonl") Jsonl(differences, file, index, row, a[i], b[i]);
            else Text(differences, file, index, row, "testo", a[i], b[i]);
            if (differences.Count == 0) { stats.Identical++; continue; }
            Record(stats, differences);
        }
        fileReports.Add(Report(stats, spec, sha));
    }

    /// <summary>SHA-256 of the file with CR LF read as LF: the same value for a checkout with or without core.autocrlf.</summary>
    public static string LfSha256(string path)
    {
        var bytes = File.ReadAllBytes(path); var normalized = new List<byte>(bytes.Length);
        for (int i = 0; i < bytes.Length; i++) if (!(bytes[i] == 13 && i + 1 < bytes.Length && bytes[i + 1] == 10)) normalized.Add(bytes[i]);
        return Convert.ToHexString(SHA256.HashData(normalized.ToArray()));
    }

    static bool IsNumber(string cell) => double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    static string RowKey(string format, string line, int keyIndex, DenseFileSpec? spec, int index)
    {
        if (format == "csv" && keyIndex >= 0) { var cells = line.Split(';'); return keyIndex < cells.Length ? cells[keyIndex] : index.ToString(CultureInfo.InvariantCulture); }
        if (format == "jsonl")
        {
            try
            {
                if (JsonNode.Parse(line) is JsonObject o && o[spec?.Key ?? "name"] is JsonValue v) return v.ToString();
            }
            catch (JsonException) { }
        }
        return index.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Classifies the differences of one row and updates the counters. Admitted differences need no classification.</summary>
    void Record(FileStats stats, List<DenseDifference> differences)
    {
        bool anyUnclassified = false, anyClassified = false; var categories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in differences)
        {
            stats.Classes[d.Class] = stats.Classes.GetValueOrDefault(d.Class) + 1; byClass[d.Class] = byClass.GetValueOrDefault(d.Class) + 1;
            if (d.Admitted) continue;
            var e = set.Expected.FirstOrDefault(x => x.Matches(d));
            if (e is null)
            {
                anyUnclassified = true; UnclassifiedCount++;
                if (unclassified.Count < MaxStored) unclassified.Add(d);
                continue;
            }
            anyClassified = true; categories.Add(e.Category);
            e.Matched++; e.Rows.Add(d.File + "#" + d.RowIndex.ToString(CultureInfo.InvariantCulture));
            if (e.Examples.Count < 3) e.Examples.Add(d);
            stats.Categories[e.Category] = stats.Categories.GetValueOrDefault(e.Category) + 1; byCategory[e.Category] = byCategory.GetValueOrDefault(e.Category) + 1;
        }
        foreach (var c in categories) { stats.CategoryRows[c] = stats.CategoryRows.GetValueOrDefault(c) + 1; rowsByCategory[c] = rowsByCategory.GetValueOrDefault(c) + 1; }
        if (anyUnclassified) stats.Unclassified++; else if (anyClassified) stats.Classified++; else stats.Admitted++;
    }

    // ------------------------------------------------------------------ CSV, JSONL, text
    void Csv(List<DenseDifference> differences, string file, int index, string row, string[] names, string a, string b)
    {
        var ca = a.Split(';'); var cb = b.Split(';');
        if (ca.Length != cb.Length) { differences.Add(new(file, index, row, "", "struttura", false, Cap(a), Cap(b))); return; }
        for (int c = 0; c < ca.Length; c++)
        {
            if (ca[c] == cb[c]) continue;
            string name = c < names.Length ? names[c] : "c" + c.ToString(CultureInfo.InvariantCulture);
            Text(differences, file, index, row, name, ca[c], cb[c]);
        }
    }

    void Text(List<DenseDifference> differences, string file, int index, string row, string field, string a, string b)
    {
        if (a == b) return;
        var q = tolerances.For(field, set.TolerancePrefix + "/" + file + ":" + field);
        var (sa, xa) = Split(a); var (sb, xb) = Split(b);
        if (sa != sb || xa.Count != xb.Count) { differences.Add(new(file, index, row, field, "testo", false, a, b, Quantity: q.Name)); return; }
        bool beyond = false, within = false; double worstDelta = 0, worstAllowance = 0, worstRatio = -1;
        for (int k = 0; k < xa.Count; k++)
        {
            double x = xa[k], y = xb[k];
            if (x.Equals(y)) continue;
            double delta = Math.Abs(x - y), allowance = q.Allowance(x, y);
            bool ok = double.IsFinite(delta) && delta <= allowance;
            if (ok) within = true; else beyond = true;
            double ratio = allowance > 0 ? delta / allowance : double.PositiveInfinity;
            if (double.IsNaN(ratio)) ratio = double.PositiveInfinity;
            if (ratio > worstRatio) { worstRatio = ratio; worstDelta = delta; worstAllowance = allowance; }
        }
        if (beyond) differences.Add(new(file, index, row, field, "numero", false, a, b, worstDelta, worstAllowance, q.Name));
        else if (within) differences.Add(new(file, index, row, field, "numero-entro-tolleranza", true, a, b, worstDelta, worstAllowance, q.Name));
        else differences.Add(new(file, index, row, field, "testo", false, a, b, Quantity: q.Name)); // same numbers written differently
    }

    static (string Skeleton, List<double> Numbers) Split(string text)
    {
        var numbers = new List<double>();
        string skeleton = NumberPattern.Replace(text, m =>
        {
            if (!double.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return m.Value;
            numbers.Add(v); return "#";
        });
        return (skeleton, numbers);
    }

    void Jsonl(List<DenseDifference> differences, string file, int index, string row, string a, string b)
    {
        JsonNode? x, y;
        try { x = JsonNode.Parse(a); y = JsonNode.Parse(b); }
        catch (JsonException) { Text(differences, file, index, row, "testo", a, b); return; }
        Json(differences, file, index, row, x, y, "$", "");
    }

    void Json(List<DenseDifference> differences, string file, int index, string row, JsonNode? x, JsonNode? y, string path, string key)
    {
        var kx = Kind(x); var ky = Kind(y);
        if (kx != ky) { differences.Add(new(file, index, row, path, "tipo", false, Short(x), Short(y))); return; }
        switch (x)
        {
            case JsonObject ox:
                var oy = (JsonObject)y!;
                foreach (var name in ox.Select(p => p.Key).Union(oy.Select(p => p.Key)).Distinct().OrderBy(k => k, StringComparer.Ordinal))
                {
                    string child = path + "." + name;
                    bool inX = ox.ContainsKey(name), inY = oy.ContainsKey(name);
                    if (inX && inY) Json(differences, file, index, row, ox[name], oy[name], child, name);
                    else differences.Add(new(file, index, row, child, inX ? "chiave-rimossa" : "chiave-aggiunta", false, inX ? Short(ox[name]) : null, inY ? Short(oy[name]) : null));
                }
                break;
            case JsonArray ax:
                var ay = (JsonArray)y!;
                for (int i = 0; i < Math.Max(ax.Count, ay.Count); i++)
                {
                    string child = path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                    if (i >= ay.Count) differences.Add(new(file, index, row, child, "elemento-rimosso", false, Short(ax[i]), null));
                    else if (i >= ax.Count) differences.Add(new(file, index, row, child, "elemento-aggiunto", false, null, Short(ay[i])));
                    else Json(differences, file, index, row, ax[i], ay[i], child, key);
                }
                break;
            case JsonValue vx:
                var vy = (JsonValue)y!;
                if (kx == JsonValueKind.Number)
                {
                    double a = double.Parse(vx.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture), b = double.Parse(vy.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (a.Equals(b)) return;
                    var q = tolerances.For(key, set.TolerancePrefix + "/" + file + ":" + path);
                    double delta = Math.Abs(a - b), allowance = q.Allowance(a, b);
                    differences.Add(new(file, index, row, path, delta <= allowance ? "numero-entro-tolleranza" : "numero", delta <= allowance, R(a), R(b), delta, allowance, q.Name));
                }
                else if (kx == JsonValueKind.String)
                {
                    string a = vx.GetValue<string>(), b = vy.GetValue<string>();
                    if (a != b) Text(differences, file, index, row, path, a, b);
                }
                else if (!JsonNode.DeepEquals(vx, vy)) differences.Add(new(file, index, row, path, "valore", false, vx.ToJsonString(), vy.ToJsonString()));
                break;
        }
    }

    static JsonValueKind Kind(JsonNode? n) => n switch
    {
        null => JsonValueKind.Null, JsonObject => JsonValueKind.Object, JsonArray => JsonValueKind.Array,
        JsonValue v => v.GetValueKind() is JsonValueKind.True or JsonValueKind.False ? JsonValueKind.True : v.GetValueKind(), _ => JsonValueKind.Undefined
    };
    static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string? Short(JsonNode? n) => n is null ? "null" : n.ToJsonString(Relaxed);
    static readonly JsonSerializerOptions Relaxed = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    static string Clip(string s) => s.Length <= 300 ? s : s[..300] + "…";
    // Whole lines kept for the classification patterns, capped so that a removed JSONL document does not fill the memory.
    static string Cap(string s) => s.Length <= 20000 ? s : s[..20000] + "…";

    // ------------------------------------------------------------------ report
    JsonObject Report(FileStats s, DenseFileSpec? spec, string? sha)
    {
        rowsCompared += s.Compared; rowsIdentical += s.Identical; rowsRandom += s.Random; rowsAdmitted += s.Admitted;
        rowsClassified += s.Classified; rowsUnclassified += s.Unclassified; linesComment += s.Comments; linesHeader += s.Headers;
        return new JsonObject
        {
            ["file"] = s.Name, ["formato"] = s.Format, ["sha256_riferimento"] = sha, ["test"] = spec?.Test,
            ["righe_dati_riferimento"] = s.RowsA, ["righe_dati_candidato"] = s.RowsB, ["righe_confrontate"] = s.Compared,
            ["righe_escluse_commento"] = s.Comments, ["righe_escluse_header"] = s.Headers,
            ["righe_identiche"] = s.Identical, ["righe_solo_identificativi_casuali"] = s.Random, ["righe_con_differenze_ammesse"] = s.Admitted,
            ["righe_con_differenze_classificate"] = s.Classified, ["righe_con_differenze_non_classificate"] = s.Unclassified,
            ["differenze_per_classe"] = Counts(s.Classes), ["differenze_per_categoria"] = Counts(s.Categories), ["righe_per_categoria"] = Counts(s.CategoryRows)
        };
    }

    static JsonObject Counts(IEnumerable<KeyValuePair<string, int>> counts)
    {
        var o = new JsonObject(); foreach (var (k, v) in counts) o[k] = v; return o;
    }

    static JsonObject Node(DenseDifference d) => new()
    {
        ["file"] = d.File, ["riga"] = d.RowIndex, ["chiave"] = d.Row, ["campo"] = d.Field, ["classe"] = d.Class, ["ammessa"] = d.Admitted,
        ["a"] = d.A is null ? null : Clip(d.A), ["b"] = d.B is null ? null : Clip(d.B), ["delta"] = d.Delta is double x ? JsonValue.Create(x) : null,
        ["tolleranza"] = d.Allowance is double t ? JsonValue.Create(t) : null, ["grandezza"] = d.Quantity
    };

    JsonObject Report(string reference, string candidate)
    {
        var expected = new JsonArray(set.Expected.Select(e =>
        {
            var node = (JsonObject)e.Node.DeepClone();
            bool ok = e.Matched > 0 && (e.ExpectedRows is not int r || e.Rows.Count == r) && (e.ExpectedDifferences is not int c || e.Matched == c);
            node["trovate_differenze"] = e.Matched; node["trovate_righe"] = e.Rows.Count;
            node["esito"] = !compared.Any(e.File.IsMatch) ? "non applicabile (file non confrontato)" : ok ? "ok" : "non conforme";
            node["esempi"] = new JsonArray(e.Examples.Select(x => (JsonNode)Node(x)).ToArray());
            return (JsonNode)node;
        }).ToArray());
        return new JsonObject
        {
            ["strumento"] = "ANTHEA.Testing compare-dense",
            ["confronto"] = set.Name, ["descrizione"] = set.Description,
            ["riferimento"] = reference, ["candidato"] = candidate,
            ["classificazione"] = classification.Source, ["tolleranze"] = tolerances.Source, ["prefisso_tolleranze"] = set.TolerancePrefix,
            ["esito"] = Passed ? "differenze tutte ammesse o classificate" : "differenze non classificate o attese non conformi",
            ["totali"] = new JsonObject
            {
                ["file_confrontati"] = filesCompared, ["righe_confrontate"] = rowsCompared, ["righe_escluse_commento"] = linesComment, ["righe_escluse_header"] = linesHeader,
                ["righe_identiche"] = rowsIdentical, ["righe_solo_identificativi_casuali"] = rowsRandom, ["righe_con_differenze_ammesse"] = rowsAdmitted,
                ["righe_con_differenze_classificate"] = rowsClassified, ["righe_con_differenze_non_classificate"] = rowsUnclassified,
                ["differenze_non_classificate"] = UnclassifiedCount,
                ["differenze_per_classe"] = Counts(byClass), ["differenze_classificate_per_categoria"] = Counts(byCategory),
                ["righe_per_categoria"] = Counts(rowsByCategory), ["identificativi_casuali"] = Counts(randomReasons)
            },
            ["problemi"] = new JsonArray(problems.Select(p => (JsonNode)p).ToArray()),
            ["file_non_confrontati"] = new JsonArray(notCompared.Select(p => (JsonNode)p).ToArray()),
            ["file"] = fileReports.DeepClone(),
            ["differenze_attese"] = expected,
            ["differenze_non_classificate"] = new JsonArray(unclassified.Select(d => (JsonNode)Node(d)).ToArray()),
            ["dettagli_troncati"] = UnclassifiedCount > unclassified.Count
        };
    }

    public static void Write(JsonObject report, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, report.ToJsonString(JsonOut) + "\n", new UTF8Encoding(false));
    }

    public static void Print(JsonObject report, int max)
    {
        var t = report["totali"]!;
        Console.WriteLine($"Confronto denso '{report["confronto"]}': {report["riferimento"]} -> {report["candidato"]}");
        Console.WriteLine($"File {t["file_confrontati"]}, righe confrontate {t["righe_confrontate"]} (identiche {t["righe_identiche"]}, solo identificativi casuali {t["righe_solo_identificativi_casuali"]}, " +
            $"ammesse {t["righe_con_differenze_ammesse"]}, classificate {t["righe_con_differenze_classificate"]}, non classificate {t["righe_con_differenze_non_classificate"]}); " +
            $"escluse: {t["righe_escluse_commento"]} '#', {t["righe_escluse_header"]} header.");
        Console.WriteLine($"Esito: {report["esito"]}");
        foreach (var f in report["file"]!.AsArray())
        {
            var classes = string.Join(", ", f!["differenze_per_classe"]!.AsObject().Select(p => $"{p.Key} {p.Value}"));
            var categories = string.Join(", ", f["righe_per_categoria"]!.AsObject().Select(p => $"{p.Key} {p.Value}"));
            Console.WriteLine($"  {f["file"],-30} righe {f["righe_confrontate"],6}  identiche {f["righe_identiche"],6}  id casuali {f["righe_solo_identificativi_casuali"],5}  " +
                $"classificate {f["righe_con_differenze_classificate"],4}  non classificate {f["righe_con_differenze_non_classificate"],4}" +
                (classes.Length > 0 ? "  [" + classes + "]" : "") + (categories.Length > 0 ? "  righe per categoria: " + categories : ""));
        }
        foreach (var e in report["differenze_attese"]!.AsArray())
            Console.WriteLine($"  attesa {e!["id"]} ({e["categoria"]}): {e["trovate_differenze"]} differenze in {e["trovate_righe"]} righe, {e["esito"]}");
        foreach (var p in report["problemi"]!.AsArray()) Console.WriteLine("  PROBLEMA " + p);
        if (report["file_non_confrontati"]!.AsArray().Count > 0) Console.WriteLine("  non confrontati: " + string.Join(", ", report["file_non_confrontati"]!.AsArray()));
        int shown = 0;
        foreach (var d in report["differenze_non_classificate"]!.AsArray())
        {
            if (shown++ >= max) { Console.WriteLine("  …"); break; }
            Console.WriteLine($"  NON CLASSIFICATA [{d!["classe"]}] {d["file"]} riga {d["riga"]} ({d["chiave"]}) {d["campo"]}: {d["a"]} -> {d["b"]}");
        }
    }
}
