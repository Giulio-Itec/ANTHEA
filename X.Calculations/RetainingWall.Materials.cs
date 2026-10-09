using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    // Existing wall paths remain authoritative so older archives and external callers retain their inputs.
    public static readonly string[] SectionMaterialKeys = ["classe_cls", "materiale_cls_nome", "cls_diagramma",
        "classe_acciaio", "materiale_acciaio_nome", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma",
        "alpha_cc", "gamma_c", "gamma_s"];
    public static bool UsesConcrete(JsonObject d) => d.S("family") == "cantilever" || d["gravity_design"].S("type") == "Calcestruzzo non armato";
    public static void CompleteMaterialInput(JsonObject d)
    {
        var m = d["materials"]!.AsObject();
        var defaults = SezioneCA.DefaultInput();
        defaults["classe_cls"] = ConcreteMaterialCatalog.MaterialSheetClasses().FirstOrDefault(c => c.Fck == m.D("fck")).Name ?? "Personalizzato";
        defaults["materiale_cls_nome"] = defaults["classe_cls"]!.DeepClone();
        // Legacy walls used a perfectly plastic steel with fu=fy and ultimate strain 100 per mille.
        // Do not relabel them B450C or change their numerical response simply by opening them.
        defaults["classe_acciaio"] = "Personalizzato"; defaults["materiale_acciaio_nome"] = "Acciaio muro";
        defaults["steel_fu_mpa"] = m["fyk"]?.DeepClone(); defaults["steel_eps_u"] = 100;
        defaults["steel_diagramma"] = "Elastoplastico"; defaults["cls_diagramma"] = ConcreteMaterials.ConcreteDiagrams[0];
        foreach (string key in SectionMaterialKeys) if (!m.ContainsKey(key)) m[key] = defaults[key]?.DeepClone();
        var detail = d["detailing"]!.AsObject();
        foreach (var (key, value) in CoverChoiceDefaults) if (!detail.ContainsKey(key)) detail[key] = value;
        foreach (string key in CoverFlags) if (!detail.ContainsKey(key)) detail[key] = false;
    }
    public static JsonObject MaterialSectionInput(JsonObject d)
    {
        var copy = J.Obj(("materials", d["materials"]!.DeepClone()), ("detailing", new JsonObject())); CompleteMaterialInput(copy);
        var m = copy["materials"]!; var input = SezioneCA.DefaultInput();
        foreach (string key in SectionMaterialKeys) input[key] = m[key]?.DeepClone();
        input["fck_mpa"] = m["fck"]?.DeepClone(); input["fyk_mpa"] = m["fyk"]?.DeepClone();
        input["cover_mm"] = m["cover"]?.DeepClone();
        return input;
    }
    public static void ApplyMaterialPreset(JsonObject d, JsonObject preset)
    {
        var m = d["materials"]!.AsObject();
        foreach (var (key, value) in preset)
        {
            string target = key == "fck_mpa" ? "fck" : key == "fyk_mpa" ? "fyk" : key;
            if (key is "fck_mpa" or "fyk_mpa" || SectionMaterialKeys.Contains(key)) m[target] = value?.DeepClone();
        }
    }
    // Copriferro del muro come la scheda Materiali: tabelle immutabili (refactoring F2.9, prova 11g; prima array pubblici modificabili).
    public static readonly ImmutableArray<(string Key, string Value)> CoverChoiceDefaults = [
        ("cover_method", "NTC + Circ. 2019"), ("cover_element", "Piastra / soletta / parete"),
        ("cover_ground", "Casseratura"), ("cover_abrasion", "Nessuno")];
    public static readonly ImmutableArray<string> CoverFlags = ["cover_quality", "cover_high_strength", "cover_slab", "cover_ec_quality", "cover_rough"];
    public static readonly ImmutableArray<(string Key, string Path)> CoverSheetPaths = [
        ("cover_method", "scelte/coverMethod"), ("cover_element", "scelte/ntcElement"),
        ("cover_ground", "scelte/ground"), ("cover_abrasion", "scelte/abrasion"),
        ("cover_high_strength", "opzioni/highStrength"), ("cover_slab", "opzioni/slab"),
        ("cover_ec_quality", "opzioni/quality"), ("cover_rough", "opzioni/rough")];
    public static JsonObject CoverMaterialState(JsonObject d)
    {
        var copy = J.Obj(("materials", d["materials"]!.DeepClone()), ("detailing", d["detailing"]!.DeepClone()), ("reinforcement", d["reinforcement"]!.DeepClone()), ("geometry", d["geometry"]!.DeepClone())); CompleteMaterialInput(copy);
        var p = copy["detailing"]!;
        var state = J.Obj(("esposizione_principale", copy["materials"].S("exposure")),
            ("numeri", J.Obj(("aggregate", p["aggregate"]?.DeepClone()), ("diameter", MaximumBarDiameter(copy)))),
            ("scelte", J.Obj(("life", p.S("life") + " anni"), ("deviationValue", p.S("cover_deviation") + " mm"))),
            ("opzioni", J.Obj(("ntcQuality", p.B("cover_quality")))));
        foreach (var (key, path) in CoverSheetPaths) { var parts = path.Split('/'); state[parts[0]]![parts[1]] = p[key]?.DeepClone(); }
        return state;
    }
    public static double MaximumBarDiameter(JsonObject d) => new[] { "stem", "stem_upper", "toe", "heel" }
        .Where(k => k != "stem_upper" || d["reinforcement"].B("two_zones"))
        .Where(k => k is not ("toe" or "heel") || d["geometry"].D(k) > 0)
        .SelectMany(k => { var r = d["reinforcement"]![k]!; return new[] { r.D("diameter"), r.B("symmetric", true) ? r.D("diameter") : r.D("opposite_diameter"), r.D("secondary_diameter") }; })
        .Append(d["reinforcement"].B("two_zones") ? d["detailing"].D("tie_diameter") : 0).Max();
    public static double RequiredCover(JsonObject d) => Materiali.MaterialCover.Required(CoverMaterialState(d), d["materials"].D("fck"), MaximumBarDiameter(d));
}
