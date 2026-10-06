using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Anthea.Testing.Comparison;

/// <summary>Tolerance of a physical quantity: |a − b| ≤ Abs + Rel·max(|a|, |b|). Never max(1, |x|): the absolute part has its own scale.</summary>
public sealed record Quantity(string Name, double Abs, double Rel, string Unit)
{
    public double Allowance(double a, double b) => Abs + Rel * Math.Max(Math.Abs(a), Math.Abs(b));
}

/// <summary>
/// The versioned tolerance policy (tests/ANTHEA.Testing/tolerances.json). Rules map a property name (or a CSV column) or a path to a
/// quantity; the first matching rule wins; without a rule the quantity is 'esatta' (exact equality, default of the pure refactorings).
/// Volatile entries are excluded from the comparison and reported with their reason; excluded files are not compared at all.
/// </summary>
public sealed class Tolerances
{
    sealed record Rule(Regex? Key, Regex? Path, Quantity Quantity);
    public sealed record Volatile(Regex? Key, Regex? Path, string Reason);

    readonly List<Rule> rules = [];
    public IReadOnlyDictionary<string, Quantity> Quantities { get; }
    public IReadOnlyList<Volatile> VolatileEntries { get; }
    public IReadOnlyList<Regex> ExcludedFiles { get; }
    public Quantity Exact { get; }
    public string Source { get; }

    Tolerances(string source, JsonObject root)
    {
        Source = source;
        var quantities = new Dictionary<string, Quantity>(StringComparer.Ordinal);
        foreach (var (name, node) in root["grandezze"] as JsonObject ?? new JsonObject())
        {
            double abs = Number(node?["abs"]), rel = Number(node?["rel"]);
            if (abs < 0 || rel < 0 || !double.IsFinite(abs) || !double.IsFinite(rel)) throw new ArgumentException($"Tolleranza non valida per {name}.");
            quantities[name] = new Quantity(name, abs, rel, node?["unita"]?.GetValue<string>() ?? "");
        }
        if (!quantities.ContainsKey("esatta")) quantities["esatta"] = new Quantity("esatta", 0, 0, "");
        Quantities = quantities; Exact = quantities["esatta"];
        foreach (var node in root["regole"] as JsonArray ?? [])
        {
            string quantity = node?["grandezza"]?.GetValue<string>() ?? throw new ArgumentException("Regola senza grandezza.");
            if (!quantities.TryGetValue(quantity, out var q)) throw new ArgumentException("Grandezza non dichiarata: " + quantity);
            rules.Add(new Rule(Pattern(node, "chiave"), Pattern(node, "percorso"), q));
        }
        VolatileEntries = (root["volatili"] as JsonArray ?? []).Select(n => new Volatile(Pattern(n, "chiave"), Pattern(n, "percorso"),
            n?["motivo"]?.GetValue<string>() ?? throw new ArgumentException("Voce volatile senza motivo."))).ToArray();
        ExcludedFiles = (root["file_esclusi"] as JsonArray ?? []).Select(n => new Regex(n!.GetValue<string>(), RegexOptions.CultureInvariant)).ToArray();
    }

    static double Number(JsonNode? node) => node is null ? 0 : node.GetValue<double>();
    static Regex? Pattern(JsonNode? node, string key) => node?[key]?.GetValue<string>() is { Length: > 0 } p ? new Regex(p, RegexOptions.CultureInvariant) : null;

    public static Tolerances Load(string? path)
    {
        path ??= System.IO.Path.Combine(AppContext.BaseDirectory, "tolerances.json");
        var root = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!.AsObject();
        if (root["schema"]?.GetValue<int>() != 1) throw new ArgumentException("Schema delle tolleranze non supportato: " + path);
        return new Tolerances(path, root);
    }

    /// <summary>The quantity of a value: <paramref name="key"/> is the property name or the CSV column, <paramref name="path"/> "file:json-path".</summary>
    public Quantity For(string key, string path)
    {
        foreach (var rule in rules)
            if ((rule.Key is null || rule.Key.IsMatch(key)) && (rule.Path is null || rule.Path.IsMatch(path)) && (rule.Key is not null || rule.Path is not null)) return rule.Quantity;
        return Exact;
    }

    public Volatile? IsVolatile(string key, string path) =>
        VolatileEntries.FirstOrDefault(v => (v.Key is null || v.Key.IsMatch(key)) && (v.Path is null || v.Path.IsMatch(path)) && (v.Key is not null || v.Path is not null));

    public bool IsExcluded(string relativeFile) => ExcludedFiles.Any(r => r.IsMatch(relativeFile));
}
