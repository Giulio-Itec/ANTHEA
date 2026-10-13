using System.Text.Json.Nodes;
using Anthea.Calculations.Geotechnics;
namespace Anthea.Application.Modules;

/// <summary>Optional analyses of a module. They do not modify the document or replace its primary result.</summary>
public static class StructuralQueries
{
    public static RetainingWall.ReinforcementProposal WallReinforcement(JsonObject source, CancellationToken cancellation = default)
    {
        var snapshot = ModuleServices.Get(RetainingWall.Module).Prepare(source);
        cancellation.ThrowIfCancellationRequested();
        return RetainingWall.DesignReinforcement(snapshot, cancellation);
    }
    public static GPC.Checkers.CompositeBridge.History.SectionResponseResult BridgeResponse(JsonObject source, JsonObject request, CancellationToken cancellation = default)
    {
        var snapshot = ModuleServices.Get(BridgeSection.Module).Prepare(source);
        cancellation.ThrowIfCancellationRequested();
        return BridgeSection.CalculateResponse(snapshot, (JsonObject)request.DeepClone(), cancellation);
    }
    public static SlopeResult WallGlobal(JsonObject source, CancellationToken cancellation = default)
    {
        var snapshot = ModuleServices.Get(RetainingWall.Module).Prepare(source);
        cancellation.ThrowIfCancellationRequested();
        return RetainingWall.CalculateGlobal(snapshot, cancellation);
    }
}
