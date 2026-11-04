using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Modules.VerticalPile;

/// <summary>
/// Sheet of the vertical pile and of the vertical micropile (geo_palo_verticale, geo_micropalo_verticale), F5.6: the behaviour of the
/// historic view (the generic branch of SheetEditor with GeoEditor) without WPF, on the same document and with the same writes, in
/// the same order.
/// <list type="bullet">
/// <item>Opening: <see cref="ModuleSheetPreparation.PrepareVertical"/> (lazy migrations), the list of the stratigraphies created when
/// missing, the three forms, the stratigraphies, then the automatic calculation after 450 ms.</item>
/// <item>Forms (<see cref="VerticalPileFields"/>): the raw text into the document at every change, as InputForm. A new technology
/// writes its γb (the change of the normative form is a second edit, as today); a new injection of the micropile writes the initial α
/// of the layers whose soil is known.</item>
/// <item>Stratigraphies: rows on display copies of the layers (calculated columns only there); an edit copies the editable columns
/// into the layer; layers and stratigraphies added, removed (confirmed) and copied (confirmed when a stratigraphy is replaced).</item>
/// <item>Calculation (<see cref="SheetCalculationMode.Background"/>): on a copy of the data, on a background thread, through the
/// <see cref="IModuleService"/> of the context; automatic 450 ms after the last edit, «Calcola» at once; only the calculation of the
/// current data is published. Status texts, warnings, verification, curves, charts and tables as the historic view.</item>
/// <item>Interface state saved in the data (<see cref="UiStateKeys"/>): the visibility of the curves, written with a result when a
/// check box or «Tutte»/«Nessuna» changes it (also when read-only, as today): Modified without recalculation.</item>
/// <item>Read-only (a previous revision): the forms are read-only (<see cref="FormViewModel.IsReadOnly"/>, decision T2 of 17/10); the
/// table, the stratigraphies and «Reset» refuse the changes and their commands are disabled; survey selection, capacity view, curves,
/// tables, «Estendi», the guide and calculation stay available.</item>
/// <item>Commands of the view (decision Q3: no code-behind but the drawings): the buttons of the historic cards and of the footer, through
/// the guard of the sheet (Q19).</item>
/// </list>
/// Texts and rules come from <see cref="VerticalPilePresentation"/> and <see cref="VerticalPileFields"/>, shared with the historic
/// view. The equivalence is proved without WPF by tests/ANTHEA.Presentation.Tests (VerticalPileSheetChecks) against the traces of the
/// historic view frozen in tests/fixtures/f5-palo-verticale, against the F3 reference and against the frozen series of f3-figures.
/// </summary>
public sealed class VerticalPileSheetViewModel : SheetViewModelBase
{
    /// <summary>The vertical pile.</summary>
    public const string PileModule = "geo_palo_verticale";

    /// <summary>The vertical micropile.</summary>
    public const string MicropileModule = "geo_micropalo_verticale";

    /// <summary>Interface state that the sheet writes into its data (<see cref="ModulePersistence"/>): the visibility of the curves.</summary>
    public static IReadOnlyList<string> UiStateKeys { get; } = ["visibilita_grafici"];

    /// <summary>Delay of the automatic recalculation after an edit, as the historic view.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(450);

    private readonly IVerticalPileFigureRenderer figures;
    private readonly JsonObject defaults;
    private List<VerticalPileCurve> curves = [];
    private readonly Dictionary<string, bool> visibility = new(StringComparer.Ordinal);
    private bool building = true;
    private FormViewModel generalForm = null!, efficiencyForm = null!, normativeForm = null!;
    private IReadOnlyList<VerticalPileSurveyViewModel> surveys = [];
    private int selectedSurvey = -1, capacityView, selectedTable = -1;
    private string status = VerticalPilePresentation.StatusToVerify, warningText = "", efficiencyText = "", raw = "";
    private string endValues = "Valori a L non disponibili";
    private string capacityTitle = VerticalPileCapacityChart.InitialTitle, capacityAxis = VerticalPileCapacityChart.InitialDepthAxis, emptyMessage = "";
    private double capacityDepth;
    private IReadOnlyList<PlotSeriesData> capacitySeries = [];
    private VerticalPileCapacityChart capacityChart = null!;
    private PlotData referenceChart = new();
    private IReadOnlyList<string[]> verification = [];
    private IReadOnlyList<VerticalPileCurveChoiceViewModel> curveChoices = [];
    private List<Tabella> tables = [];
    private IReadOnlyList<string> tableTitles = [];
    private VerticalPileTableView? shownTable;
    private string expandedCard = "";
    private readonly List<IRelayCommand> editCommands = [];

    /// <param name="moduleId"><see cref="PileModule"/> or <see cref="MicropileModule"/>.</param>
    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the module.</param>
    /// <param name="figures">Draws the figures of the report.</param>
    public VerticalPileSheetViewModel(string moduleId, SheetContext context, IVerticalPileFigureRenderer figures)
        : base(moduleId, context, RecalculationDelay, SheetCalculationMode.Background)
    {
        if (moduleId is not (PileModule or MicropileModule)) throw new ArgumentException("Modulo dei pali verticali atteso: " + moduleId, nameof(moduleId));
        ArgumentNullException.ThrowIfNull(figures);
        this.figures = figures;
        IsMicro = moduleId == MicropileModule;
        Report = new VerticalPileReport(this);
        CapacityViews = VerticalPileFields.CapacityViews(IsMicro);
        ResetNormativeCommand = EditCommand(ResetNormative);
        AddSurveyCommand = EditCommand(AddSurvey);
        RemoveSelectedSurveyCommand = EditCommand(RemoveSelectedSurvey);
        RemoveSurveyCommand = Track(new RelayCommand<VerticalPileSurveyViewModel>(survey => Guard.Run(() => { if (survey is not null) RemoveSurvey(survey.Index); }), _ => CanEdit));
        CopySurveyIntoCommand = Track(GuardedCommands.CreateAsync(Guard, _ => CopySurveyIntoAsync(), Lifetime, () => CanEdit));
        CopySurveyFromCommand = Track(GuardedCommands.CreateAsync(Guard, _ => CopySurveyFromAsync(), Lifetime, () => CanEdit));
        ShowAllCurvesCommand = GuardedCommands.Create(Guard, ShowAllCurves);
        HideAllCurvesCommand = GuardedCommands.Create(Guard, HideAllCurves);
        ToggleCardCommand = new RelayCommand<string>(card => Guard.Run(() => ToggleCard(card ?? "")));
        TransferSoilCommand = Track(GuardedCommands.CreateAsync(Guard, TransferSoilAsync, Lifetime, () => CanEdit && !IsMicro));
        CalculationGuideCommand = GuardedCommands.Create(Guard, () => CalculationGuide.Open(Context.Services.Wiki, ModuleId));
        ModuleSheetPreparation.PrepareVertical(Data, moduleId);
        defaults = Archivio.NuovoFoglio(moduleId);
        BuildForms();
        if (Data["stratigrafie"] is not JsonArray) Data["stratigrafie"] = new JsonArray();
        RebuildSurveys(null);
        Verification = VerticalPilePresentation.Verification(null, IsMicro);
        building = false;
        Preview(null);
        Status = VerticalPilePresentation.StatusPending;
        _ = ScheduleCalculationAsync();
    }

    public override IModuleReportSource Report { get; }

    /// <summary>
    /// A calculation runs. A closed sheet is never busy, as the historic view, whose Busy returned to false when the engine came back after
    /// the closing (ui/f3-scheduling): the late completion is ignored by the closed sheet, so its state stays the one of the closing.
    /// </summary>
    public override bool IsBusy => !IsDisposed && base.IsBusy;

    /// <summary>True for the micropile.</summary>
    public bool IsMicro { get; }

    // ---------------------------------------------------------------- forms

    /// <summary>«Dati generali».</summary>
    public FormViewModel GeneralForm => generalForm;

    /// <summary>«Efficienza».</summary>
    public FormViewModel EfficiencyForm => efficiencyForm;

    /// <summary>Efficiency of the group (η of compression and traction) or the error of the method, under «Efficienza».</summary>
    public string EfficiencyText { get => efficiencyText; private set => SetProperty(ref efficiencyText, value); }

    /// <summary>«Coefficienti normativa».</summary>
    public FormViewModel NormativeForm => normativeForm;

    /// <summary>The «Reset» button of «Coefficienti normativa» (disabled when read-only).</summary>
    public IRelayCommand ResetNormativeCommand { get; }

    /// <summary>«Reset» of the normative coefficients: γb of the technology and the coefficients of a new sheet; refused when read-only.</summary>
    public void ResetNormative()
    {
        if (!CanEdit) return;
        var general = Data["generali"]!;
        foreach (var field in VerticalPileFields.Normative().Where(f => !f.ReadOnly))
            NormativeForm.Set(field.Key, VerticalPilePresentation.NormativeResetValue(field.Key, general, defaults, IsMicro));
    }

    // ---------------------------------------------------------------- verification

    /// <summary>Columns of «Verifica».</summary>
    public IReadOnlyList<string> VerificationColumns { get; } = VerticalPilePresentation.VerificationColumns();

    /// <summary>Rows of «Verifica»: action, resistance, utilization and outcome for each condition.</summary>
    public IReadOnlyList<string[]> Verification { get => verification; private set => SetProperty(ref verification, value); }

    // ---------------------------------------------------------------- stratigraphies

    /// <summary>The stratigraphies, one tab each.</summary>
    public IReadOnlyList<VerticalPileSurveyViewModel> Surveys { get => surveys; private set => SetProperty(ref surveys, value); }

    /// <summary>The selected stratigraphy (−1 without stratigraphies): its profile is drawn and the reference chart follows it.</summary>
    public int SelectedSurveyIndex
    {
        get => selectedSurvey;
        set
        {
            if (value == selectedSurvey || value < -1 || value >= Surveys.Count) return;
            selectedSurvey = value;
            OnPropertyChanged();
            if (!building) ReferenceChart = VerticalPileCharts.Reference(Data, Result, IsMicro, selectedSurvey);
        }
    }

    /// <summary>«+» (disabled when read-only).</summary>
    public IRelayCommand AddSurveyCommand { get; }

    /// <summary>«−» (disabled when read-only).</summary>
    public IRelayCommand RemoveSelectedSurveyCommand { get; }

    /// <summary>The [−] of the tab of a stratigraphy, with the stratigraphy as parameter (disabled when read-only).</summary>
    public IRelayCommand<VerticalPileSurveyViewModel> RemoveSurveyCommand { get; }

    /// <summary>«Copia in…» (disabled when read-only).</summary>
    public IAsyncRelayCommand CopySurveyIntoCommand { get; }

    /// <summary>«Copia da…» (disabled when read-only).</summary>
    public IAsyncRelayCommand CopySurveyFromCommand { get; }

    /// <summary>«+»: a new stratigraphy with one layer, selected.</summary>
    public void AddSurvey()
    {
        if (!CanEdit) return;
        Data.Array("stratigrafie").Add(new JsonArray(NewLayer()));
        RebuildSurveys(Data.Array("stratigrafie").Count - 1);
        UserChanged();
    }

    /// <summary>The [−] of a tab: removes the stratigraphy <paramref name="index"/> after the confirmation of the user.</summary>
    public void RemoveSurvey(int index)
    {
        if (!CanEdit || index < 0 || index >= Data.Array("stratigrafie").Count) return;
        var surveysData = Data.Array("stratigrafie");
        if (!Context.Services.Confirmations.ConfirmDelete("Stratigrafia", $"Eliminare la stratigrafia {index + 1} e tutti i suoi strati?")) return;
        int selected = SelectedSurveyIndex;
        surveysData.RemoveAt(index);
        RebuildSurveys(index < selected ? selected - 1 : selected);
        UserChanged();
    }

    /// <summary>«−»: removes the selected stratigraphy after the confirmation of the user.</summary>
    public void RemoveSelectedSurvey()
    {
        if (SelectedSurveyIndex < 0) return;
        RemoveSurvey(SelectedSurveyIndex);
    }

    /// <summary>
    /// Copies the layers of the stratigraphy <paramref name="source"/> into <paramref name="destination"/> (−1: a new stratigraphy),
    /// replacing them after the confirmation of the user; the destination is selected. False when nothing was copied.
    /// </summary>
    public bool CopySurvey(int source, int destination)
    {
        if (!CanEdit) return false;
        var surveysData = Data.Array("stratigrafie");
        if (source < 0 || source >= surveysData.Count || destination < -1 || destination >= surveysData.Count || source == destination) return false;
        if (destination >= 0 && !Context.Services.Confirmations.ConfirmDelete("Copia stratigrafia",
            $"Sostituire tutti gli strati della stratigrafia {destination + 1} con una copia della stratigrafia {source + 1}?")) return false;
        var copy = surveysData[source]!.DeepClone();
        if (destination == -1) { surveysData.Add(copy); destination = surveysData.Count - 1; }
        else surveysData[destination] = copy;
        RebuildSurveys(destination);
        UserChanged();
        return true;
    }

    /// <summary>«Copia in…»: copies the selected stratigraphy into the one chosen in the dialog (or into a new one).</summary>
    public Task CopySurveyIntoAsync() => ChooseSurveyCopyAsync(copyInto: true);

    /// <summary>«Copia da…»: replaces the selected stratigraphy with a copy of the one chosen in the dialog.</summary>
    public Task CopySurveyFromAsync() => ChooseSurveyCopyAsync(copyInto: false);

    /// <summary>«Aggiungi strato» of the stratigraphy <paramref name="survey"/>.</summary>
    public void AddLayer(int survey)
    {
        if (!CanEdit || survey < 0 || survey >= Surveys.Count) return;
        var target = Surveys[survey];
        var layer = NewLayer();
        target.Layers.Add(layer);
        target.Rows.Add(Bind(target, layer));
        UserChanged();
    }

    /// <summary>The [−] of a layer of the stratigraphy <paramref name="survey"/>.</summary>
    public void RemoveLayer(int survey, JsonRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!CanEdit || survey < 0 || survey >= Surveys.Count) return;
        var target = Surveys[survey];
        int position = target.Rows.IndexOf(row);
        if (position < 0) return;
        target.Layers.RemoveAt(position);
        target.Rows.RemoveAt(position);
        UserChanged();
    }

    // ---------------------------------------------------------------- charts

    /// <summary>Views of the capacity chart: every design curve, then one condition at a time.</summary>
    public IReadOnlyList<string> CapacityViews { get; }

    /// <summary>The chosen view of the capacity chart (view state, not saved); allowed also when read-only.</summary>
    public int SelectedCapacityView
    {
        get => capacityView;
        set
        {
            if (value == capacityView || value < 0 || value >= CapacityViews.Count) return;
            capacityView = value;
            OnPropertyChanged();
            UpdateVisible();
            RebuildCurveChoices();
        }
    }

    /// <summary>Every curve of the result (design, mean and minimum branches, actions), with its visibility at the publication.</summary>
    public IReadOnlyList<VerticalPileCurve> Curves => curves;

    /// <summary>The capacity chart as shown.</summary>
    public VerticalPileCapacityChart CapacityChart { get => capacityChart; private set => SetProperty(ref capacityChart, value); }

    /// <summary>Text above the curves: the length when the values at L are available.</summary>
    public string EndValues { get => endValues; private set => SetProperty(ref endValues, value); }

    /// <summary>The check boxes of the curves of the chosen view.</summary>
    public IReadOnlyList<VerticalPileCurveChoiceViewModel> CurveChoices { get => curveChoices; private set => SetProperty(ref curveChoices, value); }

    /// <summary>«Tutte» (available also when read-only, as today: V5, T6).</summary>
    public IRelayCommand ShowAllCurvesCommand { get; }

    /// <summary>«Nessuna» (available also when read-only, as today: V5, T6).</summary>
    public IRelayCommand HideAllCurvesCommand { get; }

    /// <summary>«Tutte»: every curve shown (saved in the sheet).</summary>
    public void ShowAllCurves() => SetVisible(true);

    /// <summary>«Nessuna»: no curve shown (saved in the sheet).</summary>
    public void HideAllCurves() => SetVisible(false);

    /// <summary>The reference chart of the selected stratigraphy: Nq (pile) or the Bustamante–Doix abacus (micropile).</summary>
    public PlotData ReferenceChart { get => referenceChart; private set => SetProperty(ref referenceChart, value); }

    // ---------------------------------------------------------------- tables and results

    /// <summary>Items of «Tabelle e dettagli»: the capacity at the available depth, then the tables of the result.</summary>
    public IReadOnlyList<string> TableTitles { get => tableTitles; private set => SetProperty(ref tableTitles, value); }

    /// <summary>The chosen item of «Tabelle e dettagli» (−1 without result); allowed also when read-only.</summary>
    public int SelectedTableIndex
    {
        get => selectedTable;
        set
        {
            if (value == selectedTable || value < -1 || value >= TableTitles.Count) return;
            selectedTable = value;
            OnPropertyChanged();
            ShowTable();
        }
    }

    /// <summary>What «Tabelle e dettagli» shows; null after an edit.</summary>
    public VerticalPileTableView? ShownTable { get => shownTable; private set => SetProperty(ref shownTable, value); }

    /// <summary>«Risultati JSON»: the result as JSON, or the message of an edit.</summary>
    public string RawResult { get => raw; private set => SetProperty(ref raw, value); }

    // ---------------------------------------------------------------- view state

    /// <summary>The card «Stratigrafia».</summary>
    public const string SurveysCard = "stratigrafia";

    /// <summary>The card «Profilo stratigrafico».</summary>
    public const string ProfileCard = "profilo";

    /// <summary>The card «Grafici capacità portante».</summary>
    public const string ChartsCard = "grafici";

    /// <summary>
    /// The card shown extended («Estendi»), view state of the historic layout: <see cref="SurveysCard"/>, <see cref="ProfileCard"/>,
    /// <see cref="ChartsCard"/> or empty. With the profile extended the drawing shows every stratigraphy. Not saved; allowed when read-only.
    /// </summary>
    public string ExpandedCard { get => expandedCard; private set => SetProperty(ref expandedCard, value); }

    /// <summary>«Estendi»/«Riduci» of a card, with its name as parameter.</summary>
    public IRelayCommand<string> ToggleCardCommand { get; }

    /// <summary>«Estendi» of a card when it is not extended, «Riduci» when it is.</summary>
    public void ToggleCard(string card)
    {
        if (card is not (SurveysCard or ProfileCard or ChartsCard)) throw new ArgumentException("Scheda sconosciuta: " + card, nameof(card));
        ExpandedCard = ExpandedCard == card ? "" : card;
    }

    /// <summary>
    /// «Salva PNG…» of the capacity chart: the file chosen by the user («capacita.png», PNG images) receives the image that
    /// <paramref name="png"/> draws, written atomically as today. Nothing when the user cancels; refused when read-only (the historic
    /// view removes the menu of the chart in a revision).
    /// </summary>
    public void SaveCapacityImage(Func<byte[]> png)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (!CanEdit) return;
        Guard.Run(() =>
        {
            string? file = Context.Services.Files.SaveFile(new SaveFileRequest([new FileFilter("Immagine PNG", "*.png")], "capacita.png"));
            if (file is not null) Archivio.ScriviAtomico(file, png());
        });
    }

    // ---------------------------------------------------------------- footer

    /// <summary>
    /// «Invia / carica terreno…» (pile only, disabled when read-only): the sheet commits, then the shared soil transfer dialog
    /// (<see cref="SoilTransferDialogViewModel"/> on the dialog service, decision T5) receives its module, its data and the soil sheets
    /// of the project; a copy goes to the window as <see cref="ISheetHost.OpenModuleCopy"/>, a replacement as
    /// <see cref="ISheetHost.ReplaceSoil"/> (SheetEditor.OpenSoilTransfer of the historic view).
    /// </summary>
    public IAsyncRelayCommand TransferSoilCommand { get; }

    /// <summary>«Modello e dati comuni…»: the theory page of the module in the Wiki (<see cref="CalculationGuide"/>), also when read-only.</summary>
    public IRelayCommand CalculationGuideCommand { get; }

    private async Task TransferSoilAsync(CancellationToken cancellation)
    {
        if (!CanEdit || IsMicro) return;
        Commit();
        var choice = await new SoilTransferDialogService(Context.Services).ShowAsync(ModuleId, Data, Context.Host.SoilSources());
        if (choice is null || IsDisposed) return;
        if (choice.Module is null) Context.Host.ReplaceSoil(choice.Data);
        else Context.Host.OpenModuleCopy(choice.Module, choice.Data, choice.Name);
    }

    /// <summary>Status line: waiting, running, completed, missing data or error.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>Warnings of the result (with the warning of a stratigraphy shorter than the pile) or the text of the error; empty otherwise.</summary>
    public string WarningText { get => warningText; private set => SetProperty(ref warningText, value); }

    // ---------------------------------------------------------------- sheet

    /// <summary>The fields write at every change: nothing waits for the commit (the historic Commit confirmed the cells being edited).</summary>
    protected override void OnCommit()
    {
        GeneralForm.Commit(); EfficiencyForm.Commit(); NormativeForm.Commit();
    }

    /// <summary>The data were replaced: the sheet is built again on them, as at opening; the results wait for the automatic recalculation.</summary>
    protected override void OnReloaded()
    {
        building = true;
        try
        {
            ModuleSheetPreparation.PrepareVertical(Data, ModuleId);
            BuildForms();
            if (Data["stratigrafie"] is not JsonArray) Data["stratigrafie"] = new JsonArray();
            RebuildSurveys(null);
            ClearResults();
            capacityTitle = VerticalPileCapacityChart.InitialTitle; capacityAxis = VerticalPileCapacityChart.InitialDepthAxis; capacityDepth = 0;
            EndValues = VerticalPilePresentation.EndValues(Data, null);
            Verification = VerticalPilePresentation.Verification(null, IsMicro);
        }
        finally { building = false; }
        Preview(null);
        Status = VerticalPilePresentation.StatusPending;
    }

    protected override void OnCalculationPublished()
    {
        switch (CalculationState)
        {
            case CalculationState.Completed:
                WarningText = string.Join(Environment.NewLine, Warnings);
                ShowResults(Result!);
                RawResult = Result!.ToJsonString(J.Options);
                Status = VerticalPilePresentation.StatusCompleted;
                break;
            case CalculationState.Incomplete:
                Status = VerticalPilePresentation.StatusIncomplete(CalculationMessage);
                WarningText = "";
                break;
            case CalculationState.Failed:
                Status = VerticalPilePresentation.StatusFailed(CalculationMessage);
                WarningText = CalculationMessage;
                break;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        // The historic view shows «Calcolo in corso…» when the engine starts; a request that waits keeps the text it had.
        if (e.PropertyName == nameof(CalculationState) && CalculationState == CalculationState.Running) Status = VerticalPilePresentation.StatusRunning;
        if (e.PropertyName != nameof(CanEdit)) return;
        // T2: the same forms, read-only; the commands that change the data are disabled.
        foreach (var form in new[] { generalForm, efficiencyForm, normativeForm }) if (form is not null) form.IsReadOnly = !CanEdit;
        foreach (var command in editCommands.Concat(Surveys.SelectMany(s => new IRelayCommand[] { s.AddLayerCommand, s.RemoveLayerCommand }))) command.NotifyCanExecuteChanged();
    }

    /// <summary>Closing: the base raises CanEdit, so the forms become read-only and a late edit never reaches the data.</summary>
    protected override void OnDisposed()
    {
        base.OnDisposed();
        building = true;
    }

    // ---------------------------------------------------------------- construction

    /// <summary>
    /// The three forms on the current data, read-only when the sheet is (<see cref="FormViewModel.IsReadOnly"/>, decision T2: no detached
    /// copies); the «?» labels and F1 of the fields open the topics of the module (the module of the historic view, inherited by its
    /// InputForm) through the navigator of the services.
    /// </summary>
    private void BuildForms()
    {
        var general = Data["generali"]!.AsObject();
        FormViewModel Form(JsonObject values, FieldSpec[] fields, Action<string> changed, bool compact) =>
            new(values, fields, changed, compact: compact, symbolColumns: true, wikiModule: ModuleId) { IsReadOnly = !CanEdit, WikiNavigator = Context.Services.Wiki };
        generalForm = Form(general, VerticalPileFields.General(IsMicro), OnGeneralChanged, false);
        generalForm.ShowField(VerticalPileFields.HiddenMethod(IsMicro), false);
        efficiencyForm = Form(Data["efficienza"]!.AsObject(), VerticalPileFields.Efficiency(), _ => UserChanged(), true);
        normativeForm = Form(general, VerticalPileFields.Normative(), _ => UserChanged(), true);
        OnPropertyChanged(nameof(GeneralForm)); OnPropertyChanged(nameof(EfficiencyForm)); OnPropertyChanged(nameof(NormativeForm));
    }

    private IRelayCommand EditCommand(Action action) => Track(GuardedCommands.Create(Guard, action, () => CanEdit));

    private T Track<T>(T command) where T : IRelayCommand { editCommands.Add(command); return command; }

    private void OnGeneralChanged(string key)
    {
        if (!CanEdit) return;
        if (IsMicro && key == "tipo_iniezione")
        {
            VerticalPilePreview.ResetAlphas(Data);
            RebuildSurveys(null);
        }
        // D7-d: a new technology brings its γb of NTC Tab. 6.4.II; a value typed afterwards is kept (and declared by the calculation).
        if (key is "tipo_palo" or "sottotipo_palo_battuto") NormativeForm.Set("sicurezza_base", VerticalPilePreview.NormativeBase(Data["generali"]!, IsMicro));
        UserChanged();
    }

    /// <summary>
    /// RebuildSondages of the historic view: the tabs and the rows again from the data, the selection kept within the stratigraphies.
    /// The rows get their calculated columns from <see cref="Preview"/>, which always follows (opening, edit).
    /// </summary>
    private void RebuildSurveys(int? selection)
    {
        int selected = selection ?? selectedSurvey;
        var list = new List<VerticalPileSurveyViewModel>();
        var surveysData = Data.Array("stratigrafie");
        for (int i = 0; i < surveysData.Count; i++)
        {
            var layers = surveysData[i]!.AsArray();
            int index = i;
            var survey = new VerticalPileSurveyViewModel(i, layers, VerticalPileFields.Layers(IsMicro, layers),
                new RelayCommand(() => Guard.Run(() => AddLayer(index)), () => CanEdit),
                new RelayCommand<JsonRowViewModel>(row => Guard.Run(() => { if (row is not null) RemoveLayer(index, row); }), _ => CanEdit));
            foreach (var layer in layers.OfType<JsonObject>()) survey.Rows.Add(Bind(survey, layer));
            list.Add(survey);
        }
        Surveys = list;
        // The reference chart is computed by the Preview that follows, with the result of the data being shown.
        selectedSurvey = list.Count > 0 ? Math.Clamp(selected, 0, list.Count - 1) : -1;
        OnPropertyChanged(nameof(SelectedSurveyIndex));
    }

    /// <summary>A row of the table on a display copy of <paramref name="layer"/> (lateral shaft and Nc shown with their defaults).</summary>
    private JsonRowViewModel Bind(VerticalPileSurveyViewModel survey, JsonObject layer)
    {
        var display = (JsonObject)layer.DeepClone();
        if (!display.ContainsKey("laterale_attiva")) display["laterale_attiva"] = true;
        if (!IsMicro && !display.ContainsKey("nc")) display["nc"] = "9";
        JsonRowViewModel? row = null;
        row = new JsonRowViewModel(display, key => OnLayerEdited(survey, layer, row!, key));
        return row;
    }

    private void OnLayerEdited(VerticalPileSurveyViewModel survey, JsonObject layer, JsonRowViewModel row, string key)
    {
        if (IsDisposed) return;
        if (!CanEdit)
        {
            // The view shows the table read-only: an edit that reaches the row anyway is undone.
            RebuildSurveys(null);
            UpdateLayerColumns();
            return;
        }
        var display = row.Values;
        if (IsMicro && key == "terreno" && VerticalPilePreview.InitialAlpha(display.S("terreno"), Data["generali"].S("tipo_iniezione")) is { } alpha)
            row.Output("alpha", alpha);
        foreach (var column in survey.Columns.Where(c => !c.ReadOnly)) layer[column.Key] = display[column.Key]?.DeepClone();
        UserChanged();
    }

    private JsonObject NewLayer() => IsMicro ? Archivio.NuovoStratoMicropalo() : Archivio.NuovoStratoPalo();

    private async Task ChooseSurveyCopyAsync(bool copyInto)
    {
        if (!CanEdit) return;
        int current = SelectedSurveyIndex;
        var surveysData = Data.Array("stratigrafie");
        if (current < 0) { Context.Services.Messages.Inform("", "Selezionare una stratigrafia."); return; }
        var choices = Enumerable.Range(0, surveysData.Count).Where(i => i != current).Select(i => new SurveyCopyChoice(i, $"Stratigrafia {i + 1}")).ToList();
        if (copyInto) choices.Add(new(-1, "Nuova stratigrafia"));
        if (choices.Count == 0) { Context.Services.Messages.Inform("", "Non ci sono altre stratigrafie da cui copiare."); return; }
        var dialog = new SurveyCopyDialogViewModel(copyInto ? "Copia stratigrafia in…" : "Copia stratigrafia da…",
            copyInto ? $"Copia gli strati della stratigrafia {current + 1} in:" : $"Sostituisci gli strati della stratigrafia {current + 1} copiando da:", choices);
        var chosen = await Context.Services.Dialogs.ShowDialogAsync(dialog);
        if (chosen is null || IsDisposed) return;
        CopySurvey(copyInto ? current : chosen.Survey, copyInto ? chosen.Survey : current);
    }

    // ---------------------------------------------------------------- edits and preview

    /// <summary>Changed of the historic view: results cleared, preview, verification without result, then the base (new revision, automatic recalculation, Modified).</summary>
    private void UserChanged()
    {
        if (building || IsDisposed) return;
        ClearResults();
        Preview(null);
        Verification = VerticalPilePresentation.Verification(null, IsMicro);
        Status = VerticalPilePresentation.StatusPending;
        Changed();
    }

    private void ClearResults()
    {
        tables = []; TableTitles = []; selectedTable = -1; OnPropertyChanged(nameof(SelectedTableIndex)); ShownTable = null;
        WarningText = "";
        curves = []; visibility.Clear(); OnPropertyChanged(nameof(Curves)); CurveChoices = []; capacitySeries = [];
        RawResult = VerticalPilePresentation.RawChanged;
        UpdateCapacityChart();
    }

    /// <summary>Preview of the historic view: values shown before the calculation, field states, chart message, layer columns, reference chart.</summary>
    private void Preview(JsonObject? result)
    {
        PreviewForms();
        emptyMessage = VerticalPilePresentation.CapacityEmptyMessage(Data);
        UpdateCapacityChart();
        UpdateLayerColumns();
        ReferenceChart = VerticalPileCharts.Reference(Data, result, IsMicro, SelectedSurveyIndex);
    }

    /// <summary>The part of the preview shown in the forms: weight of the micropile, efficiency, ξ, enabled and visible fields.</summary>
    private void PreviewForms()
    {
        var values = VerticalPilePreview.Read(Data, IsMicro);
        if (IsMicro) GeneralForm.Set("__peso_lineare", values.Weight, display: true);
        var general = Data["generali"]!;
        EfficiencyText = values.Efficiency;
        foreach (var (key, value) in values.Correlation) NormativeForm.Set(key, value, true);
        foreach (var (key, enabled) in VerticalPilePresentation.GeneralFieldStates(general, IsMicro)) GeneralForm.Enable(key, enabled, dim: true);
        foreach (var (key, enabled) in VerticalPilePresentation.NormativeFieldStates(general, IsMicro)) NormativeForm.Enable(key, enabled, dim: true);
        string method = Data["efficienza"].S("metodo");
        foreach (var field in EfficiencyForm.Fields.Where(f => f.Key != "metodo")) EfficiencyForm.ShowField(field.Key, VerticalPilePresentation.EfficiencyFieldVisible(field.Key, method));
    }

    /// <summary>UpdatePileCoefficients of the historic view: the calculated columns of every row (the colour of the layer is a token of the view).</summary>
    private void UpdateLayerColumns()
    {
        var general = Data["generali"]!;
        foreach (var survey in Surveys)
        {
            var columns = VerticalPilePreview.LayerFields(general, survey.Rows.Select(r => r.Values), IsMicro);
            for (int i = 0; i < survey.Rows.Count; i++)
                foreach (var (key, value) in columns[i]) survey.Rows[i].Output(key, value);
        }
    }

    // ---------------------------------------------------------------- results

    /// <summary>ShowGeoResults of the historic view.</summary>
    private void ShowResults(JsonObject result)
    {
        curves = [.. VerticalPileCharts.Capacity(Data, result, IsMicro)];
        visibility.Clear();
        foreach (var curve in curves) visibility[curve.Key] = curve.Visible;
        OnPropertyChanged(nameof(Curves));
        capacityTitle = VerticalPilePresentation.CapacityTitle(IsMicro); capacityAxis = VerticalPilePresentation.CapacityAxis(IsMicro, result);
        capacityDepth = VerticalPilePresentation.AvailableDepth(result);
        UpdateVisible();
        RebuildCurveChoices();
        tables = VerticalPilePresentation.Tables(result, IsMicro);
        TableTitles = [VerticalPilePresentation.CapacitySummaryTitle, .. tables.Select(t => t.Titolo)];
        selectedTable = 0; OnPropertyChanged(nameof(SelectedTableIndex)); ShowTable();
        Verification = VerticalPilePresentation.Verification(result, IsMicro);
        ReferenceChart = VerticalPileCharts.Reference(Data, result, IsMicro, SelectedSurveyIndex);
        if (!result.B("copertura_completa")) WarningText = VerticalPilePresentation.Warnings(WarningText, result);
    }

    private bool Includes(string key) => VerticalPileCharts.CapacityIncludes(key, IsMicro, capacityView, curves.Select(c => c.Key));

    /// <summary>UpdateVisible of the historic view: the visible series of the chosen view.</summary>
    private void UpdateVisible()
    {
        capacitySeries = curves.Where(c => visibility.GetValueOrDefault(c.Key) && Includes(c.Key)).Select(c => c.Series).ToArray();
        if (Result is not null) emptyMessage = VerticalPilePresentation.NoCurveSelected;
        UpdateCapacityChart();
    }

    /// <summary>RebuildCurveChoices of the historic view: the length of the values at L and a check box for each curve of the chosen view.</summary>
    private void RebuildCurveChoices()
    {
        var result = Result;
        EndValues = VerticalPilePresentation.EndValues(Data, result);
        CurveChoices = curves.Where(c => Includes(c.Key)).Select(c => new VerticalPileCurveChoiceViewModel(c.Key,
            VerticalPilePresentation.CurveChoice(c.Series.Name, c.Series.Points, result), c.Series.Color, visibility[c.Key], OnCurveChecked)).ToArray();
    }

    private void OnCurveChecked(VerticalPileCurveChoiceViewModel choice)
    {
        if (IsDisposed) return;
        visibility[choice.Key] = choice.IsChecked;
        StoreVisibility();
        UpdateVisible();
    }

    private void SetVisible(bool show)
    {
        if (IsDisposed) return;
        foreach (var curve in curves) visibility[curve.Key] = show;
        StoreVisibility();
        UpdateVisible();
        RebuildCurveChoices();
    }

    /// <summary>StoreVisibility of the historic view: with a result, the visibility of every curve into the data; Modified when it changes.</summary>
    private void StoreVisibility()
    {
        if (Result is null) return;
        if (Data["visibilita_grafici"] is not JsonObject) Data["visibilita_grafici"] = new JsonObject();
        var saved = Data["visibilita_grafici"]!;
        bool changed = false;
        foreach (var (key, value) in visibility)
        {
            if (saved[key]?.ToString() != value.ToString().ToLowerInvariant()) changed = true;
            saved[key] = value;
        }
        if (changed) ChangedKeepingResults();
    }

    private void UpdateCapacityChart() =>
        CapacityChart = new VerticalPileCapacityChart(capacityTitle, VerticalPileCapacityChart.Axis, capacityAxis, capacityDepth, emptyMessage, capacitySeries);

    /// <summary>ShowTable of the historic view.</summary>
    private void ShowTable()
    {
        int i = selectedTable;
        if (i == 0 && Result is { } result)
        {
            ShownTable = new VerticalPileTableView(VerticalPilePresentation.CapacitySummaryNote(result, IsMicro),
                Tabelle.CapacitaPalo(result, IsMicro).Select(t => Block(t.Titolo, t)).ToArray(), VerticalPilePresentation.CapacitySummaryFootnote);
            return;
        }
        i--;
        if (i >= 0 && i < tables.Count) ShownTable = new VerticalPileTableView(null, [Block(null, tables[i])], null);
    }

    private static VerticalPileTableBlock Block(string? title, Tabella table) =>
        new(title, table.Colonne, table.Righe.Select(row => row.Select(VerticalPilePresentation.DetailCell).ToArray()).ToArray());

    /// <summary>Report of the sheet: ReportWord with the figures drawn from the charts as shown (reports frozen on 8/10).</summary>
    private sealed class VerticalPileReport(VerticalPileSheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
        {
            var result = sheet.Result ?? throw new InvalidOperationException("Premere Calcola prima di esportare.");
            var images = sheet.figures.Render(new VerticalPileFigureRequest(sheet.Data, result, sheet.IsMicro, sheet.CapacityChart, sheet.ReferenceChart));
            return DocxWriter.Write(ReportWord.CreateDocument(title, sheet.ModuleId, sheet.Data, result, options.ToHashSet(), images.Select(f => f.Image()).ToList(), !projectReport));
        }
    }
}
