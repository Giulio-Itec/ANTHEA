using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;
namespace Anthea.Application.Concrete;

public static class ConcreteVerificationSummaries
{
    public static Dictionary<string, VerificationSummary> Create(JsonObject settings,
        IReadOnlyDictionary<string, JsonObject[]> actions,
        IReadOnlyDictionary<string, Dictionary<string, ConcreteResults.Check>> domainResults,
        IReadOnlyDictionary<string, Dictionary<string, StressOutcome>> stressResults,
        IReadOnlyList<JsonObject> shearRows, IReadOnlyDictionary<string, ConcreteResults.ShearResult[]> shearResults,
        IReadOnlyDictionary<string, ConcreteResults.TorsionResult> torsionResults)
    {
        var result = new Dictionary<string, VerificationSummary>();
        string Name(string key, string id) => actions[key].FirstOrDefault(r => r.S("id") == id) is { } row ? SectionActionInputs.ActionLabel(row) : id;
        foreach (string key in new[] { "SLU", "SLV" }) foreach (string prefix in new[] { "3D", "2D" })
            result[prefix + ":" + key] = VerificationSummary.Create(prefix + " · " + SectionActionInputs.Label(key, settings), actions[key].Length,
                domainResults.GetValueOrDefault(prefix + ":" + key)?.Select(kv => (Name(key, kv.Key), kv.Value.Utilization, (bool?)null)) ?? []);
        foreach (string key in SectionWorkspace.Sets.Skip(2))
        {
            var values = stressResults.GetValueOrDefault(key);
            if (SleCheckScope.Stress(key)) result[key + ":tensioni"] = VerificationSummary.Create(SectionActionInputs.Label(key, settings) + " · tensioni", actions[key].Length, values?.Select(kv => (Name(key, kv.Key), kv.Value.Ratio, (bool?)null)) ?? []);
            if (SleCheckScope.Cracking(key, settings)) result[key + ":fessurazione"] = VerificationSummary.Create(SectionActionInputs.Label(key, settings) + " · fessurazione", actions[key].Length, values?.Select(kv => (Name(key, kv.Key), kv.Value.CrackResult?.Ratio, kv.Value.CrackResult?.Passed)) ?? []);
        }
        for (int axis = 0; axis < 2; axis++)
        {
            int index = axis;
            result[axis == 0 ? "Taglio:x" : "Taglio:y"] = VerificationSummary.Create(axis == 0 ? "Taglio Vx" : "Taglio Vy", shearRows.Count, shearResults.Select(kv =>
                (shearRows.FirstOrDefault(r => r.S("id") == kv.Key) is { } row ? SectionActionInputs.ActionLabel(row) : kv.Key, kv.Value[index].Ratio, (bool?)null)));
        }
        int torques = shearRows.Count(r => J.Number(r["T"]) is double t && t != 0);
        if (torques > 0) result["Taglio:torsione"] = VerificationSummary.Create("Torsione e interazione V–T", torques, torsionResults.Select(kv =>
            (shearRows.FirstOrDefault(r => r.S("id") == kv.Key) is { } row ? SectionActionInputs.ActionLabel(row) : kv.Key,
                kv.Value.TorsionRatio is double et && kv.Value.ConcreteCombinedRatio is double ec && kv.Value.SteelCombinedRatio is double es ? (double?)Math.Max(et, Math.Max(ec, es)) : null, (bool?)kv.Value.Passed)));
        return result;
    }
}
