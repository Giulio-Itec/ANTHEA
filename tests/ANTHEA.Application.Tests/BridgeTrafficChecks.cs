using System.IO.Compression;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Modules;
using X.Core;

internal static class BridgeTrafficChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        var old = BridgeConcept.Defaults(); string before = old.ToJsonString();
        var original = BridgeConcept.Calculate(old);
        Check(original.Traffic is null && original.Supports.All(s => s.MinimumReaction is null), "Unmarked old document no longer legacy");
        Check(old.ToJsonString() == before, "Reading traffic model mutated input");
        ModuleSheetPreparation.PrepareBridgeDesign(old);
        Check(old["input"].S("traffic_model") == BridgeConcept.UniformLegacy && old.S("traffic_migration").Length > 0, "Legacy migration not explicit");
        Check(old["assumptions"].D("gamma_q") == 1.5, "Legacy factor overwritten");
        Check(BridgeConcept.Calculate(old).TotalCost == original.TotalCost, "Legacy document changed under preparation");
        var data = BridgeConcept.NewDocument(); var result = BridgeConcept.Calculate(data);
        Check(data["input"].S("traffic_model") == BridgeConcept.Schema1 && result.Traffic?.GammaQ == 1.35, "New document does not adopt Schema 1");
        Check(result.Stations.All(s => s.MomentMinimum.HasValue && s.ShearMinimum.HasValue && s.DeflectionMinimumMm.HasValue), "Adapter dropped envelope ordinates");
        Check(result.Supports.All(s => s.MinimumReaction.HasValue), "Adapter dropped minimum reactions");
        data["assumptions"]!["gamma_q"] = 1.42;
        Check(BridgeConcept.Calculate(data).Traffic!.GammaQ == 1.42, "Explicit factor silently replaced");
        var resultArchive = JsonNode.Parse(result.Json().ToJsonString())!;
        Check(resultArchive["bridge_design"]!["Traffic"]!.S("Model") == BridgeConcept.Schema1, "Result provenance not persisted");
        using (var zip = new ZipArchive(new MemoryStream(BridgeConceptExport.Report("Traffic", data, result))))
        using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        {
            string report = reader.ReadToEnd();
            Check(report.Contains("Schema 1 NTC 2018") && report.Contains("1,35") && report.Contains("R min G+Q"), "Report does not use completed traffic result");
            Check(!report.Contains("Esclusi: inviluppi di traffico mobile"), "Report still excludes an implemented method");
            Check(!report.Contains("1,42") && !report.Contains("1.42"), "Report reconstructed gamma from changed UI data");
        }
        Check(BridgeConceptExport.Csv(result).Contains("Schema 1 NTC 2018"), "CSV dropped traffic provenance");
        try { BridgeConcept.Calculate(data, legacy: true); throw new Exception("Diagnostic engine silently discarded Schema 1"); } catch (ArgumentException) { checks++; }
        data["input"]!["traffic_model"] = "unknown";
        try { BridgeConcept.Calculate(data); throw new Exception("Invalid traffic accepted"); } catch (ArgumentException) { checks++; }
        data["input"]!["traffic_model"] = 123;
        try { BridgeConcept.Calculate(data); throw new Exception("Numeric traffic accepted"); } catch (ArgumentException) { checks++; }
        var search = BridgeConcept.Optimize(BridgeConcept.NewDocument(), new() { KeepFamily = true, KeepPier = true, KeepFoundation = true, MinSpans = 2, MaxSpans = 6 });
        Check(search.Candidates.Length > 0 && search.Candidates.All(c => c.Data["input"].S("traffic_model") == BridgeConcept.Schema1 && c.Result.Traffic is not null), "Traffic provenance lost in search");
        return checks;
    }
}
