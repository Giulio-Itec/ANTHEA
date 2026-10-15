// Spostato da X.Calculations/TensionBarSpacing.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: codice morto (D2),
// TensionBarSpacing.Maximum usa sempre la libreria. Codice storico conservato per le prove, fuori dall'installer.
namespace Anthea.Calculations;

// Unchanged circular branch from 76e7916. API-02: equivalence fails at radius rounding boundaries.
// This preserves the previous result; it is not an independent or corrected spacing algorithm.
internal static class LegacyCircularSpacing
{
    internal static double? Maximum(SezioneCA section, IReadOnlyList<int> tensileBarIndices)
    {
        var selected = tensileBarIndices.ToHashSet();
        if (selected.Count < 2) return null;
        var distances = new List<double>();
        var radii = section.Bars.Select(b => double.Hypot(b.X, b.Y)).ToArray();
        if (radii.Max() - radii.Min() > 1e-4 && (section.Input.ContainsKey("barre_manuali") || !section.Input.B("second_inner_enabled"))) return null;
        foreach (var group in section.Bars.Select((b, i) => (Bar: b, Index: i, Angle: Math.Atan2(b.Y, b.X))).GroupBy(b => Math.Round(radii[b.Index], 5)))
        {
            var ring = group.OrderBy(b => b.Angle).ToArray(); double radius = group.Average(b => radii[b.Index]);
            for (int i = 0; i < ring.Length; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % ring.Length];
                if (!selected.Contains(a.Index) || !selected.Contains(b.Index)) continue;
                double angle = b.Angle - a.Angle; if (angle <= 0) angle += 2 * Math.PI;
                distances.Add(radius * angle);
            }
        }
        return distances.Count > 0 ? distances.Max() : null;
    }
}
