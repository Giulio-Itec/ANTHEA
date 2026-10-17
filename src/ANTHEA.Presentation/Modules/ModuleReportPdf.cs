namespace Anthea.Presentation.Modules;

/// <summary>
/// The PDF of a report that the historic sheet of the module writes with its own generator, not derived from the DOCX (decision T8 of
/// 17/10/2026: the host writes the PDF as today, module by module, with a check that it is the same). The report source of the sheet
/// implements it beside <see cref="IModuleReportSource"/>: MvvmSheetEditor then writes the DOCX of
/// <see cref="IModuleReportSource.BuildReport"/> and this PDF beside it, with the same name, as SheetEditor.ExportReport does for the
/// module (the shallow foundations: FoundationReport.Word and FoundationReport.Pdf, F5.9b). The other sheets keep the PDF rendered from the
/// DOCX (ReportFiles.Write). The generators do not change (reports frozen on 8/10).
/// </summary>
public interface IModuleReportPdf
{
    /// <summary>PDF of the report with the chosen sections, built by the generator of the module.</summary>
    byte[] BuildPdf(string title, IReadOnlySet<string> options, bool projectReport = false);
}
