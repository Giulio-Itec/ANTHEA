using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static class MicropaloOrizzontale
{
    public const string Module = "geo_micropalo_orizzontale";
    public static JsonObject Defaults()
    {
        var data = PaloOrizzontale.Defaults(); data["tipo_sezione"] = "CHS";
        data["generali"]!["diametro"] = "0.24"; data["generali"]!["origine_momento"] = "Sezione CHS";
        data["sezione"] = J.Obj(("modo_chs", "Catalogo"), ("profilo_chs", "CHS 139.7 × 8"),
            ("diametro_chs_mm", "139.7"), ("spessore_chs_mm", "8"), ("fy_chs_mpa", "355"), ("gamma_m0", "1.05"));
        return data;
    }

    public static JsonObject Properties(JsonObject data)
    {
        var s = data["sezione"]!; double d, t;
        if (s.S("modo_chs") == "Catalogo")
        {
            if (!Chs.Catalogo.TryGetValue(s.S("profilo_chs"), out var profile)) throw new ArgumentException("Selezionare un CHS dal catalogo ANTHEA.");
            (d, t) = profile;
        }
        else if (s.S("modo_chs") == "Manuale") { d = s.Required("diametro_chs_mm", strict: true); t = s.Required("spessore_chs_mm", strict: true); }
        else throw new ArgumentException("Modalità CHS non riconosciuta.");
        if (2 * t >= d) throw new ArgumentException("CHS: lo spessore deve essere minore di metà diametro.");
        if (d >= data["generali"]!.Required("diametro", strict: true) * 1000) throw new ArgumentException("Il diametro CHS deve essere minore del diametro geotecnico.");
        double inner = d - 2 * t, a = Math.PI / 4 * (d * d - inner * inner), inertia = Math.PI / 64 * (Math.Pow(d, 4) - Math.Pow(inner, 4));
        double fy = s.Required("fy_chs_mpa", strict: true), gamma = s.Required("gamma_m0", 1), fyd = fy / gamma;
        double wel = 2 * inertia / d, wpl = (Math.Pow(d, 3) - Math.Pow(inner, 3)) / 6;
        double slenderness = d / t / (235 / fy);
        int cls = slenderness <= 50 ? 1 : slenderness <= 70 ? 2 : slenderness <= 90 ? 3 : 4;
        return J.Obj(("diametro_mm", d), ("spessore_mm", t), ("diametro_interno_mm", inner), ("area_mm2", a),
            ("inerzia_mm4", inertia), ("wel_mm3", wel), ("wpl_mm3", wpl), ("massa_kg_m", a * .00785),
            ("fyd_mpa", fyd), ("classe", cls), ("npl_kn", a * fyd / 1000), ("mpl_knm", wpl * fyd / 1e6));
    }

    public static JsonObject Section(JsonObject data)
    {
        var p = Properties(data);
        if (p.D("classe") != 1) throw new ArgumentException("CHS non di classe 1: momento automatico per il meccanismo plastico non disponibile. Occorre una valutazione specifica; non viene attribuita automaticamente duttilità.");
        double? axial = J.Number(data["generali"]!["azione_assiale"]);
        if (axial is null || !double.IsFinite(axial.Value)) throw new ArgumentException("Forza assiale non valida.");
        double ratio = Math.Abs(axial.Value) / p.D("npl_kn");
        if (ratio >= 1) throw new ArgumentException("La forza assiale raggiunge o supera la resistenza del CHS.");
        // Conservative linear N-M interaction (no beneficial resistance from grout).
        p["momento_knm"] = p.D("mpl_knm") * (1 - ratio); p["n_kn"] = axial.Value;
        p["tipo"] = "CHS";
        p["modello"] = "Solo acciaio CHS, classe 1 secondo limiti D/t con ε²=235/fy. My = Wpl·fy/γM0·(1−|N|/Npl). Interazione lineare conservativa N–M; riempimento escluso. Non verifica instabilità globale, taglio, giunti, corrosione o capacità di rotazione delle connessioni. fy deve essere appropriato a materiale e spessore.";
        return p;
    }
}
