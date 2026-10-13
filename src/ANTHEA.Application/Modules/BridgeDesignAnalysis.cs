using Anthea.Application.BridgeDesign;
using NativeDesign = Anthea.Calculations.BridgeConcept;
using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

public sealed record BridgeDesignAlternative(string Label, JsonObject InputChanges, BridgeDesignModel.Result Result,
    double CarbonSaving, double CostDifference);
public sealed record BridgeDesignAnalysis(BridgeDesignModel.Result Current, BridgeDesignModel.Result? Comparison,
    string? ComparisonError, IReadOnlyList<BridgeDesignAlternative> Alternatives)
{
    public static BridgeDesignAnalysis Calculate(JsonObject source, CancellationToken cancellation = default,
        IModuleService? mainService = null)
    {
        var data = (JsonObject)source.DeepClone();
        var standard = ModuleServices.Get(BridgeConcept.Module);
        var current = BridgeDesignModel.Read((mainService ?? standard).Calculate(data, cancellation));
        BridgeDesignModel.Result? comparison = null; string? comparisonError = null;
        if (data["alternative_a"] is JsonObject pinned)
            try { comparison = BridgeDesignModel.Read(standard.Calculate(pinned, cancellation)); }
            catch (ArgumentException ex) { comparisonError = ex.Message; }
        var alternatives = new List<BridgeDesignAlternative>();
        void Consider(string label, string key, JsonNode change, bool checkSpan = false)
        {
            cancellation.ThrowIfCancellationRequested();
            var candidate = (JsonObject)data.DeepClone(); candidate["input"]![key] = change.DeepClone();
            try
            {
                var result = BridgeDesignModel.Read(standard.Calculate(candidate, cancellation));
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
    public static BridgeDesignModel.OptimizationResult Optimize(JsonObject source, BridgeDesignModel.OptimizationOptions options,
        CancellationToken cancellation, IProgress<BridgeDesignModel.OptimizationProgress>? progress = null)
        => new(NativeDesign.Optimize(OptimizationSnapshot(source), options.ToNative(), cancellation,
            progress is null ? null : new ProgressProjection(progress)));
    // Forward through the caller's IProgress; do not capture another synchronization context.
    private sealed class ProgressProjection(IProgress<BridgeDesignModel.OptimizationProgress> target) : IProgress<NativeDesign.OptimizationProgress>
    {
        public void Report(NativeDesign.OptimizationProgress value) => target.Report(new(value));
    }
}
