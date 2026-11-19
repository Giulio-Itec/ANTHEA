using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;
using X.Desktop.Views.Shell;

namespace X.Desktop.Services;

/// <summary>
/// ANTHEA started on a module alone (19/11/2026, «--modulo», the shortcut of the module on the taskbar): the shell is the usual one, its main
/// window stays hidden and the sheet opens in a window of its own with the icon of the module.
/// <list type="bullet">
/// <item>Closing the window of the module closes ANTHEA, with the questions to save of the main window (<see cref="ShellViewModel.ConfirmClose"/>):
/// «Annulla» leaves the window open.</item>
/// <item>When something needs the main window (a tab brought back with «Riaggancia», a page) it shows itself, and from then on ANTHEA works as
/// usual; the main window shows itself also when the module could not open (the message of the error is in front of it).</item>
/// </list>
/// </summary>
internal sealed class ModuleWindowMode(string module)
{
    private ShellWindow? main;
    private bool closing;

    /// <summary>The module of the window.</summary>
    internal string Module { get; } = module;

    /// <summary>A detached window of the shell, with the icon of the module and the closing of ANTHEA.</summary>
    internal IShellWindow Wrap(DetachedShellWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.Icon = ModuleIdentity.WindowIcon(Module);
        window.Closing += OnClosing;
        return new DetachedWindowHandle(window);
    }

    /// <summary>Opens the module without showing the main window of <paramref name="parts"/>.</summary>
    internal void Start(ShellParts parts, StartupRequest request)
    {
        ArgumentNullException.ThrowIfNull(parts);
        main = parts.Window;
        main.Closing += (_, e) => closing = !e.Cancel;
        parts.Shell.Start(request);
        if (parts.Tracker.Hosts.Count == 0) { main.Show(); return; }
        parts.Shell.Windows.Main.Tabs.CollectionChanged += OnMainTabs;
    }

    private void OnMainTabs(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (main is { IsVisible: false } window && e.Action == NotifyCollectionChangedAction.Add) window.Show();
    }

    /// <summary>
    /// The user closes the window of the module (it still has its tabs): ANTHEA closes. A window closed by the shell, left without tabs (its tab
    /// brought back to the main window), just closes.
    /// </summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing || main is null || main.IsVisible || sender is not FrameworkElement { DataContext: TabHostViewModel { Tabs.Count: > 0 } }) return;
        e.Cancel = true;
        main.Dispatcher.BeginInvoke(main.Close);
    }
}
