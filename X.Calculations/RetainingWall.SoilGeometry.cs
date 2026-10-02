using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    /// <summary>
    /// Ground line of the global profile for the drawing before a calculation (m): the profile in front, the face of the stem, the profile behind.
    /// The calculation uses the surface of the library (WallGlobalStability.Surface).
    /// </summary>
    public static Geotechnics.SlopePoint[] GlobalSurface(JsonObject d, Geotechnics.SlopePoint[] valley, Geotechnics.SlopePoint[] uphill)
    {
        var g = d["geometry"]!; double t = g.D("slab"), h = g.D("height"), a = g.D("toe"), s = g.D("stem_base"), top = g.D("stem_top"), dv = ValleyHeight(d), y = Math.Max(t, dv);
        return valley.Concat(new[] { new Geotechnics.SlopePoint(0, y), new Geotechnics.SlopePoint(a + (s - top) * (y - t) / h, y), new Geotechnics.SlopePoint(a + s - top, h + t) }).Concat(uphill).Distinct().ToArray();
    }
    public sealed record SoilMass(double Weight, double MomentX, double MomentY);
}
