using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;

namespace X.Desktop.Views.Shell;

/// <summary>
/// DRAFT of the main window of the new shell (F5.13 c, for the checkpoint on the layout): NOT the window of the application, which stays
/// MainWindow. The code-behind only wires what XAML cannot express: the shortcuts of the menu «File» (KeyBinding on the commands of the
/// ViewModel; Esc on the exit from the full page), the reading of the appearance when its menu opens, the activation and the closing of the window towards the tracker of the
/// detached windows (the closing asks the questions of the coordinator first), the requests of the commands that only a window can satisfy
/// (exit, the rheology tool window).
/// </summary>
public partial class ShellWindow : Window
{
    /// <param name="shell">The shell, built by the composition root (in the draft by the capture of the checkpoint).</param>
    /// <param name="tracker">The detached windows of <paramref name="shell"/>, already attached to it.</param>
    internal ShellWindow(ShellViewModel shell, ShellWindowTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(tracker);
        Shell = shell;
        InitializeComponent();
        DataContext = shell;
        foreach (var item in shell.Commands.File)
            if (item is { Command: { } command, Shortcut: { } shortcut }) InputBindings.Add(new KeyBinding(command, ShortcutTextConverter.Gesture(shortcut)));
        // Esc leaves the full page; outside it the command cannot run and the key goes on unhandled (the views and the rename keep their Esc).
        InputBindings.Add(new KeyBinding(shell.ExitFullPageCommand, Key.Escape, ModifierKeys.None));
        DisplayAdaptation.Attach(this);
        Appearance.Watch(this);
        Activated += (_, _) => tracker.MainActivated();
        Closing += OnClosing;
        Closed += (_, _) => tracker.CloseAll();
        shell.Commands.ExitRequested += (_, _) => Close();
        shell.Commands.RheologyRequested += (_, _) => ConcreteTimeEffectsView.CreateWindow(this).Show();
    }

    /// <summary>The ViewModel of the window.</summary>
    internal ShellViewModel Shell { get; }

    private void OnClosing(object? sender, CancelEventArgs e) => e.Cancel = !Shell.ConfirmClose();

    private void OnAppearanceOpened(object sender, RoutedEventArgs e) => Shell.Appearance?.Refresh();
}
