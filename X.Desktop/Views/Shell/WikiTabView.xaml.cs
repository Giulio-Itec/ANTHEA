using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Shell.Navigation;

namespace X.Desktop.Views.Shell;

/// <summary>
/// DRAFT (F5.13 c): the view of <see cref="WikiTabViewModel"/>: the historic <see cref="WikiView"/> created on the ViewModel (a new view when the
/// tab moves to another window, proposal 4.1), on its last article, then on every article the shell asks.
/// </summary>
public partial class WikiTabView : UserControl
{
    private WikiTabViewModel? tab;
    private WikiView? wiki;

    public WikiTabView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as WikiTabViewModel);
        Unloaded += (_, _) => Attach(null);
        Loaded += (_, _) => Attach(DataContext as WikiTabViewModel);
    }

    /// <summary>The historic view of the Wiki, null before the ViewModel arrives.</summary>
    internal WikiView? Wiki => wiki;

    private void Attach(WikiTabViewModel? value)
    {
        if (ReferenceEquals(tab, value)) return;
        if (tab is not null) tab.NavigationRequested -= OnNavigationRequested;
        tab = value;
        wiki = null;
        Host.Content = null;
        if (tab is null) return;
        var current = tab;
        wiki = new WikiView((module, example) => current.OpenModule(module, example), returnToWork: current.ReturnToWork);
        Host.Content = wiki;
        tab.NavigationRequested += OnNavigationRequested;
        if (tab.Uri is { } uri) wiki.Navigate(uri);
    }

    private void OnNavigationRequested(object? sender, string uri) => wiki?.Navigate(uri);
}
