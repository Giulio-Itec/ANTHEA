using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Walls;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>The global stability of the wall sheet: the card of the inputs, the independent calculation and its results.</summary>
public sealed partial class RetainingWallSheetViewModel
{
    /// <summary>The tables of the points of the profile by the name of the trace and their array in global_stability.</summary>
    private static readonly IReadOnlyList<(string Table, string Array)> PointTables = [("punti-valle", "valley"), ("punti-monte", "uphill"), ("punti-falda", "water")];

    private readonly Dictionary<string, WallTableViewModel> points = new(StringComparer.Ordinal);
    private WallTableViewModel globalRear = null!, globalValley = null!;
    private WallTableViewModel? globalMatrix;
    private SlopeResult? independentGlobal;
    private JsonObject? independentInput;
    private string? independentError;
    private IReadOnlyList<string>? globalCombinations;
    private string? selectedGlobalCombination;
    private string globalMessage = "", globalReadiness = "", globalReadinessTip = "", globalSearchSummary = "", globalSetupText = "";
    private bool globalReady, globalFullProfile;
    private int globalSlice = -1, previewSoil = -1;
    private string previewColumn = "monte";
    private SlopeResult? globalDrawingResult, previewResult;
    private SlopeCaseResult? globalDrawingCase, previewCase;

    /// <summary>The result of the global stability shown: the independent calculation, otherwise that of the calculation of the wall.</summary>
    public SlopeResult? GlobalResult => independentGlobal ?? calculationResult?.GlobalStability;

    /// <summary>The combination of the global stability shown.</summary>
    public SlopeCaseResult? GlobalCase => GlobalResult?.Cases.FirstOrDefault(c => c.Factors.Name == selectedGlobalCombination);

    /// <summary>The global result comes from «Calcola globale».</summary>
    public bool HasIndependentGlobal => independentGlobal is not null;

    /// <summary>The deep layers of the profile (or of the uphill column) and of the valley column: thickness and bottom on display copies.</summary>
    public WallTableViewModel GlobalRearLayers => globalRear;
    public WallTableViewModel GlobalValleyLayers => globalValley;

    /// <summary>The points of the profile: «punti-valle», «punti-monte», «punti-falda».</summary>
    public IReadOnlyDictionary<string, WallTableViewModel> ProfilePoints => points;

    /// <summary>The global combination matrix (display copies of its rows).</summary>
    public WallTableViewModel? GlobalMatrix => globalMatrix;

    /// <summary>The text under the global matrix: its mode, the edit, or an error of the generation.</summary>
    public string GlobalMessage { get => globalMessage; private set => SetProperty(ref globalMessage, value); }

    /// <summary>What the global stability still needs (navy when ready, ochre otherwise), with its tooltip.</summary>
    public string GlobalReadiness { get => globalReadiness; private set => SetProperty(ref globalReadiness, value); }
    public string GlobalReadinessToolTip { get => globalReadinessTip; private set => SetProperty(ref globalReadinessTip, value); }
    public bool GlobalReady { get => globalReady; private set => SetProperty(ref globalReady, value); }

    /// <summary>The summary of the search area.</summary>
    public string GlobalSearchSummary { get => globalSearchSummary; private set => SetProperty(ref globalSearchSummary, value); }

    /// <summary>The «Stabilità globale» button of the header (its tooltip is <see cref="RetainingWallPresentation.GlobalSetupToolTip"/>).</summary>
    public string GlobalSetupText { get => globalSetupText; private set => SetProperty(ref globalSetupText, value); }

    /// <summary>The combinations of the global stability (null without a result) and the one shown.</summary>
    public IReadOnlyList<string>? GlobalCombinations => globalCombinations;

    public string? SelectedGlobalCombination { get => selectedGlobalCombination; set => SelectGlobalCombination(value, user: true); }

    /// <summary>«Intero profilo»: the drawing of the global stability shows the whole profile instead of the critical surface.</summary>
    public bool GlobalFullProfile
    {
        get => globalFullProfile;
        set { if (SetProperty(ref globalFullProfile, value)) NotifyDrawings(); }
    }

    /// <summary>The drawing of the global stability in the results.</summary>
    public WallGlobalDrawingState GlobalDrawing => new(IsGlobalView, globalDrawingCase?.Factors.Name, globalDrawingResult is not null && ReferenceEquals(globalDrawingResult, GlobalResult),
        !globalFullProfile, globalSlice);

    /// <summary>The preview of the global stability in the inputs (the search area shown).</summary>
    public WallGlobalPreviewState GlobalPreview => new(previewCase?.Factors.Name, previewResult is not null && ReferenceEquals(previewResult, GlobalResult), previewSoil, previewColumn);

    // ---------------------------------------------------------------- the card of the inputs

    private void BuildGlobalCard()
    {
        var g = Data["global_stability"]!.AsObject();
        WallSheetPresentation.PrepareGlobalSettings(Data);
        var form = Form("global", RetainingWallFields.Global, g, key => { if (key != "profile_confirmed" && key != "enabled") InvalidateGlobalConfirmation(); });
        foreach (var group in RetainingWallFields.GlobalGroups(g)) form.GroupFields(group.Title, [.. group.Keys], group.Expanded);
        globalRear = BuildGlobalSoilEditor(false); globalValley = BuildGlobalSoilEditor(true);
        Form("global_search_mode", RetainingWallFields.GlobalSearchMode, g);
        Form("global_search", RetainingWallFields.GlobalSearch, g);
        // The preview is created after the soil editors (their first selection went to the previous preview), as in the historic view.
        previewSoil = -1; previewColumn = "monte"; previewResult = null; previewCase = null;
        BuildGlobalMatrix();
        Card("Strati globali valle", true);
        points.Clear();
        foreach (var (name, key) in PointTables)
            points[name] = Table(name, RetainingWallFields.PointColumns, g.Array(key), _ => { InvalidateGlobalConfirmation(); RefreshGlobalLayerDisplays(); });
        OnPropertyChanged(nameof(GlobalRearLayers)); OnPropertyChanged(nameof(GlobalValleyLayers)); OnPropertyChanged(nameof(ProfilePoints));
    }

    /// <summary>«Prepara dal muro»: the proposal of the profile from the data of the wall, the global stability on, the card open.</summary>
    public void PrepareGlobal()
    {
        if (!CanEdit) return;
        Commit(); WallModel.PrepareGlobalProfile(Data); Data["global_stability"]!["enabled"] = true; BuildInputs(); UpdateFields(); UserChanged();
        cards["Stabilità globale"].IsExpanded = true;
    }

    /// <summary>
    /// «Stabilità globale» of the header and «Modifica profilo, strati e ricerca»: the card of the global stability, prepared from the
    /// wall when the profile is empty. When read-only the profile is not prepared (the historic view writes it: a defect).
    /// </summary>
    public void ShowGlobalSetup()
    {
        var g = Data["global_stability"]!;
        if (g.Array("valley").Count == 0 && g.Array("uphill").Count == 0 && g.Array("layers").Count == 0 && g.Array("valley_layers").Count == 0) PrepareGlobal();
        Page = 0; cards["Terreno"].IsExpanded = true; cards["Stabilità globale"].IsExpanded = true;
    }

    private void InvalidateGlobalConfirmation()
    {
        Data["global_stability"]!["profile_confirmed"] = false;
        // The check box of the form follows: unticking it is an edit of the form (its rules and the invalidation of the results).
        if (forms.TryGetValue("global", out var form) && form["profile_confirmed"].IsChecked) form["profile_confirmed"].IsChecked = false;
    }

    private WallTableViewModel BuildGlobalSoilEditor(bool valley)
    {
        string key = valley ? "valley_layers" : "layers", formKey = valley ? "global_soil_valley" : "global_soil_rear";
        var layers = Data["global_stability"]!.Array(key);
        WallTableViewModel table = null!;
        void Selected(int index)
        {
            if (forms.Remove(formKey, out var previous)) previous.Commit();
            if (index < 0 || index >= layers.Count) { OnPropertyChanged(nameof(Forms)); return; }
            var layer = layers[index]!.AsObject();
            Form(formKey, RetainingWallFields.GlobalSoil, layer, _ => InvalidateGlobalConfirmation());
            foreach (var (form, field, visible) in RetainingWallPresentation.GlobalRules(Data).Fields.Where(f => f.Form.StartsWith("global_soil_", StringComparison.Ordinal)))
                if (forms.TryGetValue(form, out var soil)) soil.ShowField(field, visible);
            previewSoil = index; previewColumn = valley ? "valle" : "monte";
            OnPropertyChanged(nameof(Forms)); NotifyDrawings();
        }
        void UpdateBottoms()
        {
            var bottoms = RetainingWallPresentation.GlobalBottoms(RetainingWallPresentation.GlobalSoilTop(Data, valley), table.Rows.Select(r => r.Values["__thickness"]));
            for (int i = 0; i < table.Rows.Count; i++)
            {
                double bottom = bottoms[i];
                layers[i]!["bottom"] = double.IsFinite(bottom) ? JsonValue.Create(bottom) : JsonValue.Create("");
                table.Rows[i].Output("bottom", double.IsFinite(bottom) ? bottom : "");
            }
        }
        void AddView(JsonObject source)
        {
            var view = (JsonObject)source.DeepClone();
            int i = table.Rows.Count; double upper = i == 0 ? RetainingWallPresentation.GlobalSoilTop(Data, valley) : J.Number(layers[i - 1]?["bottom"]) ?? double.NaN;
            view["__thickness"] = RetainingWallPresentation.GlobalThickness(upper, source["bottom"]);
            table.Rows.Add(new JsonRowViewModel(view, changed =>
            {
                if (changed == "__thickness") UpdateBottoms(); else source[changed] = view[changed]?.DeepClone();
                InvalidateGlobalConfirmation(); UserChanged();
            }));
        }
        table = new WallTableViewModel(valley ? "globale-valle" : "globale-monte", RetainingWallFields.GlobalLayerColumns, Selected);
        foreach (var layer in layers.OfType<JsonObject>()) AddView(layer);
        table.SelectedIndex = table.Rows.Count > 0 ? 0 : -1;
        globalLayerEditors[valley] = (layers, AddView, UpdateBottoms);
        return table;
    }

    private readonly Dictionary<bool, (JsonArray Layers, Action<JsonObject> AddView, Action UpdateBottoms)> globalLayerEditors = [];

    /// <summary>«+ Strato» of a column of the deep layers: a new layer, selected.</summary>
    public void AddGlobalLayer(bool valley)
    {
        if (!CanEdit) return;
        var (layers, addView, _) = globalLayerEditors[valley]; var table = valley ? globalValley : globalRear;
        var layer = RetainingWallPresentation.NewGlobalLayer();
        layers.Add(layer); addView(layer); table.SelectedIndex = table.Rows.Count - 1; InvalidateGlobalConfirmation(); UserChanged();
    }

    /// <summary>«− Strato» of a column of the deep layers: the selected one; the bottoms follow, the next layer is selected.</summary>
    public void RemoveGlobalLayer(bool valley)
    {
        var table = valley ? globalValley : globalRear;
        if (!CanEdit || table.SelectedIndex < 0) return;
        var (layers, _, updateBottoms) = globalLayerEditors[valley]; int i = table.SelectedIndex;
        table.SelectedIndex = -1; layers.RemoveAt(i); table.RemoveAt(i); updateBottoms();
        table.SelectedIndex = Math.Min(i, table.Rows.Count - 1); InvalidateGlobalConfirmation(); UserChanged();
    }

    /// <summary>«+ Punto» of a table of the points («punti-valle», «punti-monte», «punti-falda»).</summary>
    public void AddProfilePoint(string table)
    {
        if (!CanEdit || !points.TryGetValue(table, out var grid)) return;
        var array = Data["global_stability"]!.Array(PointTables.Single(p => p.Table == table).Array);
        AddRow(grid, array, RetainingWallPresentation.NewPoint(), _ => { InvalidateGlobalConfirmation(); RefreshGlobalLayerDisplays(); }); InvalidateGlobalConfirmation(); UserChanged();
    }

    /// <summary>«− Punto»: the selected point.</summary>
    public void RemoveProfilePoint(string table)
    {
        if (!CanEdit || !points.TryGetValue(table, out var grid) || grid.SelectedIndex < 0) return;
        int index = grid.SelectedIndex;
        Data["global_stability"]!.Array(PointTables.Single(p => p.Table == table).Array).Remove(grid.Rows[index].Values); grid.RemoveAt(index); InvalidateGlobalConfirmation(); UserChanged();
    }

    private void RefreshGlobalLayerDisplays()
    {
        foreach (bool valley in new[] { false, true })
        {
            var table = valley ? globalValley : globalRear; if (table is null) continue;
            var layers = Data["global_stability"]!.Array(valley ? "valley_layers" : "layers");
            double upper = RetainingWallPresentation.GlobalSoilTop(Data, valley);
            for (int i = 0; i < table.Rows.Count && i < layers.Count; i++)
            {
                double lower = J.Number(layers[i]?["bottom"]) ?? double.NaN;
                table.Rows[i].Output("__thickness", double.IsFinite(upper - lower) ? upper - lower : ""); upper = lower;
            }
        }
    }

    // ---------------------------------------------------------------- the global matrix

    /// <summary>«Genera globali»: the automatic global combinations; an error goes under the matrix.</summary>
    public void GenerateGlobalMatrix()
    {
        if (!CanEdit) return;
        Commit();
        try { RetainingWallPresentation.GenerateGlobalMatrix(Data); BuildGlobalMatrix(); UserChanged(); }
        catch (ArgumentException error) { GlobalMessage = error.Message; }
    }

    /// <summary>«Conferma matrice globale».</summary>
    public void ConfirmGlobalMatrix()
    {
        if (!CanEdit) return;
        Commit(); RetainingWallPresentation.ConfirmGlobalMatrix(Data); UserChanged();
    }

    private void BuildGlobalMatrix()
    {
        var g = Data["global_stability"]!;
        var table = new WallTableViewModel("matrice-globale", RetainingWallFields.GlobalMatrix(Data));
        foreach (var row in g.Array("combinations"))
        {
            var flat = RetainingWallPresentation.GlobalMatrixRow(Data, row!);
            table.Rows.Add(new JsonRowViewModel(flat, key =>
            {
                RetainingWallPresentation.GlobalMatrixEdit(Data, row!, flat, key);
                GlobalMessage = RetainingWallPresentation.GlobalMatrixEdited; UserChanged();
            }));
        }
        globalMatrix = table; OnPropertyChanged(nameof(GlobalMatrix));
        GlobalMessage = RetainingWallPresentation.GlobalMatrixStatus(Data);
    }

    // ---------------------------------------------------------------- refresh

    /// <summary>The search area (the automatic proposal written into the data), the guidance, the rules and the drawings of the global stability.</summary>
    private void RefreshGlobal()
    {
        var g = Data["global_stability"]!; bool automatic = g.S("search_mode") == "Automatica";
        if (automatic) WallModel.ProposeGlobalSearch(Data);
        if (automatic && forms.TryGetValue("global_search", out var search)) foreach (string key in RetainingWallPresentation.GlobalSearchLimits) search.Set(key, g.S(key), display: true);
        GlobalSearchSummary = RetainingWallPresentation.GlobalSearchSummary(Data);
        var (text, tip, ready) = RetainingWallPresentation.GlobalGuidance(Data, independentError ?? calculationResult?.GlobalError);
        GlobalReadiness = text; GlobalReadinessToolTip = tip; GlobalReady = ready;
        GlobalSetupText = RetainingWallPresentation.GlobalSetupText(Data, GlobalResult is not null);
        ApplyRules(RetainingWallPresentation.GlobalRules(Data));
        globalDrawingResult = GlobalResult; globalDrawingCase = GlobalCase;
        if (globalRear is not null) { previewResult = GlobalResult; previewCase = GlobalCase; }
        NotifyDrawings();
    }

    // ---------------------------------------------------------------- combinations of the global stability

    /// <summary>The items of the choice of the global combination; a selection no longer among them is cleared, as by the combo box.</summary>
    private void SetGlobalCombinations(IReadOnlyList<string>? items)
    {
        globalCombinations = items; OnPropertyChanged(nameof(GlobalCombinations));
        if (selectedGlobalCombination is not null && (items is null || !items.Contains(selectedGlobalCombination))) SelectGlobalCombination(null, user: false);
    }

    /// <summary>
    /// The global combination chosen: by the user (an item, the choice visible) or by the sheet (an item, or null to clear). A change
    /// refreshes the global stability and, with the filter of the global stability, its results, as the selection of the combo box.
    /// </summary>
    private void SelectGlobalCombination(string? value, bool user)
    {
        if (value == selectedGlobalCombination) return;
        if (value is not null && (globalCombinations is null || !globalCombinations.Contains(value))) return;
        if (user && (value is null || !IsGlobalView)) return;
        selectedGlobalCombination = value; OnPropertyChanged(nameof(SelectedGlobalCombination));
        RefreshGlobal(); if (selectedFilter == "Stabilità globale") ShowCheckTable();
    }

    // ---------------------------------------------------------------- independent calculation

    /// <summary>
    /// «Calcola globale» (also when read-only): the global stability alone, on a copy of the data, in place of the calculation not yet
    /// published; the results open in «Verifiche → Stabilità globale».
    /// </summary>
    public Task CalculateGlobalAsync()
    {
        if (IsDisposed) return Task.CompletedTask;
        Commit();
        var snapshot = (JsonObject)Data.DeepClone();
        var task = RunSecondaryAsync(token => StructuralQueries.WallGlobal(snapshot, token),
            () => { Status = RetainingWallPresentation.GlobalRunning; independentGlobal = null; independentInput = null; independentError = null; },
            () => { },
            result =>
            {
                independentGlobal = result; independentInput = snapshot;
                SetGlobalCombinations(result.Cases.Select(c => c.Factors.Name).ToArray()); SelectGlobalCombination(RetainingWallPresentation.WorstGlobalCombination(result), user: false);
                SelectView("Stabilità globale"); SelectFilter("Stabilità globale"); Page = 1; RefreshGlobal(); ShowCheckTable();
                Status = RetainingWallPresentation.GlobalUpdated(result.Cases.Length);
            },
            error => { independentError = error.Message; Status = RetainingWallPresentation.GlobalFailed(error.Message); RefreshGlobal(); ShowCheckTable(); });
        Track(task);
        return task;
    }

    /// <summary>«Word globale»: the report of the global stability shown (Word with the PDF beside it); refused without a result or while busy.</summary>
    public void ExportGlobalWord()
    {
        if (GlobalResult is not { } result || IsBusy) { Context.Services.Messages.Inform("", RetainingWallPresentation.WaitBeforeGlobalWord); return; }
        string? path = Context.Services.Files.SaveFile(new SaveFileRequest([new FileFilter("Relazione Word e PDF affiancati", "*.docx")], "Muro_stabilita_globale.docx"));
        if (path is null) return;
        var input = independentInput ?? calculationResult!.Input;
        var figure = figures.RenderGlobal(input, result, GlobalCase);
        Anthea.Reports.ReportFiles.Write(path, WallExports.CreateGlobal(input, result, new(figure.Caption, figure.Png, figure.AspectRatio)), "Stabilità globale");
    }

    // ---------------------------------------------------------------- the results of the global stability

    private IReadOnlyList<WallPanelBlock> GlobalResultsPanel()
    {
        if (GlobalResult is not { } result)
            return [WallPanelBlock.Of(independentError ?? calculationResult?.GlobalError ?? RetainingWallPresentation.GlobalNoResult, WallTextStyle.Large)];
        var blocks = new List<WallPanelBlock>
        {
            WallPanelBlock.Of(RetainingWallPresentation.GlobalResultExplanation, WallTextStyle.Normal),
            WallPanelBlock.Button("Modifica profilo, strati e ricerca", EditGlobalSetupCommand),
            WallPanelBlock.Table(RetainingWallPresentation.GlobalColumns, RetainingWallPresentation.GlobalRows(result), 155, WallRowAction.GlobalCombination),
            WallPanelBlock.Of(Slope.Formula, WallTextStyle.Small)
        };
        resultRowSelected = index => { if (index >= 0) SelectGlobalCombination(result.Cases[index].Factors.Name, user: false); };
        if (GlobalCase is { Critical: { } critical } shown)
        {
            blocks.Add(WallPanelBlock.Of(RetainingWallPresentation.GlobalCriticalText(shown, critical), WallTextStyle.Small));
            blocks.Add(WallPanelBlock.Table(RetainingWallPresentation.SliceColumns, RetainingWallPresentation.SliceRows(critical), 240, WallRowAction.Slice));
        }
        blocks.Add(WallPanelBlock.Of(RetainingWallPresentation.GlobalNotes(result), WallTextStyle.SmallMuted));
        return blocks;
    }

    /// <summary>The command of the button «Modifica profilo, strati e ricerca» of the results.</summary>
    public const string EditGlobalSetupCommand = "modifica-globale";

    /// <summary>Selects a slice of the critical surface (−1 none): highlighted in the drawing of the global stability.</summary>
    public void SelectSlice(int index)
    {
        if (index == globalSlice) return;
        globalSlice = index; NotifyDrawings();
    }
}
