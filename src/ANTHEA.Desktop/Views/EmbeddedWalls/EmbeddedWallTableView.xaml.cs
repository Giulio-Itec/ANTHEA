using System.Windows.Controls;
using System.Windows.Data;
using Anthea.Presentation.Modules.EmbeddedWalls;
namespace X.Desktop.Views.EmbeddedWalls;
public partial class EmbeddedWallTableView : UserControl
{
    public EmbeddedWallTableView()
    {
        InitializeComponent(); DataContextChanged += (_, _) =>
        {
            RowsGrid.Columns.Clear();
            if (DataContext is not EmbeddedWallTable table) return;
            foreach (var column in table.Columns) RowsGrid.Columns.Add(new DataGridTextColumn {
                Header = column.Label + (column.Unit.Length == 0 ? "" : " [" + column.Unit + "]"), MinWidth = 75,
                // Cifre significative (16/11): a choice written in English shows its Italian label, a permeability in m/s a power of ten; the cell
                // keeps the value. Other numbers as typed.
                Binding = new Binding("[" + column.Key + "]") { Converter = column.Choices is null && column.Unit != "m/s" ? null : new EmbeddedWallFieldConverter(column) } });
        };
    }
}
