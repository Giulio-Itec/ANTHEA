using Anthea.Application.Scheduling;

namespace Anthea.Presentation.Calculation;

/// <summary>
/// Limit of the calculations running at the same time in the application (proposal F5, section 4.6): one instance, owned by
/// the session of the application and shared by the sheets, never a static field. Sheets beyond the limit wait, still
/// cancellable, while the interface stays responsive. Since 1.16.0 (D-2, D-5) also the batches and the side calculations of the RC
/// section and the elastic response of the horizontal pile take a place (<see cref="Gate"/>, <see cref="RunAsync"/>): one place per
/// calculation, as a calculation of a module.
/// </summary>
public sealed class CalculationLimiter
{
    private readonly SemaphoreSlim gate;

    public CalculationLimiter(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1);
        Maximum = maximum;
        gate = new SemaphoreSlim(maximum, maximum);
    }

    /// <summary>Processors minus one, at least one: a processor stays free for the interface.</summary>
    public static int DefaultMaximum => Math.Max(1, Environment.ProcessorCount - 1);

    public int Maximum { get; }

    /// <summary>Waits for a free place; disposing the result frees it (once).</summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        return new Place(gate);
    }

    /// <summary>The limiter as the gate of the calculations of Application (<see cref="CalculationGate"/>): <see cref="EnterAsync"/>.</summary>
    public CalculationGate Gate => EnterAsync;

    /// <summary>
    /// Waits for a place, runs <paramref name="calculate"/> on a worker thread and frees the place when it ends
    /// (<see cref="CalculationGates.RunAsync"/>): the side calculations of a sheet, each in a place of its own.
    /// </summary>
    public Task<T> RunAsync<T>(Func<T> calculate, CancellationToken cancellation) => CalculationGates.RunAsync(Gate, calculate, cancellation);

    private sealed class Place(SemaphoreSlim gate) : IDisposable
    {
        private int released;
        public void Dispose() { if (Interlocked.Exchange(ref released, 1) == 0) gate.Release(); }
    }
}
