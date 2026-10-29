using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace X.Desktop.Services;

/// <summary>Microsoft.Win32 dialogs with the typed filters of Presentation; the filter text is the same the views write today.</summary>
internal sealed class WpfFileDialogService(Func<Window?> owner) : IFileDialogService
{
    public string? OpenFile(OpenFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dialog = new OpenFileDialog { Filter = FileFilter.Join(request.Filters) };
        if (request.FileName is not null) dialog.FileName = request.FileName;
        if (request.Title is not null) dialog.Title = request.Title;
        return dialog.ShowDialog(owner()) == true ? dialog.FileName : null;
    }

    public string? SaveFile(SaveFileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dialog = new SaveFileDialog { Filter = FileFilter.Join(request.Filters), FileName = request.FileName, OverwritePrompt = request.OverwritePrompt };
        if (request.Title is not null) dialog.Title = request.Title;
        return dialog.ShowDialog(owner()) == true ? dialog.FileName : null;
    }

    public string? ChooseFolder(string? title = null)
    {
        var dialog = new OpenFolderDialog();
        if (title is not null) dialog.Title = title;
        return dialog.ShowDialog(owner()) == true ? dialog.FolderName : null;
    }
}

/// <summary>
/// Modal window around a dialog ViewModel: the view comes from the DataTemplate of the ViewModel type in the application
/// resources (decision Q3); the window closes when the ViewModel asks to.
/// </summary>
internal sealed class WpfDialogService(Func<Window?> owner) : IDialogService
{
    public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        var parent = owner();
        var window = new Window
        {
            Title = dialog.Title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false,
            Owner = parent, WindowStartupLocation = parent is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
        };
        if (parent is not null) window.SetValue(NumericPresentation.EnabledProperty, NumericPresentation.Enabled(parent));
        DisplayAdaptation.Attach(window);
        DialogSizing.Attach(window);
        Appearance.Watch(window);
        void Close(object? sender, EventArgs e) => window.Close();
        dialog.CloseRequested += Close;
        // F5.13 e: a dialog at work may refuse the closing by the user (the progress of the report of a section cancels instead); the others close as before.
        if (dialog is Anthea.Presentation.Shell.Commands.IGuardedDialog guarded) window.Closing += (_, e) => e.Cancel = !guarded.CanClose();
        try { window.ShowDialog(); }
        finally { dialog.CloseRequested -= Close; }
        return Task.FromResult(dialog.Result);
    }
}

/// <summary>
/// Size of the dialogs of <see cref="WpfDialogService"/> (F5.15, family 5, decision of 25/10): the window opens at the size of the
/// view of the dialog, then the view follows the window. The minimum is the MinWidth/MinHeight the view declares (the historic
/// minimum, for example the values of the retaining walls), otherwise the size the dialog opens with.
/// </summary>
internal static class DialogSizing
{
    internal static void Attach(Window window) => window.Loaded += (_, _) => Stretch(window);

    internal static void Stretch(Window window)
    {
        if (FindView(window, window.Content) is not { ActualWidth: > 0, ActualHeight: > 0 } view) return;
        double chromeWidth = window.ActualWidth - view.ActualWidth, chromeHeight = window.ActualHeight - view.ActualHeight;
        double width = view.MinWidth > 0 ? Math.Min(view.MinWidth, view.ActualWidth) : view.ActualWidth;
        double height = view.MinHeight > 0 ? Math.Min(view.MinHeight, view.ActualHeight) : view.ActualHeight;
        window.SizeToContent = SizeToContent.Manual;
        window.Width = window.ActualWidth;
        window.Height = window.ActualHeight;
        view.Width = double.NaN;
        view.Height = double.NaN;
        view.MinWidth = width;
        view.MinHeight = height;
        window.MinWidth = width + chromeWidth;
        window.MinHeight = height + chromeHeight;
        window.SetValue(DisplayAdaptation.MinimumSizeProperty, new Size(window.MinWidth, window.MinHeight));
    }

    // The view is the first child of the presenter of the content of the window (the DataTemplate of the ViewModel).
    private static FrameworkElement? FindView(DependencyObject parent, object? content)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ContentPresenter presenter && ReferenceEquals(presenter.Content, content))
                return VisualTreeHelper.GetChildrenCount(presenter) > 0 ? VisualTreeHelper.GetChild(presenter, 0) as FrameworkElement : null;
            if (FindView(child, content) is { } found) return found;
        }
        return null;
    }
}

/// <summary>Text of the system clipboard.</summary>
internal sealed class WpfClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Clipboard.SetText(text);
    }

    public string GetText() => Clipboard.ContainsText() ? Clipboard.GetText() : "";
}

/// <summary>Opens a link with the program of the system, as WikiEditorial and HorizontalDiagrams do today; a file URI (installed document) by its path.</summary>
internal sealed class WpfExternalLinkService : IExternalLinkService
{
    public void Open(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!target.IsAbsoluteUri) throw new ArgumentException("Collegamento non assoluto: " + target, nameof(target));
        Process.Start(new ProcessStartInfo(target.IsFile ? target.LocalPath : target.AbsoluteUri) { UseShellExecute = true })?.Dispose();
    }
}

/// <summary>
/// Opens the Wiki in the window of the shell that owns the active window (the Wiki tab), as WikiContextHelp.Open does. In the Release the shell replaces
/// this navigator with its own (ShellComposition.WithShellWiki); F5.13 g3: the branch of MainWindow is archived with it.
/// </summary>
internal sealed class WpfWikiNavigator(Func<Window?> owner) : IWikiNavigator
{
    public void Show(string? uri = null)
    {
        for (var window = owner(); window is not null; window = window.Owner)
            if (window is Views.Shell.ShellWindow shell) { shell.Shell.ShowWiki(uri); return; }
        if (Application.Current?.MainWindow is Views.Shell.ShellWindow fallback) fallback.Shell.ShowWiki(uri);
    }
}
