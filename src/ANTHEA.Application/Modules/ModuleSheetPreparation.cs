using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

/// <summary>Lazy defaults of the editable sheet, applied to an explicitly owned snapshot.</summary>
public static class ModuleSheetPreparation
{
    public static JsonNode NumericText(string text) => double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? JsonValue.Create(value)! : JsonValue.Create(text)!;
    public static void PreparePileGroup(JsonObject data)
    {
        HorizontalPileGroup.Upgrade(data);
        if (data["generatore"].S("tipo") == "Generica")
            data["pali"] = new JsonArray(data.Array("pali").Select(p => (JsonNode)J.Obj(("id", p.S("id")),
                ("x", NumericText(p!["x"]!.ToString())), ("y", NumericText(p["y"]!.ToString())))).ToArray());
        HorizontalPileGroup.Regenerate(data);
    }
    public static void ApplyPileGroupSelection(JsonObject data, JsonObject result)
    {
        var selected = result["selezionato"]!;
        data["direzione_selezionata"] = selected.S("DirectionId"); data["angolo"] = selected.D("Angle");
    }

    public static void PrepareVertical(JsonObject data, string module)
    {
        bool micro = module == "geo_micropalo_verticale";
        var defaults = ModuleCatalog.CreateData(module); if (data["generali"] is not JsonObject) data["generali"] = new JsonObject(); var g = data["generali"]!.AsObject();
        string previous = micro ? "" : Nq.MetodoPrecedente(g);
        if (micro) g.Remove("peso_lineare_micropalo"); // Superseded manual input; weight is always derived from geometry.
        Calcolo.AggiornaFoglio(data, micro); // D7-d (d2): one-off migration of γb of the sheets saved before version 2.
        // A missing γb is completed with the effective value of the calculation (that of the technology), not with the default of the bored pile.
        foreach (var (k, v) in defaults["generali"]!.AsObject()) if (!g.ContainsKey(k) && k != "metodo_micropalo") g[k] = k == "sicurezza_base" ? Calcolo.SicurezzaBaseTesto(data, micro) : v?.DeepClone();
        if (!micro) { g["metodo_nq"] = "Parametrizzata"; if (previous != "") g["metodo_nq_precedente"] = previous; }
        if (data["efficienza"] is not JsonObject) data["efficienza"] = defaults["efficienza"]!.DeepClone();
        if (data["stratigrafie"] is not JsonArray) data["stratigrafie"] = new JsonArray();
    }
    public static void PrepareBridgeDesign(JsonObject data)
    {
        var completed = BridgeConcept.WithAdvancedDefaults(data);
        foreach (string group in new[] { "input", "rates", "assumptions" })
            if (data[group] is JsonObject target && completed[group] is JsonObject fields)
                foreach (var field in fields) if (!target.ContainsKey(field.Key)) target[field.Key] = field.Value?.DeepClone();
    }
}
