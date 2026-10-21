using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Presentation.Modules.BridgeDesign;

namespace X.Desktop;

/// <summary>
/// The drawings of BridgeDesignSheetViewModel (F5.12) in the desktop: the elevation and the cross section as the historic view renders them
/// for the PNG export and for the report (<see cref="BridgeDesignDrawing.Png"/>, 1200 × 480).
/// </summary>
internal sealed class BridgeDesignFigureRenderer : IBridgeDesignFigureRenderer
{
    public byte[] Drawing(JsonObject data, BridgeDesignModel.Result result, bool section) => new BridgeDesignDrawing { Data = data, Result = result }.Png(section);
}
