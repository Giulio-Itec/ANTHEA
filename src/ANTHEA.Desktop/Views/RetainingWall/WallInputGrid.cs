using System.ComponentModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.RetainingWall;

namespace X.Desktop.Views.RetainingWall;

/// <summary>How the rows of a table of the inputs are marked on their left edge (the coloured border of the historic grids).</summary>
public enum WallRowMark { None, LayerIndex, GlobalLayer }

/// <summary>
/// A table of the inputs of the wall sheet (the JsonGrid of the historic view) on a <see cref="WallTableViewModel"/>: its columns are built
/// here from the <see cref="FieldSpec"/> of the table, as JsonGrid builds them (check box, choice or number with the numeric presentation;
/// header, minimum and width of JsonGrid), with the widths of the historic grid (<see cref="ColumnWidths"/>). The cells write the rows of
/// the ViewModel at every keystroke, as the bindings of JsonGrid. The selection is the one of the table: a choice of the user goes to
/// <see cref="WallTableViewModel.SelectedIndex"/>, a selection set by the sheet comes back here; a row removed by the sheet is not a choice
/// of the user (the table clears its selection itself, as the WPF grid). The left border of a row is the colour of its layer (tokens
/// Swatch.Wall1–6), by index or, for the deep layers, by the name of the layer (RetainingWallDrawing.GlobalLayerColor). Presentation only.
/// </summary>
public sealed class WallInputGrid : DataGrid
{
    public static readonly DependencyProperty TableProperty = DependencyProperty.Register(nameof(Table), typeof(WallTableViewModel), typeof(WallInputGrid),
        new PropertyMetadata(null, (owner, e) => ((WallInputGrid)owner).Attach((WallTableViewModel?)e.OldValue, (WallTableViewModel?)e.NewValue)));

    /// <summary>
    /// Widths of the columns, separated by commas: «-» the width of JsonGrid, a number, or a star width («2*»), each with an optional minimum
    /// («2*/80»); a final «…» repeats the last entry on the remaining columns.
    /// </summary>
    public static readonly DependencyProperty ColumnWidthsProperty = DependencyProperty.Register(nameof(ColumnWidths), typeof(string), typeof(WallInputGrid),
        new PropertyMetadata(null, (owner, _) => ((WallInputGrid)owner).Build()));

    /// <summary>Star widths for every column (the «stretch» of JsonGrid).</summary>
    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(nameof(Stretch), typeof(bool), typeof(WallInputGrid),
        new PropertyMetadata(false, (owner, _) => ((WallInputGrid)owner).Build()));

    /// <summary>The style of the headers of every column (CompactHeaders of the historic view).</summary>
    public static readonly DependencyProperty HeaderStyleProperty = DependencyProperty.Register(nameof(HeaderStyle), typeof(Style), typeof(WallInputGrid),
        new PropertyMetadata(null, (owner, _) => ((WallInputGrid)owner).Build()));

    public static readonly DependencyProperty RowMarkProperty = DependencyProperty.Register(nameof(RowMark), typeof(WallRowMark), typeof(WallInputGrid),
        new PropertyMetadata(WallRowMark.None, (owner, _) => ((WallInputGrid)owner).RefreshMarks()));

    /// <summary>
    /// Without a <see cref="Table"/>: the columns of a table that is not a WallTableViewModel (the rows of the window of the values), with
    /// <see cref="Rows"/>; the selection stays the one of the grid.
    /// </summary>
    public static readonly DependencyProperty FieldsProperty = DependencyProperty.Register(nameof(Fields), typeof(IReadOnlyList<FieldSpec>), typeof(WallInputGrid),
        new PropertyMetadata(null, (owner, _) => ((WallInputGrid)owner).Build()));

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(nameof(Rows), typeof(JsonRowCollection), typeof(WallInputGrid),
        new PropertyMetadata(null, (owner, e) => { var grid = (WallInputGrid)owner; if (grid.Table is null) grid.ItemsSource = (JsonRowCollection?)e.NewValue; }));

    /// <summary>The data of the sheet, for the colour of the deep layers.</summary>
    public static readonly DependencyProperty SheetDataProperty = DependencyProperty.Register(nameof(SheetData), typeof(JsonObject), typeof(WallInputGrid),
        new PropertyMetadata(null, (owner, _) => ((WallInputGrid)owner).RefreshMarks()));

    /// <summary>The height follows the rows as UpdateColumnDepths of the soil columns: at most 200, 72 plus 31 for each row.</summary>
    public static readonly DependencyProperty FitRowsProperty = DependencyProperty.Register(nameof(FitRows), typeof(bool), typeof(WallInputGrid),
        new PropertyMetadata(false, (owner, _) => ((WallInputGrid)owner).Fit()));

    /// <summary>UpdateColumnDepths of the historic view: the height of the soil columns, header and rows, at most 200.</summary>
    private const double FitMaximum = 200, FitHeader = 72, FitRow = 31;

    private bool syncing;

    public WallInputGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        CanUserSortColumns = false;
        AutoGenerateColumns = false;
        LoadingRow += (_, e) => Mark(e.Row);
    }

    public WallTableViewModel? Table { get => (WallTableViewModel?)GetValue(TableProperty); set => SetValue(TableProperty, value); }
    public IReadOnlyList<FieldSpec>? Fields { get => (IReadOnlyList<FieldSpec>?)GetValue(FieldsProperty); set => SetValue(FieldsProperty, value); }
    public JsonRowCollection? Rows { get => (JsonRowCollection?)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
    public string? ColumnWidths { get => (string?)GetValue(ColumnWidthsProperty); set => SetValue(ColumnWidthsProperty, value); }
    public bool Stretch { get => (bool)GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
    public Style? HeaderStyle { get => (Style?)GetValue(HeaderStyleProperty); set => SetValue(HeaderStyleProperty, value); }
    public WallRowMark RowMark { get => (WallRowMark)GetValue(RowMarkProperty); set => SetValue(RowMarkProperty, value); }
    public JsonObject? SheetData { get => (JsonObject?)GetValue(SheetDataProperty); set => SetValue(SheetDataProperty, value); }
    public bool FitRows { get => (bool)GetValue(FitRowsProperty); set => SetValue(FitRowsProperty, value); }

    private void Attach(WallTableViewModel? old, WallTableViewModel? table)
    {
        if (old is not null) { old.PropertyChanged -= OnTableChanged; old.Rows.CollectionChanged -= OnRowsChanged; }
        Build();
        if (table is not null) { table.PropertyChanged += OnTableChanged; table.Rows.CollectionChanged += OnRowsChanged; }
        ItemsSource = table?.Rows;
        SelectFromTable();
        Fit();
    }

    private void OnTableChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WallTableViewModel.SelectedIndex)) SelectFromTable();
        else if (e.PropertyName == nameof(WallTableViewModel.Columns)) Build();
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) { Fit(); if (RowMark == WallRowMark.LayerIndex) RefreshMarks(); }

    private void SelectFromTable()
    {
        syncing = true;
        try { SelectedIndex = Table?.SelectedIndex ?? -1; }
        finally { syncing = false; }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (syncing || Table is not { } table) return;
        // A selection lost because its row left the table is not a choice of the user: the table follows its rows by itself.
        if (SelectedIndex < 0 && e.RemovedItems.Cast<object>().Any(item => !table.Rows.Contains(item))) return;
        table.SelectedIndex = SelectedIndex;
        if (table.SelectedIndex != SelectedIndex) SelectFromTable();
    }

    private void Fit()
    {
        if (FitRows && Table is { } table) Height = Math.Min(FitMaximum, FitHeader + FitRow * table.Rows.Count);
    }

    /// <summary>The columns of JsonGrid on the fields of the table.</summary>
    private void Build()
    {
        Columns.Clear();
        var fields = Table?.Columns ?? Fields;
        if (fields is null) return;
        string[] widths = (ColumnWidths ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool repeat = widths.Length > 0 && widths[^1].EndsWith('…');
        if (repeat) widths[^1] = widths[^1].TrimEnd('…');
        for (int i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            var binding = new Binding($"[{f.Key}]") { Mode = f.ReadOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            DataGridColumn column;
            if (f.Bool) column = new DataGridCheckBoxColumn { Binding = binding };
            else if (f.Choices is not null) column = new DataGridComboBoxColumn { ItemsSource = f.Choices.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray(), SelectedItemBinding = binding };
            else column = new NumericDisplayColumn { Key = f.Key, Binding = binding, ElementStyle = Ui.NumericTextStyle(), EditingElementStyle = Ui.NumericTextStyle(true) };
            column.Header = f.Label; column.SortMemberPath = f.Key; column.IsReadOnly = f.ReadOnly; column.MinWidth = f.Bool ? 60 : 65;
            column.Width = Stretch ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(f.Choices is not null ? 140 : 112);
            string? spec = i < widths.Length ? widths[i] : repeat ? widths[^1] : null;
            if (spec is not null) Apply(column, spec);
            if (HeaderStyle is { } style) column.HeaderStyle = style;
            Columns.Add(column);
        }
    }

    private static void Apply(DataGridColumn column, string spec)
    {
        string[] parts = spec.Split('/');
        if (parts[0] != "-")
            column.Width = parts[0].EndsWith('*')
                ? new DataGridLength(double.Parse(parts[0].TrimEnd('*'), CultureInfo.InvariantCulture), DataGridLengthUnitType.Star)
                : new DataGridLength(double.Parse(parts[0], CultureInfo.InvariantCulture));
        if (parts.Length > 1) column.MinWidth = double.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    /// <summary>The colours of the rows shown: the deep layers change colour with their names (RefreshGlobalSoilDetails of the historic view).</summary>
    internal void RefreshMarks()
    {
        if (RowMark == WallRowMark.None) return;
        for (int i = 0; i < Items.Count; i++)
            if (ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row) Mark(row);
    }

    private void Mark(DataGridRow row)
    {
        int swatch = RowMark switch
        {
            WallRowMark.LayerIndex => row.GetIndex(),
            WallRowMark.GlobalLayer when SheetData is { } data && row.Item is JsonRowViewModel layer =>
                RetainingWallDrawing.LayerColors.IndexOf(RetainingWallDrawing.GlobalLayerColor(data, layer.Values)),
            _ => -1
        };
        if (swatch < 0) { row.ClearValue(BorderBrushProperty); row.ClearValue(BorderThicknessProperty); return; }
        row.SetResourceReference(BorderBrushProperty, "Token.Swatch.Wall" + (swatch % RetainingWallDrawing.LayerColors.Length + 1).ToString(CultureInfo.InvariantCulture));
        row.BorderThickness = new Thickness(5, 0, 0, 0);
    }
}
