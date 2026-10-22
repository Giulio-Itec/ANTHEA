using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Desktop.Themes;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>Visible when the text is not empty (the notes, the messages, the values of a former document).</summary>
internal sealed class TextShownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

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

    private bool syncing;

    public ConcreteTableGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        AutoGenerateColumns = false; CanUserAddRows = false; CanUserDeleteRows = false; CanUserSortColumns = true;
        SelectionMode = DataGridSelectionMode.Extended; SelectionUnit = DataGridSelectionUnit.FullRow;
        SelectionChanged += OnSelectionChanged;
        Sorting += OnSorting;
    }

    public ConcreteTableViewModel? Table { get => (ConcreteTableViewModel?)GetValue(TableProperty); set => SetValue(TableProperty, value); }

    private void Attach(ConcreteTableViewModel? old, ConcreteTableViewModel? table)
    {
        if (old is not null) { old.PropertyChanged -= OnTableChanged; old.CommittingEdits -= OnCommitting; }
        Columns.Clear(); ItemsSource = null;
        if (table is null) return;
        table.PropertyChanged += OnTableChanged; table.CommittingEdits += OnCommitting;
        foreach (var column in table.Columns) Columns.Add(Column(column));
        IsReadOnly = table.IsReadOnly;
        Fill();
    }

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
                result = new DataGridTextColumn { Binding = binding };
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
}
