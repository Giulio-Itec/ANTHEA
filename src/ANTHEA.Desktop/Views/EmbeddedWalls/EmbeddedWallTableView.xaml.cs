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
                Header = column.Label + (column.Unit.Length == 0 ? "" : " [" + column.Unit + "]"), Binding = new Binding("[" + column.Key + "]"), MinWidth = 75 });
        };
    }
}
