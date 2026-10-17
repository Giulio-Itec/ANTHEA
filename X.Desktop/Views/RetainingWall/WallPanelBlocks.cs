using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Anthea.Presentation.Modules.RetainingWall;

namespace X.Desktop.Views.RetainingWall;

/// <summary>
/// The template of a block of a panel of results (<see cref="WallPanelBlock"/>, F5.9): a text, a table, a button or a group, the plain data
/// of the historic WPF panels (Ui.Text, Ui.Table, Ui.Button, the expanders of Group). The templates are resources of the view
/// (WallBlockText, WallBlockTable, WallBlockButton, WallBlockGroup), found when a block is shown: the group holds the same selector.
/// </summary>
public sealed class WallBlockTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not WallPanelBlock block || container is not FrameworkElement element) return null;
        string key = block.Kind switch
        {
            WallBlockKind.Table => "WallBlockTable",
            WallBlockKind.Button => "WallBlockButton",
            WallBlockKind.Group => "WallBlockGroup",
            _ => "WallBlockText"
        };
        return (DataTemplate)element.FindResource(key);
    }
}

/// <summary>
/// A table of a panel of results (Ui.Table of the historic view, as RetainingWallWorkspace.Table sizes it): read-only, one text column for
/// each column of the block, automatic widths with a minimum of 65, numbers aligned as Ui.NumericTextStyle, the height of the block when it
/// has one. The cells are the texts of the block, already formatted by the ViewModel. Presentation only: the view sends the selection of a
/// row to the sheet according to <see cref="WallPanelBlock.RowAction"/>.
/// </summary>
public sealed class WallBlockGrid : DataGrid
{
    public static readonly DependencyProperty BlockProperty = DependencyProperty.Register(nameof(Block), typeof(WallPanelBlock), typeof(WallBlockGrid),
        new PropertyMetadata(null, (owner, _) => ((WallBlockGrid)owner).Build()));

    public WallBlockGrid()
    {
        SetResourceReference(StyleProperty, typeof(DataGrid));
        IsReadOnly = true;
        AutoGenerateColumns = false;
    }

    public WallPanelBlock? Block { get => (WallPanelBlock?)GetValue(BlockProperty); set => SetValue(BlockProperty, value); }

    private void Build()
    {
        Columns.Clear();
        var block = Block;
        ItemsSource = block?.Rows;
        if (block?.Columns is not { } columns) return;
        for (int i = 0; i < columns.Count; i++)
            Columns.Add(new DataGridTextColumn
            {
                Header = columns[i], Binding = new Binding($"[{i}]") { Mode = BindingMode.OneWay }, ElementStyle = Ui.NumericTextStyle(), MinWidth = 65,
                Width = DataGridLength.Auto
            });
        if (double.IsNaN(block.Height)) ClearValue(HeightProperty); else Height = block.Height;
    }
}
