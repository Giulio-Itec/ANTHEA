using Anthea.Application.Reporting;
using Anthea.Presentation.Modules.VerticalPile;

namespace X.Desktop;

/// <summary>
/// The figures of the report of VerticalPileSheetViewModel (F5.6) in the desktop: the capacity chart and the reference chart as the
/// sheet shows them, drawn with the Plot of the historic view, then <see cref="ModuleFigureRenderer.VerticalFigures"/> (abacus,
/// profiles), the figures of the historic report. The zoom of the chart on screen belongs to the view: the figure is drawn at full extent.
/// </summary>
internal sealed class VerticalPileFigureRenderer : IVerticalPileFigureRenderer
{
    public IReadOnlyList<ReportFigure> Render(VerticalPileFigureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shown = request.Capacity;
        var capacity = new Plot
        {
            Capacity = true, Title = shown.Title, XLabel = shown.XLabel, YLabel = shown.YLabel, CapacityDepth = shown.CapacityDepth,
            EmptyMessage = shown.EmptyMessage, Series = shown.Series.Select(PlotPresentation.Series).ToList()
        };
        var reference = new Plot { InvertY = false };
        PlotPresentation.Apply(reference, request.Reference);
        return ModuleFigureRenderer.VerticalFigures(request.Data, request.Result, request.Micro, capacity, reference);
    }
}
