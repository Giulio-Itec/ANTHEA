using Anthea.Calculations;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length == 2 && args[0] == "--defaults")
{
    File.WriteAllText(args[1], BridgeConcept.Defaults().ToJsonString());
    return 0;
}
if (args.Length != 3 || args[0] != "--run")
    throw new ArgumentException("--defaults output.json oppure --run inputs.jsonl results.jsonl");
using var writer = new StreamWriter(args[2], false, new System.Text.UTF8Encoding(false));
int cases = 0, failed = 0;
foreach (string line in File.ReadLines(args[1]).Where(x => !string.IsNullOrWhiteSpace(x)))
{
    var item = JsonNode.Parse(line)!.AsObject();
    string id = item["id"]!.GetValue<string>(), mode = item["mode"]!.GetValue<string>();
    var data = item["data"]!.AsObject();
    var before = data.ToJsonString();
    try
    {
        var r = BridgeConcept.Calculate(data);
        if (before != data.ToJsonString()) throw new InvalidOperationException("Input modificato dal motore");
        var result = new {
            r.Width, r.Depth, r.PierDepth, r.Slab, r.Spacing, r.Girders, r.Web, r.Bottom, r.Spans, r.Supports,
            r.Foundation, r.PileDiameter, r.PileLength, r.Concrete, r.Steel, r.Rebar, r.Prestress,
            r.Inertia, r.DirectCost, r.TotalCost, r.Carbon, r.Duration, r.Quantities, r.Details, r.Warnings
        };
        string encoded = JsonSerializer.Serialize(result, new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        bool finite = !new[] { "\"NaN\"", "\"Infinity\"", "\"-Infinity\"" }.Any(encoded.Contains);
        writer.WriteLine(JsonSerializer.Serialize(new { id, mode, status = finite ? "calculated" : "nonfinite", result = JsonNode.Parse(encoded), error = finite ? null : "Il motore ha restituito valori numerici non finiti, conservati come stringhe NaN/Infinity." }));
        if (!finite) failed++;
    }
    catch (Exception ex)
    {
        failed++;
        writer.WriteLine(JsonSerializer.Serialize(new { id, mode, status = "rejected", error = ex.Message, errorType = ex.GetType().FullName }));
    }
    cases++;
}
Console.WriteLine($"{cases} valutazioni eseguite; {cases-failed} calcolate; {failed} rifiutate (con motivazione salvata).");
return 0;
