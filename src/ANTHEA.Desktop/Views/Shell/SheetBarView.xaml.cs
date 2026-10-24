using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Shell.Navigation;

namespace X.Desktop.Views.Shell;

/// <summary>
/// DRAFT (F5.13 c): the bar above the sheet with the focus, on <see cref="SheetBarViewModel"/>. The code-behind only opens the menu «Approfondimenti ▾»
/// built from <see cref="SheetBarViewModel.Topics"/> when the button is pressed, as MainWindow.ShowModuleWikiTopics builds it; every link runs
/// <see cref="SheetBarViewModel.OpenTopicCommand"/>.
/// </summary>
public partial class SheetBarView : UserControl
{
    public SheetBarView() => InitializeComponent();

    private void OnTopics(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SheetBarViewModel bar) return;
        var menu = new ContextMenu { PlacementTarget = Topics };
        foreach (var item in bar.Topics) menu.Items.Add(Item(bar, item));
        if (menu.Items.Count == 0) return;
        menu.IsOpen = true;
    }

    private static Control Item(SheetBarViewModel bar, WikiMenuItem item)
    {
        if (item.IsSeparator) return new Separator();
        var menuItem = new MenuItem { Header = item.Title };
        if (item.Uri is { } uri) { menuItem.Command = bar.OpenTopicCommand; menuItem.CommandParameter = uri; }
        foreach (var child in item.Children) menuItem.Items.Add(Item(bar, child));
        return menuItem;
    }
}
