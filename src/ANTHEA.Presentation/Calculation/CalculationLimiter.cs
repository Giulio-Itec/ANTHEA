namespace Anthea.Presentation.Calculation;

/// <summary>
/// Limit of the calculations running at the same time in the application (proposal F5, section 4.6): one instance, owned by
/// the session of the application and shared by the sheets, never a static field. Sheets beyond the limit wait, still
/// cancellable, while the interface stays responsive.
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

    private sealed class Place(SemaphoreSlim gate) : IDisposable
    {
        private int released;
        public void Dispose() { if (Interlocked.Exchange(ref released, 1) == 0) gate.Release(); }
    }
}
