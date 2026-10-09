using System.Text.Json.Nodes;
using GPC.Model.Materials;

namespace Anthea.Calculations;
public static class ConcreteMaterials
{
    public sealed record DesignStrengths(double Fcd, double Fyd);
    public static DesignStrengths DesignValues(JsonObject input, JsonObject workspace)
    {
        var standard = ConcreteStandards.Effective(input, workspace);
        // Getti sottili (refactoring F2.7b, commit A3, rilievo M14): fattore della norma dalla libreria attraverso la mappatura, stessa
        // fonte del limite SLE e di αcc in CheckerSection; 'gettato_sottile' si legge solo se la norma riduce, come nella regola legacy.
        double thinCasting = ConcreteLibraryMapping.ThinCastingFactor(standard);
        double reduction = thinCasting != 1 && input.S("gettato_sottile", "No") == "Sì" ? thinCasting : 1;
        return new(Math.Abs(Concrete(input).CalculateFcd(standard)) * reduction, Math.Abs(Rebar(input).CalculateFyd(standard)));
    }
    public static readonly string[] ConcreteDiagrams = ["Parabola-rettangolo", "Bilineare", "Stress block", "Non lineare"];
    public static ConcreteMaterialEN1992 Concrete(JsonObject input)
    {
        var diagram = input.S("cls_diagramma", ConcreteDiagrams[0]) switch
        {
            "Parabola-rettangolo" => ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle,
            "Bilineare" => ConcreteMaterial.CompressionStressStrainDiagrams.Bilinear,
            "Stress block" => ConcreteMaterial.CompressionStressStrainDiagrams.StressBlock,
            "Non lineare" => ConcreteMaterial.CompressionStressStrainDiagrams.NonLinear,
            _ => throw new ArgumentException("Diagramma CLS non supportato.")
        };
        double fck = input.Required("fck_mpa", strict: true);
        if (fck < 12 || fck > 90) throw new ArgumentException("CLS: fck deve essere compreso fra 12 e 90 MPa.");
        return new ConcreteMaterialEN1992(input.S("materiale_cls_nome", input.S("classe_cls")), fck, diagram);
    }
    public static SteelMaterial Rebar(JsonObject input)
    {
        double e = input.Required("steel_modulus_mpa", strict: true), fy = input.Required("fyk_mpa", strict: true);
        double fu = input.ContainsKey("steel_fu_mpa") ? input.Required("steel_fu_mpa", strict: true) : fy;
        double strain = input.ContainsKey("steel_eps_u") ? input.Required("steel_eps_u", strict: true) / 1000 : .1;
        if (fu < fy || strain <= fy / e) throw new ArgumentException("Acciaio: richiedere fu ≥ fyk e εu > fyk / Es.");
        var curve = input.S("steel_diagramma", "Elastoplastico") switch
        {
            "Elastoplastico" => SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic,
            "Incrudente" => SteelMaterial.StressStrainCurveType.ElasticHardening,
            _ => throw new ArgumentException("Diagramma acciaio non supportato.")
        };
        return new SteelMaterial(input.S("materiale_acciaio_nome", "Armatura"), e, fy, fu, strain,
            curve,
            SteelMaterial.SteelTypes.Rebar);
    }
}
