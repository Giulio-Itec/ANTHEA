using Anthea.Calculations;
using System.Text.Json;
using System.Text.Json.Nodes;

// Reproducible teaching examples, not an independent structural validation.
string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
var data = BridgeConcept.Defaults(); var input = data["input"]!;
input["length"] = 120; input["spans"] = 3; input["height"] = 12;
input["soil"] = "Roccia"; input["obstacle"] = "Nessuno";
var options = new BridgeConcept.OptimizationOptions {
    KeepPier = true, KeepFoundation = true, MaxSpans = 8,
    DepthPercentMax = 120, DepthPercentStep = 10, Alternatives = 10
};
var cases = new List<object>();
void Run(string name, JsonObject source, BridgeConcept.OptimizationOptions settings)
{
    var result = BridgeConcept.Optimize(source, settings);
    var best = result.Solutions.FirstOrDefault();
    cases.Add(new { name, input = source, options = settings, baseline = result.Baseline,
        result.Evaluated, result.Admissible, distinct = result.Solutions.Length,
        result.BaselineExclusions, result.Excluded, best = best is null ? null : BridgeConcept.Calculate(best.Data),
        top = result.Solutions.Take(10).ToArray(), trials = result.Trials,
        solutions = result.Solutions.Select(s => new { s.Rank, s.Trial, s.Score, s.Pareto }).ToArray() });
}
foreach (var objective in Enum.GetValues<BridgeConcept.OptimizationObjective>())
    Run(objective.ToString(), data, options with { Objective = objective });
Run("Minimo cinque campate", data, options with { MinSpans = 5 });
Run("Altezza minima 1.5 m", data, options with { MinDepth = 1.5 });
foreach (double factor in new[] { .8, 1.2 }) {
    var changed = (JsonObject)data.DeepClone();
    foreach (var rate in BridgeConcept.Rates) changed["rates"]![rate.Key] = changed["rates"].D(rate.Key) * factor;
    Run("Tutti i prezzi x " + factor.ToString(System.Globalization.CultureInfo.InvariantCulture), changed, options);
}
var json = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, "esempi.json"), JsonSerializer.Serialize(cases, json));
File.WriteAllText(Path.Combine(output, "input-riferimento.json"), data.ToJsonString(json));
Console.WriteLine($"Salvati {cases.Count} scenari riproducibili in {output}");
