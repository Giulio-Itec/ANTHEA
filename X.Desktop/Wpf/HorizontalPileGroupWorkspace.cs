using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

internal sealed class HorizontalPileGroupWorkspace : UserControl, IDisposable
{
    readonly JsonObject data;
    readonly ObservableCollection<PileRow> piles = new();
    readonly DataGrid coordinates = new() { AutoGenerateColumns = false, Height = 210, MinColumnWidth = 90 };
    readonly StackPanel generator = new(), spacingFields = new();
    readonly TextBlock title = Ui.Text("", 20, true), state = Ui.Text("", 12, color: Ui.Muted), geometry = Ui.Text("", 12), notes = Ui.Text("", 12), source = Ui.Text("", 11, color: Ui.Muted);
    readonly TextBlock minValue = Ui.Text("—", 22, true), meanValue = Ui.Text("—", 22, true), maxValue = Ui.Text("—", 22, true), reduction = Ui.Text("—", 22, true);
    readonly TextBlock coordinateHelp = Ui.Text("", 11, color: Ui.Muted);
    readonly PileGroupPlan plan = new() { Height = 345 };
    readonly DataGrid comparison = OutputGrid(305), details = OutputGrid(235);
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    bool loading = true, selecting, calculating, disposed;
    string spacingDirection = "X";
    internal JsonObject? Result { get; private set; }
    internal event Action? Modified;
    public sealed class PileRow
    {
        string id = "", x = "0", y = "0";
        internal Action? Changed;
        public string Id { get => id; set { id = value; Changed?.Invoke(); } }
        public string X { get => x; set { x = value; Changed?.Invoke(); } }
        public string Y { get => y; set { y = value; Changed?.Invoke(); } }
    }
    public sealed class ComparisonRow
    {
        public JsonNode Node { get; init; } = null!;
        public int Method => (int)Node.D("Method");
        public string DirectionId => Node.S("DirectionId");
        public string Direction => Node.S("DirectionName") + " · " + F(J.Number(Node["Angle"])) + "°";
        public string MethodName => HorizontalPileGroup.MethodNames[Method];
        public string Minimum => F(J.Number(Node["Minimum"]));
        public string Mean => F(J.Number(Node["Factor"]));
        public string Maximum => F(J.Number(Node["Maximum"]));
        public string Reduction => F(J.Number(Node["ReductionPercent"])) + (Node["Factor"] is null ? "" : "%");
        public string Status => Node.S("Error") != "" ? Node.S("Error") : Node.Array("Warnings").Any(w => w!.ToString().Contains("strapolazione") || w.ToString().Contains("estensione geometrica")) ? "Approssimazione esplicita" : "Disponibile";
    }
    static DataGrid OutputGrid(double height) => new() { IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, Height = height, HeadersVisibility = DataGridHeadersVisibility.Column };
    static string F(double? x) => x?.ToString("0.###", CultureInfo.CurrentCulture) ?? "—";
    static JsonNode Value(string text) => double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && double.IsFinite(x) ? JsonValue.Create(x)! : JsonValue.Create(text)!;
    static void Column(DataGrid grid, string header, string property, double width) => grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(property) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = width });
    TextBox Field(Panel parent, string label, JsonObject owner, string key, bool optional = false)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 4) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(94) });
        row.Children.Add(Ui.Text(label, 12));
        var box = new TextBox { Text = owner[key]?.ToString() ?? "", Margin = new Thickness(8, 0, 0, 0), Tag = key, VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Right };
        box.TextChanged += (_, _) => { if (optional && string.IsNullOrWhiteSpace(box.Text)) owner.Remove(key); else owner[key] = Value(box.Text); Queue(); };
        Grid.SetColumn(box, 1); row.Children.Add(box); parent.Children.Add(row); return box;
    }
    CheckBox Option(Panel parent, string label, JsonObject owner, string key)
    {
        var box = new CheckBox { Content = Ui.Text(label, 12), IsChecked = owner.B(key), Margin = new Thickness(0, 5, 0, 5), Tag = key };
        box.Click += (_, _) => { owner[key] = box.IsChecked == true; Queue(); }; parent.Children.Add(box); return box;
    }
    static void Heading(Panel parent, string text) { var title = Ui.Text(text, 16, true); title.Margin = new Thickness(0, 12, 0, 8); parent.Children.Add(title); }
    internal HorizontalPileGroupWorkspace(JsonObject data)
    {
        this.data = data; HorizontalPileGroup.Upgrade(data); Background = Ui.Bg;
        var input = new StackPanel { Margin = new Thickness(14) };
        Heading(input, "Disposizione dei pali");
        var shape = new ComboBox { ItemsSource = HorizontalPileGroup.LayoutNames, SelectedItem = data["generatore"].S("tipo"), Margin = new Thickness(0, 0, 0, 8), Tag = "geometria" };
        shape.SelectionChanged += (_, _) => { if (shape.SelectedItem is string choice) { data["generatore"]!["tipo"] = choice; BuildGenerator(); Queue(); } }; input.Children.Add(shape);
        Field(input, "Diametro comune D [m]", data, "diametro"); input.Children.Add(generator); BuildGenerator();
        Heading(input, "Coordinate dei pali [m]"); coordinates.ItemsSource = piles;
        foreach (string p in new[] { "Id", "X", "Y" }) Column(coordinates, p == "Id" ? "Palo" : p + " [m]", p, 96);
        piles.CollectionChanged += (_, e) => { if (e.NewItems is not null) foreach (PileRow row in e.NewItems) row.Changed = () => { CapturePiles(); Queue(); }; if (!loading) { CapturePiles(); Queue(); } };
        coordinates.CellEditEnding += (_, e) => { if (e.EditAction == DataGridEditAction.Cancel) Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { CapturePiles(); Queue(); })); };
        LoadPiles(); input.Children.Add(coordinates); input.Children.Add(coordinateHelp);
        Heading(input, "Direzioni di carico"); var directions = data["direzioni"]!.AsObject();
        var directionOptions = new WrapPanel(); Option(directionOptions, "+X · 0°     ", directions, "X"); Option(directionOptions, "+Y · 90°", directions, "Y"); input.Children.Add(directionOptions);
        var negativeDirections = new WrapPanel(); Option(negativeDirections, "−X · 180°     ", directions, "X-"); Option(negativeDirections, "−Y · 270°", directions, "Y-"); input.Children.Add(negativeDirections);
        var custom = Option(input, "Aggiungi direzione personalizzata", directions, "custom");
        var customAngle = Field(input, "Angolo [° antiorari da +X]", directions, "angolo_custom"); customAngle.IsEnabled = directions.B("custom"); custom.Click += (_, _) => customAngle.IsEnabled = custom.IsChecked == true;
        var capPanel = new StackPanel(); var cap = data["basamento"]!.AsObject();
        var capShape = new ComboBox { ItemsSource = new[] { "Contorno palificata", "Rettangolare" }, SelectedItem = cap.S("tipo"), Margin = new Thickness(0, 5, 0, 7) };
        capShape.SelectionChanged += (_, _) => { cap["tipo"] = capShape.SelectedItem?.ToString(); Queue(); }; capPanel.Children.Add(capShape);
        Field(capPanel, "Distanza asse palo–bordo / D", cap, "bordo_d");
        capPanel.Children.Add(Ui.Text("1,0D dall’asse = 0,5D libero dal bordo del palo. Il basamento in pianta non modifica i fattori di gruppo.", 11, color: Ui.Muted));
        input.Children.Add(new Expander { Header = "Basamento e distanze dai bordi", Content = capPanel, IsExpanded = true, Margin = new Thickness(0, 10, 0, 8) });
        var advanced = new StackPanel(); Option(advanced, "Consenti estrapolazioni", data, "estrapolazioni");
        advanced.Children.Add(Ui.Text("Davisson, AASHTO, FHWA: sotto 3D. Rollins: fuori 3,3–5,65D. Caltrans: α sotto 2D. Non riguarda Reese; non supera il limite trasversale di Davisson.", 11, color: Ui.Muted));
        Option(advanced, "Accetto l’estensione per file proiettate", data, "file_proiettate");
        advanced.Children.Add(Ui.Text("Davisson, AASHTO, FHWA, Rollins e α di Caltrans: per geometrie non regolari rispetto al carico. Reese usa sempre le coppie reali.", 11, color: Ui.Muted));
        var spacingChoice = new ComboBox { ItemsSource = new[] { "+X", "+Y", "−X", "−Y", "Personalizzata" }, SelectedIndex = 0, Margin = new Thickness(0, 8, 0, 5) };
        spacingChoice.SelectionChanged += (_, _) => { spacingDirection = new[] { "X", "Y", "X-", "Y-", "custom" }[spacingChoice.SelectedIndex]; BuildSpacing(); };
        advanced.Children.Add(Ui.Text("Interassi rappresentativi della direzione", 12, true)); advanced.Children.Add(spacingChoice); advanced.Children.Add(spacingFields); BuildSpacing();
        advanced.Children.Add(Ui.Text("S∥ = distanza tra file lungo H; S⊥ = passo ortogonale. Assegnazione per Davisson/AASHTO/FHWA/Rollins; solo S∥ interviene in α Caltrans. Nessuno cambia le distanze di Reese. Vuoto = automatico.", 11, color: Ui.Muted));
        input.Children.Add(new Expander { Header = "Opzioni di calcolo e applicabilità", Content = advanced, Margin = new Thickness(0, 8, 0, 10) });
        var output = new StackPanel { Margin = new Thickness(12) };
        var overview = new StackPanel(); overview.Children.Add(title); overview.Children.Add(state);
        var stats = new UniformGrid { Columns = 4, Margin = new Thickness(0, 10, 0, 8) };
        foreach (var (name, value) in new[] { ("MINIMO", minValue), ("MEDIA", meanValue), ("MASSIMO", maxValue), ("RIDUZIONE MEDIA", reduction) }) stats.Children.Add(new Border { Background = Ui.Bg, CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 8, 0), Child = Ui.Stack(Ui.Text(name, 11, true, Ui.Muted), value) });
        overview.Children.Add(stats); overview.Children.Add(plan); overview.Children.Add(geometry); output.Children.Add(Ui.Paper(overview, 14));
        var comparisonPanel = new StackPanel(); Heading(comparisonPanel, "Confronto · seleziona un risultato per visualizzarlo");
        comparisonPanel.Children.Add(Ui.Text("Rg riduce kh/nh; Pm riduce la reazione p. Non sono verifiche della capacità del gruppo.", 11, color: Ui.Muted));
        Column(comparison, "Direzione", "Direction", 110); Column(comparison, "Metodo / grandezza", "MethodName", 260); Column(comparison, "Min", "Minimum", 60); Column(comparison, "Media", "Mean", 65); Column(comparison, "Max", "Maximum", 60); Column(comparison, "Riduzione", "Reduction", 85); Column(comparison, "Applicabilità", "Status", 360);
        comparison.SelectionChanged += (_, _) => { if (selecting || Result is null || comparison.SelectedItem is not ComparisonRow row) return; data["metodo"] = row.Method; data["direzione_selezionata"] = row.DirectionId; data["angolo"] = row.Node.D("Angle"); HorizontalPileGroup.Select(Result, row.Method, row.DirectionId); Present(); Modified?.Invoke(); };
        RevisionInspection.Allow(comparison); comparisonPanel.Children.Add(comparison); comparisonPanel.Children.Add(source); comparisonPanel.Children.Add(notes);
        var paper = Ui.Paper(comparisonPanel, 14); paper.Margin = new Thickness(0, 12, 0, 0); output.Children.Add(paper);
        var detailPanel = new StackPanel(); Heading(detailPanel, "Valori per palo del risultato selezionato");
        foreach (var (header, key, width) in new[] { ("Palo", "Palo", 75d), ("Fila", "Fila", 60d), ("q ∥ [m]", "Q", 100d), ("t ⊥ [m]", "T", 100d), ("β", "Beta", 85d), ("α", "Alfa", 85d), ("Coefficiente", "Coefficiente", 100d), ("Riduzione", "Riduzione", 100d) }) Column(details, header, key, width);
        detailPanel.Children.Add(details); var detailPaper = Ui.Paper(detailPanel, 14); detailPaper.Margin = new Thickness(0, 12, 0, 0); output.Children.Add(detailPaper);
        var columns = new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(355) }); columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.Children.Add(new ScrollViewer { Content = input, Background = Brushes.White, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var right = new ScrollViewer { Content = output, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetColumn(right, 1); columns.Children.Add(right); Content = columns;
        timer.Tick += Tick; loading = false; TryCalculate();
    }
    void BuildGenerator()
    {
        generator.Children.Clear(); var g = data["generatore"]!.AsObject(); string kind = g.S("tipo"); bool generic = kind == "Generica";
        coordinates.IsReadOnly = !generic; coordinates.CanUserAddRows = coordinates.CanUserDeleteRows = generic;
        coordinateHelp.Text = generic ? "Modifica x e y, aggiungi una riga o elimina con Canc. Ricalcolo automatico." : "Coordinate automatiche. Scegli Generica per modificarle dalla tabella.";
        if (generic) return;
        if (kind is "Rettangolare" or "Quinconce") { Field(generator, kind == "Quinconce" ? "Pali file lunghe N (corte N−1)" : "Pali per fila Nx", g, "nx"); Field(generator, "Numero di file Ny", g, "ny"); Field(generator, "Interasse nella fila Sx / D", g, "sx_d"); Field(generator, "Interasse fra file Sy / D", g, "sy_d"); }
        else if (kind == "Triangolare") { Field(generator, "Numero di pali su ogni lato", g, "lato"); Field(generator, "Interasse triangolare S / D", g, "sx_d"); }
        else { Field(generator, "Numero di anelli concentrici", g, "anelli"); Field(generator, "Suddivisioni/lato primo anello", g, "suddivisioni"); Field(generator, "Passo lungo il lato S / D", g, "sx_d"); Option(generator, "Palo al centro", g, "centro"); generator.Children.Add(Ui.Text("L’anello k ha k volte le suddivisioni del primo; il passo lungo il lato resta costante.", 11, color: Ui.Muted)); }
        Field(generator, "Rotazione palificata [° da +X]", g, "rotazione");
    }
    void BuildSpacing()
    {
        spacingFields.Children.Clear(); var spacings = data["interassi"]!.AsObject(); if (spacings[spacingDirection] is not JsonObject) spacings[spacingDirection] = new JsonObject();
        var values = spacings[spacingDirection]!.AsObject(); Field(spacingFields, "S∥ · tra file lungo H [m]", values, "interasse_parallelo", true); Field(spacingFields, "S⊥ · ortogonale a H [m]", values, "interasse_trasversale", true);
    }
    void LoadPiles() { bool before = loading; loading = true; piles.Clear(); foreach (var p in data.Array("pali")) piles.Add(new PileRow { Id = p.S("id"), X = p!["x"]!.ToString(), Y = p["y"]!.ToString() }); loading = before; }
    bool CapturePiles()
    {
        if (loading || data["generatore"].S("tipo") != "Generica") return false;
        var rows = new JsonArray(piles.Select(p => (JsonNode)J.Obj(("id", p.Id), ("x", Value(p.X)), ("y", Value(p.Y)))).ToArray()); if (JsonNode.DeepEquals(rows, data["pali"])) return false; data["pali"] = rows; return true;
    }
    void Queue() { if (loading || disposed) return; Result = null; Clear("Aggiornamento automatico…"); Modified?.Invoke(); if (calculating) return; timer.Stop(); timer.Start(); }
    void Tick(object? sender, EventArgs e) { timer.Stop(); TryCalculate(); }
    void Clear(string message) { title.Text = "Effetto di gruppo della palificata"; state.Text = message; source.Text = notes.Text = geometry.Text = ""; minValue.Text = meanValue.Text = maxValue.Text = reduction.Text = "—"; selecting = true; comparison.ItemsSource = null; selecting = false; details.ItemsSource = null; plan.Set(data, null, null); }
    internal void Commit() { coordinates.CommitEdit(DataGridEditingUnit.Cell, true); coordinates.CommitEdit(DataGridEditingUnit.Row, true); if (CapturePiles()) Queue(); if (!calculating && (timer.IsEnabled || Result is null)) TryCalculate(); }
    internal void TryCalculate()
    {
        if (loading || disposed || calculating) return; timer.Stop(); calculating = true;
        try
        {
            coordinates.CommitEdit(DataGridEditingUnit.Cell, true); coordinates.CommitEdit(DataGridEditingUnit.Row, true); CapturePiles(); HorizontalPileGroup.Regenerate(data); if (data["generatore"].S("tipo") != "Generica") LoadPiles();
            Result = HorizontalPileGroup.Calculate(data); var selected = Result["selezionato"]!; data["direzione_selezionata"] = selected.S("DirectionId"); data["angolo"] = selected.D("Angle");
            var rows = Result.Array("confronto").Select(n => new ComparisonRow { Node = n! }).ToArray(); selecting = true; comparison.ItemsSource = rows; comparison.SelectedItem = rows.First(r => r.Method == data.D("metodo") && r.DirectionId == data.S("direzione_selezionata")); selecting = false; Present();
        }
        catch (Exception ex) { Result = null; Clear("Dati da completare: " + ex.Message); }
        finally { selecting = false; calculating = false; }
    }
    void Present()
    {
        if (Result is null) return; var selected = Result["selezionato"]!; int method = (int)selected.D("Method"); bool available = selected["Factor"] is not null;
        title.Text = HorizontalPileGroup.MethodNames[method] + " · " + selected.S("DirectionName") + " " + F(J.Number(selected["Angle"])) + "°";
        state.Text = $"{data.Array("pali").Count} pali · calcolo automatico · " + (available ? (method == 0 ? "coefficiente Rg di kh/nh" : "coefficiente Pm di reazione p") : "metodo non applicabile: valori non disponibili");
        minValue.Text = F(J.Number(selected["Minimum"])); meanValue.Text = F(J.Number(selected["Factor"])); maxValue.Text = F(J.Number(selected["Maximum"])); reduction.Text = F(J.Number(selected["ReductionPercent"])) + (available ? "%" : "");
        geometry.Text = "S∥ · distanza fra file lungo H: " + F(J.Number(selected["ParallelSpacing"])) + " m   |   S⊥ · interasse ortogonale a H: " + F(J.Number(selected["TransverseSpacing"])) + " m\n" + "Asse–bordo basamento = " + F(J.Number(data["basamento"]?["bordo_d"]) * data.D("diametro")) + " m; margine libero dal palo = " + F((J.Number(data["basamento"]?["bordo_d"]) - .5) * data.D("diametro")) + " m.";
        source.Text = selected.S("Source"); notes.Text = string.Join("\n", new[] { selected.S("Error"), Result.S("errore_basamento") }.Concat(selected.Array("Warnings").Select(w => w!.ToString())).Where(s => s.Length > 0));
        details.ItemsSource = selected.Array("Piles").Select(p => new { Palo = p.S("Id"), Fila = p.D("Row"), Q = F(J.Number(p!["ParallelCoordinate"])), T = F(J.Number(p["TransverseCoordinate"])), Beta = method >= 4 && available ? F(J.Number(p["Beta"])) : "—", Alfa = method == 5 && available ? F(J.Number(p["Alpha"])) : "—", Coefficiente = available ? F(J.Number(p["Factor"])) : "—", Riduzione = available ? F((1 - J.Number(p["Factor"])) * 100) + "%" : "—" }).ToArray(); plan.Set(data, selected, Result["basamento"] as JsonArray);
    }
    public void Dispose() { disposed = true; timer.Stop(); timer.Tick -= Tick; foreach (var row in piles) row.Changed = null; }
}
