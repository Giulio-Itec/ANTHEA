using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.BridgeDesign;

namespace X.Desktop.Views.BridgeDesign;

/// <summary>
/// View of <see cref="PriceCatalogueViewModel"/> (F5.12): bindings only. The code-behind gives the two columns of the units their items (a column
/// is not in the tree and cannot bind to the editor) and commits the cell being edited before any button acts, as the historic window (Commit
/// before every command).
/// </summary>
public partial class PriceCatalogueView : UserControl
{
    public PriceCatalogueView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            var units = (DataContext as PriceCatalogueViewModel)?.Units;
            EntryUnits.ItemsSource = units; AnalysisUnits.ItemsSource = units;
        };
    }

    /// <summary>The three tables of the editor.</summary>
    internal IEnumerable<DataGrid> Grids => [MappingGrid, EntryGrid, AnalysisGrid];

    /// <summary>A button pressed: the cell and the row being edited are committed before its command runs (ButtonBase raises Click first).</summary>
    private void Commit_Click(object sender, RoutedEventArgs e)
    {
        foreach (var grid in Grids)
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }
}
