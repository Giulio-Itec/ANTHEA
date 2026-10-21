using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Anthea.Application.Presentation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.CompositeSection;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Desktop.Themes;

namespace X.Desktop.Views.CompositeSection;

/// <summary>
/// The templates of the views of the composite section (CompositeSectionResources.xaml) found by the selectors: from the element, as WPF finds
/// them, otherwise from the dictionary itself, for the parts of the view that it keeps out of the tree while they are not shown (the other page of
/// the inputs, the other side panel, the page not shown) and that are laid out on their own (the checks build them as the historic view builds
/// every control). The dictionary is loaded when it is needed, without a static cache (rule P3).
/// </summary>
internal static class CompositeSectionTemplates
{
    internal static DataTemplate Find(FrameworkElement element, string key) => element.TryFindResource(key) as DataTemplate
        ?? (DataTemplate)new ResourceDictionary { Source = new Uri("pack://application:,,,/ANTHEA;component/Views/CompositeSection/CompositeSectionResources.xaml", UriKind.Absolute) }[key];
}

/// <summary>
/// The template of a panel of results (<see cref="BridgePanel"/>, F5.11), as Render of the historic view draws it: one block alone when the panel
/// has one and no scroll viewer of its own (a table then fills the space of its tab and scrolls by itself), a stack of blocks, or the stack in a
/// scroll viewer of its own. The templates are resources of the view (PanelSingle, PanelStack, PanelScroll).
/// </summary>
public sealed class CompositeSectionPanelSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not BridgePanel panel || container is not FrameworkElement element) return null;
        string key = panel.Scroll ? "PanelScroll" : panel.Blocks.Count == 1 ? "PanelSingle" : "PanelStack";
        return CompositeSectionTemplates.Find(element, key);
    }
}

/// <summary>The template of a block of a panel of results (<see cref="BridgeBlock"/>): a text, a table or a group (BlockText, BlockTable, BlockGroup).</summary>
public sealed class CompositeSectionBlockSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not BridgeBlock block || container is not FrameworkElement element) return null;
        string key = block.Kind switch { BridgeBlockKind.Table => "BlockTable", BridgeBlockKind.Group => "BlockGroup", _ => "BlockText" };
        return CompositeSectionTemplates.Find(element, key);
    }
}

/// <summary>The template of a table of the properties of the section (<see cref="PresentationTable"/>): a Block, or a closed group when collapsible.</summary>
public sealed class CompositeSectionTableSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not PresentationTable table || container is not FrameworkElement element) return null;
        return CompositeSectionTemplates.Find(element, table.Collapsible ? "TableGroup" : "TableBlock");
    }
}

/// <summary>The blocks of a group of a panel as a panel of their own (Render(new BridgePanel(children)) of the historic view).</summary>
internal sealed class PanelOfConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is IReadOnlyList<BridgeBlock> blocks ? new BridgePanel(blocks) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Visible when the value exists (the homogenization of a card, the properties of a phase).</summary>
internal sealed class PresentShownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Collapsed when true (the properties of the section, shown when the side panel does not show the summary).</summary>
internal sealed class CollapsedWhenTrueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Visible when the page of the inputs (an int) is the parameter: the inputs of the control panel (0) or of the phases (1).</summary>
internal sealed class PageShownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int page && int.TryParse(parameter as string, NumberStyles.Integer, CultureInfo.InvariantCulture, out int shown) && page == shown ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// The content of a tab of the properties of the section (<see cref="CompositeSectionPropertyTab"/>): the tables of the steel, the slab with its
/// form, or a phase (SteelProperties, SlabProperties, PhaseProperties, resources of the view).
/// </summary>
public sealed class PropertyTabSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not CompositeSectionPropertyTab tab || container is not FrameworkElement element) return null;
        string key = tab.Phase is not null ? "PhaseProperties" : tab.Header == BridgeInputPresentation.SlabPropertiesHeader ? "SlabProperties" : "SteelProperties";
        return CompositeSectionTemplates.Find(element, key);
    }
}

/// <summary>Visible when the text is not empty (the notices, the note of a table, the message of the cards).</summary>
internal sealed class TextShownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// The colour of the border of a card of the summary (<see cref="ConcreteCard"/>), as the historic VerificationCards: the colour of η by its band
/// (<see cref="UtilizationColors"/>), the colour of a failed check (η &gt; 1), of a missing outcome, the green of a check satisfied without a
/// ratio; the Utilization tokens of the theme, the same in every appearance.
/// </summary>
internal sealed class CardBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ConcreteCard card) return null;
        string token = card.Tone switch
        {
            ConcreteCardTone.Failed => "Utilization.Exceeded",
            ConcreteCardTone.Passed => "Utilization.Fair",
            ConcreteCardTone.Utilization => Band(card.Ratio),
            ConcreteCardTone.Information => "Accent",
            _ => "Utilization.None"
        };
        return ThemeTokens.Brush(token);
    }

    /// <summary>The token of the band of <paramref name="ratio"/>, the bands of UtilizationColors.Color.</summary>
    private static string Band(double? ratio) => ratio is not { } r || !double.IsFinite(r) ? "Utilization.None"
        : r <= .5 ? "Utilization.Low" : r <= .7 ? "Utilization.Fair" : r <= .9 ? "Utilization.High" : r <= 1 ? "Utilization.Critical" : "Utilization.Exceeded";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// A table of a panel of results or of the properties (ResultTable of the historic view on Ui.Table): read-only, one text column for each header,
/// automatic widths between 72 and 300, numbers aligned as Ui.NumericTextStyle, cells with a margin, cut with an ellipsis and their text as tool
/// tip. The rows are the texts already formatted by Application. Presentation only.
/// </summary>
public sealed class CompositeSectionGrid : DataGrid
{
    public static readonly DependencyProperty HeadersProperty = DependencyProperty.Register(nameof(Headers), typeof(IReadOnlyList<string>), typeof(CompositeSectionGrid),
        new PropertyMetadata(null, (owner, _) => ((CompositeSectionGrid)owner).Build()));

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(IEnumerable<string[]>), typeof(CompositeSectionGrid),
        new PropertyMetadata(null, (owner, _) => ((CompositeSectionGrid)owner).ItemsSource = ((CompositeSectionGrid)owner).Rows?.ToList()));

    public CompositeSectionGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        IsReadOnly = true;
        AutoGenerateColumns = false;
    }

    public IReadOnlyList<string>? Headers { get => (IReadOnlyList<string>?)GetValue(HeadersProperty); set => SetValue(HeadersProperty, value); }

    public IEnumerable<string[]>? Rows { get => (IEnumerable<string[]>?)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    private void Build()
    {
        Columns.Clear();
        if (Headers is not { } headers) return;
        for (int i = 0; i < headers.Count; i++)
        {
            var style = new Style(typeof(TextBlock), Ui.NumericTextStyle());
            style.Setters.Add(new Setter(MarginProperty, new Thickness(6, 3, 6, 3)));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(ToolTipProperty, new Binding("Text") { RelativeSource = RelativeSource.Self }));
            Columns.Add(new DataGridTextColumn
            {
                Header = headers[i], Binding = new Binding($"[{i}]") { Mode = BindingMode.OneWay }, ElementStyle = style,
                Width = DataGridLength.Auto, MinWidth = 72, MaxWidth = 300
            });
        }
    }
}

/// <summary>
/// The table of the actions of the phases (JsonGrid of the historic view on <see cref="CompositeSectionFields.Actions"/>): the columns with their
/// widths, the first two frozen, check boxes, choices and numbers with the numeric presentation, the cells written at every change of the user
/// (UpdateSourceTrigger=PropertyChanged, as the tables of the other MVVM views). The column ΔT is shown with <see cref="TorqueShown"/>; a cell
/// that does not apply to the kind of its phase (<see cref="CompositeSectionSheetViewModel.IsCellInactive"/>) is disabled and faded with its
/// tool tip, as the triggers of the historic cells. Presentation only: the rows are those of the sheet.
/// </summary>
public sealed class CompositeSectionActionsGrid : DataGrid
{
    public static readonly DependencyProperty TorqueShownProperty = DependencyProperty.Register(nameof(TorqueShown), typeof(bool), typeof(CompositeSectionActionsGrid),
        new PropertyMetadata(true, (owner, e) => ((CompositeSectionActionsGrid)owner).Column("T").Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public CompositeSectionActionsGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        AutoGenerateColumns = false; CanUserSortColumns = false; CanUserAddRows = false; CanUserDeleteRows = false;
        FrozenColumnCount = 2; MinHeight = 110;
        var fields = CompositeSectionFields.Actions;
        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var binding = new Binding($"[{field.Key}]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            DataGridColumn column;
            if (field.Bool) column = new DataGridCheckBoxColumn { Binding = binding };
            else if (field.Choices is not null) column = new DataGridComboBoxColumn { ItemsSource = field.Choices.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray(), SelectedItemBinding = binding };
            else column = new NumericDisplayColumn { Key = field.Key, Binding = binding, ElementStyle = Ui.NumericTextStyle(), EditingElementStyle = Ui.NumericTextStyle(true) };
            column.Header = field.Label; column.SortMemberPath = field.Key; column.MinWidth = field.Bool ? 60 : 65;
            column.Width = new DataGridLength(CompositeSectionFields.ActionWidths[i]);
            column.CellStyle = InactiveStyle(field.Key);
            Columns.Add(column);
        }
    }

    /// <summary>The column ΔT is shown (the box girder).</summary>
    public bool TorqueShown { get => (bool)GetValue(TorqueShownProperty); set => SetValue(TorqueShownProperty, value); }

    internal DataGridColumn Column(string key) => Columns.Single(c => c.SortMemberPath == key);

    /// <summary>The style of the cells of the column <paramref name="key"/>: disabled, faded (0,3) and with the tool tip of the sheet when the cell does not apply.</summary>
    private Style InactiveStyle(string key)
    {
        // The implicit style of the cells of the theme (the application resources: the grid is not in a tree yet), as the historic cells.
        var style = new Style(typeof(DataGridCell), TryFindResource(typeof(DataGridCell)) as Style);
        var inactive = new MultiBinding { Converter = InactiveCell.Instance, ConverterParameter = key };
        inactive.Bindings.Add(new Binding("[tipo]"));
        inactive.Bindings.Add(new Binding("DataContext") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGrid), 1) });
        var trigger = new DataTrigger { Binding = inactive, Value = true };
        trigger.Setters.Add(new Setter(IsEnabledProperty, false));
        trigger.Setters.Add(new Setter(OpacityProperty, .3));
        trigger.Setters.Add(new Setter(ToolTipProperty, BridgeInputPresentation.InactiveCellToolTip));
        style.Triggers.Add(trigger);
        return style;
    }

    /// <summary>The cell of the column (parameter) for a phase of a kind (first value) does not apply in the sheet (second value).</summary>
    private sealed class InactiveCell : IMultiValueConverter
    {
        internal static readonly InactiveCell Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values.Length == 2 && values[0] is string kind && values[1] is CompositeSectionSheetViewModel sheet && parameter is string key && sheet.IsCellInactive(kind, key);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
