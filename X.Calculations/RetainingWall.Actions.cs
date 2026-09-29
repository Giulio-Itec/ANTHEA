using System.Text.Json.Nodes;
using GpcCase = GPC.Model.LoadCases.LoadCase;
using GpcCombination = GPC.Model.Combinations.Combination;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public const string ApproachHelp = "A modifica le azioni; M riduce i parametri del terreno (tan φd = tan φk / γMφ); R divide le resistenze. Per i muri NTC 2018 §6.5.3.1.1 il preset locale è Approccio 2: A1+M1+R3, con γR = 1,10 scorrimento, 1,15 ribaltamento, 1,40 portanza. A1+M1+R1 appartiene alla combinazione 1 dell’Approccio 1 e non sostituisce la verifica A2+M2+R2. La stabilità globale A2+M2+R2 richiede un’analisi dedicata. Nella matrice ogni riga espone i fattori realmente usati: modificarli produce una combinazione personalizzata, non una certificazione dell’approccio.";
    public const string SeismicHelp = "Mononobe–Okabe: muro capace di mobilitare lo stato attivo, paramento liscio verticale e riempimento orizzontale. Wood semplificato: muro rigido non cedevole, K₀ = 1−sin φ, ΔP = kh·γ·Ht², incremento uniforme con risultante a Ht/2 (idealizzazione adottata, non soluzione elastica completa di Wood). Per entrambi: terreno omogeneo asciutto. kh e ±kv assegnati oppure ricavati dai parametri del sito allo SLV: βm=0,38 per muro libero (MO), βm=1 per muro vincolato (Wood), βm maggiorato per il ribaltamento. La riduzione presuppone spostamenti compatibili con l’opera. Per Wood il sovraccarico conserva la sola componente statica K₀q. Le verifiche sismiche restano locali, con portanza sismica e spostamenti da completare.";
    public static readonly string[] ActionTypes = ["Sovraccarico uniforme", "Forza orizzontale", "Forza verticale", "Momento", "Pressione laterale", "Urto"];
    public static readonly string[] ActionCategories = ["G1", "G2", "Q", "A"];
    public static readonly string[] States = ["SLU", "SLE", "SLE_FREQ", "SLE_QP", "SISMA", "ECCEZIONALE"];
    public static string ActionUnit(string type) => type == "Momento" ? "kNm/m" : type is "Sovraccarico uniforme" or "Pressione laterale" ? "kPa" : "kN/m";
    public static JsonObject NewAction(JsonObject d, string type = "Sovraccarico uniforme") => J.Obj(("id", Guid.NewGuid().ToString("N")), ("name", type), ("type", type),
        ("category", type == "Urto" ? "A" : "Q"), ("enabled", true), ("visible", true), ("value", 10),
        ("z", d["geometry"].D("height") + d["geometry"].D("slab")), ("z0", d["geometry"].D("slab")),
        ("x", d["geometry"].D("toe") + d["geometry"].D("stem_base") - d["geometry"].D("stem_top") / 2),
        ("psi0", .7), ("psi1", .5), ("psi2", .3));
    public static void Upgrade(JsonObject d)
    {
        CompleteSeismicInput(d);
        CompleteGlobalInput(d);
        if (d.D("version") >= 2) return;
        var actions = new JsonArray(); var l = d["loads"]!;
        foreach (var (key, type) in new[] { ("surcharge", ActionTypes[0]), ("horizontal", ActionTypes[1]), ("vertical", ActionTypes[2]) })
        {
            if (l.D(key) == 0 && key != "surcharge") continue;
            var a = NewAction(d, type); a["value"] = l[key]!.DeepClone(); a["psi1"] = l["psi1"]!.DeepClone(); a["psi2"] = l["psi2"]!.DeepClone();
            a["group"] = "legacy"; actions.Add(a); // Preserve the correlation of the three original components.
        }
        d["actions"] = actions; d["combinations"] = new JsonArray(); d["combination_mode"] = "Automatiche";
        d["seismic"]!["method"] = "Mononobe–Okabe";
        d["reinforcement"]!["two_zones"] = false; d["reinforcement"]!["lower_height"] = d["geometry"].D("height") / 2;
        d["reinforcement"]!["stem_upper"] = d["reinforcement"]!["stem"]!.DeepClone(); d["version"] = 2;
    }
    public static JsonObject Defaults() { var d = LegacyDefaults(); Upgrade(d); d["seismic"]!["source"] = SeismicSite; return d; }
    public static string CombinationSignature(JsonObject d) => string.Join("|", d.Array("actions").Select(a => string.Join("/", new[] { "id", "category", "enabled", "psi0", "psi1", "psi2", "group" }.Select(k => a.S(k)))))
        + (d["seismic"].S("source", SeismicManual) == SeismicSite ? "|sito/" + string.Join("/", new[] { "enabled", "method", "ag_g", "f0", "soil_class", "ss_mode", "ss", "st_mode", "st", "topography", "slope", "relief_height", "site_height" }.Select(k => d["seismic"].S(k))) : "");
    public static JsonArray GenerateCombinations(JsonObject d)
    {
        ValidateActions(d, false);
        var actions = d.Array("actions").OfType<JsonObject>().Where(a => a.B("enabled")).ToArray();
        var groups = actions.GroupBy(a => a.S("group") is { Length: > 0 } group ? group : a.S("id")).ToArray();
        var vars = groups.Where(g => g.First().S("category") == "Q").ToArray();
        var permanent = groups.Where(g => g.First().S("category") is "G1" or "G2").ToArray();
        var native = actions.ToDictionary(a => a.S("id"), a => new GpcCase(a.S("id"), a.S("category") switch {
            "G1" => GpcCase.LoadCaseTypes.SelfWeight, "G2" => GpcCase.LoadCaseTypes.SuperImposedDeadLoad, _ => GpcCase.LoadCaseTypes.LiveLoad }));
        var result = new JsonArray(); var seen = new HashSet<string>();
        void Add(string state, string label, double wall, double soil, double water, Dictionary<string, double> factors, double kh = 0, double kv = 0, string purpose = "")
        {
            // GPC Model owns the load-case/coefficient association; the wall adapter enumerates
            // independent favourable/unfavourable G effects, per-action psi and accidental events.
            var combination = new GpcCombination(label);
            foreach (var a in actions) combination.AddLoadCaseCoefficient(native[a.S("id")], factors.GetValueOrDefault(a.S("id")));
            var coefficients = new JsonObject(); foreach (var a in d.Array("actions")) coefficients[a.S("id")] = a.B("enabled") ? combination.GetLoadCaseCoefficient(native[a.S("id")]) : 0;
            string fingerprint = $"{state}/{purpose}/{wall}/{soil}/{water}/{kh}/{kv}/" + coefficients.ToJsonString(); if (!seen.Add(fingerprint)) return;
            if (result.Count >= 4096) throw new ArgumentException("Oltre 4096 combinazioni: raggruppare le azioni correlate o predisporre una matrice personalizzata.");
            bool design = state is "SLU" or "SISMA";
            result.Add(J.Obj(("enabled", true), ("name", label + " " + (result.Count + 1)), ("state", state), ("approach", design ? "A1+M1+R3" : state == "ECCEZIONALE" ? "Eccezionale · M1/R=1" : "SLE"),
                ("wall", wall), ("soil", soil), ("water", water), ("mphi", 1), ("rslide", design ? 1.1 : 1), ("rover", design ? 1.15 : 1), ("rbearing", design ? 1.4 : 1), ("kh", kh), ("kv", kv), ("coefficients", coefficients)));
            if (state == "SISMA") result[^1]!["approach"] = "Sismica · M1/R3";
            if (purpose != "")
            {
                result[^1]!["purpose"] = purpose; result[^1]!["approach"] = "SLV · NTC 7.11.III";
                result[^1]!["rslide"] = 1; result[^1]!["rover"] = 1; result[^1]!["rbearing"] = 1.2;
            }
        }
        Dictionary<string,double> Service(string state, string? lead = null) => actions.ToDictionary(a => a.S("id"), a => a.S("category") switch {
            "G1" or "G2" => 1d, "A" => 0d, _ => state == "SLE_QP" ? a.D("psi2") : (a.S("group") is { Length: > 0 } gr ? gr : a.S("id")) == lead ? state == "SLE" ? 1 : a.D("psi1") : a.D(state == "SLE" ? "psi0" : "psi2") });
        foreach (string state in new[] { "SLE", "SLE_FREQ" }) foreach (string? lead in vars.Length == 0 ? new string?[] { null } : vars.Select(v => (string?)v.Key)) Add(state, state, 1, 1, 1, Service(state, lead));
        Add("SLE_QP", "Quasi permanente", 1, 1, 1, Service("SLE_QP"));
        int bits = vars.Length + permanent.Length + 2 + (d["water"].B("enabled") ? 1 : 0);
        if (bits > 12 || (1L << bits) * Math.Max(1, vars.Length) > 4090) throw new ArgumentException("Generazione troppo estesa: massimo 4096 combinazioni. Raggruppare i carichi correlati.");
        for (int mask = 0; mask < (1 << bits); mask++)
        {
            double wall = (mask & 1) == 0 ? 1 : 1.3, soil = (mask & 2) == 0 ? 1 : 1.3;
            int baseBit = 2; double water = 1;
            if (d["water"].B("enabled")) { water = (mask & (1 << baseBit++)) == 0 ? 1 : 1.3; }
            var f = actions.ToDictionary(a => a.S("id"), _ => 0d);
            foreach (var p in permanent) { bool high = (mask & (1 << baseBit++)) != 0; foreach (var a in p) f[a.S("id")] = a.S("category") == "G1" ? high ? 1.3 : 1 : high ? 1.5 : 0; }
            var active = vars.Where((_, i) => (mask & (1 << (baseBit + i))) != 0).ToArray();
            if (active.Length == 0) Add("SLU", "SLU", wall, soil, water, f);
            foreach (var lead in active)
            {
                foreach (var v in active) foreach (var a in v) f[a.S("id")] = 1.5 * (v.Key == lead.Key ? 1 : a.D("psi0"));
                Add("SLU", "SLU", wall, soil, water, f);
            }
        }
        if (d["seismic"].B("enabled"))
        {
            var seismic = DeriveSeismic(d);
            if (seismic is null) foreach (double kv in new[] { -d["seismic"].D("kv"), d["seismic"].D("kv") }.Distinct()) Add("SISMA", kv < 0 ? "Sisma kv−" : "Sisma kv+", 1, 1, 1, Service("SLE_QP"), d["seismic"].D("kh"), kv);
            else foreach (var (purpose, kh, magnitude) in new[] { ("Generale", seismic.Kh, seismic.Kv), ("Ribaltamento", seismic.KhOverturning, seismic.KvOverturning) })
            {
                if (kh > .4 || magnitude > .2) throw new ArgumentException($"Sisma {purpose}: kh={kh:0.###}, |kv|={magnitude:0.###} fuori dal campo del motore (kh≤0,4; |kv|≤0,2). I coefficienti non vengono troncati.");
                foreach (double kv in new[] { -magnitude, magnitude }.Distinct()) Add("SISMA", $"SLV {purpose} kv{(kv < 0 ? "−" : "+")}", 1, 1, 1, Service("SLE_QP"), kh, kv, purpose);
            }
        }
        foreach (var accident in groups.Where(g => g.First().S("category") == "A")) { var f = Service("SLE_QP"); foreach (var a in accident) f[a.S("id")] = 1; Add("ECCEZIONALE", "Eccezionale " + accident.First().S("name"), 1, 1, 1, f); }
        return result;
    }
    public static void ValidateActions(JsonObject d, bool matrix = true)
    {
        if (d.D("version") < 2) return;
        if (d["actions"] is not JsonArray actions || actions.Count > 30 || actions.Any(a => a is not JsonObject)) throw new ArgumentException("Azioni: inserire al massimo 30 righe.");
        var ids = new HashSet<string>(); double h = d["geometry"].D("height"), t = d["geometry"].D("slab"), back = d["geometry"].D("toe") + d["geometry"].D("stem_base");
        foreach (var a in actions)
        {
            if (string.IsNullOrWhiteSpace(a.S("id")) || !ids.Add(a.S("id")) || string.IsNullOrWhiteSpace(a.S("name"))) throw new ArgumentException("Azioni: identificatore univoco e nome obbligatori.");
            if (!ActionTypes.Contains(a.S("type")) || !ActionCategories.Contains(a.S("category"))) throw new ArgumentException("Tipo o natura dell’azione non supportati.");
            foreach (string b in new[] { "enabled", "visible" }) if (a![b] is not JsonValue v || !v.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione azione non valida: " + b);
            if (!a.B("enabled")) continue;
            double val = a!.Required("value"); if (val > 10000) throw new ArgumentException("Azione oltre 10000: controllare unità e valore per metro.");
            foreach (string key in new[] { "psi0", "psi1", "psi2" }) if (a.Required(key) > 1) throw new ArgumentException("Coefficienti ψ compresi fra 0 e 1.");
            if (a.D("psi2") > a.D("psi1") || a.D("psi1") > a.D("psi0")) throw new ArgumentException("Deve risultare ψ₂ ≤ ψ₁ ≤ ψ₀.");
            if (a.S("type") == "Urto" && a.S("category") != "A") throw new ArgumentException("L’urto deve avere natura eccezionale A.");
            if (a.S("type") != ActionTypes[0]) { double z = a.Required("z"); if (z < t || z > h + t) throw new ArgumentException(a.S("name") + ": quota z dal piano di posa compresa fra t e H+t."); }
            if (a.S("type") == "Pressione laterale" && (a.Required("z0") < t || a.D("z0") >= a.D("z"))) throw new ArgumentException("Pressione laterale: t ≤ z₀ < z₁ ≤ H+t.");
            if (a.S("type") == "Forza verticale") { double x = a.Required("x"), thick = d["geometry"].D("stem_top") + (d["geometry"].D("stem_base") - d["geometry"].D("stem_top")) * (h + t - a.D("z")) / h; if (x < back - thick || x > back) throw new ArgumentException("La forza verticale deve ricadere nel fusto alla quota assegnata."); }
        }
        foreach (var group in actions.Where(a => a.B("enabled") && a.S("group").Length > 0).GroupBy(a => a.S("group")))
            if (group.Select(a => string.Join("/", new[] { "category", "psi0", "psi1", "psi2" }.Select(k => a.S(k)))).Distinct().Count() > 1) throw new ArgumentException("Azioni correlate dello stesso gruppo: natura e coefficienti ψ devono coincidere.");
        if (d["seismic"].S("method") is not ("Mononobe–Okabe" or "Wood semplificato")) throw new ArgumentException("Metodo sismico non riconosciuto.");
        if (d["reinforcement"]?["two_zones"] is not JsonValue zoneFlag || !zoneFlag.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione due zone di armatura non valida.");
        if (d["reinforcement"].B("two_zones") && (d["reinforcement"]!.Required("lower_height", strict: true) >= h)) throw new ArgumentException("Altezza dell’armatura inferiore compresa fra 0 e H.");
        if (!matrix || d.S("combination_mode") == "Automatiche") return;
        if (d.S("combination_mode") != "Personalizzate" || d["combinations"] is not JsonArray { Count: > 0 and <= 4096 }) throw new ArgumentException("Matrice delle combinazioni mancante o troppo estesa.");
        if (d.S("combination_signature") != CombinationSignature(d)) throw new ArgumentException("Azioni, coefficienti ψ o parametri sismici cambiati: rigenerare le combinazioni o confermare la matrice aggiornata.");
        var names = new HashSet<string>(); bool any = false;
        foreach (var c in d.Array("combinations"))
        {
            if (c is not JsonObject || c["enabled"] is not JsonValue b || !b.TryGetValue<bool>(out _)) throw new ArgumentException("Riga combinazione non valida.");
            if (!c.B("enabled")) continue; any = true;
            if (string.IsNullOrWhiteSpace(c.S("name")) || !names.Add(c.S("name")) || !States.Contains(c.S("state"))) throw new ArgumentException("Combinazioni: nome univoco e stato limite valido obbligatori.");
            if (c.S("purpose") is not ("" or "Generale" or "Ribaltamento") || c.S("purpose") != "" && c.S("state") != "SISMA") throw new ArgumentException("Destinazione della combinazione sismica non valida.");
            foreach (string key in new[] { "wall", "soil", "water", "mphi", "rslide", "rover", "rbearing", "kh" }) if (c.Required(key, minimum: key is "mphi" or "rslide" or "rover" or "rbearing" ? .1 : 0) > (key == "kh" ? .4 : 5)) throw new ArgumentException("Coefficiente fuori campo: " + key);
            if (J.Number(c["kv"]) is not double kv || Math.Abs(kv) > .2) throw new ArgumentException("kv deve essere compreso fra −0,2 e +0,2.");
            if ((c.D("kh") != 0 || kv != 0) && (c.S("state") != "SISMA" || !d["seismic"].B("enabled"))) throw new ArgumentException("kh/kv ammessi soltanto in combinazioni SISMA con sisma abilitato.");
            if (c.S("state") == "SISMA" && !d["seismic"].B("enabled")) throw new ArgumentException("Abilitare il sisma prima di usare combinazioni sismiche.");
            if (c["coefficients"] is not JsonObject coefficients || coefficients.Count != ids.Count || coefficients.Any(p => !ids.Contains(p.Key))) throw new ArgumentException("Matrice non allineata all’elenco delle azioni: rigenerare le combinazioni.");
            foreach (var a in actions) { double f = coefficients.Required(a.S("id")); if (f > 5 || (!a.B("enabled") && f != 0) || (a.S("category") == "A" && f != 0 && c.S("state") != "ECCEZIONALE")) throw new ArgumentException("Fattore azione incompatibile con abilitazione o stato limite."); }
        }
        if (!any) throw new ArgumentException("Abilitare almeno una combinazione.");
    }
    public sealed record PressureDetail(double Z0, double Z1, double Phi, double PhiDesign, double K, double Ke, double Sigma0, double Sigma1, double Soil0, double Soil1, double Surcharge, double Water0, double Water1, double Dynamic, double Total0, double Total1);
    public sealed record AppliedAction(string Id, string Name, string Type, double Factor, double Value, double Z, double Z0, double X);
}

