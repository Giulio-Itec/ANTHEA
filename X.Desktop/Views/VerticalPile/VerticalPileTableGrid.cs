using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Anthea.Presentation.Modules.VerticalPile;

namespace X.Desktop.Views.VerticalPile;

/// <summary>
/// A table of «Tabelle e dettagli» (DetailTable of the historic view): read-only, one column per column of the block (their number changes
/// from table to table, so the columns are built here), star widths, centred wrapping cells, headers on 12 points, at most 155 per column
/// and 1100 in all. The cells are the texts of the block, already formatted by the ViewModel. Presentation only.
/// </summary>
public sealed class VerticalPileTableGrid : DataGrid
{
    /// <summary>The block shown.</summary>
    public static readonly DependencyProperty BlockProperty = DependencyProperty.Register(nameof(Block), typeof(VerticalPileTableBlock), typeof(VerticalPileTableGrid),
        new PropertyMetadata(null, (owner, _) => ((VerticalPileTableGrid)owner).Build()));

    /// <summary>The widest column of DetailTable.</summary>
    private const double ColumnWidth = 155;

    public VerticalPileTableGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        IsReadOnly = true;
        MinHeight = 80;
        SetResourceReference(FontSizeProperty, "Font.Small");
    }

    public VerticalPileTableBlock? Block
    {
        get => (VerticalPileTableBlock?)GetValue(BlockProperty);
        set => SetValue(BlockProperty, value);
    }

    private void Build()
    {
        Columns.Clear();
        var block = Block;
        ItemsSource = block?.Rows;
        if (block is null) return;
        MaxWidth = Math.Min(1100, block.Columns.Count * ColumnWidth);
        var cell = new Style(typeof(TextBlock));
        cell.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        cell.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        cell.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
        for (int i = 0; i < block.Columns.Count; i++)
        {
            var header = new TextBlock { Text = block.Columns[i], FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
            header.SetResourceReference(TextBlock.FontSizeProperty, "Font.Small");
            Columns.Add(new DataGridTextColumn
            {
                Header = header, Binding = new Binding($"[{i}]") { Mode = BindingMode.OneWay }, ElementStyle = cell, MinWidth = 65,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }
    }
}
