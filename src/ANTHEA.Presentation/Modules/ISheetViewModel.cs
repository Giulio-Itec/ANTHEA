using System.Text.Json.Nodes;

namespace Anthea.Presentation.Modules;

/// <summary>
/// ViewModel of an open sheet (proposal F5, section 5.1). The window hosts it through MvvmSheetEditor (X.Desktop/Hosting),
/// which maps it onto the ISheetEditor contract of the historic sheets: Data, Result, HasResults, Busy, Commit, CalculateAsync,
/// the report and, through <see cref="IsReadOnly"/>, the read-only revisions. Common implementation:
/// <see cref="SheetViewModelBase"/>.
/// </summary>
public interface ISheetViewModel : IDisposable
{
    /// <summary>Identifier of the module of the sheet (<see cref="IModuleDescriptor.Info"/>.Id).</summary>
    string ModuleId { get; }

    /// <summary>Working copy of the sheet data, in the format of the documents (unchanged by F5); the host writes it back to the document.</summary>
    JsonObject Data { get; }

    /// <summary>
    /// The data with the edits that the sheet keeps until <see cref="Commit"/>, without committing them (F5.5): the shared-data
    /// status of a project reads it while the sheet is open (ISheetEditor.PreviewData). <see cref="Data"/> for the sheets that write
    /// at every edit (the default of <see cref="SheetViewModelBase"/>); the state not committed yet for the concrete material, whose
    /// historic view writes its data only at Commit.
    /// </summary>
    JsonObject PreviewData { get; }

    /// <summary>Version of <see cref="Data"/>: it grows at every change and reload. Only a calculation of the current data is published.</summary>
    long Revision { get; }

    /// <summary>True while a calculation runs.</summary>
    bool IsBusy { get; }

    bool HasResults { get; }

    /// <summary>Result of the last published calculation of the current data, or null.</summary>
    JsonObject? Result { get; }

    /// <summary>Read-only sheet (a previous revision of the project): edits are refused, results and calculation stay available.</summary>
    bool IsReadOnly { get; set; }

    /// <summary>The user changed the data.</summary>
    event EventHandler? Modified;

    /// <summary>Writes the drafts of the fields into <see cref="Data"/>.</summary>
    void Commit();

    /// <summary>Calculates the current <see cref="Data"/> (the «Calcola» command), without committing drafts.</summary>
    Task CalculateAsync(CancellationToken cancellation = default);

    /// <summary>Data replaced from outside (shared data, soil, undo): the sheet takes a copy and invalidates its results.</summary>
    void Reload(JsonObject data);

    /// <summary>Report of the sheet, built with the existing report generators.</summary>
    IModuleReportSource Report { get; }
}

/// <summary>
/// Report of a sheet (proposal 5.1), over the existing generators and ModuleReportService: the report code does not change
/// (reports frozen on 8/10). F5.2 keeps only the Word document of ISheetEditor.BuildReport; the short report, the material
/// chapter and the report options are added module by module when the module migrates.
/// </summary>
public interface IModuleReportSource
{
    /// <summary>DOCX of the report with the chosen sections; <paramref name="projectReport"/> true inside a project report.</summary>
    byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false);
}
