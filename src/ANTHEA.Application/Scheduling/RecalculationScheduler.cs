namespace Anthea.Application.Scheduling;

/// <summary>Latest-request scheduling, cooperative cancellation and serial execution. Captures the caller's synchronization context, without referencing any UI framework.</summary>
public sealed class RecalculationScheduler : IDisposable
{
    private readonly object sync = new();
    private readonly SemaphoreSlim serial = new(1, 1);
    private CancellationTokenSource? active;
    private long revision;
    private bool disposed;

    public async Task<ScheduledCalculation<T>> ScheduleAsync<T>(Func<CancellationToken, Task<T>> work,
        TimeSpan delay = default, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
        CancellationTokenSource? prior;
        CancellationTokenSource source;
        long requested;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            prior = active; active = source = CancellationTokenSource.CreateLinkedTokenSource(cancellation); requested = ++revision;
        }
        Cancel(prior);
        bool entered = false;
        try
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay, source.Token);
            await serial.WaitAsync(source.Token); entered = true;
            source.Token.ThrowIfCancellationRequested();
            var value = await work(source.Token);
            source.Token.ThrowIfCancellationRequested();
            return new(this, requested, value, null, false);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested) { return new(this, requested, default, null, true); }
        catch (Exception error) { return new(this, requested, default, error, false); }
        finally
        {
            if (entered) serial.Release();
            lock (sync) { if (ReferenceEquals(active, source)) active = null; }
            source.Dispose();
        }
    }
    internal bool Publish<T>(ScheduledCalculation<T> result, Action<ScheduledCalculation<T>> publish)
    {
        lock (sync)
        {
            if (disposed || result.Revision != revision || result.Cancelled) return false;
            publish(result); return true;
        }
    }
    public void Invalidate()
    {
        CancellationTokenSource? source;
        lock (sync) { if (disposed) return; revision++; source = active; active = null; }
        Cancel(source);
    }
    private static void Cancel(CancellationTokenSource? source)
    {
        try { source?.Cancel(); } catch (ObjectDisposedException) { /* The completed request owns disposal. */ }
    }
    public void Dispose()
    {
        CancellationTokenSource? source;
        lock (sync) { if (disposed) return; disposed = true; revision++; source = active; active = null; }
        Cancel(source);
        // A request already inside work releases the semaphore when it actually finishes.
    }
}

public sealed class ScheduledCalculation<T>
{
    private readonly RecalculationScheduler scheduler;
    internal ScheduledCalculation(RecalculationScheduler scheduler, long revision, T? value, Exception? error, bool cancelled)
    { this.scheduler = scheduler; Revision = revision; Value = value; Error = error; Cancelled = cancelled; }
    internal long Revision { get; }
    public T? Value { get; }
    public Exception? Error { get; }
    public bool Cancelled { get; }
    public bool TryPublish(Action<ScheduledCalculation<T>> publish)
    { ArgumentNullException.ThrowIfNull(publish); return scheduler.Publish(this, publish); }
}
