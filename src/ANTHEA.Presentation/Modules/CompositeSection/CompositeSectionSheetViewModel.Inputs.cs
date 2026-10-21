using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>
/// The inputs of the sheet (BuildInputs, BuildPhases, BridgeAccessories, BridgeDetailsInputs, BridgeHistory, BridgeHomogenization,
/// BridgePredalle, BridgeTorsion and BridgeActionsTable of the historic view): the forms of the definition, of the accessories and of the
/// options, the cards of the phases and the table of the actions.
/// </summary>
public sealed partial class CompositeSectionSheetViewModel
{
    /// <summary>The forms of the definition, of the accessories and of the options, in the order of the view (the forms of the traces).</summary>
    public static IReadOnlyList<string> FormNames { get; } = ["normativa", "coefficienti", "materiali", "soletta", "predalle", "carpenteria", "sezione", "piastra",
        "armatura-sup", "armatura-inf", "trasversale", "fatica", "pioli", "irrigidimenti", "appoggio", "cassoncino", "metodo", "discretizzazione", "limiti"];

    private readonly Dictionary<string, FormViewModel> forms = new(StringComparer.Ordinal);
    private readonly List<(JsonObject Phase, FormViewModel Form, CompositeSectionPhaseCard Card)> homogenizations = [];
    private IReadOnlyList<CompositeSectionPhaseCard> phases = [];
    private string materialText = "", steelHeightText = "", slabLayoutText = "", methodNotice = "", loadConvention = "", phaseCountText = "";
    private bool plateShown = true, boxShown = true, transverseShown = true, fatigueShown = true, meshShown = true, refreshingActions, steelHeightBuilt, boxBuilt, meshBuilt;
    private JsonRowViewModel? selectedActionRow;
    private string actionStyleKey = "";
    private bool torqueColumnShown = true;
    private IReadOnlyDictionary<string, IReadOnlySet<string>> inactiveKinds = new Dictionary<string, IReadOnlySet<string>>();
    private BridgeGeometry? previewGeometry;
    private JsonObject? previewInput;
    private string geometryError = "";

    /// <summary>The forms by name (<see cref="FormNames"/>).</summary>
    public IReadOnlyDictionary<string, FormViewModel> Forms => forms;

    /// <summary>The cards of the phases, in the order of the phases.</summary>
    public IReadOnlyList<CompositeSectionPhaseCard> Phases { get => phases; private set => SetProperty(ref phases, value); }

    /// <summary>«Proprietà adottate»: the materials of the catalogues, or the error that stops them.</summary>
    public string MaterialText { get => materialText; private set => SetProperty(ref materialText, value); }

    /// <summary>The clear height of the web derived from H (written into the data as h_web).</summary>
    public string SteelHeightText { get => steelHeightText; private set => SetProperty(ref steelHeightText, value); }

    /// <summary>The levels of the predalle and of the bottom bars.</summary>
    public string SlabLayoutText { get => slabLayoutText; private set => SetProperty(ref slabLayoutText, value); }

    /// <summary>The notice of the method of calculation.</summary>
    public string MethodNotice { get => methodNotice; private set => SetProperty(ref methodNotice, value); }

    /// <summary>The convention of the loads above the table of the actions.</summary>
    public string LoadConvention { get => loadConvention; private set => SetProperty(ref loadConvention, value); }

    /// <summary>The count of the phases above the table of the actions.</summary>
    public string PhaseCountText { get => phaseCountText; private set => SetProperty(ref phaseCountText, value); }

    /// <summary>The second bottom plate and its note are shown (the welded H).</summary>
    public bool PlateShown { get => plateShown; private set => SetProperty(ref plateShown, value); }

    /// <summary>The note under the second bottom plate.</summary>
    public string PlateNote => BridgeInputPresentation.PlateNote;

    /// <summary>The group of the box girder is shown.</summary>
    public bool BoxShown { get => boxShown; private set => SetProperty(ref boxShown, value); }

    /// <summary>The transverse reinforcement of the slab is shown (with the studs).</summary>
    public bool TransverseShown { get => transverseShown; private set => SetProperty(ref transverseShown, value); }

    /// <summary>The fatigue of studs and flange is shown (with the studs).</summary>
    public bool FatigueShown { get => fatigueShown; private set => SetProperty(ref fatigueShown, value); }

    /// <summary>«Discretizzazione e sottopassi» is shown (the methods with history).</summary>
    public bool MeshShown { get => meshShown; private set => SetProperty(ref meshShown, value); }

    // ---------------------------------------------------------------- the table of the actions

    /// <summary>The rows of the table of the actions, on the phases themselves (the table always shows every phase).</summary>
    public JsonRowCollection ActionRows { get; } = [];

    /// <summary>The columns of the table of the actions.</summary>
    public IReadOnlyList<FieldSpec> ActionColumns => CompositeSectionFields.Actions;

    /// <summary>The selected row of the table of the actions (−1 without one); the selection of the user, also when read-only.</summary>
    public int SelectedActionIndex
    {
        get => selectedActionRow is null ? -1 : ActionRows.IndexOf(selectedActionRow);
        set
        {
            var row = value >= 0 && value < ActionRows.Count ? ActionRows[value] : null;
            if (ReferenceEquals(row, selectedActionRow)) return;
            selectedActionRow = row; OnPropertyChanged();
        }
    }

    /// <summary>The table of the actions is read-only (a previous revision).</summary>
    public bool ActionsReadOnly => !CanEdit;

    /// <summary>The column of the torque ΔT is shown (the box girder), as the cells were last configured.</summary>
    public bool TorqueColumnShown => torqueColumnShown;

    /// <summary>The cell <paramref name="key"/> of a phase of kind <paramref name="kind"/> does not apply: disabled and faded, never edited (as the cells were last configured).</summary>
    public bool IsCellInactive(string kind, string key) => inactiveKinds.TryGetValue(key, out var kinds) && kinds.Contains(kind);

    /// <summary>The tooltip of a cell that does not apply.</summary>
    public string InactiveCellToolTip => BridgeInputPresentation.InactiveCellToolTip;

    /// <summary>
    /// The user edits the cell <paramref name="key"/> of the row <paramref name="row"/> (as the grid commits it): refused when read-only,
    /// for a hidden column or a cell that does not apply; a text is written as typed, a check box is clicked when <paramref name="value"/> is null.
    /// </summary>
    public void EditCell(int row, string key, string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (!CanEdit || row < 0 || row >= ActionRows.Count) return;
        if (key == "T" && !torqueColumnShown) return;
        var target = ActionRows[row];
        if (IsCellInactive(target.Values.S("tipo"), key)) return;
        if (value is null) target[key] = !(target[key] is true);
        else target[key] = value;
    }

    // ---------------------------------------------------------------- commands

    /// <summary>«+ Aggiungi fase» of the page of the phases and «+ Carico» under the table.</summary>
    public IRelayCommand AddLoadCommand { get; private set; } = null!;

    /// <summary>«+ Ritiro» of the page of the phases and under the table.</summary>
    public IRelayCommand AddShrinkageCommand { get; private set; } = null!;

    /// <summary>«↑» and «↓» under the table (the selected row).</summary>
    public IRelayCommand MoveSelectedUpCommand { get; private set; } = null!;
    public IRelayCommand MoveSelectedDownCommand { get; private set; } = null!;

    /// <summary>«Elimina» under the table (the selected row).</summary>
    public IRelayCommand DeleteSelectedCommand { get; private set; } = null!;

    /// <summary>«Mostra tutte le fasi → ultima situazione».</summary>
    public IRelayCommand LatestSituationCommand { get; private set; } = null!;

    /// <summary>«Dettagli sezione efficace ↓» (also when read-only).</summary>
    public IRelayCommand EffectiveDetailsCommand { get; private set; } = null!;

    /// <summary>«Esporta CSV…» (also when read-only).</summary>
    public IRelayCommand ExportCsvCommand { get; private set; } = null!;

    /// <summary>«Stacca vista» (also when read-only).</summary>
    public IRelayCommand DetachCommand { get; private set; } = null!;

    private readonly List<IRelayCommand> editCommands = [];

    private IRelayCommand EditCommand(Action action)
    {
        var command = GuardedCommands.Create(Guard, action, () => CanEdit);
        editCommands.Add(command); return command;
    }

    private void CreateCommands()
    {
        AddLoadCommand = EditCommand(() => AddPhase(false));
        AddShrinkageCommand = EditCommand(() => AddPhase(true));
        MoveSelectedUpCommand = EditCommand(() => MoveSelected(-1));
        MoveSelectedDownCommand = EditCommand(() => MoveSelected(1));
        DeleteSelectedCommand = EditCommand(DeleteSelected);
        LatestSituationCommand = EditCommand(ShowLatestSituation);
        EffectiveDetailsCommand = GuardedCommands.Create(Guard, ShowEffectiveDetails);
        ExportCsvCommand = GuardedCommands.Create(Guard, ExportCsv);
        DetachCommand = GuardedCommands.Create(Guard, Detach);
        CreateResponseCommands();
    }

    private void NotifyCommands()
    {
        foreach (var command in editCommands) command.NotifyCanExecuteChanged();
        foreach (var card in phases) { card.MoveUpCommand.NotifyCanExecuteChanged(); card.MoveDownCommand.NotifyCanExecuteChanged(); card.DeleteCommand.NotifyCanExecuteChanged(); }
        NotifyResponseCommands();
    }

    // ---------------------------------------------------------------- forms

    /// <summary>The forms of the sheet: definition, accessories, options, phases and homogenizations, slab properties, curves.</summary>
    private IEnumerable<FormViewModel> AllForms() => forms.Values
        .Concat(phases.SelectMany(c => c.Homogenization is null ? new[] { c.Form } : [c.Form, c.Homogenization]))
        .Concat(slabPropertiesForm is null ? [] : [slabPropertiesForm]).Concat(curveForm is null ? [] : [curveForm]);

    /// <summary>
    /// A form of the sheet (the Form of the historic view): compact, wide choices, the raw text into the document; after a write the handler,
    /// then the change of the data, then what the view queued on its dispatcher.
    /// </summary>
    private FormViewModel Form(JsonObject values, IEnumerable<FieldSpec> fields, Action<string>? handler = null) =>
        Configure(new FormViewModel(values, fields, key => { handler?.Invoke(key); UserChanged(); RunDeferred(); }, compact: true, wideChoices: true));

    /// <summary>What every form of the sheet shares: the Wiki of the services and of the module, read-only with the sheet, the presentation of the numbers.</summary>
    private FormViewModel Configure(FormViewModel form)
    {
        form.WikiNavigator = Context.Services.Wiki; form.InheritedWikiModule = ModuleKey;
        form.IsReadOnly = !CanEdit; form.CommitOnFocusLoss = true; form.FormatNumbers = true;
        return form;
    }

    private void Named(string name, IEnumerable<FieldSpec> fields, Action<string>? handler = null) => forms[name] = Form(Data, fields, handler);

    /// <summary>Shows the fields of a form by the data (<see cref="BridgeInputPresentation.FieldShown"/>).</summary>
    private void ShowFields(string name)
    {
        if (!forms.TryGetValue(name, out var form)) return;
        foreach (var field in form.Fields) form.ShowField(field.Key, BridgeInputPresentation.FieldShown(name, field.Key, Data));
    }

    /// <summary>BuildInputs of the historic view: the forms in the order of the view, the options of the calculation, then the phases.</summary>
    private void BuildInputs()
    {
        Named("normativa", CompositeSectionFields.Standard, key =>
        {
            if (key != "normativa") return;
            BridgeEditor.ApplyStandardDefaults(Data);
            deferred.Add(RebuildPhases);
            deferred.Add(() =>
            {
                foreach (var form in StaticAndPhaseForms().ToArray())
                    foreach (string coefficient in BridgeInputPresentation.StandardCoefficients) form.Set(coefficient, Data.S(coefficient), true);
            });
        });
        Named("coefficienti", CompositeSectionFields.Coefficients);
        Named("materiali", CompositeSectionFields.Materials, _ => ShowFields("materiali"));
        ShowFields("materiali");
        Named("soletta", CompositeSectionFields.Slab);
        Named("predalle", CompositeSectionFields.Predalle);
        RefreshSlabLayout();
        Named("carpenteria", CompositeSectionFields.Steel, _ => RefreshSteelHeight());
        steelHeightBuilt = true;
        Named("sezione", CompositeSectionFields.SectionType, key => { ShowSectionFields(); RefreshSteelHeight(); if (key == "sezione") deferred.Add(RebuildPhases); });
        Named("piastra", CompositeSectionFields.Plate, _ => { ShowFields("piastra"); RefreshSteelHeight(); });
        ShowFields("piastra");
        ShowSectionFields();
        foreach (var (side, name) in new[] { ("top", "armatura-sup"), ("bottom", "armatura-inf") })
        {
            Named(name, CompositeSectionFields.Bars(side), _ => ShowFields(name));
            ShowFields(name);
        }
        // BuildAccessories: transverse reinforcement and fatigue first, then studs, stiffeners, support and box girder.
        Named("trasversale", CompositeSectionFields.Transverse, _ => ShowFields("trasversale"));
        ShowFields("trasversale");
        Named("fatica", CompositeSectionFields.Fatigue, _ => ShowFields("fatica"));
        ShowFields("fatica");
        Named("pioli", CompositeSectionFields.Studs, _ => { UpdateStuds(); deferred.Add(RebuildPhases); });
        UpdateStuds();
        Named("irrigidimenti", CompositeSectionFields.Stiffeners, _ => ShowFields("irrigidimenti"));
        ShowFields("irrigidimenti");
        Named("appoggio", CompositeSectionFields.Support, _ => ShowFields("appoggio"));
        ShowFields("appoggio");
        Named("cassoncino", CompositeSectionFields.Box, key => { ShowFields("cassoncino"); if (key == "torsione_cassoncino") deferred.Add(RebuildPhases); });
        ShowFields("cassoncino");
        BoxShown = BridgeInputPresentation.IsBox(Data); boxBuilt = true;
        ShowSectionFields();
        // The page of the phases: the options of the calculation, the limits, the phases.
        BridgeSheetPresentation.PrepareCalculationOptions(Data);
        Named("metodo", CompositeSectionFields.Method, _ => { RefreshCalculationOptions(); deferred.Add(RebuildPhases); });
        Named("discretizzazione", CompositeSectionFields.Mesh);
        meshBuilt = true;
        RefreshCalculationOptions();
        Named("limiti", CompositeSectionFields.Limits);
        RebuildPhases();
    }

    /// <summary>The inputs before an opening (Reload): as the controls of a new historic view.</summary>
    private void ResetInputs()
    {
        forms.Clear(); homogenizations.Clear(); Phases = [];
        steelHeightBuilt = boxBuilt = meshBuilt = false;
        plateShown = boxShown = transverseShown = fatigueShown = meshShown = true;
        materialText = steelHeightText = slabLayoutText = methodNotice = loadConvention = phaseCountText = "";
        ActionRows.Clear(); selectedActionRow = null; actionStyleKey = ""; torqueColumnShown = true;
        inactiveKinds = new Dictionary<string, IReadOnlySet<string>>();
        previewGeometry = null; previewInput = null; geometryError = "";
    }

    /// <summary>The forms of the historic list of the inputs (static forms, then the forms of the phases), for the coefficients of a new standard.</summary>
    private IEnumerable<FormViewModel> StaticAndPhaseForms() => forms.Values.Concat(phases.SelectMany(c => c.Homogenization is null ? new[] { c.Form } : [c.Form, c.Homogenization]));

    /// <summary>The type of section: the fields of the inclined web and of the box, the second plate of the H, the group of the box.</summary>
    private void ShowSectionFields()
    {
        ShowFields("sezione");
        PlateShown = BridgeInputPresentation.IsH(Data);
        if (boxBuilt) BoxShown = BridgeInputPresentation.IsBox(Data);
    }

    private void UpdateStuds()
    {
        TransverseShown = FatigueShown = Data.B("pioli");
        ShowFields("pioli");
    }

    private void RefreshCalculationOptions()
    {
        bool nonlinear = BridgeEditor.IsNonlinear(Data);
        ShowFields("metodo");
        if (meshBuilt) MeshShown = BridgeEditor.IsHistory(Data);
        if (forms.TryGetValue("limiti", out var limits)) foreach (string key in BridgeInputPresentation.StabilityKeys) limits.Enable(key, !nonlinear, dim: true);
        MethodNotice = BridgeInputPresentation.MethodNotice(Data);
    }

    private void UpdateCommonReferenceField()
    {
        bool used = BridgeInputPresentation.CommonReferenceUsed(Data);
        if (forms.TryGetValue("limiti", out var limits)) limits.Enable("y_ref", used, dim: true);
    }

    private void RefreshSteelHeight()
    {
        if (!steelHeightBuilt) return;
        try { SteelHeightText = BridgePreview.UpdateSteelHeightDescription(Data); }
        catch (ArgumentException error) { SteelHeightText = error.Message; }
    }

    private void RefreshSlabLayout()
    {
        ShowFields("predalle");
        try { SlabLayoutText = BridgePreview.SlabDescription(Data); }
        catch (ArgumentException error) { SlabLayoutText = error.Message; }
    }

    /// <summary>RefreshPreview of the historic view: the texts of the inputs, the preview of the geometry, the properties, the sketch, the drawing.</summary>
    private void RefreshPreview()
    {
        RefreshSteelHeight();
        RefreshSlabLayout();
        try
        {
            var preview = BridgePreview.Read(Data);
            previewGeometry = preview.Geometry; previewInput = preview.Input;
            geometryError = preview.GeometryError; MaterialText = preview.MaterialText;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { geometryError = error.Message; MaterialText = error.Message; }
        RefreshSectionProperties();
        RefreshDetailSketch();
        RefreshDrawing();
    }

    // ---------------------------------------------------------------- phases

    /// <summary>BuildPhases of the historic view: the cards of the phases built again, with the homogenization, the table and the properties.</summary>
    private void RebuildPhases()
    {
        homogenizations.Clear();
        var cards = new List<CompositeSectionPhaseCard>();
        int index = 0;
        foreach (JsonObject p in Data.Array("fasi").OfType<JsonObject>().ToArray())
        {
            BridgeSheetPresentation.PreparePhase(p);
            int i = index++;
            var form = Form(p, CompositeSectionFields.Phase, key => { if (key == "tipo") { BridgeSheetPresentation.PrepareKind(p); deferred.Add(RebuildPhases); } });
            foreach (var field in form.Fields) form.ShowField(field.Key, BridgeInputPresentation.PhaseFieldShown(field.Key, Data, p));
            var notes = new List<string>();
            if (p.S("tipo") == BridgeEditor.ShrinkageKind) notes.Add(BridgeInputPresentation.ShrinkageNote);
            FormViewModel? homogenization = null;
            if (BridgeSheetPresentation.PhaseHomogenizationNeeded(Data, p.S("tipo")))
                homogenization = Form(p, CompositeSectionFields.Homogenization, key => BridgeSheetPresentation.HomogenizationEdited(p, key));
            else notes.Add(BridgeInputPresentation.NoHomogenizationNote(p));
            var card = new CompositeSectionPhaseCard(i, p, BridgeInputPresentation.PhaseHeader(i, p), form, homogenization, notes,
                CardCommand(() => MovePhase(i, -1)), CardCommand(() => MovePhase(i, 1)), CardCommand(() => DeletePhase(p)));
            if (homogenization is not null) homogenizations.Add((p, homogenization, card));
            cards.Add(card);
        }
        Phases = cards;
        SynchronizeHomogenization();
        UpdateCommonReferenceField();
        RefreshActionsTable(); RefreshSectionProperties();
        RefreshCalculationOptions();
    }

    private IRelayCommand CardCommand(Action action) => GuardedCommands.Create(Guard, action, () => CanEdit);

    /// <summary>n or φ of every homogenized phase derived again from the other (written into the phase) and the text n₀ · φeff.</summary>
    private void SynchronizeHomogenization()
    {
        foreach (var (phase, form, card) in homogenizations)
        {
            // The existing archive key retains the last edited quantity, including in old files.
            string derived = phase.S("modo") == "Da n" ? "phi" : "n";
            try
            {
                var h = BridgeSheetPresentation.UpdateHomogenization(Data, phase);
                form.Set(derived, phase.S(derived), display: true);
                card.HomogenizationInfo = BridgeInputPresentation.HomogenizationInfo(h);
            }
            catch (ArgumentException error)
            {
                form.Set(derived, "—", display: true);
                card.HomogenizationInfo = error.Message;
            }
        }
    }

    /// <summary>«↑» / «↓» of a card: the phase moved (nothing at the ends), the cards rebuilt, the change of the data.</summary>
    private void MovePhase(int i, int delta)
    {
        if (!CanEdit) return;
        var list = Data.Array("fasi"); int target = i + delta; if (target < 0 || target >= list.Count) return;
        var phase = list[i]!; list.RemoveAt(i); list.Insert(target, phase); RebuildPhases(); UserChanged();
    }

    /// <summary>«Elimina» of a card: the phase removed (never the last one).</summary>
    private void DeletePhase(JsonObject phase)
    {
        if (!CanEdit || Data.Array("fasi").Count <= 1) return;
        Data.Array("fasi").Remove(phase); RebuildPhases(); UserChanged();
    }

    /// <summary>
    /// «+ Aggiungi fase», «+ Carico», «+ Ritiro»: a new phase, followed by the results, and its row selected; refused beyond the phases of
    /// the cumulative method (the status line says why).
    /// </summary>
    public void AddPhase(bool shrinkage)
    {
        if (!CanEdit) return;
        if (!BridgeInputPresentation.CanAddPhase(Data)) { Status = BridgeInputPresentation.PhaseLimitReached; return; }
        var phase = BridgeInputPresentation.NewPhase(shrinkage);
        Data.Array("fasi").Add(phase); selectedPhase = phase; followLatestStage = true;
        RebuildPhases(); UserChanged(); SelectedActionIndex = ActionRows.Count - 1;
    }

    private void MoveSelected(int delta)
    {
        if (!CanEdit || selectedActionRow is not { } row) return;
        int index = Data.Array("fasi").IndexOf(row.Values); MovePhase(index, delta);
        SelectedActionIndex = Math.Clamp(index + delta, 0, ActionRows.Count - 1);
    }

    private void DeleteSelected()
    {
        if (!CanEdit || selectedActionRow is not { } row || Data.Array("fasi").Count <= 1) return;
        Data.Array("fasi").Remove(row.Values); RebuildPhases(); UserChanged();
    }

    /// <summary>The cells configured again when what they depend on changed (ConfigureActionCells): the column ΔT, the cells that do not apply.</summary>
    private void ConfigureActionCells()
    {
        string key = BridgeInputPresentation.CellStyleKey(Data);
        if (actionStyleKey == key) return;
        actionStyleKey = key;
        if (torqueColumnShown != BridgeInputPresentation.TorqueColumnShown(Data)) { torqueColumnShown = !torqueColumnShown; OnPropertyChanged(nameof(TorqueColumnShown)); }
        inactiveKinds = CompositeSectionFields.Actions.ToDictionary(c => c.Key,
            c => (IReadOnlySet<string>)BridgeEditor.PhaseKinds.Where(kind => BridgeInputPresentation.CellInactive(Data, kind, c.Key)).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var row in ActionRows) row.Refresh();
    }

    /// <summary>The kinds of phase whose cells of the column <paramref name="key"/> do not apply, as the cells were last configured.</summary>
    public IReadOnlySet<string> InactiveKinds(string key) => inactiveKinds.TryGetValue(key, out var kinds) ? kinds : new HashSet<string>();

    /// <summary>The rows of the table reconciled with the phases in place (the selection follows its phase), the count and the convention.</summary>
    private void RefreshActionsTable()
    {
        if (refreshingActions) return;
        refreshingActions = true;
        try
        {
            ConfigureActionCells();
            var list = Data.Array("fasi").OfType<JsonObject>().ToArray();
            if (!ActionRows.Select(r => r.Values).SequenceEqual(list))
            {
                var selected = selectedActionRow?.Values;
                for (int i = 0; i < list.Length; i++)
                {
                    var phase = list[i];
                    int old = ActionRows.ToList().FindIndex(r => ReferenceEquals(r.Values, phase));
                    if (old >= 0) { if (old != i) ActionRows.Move(old, i); continue; }
                    ActionRows.Insert(i, new JsonRowViewModel(phase, key =>
                    {
                        if (refreshingActions) return;
                        BridgeSheetPresentation.CellEdited(phase, key);
                        UserChanged();
                        // The cards are rebuilt after the grid has committed the cell.
                        deferred.Add(RebuildPhases); RunDeferred();
                    }));
                }
                while (ActionRows.Count > list.Length) ActionRows.RemoveAt(ActionRows.Count - 1);
                selectedActionRow = ActionRows.FirstOrDefault(r => ReferenceEquals(r.Values, selected));
                OnPropertyChanged(nameof(SelectedActionIndex));
            }
            else foreach (var row in ActionRows) row.Refresh();
            PhaseCountText = BridgeInputPresentation.PhaseCount(Data);
            LoadConvention = BridgeInputPresentation.LoadConvention(Data);
        }
        finally { refreshingActions = false; }
    }
}
