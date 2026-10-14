namespace Anthea.Application.Concrete;

public sealed record StrainVertex(double X, double Y, double Strain);

/// <summary>Owned samples of the existing strain plane, ready for a renderer.
/// No solver or material law is exposed by the drawing data.</summary>
public sealed record ConcreteStrainDiagram(double Width, double Height, double StrainReferencePoint,
    double ChiX, double ChiY, IReadOnlyList<StrainVertex> Outline, IReadOnlyList<IReadOnlyList<StrainVertex>> Holes)
{
    public static ConcreteStrainDiagram Read(ConcreteResults.StressState state, ConcreteGeometry geometry)
    {
        // Total plane: in the linear analysis with creep the solver plane is 1 + φ times smaller than the strains of the bars (F4-43)
        var plane = state.Source.Native.TotalStrainPlane;
        StrainVertex Sample(double[] p) => new(p[0], p[1], plane.GetStrain(p[0], p[1]));
        return new(geometry.Width, geometry.Height, plane.StrainReferencePoint, plane.ChiX, plane.ChiY,
            geometry.Outline.Select(Sample).ToArray(), geometry.Holes.Select(h => (IReadOnlyList<StrainVertex>)h.Select(Sample).ToArray()).ToArray());
    }
}
