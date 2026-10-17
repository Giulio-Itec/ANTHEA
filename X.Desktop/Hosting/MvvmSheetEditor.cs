using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Reports;
using X.Core;

namespace X.Desktop;

/// <summary>
/// The sheet of a module migrated to MVVM in the current window (F5.2, proposal 5.2): it hosts the <see cref="ISheetViewModel"/>
/// created by the <see cref="ISheetModule"/> and its view, and offers them to MainWindow and to the F3 captures through
/// <see cref="ISheetEditor"/>, the contract of the historic <see cref="SheetEditor"/>.
/// <list type="bullet">
/// <item>View: the DataTemplate of the type of the ViewModel (or of a base type) in the resources of the application
/// (decision Q3, X.Desktop/Views); a ViewModel without one fails at opening, with its name.</item>
/// <item>Commands of <see cref="ISheetHost"/> → the events MainWindow already handles: OpenModuleCopy → ModuleCopyRequested,
/// ReplaceSoil → SoilReplacementRequested, RequestReport → ReportRequested; SoilSources reads <see cref="SoilSources"/>.
/// ConcreteSectionRequested is never raised: MainWindow opens the section of a wall as OpenModuleCopy("str_palo", …), the
/// same command.</item>
/// <item>Revisions: <see cref="InspectRevision"/> sets <see cref="ISheetViewModel.IsReadOnly"/>.</item>
/// <item>Help: F1 not handled by a field opens the guide of the module (<see cref="IModuleDescriptor.Help"/>) with the
/// <see cref="IWikiNavigator"/> of the services, as SheetEditor opens it; the «?» labels and F1 of the fields go through the same
/// navigator, given by the ViewModel to its forms.</item>
/// <item>Results and reports: the generic path of SheetEditor (JSON of the result, Word document of the sheet, PDF rendered from it),
/// or the PDF of the generator of the module where the historic sheet writes it so (<see cref="IModuleReportPdf"/>, T8); the short
/// report and the material chapter come with the modules that have them (F5.5, F5.10).</item>
/// </list>
/// </summary>
internal sealed class MvvmSheetEditor : ISheetEditor
{
    private readonly ContentControl view;
    private bool disposed;

    /// <param name="module">The migrated module.</param>
    /// <param name="data">Data of the sheet, copied: the document is never modified by the sheet.</param>
    /// <param name="services">Services of the presentation layer of the composition root.</param>
    /// <param name="calculation">Calculation service of the module.</param>
    /// <param name="limiter">Limit of the calculations of the application.</param>
    internal MvvmSheetEditor(ISheetModule module, JsonObject data, PresentationServices services, IModuleService calculation, CalculationLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(data);
        Sheet = module.CreateSheet(new SheetContext((JsonObject)data.DeepClone(), false, new Host(this), services, calculation, limiter));
        try
        {
            if (Sheet.ModuleId != module.Info.Id)
                throw new InvalidOperationException($"Il modulo {module.Info.Id} ha creato un foglio del modulo {Sheet.ModuleId}.");
            view = new ContentControl { Focusable = false, IsTabStop = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            view.ContentTemplate = ViewOf(view, Sheet.GetType());
            view.Content = Sheet;
            view.KeyDown += (_, e) =>
            {
                if (e.Key != Key.F1 || module.Help.GuideId is not { } guide) return;
                e.Handled = true;
                services.Wiki.Show(guide);
            };
        }
        catch { Sheet.Dispose(); throw; }
        Sheet.Modified += OnModified;
    }

    /// <summary>The hosted ViewModel.</summary>
    internal ISheetViewModel Sheet { get; }

    public string Module => Sheet.ModuleId;

    public JsonObject Data => Sheet.Data;

    /// <summary>The preview of the ViewModel (F5.5): the data for the sheets that write while the user edits, as InputForm; the state not committed yet for those that write at Commit.</summary>
    public JsonObject PreviewData => Sheet.PreviewData;

    public JsonObject? Result => Sheet.Result;

    public bool HasResults => Sheet.HasResults;

    public bool Busy => Sheet.IsBusy;

    public FrameworkElement View => view;

    public event Action? Modified;

    /// <summary>Never raised (see the remarks of the class): the section of a wall is asked through ModuleCopyRequested.</summary>
    public event Action<JsonObject, string>? ConcreteSectionRequested { add { } remove { } }

    public event Action? ReportRequested;

    public event Action<string, JsonObject, string>? ModuleCopyRequested;

    public event Action<JsonObject>? SoilReplacementRequested;

    public Func<IEnumerable<(string Label, string Module, JsonObject Data, string Id)>>? SoilSources { get; set; }

    public void Commit() => Sheet.Commit();

    public async Task CalculateAsync(bool commitEdits = true)
    {
        if (commitEdits) Sheet.Commit();
        await Sheet.CalculateAsync();
    }

    public void InspectRevision() => Sheet.IsReadOnly = true;

    public void ExportResult(string filename)
    {
        if (!HasResults || Busy) throw new InvalidOperationException("Premere Calcola prima di esportare.");
        Archivio.ScriviAtomico(filename, Encoding.UTF8.GetBytes(Result!.ToJsonString(J.Options)));
    }

    /// <summary>
    /// The DOCX of the report and the PDF beside it. A module whose historic sheet writes the PDF with its own generator
    /// (<see cref="IModuleReportPdf"/>, the shallow foundations) gets both files written as SheetEditor.ExportReport writes them
    /// (decision T8 of 17/10/2026); the others the PDF rendered from the DOCX (<see cref="ReportFiles.Write"/>).
    /// </summary>
    public void ExportReport(string filename, string title, HashSet<string> options, bool projectReport = false)
    {
        if (Sheet.Report is IModuleReportPdf edition)
        {
            byte[] word = BuildReport(title, options, projectReport), pdf = edition.BuildPdf(title, options, projectReport);
            Archivio.ScriviAtomico(filename, word);
            Archivio.ScriviAtomico(ReportFiles.PdfPath(filename), pdf);
            return;
        }
        ReportFiles.Write(filename, BuildReport(title, options, projectReport), title);
    }

    public byte[] BuildReport(string title, HashSet<string> options, bool projectReport = false) => Sheet.Report.BuildReport(title, options, projectReport);

    public byte[] BuildShortReport(string title) =>
        throw new InvalidOperationException("Il report short è disponibile per le sezioni in calcestruzzo armato.");

    public ReportProject.SheetContent MaterialReportContent() =>
        throw new InvalidOperationException("Capitolo dei materiali non disponibile per il modulo " + Module + ".");

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Sheet.Modified -= OnModified;
        view.Content = null;
        Sheet.Dispose();
    }

    private void OnModified(object? sender, EventArgs e) => Modified?.Invoke();

    /// <summary>The view of a ViewModel: the DataTemplate of its type or of a base type, as the implicit templates of WPF.</summary>
    private static DataTemplate ViewOf(FrameworkElement scope, Type viewModel)
    {
        for (var type = viewModel; type is not null && type != typeof(object); type = type.BaseType)
            if (scope.TryFindResource(new DataTemplateKey(type)) is DataTemplate template) return template;
        throw new InvalidOperationException("Vista del foglio non registrata: nessun DataTemplate per " + viewModel.FullName + " (X.Desktop/Views).");
    }

    /// <summary>The window side of the sheet: each command raises the event of the editor that MainWindow handles.</summary>
    private sealed class Host(MvvmSheetEditor editor) : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(module);
            ArgumentNullException.ThrowIfNull(data);
            editor.ModuleCopyRequested?.Invoke(module, data, name);
        }

        public void ReplaceSoil(JsonObject data)
        {
            ArgumentNullException.ThrowIfNull(data);
            editor.SoilReplacementRequested?.Invoke(data);
        }

        public IReadOnlyList<SoilSource> SoilSources() =>
            editor.SoilSources?.Invoke().Select(s => new SoilSource(s.Label, s.Module, s.Data, s.Id)).ToArray() ?? [];

        public void RequestReport() => editor.ReportRequested?.Invoke();
    }
}
