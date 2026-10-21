using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Reporting;
using Anthea.Presentation.Modules.CompositeSection;

namespace X.Desktop;

/// <summary>
/// The figures of CompositeSectionSheetViewModel (F5.11) in the desktop: the figures of the report as the historic view draws them
/// (<see cref="ModuleFigureRenderer.BridgeFigures"/>, planned by BridgeReportPresentation.Figures).
/// </summary>
internal sealed class CompositeSectionFigureRenderer : ICompositeSectionFigureRenderer
{
    public IReadOnlyList<ReportFigure> Render(BridgeResultView result, JsonObject viewSettings, IReadOnlySet<string> options) =>
        ModuleFigureRenderer.BridgeFigures(result, viewSettings, options);
}
