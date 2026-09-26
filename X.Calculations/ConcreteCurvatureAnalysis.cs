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
        var domain = engine.Domain3D(token);
        return new MomentCurvatureCalculator().Calculate(request, domain.Check,
            a => engine.Stress(a, "CURVA"), engine.Geometry.Fyd / engine.Geometry.Es, token);
    }
}
