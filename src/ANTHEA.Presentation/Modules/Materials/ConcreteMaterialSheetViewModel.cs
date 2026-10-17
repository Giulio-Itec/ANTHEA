using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Presentation.Forms;
using Materiali;
using X.Core;

namespace Anthea.Presentation.Modules.Materials;

/// <summary>
/// Sheet of the concrete material (mat_calcestruzzo), F5.5: the behaviour of the historic view Materiali.MaterialView without WPF,
/// with the same states, the same texts and the same writes.
/// <list type="bullet">
/// <item>Opening: the data are read through <see cref="MaterialSheetInput.NormalizeState"/> (archives of previous versions,
/// unknown keys dropped); an invalid sheet fails with the same messages. The data of the sheet do not change at opening.</item>
/// <item>Edits stay in the sheet until <see cref="SheetViewModelBase.Commit"/>, as in the historic view: <see cref="PreviewData"/>
/// is the state the sheet would write (the state of the controls of the view), <see cref="ISheetViewModel.Data"/> changes only at
/// Commit, which replaces it with that state. Modified is raised when the state changes, once per edit.</item>
/// <item>Fields: <see cref="Inputs"/>, one <see cref="FormViewModel"/> on the state of the sheet with the write rules of the
/// historic controls (raw text of the numbers, item of the choices, booleans of the options). The class, the deviation value
/// (whose items depend on the deviation control) and the exposure menu are explicit.</item>
/// <item>No calculation is published (<see cref="SheetCalculationMode.None"/>): «Calcola» only commits, as the historic sheet; the
/// values shown are the presentation of the state, <see cref="MaterialSheetPresentation"/> and
/// <see cref="MaterialExposurePresentation"/>, computed again after every edit.</item>
/// <item>Read-only (a previous revision): the edits are refused; the fields with <see cref="FormViewModel.IsReadOnly"/> of
/// <see cref="Inputs"/> (decision T2 of 17/10/2026, no detached copy). Commit still writes the state, as in the historic sheet.</item>
/// <item>Help: the «?» labels and F1 of the fields open the Wiki with the navigator of the services
/// (<see cref="FormViewModel.WikiNavigator"/>); the view places the fields with its own layout (decision M11).</item>
/// </list>
/// The equivalence with the historic view is proved without WPF by tests/ANTHEA.Presentation.Tests (MaterialSheetChecks) against
/// its traces frozen in tests/fixtures/f5-materiali and against the F3 reference.
/// </summary>
public sealed class ConcreteMaterialSheetViewModel : SheetViewModelBase, IMaterialSheetViewModel
{
    /// <summary>The module of the sheet.</summary>
    public const string Module = "mat_calcestruzzo";

    /// <summary>The choice of the deviation value, whose items depend on the deviation control (explicit, outside <see cref="Inputs"/>).</summary>
    public const string DeviationValueKey = "deviationValue";

    private const string DeviationControlKey = "deviationControl";
    private readonly JsonObject inputs = new();
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    private FormViewModel inputsForm = null!;
    private string? selectedClass, selectedExposure, deviationValue;
    private IReadOnlyList<string> deviationValues = [];
    private int lastDeviationControl = -1;
    private JsonArray? legacy;
    private JsonObject? lastState;
    private bool loading;
    private string exposureDetails = "", exposureExamples = "", aggressiveness = "", combinedExposure = "";
    private bool combinedVisible, coverError, ntcCover;
    private string strength = "", tension = "", modulus = "", status = "", bondValue = "", bondDetails = "", minimumClass = "", minimumNote = "";
    private string coverHeadline = "", coverSteps = "", coverNotes = "", deviationNote = "";
    private string compositionSource = "", airNote = "", requirements = "", consistency = "";
    private IReadOnlyList<PropertyRow> properties = [];
    private IReadOnlyDictionary<string, string> composition = new Dictionary<string, string>();

    /// <param name="context">Data of the sheet (owned copy), host and services; the calculation service is never called.</param>
    /// <exception cref="ArgumentException">Data that the historic sheet refuses (version, class, choices, exposures).</exception>
    public ConcreteMaterialSheetViewModel(SheetContext context) : base(Module, context, mode: SheetCalculationMode.None)
    {
        Report = new MaterialSheetReport(this);
        Restore();
    }

    public override IModuleReportSource Report { get; }

    /// <summary>The state the sheet writes at Commit, with the edits not committed yet: a new object at every call.</summary>
    public override JsonObject PreviewData => CaptureState();

    // ---------------------------------------------------------------- input

    /// <summary>The classes of the menu, from the weakest.</summary>
    public IReadOnlyList<string> Classes { get; } = MaterialInputCatalog.MaterialSheetClasses().Select(c => c.Name).ToArray();

    /// <summary>The class of the concrete; a class outside <see cref="Classes"/> is ignored, as the menu of the historic view.</summary>
    public string? SelectedClass
    {
        get => selectedClass;
        set
        {
            if (value == selectedClass) return;
            if (!CanEdit || value is null || !Classes.Contains(value)) { OnPropertyChanged(); return; }
            selectedClass = value;
            OnPropertyChanged();
            Edited();
        }
    }

    /// <summary>
    /// Numbers (typed text), choices and options of the sheet, except the deviation value: one form on the state of the sheet, with
    /// the labels and units of the historic view; the view places each field by key.
    /// </summary>
    public FormViewModel Inputs => inputsForm;

    /// <summary>Items of the deviation value for the current deviation control.</summary>
    public IReadOnlyList<string> DeviationValues { get => deviationValues; private set => SetProperty(ref deviationValues, value); }

    /// <summary>The deviation value; only an item of <see cref="DeviationValues"/>, and only when the control allows a choice.</summary>
    public string? SelectedDeviationValue
    {
        get => deviationValue;
        set
        {
            if (value == deviationValue) return;
            if (!CanEdit || !CanChooseDeviationValue || value is null || !deviationValues.Contains(value)) { OnPropertyChanged(); return; }
            deviationValue = value;
            OnPropertyChanged();
            Edited();
        }
    }

    /// <summary>The deviation control allows a choice of the deviation value (not the ordinary execution).</summary>
    public bool CanChooseDeviationValue => MaterialSheetInput.DeviationChoosable(lastDeviationControl);

    /// <summary>The choice of the deviation value accepts the user: the control allows a choice and the sheet can be edited.</summary>
    public bool CanEditDeviationValue => CanEdit && CanChooseDeviationValue;

    /// <summary>Note under the deviation value, for the current deviation control.</summary>
    public string DeviationNote { get => deviationNote; private set => SetProperty(ref deviationNote, value); }

    /// <summary>Tooltip of the deviation note.</summary>
    public string DeviationToolTip => MaterialSheetPresentation.DeviationToolTip;

    /// <summary>The exposure classes of the menu, in the order of the catalogue.</summary>
    public IReadOnlyList<string> Exposures { get; } = MaterialSheetInput.ExposureChoices.Select(e => e.Code).ToArray();

    /// <summary>The exposure of the menu; choosing one makes it the only active exposure.</summary>
    public string? SelectedExposure
    {
        get => selectedExposure;
        set
        {
            if (value == selectedExposure) return;
            if (!CanEdit || value is null || !Exposures.Contains(value)) { OnPropertyChanged(); return; }
            selectedExposure = value;
            active.Clear(); active.Add(value);
            OnPropertyChanged();
            Edited();
        }
    }

    /// <summary>The exposures taken by the presentation, in the order of the catalogue: the one of the menu.</summary>
    public IReadOnlyList<string> ActiveExposures => Active().Select(e => e.Code).ToArray();

    // ---------------------------------------------------------------- output

    public string ExposureDetails { get => exposureDetails; private set => SetProperty(ref exposureDetails, value); }
    public string ExposureExamples { get => exposureExamples; private set => SetProperty(ref exposureExamples, value); }
    public string Aggressiveness { get => aggressiveness; private set => SetProperty(ref aggressiveness, value); }
    /// <summary>The active classes when more than one, or the exposures of an archive of a previous version.</summary>
    public string CombinedExposure { get => combinedExposure; private set => SetProperty(ref combinedExposure, value); }
    public bool IsCombinedExposureVisible { get => combinedVisible; private set => SetProperty(ref combinedVisible, value); }

    /// <summary>fck, fctm and Ecm of the cards.</summary>
    public string Strength { get => strength; private set => SetProperty(ref strength, value); }
    public string Tension { get => tension; private set => SetProperty(ref tension, value); }
    public string Modulus { get => modulus; private set => SetProperty(ref modulus, value); }
    /// <summary>Class and standards, in the footer.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }
    /// <summary>The table of the mechanical properties.</summary>
    public IReadOnlyList<PropertyRow> Properties { get => properties; private set => SetProperty(ref properties, value); }

    public string BondValue { get => bondValue; private set => SetProperty(ref bondValue, value); }
    public string BondDetails { get => bondDetails; private set => SetProperty(ref bondDetails, value); }

    /// <summary>The minimum class for the exposure (NTC criterion) and its note.</summary>
    public string MinimumConcreteClass { get => minimumClass; private set => SetProperty(ref minimumClass, value); }
    public string MinimumConcreteNote { get => minimumNote; private set => SetProperty(ref minimumNote, value); }

    public string CoverHeadline { get => coverHeadline; private set => SetProperty(ref coverHeadline, value); }
    /// <summary>The cover could not be calculated: the headline is shown in the colour of the errors.</summary>
    public bool CoverError { get => coverError; private set => SetProperty(ref coverError, value); }
    public string CoverSteps { get => coverSteps; private set => SetProperty(ref coverSteps, value); }
    public string CoverNotes { get => coverNotes; private set => SetProperty(ref coverNotes, value); }

    /// <summary>The cover follows NTC + Circolare (the options of the element are shown), otherwise EC2 (the reductions are shown).</summary>
    public bool IsNtcCover { get => ntcCover; private set => SetProperty(ref ntcCover, value); }

    /// <summary>Chloride class, w/c ratio, cement, air and Dmax of the prescription, by key («chloride», «ratio», «cement», «air», «dmax»).</summary>
    public IReadOnlyDictionary<string, string> Composition { get => composition; private set => SetProperty(ref composition, value); }
    public string CompositionSource { get => compositionSource; private set => SetProperty(ref compositionSource, value); }
    public string CompositionAirNote { get => airNote; private set => SetProperty(ref airNote, value); }
    public string CompositionChlorideNote => MaterialSheetPresentation.CompositionChlorideNote;
    public string CompositionRequirements { get => requirements; private set => SetProperty(ref requirements, value); }
    public string ConsistencyInfo { get => consistency; private set => SetProperty(ref consistency, value); }

    // ---------------------------------------------------------------- sheet

    /// <summary>Chapter of the material in a report: the sheet is committed first, as the historic sheet does.</summary>
    public ReportProject.SheetContent ReportContent()
    {
        Commit();
        return MaterialReportChapter.Create(Module, Data);
    }

    /// <summary>The Commit of the historic sheet: the data are replaced with the state of the sheet.</summary>
    protected override void OnCommit()
    {
        var state = CaptureState();
        Data.Clear();
        foreach (var pair in state) Data[pair.Key] = pair.Value?.DeepClone();
    }

    /// <summary>The data were replaced: the sheet reads them again as at opening, without Modified.</summary>
    protected override void OnReloaded() => Restore();

    /// <summary>Read-only revision and closing (T2): the same form refuses the typing; nothing is rebuilt.</summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(CanEdit) || inputsForm is null) return;
        inputsForm.IsReadOnly = !CanEdit;
        OnPropertyChanged(nameof(CanEditDeviationValue));
    }

    // ---------------------------------------------------------------- state

    /// <summary>
    /// RestoreState of the historic view on the normalized data: class, numbers, choices (the deviation value after its control),
    /// options, the exposure of the menu as the only active one, the exposures of a previous version; then the presentation.
    /// </summary>
    private void Restore()
    {
        var state = MaterialSheetInput.NormalizeState(Data);
        loading = true;
        try
        {
            legacy = state["esposizioni_precedenti"]?.DeepClone() as JsonArray;
            if (state["esposizioni"] is JsonArray old && old.Count > 1) legacy = (JsonArray)old.DeepClone();
            string name = state["classe"]!.GetValue<string>();
            if (!Classes.Contains(name)) throw new ArgumentException("Classe del calcestruzzo non riconosciuta.");
            selectedClass = name;
            inputs.Clear();
            foreach (string key in MaterialSheetInput.NumberKeys) inputs[key] = state["numeri"]![key]!.GetValue<string>();
            foreach (string key in MaterialSheetInput.ChoiceKeys.Where(k => k != DeviationValueKey))
            {
                string value = state["scelte"]![key]!.GetValue<string>();
                if (!MaterialSheetInput.Options(key).Contains(value)) throw new ArgumentException("Scelta materiali non valida: " + key);
                inputs[key] = value;
            }
            foreach (string key in MaterialSheetInput.FlagKeys) inputs[key] = state["opzioni"]![key]!.GetValue<bool>();
            lastDeviationControl = -1;
            UpdateDeviation();
            string deviation = state["scelte"]![DeviationValueKey]!.GetValue<string>();
            if (!deviationValues.Contains(deviation)) throw new ArgumentException("Scelta materiali non valida: " + DeviationValueKey);
            deviationValue = deviation;
            var codes = (state["esposizioni"] as JsonArray ?? []).Select(v => v!.GetValue<string>()).ToArray();
            if (codes.Any(c => !Exposures.Contains(c))) throw new ArgumentException("Classe di esposizione non riconosciuta.");
            string selected = state["esposizione_principale"]?.ToString() ?? codes.FirstOrDefault() ?? "XC1";
            if (!Exposures.Contains(selected)) throw new ArgumentException("Classe di esposizione non riconosciuta.");
            selectedExposure = selected;
            active.Clear(); active.Add(selected);
            BuildForm();
        }
        finally { loading = false; }
        foreach (string property in new[] { nameof(SelectedClass), nameof(SelectedDeviationValue), nameof(CanChooseDeviationValue), nameof(CanEditDeviationValue), nameof(SelectedExposure) })
            OnPropertyChanged(property);
        Refresh();
        lastState = CaptureState();
    }

    /// <summary>
    /// The form of the fields on the state of the sheet (at opening and after Reload): read-only while the sheet cannot be edited
    /// (T2), with the Wiki of the services for «?» and F1.
    /// </summary>
    private void BuildForm()
    {
        inputsForm = new FormViewModel(inputs, Fields(), OnFieldChanged, wikiModule: Module) { IsReadOnly = !CanEdit, WikiNavigator = Context.Services.Wiki };
        OnPropertyChanged(nameof(Inputs));
    }

    /// <summary>The fields of the historic view, with its labels and units, in the order of the persisted state.</summary>
    private static IEnumerable<FieldSpec> Fields()
    {
        FieldSpec Choice(string key, string label) => new(key, label, Choices: MaterialSheetInput.Options(key).ToArray());
        FieldSpec Number(string key, string label, string unit) => new(key, label, unit);
        FieldSpec Option(string key, string label) => new(key, label, Bool: true);
        return [
            Choice("ntcElement", "Tipo di elemento"), Choice("coverMethod", "Criterio copriferro"), Choice("life", "Vita utile di progetto"),
            Number("diameter", "Diametro della barra da verificare · φ", "mm"), Number("aggregate", "Dimensione massima aggregato · Dmax", "mm"),
            Choice(DeviationControlKey, "Controllo di esecuzione"), Choice("ground", "Superficie di getto"), Choice("abrasion", "Strato per abrasione"),
            Choice("consistency", "Consistenza al getto"), Choice("cement", "Famiglia del cemento"), Number("cementName", "Designazione / produttore", ""),
            Choice("cementClass", "Classe del cemento"), Choice("cementEarly", "Resistenza iniziale"),
            Choice("bondCondition", "Condizioni di aderenza"), Number("bondAlpha", "Coefficiente di trazione · αct", "—"),
            Number("bondGamma", "Coefficiente parziale · γc", "—"),
            Option("highStrength", "Riduzione per classe di resistenza (tab. 4.3N)"), Option("slab", "Geometria a piastra; armature non influenzate dal getto"),
            Option("quality", "Controllo speciale della produzione del calcestruzzo"), Option("rough", "Superficie irregolare / aggregato a vista (+5 mm)"),
            Option("ntcQuality", "Controllo qualità con verifica dei copriferri")];
    }

    /// <summary>
    /// CaptureState of the historic view: version, class, exposure of the menu, exposures of a previous version, numbers, choices,
    /// options and the active exposure, in this order.
    /// </summary>
    private JsonObject CaptureState()
    {
        var state = new JsonObject { ["versione_materiali"] = 1, ["classe"] = selectedClass, ["esposizione_principale"] = selectedExposure };
        var values = new JsonObject(); foreach (string key in MaterialSheetInput.NumberKeys) values[key] = inputs[key]?.DeepClone();
        var selections = new JsonObject();
        foreach (string key in MaterialSheetInput.ChoiceKeys) selections[key] = key == DeviationValueKey ? deviationValue : inputs[key]?.DeepClone();
        var flags = new JsonObject(); foreach (string key in MaterialSheetInput.FlagKeys) flags[key] = inputs[key]?.GetValue<bool>() == true;
        var exposures = new JsonArray(selectedExposure);
        if (legacy is not null) state["esposizioni_precedenti"] = legacy.DeepClone();
        state["numeri"] = values; state["scelte"] = selections; state["opzioni"] = flags; state["esposizioni"] = exposures;
        return state;
    }

    private MaterialExposureChoice[] Active() => MaterialSheetInput.ExposureChoices.Where(e => active.Contains(e.Code)).ToArray();

    private void OnFieldChanged(string key)
    {
        if (loading || IsDisposed) return;
        Edited();
    }

    /// <summary>An edit of the user: the presentation again, then Modified when the state changed (the Changed of the historic view).</summary>
    private void Edited()
    {
        Refresh();
        var current = CaptureState();
        if (JsonNode.DeepEquals(current, lastState)) return;
        lastState = current;
        Changed();
    }

    /// <summary>RefreshDetails of the historic view: the deviation value of the control, the exposure texts, then every value shown.</summary>
    private void Refresh()
    {
        UpdateDeviation();
        var choices = Active();
        var texts = MaterialExposurePresentation.Texts(selectedExposure, choices, legacy);
        if (texts.Shown != selectedExposure) { selectedExposure = texts.Shown; OnPropertyChanged(nameof(SelectedExposure)); }
        ExposureDetails = texts.Details; ExposureExamples = texts.Examples; Aggressiveness = texts.Aggressiveness;
        CombinedExposure = texts.Combined; IsCombinedExposureVisible = texts.CombinedVisible;
        var value = MaterialSheetPresentation.Evaluate(CaptureState(), choices.Select(e => e.Code));
        Strength = value.Strength; Tension = value.Tension; Modulus = value.Modulus; Status = value.Status; Properties = value.Properties;
        BondValue = value.BondValue; BondDetails = value.BondDetails;
        MinimumConcreteClass = value.MinimumConcreteClass; MinimumConcreteNote = value.MinimumConcreteNote;
        CoverHeadline = value.CoverHeadline; CoverSteps = value.CoverSteps; CoverNotes = value.CoverNotes; CoverError = value.CoverError;
        IsNtcCover = MaterialSheetInput.IsNtcCover(inputs["coverMethod"]?.GetValue<string>());
        Composition = new Dictionary<string, string>(value.Composition, StringComparer.Ordinal);
        CompositionSource = value.CompositionSource; CompositionAirNote = value.CompositionAirNote;
        CompositionRequirements = value.CompositionRequirements; ConsistencyInfo = value.ConsistencyInfo;
    }

    /// <summary>UpdateDeviation of the historic view: a new deviation control gives its items and selects the first; then the note.</summary>
    private void UpdateDeviation()
    {
        int mode = MaterialSheetInput.Options(DeviationControlKey).ToList().IndexOf(inputs[DeviationControlKey]?.GetValue<string>() ?? "");
        if (mode != lastDeviationControl)
        {
            DeviationValues = MaterialSheetInput.DeviationOptions(mode);
            deviationValue = deviationValues.FirstOrDefault();
            lastDeviationControl = mode;
            OnPropertyChanged(nameof(SelectedDeviationValue));
            OnPropertyChanged(nameof(CanChooseDeviationValue));
            OnPropertyChanged(nameof(CanEditDeviationValue));
        }
        DeviationNote = MaterialSheetPresentation.DeviationNote(mode);
    }
}
