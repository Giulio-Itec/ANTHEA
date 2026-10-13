using System.Text.Json.Nodes;
using Anthea.Calculations.Geotechnics;
namespace Anthea.Application.Modules;

/// <summary>Optional analyses of a module. They do not modify the document or replace its primary result.</summary>
public static class StructuralQueries
{
    public static Anthea.Application.Walls.WallModel.ReinforcementProposal WallReinforcement(JsonObject source, CancellationToken cancellation = default)
    {
        var snapshot = ModuleServices.Get(RetainingWall.Module).Prepare(source);
        cancellation.ThrowIfCancellationRequested();
        return new(RetainingWall.DesignReinforcement(snapshot, cancellation));
    }
    public static Anthea.Application.Bridge.SectionResponseResult BridgeResponse(JsonObject source, JsonObject request, CancellationToken cancellation = default)
    {
        var snapshot = ModuleServices.Get(BridgeSection.Module).Prepare(source);
        cancellation.ThrowIfCancellationRequested();
        return new Anthea.Application.Bridge.BridgeProjection().Read(BridgeSection.CalculateResponse(snapshot, (JsonObject)request.DeepClone(), cancellation))!;
    }
    public static Anthea.Application.Walls.SlopeResult WallGlobal(JsonObject source, CancellationToken cancellation = default)
    {
        var snapshot = ModuleServices.Get(RetainingWall.Module).Prepare(source);
        cancellation.ThrowIfCancellationRequested();
        return new(RetainingWall.CalculateGlobal(snapshot, cancellation));
    }

    public static PrincipalMomentResistance[] ConcreteQuickResistance(JsonObject input, JsonObject workspace,
        double axial, bool elastic, CancellationToken cancellation = default)
        => SectionMomentResistance.Calculate((JsonObject)input.DeepClone(), (JsonObject)workspace.DeepClone(), axial, elastic, cancellation);

    public static MomentCurvatureResult ConcreteCurvature(JsonObject input, JsonObject workspace,
        JsonObject options, CancellationToken cancellation = default)
        => ConcreteCurvatureAnalysis.Calculate((JsonObject)input.DeepClone(), (JsonObject)workspace.DeepClone(),
            (JsonObject)options.DeepClone(), cancellation);

    public static ConcreteDesignResult ConcreteReinforcement(JsonObject source, CancellationToken cancellation = default,
        IProgress<ConcreteDesignProgress>? progress = null)
    {
        var snapshot = ModuleServices.Get("str_palo").Prepare(source);
        var options = ConcreteDesignOptions.Read(snapshot["workspace_ca"]!["calcola_armature"]!.AsObject());
        return ConcreteReinforcementDesign.Optimize(snapshot, options, cancellation, progress);
    }
}
