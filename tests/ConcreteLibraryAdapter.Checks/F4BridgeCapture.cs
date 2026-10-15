using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4BridgeCapture
{
    internal static int Run(string root, string output)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        string directory = Path.Combine(root, "supporto/artefatti/refactoring/f4/bridge-before");
        string file = Path.Combine(directory, "bridge-before.json");
        if (File.Exists(file)) throw new IOException("Bridge capture already exists; do not overwrite it.");
        var cases = new JsonArray(); var timings = new JsonArray();
        foreach (var family in BridgeConcept.Families)
        {
            foreach (bool alternate in new[] { false, true })
            {
                var data = BridgeConcept.Defaults(); var i = data["input"]!;
                i["family"] = family.Id; i["spans"] = 3;
                i["length"] = (BridgeConcept.HasTowers(family.Id) ? 2 : 3) * (family.MinSpan + family.MaxSpan) / 2;
                i["obstacle"] = "Nessuno"; i["continuous"] = BridgeConcept.HasTowers(family.Id) || family.Id is not ("tied_arch" or "truss") && !alternate;
                i["low_carbon"] = alternate; i["recycled"] = alternate;
                if (alternate && BridgeConcept.HasUpperStructure(family.Id)) i["deck_type"] = BridgeConcept.DeckTypes[1];
                var options = new BridgeConcept.OptimizationOptions { KeepFamily = true, KeepFoundation = true, KeepPier = true,
                    KeepContinuity = false, MinSpans = BridgeConcept.HasTowers(family.Id) ? 3 : 2,
                    MaxSpans = BridgeConcept.HasTowers(family.Id) ? 3 : 4, Alternatives = 5 };
                Add(family.Id + (alternate ? "-alternate" : "-base"), data, alternate ? null : options);
            }
        }
        foreach (var objective in Enum.GetValues<BridgeConcept.OptimizationObjective>())
            Add("search-" + objective, BridgeConcept.Defaults(), new() { Objective = objective, KeepFoundation = true, KeepPier = true,
                MinSpans = 2, MaxSpans = 5, Alternatives = 8 });
        Directory.CreateDirectory(directory);
        File.WriteAllText(file, new JsonObject { ["cases"] = cases, ["timings"] = timings }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"capture.txt"), $"{cases.Count} cases; {file}");
        Console.WriteLine($"Captured {cases.Count} bridge cases, including all 14 families and 17 optimization grids.");return 0;

        void Add(string id, JsonObject data, BridgeConcept.OptimizationOptions? options)
        {
            string before = data.ToJsonString(); var watch = Stopwatch.StartNew(); var calculation = Calculate(data);
            double calculationMs = watch.Elapsed.TotalMilliseconds;watch.Restart();
            JsonNode? optimized = options is null ? null : JsonSerializer.SerializeToNode(BridgeConcept.Optimize(data, options));
            if(data.ToJsonString()!=before)throw new InvalidOperationException(id+": caller input changed");
            cases.Add(new JsonObject { ["id"] = id, ["input"] = data, ["options"] = JsonSerializer.SerializeToNode(options),
                ["calculation"] = calculation, ["optimization"] = optimized });
            timings.Add(new JsonObject { ["id"] = id, ["calculationMs"] = calculationMs, ["optimizationMs"] = watch.Elapsed.TotalMilliseconds });
        }
    }
    internal static JsonNode Calculate(JsonObject data)
    {
        var result=BridgeConcept.Calculate(data);
        return new JsonObject { ["result"] = JsonSerializer.SerializeToNode(result),
            ["technical"] = JsonSerializer.SerializeToNode(BridgeConcept.TechnicalSchedule(data,result)),
            ["supports"] = JsonSerializer.SerializeToNode(BridgeConcept.SupportSchedule(data,result)) };
    }
}
