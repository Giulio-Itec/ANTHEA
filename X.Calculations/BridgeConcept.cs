using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>UI-independent concept design. SI quantities, editable rates; no code compliance claim.</summary>
public static partial class BridgeConcept
{
    public const string Module = "str_bridge_design";
    public const string Scope = "Predimensionamento a spanne · geometria ideale, stime parametriche e carichi equivalenti. Non costituisce verifica NTC, Eurocodici o AASHTO.";
    public sealed record Parameter(string Key, string Label, string Unit, double Default, double Min, double Max, double Step = .1);
    public sealed record Family(string Id, string Name, double MinSpan, double MaxSpan, double Ratio, double MinDepth, double Slab, double Spacing, bool Steel = false, bool Prestressed = false);
    public static readonly Family[] Families = [
        new("slab", "Soletta piena in c.a.", 6, 15, 18, .35, 0, 1),
        new("tee", "Travi a T in c.a.", 12, 30, 17, .70, .22, 2.5),
        new("psc_i", "Travi a I in c.a.p.", 20, 50, 22.22, 1, .22, 2.5, Prestressed: true),
        new("psc_u", "Travi a U in c.a.p.", 25, 50, 22.22, 1.1, .22, 4, Prestressed: true),
        new("psc_box", "Cassone in c.a.p.", 35, 80, 22.22, 1.3, .25, 0, Prestressed: true),
        new("fcm", "Cassone a conci · altezza variabile", 80, 200, 45, 2, .28, 0, Prestressed: true),
        new("steel_i", "Travi a I acciaio–cls", 30, 90, 25, 1, .25, 3.2, Steel: true),
        new("steel_box", "Cassone acciaio–cls", 40, 150, 25, 1.2, .25, 0, Steel: true)
    ];
    public static readonly Parameter[] Site = [
        new("length", "Lunghezza totale", "m", 105, 10, 2000, 5),
        new("height", "Quota impalcato sul terreno", "m", 20, 3, 100, 1),
        new("obstacle_width", "Larghezza ostacolo", "m", 20, 0, 1000, 1),
        new("lanes", "Corsie complessive", "n.", 4, 1, 8, 1),
        new("lane_width", "Larghezza corsia", "m", 3.65, 2.5, 4.5, .05),
        new("shoulder", "Banchina per lato", "m", 1.5, 0, 4, .1),
        new("median", "Spartitraffico", "m", 1.6, 0, 5, .1),
        new("barrier_width", "Ingombro barriera per lato", "m", .5, .2, 1.5, .05)
    ];
    // Zero denotes an automatic dimension. It is preserved in the archive, never replaced by a computed value.
    public static readonly Parameter[] Layout = [new("spans", "Numero campate · 0 = auto", "n.", 5, 0, 30, 1)];
    public static readonly Parameter[] Section = [
        new("depth", "Altezza totale · 0 = auto", "m", 0, 0, 15, .05),
        new("slab", "Spessore soletta · 0 = auto", "m", 0, 0, 1, .01),
        new("spacing", "Interasse travi · 0 = auto", "m", 0, 0, 8, .1),
        new("web", "Spessore anima cls · 0 = auto", "m", 0, 0, 1.5, .01),
        new("bottom", "Soletta inferiore · 0 = auto", "m", 0, 0, 1.5, .01),
        new("bottom_ratio", "Larghezza fondo / impalcato", "—", .55, .3, .85, .05),
        new("cells", "Celle del cassone", "n.", 2, 1, 5, 1),
        new("boxes", "Cassoni metallici · 0 = auto", "n.", 0, 0, 6, 1),
        new("flange_width", "Larghezza piattabanda", "m", .55, .2, 2, .05),
        new("flange_mm", "Spessore piattabande", "mm", 24, 8, 150, 1),
        new("web_mm", "Spessore anima acciaio", "mm", 12, 6, 80, 1),
        new("web_slope", "Inclinazione anime · H per 4V", "—", 1, 0, 2, .1),
        new("haunch", "Rialzo sotto soletta", "m", .05, 0, .3, .01),
        new("u_top", "Larghezza superiore U", "m", 2.4, 1, 6, .1),
        new("u_bottom", "Larghezza inferiore U", "m", 1.4, .5, 5, .1),
        new("fc", "Resistenza cls impalcato", "MPa", 45, 20, 90, 5)
    ];
    public static readonly Parameter[] Substructure = [
        new("pile_length", "Lunghezza pali · 0 = auto", "m", 0, 0, 80, 1),
        new("pier_size", "Diametro / spessore pila · 0 = auto", "m", 0, 0, 8, .1),
        new("pile_count", "Pali per appoggio · 0 = auto", "n.", 0, 0, 64, 1),
        new("footing_size", "Lato fondazione · 0 = auto", "m", 0, 0, 40, .25),
        new("fc_sub", "Resistenza cls sottostrutture", "MPa", 35, 20, 60, 5)
    ];
    public static readonly Parameter[] Rates = [
        new("concrete_deck", "Cls impalcato", "€/m³", 240, 0, 10000, 5),
        new("concrete_sub", "Cls sottostrutture e plinti", "€/m³", 200, 0, 10000, 5),
        new("rebar", "Armatura ordinaria", "€/t", 1100, 0, 50000, 50),
        new("prestress", "Acciaio da precompressione", "€/t", 3600, 0, 50000, 50),
        new("steel", "Carpenteria metallica", "€/t", 3500, 0, 50000, 50),
        new("steel_box", "Carpenteria cassoni", "€/t", 4000, 0, 50000, 50),
        new("formwork", "Casseforme", "€/m²", 50, 0, 1000, 5),
        new("pile_1", "Palo Ø 1,0 · esclusa armatura", "€/m", 300, 0, 10000, 10),
        new("pile_15", "Palo Ø 1,5 · esclusa armatura", "€/m", 550, 0, 10000, 10),
        new("bearing", "Apparecchio d'appoggio", "€/cad", 1600, 0, 100000, 100),
        new("joint", "Giunto di dilatazione", "€/m", 2400, 0, 50000, 100),
        new("barrier", "Barriera", "€/m", 240, 0, 10000, 10),
        new("surfacing", "Pavimentazione", "€/m²", 32, 0, 1000, 1)
    ];
    public static readonly Parameter[] Assumptions = [
        new("prelims", "Oneri di cantiere", "%", 12, 0, 100, 1),
        new("contingency", "Imprevisti", "%", 15, 0, 100, 1),
        new("uncertainty", "Intervallo indicativo costo ±", "%", 30, 0, 90, 5),
        new("co2_concrete", "CO₂ cls ordinario", "kg/m³", 320, 0, 2000, 10),
        new("co2_rebar", "CO₂ armatura", "kg/kg", 1.4, 0, 10, .1),
        new("co2_steel", "CO₂ carpenteria", "kg/kg", 2, 0, 10, .1),
        new("co2_pt", "CO₂ precompressione", "kg/kg", 2.5, 0, 10, .1),
        new("co2_site", "Trasporti e cantiere / materiali", "%", 15, 0, 100, 1),
        new("low_carbon_factor", "Fattore cls a ridotta CO₂", "—", .6, .1, 1, .05),
        new("recycled_factor", "Fattore acciaio riciclato", "—", .35, .1, 1, .05),
        new("rebar_rc", "Armatura impalcato c.a.", "kg/m³", 140, 30, 500, 10),
        new("rebar_psc", "Armatura impalcato c.a.p.", "kg/m³", 110, 30, 500, 10),
        new("rebar_sub", "Armatura sottostrutture", "kg/m³", 150, 30, 500, 10),
        new("rebar_found", "Armatura fondazioni", "kg/m³", 120, 30, 500, 10),
        new("pt_density", "Precompressione", "kg/m³", 30, 5, 150, 5),
        new("wear_load", "Permanenti portati", "kN/m²", 2.5, 0, 20, .5),
        new("traffic_load", "Traffico uniforme equivalente", "kN/m²", 9, 0, 50, .5),
        new("gamma_g", "Moltiplicatore permanenti", "—", 1.35, 1, 2, .05),
        new("gamma_q", "Moltiplicatore traffico", "—", 1.5, 1, 2, .05),
        new("soil_pressure", "Pressione terreno · 0 = classe", "kPa", 0, 0, 5000, 25),
        new("pile_skin", "Resistenza laterale · 0 = classe", "kPa", 0, 0, 500, 5),
        new("pile_base", "Resistenza punta · 0 = classe", "kPa", 0, 0, 20000, 100),
        new("months_base", "Durata: avvio cantiere", "mesi", 4, 0, 36, .5),
        new("months_span", "Durata per campata", "mesi", 1.2, .1, 12, .1),
        new("months_pier", "Durata per pila", "mesi", .4, 0, 6, .1)
    ];
    public static readonly string[] Obstacles = ["Nessuno", "Fiume", "Strada / ferrovia"];
    public static readonly string[] Soils = ["Roccia", "Sabbia / ghiaia densa", "Terreno medio", "Argilla soffice"];
    public static readonly string[] Piers = ["Telaio a colonne", "Colonna circolare", "Setto", "Testa a martello"];
    public static readonly string[] Foundations = ["Automatica", "Plinto diretto", "Pali Ø 1,0 m", "Pali Ø 1,5 m"];
    public static JsonObject Defaults()
    {
        JsonObject Numbers(IEnumerable<Parameter> fields) => J.Obj(fields.Select(p => (p.Key, (object?)p.Default)).ToArray());
        var input = Numbers(Site.Concat(Layout).Concat(Section).Concat(Substructure));
        input["family"] = "psc_i"; input["obstacle"] = "Fiume"; input["soil"] = "Argilla soffice";
        input["pier"] = "Setto"; input["foundation"] = "Automatica"; input["continuous"] = true;
        input["start_pier"] = false; input["end_pier"] = false; input["low_carbon"] = false; input["recycled"] = false;
        return J.Obj(("versione_bridge_design", 1), ("input", input), ("rates", Numbers(Rates)), ("assumptions", Numbers(Assumptions)), ("scene", 1));
    }
    public static void ValidateShape(JsonObject data)
    {
        if (data.D("versione_bridge_design") != 1) throw new ArgumentException("Versione Bridge Design non supportata.");
        foreach (string key in new[] { "input", "rates", "assumptions" })
            if (data[key] is not JsonObject) throw new ArgumentException("Dati Bridge Design mancanti: " + key);
        if (data["alternative_a"] is not null)
        {
            if (data["alternative_a"] is not JsonObject a || a["alternative_a"] is not null) throw new ArgumentException("Alternativa A non valida.");
            ValidateShape(a);
        }
    }
    private static void Validate(JsonObject data)
    {
        ValidateShape(data);
        foreach (var (key, fields) in new[] { ("input", Site.Concat(Layout).Concat(Section).Concat(Substructure)), ("rates", Rates.AsEnumerable()), ("assumptions", Assumptions.AsEnumerable()) })
            foreach (var p in fields)
            {
                var n = J.Number(data[key]?[p.Key]);
                if (n is null || n < p.Min || n > p.Max || p.Unit == "n." && n != Math.Truncate(n.Value))
                    throw new ArgumentException($"{p.Label}: valore richiesto tra {p.Min} e {p.Max} {p.Unit}.");
            }
        var i = data["input"]!;
        if (!Families.Any(f => f.Id == i.S("family"))) throw new ArgumentException("Tipologia impalcato non riconosciuta.");
        foreach (var (key, choices) in new[] { ("obstacle", Obstacles), ("soil", Soils), ("pier", Piers), ("foundation", Foundations) })
            if (!choices.Contains(i.S(key))) throw new ArgumentException("Scelta non valida: " + key);
        foreach (string key in new[] { "continuous", "start_pier", "end_pier", "low_carbon", "recycled" })
            if (i[key] is not JsonValue v || !v.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione non valida: " + key);
        if (i.S("obstacle") != "Nessuno" && i.D("obstacle_width") >= i.D("length") - 4) throw new ArgumentException("L'ostacolo deve lasciare almeno 2 m per lato alle estremità del ponte.");
    }
    public sealed record Quantity(string Group, string Item, double Amount, string Unit, double Rate, double Cost, double Carbon);
    public sealed record Detail(string Name, double Value, string Unit, string Rule);
    public sealed record Station(int Span, double X, double Moment, double Shear, double DeflectionMm);
    public sealed record Support(int Index, double X, string Type, double Reaction, double PierHeight, double PierSize, int Columns, double FootingSize, double FootingWidth, int Piles);
    public sealed record Result(Family Family, double Width, double Depth, double PierDepth, double Slab, double Spacing, int Girders,
        double Web, double Bottom, double[] Spans, Support[] Supports, string Foundation, double PileDiameter, double PileLength,
        double Concrete, double Steel, double Rebar, double Prestress, double Inertia, double DirectCost, double TotalCost,
        double CostLow, double CostHigh, double Carbon, double Duration, Quantity[] Quantities, Detail[] Details, Station[] Stations, string[] Warnings)
    {
        public double Length => Spans.Sum();
        public double Area => Width * Length;
        public JsonObject Json() => J.Obj(("errore", ""), ("ambito", Scope), ("versione_motore", 1), ("avvisi", Warnings), ("bridge_design", this));
    }
}
