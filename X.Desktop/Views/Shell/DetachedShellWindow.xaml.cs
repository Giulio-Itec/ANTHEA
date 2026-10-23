using System.ComponentModel;
using System.Windows;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;

namespace X.Desktop.Views.Shell;

/// <summary>
/// DRAFT (F5.13 c): a detached window of the shell on a <see cref="TabHostViewModel"/>, created by the composition through
/// <see cref="DetachedWindowHandle"/> for <see cref="ShellWindowTracker"/>. Its title is the tab on screen; the commands of its tabs are those of
/// <see cref="ShellWindowsViewModel"/>. Not owned by the main window, so that it can stay on another monitor (question for the checkpoint).
/// </summary>
public partial class DetachedShellWindow : Window, IShellWikiHost
{
    private readonly TabHostViewModel host;
    private readonly ShellViewModel? shell;

    /// <param name="shell">The shell, for Ctrl+Z and Ctrl+Y of the project on the tab on screen (after the checkpoint of 22/10); null without them.</param>
    internal DetachedShellWindow(TabHostViewModel host, ShellWindowsViewModel windows, ShellViewModel? shell = null)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        Windows = windows ?? throw new ArgumentNullException(nameof(windows));
        this.shell = shell;
        InitializeComponent();
        DataContext = host;
        if (shell is not null) ProjectHistoryKeys.Attach(this, shell, () => host.Selected);
        DisplayAdaptation.Attach(this);
        Appearance.Watch(this);
        host.PropertyChanged += OnHostChanged;
        Closed += (_, _) => host.PropertyChanged -= OnHostChanged;
        UpdateTitle();
    }

    /// <summary>The windows and tabs of the shell: the commands of the tabs.</summary>
    public ShellWindowsViewModel Windows { get; }

    /// <summary>F1 and the «?» of a historic sheet in this window (R5, WikiContextHelp): the Wiki tab of the shell, in the main window.</summary>
    void IShellWikiHost.ShowWiki(string uri) => shell?.ShowWikiFromView(uri);

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabHostViewModel.Selected)) UpdateTitle();
    }

    private void UpdateTitle() => Title = host.Selected is { } tab ? tab.Title + " — ANTHEA" : "ANTHEA";
}
