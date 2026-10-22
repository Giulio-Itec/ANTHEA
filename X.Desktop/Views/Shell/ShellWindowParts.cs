using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;

namespace X.Desktop.Views.Shell;

/// <summary>The header of a tab in the strip: the template of a sheet («•» not saved) or of a page (the Wiki, a model).</summary>
internal sealed class TabHeaderTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Sheet { get; set; }

    public DataTemplate? Page { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item is SheetTabViewModel ? Sheet : Page;
}

/// <summary>
/// Ctrl+Z and Ctrl+Y of the project in a window of the shell (F5.13 c after the checkpoint; ProjectWorkspace.PreviewKeyDown of MainWindow): with
/// Ctrl alone, outside a text box or a combo box (they keep their own undo), where <see cref="ShellViewModel.HistoryKeysApply"/> says so; the key
/// is then handled even with nothing to undo, as today.
/// </summary>
internal static class ProjectHistoryKeys
{
    /// <param name="shown">What the window shows: the centre of the main window, the tab of a detached window.</param>
    internal static void Attach(Window window, ShellViewModel shell, Func<object?> shown) =>
        window.PreviewKeyDown += (_, e) => { if (!e.Handled && Handle(shell, shown(), e.Key, Keyboard.Modifiers, Keyboard.FocusedElement)) e.Handled = true; };

    /// <summary>True when the key is a shortcut of the project there and ran (the checks call it with the modifiers they want).</summary>
    internal static bool Handle(ShellViewModel shell, object? shown, Key key, ModifierKeys modifiers, IInputElement? focused)
    {
        if (key is not (Key.Z or Key.Y) || modifiers != ModifierKeys.Control || focused is TextBoxBase or ComboBox || !shell.HistoryKeysApply(shown)) return false;
        shell.RestoreHistory(key == Key.Y);
        return true;
    }
}

/// <summary>
/// A detached window as the shell sees it (<see cref="IShellWindow"/>, F5.13 c): <see cref="ShellWindowTracker"/> keeps the rules (activation,
/// closing by the user or by the shell), this class only forwards to the WPF window.
/// </summary>
internal sealed class DetachedWindowHandle(DetachedShellWindow window) : IShellWindow
{
    internal DetachedShellWindow Window { get; } = window ?? throw new ArgumentNullException(nameof(window));

    public bool IsActive => Window.IsActive;

    public void Show() => Window.Show();

    public void Activate() => Window.Activate();

    public void Close() => Window.Close();

    public event EventHandler? Activated { add => Window.Activated += value; remove => Window.Activated -= value; }

    public event EventHandler? Closed { add => Window.Closed += value; remove => Window.Closed -= value; }
}
