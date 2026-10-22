using System.ComponentModel;
using System.Windows;
using Anthea.Presentation.Shell.Tabs;

namespace X.Desktop.Views.Shell;

/// <summary>
/// DRAFT (F5.13 c): a detached window of the shell on a <see cref="TabHostViewModel"/>, created by the composition through
/// <see cref="DetachedWindowHandle"/> for <see cref="ShellWindowTracker"/>. Its title is the tab on screen; the commands of its tabs are those of
/// <see cref="ShellWindowsViewModel"/>. Not owned by the main window, so that it can stay on another monitor (question for the checkpoint).
/// </summary>
public partial class DetachedShellWindow : Window
{
    private readonly TabHostViewModel host;

    internal DetachedShellWindow(TabHostViewModel host, ShellWindowsViewModel windows)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        Windows = windows ?? throw new ArgumentNullException(nameof(windows));
        InitializeComponent();
        DataContext = host;
        DisplayAdaptation.Attach(this);
        Appearance.Watch(this);
        host.PropertyChanged += OnHostChanged;
        Closed += (_, _) => host.PropertyChanged -= OnHostChanged;
        UpdateTitle();
    }

    /// <summary>The windows and tabs of the shell: the commands of the tabs.</summary>
    public ShellWindowsViewModel Windows { get; }

    private void OnHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabHostViewModel.Selected)) UpdateTitle();
    }

    private void UpdateTitle() => Title = host.Selected is { } tab ? tab.Title + " — ANTHEA" : "ANTHEA";
}
