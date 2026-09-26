namespace Anthea.Calculations;

/// <summary>Geometry-only contract, independent of WPF and Checker results, ready to move into Checker.</summary>
public interface ITensionBarSpacing
{
    double? Maximum(SezioneCA section, IReadOnlyList<int> tensileBarIndices);
}

public sealed class TensionBarSpacing : ITensionBarSpacing
{
    public double? Maximum(SezioneCA section, IReadOnlyList<int> tensileBarIndices)
    {
        var selected = tensileBarIndices.ToHashSet();
        if (selected.Count < 2) return null;
        var distances = new List<double>();
        if (section.Shape == "Circolare")
        {
            var radii = section.Bars.Select(b => double.Hypot(b.X, b.Y)).ToArray();
            if (radii.Max() - radii.Min() > 1e-4 && (section.Input.ContainsKey("barre_manuali") || !section.Input.B("second_inner_enabled"))) return null;
            // Wizard concentric rings: consider adjacent bars on each ring, never bridge between rings.
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
        }
        else
        {
            // Group collinear reinforcement rows, including corners on each face.
            // Never bridge across the concave shoulders of a T section.
            foreach (bool horizontal in new[] { true, false })
            {
                var rows = section.Bars.Select((b, i) => (Bar: b, Index: i))
                    .GroupBy(b => Math.Round(horizontal ? b.Bar.Y : b.Bar.X, 6));
                foreach (var row in rows)
                {
                    var ordered = row.OrderBy(b => horizontal ? b.Bar.X : b.Bar.Y).ToArray();
                    for (int i = 1; i < ordered.Length; i++)
                    {
                        var a = ordered[i - 1]; var b = ordered[i];
                        if (!selected.Contains(a.Index) || !selected.Contains(b.Index)) continue;
                        if (!Inside(section, (a.Bar.X + b.Bar.X) / 2, (a.Bar.Y + b.Bar.Y) / 2)) continue;
                        distances.Add(double.Hypot(b.Bar.X - a.Bar.X, b.Bar.Y - a.Bar.Y));
                    }
                }
            }
        }
        return distances.Count > 0 ? distances.Max() : null;
    }

    private static bool Inside(SezioneCA section, double x, double y)
    {
        foreach(var hole in section.Holes)
        {
            bool inHole=false;
            for(int i=0,j=hole.Length-1;i<hole.Length;j=i++)if((hole[i][1]>y)!=(hole[j][1]>y)&&x<(hole[j][0]-hole[i][0])*(y-hole[i][1])/(hole[j][1]-hole[i][1])+hole[i][0])inHole=!inHole;
            if(inHole)return false;
        }
        bool inside = false; var outline = section.Outline;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            var a = outline[j]; var b = outline[i];
            if ((a[1] > y) != (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]) inside = !inside;
        }
        return inside;
    }
}
