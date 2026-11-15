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
    /// <summary>A button of the command bar with a menu (the menu Fase, step 2 of the project of 16/11): the click opens it below the button, on the
    /// ViewModel of the sheet; its items are the commands of the sheet.</summary>
    private void OpenMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.DataContext = button.DataContext;
        menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
    }
    /// <summary>A box of the command bar (Scavo, Falda, Sovraccarico, step 3) confirms its value with Invio as when it loses the focus.</summary>
    private void BarBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }
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
