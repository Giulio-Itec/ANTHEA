using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Help;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>A check box of the bar of a stress map («σ/ε barre», «σ/ε trefoli», «σ/ε vertici CLS»): its option and its label.</summary>
public sealed class ConcreteStressValueOptionViewModel : ObservableObject
{
    private readonly ConcreteStressPanelViewModel panel;
    private bool shown = true;

    internal ConcreteStressValueOptionViewModel(ConcreteStressPanelViewModel panel, string key, string label)
    {
        this.panel = panel; Key = key; Label = label;
    }

    public string Key { get; }
    public string Label { get; }
    /// <summary>The option of the family (false when missing). A click saves it with Modified; refused in a read-only revision.</summary>
    public bool IsChecked { get => panel.Options.B(Key); set => panel.SetValueOption(this, value); }
    /// <summary>The check box is shown: «σ/ε trefoli» only when the section has cables or tendons.</summary>
    public bool IsShown { get => shown; internal set => SetProperty(ref shown, value); }
    /// <summary>The check box accepts the click (not in a read-only revision).</summary>
    public bool CanChange => panel.CanEditValues;
    internal void Refresh() { OnPropertyChanged(nameof(IsChecked)); OnPropertyChanged(nameof(CanChange)); }
}

/// <summary>
/// A family of the serviceability limit states of the RC section (tab 04 «Tensioni e fessurazione»: Rara, Frequente, Quasi permanente; F5.10,
/// sub-step S4: StressPanel, BuildStressTabs, PublishStress, UpdateStressSelection and SynchronizeHomogenization of the historic
/// ConcreteStress.cs, SynchronizeSharedSle and RefreshTendonOptions of ConcreteRefinements.cs), without WPF:
/// <list type="bullet">
/// <item>the form of the common SLE options (<see cref="Form"/>, on <c>sle.&lt;famiglia&gt;</c>): analysis and homogenization (n and φ computed
/// from each other, their errors shared by the three families), environment and reinforcement, cracking, axes; a change is copied into the other
/// families and into <c>sle_comuni</c> (<see cref="ConcreteSheetViewModel"/>), as the historic view;</item>
/// <item>the table of the actions of the family (<see cref="Table"/>, the model of S2), whose columns of the stresses and of the cracking follow
/// the checks of the family;</item>
/// <item>the detail, the steps of the cracking, the tables of the bars and of the concrete vertices of the selected row, the effective zones and
/// the summary of the checks;</item>
/// <item>the stress map as plain data (<see cref="Map"/>, decisions Q3 and C8): section, stress state, contour, check boxes, zone, legend.</item>
/// </list>
/// The family is shown when the main tab is 04 and the family is chosen (<see cref="ConcreteSheetViewModel.StressFamily"/>): only then the
/// selection fills the detail and the map, and the legend of the map follows its drawing (the historic viewport writes it when it draws).
/// </summary>
public sealed class ConcreteStressPanelViewModel : ObservableObject
{
    /// <summary>The notice of the non-linear analysis.</summary>
    public const string NonLinearWarning = "Analisi non lineare: le tensioni di esercizio sono calcolate con i legami costitutivi di progetto (valori caratteristici ridotti con αcc e γ). La norma non prescrive il metodo dell'analisi tensionale; per le verifiche SLE il riferimento abituale è l'analisi lineare a sezione fessurata. Vedere la guida (?) accanto ad «Analisi».";
    /// <summary>The instructions under the options.</summary>
    public const string Instructions = "Analisi GPC Engine lineare/non lineare. Rara: limiti CLS e acciaio; quasi permanente: limite CLS. Frequente: nessun limite tensionale automatico. φ è il coefficiente di viscosità. Fessurazione: NTC/UNI frequente e quasi permanente; altri EC2 quasi permanente, salvo NS XD3/XS3 frequente. Model Code richiede wlim di progetto. Il limite assegnato sostituisce quello EC2 (tranne UNI) e va motivato nel progetto. L’apertura richiede analisi lineare con CLS teso escluso.";
    /// <summary>The subtitle of the options.</summary>
    public const string OptionsSubtitle = "Modifiche valide per Rara, Frequente e Quasi permanente. Azioni separate, già combinate.";

    // The columns of the table that follow the checks of the family, by their key: ησ and «Tensioni» the stresses, «Fessurazione» the cracking.
    // F5.15, S4-b (F5-35): the historic grid chose them by index (6, 7 and 8), which fell on the next columns with the unified actions.
    private const string StressColumn = "eta_sigma", RatioColumn = "stress_status", CrackColumn = "wk";

    private readonly ConcreteSheetViewModel sheet;
    private string detail = "Selezionare una combinazione", crackDetail = "";
    private int detailTab = -1, verificationTab = 1, region = -1;
    private bool crackTab = true, nonLinear, detailsGenerated;
    private IReadOnlyList<ConcreteResults.EffectiveRegion> regions = [];

    internal ConcreteStressPanelViewModel(ConcreteSheetViewModel sheet, string key, int index, ConcreteActionTableViewModel table,
        IReadOnlyList<ConcreteSheetPreparation.HomogenizationUpdate> prepared)
    {
        this.sheet = sheet; Key = key; Index = index; Table = table;
        Options = sheet.Settings["sle"]![key]!.AsObject();
        // BuildStressTabs of the historic view, in its order: the form, its groups, the homogenization prepared at opening, the fields enabled.
        var errors = sheet.HomogenizationErrors;
        Form = new FormViewModel(Options, [new("modello", "Analisi", Choices: ["Lineare", "Non lineare"], HelpTopic: HelpTopics.StressAnalysis.Uri),
            new("n_armature", "n armature", Note: _ => errors.GetValueOrDefault("n_armature")), new("phi", "Viscosità φ armature", Note: _ => errors.GetValueOrDefault("phi")),
            new("n_trefoli", "n trefoli (Ep riferimento)", Note: _ => errors.GetValueOrDefault("n_trefoli")),
            new("phi_trefoli", "Viscosità φ trefoli", Note: _ => errors.GetValueOrDefault("phi_trefoli")), new("__ep_ref", "Ep di riferimento", "MPa", ReadOnly: true),
            new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]), new("assi", "Assi delle azioni", Choices: ["Locali", "Principali", "Personalizzati"]),
            new("origine_x", "Origine x", "mm"), new("origine_y", "Origine y", "mm"), new("rotazione", "Rotazione assi", "°"),
            new("esposizione", "Esposizione", Choices: ConcreteEditor.ExposureChoices.ToArray()), new("sensibilita", "Armatura", Choices: ["Poco sensibile", "Sensibile"]),
            new("durata", "Durata del carico", Choices: ["Lunga", "Breve"]), new("aderenza", "Barre", Choices: ["Migliorata", "Liscia"]),
            new("copriferro_fessure", "c barra (vuoto: dalle barre efficaci)", "mm"), new("spaziatura_fessure", "Interasse massimo (vuoto: auto)", "mm"),
            new("limite_fessure", "wlim di progetto (EC2/MC)", "mm")], Changed, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        Form.GroupFields("Analisi e omogeneizzazione", ["modello", "n_armature", "phi", "n_trefoli", "phi_trefoli", "__ep_ref"], true);
        Form.GroupFields("Verifiche SLE · ambiente e armatura", ["esposizione", "sensibilita", "durata", "aderenza", "limite_fessure"], true);
        Form.GroupFields("Fessurazione · disposizione delle barre", ["copriferro_fessure", "spaziatura_fessure"]);
        Form.GroupFields("Avanzate · CLS teso e assi", ["trazione_cls", "assi", "origine_x", "origine_y", "rotazione"]);
        foreach (var update in prepared) ShowHomogenization(update);
        EnableOptions();
        // The map: the contour saved at opening, the neutral axis and the values of the check boxes from the options.
        Map = new ConcreteSectionDrawingViewModel
        {
            Contour = Options.S("contour", ConcreteSectionDrawingViewModel.Contours[0]), ShowNeutralAxis = Options.B("asse_neutro", true)
        };
        ValueOptions = [new(this, "testi_barre", "σ/ε barre"), new(this, "testi_trefoli", "σ/ε trefoli"), new(this, "testi_cls", "σ/ε vertici CLS")];
        ShowValues();
        Bars = new ConcreteTableViewModel([new("id", "Barra", readOnly: true), new("stress", "σs [MPa]", readOnly: true), new("strain", "ε [‰]", readOnly: true),
            new("type", "Stato", readOnly: true)], [], () => sheet.IsReadOnly);
        Concrete = new ConcreteTableViewModel([new("id", "Vertice", readOnly: true), new("x", "x [mm]", readOnly: true), new("y", "y [mm]", readOnly: true),
            new("stress", "σc [MPa]", readOnly: true), new("strain", "εc [‰]", readOnly: true)], [], () => sheet.IsReadOnly);
        // A bar clicked on the map: the tab «Barre e trefoli» and its row; the row selected highlights its bar.
        Map.BarClicked += (_, bar) => { if (bar < Bars.Rows.Count) { DetailTab = 1; Bars.SelectedIndex = bar; } };
        Bars.SelectionChanged += (_, _) => { Map.SelectedBar = Bars.Selected?.Values.S("id") ?? ""; Redraw(); };
        RheologyCommand = new RelayCommand(() => sheet.Inputs.OpenRheologyCommand.Execute(null));
        // The selection of the table moves the detail and the map (onSelection of ActionTable); the view of the grid selected its first row.
        Table.Table.SelectionChanged += (_, _) => UpdateSelection();
        if (Table.Table.Selected is not null) UpdateSelection();
    }

    /// <summary>SLE, SLE_FREQ or SLE_QP.</summary>
    public string Key { get; }
    /// <summary>The index of the family in the tab (0 Rara, 1 Frequente, 2 Quasi permanente).</summary>
    public int Index { get; }
    /// <summary>The title of the family (its tab and the choice «Combinazione SLE»).</summary>
    public string Label => ConcreteEditor.Label(Key);
    /// <summary>The title of the table.</summary>
    public string TableTitle => "Combinazioni · " + Label;
    /// <summary>The options of the family in the settings of the sheet (<c>sle.&lt;famiglia&gt;</c>).</summary>
    public JsonObject Options { get; }
    /// <summary>«Opzioni SLE comuni».</summary>
    public FormViewModel Form { get; }
    /// <summary>The table of the actions of the family (S2).</summary>
    public ConcreteActionTableViewModel Table { get; }
    /// <summary>The stress map as plain data.</summary>
    public ConcreteSectionDrawingViewModel Map { get; }
    /// <summary>«Barre e trefoli» of the selected row.</summary>
    public ConcreteTableViewModel Bars { get; }
    /// <summary>«Calcestruzzo» of the selected row.</summary>
    public ConcreteTableViewModel Concrete { get; }
    /// <summary>«Riepilogo verifiche» of the serviceability checks.</summary>
    public ConcreteCardsViewModel Summary { get; } = new();
    /// <summary>The check boxes of the values on the map.</summary>
    public IReadOnlyList<ConcreteStressValueOptionViewModel> ValueOptions { get; }
    /// <summary>«Reologia del calcestruzzo…» (allowed in a read-only revision).</summary>
    public IRelayCommand RheologyCommand { get; }

    /// <summary>The contours of the map.</summary>
    public static ImmutableArray<string> Contours => ConcreteSectionDrawingViewModel.Contours;

    /// <summary>«Riepilogo» of the selected row.</summary>
    public string Detail { get => detail; private set => SetProperty(ref detail, value); }
    /// <summary>«Fessurazione · passaggi» of the selected row.</summary>
    public string CrackDetail { get => crackDetail; private set => SetProperty(ref crackDetail, value); }
    /// <summary>The tab «Fessurazione · passaggi» is shown: the family checks the cracking.</summary>
    public bool CrackTabVisible { get => crackTab; private set => SetProperty(ref crackTab, value); }
    /// <summary>The notice of the non-linear analysis is shown.</summary>
    public bool NonLinearNotice { get => nonLinear; private set => SetProperty(ref nonLinear, value); }

    /// <summary>
    /// The tab of the details of the row: 0 «Riepilogo», 1 «Barre e trefoli», 2 «Calcestruzzo», 3 «Fessurazione · passaggi»; -1 until the tabs
    /// are shown the first time (the historic tab control selects its first tab only then).
    /// </summary>
    public int DetailTab { get => detailTab; set { if (value is >= -1 and <= 3) SetProperty(ref detailTab, value); } }

    /// <summary>The tab of the column of the results: 0 «Dettagli combinazione», 1 «Riepilogo verifiche» (shown at opening).</summary>
    public int VerificationTab
    {
        get => verificationTab;
        set
        {
            if (value is < 0 or > 1 || !SetProperty(ref verificationTab, value)) return;
            GenerateDetails();
        }
    }

    /// <summary>The effective zones of the cracking of the selected row (the choice on the bar of the map).</summary>
    public IReadOnlyList<ConcreteResults.EffectiveRegion> Regions { get => regions; private set => SetProperty(ref regions, value); }

    /// <summary>The zone highlighted on the map (-1 for none): allowed in a read-only revision.</summary>
    public int SelectedRegion
    {
        get => region;
        set
        {
            if (value < -1 || value >= regions.Count || value == region) return;
            region = value; OnPropertyChanged();
            Map.EffectiveRegion = value >= 0 ? regions[value] : null;
            Redraw();
        }
    }

    /// <summary>The contour of the map (<see cref="Contours"/>): saved in the options with Modified, also in a read-only revision.</summary>
    public string Contour
    {
        get => Map.Contour;
        set
        {
            if (value is null || !Contours.Contains(value) || value == Map.Contour) return;
            Options["contour"] = value; Map.Contour = value;
            OnPropertyChanged(); Redraw(); sheet.RaiseModified();
        }
    }

    /// <summary>«Asse neutro» of the map: saved in the options with Modified, also in a read-only revision.</summary>
    public bool NeutralAxis
    {
        get => Map.ShowNeutralAxis;
        set
        {
            if (value == Map.ShowNeutralAxis) return;
            Options["asse_neutro"] = value; Map.ShowNeutralAxis = value;
            OnPropertyChanged(); Redraw(); sheet.RaiseModified();
        }
    }

    /// <summary>The check boxes of the values accept the click (not in a read-only revision).</summary>
    internal bool CanEditValues => sheet.CanEdit;

    /// <summary>The family is on screen: the tab 04 and this family chosen, the sheet open.</summary>
    public bool IsShown => !sheet.Initializing && sheet.SelectedTab == ConcreteSheetViewModel.StressTab && sheet.StressFamily == Index;

    // ---------------------------------------------------------------- options

    // The callback of the form (BuildStressTabs): homogenization, the options copied into the other families, the fields enabled; the options
    // of the cracking calculate again every family; the others clear the results of this family first.
    private void Changed(string field)
    {
        if (sheet.Initializing) return;
        sheet.StressOptionChanged(this, field);
    }

    /// <summary>n and φ of the family computed from each other (after a change of <paramref name="changed"/>), with their errors.</summary>
    internal void SynchronizeHomogenization(string? changed = null)
    {
        foreach (var update in ConcreteSheetPreparation.SynchronizeHomogenization(sheet.Input, sheet.Settings, Key, changed)) ShowHomogenization(update);
    }

    // The values shown and the errors of a homogenization (SynchronizeHomogenization of the historic view): the errors are shared by the families.
    private void ShowHomogenization(ConcreteSheetPreparation.HomogenizationUpdate update)
    {
        var errors = sheet.HomogenizationErrors;
        errors.Remove(update.NKey); errors.Remove(update.PhiKey);
        Form.Set(update.Target, update.Value is double value ? EngineeringFormat.Number(value) : "", true);
        if (update.Modulus is double modulus) Form.Set("__ep_ref", EngineeringFormat.Number(modulus), true);
        if (update.Error is not null) errors[update.Source] = update.Error;
        Form.UpdateNote(update.NKey); Form.UpdateNote(update.PhiKey);
    }

    /// <summary>The notice and the fields enabled by the analysis and by the axes (EnableOptions of the historic view).</summary>
    internal void EnableOptions()
    {
        NonLinearNotice = ConcreteStressPresentation.NonLinear(Options);
        EnableFields();
    }

    /// <summary>The fields enabled by the analysis and by the axes.</summary>
    internal void EnableFields()
    {
        foreach (string field in ConcreteStressPresentation.SwitchedFields) Form.Enable(field, ConcreteStressPresentation.FieldEnabled(Options, field));
    }

    /// <summary>The fields and the check box of the cables, shown only with cables or tendons (RefreshTendonOptions).</summary>
    internal void ShowTendonOptions(bool present)
    {
        foreach (string key in new[] { "n_trefoli", "phi_trefoli", "__ep_ref" }) Form.ShowField(key, present);
        ValueOptions[1].IsShown = present;
    }

    // A check box of the values (ShowValues of the historic view).
    internal void SetValueOption(ConcreteStressValueOptionViewModel option, bool value)
    {
        if (!sheet.CanEdit) return;
        Options[option.Key] = value;
        ShowValues();
        sheet.RaiseModified();
    }

    private void ShowValues()
    {
        Map.BarValues = Options.B("testi_barre"); Map.TendonValues = Options.B("testi_trefoli"); Map.ConcreteValues = Options.B("testi_cls");
        foreach (var option in ValueOptions) option.Refresh();
        Redraw();
    }

    /// <summary>The read-only revision changed.</summary>
    internal void RefreshEditable(bool readOnly)
    {
        Form.IsReadOnly = readOnly;
        foreach (var option in ValueOptions) option.Refresh();
        Bars.RefreshReadOnly(); Concrete.RefreshReadOnly();
    }

    // ---------------------------------------------------------------- selection, map

    /// <summary>The family became visible (IsVisibleChanged of the historic map): the selection fills the detail and the map again.</summary>
    internal void Shown()
    {
        if (!IsShown) return;
        GenerateDetails();
        UpdateSelection();
    }

    // The tabs of the details of the row are shown the first time (their tab control selects its first tab).
    private void GenerateDetails()
    {
        if (detailsGenerated || verificationTab != 0 || !IsShown) return;
        detailsGenerated = true;
        if (detailTab == -1) DetailTab = 0;
    }

    /// <summary>
    /// UpdateStressSelection: the columns and the tab of the cracking follow the checks of the family; then, while the family is shown, the
    /// steps of the cracking, the stress state, the zones, the detail and the tables of the bars and of the concrete of the selected row.
    /// </summary>
    internal void UpdateSelection()
    {
        if (sheet.IsSynchronizing) return;
        bool stressRequired = ConcreteEditor.HasStressCheck(Key), crackRequired = ConcreteEditor.HasCrackCheck(Key, sheet.Settings);
        var columns = Table.Table.Columns;
        ConcreteColumnViewModel Column(string key) => columns.First(c => c.Key == key);
        Column(StressColumn).IsVisible = Column(RatioColumn).IsVisible = stressRequired;
        Column(CrackColumn).IsVisible = CrackTabVisible = crackRequired;
        if (!IsShown) return;
        using var barsRefresh = Bars.Rows.DeferRefresh();
        using var concreteRefresh = Concrete.Rows.DeferRefresh();
        var row = Table.Table.Selected;
        var outcome = row is null ? null : sheet.StressOutcome(Key, row.Values.S("id"));
        CrackDetail = ConcreteStressPresentation.CrackSteps(Key, row?.Values, outcome);
        Map.Stress = outcome?.State;
        ShowRegions(outcome?.CrackResult?.Regions ?? []);
        Redraw();
        Bars.Rows.Clear(); Concrete.Rows.Clear();
        Detail = ConcreteStressPresentation.Detail(Key, sheet.Settings, row?.Values, outcome, sheet.Inputs.Tendons.Rows.Count > 0);
        if (outcome?.State is not ConcreteResults.StressState state) return;
        foreach (var bar in ConcreteStressPresentation.BarRows(state, Map.Section?.Bars.Count ?? 0, sheet.Settings)) Bars.Rows.Add(new JsonRowViewModel(bar));
        foreach (var vertex in ConcreteStressPresentation.ConcreteRows(state)) Concrete.Rows.Add(new JsonRowViewModel(vertex));
    }

    // The zones of the row: the first one highlighted, if any.
    private void ShowRegions(IReadOnlyList<ConcreteResults.EffectiveRegion> zones)
    {
        Regions = zones;
        region = zones.Count > 0 ? 0 : -1; OnPropertyChanged(nameof(SelectedRegion));
        Map.EffectiveRegion = region >= 0 ? zones[region] : null;
    }

    /// <summary>Invalidate of the sheet: no stress state, no zone, the detail and the steps wait for the calculation, no bars and no vertices.</summary>
    internal void Invalidated()
    {
        Map.Stress = null; Map.EffectiveRegion = null; ShowRegions([]);
        Redraw();
        Detail = "Nessun risultato aggiornato"; CrackDetail = "Dati modificati: passaggi in attesa di aggiornamento.";
        Bars.Rows.Clear(); Concrete.Rows.Clear();
    }

    /// <summary>The section of the map (RefreshPreview of the historic view): section, cables and message of the preview.</summary>
    internal void ShowSection(ConcreteSectionDrawingViewModel preview)
    {
        Map.Section = preview.Section; Map.Tendons = preview.Tendons; Map.Message = preview.Message;
        Redraw();
    }

    /// <summary>The links drawn on the map (SynchronizeStirrups).</summary>
    internal void ShowStirrups(JsonObject shear) { Map.Stirrups = shear; Redraw(); }

    /// <summary>
    /// The map draws again (InvalidateVisual of the historic view). The legend is that of the drawing on screen: the historic viewport writes it
    /// when it draws, which happens only while the family is shown and the section exists; otherwise it keeps the legend of its last drawing.
    /// </summary>
    private void Redraw()
    {
        if (IsShown && Map.Section is not null) Map.Legend = ConcreteStressPresentation.ContourScale(Map.Stress, Map.Contour)?.Legend ?? "";
        Map.Invalidate();
    }
}
