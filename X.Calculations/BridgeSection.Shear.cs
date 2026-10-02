using System.Text.Json.Nodes;
namespace Anthea.Calculations;
public static partial class BridgeSection
{
    // the archives before the section types are H sections
    public static JsonObject AccessoryDefaults() => J.Obj(("sezione", SectionTypes[0]), ("offset_anima", 0), ("interasse_anime", 1800), ("gamma_m1", 1.1), ("eta_taglio", 1.2),
        ("irrigidimenti", false), ("a_irr", 3000), ("b_irr", 150), ("t_irr", 15), ("N_irr", 0),
        ("pioli", false), ("n_pioli", 2), ("d_pioli", 22), ("h_pioli", 150), ("passo_pioli", 200),
        ("passo_trasv_pioli", 150), ("fu_pioli", 450), ("gamma_v", 1.25), ("d_testa_pioli", 35), ("t_testa_pioli", 12),
        ("pioli_fatica", true), ("copriferro_pioli", 35));
    public static void EnsureAccessoryDefaults(JsonObject data)
    {
        // Migrate pre-total-height projects once, preserving their exact clear web geometry.
        if (!data.ContainsKey("h_trave"))
        {
            double legacyClear = J.Number(data["h_web"]) ?? 1800;
            double top = J.Number(data["t_top"]) ?? 25, bottom = J.Number(data["t_bottom"]) ?? 30;
            bool second = data.B("plate2") && data.S("sezione", SectionTypes[0]) == SectionTypes[0];
            double secondBottom = second ? J.Number(data["t_bottom2"]) ?? 0 : 0;
            data["h_trave"] = (legacyClear + top + bottom + secondBottom).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (data.S("metodo_analisi") == LegacyCumulativeMethod) data["metodo_analisi"] = CumulativeLinearMethod;
        if (!data.ContainsKey("predalle")) data["predalle"] = false;
        if (!data.ContainsKey("h_predalle")) data["h_predalle"] = "60";
        if (!data.ContainsKey("rif_ferri_inf")) data["rif_ferri_inf"] = PredalleTopReference;
        foreach (var pair in AccessoryDefaults()) if (!data.ContainsKey(pair.Key))
            data[pair.Key] = pair.Value?.DeepClone();
        foreach (var pair in DetailDefaults()) if (!data.ContainsKey(pair.Key))
            data[pair.Key] = pair.Value?.DeepClone();
        foreach (var pair in BoxDefaults()) if (!data.ContainsKey(pair.Key))
            data[pair.Key] = pair.Value?.DeepClone();
    }

    public static double ClearWebHeight(JsonObject data)
    {
        // Direct legacy inputs may bypass the archive migration.
        if (!data.ContainsKey("h_trave")) return data.Required("h_web", strict: true);
        double total = data.Required("h_trave", strict: true);
        double top = data.Required("t_top", strict: true), bottom = data.Required("t_bottom", strict: true);
        bool second = data.B("plate2") && data.S("sezione", SectionTypes[0]) == SectionTypes[0];
        double secondBottom = second ? data.Required("t_bottom2", strict: true) : 0;
        double clear = total - top - bottom - secondBottom;
        if (!double.IsFinite(clear) || clear <= 0)
            throw new ArgumentException("Altezza totale H deve superare la somma degli spessori delle piattabande adottate.");
        return clear;
    }

}
