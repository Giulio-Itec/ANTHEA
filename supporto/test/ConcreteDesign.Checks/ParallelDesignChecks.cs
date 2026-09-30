using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

static class ParallelDesignChecks
{
    public static int Run(JsonObject data, ConcreteDesignOptions options)
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        string Signature(ConcreteDesignResult r) => JsonSerializer.Serialize(new
        {
            Candidates = r.Candidates.Select(c => new { c.Id, Input = c.Input.ToJsonString(), c.Metrics, c.Checks, c.Score, c.Pareto }),
            Exclusions = r.Exclusions.OrderBy(e => e.Key), Examples = r.ExclusionExamples.OrderBy(e => e.Key), r.Evaluated, r.Completed
        });
        string original = data.ToJsonString();
        var sequential = ConcreteReinforcementDesign.Optimize(data, options with { MaxParallelism = 1 });
        Check(sequential.Candidates.Count > 0 && sequential.Completed, "Parallel baseline has no complete solutions");
        foreach (int workers in new[] { 2, 4, 8, 4 })
        {
            int previous = 0, preliminary = 0; var stages = new HashSet<string>();
            var r = ConcreteReinforcementDesign.Optimize(data, options with { MaxParallelism = workers }, progress: new Callback<ConcreteDesignProgress>(p =>
            {
                if (p.Evaluated < previous || p.Accepted > p.Evaluated || p.Evaluated > p.Planned || p.PreliminaryChecked < preliminary || p.PreliminaryChecked > p.Planned)
                    throw new Exception("Invalid parallel progress");
                previous = p.Evaluated; preliminary = p.PreliminaryChecked; stages.Add(p.Phase);
            }));
            Check(Signature(r) == Signature(sequential), "Parallel search altered identities, checks, ranking or rejection examples");
            Check(r.Performance!.Workers == Math.Min(workers, Environment.ProcessorCount), "Requested worker bound not applied");
            Check(r.Performance.LongitudinalReuses > 0 && r.Performance.LongitudinalCalculations < r.Evaluated, "Spacing reuse lost");
            Check(r.Performance.ProbeSections > 0 && stages.Any(s => s.Contains("estremi")), "No extreme/intermediate probes");
            Check(r.Candidates.Count + r.Exclusions.Values.Sum() == r.Evaluated, "Candidates lost or counted twice");
        }
        Check(original == data.ToJsonString(), "Parallel run mutated source");
        using (var stop = new CancellationTokenSource())
        {
            var r = ConcreteReinforcementDesign.Optimize(data, options, stop.Token, new Callback<ConcreteDesignProgress>(p =>
            { if (p.Phase.StartsWith("Prove iniziali")) stop.Cancel(); }));
            Check(!r.Completed && r.Evaluated < r.Planned && r.Performance!.LongitudinalCalculations == 0, "Cancellation before native work failed");
        }
        using (var stop = new CancellationTokenSource())
        {
            var r = ConcreteReinforcementDesign.Optimize(data, options, stop.Token, new Callback<ConcreteDesignProgress>(p =>
            { if (p.Accepted > 0) stop.Cancel(); }));
            Check(!r.Completed && r.Candidates.Count > 0 && r.Candidates.All(c => c.Checks.All(x => x.Passed != false)), "Cancellation lost valid completed solutions");
            Check(r.Candidates.Count + r.Exclusions.Values.Sum() == r.Evaluated, "Cancelled accounting includes unfinished sections");
        }
        var weak = (JsonObject)data.DeepClone(); weak["workspace_ca"]!["ancoraggi"]!["lunghezza"] = "50";
        var anchor = ConcreteReinforcementDesign.Optimize(weak, options);
        Check(anchor.Completed && anchor.Candidates.Count == 0 && anchor.Performance!.LongitudinalCalculations == 0, "Anchorage did not stop before SLU/SLV");
        var invalidOptions = options with { MaxParallelism = 9 };
        try { invalidOptions.Validate(); throw new Exception("Invalid parallel limit accepted"); }
        catch (ArgumentException) { checks++; }
        var ws = SectionWorkspace.Prepare((JsonObject)data.DeepClone()); var saved = ConcreteReinforcementDesign.Prepare(ws);
        saved["parallelismo"] = "2";
        Check(ConcreteDesignOptions.Read(saved).MaxParallelism == 2, "Parallel choice not persisted/read");
        saved["parallelismo"] = "Automatico";
        Check(ConcreteDesignOptions.Read(saved).Workers <= 4, "Automatic parallelism unbounded");
        return checks;
    }
}
