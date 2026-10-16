// Spostato da X.Calculations/SectionRegions.cs (commit 8b9ce6d3) con la decisione E5 del 15/10/2026 (F2.11c): Clip, Area e Region non
// hanno chiamanti di produzione; le usano i motori storici della fessurazione, CalculationLibrary.Checks e ANTHEA.ApiReuse.Checks.
// BarCover e il record ConcreteEffectiveRegion restano nel prodotto. Corpi invariati.
namespace Anthea.Calculations;

/// <summary>Parte di <see cref="SectionRegions"/> usata solo dalle prove (classe statica: nome nuovo, corpi invariati).
/// Polygon half-plane clipping, independent of native meshing and UI.</summary>
public static class LegacySectionRegions
{
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
