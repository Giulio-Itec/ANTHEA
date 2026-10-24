using System.Runtime.ExceptionServices;
using System.Windows;
using X.Desktop.Services;

namespace X.Desktop;

// F5.13 g3: TestServices (the services of the WPF checks in MainWindow, F1.4) is archived with MainWindow in supporto/SUPERATI/ui-storica/shell;
// the scripted questions and messages below are those of the bench of the shell and of the sheets (BenchSheetServices).

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

    /// <summary>Answer to the save question (F5.1, not asked through this service yet); null fails the check, as an unexpected question.</summary>
    internal Func<SaveChangesAnswer>? SaveChanges { get; set; }

    /// <summary>Answer to a removal question, with its caption and text (F5.1, not asked through this service yet); null fails the check.</summary>
    internal Func<string, string, bool>? Delete { get; set; }

    public SaveChangesAnswer AskSaveChanges() => SaveChanges?.Invoke() ?? throw new InvalidOperationException("Domanda di salvataggio non prevista dalla prova.");

    public bool ConfirmDelete(string title, string question) => Delete?.Invoke(title, question) ?? throw new InvalidOperationException("Domanda di eliminazione non prevista dalla prova: " + question);
}

/// <summary>Safe rethrows the original exception (same type, message and stack), so that the check fails.</summary>
internal sealed class RethrowingMessages : IMessageService
{
    public void ReportFailure(string title, Exception error) => ExceptionDispatchInfo.Capture(error).Throw();

    /// <summary>Notes and warnings shown through the service (F5.1, no call site yet), kept for the checks instead of a message box.</summary>
    internal List<(string Title, string Message, bool Warning)> Shown { get; } = [];

    public void Inform(string title, string message) => Shown.Add((title, message, false));

    public void Warn(string title, string message) => Shown.Add((title, message, true));
}
