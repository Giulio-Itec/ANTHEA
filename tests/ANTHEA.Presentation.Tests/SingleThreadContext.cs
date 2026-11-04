using System.Collections.Concurrent;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The UI thread of the checks: one thread runs every continuation in order, as the WPF dispatcher does, so that the state of
/// the ViewModels changes only there and the order of the checks is deterministic.
/// <para>
/// In lock step (<see cref="Run{T}"/>, <c>lockStep</c>) a background thread that posts to the UI thread waits until the UI thread has run
/// what it posted. This is the explicit point of synchronization of the traces that read the state right after a step that starts work on a
/// background thread (Bridge Design: the calculation at the opening, at the reopening and with «Calcola», the search of the optimum). Without
/// it, a busy machine can suspend the UI thread right after <c>Task.Run</c> long enough for the work to finish: the <c>await</c> of the
/// sheet then finds it completed and goes on synchronously, inside the step, and the trace reads a result that the historic view publishes
/// only later (4/11/2026, «ottimizzazione», «riapri»: «occupato sì» with the result already published). In lock step that work posts before
/// it completes (the end of the engine of the sheet, the first progress of the search) and stays there until the UI thread has left the step
/// and gone back to the queue; the order of the callbacks does not change. Since the same day the sheets themselves come back from their
/// background work on a later turn (BackgroundWork of ANTHEA.Application); lock step stays as a second barrier, and it still keeps a search
/// that reports its progress running until the step that started it ends (the RC section, «Ferma ricerca» right after «Calcola armature»).
/// </para>
/// </summary>
internal sealed class SingleThreadContext : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();
    private readonly bool lockStep;

    private SingleThreadContext(bool lockStep) => this.lockStep = lockStep;

    internal int ThreadId { get; } = Environment.CurrentManagedThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (!lockStep || Environment.CurrentManagedThreadId == ThreadId) { queue.Add((d, state)); return; }
        // Not disposed: after the deadline the callback still runs later and sets it.
        var done = new ManualResetEventSlim(false);
        queue.Add((s => { try { d(s); } finally { done.Set(); } }, state));
        // Bounded as the barriers of the checks: a UI thread that does not come back fails its own check, it does not hold this thread.
        done.Wait(Expect.Deadline);
    }

    public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException("Send sul thread dell'interfaccia delle prove.");

    /// <summary>
    /// Runs <paramref name="body"/> on the current thread with this context until its task completes; with <paramref name="lockStep"/> the
    /// threads that post to it wait until their callback has run.
    /// </summary>
    internal static T Run<T>(Func<SingleThreadContext, Task<T>> body, bool lockStep = false)
    {
        var previous = Current;
        var context = new SingleThreadContext(lockStep);
        SetSynchronizationContext(context);
        try
        {
            var task = body(context);
            task.ContinueWith(_ => context.queue.CompleteAdding(), TaskScheduler.Default);
            foreach (var (callback, state) in context.queue.GetConsumingEnumerable()) callback(state);
            return task.GetAwaiter().GetResult();
        }
        finally { SetSynchronizationContext(previous); }
    }
}
