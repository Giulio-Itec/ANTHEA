using System.Reflection;
using System.Text.Json.Nodes;
using GPC.Model.Materials;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;

namespace X.Core;

/// <summary>Standard presets read from the supplied catalogue DLL, not duplicated tables.</summary>
public static class ConcreteMaterialCatalog
{
    private static IEnumerable<T> Read<T>(Type catalog) => catalog.GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => typeof(T).IsAssignableFrom(p.PropertyType)).Select(p => (T)p.GetValue(null)!);
    public static JsonObject[] Concrete() => Read<ConcreteMaterialEN1992>(typeof(ConcreteMaterialEN1992Data))
        .Select(m => J.Obj(("nome", m.Name), ("classe_cls", m.Name), ("materiale_cls_nome", m.Name), ("fck_mpa", m.Fck), ("cls_diagramma", "Parabola-rettangolo"))).ToArray();
    public static JsonObject[] Steel(bool tendons) => Read<SteelMaterial>(typeof(SteelMaterialEN1992Data))
        .Where(m => m.SteelType == (tendons ? SteelMaterial.SteelTypes.Tendon : SteelMaterial.SteelTypes.Rebar))
        .Select(m => tendons
            ? J.Obj(("id", "EN1992:" + m.Name), ("tipo", "Trefoli"), ("nome", m.Name), ("Ep", m.E), ("fpyk", m.Fyk), ("fpk", m.Fu), ("eps_u", m.StrainUTension * 1000), ("diagramma", m.StressStrainCurve == SteelMaterial.StressStrainCurveType.ElasticHardening ? "Incrudente" : "Elastoplastico"))
            : J.Obj(("nome", m.Name), ("classe_acciaio", m.Name), ("materiale_acciaio_nome", m.Name), ("steel_modulus_mpa", m.E), ("fyk_mpa", m.Fyk), ("steel_fu_mpa", m.Fu), ("steel_eps_u", m.StrainUTension * 1000), ("steel_diagramma", m.StressStrainCurve == SteelMaterial.StressStrainCurveType.ElasticHardening ? "Incrudente" : "Elastoplastico"))).ToArray();
}
