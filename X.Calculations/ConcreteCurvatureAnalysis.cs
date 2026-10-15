using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Shared section M–χ path; WPF only schedules, renders and exports the result.</summary>
public static class ConcreteCurvatureAnalysis
{
    public static MomentCurvatureResult Calculate(JsonObject input, JsonObject workspace, JsonObject options,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (workspace.Array("trefoli").Count > 0)
            throw new ArgumentException("M–χ con trefoli: definire prima il criterio di snervamento/predeformazione; disponibile per armatura ordinaria.");
        var o = (JsonObject)options.DeepClone();
        var request = new MomentCurvatureRequest(SectionWorkspace.Number(o.S("N"), "N"),
            SectionWorkspace.Number(o.S("theta"), "θ"), SectionWorkspace.Subdivisions(o.S("passi"), "Passi", 10, 500),
            SectionWorkspace.Number(o.S("frazione"), "Frazione"), o.S("campionamento") == "Quadratico",
            SectionWorkspace.Number(o.S("tolleranza_n"), "Tolleranza N"),
            SectionWorkspace.Subdivisions(o.S("raffina_snervamento"), "Bisezioni", 0, 30));
        o["criterio"] = "N costante"; o["assi"] = "Locali"; o["modello"] = "Non lineare"; o["strategia"] = "Iterativo";
        var engine = new CheckerSection(input, workspace, o);
        // Tolleranza su N del punto limite (SLU-3): almeno la regola della libreria per il legame e la scala della sezione, come la
        // resistenza a N assegnato (SectionMomentResistance, F2-15); la tolleranza dell'utente vale se maggiore. Un valore non valido
        // resta all'analisi, che lo rifiuta come prima.
        double assigned = request.AxialToleranceKn;
        if (double.IsFinite(assigned) && assigned > 0)
            request = request with { AxialToleranceKn = Math.Max(assigned, SectionMomentResistance.AxialToleranceKn(engine, request.AxialKn)), AssignedAxialToleranceKn = assigned };
        var domain = engine.Domain3D(token);
        // Primo snervamento a εyd = fyd/Es con fyd della stessa norma della legge dell'acciaio: con i coefficienti unitari del foglio c.a. del palo
        // quello della norma effettiva (fyk, γs = 1), come TAG-2 per taglio e torsione e come ancoraggi e dettagli; prima SezioneCA.Fyd, cioè
        // fyk/γs dell'input, anche con la legge a fyk (seguito del 16/10/2026). Senza coefficienti unitari SezioneCA.Fyd come prima, bit per bit.
        double fyd = input.B("coefficienti_unitari") ? ConcreteMaterials.DesignValues(input, workspace).Fyd : engine.Geometry.Fyd;
        return new MomentCurvatureCalculator().Calculate(request, domain.Check,
            a => engine.Stress(a, "CURVA"), fyd / engine.Geometry.Es, token);
    }
}
