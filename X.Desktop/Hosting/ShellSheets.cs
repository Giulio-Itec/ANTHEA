using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;

namespace X.Desktop;

/// <summary>
/// The sheets of the new shell as the Release chooses them (F5.13 c, night of 22-23/10; risk R5, proposal 5.2): a module MVVM in
/// <see cref="ModuleImplementations"/> opens its ViewModel (<see cref="RegistrySheetFactory"/>, as before), a module still historic opens the historic
/// <see cref="SheetEditor"/> behind <see cref="HistoricSheetViewModel"/>, on a copy of the data and with the calculation service of the module, as
/// <see cref="SheetEditorFactory"/> opens it for MainWindow. The choice is the same of the window of the Release; in the UiTests build the bench
/// argument <c>--module-impl</c> changes it, as for MainWindow.
/// </summary>
internal sealed class ReleaseSheetFactory : ISheetFactory
{
    private readonly ModuleImplementations implementations;
    private readonly RegistrySheetFactory mvvm;
    private readonly PresentationServices services;

    /// <exception cref="InvalidOperationException">A module chosen as MVVM has no MVVM implementation in <paramref name="modules"/> (as SheetEditorFactory).</exception>
    internal ReleaseSheetFactory(ModuleImplementations implementations, ModuleRegistry modules, PresentationServices services, CalculationLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(implementations);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(services);
        var missing = implementations.Mvvm.Where(module => !modules.TryGetSheetModule(module, out _)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Implementazione MVVM scelta per moduli che non ne hanno una: " + string.Join(", ", missing) + ".");
        this.implementations = implementations; this.services = services;
        mvvm = new RegistrySheetFactory(modules, services, limiter);
    }

    /// <summary>The implementation of the sheets of <paramref name="module"/> in the shell.</summary>
    internal SheetImplementation For(string module) => implementations.For(module);

    public ISheetViewModel Open(string module, JsonObject data, bool readOnly, ISheetHost host)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(host);
        if (implementations.For(module) == SheetImplementation.Mvvm) return mvvm.Open(module, data, readOnly, host);
        var editor = new SheetEditor(module, data);
        try { return new HistoricSheetViewModel(editor, host, readOnly, services.Messages); }
        catch { editor.Dispose(); throw; }
    }
}

/// <summary>
/// The files of the sheets for the commands of the new shell (F5.13 c, R5): the sheets of the migrated modules through <see cref="SheetFileExports"/>
/// (the code of MvvmSheetEditor), the historic ones through their <see cref="SheetEditor"/>, which writes the JSON, the report with its PDF and the short
/// report as it does for MainWindow (ExportResult, ExportReport, BuildShortReport). The short report is written, Word and PDF within two pages, away from
/// the UI thread in both cases.
/// </summary>
internal sealed class DesktopSheetExports : ISheetExports
{
    private readonly SheetFileExports migrated = new();

    public void ExportResult(SheetSession sheet, string path)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (sheet.ViewModel is HistoricSheetViewModel historic) historic.Editor.ExportResult(path);
        else migrated.ExportResult(sheet, path);
    }

    public void ExportReport(SheetSession sheet, string path, string title, IReadOnlySet<string> options)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(options);
        if (sheet.ViewModel is HistoricSheetViewModel historic) historic.Editor.ExportReport(path, title, options.ToHashSet());
        else migrated.ExportReport(sheet, path, title, options);
    }

    public byte[] BuildShortReport(SheetSession sheet, string title)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return sheet.ViewModel is HistoricSheetViewModel historic ? historic.Editor.BuildShortReport(title) : migrated.BuildShortReport(sheet, title);
    }

    public Task WriteShortReportAsync(string path, byte[] report, string title) => migrated.WriteShortReportAsync(path, report, title);
}
