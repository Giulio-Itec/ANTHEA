using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.BridgeDesign;

namespace X.Desktop.Views.BridgeDesign;

/// <summary>
/// A table of a panel of Bridge Design (<see cref="BridgeDesignTable"/>, F5.12), as the historic view builds it (Table of BridgeDesignWorkspace on
/// Ui.Table): read-only, one text column for each header (at least 65 wide), the first column twice as wide as the others (the table of the
/// dimensions with its own widths), wrapping cells with a margin, the rows as tall as their text, between 90 and the height of the historic table.
/// The selection is that of the table: the row selected by the sheet is shown, the row chosen by the user goes to the table (the ranking of the
/// search selects its solution). The rows are the texts already formatted by Application. Presentation only.
/// </summary>
public sealed class BridgeDesignGrid : DataGrid
{
    public static readonly DependencyProperty TableProperty = DependencyProperty.Register(nameof(Table), typeof(BridgeDesignTable), typeof(BridgeDesignGrid),
        new PropertyMetadata(null, (owner, e) => ((BridgeDesignGrid)owner).Show((BridgeDesignTable?)e.OldValue, (BridgeDesignTable?)e.NewValue)));

    private bool showing;

    public BridgeDesignGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        IsReadOnly = true;
        AutoGenerateColumns = false;
        MinHeight = 90;
        RowHeight = double.NaN;
        SelectionChanged += (_, _) => { if (!showing && Table is { } table && table.SelectedIndex != SelectedIndex) table.SelectedIndex = SelectedIndex; };
    }

    /// <summary>The table shown.</summary>
    public BridgeDesignTable? Table { get => (BridgeDesignTable?)GetValue(TableProperty); set => SetValue(TableProperty, value); }

    /// <summary>The height of the historic table with these columns (300 unless said).</summary>
    internal static double MaximumHeight(IReadOnlyList<string> columns) =>
        ReferenceEquals(columns, BridgeDesignPresentation.DetailColumns) ? 600 : ReferenceEquals(columns, BridgeDesignPresentation.SupportColumns) ? 500
        : ReferenceEquals(columns, BridgeDesignPresentation.TechnicalColumns) ? 480 : ReferenceEquals(columns, BridgeDesignPresentation.SupportScheduleColumns) ? 400
        : ReferenceEquals(columns, BridgeDesignOptimizationPresentation.RankingColumns) ? 350 : ReferenceEquals(columns, BridgeDesignOptimizationPresentation.ChangeColumns) ? 235
        : 300;

    /// <summary>The relative width of a column (ShowTechnical of the historic view for the dimensions; otherwise 2 for the first, 1 for the others).</summary>
    internal static double RelativeWidth(IReadOnlyList<string> columns, int index) =>
        ReferenceEquals(columns, BridgeDesignPresentation.TechnicalColumns) ? index switch { 0 => 1.2, 1 => .7, 2 => .8, 3 => .5, 4 => .8, _ => 3 } : index == 0 ? 2 : 1;

    private void Show(BridgeDesignTable? previous, BridgeDesignTable? table)
    {
        if (previous is not null) previous.PropertyChanged -= OnTableChanged;
        showing = true;
        try
        {
            Columns.Clear();
            ItemsSource = null;
            if (table is null) return;
            var columns = table.Columns;
            for (int i = 0; i < columns.Count; i++)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
                style.Setters.Add(new Setter(MarginProperty, new Thickness(5, 3, 5, 3)));
                Columns.Add(new DataGridTextColumn
                {
                    Header = columns[i], Binding = new Binding($"[{i}]") { Mode = BindingMode.OneWay }, ElementStyle = style, MinWidth = 65,
                    Width = new DataGridLength(RelativeWidth(columns, i), DataGridLengthUnitType.Star)
                });
            }
            MaxHeight = MaximumHeight(columns);
            // The ranking keeps the order of the objective (CanUserSortColumns of the historic ranking).
            CanUserSortColumns = !ReferenceEquals(columns, BridgeDesignOptimizationPresentation.RankingColumns);
            ItemsSource = table.Rows;
            SelectedIndex = table.SelectedIndex < table.Rows.Length ? table.SelectedIndex : -1;
            table.PropertyChanged += OnTableChanged;
        }
        finally { showing = false; }
    }

    private void OnTableChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BridgeDesignTable.SelectedIndex) || sender is not BridgeDesignTable table || !ReferenceEquals(table, Table)) return;
        showing = true;
        try { SelectedIndex = table.SelectedIndex < Items.Count ? table.SelectedIndex : -1; }
        finally { showing = false; }
    }
}

/// <summary>
/// The slider of a parameter of the inputs (Number of the historic view): the range and the step of the parameter, then its value; a value moved by
/// the user goes to the parameter (<see cref="BridgeDesignParameter.SliderValue"/>, refused by the sheet when read-only), a value written by the sheet
/// (the text typed, undo) comes back without being sent again. The range is set before the value and is never cleared, and nothing is sent while
/// the slider follows the sheet or after its parameter has gone (a template recycled): the slider never coerces the value of the sheet.
/// </summary>
public sealed class BridgeDesignSlider : Slider
{
    public static readonly DependencyProperty ParameterProperty = DependencyProperty.Register(nameof(Parameter), typeof(BridgeDesignParameter), typeof(BridgeDesignSlider),
        new PropertyMetadata(null, (owner, e) => ((BridgeDesignSlider)owner).Show((BridgeDesignParameter?)e.OldValue, (BridgeDesignParameter?)e.NewValue)));

    private bool following;

    public BridgeDesignSlider()
    {
        IsSnapToTickEnabled = true;
        Focusable = false;
    }

    /// <summary>The parameter of the slider.</summary>
    public BridgeDesignParameter? Parameter { get => (BridgeDesignParameter?)GetValue(ParameterProperty); set => SetValue(ParameterProperty, value); }

    private void Show(BridgeDesignParameter? previous, BridgeDesignParameter? parameter)
    {
        if (previous is not null) previous.PropertyChanged -= OnParameterChanged;
        if (parameter is null) return;
        Follow(() =>
        {
            var p = parameter.Parameter;
            if (p.Min > Maximum) { Maximum = p.Max; Minimum = p.Min; } else { Minimum = p.Min; Maximum = p.Max; }
            TickFrequency = p.Step;
            Value = parameter.SliderValue;
        });
        parameter.PropertyChanged += OnParameterChanged;
    }

    private void OnParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BridgeDesignParameter.SliderValue) && sender is BridgeDesignParameter parameter && ReferenceEquals(parameter, Parameter))
            Follow(() => Value = parameter.SliderValue);
    }

    private void Follow(Action change)
    {
        following = true;
        try { change(); }
        finally { following = false; }
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        if (!following && Parameter is { } parameter) parameter.SliderValue = newValue;
    }
}

/// <summary>The moment diagram of the details (<see cref="BridgeConceptMomentPlot"/> of the historic view, unchanged: decision Q3) on the result of the item.</summary>
public sealed class BridgeDesignMomentHost : Decorator
{
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(nameof(Result), typeof(BridgeDesignModel.Result), typeof(BridgeDesignMomentHost),
        new PropertyMetadata(null, (owner, e) => ((BridgeDesignMomentHost)owner).Draw((BridgeDesignModel.Result?)e.NewValue)));

    public BridgeDesignMomentHost() => Child = new BridgeConceptMomentPlot();

    public BridgeDesignModel.Result? Result { get => (BridgeDesignModel.Result?)GetValue(ResultProperty); set => SetValue(ResultProperty, value); }

    /// <summary>The historic drawing.</summary>
    internal BridgeConceptMomentPlot Plot => (BridgeConceptMomentPlot)Child;

    private void Draw(BridgeDesignModel.Result? result)
    {
        Plot.Result = result;
        Plot.InvalidateVisual();
    }
}

/// <summary>The icon of a typology on its button (<see cref="BridgeFamilyIcon"/> of the historic view, unchanged: Q3), drawn with the ink of the button.</summary>
public sealed class BridgeDesignFamilyIcon : Decorator
{
    public static readonly DependencyProperty FamilyProperty = DependencyProperty.Register(nameof(Family), typeof(string), typeof(BridgeDesignFamilyIcon),
        new PropertyMetadata(null, (owner, _) => ((BridgeDesignFamilyIcon)owner).Draw()));

    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(nameof(Ink), typeof(Brush), typeof(BridgeDesignFamilyIcon),
        new PropertyMetadata(null, (owner, _) => ((BridgeDesignFamilyIcon)owner).Draw()));

    public BridgeDesignFamilyIcon() => Child = new BridgeFamilyIcon();

    public string? Family { get => (string?)GetValue(FamilyProperty); set => SetValue(FamilyProperty, value); }

    public Brush? Ink { get => (Brush?)GetValue(InkProperty); set => SetValue(InkProperty, value); }

    /// <summary>The historic drawing.</summary>
    internal BridgeFamilyIcon Icon => (BridgeFamilyIcon)Child;

    private void Draw()
    {
        if (Family is { } family) Icon.Family = family;
        if (Ink is { } ink) Icon.Ink = ink;
        Icon.InvalidateVisual();
    }
}

/// <summary>
/// The wheel over a slider or a closed combo box scrolls the page (ChainedScrollViewer.ForwardWheel, as the historic Number and Ui.Choice):
/// the value does not change under the wheel.
/// </summary>
public static class BridgeDesignWheel
{
    public static readonly DependencyProperty ForwardProperty = DependencyProperty.RegisterAttached("Forward", typeof(bool), typeof(BridgeDesignWheel),
        new PropertyMetadata(false, (owner, e) =>
        {
            if (owner is not UIElement element) return;
            element.PreviewMouseWheel -= OnWheel;
            if (e.NewValue is true) element.PreviewMouseWheel += OnWheel;
        }));

    public static bool GetForward(DependencyObject element) => (bool)element.GetValue(ForwardProperty);

    public static void SetForward(DependencyObject element, bool value) => element.SetValue(ForwardProperty, value);

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ComboBox { IsDropDownOpen: true } || sender is not DependencyObject source) return;
        ChainedScrollViewer.ForwardWheel(source, e);
        e.Handled = true;
    }
}

/// <summary>Visible when the value exists (the alternatives under the results, shown with a result).</summary>
internal sealed class PresentShownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// The size of a text of a panel where its role does not give it (Ui.Text of the historic view): «Caption» for the notes at 11 points (the notes of
/// the substructure, of the traffic, of the extended typologies, of the rates, of the alternatives and of the ranking), «Subheading» for the
/// placeholder of the comparison (14); otherwise empty (the size of the role).
/// </summary>
internal sealed class TextSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not BridgeDesignText text) return "";
        if (text.Role == BridgeDesignTextRole.Body && text.Text == BridgeDesignPresentation.ComparisonPlaceholder) return "Subheading";
        if (text.Role != BridgeDesignTextRole.Note) return "";
        bool caption = text.Text is BridgeDesignPresentation.SubstructureNote or BridgeDesignPresentation.TrafficFootnote or BridgeDesignPresentation.ExtendedNote
            or BridgeDesignPresentation.TowersSpansNote or BridgeDesignPresentation.AdviceNote or BridgeDesignOptimizationPresentation.RankingNote
            || BridgeDesignModel.RateNotes.Values.Contains(text.Text);
        return caption ? "Caption" : "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Part of the items of a panel: «buttons» the buttons (a bar, as Ui.Bar under the comparison and under the dimensions), «others» the other items in
/// their order, «first» the first item alone (the button of the price editor), «rest» the items after it.
/// </summary>
internal sealed class ItemsOfKindConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IReadOnlyList<BridgeDesignItem> items) return null;
        return (parameter as string) switch
        {
            "buttons" => items.OfType<BridgeDesignButton>().ToArray(),
            "others" => items.Where(i => i is not BridgeDesignButton).ToArray(),
            "first" => items.Take(1).ToArray(),
            "rest" => items.Skip(1).ToArray(),
            _ => items
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>The filter of the cloud as the index of its combo box: 0 every typology (null), then the typologies in their order.</summary>
internal sealed class FamilyFilterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string id ? Array.FindIndex(BridgeDesignModel.Families, f => f.Id == id) + 1 : 0;

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var families = BridgeDesignModel.Families;
        return value is int index && index > 0 && index <= families.Length ? families[index - 1].Id : null;
    }
}

/// <summary>The colour of a typology in the legend of the charts of the search (BridgeOptimizationPlot, Q3).</summary>
internal sealed class FamilyLegendBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string id ? BridgeOptimizationPlot.LegendBrush(id) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
