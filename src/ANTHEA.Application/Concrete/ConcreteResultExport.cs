using System.Text.Json.Nodes;
namespace Anthea.Application.Concrete;

/// <summary>The historical interactive/export contract. Independent of the smaller headless calculation contract.</summary>
public static class ConcreteResultExport
{
    public static JsonObject Create(JsonObject Data, IReadOnlyDictionary<string, VerificationSummary> summaries,
        IReadOnlyDictionary<string, Dictionary<string, DomainCheck>> domainResults,
        IReadOnlyDictionary<string, Dictionary<string, StressOutcome>> stressResults,
        IReadOnlyDictionary<string, Ntc2018Checks.ShearResult[]> shearResults,
        IReadOnlyDictionary<string, TorsionResult> torsionResults, IReadOnlyList<DetailingCheck> detailingResults,
        string detailsText, AnchorageResult? anchorageResult, string anchorageText, MomentCurvatureResult? curvatureResult,
        IReadOnlyDictionary<string, string> calculationErrors)
    {
        var domains = new JsonObject(); foreach (var (key, rows) in domainResults) domains[key] = J.Node(rows);
        var stresses = new JsonObject();
        foreach (var (key, rows) in stressResults) stresses[key] = ConcreteAnalysisSession.ExportStress(rows);
        // Attach freshly created nodes directly: J.Obj would deep-clone them again.
        return new JsonObject
        {
            ["errore"] = "", ["motore"] = "GPCChecker.Concrete.dll",
            ["normativa_riferimento"] = Data["workspace_ca"].S("normativa"), ["verifica_normativa_completa"] = false,
            ["riepilogo_verifiche"] = J.Node(summaries),
            ["domini"] = domains, ["tensioni"] = stresses, ["taglio"] = J.Node(shearResults),
            ["torsione"] = J.Node(torsionResults), ["dettagli_costruttivi"] = J.Node(detailingResults), ["dettagli_esito"] = detailsText,
            ["ancoraggio"] = J.Node(anchorageResult), ["ancoraggio_esito"] = anchorageText, ["momento_curvatura"] = J.Node(curvatureResult),
            ["dati"] = Data.DeepClone(), ["errori_calcolo"] = J.Node(calculationErrors),
            ["avvisi"] = J.Node(new[] { "Compressione negativa; azioni in kN e kNm", "Taglio e fessurazione: vedere esiti specifici e limiti di applicabilità" })
        };
    }
}
