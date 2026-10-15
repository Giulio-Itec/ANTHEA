using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class BridgeConcept
{
    public static readonly string[] DeckTypes = GPC.Design.Bridges.BridgeConcept.DeckTypes.ToArray();
    public static bool IsExtended(string family) => GPC.Design.Bridges.BridgeConcept.IsExtended(family);
    public static bool HasUpperStructure(string family) => GPC.Design.Bridges.BridgeConcept.HasUpperStructure(family);
    public static bool HasTowers(string family) => GPC.Design.Bridges.BridgeConcept.HasTowers(family);
    public static readonly Parameter[] AdvancedSection = GPC.Design.Bridges.BridgeConcept.AdvancedSection.Select(FromNative).ToArray();
    public static readonly Parameter[] AdvancedAssumptions = GPC.Design.Bridges.BridgeConcept.AdvancedAssumptions.Select(FromNative).ToArray();
    public sealed record StructuralLine(string Kind, double X1, double Z1, double X2, double Z2, int Count, double Area, double Force)
    { public double Length => ToNative(this).Length; public double Mass => ToNative(this).Mass; }
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
}
