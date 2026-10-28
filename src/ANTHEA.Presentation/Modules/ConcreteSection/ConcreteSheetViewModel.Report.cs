using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The reports of the sheet (sub-step S9, ConcreteReport.cs of the historic view): the Word report with the figures of the drawings of the
/// sheet and the short report. The generators (ReportConcrete, ReportConcreteShort, the figures of ModuleFigureRenderer) do not change: reports
/// frozen on 8/10. The sheet gives them the same data as the historic view, in the same order: committed sheet, refused while busy, queued or
/// without results; the sections chosen saved in the data with Modified (not for the report of a project); the detailing refreshed, the export
/// read again and given the data; the 2D domain drawn also when its tab is hidden.
/// </summary>
public sealed partial class ConcreteSheetViewModel
{
    /// <summary>The message of the Word report refused (BuildReportDocument of the historic view).</summary>
    public const string ReportUnavailable = "Attendere l’aggiornamento automatico e correggere i dati non validi prima di esportare il report.";

    /// <summary>The message of the short report refused (BuildShortReport of the historic view).</summary>
    public const string ShortReportUnavailable = "Attendere l’aggiornamento delle verifiche prima di esportare.";

    /// <summary>The message of a report with figures asked to a sheet built without a renderer (only the checks build one).</summary>
    public const string FiguresUnavailable = "Disegni del report della sezione c.a. non disponibili.";

    private readonly IConcreteSectionFigureRenderer? figures;

    /// <summary>The Word report (BuildReportDocument of the historic view).</summary>
    private DocxDocument BuildReportDocument(string title, IReadOnlySet<string> options, bool projectReport)
    {
        Commit();
        if (IsBusy || !HasResults || IsCalculationQueued) throw new InvalidOperationException(ReportUnavailable);
        if (!projectReport) { Settings["report_sezioni"] = J.Node(options.OrderBy(k => k).ToArray()); RaiseModified(); }
        RefreshDetailing(); var result = Result!;
        result["dati"] = Data.DeepClone();
        var drawings = ReportDrawings(result, options);
        // Without «grafici» the report has no figure (ModuleFigureRenderer.ConcreteFigures draws none); a sheet without a renderer refuses the others.
        if (figures is null && options.Contains("grafici")) throw new InvalidOperationException(FiguresUnavailable);
        var images = figures?.Render(drawings, options).Select(f => f.Image()).ToList() ?? [];
        return ReportConcrete.CreateDocument(title, SectionActionInputs.CalculationCopy(Data), result, options.ToHashSet(), images, !projectReport);
    }

    // The graphics of the historic report: the panels of the domains in their order (the 2D one drawn also when hidden, when its figure is asked
    // and its domain exists), the families of the serviceability with all their rows, the preview of the control panel, the curve published.
    private ConcreteReportDrawings ReportDrawings(JsonObject result, IReadOnlySet<string> options)
    {
        var domains = DomainPanels.Select(panel =>
        {
            if (options.Contains("grafici") && options.Contains("dominio2d") && !panel.ThreeD && checker2D.ContainsKey(panel.Key)) panel.RefreshPanel(renderHidden: true);
            var table = panel.Table.Table;
            return new ConcreteReportDomain(panel.ThreeD, panel.Options, table.Shown.Select(r => r.Values).ToArray(), table.Selected?.Values.S("id"),
                checker3D.GetValueOrDefault(panel.Key), checker2D.GetValueOrDefault(panel.Key), domainResults.GetValueOrDefault(panel.Prefix + panel.Key),
                panel.Chart, panel.Zoom);
        }).ToArray();
        var stresses = stressPanels.ToDictionary(p => p.Key, p => new ConcreteReportStress(p.Value.Map, stressResults.GetValueOrDefault(p.Key),
            actions[p.Key].Select(r => r.Values).ToArray()), StringComparer.Ordinal);
        return new ConcreteReportDrawings(Data, result, Inputs.Drawing, domains, stresses, Curvature.Result) { ModelColumn = ModelColumn.Result };
    }

    /// <summary>The short report (BuildShortReport of the historic view): committed sheet, refused while busy, queued or without results.</summary>
    private byte[] BuildShortReport(string title)
    {
        Commit();
        if (IsBusy || !HasResults || IsCalculationQueued) throw new InvalidOperationException(ShortReportUnavailable);
        return ReportConcreteShort.Create(title, SectionActionInputs.CalculationCopy(Data), Result!);
    }

    /// <summary>The report source of the sheet: the Word report and the short report.</summary>
    private sealed class ReportSource(ConcreteSheetViewModel sheet) : IModuleReportSource, IModuleShortReport
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
        {
            ArgumentNullException.ThrowIfNull(options);
            sheet.ThrowIfDisposed();
            return DocxWriter.Write(sheet.BuildReportDocument(title, options, projectReport));
        }

        public byte[] BuildShortReport(string title)
        {
            sheet.ThrowIfDisposed();
            return sheet.BuildShortReport(title);
        }
    }
}
