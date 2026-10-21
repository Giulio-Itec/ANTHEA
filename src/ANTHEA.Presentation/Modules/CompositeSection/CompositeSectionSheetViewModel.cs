using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Services;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>
/// Sheet of the composite bridge section (str_mista_ponte), F5.11: the behaviour of the historic view BridgeWorkspace without WPF, on the
/// same document and with the same writes, in the same order.
/// <list type="bullet">
/// <item>Opening: the shape checked, the accessories and the options of the calculation completed, the forms, the phases prepared and
/// homogenized, the view state of the sheet migrated (<c>ui_mista</c>), the slab properties, the local sketch and the path of the curves
/// (<c>curve_sezione</c>) completed, the page saved in the sheet chosen, then the preview and the automatic calculation 500 ms after.</item>
/// <item>Forms (<see cref="CompositeSectionFields"/>): the raw text into the document at every change, as InputForm; the fields shown by the
/// data (<see cref="BridgeInputPresentation.FieldShown"/>). What the historic view queued on its dispatcher during a change (the cards of
/// the phases rebuilt, the coefficients of a new standard shown again) follows the change, in the same order.</item>
/// <item>Phases: one card each (<see cref="CompositeSectionPhaseCard"/>) and the table of the actions (rows on the phases themselves, the
/// cells that do not apply to a kind refused); added, moved and deleted. The page of the phases rebuilds the cards when it is entered.</item>
/// <item>Calculation (<see cref="SheetCalculationMode.Background"/>, as the historic view): on a copy of the data, on a background thread,
/// through the <see cref="Anthea.Application.Modules.IModuleService"/> of the context; automatic 500 ms after the last edit, «Calcola» at
/// once; only the calculation of the current data is published; busy until the engine returns, also for a request overtaken by an edit
/// (<see cref="CompositeSectionEngineWatch"/>). The results of the last calculation stay shown, «da aggiornare», until the next one.</item>
/// <item>View state saved in the sheet (<see cref="UiStateKeys"/>): page, display, tab of the results, labels, situation, neutral axis,
/// contours, limits, scale of the concrete, local sketch, slab properties, splitters (of the view), report sections, the path of the curves:
/// written as the historic view writes them, with Modified and without recalculation.</item>
/// <item>The curves of the section: a second calculation of the sheet, on request (<see cref="CalculateResponseAsync"/>).</item>
/// <item>Read-only (a previous revision): the forms are read-only (<see cref="Anthea.Presentation.Forms.FormViewModel.IsReadOnly"/>, T2), the table, the
/// commands and the switches that change the data are refused; pages, situations, displays, the neutral axis, tabs, details, splitters,
/// the detached view, exports, calculation and report stay available, as the controls that the historic view leaves to the revision.</item>
/// <item>The detached view is a concern of the host (<see cref="Detach"/>, <see cref="Reattach"/>): the sheet only knows that its drawing is
/// shown in a window of its own, which changes what the drawing shows and which controls the control panel shows.</item>
/// </list>
/// Texts, tables and rules come from Application (<see cref="BridgeInputPresentation"/>, <see cref="BridgeResultPresentation"/>,
/// <see cref="BridgeResponsePresentation"/>, <see cref="BridgeReportPresentation"/>, <see cref="BridgeSheetPresentation"/>), shared with the
/// historic view. The equivalence is proved without WPF by tests/ANTHEA.Presentation.Tests (CompositeSectionSheetChecks) against the traces
/// of the historic view frozen in tests/fixtures/f5-sezione-mista and against the F3 reference.
/// </summary>
public sealed partial class CompositeSectionSheetViewModel : SheetViewModelBase
{
    /// <summary>The module of the sheet.</summary>
    public const string ModuleKey = "str_mista_ponte";

    /// <summary>Interface state that the sheet writes into its data (<see cref="ModulePersistence"/>): the view state and the path of the curves.</summary>
    public static IReadOnlyList<string> UiStateKeys { get; } = ["ui_mista", "curve_sezione"];

    /// <summary>Delay of the automatic recalculation after an edit, as the historic view.</summary>
    public static TimeSpan RecalculationDelay => BridgeInputPresentation.RecalculationDelay;

    private readonly ICompositeSectionFigureRenderer figures;
    private readonly List<Action> deferred = [];
    private bool building = true, engineBusy;
    private string status = BridgeInputPresentation.StatusPreparing;
    private BridgeResultView? calculationResult, displayed;
    private JsonObject[] displayedPhases = [], requestedPhases = [];
    private JsonObject? selectedPhase;
    private bool followLatestStage = true;

    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the module.</param>
    /// <param name="figures">Draws the figures of the report.</param>
    /// <param name="response">The calculation of the curves of the section; null: <see cref="StructuralQueries.BridgeResponse"/>, as the historic view.</param>
    public CompositeSectionSheetViewModel(SheetContext context, ICompositeSectionFigureRenderer figures,
        Func<JsonObject, JsonObject, CancellationToken, SectionResponseResult>? response = null)
        : this(context, figures, response, new CompositeSectionEngineWatch(context.Calculation)) { }

    private CompositeSectionSheetViewModel(SheetContext context, ICompositeSectionFigureRenderer figures,
        Func<JsonObject, JsonObject, CancellationToken, SectionResponseResult>? response, CompositeSectionEngineWatch engine)
        : base(ModuleKey, new SheetContext(context.Data, context.ReadOnly, context.Host, context.Services, engine, context.Limiter), RecalculationDelay, SheetCalculationMode.Background)
    {
        ArgumentNullException.ThrowIfNull(figures);
        this.figures = figures;
        this.response = response ?? ((data, request, token) => Anthea.Application.Modules.StructuralQueries.BridgeResponse(data, request, token));
        engine.Ended += () => { if (!engineBusy) return; engineBusy = false; OnPropertyChanged(nameof(IsBusy)); };
        PropertyChanged += OnStateChanged;
        Report = new CompositeSectionReport(this);
        CreateCommands();
        Open();
    }

    /// <summary>
    /// The construction of the historic view, in its order (also the reload of the data): the inputs and the phases, the view state, the
    /// properties of the section, the switches, the local sketch, the curves, the page; then the preview and the automatic calculation.
    /// </summary>
    private void Open()
    {
        building = true;
        BridgeEditor.ValidateShape(Data); BridgeEditor.EnsureAccessoryDefaults(Data);
        BuildInputs();
        BuildLayout();
        building = false;
        RefreshPreview();
        requestedPhases = ActivePhases();
        automaticWork = true;
        _ = ScheduleCalculationAsync();
    }

    public override IModuleReportSource Report { get; }

    /// <summary>Busy while the engine of the calculation runs, until it returns (also for a request overtaken by an edit), as the historic view.</summary>
    public override bool IsBusy => engineBusy;

    /// <summary>The values of the result of the current data (BridgeResultView.Json, as the historic view gives them to the window); null without one.</summary>
    public override JsonObject? Result => calculationResult?.Json();

    /// <summary>A result of the current data, without building its values (<see cref="Result"/> serializes the whole result).</summary>
    public override bool HasResults => calculationResult is not null;

    /// <summary>The status line.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>The result of the current data, null without one (or after a change).</summary>
    public BridgeResultView? Calculation => calculationResult;

    /// <summary>The result shown: the last one published, also after a change («da aggiornare»).</summary>
    public BridgeResultView? DisplayedCalculation => displayed;

    /// <summary>The results shown are of data that changed since.</summary>
    public bool ResultsAreStale => calculationResult is null && displayed is not null;

    /// <summary>The report and the CSV can be exported: a result of the current data and no calculation running.</summary>
    public bool CanExport => calculationResult is not null && !IsBusy;

    private JsonObject[] ActivePhases() => Data.Array("fasi").OfType<JsonObject>().Where(p => p.B("attiva")).ToArray();

    // ---------------------------------------------------------------- calculation

    /// <summary>«Calcola» of the window (the host commits the sheet first, as SheetEditor did with the view): the calculation at once.</summary>
    public override Task<bool> CalculateAsync(CancellationToken cancellation = default)
    {
        ThrowIfDisposed();
        requestedPhases = ActivePhases();
        var task = base.CalculateAsync(cancellation);
        requestedWork = task; automaticWork = false;
        return task;
    }

    private Task requestedWork = Task.CompletedTask;
    private bool automaticWork = true;

    /// <summary>The last calculation requested: the automatic one (at opening, after an edit or a Reload) or the one of «Calcola».</summary>
    public Task PendingWork => automaticWork ? PendingCalculation : requestedWork;

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The engine starts: busy until it returns (CompositeSectionEngineWatch), the status of the historic work.
        if (e.PropertyName == nameof(CalculationState) && CalculationState == CalculationState.Running)
        {
            if (!engineBusy) { engineBusy = true; OnPropertyChanged(nameof(IsBusy)); }
            Status = BridgeInputPresentation.StatusRunning;
        }
        if (e.PropertyName == nameof(IsBusy)) { OnPropertyChanged(nameof(CanExport)); NotifyCommands(); }
        if (e.PropertyName == nameof(CanEdit)) ApplyReadOnly();
    }

    /// <summary>
    /// The publication of the historic view: the result read as the view read it (an error of the engine or a result without its typed part
    /// is shown in the status line), the situations, the situation kept (the same phase, or the last one when it followed the last), the
    /// results of that situation.
    /// </summary>
    protected override void OnCalculationPublished()
    {
        try
        {
            if (PublishedCalculation is not { } published) throw new InvalidOperationException(CalculationMessage);
            var computed = BridgeResultView.Read(published);
            var active = requestedPhases;
            int previous = selectedPhase is null ? selectedSituation : Array.IndexOf(active, selectedPhase);
            calculationResult = computed; displayed = computed; displayedPhases = active;
            Situations = BridgeResultPresentation.Situations(computed);
            if (previous < 0 && selectedPhase is null) previous = ViewIndex("fase", -1);
            selectedSituation = BridgeSheetPresentation.PublishedSituation(followLatestStage, previous, computed.Stages.Count);
            OnPropertyChanged(nameof(SelectedSituation));
            selectedPhase = active[selectedSituation];
            ShowResults(); UpdateResultNotice(); Status = BridgeInputPresentation.StatusUpdated(computed.Stages.Count);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            calculationResult = null; if (displayed is null) ClearResults(); UpdateResultNotice(); Status = BridgeInputPresentation.StatusInvalid(error.Message);
        }
        OnPropertyChanged(nameof(Calculation)); OnPropertyChanged(nameof(DisplayedCalculation)); OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultsAreStale)); OnPropertyChanged(nameof(CanExport));
    }

    /// <summary>
    /// The user changed the data (Changed of the historic view): homogenization, common level, table, results invalidated, curves stale,
    /// preview, notice, status, then the automatic calculation and Modified.
    /// </summary>
    private void UserChanged()
    {
        if (building || IsDisposed) return;
        SynchronizeHomogenization();
        UpdateCommonReferenceField();
        RefreshActionsTable();
        calculationResult = null;
        InvalidateResponse();
        if (displayed is null) ClearResults();
        RefreshPreview(); UpdateResultNotice();
        Status = BridgeInputPresentation.StatusPending;
        requestedPhases = ActivePhases();
        Changed();
        automaticWork = true;
        OnPropertyChanged(nameof(Calculation)); OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(ResultsAreStale)); OnPropertyChanged(nameof(CanExport));
    }

    /// <summary>Runs what the historic view queued on its dispatcher during the change, in order.</summary>
    private void RunDeferred()
    {
        while (deferred.Count > 0)
        {
            var action = deferred[0]; deferred.RemoveAt(0);
            if (!IsDisposed) action();
        }
    }

    /// <summary>The view state changed (ViewChanged of the historic view): saved and Modified, unless the sheet is building, choosing a page or busy.</summary>
    private void ViewChanged()
    {
        if (building || selectingPage || IsBusy) return;
        SaveView(); ChangedKeepingResults();
    }

    /// <summary>Interface state written into the data without recalculation, with Modified (the bare Modified of the historic view).</summary>
    private void StateChanged()
    {
        if (!IsDisposed) ChangedKeepingResults();
    }

    protected override void OnCommit()
    {
        foreach (var form in forms.Values) form.Commit();
        foreach (var card in phases) { card.Form.Commit(); card.Homogenization?.Commit(); }
        slabPropertiesForm?.Commit();
        curveForm?.Commit();
        SaveView();
    }

    /// <summary>The data were replaced (Reload): everything is built again from the new data, as a new opening; the results are cleared.</summary>
    protected override void OnReloaded()
    {
        deferred.Clear();
        responseRecalculation.Invalidate(); responseCancellation?.Cancel();
        calculationResult = null; displayed = null; displayedPhases = []; selectedPhase = null; followLatestStage = true; detached = false;
        status = BridgeInputPresentation.StatusPreparing; OnPropertyChanged(nameof(Status));
        ResetInputs(); ResetLayout(); ResetResults(); ResetResponse();
        Open();
        // The forms are new objects in the same dictionary: the view binds them again.
        OnPropertyChanged(nameof(Forms));
        OnPropertyChanged(nameof(Calculation)); OnPropertyChanged(nameof(DisplayedCalculation)); OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultsAreStale)); OnPropertyChanged(nameof(CanExport)); OnPropertyChanged(nameof(IsDetached));
    }

    protected override void OnDisposed()
    {
        responseRecalculation.Dispose(); responseCancellation?.Cancel();
        detached = false;
        base.OnDisposed();
    }

    // ---------------------------------------------------------------- report and exports

    /// <summary>«Esporta CSV…» (also when read-only): the CSV of the stresses; refused in the status line without a result of the current data or during a calculation.</summary>
    public void ExportCsv()
    {
        if (calculationResult is null || IsBusy) { Status = BridgeInputPresentation.WaitBeforeExport; return; }
        string? path = Context.Services.Files.SaveFile(new SaveFileRequest([new FileFilter("Tabella CSV", "*.csv")], BridgeResultPresentation.CsvFileName));
        if (path is null) return;
        Archivio.ScriviAtomico(path, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BridgeResultPresentation.Csv(calculationResult))).ToArray());
    }

    /// <summary>
    /// The Word report (BuildReportDocument of the historic view): the sheet committed, refused while busy or without a result of the current
    /// data; the sections chosen saved in the sheet (not for the report of the project), with Modified; the figures planned by
    /// <see cref="BridgeReportPresentation"/>.
    /// </summary>
    private byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport)
    {
        Commit();
        if (IsBusy || calculationResult is not { } result) throw new InvalidOperationException(BridgeInputPresentation.ReportUnavailable);
        if (!projectReport) { viewSettings["report_sezioni"] = J.Node(options.OrderBy(k => k).ToArray()); StateChanged(); }
        var images = figures.Render(result, viewSettings, options).Select(f => f.Image()).ToList();
        return DocxWriter.Write(result.Report(title, options.ToHashSet(), images));
    }

    private sealed class CompositeSectionReport(CompositeSectionSheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) => sheet.BuildReport(title, options, projectReport);
    }

    // ---------------------------------------------------------------- read-only

    private void ApplyReadOnly()
    {
        foreach (var form in AllForms()) form.IsReadOnly = !CanEdit;
        foreach (var row in ActionRows) row.Refresh();
        OnPropertyChanged(nameof(ActionsReadOnly));
        NotifyCommands();
    }
}
