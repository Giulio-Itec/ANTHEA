using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4BridgeChecks
{
    internal static int Run(string root, string output)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f4/bridge-reference.json")))!;
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, manifest["file"]!.GetValue<string>()));
        if (Convert.ToHexString(SHA256.HashData(bytes)) != manifest["sha256"]!.GetValue<string>()) throw new InvalidOperationException("Frozen bridge reference changed.");
        var errors = new List<string>(); var stats = new Stats("F4 bridge adapter"); int calculations = 0, searches = 0, removed = 0;
        foreach (var row in JsonNode.Parse(bytes)!["cases"]!.AsArray())
        {
            string id = row!["id"]!.GetValue<string>(); var input = row["input"]!.AsObject(); string before = input.ToJsonString();
            var actual = F4BridgeCapture.Calculate(input); removed += Normalize(row["calculation"]);
            if (Normalize(actual) != 0) errors.Add(id + ": duplicate native stations");
            Compare(row["calculation"], actual, id); calculations++;
            Compare(JsonSerializer.SerializeToNode(BridgeConcept.SpanSchedule(BridgeConcept.Calculate(input))),
                JsonSerializer.SerializeToNode(LegacyBridgeConcept.LegacySpanSchedule(LegacyBridgeConcept.Calculate(input))), id + "/span-schedule");
            if (row["options"] is not null)
            {
                var options = row["options"]!.Deserialize<BridgeConcept.OptimizationOptions>()!;
                var result = JsonSerializer.SerializeToNode(BridgeConcept.Optimize(input, options));
                removed += Normalize(row["optimization"]); if (Normalize(result) != 0) errors.Add(id + ": duplicate native search stations");
                Compare(row["optimization"], result, id + "/search"); searches++;
            }
            if (input.ToJsonString() != before) errors.Add(id + ": source mutated");
        }
        if (calculations != 31 || searches != 17) errors.Add("Incomplete corpus");
        var document = BridgeConcept.Defaults(); document["archive_marker"] = "preserve";
        var optimized = BridgeConcept.Optimize(document, new() { KeepFamily = true });
        foreach (var c in optimized.Candidates)
        {
            if (c.Data.S("archive_marker") != "preserve") errors.Add("Unknown archive metadata lost");
            if (JsonNode.DeepEquals(c.Data, document) && ReferenceEquals(c.Data, document)) errors.Add("Candidate aliases source");
        }
        using var cancel = new CancellationTokenSource(); int notifications = 0;
        try { BridgeConcept.Optimize(document, new(), cancel.Token, new ProgressSink(p => { notifications++; cancel.Cancel(); })); errors.Add("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        if (notifications == 0) errors.Add("Missing progress");
        foreach (string group in new[] { "input", "rates", "assumptions" })
        {
            var bad = BridgeConcept.Defaults(); string key = bad[group]!.AsObject().First(p => J.Number(p.Value) is not null).Key;
            bad[group]![key] = "invalid";
            try { BridgeConcept.Calculate(bad); errors.Add("Malformed input accepted: " + group); } catch (ArgumentException) { }
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "comparison.json"), JsonSerializer.Serialize(new { calculations, searches, removed, stats.Numbers, stats.MaxRelative, differences = errors.Count, errors = errors.Take(100) }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(stats.Line()); Console.WriteLine($"{calculations} models, {searches} grids, {removed} redundant legacy stations, {errors.Count} differences");
        foreach (string error in errors.Take(20)) Console.Error.WriteLine(error);
        return errors.Count == 0 ? 0 : 1;
        void Compare(JsonNode? a, JsonNode? b, string path) => Json.Compare(Json.Sort(a), Json.Sort(b), path, stats, errors, 1e-10);
        int Normalize(JsonNode? node)
        {
            if (node is JsonArray list) return list.Sum(Normalize);
            if (node is not JsonObject obj) return 0;
            int count = 0;
            foreach (var pair in obj.ToArray())
            {
                if (pair.Key != "Stations" || pair.Value is not JsonArray stations) { count += Normalize(pair.Value); continue; }
                for (int k = stations.Count - 1; k > 0; k--)
                {
                    var a = stations[k - 1]!; var b = stations[k]!;
                    if (a.D("Span") != b.D("Span") || Math.Abs(a.D("X") - b.D("X")) > 1e-12 * Math.Max(1, Math.Abs(a.D("X")))) continue;
                    foreach (string key in new[] { "Moment", "Shear", "DeflectionMm" }) Compare(a[key], b[key], "redundant-station/" + key);
                    stations.RemoveAt(k); count++;
                }
            }
            return count;
        }
    }
    private sealed class ProgressSink(Action<BridgeConcept.OptimizationProgress> report) : IProgress<BridgeConcept.OptimizationProgress>
    { public void Report(BridgeConcept.OptimizationProgress value) => report(value); }
}
