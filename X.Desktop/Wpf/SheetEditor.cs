using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class SheetEditor : UserControl, IDisposable
{
    internal string Module { get; }
    internal JsonObject Data { get; }
    private JsonObject? result;
    private bool busy;
    private readonly ConcreteWorkspace? concrete;
    private readonly HorizontalWorkspace? horizontal;
    internal JsonObject? Result { get => horizontal is not null ? horizontal.Result : concrete is null ? result : concrete.Result; private set => result = value; }
    internal bool Busy { get => horizontal?.Busy ?? concrete?.Busy ?? busy; private set => busy = value; }
    internal event Action? Modified;
    private bool building = true, disposed;
    private int revision, expanded = -1;
    private bool Micro => Module == "geo_micropalo_verticale";
    private bool Pile => Module == "geo_palo_verticale";
    private bool Geo => Pile || Micro;
    private bool Section => Module == "str_palo";
    private readonly Canvas canvas = new();
    private readonly ScrollViewer scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<Border> cards = [];
    private readonly Dictionary<int, Button> expandButtons = new();
    private readonly TextBlock status = Ui.Text("Dati da verificare · premere Calcola", color: Ui.Muted);
    private readonly TextBox warnings = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Ui.Brush("#FFFAEB"), MaxHeight = 62, Visibility = Visibility.Collapsed };
    private readonly Button calculate;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly Plot plot = new() { Capacity = true }, reference = new() { InvertY = false };
    private readonly StratigraphyDrawing stratigraphy = new();
    private readonly TabControl outputs = new(), sondages = new();
    private readonly ComboBox tableSelect = new();
    private readonly ContentControl tableHost = new();
    private readonly TextBox raw = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas") };
    private readonly ContentControl verification = new();
    private readonly ComboBox capacityView = new();
    private readonly WrapPanel curveChoices = new();
    private readonly TextBlock endValues = Ui.Text("Valori a L non disponibili", 11, true);
    private readonly TextBlock effLabel = Ui.Text("", 12, color: Ui.Muted);
    private readonly List<JsonGrid> layerGrids = [];
    private readonly List<(string Key, Serie Series)> allSeries = [];
    private readonly Dictionary<string, bool> visibility = new();
    private List<Tabella> tables = [];
    private InputForm generalForm = null!, normativeForm = null!, efficiencyForm = null!;

    internal SheetEditor(string module, JsonObject data)
    {
        Module = module; Data = (JsonObject)data.DeepClone(); Background = Ui.Bg;
        calculate = Ui.Button("Calcola", async () => await CalculateAsync(), true); calculate.Width = 120; calculate.Visibility = Geo ? Visibility.Collapsed : Visibility.Visible;
        if (module == PaloOrizzontale.Module)
        {
            horizontal = new HorizontalWorkspace(Data); horizontal.Modified += () => Modified?.Invoke(); Content = horizontal; building = false; return;
        }
        if (Section)
        {
            concrete = new ConcreteWorkspace(Data); concrete.Modified += () => Modified?.Invoke(); Content = concrete; building = false; return;
        }
        var footer = Ui.Dock(status, bottom: null); DockPanel.SetDock(calculate, System.Windows.Controls.Dock.Right); footer.Children.Insert(0, calculate); footer.Margin = new Thickness(24, 4, 24, 4);
        scroll.Content = canvas; Content = Ui.Dock(scroll, bottom: Ui.Stack(footer, warnings));
        tableSelect.SelectionChanged += (_, _) => ShowTable();
        Ui.Tab(outputs, "Tabelle e dettagli", Ui.Dock(tableHost, tableSelect)); Ui.Tab(outputs, "Risultati JSON", raw);
        BuildGeo();
        scroll.SizeChanged += (_, _) => LayoutCards();
        scroll.ScrollChanged += (_, e) => { if (e.Source == scroll && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) LayoutCards(); };
        timer.Tick += TimerTick;
        building = false; Preview(); LayoutCards(); if (Geo) QueueCalculation();
    }
    private async void TimerTick(object? sender, EventArgs args) { if (Busy) return; timer.Stop(); if (!disposed) await CalculateAsync(commitEdits: false); }
    public void Dispose() { disposed = true; timer.Stop(); timer.Tick -= TimerTick; concrete?.Dispose(); horizontal?.Dispose(); }
    internal void Commit() { if (horizontal is not null) horizontal.Commit(); else if (concrete is not null) concrete.Commit(); else foreach (var grid in layerGrids) grid.Commit(); }
    private void AddCard(string title, UIElement content, bool expandable = false, UIElement? action = null)
    {
        int index = cards.Count; var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8), MinHeight = 28 };
        if (expandable)
        {
            var b = Ui.Button("Estendi", () => { expanded = expanded == index ? -1 : index; LayoutCards(); }); b.FontSize = 11; b.Padding = new Thickness(5, 2, 5, 2); DockPanel.SetDock(b, System.Windows.Controls.Dock.Right); header.Children.Add(b); expandButtons[index] = b;
        }
        if (action is not null) { DockPanel.SetDock(action, System.Windows.Controls.Dock.Right); header.Children.Add(action); }
        header.Children.Add(Ui.Text(title, Geo && index == 3 ? 18 : 16, true)); var card = Ui.Paper(Ui.Dock(content, header), Section ? 8 : 14); card.ClipToBounds = true; cards.Add(card); canvas.Children.Add(card);
    }
    private void LayoutCards()
    {
        double availableWidth = double.IsFinite(scroll.ViewportWidth) && scroll.ViewportWidth > 0 ? scroll.ViewportWidth : ActualWidth;
        double availableHeight = double.IsFinite(scroll.ViewportHeight) && scroll.ViewportHeight > 0 ? scroll.ViewportHeight : ActualHeight;
        double w = Math.Max(320, availableWidth - 4), h = Math.Max(240, availableHeight - 4);
        double margin = w < 1000 ? 12 : 24, gap = Geo ? 12 : 20;
        var generalSize = Geo ? generalForm.UnwrappedSize() : default;
        double generalWidth = Math.Ceiling(generalSize.Width) + 32;
        double efficiencyWidth = Geo ? Math.Ceiling(efficiencyForm.UnwrappedSize().Width) + 32 : 0;
        double normativeWidth = Geo ? Math.Ceiling(normativeForm.UnwrappedSize().Width) + 32 : 0;
        bool compactLayout = w < Math.Max(1450, Geo ? generalWidth + efficiencyWidth + normativeWidth + 378 + 2 * margin + 3 * gap : 0);
        double pileTop = Geo ? Math.Ceiling(Math.Max(generalSize.Height,
            Math.Max(normativeForm.UnwrappedSize().Height, efficiencyForm.UnwrappedSize().Height + 40))) + 68 : 460;
        foreach (var (index, b) in expandButtons) b.Content = expanded == index ? "Riduci" : "Estendi";
        void Place(int i, double x, double y, double width, double height)
        { var c = cards[i]; Canvas.SetLeft(c, x); Canvas.SetTop(c, y); c.Width = Math.Max(100, width); c.Height = Math.Max(100, height); }
        if (!Section && (expanded == 6 || Geo && expanded == 4 || compactLayout && expanded >= 0))
        {
            for (int i = 0; i < cards.Count; i++) cards[i].Visibility = i == expanded ? Visibility.Visible : Visibility.Collapsed;
            Place(expanded, 16, 12, w - 32, h - 24); canvas.Width = w; canvas.Height = h; UpdateProfile(); return;
        }
        foreach (var card in cards) card.Visibility = Visibility.Visible;
        if (!Section && compactLayout)
        {
            int columns = w < 1000 ? 1 : 2;
            double cardWidth = (w - 2 * margin - (columns - 1) * gap) / columns, y = 14;
            for (int i = 0; i < 4; i += columns)
            {
                double rowHeight = pileTop;
                for (int col = 0; col < columns; col++) Place(i + col, margin + col * (cardWidth + gap), y, cardWidth, rowHeight);
                y += rowHeight + gap;
            }
            // Tables and plots need the full available width in the compact layout.
            for (int i = 4; i < cards.Count; i++)
            {
                double rowHeight = Math.Clamp(h - 28, 340, 500);
                Place(i, margin, y, w - 2 * margin, rowHeight);
                y += rowHeight + gap;
            }
            canvas.Width = w; canvas.Height = y; UpdateProfile(); return;
        }
        if (Section)
        {
            double left = (w - 2 * margin - gap) * .60, right = w - 2 * margin - gap - left, top = 430, third = (left - 2 * gap) / 3;
            for (int i = 0; i < 3; i++) Place(i, margin + i * (third + gap), margin, third, top);
            double half = (left - gap) / 2; Place(3, margin, margin + top + gap, half, h - top - gap - 2 * margin); Place(4, margin + half + gap, margin + top + gap, left - half - gap, h - top - gap - 2 * margin);
            double rh = (h - 2 * margin - 2 * gap) / 3; for (int i = 0; i < 3; i++) Place(5 + i, margin + left + gap, margin + i * (rh + gap), right, rh);
        }
        else
        {
            double top = pileTop, x = margin, avail = w - 2 * margin - 3 * gap;
            double efficiency = Geo ? efficiencyWidth : 0, normative = Geo ? normativeWidth : 0;
            double[] widths = Geo
                ? [generalWidth, efficiency, normative, avail - generalWidth - efficiency - normative]
                : [avail * .39, avail * .17, avail * .24, avail * .20];
            for (int i = 0; i < 4; i++) { double cw = widths[i]; Place(i, x, 14, cw, top); x += cw + gap; }
            double y = 14 + top + gap; h = Math.Max(h, y + 440); avail = w - 2 * margin - 2 * gap; x = margin;
            double[] lower = expanded == 5 ? [.16, .65, .19] : expanded == 4 ? [.66, .15, .19] : [.47, .20, .33];
            for (int i = 0; i < 3; i++) { double cw = avail * lower[i]; Place(4 + i, x, y, cw, h - y - 24); x += cw + gap; }
        }
        canvas.Width = w; canvas.Height = h; UpdateProfile();
    }
    private void Changed()
    {
        if (building || disposed) return;
        revision++; Result = null; tables = []; tableSelect.Items.Clear(); tableHost.Content = null; warnings.Text = ""; warnings.Visibility = Visibility.Collapsed;
        status.Text = "Dati modificati · premere Calcola"; allSeries.Clear(); visibility.Clear(); curveChoices.Children.Clear(); plot.Series = []; plot.InvalidateVisual();
        raw.Text = "Dati modificati · risultati da ricalcolare";
        Preview(); LayoutCards(); UpdateVerification(); QueueCalculation(); Modified?.Invoke();
    }
    private void QueueCalculation() { if (!Geo || building || disposed) return; timer.Stop(); timer.Start(); status.Text = "Aggiornamento automatico in attesa…"; }
    internal async Task CalculateAsync(bool commitEdits = true)
    {
        if (horizontal is not null) { await horizontal.CalculateAsync(); return; }
        if (concrete is not null) { await concrete.CalculateAllAsync(); return; }
        if (Busy || disposed) return; if (commitEdits) Commit(); timer.Stop(); Busy = true; calculate.IsEnabled = false; if (!Geo) canvas.IsEnabled = false;
        int requested = revision; var snapshot = (JsonObject)Data.DeepClone(); status.Text = "Calcolo in corso…";
        try
        {
            var result = await Task.Run(() => Calcolo.Calcola(snapshot, Micro));
            if (disposed || requested != revision) return;
            if (result.S("errore") != "") { Result = null; status.Text = "Dati da completare: " + result.S("errore"); SetWarnings(Geo ? "" : result.S("errore")); return; }
            Result = result; SetWarnings(string.Join(Environment.NewLine, result.Array("avvisi").Select(v => v!.ToString())));
            ShowGeoResults();
            raw.Text = Result.ToJsonString(J.Options); status.Text = "Calcolo completato · risultati riferiti ai dati correnti";
        }
        catch (Exception ex) { Result = null; status.Text = "Errore: " + ex.Message; SetWarnings(ex.Message); }
        finally { Busy = false; if (!disposed) { calculate.IsEnabled = true; canvas.IsEnabled = true; if (Geo && requested != revision) timer.Start(); } }
    }
    private void SetWarnings(string text) { warnings.Text = text; warnings.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible; }
    private void PopulateTables()
    { tableSelect.Items.Clear(); if (Geo) tableSelect.Items.Add("Capacità portante — riepilogo"); foreach (var t in tables) tableSelect.Items.Add(t.Titolo); if (tableSelect.Items.Count > 0) tableSelect.SelectedIndex = 0; }
    private void ShowTable()
    {
        int i = tableSelect.SelectedIndex;
        if (Geo && i == 0 && Result is not null)
        {
            var panel = new StackPanel { MaxWidth = 620 };
            panel.Children.Add(Ui.Text($"Compressione e trazione · quota disponibile {(Micro ? "s" : "z")} = {Result.D("profondita_massima").ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))} m" + (Result.B("copertura_completa") ? "" : " · copertura incompleta"), 11, color: Ui.Muted));
            foreach (var t in Tabelle.CapacitaPalo(Result, Micro))
            {
                var title = Ui.Text(t.Titolo, 15, true); title.HorizontalAlignment = HorizontalAlignment.Center; title.Margin = new Thickness(0, 12, 0, 5); panel.Children.Add(title);
                var grid = DetailTable(t);
                grid.MinHeight = 80; panel.Children.Add(grid);
            }
            panel.Children.Add(Ui.Text("Ramo governante (media o minimo) scelto separatamente per compressione e trazione.", 10, color: Ui.Muted));
            tableHost.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            return;
        }
        if (Geo) i--;
        if (i >= 0 && i < tables.Count) { var t = tables[i]; tableHost.Content = Geo ? DetailTable(t) : Ui.Table(t.Colonne, t.Righe); }
    }
    private static DataGrid DetailTable(Tabella table)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        string Format(string value) => double.TryParse(value, System.Globalization.NumberStyles.Float, culture, out double number) && double.IsFinite(number)
            ? number.ToString("0.0", culture) : value;
        var grid = Ui.Table(table.Colonne, table.Righe.Select(row => row.Select(Format).ToArray()));
        grid.FontSize = 12; grid.MaxWidth = Math.Min(1100, table.Colonne.Length * 155);
        foreach (var column in grid.Columns.Cast<DataGridTextColumn>())
        {
            column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            column.ElementStyle = style;
            var header = Ui.Text((string)column.Header, 12, true); header.TextAlignment = TextAlignment.Center; column.Header = header;
        }
        return grid;
    }
    private void ShowDetails()
    {
        var dialog = Ui.Dialog(this, "Tabelle e dettagli", outputs, 1050, 650); dialog.Closed += (_, _) => dialog.Content = null; dialog.ShowDialog();
    }
    internal void ExportResult(string filename)
    { if (Result is null) throw new InvalidOperationException(concrete is not null ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati incompleti prima di esportare." : "Premere Calcola prima di esportare."); Archivio.ScriviAtomico(filename, Encoding.UTF8.GetBytes(Result.ToJsonString(J.Options))); }
    internal void ExportReport(string filename, string title, HashSet<string> options)
    {
        if (Result is null) throw new InvalidOperationException(concrete is not null ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati incompleti prima di esportare." : "Premere Calcola prima di esportare.");
        if (horizontal is not null) { ReportOrizzontale.Write(filename, title, Result); return; }
        var images = new List<ImmagineReport> { new(plot.Title, plot.Png(), "grafico_capacita") };
        if (Micro)
        {
            try
            {
                for (int i = 0; i < Data.Array("stratigrafie").Count; i++)
                {
                    BuildReferencePlot(i);
                    images.Add(new($"Abachi Bustamante–Doix del sondaggio {i + 1}", reference.Png(), "grafico_nq"));
                }
            }
            finally { BuildReferencePlot(); }
        }
        else images.Add(new(reference.Title, reference.Png(), "grafico_nq"));
        for (int i = 0; i < Data.Array("stratigrafie").Count; i++)
        {
            var profile = new StratigraphyDrawing { Data = Data, Micro = Micro, SelectedIndex = i, ShowAll = false };
            images.Add(new($"Profilo del sondaggio {i + 1}", profile.Png(720, Micro ? 600 : 900), "stratigrafia", i));
        }
        bool old = stratigraphy.ShowAll; stratigraphy.ShowAll = true;
        try { images.Add(new("Profilo stratigrafico", stratigraphy.Png(), "grafico_profilo")); } finally { stratigraphy.ShowAll = old; }
        ReportWord.Esporta(filename, title, Module, Data, Result, options, images);
    }
    private void SavePlot()
    { var save = new SaveFileDialog { Filter = "Immagine PNG|*.png", FileName = "capacita.png" }; if (save.ShowDialog(Window.GetWindow(this)) == true) Archivio.ScriviAtomico(save.FileName, plot.Png()); }
}
