using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed class BridgeDesignWorkspace : UserControl
{
    internal JsonObject Data { get; }
    internal BridgeConcept.Result? Calculation { get; private set; }
    internal JsonObject? Result => Calculation?.Json();
    internal event Action? Modified;
    internal readonly TabControl Inputs = new(), Outputs = new();
    internal readonly Dictionary<string, TextBox> Editors = new();
    internal readonly BridgeDesignDrawing Drawing = new() { Height = 400, MinWidth = 300 };
    private readonly Grid upper = new();
    private readonly Border inputCard, drawingCard;
    private readonly TextBlock status = Ui.Text("", 12), configuration = Ui.Text("", 12, color: Ui.Muted);
    private readonly TextBlock[] values = Enumerable.Range(0, 4).Select(_ => Ui.Text("—", 26, true)).ToArray();
    private readonly TextBlock[] captions = Enumerable.Range(0, 4).Select(_ => Ui.Text("", 11, color: Ui.Muted)).ToArray();
    private readonly ContentControl quantities = new(), detail = new(), comparison = new(), advice = new();
    private StackPanel sectionPanel = new();
    private readonly Stack<JsonObject> history = new();
    private JsonObject previous;
    private bool refreshing;
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    internal static string F(double n, string format = "N1") => n.ToString(format, It);

    internal BridgeDesignWorkspace(JsonObject data)
    {
        Data = data; previous = Clone(Data); Background = Ui.Bg;
        var title = Ui.Stack(Ui.Text("BRIDGE DESIGN", 24, true), Ui.Text("Dal sito al ponte · geometria, quantità e ordini di grandezza", 13, color: Ui.Muted));
        title.Margin = new Thickness(2, 0, 0, 10);
        var scope = Ui.Text("PREDIMENSIONAMENTO   ·   Prezzi e coefficienti modificabili   ·   Importi in EUR, IVA esclusa", 11, true, Ui.Blue);
        scope.Margin = new Thickness(2, 0, 0, 12);
        BuildInputs();
        Inputs.MinHeight = 470;
        inputCard = Ui.Paper(Inputs, 10);
        var controls = Ui.Bar(
            Ui.Button("Prospetto", () => { Drawing.Section = false; Drawing.InvalidateVisual(); }, inspection: true),
            Ui.Button("Sezione", () => { Drawing.Section = true; Drawing.InvalidateVisual(); }, inspection: true),
            Ui.Button("Nuovo paesaggio", () => Mutate(() => Data["scene"] = Data.D("scene") + 1)),
            Ui.Button("Ponte casuale", Randomize),
            Ui.Button("Fissa A", Pin), Ui.Button("Annulla", Undo),
            Ui.Button("Dettagli", () => { Outputs.SelectedIndex = 1; Outputs.BringIntoView(); }, inspection: true));
        var exports = Ui.Bar(Ui.Button("Esporta quantità CSV", ExportCsv, inspection: true), Ui.Button("Immagine PNG", ExportPng, inspection: true),
            Ui.Button("Stampa scheda", Print, inspection: true));
        drawingCard = Ui.Paper(Ui.Stack(controls, Drawing, configuration, exports), 12);
        upper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(375) }); upper.ColumnDefinitions.Add(new ColumnDefinition());
        upper.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); upper.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        upper.Children.Add(inputCard); upper.Children.Add(drawingCard); Grid.SetColumn(drawingCard, 1);
        inputCard.Margin = new Thickness(0, 0, 12, 0);
        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 12, 0, 12) };
        string[] labels = ["COSTO INDICATIVO", "IMPRONTA MATERIALI + CANTIERE", "ALTEZZA IMPALCATO", "DURATA INDICATIVA"];
        for (int k = 0; k < 4; k++)
        {
            var p = Ui.Paper(Ui.Stack(Ui.Text(labels[k], 10, true, Ui.Muted), values[k], captions[k]), 14);
            p.Margin = new Thickness(k == 0 ? 0 : 6, 0, k == 3 ? 0 : 6, 0); metrics.Children.Add(p);
        }
        Ui.Tab(Outputs, "Quantità e costi", quantities);
        Ui.Tab(Outputs, "Dettagli del predimensionamento", detail);
        Ui.Tab(Outputs, "Confronto A / B", comparison);
        Ui.Tab(Outputs, "Prezzi unitari", Scroll(ParameterPanel("rates", BridgeConcept.Rates), 365));
        Ui.Tab(Outputs, "Ipotesi e coefficienti", Scroll(ParameterPanel("assumptions", BridgeConcept.Assumptions), 365));
        Outputs.MinHeight = 330;
        var bottom = Ui.Stack(Ui.Paper(Outputs, 12), advice);
        status.Margin = new Thickness(4, 10, 4, 6);
        var stack = Ui.Stack(title, scope, upper, metrics, bottom, status); stack.Margin = new Thickness(20, 16, 20, 12);
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        SizeChanged += (_, _) =>
        {
            bool compact = ActualWidth < 1100;
            upper.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(375);
            upper.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(drawingCard, compact ? 0 : 1); Grid.SetRow(drawingCard, compact ? 1 : 0);
            inputCard.Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
            Inputs.MinHeight = compact ? 330 : 470; metrics.Columns = ActualWidth < 850 ? 2 : 4;
        };
        Recalculate();
    }
    private static ScrollViewer Scroll(UIElement content, double max = double.PositiveInfinity) => new ChainedScrollViewer
    { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = max };
    private static JsonObject Clone(JsonObject d) => (JsonObject)d.DeepClone();
    private JsonObject Input => Data["input"]!.AsObject();
    private void BuildInputs()
    {
        Inputs.Items.Clear();
        foreach (string key in Editors.Keys.Where(k => k.StartsWith("input/")).ToArray()) Editors.Remove(key);
        sectionPanel = new StackPanel();
        var site = new StackPanel();
        Choice(site, "obstacle", "Ostacolo da attraversare", BridgeConcept.Obstacles);
        Choice(site, "soil", "Terreno convenzionale", BridgeConcept.Soils);
        foreach (var p in BridgeConcept.Site) site.Children.Add(Number("input", p, true));
        Ui.Tab(Inputs, "1 · Sito", Scroll(site, 490));
        var layout = ParameterPanel("input", BridgeConcept.Layout, true);
        Toggle(layout, "continuous", "Impalcato continuo"); Toggle(layout, "start_pier", "Inizio su pila · viadotto prosegue"); Toggle(layout, "end_pier", "Fine su pila · viadotto prosegue");
        layout.Children.Add(Ui.Text("Campate terminali = 0,8 × interne per schemi continui con almeno 3 campate. Le pile vengono spostate fuori dall'ostacolo, ove possibile.", 12, color: Ui.Muted));
        layout.Children.Add(Ui.Button("Ripristina dimensioni automatiche", AutoSize));
        Ui.Tab(Inputs, "2 · Campate", Scroll(layout, 490));
        BuildSection(); Ui.Tab(Inputs, "3 · Sezione", Scroll(sectionPanel, 490));
        var sub = new StackPanel(); Choice(sub, "pier", "Tipologia pila", BridgeConcept.Piers); Choice(sub, "foundation", "Fondazioni", BridgeConcept.Foundations);
        foreach (var p in BridgeConcept.Substructure) sub.Children.Add(Number("input", p, true));
        sub.Children.Add(Ui.Text("0 mantiene il dimensionamento automatico. Le classi di terreno sono parametri convenzionali modificabili nelle ipotesi.", 11, color: Ui.Muted));
        Ui.Tab(Inputs, "4 · Pile", Scroll(sub, 490));
    }
    private void BuildSection()
    {
        sectionPanel.Children.Clear();
        var families = new UniformGrid { Columns = 2 };
        foreach (var f in BridgeConcept.Families)
        {
            var button = Ui.Button("", () => { Mutate(() => Input["family"] = f.Id); BuildSection(); });
            bool selected = Input.S("family") == f.Id;
            button.Background = selected ? Ui.Navy : Brushes.White; button.Foreground = selected ? Brushes.White : Ui.Navy;
            button.Content = Ui.Stack(Ui.Text(f.Name, 11, true, button.Foreground), new BridgeFamilyIcon { Family = f.Id, Ink = button.Foreground, Height = 32, Width = 112 }, Ui.Text($"{f.MinSpan:0}–{f.MaxSpan:0} m", 10, color: selected ? Ui.Brush("#BCCFE2") : Ui.Muted));
            button.SetValue(AutomationProperties.NameProperty, f.Name); button.MinHeight = 82; button.Padding = new Thickness(6); families.Children.Add(button);
        }
        sectionPanel.Children.Add(families);
        string id = Input.S("family");
        var active = new HashSet<string> { "depth", "fc" };
        if (id != "slab") active.Add("slab");
        if (id is "tee" or "psc_i" or "psc_u" or "steel_i") active.Add("spacing");
        if (id is "tee" or "psc_u" or "psc_box" or "fcm") active.Add("web");
        if (id is "psc_u" or "psc_box" or "fcm") active.Add("bottom");
        if (id is "psc_box" or "fcm") { active.Add("bottom_ratio"); active.Add("cells"); }
        if (id.StartsWith("steel")) foreach (string key in new[] { "flange_width", "flange_mm", "web_mm" }) active.Add(key);
        if (id == "steel_box") { active.Add("boxes"); active.Add("web_slope"); }
        if (id == "psc_i") active.Add("haunch");
        if (id == "psc_u") { active.Add("u_top"); active.Add("u_bottom"); }
        foreach (var p in BridgeConcept.Section.Where(p => active.Contains(p.Key))) sectionPanel.Children.Add(Number("input", p, true));
        sectionPanel.Children.Add(Ui.Button("Sezione automatica", AutoSize));
    }
    private StackPanel ParameterPanel(string group, IEnumerable<BridgeConcept.Parameter> parameters, bool sliders = false)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };
        if (group == "rates") panel.Children.Add(Ui.Text("Listino EUR di partenza, indicativo. Prezzi riferiti alle voci elencate; pali comprensivi di cls e perforazione, armatura conteggiata a parte.", 12, color: Ui.Muted));
        if (group == "assumptions") panel.Children.Add(Ui.Text("Ipotesi del modello di stima. I carichi sono equivalenti uniformi; i moltiplicatori non definiscono una combinazione normativa completa.", 12, color: Ui.Muted));
        foreach (var p in parameters) panel.Children.Add(Number(group, p, sliders));
        return panel;
    }
    private FrameworkElement Number(string group, BridgeConcept.Parameter p, bool slider)
    {
        var row = new Grid { Margin = new Thickness(3, 6, 3, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        row.Children.Add(Ui.Text(p.Label, 12));
        var edit = new TextBox { Text = Data[group]!.S(p.Key), TextAlignment = TextAlignment.Center, MinHeight = 26, Tag = group + "/" + p.Key };
        edit.SetValue(AutomationProperties.NameProperty, p.Label); Editors[group + "/" + p.Key] = edit;
        Grid.SetColumn(edit, 1); row.Children.Add(edit); var unit = Ui.Text(p.Unit, 11, color: Ui.Muted); unit.Margin = new Thickness(5, 0, 0, 0); Grid.SetColumn(unit, 2); row.Children.Add(unit);
        var panel = Ui.Stack(row); bool sync = false;
        Slider? range = slider ? new Slider { Minimum = p.Min, Maximum = p.Max, Value = Data[group].D(p.Key), TickFrequency = p.Step, IsSnapToTickEnabled = true, Margin = new Thickness(6, 0, 6, 2), Focusable = false } : null;
        if (range is not null)
        {
            range.SetValue(AutomationProperties.NameProperty, p.Label + " · cursore"); panel.Children.Add(range);
            range.ValueChanged += (_, _) => { if (sync || refreshing) return; edit.Text = range.Value.ToString("0.###", It); };
            range.PreviewMouseWheel += (_, e) => { ChainedScrollViewer.ForwardWheel(range, e); e.Handled = true; };
        }
        edit.TextChanged += (_, _) =>
        {
            if (refreshing) return;
            Data[group]![p.Key] = edit.Text;
            double? v = J.Number(Data[group]![p.Key]);
            edit.BorderBrush = v is null || v < p.Min || v > p.Max ? Brushes.IndianRed : Ui.Brush("#DCE2E9");
            if (range is not null && v.HasValue) { sync = true; range.Value = v.Value; sync = false; }
            Changed();
        };
        return panel;
    }
    private void Choice(Panel panel, string key, string label, string[] choices)
    {
        var combo = Ui.Choice(choices, Input.S(key)); combo.SetValue(AutomationProperties.NameProperty, label);
        var box = Ui.Stack(Ui.Text(label, 12), combo); box.Margin = new Thickness(4, 8, 4, 3); panel.Children.Add(box);
        combo.SelectionChanged += (_, _) => { if (!refreshing && combo.SelectedItem is string s) Mutate(() => Input[key] = s); };
    }
    private void Toggle(Panel panel, string key, string label)
    {
        var check = new CheckBox { Content = label, IsChecked = Input.B(key), Margin = new Thickness(6, 12, 6, 8) }; panel.Children.Add(check);
        check.Checked += (_, _) => { if (!refreshing) Mutate(() => Input[key] = true); };
        check.Unchecked += (_, _) => { if (!refreshing) Mutate(() => Input[key] = false); };
    }
    private void Changed()
    {
        if (refreshing) return;
        if (!JsonNode.DeepEquals(previous, Data)) { history.Push(previous); if (history.Count > 80) { var recent = history.Take(60).Reverse().ToArray(); history.Clear(); foreach (var h in recent) history.Push(h); } previous = Clone(Data); }
        Recalculate(); Modified?.Invoke();
    }
    private void Mutate(Action change, bool rebuild = false)
    {
        change(); Changed();
        if (rebuild) RefreshInputs();
    }
    private void RefreshInputs()
    {
        int selected = Inputs.SelectedIndex; refreshing = true;
        try
        {
            BuildInputs(); Inputs.SelectedIndex = Math.Max(0, selected);
            foreach (var (key, editor) in Editors) { var parts = key.Split('/'); editor.Text = Data[parts[0]]!.S(parts[1]); }
        }
        finally { refreshing = false; }
    }
    internal void Recalculate()
    {
        try
        {
            Calculation = BridgeConcept.Calculate(Data); Drawing.Data = Data; Drawing.Result = Calculation; Drawing.InvalidateVisual();
            var r = Calculation;
            values[0].Text = F(r.TotalCost / 1e6, "0.00") + " M€"; captions[0].Text = $"{F(r.TotalCost / r.Area, "N0")} €/m² · {F(r.CostLow / 1e6, "0.00")}–{F(r.CostHigh / 1e6, "0.00")} M€";
            values[1].Text = F(r.Carbon, "N0") + " tCO₂e"; captions[1].Text = F(r.Carbon * 1000 / r.Area, "N0") + " kgCO₂e/m² · stima parziale";
            values[2].Text = F(r.Depth, "0.00") + " m"; captions[2].Text = $"L/d ≈ {F(r.Spans.Max() / r.Depth, "0")} · {r.Spans.Length} campate";
            values[3].Text = F(r.Duration, "0") + " mesi"; captions[3].Text = $"±25% · {r.Supports.Count(s => s.Type != "Spalla")} pile · {r.Supports.Count(s => s.Type == "Spalla")} spalle";
            configuration.Text = $"L {F(r.Length)} m · W {F(r.Width)} m · {r.Family.Name} · {r.Girders} elementi · {r.Foundation}" + (r.PileLength > 0 ? $" × {F(r.PileLength, "0")} m" : "");
            ShowQuantities(r); ShowDetails(r); ShowComparison(); ShowAdvice(r);
            status.Text = BridgeConcept.Scope; status.Foreground = Ui.Muted;
        }
        catch (ArgumentException ex)
        {
            Calculation = null; Drawing.Result = null; Drawing.InvalidateVisual();
            foreach (var v in values) v.Text = "—"; foreach (var c in captions) c.Text = "Dati da completare";
            quantities.Content = detail.Content = comparison.Content = advice.Content = null; configuration.Text = "";
            status.Text = ex.Message; status.Foreground = Brushes.Firebrick;
        }
    }
    private static DataGrid Table(string[] headers, IEnumerable<string[]> rows, double height = 300)
    {
        var grid = Ui.Table(headers, rows); grid.MaxHeight = height; grid.MinHeight = 90;
        foreach (var column in grid.Columns) column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        grid.Columns[0].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
        foreach (var column in grid.Columns.OfType<DataGridTextColumn>())
        {
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap)); style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(5, 3, 5, 3))); column.ElementStyle = style;
        }
        grid.RowHeight = double.NaN; return grid;
    }
    private void ShowQuantities(BridgeConcept.Result r)
    {
        var table = Table(["Voce", "Parte", "Quantità", "Unità", "Prezzo €", "Importo €"], r.Quantities.Select(q => new[] { q.Item, q.Group, F(q.Amount), q.Unit, F(q.Rate), F(q.Cost, "N0") }));
        double prelims = r.DirectCost * Data["assumptions"].D("prelims") / 100;
        quantities.Content = Ui.Stack(table, Ui.Text($"Diretto {F(r.DirectCost, "N0")} €   +   cantiere {F(prelims, "N0")} €   +   imprevisti {F(r.TotalCost - r.DirectCost - prelims, "N0")} €   =   {F(r.TotalCost, "N0")} €", 13, true));
    }
    private void ShowDetails(BridgeConcept.Result r)
    {
        var panel = Ui.Stack(Ui.Text(BridgeConcept.Scope, 12, true), Ui.Text("Carico uniforme su tutte le campate. Diagramma riferito all'intero impalcato; non è l'inviluppo di un treno di carico.", 12, color: Ui.Muted), new BridgeConceptMomentPlot { Result = r, Height = 180 });
        panel.Children.Add(Table(["Grandezza", "Valore", "Unità", "Regola / ipotesi"], r.Details.Select(d => new[] { d.Name, F(d.Value, "N3"), d.Unit, d.Rule }), 600));
        panel.Children.Add(Table(["Appoggio", "x [m]", "Tipo", "R G+Q [kN]", "H [m]", "Plinto B×W [m]", "Pali"], r.Supports.Select(s => new[] { (s.Index + 1).ToString(), F(s.X), s.Type, F(s.Reaction), F(s.PierHeight), F(s.FootingSize) + "×" + F(s.FootingWidth), s.Piles.ToString() }), 500));
        foreach (string warning in r.Warnings) panel.Children.Add(Ui.Text("• " + warning, 12, color: Ui.Brush("#895C17")));
        panel.Children.Add(Ui.Text("Esclusi: sismica, vento, frenatura, urti, fatica, fessurazione, viscosità, perdite di precompressione, instabilità locale, collegamenti, fasi costruttive e verifiche geotecniche complete.", 12, color: Ui.Muted));
        detail.Content = Scroll(panel, 365);
    }
    internal void Pin()
    {
        if (Calculation is null) return;
        var a = Clone(Data); a.Remove("alternative_a"); Mutate(() => Data["alternative_a"] = a);
        Outputs.SelectedIndex = 2;
    }
    private void ShowComparison()
    {
        if (Data["alternative_a"] is not JsonObject a || Calculation is null) { comparison.Content = Ui.Text("Fissa una configurazione con “Fissa A”, poi modifica il ponte. Qui appariranno gli scostamenti della soluzione B corrente.", 14); return; }
        try
        {
            var old = BridgeConcept.Calculate(a); var r = Calculation;
            var rows = new[] { ("Costo totale [€]", old.TotalCost, r.TotalCost), ("CO₂ [t]", old.Carbon, r.Carbon), ("Calcestruzzo [m³]", old.Concrete, r.Concrete), ("Acciaio strutturale [t]", old.Steel, r.Steel), ("Durata [mesi]", old.Duration, r.Duration), ("Altezza [m]", old.Depth, r.Depth) };
            comparison.Content = Ui.Stack(Ui.Text($"A: {old.Family.Name}, {old.Spans.Length} campate  →  B: {r.Family.Name}, {r.Spans.Length} campate", 14, true),
                Table(["Indicatore", "A salvata", "B corrente", "Δ B − A", "Δ %"], rows.Select(v => new[] { v.Item1, F(v.Item2), F(v.Item3), F(v.Item3 - v.Item2), v.Item2 != 0 ? F((v.Item3 / v.Item2 - 1) * 100) + "%" : "—" })),
                Ui.Bar(Ui.Button("Ripristina A", () => Restore(a)), Ui.Button("Rimuovi confronto", () => Mutate(() => Data.Remove("alternative_a")))));
        }
        catch (ArgumentException ex) { comparison.Content = Ui.Text("Alternativa A non calcolabile: " + ex.Message, color: Brushes.Firebrick); }
    }
    private void ShowAdvice(BridgeConcept.Result r)
    {
        var panel = Ui.Stack(Ui.Text("Alternative per ridurre le quantità e le emissioni", 15, true)); panel.Margin = new Thickness(0, 14, 0, 0);
        void Option(string label, Action<JsonObject> change)
        {
            var candidate = Clone(Data); change(candidate["input"]!.AsObject());
            try
            {
                var alternate = BridgeConcept.Calculate(candidate); double saving = r.Carbon - alternate.Carbon;
                if (saving <= .01) return;
                var button = Ui.Button($"{label}   ·   −{F(saving, "N0")} tCO₂e   ·   Δ {F(alternate.TotalCost - r.TotalCost, "N0")} €", () => Mutate(() => change(Input), true));
                button.HorizontalContentAlignment = HorizontalAlignment.Left; panel.Children.Add(button);
            }
            catch (ArgumentException) { /* An infeasible alternative is not presented as a saving. */ }
        }
        if (!Input.B("low_carbon")) Option("Cls a ridotta impronta", n => n["low_carbon"] = true);
        if (r.Steel > 0 && !Input.B("recycled")) Option("Acciaio ad alto riciclato", n => n["recycled"] = true);
        if (Input.D("shoulder") > 1) Option("Banchine a 1,0 m", n => n["shoulder"] = 1);
        if (r.Spans.Length > 1)
        {
            var candidate = Clone(Data); candidate["input"]!["spans"] = r.Spans.Length - 1;
            try { var test = BridgeConcept.Calculate(candidate); if (test.Spans.Max() <= test.Family.MaxSpan) Option("Una campata in meno", n => n["spans"] = r.Spans.Length - 1); } catch (ArgumentException) { }
        }
        panel.Children.Add(Ui.Text("Confronti sul modello corrente: disponibilità dei materiali, resistenza iniziale e larghezze stradali vanno valutate per il progetto. I prezzi restano quelli del listino impostato.", 11, color: Ui.Muted));
        if (Input.B("low_carbon") || Input.B("recycled")) panel.Children.Add(Ui.Button("Ripristina fattori ambientali ordinari", () => Mutate(() => { Input["low_carbon"] = false; Input["recycled"] = false; })));
        advice.Content = panel;
    }
    internal void AutoSize() => Mutate(() => { foreach (string k in new[] { "depth", "slab", "spacing", "web", "bottom", "boxes", "pier_size", "pile_count", "footing_size", "pile_length" }) Input[k] = 0; }, true);
    internal void Undo() { if (!history.TryPop(out var snapshot)) return; refreshing = true; try { Data.Clear(); foreach (var pair in snapshot) Data[pair.Key] = pair.Value?.DeepClone(); previous = Clone(Data); } finally { refreshing = false; } RefreshInputs(); Recalculate(); Modified?.Invoke(); }
    private void Restore(JsonObject source)
    {
        var copy = Clone(source); var pinned = Data["alternative_a"]?.DeepClone();
        Mutate(() => { Data.Clear(); foreach (var p in copy) Data[p.Key] = p.Value?.DeepClone(); if (pinned is not null) Data["alternative_a"] = pinned; }, true);
    }
    internal void Randomize()
    {
        var random = Random.Shared; var f = BridgeConcept.Families[random.Next(BridgeConcept.Families.Length)];
        Mutate(() =>
        {
            var defaults = BridgeConcept.Defaults()["input"]!.AsObject(); Data["input"] = defaults.DeepClone();
            Input["family"] = f.Id; int spans = random.Next(2, 7); Input["spans"] = spans;
            Input["length"] = Math.Round((f.MinSpan + (f.MaxSpan - f.MinSpan) * (.2 + random.NextDouble() * .45)) * (spans - .4));
            Input["height"] = Math.Max(random.Next(8, 31), Math.Ceiling(Input.D("length") / (spans - .4) / 18 + 5)); Input["lanes"] = random.Next(2, 7); Input["obstacle"] = BridgeConcept.Obstacles[random.Next(3)];
            Input["soil"] = BridgeConcept.Soils[random.Next(4)]; Input["pier"] = BridgeConcept.Piers[random.Next(4)];
            Input["obstacle_width"] = Math.Min(20, Input.D("length") / 8); Data["scene"] = random.Next(10000);
        }, true);
    }
    private void Save(string filter, string name, Action<string> write)
    {
        if (Calculation is null) return;
        var dialog = new SaveFileDialog { Filter = filter, FileName = name };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { write(dialog.FileName); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione non riuscita"); }
    }
    private void ExportCsv() => Save("Quantità CSV|*.csv", "BridgeDesign_quantita.csv", filename => Archivio.ScriviAtomico(filename, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BridgeConceptExport.Csv(Calculation!))).ToArray()));
    private void ExportPng() => Save("Immagine PNG|*.png", "BridgeDesign.png", filename => Archivio.ScriviAtomico(filename, Drawing.Png()));
    internal byte[] BuildReport(string title) => BridgeConceptExport.Report(title, Data, Calculation ?? throw new InvalidOperationException("Completare i dati del ponte."), Drawing.Png(false), Drawing.Png(true));
    private void Print()
    {
        if (Calculation is null) return;
        var dialog = new PrintDialog(); if (dialog.ShowDialog() != true) return;
        var r = Calculation;
        var drawing = new BridgeDesignDrawing { Data = Data, Result = r, Width = 1000, Height = 420 };
        var panel = Ui.Stack(Ui.Text("ANTHEA · Bridge Design", 26, true), Ui.Text(BridgeConcept.Scope, 14), drawing,
            Ui.Text($"{r.Family.Name} · L {F(r.Length)} m · W {F(r.Width)} m · d {F(r.Depth)} m", 16),
            Ui.Text($"Costo {F(r.TotalCost, "N0")} € · CO₂ {F(r.Carbon, "N0")} t · Durata {r.Duration:0} mesi", 18, true));
        foreach (var q in r.Quantities) panel.Children.Add(Ui.Text($"{q.Group} / {q.Item}: {F(q.Amount)} {q.Unit} × {F(q.Rate)} € = {F(q.Cost, "N0")} €", 14));
        panel.Children.Add(Ui.Text("Listino e fattori ambientali indicativi. Dettagli e ipotesi completi nel report Word.", 12));
        var box = new Viewbox { Child = panel, Width = dialog.PrintableAreaWidth, Height = dialog.PrintableAreaHeight, Stretch = Stretch.Uniform };
        box.Measure(new Size(box.Width, box.Height)); box.Arrange(new Rect(0, 0, box.Width, box.Height)); dialog.PrintVisual(box, "ANTHEA Bridge Design");
    }
}
