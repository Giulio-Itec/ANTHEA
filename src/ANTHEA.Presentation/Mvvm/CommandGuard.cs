using Anthea.Presentation.Services;

namespace Anthea.Presentation.Mvvm;

/// <summary>
/// Error policy of the commands (decision Q19 of 16/10/2026), the equivalent of MainWindow.Safe: an error of a command is shown
/// in the screen through <see cref="IMessageService.ReportFailure"/> with the title «Operazione non completata» and the text of
/// the exception; it is not written to errori.log, which keeps recording only the unhandled errors (F1.4). An error of the
/// message service itself is not caught: it becomes unhandled and reaches the log, as today.
/// Cancellation: an <see cref="OperationCanceledException"/> is silent only when the token of the command was cancelled
/// (cancel command, sheet closed); any other one is reported like every error, as MainWindow.Safe does.
/// </summary>
public sealed class CommandGuard
{
    /// <summary>Title of the failure message, the same of MainWindow.Safe.</summary>
    public const string FailureTitle = "Operazione non completata";

    private readonly IMessageService messages;

    public CommandGuard(IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        this.messages = messages;
    }

    /// <summary>Runs a synchronous command; false when it failed (the error has been shown).</summary>
    public bool Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try { action(); return true; }
        catch (Exception error) { messages.ReportFailure(FailureTitle, error); return false; }
    }

    /// <summary>Runs an asynchronous command; false when it failed (shown) or was cancelled through <paramref name="cancellation"/> (silent).</summary>
    public async Task<bool> RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        try { await action(cancellation); return true; }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
        catch (Exception error) { messages.ReportFailure(FailureTitle, error); return false; }
    }
}
