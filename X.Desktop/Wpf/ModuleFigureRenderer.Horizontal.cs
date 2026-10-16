using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
namespace X.Desktop;
internal sealed partial class ModuleFigureRenderer
{
    /// <summary>The figures of the report: the plan of HorizontalPilePresentation.Figures, drawn by HorizontalDiagrams.</summary>
    internal static IReadOnlyList<ReportFigure> HorizontalFigures(JsonObject result) => HorizontalPilePresentation.Figures(result).Select(f =>
        new ReportFigure(f.Caption, new HorizontalDiagrams(result, f.Survey - 1, f.SoilView).Png(f.Width, f.Height), "sondaggio", f.Survey, f.AspectRatio)).ToArray();
}
