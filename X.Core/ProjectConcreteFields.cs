using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ProjectSharedData
{
    private static void AddConcreteDurabilityFields(string module, JsonObject data, Dictionary<string, Field> fields)
    {
        if (module is not ("str_palo" or "mat_calcestruzzo")) return;
        bool material = module == "mat_calcestruzzo";
        foreach (var (name, caKey, materialPath, fallback, suffix) in new[] {
            ("aggregato [mm]", "aggregato", "numeri/aggregate", "20", ""),
            ("vita utile [anni]", "vita_durabilita", "scelte/life", "50", " anni"),
            ("qualità copriferri", "qualita_copriferro", "opzioni/ntcQuality", "No", ""),
            ("tolleranza [mm]", "delta_c", "scelte/deviationValue", "10", " mm") })
        {
            string path = material ? materialPath : "workspace_ca/dettagli_costruttivi/" + caKey;
            JsonNode? value = data;
            foreach (string part in path.Split('/')) value = value?[part];
            string raw = value?.ToString() ?? fallback;
            JsonNode normalized = caKey == "qualita_copriferro" ? JsonValue.Create(raw is "Sì" or "true" or "True") : JsonValue.Create(suffix.Length == 0 ? raw : raw.Replace(suffix, ""));
            string key = "Durabilità · " + name;
            fields[key] = new(key, "Materiali", path, normalized);
            if (material) fields.Remove("Scheda CLS · " + materialPath);
        }
    }
    private static bool SameDurabilityModel(JsonObject first, JsonObject second) => first.S("modulo_id") == second.S("modulo_id") ||
        new[] { first, second }.Where(s => s.S("modulo_id") == "mat_calcestruzzo").All(s => s["dati"]?["scelte"].S("coverMethod", "NTC + Circ. 2019") != "EC2 2004");
    /// <summary>Shared relevance rules for comparison and report inputs; dormant values stay saved.</summary>
    public static bool ActiveField(JsonObject sheet, string key)
    {
        string module = sheet.S("modulo_id");
        if (sheet["dati"] is JsonObject data && !CalculationCoefficients.Active(module, data, key)) return false;
        if (module == BridgeSection.Module && key.StartsWith(BridgePrefix)) return ActiveBridgeField(sheet, key);
        if (key.StartsWith("Durabilità · ")) return module == "mat_calcestruzzo" || sheet["dati"]?["workspace_ca"].S("normativa", "NTC 2018") == "NTC 2018";
        if (module is not ("str_palo" or PaloOrizzontale.Module)) return true;
        var input = sheet["dati"]?[module == "str_palo" ? "input" : "sezione"];
        string shape = module == "str_palo" ? input.S("shape", "Rettangolare") : "Circolare";
        bool manual = input?["barre_manuali"] is JsonArray { Count: > 0 };
        if (key is "diameter_mm" or "circular_sides") return shape == "Circolare";
        if (key == "width_mm") return shape == "Rettangolare";
        if (key == "height_mm") return shape != "Circolare";
        if (key is "flange_width_mm" or "web_width_mm" or "flange_thickness_mm") return shape == "A T";
        if (key == "foro_presente") return shape is "Circolare" or "Rettangolare";
        if (key.StartsWith("inner_")) return input.B("foro_presente") &&
            (key == "inner_diameter_mm" ? shape == "Circolare" : shape == "Rettangolare");
        if (key.StartsWith("longitudinal_")) return !manual && shape == "Circolare";
        if (key.StartsWith("top_bar_") || key.StartsWith("bottom_bar_")) return !manual && shape != "Circolare";
        if (key.StartsWith("side_bar_")) return !manual && shape != "Circolare" &&
            (key == "side_bar_count_per_side" || input.D("side_bar_count_per_side", 2) > 0);
        if (key.StartsWith("flange_bottom_")) return !manual && shape == "A T" &&
            (key == "flange_bottom_count" || input.D("flange_bottom_count") > 0);
        if (key.StartsWith("second_"))
        {
            string layer = key.Split('_')[1];
            return !manual && shape == (layer == "inner" ? "Circolare" : "Rettangolare") &&
                (key.EndsWith("_enabled") || input.B("second_" + layer + "_enabled"));
        }
        if (key.StartsWith("transverse_") || key.StartsWith("Staffe · "))
        {
            if (input.S("staffe_presenti", "Sì") == "No") return false;
            if (key is "Staffe · tipo_staffa" or "Staffe · schema_interno" or "Staffe · rami_interni" or "Staffe · rotazione_staffa") return shape == "Circolare";
        }
        return true;
    }
}
