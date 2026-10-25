using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>A check box of the bar of a domain panel («Sollecitazioni», «Resistenze»…): its option, its label, its default.</summary>
public sealed class ConcreteDomainOptionViewModel : ObservableObject
{
    private readonly ConcreteDomainPanelViewModel panel;

    internal ConcreteDomainOptionViewModel(ConcreteDomainPanelViewModel panel, string key, string label, bool initial)
    {
        this.panel = panel; Key = key; Label = label; Initial = initial;
    }

    public string Key { get; }
    public string Label { get; }
    internal bool Initial { get; }
    /// <summary>The state of the check box: the option of the panel (its default when missing). A click saves it with Modified; allowed also in a
    /// read-only revision, as the bar of the historic drawing.</summary>
    public bool IsChecked { get => panel.Options.B(Key, Initial); set => panel.SetDisplayOption(this, value); }
    internal void Refresh() => OnPropertyChanged(nameof(IsChecked));
}

/// <summary>
/// The 2D domain as the plain data of its chart (the Plot of the historic panel, decisions Q3 and C8): titles, segments of the domain, markers
/// and lines of the actions, factors of the axes. Geometry and actions come from <see cref="ConcreteDomainChart"/> (ANTHEA.Application).
/// </summary>
public sealed class ConcreteDomain2DViewModel : ObservableObject
{
    public string Title { get; private set; } = "Sezione del dominio";
    public string XLabel { get; private set; } = "Forza assiale [kN]";
    public string YLabel { get; private set; } = "Profondità z [m]";
    public string Note { get; private set; } = "";
    public string EmptyMessage { get; private set; } = "Dominio in attesa di aggiornamento automatico";
    public IReadOnlyList<(double[] A, double[] B)> Segments { get; private set; } = [];
    public IReadOnlyList<(double[] A, double[] B)> VerificationSegments { get; private set; } = [];
    public IReadOnlyList<PlotMarkerData> Markers { get; private set; } = [];
    public double ScaleX { get; private set; } = 1;
    public double ScaleY { get; private set; } = 1;
    /// <summary>«Adatta anche le azioni».</summary>
    public bool FitIncludesMarkers { get; private set; } = true;
    /// <summary>The data changed: the view draws them again.</summary>
    public event EventHandler? Changed;

    internal void ApplyGeometry(DomainChartGeometry source)
    {
        Markers = []; Segments = source.Segments; EmptyMessage = source.EmptyMessage;
        if (source.Title is not null) Title = source.Title;
        if (source.XLabel is not null) XLabel = source.XLabel;
        if (source.YLabel is not null) YLabel = source.YLabel;
        if (source.Note is not null) Note = source.Note;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void ApplyActions(DomainChartActions source)
    {
        Markers = source.Markers.ToList(); VerificationSegments = source.Lines.ToList();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Invalidate of the historic view: series, segments, markers and verification lines emptied.
    internal void Clear()
    {
        Markers = []; Segments = []; VerificationSegments = [];
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void ClearSegments() { Segments = []; Changed?.Invoke(this, EventArgs.Empty); }

    internal void SetScale(double x, double y, bool fitMarkers) { ScaleX = x; ScaleY = y; FitIncludesMarkers = fitMarkers; Changed?.Invoke(this, EventArgs.Empty); }
}

/// <summary>
/// A domain panel of the RC section, 3D (tab 02) or 2D (tab 03) (F5.10, sub-step S3: DomainPanel, BuildDomainPanel, AttachDomainRows,
/// RefreshDomainPanel and UpdateSelection of the historic ConcreteDomains.cs, the domain helpers of ConcreteRefinements.cs), without WPF:
/// <list type="bullet">
/// <item>the form of the options (<see cref="Form"/>, on the options «dominio3d» or «dominio2d» of the sheet): limit state and filter of the rows
/// (allowed also in a read-only revision), search of the resistance, mesh, angles, axes, plane of the 2D section, with the invalidations of the
/// historic view;</item>
/// <item>the table of the actions of the limit state (<see cref="Table"/>, the model of S2), filtered by the outcome of the domain;</item>
/// <item>the detail of the selected row, the summary of the checks, the bar of the drawing (forces, check boxes, sizes of the points,
/// transparency, mesh) and the drawing as plain data (<see cref="View3D"/> or <see cref="Chart"/>, decisions Q3 and C8).</item>
/// </list>
/// The rows belong to the sheet; the panel writes the outputs of the rows of its limit state (η and outcome) as the historic panel. The drawing
/// and the detail follow the selection only while the table is shown (<see cref="IsShown"/>), as the historic grid; until then they wait
/// (<see cref="NeedsVisualRefresh"/>). The section σ/ε of the tab «Sezione σ / ε» is <see cref="Inspection"/> (sub-step S7): the panel gives it
/// its inner tab and its two check boxes.
/// </summary>
public sealed class ConcreteDomainPanelViewModel : ObservableObject, IViewTabMemory
{
    private readonly ConcreteSheetViewModel sheet;
    private readonly Dictionary<string, ConcreteResults.Mesh> meshes = new(StringComparer.Ordinal);
    private string detail = "Selezionare una combinazione";
    private int inspectionTab = -1;
    private bool attaching, neutralAxis, barValues;

    /// <summary>Inner tabs chosen in the view of the panel (<see cref="IViewTabMemory"/>, SH49 reduced): in memory, not saved.</summary>
    public IDictionary<string, int> SelectedTabs { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
    private double actionSize, resistanceSize, transparency;
    private string? outputKey;
    private Dictionary<string, ConcreteResults.Check>? outputChecks;

    internal ConcreteDomainPanelViewModel(ConcreteSheetViewModel sheet, ConcreteActionTableViewModel table, bool threeD)
    {
        this.sheet = sheet; ThreeD = threeD; Table = table;
        Options = sheet.Settings[threeD ? "dominio3d" : "dominio2d"]!.AsObject();
        // BuildDomainPanel of the historic view, in its order; nothing is written.
        bool unified = table.Unified;
        var fields = new List<FieldSpec> { new("stato", unified ? "Stato limite" : "Tipo di dominio precedente", Choices: ["SLU", "SLV"]) };
        if (unified) fields.Add(new("modello_resistente", "Criterio resistente", Choices: ["Plastico", "Elastico"]));
        if (threeD) fields.AddRange([new("criterio", "Ricerca resistenza", Choices: ConcreteEditor.ResistanceCriteria), new("strategia", "Metodo", Choices: ["Iterativo", "Intersezione"]),
            new("interpolazione", "Interpolazione mesh", Choices: ["Quadratica", "Lineare"]), new("suddivisioni_n", "Suddivisioni N (mesh)")]);
        else fields.AddRange([new("tipo", "Sezione dominio", Choices: ["N–M", "Mx–My"]), new("theta", "Direzione θ", "°"), new("N", "N fissato", "kN")]);
        fields.AddRange([new("angoli", "Direzioni angolari"), new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]),
            new("assi", "Assi delle azioni", Choices: ["Locali", "Principali", "Personalizzati"]), new("origine_x", "Origine x", "mm"), new("origine_y", "Origine y", "mm"),
            new("rotazione", "Rotazione assi", "°")]);
        if (!threeD) fields.Add(new("proietta", "Proietta azioni sul piano", Choices: ["No", "Sì"]));
        fields.Add(new("filtro", "Filtro azioni", Choices: ["Tutte", "Entro il dominio", "Fuori dominio", "Da controllare"]));
        Form = new FormViewModel(Options, fields, Changed, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        // The limit state and the filter stay usable in a read-only revision (RevisionInspection.Allow of the historic view).
        Form.AllowWhenReadOnly("stato", "filtro");
        Form.GroupFields("Discretizzazione e interpolazione", ["angoli", "interpolazione", "suddivisioni_n"]);
        Form.GroupFields("Modello e assi delle azioni", ["trazione_cls", "assi", "origine_x", "origine_y", "rotazione"]);
        transparency = Math.Clamp(ConcreteDomainChart.Transparency(Options), 0, 100);
        if (threeD)
        {
            View3D = new ConcreteDomain3DViewModel { SurfaceOpacity = 1 - transparency / 100 };
            View3D.ActionPicked += (_, id) => Table.Table.Selected = sheet.ActionRows(Key).FirstOrDefault(r => r.Values.S("id") == id);
        }
        else Chart = new ConcreteDomain2DViewModel();
        ApplyScale();
        if (View3D is not null) View3D.FitIncludesActions = Options.B("fit_azioni");
        actionSize = Math.Clamp(ConcreteDomainChart.PointSize(Options, "dimensione_ed"), 2, 14);
        resistanceSize = Math.Clamp(ConcreteDomainChart.PointSize(Options, "dimensione_rd"), 2, 14);
        ForceCommand = new RelayCommand(ToggleForces);
        MeshCommand = new RelayCommand(() => View3D?.ToggleWireframe());
        SectionOnActionCommand = new RelayCommand(SectionOnAction, () => sheet.CanEdit);
        DisplayOptions = [new(this, "mostra_ed", "Sollecitazioni", true), new(this, "mostra_rd", "Resistenze", true), new(this, "tutte_rd", "Tutti i punti resistenti", false),
            new(this, "mostra_linee", "Linee di verifica", true), new(this, "colora_eta", "Colori η", false)];
        neutralAxis = Options.B("asse_neutro", true);
        // The section σ/ε of the inner tab (S7), with the neutral axis of the options.
        Inspection = new ConcreteSectionInspectionViewModel(sheet, this, neutralAxis);
        // The selection and the filter of the table move the drawing and the detail (onSelection of ActionTable, Refresh of WithFilters).
        Table.Table.SelectionChanged += (_, _) => UpdateSelection();
        Table.Table.FilterApplied += (_, _) => UpdateSelection();
        EnableFields();
    }

    public bool ThreeD { get; }
    /// <summary>The options of the panel in the settings of the sheet (<c>dominio3d</c> or <c>dominio2d</c>).</summary>
    public JsonObject Options { get; }
    /// <summary>The limit state shown (SLU or SLV).</summary>
    public string Key => Options.S("stato", "SLU");
    /// <summary>«3D:» or «2D:», the prefix of the checks of the panel.</summary>
    public string Prefix => ThreeD ? "3D:" : "2D:";
    /// <summary>«Opzioni di calcolo».</summary>
    public FormViewModel Form { get; }
    /// <summary>The table of the actions of the limit state (S2).</summary>
    public ConcreteActionTableViewModel Table { get; }
    /// <summary>The 3D domain (tab 02); null for the 2D panel.</summary>
    public ConcreteDomain3DViewModel? View3D { get; }
    /// <summary>The section of the domain (tab 03); null for the 3D panel.</summary>
    public ConcreteDomain2DViewModel? Chart { get; }
    /// <summary>«Dettagli combinazione».</summary>
    public string Detail { get => detail; private set => SetProperty(ref detail, value); }
    /// <summary>«Riepilogo verifiche» of the checks of the panel.</summary>
    public ConcreteCardsViewModel Summary { get; } = new();
    /// <summary>The check boxes of the bar of the drawing.</summary>
    public IReadOnlyList<ConcreteDomainOptionViewModel> DisplayOptions { get; }
    /// <summary>«Forze: tutte» / «Forze: selezionata».</summary>
    public string ForceText => Options.B("solo_selezionata") ? "Forze: selezionata" : "Forze: tutte";
    /// <summary>Shows all the visible actions or only the selected one; allowed in a read-only revision.</summary>
    public IRelayCommand ForceCommand { get; }
    /// <summary>«Mesh» of the 3D domain: the edges of the triangles.</summary>
    public IRelayCommand MeshCommand { get; }
    /// <summary>«Sezione sull’azione selezionata» of the 2D domain (not in a read-only revision).</summary>
    public IRelayCommand SectionOnActionCommand { get; }

    /// <summary>The slider «Punti Ed · dimensione» (2–14): saved in the options with Modified (not in a read-only revision).</summary>
    public double ActionPointSize { get => actionSize; set => SetSize("dimensione_ed", value); }
    /// <summary>The slider «Punti Rd · dimensione» (2–14).</summary>
    public double ResistancePointSize { get => resistanceSize; set => SetSize("dimensione_rd", value); }
    /// <summary>The transparency of the 3D domain (0–100 %): saved in the options with Modified; allowed in a read-only revision.</summary>
    public double Transparency
    {
        get => transparency;
        set
        {
            value = Math.Clamp(value, 0, 100);
            if (!ThreeD || value == transparency) return;
            transparency = value; Options["trasparenza"] = value; View3D!.SurfaceOpacity = 1 - value / 100;
            OnPropertyChanged(); sheet.RaiseModified();
        }
    }

    /// <summary>«Asse neutro» of the section σ/ε: saved in the options with Modified, the map follows (S7); allowed in a read-only revision.</summary>
    public bool NeutralAxis
    {
        get => neutralAxis;
        set { if (!SetProperty(ref neutralAxis, value)) return; Options["asse_neutro"] = value; Inspection.ShowNeutralAxis(value); sheet.RaiseModified(); }
    }

    /// <summary>«Valori σ/ε sulle barre» of the section σ/ε: the map follows (S7, view only; not in a read-only revision).</summary>
    public bool BarValues { get => barValues; set { if (sheet.CanEdit && SetProperty(ref barValues, value)) Inspection.ShowBarValues(value); } }

    /// <summary>The section σ/ε of the inner tab «Sezione σ / ε» (S7).</summary>
    public ConcreteSectionInspectionViewModel Inspection { get; }

    /// <summary>
    /// The inner tab of the panel: -1 until the panel is on screen the first time (the historic tab control has no tab chosen until it is laid
    /// out), then 0 «Dominio» or 1 «Sezione σ / ε». Every change updates the section σ/ε (S7).
    /// </summary>
    public int InspectionTab
    {
        get => inspectionTab;
        set
        {
            if (value is < -1 or > 1 || value == inspectionTab) return;
            inspectionTab = value; OnPropertyChanged();
            // The SelectionChanged of the historic inner tabs.
            _ = Inspection.UpdateAsync();
        }
    }

    /// <summary>
    /// The table of the panel is on screen: its main tab is chosen. The table is under the inner tabs, so it stays visible also with «Sezione σ /
    /// ε» (F5.10 S7, correcting S3: the trace ispezione/sezione-sigma-epsilon shows the 2D section updated after a calculation with that tab).
    /// </summary>
    public bool IsShown => sheet.SelectedTab == (ThreeD ? 1 : 2);

    /// <summary>The drawing and the detail wait for the table to be shown (NeedsVisualRefresh of the historic panel).</summary>
    internal bool NeedsVisualRefresh { get; set; } = true;

    /// <summary>
    /// The panel became visible (IsVisibleChanged of the historic grid): the first time the inner tabs choose «Dominio»; the panel is drawn again
    /// when it waits.
    /// </summary>
    internal void Shown()
    {
        if (!IsShown) return;
        if (inspectionTab == -1) InspectionTab = 0;
        if (NeedsVisualRefresh) RefreshPanel();
    }

    /// <summary>The Wiki of the services (F1 of the fields of the windows of the panel).</summary>
    internal IWikiNavigator Wiki => sheet.Wiki;

    /// <summary>The read-only revision changed: the form and the commands follow.</summary>
    internal void RefreshEditable(bool readOnly)
    {
        Form.IsReadOnly = readOnly;
        SectionOnActionCommand.NotifyCanExecuteChanged();
        Inspection.RefreshEditable();
    }

    // ---------------------------------------------------------------- options

    // The callback of the form (BuildDomainPanel): the filter and the limit state attach the rows again; the options of the mesh draw only the
    // mesh again; the search of the resistant points and the projection keep the domain; the other options draw it again.
    private void Changed(string key)
    {
        if (sheet.Initializing) return;
        if (key is "filtro" or "stato") { Attach(); RefreshPanel(); sheet.RaiseModified(); }
        else if (key is "interpolazione" or "suddivisioni_n") _ = RefreshDisplayMeshAsync();
        else if (key is "criterio" or "strategia" or "proietta") sheet.InvalidateDomainForces(this);
        else
        {
            NeedsVisualRefresh = true;
            if (ThreeD) View3D!.SetMesh(null); else Chart!.ClearSegments();
            sheet.InvalidateDomainForces(this);
        }
        EnableFields();
    }

    private void EnableFields()
    {
        Form.Enable("theta", Options.S("tipo") == "N–M"); Form.Enable("N", Options.S("tipo") == "Mx–My");
        foreach (string field in new[] { "origine_x", "origine_y", "rotazione" }) Form.Enable(field, Options.S("assi") == "Personalizzati");
    }

    // AddDomainScaleControls: the factors of the axes saved in the options.
    private void ApplyScale()
    {
        if (ThreeD) View3D!.SetAxisScale(ConcreteDomainChart.AxisScale(Options, "scala_mx"), ConcreteDomainChart.AxisScale(Options, "scala_n"), ConcreteDomainChart.AxisScale(Options, "scala_my"));
        else Chart!.SetScale(ConcreteDomainChart.AxisScale(Options, "scala_x"), ConcreteDomainChart.AxisScale(Options, "scala_y"), Options.B("fit_azioni"));
    }

    // ---------------------------------------------------------------- rows, drawing, detail

    /// <summary>AttachDomainRows: the rows of the limit state filtered again; the selected row kept when it is shown, otherwise the first.</summary>
    internal void Attach()
    {
        NeedsVisualRefresh = true; attaching = true;
        try { Table.Reattach(); }
        finally { attaching = false; }
    }

    /// <summary>
    /// RefreshDomainPanel: the outputs of the rows when the limit state or its results changed; then, while the table is shown (or with
    /// <paramref name="renderHidden"/>), the drawing of the domain and the selection.
    /// </summary>
    internal void RefreshPanel(bool renderHidden = false)
    {
        var checks = sheet.DomainChecks(Prefix + Key);
        if (outputKey != Key || !ReferenceEquals(outputChecks, checks))
        {
            var rows = sheet.ActionRows(Key);
            using (JsonRowViewModel.DeferNotifications(rows))
            {
                string eta = ThreeD ? "eta3d" : "eta2d", outcome = ThreeD ? "esito3d" : "esito2d";
                foreach (var row in rows)
                {
                    var check = checks?.GetValueOrDefault(row.Values.S("id"));
                    row.Output(eta, EngineeringFormat.Number(check?.Utilization)); row.Output(outcome, check?.Status ?? "Da calcolare");
                }
            }
            outputKey = Key; outputChecks = checks; NeedsVisualRefresh = true;
        }
        if (sheet.IsSynchronizing || !NeedsVisualRefresh || !renderHidden && !IsShown) return;
        if (ThreeD)
        {
            var mesh = sheet.Checker3D(Key)?.DisplayMesh(Options);
            if (mesh is not null) meshes[Key] = mesh;
            View3D!.SetMesh(mesh);
        }
        else Chart!.ApplyGeometry(ConcreteDomainChart.Geometry(Key, Options, sheet.Checker2D(Key), sheet.Calculation.Errors.GetValueOrDefault(Prefix + Key)));
        UpdateSelection(renderHidden);
    }

    /// <summary>UpdateSelection: the drawing and the detail of the selected row, while the table is shown.</summary>
    internal void UpdateSelection(bool renderHidden = false)
    {
        NeedsVisualRefresh = true;
        if (sheet.IsSynchronizing || attaching || !renderHidden && !IsShown) return;
        NeedsVisualRefresh = false;
        // The section σ/ε of the selected row (UpdateSectionInspection of the historic view, S7).
        _ = Inspection.UpdateAsync();
        var row = Table.Table.Selected; string? id = row?.Values.S("id");
        var checks = sheet.DomainChecks(Prefix + Key);
        var selectedCheck = id is null ? null : checks?.GetValueOrDefault(id);
        var shown = Table.Table.Shown;
        if (ThreeD)
        {
            var view = View3D!;
            var visible = ConcreteDomainChart.VisibleActions(Options, shown.Select(r => r.Values), id, checks, sheet.Checker3D(Key));
            view.ActionPointSize = ConcreteDomainChart.PointSize(Options, "dimensione_ed"); view.ResistancePointSize = ConcreteDomainChart.PointSize(Options, "dimensione_rd");
            view.OnlySelectedActions = Options.B("solo_selezionata"); view.ShowActions = Options.B("mostra_ed", true); view.ShowResistance = Options.B("mostra_rd", true);
            view.ShowVerificationLines = Options.B("mostra_linee", true); view.ColorByRatio = Options.B("colora_eta");
            view.Ratios = checks?.ToDictionary(kv => kv.Key, kv => kv.Value.Utilization);
            view.Resistances = Options.B("tutte_rd") ? shown.Where(r => r.Values.B("visible", true))
                .Select(r => (Id: r.Values.S("id"), Check: checks?.GetValueOrDefault(r.Values.S("id")))).Where(r => r.Check?.Resistance is not null)
                .ToDictionary(r => r.Id, r => r.Check!.Resistance!.Value) : null;
            view.VerificationCriterion = Options.S("criterio", "N costante");
            view.SetActions(visible, id, selectedCheck?.Resistance);
        }
        else Chart!.ApplyActions(ConcreteDomainChart.Actions(Options, sheet.Checker2D(Key), shown.Select(r => r.Values).ToArray(), id, checks));
        if (row is null) { Detail = "Nessuna combinazione selezionata"; return; }
        var extrema = ThreeD ? meshes.GetValueOrDefault(Key)?.Vertices.AsEnumerable() : sheet.Checker2D(Key)?.Segments.SelectMany(s => new[] { s.A, s.B });
        Detail = ConcreteDomainChart.Detail(SectionActionInputs.Label(Key, sheet.Settings), ThreeD, Options, row.Values, selectedCheck, extrema,
            sheet.Calculation.Errors.GetValueOrDefault(Prefix + Key));
    }

    /// <summary>Invalidate of the sheet: no mesh, no chart, the detail waits for the calculation; the meshes kept for the extremes are cleared.</summary>
    internal void Invalidated()
    {
        meshes.Clear();
        if (ThreeD) View3D!.SetMesh(null); else Chart!.Clear();
        Detail = "Dati modificati · aggiornamento automatico in attesa";
    }

    // RefreshDisplayMesh: only the mesh shown, on a worker thread, with the checks kept.
    private async Task RefreshDisplayMeshAsync()
    {
        var options = (JsonObject)Options.DeepClone();
        string key = Key; long requested = sheet.Calculation.Revision;
        sheet.RaiseModified();
        if (sheet.Checker3D(key) is not { } domain) return;
        try
        {
            sheet.ShowStatus("Aggiornamento della sola mesh · verifiche conservate…");
            var mesh = await Task.Run(() => domain.DisplayMesh(options));
            if (sheet.IsDisposed || requested != sheet.Calculation.Revision || key != Key || options.S("interpolazione") != Options.S("interpolazione")
                || options.S("suddivisioni_n") != Options.S("suddivisioni_n")) return;
            meshes[key] = mesh; NeedsVisualRefresh = true; RefreshPanel();
            sheet.ShowStatus("Mesh aggiornata · punti resistenti e tassi invariati"); sheet.ResetExportCache();
        }
        catch (ArgumentException failure) { sheet.ShowStatus("Mesh non aggiornata: " + failure.Message); }
    }

    // ---------------------------------------------------------------- the bar of the drawing

    private void ToggleForces()
    {
        Options["solo_selezionata"] = !Options.B("solo_selezionata");
        OnPropertyChanged(nameof(ForceText));
        UpdateSelection(); sheet.RaiseModified();
    }

    // A check box of the bar, saved in the options; «Tutti i punti resistenti» shows also the lines and the resistances.
    internal void SetDisplayOption(ConcreteDomainOptionViewModel option, bool value)
    {
        Options[option.Key] = value;
        if (option.Key == "tutte_rd" && value) { Options["mostra_linee"] = true; Options["mostra_rd"] = true; }
        foreach (var o in DisplayOptions) o.Refresh();
        UpdateSelection(); sheet.RaiseModified();
    }

    // The sliders of the sizes of the points: the value of the slider (2–14) saved in the options, the drawing follows.
    private void SetSize(string key, double value)
    {
        if (!sheet.CanEdit) return;
        value = Math.Clamp(value, 2, 14);
        bool action = key == "dimensione_ed";
        if (value == (action ? actionSize : resistanceSize)) return;
        if (action) actionSize = value; else resistanceSize = value;
        Options[key] = value;
        OnPropertyChanged(action ? nameof(ActionPointSize) : nameof(ResistancePointSize));
        UpdateSelection(); sheet.RaiseModified();
    }

    // ---------------------------------------------------------------- «Scala…» and «Adatta» (the scale controls of the historic panel)

    /// <summary>The zoom of the drawing (1 = fitted), kept by the view (camera of the 3D domain, zoom of the chart); «Scala…» proposes it.</summary>
    public double Zoom { get; set; } = 1;

    /// <summary>The view fits the drawing («Adatta», «Scala…»).</summary>
    public event EventHandler? FitRequested;
    /// <summary>The view sets the zoom of the drawing to <see cref="Zoom"/> («Scala…»).</summary>
    public event EventHandler? ZoomRequested;

    /// <summary>«Scala…»: the window of the factors of the axes, of the zoom and of «Adatta anche le azioni»; allowed in a read-only revision.</summary>
    public IAsyncRelayCommand ScaleCommand => scaleCommand ??= new AsyncRelayCommand(() => sheet.Dialogs.ShowDialogAsync(CreateScaleDialog()));
    private IAsyncRelayCommand? scaleCommand;

    /// <summary>«Adatta»: every factor of the axes back to 1, the drawing fitted, Modified; allowed in a read-only revision.</summary>
    public IRelayCommand FitCommand => fitCommand ??= new RelayCommand(Fit);
    private IRelayCommand? fitCommand;

    /// <summary>The window «Scala…» on the factors of the panel.</summary>
    public ConcreteDomainScaleDialogViewModel CreateScaleDialog() => new(this);

    /// <summary>The view of the 2D chart was reset by the user (ViewReset of the historic Plot): the factors x and y back to 1, Modified.</summary>
    public void ChartViewReset()
    {
        if (ThreeD) return;
        Options["scala_x"] = 1; Options["scala_y"] = 1;
        sheet.RaiseModified();
    }

    private void Fit()
    {
        foreach (string key in new[] { "scala_x", "scala_y", "scala_n", "scala_mx", "scala_my" }) Options[key] = 1;
        ApplyScale();
        FitRequested?.Invoke(this, EventArgs.Empty);
        sheet.RaiseModified();
    }

    // «Applica» of «Scala…»: the factors and «Adatta anche le azioni» saved, the drawing fitted with the zoom chosen, Modified.
    internal void ApplyScaleDialog(IReadOnlyDictionary<string, double> values, bool fitActions, double zoom)
    {
        foreach (var (key, value) in values) Options[key] = value;
        Options["fit_azioni"] = fitActions;
        ApplyScale();
        if (View3D is not null) { View3D.FitIncludesActions = fitActions; FitRequested?.Invoke(this, EventArgs.Empty); }
        Zoom = zoom; ZoomRequested?.Invoke(this, EventArgs.Empty);
        sheet.RaiseModified();
    }

    // «Sezione sull’azione selezionata»: the plane through the selected action, written by the form (its callback invalidates).
    private void SectionOnAction()
    {
        if (!sheet.CanEdit || Table.Table.Selected is not { } row) return;
        try
        {
            var (field, value) = ConcreteDomainChart.PlaneThrough(Options, ConcreteDomainChart.ReadNamedAction(row.Values));
            Form.Set(field, value);
        }
        catch (ArgumentException failure) { sheet.ShowStatus(failure.Message); }
    }
}

/// <summary>
/// The window «Scala del dominio 3D | 2D» of a domain panel (the dialog of AddDomainScaleControls of the historic view, F5.10 S3) on the dialog
/// service: the factors of the axes (0.25–4), the zoom in percent (10–2000 %) and «Adatta anche le azioni»; «Applica» saves them in the
/// options of the panel when they are valid (<see cref="ConcreteDomainChart.ReadScale"/>), otherwise the message says why.
/// </summary>
public sealed class ConcreteDomainScaleDialogViewModel : DialogViewModel<bool>
{
    private readonly ConcreteDomainPanelViewModel panel;
    private readonly IReadOnlyList<string> keys;
    private string message = "Fattori grafici 0,25–4; zoom 10–2000%. Non modificano i calcoli. Adatta ripristina i fattori unitari.";

    internal ConcreteDomainScaleDialogViewModel(ConcreteDomainPanelViewModel panel) : base("Scala del dominio " + (panel.ThreeD ? "3D" : "2D"))
    {
        this.panel = panel;
        keys = ConcreteDomainChart.ScaleKeys(panel.ThreeD);
        Draft = new JsonObject();
        foreach (string key in keys) Draft[key] = ConcreteDomainChart.AxisScale(panel.Options, key).ToString("G12", System.Globalization.CultureInfo.InvariantCulture);
        Draft["zoom"] = (panel.Zoom * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        Draft["fit_azioni"] = panel.Options.B("fit_azioni") ? "Sì" : "No";
        var fields = keys.Select(k => new FieldSpec(k, "Fattore " + k[6..].ToUpperInvariant(), "×")).ToList();
        fields.Add(new("zoom", "Zoom", "%")); fields.Add(new("fit_azioni", "Adatta anche le azioni", Choices: ["No", "Sì"]));
        Form = new FormViewModel(Draft, fields) { WikiNavigator = panel.Wiki };
        ApplyCommand = new RelayCommand(Apply);
        CancelCommand = new RelayCommand(Cancel);
    }

    /// <summary>The values of the window (texts), written by <see cref="Form"/>; the options change only at «Applica».</summary>
    public JsonObject Draft { get; }
    public FormViewModel Form { get; }
    /// <summary>The note of the window, or why «Applica» refused the values.</summary>
    public string Message { get => message; private set => SetProperty(ref message, value); }
    /// <summary>«Applica».</summary>
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand CancelCommand { get; }

    private void Apply()
    {
        Form.Commit();
        try
        {
            var (values, zoom) = ConcreteDomainChart.ReadScale(Draft, keys);
            panel.ApplyScaleDialog(values, Draft.S("fit_azioni") == "Sì", zoom);
            Close(true);
        }
        catch (ArgumentException failure) { Message = failure.Message; }
    }
}
