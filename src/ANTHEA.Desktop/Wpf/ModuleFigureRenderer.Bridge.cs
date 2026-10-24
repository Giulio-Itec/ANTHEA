using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using System.Text.Json.Nodes;
using System.Windows;
namespace X.Desktop;

internal sealed partial class ModuleFigureRenderer
{
    /// <summary>The figures of the report of the composite bridge section, drawn as BridgeReportPresentation plans them.</summary>
    internal static IReadOnlyList<ReportFigure> BridgeFigures(BridgeResultView result, JsonObject viewSettings, IReadOnlySet<string> options)
    {
        var style = BridgeReportPresentation.Style(result, viewSettings);
        return BridgeReportPresentation.Figures(result, viewSettings, options).Select(plan => new ReportFigure(plan.Caption, plan.Sketch ? Sketch(result, plan) : Section(result, style, plan), plan.Category)).ToArray();
    }

    private static byte[] Sketch(BridgeResultView result, BridgeFigurePlan plan)
    {
        var sketch = new BridgeDetailSketch { Input = result.Input, AtSupport = plan.AtSupport, Width = plan.Width };
        sketch.Measure(new Size(plan.Width, plan.Height)); sketch.Arrange(new Rect(0, 0, plan.Width, plan.Height)); sketch.UpdateLayout();
        return Ui.DocumentSnapshot(sketch);
    }

    private static byte[] Section(BridgeResultView result, BridgeFigureStyle style, BridgeFigurePlan plan)
    {
        var stage = plan.Stage is int index ? result.Stages[index] : null;
        var drawing = new BridgeDrawing { Geometry = result.Geometry, Stage = stage, Input = result.Input,
            LoadPoints = stage is null ? [] : BridgeResultPresentation.LoadPoints(stage),
            ShowGeometryLabels = plan.Mode == 2, ShowRebarLabels = plan.Mode == 2, ConcreteAmplification = style.ConcreteAmplification,
            ShowNeutralAxis = style.NeutralAxis, ContourSection = style.ContourSection, ContourDiagram = style.ContourDiagram, ShowStressLimits = style.StressLimits,
            Mode = plan.Mode, Width = plan.Width, Height = plan.Height };
        drawing.Measure(new Size(plan.Width, plan.Height)); drawing.Arrange(new Rect(0, 0, plan.Width, plan.Height)); drawing.UpdateLayout();
        return Ui.DocumentSnapshot(drawing);
    }
}
