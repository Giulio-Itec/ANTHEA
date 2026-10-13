namespace Anthea.Application.Modules;

public sealed record PileProgress(string Phase, int Completed = 0, int Total = 0)
{
    internal static IProgress<Anthea.Calculations.PileCalculationProgress>? Adapt(IProgress<PileProgress>? target)
        => target is null ? null : new Forward(target);
    // Do not capture a second context: the caller owns progress dispatch.
    private sealed class Forward(IProgress<PileProgress> target) : IProgress<Anthea.Calculations.PileCalculationProgress>
    {
        public void Report(Anthea.Calculations.PileCalculationProgress value)
            => target.Report(new(value.Phase, value.Completed, value.Total));
    }
}
