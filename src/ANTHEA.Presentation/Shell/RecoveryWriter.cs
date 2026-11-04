using Anthea.Projects;

namespace Anthea.Presentation.Shell;

/// <summary>
/// The recovery copies of the open documents (docs/sviluppo/progetto-sistema.md, step 2e, decision D2 of 3/11/2026). Every
/// <see cref="Interval"/>, on the thread of the interface, each document changed since its last copy is frozen (a project through the
/// history of the undo, a few milliseconds) and written on a thread of the pool; a document saved or closed loses its copy at once. The
/// copies of an ANTHEA that ended badly are offered at the next start (<see cref="ShellViewModel.Start"/>).
/// </summary>
public sealed class RecoveryWriter : IDisposable
{
    /// <summary>Seconds between two passes.</summary>
    public const int IntervalSeconds = 60;

    /// <summary>Time between two passes.</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(IntervalSeconds);

    private sealed class Copy(RecoverySlot slot)
    {
        public RecoverySlot Slot { get; } = slot;
        public long Changes { get; set; } = -1;
        public Task Pending { get; set; } = Task.CompletedTask;
    }

    private readonly RecoveryStore store;
    private readonly WorkspaceSession workspace;
    private readonly Dictionary<DocumentSession, Copy> copies = [];
    private readonly HashSet<DocumentSession> watched = [];
    private readonly Timer? timer;
    private bool disposed;

    /// <param name="store">Where the copies go.</param>
    /// <param name="workspace">The open documents.</param>
    /// <param name="automatic">True to write every <see cref="Interval"/> through the synchronization context of the caller (the thread of
    /// the interface); false for the checks, which call <see cref="WriteNow"/>.</param>
    public RecoveryWriter(RecoveryStore store, WorkspaceSession workspace, bool automatic)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        workspace.DocumentsChanged += OnDocumentsChanged;
        Watch();
        if (automatic && SynchronizationContext.Current is { } ui)
            timer = new Timer(_ => ui.Post(_ => { if (!disposed) _ = WriteNow(); }, null), null, Interval, Interval);
    }

    /// <summary>The last write that failed (full disk, folder not writable), null when none: the copy is a help, not a duty, so the user is
    /// not interrupted.</summary>
    public Exception? LastFailure { get; private set; }

    /// <summary>The copy of <paramref name="document"/>, null when it has none.</summary>
    public RecoverySlot? SlotOf(DocumentSession document) => copies.TryGetValue(document, out var copy) ? copy.Slot : null;

    /// <summary>
    /// One pass, on the thread of the interface: a copy for each document changed since its last copy (frozen now, written on a thread of
    /// the pool); the documents saved or closed lose theirs.
    /// </summary>
    /// <returns>The writes started, completed when they end.</returns>
    public Task WriteNow()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Forget();
        var pending = new List<Task>();
        foreach (var document in workspace.Documents)
        {
            if (!document.IsDirty) continue;
            if (!copies.TryGetValue(document, out var copy))
            {
                if (store.Create() is not { } slot) continue;
                copies[document] = copy = new Copy(slot);
            }
            if (copy.Changes == document.Changes) continue;
            copy.Changes = document.Changes;
            var frozen = document is ProjectSession project ? project.History.Snapshot(project.WorkingDocument) : FrozenDocument.Of(document.WorkingDocument);
            var info = new RecoveryInfo(document.Path, Title(document), DateTimeOffset.Now, document.Stamp, document is ProjectSession);
            var results = (document as ProjectSession)?.Results;
            var slotToWrite = copy.Slot;
            copy.Pending = copy.Pending.ContinueWith(_ =>
            {
                try { slotToWrite.Write(frozen, info, results); }
                catch (Exception error) { LastFailure = error; }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            pending.Add(copy.Pending);
        }
        return Task.WhenAll(pending);
    }

    /// <summary>The name of the document for the question at the start.</summary>
    public static string Title(DocumentSession document) => document.Path is { } path ? Path.GetFileName(path)
        : document is ProjectSession ? "Nuovo progetto" : "Nuovo documento";

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer?.Dispose();
        workspace.DocumentsChanged -= OnDocumentsChanged;
        foreach (var document in watched) document.StateChanged -= OnStateChanged;
        watched.Clear();
        // The application closes: each copy is deleted after the write running, if any (the slot waits for it), so none is left behind.
        foreach (var copy in copies.Values) copy.Slot.Delete();
        copies.Clear();
    }

    private void OnDocumentsChanged(object? sender, EventArgs e) { Watch(); Forget(); }

    private void OnStateChanged(object? sender, EventArgs e) { if (sender is DocumentSession { IsDirty: false } document) Forget(document); }

    private void Watch()
    {
        foreach (var document in workspace.Documents)
            if (watched.Add(document)) document.StateChanged += OnStateChanged;
    }

    /// <summary>The copies of the documents closed or saved are deleted.</summary>
    private void Forget()
    {
        var open = workspace.Documents.ToHashSet();
        foreach (var document in copies.Keys.Where(d => !open.Contains(d) || !d.IsDirty).ToArray()) Forget(document);
        foreach (var document in watched.Where(d => !open.Contains(d)).ToArray()) { document.StateChanged -= OnStateChanged; watched.Remove(document); }
    }

    private void Forget(DocumentSession document)
    {
        if (!copies.Remove(document, out var copy)) return;
        Delete(copy);
    }

    /// <summary>The copy is deleted now when no write is running, otherwise after it (the interface does not wait).</summary>
    private static void Delete(Copy copy)
    {
        if (copy.Pending.IsCompleted) copy.Slot.Delete();
        else _ = copy.Pending.ContinueWith(_ => copy.Slot.Delete(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }
}
