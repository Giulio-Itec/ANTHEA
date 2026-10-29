namespace Anthea.Application.Scheduling;

/// <summary>
/// A place among the calculations that run at the same time in the application (1.16.0, decisions D-2 and D-5): waits for a free place,
/// cancellable, and returns it; disposing the result frees it (once). The limiter of the application (CalculationLimiter.EnterAsync of
/// ANTHEA.Presentation) is the one gate of the product: the calculations of Application that run on worker threads take a place when the
/// caller gives them the gate, and run without limit otherwise (the reports, the tests of the libraries).
/// </summary>
public delegate Task<IDisposable> CalculationGate(CancellationToken cancellation);

/// <summary>A calculation on a worker thread inside a place of a <see cref="CalculationGate"/>.</summary>
public static class CalculationGates
{
    /// <summary>
    /// Waits for a place of <paramref name="gate"/> (no wait without a gate), then runs <paramref name="calculate"/> on a worker thread
    /// (Task.Run, as without the gate) and frees the place when it ends, also with an error or a cancellation. The cancellation stops the
    /// wait too. The continuations keep the context of the caller: a request of the UI thread comes back to it, as an await of Task.Run.
    /// </summary>
    public static async Task<T> RunAsync<T>(CalculationGate? gate, Func<T> calculate, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(calculate);
        using var place = gate is null ? null : await gate(cancellation);
        return await Task.Run(calculate, cancellation);
    }
}
