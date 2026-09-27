using GPC.Geometry;

namespace Anthea.Calculations;

public static class ReinforcementGeometry
{
    public static double Area(double diameter, int count = 1)
    {
        if (!double.IsFinite(diameter) || diameter <= 0 || count < 1) throw new ArgumentException("Diametro e numero barre devono essere positivi.");
        return count * new Circle2d(new Point2d(0, 0), diameter / 2).Area;
    }
    public static double EquivalentDiameter(double area)
    {
        if (!double.IsFinite(area) || area <= 0) throw new ArgumentException("Area metallica deve essere finita e positiva.");
        return 2 * Math.Sqrt(area / Math.PI);
    }
}
