using System.Text.Json.Nodes;
namespace Anthea.Application.Concrete;

/// <summary>Preview preparation through the existing engines, shared by sheets and reports.</summary>
public sealed record ConcretePreview(ConcreteGeometry Geometry, double Fcd, double Fyd, double Ecm, double Ec2, double Ecu, double N)
{
    public static ConcreteGeometry PrepareGeometry(JsonObject input, int radialDivisions = 28, int angularDivisions = 96)
        => new(new SezioneCA(input, radialDivisions, angularDivisions));

    public static ConcretePreview Read(JsonObject input, JsonObject workspace)
    {
        var geometry = PrepareGeometry(input);
        var concrete = ConcreteMaterials.Concrete(input); var steel = ConcreteMaterials.Rebar(input);
        var strengths = ConcreteMaterials.DesignValues(input, workspace);
        return new(geometry, strengths.Fcd, strengths.Fyd, concrete.E,
            Math.Abs(concrete.StrainYCompression) * 1000, Math.Abs(concrete.StrainUCompression) * 1000, steel.E / concrete.E);
    }
}
