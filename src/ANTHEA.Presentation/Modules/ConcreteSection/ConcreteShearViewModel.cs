using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The links of the section shown read-only in a tab (BuildStirrups(readOnly: true) of the historic view): their values on the inputs
/// (present, diameter, spacing) and their scheme on the shear options; the fields of the circular scheme only for circular links, every field
/// disabled without links (SynchronizeStirrups). The tab 05 «Taglio e torsione» has one (<see cref="ConcreteShearViewModel.Stirrups"/>), the tab
/// 06 «Dettagli costruttivi» another (<see cref="ConcreteDetailingViewModel.Stirrups"/>, S6).
/// </summary>
public sealed class ConcreteStirrupsViewModel
{
    internal ConcreteStirrupsViewModel(ConcreteSheetViewModel sheet)
    {
        Values = new FormViewModel(sheet.Input, [new("staffe_presenti", "Staffe presenti", ReadOnly: true), new("transverse_bar_diameter_mm", "Ø staffe", "mm", ReadOnly: true),
            new("transverse_spacing_mm", "Passo", "mm", ReadOnly: true)], compact: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        Scheme = new FormViewModel(sheet.ShearOptions, [new("tipo_staffa", "Circolare", ReadOnly: true), new("rami_x", "Braccia resistenti a Vx", ReadOnly: true),
            new("rami_y", "Braccia resistenti a Vy", ReadOnly: true), new("schema_interno", "Schema interno", ReadOnly: true),
            new("rami_interni", "Bracci aggiunti / staffe interne", ReadOnly: true), new("rotazione_staffa", "Rotazione schema", "°", ReadOnly: true)], compact: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
    }

    /// <summary>«Staffe presenti», «Ø staffe», «Passo».</summary>
    public FormViewModel Values { get; }
    /// <summary>The scheme of the links: circular type, branches resisting Vx and Vy, inner scheme, added branches, rotation.</summary>
    public FormViewModel Scheme { get; }
    /// <summary>The button «Modifica staffe nel pannello di controllo →» (the view selects the tab 01).</summary>
    public string EditText => "Modifica staffe nel pannello di controllo →";

    internal IEnumerable<FormViewModel> Forms => [Values, Scheme];

    internal void RefreshEditable(bool readOnly) { Values.IsReadOnly = readOnly; Scheme.IsReadOnly = readOnly; }
}

/// <summary>
/// The shear and torsion of the RC section (tab 05, F5.10 sub-step S5: BuildShearPanel, RefreshAutomaticShear, UpdateShearSelection and the
/// summaries of CalculateShear of the historic ConcreteShear.cs, BuildTorsionOptions of ConcreteTorsion.cs), without WPF:
/// <list type="bullet">
/// <item>the links shown read-only (<see cref="Stirrups"/>), the form of the shear (<see cref="Form"/>, on <c>taglio</c>: model, geometric
/// parameters automatic or manual, the directions Vx and Vy, the eccentricities of the Model Code) and that of the torsion and of the circular
/// model (<see cref="Torsion"/>);</item>
/// <item>the table of the shear (<see cref="Table"/>, the model of S2) with its commands;</item>
/// <item>the detail of the selected row, the summary under the table, the summary of the checks (<see cref="Worst"/>) and the section drawn as
/// plain data (<see cref="Drawing"/>, decisions Q3 and C8).</item>
/// </list>
/// The calculation of the shear runs on the UI thread inside the batch of the sheet (decision C2), which owns the rows, the results and the
/// automatic parameters (ConcreteSheetViewModel.Checks.cs). The detail and the drawing follow the selection only while the tab is shown, as the
/// historic grid (IsVisibleChanged).
/// </summary>
public sealed class ConcreteShearViewModel : ObservableObject
{
    /// <summary>The notice under the forms.</summary>
    public const string Notice = "Model Code 2010: Mx è associato a Vy, My a Vx. N negativo a compressione. Il diametro massimo dell’aggregato è condiviso con Dettagli → Copriferro e durabilità; modificarlo aggiorna anche il taglio. Le azioni sono già coefficientate; il modulo applica i coefficienti dei materiali alle resistenze.";
    /// <summary>The note of the torsion.</summary>
    public const string TorsionNote = "Torsione: As è la quota disponibile dopo pressoflessione. Staffe chiuse ortogonali all’asse; area di un ramo per torsione. Per Vx+Vy si adotta la somma conservativa dei contributi sul calcestruzzo, non una formula normativa di interazione biassiale. Modello delle pile: z/d = 0,75 piena, 0,60 cava.";

    private static ImmutableArray<string> Axes { get; } = ["x", "y"];
    private static ImmutableArray<string> Parameters { get; } = ["bw_", "d_", "asl_"];

    private readonly ConcreteSheetViewModel sheet;
    private string detail = "Selezionare una combinazione", summary = "Taglio · da calcolare";
    private int detailTab = 1;

    internal ConcreteShearViewModel(ConcreteSheetViewModel sheet, ConcreteActionTableViewModel table)
    {
        this.sheet = sheet; Table = table;
        var options = sheet.ShearOptions;
        // BuildShearPanel of the historic view, in its order: the form, its groups, the fields enabled, the automatic parameters shown; the links
        // read-only and the torsion when the panel is laid out. Nothing is written.
        var fields = new List<FieldSpec> { new("modello", "Modello", Choices: ["Con staffe", "Senza staffe"]), new("parametri", "Parametri geometrici", Choices: ["Automatici da sezione", "Manuali"]),
            new("ancoraggio", "Asl automatica efficacemente ancorata", Choices: ["Da verificare", "Confermato"]) };
        foreach (string axis in Axes)
            fields.AddRange([new("bw_" + axis, "bw · " + axis, "mm"), new("d_" + axis, "d utile · " + axis, "mm"), new("asl_" + axis, "Asl ancorata · " + axis, "mm²"),
                new("alpha_" + axis, "α staffa · " + axis, "°"), new("cot_" + axis, "cot θ · " + axis + " (vuoto: auto)")]);
        fields.AddRange([new("eccentricita_mc_x", "Δe MC · Vx", "mm"), new("eccentricita_mc_y", "Δe MC · Vy", "mm")]);
        Form = new FormViewModel(options, fields, Changed, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        foreach (string axis in Axes) Form.GroupFields("Direzione V" + axis, new[] { "bw_", "d_", "asl_", "rami_", "alpha_", "cot_" }.Select(f => f + axis).ToArray(), true);
        EnableFields();
        ShowAutomaticParameters(sheet.AutomaticShearFailed);
        Stirrups = new ConcreteStirrupsViewModel(sheet);
        Torsion = new FormViewModel(options, [new("cot_torsione", "cot θ comune (T ≠ 0)"), new("as_torsione", "As longitudinale disponibile per torsione", "mm²"),
            new("chiusura_torsione", "Staffe chiuse e barre sul profilo", Choices: ["Da confermare", "Confermato"]),
            new("modello_circolare", "Taglio circolare", Choices: ["Da scegliere", "Pile · NTC §7.9.5.2", "Parametri assegnati"]), new("z_d", "z/d assegnato · circolare")],
            TorsionChanged, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        // The selection of the table moves the detail and the drawing (SelectionChanged of the historic grid).
        Table.Table.SelectionChanged += (_, _) => UpdateSelection();
    }

    /// <summary>The links of the section, read-only («Staffe · dati comuni»).</summary>
    public ConcreteStirrupsViewModel Stirrups { get; }
    /// <summary>The shear: model, geometric parameters, the directions Vx and Vy, the eccentricities of the Model Code.</summary>
    public FormViewModel Form { get; }
    /// <summary>«Torsione / modello circolare».</summary>
    public FormViewModel Torsion { get; }
    /// <summary>The table of the shear (S2).</summary>
    public ConcreteActionTableViewModel Table { get; }
    /// <summary>The summary of the checks of the shear («Riepilogo verifiche»).</summary>
    public ConcreteCardsViewModel Worst { get; } = new();
    /// <summary>The section with the geometric references of the shear, as plain data.</summary>
    public ConcreteSectionDrawingViewModel Drawing { get; } = new();

    /// <summary>The note of the automatic parameters (or of the manual ones, of the wizard).</summary>
    public string AutomaticNote => sheet.ShearAutomaticNote;
    /// <summary>«Dettagli combinazione» of the selected row.</summary>
    public string Detail { get => detail; private set => SetProperty(ref detail, value); }
    /// <summary>The summary under the table.</summary>
    public string Summary { get => summary; internal set => SetProperty(ref summary, value); }
    /// <summary>The tab of the results: 0 «Dettagli combinazione», 1 «Riepilogo verifiche» (shown at opening).</summary>
    public int DetailTab { get => detailTab; set { if (value is >= 0 and <= 1) SetProperty(ref detailTab, value); } }

    /// <summary>The tab 05 is on screen (the sheet open).</summary>
    public bool IsShown => !sheet.Initializing && sheet.SelectedTab == ConcreteSheetViewModel.ShearTab;

    // ---------------------------------------------------------------- options

    // The callback of the shear form (BuildShearPanel): the model with links brings the links back in the inputs; then the fields, the automatic
    // parameters, the links of the drawings and the shear calculated again.
    private void Changed(string key)
    {
        if (sheet.Initializing) return;
        sheet.ShearOptionChanged(key);
    }

    // The callback of the torsion: the shear calculated again, then one more Modified (behaviour kept, C5).
    private void TorsionChanged(string key)
    {
        if (sheet.Initializing) return;
        sheet.InvalidateActions("Taglio"); sheet.RaiseModified();
    }

    /// <summary>EnableFields of the historic view: Asl without links or with the Model Code; the angles and cot θ with the links.</summary>
    internal void EnableFields()
    {
        bool links = ConcreteShearPresentation.WithLinks(sheet.ShearOptions), modelCode = ConcreteShearPresentation.ModelCode(sheet.Settings);
        foreach (string axis in Axes)
        {
            Form.Enable("asl_" + axis, !links || modelCode);
            foreach (string field in new[] { "rami_", "alpha_", "cot_" }) Form.Enable(field + axis, links);
        }
    }

    /// <summary>
    /// The form part of RefreshAutomaticShear: bw, d and Asl editable only with manual parameters (Asl also without links or with the Model
    /// Code), the anchorage with the automatic parameters without links or with the Model Code, the eccentricities with the Model Code.
    /// </summary>
    internal void EnableAutomaticFields()
    {
        var options = sheet.ShearOptions;
        bool automatic = ConcreteShearPresentation.Automatic(options), links = ConcreteShearPresentation.WithLinks(options), modelCode = ConcreteShearPresentation.ModelCode(sheet.Settings);
        foreach (string axis in Axes)
            foreach (string field in Parameters) Form.Enable(field + axis, !automatic && (field != "asl_" || !links || modelCode));
        Form.ShowField("ancoraggio", automatic && (!links || modelCode));
        foreach (string axis in Axes) Form.ShowField("eccentricita_mc_" + axis, modelCode);
    }

    /// <summary>The automatic parameters computed from the section shown in the form, with the anchorage.</summary>
    internal void ShowAutomaticValues()
    {
        foreach (string axis in Axes)
            foreach (string field in Parameters) Form.Set(field + axis, EngineeringFormat.Number(J.Number(sheet.ShearOptions[field + axis])), true);
        Form.Set("ancoraggio", sheet.ShearOptions.S("ancoraggio"), true);
    }

    /// <summary>The parameters of a section that gives none: emptied in the form.</summary>
    internal void ClearAutomaticValues()
    {
        foreach (string axis in Axes) foreach (string field in Parameters) Form.Set(field + axis, "", true);
    }

    // The automatic parameters at the construction of the panel (RefreshAutomaticShear of BuildShearPanel; the sheet computed them at opening).
    private void ShowAutomaticParameters(bool failed)
    {
        EnableAutomaticFields();
        if (!ConcreteShearPresentation.Automatic(sheet.ShearOptions)) return;
        if (failed) ClearAutomaticValues(); else ShowAutomaticValues();
    }

    /// <summary>The values of the shear shown again from the options (after the wizard, which rewrites them).</summary>
    internal void ReloadFields() { foreach (var field in Form.Fields) Form.Set(field.Key, sheet.ShearOptions.S(field.Key), true); }

    /// <summary>The read-only revision changed.</summary>
    internal void RefreshEditable(bool readOnly)
    {
        Form.IsReadOnly = readOnly; Torsion.IsReadOnly = readOnly; Stirrups.RefreshEditable(readOnly);
    }

    internal void RefreshNote() => OnPropertyChanged(nameof(AutomaticNote));

    // ---------------------------------------------------------------- selection

    /// <summary>The tab became visible (IsVisibleChanged of the historic grid): the detail and the drawing of the selection.</summary>
    internal void Shown() => UpdateSelection();

    /// <summary>UpdateShearSelection: while the tab is shown, the section drawn and the detail of the selected row.</summary>
    internal void UpdateSelection()
    {
        if (sheet.IsSynchronizing || !IsShown) return;
        try { Drawing.Section = ConcretePreview.PrepareGeometry(sheet.Input); } catch (ArgumentException) { Drawing.Section = null; }
        Drawing.Invalidate();
        if (Table.Table.Selected is not { } row) { Detail = ConcreteShearPresentation.NoSelection; return; }
        string id = row.Values.S("id");
        Detail = ConcreteShearPresentation.Detail(row.Values, sheet.ShearOptions, sheet.Input.S("shape"), sheet.ShearChecks(id), sheet.TorsionCheck(id));
    }

    /// <summary>InvalidateShear of the historic view: the summaries wait for the calculation, the detail says why.</summary>
    internal void Invalidated()
    {
        Summary = ConcreteShearPresentation.Pending; Worst.Text = ConcreteShearPresentation.Pending;
        Detail = ConcreteShearPresentation.DetailPending;
    }

    /// <summary>The links drawn (SynchronizeStirrups).</summary>
    internal void ShowStirrups(JsonObject shear) { Drawing.Stirrups = shear; Drawing.Invalidate(); }
}
