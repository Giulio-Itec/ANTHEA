using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

static class SearchCoverageChecks
{
    public static int Run(string folder)
    {
        int assertions = 0; var watch = Stopwatch.StartNew();
        void Check(bool passed, string message) { if (!passed) throw new Exception(message); assertions++; }
        var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data);
        ws["dettagli_costruttivi"]!["elemento"] = "Trave"; ws["sle_comuni"]!["esposizione"] = "XC1";
        // Existing sheets already saved the old 2000 default: do not require manual reset to benefit from the fix.
        ws["calcola_armature"] = J.Obj(("max_tentativi", "2000"));
        var saved = ConcreteReinforcementDesign.Prepare(ws);
        Check(saved.B("ricerca_completa"), "Old sheets did not adopt full search");
        Check(saved.S("max_tentativi") == "2000", "Saved user budget was destroyed");
        var defaults = ConcreteDesignOptions.Read(saved);
        Check(defaults.MaxEvaluations == 100000, "Full-search choice still uses 2000 cap");
        var full = ConcreteReinforcementDesign.Optimize(data, defaults);
        Check(full.Completed && full.Evaluated == 4608, "Default grid not exhausted");
        Check(full.Candidates.Count > 0, "Regression: ordinary default SLU/SLV/SLE finds no configuration");
        saved["ricerca_completa"] = false;
        var budgeted = ConcreteDesignOptions.Read(saved);
        Check(budgeted.MaxEvaluations == 2000, "Explicit budget ignored");
        var sample = ConcreteReinforcementDesign.Optimize(data, budgeted);
        Check(!sample.Completed && sample.Evaluated == 2000, "Partial search misreported");
        Check(sample.Candidates.Count > 0, "Limited search still samples only under-reinforced layouts");
        Check(sample.Candidates.All(c => full.Candidates.Any(f => JsonNode.DeepEquals(f.Input, c.Input))), "Sampling invented admissible layouts");
        Check(full.Candidates[0].Score <= sample.Candidates[0].Score, "Full search lost the sampled optimum");
        Check(full.ExclusionExamples.Values.Any(c => c.Key == "sigma_c_rara" && c.Ratio > c.Target && c.Detail.Contains("MPa")), "Missing actionable rejection details");
        // A strongly loaded beam also had 0 accepted in the lightest prefix, despite 306 in the grid.
        foreach (string set in SectionWorkspace.Sets.Skip(1)) data["combinazioni"]![set] = new JsonArray();
        data["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("0", "750", "150");
        var beam = ConcreteReinforcementDesign.Optimize(data, new ConcreteDesignOptions { MaxEvaluations = 32 });
        Check(beam.Candidates.Count > 0 && !beam.Completed, "Heavy feasible layouts absent from a small bounded search");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "search-coverage.json"), JsonSerializer.Serialize(new
        {
            assertions, seconds = watch.Elapsed.TotalSeconds,
            full = new { full.Evaluated, full.Planned, full.Completed, accepted = full.Candidates.Count, best = full.Candidates.FirstOrDefault()?.Description },
            sample = new { sample.Evaluated, sample.Planned, sample.Completed, accepted = sample.Candidates.Count },
            beam = new { beam.Evaluated, beam.Planned, beam.Completed, accepted = beam.Candidates.Count }
        }, new JsonSerializerOptions { WriteIndented = true }));
        return assertions;
    }
}
