// Spostato da X.Calculations/ConcreteReinforcementDesign.Search.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: PreparedDesign e
// ProbeGroups, usati solo dalla ricerca precedente (H1). Codice storico conservato per le prove, fuori dall'installer.
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class LegacyConcreteReinforcementDesign
{
    private sealed record PreparedDesign(int Id, JsonObject Input, JsonObject Shear, JsonObject Detailing,
        ConcreteDesignMetrics Metrics, IReadOnlyList<ConcreteDesignCheck> Checks, double Score, string Key);

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
