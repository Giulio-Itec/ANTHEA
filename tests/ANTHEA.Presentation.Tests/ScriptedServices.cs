using Anthea.Presentation.Services;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Scripted implementations of the services (proposal F5, section 3.3): every answer is queued by the check, every call is
/// recorded, and a question without a scripted answer fails, so that a ViewModel cannot ask more than the check expects.
/// </summary>
internal static class Scripted
{
    internal static PresentationServices Services(ScriptedMessages? messages = null, ScriptedConfirmations? confirmations = null,
        ScriptedFileDialogs? files = null, ScriptedDialogs? dialogs = null)
        => new(messages ?? new(), confirmations ?? new(), files ?? new(), dialogs ?? new(), new RecordingClipboard(), new RecordingLinks(), new RecordingWiki());
}

internal sealed class ScriptedMessages : IMessageService
{
    internal List<(string Title, Exception Error)> Failures { get; } = [];
    internal List<(string Title, string Message, bool Warning)> Shown { get; } = [];
    /// <summary>When set, ReportFailure throws it: a failing message service must not be swallowed by the guard.</summary>
    internal Exception? FailWith { get; set; }

    public void ReportFailure(string title, Exception error)
    {
        if (FailWith is not null) throw FailWith;
        Failures.Add((title, error));
    }

    public void Inform(string title, string message) => Shown.Add((title, message, false));
    public void Warn(string title, string message) => Shown.Add((title, message, true));
}

internal sealed class ScriptedConfirmations : IConfirmationService
{
    private readonly Queue<object> answers = new();
    internal List<string> Asked { get; } = [];

    internal ScriptedConfirmations Then(object answer) { answers.Enqueue(answer); return this; }

    private T Next<T>(string question)
    {
        Asked.Add(question);
        if (answers.Count == 0) throw new InvalidOperationException("Domanda non prevista: " + question);
        return answers.Dequeue() is T answer ? answer : throw new InvalidOperationException("Risposta di tipo errato per: " + question);
    }

    public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => Next<bool>("dati condivisi: " + prompt.SectionName);
    public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => Next<bool>("spostamento: " + destination);
    public bool ConfirmClose(Func<bool> saveOrDiscard) => Next<bool>("chiusura") && saveOrDiscard();
    public SaveChangesAnswer AskSaveChanges() => Next<SaveChangesAnswer>("salvataggio");
    public bool ConfirmDelete(string title, string question) => Next<bool>(title + ": " + question);
}

internal sealed class ScriptedFileDialogs : IFileDialogService
{
    private readonly Queue<string?> paths = new();
    internal List<object> Requests { get; } = [];

    internal ScriptedFileDialogs Then(string? path) { paths.Enqueue(path); return this; }

    private string? Next(object request)
    {
        Requests.Add(request);
        return paths.Count == 0 ? throw new InvalidOperationException("Dialogo di file non previsto: " + request) : paths.Dequeue();
    }

    public string? OpenFile(OpenFileRequest request) => Next(request);
    public string? SaveFile(SaveFileRequest request) => Next(request);
    public string? ChooseFolder(string? title = null) => Next("cartella: " + title);
}

/// <summary>Each scripted step acts on the dialog ViewModel (sets fields, runs its commands) as the user would in the window.</summary>
internal sealed class ScriptedDialogs : IDialogService
{
    private readonly Queue<Action<object>> steps = new();
    internal List<object> Shown { get; } = [];

    internal ScriptedDialogs Then(Action<object> step) { steps.Enqueue(step); return this; }

    public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
    {
        Shown.Add(dialog);
        if (steps.Count == 0) throw new InvalidOperationException("Dialogo non previsto: " + dialog.Title);
        bool closed = false;
        void Close(object? sender, EventArgs e) => closed = true;
        dialog.CloseRequested += Close;
        try { steps.Dequeue()(dialog); }
        finally { dialog.CloseRequested -= Close; }
        return Task.FromResult(closed ? dialog.Result : default);
    }
}

internal sealed class RecordingClipboard : IClipboardService
{
    private string text = "";
    public void SetText(string value) => text = value ?? throw new ArgumentNullException(nameof(value));
    public string GetText() => text;
}

internal sealed class RecordingLinks : IExternalLinkService
{
    internal List<Uri> Opened { get; } = [];
    public void Open(Uri target) => Opened.Add(target);
}

internal sealed class RecordingWiki : IWikiNavigator
{
    internal List<string?> Shown { get; } = [];
    public void Show(string? uri = null) => Shown.Add(uri);
}
