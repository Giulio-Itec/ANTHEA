namespace Anthea.Calculations;

public sealed record ConcreteEffectiveRegion(string Name,double Qx,double Qy,double Level,double Area,
    int[] BarIndices,double SteelArea,double? Width,double[][] Outline,double[][][] Holes);
/// <summary>Copriferro di una barra rispetto al contorno e ai fori. Clip, Area e Region, usate solo dalle prove, sono in
/// tests/ANTHEA.Legacy (LegacySectionRegions) dalla decisione E5 del 15/10/2026 (F2.11c).</summary>
public static class SectionRegions
{
    public static double BarCover(SezioneCA section,Barra bar) => SectionGeometry.BarCover(section, bar);
}
