using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace X.Desktop;

internal static class StratigraphyTable
{
    private sealed class EmptyVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
    private static Binding Value(string key, bool readOnly = false) => new($"[{key}]")
    { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };

    internal static FrameworkElement Build(JsonGrid grid, IEnumerable<Field> fields, Action add, Action<JsonRow> delete, bool micro = false, bool gammaFallback = true)
    {
        grid.Columns.Clear(); grid.FontSize = 12; grid.RowHeight = 44; grid.ColumnHeaderHeight = 54;
        if (micro) { grid.MaxWidth = 650; grid.HorizontalAlignment = HorizontalAlignment.Left; }
        grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
        grid.VerticalGridLinesBrush = Ui.Brush("#DCE2E9");
        grid.RowBackground = Brushes.White; grid.AlternatingRowBackground = Ui.Brush("#F8FAFC");
        var headerStyle = new Style(typeof(DataGridColumnHeader), (Style)Application.Current.FindResource(typeof(DataGridColumnHeader)));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Ui.Navy));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.0));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
        grid.ColumnHeaderStyle = headerStyle;
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3)));
        cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        grid.CellStyle = cellStyle;

        var layer = new FrameworkElementFactory(typeof(StackPanel));
        layer.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        var line = new FrameworkElementFactory(typeof(StackPanel)); line.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        line.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        var name = new FrameworkElementFactory(typeof(TextBlock)); name.SetBinding(TextBlock.TextProperty, Value("__strato", true));
        name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); line.AppendChild(name);
        var swatch = new FrameworkElementFactory(typeof(Border)); swatch.SetValue(FrameworkElement.WidthProperty, 18.0); swatch.SetValue(FrameworkElement.HeightProperty, 12.0);
        swatch.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0)); swatch.SetValue(Border.BorderBrushProperty, Ui.Muted); swatch.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        swatch.SetBinding(Border.BackgroundProperty, Value("__color", true)); line.AppendChild(swatch); layer.AppendChild(line);
        if (fields.Any(f => f.Key == "laterale_attiva"))
        {
            var lateral = new FrameworkElementFactory(typeof(CheckBox)); lateral.SetValue(ContentControl.ContentProperty, "Laterale");
            lateral.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0)); lateral.SetBinding(ToggleButton.IsCheckedProperty, Value("laterale_attiva")); layer.AppendChild(lateral);
        }
        AddColumn("Strato", layer, 100);

        foreach (var field in fields.Where(f => f.Key is not ("__strato" or "laterale_attiva")))
        {
            FrameworkElementFactory control;
            if (field.Choices is not null)
            {
                control = new FrameworkElementFactory(typeof(ComboBox)); control.SetValue(ItemsControl.ItemsSourceProperty, field.Choices.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray());
                control.SetBinding(Selector.SelectedItemProperty, Value(field.Key));
                control.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center);
            }
            else
            {
                control = new FrameworkElementFactory(typeof(TextBox)); control.SetBinding(TextBox.TextProperty, Value(field.Key, field.ReadOnly));
                control.SetValue(TextBox.IsReadOnlyProperty, field.ReadOnly); control.SetValue(TextBox.TextAlignmentProperty, TextAlignment.Center);
                control.SetValue(Control.BackgroundProperty, field.ReadOnly ? Ui.Brush("#EAF2FA") : Brushes.White);
            }
            control.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            control.SetValue(FrameworkElement.MarginProperty, new Thickness(3)); control.SetValue(FrameworkElement.HeightProperty, 26.0);
            control.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, field.Label);
            if (field.Key == "__tau") control.SetValue(FrameworkElement.ToolTipProperty, "Aderenza dell’abaco (s), applicata al solo tratto aderente nel calcolo ΔRs = π ds Δl τ. Zero se la laterale è esclusa; — se i dati sono incompleti o fuori abaco.");
            string label = field.Key switch
            {
                "spessore" => micro ? "Spessore verticale\nS [m]" : "Spessore\nS [m]", "peso_specifico" => "Peso specifico\nγ [kN/m³]",
                "peso_specifico_saturo" => "Peso specifico saturo\nγsat [kN/m³]", "angolo_attrito" => "Angolo di attrito\nφ′ [°]",
                "coesione_efficace" => "Coesione efficace\nc′ [kPa]", "coesione_non_drenata" => "Coesione non drenata\nCu [kPa]",
                "nc" => "Fattore\nNc [−]", "__tau" => "Aderenza\nτ [kPa]", _ => field.Label.Replace("Coeff. ", "Coefficiente\n").Replace("Fattore ", "Fattore\n")
            };
            if (field.Key == "peso_specifico_saturo" && gammaFallback)
            {
                // The hint is not input: keep the stored value empty so it follows γ dynamically.
                var container = new FrameworkElementFactory(typeof(Grid));
                container.SetValue(FrameworkElement.ToolTipProperty, "Se vuoto, si usa automaticamente γ dello stesso strato (valore in grigio). Per tornare al valore automatico, cancellare γsat.");
                container.AppendChild(control);
                var hint = new FrameworkElementFactory(typeof(TextBlock));
                hint.SetBinding(TextBlock.TextProperty, Value("peso_specifico", true));
                var visibility = Value(field.Key, true); visibility.Converter = new EmptyVisibility();
                hint.SetBinding(UIElement.VisibilityProperty, visibility);
                hint.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
                hint.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
                hint.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
                hint.SetValue(UIElement.IsHitTestVisibleProperty, false);
                hint.SetValue(FrameworkElement.TagProperty, "gamma-sat-automatico");
                container.AppendChild(hint); control = container;
            }
            AddColumn(label, control, micro ? field.Key switch { "terreno" => 200, "spessore" => 110, "alpha" => 80, "__tau" => 90, _ => 125 } : field.Choices is not null ? 130 : 125);
        }
        var remove = new FrameworkElementFactory(typeof(Button)); remove.SetValue(ContentControl.ContentProperty, "[−]");
        remove.SetValue(FrameworkElement.ToolTipProperty, "Elimina questo strato");
        remove.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        remove.AddHandler(Button.ClickEvent, new RoutedEventHandler((sender, _) => { if (((Button)sender).DataContext is JsonRow row) delete(row); }));
        remove.SetValue(Control.PaddingProperty, new Thickness(4, 1, 4, 1));
        AddColumn("Elimina", remove, 60);
        void Resize() => grid.Height = grid.ColumnHeaderHeight + grid.Rows.Count * grid.RowHeight + SystemParameters.HorizontalScrollBarHeight + 4;
        grid.Rows.CollectionChanged += (_, _) => Resize(); Resize();
        var addButton = Ui.Button("Aggiungi strato", add); addButton.FontSize = 12; addButton.HorizontalAlignment = HorizontalAlignment.Left;
        return new ScrollViewer { Content = Ui.Stack(grid, addButton), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        void AddColumn(string title, FrameworkElementFactory factory, double width)
        {
            var header = Ui.Text(title, 12, true, Brushes.White); header.TextAlignment = TextAlignment.Center;
            grid.Columns.Add(new DataGridTemplateColumn { Header = header, Width = width, CellTemplate = new DataTemplate { VisualTree = factory } });
        }
    }
}
