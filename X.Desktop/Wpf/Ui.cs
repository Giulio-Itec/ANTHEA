using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X.Core;

namespace X.Desktop;

internal static class Ui
{
    internal static readonly Brush Navy = Brush("#0B2A4A"), Blue = Brush("#0B5CAD"), Bg = Brush("#F3F5F8"), Muted = Brush("#64748B");
    internal static Brush Brush(string color) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!; b.Freeze(); return b; }
    internal static Button Button(string title, Action action, bool primary = false)
    {
        var b = new Button { Content = title, Background = primary ? Navy : Brushes.White, Foreground = primary ? Brushes.White : Navy };
        b.Click += (_, _) => action(); return b;
    }
    internal static TextBlock Text(string text, double size = 13, bool bold = false, Brush? color = null) => new()
    {
        Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = color ?? Navy, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };
    internal static StackPanel Stack(params UIElement[] items) { var s = new StackPanel(); foreach (var i in items) s.Children.Add(i); return s; }
    internal static WrapPanel Bar(params UIElement[] items) { var p = new WrapPanel(); foreach (var i in items) p.Children.Add(i); return p; }
    internal static DockPanel Dock(UIElement body, UIElement? top = null, UIElement? bottom = null)
    {
        var p = new DockPanel();
        if (top is not null) { DockPanel.SetDock(top, System.Windows.Controls.Dock.Top); p.Children.Add(top); }
        if (bottom is not null) { DockPanel.SetDock(bottom, System.Windows.Controls.Dock.Bottom); p.Children.Add(bottom); }
        p.Children.Add(body); return p;
    }
    internal static Border Paper(UIElement body, double padding = 12) => new()
    { Child = body, Background = Brushes.White, BorderBrush = Brush("#D8E0EB"), BorderThickness = new Thickness(1), Padding = new Thickness(padding) };
    internal static TabItem Tab(TabControl tabs, string title, UIElement body)
    { var t = new TabItem { Header = title, Content = body, Background = Brushes.White }; tabs.Items.Add(t); return t; }
    internal static ComboBox Choice(IEnumerable<string> choices, string? value = null)
    {
        var c = new ComboBox { ItemsSource = choices.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray(), Margin = new Thickness(2) }; c.SelectedItem = value;
        c.PreviewMouseWheel += (_, e) => { if (c.IsDropDownOpen) return; ChainedScrollViewer.ForwardWheel(c, e); e.Handled = true; };
        return c;
    }
    internal static Style NumericTextStyle(bool editing = false)
    {
        var style = new Style(editing ? typeof(TextBox) : typeof(TextBlock));
        style.Setters.Add(new Setter(editing ? TextBox.TextAlignmentProperty : TextBlock.TextAlignmentProperty,
            new Binding("Text") { RelativeSource = new RelativeSource(RelativeSourceMode.Self), Converter = new NumericAlignmentConverter() }));
        return style;
    }
    internal static Window Dialog(DependencyObject owner, string title, UIElement body, double width = 500, double height = 400)
    {
        var dialog = new Window { Owner = Window.GetWindow(owner), Title = title, Content = body, Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        DisplayAdaptation.Attach(dialog);
        return dialog;
    }
    internal static string? Ask(Window owner, string title, string initial)
    {
        var input = new TextBox { Text = initial, Margin = new Thickness(15) };
        var dialog = Dialog(owner, title, new Grid(), 460, 170); dialog.ResizeMode = ResizeMode.NoResize;
        var ok = Button("Conferma", () => dialog.DialogResult = true, true); ok.IsDefault = true;
        var cancel = Button("Annulla", () => dialog.DialogResult = false); cancel.IsCancel = true;
        dialog.Content = Dock(input, bottom: Bar(ok, cancel)); dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return dialog.ShowDialog() == true ? input.Text : null;
    }
    internal static Image Logo(double size) => new() { Source = Asset("logo.png"), Width = size, Height = size, Stretch = Stretch.Uniform };
    internal static BitmapImage Asset(string name) => new(new Uri($"pack://application:,,,/ANTHEA;component/Assets/{name}"));
    internal static FrameworkElement ModuleIcon(string module)
    {
        if (module == "str_palo") return new SectionIcon { Width = 80, Height = 80 };
        var (x, y) = module switch { "geo_palo_orizzontale" => (1, 0), "geo_micropalo_verticale" => (2, 0), "geo_micropalo_orizzontale" => (0, 1), "str_micropalo" => (2, 1), _ => (0, 0) };
        return new Image { Source = new CroppedBitmap(Asset("moduli.png"), new Int32Rect(x * 160, y * 160, 160, 160)), Width = 80, Height = 80 };
    }
    internal static byte[] Snapshot(FrameworkElement element)
    {
        if (element is Window { Content: FrameworkElement content }) element = content;
        element.UpdateLayout(); var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
    internal static DataGrid Table(string[] headers, IEnumerable<string[]> rows)
    {
        var grid = new DataGrid { IsReadOnly = true };
        for (int i = 0; i < headers.Length; i++) grid.Columns.Add(new DataGridTextColumn { Header = headers[i], Binding = new Binding($"[{i}]"), ElementStyle = NumericTextStyle(), MinWidth = 65, Width = DataGridLength.SizeToCells });
        grid.ItemsSource = rows.ToList(); return grid;
    }
    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T t) yield return t; foreach (var d in Descendants<T>(child)) yield return d; }
    }
}

internal sealed class SectionIcon : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.WhiteSmoke, new Pen(Ui.Navy, 3), new Rect(13, 13, 54, 54));
        foreach (int x in new[] { 22, 54 }) foreach (int y in new[] { 22, 38, 54 }) dc.DrawRectangle(Ui.Brush("#C7953E"), null, new Rect(x, y, 6, 6));
    }
}

internal sealed record Field(string Key, string Label, string Unit = "", string[]? Choices = null, bool Bool = false, bool ReadOnly = false, string Symbol = "");

internal class ChainedScrollViewer : ScrollViewer
{
    internal static void ForwardWheel(DependencyObject source, MouseWheelEventArgs e)
    {
        for (var parent = VisualTreeHelper.GetParent(source); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is ScrollViewer scroll)
            {
                scroll.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = scroll });
                return;
            }
    }
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (ScrollableHeight <= 0 || (e.Delta > 0 ? VerticalOffset <= 0 : VerticalOffset >= ScrollableHeight))
        { ForwardWheel(this, e); e.Handled = true; return; }
        base.OnMouseWheel(e);
    }
}

internal sealed class NumericAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        J.Number(JsonValue.Create(value?.ToString())) is double ? TextAlignment.Center : TextAlignment.Left;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

internal sealed class InputForm : ChainedScrollViewer
{
    internal static readonly DependencyProperty CommitOnFocusLossProperty = DependencyProperty.RegisterAttached("CommitOnFocusLoss", typeof(bool), typeof(InputForm), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    private readonly Grid table = new();
    private readonly JsonObject values;
    private readonly Action<string> changed;
    private bool displayOnly;
    private readonly HashSet<string> drafts = [];
    internal readonly Dictionary<string, FrameworkElement> Editors = new();
    private readonly Dictionary<string, List<FrameworkElement>> rows = new();
    internal InputForm(JsonObject values, IEnumerable<Field> fields, Action<string> changed, bool compact = false, bool wideChoices = false, bool symbolColumns = false)
    {
        this.values = values; this.changed = changed;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto; HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; Content = table;
        table.ColumnDefinitions.Add(new ColumnDefinition());
        if (symbolColumns) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 82 : 110) }); table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 42 : 55) });
        foreach (var original in fields)
        {
            var f = symbolColumns ? WithSymbol(original) : original;
            int row = table.RowDefinitions.Count; table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = Ui.Text(f.Label, compact ? 12 : 13); label.Margin = new Thickness(2, 3, 6, 3); label.ToolTip = f.Label;
            FrameworkElement editor;
            if (f.Bool)
            {
                var c = new CheckBox { Content = symbolColumns ? null : f.Label, IsChecked = values.B(f.Key), VerticalContentAlignment = VerticalAlignment.Center };
                c.Checked += (_, _) => Store(f.Key, true); c.Unchecked += (_, _) => Store(f.Key, false); editor = c;
            }
            else if (f.Choices is not null)
            {
                var choices = f.Choices.ToList(); string v = values.S(f.Key); if (v != "" && !choices.Contains(v)) choices.Add(v);
                var c = Ui.Choice(choices, v); c.SelectionChanged += (_, _) => Store(f.Key, c.SelectedItem?.ToString() ?? ""); editor = c;
            }
            else
            {
                var t = new TextBox { Text = values.S(f.Key), IsReadOnly = f.ReadOnly, TextAlignment = symbolColumns ? TextAlignment.Center : TextAlignment.Right, Background = f.ReadOnly ? Ui.Brush("#EAF2FA") : Ui.Brush("#F8FAFC") };
                if (!f.ReadOnly)
                {
                    t.TextChanged += (_, _) => { if (displayOnly) return; if ((bool)GetValue(CommitOnFocusLossProperty) && t.IsKeyboardFocusWithin) drafts.Add(f.Key); else Store(f.Key, t.Text); };
                    t.LostKeyboardFocus += (_, _) => { if (drafts.Remove(f.Key)) Store(f.Key, t.Text); };
                }
                editor = t;
            }
            editor.Margin = new Thickness(2, 3, 2, 3); editor.MinHeight = compact ? 22 : 27; editor.ToolTip = f.Label + (f.Unit != "" ? " [" + f.Unit + "]" : "");
            editor.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, f.Label);
            var unit = Ui.Text(f.Unit, 11, color: Ui.Muted); unit.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetRow(label, row); Grid.SetRow(editor, row); Grid.SetRow(unit, row); Grid.SetColumn(editor, symbolColumns ? 2 : 1); Grid.SetColumn(unit, symbolColumns ? 3 : 2);
            var elements = new List<FrameworkElement> { label, editor, unit };
            if (symbolColumns)
            {
                var symbol = Ui.Text(f.Symbol, 12); symbol.TextAlignment = TextAlignment.Center; Grid.SetRow(symbol, row); Grid.SetColumn(symbol, 1); elements.Add(symbol);
                if (f.Choices is not null && f.Unit == "") { Grid.SetColumnSpan(editor, 2); elements.Remove(unit); }
            }
            else if (f.Bool || f.Key == "metodo") { Grid.SetColumn(editor, 0); Grid.SetColumnSpan(editor, 3); elements = [editor]; }
            else if (f.Choices is not null && wideChoices)
            {
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumnSpan(label, 3); Grid.SetRow(editor, row + 1); Grid.SetColumn(editor, 0); Grid.SetColumnSpan(editor, 3); elements = [label, editor];
            }
            else if (f.Choices is not null && f.Unit == "" && !compact) { Grid.SetColumnSpan(editor, 2); elements = [label, editor]; }
            foreach (var element in elements) table.Children.Add(element);
            Editors[f.Key] = editor; rows[f.Key] = elements;
        }
    }
    private void Store(string key, object value) { if (displayOnly) return; values[key] = J.Node(value); changed(key); }
    internal Size UnwrappedSize()
    {
        // Measure the form's labels on one line, including the fixed editor/unit columns.
        table.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return table.DesiredSize;
    }
    private static Field WithSymbol(Field f)
    {
        if (f.Symbol != "") return f;
        var (name, symbol) = f.Key switch
        {
            "diametro" => ("Diametro palo", "D"), "lunghezza" => ("Lunghezza palo", "L"),
            "peso_specifico_palo" => ("Peso specifico CLS / palo", "γp"),
            "azione_compressione" => ("Azione assiale — Compressione", "NEd,c"), "azione_trazione" => ("Azione assiale — Trazione", "NEd,t"),
            "profondita_falda" => ("Profondità falda", "zf"), "verticali_indagate" => ("Verticali indagate", "n"),
            "__xi3" => ("Correlazione", "ξ3"), "__xi4" => ("Correlazione", "ξ4"),
            "sicurezza_laterale_compressione" => ("Sicurezza laterale — Compressione", "γs"),
            "sicurezza_laterale_trazione" => ("Sicurezza laterale — Trazione", "γt"),
            "sicurezza_base" => ("Sicurezza di base", "γb"), "peso_palo_sfavorevole" => ("Peso proprio — Sfavorevole", "γG"),
            "peso_palo_favorevole" => ("Peso proprio — Favorevole", "γG"),
            "numero_pali_x" => ("Pali in X", "nx"), "numero_pali_y" => ("Pali in Y", "ny"),
            "interasse_x" => ("Interasse X", "sx"), "interasse_y" => ("Interasse Y", "sy"),
            "eta_compressione" => ("Efficienza compressione", "ηg,c"), "eta_trazione" => ("Efficienza trazione", "ηg,t"),
            "pressione_iniezione" => ("Pressione di iniezione", "pi = pl"), "inclinazione" => ("Inclinazione dalla verticale", "θ"),
            "inizio_aderenza" => ("Inizio aderenza lungo asse", "sb"), "percentuale_punta" => ("Punta rispetto alla laterale", ""),
            _ => (f.Label, f.Symbol)
        };
        return f with { Label = name, Symbol = symbol };
    }
    internal void Commit()
    {
        foreach (var key in drafts.ToArray()) { drafts.Remove(key); if (Editors[key] is TextBox { IsReadOnly: false } text) Store(key, text.Text); }
    }
    internal string Get(string key) => Editors[key] switch { TextBox t => t.Text, ComboBox c => c.SelectedItem?.ToString() ?? "", _ => "" };
    internal void Set(string key, string value, bool display = false)
    {
        if (!Editors.TryGetValue(key, out var editor)) return;
        drafts.Remove(key);
        displayOnly = display;
        try { if (editor is TextBox t) { t.Text = value; if (!display && values.S(key) != value) Store(key, value); } else if (editor is ComboBox c) c.SelectedItem = value; }
        finally { displayOnly = false; }
    }
    internal void Enable(string key, bool enabled, bool dim = false)
    {
        if (rows.TryGetValue(key, out var row)) foreach (var e in row)
        { e.IsEnabled = enabled; if (dim) e.Opacity = enabled ? 1 : .4; }
    }
    internal void ShowField(string key, bool show) { if (rows.TryGetValue(key, out var row)) foreach (var e in row) e.Visibility = show ? Visibility.Visible : Visibility.Collapsed; }
    internal void GroupFields(string title, string[] keys, bool expanded = false)
    {
        // Keep the same editors/bindings; only their visual containers change.
        if (Content is not StackPanel) { Content = null; Content = Ui.Stack(table); }
        var group = new Grid();
        foreach (var column in table.ColumnDefinitions) group.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width });
        foreach (string key in keys)
        {
            if (!rows.TryGetValue(key, out var elements)) continue;
            int start = elements.Min(Grid.GetRow), end = elements.Max(Grid.GetRow), target = group.RowDefinitions.Count;
            for (int i = start; i <= end; i++) { group.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); table.RowDefinitions[i].Height = new GridLength(0); }
            foreach (var element in elements) { table.Children.Remove(element); Grid.SetRow(element, target + Grid.GetRow(element) - start); group.Children.Add(element); }
        }
        ((StackPanel)Content).Children.Add(new Expander { Header = Ui.Text(title, 13, true), Content = group, IsExpanded = expanded, Margin = new Thickness(0, 8, 0, 8) });
    }
}

// A small binding adapter keeps the existing JSON file format and unparsed user input.
internal sealed class JsonRow : INotifyPropertyChanged
{
    private int notificationDepth;
    private bool notificationPending;
    internal static IDisposable DeferNotifications(IEnumerable<JsonRow> rows)
    {
        var snapshot = rows.Distinct().ToArray();
        foreach (var row in snapshot) row.notificationDepth++;
        return new UpdateScope(() =>
        {
            foreach (var row in snapshot)
                if (--row.notificationDepth == 0 && row.notificationPending)
                { row.notificationPending = false; row.PropertyChanged?.Invoke(row, new("Item[]")); }
        });
    }
    private void Notify()
    {
        if (notificationDepth > 0) notificationPending = true;
        else PropertyChanged?.Invoke(this, new("Item[]"));
    }
    internal JsonObject Values { get; }
    private readonly Action<string>? changed;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal JsonRow(JsonObject values, Action<string>? changed = null) { Values = values; this.changed = changed; }
    public object? this[string key]
    {
        get => Values[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : Values.S(key);
        set { Values[key] = J.Node(value); Notify(); changed?.Invoke(key); }
    }
    internal void Output(string key, object? value) { var node = J.Node(value); if (JsonNode.DeepEquals(Values[key], node)) return; Values[key] = node; Notify(); }
}

internal sealed class UpdateScope(Action end) : IDisposable
{
    private Action? end = end;
    public void Dispose() => Interlocked.Exchange(ref end, null)?.Invoke();
}

internal sealed class JsonRows : ObservableCollection<JsonRow>
{
    private int updateDepth;
    private bool pending;
    internal IDisposable DeferRefresh()
    {
        updateDepth++;
        return new UpdateScope(() =>
        {
            if (--updateDepth != 0 || !pending) return;
            pending = false;
            base.OnPropertyChanged(new("Count"));
            base.OnPropertyChanged(new("Item[]"));
            base.OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
        });
    }
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    { if (updateDepth > 0) pending = true; else base.OnCollectionChanged(e); }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    { if (updateDepth == 0) base.OnPropertyChanged(e); }
}

internal sealed class JsonGrid : DataGrid
{
    internal JsonRows Rows { get; }
    internal JsonGrid(IEnumerable<Field> fields, bool stretch = false, JsonRows? rows = null)
    {
        Rows = rows ?? [];
        Style = (Style)Application.Current.FindResource(typeof(DataGrid));
        ItemsSource = Rows; CanUserSortColumns = false;
        Loaded += (_, _) =>
        {
            if (!(bool)GetValue(InputForm.CommitOnFocusLossProperty)) return;
            foreach (var column in Columns.OfType<DataGridTextColumn>())
                if (column.Binding is Binding binding && !column.IsReadOnly && binding.UpdateSourceTrigger != UpdateSourceTrigger.LostFocus)
                    column.Binding = new Binding(binding.Path.Path) { Mode = binding.Mode, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus };
        };
        foreach (var f in fields)
        {
            var binding = new Binding($"[{f.Key}]") { Mode = f.ReadOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            DataGridColumn column;
            if (f.Bool) column = new DataGridCheckBoxColumn { Binding = binding };
            else if (f.Choices is not null) column = new DataGridComboBoxColumn { ItemsSource = f.Choices.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray(), SelectedItemBinding = binding };
            else column = new DataGridTextColumn { Binding = binding, ElementStyle = Ui.NumericTextStyle(), EditingElementStyle = Ui.NumericTextStyle(true) };
            column.Header = f.Label; column.SortMemberPath = f.Key; column.IsReadOnly = f.ReadOnly; column.MinWidth = f.Bool ? 60 : 65;
            column.Width = stretch ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(f.Choices is not null ? 140 : 112);
            Columns.Add(column);
        }
    }
    internal void Commit() { CommitEdit(DataGridEditingUnit.Cell, true); CommitEdit(DataGridEditingUnit.Row, true); }
}
