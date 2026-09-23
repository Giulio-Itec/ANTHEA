using System.Text.Json.Nodes;
using GPC.Model.Materials;

namespace X.Core;

/// <summary>Display data sampled from the same DLL constitutive laws as the section solver.</summary>
public sealed record MaterialDiagram(double[][] Characteristic, double[][] Design)
{
    public static MaterialDiagram Create(string type, JsonObject material, JsonObject input, JsonObject workspace)
    {
        var standard = ConcreteStandards.Effective(input, workspace);
        Material native; Func<double, double> design; double min, max, designMin, designMax;
        if (type == "Calcestruzzo")
        {
            var concrete = ConcreteMaterials.Concrete(material); native = concrete;
            min = -Math.Abs(concrete.StrainUCompression); max = 0; designMin = min; designMax = 0;
            design = e => concrete.CalculateDesignStressConcrete(standard, e);
        }
        else
        {
            SteelMaterial steel;
            if (type == "Acciaio") steel = ConcreteMaterials.Rebar(material);
            else
            {
                double ep = material.Required("Ep", strict: true), fy = material.Required("fpyk", strict: true), fu = material.Required("fpk", strict: true), eps = material.Required("eps_u", strict: true) / 1000;
                if (fu < fy || eps <= fy / ep) throw new ArgumentException("Trefoli: richiedere fpk ≥ fpyk e εpu > fpyk/Ep.");
                steel = new SteelMaterial(material.S("nome"), ep, fy, fu, eps, material.S("diagramma") == "Incrudente" ? SteelMaterial.StressStrainCurveType.ElasticHardening : SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Tendon);
            }
            native = steel; max = Math.Abs(steel.StrainUTension); min = type == "Trefoli" ? 0 : -max;
            designMax = Math.Abs(steel.CalculateDesignUltimateStrainTension(standard)); designMin = type == "Trefoli" ? 0 : -Math.Abs(steel.CalculateDesignUltimateStrainCompression(standard));
            design = e => steel.CalculateDesignStress(standard, e);
        }
        double[][] Curve(double lo, double hi, Func<double, double> stress) => Enumerable.Range(0, 241).Select(i =>
        {
            double e = lo + (hi - lo) * i / 240; double s = stress(e);
            if (!double.IsFinite(e) || !double.IsFinite(s)) throw new ArgumentException("Diagramma non valido per i parametri inseriti.");
            return new[] { e * 1000, s };
        }).ToArray();
        return new(Curve(min, max, native.GetStress), Curve(designMin, designMax, design));
    }
}
