using System.Text.Json.Nodes;
using System.Windows;
using X.Core;

namespace X.Desktop;

/// <summary>
/// Editor of one open sheet, as MainWindow and the F3 equivalence captures use it (F5.2, proposal 4.5 and 5.2).
/// It was implemented by the historic SheetEditor (archived in F5.13 g) and is implemented by <see cref="MvvmSheetEditor"/>, the host of the modules
/// migrated to MVVM; <see cref="SheetEditorFactory"/> chooses between them as <see cref="ModuleImplementations"/> says
/// (every module historic until F5.4).
/// </summary>
/// <remarks>
/// It stays in the WPF layer because of <see cref="View"/>. Every other member is free of WPF types and maps onto the
/// Presentation contract of the proposal (5.1): Module, Data, Result, HasResults, Busy, Commit, CalculateAsync and the
/// report members onto <c>ISheetViewModel</c>; the events and <see cref="SoilSources"/> onto <c>ISheetHost</c>;
/// <see cref="InspectRevision"/> onto <c>ISheetViewModel.IsReadOnly</c>.
/// </remarks>
internal interface ISheetEditor : IDisposable
{
    /// <summary>Identifier of the module of the sheet.</summary>
    string Module { get; }

    /// <summary>Working copy of the sheet data: a deep copy of the input, written back to the document by the shell after <see cref="Commit"/>.</summary>
    JsonObject Data { get; }

    /// <summary>The working copy with the edits not yet committed, without committing them; used by the shared-data status of a project.</summary>
    JsonObject PreviewData { get; }

    /// <summary>Result of the last published calculation, or null.</summary>
    JsonObject? Result { get; }

    bool HasResults { get; }

    /// <summary>True while a calculation is running.</summary>
    bool Busy { get; }

    /// <summary>The control shown in the sheet area of the window.</summary>
    FrameworkElement View { get; }

    /// <summary>The user changed the data.</summary>
    event Action? Modified;

    /// <summary>A retaining wall asks to open one of its sections in a concrete-section sheet (data, name).</summary>
    event Action<JsonObject, string>? ConcreteSectionRequested;

    /// <summary>The sheet asks the shell to export its report.</summary>
    event Action? ReportRequested;

    /// <summary>The soil transfer asks to open a copy in another module (module, data, name).</summary>
    event Action<string, JsonObject, string>? ModuleCopyRequested;

    /// <summary>The soil transfer asks to replace the soil of the current sheet with these data.</summary>
    event Action<JsonObject>? SoilReplacementRequested;

    /// <summary>Other sheets of the project that can give or receive the soil, supplied by the shell.</summary>
    Func<IEnumerable<(string Label, string Module, JsonObject Data, string Id)>>? SoilSources { get; set; }

    /// <summary>Writes the edits pending in the view into <see cref="Data"/>.</summary>
    void Commit();

    /// <summary>Calculates the sheet as the "Calcola" command does; <paramref name="commitEdits"/> false keeps pending edits out of the snapshot.</summary>
    Task CalculateAsync(bool commitEdits = true);

    /// <summary>Makes the view read-only for a previous revision of the project.</summary>
    void InspectRevision();

    void ExportResult(string filename);

    void ExportReport(string filename, string title, HashSet<string> options, bool projectReport = false);

    byte[] BuildReport(string title, HashSet<string> options, bool projectReport = false);

    /// <summary>Short report of a concrete section; other modules throw <see cref="InvalidOperationException"/>.</summary>
    byte[] BuildShortReport(string title);

    /// <summary>Report chapter of a material sheet (concrete or reinforcing steel).</summary>
    ReportProject.SheetContent MaterialReportContent();
}
