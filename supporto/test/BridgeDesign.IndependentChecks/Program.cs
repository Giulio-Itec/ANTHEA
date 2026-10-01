using Anthea.Calculations;
using System.Text.Json;
using System.Text.Json.Nodes;

string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
var random = new Random(30092026); var beams = new List<object>();
for (int k = 0; k < 100; k++) {
    double[] spans = Enumerable.Range(0, 1 + k % 12).Select(_ => 5 + 65 * random.NextDouble()).ToArray();
    double q = 2 + 498 * random.NextDouble(), ei = Math.Pow(10, 5 + 4 * random.NextDouble());
    bool continuous = k % 4 != 0;
    var result = BridgeConcept.Analyze(spans, q, ei, continuous);
    beams.Add(new { spans, q, ei, continuous, result.Reactions, result.Stations });
}
var data = BridgeConcept.Defaults(); var input = data["input"]!;
input["family"] = "slab"; input["length"] = 120; input["height"] = 12;
input["lanes"] = 2; input["median"] = 0; input["spans"] = 10;
input["soil"] = "Roccia"; input["obstacle"] = "Nessuno"; input["continuous"] = false;
input["pier"] = "Colonna circolare"; input["pier_size"] = 2;
input["foundation"] = "Plinto diretto"; input["footing_size"] = 6;
var options = new BridgeConcept.OptimizationOptions {
    KeepFamily = true, KeepPier = true, KeepFoundation = true,
    MinSpans = 8, MaxSpans = 16, DepthPercentMax = 120, DepthPercentStep = 10, Alternatives = 50
};
var optimization = BridgeConcept.Optimize(data, options);
var json = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, "osservati.json"), JsonSerializer.Serialize(new {
    seed = 30092026, beams, input = data, options,
    optimization.Evaluated, optimization.Admissible, optimization.Trials,
    solutions = optimization.Solutions.Select(s => new { s.Rank, s.Score, s.Pareto, result = BridgeConcept.Calculate(s.Data) })
}, json));
Console.WriteLine($"Esportate {beams.Count} travi e {optimization.Solutions.Length} soluzioni per il verificatore Python indipendente.");
