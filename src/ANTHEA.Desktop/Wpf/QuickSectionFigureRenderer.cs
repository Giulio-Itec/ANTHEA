using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Presentation.Modules.QuickSection;
using Anthea.Reports;

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
            Axes = true, Labels = !brief, Dimensions = !brief, CoverDimensions = !brief && first.Preview.Geometry.Shape != ConcreteQuickSection.Dxf, SpacingDimensions = false,
            InnerLinkDiameter = ConcreteQuickSection.SecondRingLinkDiameter(analysis.Data) };
        if (brief || options.Contains("sezione")) figures.Add(new(brief ? "Sezione e armature · assi di riferimento" : "Sezione e armature · dimensioni in mm",
            Section().ReportPng(brief ? 370 : 520, brief ? 210 : 360), "sezione"));
        var governingCases = QuickSectionReport.Governing(analysis.Report).Select(p => p.Case).DistinctBy(c => c.Id).ToArray();
        var governing = governingCases.Select(c => c.Id).ToHashSet();
        IEnumerable<QuickReportCase> cases = brief ? governingCases : analysis.Report.Cases;
        foreach (var item in cases)
        {
            bool selectedService = !brief && item.State.StartsWith("SLE") && options.Contains("risultati_" + item.State);
            bool governor = governing.Contains(item.Id);
            if (!governor && !selectedService) continue;
            var result = analysis.Results[item.Id];
            string caption = item.Name + " · " + QuickSectionReport.StateName(item.State);
            if (governor && brief && !figures.Any(f => f.Category == "dominio") && item.State is "SLU" or "SLV")
            {
                var plot = new Plot { InvertY = false, CenteredAxes = true };
                PlotPresentation.ApplyDomainGeometry(plot, result.DomainGeometry);
                PlotPresentation.ApplyDomainActions(plot, result.DomainActions);
                figures.Add(new(caption + " · " + plot.Title, plot.ReportPng(brief ? 600 : 640, brief ? 230 : 480), "dominio"));
            }
            if (selectedService
                && (result.Method == ConcreteQuickSection.Methods[2] ? result.ServiceState : result.UltimateState) is { } stress)
            {
                var section = Section(); section.Stress = stress;
                // With the method n the stress of the most stressed bar of each layer, as on the sheet (19/11/2026).
                if (result.Method == ConcreteQuickSection.Methods[2]) section.Callouts = ConcreteQuickSection.StressCalloutLabels(analysis.Data, first.Preview.Geometry, stress);
                figures.Add(new(caption + " · tensioni nella sezione", section.ReportPng(640, 400), QuickSectionReport.ServiceStressCategory(item.Id)));
            }
        }
        return figures;
    }
}
