using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Services;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Modules.BridgeDesign;

/// <summary>
/// Sheet of Bridge Design (str_bridge_design), F5.12: the behaviour of the historic view BridgeDesignWorkspace without WPF, on the same
/// document, with the same writes in the same order.
/// <list type="bullet">
/// <item>Opening: the preparation of the module (ModuleSheetPreparation.PrepareBridgeDesign: advanced defaults, traffic of the archives,
/// environmental factors), the tabs of the inputs, the prices, the assumptions and the search, then the calculation at once.</item>
/// <item>Parameters (<see cref="BridgeDesignParameter"/>): the raw text into the document at every change, the red border of an invalid
/// value, the slider; choices, options and the buttons of the typologies (<see cref="BridgeDesignPresentation.SelectFamily"/>) write the inputs;
/// the deck, the traffic model and the buttons that change the dimensions rebuild the tabs. Every edit is recorded for «Annulla»
/// (<see cref="BridgeDesignHistory"/>), synchronizes the environmental factors and invalidates the results.</item>
/// <item>Calculation (<see cref="SheetCalculationMode.Background"/>): the analysis of the bridge, of the alternative A and of the alternatives
/// (<see cref="BridgeDesignAnalysis"/>) on a copy, on a background thread, through the calculation service of the context; automatic 450 ms
/// after the last edit, at once at the opening and with «Calcola»; only the current revision is published. «Occupato» lasts until the engine
/// returns, also for a request overtaken.</item>
/// <item>Results: metrics, configuration, quantities, details with the moment diagram, comparison A / B, prices, assumptions, sections and
/// dimensions, the alternatives, as items (<see cref="BridgeDesignItem"/>); the drawing as data (<see cref="DrawingData"/>, <see cref="DrawingResult"/>).</item>
/// <item>The search of the optimum (BridgeDesignSheetViewModel.Optimization.cs) and the price editor (<see cref="PriceCatalogueViewModel"/> on
/// the dialog service).</item>
/// <item>Read-only (a previous revision): parameters, choices, options and the commands that change the data are refused; pages, tabs, the
/// view of the drawing, the selection of the solutions, «Interrompi», «Suggerisci campate», «Aggiorna elenco», the exports, the report and the
/// calculation stay available. The price editor is refused (the historic view opens it and its «Applica» writes the revision: a defect, see
/// docs/refactoring/f5-12-bridge-design.md).</item>
/// </list>
/// Texts, rows and rules come from <see cref="BridgeDesignPresentation"/> and <see cref="BridgeDesignOptimizationPresentation"/>, shared with
/// the historic view. The equivalence is proved without WPF by tests/ANTHEA.Presentation.Tests (BridgeDesignSheetChecks) against the traces of
/// the historic view frozen in tests/fixtures/f5-bridge-design and against the F3 reference.
/// </summary>
public sealed partial class BridgeDesignSheetViewModel : SheetViewModelBase
{
    /// <summary>The module of the sheet.</summary>
    public const string ModuleKey = BridgeDesignModel.Module;

    /// <summary>Interface state that the sheet writes into its data: the scenery of the drawing («Nuovo paesaggio», «Ponte casuale»).</summary>
    public static IReadOnlyList<string> UiStateKeys { get; } = ["scene"];

    private readonly IBridgeDesignFigureRenderer figures;
    private readonly BridgeDesignEngine engine;
    private BridgeDesignHistory history;
    private bool engineBusy;
    private BridgeDesignAnalysis? analysis;
    private BridgeDesignModel.Result? calculationResult;
    private string status = "", configuration = "";
    private bool statusError;
    private int page, inputTab, outputTab;
    private bool sectionView;
    private JsonObject? drawingData;
    private BridgeDesignModel.Result? drawingResult;
    private IReadOnlyList<BridgeDesignMetric> metrics;
    private Task pending = Task.CompletedTask;
    private bool pendingAutomatic;

    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the module.</param>
    /// <param name="figures">Draws the bridge for the PNG export and the report.</param>
    public BridgeDesignSheetViewModel(SheetContext context, IBridgeDesignFigureRenderer figures) : this(context, figures, new BridgeDesignEngine(context.Calculation)) { }

    private BridgeDesignSheetViewModel(SheetContext context, IBridgeDesignFigureRenderer figures, BridgeDesignEngine engine)
        : base(ModuleKey, new SheetContext(context.Data, context.ReadOnly, context.Host, context.Services, engine, context.Limiter),
            BridgeDesignPresentation.RecalculationDelay, SheetCalculationMode.Background)
    {
        ArgumentNullException.ThrowIfNull(figures);
        this.figures = figures; this.engine = engine;
        engine.Ended += () => { if (!engineBusy) return; engineBusy = false; OnPropertyChanged(nameof(IsBusy)); };
        PropertyChanged += OnStateChanged;
        Report = new BridgeDesignReport(this);
        metrics = BridgeDesignPresentation.MetricLabels.Select(l => new BridgeDesignMetric(l, BridgeDesignPresentation.MetricEmpty, "")).ToArray();
        CreateCommands();
        ModuleSheetPreparation.PrepareBridgeDesign(Data);
        history = new BridgeDesignHistory(Data);
        BuildInputs();
        BuildPrices();
        BuildAssumptions();
        CreateOptimization();
        Open();
    }

    public override IModuleReportSource Report { get; }

    /// <summary>Busy while the engine of the sheet runs, until it returns.</summary>
    public override bool IsBusy => engineBusy;

    /// <summary>The result of the current data, a new object at every read (as the historic view), null without one.</summary>
    public override JsonObject? Result => calculationResult?.Json();

    public override bool HasResults => calculationResult is not null;

    /// <summary>The result shown, null without one.</summary>
    public BridgeDesignModel.Result? Calculation => calculationResult;

    /// <summary>The analysis shown: the alternative A and the alternatives, null without a result.</summary>
    public BridgeDesignAnalysis? Analysis => analysis;

    /// <summary>The last work requested: the calculation of the opening, the automatic one or «Calcola».</summary>
    public Task PendingWork => pendingAutomatic ? PendingCalculation : pending;

    /// <summary>The status line.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>The status line shows an error (red), set when a calculation is published and kept otherwise, as the historic view.</summary>
    public bool StatusIsError { get => statusError; private set => SetProperty(ref statusError, value); }

    /// <summary>The four metrics under the drawing.</summary>
    public IReadOnlyList<BridgeDesignMetric> Metrics { get => metrics; private set => SetProperty(ref metrics, value); }

    /// <summary>The line under the drawing.</summary>
    public string Configuration { get => configuration; private set => SetProperty(ref configuration, value); }

    /// <summary>The page of the sheet: 0 «Progetto», 1 «Ottimizzazione».</summary>
    public int Page { get => page; set { if (value is 0 or 1) SetProperty(ref page, value); } }

    /// <summary>The tab of the inputs (0–4).</summary>
    public int InputTab { get => inputTab; set { if (value >= 0 && value < BridgeDesignPresentation.InputTabs.Count) SetProperty(ref inputTab, value); } }

    /// <summary>The tab of the results (0–5).</summary>
    public int OutputTab { get => outputTab; set { if (value >= 0 && value < BridgeDesignPresentation.OutputTabs.Count) SetProperty(ref outputTab, value); } }

    /// <summary>The drawing shows the cross section («Sezione») or the elevation («Prospetto»): view state, also when read-only.</summary>
    public bool SectionView { get => sectionView; set => SetProperty(ref sectionView, value); }

    /// <summary>The data drawn: the data of the sheet from the first result on, null before.</summary>
    public JsonObject? DrawingData { get => drawingData; private set => SetProperty(ref drawingData, value); }

    /// <summary>The result drawn, null without a result («Completa i dati per visualizzare il ponte»).</summary>
    public BridgeDesignModel.Result? DrawingResult { get => drawingResult; private set => SetProperty(ref drawingResult, value); }

    /// <summary>The undo states recorded.</summary>
    public int UndoCount => history.Count;

    // ---------------------------------------------------------------- calculation

    /// <summary>
    /// «Calcola» (also when read-only): the environmental factors synchronized, then the calculation at once; the results shown stay until
    /// it is published. True when published.
    /// </summary>
    public override Task<bool> CalculateAsync(CancellationToken cancellation = default)
    {
        ThrowIfDisposed();
        Synchronize();
        var task = base.CalculateAsync(cancellation);
        pending = task; pendingAutomatic = false;
        return task;
    }

    /// <summary>Waits for the last calculation requested, also when a later one takes its place while waiting.</summary>
    public async Task WaitForCalculationAsync()
    {
        Task current;
        do { current = PendingWork; await current; }
        while (!ReferenceEquals(current, PendingWork));
    }

    // The opening: the results cleared and the calculation at once.
    private void Open()
    {
        ClearCalculation(); InvalidateOptimization();
        Status = BridgeDesignPresentation.StatusPending; StatusIsError = false;
        _ = CalculateAsync();
    }

    // The environmental factors written into the data before the calculation (an unfinished value is validated by the calculation).
    private void Synchronize()
    {
        try { Anthea.Serialization.BridgeEnvironmentData.Synchronize(Data); } catch (ArgumentException) { /* The calculation validates unfinished inputs. */ }
    }

    /// <summary>The user changed the data (Changed of the historic view): recorded for «Annulla», results cleared, the search invalidated, recalculation after 450 ms, Modified.</summary>
    private void UserChanged()
    {
        if (IsDisposed) return;
        history.Record(Data);
        Recalculate();
        OnPropertyChanged(nameof(UndoCount));
    }

    private void Recalculate()
    {
        ClearCalculation(); InvalidateOptimization();
        Status = BridgeDesignPresentation.StatusPending; StatusIsError = false;
        Synchronize();
        Changed();
        pendingAutomatic = true;
    }

    /// <summary>A change of the data by a button: the change, the edit of the user, then the tabs rebuilt when asked.</summary>
    private void Mutate(Action change, bool rebuild = false)
    {
        change(); UserChanged();
        if (rebuild) RefreshInputs();
    }

    private void ClearCalculation()
    {
        analysis = null; calculationResult = null; DrawingResult = null; comparedA = null;
        Metrics = BridgeDesignPresentation.MetricLabels.Select(l => new BridgeDesignMetric(l, BridgeDesignPresentation.MetricEmpty, BridgeDesignPresentation.MetricMissing)).ToArray();
        Quantities = Details = Comparison = Advice = Technical = null; Configuration = "";
        OnPropertyChanged(nameof(Calculation)); OnPropertyChanged(nameof(Analysis)); OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults));
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The engine starts: busy until it returns, with the status of the historic work (its colour does not change).
        if (e.PropertyName == nameof(CalculationState) && CalculationState == CalculationState.Running)
        {
            if (!engineBusy) { engineBusy = true; OnPropertyChanged(nameof(IsBusy)); }
            Status = BridgeDesignPresentation.StatusRunning;
        }
    }

    protected override void OnCalculationPublished()
    {
        if (CalculationState != CalculationState.Completed || PublishedCalculation is not { } published)
        {
            ClearCalculation(); Status = CalculationMessage; StatusIsError = true;
            return;
        }
        try
        {
            analysis = published.NativeAs<BridgeDesignAnalysis>(); calculationResult = analysis.Current;
            DrawingData = Data; DrawingResult = calculationResult;
            var values = BridgeDesignPresentation.Metrics(calculationResult);
            Metrics = BridgeDesignPresentation.MetricLabels.Select((l, k) => new BridgeDesignMetric(l, values[k].Value, values[k].Caption)).ToArray();
            Configuration = BridgeDesignPresentation.Configuration(calculationResult);
            ShowQuantities(calculationResult); ShowDetails(calculationResult); ShowComparison(); ShowAdvice(); ShowTechnical(calculationResult); InvalidateOptimization();
            Status = BridgeDesignModel.Scope; StatusIsError = false;
        }
        catch (ArgumentException error)
        {
            ClearCalculation(); Status = error.Message; StatusIsError = true;
        }
        OnPropertyChanged(nameof(Calculation)); OnPropertyChanged(nameof(Analysis)); OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults));
    }

    /// <summary>The data were replaced (Reload): as a new opening of the sheet on the new data; the calculation follows with the delay of the module.</summary>
    protected override void OnReloaded()
    {
        CancelOptimization();
        ModuleSheetPreparation.PrepareBridgeDesign(Data);
        history = new BridgeDesignHistory(Data); OnPropertyChanged(nameof(UndoCount));
        Page = 0; InputTab = 0; OutputTab = 0; SectionView = false; DrawingData = null;
        editors.Clear();
        BuildInputs(); BuildPrices(); BuildAssumptions(); ResetOptimization();
        ClearCalculation(); InvalidateOptimization();
        Status = BridgeDesignPresentation.StatusPending; StatusIsError = false;
        Synchronize();
        pendingAutomatic = true;
    }

    protected override void OnDisposed()
    {
        CancelOptimization();
        base.OnDisposed();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(CanEdit)) NotifyCommands();
    }

    // ---------------------------------------------------------------- commands of the drawing card

    /// <summary>«Prospetto».</summary>
    public IRelayCommand ElevationCommand { get; private set; } = null!;
    /// <summary>«Sezione».</summary>
    public IRelayCommand SectionCommand { get; private set; } = null!;
    /// <summary>«Nuovo paesaggio».</summary>
    public IRelayCommand NewSceneCommand { get; private set; } = null!;
    /// <summary>«Ponte casuale».</summary>
    public IRelayCommand RandomCommand { get; private set; } = null!;
    /// <summary>«Ottimizza»: the page of the search.</summary>
    public IRelayCommand OptimizeCommand { get; private set; } = null!;
    /// <summary>«Fissa A».</summary>
    public IRelayCommand PinCommand { get; private set; } = null!;
    /// <summary>«Annulla».</summary>
    public IRelayCommand UndoCommand { get; private set; } = null!;
    /// <summary>«Sezioni e quote».</summary>
    public IRelayCommand DimensionsCommand { get; private set; } = null!;
    /// <summary>«Esporta quantità CSV».</summary>
    public IRelayCommand ExportCsvCommand { get; private set; } = null!;
    /// <summary>«Immagine PNG».</summary>
    public IRelayCommand ExportPngCommand { get; private set; } = null!;

    private void CreateCommands()
    {
        ElevationCommand = new RelayCommand(() => SectionView = false);
        SectionCommand = new RelayCommand(() => SectionView = true);
        NewSceneCommand = new RelayCommand(NewScene, () => CanEdit);
        RandomCommand = new RelayCommand(() => Randomize(Random.Shared), () => CanEdit);
        OptimizeCommand = new RelayCommand(() => Page = 1);
        PinCommand = new RelayCommand(Pin, () => CanEdit);
        UndoCommand = new RelayCommand(Undo, () => CanEdit);
        DimensionsCommand = new RelayCommand(() => OutputTab = 5);
        ExportCsvCommand = new RelayCommand(ExportCsv);
        ExportPngCommand = new RelayCommand(ExportPng);
        AutoSizeCommand = new RelayCommand(AutoSize, () => CanEdit);
        RecommendedTrafficCommand = new RelayCommand(ApplyRecommendedTraffic, () => CanEdit);
        FamilyCommand = new RelayCommand<string>(id => { if (id is not null) SelectFamily(id); }, _ => CanEdit);
        ManagePricesCommand = new AsyncRelayCommand(ManagePricesAsync);
        IndicativeRatesCommand = new RelayCommand(ApplyIndicativeRates, () => CanEdit);
        RestoreACommand = new RelayCommand(RestoreA, () => CanEdit);
        RemoveComparisonCommand = new RelayCommand(RemoveComparison, () => CanEdit);
        RestoreEnvironmentCommand = new RelayCommand(RestoreEnvironment, () => CanEdit);
        ExportTechnicalCommand = new RelayCommand(ExportTechnicalCsv);
        CreateOptimizationCommands();
    }

    private void NotifyCommands()
    {
        foreach (var command in new IRelayCommand[] { NewSceneCommand, RandomCommand, PinCommand, UndoCommand, AutoSizeCommand, RecommendedTrafficCommand, FamilyCommand,
            IndicativeRatesCommand, RestoreACommand, RemoveComparisonCommand, RestoreEnvironmentCommand })
            command.NotifyCanExecuteChanged();
        foreach (var command in alternativeCommands) command.NotifyCanExecuteChanged();
        NotifyOptimizationCommands();
    }

    /// <summary>«Nuovo paesaggio»: the next scenery of the drawing.</summary>
    public void NewScene() { if (CanEdit) Mutate(() => BridgeDesignPresentation.NewScene(Data)); }

    /// <summary>«Ponte casuale» with the generator <paramref name="random"/>: a typology and a plausible bridge, the tabs rebuilt.</summary>
    public void Randomize(Random random) { if (CanEdit) Mutate(() => BridgeDesignPresentation.Randomize(Data, random), true); }

    /// <summary>«Fissa A»: with a result, the current design pinned as the alternative A; the comparison shown.</summary>
    public void Pin()
    {
        if (!CanEdit || calculationResult is null) return;
        var a = BridgeDesignPresentation.Pinned(Data); Mutate(() => Data["alternative_a"] = a);
        OutputTab = 2;
    }

    /// <summary>«Annulla»: the data before the last edit, the tabs rebuilt, the recalculation after 450 ms; nothing is recorded.</summary>
    public void Undo()
    {
        if (!CanEdit || !history.Undo(Data)) return;
        RefreshInputs();
        Recalculate();
        OnPropertyChanged(nameof(UndoCount));
    }

    // ---------------------------------------------------------------- exports and report

    private void Save(string filter, string pattern, string name, Func<byte[]> content)
    {
        if (calculationResult is null) return;
        string? path = Context.Services.Files.SaveFile(new SaveFileRequest([new FileFilter(filter, pattern)], name));
        if (path is null) return;
        try { Archivio.ScriviAtomico(path, content()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Context.Services.Messages.Inform("Esportazione non riuscita", ex.Message); }
    }

    private static byte[] Utf8WithBom(string text) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();

    /// <summary>«Esporta quantità CSV» (also when read-only, with a result).</summary>
    public void ExportCsv() => Save("Quantità CSV", "*.csv", "BridgeDesign_quantita.csv", () => Utf8WithBom(BridgeDesignModel.Csv(calculationResult!)));

    /// <summary>«Immagine PNG» (also when read-only, with a result): the drawing shown.</summary>
    public void ExportPng() => Save("Immagine PNG", "*.png", "BridgeDesign.png", () => figures.Drawing(Data, calculationResult!, SectionView));

    /// <summary>«Esporta sezioni e quote CSV» (also when read-only, with a result).</summary>
    public void ExportTechnicalCsv() => Save("Sezioni e quote CSV", "*.csv", "BridgeDesign_sezioni_quote.csv", () => Utf8WithBom(BridgeDesignModel.TechnicalCsv(Data, calculationResult!)));

    /// <summary>The texts of the page of «Stampa scheda», null without a result (the view prints them with the drawing).</summary>
    public IReadOnlyList<string>? PrintLines => calculationResult is null ? null : BridgeDesignPresentation.PrintLines(calculationResult);

    /// <summary>The document of the report: the existing generator on the data and the result, with the elevation and the section.</summary>
    public DocxDocument BuildReportDocument(string title)
    {
        var r = calculationResult ?? throw new InvalidOperationException("Completare i dati del ponte.");
        return BridgeDesignModel.ReportDocument(title, Data, r, figures.Drawing(Data, r, false), figures.Drawing(Data, r, true));
    }

    private sealed class BridgeDesignReport(BridgeDesignSheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) => DocxWriter.Write(sheet.BuildReportDocument(title));
    }
}
