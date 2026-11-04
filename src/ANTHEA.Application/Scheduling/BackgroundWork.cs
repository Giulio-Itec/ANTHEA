namespace Anthea.Application.Scheduling;

/// <summary>
/// The work of a sheet on a worker thread (Task.Run), that comes back to its caller always on a later turn of the caller's synchronization
/// context (the UI thread), also when the work has already ended at the await (decision of the user of 4/11/2026: the sheets always publish on
/// a later turn of the UI thread).
/// <para>
/// A plain <c>await Task.Run(...)</c> goes on at once, inside the request that started the work, when the work has already ended at the await:
/// a busy machine that suspends the UI thread right after Task.Run, or a result from a cache. The sheet then publishes inside the step of the
/// user, before what the work posted to the same thread (the end of the engine, «occupato»), and shows states that the historic views, which
/// queue their work, never show: Bridge Design «occupato» with the result already published (Presentation.Tests, 4/11/2026), the vertical pile
/// exportable right after «Calcola» on data already calculated, the quick resistances of the RC section already calculated at the opening. Here
/// the continuation is always queued on the context, after what the work queued before it ended; when the work is still running nothing
/// changes. Without a context (a worker thread) the caller goes on on the thread pool.
/// </para>
/// </summary>
public static class BackgroundWork
{
    private const ConfigureAwaitOptions LaterTurn = ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.ForceYielding;

    /// <summary>Runs <paramref name="work"/> on a worker thread, as Task.Run; the caller goes on on a later turn of its context.</summary>
    public static async Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellation = default) =>
        await Task.Run(work, cancellation).ConfigureAwait(LaterTurn);

    /// <summary>Runs <paramref name="work"/> on a worker thread, as Task.Run; the caller goes on on a later turn of its context.</summary>
    public static async Task RunAsync(Action work, CancellationToken cancellation = default) =>
        await Task.Run(work, cancellation).ConfigureAwait(LaterTurn);
}
