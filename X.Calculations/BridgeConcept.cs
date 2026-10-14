using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>UI-independent concept design. SI quantities, editable rates; no code compliance claim.</summary>
public static partial class BridgeConcept
{
    public const string Module = "str_bridge_design";
    public const string Scope = "Predimensionamento a spanne · geometria ideale, stime parametriche e carichi equivalenti. Non costituisce verifica NTC, Eurocodici o AASHTO.";
    public sealed record Parameter(string Key, string Label, string Unit, double Default, double Min, double Max, double Step = .1);
    public sealed record Family(string Id, string Name, double MinSpan, double MaxSpan, double Ratio, double MinDepth, double Slab, double Spacing, bool Steel = false, bool Prestressed = false);
    public static readonly Family[] Families = GPC.Design.Bridges.BridgeConcept.Families.Select(FromNative).ToArray();
    public static readonly Parameter[] Site = GPC.Design.Bridges.BridgeConcept.Site.Select(FromNative).ToArray();
    // Zero denotes an automatic dimension. It is preserved in the archive, never replaced by a computed value.
    public static readonly Parameter[] Layout = GPC.Design.Bridges.BridgeConcept.Layout.Select(FromNative).ToArray();
    public static readonly Parameter[] Section = GPC.Design.Bridges.BridgeConcept.Section.Select(FromNative).ToArray();
    public static readonly Parameter[] Substructure = GPC.Design.Bridges.BridgeConcept.Substructure.Select(FromNative).ToArray();
    public static readonly Parameter[] Rates = GPC.Design.Bridges.BridgeConcept.Rates.Select(FromNative).ToArray();
    public static readonly Parameter[] Assumptions = GPC.Design.Bridges.BridgeConcept.Assumptions.Select(FromNative).ToArray();
    public static readonly string[] Obstacles = GPC.Design.Bridges.BridgeConcept.Obstacles.ToArray();
    public static readonly string[] Soils = GPC.Design.Bridges.BridgeConcept.Soils.ToArray();
    public static readonly string[] Piers = GPC.Design.Bridges.BridgeConcept.Piers.ToArray();
    public static readonly string[] Foundations = GPC.Design.Bridges.BridgeConcept.Foundations.ToArray();
    public static JsonObject Defaults()
    {
        JsonObject Numbers(IEnumerable<Parameter> fields) => J.Obj(fields.Select(p => (p.Key, (object?)p.Default)).ToArray());
        var input = Numbers(Site.Concat(Layout).Concat(Section).Concat(AdvancedSection).Concat(Substructure));
        input["deck_type"] = DeckTypes[0];
        input["family"] = "psc_i"; input["obstacle"] = "Fiume"; input["soil"] = "Argilla soffice";
        input["pier"] = "Setto"; input["foundation"] = "Automatica"; input["continuous"] = true;
        input["start_pier"] = false; input["end_pier"] = false; input["low_carbon"] = false; input["recycled"] = false;
        return J.Obj(("versione_bridge_design", 1), ("input", input), ("rates", Numbers(Rates)), ("assumptions", Numbers(Assumptions.Concat(AdvancedAssumptions))), ("scene", 1));
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
        foreach (var (key, fields) in new[] { ("input", Site.Concat(Layout).Concat(Section).Concat(AdvancedSection).Concat(Substructure)), ("rates", Rates.AsEnumerable()), ("assumptions", Assumptions.Concat(AdvancedAssumptions)) })
            foreach (var p in fields)
            {
                var n = J.Number(data[key]?[p.Key]);
                if (n is null || n < p.Min || n > p.Max || p.Unit == "n." && n != Math.Truncate(n.Value))
                    throw new ArgumentException($"{p.Label}: valore richiesto tra {p.Min} e {p.Max} {p.Unit}.");
            }
        var i = data["input"]!;
        if (!DeckTypes.Contains(i.S("deck_type"))) throw new ArgumentException("Tipo di impalcato non riconosciuto.");
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
        public AdvancedGeometry? Advanced { get; init; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? PriceRevision { get; init; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? EnvironmentRevision { get; init; }
        public double Length => Spans.Sum();
        public double Area => Width * Length;
        public JsonObject Json() => J.Obj(("errore", ""), ("ambito", Scope), ("versione_motore", 1), ("avvisi", Warnings), ("bridge_design", this));
    }
}
