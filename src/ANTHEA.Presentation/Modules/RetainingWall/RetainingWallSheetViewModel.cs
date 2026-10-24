using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Application.Scheduling;
using Anthea.Application.Walls;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Services;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>
/// Sheet of the retaining walls (geo_muri_sostegno), F5.9: the behaviour of the historic view RetainingWallWorkspace without WPF, on the
/// same document, with the same writes in the same order.
/// <list type="bullet">
/// <item>Opening: <see cref="WallModel.Upgrade"/>, the inputs (soils, global stability, advanced options, materials, reinforcement,
/// geometry, actions, seismic action, accelerograms), the matrix when the document has combinations, the rules of the fields, then the
/// automatic calculation after 450 ms.</item>
/// <item>Forms (<see cref="RetainingWallFields"/>, <see cref="FormViewModel"/>): the raw text into the document at every change, as InputForm;
/// every edit applies the rules of the fields (<see cref="RetainingWallPresentation.InputRules"/>) and invalidates the results.</item>
/// <item>Tables (<see cref="WallTableViewModel"/>): the soil columns (linked or not), the deformable layers, the actions, the combination
/// matrix and the global matrix on display copies, the deep layers of the global stability (thickness and bottom), the points of the profile.</item>
/// <item>Calculation (<see cref="SheetCalculationMode.Background"/>): on a copy, on a background thread, through the <see cref="IModuleService"/>
/// of the context; automatic 450 ms after the last edit, «Ricalcola» at once; only the current revision is published. The publication
/// writes the automatic combinations into the document, as today. «Occupato» lasts until the engine returns, also for a request overtaken.</item>
/// <item>The search of the reinforcement and the independent global calculation take the place of the calculation (one scheduler in the
/// historic view): starting one cancels the calculation not yet published, an edit or «Ricalcola» cancels them, and they start when the
/// running engine returns.</item>
/// <item>Results: combinations, views, members, filters, the panels as plain blocks (<see cref="WallPanelBlock"/>), the section that can be
/// sent to the module c.a. (<see cref="ISheetHost.OpenModuleCopy"/>), the drawings as plain data.</item>
/// <item>The window of the values used in the calculation: <see cref="RetainingWallParametersViewModel"/>, with its own calculation, shown as
/// a modal dialog of the services (decision MU10 of 17/10); the bar schedule is a dialog of the view on <see cref="OpenBarSchedule"/>.</item>
/// <item>«Invia / carica terreno…»: the shared soil transfer dialog (decisions T5 and MU9 of 17/10).</item>
/// <item>The buttons of the view are commands (RetainingWallSheetViewModel.Commands.cs); the drawings are plain data and the objects they
/// draw, redrawn by the view at <see cref="DrawingsChanged"/>.</item>
/// <item>Read-only (a previous revision): forms, tables and the commands that change the data are refused; combinations, views, filters,
/// switches, the global calculation, the report and the calculation stay available. The window of the values is read-only too (the
/// historic one is not: a defect, see docs/refactoring/f5-9-muri.md).</item>
/// </list>
/// Texts, rows and rules come from <see cref="RetainingWallPresentation"/> and <see cref="RetainingWallFields"/>, shared with the historic view.
/// The equivalence is proved without WPF by tests/ANTHEA.Presentation.Tests (RetainingWallSheetChecks) against the traces of the historic
/// view frozen in tests/fixtures/f5-muri and against the F3 reference.
/// </summary>
public sealed partial class RetainingWallSheetViewModel : SheetViewModelBase
{
    /// <summary>The module of the sheet.</summary>
    public const string ModuleKey = "geo_muri_sostegno";

    /// <summary>Interface state that the sheet writes into its data: none (the display columns of the layers are part of the document, as today).</summary>
    public static IReadOnlyList<string> UiStateKeys { get; } = [];

    /// <summary>Delay of the automatic recalculation after an edit, as the historic view.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(450);

    private readonly IRetainingWallFigureRenderer figures;
    private readonly WallEngineWatch engine;
    private readonly RecalculationScheduler secondary = new();
    private readonly Dictionary<string, FormViewModel> forms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WallCardViewModel> cards = new(StringComparer.Ordinal);
    private bool building = true, engineBusy, secondaryBusy, designEnabled = true;
    private int revision, page;
    private WallModel.Result? calculationResult;
    private string status = RetainingWallPresentation.StatusToVerify, summary = "";
    private WallStatusSeverity statusSeverity;
    private Task pending = Task.CompletedTask;
    private bool pendingAutomatic = true;
    private readonly List<RetainingWallParametersViewModel> parameters = [];

    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the module.</param>
    /// <param name="figures">Draws the figures of the report.</param>
    public RetainingWallSheetViewModel(SheetContext context, IRetainingWallFigureRenderer figures) : this(context, figures, new WallEngineWatch(context.Calculation)) { }

    private RetainingWallSheetViewModel(SheetContext context, IRetainingWallFigureRenderer figures, WallEngineWatch engine)
        : base(ModuleKey, new SheetContext(context.Data, context.ReadOnly, context.Host, context.Services, engine, context.Limiter), RecalculationDelay, SheetCalculationMode.Background)
    {
        ArgumentNullException.ThrowIfNull(figures);
        this.figures = figures; this.engine = engine;
        Service = context.Calculation;
        CreateCommands();
        engine.Ended += () => { if (!engineBusy) return; engineBusy = false; OnPropertyChanged(nameof(IsBusy)); };
        PropertyChanged += OnStateChanged;
        Report = new RetainingWallReport(this);
        WallModel.Upgrade(Data);
        Families = WallModel.Families.Select(f => new WallFamily(f.Id, f.Name + (f.Available ? "" : " · previsto"), f.Available)).ToArray();
        family = Data.S("family");
        BuildInputs();
        if (Data.Array("combinations").Count > 0) BuildMatrix();
        building = false;
        UpdateFields();
        Preview();
        _ = ScheduleCalculationAsync();
    }

    public override IModuleReportSource Report { get; }

    /// <summary>The calculation service of the context, without the watch of the engine: the window of the values validates with it.</summary>
    internal IModuleService Service { get; }

    /// <summary>Busy while an engine of the sheet runs: the calculation (until the engine returns) or a search.</summary>
    public override bool IsBusy => engineBusy || secondaryBusy;

    /// <summary>The last work requested: the automatic recalculation, «Ricalcola», the global search or the search of the reinforcement.</summary>
    public Task PendingWork => pendingAutomatic ? PendingCalculation : pending;

    /// <summary>The revision of the edits of the user (the «revision» of the historic view): the proposal of the reinforcement and the
    /// window of the values are refused when it changed.</summary>
    internal int EditRevision => revision;

    private void Track(Task task) { pending = task; pendingAutomatic = false; }

    /// <summary>The status line.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>The colour of the status line, set when a calculation is published without errors and kept otherwise, as today.</summary>
    public WallStatusSeverity StatusSeverity { get => statusSeverity; private set => SetProperty(ref statusSeverity, value); }

    /// <summary>The page shown: 0 «Input», 1 «Verifiche». Some commands open a page, as the historic view.</summary>
    public int Page { get => page; set { if (value is 0 or 1) SetProperty(ref page, value); } }

    /// <summary>The result of the current data, null without one.</summary>
    public WallModel.Result? Calculation => calculationResult;

    /// <summary>The summary under the drawing of the inputs.</summary>
    public string Summary { get => summary; private set => SetProperty(ref summary, value); }

    public IReadOnlyDictionary<string, FormViewModel> Forms => forms;

    public IReadOnlyDictionary<string, WallCardViewModel> Cards => cards;

    // ---------------------------------------------------------------- typology

    private string family;

    /// <summary>The typologies; those planned for future developments cannot be chosen.</summary>
    public IReadOnlyList<WallFamily> Families { get; }

    /// <summary>The typology of the wall (its identifier); refused for a typology not available, when read-only or while building.</summary>
    public string Family
    {
        get => family;
        set
        {
            if (!CanEdit || value == family || !Families.Any(f => f.Id == value && f.Available)) return;
            family = value; OnPropertyChanged();
            Data["family"] = value; UpdateFields(); UserChanged();
        }
    }

    /// <summary>«Mensola esempio» / «Gravità esempio»: the example of the typology replaces the data.</summary>
    public void LoadExample(string example)
    {
        if (!CanEdit) return;
        building = true;
        try
        {
            var d = WallModel.Example(example); Data.Clear(); foreach (var p in d) Data[p.Key] = p.Value?.DeepClone();
            family = example; OnPropertyChanged(nameof(Family)); BuildInputs(); BuildMatrix(); UpdateFields();
        }
        finally { building = false; }
        UserChanged();
    }

    // ---------------------------------------------------------------- calculation

    /// <summary>«Ricalcola» (also when read-only): drafts written, the searches cancelled, the calculation at once; true when published.</summary>
    public Task<bool> RecalculateAsync()
    {
        ThrowIfDisposed();
        Commit();
        secondary.Invalidate();
        var task = CalculateAsync();
        Track(task);
        return task;
    }

    // ---------------------------------------------------------------- the window of the values used in the calculation

    /// <summary>The windows of the values open.</summary>
    public IReadOnlyList<RetainingWallParametersViewModel> ParameterWindows => parameters;

    /// <summary>
    /// «Valori di calcolo…» (also when read-only): the calculation at once, then the window of the values on the data and the result of
    /// now; null when the sheet was closed meanwhile. The window (<see cref="RetainingWallParametersViewModel"/>) is closed with the sheet.
    /// </summary>
    public async Task<RetainingWallParametersViewModel?> OpenParametersAsync()
    {
        await RecalculateAsync();
        if (IsDisposed) return null;
        Commit();
        var child = new RetainingWallParametersViewModel(this, revision, calculationResult?.Cases.Select(c => c.Name).ToArray() ?? [],
            selectedCombination ?? calculationResult?.Cases.FirstOrDefault()?.Name);
        parameters.Add(child);
        child.Closed += (_, _) => parameters.Remove(child);
        return child;
    }

    /// <summary>
    /// The button «Valori di calcolo…» (decision MU10 of 17/10): <see cref="OpenParametersAsync"/>, then the window as a modal dialog of the
    /// services (<see cref="IDialogService"/>, view in src/ANTHEA.Desktop/Views/RetainingWall). When the dialog returns, also because its window was
    /// closed during the check, the window is closed: nothing more is applied.
    /// </summary>
    public async Task ShowParametersAsync()
    {
        var child = await OpenParametersAsync();
        if (child is null) return;
        try { await Context.Services.Dialogs.ShowDialogAsync(child); }
        finally { child.Close(); }
    }

    /// <summary>The values checked by the window replace the data: the inputs and the matrix rebuilt, the edit of the user.</summary>
    internal void ApplyParameters(JsonObject values)
    {
        Data.Clear(); foreach (var p in values) Data[p.Key] = p.Value?.DeepClone();
        BuildInputs(); BuildMatrix(); UpdateFields(); UserChanged();
    }

    protected override void OnCommit()
    {
        foreach (var form in forms.Values) form.Commit();
    }

    /// <summary>The user changed the data (Changed of the historic view).</summary>
    private void UserChanged()
    {
        if (building || IsDisposed) return;
        revision++; secondary.Invalidate(); SetResult(null); UpdateSeismicStatus(); ChecksPanel = null; PressurePanel = null;
        independentGlobal = null; independentInput = null; independentError = null; SetGlobalCombinations(null); RefreshGlobal();
        SelectSection(null);
        SetCombinations(null); Summary = RetainingWallPresentation.ResultsToUpdate; diagramCalculation = null; diagramCase = null; NotifyDrawings(); Preview();
        Status = RetainingWallPresentation.StatusPending;
        Changed();
        pendingAutomatic = true;
    }

    private void SetResult(WallModel.Result? result)
    {
        if (ReferenceEquals(calculationResult, result)) return;
        calculationResult = result; OnPropertyChanged(nameof(Calculation)); OnPropertyChanged(nameof(GlobalResult));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        // The buttons of the view follow the commands: «Calcola armature» and «Apri sezione in c.a.» their own state.
        if (e.PropertyName == nameof(CanDesign)) DesignRebarCommand?.NotifyCanExecuteChanged();
        if (e.PropertyName == nameof(CanSendSection)) SendSectionCommand?.NotifyCanExecuteChanged();
        if (e.PropertyName != nameof(CanEdit)) return;
        // A read-only revision (or the closing): the forms refuse what the user types, the commands that change the data are refused.
        foreach (var form in forms.Values) form.IsReadOnly = !CanEdit;
        foreach (var child in parameters) child.Refresh();
        if (!IsDisposed) SelectSection(selectedSection);
        OnPropertyChanged(nameof(CanDesign));
        NotifyCommands();
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The engine of the calculation starts: busy until it returns (WallEngineWatch), the status of the historic work.
        if (e.PropertyName == nameof(CalculationState) && CalculationState == CalculationState.Running)
        {
            if (!engineBusy) { engineBusy = true; OnPropertyChanged(nameof(IsBusy)); }
            Status = RetainingWallPresentation.StatusRunning;
        }
    }

    protected override void OnCalculationPublished()
    {
        try
        {
            if (CalculationState != CalculationState.Completed || PublishedCalculation is not { } published) throw new ArgumentException(CalculationMessage);
            var result = WallModel.Read(published); SetResult(result); ShowResults();
            var (text, severity) = RetainingWallPresentation.Status(result);
            Status = text; StatusSeverity = severity;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            SetResult(null); Status = RetainingWallPresentation.StatusError(error.Message);
        }
    }

    /// <summary>The data were replaced (Reload): the searches cancelled, the results cleared, the inputs and the matrix rebuilt as at the
    /// opening; the choices of the views stay. The calculation follows with the delay of the module.</summary>
    protected override void OnReloaded()
    {
        building = true;
        try
        {
            WallModel.Upgrade(Data);
            family = Data.S("family"); OnPropertyChanged(nameof(Family));
            secondary.Invalidate(); revision++; SetResult(null); independentGlobal = null; independentInput = null; independentError = null;
            ChecksPanel = null; PressurePanel = null; SetGlobalCombinations(null); SetCombinations(null); SelectSection(null);
            Summary = ""; diagramCalculation = null; diagramCase = null; Status = RetainingWallPresentation.StatusToVerify;
            matrix = null; OnPropertyChanged(nameof(Matrix)); MatrixStatus = "";
            BuildInputs(); if (Data.Array("combinations").Count > 0) BuildMatrix();
        }
        finally { building = false; }
        UpdateFields(); Preview();
        pendingAutomatic = true;
    }

    protected override void OnDisposed()
    {
        secondary.Dispose();
        foreach (var child in parameters.ToArray()) child.Dispose();
        parameters.Clear();
        base.OnDisposed();
    }

    // ---------------------------------------------------------------- report and exports

    /// <summary>«Relazione Word» (also when read-only): the window exports the report.</summary>
    public void RequestReport() => Context.Host.RequestReport();

    /// <summary>
    /// «Invia / carica terreno…» (refused when read-only, as the historic button protected by the revision): the sheet commits, then the
    /// shared soil transfer dialog (<see cref="SoilTransferDialogViewModel"/> on the dialog service, decisions T5 and MU9 of 17/10) receives
    /// its module, its data and the soil sheets of the project; a copy goes to the window as <see cref="ISheetHost.OpenModuleCopy"/>, a
    /// replacement as <see cref="ISheetHost.ReplaceSoil"/>, as SheetEditor.OpenSoilTransfer of the historic view.
    /// </summary>
    public async Task TransferSoilAsync()
    {
        if (!CanEdit) return;
        Commit();
        var choice = await new SoilTransferDialogService(Context.Services).ShowAsync(ModuleId, Data, Context.Host.SoilSources());
        if (choice is null || IsDisposed) return;
        if (choice.Module is null) Context.Host.ReplaceSoil(choice.Data);
        else Context.Host.OpenModuleCopy(choice.Module, choice.Data, choice.Name);
    }

    /// <summary>«CSV»: the checks, or the slices of the global stability shown; refused without a result or while busy, as today.</summary>
    public void ExportCsv()
    {
        bool global = SelectedView == "Stabilità globale" && GlobalResult is not null;
        if ((!global && calculationResult is null) || IsBusy) { Context.Services.Messages.Inform("", RetainingWallPresentation.WaitBeforeExport); return; }
        string? path = Context.Services.Files.SaveFile(new SaveFileRequest([new FileFilter("Tabelle CSV", "*.csv")], global ? "Muro_stabilita_globale_conci.csv" : "Muro_verifiche.csv"));
        if (path is null) return;
        string text = global ? WallExports.GlobalCsv(GlobalResult!) : WallExports.Csv(calculationResult!);
        Archivio.ScriviAtomico(path, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray());
    }

    /// <summary>The DOCX of the report: the existing generator on the result, with the combination and the global combination shown.</summary>
    private byte[] BuildReport(string title)
    {
        if (calculationResult is null || IsBusy) throw new InvalidOperationException("Attendere il calcolo del muro.");
        var context = WallReportPresentation.Prepare(calculationResult, drawingCase ?? calculationResult.Cases[0], GlobalCase);
        var images = figures.Render(calculationResult, context).Select(f => new ReportRetainingWall.Figure(f.Caption, f.Png, f.AspectRatio)).ToList();
        return DocxWriter.Write(WallExports.CreateDocument(title, calculationResult, images));
    }

    private sealed class RetainingWallReport(RetainingWallSheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) => sheet.BuildReport(title);
    }
}

/// <summary>A typology of the wall in the header: identifier, name (planned ones marked) and whether it can be chosen.</summary>
public sealed record WallFamily(string Id, string Name, bool Available);
