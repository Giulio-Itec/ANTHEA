using System.IO;
using System.Text.Json.Nodes;

// Compatibility audit of the diagnostic catalogue: its progressive IDs/order change when
// formerly accepted invalid inputs are rejected. Source captures are read only.
internal static class CatalogAudit
{
    private static IEnumerable<Dictionary<string, string>> Csv(string path)
    {
        var lines = File.ReadLines(path).Where(x => !x.StartsWith('#')).ToArray();
        var names = lines[0].Split(';');
        return lines.Skip(1).Select(line => names.Zip(line.Split(';')).ToDictionary(x => x.First, x => x.Second));
    }
    private static string Decode(string text) => Uri.UnescapeDataString(text);
    private static string Key(string kind, string name, string unit) => string.Join("\t", kind, name, unit);
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(string reference, string candidate)
    {
        var before = Csv(Path.Combine(reference, "detailing-texts-legacy.csv")).ToArray();
        var after = Csv(Path.Combine(candidate, "detailing-texts-legacy.csv")).ToArray();
        string Metadata(Dictionary<string, string> row) => string.Join("\t", new[] { "kind", "name", "unit", "reference", "explanation" }.Select(k => row[k]));
        Require(before.Select(Metadata).Order().SequenceEqual(after.Select(Metadata).Order()), "Il catalogo ha cambiato testi, unità o riferimenti.");
        string[] columns = ["occurrences", "passed", "failed", "pending"];
        Dictionary<string, int[]> Counts(IEnumerable<Dictionary<string, string>> rows) => rows
            .GroupBy(r => Key(r["kind"], Decode(r["name"]), Decode(r["unit"])))
            .ToDictionary(g => g.Key, g => columns.Select(c => g.Sum(r => int.Parse(r[c]))).ToArray());
        var expected = Counts(before); var actual = Counts(after);
        void Remove(string name, string unit, string passed)
        {
            var counts = expected[Key("Beam", name, unit)];
            counts[0]--; counts[passed.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : passed.Equals("false", StringComparison.OrdinalIgnoreCase) ? 2 : 3]--;
        }
        var direct = Csv(Path.Combine(reference, "detailing-plate-legacy.csv"))
            .Where(r => r["kind"] == "Beam" && r["legs"] == "-2" && r["stirrups"] == "True" && r["outcome"] == "ok").ToArray();
        Require(direct.Length == 3, "Attesi tre casi diretti con rami negativi, riferimento diverso.");
        foreach (var row in direct)
            foreach (var text in row["checks"].Split('|'))
            {
                var cells = text.Split(':'); Require(cells.Length == 5, "Schema checks inatteso.");
                Remove(Decode(cells[0]), Decode(cells[3]), cells[4]);
            }
        var sheets = File.ReadLines(Path.Combine(reference, "detailing-adapter-legacy.jsonl"))
            .Where(x => !x.StartsWith('#') && !string.IsNullOrWhiteSpace(x)).Select(x => JsonNode.Parse(x)!).Where(x => x["header"] is null).Where(x =>
                x["name"]!.GetValue<string>().StartsWith("Trave/") && x["input"]!["rami_y"]!.GetValue<string>() == "-2" && x["detailing"]!["Checks"] is JsonArray).ToArray();
        Require(sheets.Length == 2, "Attesi due fogli con rami negativi, riferimento diverso.");
        foreach (var row in sheets)
            foreach (var check in row["detailing"]!["Checks"]!.AsArray())
                Remove(check!["Name"]!.GetValue<string>(), check["Unit"]!.GetValue<string>(), check["Passed"]?.ToString() ?? "");
        Require(expected.Keys.Order().SequenceEqual(actual.Keys.Order()), "Chiavi del catalogo diverse.");
        foreach (var (key, counts) in expected)
            Require(counts.SequenceEqual(actual[key]), "Conteggi del catalogo inattesi: " + key);
        foreach (var row in after)
            Require(int.Parse(row["occurrences"]) == columns.Skip(1).Sum(c => int.Parse(row[c])), "Conteggio esiti incoerente.");
        Console.WriteLine($"PASS catalogo: {before.Length} testi, unità e riferimenti invariati; sottratti soltanto i 5 casi rifiutati per rami negativi; ID progressivi e ordine non significativi.");
    }
}
