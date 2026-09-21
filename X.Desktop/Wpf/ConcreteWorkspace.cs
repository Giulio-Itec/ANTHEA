using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    internal JsonObject? Result { get; private set; }
    internal bool Busy { get; private set; }
    internal event Action? Modified;
    private readonly JsonObject settings;
    private JsonObject Input => Data["input"]!.AsObject();
    private readonly TabControl tabs = new() { Margin = new Thickness(12, 8, 12, 0), BorderThickness = new Thickness(0), Background = Ui.Bg };
    private readonly ScrollViewer workspaceScroll = new ChainedScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock status = Ui.Text("Dati della sezione · selezionare una scheda per iniziare", 12);
    private readonly ProgressBar progress = new() { Height = 3, IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private bool calculationQueued;
    private bool forceOnlyUpdate;
    private readonly HashSet<DomainPanel> pendingForcePanels = [];
    private CancellationTokenSource? cancellation;
    private bool initializing = true, disposed, synchronizing;
    private int revision;
    private readonly Dictionary<string, ObservableCollection<JsonRow>> actions = new();
    private readonly List<JsonGrid> grids = [];
    private readonly Dictionary<string, TextBlock> summaries = new();
    private readonly Dictionary<string, SectionDomainMesh> meshes = new();
    private readonly Dictionary<string, CheckerDomain3D> checker3D = new();
    private readonly Dictionary<string, CheckerDomain2D> checker2D = new();
    private readonly Dictionary<string, Dictionary<string, DomainCheck>> domainResults = new();
    private readonly Dictionary<string, Dictionary<string, StressOutcome>> stressResults = new();
    private readonly ConcreteSectionViewport preview = new();
    private readonly JsonGrid barInventory = new([new("id", "Barra", ReadOnly: true), new("x", "x [mm]", ReadOnly: true), new("y", "y [mm]", ReadOnly: true), new("phi", "Ø [mm]", ReadOnly: true)], true);
    private readonly JsonGrid tendons = new([new("id", "ID"), new("x", "x [mm]"), new("y", "y [mm]"), new("area", "Ap [mm²]"), new("sigma0", "σp0 [MPa]"), new("Ep", "Ep [MPa]"), new("fpyk", "fpyk [MPa]"), new("fpk", "fpk [MPa]"), new("eps_u", "εpu [‰]")]);
    private InputForm geometry = null!, materials = null!, reinforcement = null!;
    private readonly List<DomainPanel> domainPanels = [];
    private readonly Dictionary<string, StressPanel> stressPanels = new();

    internal ConcreteWorkspace(JsonObject data)
    {
        Data = data; settings = SectionWorkspace.Prepare(Data); Background = Ui.Bg;
        SetValue(InputForm.CommitOnFocusLossProperty, true);
        PrepareCoefficients(); PrepareStirrups();
        foreach (string key in SectionWorkspace.Sets)
        {
            var rows = new ObservableCollection<JsonRow>(); actions[key] = rows;
            foreach (var item in Data["combinazioni"]![key]!.AsArray())
            {
                var a = item!.Array("azioni");
                rows.Add(CreateAction(key, item.S("nome"), a.ElementAtOrDefault(0)?.ToString() ?? "", a.ElementAtOrDefault(1)?.ToString() ?? "", a.ElementAtOrDefault(2)?.ToString() ?? "", item.S("id"), item.B("visible", true)));
            }
        }
        Loaded += (_, _) => { if (Result is null) QueueCalculation(); };
        var footer = new DockPanel { Margin = new Thickness(16, 4, 16, 8) }; footer.Children.Add(status);
        var workspaceBody = Ui.Dock(tabs, bottom: Ui.Stack(progress, footer));
        workspaceScroll.Content = workspaceBody; Content = workspaceScroll;
        void ResizeWorkspace()
        {
            workspaceBody.Width = Math.Max(1120, double.IsFinite(workspaceScroll.ViewportWidth) ? workspaceScroll.ViewportWidth : 0);
            workspaceBody.Height = Math.Max(700, double.IsFinite(workspaceScroll.ViewportHeight) ? workspaceScroll.ViewportHeight : 0);
        }
        workspaceScroll.SizeChanged += (_, _) => ResizeWorkspace();
        workspaceScroll.ScrollChanged += (_, e) => { if (e.Source == workspaceScroll && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) ResizeWorkspace(); };
        AddTab("01", "Pannello di controllo", BuildControlPanel());
        AddTab("02", "Dominio 3D", BuildDomainPanel(true));
        AddTab("03", "Dominio 2D", BuildDomainPanel(false));
        AddTab("04", "Tensioni e fessurazione", BuildStressTabs());
        AddTab("05", "Taglio", BuildShearPanel());
        tabs.SelectedIndex = Math.Clamp((int)settings.D("tab"), 0, 4);
        tabs.SelectionChanged += (_, e) => { if (e.Source == tabs && !initializing) { Commit(); settings["tab"] = tabs.SelectedIndex; Modified?.Invoke(); } };
        initializing = false; RefreshPreview(); RefreshSummary();
    }
    private void AddTab(string number, string title, UIElement body)
    {
        var header = Ui.Bar(Ui.Text(number, 11, true, Ui.Muted), Ui.Text("  " + title, 14, true));
        tabs.Items.Add(new TabItem { Header = header, Content = body, Padding = new Thickness(14, 9, 14, 9) });
    }
    private static Border Panel(string title, UIElement content, string? subtitle = null)
    {
        var heading = Ui.Stack(Ui.Text(title, 15, true)); heading.Margin = new Thickness(0, 0, 0, 10);
        if (subtitle is not null) { var text = Ui.Text(subtitle, 11, color: Ui.Muted); text.Margin = new Thickness(0, 5, 0, 0); heading.Children.Add(text); }
        return Ui.Paper(Ui.Dock(content, heading), 14);
    }
    private static Grid Columns(params (UIElement Element, double Weight, double Min)[] elements)
    {
        var grid = new Grid();
        for (int i = 0; i < elements.Length; i++)
        {
            if (i > 0)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                var splitter = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch, Background = Ui.Bg, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
                Grid.SetColumn(splitter, grid.ColumnDefinitions.Count - 1); grid.Children.Add(splitter);
            }
            var item = elements[i]; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(item.Weight, GridUnitType.Star), MinWidth = item.Min });
            Grid.SetColumn(item.Element, grid.ColumnDefinitions.Count - 1); grid.Children.Add(item.Element);
        }
        return grid;
    }
    private static Grid Rows(UIElement top, UIElement bottom, double topWeight = 3, double bottomWeight = 2)
    {
        var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(topWeight, GridUnitType.Star), MinHeight = 160 }); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) }); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(bottomWeight, GridUnitType.Star), MinHeight = 155 });
        grid.Children.Add(top); var split = new GridSplitter { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, Background = Ui.Bg, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
        Grid.SetRow(split, 1); grid.Children.Add(split); Grid.SetRow(bottom, 2); grid.Children.Add(bottom); return grid;
    }
    private static ScrollViewer Scroller(UIElement content) => new ChainedScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private static Border Notice(string text)
    {
        var border = Ui.Paper(Ui.Text(text, 11, color: Ui.Brush("#865D16")), 10); border.Background = Ui.Brush("#FFF6DD"); border.BorderBrush = Ui.Brush("#EEDCAF"); border.Margin = new Thickness(0, 8, 0, 8); return border;
    }
    private static Expander Group(string title, UIElement body, bool expanded = false) => new() { Header = Ui.Text(title, 14, true), Content = body, IsExpanded = expanded, Padding = new Thickness(0, 8, 0, 10), Margin = new Thickness(0, 4, 0, 4) };

    private UIElement BuildControlPanel()
    {
        var norm = new InputForm(settings, [new("normativa", "Normativa", Choices: ConcreteStandards.Names), new("nota", "Nota del foglio")], key => { if (key == "normativa") ResetCoefficients(); Invalidate(); });
        geometry = new(Input, [new("shape", "Sezione", Choices: ["Circolare", "Rettangolare", "A T", "Generica (da definire)"]), new("diameter_mm", "Diametro D", "mm"), new("width_mm", "Larghezza b", "mm"), new("height_mm", "Altezza h", "mm"), new("flange_width_mm", "Larghezza ala bf", "mm"), new("web_width_mm", "Larghezza anima bw", "mm"), new("flange_thickness_mm", "Spessore ala hf", "mm"), new("cover_mm", "Copriferro netto", "mm")], _ => Invalidate(), true);
        materials = new(Input, [new("classe_cls", "Classe CLS", Choices: ["C12/15", "C16/20", "C20/25", "C25/30", "C30/37", "C35/45", "C40/50", "C45/55", "C50/60", "C55/67", "C60/75", "C70/85", "C80/95", "C90/105", "Personalizzato"]), new("fck_mpa", "fck", "MPa"), new("fyk_mpa", "fyk", "MPa"), new("steel_modulus_mpa", "Es", "MPa"), new("alpha_cc", "αcc"), new("gamma_c", "γc"), new("gamma_s", "γs"), new("gettato_sottile", "Piano gettato in opera < 50 mm", Choices: ["No", "Sì"]), new("__fcd", "fcd", "MPa", ReadOnly: true), new("__fyd", "fyd", "MPa", ReadOnly: true), new("__ecm", "Ecm", "MPa", ReadOnly: true), new("__ec2", "εc,y (diagramma)", "‰", ReadOnly: true), new("__ecu", "εc,u (diagramma)", "‰", ReadOnly: true), new("n", "Es / Ecm (riferimento)", ReadOnly: true), new("__nmode", "Viscosità", ReadOnly: true)], key =>
        {
            if (key == "classe_cls" && Input.S(key).StartsWith('C')) materials.Set("fck_mpa", Input.S(key)[1..].Split('/')[0]);
            if (key == "n") Data["n_automatico"] = false;
            SyncCoefficientsFromInput(); Invalidate();
        }, true);
        reinforcement = new(Input, [new("longitudinal_bar_count", "Barre circolari"), new("longitudinal_bar_diameter_mm", "Diametro", "mm"), new("top_bar_count", "Barre superiori"), new("top_bar_diameter_mm", "Ø superiori", "mm"), new("bottom_bar_count", "Barre inferiori"), new("bottom_bar_diameter_mm", "Ø inferiori", "mm"), new("side_bar_count_per_side", "Barre laterali / lato"), new("side_bar_diameter_mm", "Ø laterali", "mm")], _ => Invalidate(), true);
        foreach (var item in settings.Array("trefoli").OfType<JsonObject>()) tendons.Rows.Add(new JsonRow((JsonObject)item.DeepClone(), _ => TendonsChanged()));
        grids.Add(tendons); tendons.Height = 165;
        var tendonInput = Ui.Stack(Notice("Trefoli gestiti da Checker. Inserire le proprietà del materiale; σp0 è la tensione iniziale positiva del trefolo. Campi mancanti bloccano il calcolo."), WithFilters(tendons),
            Ui.Bar(Ui.Button("+ Trefolo", () => { var m = settings["materiale_trefolo"]; tendons.Rows.Add(new JsonRow(J.Obj(("id", "T" + (tendons.Rows.Count + 1)), ("x", "0"), ("y", "0"), ("area", "150"), ("sigma0", ""), ("Ep", m.S("Ep")), ("fpyk", m.S("fpyk")), ("fpk", m.S("fpk")), ("eps_u", m.S("eps_u"))), _ => TendonsChanged())); TendonsChanged(); }), Ui.Button("−", () => { tendons.Commit(); if (tendons.SelectedItem is JsonRow r) { tendons.Rows.Remove(r); TendonsChanged(); } })));
        var inputStack = Ui.Stack(norm, standardNote, Group("Geometria", geometry, true), Group("Materiali", Ui.Stack(materials, BuildCustomMaterials())), Group("Coefficienti da normativa / personalizzati", BuildCoefficients()), Group("Armature", reinforcement), Group("Staffe", BuildStirrups()), Group("Trefoli", tendonInput));
        var left = Panel("Definizione della sezione", Scroller(inputStack), "Dati comuni a tutte le verifiche · mm, MPa");
        var viewport = new ViewportFrame("Sezione geometrica", preview, preview.ResetView);
        var axes = new CheckBox { Content = "Assi", IsChecked = true, Margin = new Thickness(7, 3, 5, 3), VerticalAlignment = VerticalAlignment.Center };
        var labels = new CheckBox { Content = "ID", IsChecked = true, Margin = new Thickness(5, 3, 5, 3), VerticalAlignment = VerticalAlignment.Center };
        axes.Click += (_, _) => { preview.Axes = axes.IsChecked == true; preview.InvalidateVisual(); }; labels.Click += (_, _) => { preview.Labels = labels.IsChecked == true; preview.InvalidateVisual(); };
        viewport.Toolbar.Children.Insert(0, axes); viewport.Toolbar.Children.Insert(1, labels);
        barInventory.SelectionChanged += (_, _) => { preview.SelectedBar = (barInventory.SelectedItem as JsonRow)?.Values.S("id") ?? ""; preview.InvalidateVisual(); };
        preview.BarSelected += index => { if (index < barInventory.Rows.Count) { barInventory.SelectedIndex = index; barInventory.ScrollIntoView(barInventory.SelectedItem); } };
        var middle = Rows(viewport, Panel("Coordinate e identificativi delle barre", WithFilters(barInventory), "x e y rispetto al baricentro · clic su una barra per identificarla"), 4, 1.6);
        var summary = new StackPanel();
        foreach (string key in SectionWorkspace.Sets)
        {
            var value = Ui.Text("Non calcolata", 12); summaries[key] = value;
            string title = key.StartsWith("SLE") ? "SLE · " + SectionWorkspace.Label(key) : SectionWorkspace.Label(key);
            var button = Ui.Button("Apri →", () => { tabs.SelectedIndex = key.StartsWith("SLE") ? 3 : 1; if (key.StartsWith("SLE")) sleTabs.SelectedIndex = Array.IndexOf(SectionWorkspace.Sets, key) - 2; else domainPanels[0].Mode.SelectedItem = key; }); button.HorizontalAlignment = HorizontalAlignment.Right;
            var card = Panel(title, Ui.Stack(value, button)); card.Margin = new Thickness(0, 0, 0, 9); summary.Children.Add(card);
        }
        summary.Children.Add(Panel("Taglio", Ui.Stack(shearDashboard, Ui.Button("Apri →", () => tabs.SelectedIndex = 4))));
        summary.Children.Add(Notice("Motore GPC Checker · compressione negativa. Esiti distinti per ogni verifica; nessun esito globale di conformità."));
        var page = Columns((left, 3, 285), (middle, 5.3, 380), (Scroller(summary), 2.7, 250)); page.Margin = new Thickness(0, 10, 0, 0); return page;
    }
    private JsonRow CreateAction(string key, string name, string n, string mx, string my, string? id = null, bool visible = true)
    {
        var values = J.Obj(("id", string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id), ("nome", name), ("N", n), ("Mx", mx), ("My", my), ("visible", visible), ("eta", "—"), ("esito", "Da calcolare"));
        return new JsonRow(values, field =>
        {
            if (synchronizing) return;
            SyncActions(key);
            if (field == "visible") { RefreshDomainViews(); Modified?.Invoke(); }
            else Invalidate(false);
        });
    }
    private void SyncActions(string key)
    {
        Data["combinazioni"]![key] = new JsonArray(actions[key].Select(row => (JsonNode)J.Obj(("id", row.Values.S("id")), ("nome", row.Values.S("nome")), ("azioni", new[] { row.Values.S("N"), row.Values.S("Mx"), row.Values.S("My") }), ("visible", row.Values.B("visible", true)))).ToArray());
    }
    private void TendonsChanged() { settings["trefoli"] = new JsonArray(tendons.Rows.Select(r => r.Values.DeepClone()).ToArray()); Invalidate(); }
    internal void Commit()
    {
        foreach (var form in Ui.Descendants<InputForm>(this).ToArray()) form.Commit();
        foreach (var grid in grids) grid.Commit(); foreach (string key in actions.Keys) SyncActions(key);
        if (shearGrid is not null) ShearOptions["azioni"] = new JsonArray(shearGrid.Rows.Select(r => (JsonNode)J.Obj(("id",r.Values.S("id")),("nome",r.Values.S("nome")),("N",r.Values.S("N")),("Vx",r.Values.S("Vx")),("Vy",r.Values.S("Vy")))).ToArray());
    }
    internal void SetGeometry(string value) => geometry.Set("diameter_mm", value);
    private void Invalidate(bool geometryChanged = true)
    {
        if (initializing || disposed || synchronizing) return;
        forceOnlyUpdate = false; pendingForcePanels.Clear();
        revision++; cancellation?.Cancel(); Result = null; meshes.Clear(); checker3D.Clear(); checker2D.Clear(); domainResults.Clear(); stressResults.Clear(); InvalidateShear();
        synchronizing = true;
        try { foreach (var row in actions.Values.SelectMany(r => r)) foreach (string key in new[] { "eta3d", "eta2d", "esito3d", "esito2d", "sigma_c", "sigma_s", "eta_sigma", "stress_status", "wk" }) row.Output(key, key.StartsWith("esito") || key == "stress_status" ? "Da calcolare" : key == "wk" ? "Da calcolare" : "—"); }
        finally { synchronizing = false; }
        foreach (var panel in domainPanels) { panel.View3D?.SetMesh(null); panel.Plot.Series = []; panel.Plot.Segments = []; panel.Plot.Markers = []; panel.Plot.VerificationSegments = []; panel.Plot.InvalidateVisual(); panel.Detail.Text = "Dati modificati · aggiornamento automatico in attesa"; }
        foreach (var panel in stressPanels.Values) { panel.View.Stress = null; panel.View.InvalidateVisual(); panel.Detail.Text = "Nessun risultato aggiornato"; panel.Bars.Rows.Clear(); panel.Concrete.Rows.Clear(); }
        if (geometryChanged) RefreshPreview(); RefreshSummary(); status.Text = "Modifiche acquisite · aggiornamento automatico in attesa…"; Modified?.Invoke(); QueueCalculation();
    }
    private void QueueCalculation()
    {
        if (initializing || disposed || calculationQueued) return;
        calculationQueued = true;
        // One dispatcher turn lets the cell/focus commit finish. There is no time-based debounce.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(async () =>
        {
            if (!calculationQueued || disposed) return;
            calculationQueued = false;
            if (!Busy) await CalculateAllAsync();
        }));
    }
    private void InvalidateChecks(bool onlyDomainForces = false)
    {
        if (initializing || disposed || synchronizing) return;
        forceOnlyUpdate = onlyDomainForces && (forceOnlyUpdate || !Busy && !calculationQueued);
        if (!forceOnlyUpdate) pendingForcePanels.Clear();
        revision++; cancellation?.Cancel(); Result = null; QueueCalculation(); Modified?.Invoke();
    }
    private void RefreshPreview()
    {
        string shape = Input.S("shape");
        foreach (string key in new[] { "diameter_mm", "width_mm", "height_mm", "flange_width_mm", "web_width_mm", "flange_thickness_mm" }) geometry.ShowField(key, key switch { "diameter_mm" => shape == "Circolare", "width_mm" => shape == "Rettangolare", "height_mm" => shape != "Circolare", _ => shape == "A T" });
        foreach (string key in reinforcement.Editors.Keys.Where(k => !k.StartsWith("transverse"))) reinforcement.ShowField(key, key.StartsWith("longitudinal") ? shape == "Circolare" : shape != "Circolare");
        SezioneCA? engine = null;
        try
        {
            engine = new SezioneCA(Input); preview.Message = "";
            var concrete = ConcreteMaterials.Concrete(Input); var steel = ConcreteMaterials.Rebar(Input); var standard = ConcreteStandards.Effective(Input, settings);
            double reduction = settings.S("normativa") == "NTC 2018" && Input.S("gettato_sottile") == "Sì" ? .8 : 1;
            materials.Set("__fcd", EngineeringFormat.Number(Math.Abs(concrete.CalculateFcd(standard)) * reduction), true); materials.Set("__fyd", EngineeringFormat.Number(Math.Abs(steel.CalculateFyd(standard))), true);
            materials.Set("__ecm", EngineeringFormat.Number(concrete.E), true); materials.Set("__ec2", EngineeringFormat.Number(Math.Abs(concrete.StrainYCompression) * 1000), true); materials.Set("__ecu", EngineeringFormat.Number(Math.Abs(concrete.StrainUCompression) * 1000), true);
            materials.Set("__nmode", "φ nelle schede SLE", true);
            materials.Set("n", EngineeringFormat.Number(steel.E / concrete.E), true);
        }
        catch (ArgumentException ex) { engine = null; preview.Message = ex.Message; foreach (string key in materials.Editors.Keys.Where(k => k.StartsWith("__"))) materials.Set(key, "—", true); }
        preview.Section = engine; preview.Tendons = [];
        foreach (var row in tendons.Rows)
        {
            var t = row.Values; var x = J.Number(t["x"]); var y = J.Number(t["y"]); var a = J.Number(t["area"]);
            if (x is double xx && y is double yy && a is > 0) preview.Tendons.Add(new(t.S("id"), xx, yy, a.Value));
        }
        SynchronizeStirrups(); foreach (string key in stressPanels.Keys) SynchronizeHomogenization(key);
        foreach (string field in SectionWorkspace.SharedSleFields) settings["sle_comuni"]![field] = settings["sle"]!["SLE"]![field]?.DeepClone();
        RefreshTendonOptions(); RefreshAutomaticShear();
        preview.InvalidateVisual(); barInventory.Rows.Clear();
        if (engine is not null) for (int i = 0; i < engine.Bars.Count; i++) { var b = engine.Bars[i]; barInventory.Rows.Add(new JsonRow(J.Obj(("id", "B" + (i + 1)), ("x", b.X.ToString("0.00")), ("y", b.Y.ToString("0.00")), ("phi", b.Diametro.ToString("0.00"))))); }
        foreach (var panel in stressPanels.Values) { panel.View.Section = engine; panel.View.Tendons = preview.Tendons; panel.View.Message = preview.Message; panel.View.InvalidateVisual(); }
    }
    private UIElement ActionTable(string key, JsonGrid grid, Action? onSelection = null)
    {
        grids.Add(grid); grid.SelectionChanged += (_, _) => onSelection?.Invoke();
        // A view cannot be replaced/refreshed during DataGrid's EditItem transaction.
        grid.RowEditEnding += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;
            if (grid.ItemsSource is ListCollectionView view && !view.IsEditingItem && !view.IsAddingNew) view.Refresh();
        }));
        string ActiveKey() => grid.Tag as string ?? key;
        string NextName(string set)
        { int n = 1; while (actions[set].Any(r => r.Values.S("nome") == "Combo " + n)) n++; return "Combo " + n; }
        void Add()
        { string set = ActiveKey(); grid.Commit(); var row = CreateAction(set, NextName(set), "0", "0", "0"); actions[set].Add(row); SyncActions(set); Invalidate(false); grid.SelectedItem = row; grid.ScrollIntoView(row); }
        void Duplicate()
        { if (grid.SelectedItem is not JsonRow selected) return; string set = ActiveKey(); var r = selected.Values; var row = CreateAction(set, NextName(set), r.S("N"), r.S("Mx"), r.S("My")); actions[set].Add(row); SyncActions(set); Invalidate(false); grid.SelectedItem = row; }
        void Remove()
        { grid.Commit(); if (grid.SelectedItem is not JsonRow row) return; string set = ActiveKey(); int index = actions[set].IndexOf(row); actions[set].Remove(row); SyncActions(set); Invalidate(false); if (actions[set].Count > 0) grid.SelectedItem = actions[set][Math.Min(index, actions[set].Count - 1)]; }
        var buttons = Ui.Bar(Ui.Button("+ Riga", Add), Ui.Button("Duplica", Duplicate), Ui.Button("− Riga", Remove));
        AttachClipboard(grid, ActiveKey, buttons);
        return Ui.Dock(WithFilters(grid), bottom: buttons);
    }
    internal static List<string[]> ParsePaste(string text)
    {
        var result = new List<string[]>();
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var cells = line.Split('\t');
            if (cells.Length == 4 && cells[1].Trim().StartsWith("N", StringComparison.OrdinalIgnoreCase)) continue;
            if (cells.Length == 3) cells = new[] { "", cells[0], cells[1], cells[2] };
            if (cells.Length != 4) throw new ArgumentException("Usare 3 colonne N–Mx–My oppure 4 colonne Nome–N–Mx–My, separate da tabulazioni.");
            for (int i = 1; i < 4; i++) SectionWorkspace.Number(cells[i], "Riga " + (result.Count + 1));
            result.Add(cells);
        }
        if (result.Count == 0) throw new ArgumentException("Nessuna combinazione da incollare.");
        return result;
    }
    public void Dispose() { disposed = true; calculationQueued = false; cancellation?.Cancel(); }
}
