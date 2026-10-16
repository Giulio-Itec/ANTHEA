using System.Collections.Concurrent;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The UI thread of the checks: one thread runs every continuation in order, as the WPF dispatcher does, so that the state of
/// the ViewModels changes only there and the order of the checks is deterministic.
/// </summary>
internal sealed class SingleThreadContext : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();

    internal int ThreadId { get; } = Environment.CurrentManagedThreadId;

    public override void Post(SendOrPostCallback d, object? state) => queue.Add((d, state));

    public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException("Send sul thread dell'interfaccia delle prove.");

    /// <summary>Runs <paramref name="body"/> on the current thread with this context until its task completes.</summary>
    internal static T Run<T>(Func<SingleThreadContext, Task<T>> body)
    {
        var previous = Current;
        var context = new SingleThreadContext();
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
