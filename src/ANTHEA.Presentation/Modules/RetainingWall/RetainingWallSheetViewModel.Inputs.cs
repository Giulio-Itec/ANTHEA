using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Walls;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>The inputs of the wall sheet: forms, tables, cards and their rules, as BuildInputs and UpdateFields of the historic view.</summary>
public sealed partial class RetainingWallSheetViewModel
{
    private readonly Dictionary<string, bool> formVisibility = new(StringComparer.Ordinal);
    private WallTableViewModel valleyLayers = null!, mountainLayers = null!, serviceLayers = null!, actions = null!;
    private JsonObject? selectedAction;
    private string layerStatus = "", coverStatus = "", seismicAudit = "";
    private bool layerStatusValid = true, coverStatusError, seismicAuditError;
    private string seismicButton = "", seismicTip = "";
    private bool seismicWarning;
    private IReadOnlyList<RetainingWallHistory> histories = [];
    private IReadOnlyList<WallPanelBlock>? proposalPanel;
    private WallModel.ReinforcementProposal? proposal;
    private int proposalRequest;

    // ---------------------------------------------------------------- state of the inputs

    /// <summary>The valley column of the soil.</summary>
    public WallTableViewModel ValleyLayers => valleyLayers;

    /// <summary>The uphill column of the soil.</summary>
    public WallTableViewModel MountainLayers => mountainLayers;

    /// <summary>The deformable layers under the foundation.</summary>
    public WallTableViewModel ServiceLayers => serviceLayers;

    /// <summary>The actions.</summary>
    public WallTableViewModel Actions => actions;

    /// <summary>The depths reached by the two soil columns, and whether they reach the foundation (red otherwise).</summary>
    public string LayerStatus { get => layerStatus; private set => SetProperty(ref layerStatus, value); }
    public bool LayerStatusValid { get => layerStatusValid; private set => SetProperty(ref layerStatusValid, value); }

    /// <summary>The minimum cover against the cover adopted, or the error of the materials.</summary>
    public string CoverStatus { get => coverStatus; private set => SetProperty(ref coverStatus, value); }

    /// <summary>The seismic coefficients of the site, the text of the manual mode or of the exclusion, or the error (red).</summary>
    public string SeismicAudit { get => seismicAudit; private set => SetProperty(ref seismicAudit, value); }
    public bool SeismicAuditError { get => seismicAuditError; private set => SetProperty(ref seismicAuditError, value); }

    /// <summary>The «Sisma» button of the header: text, warning colour and tooltip.</summary>
    public string SeismicButton { get => seismicButton; private set => SetProperty(ref seismicButton, value); }
    public bool SeismicWarning { get => seismicWarning; private set => SetProperty(ref seismicWarning, value); }
    public string SeismicToolTip { get => seismicTip; private set => SetProperty(ref seismicTip, value); }

    /// <summary>«Calcola armature» as the view enables it (cantilever, not busy at the last refresh of the rules).</summary>
    public bool DesignEnabled { get => designEnabled; private set { if (SetProperty(ref designEnabled, value)) OnPropertyChanged(nameof(CanDesign)); } }

    /// <summary>«Calcola armature» usable: enabled and the sheet editable.</summary>
    public bool CanDesign => designEnabled && CanEdit;

    /// <summary>The accelerograms (Newmark): name, samples and the key of their form.</summary>
    public IReadOnlyList<RetainingWallHistory> Histories { get => histories; private set => SetProperty(ref histories, value); }

    /// <summary>The proposal of the search of the reinforcement, or the request to search again; null without a proposal.</summary>
    public IReadOnlyList<WallPanelBlock>? ProposalPanel { get => proposalPanel; private set => SetProperty(ref proposalPanel, value); }

    /// <summary>Visible form: its own visibility and the card that holds it (the cards of the reinforcement, the valley column of the global stability).</summary>
    public bool IsFormShown(string key)
    {
        if (!formVisibility.GetValueOrDefault(key, true)) return false;
        if (key.StartsWith("rebar_", StringComparison.Ordinal)) return cards.TryGetValue(key, out var card) && card.IsVisible;
        if (key == "global_soil_valley") return cards.TryGetValue("Strati globali valle", out var valley) && valley.IsVisible;
        return true;
    }

    // ---------------------------------------------------------------- building

    private FormViewModel Form(string key, IEnumerable<FieldSpec> fields, JsonObject? values = null, Action<string>? handler = null)
    {
        var form = new FormViewModel(values ?? Data[key]!.AsObject(), fields, k => { handler?.Invoke(k); UpdateFields(); UserChanged(); RebuildAction(); }, compact: true,
            symbolColumns: true) { IsReadOnly = !CanEdit, FormatNumbers = true, WikiNavigator = Context.Services.Wiki, InheritedWikiModule = ModuleKey };
        forms[key] = form;
        return form;
    }

    private WallCardViewModel Card(string key, bool expanded) => cards[key] = new WallCardViewModel(key, expanded);

    /// <summary>A table of the inputs on <paramref name="array"/>: an edit runs <paramref name="edit"/>, then invalidates the results (only Modified for «visible»).</summary>
    private WallTableViewModel Table(string key, IReadOnlyList<FieldSpec> columns, JsonArray array, Action<string>? edit = null, Action<int>? selected = null)
    {
        var table = new WallTableViewModel(key, columns, selected);
        foreach (var obj in array.OfType<JsonObject>()) table.Rows.Add(Row(obj, edit));
        return table;
    }

    private JsonRowViewModel Row(JsonObject obj, Action<string>? edit) => new(obj, key =>
    {
        edit?.Invoke(key);
        if (key != "visible") UserChanged(); else { NotifyDrawings(); ChangedKeepingResults(); }
    });

    /// <summary>AddRow of the historic view: the object at the end of the array and of the table, then selected.</summary>
    private void AddRow(WallTableViewModel table, JsonArray array, JsonObject obj, Action<string>? edit = null)
    {
        array.Add(obj); table.Rows.Add(Row(obj, edit)); table.SelectedIndex = table.Rows.Count - 1;
    }

    private void BuildInputs()
    {
        cards.TryGetValue("Combinazioni", out var matrixCard);
        forms.Clear(); cards.Clear(); formVisibility.Clear();
        cards["Combinazioni"] = matrixCard ?? new WallCardViewModel("Combinazioni", true);
        // The choice of the type of a new action is built with the inputs: back to the first type, as the historic combo box.
        NewActionType = WallModel.ActionTypes[0];
        BuildSoilColumns();
        BuildAdvancedSoil();
        Card("Terreno", true);
        BuildWallMaterials();
        Card("Materiali", true);
        Form("zones", RetainingWallFields.Zones, Data["reinforcement"]!.AsObject());
        foreach (var zone in RetainingWallFields.RebarZones)
        {
            Form("rebar_" + zone.Key, RetainingWallFields.Rebar, Data["reinforcement"]![zone.Key]!.AsObject());
            Card("rebar_" + zone.Key, false);
        }
        Form("geometry", RetainingWallFields.Geometry);
        BuildDetailing();
        Card("Geometria", true);
        actions = Table("azioni", RetainingWallFields.ActionColumns, Data.Array("actions"), selected: _ => EditAction());
        OnPropertyChanged(nameof(Actions));
        BuildSeismicCard();
        BuildHistories();
        Card("Azioni", true);
        UpdateColumnDepths();
        actions.SelectedIndex = actions.Rows.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(Forms)); OnPropertyChanged(nameof(Cards));
    }

    // ---------------------------------------------------------------- soils

    private void BuildSoilColumns()
    {
        WallModel.CompleteSoilInput(Data);
        var valley = Data["valley"]!.AsObject();
        Form("valley", RetainingWallFields.Valley, valley, key => { if (key == "linked" && valley.B("linked")) SyncSoils(false); });
        valleyLayers = Table("valle", RetainingWallFields.SoilColumns, valley.Array("layers"), _ => { SyncSoils(true); UpdateColumnDepths(); }, row => LayerSelected(row, true));
        mountainLayers = Table("monte", RetainingWallFields.SoilColumns, Data.Array("layers"), _ => { SyncSoils(false); UpdateColumnDepths(); }, row => LayerSelected(row, false));
        OnPropertyChanged(nameof(ValleyLayers)); OnPropertyChanged(nameof(MountainLayers));
        Form("interfaces", RetainingWallFields.Interfaces);
        Form("water", RetainingWallFields.Water);
        Form("foundation", RetainingWallFields.Foundation);
        BuildGlobalCard();
        Card("Stabilità globale", false);
    }

    private void LayerSelected(int row, bool valley) { selectedLayer = row; selectedValley = valley; NotifyDrawings(); }

    /// <summary>With the columns linked, the edited column is copied into the other one (its rows rebuilt).</summary>
    private bool syncingSoils;
    private void SyncSoils(bool fromValley)
    {
        if (syncingSoils || !Data["valley"].B("linked")) return;
        syncingSoils = true;
        try
        {
            var source = fromValley ? Data["valley"]!.Array("layers") : Data.Array("layers");
            var target = fromValley ? Data.Array("layers") : Data["valley"]!.Array("layers");
            var table = fromValley ? mountainLayers : valleyLayers;
            target.Clear(); table.Clear();
            foreach (var l in source)
            {
                var copy = (JsonObject)l!.DeepClone(); target.Add(copy);
                table.Rows.Add(new JsonRowViewModel(copy, _ => { SyncSoils(!fromValley); UpdateColumnDepths(); UserChanged(); }));
            }
        }
        finally { syncingSoils = false; }
    }

    /// <summary>«+ Strato» of a soil column (at most 50 layers): a layer 1 m thick, the columns synchronized.</summary>
    public void AddSoilLayer(bool valley)
    {
        if (!CanEdit) return;
        var table = valley ? valleyLayers : mountainLayers; var array = valley ? Data["valley"]!.Array("layers") : Data.Array("layers"); if (array.Count >= 50) return;
        var layer = WallModel.Layer(); layer["thickness"] = 1;
        AddRow(table, array, layer, _ => { SyncSoils(valley); UpdateColumnDepths(); }); SyncSoils(valley); UpdateColumnDepths(); UserChanged();
    }

    /// <summary>«−» of a soil column: the selected layer, never the last one.</summary>
    public void RemoveSoilLayer(bool valley)
    {
        if (!CanEdit) return;
        var table = valley ? valleyLayers : mountainLayers; int index = table.SelectedIndex;
        if (table.Rows.Count <= 1 || index < 0) return;
        (valley ? Data["valley"]!.Array("layers") : Data.Array("layers")).Remove(table.Rows[index].Values); table.RemoveAt(index); SyncSoils(valley); UpdateColumnDepths(); UserChanged();
    }

    /// <summary>«↑» (−1) or «↓» (+1) of a soil column: the selected layer moves and stays selected.</summary>
    public void MoveSoilLayer(bool valley, int step)
    {
        if (!CanEdit) return;
        var table = valley ? valleyLayers : mountainLayers; int i = table.SelectedIndex, j = i + step; if (i < 0 || j < 0 || j >= table.Rows.Count) return;
        var array = valley ? Data["valley"]!.Array("layers") : Data.Array("layers"); var node = array[i]; array.RemoveAt(i); array.Insert(j, node); table.Move(i, j); table.SelectedIndex = j;
        SyncSoils(valley); UpdateColumnDepths(); UserChanged();
    }

    private void UpdateColumnDepths()
    {
        if (valleyLayers is null || mountainLayers is null) return;
        var described = WallSheetPresentation.DescribeColumnDepths(Data, valleyLayers.Rows.Select(r => r.Values), mountainLayers.Rows.Select(r => r.Values));
        foreach (var table in new[] { valleyLayers, mountainLayers }) foreach (var row in table.Rows) row.Refresh();
        LayerStatus = described.Text; LayerStatusValid = described.Valid;
    }

    private void BuildAdvancedSoil()
    {
        var layers = Data["serviceability"]!.Array("layers");
        serviceLayers = Table("cedimenti", RetainingWallFields.ServiceLayerColumns, layers);
        OnPropertyChanged(nameof(ServiceLayers));
        Form("bearing_seismic", RetainingWallFields.BearingSeismic);
        Form("serviceability", RetainingWallFields.Serviceability);
    }

    /// <summary>«+ Strato» of the deformable layers.</summary>
    public void AddServiceLayer()
    {
        if (!CanEdit) return;
        AddRow(serviceLayers, Data["serviceability"]!.Array("layers"), RetainingWallPresentation.NewServiceLayer());
    }

    /// <summary>«− Strato» of the deformable layers: the selected one.</summary>
    public void RemoveServiceLayer()
    {
        if (!CanEdit || serviceLayers.SelectedIndex < 0) return;
        int index = serviceLayers.SelectedIndex;
        Data["serviceability"]!.Array("layers").Remove(serviceLayers.Rows[index].Values); serviceLayers.RemoveAt(index); UserChanged();
    }

    // ---------------------------------------------------------------- materials

    private void BuildWallMaterials()
    {
        var m = Data["materials"]!.AsObject();
        var form = Form("materials", RetainingWallFields.Materials(m), handler: key =>
        {
            if (key is "classe_cls" or "classe_acciaio")
            {
                RetainingWallPresentation.ApplyMaterialChoice(Data, key);
                foreach (var field in forms["materials"].Fields.Where(f => !f.Key.StartsWith("__", StringComparison.Ordinal))) forms["materials"].Set(field.Key, m.S(field.Key), true);
            }
        });
        foreach (var group in RetainingWallFields.MaterialGroups) form.GroupFields(group.Title, [.. group.Keys], group.Expanded);
        Form("material_cover", RetainingWallFields.Cover, Data["detailing"]!.AsObject());
        Form("gravity_design", RetainingWallFields.GravityDesign);
    }

    /// <summary>«Adotta copriferro minimo»: the minimum cover of the durability written into the cover adopted.</summary>
    public void AdoptMinimumCover()
    {
        if (!CanEdit) return;
        try
        {
            Commit(); var m = Data["materials"]!; m["cover"] = WallModel.RequiredCover(Data); forms["materials"].Set("cover", m.S("cover"), true); UpdateFields(); UserChanged();
        }
        catch (ArgumentException error) { CoverStatus = error.Message; }
    }

    private void UpdateWallMaterials()
    {
        var form = forms["materials"];
        try
        {
            var preview = WallMaterialPreview.Read(Data);
            foreach (var (key, value) in preview.Fields) form.Set(key, value, true);
            CoverStatus = preview.CoverDescription; CoverStatusError = false;
        }
        catch (ArgumentException error) { foreach (string key in new[] { "__fcd", "__fyd", "__ecm" }) form.Set(key, "—", true); CoverStatus = error.Message; CoverStatusError = true; }
    }

    /// <summary>The cover status shows an error of the materials (the colour of the view).</summary>
    public bool CoverStatusError { get => coverStatusError; private set => SetProperty(ref coverStatusError, value); }

    // ---------------------------------------------------------------- reinforcement details and search

    private void BuildDetailing()
    {
        ProposalPanel = null; proposal = null;
        Form("bar_schedule", RetainingWallFields.BarSchedule);
        Form("detailing", RetainingWallFields.Detailing);
    }

    /// <summary>«Calcola armature»: the search of symmetric bars for each zone, checked with GPC; refused while busy or when read-only.</summary>
    public Task DesignRebarAsync()
    {
        if (!CanDesign) return Task.CompletedTask;
        // As the historic click: the last work requested becomes this one, also when it does nothing because an engine is running.
        if (IsBusy || IsDisposed) { Track(Task.CompletedTask); return Task.CompletedTask; }
        Commit(); int request = revision;
        var snapshot = (JsonObject)Data.DeepClone();
        var task = RunSecondaryAsync(token => StructuralQueries.WallReinforcement(snapshot, token), () => { DesignEnabled = false; Status = RetainingWallPresentation.DesignRunning; },
            () => { if (!IsDisposed) DesignEnabled = true; },
            result => { proposal = result; proposalRequest = request; ProposalPanel = ProposalBlocks(result); Status = result.Message; },
            error => Status = RetainingWallPresentation.DesignError(error.Message));
        Track(task);
        return task;
    }

    private static IReadOnlyList<WallPanelBlock> ProposalBlocks(WallModel.ReinforcementProposal result) => [
        WallPanelBlock.Of(result.Message, WallTextStyle.NormalBold),
        WallPanelBlock.Table(RetainingWallPresentation.ProposalColumns, RetainingWallPresentation.ProposalRows(result), 140),
        WallPanelBlock.Table(RetainingWallPresentation.CheckColumns, RetainingWallPresentation.CheckRows(WallExports.Envelope(result.Checks)), 200),
        WallPanelBlock.Button("Applica proposta", ApplyProposalCommand)];

    /// <summary>The command of the button of the proposal.</summary>
    public const string ApplyProposalCommand = "applica-proposta";

    /// <summary>«Applica proposta»: the bars and details of the proposal replace those of the sheet, unless the data changed since the search.</summary>
    public void ApplyProposal()
    {
        if (!CanEdit || proposal is null || proposalPanel is null) return;
        if (proposalRequest != revision) { proposal = null; ProposalPanel = [WallPanelBlock.Of(RetainingWallPresentation.DesignStale, WallTextStyle.Normal)]; return; }
        Data["reinforcement"] = proposal.Input["reinforcement"]!.DeepClone(); Data["detailing"] = proposal.Input["detailing"]!.DeepClone();
        BuildInputs(); UpdateFields(); UserChanged(); SelectedView = "Armature"; SelectedFilter = "Dettagli armature";
    }

    /// <summary>«Interrompi ricerca»: the work requested (the search, or the calculation that replaced it) is cancelled; the bars entered stay.</summary>
    public void InterruptSearch()
    {
        if (!CanEdit) return;
        secondary.Invalidate(); CancelCalculation(restorePublished: true); Status = RetainingWallPresentation.DesignInterrupted;
    }

    // ---------------------------------------------------------------- actions

    private string newActionType = WallModel.ActionTypes[0];

    /// <summary>The type of a new action («+ Azione»): one of <see cref="WallModel.ActionTypes"/>.</summary>
    public string NewActionType
    {
        get => newActionType;
        set { if (value is not null && WallModel.ActionTypes.Contains(value)) SetProperty(ref newActionType, value); }
    }

    /// <summary>The types of a new action.</summary>
    public IReadOnlyList<string> ActionTypes { get; } = WallModel.ActionTypes;

    /// <summary>«+ Azione» (at most 30): a new action of <see cref="NewActionType"/>, selected.</summary>
    public void AddAction()
    {
        if (!CanEdit || actions.Rows.Count >= 30) return;
        var action = WallModel.NewAction(Data, NewActionType); AddRow(actions, Data.Array("actions"), action); UserChanged();
    }

    /// <summary>«− Azione»: the selected action; the last one is then selected.</summary>
    public void RemoveAction()
    {
        if (!CanEdit || actions.SelectedIndex < 0) return;
        int index = actions.SelectedIndex;
        Data.Array("actions").Remove(actions.Rows[index].Values); actions.RemoveAt(index); actions.SelectedIndex = actions.Rows.Count - 1; UserChanged();
    }

    /// <summary>The selected action and its form; a new type rebuilds the form after the edit, as the view does on the dispatcher.</summary>
    private void EditAction()
    {
        forms.Remove("action"); selectedAction = actions.SelectedIndex >= 0 ? actions.Rows[actions.SelectedIndex].Values : null;
        drawingAction = selectedAction?.S("id"); NotifyDrawings();
        if (selectedAction is null) { OnPropertyChanged(nameof(Forms)); return; }
        var a = selectedAction;
        var form = Form("action", RetainingWallFields.Action(a.S("type")), a, key => { foreach (var row in actions.Rows) row.Refresh(); if (key == "type") rebuildAction = true; });
        foreach (var (key, visible) in RetainingWallPresentation.ActionTypeFields(a.S("type"))) form.ShowField(key, visible);
        foreach (string key in new[] { "psi0", "psi1", "psi2" }) form.ShowField(key, a.S("category") == "Q");
        OnPropertyChanged(nameof(Forms));
    }

    /// <summary>The form of the action is rebuilt after an edit of its type (the BeginInvoke of the historic view, run after the edit).</summary>
    private bool rebuildAction;

    private void RebuildAction()
    {
        if (!rebuildAction || IsDisposed) return;
        rebuildAction = false; EditAction();
    }

    // ---------------------------------------------------------------- seismic action

    private void BuildSeismicCard()
    {
        var form = new FormViewModel(Data["seismic"]!.AsObject(), RetainingWallFields.Seismic, _ => { UpdateFields(); UserChanged(); }, compact: true, wideChoices: true)
            { IsReadOnly = !CanEdit, FormatNumbers = true, WikiNavigator = Context.Services.Wiki, InheritedWikiModule = ModuleKey };
        forms["seismic"] = form;
        Card("Sisma", Data["seismic"].B("enabled"));
    }

    private void UpdateSeismicFields()
    {
        if (!forms.TryGetValue("seismic", out var form)) return;
        var s = Data["seismic"]!; bool enabled = s.B("enabled"), site = s.S("source") == WallModel.SeismicSite;
        SeismicAuditError = false;
        if (!enabled) { SeismicAudit = RetainingWallPresentation.SeismicDisabled; return; }
        if (!site) { SeismicAudit = RetainingWallPresentation.SeismicManualText; return; }
        try
        {
            var result = WallModel.DeriveSeismic(Data)!;
            s["kh"] = result.Kh; s["kv"] = result.Kv;
            form.Set("kh", s.S("kh"), display: true); form.Set("kv", s.S("kv"), display: true);
            SeismicAudit = result.Description;
        }
        catch (ArgumentException error) { SeismicAudit = error.Message; SeismicAuditError = true; }
    }

    private void UpdateSeismicStatus()
    {
        var (content, warning, tip) = RetainingWallPresentation.SeismicStatus(Data, calculationResult);
        SeismicButton = content; SeismicWarning = warning; SeismicToolTip = tip;
    }

    /// <summary>The «Sisma» button: the page of the inputs with the cards of the actions and of the seismic action open.</summary>
    public void ShowSeismic()
    {
        Page = 0; cards["Azioni"].IsExpanded = true; cards["Sisma"].IsExpanded = true;
        CardShown?.Invoke(this, "Sisma");
    }

    /// <summary>A command opened a card for the user («Sisma», «Stabilità globale»): the view scrolls to it, as the historic view.</summary>
    public event EventHandler<string>? CardShown;

    private void ShowCard(string key) => CardShown?.Invoke(this, key);

    // ---------------------------------------------------------------- accelerograms

    private void BuildHistories()
    {
        foreach (string key in forms.Keys.Where(k => k.StartsWith("history_", StringComparison.Ordinal)).ToArray()) forms.Remove(key);
        var list = new List<RetainingWallHistory>();
        foreach (var (record, index) in Data["serviceability"]!.Array("histories").OfType<JsonObject>().Select((r, i) => (r, i)))
        {
            Form("history_" + index, RetainingWallFields.History, record);
            list.Add(new(record.S("name"), RetainingWallPresentation.HistorySamples(record), "history_" + index));
        }
        Histories = list;
    }

    /// <summary>«+ Accelerogramma»: a new accelerogram without samples.</summary>
    public void AddHistory()
    {
        if (!CanEdit) return;
        var list = Data["serviceability"]!.Array("histories"); list.Add(RetainingWallPresentation.NewHistory(list.Count)); BuildHistories(); OnPropertyChanged(nameof(Forms)); UserChanged();
    }

    /// <summary>«Elimina accelerogramma».</summary>
    public void RemoveHistory(int index)
    {
        var list = Data["serviceability"]!.Array("histories");
        if (!CanEdit || index < 0 || index >= list.Count) return;
        list.RemoveAt(index); BuildHistories(); OnPropertyChanged(nameof(Forms)); UserChanged();
    }

    /// <summary>«Importa CSV t; a/g»: the samples of an accelerogram from a file (two columns, seconds and g); an error goes to the status.</summary>
    public void ImportHistory(int index)
    {
        var list = Data["serviceability"]!.Array("histories");
        if (!CanEdit || index < 0 || index >= list.Count) return;
        string? path = Context.Services.Files.OpenFile(new OpenFileRequest([new FileFilter("Accelerogramma CSV", "*.csv", "*.txt")]));
        if (path is null) return;
        try
        {
            var samples = WallSheetPresentation.ParseHistory(File.ReadAllText(path)); list[index]!["samples"] = samples; BuildHistories(); OnPropertyChanged(nameof(Forms)); UserChanged();
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException) { Status = "Accelerogramma non importato: " + error.Message; }
    }

    // ---------------------------------------------------------------- rules

    /// <summary>UpdateFields of the historic view: the rules of the fields, the values calculated from the materials, the seismic
    /// coefficients of the site, the global stability, the «Sisma» button and the depths of the soil columns.</summary>
    private void UpdateFields()
    {
        if (!forms.ContainsKey("materials")) return;
        ApplyRules(RetainingWallPresentation.InputRules(Data, forms.ContainsKey("action") ? selectedAction?.S("category") : null));
        UpdateWallMaterials();
        DesignEnabled = Data.S("family") == "cantilever" && !IsBusy;
        UpdateSeismicFields();
        RefreshGlobal();
        UpdateSeismicStatus();
        UpdateColumnDepths();
    }

    private void ApplyRules(WallInputRules rules)
    {
        foreach (var (key, visible) in rules.Forms) if (forms.ContainsKey(key)) formVisibility[key] = visible;
        foreach (var (key, visible) in rules.Cards) if (cards.TryGetValue(key, out var card)) card.IsVisible = visible;
        foreach (var (key, field, visible) in rules.Fields) if (forms.TryGetValue(key, out var form)) form.ShowField(field, visible);
        foreach (var (key, field, enabled, dim) in rules.Enabled) if (forms.TryGetValue(key, out var form)) form.Enable(field, enabled, dim);
        if (formVisibility.Count != shownForms.Count || formVisibility.Any(p => !shownForms.TryGetValue(p.Key, out bool shown) || shown != p.Value))
        {
            shownForms = new Dictionary<string, bool>(formVisibility, StringComparer.Ordinal);
            OnPropertyChanged(nameof(FormVisibility));
        }
    }

    private IReadOnlyDictionary<string, bool> shownForms = new Dictionary<string, bool>(StringComparer.Ordinal);

    /// <summary>
    /// The forms that the rules of the fields show or hide (the InputForm collapsed by ApplyRules of the historic view), by key; a form not
    /// listed is shown. A new copy, notified, every time it changes (for the view; the cards that hold a form are in <see cref="Cards"/>).
    /// </summary>
    public IReadOnlyDictionary<string, bool> FormVisibility => shownForms;
}

/// <summary>An accelerogram of the serviceability: its name (the title of its group), the samples saved and the key of its form.</summary>
public sealed record RetainingWallHistory(string Name, string Samples, string FormKey);
