using Anthea.Calculations;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;

internal static class BridgeDesignAnalysisChecks
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var data = BridgeConcept.Defaults();
        data["input"]!["low_carbon"] = false;
        data["input"]!["recycled"] = false;
        data["input"]!["shoulder"] = 2;
        var pinned = (JsonObject)data.DeepClone();
        pinned["input"]!["length"] = -1;
        data["alternative_a"] = pinned;
        string before = data.ToJsonString();
        var result = BridgeDesignAnalysis.Calculate(data);
        Check(before == data.ToJsonString(), "L’analisi ha modificato il documento");
        Check(result.Current.TotalCost > 0 && result.Comparison is null && result.ComparisonError is not null,
            "Il confronto invalido annulla anche il calcolo corrente");
        Check(result.Alternatives.Count > 0, "Proposte di risparmio mancanti");
        foreach (var candidate in result.Alternatives)
        {
            var alternative = (JsonObject)data.DeepClone();
            foreach (var change in candidate.InputChanges) alternative["input"]![change.Key] = change.Value!.DeepClone();
            var expected = ModuleServices.Get(BridgeConcept.Module).Calculate(alternative).NativeAs<BridgeConcept.Result>();
            Check(expected.Carbon == candidate.Result.Carbon && expected.TotalCost == candidate.Result.TotalCost,
                "Proposta diversa dalla geometria applicata");
            Check(result.Current.Carbon - expected.Carbon == candidate.CarbonSaving &&
                expected.TotalCost - result.Current.TotalCost == candidate.CostDifference, "Delta della proposta incoerente");
        }
        var snapshot = BridgeDesignAnalysis.OptimizationSnapshot(data);
        snapshot["input"]!["length"] = 10;
        Check(snapshot["alternative_a"] is null && snapshot["scene"] is null && data.ToJsonString() == before,
            "Fotografia per la ricerca condivide lo stato della vista");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { BridgeDesignAnalysis.Calculate(data, cancellation.Token); throw new InvalidOperationException("Annullamento ignorato"); }
        catch (OperationCanceledException) { checks++; }
        return checks;
    }
}
