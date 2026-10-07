using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Anthea.Testing.Comparison;

/// <summary>
/// Versioned classification of the dense comparisons (tests/ANTHEA.Testing/f2-classificazione.json, refactoring F2.1). Every named
/// comparison ('confronti') declares its files (format, key, SHA-256 of the reference), the random identifiers normalised before the
/// comparison, the prefix of its tolerance rules in tolerances.json and the expected differences, each with category and reason.
/// 'compare-dense' exits with 0 only if every difference not admitted by the tolerances matches an expected difference and every
/// expected difference is found with the declared counts.
/// </summary>
public sealed class DenseClassification
{
    public string Source { get; }
    public IReadOnlyDictionary<string, string> Categories { get; }
    public IReadOnlyDictionary<string, DenseSet> Sets { get; }

    DenseClassification(string source, IReadOnlyDictionary<string, string> categories, IReadOnlyDictionary<string, DenseSet> sets)
    {
        Source = source; Categories = categories; Sets = sets;
    }

    public static DenseClassification Load(string? path)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, "f2-classificazione.json");
        var root = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!.AsObject();
        if (root["schema"]?.GetValue<int>() != 1) throw new ArgumentException("Schema della classificazione non supportato: " + path);
        var categories = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, node) in root["categorie"] as JsonObject ?? new JsonObject()) categories[name] = node?.GetValue<string>() ?? "";
        if (categories.Count == 0) throw new ArgumentException("Classificazione senza categorie: " + path);
        var sets = new Dictionary<string, DenseSet>(StringComparer.Ordinal);
        foreach (var (name, node) in root["confronti"] as JsonObject ?? new JsonObject()) sets[name] = DenseSet.Parse(name, node as JsonObject ?? new JsonObject(), categories);
        return new DenseClassification(path, categories, sets);
    }

    internal static Regex? Pattern(JsonNode? node, string key) =>
        node?[key]?.GetValue<string>() is { Length: > 0 } p ? new Regex(p, RegexOptions.CultureInvariant) : null;

    internal static string Required(JsonNode? node, string key, string where) =>
        node?[key]?.GetValue<string>() is { Length: > 0 } v ? v : throw new ArgumentException($"{where}: manca '{key}'.");
}

/// <summary>A named comparison of the classification file.</summary>
public sealed class DenseSet
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string TolerancePrefix { get; init; }
    public bool ReferenceFilesOnly { get; init; }
    public IReadOnlyList<Regex> ExcludedFiles { get; init; } = [];
    public IReadOnlyList<DenseFileSpec> Files { get; init; } = [];
    public IReadOnlyList<RandomIdentifiers> RandomIds { get; init; } = [];
    public IReadOnlyList<ExpectedDifference> Expected { get; init; } = [];

    public DenseFileSpec? Spec(string file) => Files.FirstOrDefault(f => f.Name == file);

    internal static DenseSet Parse(string name, JsonObject node, IReadOnlyDictionary<string, string> categories)
    {
        string where = "confronto " + name;
        var expected = new List<ExpectedDifference>();
        foreach (var e in node["differenze_attese"] as JsonArray ?? [])
        {
            string id = DenseClassification.Required(e, "id", where);
            string category = DenseClassification.Required(e, "categoria", where + " / " + id);
            if (!categories.ContainsKey(category)) throw new ArgumentException($"{where} / {id}: categoria non dichiarata '{category}'.");
            if (expected.Any(x => x.Id == id)) throw new ArgumentException($"{where}: id ripetuto '{id}'.");
            expected.Add(new ExpectedDifference
            {
                Id = id, Category = category, Reason = DenseClassification.Required(e, "motivo", where + " / " + id),
                File = DenseClassification.Pattern(e, "file") ?? throw new ArgumentException($"{where} / {id}: manca 'file'."),
                Row = DenseClassification.Pattern(e, "righe"), Field = DenseClassification.Pattern(e, "campo"), Class = DenseClassification.Pattern(e, "classe"),
                A = DenseClassification.Pattern(e, "a"), B = DenseClassification.Pattern(e, "b"),
                ExpectedRows = e?["righe_attese"]?.GetValue<int>(), ExpectedDifferences = e?["differenze_attese"]?.GetValue<int>(),
                Node = (JsonObject)e!.DeepClone()
            });
        }
        return new DenseSet
        {
            Name = name,
            Description = node["descrizione"]?.GetValue<string>() ?? "",
            TolerancePrefix = DenseClassification.Required(node, "prefisso_tolleranze", where),
            ReferenceFilesOnly = node["solo_file_del_riferimento"]?.GetValue<bool>() ?? false,
            ExcludedFiles = (node["file_esclusi"] as JsonArray ?? []).Select(n => new Regex(n!.GetValue<string>(), RegexOptions.CultureInvariant)).ToArray(),
            Files = (node["file"] as JsonArray ?? []).Select(f => new DenseFileSpec(
                DenseClassification.Required(f, "nome", where), f?["intestazione"]?.GetValue<bool>(), f?["colonne"]?.GetValue<string>(),
                f?["chiave"]?.GetValue<string>(), f?["sha256"]?.GetValue<string>(), f?["righe_dati"]?.GetValue<int>(), f?["test"]?.GetValue<string>())).ToArray(),
            RandomIds = (node["identificativi_casuali"] as JsonArray ?? []).Select(r => new RandomIdentifiers(
                DenseClassification.Pattern(r, "file") ?? throw new ArgumentException(where + ": identificativi casuali senza 'file'."),
                DenseClassification.Required(r, "forma", where) switch { "guid" => RandomIdForm.Guid, "hex32" => RandomIdForm.Hex32, var f => throw new ArgumentException($"{where}: forma sconosciuta '{f}'.") },
                DenseClassification.Required(r, "motivo", where))).ToArray(),
            Expected = expected
        };
    }
}

/// <summary>A file of a comparison. Header null: detected (a first line without numeric cells). Columns: names of a headerless CSV.
/// Key: column (CSV) or property (JSONL) naming the rows in the report and in the expected differences; the row number otherwise.</summary>
public sealed record DenseFileSpec(string Name, bool? Header, string? Columns, string? Key, string? Sha256, int? DataRows, string? Test);

public enum RandomIdForm { Guid, Hex32 }

/// <summary>Identifiers generated at every run (documented in supporto/test/CheckerMigration.Capture/README.md), replaced by their order of appearance in the file.</summary>
public sealed record RandomIdentifiers(Regex File, RandomIdForm Form, string Reason);

/// <summary>An expected difference: regular expressions on file, row key, field (CSV column, JSON path, 'testo'), class and the two values.</summary>
public sealed class ExpectedDifference
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Reason { get; init; }
    public required Regex File { get; init; }
    public Regex? Row { get; init; }
    public Regex? Field { get; init; }
    public Regex? Class { get; init; }
    public Regex? A { get; init; }
    public Regex? B { get; init; }
    public int? ExpectedRows { get; init; }
    public int? ExpectedDifferences { get; init; }
    public required JsonObject Node { get; init; }

    public int Matched { get; set; }
    public HashSet<string> Rows { get; } = new(StringComparer.Ordinal);
    public List<DenseDifference> Examples { get; } = [];

    public bool Matches(DenseDifference d) =>
        File.IsMatch(d.File) && (Row is null || Row.IsMatch(d.Row)) && (Field is null || Field.IsMatch(d.Field)) && (Class is null || Class.IsMatch(d.Class))
        && (A is null || A.IsMatch(d.A ?? "")) && (B is null || B.IsMatch(d.B ?? ""));
}
