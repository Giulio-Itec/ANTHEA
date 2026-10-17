using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// A table of the horizontal pile (CenteredTable of the historic view: the verification and the tables of «Diagrammi e dettagli»): read-only,
/// one column per heading (their number changes from table to table, so the columns are built here), star widths with a minimum of 65,
/// centred wrapping cells and headings, 13 points, at most <see cref="TableWidth"/> wide and centred. The cells are the texts of the
/// ViewModel, already formatted. Presentation only.
/// </summary>
public sealed class HorizontalPileTableGrid : DataGrid
{
    /// <summary>The headings of the columns.</summary>
    public static readonly DependencyProperty HeadingsProperty = DependencyProperty.Register(nameof(Headings), typeof(IReadOnlyList<string>), typeof(HorizontalPileTableGrid),
        new PropertyMetadata(null, (owner, _) => ((HorizontalPileTableGrid)owner).Build()));

    /// <summary>The widest table (650 for the verification, 920 for the results, as CenteredTable).</summary>
    public static readonly DependencyProperty TableWidthProperty = DependencyProperty.Register(nameof(TableWidth), typeof(double), typeof(HorizontalPileTableGrid),
        new PropertyMetadata(920d, (owner, e) => ((HorizontalPileTableGrid)owner).MaxWidth = (double)e.NewValue));

    public HorizontalPileTableGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        IsReadOnly = true;
        HorizontalAlignment = HorizontalAlignment.Center;
        MaxWidth = 920;
        SetResourceReference(FontSizeProperty, "Font.Body");
    }

    public IReadOnlyList<string>? Headings
    {
        get => (IReadOnlyList<string>?)GetValue(HeadingsProperty);
        set => SetValue(HeadingsProperty, value);
    }

    public double TableWidth
    {
        get => (double)GetValue(TableWidthProperty);
        set => SetValue(TableWidthProperty, value);
    }

    private void Build()
    {
        Columns.Clear();
        if (Headings is not { } headings) return;
        var cell = new Style(typeof(TextBlock));
        cell.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        cell.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
        cell.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        // On every column: the implicit header style of the theme wins over DataGrid.ColumnHeaderStyle.
        var header = new Style(typeof(DataGridColumnHeader), TryFindResource(typeof(DataGridColumnHeader)) as Style);
        header.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        for (int i = 0; i < headings.Count; i++)
            Columns.Add(new DataGridTextColumn
            {
                Header = headings[i], HeaderStyle = header, Binding = new Binding($"[{i}]") { Mode = BindingMode.OneWay }, ElementStyle = cell, MinWidth = 65,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
    }
}
