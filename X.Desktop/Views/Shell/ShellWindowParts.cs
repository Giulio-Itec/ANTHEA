using System.Windows;
using System.Windows.Controls;
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
