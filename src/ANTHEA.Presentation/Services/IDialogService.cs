using Anthea.Presentation.Mvvm;

namespace Anthea.Presentation.Services;

/// <summary>
/// Modal windows of a module (proposal F5, section 3.3): soil transfer, report options, custom materials, rename… The view
/// is found by the type of the ViewModel (DataTemplate in X.Desktop). Production: WpfDialogService.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows the dialog and completes with its result when it closes; default when the user cancels.</summary>
    Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog);
}

/// <summary>ViewModel shown by <see cref="IDialogService"/>: caption, result and the request to close.</summary>
public interface IDialogViewModel<out TResult>
{
    string Title { get; }
    TResult? Result { get; }
    event EventHandler? CloseRequested;
}

/// <summary>Base of the dialog ViewModels: <see cref="Close"/> sets the result and asks the host to close the window.</summary>
public abstract class DialogViewModel<TResult> : ViewModelBase, IDialogViewModel<TResult>
{
    private TResult? result;

    protected DialogViewModel(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        Title = title;
    }

    public string Title { get; }

    public TResult? Result { get => result; private set => SetProperty(ref result, value); }

    public event EventHandler? CloseRequested;

    /// <summary>Confirms the dialog with <paramref name="value"/>.</summary>
    protected void Close(TResult? value)
    {
        Result = value;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Closes without a result, as the Cancel button or the closing of the window.</summary>
    protected void Cancel() => Close(default);
}
