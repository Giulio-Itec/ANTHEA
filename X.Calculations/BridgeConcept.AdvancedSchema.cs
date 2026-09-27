using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class BridgeConcept
{
    public static readonly string[] DeckTypes = ["Soletta in cls", "Piastra ortotropa"];
    public static bool IsExtended(string family) => family is "filler_beam" or "orthotropic" or "tied_arch" or "cable_stayed" or "suspension" or "truss";
    public static bool HasUpperStructure(string family) => family is "tied_arch" or "cable_stayed" or "suspension" or "truss";
    public static bool HasTowers(string family) => family is "cable_stayed" or "suspension";
    public static readonly Parameter[] AdvancedSection = [
        new("arch_rise", "Freccia arco / luce", "—", .2, .1, .35, .01),
        new("tower_height", "Antenna sopra impalcato · 0 = auto", "m", 0, 0, 300, 1),
        new("cable_sag", "Freccia cavo / luce centrale", "—", .1, .06, .2, .01),
        new("suspender_spacing", "Passo indicativo pendini / stralli", "m", 8, 3, 20, 1),
        new("truss_ratio", "Altezza reticolare / luce", "—", .12, .08, .25, .01),
        new("embedded_cover", "Cls sopra e sotto trave incorporata", "m", .06, .03, .15, .01),
        new("deck_plate_mm", "Lamiera piastra ortotropa", "mm", 16, 12, 40, 1),
        new("rib_height", "Altezza canaletta ortotropa", "m", .3, .15, .6, .01),
        new("rib_top", "Larghezza superiore canaletta", "m", .3, .15, .6, .01),
        new("rib_bottom", "Larghezza inferiore canaletta", "m", .18, .08, .45, .01),
        new("rib_mm", "Spessore canaletta", "mm", 8, 6, 20, 1),
        new("rib_spacing", "Interasse canalette", "m", .6, .3, 1.2, .05)
    ];
    public static readonly Parameter[] AdvancedAssumptions = [
        new("concept_tension", "Tensione di riferimento carpenteria", "MPa", 180, 50, 300, 10),
        new("concept_compression", "Compressione di riferimento archi / aste", "MPa", 100, 30, 200, 10),
        new("concept_cable", "Tensione di riferimento cavi", "MPa", 600, 200, 900, 25),
        new("concept_tower", "Compressione di riferimento antenne cls", "MPa", 6, 2, 12, .5),
        new("anchor_friction", "Attrito convenzionale blocchi ancoraggio", "—", .5, .2, .8, .05),
        new("special_connections", "Aggiunta collegamenti arco / reticolare", "%", 20, 0, 50, 5),
        new("co2_cable", "CO₂ cavi e pendini", "kg/kg", 2.5, 0, 10, .1)
    ];
    public sealed record StructuralLine(string Kind, double X1, double Z1, double X2, double Z2, int Count, double Area, double Force)
    { public double Length => Math.Sqrt(Math.Pow(X2 - X1, 2) + Math.Pow(Z2 - Z1, 2)); public double Mass => Count * Area * Length * 7.85; }
    public sealed record AdvancedGeometry(bool Orthotropic, bool GlobalBeam, double MainSpan, double Rise, double TowerHeight,
        double DeckSteel, double StructureSteel, double CableSteel, double AnchorVolume, double TowerVolume, double HorizontalForce,
        StructuralLine[] Members, TechnicalItem[] Dimensions)
    { public SupportGeometry[] FoundationSchedule { get; init; } = []; }

    /// <summary>Add only newly introduced fields to old v1 archives, without mutating the caller or masking absent legacy fields.</summary>
    public static JsonObject WithAdvancedDefaults(JsonObject source)
    {
        var d = (JsonObject)source.DeepClone();
        void Fill(string group, IEnumerable<Parameter> fields)
        { if (d[group] is JsonObject target) foreach (var p in fields) if (!target.ContainsKey(p.Key)) target[p.Key] = p.Default; }
        Fill("input", AdvancedSection); Fill("rates", Rates.Where(p => p.Key is "steel_ortho" or "cables" or "erection_special")); Fill("assumptions", AdvancedAssumptions);
        if (d["input"] is JsonObject i && !i.ContainsKey("deck_type")) i["deck_type"] = DeckTypes[0];
        if (d["alternative_a"] is JsonObject a && a["alternative_a"] is null) d["alternative_a"] = WithAdvancedDefaults(a);
        return d;
    }
    public static double ReferenceDepth(Family family, double maxSpan, bool continuous) => Math.Max(family.MinDepth,
        maxSpan / family.Ratio * (HasUpperStructure(family.Id) ? 1 : continuous ? .95 : 1.1));
}
