using System.Text.Json.Nodes;

namespace X.Core;

public sealed record BridgeSectionProperties(string Name, double Area, double X, double Y, double Ix, double Iy, double Top, double Bottom, double Width)
{
    public double? WTop => Top > Y ? Ix / (Top - Y) : null;
    public double? WBottom => Y > Bottom ? Ix / (Y - Bottom) : null;
    public double Rx => Math.Sqrt(Ix / Area);
    public double Ry => Math.Sqrt(Iy / Area);
}

public static partial class BridgeSection
{
    public static BridgeSectionProperties RectangleProperties(string name, double width, double height, double bottom, double x) =>
        new(name, width * height, x, bottom + height / 2, width * Math.Pow(height, 3) / 12, height * Math.Pow(width, 3) / 12, bottom + height, bottom, width);

    public static BridgeSectionProperties CombineProperties(string name, IEnumerable<BridgeSectionProperties> source)
    {
        var p = source.Where(p => p.Area > 0).ToArray(); double area = p.Sum(p => p.Area);
        double x = p.Sum(p => p.Area * p.X) / area, y = p.Sum(p => p.Area * p.Y) / area;
        return new(name, area, x, y, p.Sum(p => p.Ix + p.Area * Math.Pow(p.Y - y, 2)), p.Sum(p => p.Iy + p.Area * Math.Pow(p.X - x, 2)),
            p.Max(p => p.Top), p.Min(p => p.Bottom), p.Max(p => p.Width));
    }
    public static List<BridgeSectionProperties> SteelPartProperties(BridgeGeometry g, bool equivalent = false)
    {
        var p = new List<BridgeSectionProperties> {
            RectangleProperties("Piattabanda superiore", g.TopWidth, g.TopThickness, -g.TopThickness, g.Width / 2),
            RectangleProperties("Anima", g.WebThickness, g.WebHeight, -g.TopThickness - g.WebHeight, g.Width / 2) };
        if (equivalent) p.Add(RectangleProperties("Piattabanda inferiore equivalente", g.BottomEquivalentWidth, g.BottomEquivalentThickness, -g.Height, g.Width / 2));
        else
        {
            p.Add(RectangleProperties("Piattabanda inferiore 1", g.Bottom1Width, g.Bottom1Thickness, -g.TopThickness - g.WebHeight - g.Bottom1Thickness, g.Width / 2));
            if (g.Bottom2Thickness > 0) p.Add(RectangleProperties("Piattabanda inferiore 2", g.Bottom2Width, g.Bottom2Thickness, -g.Height, g.Width / 2));
        }
        return p;
    }
    public static BridgeSectionProperties GrossPhaseProperties(JsonObject data, JsonObject phase, bool slabOnly = false)
    {
        var g = Geometry(data); var m = Materials(data); string kind = phase.S("tipo");
        if (slabOnly || HasConcrete(kind))
        {
            var section = NativeSection(data); if (slabOnly) section.SteelSections.Clear();
            var h = Homogenization(data, phase);
            // Model returns zeros for a concrete-only section without bars or structural steel.
            if (slabOnly && g.Bars.Length == 0) return RectangleProperties("Soletta omogeneizzata al CLS", g.Width, g.SlabHeight, 0, g.Width / 2);
            var props = section.GetHomogeneizedMechanicalProperties(h.PhiEffective); double factor = slabOnly ? 1 : h.N;
            return new(slabOnly ? "Soletta omogeneizzata al CLS" : "Sezione omogeneizzata all’acciaio", props.areaH / factor, props.centroidH.X, props.centroidH.Y,
                props.JxxH / factor, props.JyyH / factor, g.SlabHeight, slabOnly ? 0 : -g.Height, g.Width);
        }
        var pieces = SteelPartProperties(g, true);
        if (kind == "Soletta esclusa")
            pieces.AddRange(g.Bars.Select(b => new BridgeSectionProperties(b.Id, b.Area * m.Rebar.ElasticModulusTension / m.Steel.ElasticModulusTension,
                b.X, b.Y, Math.PI * Math.Pow(b.Diameter, 4) / 64 * m.Rebar.ElasticModulusTension / m.Steel.ElasticModulusTension,
                Math.PI * Math.Pow(b.Diameter, 4) / 64 * m.Rebar.ElasticModulusTension / m.Steel.ElasticModulusTension, b.Y + b.Diameter / 2, b.Y - b.Diameter / 2, b.Diameter)));
        return CombineProperties(kind, pieces);
    }
}
