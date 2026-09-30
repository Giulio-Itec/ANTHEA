using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class ConcreteReinforcementDesign
{
    private sealed record PreparedDesign(int Id, JsonObject Input, JsonObject Shear, JsonObject Detailing,
        ConcreteDesignMetrics Metrics, IReadOnlyList<ConcreteDesignCheck> Checks, double Score, string Key);

    // Timings are cumulative worker time, not additive wall time when workers overlap.
    private sealed class DesignTimers
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

    /// <summary>Probe feasible geometric extremes and intermediate reinforcement areas before completing the grid.
    /// These are priorities only: neither strength nor feasibility is assumed monotone.</summary>
    private static PreparedDesign[][] ProbeGroups(PreparedDesign[][] groups)
    {
        if (groups.Length <= 16) return groups;
        var chosen = new Dictionary<string, PreparedDesign[]>();
        void Add(PreparedDesign[] g) => chosen.TryAdd(g[0].Key, g);
        void Extremes(Func<PreparedDesign, double> value)
        {
            var ordered = groups.OrderBy(g => value(g[0])).ThenBy(g => g[0].Score).ThenBy(g => g[0].Id).ToArray();
            Add(ordered[0]); Add(ordered[^1]);
        }
        var byArea = groups.OrderBy(g => g[0].Metrics.SteelArea).ThenBy(g => g[0].Score).ThenBy(g => g[0].Id).ToArray();
        // Middle first: the largest arrangement is often excluded by congestion or a maximum steel ratio.
        foreach (double fraction in new[] { .5, 1, 0, .75, .25, .875, .625, .375, .125 }) Add(byArea[(int)(fraction * (byArea.Length - 1))]);
        Extremes(c => c.Metrics.Bars);
        Extremes(c => c.Input.D("top_bar_count")); Extremes(c => c.Input.D("bottom_bar_count"));
        Extremes(c => c.Input.D("side_bar_count_per_side"));
        Extremes(c => c.Input.D("top_bar_diameter_mm")); Extremes(c => c.Input.D("bottom_bar_diameter_mm"));
        return chosen.Values.ToArray();
    }
}
