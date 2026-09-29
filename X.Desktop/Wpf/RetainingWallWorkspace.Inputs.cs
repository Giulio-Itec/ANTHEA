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
    internal readonly ComboBox ViewMode = Ui.Choice(["Geometria e carichi", "Sollecitazioni", "Forze resistenti", "Tassi di lavoro", "Armature", "Stabilità globale"], "Geometria e carichi");
    internal readonly ComboBox CheckFilter = Ui.Choice(["Riepilogo completo", "Combinazione selezionata", "Tutti i controlli", "Sollecitazioni numeriche", "Calcolo delle spinte", "Stabilità globale"], "Riepilogo completo");
    internal readonly ComboBox Member = Ui.Choice(["Fusto", "Valle", "Monte"], "Fusto");
    internal readonly Dictionary<string, Expander> Cards = [];
    internal readonly Button SeismicButton;
    private readonly ContentControl inputHost = new();
    private readonly TextBlock matrixStatus = Ui.Text("", 11), layerStatus = Ui.Text("", 11), probe = Ui.Text("Passare sul disegno per leggere i dati; clic per selezionare un carico.", 11, color: Ui.Muted);
    private JsonObject? selectedAction;
    private readonly Grid inputLayout = new();
    internal RetainingWallWorkspace(JsonObject data)
    {
        RetainingWall.Upgrade(data); Data = data; Background = Ui.Bg;
        SetValue(NumericPresentation.EnabledProperty, true);
        foreach (var f in RetainingWall.Families) Family.Items.Add(new ComboBoxItem { Content = f.Name + (f.Available ? "" : " · previsto"), Tag = f.Id, IsEnabled = f.Available });
        Family.SelectedItem = Family.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == Data.S("family")); Family.MinWidth = 180;
        Family.SelectionChanged += (_, _) => { if (building || Family.SelectedItem is not ComboBoxItem item) return; Data["family"] = (string)item.Tag; UpdateFields(); Changed(); };
        SeismicButton = Ui.Button("Sisma: non attivo", ShowSeismic, inspection: true);
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
            Group("Calcolo delle spinte · valori e coefficienti", Ui.Stack(Ui.Text("Scegliere la combinazione nella barra superiore. z è la profondità dalla sommità; Ht = H+t. Selezionare una riga per evidenziare il tratto.", 11, color: Ui.Muted), pressures), false)), 10);
        inputLayout.ColumnDefinitions.Add(new() { Width = new GridLength(1.05, GridUnitType.Star) }); inputLayout.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        inputLayout.RowDefinitions.Add(new() { Height = GridLength.Auto }); inputLayout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        inputLayout.Children.Add(inputHost); inputLayout.Children.Add(inputView); Grid.SetColumn(inputView, 1); inputView.Margin = new Thickness(10, 0, 0, 0);
        var matrix = Group("Combinazioni · matrice modificabile", Ui.Stack(Ui.Bar(Ui.Button("Genera / ripristina automatiche", GenerateMatrix), Ui.Button("Copia riga", CopyCombination), Ui.Button("Elimina riga", RemoveCombination), Ui.Button("Conferma matrice", ConfirmMatrix)), matrixStatus, matrixHost, Ui.Text(RetainingWall.ApproachHelp, 11, color: Ui.Muted)), true);
        matrix.Margin = new Thickness(0, 10, 0, 0); Cards["Combinazioni"] = matrix;
        var inputPage = Ui.Stack(inputLayout, Ui.Paper(matrix, 10));
        GlobalCombination.SelectionChanged += (_, _) => { RefreshGlobal(); if ((string?)CheckFilter.SelectedItem == "Stabilità globale") ShowCheckTable(); };
        RevisionInspection.Allow(GlobalCombination);
        globalFullProfile = Toggle("Intero profilo", false, v => { GlobalDrawing.FocusCritical = !v; GlobalDrawing.InvalidateVisual(); });
        var resultGraphic = Ui.Paper(Ui.Stack(Ui.Bar(Ui.Text("Vista dei risultati", 15, true), ViewMode, Member, GlobalCombination, globalFullProfile), Diagrams, GlobalDrawing));
        GlobalCombination.Visibility = GlobalDrawing.Visibility = globalFullProfile.Visibility = Visibility.Collapsed;
        SendSectionButton = Ui.Button("Apri sezione in c.a.", SendSelectedSection); SendSectionButton.IsEnabled = false;
        SendSectionButton.ToolTip = "Copia geometria, materiali, armatura effettiva e combinazioni N/M/V della sezione selezionata nel modulo c.a. Il muro conserva i suoi dati.";
        var checksPane = Ui.Paper(Ui.Stack(Ui.Bar(Ui.Text("Riepilogo e dettagli", 15, true), CheckFilter, SendSectionButton), sectionSelection, checks)); checksPane.Margin = new Thickness(0, 10, 0, 0);
        var resultPage = Ui.Stack(resultGraphic, checksPane, Group("Ipotesi, approcci e tipologie previste", Ui.Stack(Ui.Text(RetainingWall.Scope, 12), Ui.Text(RetainingWall.Limits, 12), Ui.Text(RetainingWall.ApproachHelp, 12), Ui.Text(RetainingWall.SeismicHelp, 12), Ui.Text("Tipologie predisposte: " + string.Join(", ", RetainingWall.Families.Where(f => !f.Available).Select(f => f.Name)) + ". Calcolo attivo soltanto per mensola e gravità.", 12)), false));
        Ui.Tab(Pages, "Input", Scroll(inputPage)); Ui.Tab(Pages, "Verifiche", Scroll(resultPage)); Pages.Margin = new Thickness(12, 4, 12, 6);
        var header = Ui.Bar(Ui.Text("Muri di sostegno", 19, true), Family, Ui.Button("Mensola esempio", () => Example("cantilever")), Ui.Button("Gravità esempio", () => Example("gravity")), Ui.Button("Ricalcola", async () => await CalculateAsync(), inspection: true), Ui.Button("Relazione Word", () => ReportRequested?.Invoke(), inspection: true), Ui.Button("CSV", ExportCsv, inspection: true)); header.Margin = new Thickness(14, 8, 14, 4);
        var comboBar = Ui.Bar(Ui.Text("Combinazione visualizzata", 12), Combination, SeismicButton); comboBar.Margin = new Thickness(14, 0, 14, 4);
        status.Margin = new Thickness(15, 4, 15, 8);
        Content = Ui.Dock(Pages, Ui.Stack(header, comboBar), status);
        SizeChanged += (_, _) => { bool narrow = ActualWidth < 1120; inputLayout.ColumnDefinitions[0].Width = new GridLength(1.05, GridUnitType.Star); inputLayout.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star); Grid.SetColumn(inputView, narrow ? 0 : 1); Grid.SetRow(inputView, narrow ? 1 : 0); inputView.Margin = narrow ? new Thickness(0, 10, 0, 0) : new Thickness(10, 0, 0, 0); };
        if (Data.Array("combinations").Count > 0) BuildMatrix();
        timer.Tick += Tick; building = false; UpdateFields(); Preview(); timer.Start();
    }
    private static CheckBox Toggle(string title, bool value, Action<bool> changed)
    {
        var c = new CheckBox { Content = title, IsChecked = value, Margin = new Thickness(8, 4, 2, 4) }; RevisionInspection.Allow(c); c.Checked += (_, _) => changed(true); c.Unchecked += (_, _) => changed(false); return c;
    }
    private static ScrollViewer Scroll(UIElement content) => new ChainedScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private static Expander Group(string title, UIElement content, bool open = true) => new() { Header = title, IsExpanded = open, Content = content, Margin = new Thickness(0, 3, 0, 6), FontSize = 13 };
    private static IEnumerable<Field> Fields(IEnumerable<RetainingWall.Parameter> parameters) => parameters.Select(p => new Field(p.Key, p.Label, p.Unit, Symbol: p.Symbol));
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
        LayerGrid = GridFor([new("__index", "Strato", ReadOnly: true), new("name", "Descrizione"), new("thickness", "Δz\n[m]"), new("__from", "Da z\n[m]", ReadOnly: true), new("__to", "A z\n[m]", ReadOnly: true), new("gamma", "γ\n[kN/m³]"), new("gamma_sat", "γsat\n[kN/m³]"), new("phi", "φ′\n[°]")], Data.Array("layers"), _ => UpdateLayerDepths(), 180);
        double[] layerWidths = [56, 145, 70, 65, 65, 78, 78, 65]; for (int i = 0; i < layerWidths.Length; i++) LayerGrid.Columns[i].Width = layerWidths[i];
        LayerGrid.LoadingRow += (_, e) => { e.Row.BorderBrush = Ui.Brush(RetainingWallDrawing.LayerColors[e.Row.GetIndex() % RetainingWallDrawing.LayerColors.Length]); e.Row.BorderThickness = new Thickness(6, 0, 0, 0); };
        LayerGrid.SelectionChanged += (_, _) => { Drawing.SelectedLayer = LayerGrid.SelectedIndex; Drawing.InvalidateVisual(); };
        var water = Form("water", [new("enabled", "Presenza falda", Bool: true), new("depth", "Profondità da sommità", "m", Symbol: "zf"), new("front_head", "Battente a valle dal piano di posa", "m", Symbol: "hw,v")]);
        SoilButton = Ui.Button("Invia / carica terreno…", () => SoilTransferRequested?.Invoke());
        Card("Terreno", Ui.Stack(Ui.Bar(SoilButton), Ui.Text("Strati dall’alto verso il basso · profondità z dalla sommità · c′ = 0", 11, color: Ui.Muted), LayerGrid,
            Ui.Bar(Ui.Button("+ Strato", () => { if (Data.Array("layers").Count >= 50) return; var l = RetainingWall.Layer(); l["thickness"] = 1; AddRow(LayerGrid, Data.Array("layers"), l, _ => UpdateLayerDepths()); UpdateLayerDepths(); Changed(); }), Ui.Button("− Strato", () => RemoveLayer()), Ui.Button("↑", () => MoveLayer(-1)), Ui.Button("↓", () => MoveLayer(1))), layerStatus,
            Group("Terreno di fondazione", Form("foundation", Fields(RetainingWall.FoundationFields)), false), Group("Falda e sottospinta", water, false), GlobalCard()));
        var materials = Form("materials", Fields(RetainingWall.MaterialFields).Concat([new Field("exposure", "Classe di esposizione", Choices: Ntc2018Checks.Exposures.Skip(1).ToArray())]));
        Card("Materiali", materials);
        var reinforcement = Ui.Stack(Form("zones", [new("two_zones", "Due zone verticali di armatura", Bool: true), new("lower_height", "Altezza zona inferiore dal piede del fusto", "m", Symbol: "h₁")], Data["reinforcement"]!.AsObject()));
        foreach (var (key, label) in new[] { ("stem", "Fusto · zona inferiore / intera altezza"), ("stem_upper", "Fusto · zona superiore"), ("toe", "Mensola a valle"), ("heel", "Mensola a monte") })
        {
            var arm = Form("rebar_" + key, [new("diameter", "Diametro per faccia", "mm", Symbol: "Ø"), new("count", "Barre per metro e per faccia", "−", Symbol: "n")], Data["reinforcement"]![key]!.AsObject());
            var group = Group(label, arm, false); Cards["rebar_" + key] = group; reinforcement.Children.Add(group);
        }
        Card("Geometria", Ui.Stack(Form("geometry", Fields(RetainingWall.GeometryFields)), Group("Armature inserite", reinforcement), Ui.Text("Le due facce hanno la stessa armatura. La quota h₁ separa la zona inferiore dalla superiore; entrambe sono verificate anche in prossimità del cambio. Quantità prive di sovrapposizioni e ancoraggi.", 11, color: Ui.Muted)));
        ActionGrid = GridFor([new("enabled", "Calcola", Bool: true), new("visible", "Disegna", Bool: true), new("name", "Azione"), new("type", "Tipo", ReadOnly: true), new("category", "Natura", ReadOnly: true), new("value", "Valore", ReadOnly: true)], Data.Array("actions"), height: 145);
        ActionGrid.Columns[2].Width = 140; ActionGrid.Columns[3].Width = 160; ActionGrid.Columns[4].Width = 65; ActionGrid.Columns[5].Width = 70;
        ActionGrid.SelectionChanged += (_, _) => EditAction();
        var type = Ui.Choice(RetainingWall.ActionTypes, RetainingWall.ActionTypes[0]); type.Width = 190;
        Cards["Sisma"] = BuildSeismicCard();
        Card("Azioni", Ui.Stack(Cards["Sisma"], Ui.Text("Valori per metro di muro. Calcola include/esclude l’azione; Disegna modifica soltanto la vista. G1: permanente strutturale, G2: permanente non strutturale, Q: variabile, A: eccezionale.", 11, color: Ui.Muted), ActionGrid,
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
        double z = 0; int i = 0;
        foreach (var row in LayerGrid.Rows) { row.Output("__index", ++i); row.Output("__from", F(z)); z += row.Values.D("thickness"); row.Output("__to", F(z)); }
        LayerGrid.Height = Math.Min(200, 63 + 31 * LayerGrid.Rows.Count);
        double ht = Data["geometry"].D("height") + Data["geometry"].D("slab"); layerStatus.Text = $"Copertura {F(z)} m · richiesta fino al piano di posa {F(ht)} m"; layerStatus.Foreground = z + 1e-9 < ht ? Brushes.Firebrick : Ui.Muted;
    }
    private void RemoveLayer() { if (LayerGrid.Rows.Count <= 1 || LayerGrid.SelectedItem is not JsonRow row) return; Data.Array("layers").Remove(row.Values); LayerGrid.Rows.Remove(row); UpdateLayerDepths(); Changed(); }
    private void MoveLayer(int step) { int i = LayerGrid.SelectedIndex, j = i + step; if (i < 0 || j < 0 || j >= LayerGrid.Rows.Count) return; var a = Data.Array("layers"); var node = a[i]; a.RemoveAt(i); a.Insert(j, node); LayerGrid.Rows.Move(i, j); LayerGrid.SelectedIndex = j; UpdateLayerDepths(); Changed(); }
    internal void AddAction(string type) { if (ActionGrid.Rows.Count >= 30) return; var a = RetainingWall.NewAction(Data, type); AddRow(ActionGrid, Data.Array("actions"), a); Changed(); }
    private void RemoveAction() { if (ActionGrid.SelectedItem is not JsonRow row) return; Data.Array("actions").Remove(row.Values); ActionGrid.Rows.Remove(row); ActionGrid.SelectedIndex = ActionGrid.Rows.Count - 1; Changed(); }
    private void EditAction()
    {
        Forms.Remove("action"); selectedAction = (ActionGrid.SelectedItem as JsonRow)?.Values; actionDetails.Content = null;
        Drawing.SelectedAction = selectedAction?.S("id"); Drawing.InvalidateVisual(); if (selectedAction is null) return;
        var a = selectedAction;
        var form = Form("action", [new("name", "Nome"), new("type", "Tipo", Choices: RetainingWall.ActionTypes), new("category", "Natura", Choices: RetainingWall.ActionCategories), new("value", "Intensità caratteristica", RetainingWall.ActionUnit(a.S("type"))),
            new("z", "Quota di applicazione / estremo superiore", "m", Symbol: "z₁"), new("z0", "Quota inferiore della pressione", "m", Symbol: "z₀"), new("x", "Ascissa dal bordo a valle", "m", Symbol: "x"), new("psi0", "Accompagnamento SLU / rara", "−", Symbol: "ψ₀"), new("psi1", "Frequente", "−", Symbol: "ψ₁"), new("psi2", "Quasi permanente", "−", Symbol: "ψ₂"), new("group", "Gruppo correlato (vuoto = indipendente)")], a, key => { foreach (var row in ActionGrid.Rows) row.Refresh(); if (key == "type") Dispatcher.BeginInvoke(EditAction); });
        form.ShowField("z", a.S("type") != "Sovraccarico uniforme"); form.ShowField("x", a.S("type") == "Forza verticale"); form.ShowField("z0", a.S("type") == "Pressione laterale");
        foreach (string key in new[] { "psi0", "psi1", "psi2" }) form.ShowField(key, a.S("category") == "Q");
        actionDetails.Content = Ui.Stack(form, Ui.Text("Quote z dal piano di posa; x dal bordo a valle. H e pressioni positivi verso valle, N in compressione, M ribaltante. Urto: forza statica equivalente assegnata, distribuita per metro. Il sovraccarico è uniforme su tutto il riempimento.", 11, color: Ui.Muted));
    }
    private void UpdateFields()
    {
        if (!Forms.TryGetValue("materials", out var material)) return;
        bool rc = Data.S("family") == "cantilever";
        foreach (var f in RetainingWall.MaterialFields) material.ShowField(f.Key, f.Key == "gamma" || (rc ? !f.Key.EndsWith("_rd") : f.Key.EndsWith("_rd")));
        material.ShowField("exposure", rc);
        foreach (var (key, card) in Cards.Where(f => f.Key.StartsWith("rebar_"))) card.Visibility = rc && (key != "rebar_stem_upper" || Data["reinforcement"].B("two_zones")) ? Visibility.Visible : Visibility.Collapsed;
        if (Forms.TryGetValue("zones", out var zones)) { zones.Visibility = rc ? Visibility.Visible : Visibility.Collapsed; zones.ShowField("lower_height", Data["reinforcement"].B("two_zones")); }
        if (Forms.TryGetValue("water", out var water)) foreach (string key in new[] { "depth", "front_head" }) water.Enable(key, Data["water"].B("enabled"), true);
        UpdateSeismicFields();
        RefreshGlobal();
        UpdateSeismicStatus();
        if (Forms.TryGetValue("action", out var action) && selectedAction is not null) foreach (string key in new[] { "psi0", "psi1", "psi2" }) action.ShowField(key, selectedAction.S("category") == "Q");
        if (LayerGrid is not null) UpdateLayerDepths();
    }
    private void ShowSeismic()
    {
        Pages.SelectedIndex = 0; Cards["Azioni"].IsExpanded = true; Cards["Sisma"].IsExpanded = true;
        UpdateLayout(); Cards["Sisma"].BringIntoView();
    }
    private void UpdateSeismicStatus()
    {
        bool enabled = Data["seismic"].B("enabled"); int? cases = Calculation?.Cases.Count(c => c.State == "SISMA");
        SeismicButton.Content = !enabled ? "Sisma: non attivo · imposta" : cases is null ? "Sisma: attivo · da ricalcolare" : cases == 0 ? "Sisma: attivo · nessuna combinazione" : $"Sisma: attivo · {cases} combinazioni";
        SeismicButton.Foreground = enabled && cases == 0 ? Brushes.Firebrick : Ui.Navy;
        SeismicButton.ToolTip = "Apri Input → Azioni → Sisma. " + (enabled ? $"{Data["seismic"].S("method")} · kh={Data["seismic"].D("kh"):0.###} · |kv|={Data["seismic"].D("kv"):0.###}. " : "Abilita l’azione sismica e assegna modello, kh e |kv|. ")
            + "Con matrice personalizzata controllare le righe SISMA; Genera / ripristina automatiche ricrea le combinazioni dai parametri inseriti.";
    }
}
