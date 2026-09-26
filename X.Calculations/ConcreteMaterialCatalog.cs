using System.Reflection;
using System.Text.Json.Nodes;
using GPC.Model.Materials;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;

namespace Anthea.Calculations;

/// <summary>Standard presets read from the supplied catalogue DLL, not duplicated tables.</summary>
public static class ConcreteMaterialCatalog
{
    public static ConcreteMaterialEN1992 Material(double fck) => new("Calcestruzzo", fck,
        ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
    private static IEnumerable<T> Read<T>(Type catalog) => catalog.GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => typeof(T).IsAssignableFrom(p.PropertyType)).Select(p => (T)p.GetValue(null)!);
    // Fresh native instances on each request: user overrides never mutate another calculation's catalogue.
    public static ConcreteMaterialEN1992[] NativeConcrete() => Read<ConcreteMaterialEN1992>(typeof(ConcreteMaterialEN1992Data)).ToArray();
    public static SteelMaterialEN1993[] NativeStructuralSteel() => Read<SteelMaterialEN1993>(typeof(SteelMaterialEN1993Data)).ToArray();
    public static SteelMaterialEN1992[] NativeReinforcement() => Read<SteelMaterialEN1992>(typeof(SteelMaterialEN1992Data)).ToArray();
    public static (string Name, double Fck)[] MaterialSheetClasses() => Concrete().Select(m => (Name: m.S("nome"), Fck: m.D("fck_mpa")))
        // C12/15 and C16/20 were supported by the material sheet but are absent from this ModelData snapshot.
        .Concat(new[] { (Name: "C12/15", Fck: 12d), (Name: "C16/20", Fck: 16d) }).DistinctBy(m => m.Name).OrderBy(m => m.Fck).ToArray();
    public static JsonObject[] Concrete(string standard = "NTC 2018") => Read<ConcreteMaterialEuropeanCommon>(standard == "Model Code 2010" ? typeof(ConcreteMaterialModelCode2010Data) : typeof(ConcreteMaterialEN1992Data))
        // The DLL uses negative compressive stresses; ANTHEA's material input is a strength magnitude.
        .Select(m => J.Obj(("nome", m.Name), ("classe_cls", m.Name), ("materiale_cls_nome", m.Name), ("fck_mpa", Math.Abs(m.Fck)), ("cls_diagramma", "Parabola-rettangolo"))).ToArray();
    public static JsonObject[] Steel(bool tendons, string standard = "EN 1992-1-1") => Read<SteelMaterial>(typeof(SteelMaterialEN1992Data))
        .Where(m => m.SteelType == (tendons ? SteelMaterial.SteelTypes.Tendon : SteelMaterial.SteelTypes.Rebar))
        .Where(m => tendons || standard != "NTC 2018" || m.Name is "B450A" or "B450C")
        .Select(m => tendons
            ? J.Obj(("id", "EN1992:" + m.Name), ("tipo", "Trefoli"), ("nome", m.Name), ("Ep", m.E), ("fpyk", m.Fyk), ("fpk", m.Fu), ("eps_u", m.StrainUTension * 1000), ("diagramma", m.StressStrainCurve == SteelMaterial.StressStrainCurveType.ElasticHardening ? "Incrudente" : "Elastoplastico"))
            : J.Obj(("nome", m.Name), ("classe_acciaio", m.Name), ("materiale_acciaio_nome", m.Name), ("steel_modulus_mpa", m.E), ("fyk_mpa", m.Fyk), ("steel_fu_mpa", m.Fu), ("steel_eps_u", m.StrainUTension * 1000), ("steel_diagramma", m.StressStrainCurve == SteelMaterial.StressStrainCurveType.ElasticHardening ? "Incrudente" : "Elastoplastico"))).ToArray();
}
