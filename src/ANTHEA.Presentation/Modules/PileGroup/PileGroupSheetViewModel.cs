using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Modules.PileGroup;

/// <summary>
/// Sheet of the horizontal pile group (geo_efficienza_orizzontale), the F5.4 pilot: the behaviour of the historic view
/// HorizontalPileGroupWorkspace without WPF, on the same document and with the same writes, in the same order.
/// <list type="bullet">
/// <item>Opening: <see cref="HorizontalPileGroupEditor.Upgrade"/> (archives of version 1), the object of the representative
/// spacings of the shown direction created when missing (a write without Modified), then the calculation at once.</item>
/// <item>Fields: written at every change with the rule of the historic view: a number when the text is a number, otherwise the
/// text (<see cref="FieldWriteRule.NumberOrText"/>); the two representative spacings are optional and their key is removed when
/// emptied (<see cref="FieldWriteRule.NumberOrTextRemoveEmpty"/>); options as JSON booleans.</item>
/// <item>Layout: choosing it writes generatore.tipo and rebuilds the fields of the generator. The coordinates of the table are
/// written into «pali» only with the generic layout (numbers when the texts are numbers); otherwise they are regenerated before
/// every calculation and the table is reloaded.</item>
/// <item>Calculation (decision Q8 a, <see cref="SheetCalculationMode.Synchronous"/>): on the UI thread through the
/// <see cref="IModuleService"/> of the context, at opening, at «Calcola», at Commit when a recalculation is pending or there is no
/// result, and 180 ms after the last edit. Before: the table and <see cref="HorizontalPileGroupEditor.Regenerate"/>; after:
/// <see cref="ModuleSheetPreparation.ApplyPileGroupSelection"/> writes the selected direction and its angle into the data. An
/// error leaves no result («Dati da completare: …»).</item>
/// <item>Comparison: selecting a row writes metodo, direzione_selezionata and angolo, changes the selected result in place
/// (<see cref="HorizontalPileGroupEditor.Select"/>) and raises Modified, without recalculating; allowed also when read-only.</item>
/// <item>View state not saved: the direction of the representative spacings shown.</item>
/// <item>Read-only (a previous revision): layout, cap, spacing direction, cells and the cancelled cell edit refuse the changes;
/// the forms are rebuilt on detached copies, because <see cref="FormViewModel"/> has no read-only state of its own. Selection and
/// calculation stay available.</item>
/// </list>
/// Numbers and rules of the texts come from <see cref="PileGroupPresentation"/>, shared with the historic view. The equivalence
/// with that view is proved without WPF by tests/ANTHEA.Presentation.Tests (PileGroupSheetChecks) against its traces frozen in
/// tests/fixtures/f5-palificata and against the F3 reference.
/// </summary>
public sealed class PileGroupSheetViewModel : SheetViewModelBase
{
    /// <summary>Interface state that the sheet writes into its data (<see cref="ModulePersistence"/>): the selected result.</summary>
    public static IReadOnlyList<string> UiStateKeys { get; } = ["metodo", "direzione_selezionata", "angolo"];

    /// <summary>Delay of the automatic recalculation after an edit, as the historic view.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(180);

    private const string Generic = "Generica";
    private const string Updating = "Aggiornamento automatico…";
    private bool loading = true, selecting, calculating;
    private string? selectedLayout, selectedCapShape;
    private string spacingDirection = "X";
    private int spacingIndex;
    private bool isGeneric;
    private string coordinateHelp = "", generatorNote = "";
    private FormViewModel? diameterForm, generatorForm, directionsForm, capForm, optionsForm, spacingForm;
    private string title = "", state = "", minimum = "—", mean = "—", maximum = "—", reduction = "—", geometry = "", source = "", notes = "";
    private IReadOnlyList<ComparisonRowViewModel> comparison = [];
    private ComparisonRowViewModel? selectedComparison;
    private IReadOnlyList<PileDetailRowViewModel> details = [];
    private PileGroupPlanInput? plan;
    private (ComparisonRowViewModel[] Rows, ComparisonRowViewModel Selected)? calculated;

    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the pile group.</param>
    public PileGroupSheetViewModel(SheetContext context)
        : base(HorizontalPileGroupEditor.Module, context, RecalculationDelay, SheetCalculationMode.Synchronous)
    {
        Report = new PileGroupReport(this);
        Piles.CollectionChanged += OnPilesChanged;
        Build();
        loading = false;
        CalculateNow();
    }

    public override IModuleReportSource Report { get; }

    // ---------------------------------------------------------------- input

    /// <summary>The layouts of the generator.</summary>
    public IReadOnlyList<string> Layouts { get; } = HorizontalPileGroupEditor.LayoutNames;

    /// <summary>Layout of the generator; null when the data name none of <see cref="Layouts"/>.</summary>
    public string? SelectedLayout
    {
        get => selectedLayout;
        set
        {
            if (value == selectedLayout) return;
            if (!CanEdit || value is null || !Layouts.Contains(value)) { OnPropertyChanged(); return; }
            selectedLayout = value;
            OnPropertyChanged();
            Data["generatore"]!["tipo"] = value;
            BuildGenerator(keepTexts: false);
            Queue();
        }
    }

    /// <summary>Common diameter D of the piles.</summary>
    public FormViewModel DiameterForm => diameterForm!;

    /// <summary>Fields of the layout; null with the generic layout, whose coordinates are typed in the table.</summary>
    public FormViewModel? GeneratorForm => generatorForm;

    /// <summary>Note under the fields of the rings (pentagonal and hexagonal layouts); empty otherwise.</summary>
    public string GeneratorNote { get => generatorNote; private set => SetProperty(ref generatorNote, value); }

    /// <summary>Coordinates of the piles: typed with the generic layout, regenerated otherwise.</summary>
    public ObservableCollection<PileRowViewModel> Piles { get; } = [];

    /// <summary>The coordinates are typed in the table (generic layout).</summary>
    public bool IsGeneric
    {
        get => isGeneric;
        private set { if (SetProperty(ref isGeneric, value)) OnPropertyChanged(nameof(CanEditCoordinates)); }
    }

    /// <summary>The table accepts edits, new rows and deletions: generic layout and an editable sheet.</summary>
    public bool CanEditCoordinates => isGeneric && CanEdit;

    /// <summary>Help under the table.</summary>
    public string CoordinateHelp { get => coordinateHelp; private set => SetProperty(ref coordinateHelp, value); }

    /// <summary>Directions of the load: +X, +Y, −X, −Y, the custom one and its angle (enabled only with the custom direction).</summary>
    public FormViewModel DirectionsForm => directionsForm!;

    /// <summary>Shapes of the cap.</summary>
    public IReadOnlyList<string> CapShapes { get; } = ["Contorno palificata", "Rettangolare"];

    /// <summary>Shape of the cap; null when the data name none of <see cref="CapShapes"/>.</summary>
    public string? SelectedCapShape
    {
        get => selectedCapShape;
        set
        {
            if (value == selectedCapShape) return;
            if (!CanEdit || value is null || !CapShapes.Contains(value)) { OnPropertyChanged(); return; }
            selectedCapShape = value;
            OnPropertyChanged();
            Data["basamento"]!["tipo"] = value;
            Queue();
        }
    }

    /// <summary>Distance between the axis of the pile and the edge of the cap, in diameters.</summary>
    public FormViewModel CapForm => capForm!;

    /// <summary>Options of applicability: extrapolations and the extension to projected rows.</summary>
    public FormViewModel OptionsForm => optionsForm!;

    /// <summary>Directions of the representative spacings, in the order of <see cref="SpacingDirectionIndex"/>.</summary>
    public IReadOnlyList<string> SpacingDirections { get; } = ["+X", "+Y", "−X", "−Y", "Personalizzata"];

    /// <summary>Direction whose representative spacings are shown (view state, not saved): 0 +X … 4 custom.</summary>
    public int SpacingDirectionIndex
    {
        get => spacingIndex;
        set
        {
            if (value == spacingIndex) return;
            if (!CanEdit || value < 0 || value >= SpacingDirections.Count) { OnPropertyChanged(); return; }
            spacingIndex = value;
            spacingDirection = SpacingId(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SpacingDirection));
            BuildSpacing(keepTexts: false);
        }
    }

    /// <summary>Key of the shown direction in «interassi»: X, Y, X-, Y- or custom.</summary>
    public string SpacingDirection => spacingDirection;

    /// <summary>Representative spacings S∥ and S⊥ of the shown direction; empty means automatic (the key is removed).</summary>
    public FormViewModel SpacingForm => spacingForm!;

    /// <summary>
    /// The view cancelled the edit of a cell of the table (Esc): the table is compared with the data again and the sheet
    /// recalculates, as the historic view did after the cancellation.
    /// </summary>
    public void CoordinateEditCancelled()
    {
        if (!CanEdit) return;
        CapturePiles();
        Queue();
    }

    // ---------------------------------------------------------------- output

    /// <summary>Method and direction of the selected result, or the title of the sheet without result.</summary>
    public string Title { get => title; private set => SetProperty(ref title, value); }

    /// <summary>Piles and quantity reduced, or the state of the calculation («Aggiornamento automatico…», «Dati da completare: …»).</summary>
    public string State { get => state; private set => SetProperty(ref state, value); }

    public string Minimum { get => minimum; private set => SetProperty(ref minimum, value); }

    public string Mean { get => mean; private set => SetProperty(ref mean, value); }

    public string Maximum { get => maximum; private set => SetProperty(ref maximum, value); }

    public string Reduction { get => reduction; private set => SetProperty(ref reduction, value); }

    /// <summary>Spacings of the selected result and distances of the cap, on two lines.</summary>
    public string Geometry { get => geometry; private set => SetProperty(ref geometry, value); }

    /// <summary>Source of the selected method.</summary>
    public string Source { get => source; private set => SetProperty(ref source, value); }

    /// <summary>Error of the method, error of the cap and warnings, one per line.</summary>
    public string Notes { get => notes; private set => SetProperty(ref notes, value); }

    /// <summary>Every method in every direction.</summary>
    public IReadOnlyList<ComparisonRowViewModel> Comparison { get => comparison; private set => SetProperty(ref comparison, value); }

    /// <summary>The selected row of the comparison; choosing another row selects that result (see the class).</summary>
    public ComparisonRowViewModel? SelectedComparison
    {
        get => selectedComparison;
        set
        {
            if (ReferenceEquals(selectedComparison, value)) return;
            selectedComparison = value;
            OnPropertyChanged();
            if (selecting || Result is not { } result || value is null || IsDisposed) return;
            Data["metodo"] = value.Method; Data["direzione_selezionata"] = value.DirectionId; Data["angolo"] = PileGroupPresentation.Angle(value.Node);
            HorizontalPileGroupEditor.Select(result, value.Method, value.DirectionId);
            Present();
            ChangedKeepingResults();
        }
    }

    /// <summary>Values per pile of the selected result.</summary>
    public IReadOnlyList<PileDetailRowViewModel> Details { get => details; private set => SetProperty(ref details, value); }

    /// <summary>What the drawing of the plan shows; null before the first calculation.</summary>
    public PileGroupPlanInput? Plan { get => plan; private set => SetProperty(ref plan, value); }

    // ---------------------------------------------------------------- sheet

    /// <summary>The Commit of the historic view: the table into the data, then the calculation if one is pending or missing.</summary>
    protected override void OnCommit()
    {
        if (CapturePiles()) Queue();
        if (!calculating && (!PendingCalculation.IsCompleted || Result is null)) CalculateNow();
    }

    /// <summary>The data were replaced: the sheet is built again on them, as at opening; the results wait for the automatic recalculation.</summary>
    protected override void OnReloaded()
    {
        loading = true;
        try { Build(); }
        finally { loading = false; }
        Clear(Updating);
    }

    /// <summary>CalculateCurrent of the historic view: table, regenerated layout, calculation, selection written into the data, rows.</summary>
    protected override ModuleCalculation CalculateSynchronously()
    {
        calculated = null;
        calculating = true;
        try
        {
            CapturePiles();
            HorizontalPileGroupEditor.Regenerate(Data);
            if (Data["generatore"].S("tipo") != Generic) LoadPiles();
            var calculation = CalculateModule(Data);
            ModuleSheetPreparation.ApplyPileGroupSelection(Data, calculation.Values);
            var rows = calculation.Values.Array("confronto").Select(n => new ComparisonRowViewModel(n!)).ToArray();
            calculated = (rows, rows.First(r => PileGroupPresentation.IsSelected(r.Node, Data)));
            return calculation;
        }
        finally { calculating = false; }
    }

    protected override void OnCalculationPublished()
    {
        if (Result is null || calculated is not { } shown) { Clear("Dati da completare: " + CalculationMessage); return; }
        selecting = true;
        try { Comparison = shown.Rows; SelectedComparison = shown.Selected; }
        finally { selecting = false; }
        Present();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(CanEdit)) return;
        OnPropertyChanged(nameof(CanEditCoordinates));
        if (loading || IsDisposed) return;
        foreach (var row in Piles) row.Locked = !CanEdit;
        BuildForms(keepTexts: true);
    }

    protected override void OnDisposed()
    {
        foreach (var row in Piles) row.Changed = null;
        Piles.CollectionChanged -= OnPilesChanged;
        base.OnDisposed();
    }

    // ---------------------------------------------------------------- construction

    private void Build()
    {
        HorizontalPileGroupEditor.Upgrade(Data);
        string kind = Data["generatore"].S("tipo");
        selectedLayout = Layouts.Contains(kind) ? kind : null;
        OnPropertyChanged(nameof(SelectedLayout));
        string cap = Data["basamento"].S("tipo");
        selectedCapShape = CapShapes.Contains(cap) ? cap : null;
        OnPropertyChanged(nameof(SelectedCapShape));
        BuildForms(keepTexts: false);
        LoadPiles();
    }

    /// <summary>The forms of the sheet on the current data: on detached copies when read-only; with the texts shown before when kept.</summary>
    private void BuildForms(bool keepTexts)
    {
        var shown = keepTexts ? Texts(diameterForm, directionsForm, capForm, optionsForm) : null;
        Set(ref diameterForm, Form(Data, [Number("diametro", "Diametro comune D [m]")], shown), nameof(DiameterForm));
        BuildGenerator(keepTexts);
        var directions = Data["direzioni"]!.AsObject();
        Set(ref directionsForm, Form(directions, [Option("X", "+X · 0°"), Option("Y", "+Y · 90°"), Option("X-", "−X · 180°"), Option("Y-", "−Y · 270°"),
            Option("custom", "Aggiungi direzione personalizzata"), Number("angolo_custom", "Angolo [° antiorari da +X]")], shown), nameof(DirectionsForm));
        DirectionsForm.Enable("angolo_custom", directions.B("custom"));
        Set(ref capForm, Form(Data["basamento"]!.AsObject(), [Number("bordo_d", "Distanza asse palo–bordo / D")], shown), nameof(CapForm));
        Set(ref optionsForm, Form(Data, [Option("estrapolazioni", "Consenti estrapolazioni"), Option("file_proiettate", "Accetto l’estensione per file proiettate")], shown),
            nameof(OptionsForm));
        BuildSpacing(keepTexts);
    }

    /// <summary>BuildGenerator of the historic view: the fields of the layout, none for the generic one.</summary>
    private void BuildGenerator(bool keepTexts)
    {
        var shown = keepTexts ? Texts(generatorForm) : null;
        var g = Data["generatore"]!.AsObject();
        string kind = g.S("tipo");
        bool generic = kind == Generic;
        IsGeneric = generic;
        CoordinateHelp = generic ? "Modifica x e y, aggiungi una riga o elimina con Canc. Ricalcolo automatico."
            : "Coordinate automatiche. Scegli Generica per modificarle dalla tabella.";
        if (generic) { Set(ref generatorForm, null, nameof(GeneratorForm)); GeneratorNote = ""; return; }
        var fields = new List<FieldSpec>();
        bool rings = false;
        if (kind is "Rettangolare" or "Quinconce")
            fields.AddRange([Number("nx", kind == "Quinconce" ? "Pali file lunghe N (corte N−1)" : "Pali per fila Nx"), Number("ny", "Numero di file Ny"),
                Number("sx_d", "Interasse nella fila Sx / D"), Number("sy_d", "Interasse fra file Sy / D")]);
        else if (kind == "Triangolare") fields.AddRange([Number("lato", "Numero di pali su ogni lato"), Number("sx_d", "Interasse triangolare S / D")]);
        else
        {
            rings = true;
            fields.AddRange([Number("anelli", "Numero di anelli concentrici"), Number("suddivisioni", "Suddivisioni/lato primo anello"),
                Number("sx_d", "Passo lungo il lato S / D"), Option("centro", "Palo al centro")]);
        }
        fields.Add(Number("rotazione", "Rotazione palificata [° da +X]"));
        Set(ref generatorForm, Form(g, fields, shown), nameof(GeneratorForm));
        GeneratorNote = rings ? "L’anello k ha k volte le suddivisioni del primo; il passo lungo il lato resta costante." : "";
    }

    /// <summary>BuildSpacing of the historic view: the object of the shown direction is created when missing, without Modified.</summary>
    private void BuildSpacing(bool keepTexts)
    {
        var shown = keepTexts ? Texts(spacingForm) : null;
        var spacings = Data["interassi"]!.AsObject();
        if (spacings[spacingDirection] is not JsonObject) spacings[spacingDirection] = new JsonObject();
        Set(ref spacingForm, Form(spacings[spacingDirection]!.AsObject(), [Optional("interasse_parallelo", "S∥ · tra file lungo H [m]"),
            Optional("interasse_trasversale", "S⊥ · ortogonale a H [m]")], shown), nameof(SpacingForm));
    }

    private void Set(ref FormViewModel? field, FormViewModel? value, string name)
    {
        field = value;
        OnPropertyChanged(name);
    }

    private static FieldSpec Number(string key, string label) => new(key, label, Write: FieldWriteRule.NumberOrText);

    private static FieldSpec Optional(string key, string label) => new(key, label, Write: FieldWriteRule.NumberOrTextRemoveEmpty);

    private static FieldSpec Option(string key, string label) => new(key, label, Bool: true);

    private static string SpacingId(int index) => index switch { 0 => "X", 1 => "Y", 2 => "X-", 3 => "Y-", _ => "custom" };

    /// <summary>Texts of the text fields of <paramref name="forms"/>, by key.</summary>
    private static Dictionary<string, string> Texts(params FormViewModel?[] forms) => forms.Where(f => f is not null)
        .SelectMany(f => f!.Fields).Where(f => f.Kind == FieldKind.Text).ToDictionary(f => f.Key, f => f.Text, StringComparer.Ordinal);

    /// <summary>
    /// A form on <paramref name="values"/> that writes with the rules of its fields and queues the recalculation. Read-only: on a
    /// detached copy and without callback, so that typing never reaches the data. <paramref name="shown"/>: texts shown again
    /// (a typed «1,2» stays as typed, not as the saved 1.2), without writing.
    /// </summary>
    private FormViewModel Form(JsonObject values, IEnumerable<FieldSpec> fields, IReadOnlyDictionary<string, string>? shown)
    {
        var form = CanEdit ? new FormViewModel(values, fields, OnFieldChanged, wikiModule: ModuleId)
            : new FormViewModel((JsonObject)values.DeepClone(), fields, wikiModule: ModuleId);
        if (shown is not null)
            foreach (var field in form.Fields.Where(f => f.Kind == FieldKind.Text && shown.ContainsKey(f.Key))) form.Set(field.Key, shown[field.Key], display: true);
        return form;
    }

    private void OnFieldChanged(string key)
    {
        Queue();
        if (key == "custom") DirectionsForm.Enable("angolo_custom", DirectionsForm["custom"].IsChecked);
    }

    // ---------------------------------------------------------------- coordinates

    /// <summary>LoadPiles of the historic view: the table from «pali», without writing.</summary>
    private void LoadPiles()
    {
        bool before = loading;
        loading = true;
        try
        {
            foreach (var row in Piles) row.Changed = null;
            Piles.Clear();
            foreach (var p in Data.Array("pali"))
                Piles.Add(new PileRowViewModel { Id = p.S("id"), X = p!["x"]!.ToString(), Y = p["y"]!.ToString() });
        }
        finally { loading = before; }
    }

    private void OnPilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (PileRowViewModel row in e.NewItems) { row.Changed = OnPileEdited; row.Locked = !CanEdit; }
        if (!loading) OnPileEdited();
    }

    private void OnPileEdited()
    {
        CapturePiles();
        Queue();
    }

    /// <summary>CapturePiles of the historic view: with the generic layout, the table into «pali» when it differs.</summary>
    private bool CapturePiles()
    {
        if (loading || Data["generatore"].S("tipo") != Generic) return false;
        var rows = new JsonArray(Piles.Select(p => (JsonNode)J.Obj(("id", p.Id), ("x", ModuleSheetPreparation.NumericText(p.X)), ("y", ModuleSheetPreparation.NumericText(p.Y)))).ToArray());
        if (JsonNode.DeepEquals(rows, Data["pali"])) return false;
        Data["pali"] = rows;
        return true;
    }

    // ---------------------------------------------------------------- results

    /// <summary>Queue of the historic view: an edit of the user clears the results, raises Modified and schedules the recalculation.</summary>
    private void Queue()
    {
        if (loading || IsDisposed) return;
        Clear(Updating);
        Changed();
    }

    private void Clear(string message)
    {
        Title = "Effetto di gruppo della palificata"; State = message; Source = Notes = Geometry = "";
        Minimum = Mean = Maximum = Reduction = "—";
        selecting = true;
        try { Comparison = []; SelectedComparison = null; }
        finally { selecting = false; }
        Details = [];
        Plan = new PileGroupPlanInput(Data, null, null);
    }

    private void Present()
    {
        if (Result is not { } result) return;
        var selected = result["selezionato"]!;
        int method = PileGroupPresentation.Method(selected);
        bool available = PileGroupPresentation.Available(selected);
        static string F(double? value) => PileGroupPresentation.Format(value);
        Title = HorizontalPileGroupEditor.MethodNames[method] + " · " + selected.S("DirectionName") + " " + F(J.Number(selected["Angle"])) + "°";
        State = $"{Data.Array("pali").Count} pali · calcolo automatico · " + (available ? PileGroupPresentation.FactorDescription(method) : "metodo non applicabile: valori non disponibili");
        Minimum = F(J.Number(selected["Minimum"])); Mean = F(J.Number(selected["Factor"])); Maximum = F(J.Number(selected["Maximum"]));
        Reduction = F(J.Number(selected["ReductionPercent"])) + (available ? "%" : "");
        var (edge, margin) = PileGroupPresentation.CapDistances(Data);
        Geometry = "S∥ · distanza fra file lungo H: " + F(J.Number(selected["ParallelSpacing"])) + " m   |   S⊥ · interasse ortogonale a H: "
            + F(J.Number(selected["TransverseSpacing"])) + " m\n" + "Asse–bordo basamento = " + F(edge) + " m; margine libero dal palo = " + F(margin) + " m.";
        Source = selected.S("Source");
        Notes = string.Join("\n", new[] { selected.S("Error"), result.S("errore_basamento") }.Concat(selected.Array("Warnings").Select(w => w!.ToString())).Where(s => s.Length > 0));
        Details = selected.Array("Piles").Select(p => PileDetailRowViewModel.For(p!, method, available)).ToArray();
        Plan = new PileGroupPlanInput(Data, selected, result["basamento"] as JsonArray);
    }

    /// <summary>Report of the sheet: the existing generator of the pile group on the shown result (reports frozen on 8/10).</summary>
    private sealed class PileGroupReport(PileGroupSheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) =>
            DocxWriter.Write(ReportPileGroup.CreateDocument(title, sheet.Result ?? throw new InvalidOperationException("Calcolare prima di esportare.")));
    }
}
