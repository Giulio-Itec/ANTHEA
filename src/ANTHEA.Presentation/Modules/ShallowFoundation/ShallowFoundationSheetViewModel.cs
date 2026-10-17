using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.FoundationWorkspace;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.Stratigraphy;
using Anthea.Presentation.Mvvm;
using Anthea.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ShallowFoundation;

/// <summary>
/// Sheet of the shallow foundations (geo_fondazioni_superficiali), F5.9b: the behaviour of the historic view FoundationWorkspace
/// without WPF, on the same document and with the same writes, in the same order.
/// <list type="bullet">
/// <item>Opening: <see cref="FoundationDocument.ValidateShape"/> and <see cref="FoundationMaterials.Complete"/> (the migration of the
/// archives of version 1, written without Modified), then the fields; no calculation.</item>
/// <item>Numbers, name of the combination, groundwater and concrete: the raw text, at every change
/// (<see cref="FieldWriteRule.RawText"/>); options as JSON booleans. The five choices of the method write the INDEX of the item
/// (<see cref="ShallowFoundationChoiceViewModel"/>), not its text: explicit, outside <see cref="FieldSpec"/>.</item>
/// <item>An edit clears the result («Dati modificati · premere Calcola») and raises Modified; nothing is recalculated
/// automatically. The rules of visibility (<see cref="ShallowFoundationPresentation.FieldVisible"/>) follow every edit.</item>
/// <item>Calculation (decision Q8 a, <see cref="SheetCalculationMode.Synchronous"/>): only with «Calcola», on the UI thread, through
/// the <see cref="IModuleService"/> of the context. An error leaves no result and its text is the status line.</item>
/// <item>The groups of inputs and «Raccogli input» save their state in «vista_fondazioni» and raise Modified without clearing the
/// result (<see cref="SheetViewModelBase.ChangedKeepingResults"/>), also in a read-only revision, as today.</item>
/// <item>Surveys and layers: the table shows a copy of each layer of the survey examined with its letter (the colour is a token of
/// the view); a cell writes its value into the layer. Removing a survey asks <see cref="Services.IConfirmationService.ConfirmDelete"/>;
/// the last survey stays («Conservare almeno un sondaggio.»).</item>
/// <item>Read-only (a previous revision, <see cref="SheetViewModelBase.IsReadOnly"/>, decision T2): fields, choices, surveys, layers,
/// the soil transfer and the section of the failure mechanism refuse the changes, as the controls of the historic view protected by
/// RevisionInspection (decision FN4: the section is always refused); groups, «Raccogli input», report, guide and calculation stay
/// available (FN3: groups and collapsed inputs still write vista_fondazioni, as today).</item>
/// <item>Commands of the view (F5.9b): «Calcola» commits and calculates (decision D7), «Raccogli input», «Report Word e PDF», «Guida»,
/// «+ Sondaggio», «− Sondaggio», «Invia / carica terreno…» (the historic dialog behind <see cref="ISoilTransferDialog"/>, decision T5
/// until its shared ViewModel), «Aggiungi strato» and the [−] of a layer; their errors go through the guard of the sheet (Q19).</item>
/// <item>Help (decision FN9, as today): the «?» labels and F1 of the fields only for the groundwater and the concrete, the forms of the
/// historic view built with InputForm; F1 elsewhere opens the guide of the module through the host.</item>
/// <item>The six graphic tabs are plain data: the drawings of the geometry and of the stratigraphy read <see cref="InputDrawing"/>,
/// those of the results <see cref="Graphics"/> and <see cref="Mechanism"/>; without them <see cref="GraphicsMessage"/>.</item>
/// </list>
/// Texts and rules come from <see cref="ShallowFoundationPresentation"/>, fields from <see cref="ShallowFoundationFields"/>, both shared
/// with the historic view. The equivalence with that view is proved without WPF by tests/ANTHEA.Presentation.Tests
/// (ShallowFoundationSheetChecks) against its traces frozen in tests/fixtures/f5-fondazioni and against the F3 reference.
/// </summary>
public sealed class ShallowFoundationSheetViewModel : SheetViewModelBase
{
    /// <summary>Interface state that the sheet writes into its data (<see cref="ModulePersistence"/>): groups and collapsed inputs.</summary>
    public static IReadOnlyList<string> UiStateKeys { get; } = ["vista_fondazioni"];

    private const string CollapsedKey = "input_raccolti";
    private readonly bool libraryAvailable;
    private readonly ISoilTransferDialog? transfer;
    private readonly JsonObject[] concreteCatalog = MaterialInputCatalog.Concrete();
    private bool loading = true, inputsCollapsed;
    private FormViewModel? geometryForm, actionsForm, surchargeForm, waterForm, combinationForm, confirmationForm, relativeDensityForm, methodOptionsForm,
        waterUnitWeightForm, concreteForm, halfSpaceForm;
    private IReadOnlyList<ShallowFoundationChoiceViewModel> methodChoices = [];
    private ShallowFoundationChoiceViewModel? compression;
    private IReadOnlyList<ShallowFoundationGroupViewModel> groups = [];
    private IReadOnlyList<string> surveys = [];
    private int selectedSurvey;
    private string state = ShallowFoundationPresentation.StateToVerify, resultsText = "";
    private bool hasDetails;
    private IReadOnlyList<string> summary = [], notes = [];
    private IReadOnlyList<string[]> coefficients = [], additive = [];
    private string? graphicsMessage;
    private JsonObject? graphics, mechanism;
    private string pressureSummary = "", contactDescription = "", mechanismDescription = "";
    private int mechanismAxis = -1, drawingVersion;
    private ShallowFoundationInputDrawing? inputDrawing;

    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the module.</param>
    /// <param name="libraryAvailable">The approved release of the GPC library is installed (FoundationLibrary.Available): only the
    /// text of the results without a result depends on it.</param>
    /// <param name="transfer">The soil transfer dialog of «Invia / carica terreno…»; without it the command is not available.</param>
    public ShallowFoundationSheetViewModel(SheetContext context, bool libraryAvailable = true, ISoilTransferDialog? transfer = null)
        : base(FoundationDocument.Module, context, null, SheetCalculationMode.Synchronous)
    {
        this.libraryAvailable = libraryAvailable;
        this.transfer = transfer;
        Report = new FoundationReportSource(this);
        Build();
        loading = false;
        RefreshVisibility();
        ClearResults();
        CalculateCommand = GuardedCommands.CreateAsync(Guard, CalculateFromViewAsync, Lifetime, () => !IsDisposed);
        CollectCommand = GuardedCommands.Create(Guard, ToggleInputs, () => !IsDisposed);
        ReportCommand = GuardedCommands.Create(Guard, RequestReport, () => !IsDisposed);
        GuideCommand = GuardedCommands.Create(Guard, OpenGuide);
        AddSurveyCommand = GuardedCommands.Create(Guard, AddSurvey, () => CanEdit);
        RemoveSurveyCommand = GuardedCommands.Create(Guard, RemoveSurvey, () => CanEdit);
        TransferSoilCommand = GuardedCommands.CreateAsync(Guard, TransferSoilAsync, Lifetime, () => CanEdit && transfer is not null);
        AddLayerCommand = GuardedCommands.Create(Guard, AddLayer, () => CanEdit);
        RemoveLayerCommand = new RelayCommand<JsonRowViewModel?>(row => Guard.Run(() => RemoveLayer(row is null ? -1 : Layers.IndexOf(row))), _ => CanEdit);
    }

    public override IModuleReportSource Report { get; }

    // ---------------------------------------------------------------- commands of the view

    /// <summary>«Calcola»: the drafts are committed, then the sheet is calculated now (synchronous, Q8 a); allowed when read-only.</summary>
    public IAsyncRelayCommand CalculateCommand { get; }

    /// <summary>«Raccogli input» / «Mostra input» (<see cref="ToggleInputs"/>); allowed when read-only.</summary>
    public IRelayCommand CollectCommand { get; }

    /// <summary>«Report Word e PDF» (<see cref="RequestReport"/>); allowed when read-only.</summary>
    public IRelayCommand ReportCommand { get; }

    /// <summary>«Guida» (<see cref="OpenGuide"/>).</summary>
    public IRelayCommand GuideCommand { get; }

    /// <summary>«+ Sondaggio» (<see cref="AddSurvey"/>).</summary>
    public IRelayCommand AddSurveyCommand { get; }

    /// <summary>«− Sondaggio» (<see cref="RemoveSurvey"/>).</summary>
    public IRelayCommand RemoveSurveyCommand { get; }

    /// <summary>
    /// «Invia / carica terreno…»: the sheet commits, then the soil transfer dialog receives its module, its data and the soil sheets
    /// of the project; a copy goes to the window as a new sheet, a replacement as the soil of this sheet (SheetEditor.OpenSoilTransfer).
    /// </summary>
    public IAsyncRelayCommand TransferSoilCommand { get; }

    /// <summary>«Aggiungi strato» (<see cref="AddLayer"/>).</summary>
    public IRelayCommand AddLayerCommand { get; }

    /// <summary>The [−] of a layer: the parameter is the row of <see cref="Layers"/>.</summary>
    public IRelayCommand<JsonRowViewModel?> RemoveLayerCommand { get; }

    // ---------------------------------------------------------------- input

    /// <summary>The choices of the group «Metodo e verifica»: method, shape, condition, profile.</summary>
    public IReadOnlyList<ShallowFoundationChoiceViewModel> MethodChoices { get => methodChoices; private set => SetProperty(ref methodChoices, value); }

    /// <summary>The numbers of the group «Geometria».</summary>
    public FormViewModel GeometryForm => geometryForm!;

    /// <summary>The numbers of the group «Azioni».</summary>
    public FormViewModel ActionsForm => actionsForm!;

    /// <summary>The external surcharge, after the groups.</summary>
    public FormViewModel SurchargeForm => surchargeForm!;

    /// <summary>Groundwater of the common data (generali).</summary>
    public FormViewModel WaterForm => waterForm!;

    /// <summary>Name of the combination (nome_combinazione).</summary>
    public FormViewModel CombinationForm => combinationForm!;

    /// <summary>Confirmation of the A1 actions (azioni_confermate), shown only for the NTC 2018 profile.</summary>
    public FormViewModel ConfirmationForm => confirmationForm!;

    /// <summary>Options of the method: the compressibility (an index choice), then these fields.</summary>
    public ShallowFoundationChoiceViewModel Compression => compression!;
    public FormViewModel RelativeDensityForm => relativeDensityForm!;
    public FormViewModel MethodOptionsForm => methodOptionsForm!;
    public FormViewModel WaterUnitWeightForm => waterUnitWeightForm!;

    /// <summary>The shared concrete (sezione): a class of the catalogue copies its values, a typed fck names its class.</summary>
    public FormViewModel ConcreteForm => concreteForm!;

    /// <summary>The bearing layer assumed as a homogeneous half-space (generali/strato_portante_semiinfinito).</summary>
    public FormViewModel HalfSpaceForm => halfSpaceForm!;

    /// <summary>The forms of the sheet in the order of the view.</summary>
    public IEnumerable<FormViewModel> Forms => new[] { geometryForm, actionsForm, surchargeForm, waterForm, combinationForm, confirmationForm, relativeDensityForm,
        methodOptionsForm, waterUnitWeightForm, concreteForm, halfSpaceForm }.OfType<FormViewModel>();

    /// <summary>The field <paramref name="key"/> of the forms of the sheet (the keys are unique among the forms).</summary>
    public FieldViewModel Field(string key) => Forms.Where(f => f.Contains(key)).Select(f => f[key]).Single();

    /// <summary>The choice <paramref name="key"/> (Method, Shape, Condition, Profile, Compression).</summary>
    public ShallowFoundationChoiceViewModel Choice(string key) => MethodChoices.Append(Compression).Single(c => c.Key == key);

    /// <summary>The groups of inputs whose state is saved («Metodo e verifica», «Geometria», «Azioni», «Stratigrafia»).</summary>
    public IReadOnlyList<ShallowFoundationGroupViewModel> Groups { get => groups; private set => SetProperty(ref groups, value); }

    /// <summary>The inputs are collapsed and the graphics take the whole width (vista_fondazioni/input_raccolti).</summary>
    public bool InputsCollapsed
    {
        get => inputsCollapsed;
        private set { if (SetProperty(ref inputsCollapsed, value)) OnPropertyChanged(nameof(CollectText)); }
    }

    /// <summary>Text of the command that collapses or shows the inputs.</summary>
    public string CollectText => inputsCollapsed ? ShallowFoundationFields.ShowInputs : ShallowFoundationFields.CollectInputs;

    /// <summary>«Raccogli input» / «Mostra input»: saved in the sheet, the result stays; allowed also when read-only, as today.</summary>
    public void ToggleInputs()
    {
        if (IsDisposed) return;
        InputsCollapsed = !inputsCollapsed;
        ShallowFoundationPresentation.SaveViewFlag(Data, CollapsedKey, inputsCollapsed);
        ChangedKeepingResults();
    }

    /// <summary>Names of the surveys («Sondaggio 1» …).</summary>
    public IReadOnlyList<string> Surveys { get => surveys; private set => SetProperty(ref surveys, value); }

    /// <summary>The survey examined (generali/sondaggio_fondazione); choosing another one shows its layers and clears the result.</summary>
    public int SelectedSurvey
    {
        get => selectedSurvey;
        set
        {
            if (value == selectedSurvey) return;
            if (!CanEdit || value < 0 || value >= surveys.Count) { OnPropertyChanged(); return; }
            selectedSurvey = value;
            OnPropertyChanged();
            Data["generali"]!["sondaggio_fondazione"] = value;
            BuildLayers();
            Edited();
        }
    }

    /// <summary>«+ Sondaggio»: a new survey with one layer, selected.</summary>
    public void AddSurvey()
    {
        if (!CanEdit) return;
        var list = Data["stratigrafie"]!.AsArray();
        list.Add(new JsonArray(FoundationMaterials.NewLayer()));
        Data["generali"]!["sondaggio_fondazione"] = list.Count - 1;
        RefreshSurveys(); BuildLayers(); Edited();
    }

    /// <summary>«− Sondaggio»: removes the survey examined after the question; the last survey stays.</summary>
    public void RemoveSurvey()
    {
        if (!CanEdit) return;
        var list = Data["stratigrafie"]!.AsArray();
        if (list.Count <= 1) { State = ShallowFoundationPresentation.KeepOneSurvey; return; }
        if (!Context.Services.Confirmations.ConfirmDelete(ShallowFoundationPresentation.RemoveSurveyTitle, ShallowFoundationPresentation.RemoveSurveyQuestion)) return;
        list.RemoveAt(selectedSurvey);
        Data["generali"]!["sondaggio_fondazione"] = Math.Min(selectedSurvey, list.Count - 1);
        RefreshSurveys(); BuildLayers(); Edited();
    }

    /// <summary>Columns of the table of the layers.</summary>
    public IReadOnlyList<FieldSpec> LayerColumns => ShallowFoundationFields.LayerColumns;

    /// <summary>The layers of the survey examined: a copy of each layer with its letter («__strato»); a cell writes its value into the layer.</summary>
    public JsonRowCollection Layers { get; } = [];

    /// <summary>«Aggiungi strato»: a new layer at the end of the survey examined.</summary>
    public void AddLayer()
    {
        if (!CanEdit) return;
        var rows = FoundationMaterials.Layers(Data);
        var row = FoundationMaterials.NewLayer();
        rows.Add(row);
        Layers.Add(Bind(row));
        Letters();
        Edited();
    }

    /// <summary>The [−] of the layer <paramref name="index"/>.</summary>
    public void RemoveLayer(int index)
    {
        if (!CanEdit || index < 0 || index >= Layers.Count) return;
        FoundationMaterials.Layers(Data).RemoveAt(index);
        Layers.RemoveAt(index);
        Letters();
        Edited();
    }

    // ---------------------------------------------------------------- output

    /// <summary>Status line: to verify, modified, calculated, the error of the calculation or of the surveys.</summary>
    public string State { get => state; private set => SetProperty(ref state, value); }

    /// <summary>The headline of the result, or the text of the results without a result.</summary>
    public string ResultsText { get => resultsText; private set => SetProperty(ref resultsText, value); }

    /// <summary>The details of the verification are shown (expander under the headline).</summary>
    public bool HasDetails { get => hasDetails; private set => SetProperty(ref hasDetails, value); }

    /// <summary>The summary of the details, one line per value («label   value»).</summary>
    public IReadOnlyList<string> Summary { get => summary; private set => SetProperty(ref summary, value); }

    public IReadOnlyList<string> CoefficientColumns => ShallowFoundationPresentation.CoefficientColumns;

    /// <summary>Coefficients and contributions: cohesion, surcharge, soil weight.</summary>
    public IReadOnlyList<string[]> Coefficients { get => coefficients; private set => SetProperty(ref coefficients, value); }

    public string AdditiveTitle => ShallowFoundationPresentation.AdditiveTitle;

    public IReadOnlyList<string> AdditiveColumns => ShallowFoundationPresentation.AdditiveColumns;

    /// <summary>Additive corrections of Brinch Hansen (undrained); empty otherwise, and the view hides the title and the table.</summary>
    public IReadOnlyList<string[]> Additive { get => additive; private set => SetProperty(ref additive, value); }

    /// <summary>Limits, reference of the method and warnings of the library, one per line.</summary>
    public IReadOnlyList<string> Notes { get => notes; private set => SetProperty(ref notes, value); }

    /// <summary>What the drawings of the geometry and of the stratigraphy show: the data of the sheet, renewed after every edit.</summary>
    public ShallowFoundationInputDrawing? InputDrawing { get => inputDrawing; private set => SetProperty(ref inputDrawing, value); }

    /// <summary>Text of the four tabs of the results when they show no drawing (before a result, or a result without graphics).</summary>
    public string? GraphicsMessage { get => graphicsMessage; private set => SetProperty(ref graphicsMessage, value); }

    /// <summary>Graphics of the result (effective area, pressures, sections): plain data of the library; null with <see cref="GraphicsMessage"/>.</summary>
    public JsonObject? Graphics { get => graphics; private set => SetProperty(ref graphics, value); }

    /// <summary>Text under the map of the pressures.</summary>
    public string PressureSummary { get => pressureSummary; private set => SetProperty(ref pressureSummary, value); }

    /// <summary>Text under the sections of the pressures.</summary>
    public string ContactDescription { get => contactDescription; private set => SetProperty(ref contactDescription, value); }

    public IReadOnlyList<string> MechanismAxes => ShallowFoundationPresentation.MechanismAxes;

    /// <summary>Section of the failure mechanism (0 X, 1 Y); view state not saved, refused when read-only as the historic choice.</summary>
    public int MechanismAxis
    {
        get => mechanismAxis;
        set
        {
            if (value == mechanismAxis) return;
            if (!CanEdit || graphics is null || value < 0 || value >= MechanismAxes.Count) { OnPropertyChanged(); return; }
            mechanismAxis = value;
            OnPropertyChanged();
            Mechanism = graphics[ShallowFoundationPresentation.MechanismKey(value)]!.AsObject();
        }
    }

    /// <summary>The failure mechanism of the section shown.</summary>
    public JsonObject? Mechanism { get => mechanism; private set => SetProperty(ref mechanism, value); }

    /// <summary>Text under the failure mechanism.</summary>
    public string MechanismDescription { get => mechanismDescription; private set => SetProperty(ref mechanismDescription, value); }

    /// <summary>«Report Word e PDF» of the sheet: the window exports it.</summary>
    public void RequestReport() => Context.Host.RequestReport();

    /// <summary>«Guida»: the guide of the module in the Wiki.</summary>
    public void OpenGuide() { if (ModuleHelp.For(ModuleId).GuideId is { } guide) Context.Services.Wiki.Show(guide); }

    /// <summary>«Calcola» of the view: as TryCalculate of the historic view, which commits the table of the layers first (D7).</summary>
    private async Task CalculateFromViewAsync(CancellationToken cancellation)
    {
        if (IsDisposed) return;
        Commit();
        await CalculateAsync(cancellation);
    }

    /// <summary>«Invia / carica terreno…» (see <see cref="TransferSoilCommand"/>); refused when read-only, as the historic button.</summary>
    private async Task TransferSoilAsync(CancellationToken cancellation)
    {
        if (!CanEdit || transfer is null) return;
        Commit();
        var choice = await transfer.ShowAsync(ModuleId, Data, Context.Host.SoilSources());
        if (choice is null || IsDisposed) return;
        if (choice.Module is null) Context.Host.ReplaceSoil(choice.Data);
        else Context.Host.OpenModuleCopy(choice.Module, choice.Data, choice.Name);
    }

    // ---------------------------------------------------------------- sheet

    /// <summary>The data were replaced: the sheet is built again on them, as at opening, without a result.</summary>
    protected override void OnReloaded()
    {
        loading = true;
        try { Build(); }
        finally { loading = false; }
        RefreshVisibility();
        ClearResults();
        State = ShallowFoundationPresentation.StateToVerify;
    }

    /// <summary>TryCalculate of the historic view: the results are cleared, then the module calculates the data.</summary>
    protected override ModuleCalculation CalculateSynchronously()
    {
        ClearResults();
        return CalculateModule(Data);
    }

    protected override void OnCalculationPublished()
    {
        if (Result is not { } result) { State = CalculationMessage; return; }
        Present(result);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(CanEdit) || IsDisposed) return;
        foreach (var form in Forms) form.IsReadOnly = !CanEdit;
        foreach (var choice in MethodChoices.Append(compression).OfType<ShallowFoundationChoiceViewModel>()) choice.Refresh();
        // The commands exist after the construction (the base can raise CanEdit before).
        foreach (var command in new IRelayCommand?[] { AddSurveyCommand, RemoveSurveyCommand, TransferSoilCommand, AddLayerCommand, RemoveLayerCommand })
            command?.NotifyCanExecuteChanged();
    }

    protected override void OnDisposed()
    {
        foreach (var group in groups) group.Changed = null;
        base.OnDisposed();
    }

    // ---------------------------------------------------------------- construction

    private void Build()
    {
        FoundationDocument.ValidateShape(Data); FoundationMaterials.Complete(Data);
        var input = Data["input"]!.AsObject();
        MethodChoices = ShallowFoundationFields.MethodChoices.Select(c => new ShallowFoundationChoiceViewModel(this, input, c)).ToArray();
        Set(ref geometryForm, Form(input, ShallowFoundationFields.Geometry, compact: true), nameof(GeometryForm));
        Set(ref actionsForm, Form(input, ShallowFoundationFields.Actions, compact: true), nameof(ActionsForm));
        Set(ref surchargeForm, Form(input, [ShallowFoundationFields.Surcharge], compact: true), nameof(SurchargeForm));
        var general = Data["generali"]!.AsObject();
        Set(ref waterForm, Form(general, ShallowFoundationFields.Water, help: true), nameof(WaterForm));
        Set(ref combinationForm, Form(Data, [new FieldSpec("nome_combinazione", ShallowFoundationFields.CombinationLabel)], compact: true), nameof(CombinationForm));
        Set(ref confirmationForm, Form(Data, [new FieldSpec("azioni_confermate", ShallowFoundationPresentation.ConfirmationLabel, Bool: true)]), nameof(ConfirmationForm));
        compression = new ShallowFoundationChoiceViewModel(this, input, ShallowFoundationFields.Compression);
        OnPropertyChanged(nameof(Compression));
        Set(ref relativeDensityForm, Form(input, [ShallowFoundationFields.RelativeDensity], compact: true), nameof(RelativeDensityForm));
        Set(ref methodOptionsForm, Form(input, ShallowFoundationFields.MethodOptions), nameof(MethodOptionsForm));
        Set(ref waterUnitWeightForm, Form(input, [ShallowFoundationFields.WaterUnitWeight], compact: true), nameof(WaterUnitWeightForm));
        var section = Data["sezione"]!.AsObject();
        FormViewModel? concrete = null;
        concrete = new FormViewModel(section, ShallowFoundationFields.Concrete(section, concreteCatalog), key =>
        {
            ShallowFoundationPresentation.ConcreteChanged(section, key, concreteCatalog, (k, v) => concrete!.Set(k, v, true));
            Edited();
        }, wikiModule: ModuleId) { IsReadOnly = !CanEdit, WikiNavigator = Context.Services.Wiki };
        Set(ref concreteForm, concrete, nameof(ConcreteForm));
        foreach (var group in groups) group.Changed = null;
        Groups = new[] { ShallowFoundationFields.MethodGroup, ShallowFoundationFields.GeometryGroup, ShallowFoundationFields.ActionsGroup, ShallowFoundationFields.SoilGroup }
            .Select(g => new ShallowFoundationGroupViewModel(g, ShallowFoundationPresentation.ViewFlag(Data, g.Key, true)) { Changed = OnGroupChanged }).ToArray();
        InputsCollapsed = ShallowFoundationPresentation.ViewFlag(Data, CollapsedKey, false);
        RefreshSurveys();
        BuildLayers();
    }

    private void Set(ref FormViewModel? field, FormViewModel value, string name)
    {
        field = value;
        OnPropertyChanged(name);
    }

    /// <summary>
    /// A form that writes the raw text and clears the result at every change; read-only while the sheet cannot be edited. Compact: the
    /// 12-point rows of the historic view (decision D10). With <paramref name="help"/> the forms of the historic view built with
    /// InputForm (groundwater, concrete), which had the «?» and F1 of the module (FN9): the module is explicit, so that the module
    /// inherited by the view (WikiContextHelp.Module, not set by MvvmSheetEditor) does not replace it.
    /// </summary>
    private FormViewModel Form(JsonObject values, IEnumerable<FieldSpec> fields, bool help = false, bool compact = false) => help
        ? new FormViewModel(values, fields, _ => Edited(), compact, wikiModule: ModuleId) { IsReadOnly = !CanEdit, WikiNavigator = Context.Services.Wiki }
        : new FormViewModel(values, fields, _ => Edited(), compact) { IsReadOnly = !CanEdit };

    private void RefreshSurveys()
    {
        Surveys = ShallowFoundationPresentation.SurveyNames(Data);
        selectedSurvey = ShallowFoundationPresentation.SelectedSurvey(Data);
        OnPropertyChanged(nameof(SelectedSurvey));
    }

    /// <summary>BuildSoils of the historic view: the table of the survey examined and the option of the half-space.</summary>
    private void BuildLayers()
    {
        var rows = FoundationMaterials.Layers(Data);
        using (Layers.DeferRefresh())
        {
            Layers.Clear();
            foreach (var row in rows.OfType<JsonObject>()) Layers.Add(Bind(row));
            Letters();
        }
        var general = Data["generali"]!.AsObject();
        Set(ref halfSpaceForm, Form(general, [new FieldSpec("strato_portante_semiinfinito", ShallowFoundationPresentation.HalfSpaceLabel, Bool: true)]), nameof(HalfSpaceForm));
    }

    /// <summary>The copy of a layer shown by the table; a cell writes its value into the layer and clears the result.</summary>
    private JsonRowViewModel Bind(JsonObject row)
    {
        var display = row.DeepClone().AsObject();
        return new JsonRowViewModel(display, key =>
        {
            // A read-only table refuses the cell (the view binds the cells to CanEdit): the copy is shown again.
            if (!CanEdit) { BuildLayers(); return; }
            row[key] = display[key]?.DeepClone();
            Edited();
        });
    }

    private void Letters()
    {
        for (int i = 0; i < Layers.Count; i++) Layers[i].Output("__strato", ShallowFoundationPresentation.LayerLetter(i));
    }

    private void OnGroupChanged(ShallowFoundationGroupViewModel group)
    {
        if (loading || IsDisposed) return;
        ShallowFoundationPresentation.SaveViewFlag(Data, group.Key, group.IsExpanded);
        ChangedKeepingResults();
    }

    // ---------------------------------------------------------------- results

    /// <summary>Changed of the historic view: an edit of the user clears the result, refreshes the rules and raises Modified.</summary>
    internal void Edited()
    {
        if (loading || IsDisposed) return;
        RefreshVisibility();
        ClearResults();
        State = ShallowFoundationPresentation.StateChanged;
        Changed();
    }

    /// <summary>RefreshVisibility of the historic view: the rules of the fields, then the drawings of the inputs on the data.</summary>
    private void RefreshVisibility()
    {
        var input = Data["input"]!.AsObject();
        var visibility = ShallowFoundationPresentation.VisibilityInput(Data);
        foreach (var choice in MethodChoices) choice.IsVisible = ShallowFoundationPresentation.FieldVisible(input, visibility, choice.Key);
        foreach (var form in new[] { geometryForm, actionsForm, surchargeForm, confirmationForm, relativeDensityForm, methodOptionsForm, waterUnitWeightForm })
            foreach (var field in form!.Fields) form.ShowField(field.Key, ShallowFoundationPresentation.FieldVisible(input, visibility, field.Key));
        compression!.IsVisible = ShallowFoundationPresentation.FieldVisible(input, visibility, compression.Key);
        InputDrawing = new ShallowFoundationInputDrawing(Data, ++drawingVersion);
    }

    /// <summary>ClearResults of the historic view.</summary>
    private void ClearResults()
    {
        ResultsText = ShallowFoundationPresentation.ResultsPlaceholder(libraryAvailable);
        HasDetails = false; Summary = []; Coefficients = []; Additive = []; Notes = [];
        Graphics = null; Mechanism = null; mechanismAxis = -1; OnPropertyChanged(nameof(MechanismAxis));
        PressureSummary = ContactDescription = MechanismDescription = "";
        GraphicsMessage = ShallowFoundationPresentation.ViewToCalculate;
    }

    /// <summary>The result of TryCalculate of the historic view: headline, details, graphics and status line.</summary>
    private void Present(JsonObject result)
    {
        var input = Data["input"]!.AsObject();
        ResultsText = ShallowFoundationPresentation.Headline(input, result);
        HasDetails = true;
        Summary = FoundationPresentation.Summary(result).Select(ShallowFoundationPresentation.SummaryLine).ToArray();
        Coefficients = FoundationPresentation.CoefficientRows(result).ToArray();
        Additive = FoundationPresentation.AdditiveRows(result).ToArray();
        Notes = new[] { ShallowFoundationPresentation.Limits, result["Reference"]!.ToString() }.Concat(result["Warnings"]!.AsArray().Select(w => w!.ToString())).ToArray();
        if (result["Graphics"] is JsonObject shown)
        {
            GraphicsMessage = null;
            Graphics = shown;
            PressureSummary = ShallowFoundationPresentation.PressureSummary(input, shown);
            ContactDescription = shown["ContactDescription"]!.ToString();
            mechanismAxis = 0; OnPropertyChanged(nameof(MechanismAxis));
            Mechanism = shown[ShallowFoundationPresentation.MechanismKey(0)]!.AsObject();
            MechanismDescription = shown["MechanismDescription"]!.ToString();
        }
        else GraphicsMessage = ShallowFoundationPresentation.ViewNotAvailable;
        State = ShallowFoundationPresentation.CalculatedState(selectedSurvey, Data);
    }

    /// <summary>
    /// Report of the sheet: the existing generator of the foundations on the result (reports frozen on 8/10), the Word document and,
    /// beside it, the PDF of MigraDoc (FoundationReport.Pdf), as SheetEditor.ExportReport writes them (decision T8, FN6).
    /// </summary>
    private sealed class FoundationReportSource(ShallowFoundationSheetViewModel sheet) : IModuleReportSource, IModuleReportPdf
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) =>
            DocxWriter.Write(FoundationReport.WordDocument(title, Calculated()));

        public byte[] BuildPdf(string title, IReadOnlySet<string> options, bool projectReport = false) => FoundationReport.Pdf(title, Calculated());

        private JsonObject Calculated() => sheet.Result ?? throw new InvalidOperationException("Calcolare prima di esportare.");
    }
}

/// <summary>What the drawings of the geometry and of the stratigraphy read: the data of the sheet; a new instance after every edit.</summary>
public sealed record ShallowFoundationInputDrawing(JsonObject Data, int Version);

/// <summary>
/// A choice of the method (Method, Shape, Condition, Profile, Compression): it writes the index of the item into «input», as the
/// combo boxes of the historic view; refused when the sheet cannot be edited.
/// </summary>
public sealed class ShallowFoundationChoiceViewModel : ObservableObject
{
    private readonly ShallowFoundationSheetViewModel sheet;
    private readonly JsonObject input;
    private int selectedIndex;
    private bool isVisible = true;

    internal ShallowFoundationChoiceViewModel(ShallowFoundationSheetViewModel sheet, JsonObject input, ShallowFoundationChoice choice)
    {
        this.sheet = sheet; this.input = input;
        Key = choice.Key; Label = choice.Label; Items = choice.Items;
        selectedIndex = input[Key]!.GetValue<int>();
    }

    public string Key { get; }
    public string Label { get; }
    public IReadOnlyList<string> Items { get; }

    /// <summary>The user can choose (the view disables the choice otherwise).</summary>
    public bool CanChange => sheet.CanEdit;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value == selectedIndex) return;
            if (!sheet.CanEdit || value < 0 || value >= Items.Count) { OnPropertyChanged(); return; }
            selectedIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedItem));
            input[Key] = value;
            sheet.Edited();
        }
    }

    /// <summary>The item chosen; setting an item that is not in the list is ignored.</summary>
    public string? SelectedItem
    {
        get => selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex] : null;
        set { int index = value is null ? -1 : Items.ToList().IndexOf(value); if (index >= 0) SelectedIndex = index; else OnPropertyChanged(); }
    }

    /// <summary>Shown by the rules of the sheet (<see cref="ShallowFoundationPresentation.FieldVisible"/>).</summary>
    public bool IsVisible { get => isVisible; internal set => SetProperty(ref isVisible, value); }

    internal void Refresh() => OnPropertyChanged(nameof(CanChange));
}

/// <summary>A group of inputs whose state the sheet saves in «vista_fondazioni»; allowed also when read-only, as today.</summary>
public sealed class ShallowFoundationGroupViewModel(ShallowFoundationGroup group, bool expanded) : ObservableObject
{
    private bool isExpanded = expanded;

    public string Key { get; } = group.Key;
    public string Title { get; } = group.Title;

    public bool IsExpanded
    {
        get => isExpanded;
        set { if (SetProperty(ref isExpanded, value)) Changed?.Invoke(this); }
    }

    internal Action<ShallowFoundationGroupViewModel>? Changed { get; set; }
}
