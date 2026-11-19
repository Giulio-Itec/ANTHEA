using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Anthea.Presentation.Shell.Navigation;

namespace X.Desktop.Views.Shell;

/// <summary>
/// DRAFT (F5.13 c): the bar above the sheet with the focus, on <see cref="SheetBarViewModel"/>. The code-behind only opens the menu «Approfondimenti ▾»
/// built from <see cref="SheetBarViewModel.Topics"/> when the button is pressed, as MainWindow.ShowModuleWikiTopics builds it; every link runs
/// <see cref="SheetBarViewModel.OpenTopicCommand"/>. The detached windows open the same menu (<see cref="ShowTopics"/>, 19/11/2026).
/// </summary>
public partial class SheetBarView : UserControl
{
    public SheetBarView() => InitializeComponent();

    private void OnTopics(object sender, RoutedEventArgs e)
    {
        if (DataContext is SheetBarViewModel bar) ShowTopics(Topics, bar.Topics, bar.OpenTopicCommand);
    }

    /// <summary>The menu «Approfondimenti ▾» under <paramref name="target"/>: the items of <paramref name="topics"/>, every link through <paramref name="open"/>.</summary>
    internal static void ShowTopics(UIElement target, IReadOnlyList<WikiMenuItem> topics, ICommand open)
    {
        var menu = new ContextMenu { PlacementTarget = target };
        foreach (var item in topics) menu.Items.Add(Item(open, item));
        if (menu.Items.Count == 0) return;
        menu.IsOpen = true;
    }

    private static Control Item(ICommand open, WikiMenuItem item)
    {
        if (item.IsSeparator) return new Separator();
        var menuItem = new MenuItem { Header = item.Title };
        if (item.Uri is { } uri) { menuItem.Command = open; menuItem.CommandParameter = uri; }
        foreach (var child in item.Children) menuItem.Items.Add(Item(open, child));
        return menuItem;
    }
}
