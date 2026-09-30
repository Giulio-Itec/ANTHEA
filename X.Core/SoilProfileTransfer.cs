using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>A portable soil profile; module-specific resistance factors and loads are never copied.</summary>
public static class SoilProfileTransfer
{
    public static readonly string[] Modules = [RetainingWall.Module, "geo_palo_verticale", PaloOrizzontale.Module, MicropaloOrizzontale.Module];
    private static readonly string[] Properties = ["name", "spessore", "tipologia", "peso_specifico", "peso_specifico_saturo", "angolo_attrito", "coesione_efficace", "coesione_non_drenata"];
    public static bool Supports(string module) => Modules.Contains(module);
    public static int SurveyCount(string module, JsonObject data) => module == RetainingWall.Module ? 2 : data.Array("stratigrafie").Count;
    public static JsonObject Extract(string module, JsonObject data, int survey = 0)
    {
        if (!Supports(module)) throw new ArgumentException("Modulo non compatibile con il profilo terreno comune.");
        var rows = new JsonArray();
        bool wall = module == RetainingWall.Module;
        if (wall && survey is not (0 or 1)) throw new ArgumentException("Selezionare Monte o Valle.");
        if (wall) foreach (var row in (survey == 0 ? data.Array("layers") : RetainingWall.ValleyLayers(data)).OfType<JsonObject>())
            rows.Add(J.Obj(("name", row.S("name")), ("spessore", row["thickness"]?.DeepClone()), ("tipologia", "Granulare"),
                ("peso_specifico", row["gamma"]?.DeepClone()), ("peso_specifico_saturo", row["gamma_sat"]?.DeepClone()), ("angolo_attrito", row["phi"]?.DeepClone()),
                ("coesione_efficace", 0), ("coesione_non_drenata", "")));
        else
        {
            var surveys = data.Array("stratigrafie");
            if (survey < 0 || survey >= surveys.Count || surveys[survey] is not JsonArray layers) throw new ArgumentException("Sondaggio non presente.");
            foreach (var row in layers.OfType<JsonObject>())
            {
                var copy = new JsonObject(); foreach (string key in Properties) copy[key] = row[key]?.DeepClone(); rows.Add(copy);
            }
        }
        var profile = J.Obj(("formato", "ANTHEA.Terreno"), ("versione", 1), ("source_module", module), ("source_survey", survey + 1),
            ("datum", wall ? survey == 0 ? "z=0: sommità del terreno a monte del muro" : "z=0: superficie del terreno a valle del muro" : "z=0: superficie del terreno / testa del palo"), ("layers", rows),
            ("water", J.Obj(("enabled", wall ? data["water"].B("enabled") && (survey == 0 || data["water"].D("front_head") > 0) : data["generali"].B("presenza_falda")),
                ("depth", (wall ? survey == 0 ? data["water"]?["depth"] : JsonValue.Create(Math.Max(0, RetainingWall.ValleyHeight(data) - data["water"].D("front_head"))) : data["generali"]?["profondita_falda"])?.DeepClone()))));
        Validate(profile); return profile;
    }
    public static void Validate(JsonObject profile)
    {
        if (profile.S("formato") != "ANTHEA.Terreno" || profile.D("versione") != 1 || profile["layers"] is not JsonArray { Count: > 0 and <= 50 } rows)
            throw new ArgumentException("Profilo terreno: formato non riconosciuto o numero di strati non valido (1–50).");
        foreach (var row in rows)
        {
            if (row is not JsonObject) throw new ArgumentException("Strato non valido.");
            if (row.S("tipologia") is not ("Granulare" or "Coesivo")) throw new ArgumentException("Completare la tipologia di ogni strato prima del trasferimento.");
            row.Required("spessore", strict: true); row.Required("peso_specifico", strict: true);
            if (row.Required("angolo_attrito") >= 90) throw new ArgumentException("Angolo di attrito fuori campo.");
            foreach (string key in new[] { "peso_specifico_saturo", "coesione_efficace", "coesione_non_drenata" }) if (!string.IsNullOrWhiteSpace(row.S(key))) row.Required(key);
        }
        if (profile["water"] is not JsonObject water || water["enabled"] is not JsonValue flag || !flag.TryGetValue<bool>(out bool wet)) throw new ArgumentException("Dati della falda non validi.");
        if (wet) water.Required("depth");
    }
    public static JsonObject Apply(string module, JsonObject original, JsonObject profile, int survey = 0)
    {
        Validate(profile); if (!Supports(module)) throw new ArgumentException("Modulo non compatibile con il profilo terreno comune.");
        var data = (JsonObject)original.DeepClone(); var layers = profile.Array("layers"); var water = profile["water"]!;
        if (module == RetainingWall.Module)
        {
            var rows = new JsonArray(); int i = 0;
            foreach (var row in layers.OfType<JsonObject>())
            {
                i++;
                if (row.S("tipologia") != "Granulare" || string.IsNullOrWhiteSpace(row.S("coesione_efficace")) || row.D("coesione_efficace") != 0)
                    throw new ArgumentException("Il modulo muri attuale richiede strati granulari con c′=0. Il profilo non viene modificato né convertito automaticamente.");
                double gamma = row.Required("peso_specifico"), sat = row.Required("peso_specifico_saturo"), phi = row.Required("angolo_attrito");
                if (phi < 10 || phi > 45 || gamma < 10 || sat < gamma || sat > 28 || row.D("spessore") > 100) throw new ArgumentException("Parametri del profilo fuori dal campo del modulo muri: 10°≤φ′≤45°, 10≤γ≤γsat≤28, spessore≤100 m.");
                rows.Add(J.Obj(("name", string.IsNullOrWhiteSpace(row.S("name")) ? "Strato " + i : row.S("name")), ("thickness", row["spessore"]?.DeepClone()),
                    ("gamma", gamma), ("gamma_sat", sat), ("phi", phi)));
            }
            RetainingWall.CompleteSoilInput(data);
            if (survey == 0) { data["layers"] = rows; data["water"]!["enabled"] = water.B("enabled"); data["water"]!["depth"] = water["depth"]?.DeepClone(); }
            else if (survey == 1)
            {
                double dv = RetainingWall.ValleyHeight(data);
                if (water.B("enabled") && water.D("depth") > dv) throw new ArgumentException("Falda di valle sotto il piano di posa: completare un modello idraulico dedicato; importazione non applicata.");
                data["valley"]!["layers"] = rows; data["water"]!["front_head"] = water.B("enabled") ? dv - water.D("depth") : 0;
                if (water.B("enabled")) data["water"]!["enabled"] = true;
            }
            else throw new ArgumentException("Selezionare Monte o Valle.");
            if (data["valley"].B("linked")) RetainingWall.CopySoilColumn(data, survey == 1);
            // The front water level and foundation soil are separate physical inputs, not part of the retained profile.
        }
        else
        {
            var surveys = data.Array("stratigrafie"); if (survey < 0 || survey >= surveys.Count) throw new ArgumentException("Sondaggio di destinazione non presente.");
            var rows = new JsonArray();
            foreach (var row in layers.OfType<JsonObject>())
            {
                var copy = module == "geo_palo_verticale" ? Archivio.NuovoStratoPalo() : new JsonObject();
                foreach (string key in Properties) copy[key] = row[key]?.DeepClone();
                // Density, interface factors and method-specific data must be assigned for the destination pile.
                rows.Add(copy);
            }
            surveys[survey] = rows; data["generali"]!["presenza_falda"] = water.B("enabled"); data["generali"]!["profondita_falda"] = water["depth"]?.DeepClone();
        }
        data["soil_transfer"] = J.Obj(("source_module", profile.S("source_module")), ("source_survey", profile.D("source_survey", 1)), ("datum", profile.S("datum")), ("note", Guidance(module)));
        ModuleCatalog.ValidateData(module, data); return data;
    }
    public static JsonObject Create(string module, JsonObject profile) => Apply(module, ModuleCatalog.CreateData(module), profile);
    public static string Guidance(string module) => "Copia indipendente di un profilo; profondità e falda mantengono lo stesso z=0. Nessuna traslazione automatica di quota. "
        + (module == RetainingWall.Module ? "Selezionare Monte o Valle. Controllare terreno di fondazione, battenti e copertura fino al piano di posa; il collegamento copia i materiali, non le falde. Il calcolo dei muri ammette solo strati granulari con c′=0."
        : "Completare geometria e azioni del palo, addensamento e parametri specifici del metodo. Controllare che l’indagine raggiunga la punta: il profilo non viene prolungato. Cu resta da assegnare dove richiesto; nessuna correlazione automatica da φ′.");
}
