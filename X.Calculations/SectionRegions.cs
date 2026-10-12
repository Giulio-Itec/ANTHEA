namespace Anthea.Calculations;

public sealed record ConcreteEffectiveRegion(string Name,double Qx,double Qy,double Level,double Area,
    int[] BarIndices,double SteelArea,double? Width,double[][] Outline,double[][][] Holes);
/// <summary>Polygon half-plane clipping, independent of native meshing and UI.</summary>
public static class SectionRegions
{
    public static double BarCover(SezioneCA section,Barra bar) => SectionGeometry.BarCover(section, bar);
    public static double[][] Clip(IReadOnlyList<double[]> polygon,double qx,double qy,double level)
        => GPC.Checkers.Concrete.Cracking.CrackSectionGeometry.Clip(polygon.Select(p => new GPC.Geometry.Point2d(p[0],p[1])).ToArray(),qx,qy,level)
            .Select(p => new[] { p.X,p.Y }).ToArray();
    public static double Area(IReadOnlyList<double[]> polygon)
        => SectionGeometry.Area(polygon);
    public static ConcreteEffectiveRegion Region(SezioneCA section,string name,double qx,double qy,double level,int[] bars,double? width=null)
    {
        var r=ConcreteLibraryMapping.CrackGeometry(section).Region(name,qx,qy,level,bars,width);
        return new(r.Key,r.Qx,r.Qy,r.Level,r.Area,r.BarIndices.ToArray(),r.SteelArea,r.Width,
            r.Outline.Select(p=>new[]{p.X,p.Y}).ToArray(),r.Holes.Select(h=>h.Select(p=>new[]{p.X,p.Y}).ToArray()).ToArray());
    }
}
