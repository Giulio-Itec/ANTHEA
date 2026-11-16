using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ProjectSharedData
{
    private static void AddWallFields(string module, JsonObject stored, Dictionary<string, Field> fields)
    {
        if (module != RetainingWall.Module) return;
        var data = (JsonObject)stored.DeepClone(); RetainingWall.Upgrade(data);
        void Add(string key, string group, string path)
        {
            JsonNode? value = data; foreach (string part in path.Split('/')) value = value?[part];
            fields[key] = new(key, group, path, value?.DeepClone());
        }
        Add("CLS · fck [MPa]", "Materiali", "materials/fck");
        Add("fyk_mpa", "Materiali", "materials/fyk");
        foreach (string key in RetainingWall.SectionMaterialKeys.Where(k => k is not ("classe_cls" or "materiale_cls_nome"))) Add(key, "Materiali", "materials/" + key);
        Add("cover_mm", "Armatura", "materials/cover");
        Add("esposizione", "Materiali", "materials/exposure");
        foreach (var (key, path) in RetainingWall.CoverSheetPaths) Add("Scheda CLS · " + path, "Materiali", "detailing/" + key);
        Add("Durabilità · aggregato [mm]", "Materiali", "detailing/aggregate");
        Add("Durabilità · vita utile [anni]", "Materiali", "detailing/life");
        Add("Durabilità · qualità copriferri", "Materiali", "detailing/cover_quality");
        Add("Durabilità · tolleranza [mm]", "Materiali", "detailing/cover_deviation");
    }
    private static bool ActiveWallField(JsonObject sheet, string key)
    {
        // A wall left without dati (an unresolved reference opened with diagnosis) has no active fields: it is not compared with the
        // other sheets, like the soils of such a sheet (ComparableSoils), and reports no values (ProjectReportPlan.Relevant).
        if (sheet["dati"] is not JsonObject data) return false;
        bool rc = data.S("family") == "cantilever";
        if (key is "CLS · fck [MPa]" or "cls_diagramma" or "alpha_cc" or "gamma_c") return RetainingWall.UsesConcrete(data);
        return rc;
    }
}
