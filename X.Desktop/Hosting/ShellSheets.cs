using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;

namespace X.Desktop;

/// <summary>
/// The sheets of the new shell as the Release chooses them (F5.13 c, night of 22-23/10; risk R5, proposal 5.2): a module MVVM in
/// <see cref="ModuleImplementations"/> opens its ViewModel (<see cref="RegistrySheetFactory"/>, as before). Until F5.13 g a module still historic opened
/// the historic SheetEditor behind HistoricSheetViewModel (R5); since every module is MVVM, both are archived in supporto/SUPERATI/ui-storica/shell and a
/// module without an MVVM view is refused with the text of the archived SheetEditor. In the UiTests build the bench argument <c>--module-impl</c> applies.
/// </summary>
internal sealed class ReleaseSheetFactory : ISheetFactory
{
    private readonly ModuleImplementations implementations;
    private readonly RegistrySheetFactory mvvm;

    /// <exception cref="InvalidOperationException">A module chosen as MVVM has no MVVM implementation in <paramref name="modules"/> (as SheetEditorFactory).</exception>
    internal ReleaseSheetFactory(ModuleImplementations implementations, ModuleRegistry modules, PresentationServices services, CalculationLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(implementations);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(services);
        var missing = implementations.Mvvm.Where(module => !modules.TryGetSheetModule(module, out _)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Implementazione MVVM scelta per moduli che non ne hanno una: " + string.Join(", ", missing) + ".");
        this.implementations = implementations;
        mvvm = new RegistrySheetFactory(modules, services, limiter);
    }

    /// <summary>The implementation of the sheets of <paramref name="module"/> in the shell.</summary>
    internal SheetImplementation For(string module) => implementations.For(module);

    public ISheetViewModel Open(string module, JsonObject data, bool readOnly, ISheetHost host)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(host);
        if (implementations.For(module) == SheetImplementation.Mvvm) return mvvm.Open(module, data, readOnly, host);
        throw SheetEditorFactory.NoView(module);
    }
}

/// <summary>
/// The files of the sheets for the commands of the new shell (F5.13 c): the sheets through <see cref="SheetFileExports"/> (the code of MvvmSheetEditor),
/// which writes the JSON, the report with its PDF and the short report, Word and PDF within two pages, away from the UI thread. Until F5.13 g the historic
/// sheets (R5) wrote their files through their SheetEditor, archived with it.
/// </summary>
internal sealed class DesktopSheetExports : ISheetExports
{
    private readonly SheetFileExports migrated = new();

    public void ExportResult(SheetSession sheet, string path)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        migrated.ExportResult(sheet, path);
    }

    public void ExportReport(SheetSession sheet, string path, string title, IReadOnlySet<string> options)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(options);
        migrated.ExportReport(sheet, path, title, options);
    }

    public byte[] BuildShortReport(SheetSession sheet, string title)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return migrated.BuildShortReport(sheet, title);
    }

    public Task WriteShortReportAsync(string path, byte[] report, string title) => migrated.WriteShortReportAsync(path, report, title);
}

/// <summary>
/// The report of a section for the new shell (F5.13 e): the same call of MainWindow.GenerateSectionReport (ProjectReport.cs), the snapshot written by
/// ProjectReportService with the chapters of ModuleReportService and the figures of the desktop. The code of the reports does not change (frozen on 8/10).
/// </summary>
internal sealed class DesktopSectionReports : ISectionReports
{
    public Task<int> WriteAsync(JsonObject snapshot, string path, Action<string> progress, CancellationToken cancellation) =>
        Anthea.Application.Reporting.ProjectReportService.WriteSnapshotAsync(snapshot, path,
            new Anthea.Application.Reporting.ModuleReportService(new ModuleFigureRenderer()), progress, cancellation);
}
