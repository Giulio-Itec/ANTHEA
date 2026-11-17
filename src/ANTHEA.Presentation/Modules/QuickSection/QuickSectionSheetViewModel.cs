using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.QuickSection;

/// <summary>A row of the table of the rings of the circular section: the switch of the second ring, number, diameter and clear distance (null when the row has none).</summary>
public sealed record QuickBarRow(string Title, FieldViewModel? Enabled, FieldViewModel Count, FieldViewModel Diameter, FieldViewModel? Gap);

/// <summary>A row of the table of the actions of the quick section: N, Mx and My.</summary>
public sealed record QuickActionRow(string Title, FieldViewModel N, FieldViewModel Mx, FieldViewModel My);

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
    public string Position { get => layer.S("posizione"); set => Write("posizione", value); }
    public string Distance { get => layer.S("d"); set => Write("d", value); }
    public string Count { get => layer.S("n"); set => Write("n", value); }
    public string Diameter { get => layer.S("phi"); set => Write("phi", value); }
    /// <summary>The distance d is used only by the layer «Da sopra».</summary>
    public bool UsesDistance => Position == ConcreteQuickSection.LayerPositions[2];
    public IRelayCommand RemoveCommand { get; }

    private void Write(string key, string? value, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        if (value is not null && value != layer.S(key)) owner.EditLayer(Index, key, value);
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

    private JsonObject ultimate = new(), service = new();
    private bool building;
    private ConcreteGeometry? section;
    private string drawingMessage = "";
    private bool showSection = true;

    public QuickSectionSheetViewModel(SheetContext context) : base(ConcreteQuickSection.Module, context, RecalculationDelay)
    {
        ConcreteQuickSection.Validate(Data);
        Report = new NoReport();
        AddLayerCommand = GuardedCommands.Create(Guard, AddLayer, () => CanEdit && !IsCircular);
        CoverHelpCommand = GuardedCommands.Create(Guard, () => Context.Services.Messages.Inform(ConcreteQuickSection.CoverHelpTitle, ConcreteQuickSection.CoverHelp));
        BuildForms();
        RefreshDrawing();
        // As Spectra: the sheet calculates at opening, without modifying the document; after an edit the automatic recalculation follows.
        _ = ScheduleCalculationAsync();
    }

    public override IModuleReportSource Report { get; }

    private JsonObject Input => Data["input"]!.AsObject();
    private JsonObject View => ConcreteQuickSection.View(Data);

    // ---------------------------------------------------------------- choices

    public IReadOnlyList<string> Shapes => ConcreteQuickSection.Shapes;
    /// <summary>The checks on screen: positive moment, negative moment (the SLU), method n.</summary>
    public IReadOnlyList<string> Methods => ConcreteQuickSection.MethodLabels;
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

    public bool IsCircular => Shape == ConcreteQuickSection.Shapes[2];

    /// <summary>The check: «Momento positivo», «Momento negativo» or «Metodo n» (codes SLU+, SLU−, Metodo n in the document).</summary>
    public string Method
    {
        get => ConcreteQuickSection.MethodLabel(View.S("metodo"));
        set => Choose("metodo", ConcreteQuickSection.Methods.ElementAtOrDefault(ConcreteQuickSection.MethodLabels.IndexOf(value)), ConcreteQuickSection.Methods);
    }

    /// <summary>The resistance model of the ultimate check: «Plastica» or «Elastica», written where the RC section reads it.</summary>
    public string Model
    {
        get => ConcreteQuickSection.ModelLabel(ConcreteQuickSection.Model(Data));
        set
        {
            string? model = ConcreteQuickSection.Models.ElementAtOrDefault(ConcreteQuickSection.ModelLabels.IndexOf(value));
            if (model is not null && model != ConcreteQuickSection.Model(Data)) Edit(d => ConcreteQuickSection.WriteModel(d, model));
            OnPropertyChanged();
        }
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
    public bool IsServiceMethod => View.S("metodo") == ConcreteQuickSection.Methods[2];
    public bool ShowsDomain => Chart == ConcreteQuickSection.Charts[0];

    private bool Choose(string key, string? value, IReadOnlyList<string> choices, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        if (value is null || View.S(key) == value || !choices.Contains(value) || !Edit(d => ConcreteQuickSection.View(d)[key] = value))
        {
            OnPropertyChanged(property); return false;
        }
        OnPropertyChanged(property);
        foreach (string p in new[] { nameof(IsStraight), nameof(IsServiceMethod), nameof(ShowsDomain) }) OnPropertyChanged(p);
        return true;
    }

    // ---------------------------------------------------------------- forms

    public FormViewModel GeometryForm { get; private set; } = null!;
    public FormViewModel BarsForm { get; private set; } = null!;
    public FormViewModel StirrupForm { get; private set; } = null!;
    public FormViewModel MaterialsForm { get; private set; } = null!;
    public FormViewModel UltimateForm { get; private set; } = null!;
    public FormViewModel ServiceForm { get; private set; } = null!;
    private IEnumerable<FormViewModel> Forms => new[] { GeometryForm, BarsForm, StirrupForm, MaterialsForm, UltimateForm, ServiceForm }.Where(f => f is not null);

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

    /// <summary>The rows of the table of the actions: the ultimate action and the action of the method n.</summary>
    public IReadOnlyList<QuickActionRow> ActionRows { get; private set; } = [];

    /// <summary>The creep coefficient of the serviceability checks of the RC section (method n), when the data have it.</summary>
    public FieldViewModel? PhiField { get; private set; }

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
            BarsForm = Form(Input, shape == ConcreteQuickSection.Shapes[2]
                ? [BarCount("longitudinal_bar_count", "Barre sull'anello"), BarDiameter("longitudinal_bar_diameter_mm", "Diametro delle barre"),
                    B("second_inner_enabled", "Secondo anello"), BarCount("second_inner_count", "Barre del secondo anello"), BarDiameter("second_inner_diameter", "Diametro del secondo anello"),
                    T("second_inner_gap", "s", "mm", "Distanza libera fra gli anelli")]
                : [], InputEdited);
            BarRows = shape == ConcreteQuickSection.Shapes[2]
                ? [new("Anello", null, BarsForm["longitudinal_bar_count"], BarsForm["longitudinal_bar_diameter_mm"], null),
                    new("2° anello", BarsForm["second_inner_enabled"], BarsForm["second_inner_count"], BarsForm["second_inner_diameter"], BarsForm["second_inner_gap"])]
                : [];
            BuildLayers();
            MaterialsForm = Form(Input, [new("classe_cls", "Calcestruzzo", Choices: ConcreteQuickSection.MaterialChoices(Data, "classe_cls"), ToolTip: "Classe del calcestruzzo"),
                new("classe_acciaio", "Acciaio", Choices: ConcreteQuickSection.MaterialChoices(Data, "classe_acciaio"), ToolTip: "Classe dell'acciaio delle barre")], InputEdited);
            ultimate = ConcreteQuickSection.ActionValues(Data, "SLU");
            service = ConcreteQuickSection.ActionValues(Data, "SLE");
            UltimateForm = Form(ultimate, ActionFields("Ed"), key => ActionEdited("SLU", ultimate, key));
            var common = Data["workspace_ca"]?["sle_comuni"] as JsonObject;
            var serviceFields = ActionFields("");
            if (common is not null) serviceFields = [.. serviceFields, new("phi", "φ", ToolTip: "Coefficiente di viscosità delle verifiche SLE della sezione in c.a. (metodo n)", Source: common)];
            ServiceForm = Form(service, serviceFields, key => { if (key == "phi") InputEdited(key); else ActionEdited("SLE", service, key); });
            ActionRows = [new("SLU", UltimateForm["N"], UltimateForm["Mx"], UltimateForm["My"]), new("Metodo n", ServiceForm["N"], ServiceForm["Mx"], ServiceForm["My"])];
            PhiField = ServiceForm.Fields.FirstOrDefault(f => f.Key == "phi");
        }
        finally { building = false; }
        foreach (string p in new[] { nameof(GeometryForm), nameof(BarsForm), nameof(StirrupForm), nameof(StirrupField), nameof(MaterialsForm), nameof(UltimateForm), nameof(ServiceForm),
            nameof(BarRows), nameof(ActionRows), nameof(PhiField), nameof(Shape), nameof(IsCircular), nameof(Method), nameof(Model), nameof(Bending), nameof(Chart), nameof(Title),
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

    private FieldSpec[] ActionFields(string suffix) =>
    [
        T("N", "N" + suffix, "kN", "N" + suffix + " (negativo a compressione)"), T("Mx", "Mx" + suffix, "kNm", "Mx" + suffix),
        new("My", "My" + suffix, "kNm", ReadOnly: IsStraight, ToolTip: IsStraight ? "Flessione retta: My non è usato" : "My" + suffix)
    ];

    private void InputEdited(string key)
    {
        if (building || IsDisposed) return;
        if (key is "classe_cls" or "classe_acciaio") ConcreteQuickSection.ApplyMaterial(Data, key);
        // The bars of the layers depend on the dimensions, the cover and the links.
        ConcreteQuickSection.ApplyLayers(Data);
        Changed();
        RefreshDrawing();
    }

    private void ActionEdited(string state, JsonObject values, string key)
    {
        if (building || IsDisposed || !CanEdit) return;
        ConcreteQuickSection.WriteAction(Data, state, key, values.S(key));
        Changed();
    }

    // ---------------------------------------------------------------- drawing

    /// <summary>The section drawn, from the geometry of the engine; null when the data do not describe a section.</summary>
    public ConcreteGeometry? Section => section;

    /// <summary>The links drawn on the section: the scheme of the shear of the RC section in the data.</summary>
    public JsonObject? Stirrups => Data["workspace_ca"]?["taglio"] as JsonObject;

    /// <summary>Why the section cannot be drawn.</summary>
    public string DrawingMessage => drawingMessage;

    /// <summary>The stresses drawn on the section: the method n, or the state at the resistant point of the ultimate check.</summary>
    public ConcreteResults.StressState? SectionStress => Current is { } r ? (IsServiceMethod ? r.ServiceState : r.UltimateState) : null;

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
        foreach (string p in new[] { nameof(Current), nameof(Summary), nameof(IsDuctile), nameof(MaterialValues), nameof(ResultValues), nameof(Errors), nameof(DomainGeometry),
            nameof(DomainActions), nameof(CurvaturePoints), nameof(SectionStress), nameof(Status) })
            OnPropertyChanged(p);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(CanEdit))
        {
            foreach (var form in Forms) form.IsReadOnly = !CanEdit;
            if (!building && LayerRows.Count > 0) BuildLayers();
            AddLayerCommand?.NotifyCanExecuteChanged();
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
