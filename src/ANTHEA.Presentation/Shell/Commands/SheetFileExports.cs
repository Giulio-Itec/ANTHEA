using System.Text;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.BridgeDesign;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Modules.Materials;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Shell.Commands;

/// <summary>
/// The files of a sheet for the commands of the shell (F5.13 c, <see cref="ISheetExports"/>): the code that MvvmSheetEditor ran for the
/// sheets of the migrated modules, moved here so that the window of today and the new shell write the same files with the same code
/// (MvvmSheetEditor.ExportResult, ExportReport and BuildShortReport call the static members). Nothing of the reports changes (frozen on
/// 8/10): the generators, ReportFiles and the chapter of the materials are called as before.
/// <list type="bullet">
/// <item>Results: the JSON of the result, refused without results or during a calculation; for Bridge Design and the RC section with the
/// data of the sheet, as SheetEditor (BD7, F5.12; F5.10 S9), the RC section refusing with the message of its historic sheet.</item>
/// <item>Report: a material sheet writes its chapter, Word and PDF from the same document; a module whose historic sheet writes the PDF
/// with its own generator (<see cref="IModuleReportPdf"/>, T8) writes the DOCX and that PDF beside it; the others the DOCX and the PDF
/// rendered from it (<see cref="ReportFiles.Write"/>).</item>
/// <item>Short report: from the sheets that write it (<see cref="IModuleShortReport"/>, the RC section since F5.10 S9), refused for the
/// others with the text of MvvmSheetEditor; the writing, Word and PDF within two pages, away from the UI thread as
/// MainWindow.ExportShortReport.</item>
/// </list>
/// </summary>
public sealed class SheetFileExports : ISheetExports
{
    /// <summary>A JSON of the results asked without results or during a calculation (MvvmSheetEditor.ExportResult).</summary>
    public const string CalculateFirst = "Premere Calcola prima di esportare.";

    /// <summary>The same refusal for the RC section, with the text of its historic sheet (F5.10 S9).</summary>
    public const string ConcreteUpdateFirst = "Attendere l’aggiornamento automatico e correggere gli eventuali dati incompleti prima di esportare.";

    /// <summary>The short report of a sheet that does not write it (MvvmSheetEditor.BuildShortReport).</summary>
    public const string ShortReportUnavailable = "Il report short è disponibile per le sezioni in calcestruzzo armato.";

    public void ExportResult(SheetSession sheet, string path)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        WriteResult(sheet.ViewModel, path);
    }

    public void ExportReport(SheetSession sheet, string path, string title, IReadOnlySet<string> options)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        WriteReport(sheet.ViewModel, path, title, options);
    }

    public byte[] BuildShortReport(SheetSession sheet, string title)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return ShortReport(sheet.ViewModel, title);
    }

    /// <summary>Word and PDF of the short report within two pages, written away from the UI thread (MainWindow.ExportShortReport).</summary>
    public Task WriteShortReportAsync(string path, byte[] report, string title)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(report);
        return Task.Run(() => { ReportFiles.Write(path, report, title, ReportFiles.ShortReportPages); });
    }

    /// <summary>The JSON of the result of <paramref name="sheet"/> (MvvmSheetEditor.ExportResult), written atomically in UTF-8.</summary>
    /// <exception cref="InvalidOperationException">Without results or during a calculation.</exception>
    public static void WriteResult(ISheetViewModel sheet, string path)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentException.ThrowIfNullOrEmpty(path);
        // F5.10 S9: as SheetEditor, the RC section refuses with its message and its JSON carries also the data of the sheet.
        bool concrete = sheet is ConcreteSheetViewModel;
        if (!sheet.HasResults || sheet.IsBusy) throw new InvalidOperationException(concrete ? ConcreteUpdateFirst : CalculateFirst);
        var exported = sheet.Result!;
        // F5.12 (BD7): as SheetEditor, the JSON of Bridge Design carries also the data of the sheet (the result is a new object at every read).
        if (sheet is BridgeDesignSheetViewModel || concrete) exported["dati"] = sheet.Data.DeepClone();
        Archivio.ScriviAtomico(path, Encoding.UTF8.GetBytes(exported.ToJsonString(J.Options)));
    }

    /// <summary>
    /// The DOCX of the report of <paramref name="sheet"/> and the PDF beside it (MvvmSheetEditor.ExportReport, decision T8 of 17/10/2026):
    /// a material sheet from the document of its chapter (F5.5); a module with its own PDF generator (<see cref="IModuleReportPdf"/>, the
    /// shallow foundations) with that generator; the others with the PDF rendered from the DOCX.
    /// </summary>
    /// <param name="projectReport">True inside a project report.</param>
    public static void WriteReport(ISheetViewModel sheet, string path, string title, IReadOnlySet<string> options, bool projectReport = false)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(options);
        if (sheet is IMaterialSheetViewModel)
        {
            // T8: the report of a material sheet written as SheetEditor writes it, DOCX and PDF from the same document.
            sheet.Commit();
            Materiali.MaterialReportChapter.Write(path, sheet.ModuleId, title, sheet.Data);
            return;
        }
        if (sheet.Report is IModuleReportPdf edition)
        {
            byte[] word = sheet.Report.BuildReport(title, options, projectReport), pdf = edition.BuildPdf(title, options, projectReport);
            Archivio.ScriviAtomico(path, word);
            Archivio.ScriviAtomico(ReportFiles.PdfPath(path), pdf);
            return;
        }
        ReportFiles.Write(path, sheet.Report.BuildReport(title, options, projectReport), title);
    }

    /// <summary>The short report of <paramref name="sheet"/> (MvvmSheetEditor.BuildShortReport), from a sheet that writes it (<see cref="IModuleShortReport"/>: the RC section, F5.10 S9).</summary>
    /// <exception cref="InvalidOperationException">For the other sheets, with <see cref="ShortReportUnavailable"/>.</exception>
    public static byte[] ShortReport(ISheetViewModel sheet, string title)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return sheet.Report is IModuleShortReport report ? report.BuildShortReport(title)
            : throw new InvalidOperationException(ShortReportUnavailable);
    }
}
