using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.QuickSection;

/// <summary>A row of the table of the rings of the circular section: the switch of the second ring, number, diameter and clear distance (null when the row has none).</summary>
public sealed record QuickBarRow(string Title, FieldViewModel? Enabled, FieldViewModel Count, FieldViewModel Diameter, FieldViewModel? Gap);

/// <summary>
/// A combination of the quick section (17/11/2026): name, N, Mx, My as texts of the document, the ratio of the engine for the method chosen (η, or
/// the ratio of the stresses), the selection (the results in evidence are those of the selected combination). Every change is an edit of the sheet.
/// </summary>
public sealed class QuickCombinationRow : ObservableObject
{
    private readonly QuickSectionSheetViewModel owner;
    private readonly JsonObject row;

    internal QuickCombinationRow(QuickSectionSheetViewModel owner, JsonObject row, bool canEdit, bool myEditable, IRelayCommand remove, IRelayCommand select)
    {
        this.owner = owner; this.row = row; CanEdit = canEdit; MyEditable = myEditable; RemoveCommand = remove; SelectCommand = select;
    }

    public string Id => row.S("id");
    public bool CanEdit { get; }
    public bool IsReadOnly => !CanEdit;
    /// <summary>My is used only with the biaxial bending.</summary>
    public bool MyEditable { get; }
    public bool MyReadOnly => IsReadOnly || !MyEditable;
    public string Name { get => row.S("nome"); set => Write("nome", value); }
    public string N { get => row.S("N"); set => Write("N", value); }
    public string Mx { get => row.S("Mx"); set => Write("Mx", value); }
    public string My { get => row.S("My"); set => Write("My", value); }

    /// <summary>
    /// The limit state of the combination (18/11/2026): «Capacità» follows the check on the top of the sheet; SLU, SLV and the SLE states set the
    /// resistance model or the method n and the checks of that state.
    /// </summary>
    public string State
    {
        get => row.S(ConcreteQuickSection.StateField) is { Length: > 0 } state ? state : ConcreteQuickSection.LimitStates[0];
        set { if (value is not null && value != State && owner.SetCombinationState(Id, value)) row[ConcreteQuickSection.StateField] = value; OnPropertyChanged(); }
    }

    /// <summary>The choices of <see cref="State"/>.</summary>
    public IReadOnlyList<ConcreteQuickLimitState> States => ConcreteQuickSection.LimitStateChoices;
    public bool IsSelected { get => owner.SelectedCombinationId == Id; set { if (value) owner.SelectCombination(Id); OnPropertyChanged(); } }
    /// <summary>The ratio of the engine for this combination, or its status when there is none.</summary>
    public string Ratio => owner.Current?.Combinations.FirstOrDefault(c => c.Id == Id) is { } c
        ? (c.Ratio is double r ? EngineeringFormat.Number(r) + (c.Governing.Length > 0 ? " " + c.Governing : "") : "—") : "";
    /// <summary>The status of the check; with two checks (quasi-permanent) both ratios and which one governs (w: cracking, σ: stresses).</summary>
    public string RatioToolTip => owner.Current?.Combinations.FirstOrDefault(c => c.Id == Id) is { } c
        ? (c.Ratios.Length > 0 ? c.Ratios + " · comanda " + (c.Governing == "w" ? "la fessurazione" : "la tensione") + "\n" : "") + c.Status : "";
    /// <summary>True within 1, false beyond, null without a ratio.</summary>
    public bool? Passed => owner.Current?.Combinations.FirstOrDefault(c => c.Id == Id)?.Passed;
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand SelectCommand { get; }

    internal void Refresh()
    {
        foreach (string p in new[] { nameof(IsSelected), nameof(Ratio), nameof(RatioToolTip), nameof(Passed) }) OnPropertyChanged(p);
    }

    private void Write(string key, string? value, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        if (value is not null && value != row.S(key) && owner.EditCombination(Id, key, value)) row[key] = value;
        OnPropertyChanged(property);
    }
}

/// <summary>
/// A layer of bars of the quick section (rectangular and flanged sections): position (top, bottom, at a distance from the top), distance d, number
/// and diameter, as texts of the document. Every change is an edit of the sheet; the bars are arranged by <see cref="ConcreteQuickSection.ApplyLayers"/>.
/// </summary>
public sealed class QuickLayerRow : ObservableObject
{
    private readonly QuickSectionSheetViewModel owner;
    private readonly JsonObject layer;

    internal QuickLayerRow(QuickSectionSheetViewModel owner, int index, JsonObject layer, bool canEdit, IRelayCommand remove)
    {
        this.owner = owner; this.layer = layer; Index = index; CanEdit = canEdit; RemoveCommand = remove;
    }

    public int Index { get; }
    public string Title => (Index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
    public bool CanEdit { get; }
    public bool IsReadOnly => !CanEdit;
    public IReadOnlyList<string> Positions => ConcreteQuickSection.LayerPositions;
    public string Position { get => ConcreteQuickSection.LayerPosition(layer.S("posizione")); set => Write("posizione", value); }
    public string Distance { get => layer.S("d"); set => Write("d", value); }
    public string Count { get => layer.S("n"); set => Write("n", value); }
    public string Diameter { get => layer.S("phi"); set => Write("phi", value); }
    /// <summary>The distance d is used only by the layers «Da sopra» and «Da sotto».</summary>
    public bool UsesDistance => Position == ConcreteQuickSection.LayerPositions[2] || Position == ConcreteQuickSection.LayerPositions[3];
    public IRelayCommand RemoveCommand { get; }

    private void Write(string key, string? value, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        string current = key == "posizione" ? Position : layer.S(key);
        if (value is not null && value != current) owner.EditLayer(Index, key, value);
        OnPropertyChanged(property);
        if (key == "posizione") OnPropertyChanged(nameof(UsesDistance));
    }
}

/// <summary>The quick RC section (17/11/2026): a simplified interface on the engine of the RC section, without a report of its own.</summary>
public sealed class QuickSectionSheetModule : ISheetModule
{
    public ModuleInfo Info => ModuleDirectory.Get(ConcreteQuickSection.Module);
    public ModuleHelp Help => ModuleHelp.For(Info.Id);
    /// <summary>The choices of the view (title, method, bending, chart, layers) are data of the sheet, in <see cref="ConcreteQuickSection.ViewKey"/>.</summary>
    public ModulePersistence Persistence { get; } = new([ConcreteQuickSection.ViewKey]);
    public ModuleReportCapabilities Reports { get; } = new(false);
    public ISheetViewModel CreateSheet(SheetContext context) => new QuickSectionSheetViewModel(context);
}

/// <summary>
/// The sheet of the quick RC section, on one page: shape and dimensions, layers of bars (rings for the circular section) with the links, materials, the
/// ultimate action and the action of the method n, the check (positive or negative moment, method n), the resistance model (plastic, elastic), the
/// bending (straight or biaxial) and the drawing area (section, domain or moment–curvature). The data have the format of the RC section; the
/// calculation is the service of the module (<see cref="ConcreteQuickSectionService"/>), which calls the engine of the RC section after every edit,
/// with the delay of the other sheets. The ViewModel computes nothing: the values come from <see cref="ConcreteQuickSection"/>.
/// </summary>
public sealed class QuickSectionSheetViewModel : SheetViewModelBase
{
    /// <summary>The delay of the automatic recalculation after an edit.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(450);

    private bool building;
    private ConcreteGeometry? section;
    private string drawingMessage = "";
    private bool showSection = true;

    public QuickSectionSheetViewModel(SheetContext context) : base(ConcreteQuickSection.Module, context, RecalculationDelay)
    {
        ConcreteQuickSection.Validate(Data);
        Report = new NoReport();
        AddLayerCommand = GuardedCommands.Create(Guard, AddLayer, () => CanEdit && !IsCircular);
        AddCombinationCommand = GuardedCommands.Create(Guard, AddCombination, () => CanEdit);
        CoverHelpCommand = GuardedCommands.Create(Guard, () => Context.Services.Messages.Inform(ConcreteQuickSection.CoverHelpTitle, ConcreteQuickSection.CoverHelp));
        ExportExcelCommand = GuardedCommands.Create(Guard, ExportExcel);
        ImportExcelCommand = GuardedCommands.Create(Guard, ImportExcel, () => CanEdit);
        BuildForms();
        RefreshDrawing();
        // As Spectra: the sheet calculates at opening, without modifying the document; after an edit the automatic recalculation follows.
        _ = ScheduleCalculationAsync();
    }

    public override IModuleReportSource Report { get; }

    private JsonObject Input => Data["input"]!.AsObject();
    private JsonObject View => ConcreteQuickSection.View(Data);

    // ---------------------------------------------------------------- choices

    /// <summary>The shapes that can be chosen: rectangular, circular.</summary>
    public IReadOnlyList<string> Shapes => ConcreteQuickSection.Shapes;

    /// <summary>
    /// The shapes on screen, in order: rectangular, circular and DXF, the last one shown off with its note (18/11/2026). A sheet saved with the
    /// flanged section keeps it: no choice is selected and its fields stay.
    /// </summary>
    public IReadOnlyList<ConcreteQuickShape> ShapeChoices => ConcreteQuickSection.ShapeChoices;
    /// <summary>The checks on screen: positive moment, negative moment (the SLU), method n.</summary>
    public IReadOnlyList<string> Methods => ConcreteQuickSection.MethodChoices(IsStraight);
    /// <summary>The resistance models on screen: plastic, elastic.</summary>
    public IReadOnlyList<string> Models => ConcreteQuickSection.ModelLabels;
    public IReadOnlyList<string> Bendings => ConcreteQuickSection.Bendings;
    public IReadOnlyList<string> Charts => ConcreteQuickSection.Charts;

    /// <summary>The shape of the section: the fields of the geometry and of the bars follow it.</summary>
    public string Shape
    {
        get => Input.S("shape");
        set
        {
            if (value is null || value == Shape || !Shapes.Contains(value)
                || !Edit(d => { d["input"]!["shape"] = value; ConcreteQuickSection.ApplyLayers(d); })) { OnPropertyChanged(); return; }
            BuildForms(); RefreshDrawing(); AddLayerCommand.NotifyCanExecuteChanged(); OnPropertyChanged();
        }
    }

    public bool IsCircular => Shape == ConcreteQuickSection.Circular;

    // ---------------------------------------------------------------- the settings of the selected combination (18/11/2026)

    /// <summary>The selected combination as <see cref="ConcreteQuickSection.Combinations"/> reads it (limit state, model, sign), or null.</summary>
    private JsonObject? SelectedRow => ConcreteQuickSection.Combinations(Data).FirstOrDefault(r => r.S("id") == SelectedCombinationId);

    private bool SelectedUltimate => SelectedRow is not { } row || ConcreteQuickSection.Ultimate(row.S(ConcreteQuickSection.StateField));

    /// <summary>The code of the check of the selected combination: its sign (chosen, or from Mx) for SLU and SLV, «Metodo n» for the SLE states.</summary>
    private string MethodCode()
    {
        if (SelectedRow is not { } row) return ConcreteQuickSection.Methods[0];
        if (!ConcreteQuickSection.Ultimate(row.S(ConcreteQuickSection.StateField))) return ConcreteQuickSection.Methods[2];
        if (row.S(ConcreteQuickSection.SignField) is { Length: > 0 } sign) return sign;
        return J.Number(row["Mx"]) is < 0 ? ConcreteQuickSection.Methods[1] : ConcreteQuickSection.Methods[0];
    }

    /// <summary>
    /// The check of the selected combination: «Momento positivo», «Momento negativo» (the sign of MRd of SLU and SLV) or «Metodo n» (the SLE states).
    /// A change applies to that combination: «Metodo n» makes an SLU or SLV combination SLE rara, a moment makes an SLE combination SLU.
    /// </summary>
    public string Method
    {
        get => ConcreteQuickSection.MethodLabel(MethodCode(), IsStraight);
        set
        {
            if (value is null || SelectedCombinationId is not { } id || building || IsDisposed) { NotifyCombinationSettings(); return; }
            string? code = ConcreteQuickSection.MethodCode(value, MethodCode());
            bool ultimate = SelectedUltimate;
            if (code == ConcreteQuickSection.Methods[2])
            {
                if (ultimate) Edit(d => ConcreteQuickSection.SetCombinationState(d, id, "SLE"));
            }
            else if (code is not null)
            {
                Edit(d =>
                {
                    if (!ultimate) ConcreteQuickSection.SetCombinationState(d, id, "SLU");
                    if (IsStraight) ConcreteQuickSection.WriteCombinationSetting(d, id, ConcreteQuickSection.SignField, code);
                });
            }
            BuildCombinations();
            NotifyCombinationSettings();
        }
    }

    /// <summary>
    /// The choices of the check for each bending, with fixed lists (the view shows the one of the bending): a list that changed under a selected
    /// choice would lose the selection.
    /// </summary>
    public IReadOnlyList<string> StraightMethods => ConcreteQuickSection.MethodLabels;
    public IReadOnlyList<string> BiaxialMethods => ConcreteQuickSection.BiaxialMethodLabels;

    /// <summary>The check with the straight bending: «Momento positivo», «Momento negativo», «Metodo n»; a value of the other list is ignored.</summary>
    public string? StraightMethod
    {
        get => ConcreteQuickSection.MethodLabel(MethodCode());
        set { if (value is not null && StraightMethods.Contains(value)) Method = value; else OnPropertyChanged(); }
    }

    /// <summary>The check with the biaxial bending: «Capacità» or «Metodo n»; a value of the other list is ignored.</summary>
    public string? BiaxialMethod
    {
        get => ConcreteQuickSection.MethodLabel(MethodCode(), false);
        set { if (value is not null && BiaxialMethods.Contains(value)) Method = value; else OnPropertyChanged(); }
    }

    /// <summary>
    /// The resistance model of the selected combination: «Plastica» or «Elastica» for SLU and SLV (by default plastic and elastic), none for the SLE
    /// states. A change applies to that combination; on an SLE combination it makes it SLU with that model.
    /// </summary>
    public string? Model
    {
        get => SelectedRow is { } row && ConcreteQuickSection.Ultimate(row.S(ConcreteQuickSection.StateField))
            ? ConcreteQuickSection.ModelLabel(row.S(ConcreteQuickSection.ModelField)) : SelectedRow is null ? ConcreteQuickSection.ModelLabels[0] : null;
        set
        {
            string? model = ConcreteQuickSection.Models.ElementAtOrDefault(ConcreteQuickSection.ModelLabels.IndexOf(value ?? ""));
            if (model is not null && SelectedCombinationId is { } id && !building && !IsDisposed && ConcreteQuickSection.ModelLabel(model) != Model)
            {
                bool ultimate = SelectedUltimate;
                Edit(d =>
                {
                    if (!ultimate) ConcreteQuickSection.SetCombinationState(d, id, "SLU");
                    ConcreteQuickSection.WriteCombinationSetting(d, id, ConcreteQuickSection.ModelField, model);
                });
                BuildCombinations();
            }
            NotifyCombinationSettings();
        }
    }

    /// <summary>The settings of the selected combination shown again (selection, limit state, model, sign).</summary>
    private void NotifyCombinationSettings()
    {
        foreach (string p in new[] { nameof(Model), nameof(Method), nameof(StraightMethod), nameof(BiaxialMethod), nameof(IsServiceMethod), nameof(RatioHeader) })
            OnPropertyChanged(p);
    }
    /// <summary>Straight or biaxial bending: with the straight one My is not used.</summary>
    public string Bending
    {
        get => View.S("flessione");
        set { if (Choose("flessione", value, Bendings)) BuildForms(); }
    }

    /// <summary>The chart of the drawing area: the 2D domain or the moment–curvature.</summary>
    public string Chart { get => View.S("grafico"); set => Choose("grafico", value, Charts); }

    /// <summary>The title of the sheet.</summary>
    public string Title
    {
        get => View.S("titolo");
        set { if (value is not null && value != Title && Edit(d => ConcreteQuickSection.View(d)["titolo"] = value)) OnPropertyChanged(); }
    }

    /// <summary>What the drawing area shows: the section, or the chart chosen (domain or moment–curvature).</summary>
    public IReadOnlyList<string> Displays { get; } = ["Sezione", .. ConcreteQuickSection.Charts];

    /// <summary>
    /// The content of the drawing area. «Sezione» is a state of the view (not saved, the document does not change); the domain and the
    /// moment–curvature are the chart of the sheet (<see cref="Chart"/>), a datum: the moment–curvature is calculated only when chosen.
    /// </summary>
    public string Display
    {
        get => showSection ? Displays[0] : Chart;
        set
        {
            if (value is null || !Displays.Contains(value)) { OnPropertyChanged(); return; }
            if (value == Displays[0]) showSection = true;
            else if (value == Chart || CanEdit) { showSection = false; Chart = value; }
            OnPropertyChanged(); OnPropertyChanged(nameof(ShowsSection)); OnPropertyChanged(nameof(ShowsDomain));
        }
    }

    /// <summary>True when the drawing area shows the section.</summary>
    public bool ShowsSection => showSection;

    public bool IsStraight => Bending == ConcreteQuickSection.Bendings[0];
    /// <summary>True when the selected combination is an SLE one (method n).</summary>
    public bool IsServiceMethod => !SelectedUltimate;
    public bool ShowsDomain => Chart == ConcreteQuickSection.Charts[0];

    private bool Choose(string key, string? value, IReadOnlyList<string> choices, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        if (value is null || View.S(key) == value || !choices.Contains(value) || !Edit(d => ConcreteQuickSection.View(d)[key] = value))
        {
            OnPropertyChanged(property); return false;
        }
        OnPropertyChanged(property);
        foreach (string p in new[] { nameof(IsStraight), nameof(IsServiceMethod), nameof(ShowsDomain), nameof(RatioHeader) }) OnPropertyChanged(p);
        return true;
    }

    // ---------------------------------------------------------------- forms

    public FormViewModel GeometryForm { get; private set; } = null!;
    public FormViewModel BarsForm { get; private set; } = null!;
    public FormViewModel StirrupForm { get; private set; } = null!;
    public FormViewModel MaterialsForm { get; private set; } = null!;

    /// <summary>
    /// The creep coefficient φ of the method n (18/11/2026), with the concrete: a copy of the value of the document, written in the options of the
    /// serviceability limit states of the RC section (<see cref="ConcreteQuickSection.WriteCreep"/>) at every edit.
    /// </summary>
    public FormViewModel CreepForm { get; private set; } = null!;

    /// <summary>The field of φ, under the values of the concrete.</summary>
    public FieldViewModel CreepField => CreepForm[ConcreteQuickSection.CreepKey];

    /// <summary>The exposure class (18/11/2026), under φ: the environment and the limit of the cracking of the SLE combinations.</summary>
    public FieldViewModel ExposureField => CreepForm[ConcreteQuickSection.ExposureKey];

    private IEnumerable<FormViewModel> Forms => new[] { GeometryForm, BarsForm, StirrupForm, MaterialsForm, CreepForm }.Where(f => f is not null);

    /// <summary>The diameter of the links, with the bars.</summary>
    public FieldViewModel StirrupField => StirrupForm["transverse_bar_diameter_mm"];

    /// <summary>The rings of the circular section (ring, second ring).</summary>
    public IReadOnlyList<QuickBarRow> BarRows { get; private set; } = [];

    /// <summary>The layers of bars of the rectangular and flanged sections.</summary>
    public IReadOnlyList<QuickLayerRow> LayerRows { get; private set; } = [];

    /// <summary>«+»: a new layer of bars.</summary>
    public IRelayCommand AddLayerCommand { get; }

    /// <summary>«?» of the cover: the window that explains what c measures.</summary>
    public IRelayCommand CoverHelpCommand { get; }

    /// <summary>The fields of the concrete: the class and its properties typed by hand.</summary>
    public IReadOnlyList<FieldViewModel> ConcreteFields => MaterialsForm.Fields.Where(f => f.Key is "classe_cls" || ConcreteQuickSection.ConcreteInputs.Contains(f.Key)).ToArray();

    /// <summary>The fields of the steel: the class and its properties typed by hand.</summary>
    public IReadOnlyList<FieldViewModel> SteelFields => MaterialsForm.Fields.Where(f => f.Key is "classe_acciaio" || ConcreteQuickSection.SteelInputs.Contains(f.Key)).ToArray();

    /// <summary>The combinations: one table for every method, the results in evidence for the selected one.</summary>
    public IReadOnlyList<QuickCombinationRow> CombinationRows { get; private set; } = [];

    /// <summary>«+»: a new combination at zero, selected.</summary>
    public IRelayCommand AddCombinationCommand { get; private set; } = null!;

    /// <summary>The identifier of the selected combination.</summary>
    public string? SelectedCombinationId => ConcreteQuickSection.SelectedCombination(Data);

    /// <summary>The head of the column of the ratios: η for the ultimate check, the ratio of the stresses for the method n.</summary>
    public string RatioHeader => ConcreteQuickSection.Combinations(Data).Select(r => r.S(ConcreteQuickSection.StateField)).Distinct().ToArray() switch
    {
        var states when states.All(ConcreteQuickSection.Ultimate) => "η",
        ["SLE"] => "σ/σlim",
        ["SLE_FREQ"] => "wk/wlim",
        _ => "rapp."
    };

    private static FieldSpec T(string key, string label, string unit, string tip) => new(key, label, unit, ToolTip: tip);
    private static FieldSpec B(string key, string tip) => new(key, "", Bool: true, ToolTip: tip);

    private FormViewModel Form(JsonObject values, IEnumerable<FieldSpec> fields, Action<string> changed) =>
        new(values, fields, changed, compact: true) { IsReadOnly = !CanEdit };

    private void BuildForms()
    {
        building = true;
        try
        {
            string shape = Shape;
            var cover = T("cover_mm", "c", "mm", "Copriferro netto della staffa: dal bordo alla faccia esterna della staffa");
            GeometryForm = Form(Input, shape switch
            {
                "Circolare" => [T("diameter_mm", "D", "mm", "Diametro della sezione"), cover],
                "A T" => [T("flange_width_mm", "B ala", "mm", "Larghezza dell'ala"), T("flange_thickness_mm", "s ala", "mm", "Spessore dell'ala"),
                    T("web_width_mm", "b anima", "mm", "Larghezza dell'anima"), T("height_mm", "h", "mm", "Altezza totale"), cover],
                _ => new[] { T("width_mm", "b", "mm", "Base"), T("height_mm", "h", "mm", "Altezza"), cover }
            }, InputEdited);
            StirrupForm = Form(Input, [T("transverse_bar_diameter_mm", "Ø staffe", "mm", "Diametro delle staffe")], InputEdited);
            FieldSpec BarCount(string key, string tip) => T(key, "n", "", tip);
            FieldSpec BarDiameter(string key, string tip) => T(key, "Ø", "mm", tip);
            BarsForm = Form(Input, shape == ConcreteQuickSection.Circular
                ? [BarCount("longitudinal_bar_count", "Barre sull'anello"), BarDiameter("longitudinal_bar_diameter_mm", "Diametro delle barre"),
                    B("second_inner_enabled", "Secondo anello"), BarCount("second_inner_count", "Barre del secondo anello"), BarDiameter("second_inner_diameter", "Diametro del secondo anello"),
                    T("second_inner_gap", "s", "mm", "Distanza libera fra gli anelli")]
                : [], InputEdited);
            BarRows = shape == ConcreteQuickSection.Circular
                ? [new("Anello", null, BarsForm["longitudinal_bar_count"], BarsForm["longitudinal_bar_diameter_mm"], null),
                    new("2° anello", BarsForm["second_inner_enabled"], BarsForm["second_inner_count"], BarsForm["second_inner_diameter"], BarsForm["second_inner_gap"])]
                : [];
            BuildLayers();
            MaterialsForm = Form(Input, [new("classe_cls", "Classe", Choices: ConcreteQuickSection.MaterialChoices(Data, "classe_cls"), ToolTip: "Classe del calcestruzzo"),
                T("fck_mpa", "fck", "MPa", "Resistenza caratteristica cilindrica: scritta a mano rende la classe «Personalizzato»"),
                new("classe_acciaio", "Classe", Choices: ConcreteQuickSection.MaterialChoices(Data, "classe_acciaio"), ToolTip: "Classe dell'acciaio delle barre"),
                T("fyk_mpa", "fyk", "MPa", "Tensione caratteristica di snervamento"), T("steel_modulus_mpa", "Es", "MPa", "Modulo elastico dell'acciaio"),
                T("steel_fu_mpa", "fu", "MPa", "Tensione di rottura"), T("steel_eps_u", "εsu", "‰", "Deformazione ultima dell'acciaio")], InputEdited);
            var service = new JsonObject
            {
                [ConcreteQuickSection.CreepKey] = ConcreteQuickSection.Creep(Data), [ConcreteQuickSection.ExposureKey] = ConcreteQuickSection.Exposure(Data)
            };
            CreepForm = Form(service, [T(ConcreteQuickSection.CreepKey, "φ", "", ConcreteQuickSection.CreepHelp),
                new(ConcreteQuickSection.ExposureKey, "Esp.", Choices: ConcreteQuickSection.ExposureChoices.ToArray(), ToolTip: ConcreteQuickSection.ExposureHelp)],
                key => ServiceEdited(service, key));
            BuildCombinations();
        }
        finally { building = false; }
        foreach (string p in new[] { nameof(GeometryForm), nameof(BarsForm), nameof(StirrupForm), nameof(StirrupField), nameof(MaterialsForm), nameof(CreepForm), nameof(CreepField), nameof(ExposureField),
            nameof(ConcreteFields), nameof(SteelFields),
            nameof(BarRows), nameof(RatioHeader), nameof(Shape), nameof(IsCircular), nameof(Methods), nameof(Method), nameof(StraightMethod), nameof(BiaxialMethod), nameof(Model), nameof(Bending), nameof(Chart), nameof(Title),
            nameof(IsStraight), nameof(IsServiceMethod), nameof(ShowsDomain) })
            OnPropertyChanged(p);
    }

    private void BuildLayers()
    {
        var layers = IsCircular ? [] : ConcreteQuickSection.Layers(Data).OfType<JsonObject>().ToArray();
        LayerRows = layers.Select((layer, index) => new QuickLayerRow(this, index, layer, CanEdit,
            GuardedCommands.Create(Guard, () => RemoveLayer(index), () => CanEdit))).ToArray();
        OnPropertyChanged(nameof(LayerRows));
    }

    private void AddLayer()
    {
        if (Edit(d => { ConcreteQuickSection.AddLayer(d); ConcreteQuickSection.ApplyLayers(d); })) { BuildLayers(); RefreshDrawing(); }
    }

    private void RemoveLayer(int index)
    {
        if (Edit(d => { ConcreteQuickSection.RemoveLayer(d, index); ConcreteQuickSection.ApplyLayers(d); })) { BuildLayers(); RefreshDrawing(); }
    }

    /// <summary>A field of a layer changed (<see cref="QuickLayerRow"/>): an edit of the sheet, with the bars arranged again.</summary>
    internal void EditLayer(int index, string key, string value)
    {
        if (building || IsDisposed) return;
        if (Edit(d => { ConcreteQuickSection.WriteLayer(d, index, key, value); ConcreteQuickSection.ApplyLayers(d); })) RefreshDrawing();
    }

    private void BuildCombinations()
    {
        CombinationRows = ConcreteQuickSection.Combinations(Data).Select(row =>
        {
            string id = row.S("id");
            return new QuickCombinationRow(this, row, CanEdit, !IsStraight,
                GuardedCommands.Create(Guard, () => RemoveCombination(id), () => CanEdit && CombinationRows.Count > 1),
                GuardedCommands.Create(Guard, () => SelectCombination(id)));
        }).ToArray();
        OnPropertyChanged(nameof(CombinationRows)); OnPropertyChanged(nameof(SelectedCombinationId)); NotifyCombinationSettings();
    }

    private void AddCombination()
    {
        if (Edit(d => ConcreteQuickSection.AddCombination(d))) BuildCombinations();
    }

    // ---------------------------------------------------------------- Excel

    /// <summary>The title of the messages of the Excel buttons.</summary>
    public const string ExcelTitle = "Sollecitazioni in Excel";

    /// <summary>
    /// «Esporta in Excel»: the combinations written in a workbook of the work folder (<see cref="ExcelFolder"/>), without a save window, and
    /// opened with the program of the xlsx files. The same file is written again at the next export; a new one when Excel holds it.
    /// </summary>
    public IRelayCommand ExportExcelCommand { get; }

    /// <summary>
    /// «Importa da Excel»: the workbook of the last export read again (after Ctrl+S in Excel), without an open window; the combinations of the sheet
    /// replaced by its rows. Without an export in this session the workbook is chosen in the open window.
    /// </summary>
    public IRelayCommand ImportExcelCommand { get; }

    /// <summary>The work folder of the workbooks of the combinations: %LocalAppData%\ANTHEA\Sollecitazioni (the checks point it to their folder).</summary>
    public string ExcelFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "Sollecitazioni");

    /// <summary>The workbook of the last export, read again by «Importa da Excel»; null before the first export.</summary>
    public string? ExcelPath { get; private set; }

    private static FileFilter ExcelFilter { get; } = new("Cartella Excel", "*.xlsx");

    private string NewExcelPath()
    {
        string title = string.Concat(Title.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string name = "Sollecitazioni " + (title.Length == 0 ? "sezione c.a." : title) + " " + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string folder = Path.GetFullPath(ExcelFolder);
        string path = Path.Combine(folder, name + ".xlsx");
        for (int k = 2; File.Exists(path); k++) path = Path.Combine(folder, name + " (" + k + ").xlsx");
        return path;
    }

    private void ExportExcel()
    {
        byte[] bytes;
        try { bytes = ConcreteQuickActionsExcel.Export(Data); }
        catch (ArgumentException error) { Context.Services.Messages.Inform(ExcelTitle, error.Message); return; }
        Directory.CreateDirectory(ExcelFolder);
        string path = ExcelPath ?? NewExcelPath();
        try { X.Core.Archivio.ScriviAtomico(path, bytes); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The workbook of the last export is open in Excel: a new one.
            path = NewExcelPath();
            X.Core.Archivio.ScriviAtomico(path, bytes);
        }
        ExcelPath = path;
        OnPropertyChanged(nameof(ExcelPath));
        Context.Services.Links.Open(new Uri(path));
    }

    private void ImportExcel()
    {
        string? path = ExcelPath is { } last && File.Exists(last) ? last
            : Context.Services.Files.OpenFile(new OpenFileRequest([ExcelFilter], null, "Importa le sollecitazioni da Excel"));
        if (path is null) return;
        IReadOnlyList<ConcreteQuickActionsExcel.Row> rows;
        try
        {
            // Excel keeps the workbook open while the user works on it: read it without excluding it.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            rows = ConcreteQuickActionsExcel.Read(buffer.ToArray());
        }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Context.Services.Messages.Inform(ExcelTitle, error.Message + "\n\nFile: " + path);
            return;
        }
        if (Edit(d => ConcreteQuickSection.ReplaceCombinations(d, rows)))
        {
            ExcelPath = path;
            OnPropertyChanged(nameof(ExcelPath));
            BuildCombinations();
        }
    }

    private void RemoveCombination(string id)
    {
        if (Edit(d => ConcreteQuickSection.RemoveCombination(d, id))) BuildCombinations();
    }

    /// <summary>Selects a combination: the results in evidence and the resistances at its N (an edit of the sheet: the selection is saved).</summary>
    internal void SelectCombination(string id)
    {
        if (building || IsDisposed || id == SelectedCombinationId) return;
        if (Edit(d => ConcreteQuickSection.SelectCombination(d, id)))
        {
            OnPropertyChanged(nameof(SelectedCombinationId));
            foreach (var row in CombinationRows) row.Refresh();
            NotifyCombinationSettings();
        }
    }

    /// <summary>The limit state of a combination changed (<see cref="QuickCombinationRow.State"/>): an edit of the sheet; false when refused.</summary>
    internal bool SetCombinationState(string id, string state)
    {
        if (building || IsDisposed) return false;
        if (!Edit(d => ConcreteQuickSection.SetCombinationState(d, id, state))) return false;
        NotifyCombinationSettings();
        return true;
    }

    /// <summary>A field of a combination changed (<see cref="QuickCombinationRow"/>): an edit of the sheet; false when refused.</summary>
    internal bool EditCombination(string id, string key, string value)
    {
        if (building || IsDisposed) return false;
        bool done = Edit(d => ConcreteQuickSection.WriteCombination(d, id, key, value));
        // The sign of the moment of an SLU or SLV combination may follow its Mx.
        if (done && key == "Mx" && id == SelectedCombinationId) NotifyCombinationSettings();
        return done;
    }

    private void InputEdited(string key)
    {
        if (building || IsDisposed) return;
        if (key is "classe_cls" or "classe_acciaio")
        {
            // The class of the catalogue: its values written and shown again, as the RC section does.
            ConcreteQuickSection.ApplyMaterial(Data, key);
            foreach (string value in ConcreteQuickSection.MaterialKeys(Data, key)) MaterialsForm.Set(value, Input.S(value), true);
        }
        else if (ConcreteQuickSection.MarkCustomMaterial(Data, key) is { } classKey) MaterialsForm.Set(classKey, Input.S(classKey), true);
        // The bars of the layers depend on the dimensions, the cover and the links.
        ConcreteQuickSection.ApplyLayers(Data);
        Changed();
        RefreshDrawing();
    }

    // φ or the exposure changed: written where the RC section reads it, then the serviceability checks calculated again.
    private void ServiceEdited(JsonObject service, string key)
    {
        if (building || IsDisposed) return;
        Edit(d => ConcreteQuickSection.WriteServiceOption(d, key, service.S(key)));
    }

    // ---------------------------------------------------------------- drawing

    /// <summary>The section drawn, from the geometry of the engine; null when the data do not describe a section.</summary>
    public ConcreteGeometry? Section => section;

    /// <summary>The links drawn on the section: the scheme of the shear of the RC section in the data.</summary>
    public JsonObject? Stirrups => Data["workspace_ca"]?["taglio"] as JsonObject;

    /// <summary>Why the section cannot be drawn.</summary>
    public string DrawingMessage => drawingMessage;

    /// <summary>The stresses drawn on the section: the method n, or the state at the resistant point of the ultimate check.</summary>
    public ConcreteResults.StressState? SectionStress => Current is { } r ? (r.Method == ConcreteQuickSection.Methods[2] ? r.ServiceState : r.UltimateState) : null;

    private void RefreshDrawing()
    {
        (section, drawingMessage) = ConcreteQuickSection.Geometry(Data);
        OnPropertyChanged(nameof(Section)); OnPropertyChanged(nameof(DrawingMessage)); OnPropertyChanged(nameof(SectionStress));
    }

    // ---------------------------------------------------------------- results

    /// <summary>The outcome of the engine for the current data, or null.</summary>
    public ConcreteQuickResult? Current => PublishedCalculation?.Native as ConcreteQuickResult;

    /// <summary>The main outcome in evidence: the design resisting moment (or the stresses of the method n), η with the outcome, the behaviour.</summary>
    public ConcreteQuickSummary? Summary => ConcreteQuickSection.Summary(Current);

    /// <summary>True when the behaviour is ductile, false when brittle, null when not given (method n, no result).</summary>
    public bool? IsDuctile => Summary?.Ductile;

    public IReadOnlyList<ConcreteQuickValue> MaterialValues => ConcreteQuickSection.MaterialValues(Data, Current);
    public IReadOnlyList<ConcreteQuickValue> ConcreteValues => ConcreteQuickSection.ConcreteValues(Current);
    public IReadOnlyList<ConcreteQuickValue> SteelValues => ConcreteQuickSection.SteelValues(Current);
    public IReadOnlyList<ConcreteQuickValue> ResultValues => ConcreteQuickSection.ResultValues(Current);
    public IReadOnlyList<string> Errors => Current?.Errors ?? [];
    public DomainChartGeometry? DomainGeometry => Current?.DomainGeometry;
    public DomainChartActions? DomainActions => Current?.DomainActions;
    public IReadOnlyList<double[]> CurvaturePoints => ConcreteQuickSection.CurvaturePoints(Current);

    /// <summary>The state of the calculation, in words.</summary>
    public string Status => CalculationState switch
    {
        CalculationState.Pending or CalculationState.Running => "Calcolo in corso con il motore della sezione in c.a.…",
        CalculationState.Incomplete or CalculationState.Failed => "Verifica non disponibile: " + CalculationMessage,
        _ when Current is not null => "Risultati del motore della sezione in c.a. riferiti ai dati correnti.",
        _ => "I risultati si aggiornano da soli dopo ogni modifica."
    };

    protected override void OnCalculationPublished() => RefreshResults();

    private void RefreshResults()
    {
        foreach (string p in new[] { nameof(Current), nameof(Summary), nameof(IsDuctile), nameof(MaterialValues), nameof(ConcreteValues), nameof(SteelValues), nameof(ResultValues), nameof(Errors), nameof(DomainGeometry),
            nameof(DomainActions), nameof(CurvaturePoints), nameof(SectionStress), nameof(Status) })
            OnPropertyChanged(p);
        foreach (var row in CombinationRows) row.Refresh();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(CanEdit))
        {
            foreach (var form in Forms) form.IsReadOnly = !CanEdit;
            if (!building && LayerRows.Count > 0) BuildLayers();
            if (!building && CombinationRows.Count > 0) BuildCombinations();
            AddLayerCommand?.NotifyCanExecuteChanged();
            AddCombinationCommand?.NotifyCanExecuteChanged();
        }
        if (e.PropertyName is nameof(Revision) or nameof(Result)) RefreshResults();
        if (e.PropertyName is nameof(CalculationState) or nameof(CalculationMessage)) OnPropertyChanged(nameof(Status));
    }

    protected override void OnCommit()
    {
        foreach (var form in Forms) form.Commit();
        ConcreteQuickSection.Validate(Data);
    }

    protected override void OnReloaded() { BuildForms(); RefreshDrawing(); }

    /// <summary>The quick section has no report of its own in this version.</summary>
    private sealed class NoReport : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) => throw new InvalidOperationException(ConcreteQuickSection.NoReport);
    }
}
