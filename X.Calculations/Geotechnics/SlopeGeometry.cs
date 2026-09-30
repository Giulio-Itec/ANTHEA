using GPC.Geometry;

namespace Anthea.Calculations.Geotechnics;

/// <summary>Geometry only. Candidate for migration to GPC.Geometry; no strengths or equilibrium.</summary>
public static class SlopeGeometry
{
    public static double Height(IReadOnlyList<SlopePoint> line, double x)
    {
        if (line.Count < 2 || x < line[0].X - 1e-8 || x > line[^1].X + 1e-8) throw new ArgumentException("Profilo non esteso all’ascissa richiesta.");
        for (int i = 1; i < line.Count; i++)
            if (line[i].X > line[i - 1].X && x <= line[i].X + 1e-10)
                return line[i - 1].Y + (line[i].Y - line[i - 1].Y) * (x - line[i - 1].X) / (line[i].X - line[i - 1].X);
        return line[^1].Y;
    }

    public static (double Area, SlopePoint Centroid) Properties(SlopePoint[] points)
    {
        var polygon = new Polygon2d(points.Select(p => new Point2d(p.X, p.Y)));
        var center = polygon.GetCentroid();
        return (Math.Abs(polygon.GetSignedArea()), new(center.X, center.Y));
    }

    // Circle through entry/exit with its lowest point at the requested elevation.
    public static SlipCircle? Through(SlopePoint a, SlopePoint b, double bottom)
    {
        double ha = a.Y - bottom, hb = b.Y - bottom;
        if (ha <= 0 || hb <= 0 || b.X <= a.X) return null;
        double r0 = Math.Max(ha, hb), r1 = Math.Max(1, r0 * 2);
        double Span(double r) => Math.Sqrt(Math.Max(0, 2 * r * ha - ha * ha)) + Math.Sqrt(Math.Max(0, 2 * r * hb - hb * hb));
        if (Span(r0) > b.X - a.X + 1e-10) return null;
        while (Span(r1) < b.X - a.X && r1 < 1e6) r1 *= 2;
        for (int i = 0; i < 65; i++) { double r = (r0 + r1) / 2; if (Span(r) < b.X - a.X) r0 = r; else r1 = r; }
        double radius = (r0 + r1) / 2;
        var circle = new SlipCircle(a.X + Math.Sqrt(2 * radius * ha - ha * ha), bottom + radius, radius, a.X, b.X);
        // The arc remains single-valued even when an endpoint has a vertical tangent.
        // Slice bases are evaluated strictly inside each interval, away from that tangent.
        return circle.Y < Math.Max(a.Y, b.Y) - 1e-10 ? null : circle;
    }

    /// <summary>Boundary of the lower-arc family: vertical tangent at the higher entry.</summary>
    public static SlipCircle? TangentAtEntry(SlopePoint exit, SlopePoint entry)
    {
        double dx = entry.X - exit.X, dy = entry.Y - exit.Y;
        if (dx <= 0 || dy < 0 || dy >= dx) return null;
        double radius = (dx * dx + dy * dy) / (2 * dx);
        return new(entry.X - radius, entry.Y, radius, exit.X, entry.X);
    }

    public static IEnumerable<double> Crossings(SlipCircle c, double y)
    {
        if (y > c.Y || y < c.Y - c.Radius) yield break;
        double dx = Math.Sqrt(Math.Max(0, c.Radius * c.Radius - (c.Y - y) * (c.Y - y)));
        foreach (double x in new[] { c.X - dx, c.X + dx }) if (x > c.Left + 1e-8 && x < c.Right - 1e-8) yield return x;
    }

    public static (double Bottom, double Top)? VerticalInterval(SlopePoint[] polygon, double x)
    {
        var hits = new List<double>();
        for (int i = 0; i < polygon.Length; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
            if (x >= Math.Min(a.X, b.X) && x < Math.Max(a.X, b.X)) hits.Add(a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X));
        }
        if (hits.Count == 0) return null;
        if (hits.Count != 2) throw new ArgumentException("Corpo rigido non semplice rispetto alla verticale.");
        return (hits.Min(), hits.Max());
    }

    public static double[] Divisions(SlopeSection section, SlipCircle circle, int count)
    {
        var x = Enumerable.Range(0, count + 1).Select(i => circle.Left + (circle.Right - circle.Left) * i / count)
            .Concat(section.Surface.Select(p => p.X)).Concat(section.Water.Select(p => p.X))
            .Concat(section.Bodies.SelectMany(b => b.Polygon.Select(p => p.X)))
            .Concat(section.SoilColumns.SelectMany(l => l).SelectMany(s => Crossings(circle, s.Bottom)))
            .Concat(section.ValleySoils.Length > 0 ? new[] { section.SoilSplitX } : Array.Empty<double>())
            .Concat(section.Loads.Where(l => l.Distributed).SelectMany(l => new[] { l.Left, l.Right }))
            .Where(v => v >= circle.Left && v <= circle.Right).Order().ToArray();
        return x.Aggregate(new List<double>(), (a, v) => { if (a.Count == 0 || v - a[^1] > 1e-7) a.Add(v); return a; }).ToArray();
    }

    public static bool Admissible(SlopeSection section, SlipCircle circle)
    {
        if (circle.Left >= section.RequiredLeft || circle.Right <= section.RequiredRight || circle.Y - circle.Radius < section.CoveredBottom) return false;
        // Difference between a line and the convex lower circular arc is concave: minima are at segment ends.
        foreach (var p in section.Surface.Where(p => p.X > circle.Left && p.X < circle.Right)) if (circle.Base(p.X) > p.Y - 1e-7) return false;
        foreach (var body in section.Bodies)
            foreach (var p in body.Polygon) if (circle.Base(p.X) >= p.Y - 1e-6) return false;
        return true;
    }
}
