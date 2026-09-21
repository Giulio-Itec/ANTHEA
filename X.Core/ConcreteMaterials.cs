using System.Text.Json.Nodes;
using GPC.Model.Materials;

namespace X.Core;
public static class ConcreteMaterials
{
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
        return new SteelMaterial(input.S("materiale_acciaio_nome", "Armatura"), e, fy, fu, strain,
            input.S("steel_diagramma") == "Incrudente" ? SteelMaterial.StressStrainCurveType.ElasticHardening : SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic,
            SteelMaterial.SteelTypes.Rebar);
    }
}
