using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Materials;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>An expander of the control panel (the Group of the historic view): title and whether it is open.</summary>
public sealed class ConcreteGroupViewModel(string title, bool expanded) : ObservableObject
{
    private bool isExpanded = expanded;
    public string Title { get; } = title;
    public bool IsExpanded { get => isExpanded; set => SetProperty(ref isExpanded, value); }
}

/// <summary>A second layer of bars (top, bottom, inner ring): its toggle, its form and when they are shown.</summary>
public sealed class ConcreteLayerViewModel : ObservableObject
{
    private bool isShown, toggleEnabled = true, formEnabled = true, formVisible;
    private string toggleText = "", toggleToolTip = "";

    internal ConcreteLayerViewModel(string key, string title, FormViewModel form, Action toggle, Func<bool> canEdit)
    {
        Key = key; Title = title; Form = form;
        ToggleCommand = new RelayCommand(toggle, () => toggleEnabled && canEdit());
    }

    public string Key { get; }
    public string Title { get; }
    public FormViewModel Form { get; }
    /// <summary>«+ Attiva · …» / «− Disattiva · …»: enables or disables the layer (disabled while locked or in a read-only revision).</summary>
    public IRelayCommand ToggleCommand { get; }
    /// <summary>The layer belongs to the shape (rows of the rectangle, inner ring of the circle).</summary>
    public bool IsShown { get => isShown; internal set => SetProperty(ref isShown, value); }
    public string ToggleText { get => toggleText; internal set => SetProperty(ref toggleText, value); }
    public string ToggleToolTip { get => toggleToolTip; internal set => SetProperty(ref toggleToolTip, value); }
    /// <summary>False while the coordinates of the bars lock the layers.</summary>
    public bool ToggleEnabled { get => toggleEnabled; internal set { if (SetProperty(ref toggleEnabled, value)) ToggleCommand.NotifyCanExecuteChanged(); } }
    /// <summary>The fields of the layer accept the user (false while the coordinates of the bars lock them).</summary>
    public bool FormEnabled { get => formEnabled; internal set => SetProperty(ref formEnabled, value); }
    /// <summary>The fields are shown while the layer is active.</summary>
    public bool FormVisible { get => formVisible; internal set => SetProperty(ref formVisible, value); }
}

/// <summary>A card of the quick resistances: direction and moment (or the reason of a missing value).</summary>
public sealed record ConcreteQuickResistanceCard(string Direction, string Value);

/// <summary>
/// Inputs and materials of the RC section (sub-step S1 of F5.10, the control panel of the historic ConcreteWorkspace without its
/// results): one <see cref="FormViewModel"/> per block (standard and note, coefficients, materials with their groups, geometry, hole,
/// reinforcement, second layers, links and their scheme, new cable, tendon of the standard, quick resistances), the tables of the bars and of
/// the cables, the saved materials and the window «Nuovo materiale» (<see cref="ConcreteMaterialDialogViewModel"/> on the dialog service),
/// and the plain data of the drawing of the section. The writes into the document and their order are those of the historic view; the
/// derivations are those of ConcreteInputPresentation (ANTHEA.Application). The sheet (<see cref="ConcreteSheetViewModel"/>) owns the
/// invalidations and the refresh of the preview.
/// </summary>
public sealed partial class ConcreteInputViewModel : ObservableObject
{
    private readonly ConcreteSheetViewModel sheet;
    private bool holeChecked, holeEnabled = true, holeFieldsVisible, reinforcementEnabled = true, holeFormEnabled = true, templateSection;
    private string? reinforcementToolTip;

    internal ConcreteInputViewModel(ConcreteSheetViewModel sheet)
    {
        this.sheet = sheet;
        var input = Input; var settings = Settings;
        // BuildControlPanel of the historic view, in its order: the writes of the lazy defaults happen where it does them.
        Standard = Form(settings, [new("normativa", "Normativa", Choices: ConcreteEditor.Standards), new("nota", "Nota del foglio")], key =>
        {
            if (key == "normativa") { ResetCoefficients(); RefreshStandardMaterialChoices(); }
            sheet.Invalidate();
        }, compact: false);
        Geometry = Form(input, [new("shape", "Sezione", Choices: ["Circolare", "Rettangolare", "A T", "Generica (da definire)"]), new("diameter_mm", "Diametro D", "mm"),
            new("circular_sides", "Lati del contorno (12–720, multipli di 4)"), new("width_mm", "Larghezza b", "mm"), new("height_mm", "Altezza h", "mm"),
            new("flange_width_mm", "Larghezza ala bf", "mm"), new("web_width_mm", "Larghezza anima bw", "mm"), new("flange_thickness_mm", "Spessore ala hf", "mm"),
            new("cover_mm", "Copriferro netto", "mm")], _ => sheet.Invalidate(), wikiModule: ConcreteSectionSheetModule.Id);
        ConcreteSheetPreparation.PrepareHoles(input);
        holeChecked = input.B("foro_presente");
        Hole = Form(input, [new("inner_diameter_mm", "Diametro foro", "mm"), new("inner_width_mm", "Larghezza foro", "mm"), new("inner_height_mm", "Altezza foro", "mm")], _ => sheet.Invalidate());
        UpdateHoleFields();
        Materials = BuildMaterials();
        ConcreteSheetPreparation.PrepareFlange(input);
        Reinforcement = Form(input, [new("longitudinal_bar_count", "Barre circolari"), new("longitudinal_bar_diameter_mm", "Diametro", "mm"),
            new("top_bar_count", "Barre superiori"), new("top_bar_diameter_mm", "Ø superiori", "mm"), new("flange_bottom_count", "Barre intradosso ala (0: assenti)"),
            new("flange_bottom_diameter_mm", "Ø intradosso ala", "mm"), new("flange_bottom_offset_mm", "Intradosso ala → asse barra", "mm"),
            new("bottom_bar_count", "Barre inferiori"), new("bottom_bar_diameter_mm", "Ø inferiori", "mm"), new("side_bar_count_per_side", "Barre laterali / lato"),
            new("side_bar_diameter_mm", "Ø laterali", "mm")], _ => sheet.Invalidate());
        Tendons = new ConcreteTableViewModel(TendonColumns(), [], () => sheet.IsReadOnly);
        foreach (var item in settings.Array("trefoli").OfType<JsonObject>()) Tendons.Rows.Add(TendonRow((JsonObject)item.DeepClone()));
        Cable = BuildTendonInput();
        BuildCustomMaterials();
        MigratedInput = settings["migrazione_input"] is JsonObject migrated
            ? "Valori storici iniziali aggiunti ai campi assenti; controllarli prima dell’uso.\n" + string.Join("\n", migrated.Select(p => p.Key + " = " + p.Value)) : null;
        Coefficients = BuildCoefficients();
        Layers = BuildLayers();
        RefreshAdditionalLayers();
        (Stirrups, StirrupScheme) = BuildStirrups();
        Groups = (MigratedInput is null ? [] : new[] { new ConcreteGroupViewModel("Valori integrati dal documento precedente", false) })
            .Concat([new("Coefficienti da normativa", false), new("Materiali", true), new("Geometria", true), new("Armature", false), new("Staffe", false), new("Trefoli", false)])
            .ToArray();
        TendonGroup = new ConcreteGroupViewModel("Trefoli", true);
        Group = Groups.ToDictionary(g => GroupKey(g.Title), StringComparer.Ordinal);
        // The viewport of the section: the dimensions saved in the sheet.
        Drawing.Dimensions = settings.B("quote_dimensioni", true); Drawing.CoverDimensions = settings.B("quote_copriferro", true); Drawing.SpacingDimensions = settings.B("quote_interferro", true);
        Bars = new ConcreteTableViewModel([new("id", "Barra", readOnly: true), new("x", "x [mm]"), new("y", "y [mm]"),
            new("materiale", "Materiale", ConcreteColumnKind.Choice), new("phi", "Ø [mm]")], [], () => sheet.IsReadOnly);
        Bars.SelectionChanged += (_, _) => { Drawing.SelectedBar = Bars.Selected?.Values.S("id") ?? ""; Drawing.Invalidate(); };
        Drawing.BarClicked += (_, index) => { if (index < Bars.Rows.Count) Bars.SelectedIndex = index; };
        // The tables of the bars and of the cables get their views here (the bars are added later by the preview).
        Bars.StartView(); Tendons.StartView();
        // The dividers of the control panel (Rows, ResultColumns, WorkspaceLayout): the first one creates «layout».
        if (settings["layout"] is not JsonObject) settings["layout"] = new JsonObject();
        QuickResistance = BuildQuickResistance();
        RestoreBarsCommand = new RelayCommand(RestoreBars, () => sheet.CanEdit);
        ToggleHoleCommand = new RelayCommand(ToggleHole, () => sheet.CanEdit && holeEnabled);
        AddCableCommand = new RelayCommand(AddCable, () => sheet.CanEdit);
        RemoveCableCommand = new RelayCommand(RemoveCable, () => sheet.CanEdit);
        ResetCoefficientsCommand = new RelayCommand(() => { ResetCoefficients(); sheet.Invalidate(); }, () => sheet.CanEdit);
        NewConcreteMaterialCommand = new AsyncRelayCommand(() => EditMaterialAsync("Calcestruzzo"), () => sheet.CanEdit);
        NewSteelMaterialCommand = new AsyncRelayCommand(() => EditMaterialAsync("Acciaio"), () => sheet.CanEdit);
        NewTendonMaterialCommand = new AsyncRelayCommand(() => EditMaterialAsync("Trefoli"), () => sheet.CanEdit);
        ApplySavedMaterialCommand = new RelayCommand(ApplySavedMaterial, () => sheet.CanEdit);
        OpenRheologyCommand = new RelayCommand(() => sheet.RequestRheology(RheologyStrength));
        OpenPropertiesCommand = new RelayCommand(sheet.OpenProperties, () => sheet.CanEdit);
        OpenWizardCommand = new AsyncRelayCommand(OpenWizardAsync, () => sheet.CanEdit);
    }

    private JsonObject Input => sheet.Input;
    private JsonObject Settings => sheet.Settings;

    // ---------------------------------------------------------------- forms

    /// <summary>Standard and note of the sheet.</summary>
    public FormViewModel Standard { get; }
    /// <summary>The note of the standard under the first form.</summary>
    public string StandardNote { get => standardNote; private set => SetProperty(ref standardNote, value); }
    private string standardNote = "";
    /// <summary>The values added by the migration of a previous document (the group «Valori integrati…»); null without migration.</summary>
    public string? MigratedInput { get; }
    public FormViewModel Coefficients { get; }
    /// <summary>«Ripristina coefficienti della normativa».</summary>
    public IRelayCommand ResetCoefficientsCommand { get; }
    public FormViewModel Materials { get; }
    public FormViewModel Geometry { get; }
    public FormViewModel Hole { get; }
    public FormViewModel Reinforcement { get; }
    public IReadOnlyList<ConcreteLayerViewModel> Layers { get; }
    /// <summary>Links: present, diameter, spacing.</summary>
    public FormViewModel Stirrups { get; }
    /// <summary>Scheme of the links (shear options of the sheet).</summary>
    public FormViewModel StirrupScheme { get; }
    /// <summary>The form «+ Cavo / trefolo» (values not saved in the sheet).</summary>
    public FormViewModel Cable { get; }
    /// <summary>The properties of the tendon of the standard (shown) and its law (written for the material).</summary>
    public FormViewModel TendonProperties { get; private set; } = null!;
    /// <summary>N and kind of the quick resistances.</summary>
    public FormViewModel QuickResistance { get; }

    /// <summary>The expanders of the column of the inputs, in order (open or closed as the historic view opens them).</summary>
    public IReadOnlyList<ConcreteGroupViewModel> Groups { get; }
    /// <summary>The expander «Trefoli» of the materials.</summary>
    public ConcreteGroupViewModel TendonGroup { get; }

    /// <summary>The groups of the panel by a short key, for the bindings of the XAML view: «valori» (only with values of a former document),
    /// «coefficienti», «materiali», «geometria», «armature», «staffe», «trefoli».</summary>
    public IReadOnlyDictionary<string, ConcreteGroupViewModel> Group { get; }

    private static string GroupKey(string title) => title switch
    {
        "Coefficienti da normativa" => "coefficienti", "Materiali" => "materiali", "Geometria" => "geometria", "Armature" => "armature",
        "Staffe" => "staffe", "Trefoli" => "trefoli", _ => "valori"
    };

    /// <summary>Every form of the inputs, in the order of the panel.</summary>
    public IEnumerable<FormViewModel> AllForms => new[] { Standard, Coefficients, Materials, Geometry, Hole, Reinforcement }
        .Concat(Layers.Select(l => l.Form)).Concat([Stirrups, StirrupScheme, Cable, TendonProperties, QuickResistance]);

    // ---------------------------------------------------------------- hole and reinforcement

    /// <summary>«Foro centrale».</summary>
    public bool HoleChecked { get => holeChecked; private set => SetProperty(ref holeChecked, value); }
    /// <summary>The hole exists for the shape (circle and rectangle).</summary>
    public bool HoleEnabled { get => holeEnabled; private set { if (SetProperty(ref holeEnabled, value)) ToggleHoleCommand.NotifyCanExecuteChanged(); } }
    /// <summary>The fields of the hole are shown while the hole is active.</summary>
    public bool HoleFieldsVisible { get => holeFieldsVisible; private set => SetProperty(ref holeFieldsVisible, value); }
    /// <summary>A click on «Foro centrale».</summary>
    public IRelayCommand ToggleHoleCommand { get; }
    /// <summary>The fields of the reinforcement accept the user: false while the coordinates of the bars are active.</summary>
    public bool ReinforcementEnabled { get => reinforcementEnabled; private set => SetProperty(ref reinforcementEnabled, value); }
    public string? ReinforcementToolTip { get => reinforcementToolTip; private set => SetProperty(ref reinforcementToolTip, value); }
    /// <summary>«Ripristina barre da wizard».</summary>
    public IRelayCommand RestoreBarsCommand { get; }
    /// <summary>The fields of the hole accept the user: false for a section of the wizard (its hole is edited in the wizard).</summary>
    public bool HoleFormEnabled { get => holeFormEnabled; private set => SetProperty(ref holeFormEnabled, value); }

    // ---------------------------------------------------------------- sections made by the wizard (S1b)

    /// <summary>The text of the command of the wizard in the group «Geometria».</summary>
    public string WizardCommandText => "Wizard tipologie / modifica disposizione…";
    /// <summary>
    /// «Wizard tipologie / modifica disposizione…»: commits the sheet, shows <see cref="ConcreteTemplateWizardViewModel"/> on the dialog service
    /// and, after «Applica sezione», shows the section of the wizard in the forms (the shape added to the choices of «Sezione» when missing),
    /// then invalidates the sheet (OpenTemplateWizard of the historic view). Disabled in a read-only revision.
    /// </summary>
    public IAsyncRelayCommand OpenWizardCommand { get; }
    /// <summary>The section is made by the wizard: the forms of the geometry and of the reinforcement are hidden and disabled, their notes shown.</summary>
    public bool IsTemplateSection
    {
        get => templateSection;
        private set
        {
            if (!SetProperty(ref templateSection, value)) return;
            OnPropertyChanged(nameof(GeometryShown)); OnPropertyChanged(nameof(GeometryEnabled)); OnPropertyChanged(nameof(ReinforcementShown));
            OnPropertyChanged(nameof(TemplateNotesShown));
        }
    }
    /// <summary>The form of the geometry is shown (not for a section of the wizard).</summary>
    public bool GeometryShown => !templateSection;
    /// <summary>The form of the geometry accepts the user (not for a section of the wizard).</summary>
    public bool GeometryEnabled => !templateSection;
    /// <summary>The form of the reinforcement is shown (not for a section of the wizard).</summary>
    public bool ReinforcementShown => !templateSection;
    /// <summary>The two notes of the wizard are shown in place of the forms of the geometry and of the reinforcement.</summary>
    public bool TemplateNotesShown => templateSection;
    public string TemplateGeometryNote => ConcreteInputPresentation.TemplateGeometryNote;
    public string TemplateReinforcementNote => ConcreteInputPresentation.TemplateReinforcementNote;

    // ---------------------------------------------------------------- drawing and bars

    /// <summary>The section drawn in the control panel.</summary>
    public ConcreteSectionDrawingViewModel Drawing { get; } = new();
    /// <summary>The bars of the section (coordinates editable: the first edit writes <c>barre_manuali</c>).</summary>
    public ConcreteTableViewModel Bars { get; }

    /// <summary>The check box «Assi» of the drawing (view only).</summary>
    public bool ShowAxes { get => Drawing.Axes; set { if (Drawing.Axes == value || !sheet.CanEdit) { OnPropertyChanged(); return; } Drawing.Axes = value; Drawing.Invalidate(); OnPropertyChanged(); } }
    /// <summary>The check box «ID» of the drawing (view only).</summary>
    public bool ShowLabels { get => Drawing.Labels; set { if (Drawing.Labels == value || !sheet.CanEdit) { OnPropertyChanged(); return; } Drawing.Labels = value; Drawing.Invalidate(); OnPropertyChanged(); } }

    /// <summary>
    /// The check boxes of the dimensions (<c>quote_dimensioni</c>, <c>quote_copriferro</c>, <c>quote_interferro</c>): saved in the sheet, with
    /// Modified, as the historic view; refused in a read-only revision.
    /// </summary>
    public void SetDimension(string key, bool value)
    {
        if (!sheet.CanEdit) return;
        Settings[key] = value;
        if (key == "quote_dimensioni") Drawing.Dimensions = value; else if (key == "quote_copriferro") Drawing.CoverDimensions = value; else Drawing.SpacingDimensions = value;
        Drawing.Invalidate();
        sheet.RaiseModified();
    }

    /// <summary>The state of a check box of the dimensions.</summary>
    public bool Dimension(string key) => key == "quote_dimensioni" ? Drawing.Dimensions : key == "quote_copriferro" ? Drawing.CoverDimensions : Drawing.SpacingDimensions;

    /// <summary>The check box «Dimensioni» of the XAML view (<see cref="SetDimension"/> with <c>quote_dimensioni</c>).</summary>
    public bool ShowDimensions { get => Drawing.Dimensions; set { SetDimension("quote_dimensioni", value); OnPropertyChanged(); } }
    /// <summary>The check box «Copriferro» of the XAML view (<see cref="SetDimension"/> with <c>quote_copriferro</c>).</summary>
    public bool ShowCoverDimensions { get => Drawing.CoverDimensions; set { SetDimension("quote_copriferro", value); OnPropertyChanged(); } }
    /// <summary>The check box «Interferro min.» of the XAML view (<see cref="SetDimension"/> with <c>quote_interferro</c>).</summary>
    public bool ShowSpacingDimensions { get => Drawing.SpacingDimensions; set { SetDimension("quote_interferro", value); OnPropertyChanged(); } }

    // ---------------------------------------------------------------- rheology and properties

    /// <summary>The strength passed to the window «Reologia del calcestruzzo…» (fck as a magnitude).</summary>
    public double RheologyStrength => ConcreteInputPresentation.RheologyStrength(Input);
    /// <summary>«Reologia del calcestruzzo…»: asks the view to open the non modal window of F5.5 (allowed also when read-only).</summary>
    public IRelayCommand OpenRheologyCommand { get; }
    /// <summary>«Proprietà / report…»: the non modal window of the properties of the section.</summary>
    public IRelayCommand OpenPropertiesCommand { get; }

    // ---------------------------------------------------------------- refresh (called by the sheet in the order of RefreshPreview)

    /// <summary>The controls of the hole from the data (refreshHoleControls of the historic view).</summary>
    internal void RefreshHoleControls()
    {
        HoleChecked = Input.B("foro_presente");
        HoleEnabled = ConcreteInputPresentation.HoleToggleEnabled(Input);
        // The fields of the hole read the data again (only shown) and are disabled for a section of the wizard.
        foreach (var field in Hole.Fields) Hole.Set(field.Key, Input.S(field.Key), true);
        HoleFormEnabled = !ConcreteInputPresentation.TemplateSection(Input);
        UpdateHoleFields();
    }

    /// <summary>The reinforcement locked by the coordinates of the bars and the fields of each shape.</summary>
    internal void RefreshShapeFields()
    {
        string shape = Input.S("shape");
        IsTemplateSection = ConcreteInputPresentation.TemplateSection(Input);
        ReinforcementEnabled = !ConcreteInputPresentation.ManualBars(Input) && GeometryEnabled;
        ReinforcementToolTip = ConcreteInputPresentation.ReinforcementToolTip(Input, ReinforcementEnabled);
        foreach (string key in ConcreteInputPresentation.ShapeFields) Geometry.ShowField(key, ConcreteInputPresentation.GeometryFieldShown(key, shape));
        foreach (var field in Reinforcement.Fields.Where(f => !f.Key.StartsWith("transverse", StringComparison.Ordinal)))
            Reinforcement.ShowField(field.Key, ConcreteInputPresentation.ReinforcementFieldShown(field.Key, shape));
    }

    /// <summary>The second layers: shown by the shape, locked by the coordinates of the bars, active or not.</summary>
    internal void RefreshAdditionalLayers()
    {
        foreach (var layer in Layers)
        {
            layer.IsShown = ConcreteInputPresentation.LayerVisible(layer.Key, Input);
            bool enabled = Input.B("second_" + layer.Key + "_enabled");
            bool locked = ConcreteInputPresentation.LayersLocked(Input);
            layer.ToggleEnabled = layer.FormEnabled = !locked;
            layer.ToggleToolTip = ConcreteInputPresentation.LayerToolTip(locked);
            layer.ToggleText = ConcreteInputPresentation.LayerToggleText(enabled, layer.Title);
            layer.FormVisible = enabled;
        }
    }

    /// <summary>The values of the materials computed by the preview (fcd, Ecm, strains, fyd, n), or «—» when the data give no section.</summary>
    internal void ShowPreviewValues(ConcretePreview? values)
    {
        if (values is not null)
        {
            Materials.Set("__fcd", EngineeringFormat.Number(values.Fcd), true); Materials.Set("__fyd", EngineeringFormat.Number(values.Fyd), true);
            Materials.Set("__ecm", EngineeringFormat.Number(values.Ecm), true); Materials.Set("__ec2", EngineeringFormat.Number(values.Ec2), true); Materials.Set("__ecu", EngineeringFormat.Number(values.Ecu), true);
            Materials.Set("__nmode", "φ nelle schede SLE", true);
            Materials.Set("n", EngineeringFormat.Number(values.N), true);
        }
        else foreach (var field in Materials.Fields.Where(f => f.Key.StartsWith("__", StringComparison.Ordinal))) Materials.Set(field.Key, "—", true);
    }

    /// <summary>The cables drawn: those with coordinates and a positive area.</summary>
    internal List<ConcreteTendonPoint> TendonPoints()
    {
        var points = new List<ConcreteTendonPoint>();
        foreach (var row in Tendons.Rows)
        {
            var t = row.Values; var x = J.Number(t["x"]); var y = J.Number(t["y"]); var a = J.Number(t["area"]);
            if (x is double xx && y is double yy && a is > 0) points.Add(new(t.S("id"), xx, yy, a.Value));
        }
        return points;
    }

    /// <summary>The links shown in the panel: values, fields enabled by «Staffe presenti», fields of the circular scheme.</summary>
    internal void SynchronizeStirrups(JsonObject shear)
    {
        foreach (var form in new[] { Stirrups, StirrupScheme }) SynchronizeStirrupForm(form, Input, shear);
    }

    /// <summary>
    /// A form of the links (SynchronizeStirrups of the historic view, for every form of the links, also those shown read-only in the tabs of the
    /// shear and of the detailing): the values shown from the inputs and from the shear options, every field but «Staffe presenti» enabled only
    /// with links, the fields of the circular scheme shown only for circular links (not for the sections of the wizard).
    /// </summary>
    internal static void SynchronizeStirrupForm(FormViewModel form, JsonObject input, JsonObject shear)
    {
        foreach (var field in form.Fields) form.Set(field.Key, field.Key.StartsWith("transverse_", StringComparison.Ordinal) || field.Key == "staffe_presenti" ? input.S(field.Key) : shear.S(field.Key), true);
        foreach (var field in form.Fields.Where(f => f.Key != "staffe_presenti")) form.Enable(field.Key, input.S("staffe_presenti", "Sì") == "Sì");
        bool circular = ConcreteInputPresentation.CircularLinkScheme(input);
        form.ShowField("tipo_staffa", circular); form.ShowField("rami_interni", circular); form.ShowField("schema_interno", circular); form.ShowField("rotazione_staffa", circular);
        foreach (string key in new[] { "rami_x", "rami_y" }) form.ShowField(key, true);
    }

    /// <summary>The coefficients of the tendons are shown only with cables.</summary>
    internal void RefreshTendonOptions()
    {
        bool present = Tendons.Rows.Count > 0;
        foreach (var (key, _) in ConcreteEditor.Coefficients.Where(c => c.Key.Contains("Prestress", StringComparison.Ordinal))) Coefficients.ShowField(key, present);
    }

    /// <summary>The rows of the bars from the section of the engine, unless the coordinates of the bars are active and the table has rows.</summary>
    internal void RefreshBars(ConcreteGeometry? engine)
    {
        if (Input["barre_manuali"] is JsonArray && Bars.Rows.Count > 0) { ReloadBarMaterials(); return; }
        Bars.Rows.Clear();
        if (engine is null) { ReloadBarMaterials(); return; }
        for (int i = 0; i < engine.Bars.Count; i++)
        {
            var b = engine.Bars[i];
            var values = J.Obj(("id", "B" + (i + 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture)), ("x", ConcreteInputPresentation.Exact(b.X)),
                ("y", ConcreteInputPresentation.Exact(b.Y)), ("phi", ConcreteInputPresentation.Exact(b.Diametro)));
            if (Input["barre_manuali"] is JsonArray manual && manual[i] is JsonObject stored && stored.ContainsKey("acciaio"))
                values["acciaio"] = stored["acciaio"]?.DeepClone();
            Bars.Rows.Add(EditableBar(values));
        }
        ReloadBarMaterials();
    }

    private void ReloadBarMaterials()
    {
        foreach (var row in Bars.Rows) row.Output("materiale", ConcreteBarMaterialPresentation.Label(Input, row.Values));
        Bars.Columns.First(c => c.Key == "materiale").Choices = new[] { ConcreteBarMaterialPresentation.DefaultLabel(Input) }
            .Concat(ConcreteBarMaterialPresentation.Catalog(Settings).Select(m => m.S("nome")))
            .Concat(Bars.Rows.Select(r => r.Values.S("materiale"))).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>The read-only revision changed: the forms and the tables of the bars and of the cables refuse or accept the user, the commands follow.</summary>
    internal void RefreshEditable(bool readOnly)
    {
        foreach (var form in AllForms) form.IsReadOnly = readOnly;
        // The grids of the view read IsReadOnly again (F5.10, traces through the view: the bars stayed editable in a read-only revision).
        Bars.RefreshReadOnly(); Tendons.RefreshReadOnly();
        foreach (var command in new IRelayCommand[] { RestoreBarsCommand, ToggleHoleCommand, AddCableCommand, RemoveCableCommand, ResetCoefficientsCommand,
            NewConcreteMaterialCommand, NewSteelMaterialCommand, NewTendonMaterialCommand, ApplySavedMaterialCommand, OpenPropertiesCommand, OpenWizardCommand }) command.NotifyCanExecuteChanged();
        foreach (var layer in Layers) layer.ToggleCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanEditTables));
    }

    /// <summary>The tables of the bars and of the cables accept edits (not in a read-only revision).</summary>
    public bool CanEditTables => sheet.CanEdit;

    /// <summary>Writes the texts typed and not confirmed of every form.</summary>
    internal void Commit() { foreach (var form in AllForms) form.Commit(); }

    // ---------------------------------------------------------------- private

    private FormViewModel Form(JsonObject values, IEnumerable<FieldSpec> fields, Action<string> changed, bool compact = true, bool wideChoices = false, string? wikiModule = null)
    {
        // As the historic view: every field confirms on focus loss and shows its numbers formatted (CommitOnFocusLoss and
        // NumericPresentation set on the whole workspace); F1 and «?» open the Wiki of the services (lesson of 17/10).
        var form = new FormViewModel(values, fields, changed, compact, wideChoices, wikiModule: wikiModule)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        return form;
    }

    private void UpdateHoleFields()
    {
        HoleFieldsVisible = Input.B("foro_presente");
        foreach (string key in new[] { "inner_diameter_mm", "inner_width_mm", "inner_height_mm" }) Hole.ShowField(key, ConcreteInputPresentation.HoleFieldShown(key, Input.S("shape")));
    }

    private void ToggleHole()
    {
        if (!sheet.CanEdit || !holeEnabled) return;
        HoleChecked = !HoleChecked;
        Input["foro_presente"] = HoleChecked;
        UpdateHoleFields();
        sheet.Invalidate();
    }

    /// <summary>The wizard of the types on the data of the sheet, with the Wiki of the services.</summary>
    public ConcreteTemplateWizardViewModel CreateWizardDialog() => new(sheet.Data, sheet.Wiki);

    private async Task OpenWizardAsync()
    {
        if (!sheet.CanEdit) return;
        sheet.CommitSheet();
        if (!await sheet.Dialogs.ShowDialogAsync(CreateWizardDialog()) || sheet.IsDisposed) return;
        var shape = Geometry["shape"];
        if (ConcreteInputPresentation.ShapeChoicesAfterWizard(shape.Choices, Input.S("shape")) is { } choices) shape.ReplaceChoices(choices);
        foreach (var form in new[] { Geometry, Reinforcement }.Concat(Layers.Select(l => l.Form)))
            foreach (var field in form.Fields) form.Set(field.Key, Input.S(field.Key), true);
        // The wizard rewrites the parameters of the shear (S5): the form of the shear shows them again.
        sheet.Shear?.ReloadFields();
        RefreshHoleControls();
        sheet.Invalidate();
    }

    private void RestoreBars()
    {
        if (!sheet.CanEdit) return;
        Input.Remove("barre_manuali");
        sheet.Invalidate();
    }

    // A bar of the table: the first edit writes the coordinates of every bar, invalidates without refreshing the preview, then refreshes it
    // one turn of the UI thread later (as the historic view posts RefreshPreview after the commit of the cell).
    private JsonRowViewModel EditableBar(JsonObject values) => new(values, field =>
    {
        if (field == "materiale") ConcreteBarMaterialPresentation.Select(values, values.S(field), Input, Settings);
        Input["barre_manuali"] = new JsonArray(Bars.Rows.Select(r => (JsonNode)ConcreteBarMaterialPresentation.StoredRow(r.Values)).ToArray());
        sheet.Invalidate(false);
        sheet.PostRefreshPreview();
    });

    private ConcreteGeometry? lastEngine;
    internal ConcreteGeometry? LastEngine { get => lastEngine; set => lastEngine = value; }
}
