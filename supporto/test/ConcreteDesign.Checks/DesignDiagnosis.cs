using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

static class DesignDiagnosis
{
    public static void Run(string[] args)
    {
        string folder = args.First(a => a != "--diagnose"); Directory.CreateDirectory(folder);
        var reports = new List<object>();
        foreach (string mode in new[] { "default-all", "default-slu", "loaded-beam", "loaded-column", "narrow-beam" })
        {
            var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data);
            ws["dettagli_costruttivi"]!["elemento"] = mode == "loaded-column" ? "Pilastro" : "Trave";
            ws["sle_comuni"]!["esposizione"] = "XC1";
            if (mode != "default-all") foreach (string set in SectionWorkspace.Sets.Skip(1)) data["combinazioni"]![set] = new JsonArray();
            if (mode == "loaded-beam") data["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("0", "750", "150");
            if (mode == "loaded-column") data["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("-5500", "350", "100");
            if (mode == "narrow-beam")
            {
                data["input"]!["width_mm"] = "300"; data["input"]!["height_mm"] = "500"; data["input"]!["cover_mm"] = "35";
                data["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("0", "160", "0");
            }
            foreach (int limit in new[] { 2000, 100000 })
            {
                var watch = Stopwatch.StartNew();
                var r = ConcreteReinforcementDesign.Optimize(data, new ConcreteDesignOptions { MaxEvaluations = limit });
                var summary = new { mode, limit, r.Evaluated, r.Planned, accepted = r.Candidates.Count, r.Completed, seconds = watch.Elapsed.TotalSeconds, r.Exclusions, best = r.Candidates.FirstOrDefault()?.Description };
                reports.Add(summary); Console.WriteLine(JsonSerializer.Serialize(summary));
            }
        }
        File.WriteAllText(Path.Combine(folder, "diagnosis.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
    }
}
