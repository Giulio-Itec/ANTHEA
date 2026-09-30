using System.Text.Json.Nodes;

namespace Anthea.Calculations;

// Geometry kept separate from the soil law and equilibrium, ready for extraction to GPC.
public static partial class RetainingWall
{
    public static Geotechnics.SlopePoint[] GlobalSurface(JsonObject d, Geotechnics.SlopePoint[] valley, Geotechnics.SlopePoint[] uphill)
    {
        var g = d["geometry"]!; double t = g.D("slab"), h = g.D("height"), a = g.D("toe"), s = g.D("stem_base"), top = g.D("stem_top"), dv = ValleyHeight(d), y = Math.Max(t, dv);
        return valley.Concat(new[] { new Geotechnics.SlopePoint(0, y), new Geotechnics.SlopePoint(a + (s - top) * (y - t) / h, y), new Geotechnics.SlopePoint(a + s - top, h + t) }).Concat(uphill).Distinct().ToArray();
    }
    public sealed record SoilMass(double Weight, double MomentX, double MomentY);
    public static SoilMass FrontSoilMass(JsonObject d, IEnumerable<ValleyBand> bands, double above = 0, bool wedgeOnly = false)
    {
        var g = d["geometry"]!; double a = g.D("toe"), t = g.D("slab"), slope = (g.D("stem_base") - g.D("stem_top")) / g.D("height");
        double w = 0, mx = 0, my = 0;
        foreach (var band in bands)
        {
            double low = Math.Max(Math.Max(t, above), band.Bottom), high = band.Top; if (high <= low) continue;
            double left = wedgeOnly ? a : 0, x0 = a + slope * (low - t), x1 = a + slope * (high - t);
            if (Math.Max(x0, x1) <= left) continue;
            var polygon = SectionGeometry.Polygon([[left, low], [x0, low], [x1, high], [left, high]]);
            double weight = Math.Abs(polygon.GetSignedArea()) * band.Gamma; var c = polygon.GetCentroid();
            w += weight; mx += weight * c.X; my += weight * c.Y;
        }
        return new(w, mx, my);
    }
}
