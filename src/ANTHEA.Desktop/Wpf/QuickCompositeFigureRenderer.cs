using System.Windows;
using Anthea.Application.Bridge;
using Anthea.Application.Reporting;
using Anthea.Presentation.Modules.QuickCompositeSection;
using Anthea.Reports;

namespace X.Desktop;

internal sealed class QuickCompositeFigureRenderer : IQuickCompositeFigureRenderer
{
    public IReadOnlyList<ReportFigure> Render(BridgeQuickReportAnalysis analysis, IReadOnlySet<string> options)
    {
        var images = new List<ReportFigure>(); bool brief = options.Contains(BridgeQuickReport.Brief);
        if ((brief || options.Contains("sezione")) && analysis.Result.Cases.FirstOrDefault(c => c.Calculation is not null) is { } first)
            images.Add(new("Sezione composta · geometria e armature", Draw(first.Calculation!, 2), "sezione"));
        if (!brief && options.Contains("tensioni"))
            foreach (var item in analysis.Result.Cases.Where(c => c.Calculation is not null))
                images.Add(new(item.Name + " · " + item.State + " · tensioni totali nell’ultima situazione", Draw(item.Calculation!, 0), "tensioni"));
        return images;
    }
    private static byte[] Draw(BridgeResultView result, int mode)
    {
        var drawing = new BridgeDrawing { Geometry = result.Geometry, Input = result.Input, Stage = mode == 2 ? null : result.Stages.Last(),
            Mode = mode, ShowGeometryLabels = true, ShowRebarLabels = mode == 2, ShowNeutralAxis = false, Width = 800, Height = 420 };
        drawing.Measure(new Size(drawing.Width, drawing.Height)); drawing.Arrange(new Rect(0, 0, drawing.Width, drawing.Height)); drawing.UpdateLayout();
        return Ui.DocumentSnapshot(drawing);
    }
}
