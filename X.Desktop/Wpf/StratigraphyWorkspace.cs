using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.Stratigraphy;

namespace X.Desktop;

internal sealed partial class StratigraphyWorkspace : UserControl
{
    private const double MinimumLayoutWidth = 730;
    private readonly JsonObject data;
    private readonly ListBox layers = new() { MinHeight = 140, MaxHeight = 220, Background = Appearance.Paper, Foreground = Appearance.Ink };
    private readonly StackPanel fields = new();
    private readonly TextBlock status = Ui.Text("", 12);
    private readonly StratigraphyProfileView drawing = new();
    private bool refreshing;
    internal event Action? Modified;

    internal StratigraphyWorkspace(JsonObject data, bool allowImport = true)
    {
        this.data = data;
        var general = new StackPanel();
        if (allowImport)
        {
            var importButton = Ui.Button("Importa relazione PDF con AI…", Import);
            importButton.IsEnabled = false;
            importButton.ToolTip = StratigraphyImport.Disabled;
            ToolTipService.SetShowOnDisabled(importButton, true);
            general.Children.Add(importButton);
        }
        // The fields of the sheet are defined once (F5.6b) for this view and for StratigraphyEditorViewModel.
        foreach (var field in StratigraphyFields.General()) Add(general, data, field);
        general.Children.Add(Ui.Text("Origine delle profondità: piano campagna (0 m). Un solo livello di falda; nessuna modellazione artesiana o di falde sospese.", 12));
        var actions = Ui.Bar(
            Ui.Button("+ Strato", () => Edit(() => StratigraphyDocument.Add(data))),
            Ui.Button("Duplica", () => Edit(() => StratigraphyDocument.Duplicate(data, Selected))),
            Ui.Button("Elimina", () => Edit(() => StratigraphyDocument.Remove(data, Selected))),
            Ui.Button("↑", () => Edit(() => StratigraphyDocument.Move(data, Selected, Selected - 1), Selected - 1)),
            Ui.Button("↓", () => Edit(() => StratigraphyDocument.Move(data, Selected, Selected + 1), Selected + 1)));
        var left = Ui.Stack(Ui.Text("Stratigrafia del terreno", 23, true), general,
            Ui.Text("Strati · dall’alto verso il basso", 16, true), actions, layers, fields);
        var layout = new Grid { Margin = new Thickness(16) };
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(3, GridUnitType.Star), MinWidth = 430 });
        layout.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star), MinWidth = 300 });
        layout.Children.Add(new ScrollViewer { Content = left, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var right = Ui.Dock(drawing, Ui.Text("Profilo verticale · profondità [m]", 16, true),
            new ScrollViewer { Content = status, MaxHeight = 170, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        right.Margin = new Thickness(20, 0, 0, 0); Grid.SetColumn(right, 1); layout.Children.Add(right);
        var viewport = new ScrollViewer { Content = layout, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        viewport.SizeChanged += (_, e) => layout.Width = Math.Max(MinimumLayoutWidth, e.NewSize.Width - layout.Margin.Left - layout.Margin.Right);
        Content = viewport;
        layers.SelectionChanged += (_, _) => { if (!refreshing) { ShowLayer(); Refresh(); } };
        drawing.Selected += index => layers.SelectedIndex = index;
        Reload(0);
    }
    private int Selected => Math.Max(0, layers.SelectedIndex);
    private void Field(Panel host, JsonObject owner, string key, string label)
    {
        var box = new TextBox { Text = owner[key]?.ToString() ?? "", Margin = new Thickness(0, 2, 0, 8), Padding = new Thickness(5) };
        if (key is "source" or "notes" or "description") { box.AcceptsReturn = true; box.TextWrapping = TextWrapping.Wrap; box.MaxHeight = 110; box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
        System.Windows.Automation.AutomationProperties.SetName(box, label);
        box.TextChanged += (_, _) =>
        {
            owner[key] = box.Text;
            if (key == "name" && owner != data && layers.SelectedItem is ListBoxItem item) item.Content = box.Text;
            Changed();
        };
        host.Children.Add(Ui.Text(label, 12)); host.Children.Add(box);
    }
    private void Add(Panel host, JsonObject owner, FieldSpec field)
    {
        if (field.Choices is not null) Choice(host, owner, field.Key, field.Label, field.Choices);
        else Field(host, owner, field.Key, field.Label);
    }
    private static FieldSpec Spec(FieldSpec[] fields, string key) => fields.Single(f => f.Key == key);
    private void Choice(Panel host, JsonObject owner, string key, string label, string[] choices)
    {
        var combo = new ComboBox { ItemsSource = choices, SelectedItem = owner[key]?.ToString() ?? StratigraphyPresentation.DefaultChoice(key), Margin = new Thickness(0, 2, 0, 8) };
        System.Windows.Automation.AutomationProperties.SetName(combo, label);
        combo.SelectionChanged += (_, _) => { owner[key] = combo.SelectedItem?.ToString(); Changed(); };
        host.Children.Add(Ui.Text(label, 12)); host.Children.Add(combo);
    }
    private void Reload(int selected)
    {
        refreshing = true; layers.Items.Clear();
        foreach (var layer in StratigraphyDocument.Layers(data)) layers.Items.Add(new ListBoxItem { Content = layer!["name"]!.ToString(), Foreground = Appearance.Ink });
        layers.SelectedIndex = Math.Clamp(selected, 0, layers.Items.Count - 1);
        refreshing = false; ShowLayer(); Refresh();
    }
    private void ShowLayer()
    {
        fields.Children.Clear(); if (layers.SelectedIndex < 0) return;
        var layer = StratigraphyDocument.Layers(data)[Selected]!.AsObject();
        var identity = StratigraphyFields.Layer();
        fields.Children.Add(Ui.Text("Dati dello strato selezionato", 16, true));
        Add(fields, layer, Spec(identity, "name"));
        Add(fields, layer, Spec(identity, "soil_type"));
        Add(fields, layer, Spec(identity, "behavior"));
        fields.Children.Add(Ui.Text("Per terreni misti scegli la componente prevalente e specifica la miscela nella descrizione. Il retino è una convenzione grafica, non assegna parametri.", 12));
        Add(fields, layer, Spec(identity, "description"));
        Add(fields, layer, Spec(identity, "color"));
        bool infinite = StratigraphyDocument.Infinite(layer);
        var check = new CheckBox { Content = StratigraphyFields.InfiniteLabel, IsChecked = infinite,
            IsEnabled = Selected == layers.Items.Count - 1, Margin = new Thickness(0, 4, 0, 8) };
        check.Click += (_, _) => { StratigraphyPresentation.SetInfinite(layer, check.IsChecked == true); ShowLayer(); Changed(); };
        fields.Children.Add(check);
        if (!infinite) Add(fields, layer, Spec(StratigraphyFields.Thickness(), "thickness"));
        var useMinimum = new CheckBox { Content = StratigraphyFields.MinimaLabel, IsChecked = layer["minimum"] is JsonObject, Margin = new Thickness(0, 4, 0, 8) };
        useMinimum.Click += (_, _) =>
        {
            if (useMinimum.IsChecked == true) layer["minimum"] = StratigraphyDocument.NewMinimum();
            else if (MessageBox.Show(Window.GetWindow(this), StratigraphyPresentation.MinimaRemoval, StratigraphyPresentation.MinimaCaption, MessageBoxButton.YesNo) == MessageBoxResult.Yes) layer["minimum"] = null;
            ShowLayer(); Changed();
        };
        fields.Children.Add(useMinimum);
        var parameterGrid = new Grid();
        parameterGrid.ColumnDefinitions.Add(new()); parameterGrid.ColumnDefinitions.Add(new());
        var means = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        var minima = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        parameterGrid.Children.Add(means); Grid.SetColumn(minima, 1); parameterGrid.Children.Add(minima);
        means.Children.Add(Ui.Text("Valori medi · insieme principale", 14, true));
        minima.Children.Add(Ui.Text("Valori minimi · opzionali", 14, true));
        var meanFields = StratigraphyFields.Means();
        var minimumFields = StratigraphyFields.Minima();
        foreach (var (key, _) in StratigraphyFields.Parameters())
        {
            Add(means, layer, Spec(meanFields, key));
            if (layer["minimum"] is JsonObject minimum) Add(minima, minimum, Spec(minimumFields, key));
        }
        Add(means, layer, Spec(meanFields, "qualification"));
        Add(means, layer, Spec(meanFields, "source"));
        if (layer["minimum"] is JsonObject minimumData)
            foreach (string key in new[] { "qualification", "source", "notes" }) Add(minima, minimumData, Spec(minimumFields, key));
        else minima.Children.Add(Ui.Text(StratigraphyPresentation.MinimaNote, 12));
        fields.Children.Add(parameterGrid);
        fields.Children.Add(Ui.Text("Vuoto = dato non disponibile. Nessuna correlazione automatica o applicazione di coefficienti parziali.", 12));
        Add(fields, layer, Spec(StratigraphyFields.Notes(), "notes"));
    }
    private void Import()
    {
        var dialog = new StratigraphyImportWindow { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is not JsonObject result) return;
        StratigraphyImport.Replace(data, result);
        var replacement = new StratigraphyWorkspace(data);
        replacement.Modified += () => Modified?.Invoke();
        Content = replacement;
        Modified?.Invoke();
    }
    private void Edit(Action action, int? selected = null)
    {
        try { action(); Reload(selected ?? Selected); Modified?.Invoke(); }
        catch (ArgumentException ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, StratigraphyPresentation.Caption); }
    }
    private void Changed() { Refresh(); Modified?.Invoke(); }
    private void Refresh()
    {
        foreach (ListBoxItem item in layers.Items) item.Background = item.IsSelected ? Appearance.Selection("#DCEBFA") : Appearance.Paper;
        var current = StratigraphyPresentation.Status(data);
        status.Text = current.Text; drawing.Data = current.Drawn ? data : null;
        drawing.Selection = Selected; drawing.InvalidateVisual();
    }
    internal void Commit() => StratigraphyDocument.Validate(data);
}

internal sealed class StratigraphyProfileView : DrawingView
{
    // Display dimensions only: pixels and proportions of the visible panel, not soil parameters.
    private const double ProfileWidthRatio = .35, InfiniteTailRatio = .25;
    private const double TopPixels = 30, LabelGapPixels = 8, TextInsetPixels = 3, EdgePixels = 4,
        LabelPaddingPixels = 12, WaterFooterPixels = 44, ExtentFooterPixels = 22;
    internal JsonObject? Data;
    internal int Selection;
    internal event Action<int>? Selected;
    private readonly List<(Rect Bounds, int Index)> hits = [];
    internal StratigraphyProfileView()
    {
        MinHeight = 320;
        MouseLeftButtonDown += (_, e) =>
        {
            var point = e.GetPosition(this);
            foreach (var hit in hits) if (hit.Bounds.Contains(point)) { Selected?.Invoke(hit.Index); break; }
        };
    }
    protected override void Render(DrawingContext dc, Size size)
    {
        hits.Clear(); if (Data is null || size.Width < 150 || size.Height < 150) return;
        var rows = StratigraphyDocument.Layers(Data).OfType<JsonObject>().ToArray();
        double finite = rows.Where(r => !StratigraphyDocument.Infinite(r)).Sum(r => StratigraphyDocument.Number(r["thickness"], "Spessore")!.Value);
        bool infinite = StratigraphyDocument.Infinite(rows[^1]);
        double shown = Math.Min(double.MaxValue, finite + (infinite ? Math.Max(2, finite * InfiniteTailRatio) : 0));
        double water = StratigraphyDocument.Number(Data["water_depth"], "Falda") ?? 0;
        if (infinite && Data["water_state"]!.ToString() == "Assegnata") shown = Math.Max(shown, water + 1);
        double height = size.Height - 100, top = 0, left = 58, width = Math.Max(60, size.Width * ProfileWidthRatio);
        var ink = Appearance.Foreground(Ui.Muted);
        Text(dc, "Piano campagna", left, 5, 12, ink);
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i]; bool endless = StratigraphyDocument.Infinite(row);
            double bottom = endless ? shown : top + StratigraphyDocument.Number(row["thickness"], "Spessore")!.Value;
            var bounds = new Rect(left, TopPixels + height * (top / shown), width, height * ((bottom - top) / shown));
            var fill = (SolidColorBrush)Ui.Brush(row["color"]!.ToString());
            dc.DrawRectangle(fill, null, bounds);
            dc.DrawRectangle(SoilHatchBrush.Create(StratigraphyDocument.SoilType(row), fill.Color), null, bounds);
            dc.DrawRectangle(null, new Pen(i == Selection ? Brushes.DodgerBlue : Brushes.DimGray, i == Selection ? 3 : 1), bounds);
            Text(dc, top.ToString("G4", CultureInfo.GetCultureInfo("it-IT")), 2, bounds.Top, 11, ink);
            Text(dc, row["name"]!.ToString() + "\n" + StratigraphyDocument.SoilType(row) + (endless ? "\n↓ indefinito" : ""), bounds.Right + LabelGapPixels, bounds.Top + TextInsetPixels, 12, ink, width: Math.Max(30, size.Width - bounds.Right - LabelPaddingPixels));
            hits.Add((bounds, i)); top = bottom;
        }
        Text(dc, infinite ? "↓" : top.ToString("G4", CultureInfo.GetCultureInfo("it-IT")), 2, TopPixels + height * (top / shown), 11, ink);
        if (Data["water_state"]!.ToString() == "Assegnata")
        {
            if (water <= shown)
            {
                double y = TopPixels + height * (water / shown);
                dc.DrawLine(new Pen(Brushes.DodgerBlue, 2) { DashStyle = DashStyles.Dash }, new(left - LabelGapPixels, y), new(left + width + LabelPaddingPixels, y));
                Text(dc, $"Falda: {water:G4} m", left, size.Height - WaterFooterPixels, 12, Brushes.DodgerBlue);
            }
            else Text(dc, $"Falda a {water:G4} m · sotto il profilo", left, size.Height - WaterFooterPixels, 12, Brushes.DodgerBlue);
        }
        if (infinite) Text(dc, $"Vista fino a {shown:G4} m; ultimo strato indefinito.", 2, size.Height - ExtentFooterPixels, 11, ink, width: size.Width - EdgePixels);
    }
}

internal sealed class SoilProfileIcon : DrawingView
{
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Ui.Brush("#D5BE92"), null, new Rect(10, 10, 60, 20));
        dc.DrawRectangle(Ui.Brush("#B29C7C"), null, new Rect(10, 30, 60, 20));
        dc.DrawRectangle(Ui.Brush("#A7B7C7"), null, new Rect(10, 50, 60, 20));
        dc.DrawRectangle(null, new Pen(Appearance.Ink, 2), new Rect(10, 10, 60, 60));
        dc.DrawLine(new Pen(Brushes.DodgerBlue, 2) { DashStyle = DashStyles.Dash }, new Point(4, 40), new Point(76, 40));
    }
}
