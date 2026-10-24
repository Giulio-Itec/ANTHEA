using System.Text.Json.Nodes;
using System.Windows;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Application.Walls;
using Anthea.Presentation.Modules.RetainingWall;

namespace X.Desktop;

/// <summary>
/// The figures of RetainingWallSheetViewModel (F5.9) in the desktop: the figures of the report as the historic view draws them
/// (<see cref="ModuleFigureRenderer.WallFigures"/>, planned by RetainingWallPresentation.Figures) and the figure of «Word globale».
/// </summary>
internal sealed class RetainingWallFigureRenderer : IRetainingWallFigureRenderer
{
    public IReadOnlyList<ReportFigure> Render(WallModel.Result calculation, WallReportPresentation context) => ModuleFigureRenderer.WallFigures(calculation, context);

    public ReportFigure RenderGlobal(JsonObject input, SlopeResult result, SlopeCaseResult? shown)
    {
        var figure = RetainingWallPresentation.GlobalExportFigure;
        var drawing = new GlobalStabilityDrawing { Data = input, Result = result, Case = shown, Width = figure.Width, Height = figure.Height };
        drawing.Measure(new Size(figure.Width, figure.Height)); drawing.Arrange(new Rect(0, 0, figure.Width, figure.Height)); drawing.UpdateLayout();
        return new(figure.Caption, Ui.DocumentSnapshot(drawing), figure.Category, AspectRatio: figure.AspectRatio);
    }
}
