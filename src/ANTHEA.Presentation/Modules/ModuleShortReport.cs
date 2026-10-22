namespace Anthea.Presentation.Modules;

/// <summary>
/// The short report of a sheet (ISheetEditor.BuildShortReport, the command «Report short CLS…» of the window), for the modules whose
/// <see cref="ModuleReportCapabilities.ShortReport"/> is true: today only the RC section (F5.10, sub-step S9). The report source of the sheet
/// implements it beside <see cref="IModuleReportSource"/>; MvvmSheetEditor gives it to the window. The generator does not change (reports
/// frozen on 8/10).
/// </summary>
public interface IModuleShortReport
{
    /// <summary>DOCX of the short report (two pages at most once written with ReportFiles), built by the generator of the module.</summary>
    byte[] BuildShortReport(string title);
}
