using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.RetainingWall;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal JsonGrid LayerGrid = null!, ActionGrid = null!, MatrixGrid = null!;
    internal readonly ComboBox ViewMode = Ui.Choice(RetainingWallFields.ViewModes, RetainingWallFields.ViewModes[0]);
    internal readonly ComboBox CheckFilter = Ui.Choice(RetainingWallFields.CheckFilters, RetainingWallFields.CheckFilters[0]);
    internal readonly ComboBox Member = Ui.Choice(RetainingWallFields.Members, RetainingWallFields.Members[0]);
    internal readonly Dictionary<string, Expander> Cards = [];
    internal readonly Button SeismicButton, GlobalSetupButton;
    private readonly ContentControl inputHost = new();
    private readonly TextBlock matrixStatus = Ui.Text("", 11), layerStatus = Ui.Text("", 11), probe = Ui.Text("Passare sul disegno per leggere i dati; clic per selezionare un carico.", 11, color: Ui.Muted);
    private JsonObject? selectedAction;
    private readonly Grid inputLayout = new();
    internal RetainingWallWorkspace(JsonObject data, Anthea.Application.Modules.IModuleService? calculationService = null)
    {
        this.calculationService = calculationService ?? Anthea.Application.Modules.ModuleServices.Get(WallModel.Module);
        WallModel.Upgrade(data); Data = data; Background = Appearance.Surface;
        SetValue(NumericPresentation.EnabledProperty, true);
        foreach (var f in WallModel.Families) Family.Items.Add(new ComboBoxItem { Content = f.Name + (f.Available ? "" : " · previsto"), Tag = f.Id, IsEnabled = f.Available });
        Family.SelectedItem = Family.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == Data.S("family")); Family.MinWidth = 180;
        Family.SelectionChanged += (_, _) => { if (building || Family.SelectedItem is not ComboBoxItem item) return; Data["family"] = (string)item.Tag; UpdateFields(); Changed(); };
        SeismicButton = Ui.Button("Sisma: non attivo", ShowSeismic, inspection: true);
        GlobalSetupButton = Ui.Button("Stabilità globale · imposta", ShowGlobalSetup, inspection: true);
        BuildInputs();
        Combination.MinWidth = 210; Combination.MaxWidth = 380; Combination.SelectionChanged += (_, _) => ShowCombination();
        foreach (var c in new[] { Combination, ViewMode, CheckFilter, Member }) RevisionInspection.Allow(c);
        ViewMode.SelectionChanged += (_, _) => RefreshViews(); Member.SelectionChanged += (_, _) => RefreshViews(); CheckFilter.SelectionChanged += (_, _) => ShowCheckTable();
        Drawing.Height = 440; Drawing.MinHeight = 330; Diagrams.Height = 390; Diagrams.CombinedLoads = true;
        Drawing.Inspected += text => probe.Text = text; Diagrams.Inspected += text => probe.Text = text;
        Drawing.ActionSelected += id => { var row = ActionGrid.Rows.FirstOrDefault(r => r.Values.S("id") == id); if (row is not null) { ActionGrid.SelectedItem = row; Cards["Azioni"].IsExpanded = true; Cards["Azioni"].BringIntoView(); } };
        var loads = Toggle("Carichi", true, v => { Drawing.ShowLoads = v; Drawing.InvalidateVisual(); });
        var labels = Toggle("Dati", true, v => { Drawing.ShowLabels = v; Drawing.InvalidateVisual(); });
        var steel = Toggle("Armature", true, v => { Drawing.ShowRebar = v; Drawing.InvalidateVisual(); });
        var push = Toggle("Spinte", true, v => { Drawing.ShowPressures = v; Drawing.InvalidateVisual(); });
        var inputView = Ui.Paper(Ui.Stack(Ui.Bar(Ui.Text("Sezione e azioni", 15, true), loads, labels, steel, push), Ui.Text("Carichi caratteristici inseriti · spinte e contatto della combinazione selezionata", 10, color: Ui.Muted), Drawing, probe, summary,
            Group("Calcolo delle spinte · valori e coefficienti", Ui.Stack(Ui.Text(RetainingWallPresentation.PressureHint, 11, color: Ui.Muted), pressures), false)), 10);
        inputLayout.ColumnDefinitions.Add(new() { Width = new GridLength(1.05, GridUnitType.Star) }); inputLayout.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        inputLayout.RowDefinitions.Add(new() { Height = GridLength.Auto }); inputLayout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        inputLayout.Children.Add(inputHost); inputLayout.Children.Add(inputView); Grid.SetColumn(inputView, 1); inputView.Margin = new Thickness(10, 0, 0, 0);
        var matrix = Group("Combinazioni · matrice modificabile", Ui.Stack(Ui.Bar(Ui.Button("Genera / ripristina automatiche", GenerateMatrix), Ui.Button("Copia riga", CopyCombination), Ui.Button("Elimina riga", RemoveCombination), Ui.Button("Conferma matrice", ConfirmMatrix)), matrixStatus, matrixHost, Ui.Text(WallModel.ApproachHelp, 11, color: Ui.Muted)), true);
        matrix.Margin = new Thickness(0, 10, 0, 0); Cards["Combinazioni"] = matrix;
        var inputPage = Ui.Stack(inputLayout, Ui.Paper(matrix, 10));
        GlobalCombination.SelectionChanged += (_, _) => { RefreshGlobal(); if ((string?)CheckFilter.SelectedItem == "Stabilità globale") ShowCheckTable(); };
        RevisionInspection.Allow(GlobalCombination);
        globalFullProfile = Toggle("Intero profilo", false, v => { GlobalDrawing.FocusCritical = !v; GlobalDrawing.InvalidateVisual(); });
        var resultGraphic = Ui.Paper(Ui.Stack(Ui.Bar(Ui.Text("Vista dei risultati", 15, true), ViewMode, Member, GlobalCombination, globalFullProfile, Ui.Button("Distinta ferri…", ShowBarSchedule, inspection: true)), Diagrams, GlobalDrawing));
        GlobalCombination.Visibility = GlobalDrawing.Visibility = globalFullProfile.Visibility = Visibility.Collapsed;
        SendSectionButton = Ui.Button("Apri sezione in c.a.", SendSelectedSection); SendSectionButton.IsEnabled = false;
        SendSectionButton.ToolTip = "Copia geometria, materiali, armatura effettiva e combinazioni N/M/V della sezione selezionata nel modulo c.a. Il muro conserva i suoi dati.";
        var checksPane = Ui.Paper(Ui.Stack(Ui.Bar(Ui.Text("Riepilogo e dettagli", 15, true), CheckFilter, SendSectionButton), sectionSelection, checks)); checksPane.Margin = new Thickness(0, 10, 0, 0);
        var resultPage = Ui.Stack(resultGraphic, checksPane, Group(RetainingWallPresentation.ApproachTitle, Ui.Stack(Ui.Text(WallModel.Scope, 12), Ui.Text(WallModel.Limits, 12), Ui.Text(WallModel.ApproachHelp, 12), Ui.Text(WallModel.SeismicHelp, 12), Ui.Text(RetainingWallPresentation.PlannedFamilies, 12)), false));
        Ui.Tab(Pages, "Input", Scroll(inputPage)); Ui.Tab(Pages, "Verifiche", Scroll(resultPage)); Pages.Margin = new Thickness(12, 4, 12, 6);
        var header = Ui.Bar(Ui.Text(RetainingWallFields.Title, 19, true), Family, Ui.Button("Mensola esempio", () => Example("cantilever")), Ui.Button("Gravità esempio", () => Example("gravity")), Ui.Button("Ricalcola", async () => await CalculateAsync(), inspection: true), Ui.Button("Relazione Word", () => ReportRequested?.Invoke(), inspection: true), Ui.Button("CSV", ExportCsv, inspection: true)); header.Margin = new Thickness(14, 8, 14, 4);
        var comboBar = Ui.Bar(Ui.Text("Combinazione visualizzata", 12), Combination, SeismicButton, GlobalSetupButton, Ui.Button("Valori di calcolo…", () => { _ = ShowCalculationParameters(); }, inspection: true)); comboBar.Margin = new Thickness(14, 0, 14, 4);
        status.Margin = new Thickness(15, 4, 15, 8);
        Content = Ui.Dock(Pages, Ui.Stack(header, comboBar), status);
        SizeChanged += (_, _) => { bool narrow = ActualWidth < 1450; inputLayout.ColumnDefinitions[0].Width = new GridLength(1.05, GridUnitType.Star); inputLayout.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star); Grid.SetColumn(inputView, narrow ? 0 : 1); Grid.SetRow(inputView, narrow ? 1 : 0); inputView.Margin = narrow ? new Thickness(0, 10, 0, 0) : new Thickness(10, 0, 0, 0); };
        if (Data.Array("combinations").Count > 0) BuildMatrix();
        building = false; UpdateFields(); Preview(); QueueCalculation();
    }
    private static CheckBox Toggle(string title, bool value, Action<bool> changed)
    {
        var c = new CheckBox { Content = title, IsChecked = value, Margin = new Thickness(8, 4, 2, 4) }; RevisionInspection.Allow(c); c.Checked += (_, _) => changed(true); c.Unchecked += (_, _) => changed(false); return c;
    }
    private static ScrollViewer Scroll(UIElement content) => new ChainedScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private static Expander Group(string title, UIElement content, bool open = true) => new() { Header = title, IsExpanded = open, Content = content, Margin = new Thickness(0, 3, 0, 6), FontSize = 13 };
    private InputForm Form(string key, IEnumerable<Field> fields, JsonObject? obj = null, Action<string>? handler = null)
    {
        var form = new InputForm(obj ?? Data[key]!.AsObject(), fields, k => { handler?.Invoke(k); UpdateFields(); Changed(); }, compact: true, symbolColumns: true); Forms[key] = form; return form;
    }
    private JsonGrid GridFor(IEnumerable<Field> fields, JsonArray array, Action<string>? edit = null, double height = 170)
    {
        var grid = new JsonGrid(fields) { Height = height, RowHeight = 31, ColumnHeaderHeight = 40, AlternatingRowBackground = Ui.Brush("#F3F6FA"), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        CompactHeaders(grid);
        grids.Add(grid);
        foreach (var obj in array.OfType<JsonObject>()) grid.Rows.Add(new JsonRow(obj, key => { edit?.Invoke(key); if (key != "visible") Changed(); else { Drawing.InvalidateVisual(); Diagrams.InvalidateVisual(); Modified?.Invoke(); } }));
        return grid;
    }
    private static void CompactHeaders(DataGrid grid)
    {
        var style = new Style(typeof(DataGridColumnHeader), (Style)Application.Current.FindResource(typeof(DataGridColumnHeader)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5, 3, 5, 3)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 11d));
        grid.ColumnHeaderStyle = style;
    }
    private void BuildInputs()
    {
        foreach (FrameworkElement shared in new FrameworkElement[] { actionDetails, layerStatus }) if (shared.Parent is Panel parent) parent.Children.Remove(shared);
        inputHost.Content = null;
        Cards.TryGetValue("Combinazioni", out var matrixCard);
        Forms.Clear(); grids.Clear(); Cards.Clear(); if (matrixCard is not null) Cards["Combinazioni"] = matrixCard; var cards = Ui.Stack();
        void Card(string title, UIElement body) { var group = Group(title, body); Cards[title] = group; var paper = Ui.Paper(group, 10); paper.Margin = new Thickness(0, 0, 0, 8); cards.Children.Add(paper); }
        Card("Terreno", Ui.Stack(BuildSoilColumns(), BuildAdvancedSoil()));
        Card("Materiali", BuildWallMaterials());
        var reinforcement = Ui.Stack(Form("zones", RetainingWallFields.Zones, Data["reinforcement"]!.AsObject()));
        foreach (var (key, label) in RetainingWallFields.RebarZones)
        {
            var arm = Form("rebar_" + key, RetainingWallFields.Rebar, Data["reinforcement"]![key]!.AsObject());
            var group = Group(label, arm, false); Cards["rebar_" + key] = group; reinforcement.Children.Add(group);
        }
        Card("Geometria", Ui.Stack(Form("geometry", RetainingWallFields.Geometry), Group("Armature inserite", reinforcement), BuildDetailing(), Ui.Text("Facce simmetriche oppure indipendenti. h₁ separa le due zone del fusto; la vista Armature mostra ancoraggi e sovrapposizioni. La distinta è un predimensionamento, da completare con il disegno esecutivo.", 11, color: Ui.Muted)));
        ActionGrid = GridFor(RetainingWallFields.ActionColumns, Data.Array("actions"), height: 145);
        ActionGrid.Columns[2].Width = 140; ActionGrid.Columns[3].Width = 160; ActionGrid.Columns[4].Width = 65; ActionGrid.Columns[5].Width = 70;
        ActionGrid.SelectionChanged += (_, _) => EditAction();
        var type = Ui.Choice(WallModel.ActionTypes, WallModel.ActionTypes[0]); type.Width = 190;
        Cards["Sisma"] = BuildSeismicCard();
        Card("Azioni", Ui.Stack(Cards["Sisma"], BuildHistories(), Ui.Text("Valori per metro di muro. Calcola include/esclude l’azione; Disegna modifica soltanto la vista. G1: permanente strutturale, G2: permanente non strutturale, Q: variabile, A: eccezionale.", 11, color: Ui.Muted), ActionGrid,
            Ui.Bar(type, Ui.Button("+ Azione", () => AddAction((string)type.SelectedItem)), Ui.Button("− Azione", RemoveAction)), actionDetails));
        inputHost.Content = new ChainedScrollViewer { Content = cards, MaxHeight = 605, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        UpdateLayerDepths(); ActionGrid.SelectedIndex = ActionGrid.Rows.Count > 0 ? 0 : -1;
    }
    private void AddRow(JsonGrid grid, JsonArray array, JsonObject obj, Action<string>? handler = null)
    {
        array.Add(obj); grid.Rows.Add(new JsonRow(obj, key => { handler?.Invoke(key); if (key == "visible") { Drawing.InvalidateVisual(); Modified?.Invoke(); } else Changed(); })); grid.SelectedIndex = grid.Rows.Count - 1;
    }
    private void UpdateLayerDepths()
    {
        UpdateColumnDepths();
    }
    private void RemoveLayer() => RemoveSoilLayer(false);
    private void MoveLayer(int step) => MoveSoilLayer(false, step);
    internal void AddAction(string type) { if (ActionGrid.Rows.Count >= 30) return; var a = WallModel.NewAction(Data, type); AddRow(ActionGrid, Data.Array("actions"), a); Changed(); }
    private void RemoveAction() { if (ActionGrid.SelectedItem is not JsonRow row) return; Data.Array("actions").Remove(row.Values); ActionGrid.Rows.Remove(row); ActionGrid.SelectedIndex = ActionGrid.Rows.Count - 1; Changed(); }
    private void EditAction()
    {
        Forms.Remove("action"); selectedAction = (ActionGrid.SelectedItem as JsonRow)?.Values; actionDetails.Content = null;
        Drawing.SelectedAction = selectedAction?.S("id"); Drawing.InvalidateVisual(); if (selectedAction is null) return;
        var a = selectedAction;
        var form = Form("action", RetainingWallFields.Action(a.S("type")), a, key => { foreach (var row in ActionGrid.Rows) row.Refresh(); if (key == "type") Dispatcher.BeginInvoke(EditAction); });
        foreach (var (key, visible) in RetainingWallPresentation.ActionTypeFields(a.S("type"))) form.ShowField(key, visible);
        foreach (string key in new[] { "psi0", "psi1", "psi2" }) form.ShowField(key, a.S("category") == "Q");
        actionDetails.Content = Ui.Stack(form, Ui.Text(RetainingWallPresentation.ActionHelp, 11, color: Ui.Muted));
    }
    private void UpdateFields()
    {
        if (!Forms.ContainsKey("materials")) return;
        // The rules of the fields come from RetainingWallPresentation.InputRules (F5.9), shared with the ViewModel; the previews, the
        // seismic coefficients of the site and the global stability are refreshed in the same order as before.
        ApplyRules(RetainingWallPresentation.InputRules(Data, Forms.ContainsKey("action") ? selectedAction?.S("category") : null));
        UpdateWallMaterials();
        if (DesignButton is not null) DesignButton.IsEnabled = Data.S("family") == "cantilever" && !Busy;
        UpdateSeismicFields();
        RefreshGlobal();
        UpdateSeismicStatus();
        if (LayerGrid is not null) UpdateLayerDepths();
    }
    private void ApplyRules(WallInputRules rules)
    {
        foreach (var (key, visible) in rules.Forms) if (Forms.TryGetValue(key, out var form)) form.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (key, visible) in rules.Cards) if (Cards.TryGetValue(key, out var card)) card.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (key, field, visible) in rules.Fields) if (Forms.TryGetValue(key, out var form)) form.ShowField(field, visible);
        foreach (var (key, field, enabled, dim) in rules.Enabled) if (Forms.TryGetValue(key, out var form)) form.Enable(field, enabled, dim);
    }
    private void ShowSeismic()
    {
        Pages.SelectedIndex = 0; Cards["Azioni"].IsExpanded = true; Cards["Sisma"].IsExpanded = true;
        UpdateLayout(); Cards["Sisma"].BringIntoView();
    }
    private void UpdateSeismicStatus()
    {
        var (content, warning, tip) = RetainingWallPresentation.SeismicStatus(Data, Calculation);
        SeismicButton.Content = content;
        SeismicButton.Foreground = Appearance.Foreground(warning ? Brushes.Firebrick : Ui.Navy);
        SeismicButton.ToolTip = tip;
    }
}
