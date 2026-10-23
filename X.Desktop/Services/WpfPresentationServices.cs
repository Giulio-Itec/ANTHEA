using System.Diagnostics;
using System.Windows;
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

/// <summary>Opens a link with the program of the system, as WikiEditorial and HorizontalDiagrams do today.</summary>
internal sealed class WpfExternalLinkService : IExternalLinkService
{
    public void Open(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!target.IsAbsoluteUri) throw new ArgumentException("Collegamento non assoluto: " + target, nameof(target));
        Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true })?.Dispose();
    }
}

/// <summary>
/// Opens the Wiki in the main window that owns the active window, as WikiContextHelp.Open does today (same Safe wrapper).
/// The new shell (F5.13) will open a Wiki tab instead.
/// </summary>
internal sealed class WpfWikiNavigator(Func<Window?> owner) : IWikiNavigator
{
    public void Show(string? uri = null)
    {
        for (var window = owner(); window is not null; window = window.Owner)
            if (window is MainWindow main) { main.Safe(() => main.ShowWiki(uri)); return; }
        if (Application.Current?.MainWindow is MainWindow fallback) fallback.Safe(() => fallback.ShowWiki(uri));
    }
}
