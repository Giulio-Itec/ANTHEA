using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Anthea.Presentation.Modules.EmbeddedWalls;
namespace X.Desktop.Views.EmbeddedWalls;
public partial class EmbeddedWallSheetView : UserControl
{
    public EmbeddedWallSheetView() => InitializeComponent();
    private void WorkTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is EmbeddedWallSheetViewModel sheet && e.NewValue is EmbeddedWallWorkNode node)
            sheet.SelectWorkNodeCommand.Execute(node);
        e.Handled = true;
    }
    private void WorkTreeItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // A click on the current item must also return from a results tab to its editor.
        // The expander only opens/closes its branch.
        DependencyObject? current = e.OriginalSource as DependencyObject;
        while (current is not null && current is not TreeViewItem)
        {
            if (current is ToggleButton) return;
            current = current is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(current);
        }
        if (current is TreeViewItem { DataContext: EmbeddedWallWorkNode node } && DataContext is EmbeddedWallSheetViewModel sheet)
            sheet.SelectWorkNodeCommand.Execute(node);
    }
    private void WorkTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TreeView { SelectedItem: EmbeddedWallWorkNode node }
            && DataContext is EmbeddedWallSheetViewModel sheet)
        {
            sheet.SelectWorkNodeCommand.Execute(node);
            e.Handled = true;
        }
    }
}
