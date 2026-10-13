using System.Text.Json.Nodes;
namespace Anthea.Application.Concrete;

public sealed record ConcreteReportAnalysis(JsonObject Data, JsonObject Result, SezioneCA Geometry,
    IReadOnlyDictionary<string, JsonObject[]> Actions,
    IReadOnlyDictionary<string, CheckerDomain3D> Domains3D,
    IReadOnlyDictionary<string, CheckerDomain2D> Domains2D,
    IReadOnlyDictionary<string, Dictionary<string, DomainCheck>> DomainChecks,
    IReadOnlyDictionary<string, Dictionary<string, StressOutcome>> StressChecks)
{
    public static async Task<ConcreteReportAnalysis> CalculateAsync(JsonObject preparedData, CancellationToken cancellation)
    {
        var data = (JsonObject)preparedData.DeepClone();
        var input = data["input"]!.AsObject(); var settings = data["workspace_ca"]!.AsObject();
        var actions = ConcreteSheetPreparation.ReadActions(data);
        var domains = new Dictionary<string, Dictionary<string, DomainCheck>>();
        var three = new Dictionary<string, CheckerDomain3D>(); var two = new Dictionary<string, CheckerDomain2D>();
        var stresses = new Dictionary<string, Dictionary<string, StressOutcome>>();
        var shear = new Dictionary<string, Ntc2018Checks.ShearResult[]>(); var torsion = new Dictionary<string, TorsionResult>();
        var errors = new Dictionary<string, string>();
        var session = new ConcreteAnalysisSession();
        await session.CalculateBatchAsync(input, settings, actions,
            new HashSet<string> { "3D:SLU", "2D:SLU", "3D:SLV", "2D:SLV", "SLE", "SLE_FREQ", "SLE_QP" },
            outcome =>
            {
                if (outcome.Error is not null) errors[outcome.Name] = outcome.Error;
                else if (outcome.Domain is { } domain)
                {
                    domains[outcome.Name] = domain.Checks;
                    if (domain.Three is not null) three[outcome.Name[3..]] = domain.Three;
                    if (domain.Two is not null) two[outcome.Name[3..]] = domain.Two;
                }
                else if (outcome.Stress is { } rows) stresses[outcome.Name] = rows;
            }, cancellation: cancellation);
        var shearRows = settings["taglio"]!.Array("azioni").OfType<JsonObject>().ToArray();
        await Task.Run(() =>
        {
            try
            {
                ConcreteSheetPreparation.RefreshAutomaticShear(input, settings["taglio"]!.AsObject());
                ConcreteShearBatch.Calculate(input, settings, shearRows, (index, result, error) =>
                {
                    string id = shearRows[index].S("id");
                    if (result is null) { shear.Remove(id); torsion.Remove(id); return; }
                    shear[id] = result.Shear;
                    if (result.Torsion is not null) torsion[id] = result.Torsion;
                }, cancellation);
            }
            catch (Exception error) when (error is not OperationCanceledException) { errors["Taglio"] = error.Message; }
        }, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (domains.Count == 0 && stresses.Count == 0 && shear.Count == 0)
            throw new ArgumentException("Dati incompleti o calcolo non riuscito. Aprire la scheda per correggere gli input.");
        var details = await Task.Run(() => ConcreteDetailingPresentation.Calculate(input, settings, actions["SLU"].Concat(actions["SLV"])), cancellation);
        details.ApplyDerivedValues(settings);
        var summary = ConcreteVerificationSummaries.Create(settings, actions, domains, stresses, shearRows, shear, torsion);
        var exported = ConcreteResultExport.Create(data, summary, domains, stresses, shear, torsion, details.Checks,
            details.DetailsText, details.Anchorage, details.AnchorageDetails.Text, null, errors);
        var geometry = await Task.Run(() => ConcretePreview.PrepareGeometry(input), cancellation);
        return new(data, exported, geometry, actions, three, two, domains, stresses);
    }
}
