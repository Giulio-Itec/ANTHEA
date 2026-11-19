using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Presentation.Modules.QuickSection;
using Anthea.Reports;
using X.Desktop.Themes;

namespace X.Desktop;

/// <summary>Disegna esclusivamente geometrie, domini e stati già calcolati.</summary>
internal sealed class QuickSectionFigureRenderer : IQuickSectionFigureRenderer
{
    public IReadOnlyList<ReportFigure> Render(ConcreteQuickReportAnalysis analysis, IReadOnlySet<string> options)
    {
        var figures = new List<ReportFigure>();
        bool brief = options.Contains(QuickSectionReport.Brief);
        var first = analysis.Results.Values.FirstOrDefault();
        if (first is null) return figures;
        ConcreteSectionViewport Section() => new() { Section = first.Preview.Geometry,
            Stirrups = analysis.Data["workspace_ca"]?["taglio"] as System.Text.Json.Nodes.JsonObject,
            Axes = true, Labels = !brief, Dimensions = !brief, CoverDimensions = !brief, SpacingDimensions = false,
            InnerLinkDiameter = ConcreteQuickSection.SecondRingLinkDiameter(analysis.Data) };
        if (brief || options.Contains("sezione")) figures.Add(new(brief ? "Sezione e armature · assi di riferimento" : "Sezione e armature · dimensioni in mm",
            Section().ReportPng(brief ? 370 : 520, brief ? 210 : 360), "sezione"));
        var governing = QuickSectionReport.Governing(analysis.Report).Select(p => p.Case).DistinctBy(c => c.Id).ToArray();
        foreach (var item in governing)
        {
            var result = analysis.Results[item.Id];
            string caption = item.Name + " · " + QuickSectionReport.StateName(item.State);
            if ((brief && !figures.Any(f => f.Category == "dominio") || options.Contains("dominio")) && item.State is "SLU" or "SLV")
            {
                var plot = new Plot { InvertY = false, CenteredAxes = true };
                PlotPresentation.ApplyDomainGeometry(plot, result.DomainGeometry);
                PlotPresentation.ApplyDomainActions(plot, result.DomainActions);
                figures.Add(new(caption + " · " + plot.Title, plot.ReportPng(brief ? 600 : 640, brief ? 230 : 480), "dominio"));
            }
            if (!brief && options.Contains("tensioni") && (result.Method == ConcreteQuickSection.Methods[2] ? result.ServiceState : result.UltimateState) is { } stress)
            {
                var section = Section(); section.Stress = stress;
                // With the method n the stress of the most stressed bar of each layer, as on the sheet (19/11/2026).
                if (result.Method == ConcreteQuickSection.Methods[2]) section.Callouts = ConcreteQuickSection.StressCalloutLabels(analysis.Data, first.Preview.Geometry, stress);
                figures.Add(new(caption + " · tensioni nella sezione", section.ReportPng(640, 400), "tensioni"));
            }
            if (!brief && options.Contains("curvatura") && ConcreteQuickSection.CurvaturePoints(result) is { Count: > 0 } points)
            {
                var curve = new Plot { InvertY = false, CenteredAxes = false, Title = ConcreteCurvaturePresentation.Title,
                    XLabel = ConcreteCurvaturePresentation.XLabel, YLabel = ConcreteCurvaturePresentation.YLabel,
                    Series = [new(ConcreteCurvaturePresentation.SeriesName, points, ThemeTokens.Brush("Series.Blue"))] };
                figures.Add(new(caption + " · momento–curvatura", curve.ReportPng(), "curvatura"));
            }
        }
        return figures;
    }
}
