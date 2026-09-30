using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

static class DesignPerformanceChecks
{
    public static void Run(string folder, string label, int workers = 0)
    {
        Directory.CreateDirectory(folder);
        var reports = new List<object>();
        foreach (string scenario in new[] { "all-checks", "loaded-beam", "short-anchorage", "high-shear" })
        {
            var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data);
            ws["dettagli_costruttivi"]!["elemento"] = "Trave"; ws["sle_comuni"]!["esposizione"] = "XC1";
            if (scenario == "loaded-beam")
            {
                foreach (string set in SectionWorkspace.Sets.Skip(1)) data["combinazioni"]![set] = new JsonArray();
                data["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("0", "750", "150");
            }
            if (scenario == "short-anchorage") ws["ancoraggi"]!["lunghezza"] = "100";
            if (scenario == "high-shear") ws["taglio"]!["azioni"] = new JsonArray(J.Obj(("nome", "Taglio elevato"), ("N", "0"), ("Mx", "0"), ("My", "0"), ("Vx", "9000"), ("Vy", "9000"), ("T", "0")));
            var watch = Stopwatch.StartNew(); double? first = null;
            var result = ConcreteReinforcementDesign.Optimize(data, new ConcreteDesignOptions { MaxParallelism = workers }, progress: new Callback<ConcreteDesignProgress>(p =>
            { if (p.Accepted > 0) first ??= watch.Elapsed.TotalSeconds; }));
            double elapsed = watch.Elapsed.TotalSeconds;
            var signature = string.Join("\n", result.Candidates.OrderBy(c => c.Input.ToJsonString()).Select(c => c.Input.ToJsonString() + "|" + c.Metrics + "|" +
                string.Join(";", c.Checks.OrderBy(c => c.Key).ThenBy(c => c.Combination).ThenBy(c => c.Name).Select(c => $"{c.Key}|{c.Name}|{c.Combination}|{c.Ratio:R}|{c.Passed}"))));
            var report = new { scenario, elapsed, first, result.Evaluated, result.Planned, result.Completed, accepted = result.Candidates.Count,
                best = result.Candidates.FirstOrDefault()?.Description, checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))), result.Performance };
            if (!result.Completed || result.Evaluated != 4608 || result.Candidates.Count + result.Exclusions.Values.Sum() != result.Evaluated)
                throw new Exception("Incomplete or inconsistent benchmark: " + scenario);
            if (label != "before")
            {
                var baseline = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "performance-before.json")))!["results"]!.AsArray()
                    .Single(r => r!["scenario"]!.GetValue<string>() == scenario)!;
                if (baseline["checksum"]!.GetValue<string>() != report.checksum) throw new Exception("Candidate/check regression: " + scenario);
            }
            if (scenario is "short-anchorage" or "high-shear" && result.Performance!.LongitudinalCalculations != 0)
                throw new Exception("Resistance evaluated despite failed preliminary checks: " + scenario);
            reports.Add(report); Console.WriteLine(JsonSerializer.Serialize(report));
        }
        File.WriteAllText(Path.Combine(folder, "performance-" + label + ".json"), JsonSerializer.Serialize(new { Environment.ProcessorCount, workers, results = reports }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
