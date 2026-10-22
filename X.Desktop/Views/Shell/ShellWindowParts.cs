using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;

namespace X.Desktop.Views.Shell;

/// <summary>
/// A window of the new shell where the historic views open the Wiki (F5.13 c, night of 22-23/10; R5): WikiContextHelp, used by F1 and the «?» labels of
/// SheetEditor, looks for MainWindow among the owners of the window and, failing it, for this; in the Release there is no such window.
/// </summary>
internal interface IShellWikiHost
{
    void ShowWiki(string uri);
}

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
/// The keys of the tree of the project in the new shell (F5.13 c after the checkpoint; ProjectHierarchy.ConfigureProjectNode and BeginProjectRename of
/// MainWindow), as attached properties set by the styles:
/// <list type="bullet">
/// <item><c>NodeKeys</c> on a node: F2 starts the rename in place, Enter opens the node (a sheet in its tab, a section in its overview), not from the
/// box of the rename;</item>
/// <item><c>RenameBox</c> on the box of the rename: it takes the focus with the name selected when it shows; Enter or the loss of the focus save,
/// Esc cancels (<see cref="ProjectNodeViewModel.EndRename"/>).</item>
/// </list>
/// </summary>
internal static class ProjectTreeKeys
{
    public static readonly DependencyProperty NodeKeysProperty =
        DependencyProperty.RegisterAttached("NodeKeys", typeof(bool), typeof(ProjectTreeKeys), new PropertyMetadata(false, OnNodeKeys));

    public static readonly DependencyProperty RenameBoxProperty =
        DependencyProperty.RegisterAttached("RenameBox", typeof(bool), typeof(ProjectTreeKeys), new PropertyMetadata(false, OnRenameBox));

    public static bool GetNodeKeys(DependencyObject element) => (bool)element.GetValue(NodeKeysProperty);

    public static void SetNodeKeys(DependencyObject element, bool value) => element.SetValue(NodeKeysProperty, value);

    public static bool GetRenameBox(DependencyObject element) => (bool)element.GetValue(RenameBoxProperty);

    public static void SetRenameBox(DependencyObject element, bool value) => element.SetValue(RenameBoxProperty, value);

    private static void OnNodeKeys(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TreeViewItem item) return;
        item.KeyDown -= OnNodeKeyDown;
        if (e.NewValue is true) item.KeyDown += OnNodeKeyDown;
    }

    /// <summary>The innermost node handles the key; its ancestors see it handled.</summary>
    private static void OnNodeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.OriginalSource is TextBoxBase || sender is not FrameworkElement { DataContext: ProjectNodeViewModel node }) return;
        var command = e.Key switch { Key.F2 => node.BeginRenameCommand, Key.Enter => node.OpenCommand, _ => null };
        if (command is null) return;
        e.Handled = true;
        if (command.CanExecute(null)) command.Execute(null);
    }

    private static void OnRenameBox(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox box || e.NewValue is not true) return;
        box.IsVisibleChanged += (_, _) =>
        {
            if (box.IsVisible) box.Dispatcher.InvokeAsync(() => { box.Focus(); box.SelectAll(); }, System.Windows.Threading.DispatcherPriority.Input);
        };
        box.KeyDown += (_, args) =>
        {
            if (args.Key is not (Key.Enter or Key.Escape) || box.DataContext is not ProjectNodeViewModel node) return;
            args.Handled = true;
            node.EndRename(args.Key == Key.Enter);
        };
        box.LostKeyboardFocus += (_, _) => { if (box.DataContext is ProjectNodeViewModel { IsRenaming: true } node) node.EndRename(true); };
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
