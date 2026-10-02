using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static readonly string[] FrictionModes = ["Assegnato", "Gettato in opera", "Prefabbricato liscio", "Liscio"];
    public static (string Label, string Unit, string Origin) AuditMeaning(string key) => key switch
    {
        "Dv_m" => ("Quota terreno di valle Dv", "m", "H+t−Hlib"),
        "Hlib_m" => ("Altezza libera Hlib", "m", "Sommità → terreno di valle"),
        "peso_valle_kN_m" => ("Peso terreno a valle", "kN/m", "Volume reale sopra mensola e paramento × γ × γG,valle × (1−kv)"),
        "momento_peso_valle_kNm_m" => ("Momento del peso a valle", "kNm/m", "Peso × ascissa del baricentro GPC"),
        "q_ricoprimento_kPa" => ("Ricoprimento efficace q′", "kPa", "Integrale γ′ di valle fino al piano di posa"),
        "delta_muro_d_gradi" => ("Attrito muro δd", "°", "Automatico da φcv o assegnato; Wood: 0 mobilitato"),
        "delta_base_d_gradi" => ("Attrito base δb,d", "°", "Automatico da φcv o assegnato, con γMφ"),
        "mu_base" => ("Coefficiente attrito base μd", "−", "tan δb,d"),
        "delta_piano_equilibrio_gradi" => ("Attrito piano di equilibrio", "°", "Piano virtuale con mensola: 0; senza mensola: δ muro"),
        "passiva_disponibile_kN_m" => ("Passiva disponibile", "kN/m", "Integrale ηp Kp σ′v, Kp=1/Ka"),
        "passiva_usata_kN_m" => ("Passiva utilizzata", "kN/m", "Limitata alla spinta motrice; sisma: 0"),
        "passiva_frazione" => ("Frazione passiva ηp", "−", "Input 0–1; sisma: 0"),
        "passiva_limite_equilibrio" => ("Riduzione passiva per equilibrio", "−", "min(1, spinta motrice / passiva disponibile)"),
        "Nq" => ("Fattore di portanza Nq", "−", "exp(π tanφd) tan²(45°+φd/2)"),
        "Ngamma" => ("Fattore di portanza Nγ", "−", "2(Nq−1)tanφd"),
        "iq" => ("Inclinazione iq", "−", "max(0,1−|H|/V′)²"),
        "igamma" => ("Inclinazione iγ", "−", "max(0,1−|H|/V′)³"),
        "base_ruvida" => ("Campo portanza base ruvida", "sì/no", "δb,d≥φd/2"),
        _ => (key, "−", "Valore derivato")
    };
    public const string SoilHelp = "Due colonne con profondità dalla rispettiva superficie. Hlib è misurata dalla sommità al terreno di valle; Dv=H+t−Hlib dal piano di posa. Collegare le colonne copia spessori e materiali, non le quote assolute. Peso del terreno sopra la mensola e ricoprimento efficace nella portanza sempre inclusi. Passiva Rankine attivabile con frazione mobilitata 0–1; esclusa nei casi sismici. Il terreno di posa resta un input distinto.";
    public const string FrictionHelp = "Automatico: δd=k·atan(tanφcv,k/γMφ), k=1 gettato in opera, 2/3 prefabbricato, 0 liscio. φcv è l’angolo a volume costante, da caratterizzare; non è automaticamente l’angolo di picco. Assegnato: tanδd=tanδk/γMφ. Sul fusto Coulomb/Mononobe–Okabe con attrito; per l’equilibrio con mensola a monte la spinta è sul piano virtuale a tergo della mensola, δ=0. Wood non mobilita δ. Nessun doppio conteggio dell’attrito interno al blocco muro–terreno.";
    public static void CompleteSoilInput(JsonObject d)
    {
        d["valley"] ??= J.Obj(("height_mode", "Interamente libero"), ("free_height", d["geometry"].D("height") + d["geometry"].D("slab")),
            ("linked", false), ("layers", d["layers"]?.DeepClone()), ("passive", false), ("mobilization", 0));
        d["interfaces"] ??= J.Obj(("wall_mode", "Assegnato"), ("wall_delta", 0), ("wall_phi_cv", 30),
            ("base_mode", "Assegnato"), ("base_phi_cv", d["foundation"].D("phi")));
    }
    public static double ValleyHeight(JsonObject d) => d["valley"].S("height_mode", "Interamente libero") == "Interamente libero" ? 0 : d["geometry"].D("height") + d["geometry"].D("slab") - d["valley"].D("free_height");
    public static JsonArray ValleyLayers(JsonObject d) => d["valley"].B("linked") ? d.Array("layers") : d["valley"] is JsonObject v ? v.Array("layers") : d.Array("layers");
    public static void CopySoilColumn(JsonObject d, bool fromValley)
    {
        CompleteSoilInput(d);
        if (fromValley) d["layers"] = d["valley"]!["layers"]!.DeepClone();
        else d["valley"]!["layers"] = d["layers"]!.DeepClone();
    }
    private static void ValidateSoils(JsonObject d)
    {
        if (d["valley"] is not JsonObject v || d["interfaces"] is not JsonObject f) throw new ArgumentException("Dati delle due colonne o degli attriti non validi.");
        foreach (string flag in new[] { "linked", "passive" }) if (v[flag] is not JsonValue b || !b.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione terreno di valle non valida: " + flag);
        if (v.S("height_mode") is not ("Interamente libero" or "Assegnato")) throw new ArgumentException("Modalità altezza libera non valida.");
        double ht = d["geometry"].D("height") + d["geometry"].D("slab");
        if (v.S("height_mode") == "Assegnato" && (J.Number(v["free_height"]) is not double free || !double.IsFinite(free) || free < 0 || free > ht)) throw new ArgumentException("Altezza libera: 0≤Hlib≤H+t.");
        if (J.Number(v["mobilization"]) is not double eta || !double.IsFinite(eta) || eta < 0 || eta > 1) throw new ArgumentException("Frazione di passiva mobilitata: 0–1.");
        var layers = ValleyLayers(d);
        if (layers.Count is < 1 or > 50 || layers.Any(l => l is not JsonObject)) throw new ArgumentException("Valle: inserire 1–50 strati.");
        foreach (var l in layers)
        {
            foreach (string key in new[] { "thickness", "gamma", "gamma_sat", "phi" }) l!.Required(key, strict: true);
            if (l.D("thickness") > 100 || l.D("phi") < 10 || l.D("phi") > 45 || l.D("gamma") < 10 || l.D("gamma_sat") < l.D("gamma") || l.D("gamma_sat") > 28) throw new ArgumentException("Valle: 10°≤φ′≤45°, 10≤γ≤γsat≤28, spessore≤100 m.");
        }
        if (layers.Sum(l => l.D("thickness")) + 1e-9 < ValleyHeight(d)) throw new ArgumentException("La colonna di valle deve raggiungere il piano di posa.");
        foreach (bool wall in new[] { false, true })
        {
            string prefix = wall ? "wall" : "base";
            if (!FrictionModes.Contains(f.S(prefix + "_mode"))) throw new ArgumentException("Modalità attrito non valida.");
            double limit = wall ? d.Array("layers").Min(l => l.D("phi")) : d["foundation"].D("phi");
            if (f.S(prefix + "_mode") is "Gettato in opera" or "Prefabbricato liscio")
                if (J.Number(f[prefix + "_phi_cv"]) is not double cv || !double.IsFinite(cv) || cv <= 0 || cv > limit) throw new ArgumentException("Attrito automatico: assegnare φcv positivo e non superiore a φ′ del terreno a contatto.");
            if (wall && f.S("wall_mode") == "Assegnato" && (J.Number(f["wall_delta"]) is not double dd || !double.IsFinite(dd) || dd < 0)) throw new ArgumentException("Assegnare δ muro finito e non negativo.");
            if (InterfaceDelta(d, wall) > limit) throw new ArgumentException("L’attrito di interfaccia non può superare φ′ del terreno.");
        }
    }
    /// <summary>A band of the soil in front of the wall (m above the base, kN/m³, degrees, σ′v in kPa), from the library.</summary>
    public sealed record ValleyBand(double Top, double Bottom, double Gamma, double Effective, double Phi, double SigmaTop, double SigmaBottom);
}
