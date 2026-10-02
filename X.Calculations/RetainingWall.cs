using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Versioned wall document. Families and future components are separate from the active solvers.</summary>
public static partial class RetainingWall
{
    public const string Module = "geo_muri_sostegno";
    public const string Scope = "Sezione piana per metro di sviluppo. Paramento di monte verticale, riempimento orizzontale granulare (c′ = 0), spinta attiva oppure a riposo nel modello Wood. Fondazione nastriforme superficiale su terreno omogeneo drenato, con due stratigrafie e terreno a valle. Muro a mensola in c.a. o a gravità con sezione trapezia.";
    public const string Limits = "Bishop, cedimenti edometrici e spostamenti sono verifiche separate da attivare con i dati richiesti. Portanza sismica: EN 1998-5 allegato F, base ruvida e terreno granulare asciutto. Newmark: accelerogrammi e soglia di scorrimento assegnati, solo muro libero. Gravità: resistenze assegnate oppure CLS/muratura nel dominio senza trazione; fuori campo occorre analisi non lineare. Il disegno delle armature è un predimensionamento, da completare con dettagli esecutivi. Liquefazione, verifiche idrauliche e degradazione ciclica non incluse. Nessun esito complessivo dell’opera.";
    public const string Method = "Statica: Rankine per strati (K₀ nel modello Wood), pressioni efficaci e acqua separate. Preset SLU A1+M1+R3: inviluppo γG,muro e γG,terra = 1 / 1,3; γQ = 0 / 1,5; γG,acqua = 1 / 1,3, correlato a spinta e sottospinta. γR scorrimento 1,1, ribaltamento 1,15, portanza 1,4. Portanza: EN 1997-1, allegato D, fondazione nastriforme c′=0, q′ di ricoprimento da valle, Nγ=2(Nq−1)tanφ, iγ=(1−H/V)³; base ruvida δ≥φ/2. Contatto elastico senza trazione; B′=B−2|e|. Oltre |e|=B/3 la portanza è fuori campo. Passiva Rankine opzionale e parzializzabile, esclusa nel sisma.";
    public sealed record Family(string Id, string Name, bool Available);
    public static readonly Family[] Families = [new("cantilever", "Mensola in c.a.", true), new("gravity", "Gravità", true),
        new("semigravity", "Semigravità", false), new("gabions", "Gabbioni", false), new("counterforts", "Contrafforti", false),
        new("piles", "Su pali", false), new("micropiles", "Su micropali", false), new("anchors", "Con tiranti", false),
        new("key", "Con dente di fondazione", false), new("shelves", "Con mensole intermedie", false), new("basement", "Muro di cantina", false)];
    public sealed record Parameter(string Key, string Label, string Symbol, string Unit, double Default, double Min, double Max);
    public static readonly Parameter[] GeometryFields = [
        new("height", "Altezza sopra fondazione", "H", "m", 3, .2, 15), new("stem_base", "Spessore fusto al piede", "s₀", "m", .4, .15, 8),
        new("stem_top", "Spessore fusto in testa", "s₁", "m", .25, .15, 8), new("slab", "Spessore fondazione", "t", "m", .45, .15, 4),
        new("toe", "Mensola a valle", "a", "m", .8, 0, 12), new("heel", "Mensola a monte", "b", "m", 1.8, 0, 12)];
    public static readonly Parameter[] FoundationFields = [new("phi", "Attrito terreno di posa", "φ′f", "°", 34, 10, 45),
        new("gamma", "Peso terreno asciutto", "γf", "kN/m³", 19, 10, 25), new("gamma_sat", "Peso terreno saturo", "γsat,f", "kN/m³", 21, 10, 28),
        new("delta", "Attrito alla base", "δb", "°", 26, 0, 45)];
    public static readonly Parameter[] LoadFields = [new("surcharge", "Sovraccarico uniforme variabile", "qk", "kPa", 10, 0, 200),
        new("horizontal", "Forza variabile in testa verso valle", "Hk", "kN/m", 0, 0, 1000),
        new("vertical", "Forza variabile in testa (centrata)", "Nk", "kN/m", 0, 0, 2000),
        new("psi1", "Coefficiente frequente del carico", "ψ₁", "−", .5, 0, 1), new("psi2", "Coefficiente quasi permanente", "ψ₂", "−", .3, 0, 1)];
    public static readonly Parameter[] MaterialFields = [new("gamma", "Peso specifico muro", "γm", "kN/m³", 25, 12, 30),
        new("fck", "Resistenza calcestruzzo", "fck", "MPa", 30, 12, 50), new("fyk", "Snervamento armature", "fyk", "MPa", 450, 400, 600),
        new("cover", "Copriferro netto barre", "c", "mm", 40, 15, 150),
        new("compression_rd", "Resistenza di progetto a compressione", "σRd", "MPa", 5, .1, 30),
        new("shear_rd", "Resistenza di progetto a taglio", "τRd", "MPa", .2, .01, 5),
        new("tension_rd", "Resistenza di progetto a trazione fondazione", "fctd", "MPa", .3, .01, 5),
        new("creep", "Coefficiente viscosità per SLE", "φ", "−", 2, 0, 5)];
    public static readonly Parameter[] SeismicFields = [new("kh", "Coefficiente orizzontale assegnato", "kh", "−", .1, 0, .4), new("kv", "Coefficiente verticale (entrambi i segni)", "|kv|", "−", .05, 0, .2)];
    private static JsonObject Values(IEnumerable<Parameter> fields) => J.Obj(fields.Select(f => (f.Key, (object?)f.Default)).ToArray());
    public static JsonObject Layer() => J.Obj(("name", "Terreno granulare"), ("thickness", 10), ("gamma", 18), ("gamma_sat", 20), ("phi", 30));
    public static JsonObject LegacyDefaults() => J.Obj(("version", 1), ("family", "cantilever"), ("geometry", Values(GeometryFields)),
        ("foundation", Values(FoundationFields)), ("loads", Values(LoadFields)), ("materials", MaterialsDefault()),
        ("layers", new JsonArray(Layer())), ("water", J.Obj(("enabled", false), ("depth", 2), ("front_head", 0))),
        ("seismic", J.Obj(("enabled", false), ("kh", .1), ("kv", .05))),
        ("reinforcement", J.Obj(new[] { "stem", "toe", "heel" }.Select(k => (k, (object?)J.Obj(("diameter", 16), ("count", 6)))).ToArray())),
        ("extensions", new JsonObject()));
    private static JsonObject MaterialsDefault() { var m = Values(MaterialFields); m["exposure"] = "XC2"; return m; }
    public static JsonObject Example(string family)
    {
        var d = Defaults(); d["family"] = family;
        if (family == "gravity") { d["geometry"]!["stem_base"] = 2.2; d["geometry"]!["stem_top"] = .55; d["geometry"]!["toe"] = .25; d["geometry"]!["heel"] = .25; }
        return d;
    }
    public static void ValidateShape(JsonObject d)
    {
        if (d.D("version") is not (1 or 2) || !Families.Any(f => f.Id == d.S("family"))) throw new ArgumentException("Formato o tipologia del muro non supportati.");
        foreach (string key in new[] { "geometry", "foundation", "loads", "materials", "water", "seismic", "reinforcement", "extensions" })
            if (d[key] is not JsonObject) throw new ArgumentException("Dati del muro mancanti: " + key);
        if (d["layers"] is not JsonArray { Count: > 0 and <= 50 } layers || layers.Any(l => l is not JsonObject)) throw new ArgumentException("Inserire da 1 a 50 strati.");
        foreach (string key in new[] { "stem", "toe", "heel" }) if (d["reinforcement"]![key] is not JsonObject) throw new ArgumentException("Armatura mancante: " + key);
        foreach (var (group, key) in new[] { ("water", "enabled"), ("seismic", "enabled") })
            if (d[group]![key] is not JsonValue v || !v.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione non valida: " + group);
        if (d["global_stability"] is { } global)
        {
            if (global is not JsonObject) throw new ArgumentException("Formato della stabilità globale non valido.");
            foreach (string key in new[] { "enabled", "profile_confirmed", "water_enabled", "seismic" })
                if (global[key] is not JsonValue flag || !flag.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione globale non valida: " + key);
            foreach (string key in new[] { "valley", "uphill", "layers", "water", "combinations" })
                if (global[key] is not JsonArray array || array.Count > (key == "combinations" ? 256 : 100) || array.Any(x => x is not JsonObject)) throw new ArgumentException("Tabella globale non valida: " + key);
        }
    }
    private static void Validate(JsonObject d)
    {
        ValidateShape(d); ValidateActions(d);
        if (!Families.Single(f => f.Id == d.S("family")).Available) throw new ArgumentException("Tipologia predisposta per sviluppi futuri: calcolo non disponibile.");
        if (d["extensions"]!.AsObject().Count != 0) throw new ArgumentException("Componenti aggiuntivi presenti: calcolo non disponibile; non possono essere ignorati.");
        void Fields(string group, IEnumerable<Parameter> fields)
        {
            foreach (var f in fields) if (J.Number(d[group]![f.Key]) is not double n || n < f.Min || n > f.Max)
                throw new ArgumentException($"{f.Label}: inserire un valore fra {f.Min} e {f.Max} {f.Unit}.");
        }
        Fields("geometry", GeometryFields); Fields("foundation", FoundationFields); if (d.D("version") == 1) Fields("loads", LoadFields);
        Fields("materials", MaterialFields.Where(f => f.Key == "gamma" || (d.S("family") == "gravity" ? f.Key.EndsWith("_rd") : !f.Key.EndsWith("_rd"))));
        var g = d["geometry"]!; double h = g.D("height"), t = g.D("slab");
        if (g.D("stem_top") > g.D("stem_base")) throw new ArgumentException("Lo spessore in testa deve essere ≤ quello al piede.");
        ValidateSoils(d);
        if (d.D("version") == 1 && d["loads"].D("psi2") > d["loads"].D("psi1")) throw new ArgumentException("Deve risultare ψ₂ ≤ ψ₁.");
        if (d.S("family") == "cantilever" && !Ntc2018Checks.Exposures.Skip(1).Contains(matExposure(d))) throw new ArgumentException("Selezionare la classe di esposizione del calcestruzzo.");
        foreach (var l in d.Array("layers"))
        {
            foreach (string k in new[] { "thickness", "gamma", "gamma_sat", "phi" }) l!.Required(k, strict: true);
            if (l.D("thickness") > 100 || l.D("phi") < 10 || l.D("phi") > 45 || l.D("gamma") < 10 || l.D("gamma_sat") < l.D("gamma") || l.D("gamma_sat") > 28) throw new ArgumentException("Strati: 10° ≤ φ′ ≤ 45°, 10 ≤ γ ≤ γsat ≤ 28 kN/m³, spessore ≤ 100 m.");
        }
        if (d.Array("layers").Sum(l => l.D("thickness")) + 1e-9 < h + t) throw new ArgumentException("La stratigrafia deve coprire l’altezza H+t fino al piano di posa.");
        if (d["foundation"].D("gamma_sat") < d["foundation"].D("gamma")) throw new ArgumentException("Terreno di posa: γsat deve essere ≥ γ.");
        if (d["water"].B("enabled"))
        {
            double z = d["water"]!.Required("depth"), front = d["water"]!.Required("front_head");
            if (z > h + t || front > Math.Max(t, ValleyHeight(d)) || front > h + t - z) throw new ArgumentException("Falda: 0≤profondità≤H+t; battente a valle ≤max(t,Dv) e ≤battente a monte.");
        }
        if (d["seismic"].B("enabled"))
        {
            Fields("seismic", SeismicFields);
            var layers = d.Array("layers"); var first = layers[0]!;
            if (d["water"].B("enabled") || layers.Any(l => l.D("phi") != first.D("phi") || l.D("gamma") != first.D("gamma")))
                throw new ArgumentException("Pseudostatica: questa versione richiede terreno omogeneo asciutto; disattivare il sisma per il caso con falda o strati differenti.");
            if (d["seismic"].S("method") != "Wood semplificato") _ = SeismicKa(first.D("phi"), d["seismic"].D("kh"), d["seismic"].D("kv"));
        }
    }
    private static string matExposure(JsonObject d) => d["materials"].S("exposure");
    public sealed record Check(string Name, string Combination, double Demand, double? Resistance, string Unit, double? Ratio, string Status)
    {
        public string? Member { get; init; }
        public double? Position { get; init; }
    }
    public sealed record Contact(double Start, double End, double Toe, double Heel, double Peak, bool Valid)
    {
        /// <summary>The contact law of the library (mm, MPa) behind the values in m and kPa.</summary>
        [System.Text.Json.Serialization.JsonIgnore] public GPC.Checkers.Geotechnics.Walls.WallContact? Law { get; init; }
    }
    public sealed record PressureSegment(double Z0, double Z1, double P0, double P1);
    public sealed record SectionForce(string Name, double Position, double Thickness, double N, double M, double V);
    public sealed record LoadCase(string Name, string State, double WallFactor, double SoilFactor, double LiveFactor, double WaterFactor,
        double Kh, double Kv, double Horizontal, double Vertical, double Uplift, double Stabilizing, double Overturning, double X, double Eccentricity,
        double EffectiveWidth, Contact Contact, double SlidingResistance, double OverturningResistance, double? BearingResistance,
        List<PressureSegment> Pressures, List<SectionForce> Sections)
    {
        public JsonObject? Factors { get; init; }
        public List<PressureDetail> PressureDetails { get; init; } = [];
        public List<AppliedAction> Actions { get; init; } = [];
        public JsonObject SoilAudit { get; init; } = new();
        public List<PressureSegment> StemPressures { get; init; } = [];
        public List<PressureSegment> ValleyPressures { get; init; } = [];
        public List<PressureDetail> StemPressureDetails { get; init; } = [];
        public Geotechnics.SeismicBearing? SeismicBearing { get; init; }
        public string? SeismicBearingError { get; init; }
        public List<CurvaturePoint> Curvatures { get; init; } = [];
    }
    public sealed record Result(JsonObject Input, double Width, double Area, double Volume, double SteelKg, List<LoadCase> Cases, List<Check> Checks, List<Check> Structural, List<string> Notes)
    {
        public Geotechnics.SlopeResult? GlobalStability { get; init; }
        public string? GlobalError { get; init; }
        public ServiceResult? Serviceability { get; init; }
        public ReinforcementDetails? Detailing { get; init; }
        public JsonObject Json() => J.Obj(("errore", ""), ("motore", "ANTHEA.Muri/1"), ("input", Input), ("larghezza", Width), ("area", Area),
            ("volume_m3_m", Volume), ("acciaio_kg_m", SteelKg), ("combinazioni", Cases), ("verifiche_geotecniche", Checks), ("verifiche_strutturali", Structural),
            ("avvisi", Notes), ("campo", Scope), ("limiti", Limits), ("metodo", Method), ("verifica_completa", false), ("stabilita_globale", GlobalStability), ("errore_stabilita_globale", GlobalError), ("esercizio", Serviceability), ("dettagli_armature", Detailing));
    }
}

