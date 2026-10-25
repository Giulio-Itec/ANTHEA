using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Desktop.Themes;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>Visible when the text is not empty (the notes, the messages, the values of a former document).</summary>
internal sealed class TextShownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Collapsed when true (the plane through the selected action, only for the 2D domain).</summary>
internal sealed class CollapsedWhenTrueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// The template of a card by its layout (<see cref="ConcreteCard.Kind"/>): the summary, the detail with its reference and the value of the historic
/// VerificationCards (AddSummary, AddDetail, AddValue).
/// </summary>
internal sealed class ConcreteCardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Summary { get; set; }

    public DataTemplate? Detail { get; set; }

    public DataTemplate? Value { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item is not ConcreteCard card ? null : card.Kind switch
    {
        ConcreteCardKind.Detail => Detail,
        ConcreteCardKind.Value => Value,
        _ => Summary
    };
}

/// <summary>
/// The labels of the limit state of a domain panel (<see cref="Common.FieldForm.ChoiceLabelsProperty"/>): with separate actions the choice
/// «Tipo di dominio precedente» shows SLU and SLV as «Plastico» and «Elastico» (the ItemTemplate with DomainLabelConverter of BuildDomainPanel in
/// the historic view); with the unified actions («Stato limite») none. Value: <see cref="ConcreteActionTableViewModel.Unified"/>.
/// </summary>
internal sealed class DomainStateLabelsConverter : IValueConverter
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> labels = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
    {
        ["stato"] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SLU"] = Anthea.Application.Concrete.ConcreteEditor.Label("SLU"), ["SLV"] = Anthea.Application.Concrete.ConcreteEditor.Label("SLV")
        }
    };

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is false ? labels : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Visible when the value exists.</summary>
internal sealed class PresentShownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// The colour of the border of a card of a summary (<see cref="ConcreteCard"/>), as the historic VerificationCards: the colour of η by its band, the
/// colour of a failed check, of a missing outcome, the green of a check satisfied without a ratio; the Utilization tokens of the theme.
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
/// A table of the RC section (the JsonGrid of the historic view) on a <see cref="ConcreteTableViewModel"/>: the columns are built here from the
/// columns of the table (check box, choice with its items, text), with their header and visibility; the rows are those the table shows, in its
/// order; the selection is the one of the table, both ways; a click on a header orders the table as the historic grid (the arrow on the column
/// clicked last); the edits are committed when the table asks it (the bar of the filters, the commands). Read-only when the table is. Presentation
/// only: every write goes into the rows of the ViewModel.
/// </summary>
public sealed class ConcreteTableGrid : DataGrid
{
    public static readonly DependencyProperty TableProperty = DependencyProperty.Register(nameof(Table), typeof(ConcreteTableViewModel), typeof(ConcreteTableGrid),
        new PropertyMetadata(null, (owner, e) => ((ConcreteTableGrid)owner).Attach((ConcreteTableViewModel?)e.OldValue, (ConcreteTableViewModel?)e.NewValue)));

    /// <summary>
    /// The table of the actions that owns <see cref="Table"/> (AttachClipboard of the historic view): the extended selection goes to it, the column of
    /// the current cell is where the paste starts, Ctrl+C and Ctrl+V copy and paste, the menu «Copia righe · Ctrl+C», «Incolla dalla cella · Ctrl+V»,
    /// «Aggiungi dagli appunti».
    /// </summary>
    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(nameof(Actions), typeof(ConcreteActionTableViewModel), typeof(ConcreteTableGrid),
        new PropertyMetadata(null, (owner, e) => ((ConcreteTableGrid)owner).AttachActions((ConcreteActionTableViewModel?)e.NewValue)));

    /// <summary>
    /// The columns share the width of the table (the JsonGrid «stretch» of the historic view: the bars, the actions, the shear, the stresses and their
    /// details, the alternatives of the search); otherwise 112 wide, 140 a choice (the cables).
    /// </summary>
    public static readonly DependencyProperty StretchColumnsProperty = DependencyProperty.Register(nameof(StretchColumns), typeof(bool), typeof(ConcreteTableGrid),
        new PropertyMetadata(false, (owner, _) => ((ConcreteTableGrid)owner).ApplyWidths()));

    /// <summary>
    /// The shares of the last columns of a stretched table, separated by spaces, the last one at the end (the historic view: «2» for the outcome of
    /// the domains, «1.8 1.4» for the stresses and the cracking, «3» for the outcome of the shear); the others take 1. Inherited: set on the host of
    /// the table of the actions.
    /// </summary>
    public static readonly DependencyProperty TrailingWeightsProperty = DependencyProperty.RegisterAttached("TrailingWeights", typeof(string), typeof(ConcreteTableGrid),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.Inherits, (owner, _) => (owner as ConcreteTableGrid)?.ApplyWidths()));

    public static string GetTrailingWeights(DependencyObject target) => (string)target.GetValue(TrailingWeightsProperty);

    public static void SetTrailingWeights(DependencyObject target, string value) => target.SetValue(TrailingWeightsProperty, value);

    /// <summary>
    /// Minimum widths of some columns, «key=width» separated by spaces (the shear of the historic view: «nome=120 esito=220»), in place of 60 and 65.
    /// Inherited: set on the host of the table of the actions.
    /// </summary>
    public static readonly DependencyProperty MinimumWidthsProperty = DependencyProperty.RegisterAttached("MinimumWidths", typeof(string), typeof(ConcreteTableGrid),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.Inherits, (owner, _) => (owner as ConcreteTableGrid)?.ApplyWidths()));

    public static string GetMinimumWidths(DependencyObject target) => (string)target.GetValue(MinimumWidthsProperty);

    public static void SetMinimumWidths(DependencyObject target, string value) => target.SetValue(MinimumWidthsProperty, value);

    /// <summary>
    /// Text columns whose cells wrap, keys separated by spaces, with rows as high as their text (the outcome of the shear of the historic view: «esito»).
    /// Inherited: set on the host of the table of the actions.
    /// </summary>
    public static readonly DependencyProperty WrappedColumnsProperty = DependencyProperty.RegisterAttached("WrappedColumns", typeof(string), typeof(ConcreteTableGrid),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.Inherits, (owner, _) => (owner as ConcreteTableGrid)?.ApplyWidths()));

    public static string GetWrappedColumns(DependencyObject target) => (string)target.GetValue(WrappedColumnsProperty);

    public static void SetWrappedColumns(DependencyObject target, string value) => target.SetValue(WrappedColumnsProperty, value);

    /// <summary>The columns of the unified actions, inserted by the historic view with their own minimum and the width of the header.</summary>
    private static readonly IReadOnlyDictionary<string, double> InsertedColumns = new Dictionary<string, double>(StringComparer.Ordinal) { ["elemento"] = 75, ["stato"] = 80 };

    private bool syncing;

    public bool StretchColumns { get => (bool)GetValue(StretchColumnsProperty); set => SetValue(StretchColumnsProperty, value); }

    public ConcreteTableGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        AutoGenerateColumns = false; CanUserAddRows = false; CanUserDeleteRows = false; CanUserSortColumns = true;
        SelectionMode = DataGridSelectionMode.Extended; SelectionUnit = DataGridSelectionUnit.FullRow;
        SelectionChanged += OnSelectionChanged;
        Sorting += OnSorting;
    }

    public ConcreteTableViewModel? Table { get => (ConcreteTableViewModel?)GetValue(TableProperty); set => SetValue(TableProperty, value); }

    public ConcreteActionTableViewModel? Actions { get => (ConcreteActionTableViewModel?)GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    private void AttachActions(ConcreteActionTableViewModel? actions)
    {
        ContextMenu = null;
        if (actions is null) return;
        var menu = new ContextMenu();
        foreach (var (label, command) in new (string, System.Windows.Input.ICommand)[] { ("Copia righe · Ctrl+C", actions.CopyCommand),
            ("Incolla dalla cella · Ctrl+V", actions.PasteCommand), ("Aggiungi dagli appunti", actions.AppendCommand) })
            menu.Items.Add(new MenuItem { Header = label, Command = command });
        ContextMenu = menu;
    }

    // Ctrl+C and Ctrl+V of the historic grid: the copy outside a cell being edited, the paste unless a single value is typed in a cell.
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (Actions is { } actions && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            if (e.Key == System.Windows.Input.Key.C && e.OriginalSource is not TextBox)
            {
                if (actions.CopyCommand.CanExecute(null)) actions.CopyCommand.Execute(null);
                e.Handled = true; return;
            }
            if (e.Key == System.Windows.Input.Key.V)
            {
                string text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                if (!(e.OriginalSource is TextBox && !text.Contains('\t') && !text.Contains('\n')))
                {
                    if (actions.PasteCommand.CanExecute(null)) actions.PasteCommand.Execute(null);
                    e.Handled = true; return;
                }
            }
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnCurrentCellChanged(EventArgs e)
    {
        base.OnCurrentCellChanged(e);
        if (Actions is { } actions) actions.CurrentColumn = CurrentColumn?.SortMemberPath ?? "";
    }

    private void Attach(ConcreteTableViewModel? old, ConcreteTableViewModel? table)
    {
        if (old is not null) { old.PropertyChanged -= OnTableChanged; old.CommittingEdits -= OnCommitting; }
        // The rows of the previous table leave the grid without writing an empty selection into the new table (a new panel after Reload of the
        // sheet, «Applica» of «Sollecitazioni…»: the table keeps the selection it was created with).
        syncing = true;
        try { Columns.Clear(); ItemsSource = null; }
        finally { syncing = false; }
        if (table is null) return;
        table.PropertyChanged += OnTableChanged; table.CommittingEdits += OnCommitting;
        foreach (var column in table.Columns) Columns.Add(Column(column));
        ApplyWidths();
        IsReadOnly = table.IsReadOnly;
        Fill();
    }

    // The widths of the columns of the historic JsonGrid: minimum 60 for a check box, otherwise 65 (or MinimumWidths); shares or fixed widths
    // (StretchColumns, TrailingWeights); the columns of the unified actions at the width of their header; the wrapped columns (WrappedColumns).
    private void ApplyWidths()
    {
        string[] weights = GetTrailingWeights(this).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var minimums = GetMinimumWidths(this).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('='))
            .ToDictionary(p => p[0], p => double.Parse(p[1], CultureInfo.InvariantCulture), StringComparer.Ordinal);
        var wrapped = GetWrappedColumns(this).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var regular = Columns.Where(c => !InsertedColumns.ContainsKey(c.SortMemberPath)).ToList();
        foreach (var column in Columns)
        {
            if (InsertedColumns.TryGetValue(column.SortMemberPath, out double minimum)) { column.MinWidth = minimum; column.ClearValue(DataGridColumn.WidthProperty); continue; }
            column.MinWidth = Math.Max(minimums.TryGetValue(column.SortMemberPath, out double own) ? own : column is DataGridCheckBoxColumn ? 60 : 65, HeaderWidth(column));
            int fromEnd = regular.Count - regular.IndexOf(column);
            double share = fromEnd <= weights.Length ? double.Parse(weights[^fromEnd], CultureInfo.InvariantCulture) : 1;
            column.Width = StretchColumns ? new DataGridLength(share, DataGridLengthUnitType.Star) : new DataGridLength(column is DataGridComboBoxColumn ? 140 : 112);
            if (wrapped.Contains(column.SortMemberPath) && column is DataGridTextColumn text) text.ElementStyle = wrapStyle;
        }
        if (wrapped.Count > 0) RowHeight = double.NaN;
    }

    // F5.15, V-c (decision of 25/10): a column is never narrower than its header, so that the headers of the shear table
    // («Mx [kNm]», «VRd,x [kN]»…) are not cut at 1600 px. Header in semibold with the padding 6 of the theme on each side.
    private double HeaderWidth(DataGridColumn column)
    {
        if (column.Header is not string { Length: > 0 } text) return 0;
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection, new Typeface(FontFamily, FontStyle, FontWeights.SemiBold, FontStretch),
            FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return Math.Ceiling(formatted.WidthIncludingTrailingWhitespace) + 16;
    }

    // The cells of a wrapped column (the outputStyle of the shear of the historic view).
    private readonly Style wrapStyle = new(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap) } };

    private static DataGridColumn Column(ConcreteColumnViewModel column)
    {
        var binding = new Binding("[" + column.Key + "]") { Mode = column.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay };
        DataGridColumn result;
        switch (column.Kind)
        {
            case ConcreteColumnKind.Bool:
                binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                result = new DataGridCheckBoxColumn { Binding = binding };
                break;
            case ConcreteColumnKind.Choice:
                var choice = new DataGridComboBoxColumn { SelectedItemBinding = binding };
                BindingOperations.SetBinding(choice, DataGridComboBoxColumn.ItemsSourceProperty, new Binding(nameof(ConcreteColumnViewModel.Choices)) { Source = column });
                result = choice;
                break;
            default:
                // The numbers centred, the texts on the left, and the presentation of the numbers when the host enables it (JsonGrid).
                result = new NumericDisplayColumn { Key = column.Key, Binding = binding, ElementStyle = Ui.NumericTextStyle(), EditingElementStyle = Ui.NumericTextStyle(true) };
                break;
        }
        result.Header = column.Header; result.SortMemberPath = column.Key; result.IsReadOnly = column.IsReadOnly;
        BindingOperations.SetBinding(result, DataGridColumn.VisibilityProperty,
            new Binding(nameof(ConcreteColumnViewModel.IsVisible)) { Source = column, Converter = new BooleanToVisibilityConverter() });
        return result;
    }

    // The rows shown and the selection of the table.
    private void Fill()
    {
        if (Table is not { } table) return;
        syncing = true;
        try
        {
            ItemsSource = table.Shown.ToList();
            SelectedIndex = table.SelectedIndex;
            foreach (var column in Columns)
                column.SortDirection = column.SortMemberPath == table.Filter.SortedColumn ? table.Filter.SortedAscending ? ListSortDirection.Ascending : ListSortDirection.Descending : null;
        }
        finally { syncing = false; }
    }

    private void OnTableChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Table is not { } table) return;
        switch (e.PropertyName)
        {
            case nameof(ConcreteTableViewModel.Shown): Fill(); break;
            case nameof(ConcreteTableViewModel.SelectedIndex) or nameof(ConcreteTableViewModel.Selected):
                if (SelectedIndex != table.SelectedIndex) { syncing = true; try { SelectedIndex = table.SelectedIndex; } finally { syncing = false; } }
                break;
            case nameof(ConcreteTableViewModel.IsReadOnly): IsReadOnly = table.IsReadOnly; break;
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || Table is not { } table || !ReferenceEquals(e.OriginalSource, this)) return;
        table.SelectedIndex = SelectedIndex;
        Actions?.SetSelectedRows(SelectedItems.OfType<Anthea.Presentation.Forms.JsonRowViewModel>());
    }

    private void OnSorting(object? sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (Table is not { } table || string.IsNullOrEmpty(e.Column.SortMemberPath)) return;
        CommitEdit(DataGridEditingUnit.Row, true);
        table.Filter.SortBy(e.Column.SortMemberPath);
        Fill();
    }

    private void OnCommitting(object? sender, EventArgs e) => CommitEdit(DataGridEditingUnit.Row, true);

    // The edit of a row committed: the rows shown again in the order and with the filter of the table.
    protected override void OnRowEditEnding(DataGridRowEditEndingEventArgs e)
    {
        base.OnRowEditEnding(e);
        if (!e.Cancel && e.EditAction == DataGridEditAction.Commit) RowCommitted();
    }

    /// <summary>
    /// The end of the edit of a row: the table shows its rows again (filter and order), queued after the edit, as the collection view of the historic
    /// JsonGrid moves an edited row to its place; not while another row is being edited.
    /// </summary>
    internal void RowCommitted() => Dispatcher.BeginInvoke(new Action(() =>
    {
        if (Table is { } table && !((System.ComponentModel.IEditableCollectionView)Items).IsEditingItem) table.Refresh();
    }));
}
