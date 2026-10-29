using System.Text.Json.Nodes;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.Seepage;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics.Seepage;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

internal sealed record EmbeddedWallHydraulicAnalysis(IReadOnlyList<EmbeddedWallHydraulicData> Reports, IReadOnlyDictionary<string, SeepageResult> Fields);

/// <summary>Application orchestration and unit conversion. Mesh, constitutive law, solution and pressure
/// interpolation are supplied by Geotechnics. Each stage is an independent stationary hydraulic analysis.</summary>
internal static class EmbeddedWallHydraulics
{
    private const double CubicMillimetresPerLitre = 1e6;
    private const double LitresPerSecondPerMetreFromSquareMillimetresPerSecond = SoilUnits.Metre / CubicMillimetresPerLitre;
    internal static EmbeddedWallHydraulicAnalysis Calculate(JsonObject data, EmbeddedWallModel model,
        CancellationToken cancellation, Action<EmbeddedWallProgress> progress)
    {
        string mode = Text(data, "hydraulic_mode");
        string weightMode = Text(data, "hydraulic_weight_mode");
        if (weightMode is not ("Quota di falda assegnata" or "Pressioni FEM")) throw new ArgumentException("Scegliere il criterio di saturazione per i pesi.");
        if (mode == "Idrostatica")
        {
            if (weightMode == "Pressioni FEM") throw new ArgumentException("Il criterio Pressioni FEM richiede la filtrazione FEM 2D.");
            return new([], new Dictionary<string, SeepageResult>());
        }
        if (mode != "FEM 2D") throw new ArgumentException("Scegliere pressione idrostatica o filtrazione FEM 2D.");
        var left = Layers(data, "hydraulic_left"); var right = Layers(data, "hydraulic_right");
        bool ignoreSuction = data["ignore_suction"] is JsonValue v && v.TryGetValue<bool>(out bool b) ? b : throw new ArgumentException("Dichiarare il trattamento della suzione.");
        var results = new List<EmbeddedWallHydraulicData>();
        var fields = new Dictionary<string, SeepageResult>(StringComparer.Ordinal);
        foreach (var stage in model.Stages)
        {
            cancellation.ThrowIfCancellationRequested();
            var domain = new EmbeddedWallSeepageDomain
            {
                CaseId = model.CaseId + " / " + stage.Id, WallTop = model.Top, WallBottom = model.Bottom,
                WallThickness = Number(data, "hydraulic_thickness") * SoilUnits.Metre,
                LeftExtent = Number(data, "hydraulic_left_extent") * SoilUnits.Metre, RightExtent = Number(data, "hydraulic_right_extent") * SoilUnits.Metre,
                Bottom = Number(data, "hydraulic_bottom") * SoilUnits.Metre, MaximumElementLength = Number(data, "hydraulic_mesh") * SoilUnits.Metre,
                LeftBoundary = Boundary(data, "hydraulic_left_boundary", "sinistro"), RightBoundary = Boundary(data, "hydraulic_right_boundary", "destro"),
                LeftGround = stage.Left.Elevation, RightGround = stage.Right.Elevation,
                LeftHead = stage.Left.WaterElevation ?? throw new ArgumentException(stage.Id + ": assegnare il carico idraulico sinistro."),
                RightHead = stage.Right.WaterElevation ?? throw new ArgumentException(stage.Id + ": assegnare il carico idraulico destro."),
                LeftLayers = left, RightLayers = right
            };
            var result = SeepageAnalysis.Calculate(EmbeddedWallSeepageMesh.Create(domain), new SeepageOptions { MaximumIterations = Integer(data, "hydraulic_iterations") }, cancellation: cancellation,
                progress: new ProgressRelay(p => progress(new("Filtrazione " + stage.Id, null, p.Iteration))));
            results.Add(new(stage.Id, result.Status.ToString(), result.IsComplete, result.Diagnostic, result.Iterations,
                result.Inflow * LitresPerSecondPerMetreFromSquareMillimetresPerSecond, result.Outflow * LitresPerSecondPerMetreFromSquareMillimetresPerSecond,
                result.Balance * LitresPerSecondPerMetreFromSquareMillimetresPerSecond, result.MaximumResidual,
                Array.AsReadOnly(result.Nodes.Select(n => new EmbeddedWallHydraulicNode(n.Index, n.X / SoilUnits.Metre, n.Z / SoilUnits.Metre,
                    n.Head / SoilUnits.Metre, n.Pressure / SoilUnits.KiloPascal, n.Draining)).ToArray()),
                Array.AsReadOnly(result.Elements.Select(e => new EmbeddedWallHydraulicElement(e.A, e.B, e.C, e.GradientX, e.GradientZ,
                    e.FluxX / SoilUnits.Metre, e.FluxZ / SoilUnits.Metre, e.RelativeConductivity)).ToArray())));
            if (!result.IsComplete) break;
            fields.Add(stage.Id, result);
            // The geometry adapter maps the declared centered wall thickness to its two face coordinates.
            // Interpolation, domain checks and the explicit suction policy stay in Geotechnics.
            stage.Left.PorePressures = SeepageWallCoupling.Profile(result, -domain.WallThickness / 2, stage.Left.Elevation, model.Bottom, ignoreSuction);
            stage.Right.PorePressures = SeepageWallCoupling.Profile(result, domain.WallThickness / 2, stage.Right.Elevation, model.Bottom, ignoreSuction);
            stage.Left.PorePressureSource = stage.Right.PorePressureSource = "FEM 2D " + result.CaseId + (ignoreSuction ? "; suzione trascurata nel modello meccanico" : "; pressioni non negative");
            stage.Left.SaturationFromPorePressures = stage.Right.SaturationFromPorePressures = weightMode == "Pressioni FEM";
        }
        return new(results.AsReadOnly(), new System.Collections.ObjectModel.ReadOnlyDictionary<string, SeepageResult>(fields));
    }
    // The head of the stage stays required with no flow: it decides ponding or seepage face on the ground of that side.
    private static SeepageLateralBoundary Boundary(JsonObject data, string key, string side) => Text(data, key) switch
    {
        EmbeddedWallDocument.FixedHeadBoundary => SeepageLateralBoundary.FixedHead,
        EmbeddedWallDocument.NoFlowBoundary => SeepageLateralBoundary.NoFlow,
        _ => throw new ArgumentException("Scegliere la condizione del bordo " + side + " della filtrazione: carico imposto o flusso nullo.")
    };
    private static SeepageLayer[] Layers(JsonObject data, string table) => EmbeddedWallDocument.Rows(data, table).Select(s =>
    {
        double? alpha = Optional(s, "alpha"), n = Optional(s, "n");
        if (alpha.HasValue != n.HasValue) throw new ArgumentException("Legge non satura: α e n devono essere entrambi assegnati o entrambi assenti.");
        var material = new SeepageMaterial(Text(s, "name"), Number(s, "kxx") * SoilUnits.Metre, Number(s, "kzz") * SoilUnits.Metre,
            Number(s, "kxz") * SoilUnits.Metre, Text(s, "source"), alpha.HasValue ? new VanGenuchtenCurve(alpha.Value / SoilUnits.Metre, n!.Value) : null);
        return new SeepageLayer(Number(s, "top") * SoilUnits.Metre, Number(s, "bottom") * SoilUnits.Metre, material);
    }).ToArray();
    private sealed class ProgressRelay(Action<SeepageProgress> show) : IProgress<SeepageProgress>
    { public void Report(SeepageProgress value) => show(value); }
}
