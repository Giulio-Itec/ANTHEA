using Anthea.Application.Concrete;
using Anthea.Application.Reporting;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Modules.QuickSection;

public interface IQuickSectionFigureRenderer
{
    IReadOnlyList<ReportFigure> Render(ConcreteQuickReportAnalysis analysis, IReadOnlySet<string> options);
}

public sealed partial class QuickSectionSheetViewModel
{
    private readonly IQuickSectionFigureRenderer? figures;
    public const string ReportUnavailable = "Attendere l’aggiornamento automatico e correggere i dati non validi prima di esportare il report.";

    private sealed class ReportSource(QuickSectionSheetViewModel sheet) : IModuleReportSource, IModuleShortReport
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
        {
            sheet.ThrowIfDisposed(); sheet.Commit();
            if (sheet.IsBusy || !sheet.HasResults || sheet.CalculationState is not (Anthea.Presentation.Calculation.CalculationState.Completed or Anthea.Presentation.Calculation.CalculationState.Incomplete)) throw new InvalidOperationException(ReportUnavailable);
            bool brief = options.Contains(QuickSectionReport.Brief);
            var analysis = ConcreteQuickReportAnalysis.Create(sheet.Data, sheet.Current, options.Contains("curvatura"));
            bool drawings = brief || options.Overlaps(new[] { "sezione", "dominio", "tensioni", "curvatura" });
            if (drawings && sheet.figures is null) throw new InvalidOperationException("Disegni della relazione non disponibili.");
            var images = sheet.figures?.Render(analysis, options).Select(f => f.Image()).ToArray() ?? [];
            return DocxWriter.Write(QuickSectionReport.Create(title, analysis.Report, options, images));
        }
        public byte[] BuildShortReport(string title) => BuildReport(title, new HashSet<string> { QuickSectionReport.Version, QuickSectionReport.Brief });
    }
}
