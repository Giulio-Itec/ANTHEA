using Anthea.Application.Concrete;
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

    public static ConcreteResults.PrincipalResistance[] ConcreteQuickResistance(JsonObject input, JsonObject workspace,
        double axial, bool elastic, CancellationToken cancellation = default)
        => SectionMomentResistance.Calculate((JsonObject)input.DeepClone(), (JsonObject)workspace.DeepClone(), axial, elastic, cancellation).Select(value => new ConcreteResults.PrincipalResistance(value)).ToArray();

    public static ConcreteResults.CurvatureResult ConcreteCurvature(JsonObject input, JsonObject workspace,
        JsonObject options, CancellationToken cancellation = default)
        => new(ConcreteCurvatureAnalysis.Calculate((JsonObject)input.DeepClone(), (JsonObject)workspace.DeepClone(),
            (JsonObject)options.DeepClone(), cancellation));

    public static ConcreteResults.DesignResult ConcreteReinforcement(JsonObject source, CancellationToken cancellation = default,
        IProgress<ConcreteResults.DesignProgress>? progress = null)
    {
        var snapshot = Anthea.Application.Concrete.SectionActionInputs.CalculationCopy(ModuleServices.Get("str_palo").Prepare(source));
        var options = ConcreteDesignOptions.Read(snapshot["workspace_ca"]!["calcola_armature"]!.AsObject());
        return new(ConcreteReinforcementDesign.Optimize(snapshot, options, cancellation,
            progress is null ? null : new DesignProgressForward(progress)));
    }
    private sealed class DesignProgressForward(IProgress<ConcreteResults.DesignProgress> target) : IProgress<ConcreteDesignProgress>
    {
        public void Report(ConcreteDesignProgress value) => target.Report(new(value));
    }
}
