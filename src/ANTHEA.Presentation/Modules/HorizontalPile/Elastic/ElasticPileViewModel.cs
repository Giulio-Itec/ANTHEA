using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Application.Presentation;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using X.Core;

namespace Anthea.Presentation.Modules.HorizontalPile.Elastic;

/// <summary>What the elastic response asks of the sheet that hosts it (the ViewModel of the horizontal pile, F5.7).</summary>
/// <param name="CanEdit">False when the sheet is read-only or closed.</param>
/// <param name="Modified">The data changed (the Modified of the historic view, without recalculation of the capacity).</param>
/// <param name="EditInputs">«Modifica dati e strati»: the tab of the capacity.</param>
/// <param name="Migrated">The shared data were replaced (comparison with an old archive, main section from a dialog): the capacity is built again.</param>
public sealed record ElasticPileSheet(Func<bool> CanEdit, Action Modified, Action EditInputs, Action Migrated, PresentationServices Services, ISheetHost Host, string ModuleId);

/// <summary>A check box of what the drawing shows: the key in «elastico.visualizzazione», its label; checking it is the click of the user.</summary>
public sealed class ElasticToggleViewModel : ObservableObject
{
    private readonly ElasticPileViewModel owner;
    private bool isChecked;

    internal ElasticToggleViewModel(ElasticPileViewModel owner, string key, string label, bool isChecked)
    {
        this.owner = owner; Key = key; Label = label; this.isChecked = isChecked;
    }

    public string Key { get; }

    internal void Refresh() => OnPropertyChanged(nameof(IsChecked));

    public string Label { get; }

    /// <summary>Shown: the click writes the choice into the data and raises Modified, without recalculation (refused when read-only).</summary>
    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (value == isChecked || !owner.CanEdit) { OnPropertyChanged(); return; }
            isChecked = value;
            OnPropertyChanged();
            owner.Toggled(Key, value);
        }
    }
}

/// <summary>
/// The tab «Risposta elastica · trave su molle» of the horizontal pile and micropile (F5.8): the historic ElasticPileWorkspace without WPF,
/// on the same data and with the same writes, in the same order. It is a part of <see cref="HorizontalPileSheetViewModel"/>, which creates it
/// when it is built, as HorizontalWorkspace creates its second tab.
/// <list type="bullet">
/// <item>Opening: the shared data prepared (ElasticPileEditor.PrepareShared), the reinforcement (<see cref="PileReinforcementViewModel"/>),
/// the header (or the comparison with an old archive), then the automatic calculation after 300 ms.</item>
/// <item>Every change of the shared data (<see cref="InvalidateShared"/>, also from the tab of the capacity): the results are cleared, the
/// FEM response is kept when its key does not change, a new number of stratigraphies clears the choice of the response, the stiffness and
/// the status are shown again, the calculation is scheduled again and the sheet is told (Modified).</item>
/// <item>Calculation (<see cref="CalculateAsync"/>, background): the existing <see cref="IElasticPileAnalysis"/> (FEM, then the reinforcement)
/// on a copy of the data, through the RecalculationScheduler of the application as the historic view (latest request wins, only the
/// current one is published), with its intermediate states (the response, the phases of MRd).</item>
/// <item>The drawing: what it shows (<see cref="DrawingSource"/>, the options of the data) and the selected point; the section at that depth.</item>
/// </list>
/// Texts and rules come from <see cref="ElasticPilePresentation"/> and <see cref="ElasticPileFields"/>, shared with the historic view. The
/// equivalence is proved without WPF by tests/ANTHEA.Presentation.Tests against the traces of the historic view (tests/fixtures/f5-elastico).
/// </summary>
public sealed class ElasticPileViewModel : ObservableObject, IDisposable
{
    /// <summary>Delay of the automatic recalculation after a change, as the historic view.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(300);

    private readonly JsonObject data;
    private readonly ElasticPileSheet sheet;
    private readonly IElasticPileAnalysis analysis;
    private readonly RecalculationScheduler recalculation = new();
    private readonly bool constructing = true;
    private bool busy, disposed, sectionFollowsSelection;
    private int surveyCount = -1, surveyChoice = -1;
    private string status = "", stiffness = "", selection = ElasticPilePresentation.SelectionInitial, sectionDetail = ElasticPilePresentation.SectionDetailInitial;
    private string extremes = "", reinforcementSummary = "", sheetMessage = ElasticPilePresentation.BillPending;
    private bool progressVisible, progressIndeterminate, canExportCsv, hasHeader;
    private double progressValue;
    private string? migration;
    private IReadOnlyList<string> surveyChoices = [];
    private FormViewModel? options;
    private IReadOnlyList<ElasticToggleViewModel> toggles = [];
    private JsonObject drawingOptions = new();
    private JsonObject? drawingSource, response, result, sheetResult;
    private JsonNode? selectedPoint;
    private ElasticPileSectionView section = new([], [], 0, 0, "Staffa", false, null);
    private IReadOnlyList<ElasticPilePointRow> points = [];

    /// <param name="data">The data of the sheet (shared with the tab of the capacity).</param>
    /// <param name="sheet">The sheet that hosts the tab.</param>
    /// <param name="analysis">The calculation of the response, owned and disposed by the tab.</param>
    public ElasticPileViewModel(JsonObject data, ElasticPileSheet sheet, IElasticPileAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(analysis);
        this.data = data; this.sheet = sheet; this.analysis = analysis;
        ElasticPileEditor.PrepareShared(data);
        Reinforcement = new PileReinforcementViewModel(data, this, sheet);
        BuildHeader();
        InvalidateShared();
        constructing = false;
        sectionFollowsSelection = true;
    }

    /// <summary>The editor of the reinforcement of the pile.</summary>
    public PileReinforcementViewModel Reinforcement { get; }

    internal bool CanEdit => !disposed && sheet.CanEdit();

    /// <summary>The FEM response of the current shared data (kept while only the reinforcement changes), or null.</summary>
    public JsonObject? Response { get => response; private set => SetProperty(ref response, value); }

    /// <summary>The result with the reinforcement of the current data, or null.</summary>
    public JsonObject? Result { get => result; private set => SetProperty(ref result, value); }

    public bool HasResults => Result is not null;

    /// <summary>FEM responses calculated by the analysis of the tab.</summary>
    public int ResponseCalculations => analysis.ResponseCalculations;

    /// <summary>The last calculation requested (automatic or «Calcola»), completed when none.</summary>
    public Task PendingCalculation { get; private set; } = Task.CompletedTask;

    /// <summary>True while the engine works.</summary>
    public bool IsBusy => busy;

    // ---------------------------------------------------------------- status

    public string Status { get => status; private set => SetProperty(ref status, value); }

    public bool ProgressVisible { get => progressVisible; private set => SetProperty(ref progressVisible, value); }

    public bool ProgressIndeterminate { get => progressIndeterminate; private set => SetProperty(ref progressIndeterminate, value); }

    public double ProgressValue { get => progressValue; private set => SetProperty(ref progressValue, value); }

    /// <summary>EJ of the section, or what to complete.</summary>
    public string Stiffness { get => stiffness; private set => SetProperty(ref stiffness, value); }

    // ---------------------------------------------------------------- header

    /// <summary>The text of the comparison with an archive of the old elastic input; null when the archive needs none.</summary>
    public string? Migration { get => migration; private set => SetProperty(ref migration, value); }

    /// <summary>The header (title, stratigraphy, options, drawing check boxes) is shown: no comparison with an old archive pending.</summary>
    public bool HasHeader { get => hasHeader; private set => SetProperty(ref hasHeader, value); }

    public IReadOnlyList<string> SurveyChoices { get => surveyChoices; private set => SetProperty(ref surveyChoices, value); }

    /// <summary>The stratigraphy of the response: choosing one writes «elastico.stratigrafia» and is a change (refused when read-only).</summary>
    public int SurveyChoice
    {
        get => surveyChoice;
        set
        {
            if (value == surveyChoice || !CanEdit || !HasHeader) { OnPropertyChanged(); return; }
            surveyChoice = value;
            OnPropertyChanged();
            data["elastico"]!["stratigrafia"] = value;
            InvalidateShared();
        }
    }

    /// <summary>The options of the response (initial FEM step, tip constraint); null without header.</summary>
    public FormViewModel? Options { get => options; private set => SetProperty(ref options, value); }

    /// <summary>What the drawing shows.</summary>
    public IReadOnlyList<ElasticToggleViewModel> Toggles { get => toggles; private set => SetProperty(ref toggles, value); }

    /// <summary>«Info · risposta elastica del palo».</summary>
    public string Info => ElasticPilePresentation.Info;

    // ---------------------------------------------------------------- drawing and section

    /// <summary>The options of the drawing («elastico.visualizzazione»).</summary>
    public JsonObject DrawingOptions { get => drawingOptions; private set => SetProperty(ref drawingOptions, value); }

    /// <summary>What the drawing shows: the response, the result, or nothing.</summary>
    public JsonObject? DrawingSource { get => drawingSource; private set => SetProperty(ref drawingSource, value); }

    /// <summary>The point of the response selected on the drawing (depth and side), or null.</summary>
    public JsonNode? SelectedPoint { get => selectedPoint; private set => SetProperty(ref selectedPoint, value); }

    /// <summary>The line of the selected point.</summary>
    public string Selection { get => selection; private set => SetProperty(ref selection, value); }

    /// <summary>The section drawn at the selected depth.</summary>
    public ElasticPileSectionView Section { get => section; private set => SetProperty(ref section, value); }

    public string SectionDetail { get => sectionDetail; private set => SetProperty(ref sectionDetail, value); }

    /// <summary>«Estremi, equilibrio e convergenza».</summary>
    public string Extremes { get => extremes; private set => SetProperty(ref extremes, value); }

    /// <summary>«Valori per quota · N–V–M concomitanti».</summary>
    public IReadOnlyList<ElasticPilePointRow> Points { get => points; private set => SetProperty(ref points, value); }

    /// <summary>«Esporta CSV» is available (a result).</summary>
    public bool CanExportCsv { get => canExportCsv; private set => SetProperty(ref canExportCsv, value); }

    /// <summary>«Verifiche per tratto e sezioni critiche».</summary>
    public string ReinforcementSummary { get => reinforcementSummary; private set => SetProperty(ref reinforcementSummary, value); }

    /// <summary>The result drawn in «Tavola armature e distinta ferri», or null (then <see cref="SheetMessage"/>).</summary>
    public JsonObject? SheetResult { get => sheetResult; private set => SetProperty(ref sheetResult, value); }

    /// <summary>The message of the sheet of the reinforcement when nothing is drawn.</summary>
    public string SheetMessage { get => sheetMessage; private set => SetProperty(ref sheetMessage, value); }

    /// <summary>The pieces of bars drawn in the sheet.</summary>
    public int SheetPieces => ElasticPilePresentation.SheetPieces(SheetResult);

    /// <summary>A click on the drawing at <paramref name="depth"/> [m] from the head: the nearest point, the other side at a second click on an interface.</summary>
    public void SelectDepth(double depth)
    {
        if (DrawingSource is not { } shown) return;
        var chosen = ElasticPilePresentation.PointAt(shown, depth, SelectedPoint);
        SelectedPoint = chosen;
        OnSelected(chosen);
    }

    // ---------------------------------------------------------------- commands

    /// <summary>«Modifica dati e strati».</summary>
    public void EditInputs()
    {
        if (!CanEdit || !HasHeader) return;
        sheet.EditInputs();
    }

    /// <summary>The comparison with an old archive: the shared data kept (<paramref name="legacy"/> false) or the old input imported.</summary>
    public void ResolveMigration(bool legacy)
    {
        if (!CanEdit || Migration is null) return;
        try { ElasticPileEditor.ResolveMigration(data, legacy); BuildHeader(); sheet.Migrated(); InvalidateShared(); }
        catch (ArgumentException error) { Status = error.Message; }
    }

    /// <summary>«Mostra confronto completo»: the data of the sheet.</summary>
    public string MigrationComparison => data.ToJsonString(J.Options);

    /// <summary>«Esporta CSV»: the values of the result into the chosen file.</summary>
    public void ExportCsv()
    {
        Commit();
        if (Result is not { } current) return;
        string? path = sheet.Services.Files.SaveFile(new SaveFileRequest([new FileFilter("CSV", "*.csv")], "Palo_risposta_elastica.csv"));
        if (path is null) return;
        Archivio.ScriviAtomico(path, Encoding.UTF8.GetBytes(ElasticPileEditor.Csv(current)));
    }

    /// <summary>The drafts of the fields (none: the fields write at every change).</summary>
    public void Commit()
    {
        Options?.Commit();
        Reinforcement.Commit();
    }

    /// <summary>«Calcola» on this tab: the drafts, then the calculation without delay.</summary>
    public Task CalculateAsync() => PendingCalculation = CalculateScheduledAsync(TimeSpan.Zero, true);

    /// <summary>
    /// InvalidateShared of the historic view: a change of the shared data or of the reinforcement. The results are cleared; the response is
    /// kept when its key does not change; a new number of stratigraphies clears the choice of the response and builds the header again; the
    /// calculation is scheduled again, then Modified.
    /// </summary>
    public void InvalidateShared()
    {
        if (disposed) return;
        recalculation.Invalidate(); Result = null; SetSheet(null); ReinforcementSummary = ElasticPilePresentation.ReinforcementPending;
        Section = Section with { Outline = [], Bars = [] }; SectionDetail = ElasticPilePresentation.SectionPending; CanExportCsv = false;
        if (surveyCount != data.Array("stratigrafie").Count) { if (surveyCount >= 0 && !ElasticPileEditor.NeedsMigration(data)) data["elastico"]!["stratigrafia"] = -1; BuildHeader(); }
        bool retain = ElasticPileAnalysisSession.Matches(Response, data);
        Reinforcement.InvalidateResults();
        if (retain) SetDrawing(Response, preserveSelection: true);
        else { Response = null; SetDrawing(null, false); Points = []; Selection = Extremes = ""; }
        Stiffness = HorizontalPreview.StiffnessDescription(data);
        CalculationState(retain ? ElasticPilePresentation.StatusRetained : ElasticPilePresentation.StatusChanged, true);
        if (!ElasticPileEditor.NeedsMigration(data)) PendingCalculation = CalculateScheduledAsync(RecalculationDelay, false);
        if (!constructing) sheet.Modified();
    }

    /// <summary>The sheet changed between editable and read-only.</summary>
    internal void RefreshEditable()
    {
        if (Options is { } form) form.IsReadOnly = !CanEdit;
        foreach (var toggle in Toggles) toggle.Refresh();
        OnPropertyChanged(nameof(SurveyChoice));
        Reinforcement.RefreshEditable();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        recalculation.Dispose();
        analysis.Dispose();
        Reinforcement.Dispose();
    }

    // ---------------------------------------------------------------- internals

    internal void Toggled(string key, bool value)
    {
        DrawingOptions[key] = value;
        OnPropertyChanged(nameof(DrawingOptions));
        sheet.Modified();
    }

    private void BuildHeader()
    {
        surveyCount = data.Array("stratigrafie").Count; HasHeader = false; Migration = null; Options = null; Toggles = []; SurveyChoices = [];
        if (ElasticPileEditor.NeedsMigration(data)) { Migration = ElasticPilePresentation.MigrationText(data); return; }
        var e = data["elastico"]!.AsObject(); var view = HorizontalSheetPresentation.PrepareElasticView(data); DrawingOptions = view;
        SurveyChoices = ElasticPilePresentation.SurveyChoices(surveyCount);
        int chosen = ElasticPilePresentation.SurveyChoice(e);
        surveyChoice = chosen >= -1 && chosen < surveyCount ? chosen : -1;
        OnPropertyChanged(nameof(SurveyChoice));
        Options = new FormViewModel(e, ElasticPileFields.Options(), _ => InvalidateShared(), compact: true) { InheritedWikiModule = sheet.ModuleId, IsReadOnly = !CanEdit };
        Toggles = ElasticPilePresentation.Toggles.Select(t => new ElasticToggleViewModel(this, t.Key, t.Label, ElasticPilePresentation.Shown(view, t.Key))).ToArray();
        HasHeader = true;
    }

    private void CalculationState(string text, bool running, int completed = 0, int total = 0)
    {
        Status = text; ProgressVisible = running; ProgressIndeterminate = running && total == 0; ProgressValue = total > 0 ? 100d * completed / total : 0;
        Reinforcement.SetCalculationState(text, running, completed, total);
    }

    private async Task CalculateScheduledAsync(TimeSpan delay, bool commit)
    {
        if (disposed) return;
        if (commit) Commit();
        var snapshot = (JsonObject)data.DeepClone();
        var completed = await recalculation.ScheduleAsync(async token =>
        {
            busy = true;
            if (Response == null) CalculationState(ElasticPilePresentation.StatusResponseRunning, true);
            var updates = new Progress<PileProgress>(p => { if (!disposed && !token.IsCancellationRequested && busy && Result == null) CalculationState(ElasticPilePresentation.Phase(p), true, p.Completed, p.Total); });
            try
            {
                return await analysis.CalculateAsync(snapshot, ready =>
                {
                    if (disposed || token.IsCancellationRequested) return;
                    if (!ElasticPileAnalysisSession.Matches(Response, snapshot)) { Response = ready; PresentResponse(); }
                    CalculationState(ElasticPilePresentation.StatusResponseReady, true);
                }, updates, token);
            }
            finally { busy = false; }
        }, delay);
        completed.TryPublish(current =>
        {
            if (current.Error is not null)
            {
                Result = null; SetDrawing(Response, preserveSelection: true); SetSheet(null, ElasticPilePresentation.SheetUnavailable(current.Error.Message)); CanExportCsv = false;
                CalculationState(ElasticPilePresentation.Unavailable(current.Error.Message), false);
                return;
            }
            Result = current.Value; Present();
        });
    }

    private void Present()
    {
        var current = Result!;
        SetDrawing(current, preserveSelection: true); CanExportCsv = true; Status = ElasticPilePresentation.Completed(current);
        SetSheet(current); Reinforcement.Refresh();
        CalculationState(Status, false); Reinforcement.UpdateResults(); ShowSection(SelectedPoint);
        ReinforcementSummary = ElasticPilePresentation.ReinforcementSummary(current);
    }

    private void PresentResponse()
    {
        if (Response is not { } current) return;
        SetDrawing(current, false);
        Extremes = ElasticPilePresentation.ResponseSummary(current);
        Points = ElasticPilePresentation.Points(current);
    }

    /// <summary>ElasticPileDrawing.Set: a new source clears the selection unless it is preserved.</summary>
    private void SetDrawing(JsonObject? value, bool preserveSelection)
    {
        DrawingSource = value;
        if (!preserveSelection) { SelectedPoint = null; OnSelected(null); }
    }

    /// <summary>PileReinforcementDrawing.Set.</summary>
    private void SetSheet(JsonObject? value, string? unavailableReason = null)
    {
        SheetResult = value;
        SheetMessage = ElasticPilePresentation.SheetMessage(value, unavailableReason);
        OnPropertyChanged(nameof(SheetPieces));
    }

    /// <summary>The handlers of the drawing of the historic view, in their order: the line of the selection, then (after the opening) the section.</summary>
    private void OnSelected(JsonNode? point)
    {
        if (point is null) Selection = ""; else Selection = ElasticPilePresentation.Selection(point);
        if (sectionFollowsSelection) ShowSection(point);
    }

    private void ShowSection(JsonNode? point)
    {
        Section = Section with { Outline = [], Bars = [], LinkRadius = 0 }; SectionDetail = ElasticPilePresentation.SectionNone;
        if (ElasticPilePresentation.SectionAt(Result, point) is { } shown) { Section = shown.View; SectionDetail = shown.Detail; }
    }
}
