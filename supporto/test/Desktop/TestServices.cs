using System.Runtime.ExceptionServices;
using System.Windows;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>
/// Services of the WPF checks (UiTests configuration), registered by the harness through new MainWindow(TestServices.Create).
/// They reproduce the former 'testing' flag of MainWindow: no question on close, errors of Safe rethrown,
/// shared data kept in the current sheet, moves accepted; the checks can script the two answers.
/// </summary>
internal static class TestServices
{
    /// <summary>Factory of MainWindow: every window, including the copies opened by a check, gets its own services.</summary>
    internal static DesktopServices Create(Window owner) => new(new ScriptedConfirmations(), new RethrowingMessages());
}

internal sealed class ScriptedConfirmations : IConfirmationService
{
    /// <summary>Answer to the shared data question (formerly MainWindow.sharedChoiceForTest); null answers false, as the flag did.</summary>
    internal Func<bool>? SharedUpdate { get; set; }

    /// <summary>Answer to the move preview, with its changes (formerly MainWindow.projectMoveChoiceForTest); null answers true, as the flag did.</summary>
    internal Func<string[], bool>? ProjectMove { get; set; }

    public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => SharedUpdate?.Invoke() ?? false;

    public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => ProjectMove?.Invoke(changes.ToArray()) ?? true;

    /// <summary>Closes without committing the sheet and without asking to save, as the flag did.</summary>
    public bool ConfirmClose(Func<bool> saveOrDiscard) => true;
}

/// <summary>Safe rethrows the original exception (same type, message and stack), so that the check fails.</summary>
internal sealed class RethrowingMessages : IMessageService
{
    public void ReportFailure(string title, Exception error) => ExceptionDispatchInfo.Capture(error).Throw();
}

public sealed partial class MainWindow
{
    /// <summary>Scripted answers of this window, created with TestServices.Create.</summary>
    internal ScriptedConfirmations Scripted => (ScriptedConfirmations)services.Confirmations;
}
