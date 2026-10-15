using System.Diagnostics;

namespace Anthea.Calculations;

public static partial class ConcreteReinforcementDesign
{
    // Timings are cumulative worker time, not additive wall time when workers overlap.
    internal sealed class DesignTimers
    {
        private readonly string[] names = ["Geometria e parametri", "Dettagli costruttivi", "Ancoraggi", "Taglio e torsione", "Resistenza e SLE"];
        private readonly long[] ticks = new long[5];
        private readonly int[] calls = new int[5];
        public T Measure<T>(int stage, Func<T> calculate)
        {
            long start = Stopwatch.GetTimestamp();
            try { return calculate(); }
            finally { Interlocked.Add(ref ticks[stage], Stopwatch.GetTimestamp() - start); Interlocked.Increment(ref calls[stage]); }
        }
        public void Measure(int stage, Action calculate) => Measure(stage, () => { calculate(); return 0; });
        public ConcreteDesignStage[] Results() => names.Select((name, i) => new ConcreteDesignStage(name, calls[i], (double)ticks[i] / Stopwatch.Frequency)).ToArray();
    }
}
