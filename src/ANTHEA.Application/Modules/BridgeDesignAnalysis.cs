using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

public sealed record BridgeDesignAlternative(string Label, JsonObject InputChanges, BridgeConcept.Result Result,
    double CarbonSaving, double CostDifference);
public sealed record BridgeDesignAnalysis(BridgeConcept.Result Current, BridgeConcept.Result? Comparison,
    string? ComparisonError, IReadOnlyList<BridgeDesignAlternative> Alternatives)
{
    public static BridgeDesignAnalysis Calculate(JsonObject source, CancellationToken cancellation = default,
        IModuleService? mainService = null)
    {
        var data = (JsonObject)source.DeepClone();
        var standard = ModuleServices.Get(BridgeConcept.Module);
        var current = (mainService ?? standard).Calculate(data, cancellation).NativeAs<BridgeConcept.Result>();
        BridgeConcept.Result? comparison = null; string? comparisonError = null;
        if (data["alternative_a"] is JsonObject pinned)
            try { comparison = standard.Calculate(pinned, cancellation).NativeAs<BridgeConcept.Result>(); }
            catch (ArgumentException ex) { comparisonError = ex.Message; }
        var alternatives = new List<BridgeDesignAlternative>();
        void Consider(string label, string key, JsonNode change, bool checkSpan = false)
        {
            cancellation.ThrowIfCancellationRequested();
            var candidate = (JsonObject)data.DeepClone(); candidate["input"]![key] = change.DeepClone();
            try
            {
                var result = standard.Calculate(candidate, cancellation).NativeAs<BridgeConcept.Result>();
                if (checkSpan && result.Spans.Max() > result.Family.MaxSpan) return;
                double saving = current.Carbon - result.Carbon;
                if (saving > .01) alternatives.Add(new(label, new JsonObject { [key] = change.DeepClone() }, result, saving, result.TotalCost - current.TotalCost));
            }
            catch (ArgumentException) { /* An infeasible alternative is not presented as a saving. */ }
        }
        var input = data["input"]!;
        if (!input.B("low_carbon")) Consider("Cls a ridotta impronta", "low_carbon", JsonValue.Create(true)!);
        if (current.Steel > 0 && !input.B("recycled")) Consider("Acciaio ad alto riciclato", "recycled", JsonValue.Create(true)!);
        if (input.D("shoulder") > 1) Consider("Banchine a 1,0 m", "shoulder", JsonValue.Create(1)!);
        if (current.Spans.Length > 1) Consider("Una campata in meno", "spans", JsonValue.Create(current.Spans.Length - 1)!, checkSpan: true);
        cancellation.ThrowIfCancellationRequested();
        return new(current, comparison, comparisonError, alternatives);
    }
    public static JsonObject OptimizationSnapshot(JsonObject source)
    { var result = (JsonObject)source.DeepClone(); result.Remove("alternative_a"); result.Remove("scene"); return result; }
    public static BridgeConcept.OptimizationResult Optimize(JsonObject source, BridgeConcept.OptimizationOptions options,
        CancellationToken cancellation, IProgress<BridgeConcept.OptimizationProgress>? progress = null)
        => BridgeConcept.Optimize(OptimizationSnapshot(source), options, cancellation, progress);
}
