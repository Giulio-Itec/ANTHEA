using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static string ReinforcementKey(JsonObject d, string member, double? position = null) => member == "Fusto" ? d["reinforcement"].B("two_zones") && position is double z && z < d["geometry"].D("height") - d["reinforcement"].D("lower_height") - 1e-9 ? "stem_upper" : "stem" : member == "Valle" ? "toe" : "heel";
    public static JsonObject SectionInput(JsonObject d, string member, double thickness, double? position = null)
    {
        string key = ReinforcementKey(d, member, position);
        var r = d["reinforcement"]![key]!; var m = d["materials"]!;
        double count = r.Required("count", 2), dia = r.Required("diameter", 6);
        if (count % 1 != 0 || count > 30 || dia > 40) throw new ArgumentException("Armature: da 2 a 30 barre per metro e per faccia; diametro da 6 a 40 mm.");
        double cover = m.D("cover");
        if (2 * (cover + dia) >= thickness * 1000) throw new ArgumentException(member + ": spessore insufficiente per due facce di armatura e copriferro.");
        var input = MaterialSectionInput(d);
        input["width_mm"] = 1000; input["height_mm"] = thickness * 1000; input["cover_mm"] = cover;
        input["staffe_presenti"] = "No"; input["transverse_bar_diameter_mm"] = 0; input["side_bar_count_per_side"] = 0;
        double otherCount = r.B("symmetric", true) ? count : r.Required("opposite_count", 2), otherDia = r.B("symmetric", true) ? dia : r.Required("opposite_diameter", 6);
        if (otherCount % 1 != 0 || otherCount > 30 || otherDia > 40 || 2 * cover + dia + otherDia >= thickness * 1000) throw new ArgumentException("Armatura della faccia opposta incompatibile con la sezione.");
        input["top_bar_count"] = otherCount; input["bottom_bar_count"] = count; input["top_bar_diameter_mm"] = otherDia; input["bottom_bar_diameter_mm"] = dia;
        input["fck_mpa"] = m.D("fck"); input["fyk_mpa"] = m.D("fyk"); input["axial_force_kn"] = 0; input["moment_x_knm"] = 0; input["moment_y_knm"] = 0;
        return input;
    }
    /// <summary>Verifiche strutturali delle sezioni del muro: GPC (c.a. e gravità). Il percorso storico dei muri (LegacyRetainingWall in
    /// tests/ANTHEA.Legacy, decisione D3 del 15/10/2026) si raggiunge solo con l'aggancio delle prove <see cref="ILegacyWalls"/>.</summary>
    internal static List<Check> StructuralChecks(JsonObject d, List<LoadCase> cases, CancellationToken token, out double steelKg)
    {
        if (EngineHook.Get<ILegacyWalls>() is { } legacy) return legacy.StructuralChecks(d, cases, token, out steelKg);
        steelKg = 0;
        return d.S("family") != "gravity" ? StructuralChecksNative(d, cases, token, out steelKg) : GravityChecksNative(d, cases, token);
    }
}

/// <summary>
/// Muri storici agganciati dalle prove (<see cref="EngineHook"/>, decisione D3 del 15/10/2026, modo b): verifiche strutturali e dettagli
/// d'armatura del calcolo del muro. L'implementazione (LegacyRetainingWall) sta in tests/ANTHEA.Legacy.
/// </summary>
internal interface ILegacyWalls
{
    List<RetainingWall.Check> StructuralChecks(JsonObject d, List<RetainingWall.LoadCase> cases, CancellationToken token, out double steelKg);
    /// <summary>Dettagli d'armatura storici, oppure null per quelli della libreria.</summary>
    RetainingWall.ReinforcementDetails? ReinforcementDetails(JsonObject d);
}
