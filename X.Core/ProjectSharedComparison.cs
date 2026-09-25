using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ProjectSharedData
{
    // Reading and comparing inputs is symmetric. Whether one module can accept the other
    // module's value is a separate, directional decision made by Common/Apply.
    public static IEnumerable<(Field Source, Field Target)> ComparableFields(JsonObject first, JsonObject second)
    {
        var a = Fields(first); var b = Fields(second);
        foreach (var left in a.Values)
            if (b.TryGetValue(left.Key, out var right) && CanCompare(left, first, second, a, b))
                yield return (left, right);
    }

    static bool CanCompare(Field field, JsonObject first, JsonObject second,
        Dictionary<string, Field> a, Dictionary<string, Field> b)
    {
        string fm = first.S("modulo_id"), sm = second.S("modulo_id"), key = field.Key;
        if (!ActiveField(first, key) || !ActiveField(second, key)) return false;
        if (key.StartsWith("Durabilità · ") && !SameDurabilityModel(first, second)) return false;
        if ((key == "profondita_falda" || key.StartsWith("Strato · ") && key.EndsWith("/peso_specifico_saturo")) &&
            !WaterDataActive(first, second)) return false;
        if (key == "lunghezza_micropalo" && fm != sm &&
            ((first["dati"]?["generali"]).D("inclinazione") != 0 || (second["dati"]?["generali"]).D("inclinazione") != 0)) return false;
        if (key == "CHS · profilo_chs")
            return (fm != MicropaloOrizzontale.Module || first["dati"]?["sezione"].S("modo_chs") == "Catalogo") &&
                (sm != MicropaloOrizzontale.Module || second["dati"]?["sezione"].S("modo_chs") == "Catalogo");
        if (fm == sm || field.Group is "Terreno" or "Materiali" || key is "perforazione_mm" or "lunghezza_micropalo") return true;

        bool rcPair = fm is "str_palo" or PaloOrizzontale.Module && sm is "str_palo" or PaloOrizzontale.Module;
        if (rcPair && key is "shape" or "cover_mm") return true;
        if (!a.TryGetValue("shape", out var shapeA) || !b.TryGetValue("shape", out var shapeB) ||
            Text(shapeA.Value) != "Circolare" || Text(shapeB.Value) != "Circolare") return false;
        if (key == "shape") return false;
        if (field.Group == "Geometria") return key is "diameter_mm" or "lunghezza";
        if (field.Group == "Armatura")
        {
            if (key.StartsWith("top_") || key.StartsWith("bottom_") || key.StartsWith("side_") ||
                key.StartsWith("flange_") || key.StartsWith("second_") || key == "barre_manuali") return false;
            if ((first["dati"]?["input"]?["barre_manuali"] as JsonArray)?.Count > 0 ||
                (second["dati"]?["input"]?["barre_manuali"] as JsonArray)?.Count > 0) return false;
        }
        return true;
    }

    static JsonObject[] ComparisonSheets(JsonObject section) => ContextSheets(section).OrderBy(SheetOrder, StringComparer.Ordinal).ToArray();
}
