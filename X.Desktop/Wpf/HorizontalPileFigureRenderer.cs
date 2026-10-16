using System.Text.Json.Nodes;
using Anthea.Presentation.Modules.HorizontalPile;
using X.Core;

namespace X.Desktop;

/// <summary>
/// The figures of the report of HorizontalPileSheetViewModel (F5.7) in the desktop: the diagrams of every survey drawn by
/// <see cref="HorizontalDiagrams"/>, as <see cref="ModuleFigureRenderer.HorizontalFigures"/> draws them for the historic report.
/// </summary>
internal sealed class HorizontalPileFigureRenderer : IHorizontalPileFigureRenderer
{
    public IReadOnlyList<ReportOrizzontale.Figure> Render(JsonObject result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ModuleFigureRenderer.HorizontalFigures(result).Select(f => new ReportOrizzontale.Figure(f.Index!.Value, f.Caption, f.Png, f.AspectRatio)).ToArray();
    }
}
