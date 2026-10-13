using System.Text.Json.Nodes;
using Anthea.Application.Reporting;
namespace X.Desktop;
internal sealed partial class ModuleFigureRenderer
{
    internal static IReadOnlyList<ReportFigure> HorizontalFigures(JsonObject result) =>
        Enumerable.Range(0, result.Array("sondaggi").Count).SelectMany(i => new[] { true, false }.Select(soil =>
            new ReportFigure(soil ? "Tensioni e resistenze del terreno" : "Equilibrio e sollecitazioni alla capacità ultima",
                new HorizontalDiagrams(result, i, soil).Png(1000, 660), "sondaggio", i + 1, 1000d / 660))).ToArray();
}
