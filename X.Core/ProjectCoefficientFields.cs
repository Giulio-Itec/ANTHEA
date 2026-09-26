using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ProjectSharedData
{
    private static void AddCoefficientFields(string module, JsonObject data, Dictionary<string, Field> fields)
    {
        foreach (var c in CalculationCoefficients.Read(module, data))
            fields[c.Key] = new(c.Key, "Coefficienti", c.Path, c.Value);
        if (module is "str_palo" or BridgeSection.Module)
        {
            string path = module == "str_palo" ? "workspace_ca/normativa" : "normativa";
            string value = module == "str_palo" ? data["workspace_ca"].S("normativa", "NTC 2018") : data.S("normativa", BridgeSection.Standards[0]);
            fields["Normativa · " + module] = new("Normativa · " + module, "Normativa", path, JsonValue.Create(value));
        }
    }
    private static bool CompatibleCoefficient(string key, JsonObject first, JsonObject second)
    {
        if (CalculationCoefficients.Label(key) is null) return true;
        string a = CalculationCoefficients.Standard(first.S("modulo_id"), first["dati"] as JsonObject ?? new());
        string b = CalculationCoefficients.Standard(second.S("modulo_id"), second["dati"] as JsonObject ?? new());
        return a == b || key == "gamma_s" && (a == "Assegnato" || b == "Assegnato");
    }
    private static void PrepareCoefficientTarget(JsonObject source, JsonObject target, HashSet<string> groups, HashSet<string>? keys)
    {
        string module = source.S("modulo_id"), key = "Normativa · " + module;
        if (module != target.S("modulo_id") || !groups.Contains("Normativa") || keys is not null && !keys.Contains(key)) return;
        if (Fields(source).TryGetValue(key, out var field)) Put(target["dati"]!.AsObject(), field.Path, field.Value);
    }
}
