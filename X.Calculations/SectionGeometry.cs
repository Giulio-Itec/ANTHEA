using GPC.Geometry;

namespace Anthea.Calculations;

/// <summary>JSON/view coordinates are adapters; native topology and distances use GPC.Geometry.</summary>
public static class SectionGeometry
{
    public static Polygon2d Polygon(IEnumerable<double[]> points) => new(points.Select(p => new Point2d(p[0], p[1])));
    public static Shape2d Shape(SezioneCA section) => new(Polygon(section.Outline), section.Holes.Select(Polygon).ToArray());
    public static double Area(IEnumerable<double[]> points) => Math.Abs(Polygon(points).GetSignedArea());
    public static double BarCover(SezioneCA section, Barra bar)
        => SignedCover(Polygon(section.Outline), section.Holes.Select(Polygon), new Point2d(bar.X, bar.Y), bar.Diametro / 2);
    public static double SignedCover(Polygon2d outline, IEnumerable<Polygon2d> holes, Point2d point, double radius)
    {
        double clearance = outline.DistanceTo(point);
        bool inside = outline.IsPointInside(point, 0);
        foreach (var hole in holes)
        {
            clearance = Math.Min(clearance, hole.DistanceTo(point));
            if (hole.IsPointInside(point, 0)) inside = false;
        }
        return (inside ? clearance : -clearance) - radius;
    }
}
