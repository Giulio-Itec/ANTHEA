using System.Text.Json.Nodes;
using Anthea.Calculations.Geotechnics;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Geotechnics;
using GpcSlopes = GPC.Checkers.Geotechnics.Slopes;

namespace Anthea.Calculations;

/// <summary>
/// Global stability of the wall: the document (profile, deep layers, search, matrix) is read and checked here, the combinations, the section of
/// the slope and the Bishop search are GPCChecker.Geotechnics (WallGlobalStability, SlopeStability). The profile proposals only fill the tables.
/// </summary>
public static partial class RetainingWall
{
    public const string GlobalHelp = WallGlobalStability.Help;
    public static void CompleteGlobalInput(JsonObject d)
    {
        if (d["global_stability"] is not null) return;
        d["global_stability"] = J.Obj(("enabled", false), ("condition", "Drenata"), ("water_enabled", false), ("seismic", false),
            ("seismic_source", "Da sito · βs=0,38"), ("kh", ""), ("kv", ""), ("profile_confirmed", false),
            ("valley", new JsonArray()), ("uphill", new JsonArray()), ("layers", new JsonArray()), ("water", new JsonArray()),
            ("exit_min", ""), ("exit_max", ""), ("entry_min", ""), ("entry_max", ""), ("depth_min", .5), ("depth_max", ""),
            ("grid", 9), ("slices", 60), ("refinements", 4), ("search_mode", "Automatica"), ("combination_mode", "Automatiche"), ("combinations", new JsonArray()));
    }
    public static void PrepareGlobalProfile(JsonObject d)
    {
        CompleteGlobalInput(d); var global = d["global_stability"]!.AsObject(); var g = d["geometry"]!;
        double height = g.D("height") + g.D("slab"), back = g.D("toe") + g.D("stem_base"), width = back + g.D("heel"), span = Math.Max(10, height * 4);
        JsonObject Point(double x, double y) => J.Obj(("x", x), ("y", y));
        double dv = ValleyHeight(d);
        global["valley"] = new JsonArray(Point(-span, dv), Point(0, dv));
        global["uphill"] = new JsonArray(Point(back, height), Point(width + span, height));
        double bottom = height; var layers = new JsonArray();
        foreach (var layer in d.Array("layers"))
        {
            bottom -= layer.D("thickness"); layers.Add(J.Obj(("name", layer.S("name")), ("bottom", bottom), ("gamma", layer.D("gamma")), ("gamma_sat", layer.D("gamma_sat")), ("phi", layer.D("phi")), ("c", 0), ("cu", "")));
        }
        global["layers"] = layers; global["profile_confirmed"] = false;
        double valleyBottom = dv; var valleyLayers = new JsonArray();
        foreach (var layer in ValleyLayers(d)) { valleyBottom -= layer.D("thickness"); valleyLayers.Add(J.Obj(("name", layer.S("name")), ("bottom", valleyBottom), ("gamma", layer.D("gamma")), ("gamma_sat", layer.D("gamma_sat")), ("phi", layer.D("phi")), ("c", 0), ("cu", ""))); }
        global["valley_layers"] = valleyLayers; global["soil_mode"] = "Due colonne"; global["soil_split_x"] = back;
        bottom = Math.Max(bottom, valleyBottom);
        global["water_enabled"] = d["water"].B("enabled");
        double ywater = height - d["water"].D("depth");
        global["water"] = new JsonArray(Point(-span, d["water"].D("front_head")), Point(0, d["water"].D("front_head")), Point(back, ywater), Point(width + span, ywater));
        global["exit_min"] = -span; global["exit_max"] = -.1; global["entry_min"] = width + .1; global["entry_max"] = width + span;
        global["depth_max"] = bottom < -.6 ? Math.Min(-bottom, height * 2) : JsonValue.Create("");
        global["seismic"] = d["seismic"].B("enabled");
        global["combination_mode"] = "Automatiche";
        global["search_mode"] = "Automatica";
        ProposeGlobalSearch(d);
    }
    /// <summary>UI proposal only: uses the surveyed extent and the shallower known soil column; never extends geology.</summary>
    public static void ProposeGlobalSearch(JsonObject d)
    {
        var g = d["global_stability"]!; var geometry = d["geometry"]!;
        double width = geometry.D("toe") + geometry.D("stem_base") + geometry.D("heel");
        JsonNode NumberOrBlank(double value) => double.IsFinite(value) ? JsonValue.Create(value)! : JsonValue.Create("")!;
        double End(JsonArray points, string key, bool minimum) => points.Count > 0 && points.All(p => J.Number(p?[key]) is double n && double.IsFinite(n))
            ? minimum ? points.Min(p => p.D(key)) : points.Max(p => p.D(key)) : double.NaN;
        g["exit_min"] = NumberOrBlank(End(g.Array("valley"), "x", true)); g["exit_max"] = -.1;
        g["entry_min"] = width + .1; g["entry_max"] = NumberOrBlank(End(g.Array("uphill"), "x", false));
        var columns = g.S("soil_mode") == "Due colonne" ? new[] { g.Array("layers"), g.Array("valley_layers") } : new[] { g.Array("layers") };
        double bottom = columns.All(c => c.Count > 0 && J.Number(c[^1]?["bottom"]) is double n && double.IsFinite(n))
            ? columns.Max(c => c[^1].D("bottom")) : double.NaN;
        double depth = Math.Min(-bottom, 2 * (geometry.D("height") + geometry.D("slab")));
        g["depth_min"] = .1; g["depth_max"] = NumberOrBlank(depth > .1 ? depth : double.NaN);
    }
    public static string GlobalSignature(JsonObject d) => CombinationSignature(d) + "|global/" + string.Join("/", new[] { "seismic", "seismic_source", "kh", "kv" }.Select(k => d["global_stability"].S(k)));
    /// <summary>The seismic coefficients of the global stability (βs = 0.38 from the site, or assigned), null without the seismic action.</summary>
    static WallGlobalSeismic? GlobalSeismic(JsonObject input, JsonNode global)
    {
        if (!global.B("seismic")) return null;
        if (global.S("seismic_source") == "Da sito · βs=0,38")
        {
            var s = input["seismic"]!;
            if (s.S("source", SeismicManual) != SeismicSite) throw new ArgumentException("Stabilità globale: per ricavare kh dal sito selezionare i parametri del sito nel pannello Sisma, oppure assegnare kh e kv globali.");
            return WallGlobalSeismic.FromSite(Site(s));
        }
        if (global.S("seismic_source") == "kh e kv assegnati") return WallGlobalSeismic.Assigned(GlobalNumber(global, "kh", 0, .5), GlobalNumber(global, "kv", 0, .5));
        throw new ArgumentException("Sorgente dei coefficienti sismici globali non valida.");
    }

    /// <summary>
    /// The automatic combinations of the global stability from GPCChecker.Geotechnics (WallGlobalStability.Combinations: A2+M2+R2 from the ordinary ULS,
    /// accidental, SLV), as rows of the document.
    /// </summary>
    public static JsonArray GenerateGlobalCombinations(JsonObject input)
    {
        var d = (JsonObject)input.DeepClone(); Upgrade(d); var global = d["global_stability"]!;
        ValidateActions(d, false);
        var seismic = GlobalSeismic(input, global);
        var plain = (JsonObject)d.DeepClone(); plain["seismic"]!["enabled"] = false;
        var result = new JsonArray();
        foreach (var f in WallGlobalStability.Combinations(ToWallInput(plain), seismic, global.S("condition") == "Non drenata"))
        {
            var factors = new JsonObject(); foreach (var a in d.Array("actions")) factors[a.S("id")] = f.Loads.TryGetValue(a.S("id"), out double v) ? v : 0;
            string state = f.Name.StartsWith("Globale A2") ? "SLU" : f.Name.StartsWith("Globale SLV") ? "SISMA" : "ECCEZIONALE";
            result.Add(J.Obj(("enabled", true), ("name", f.Name), ("state", state), ("soil", f.SoilWeight), ("wall", f.BodyWeight),
                ("mphi", f.TanFrictionAngle), ("mc", f.EffectiveCohesion), ("mcu", f.UndrainedShearStrength), ("r", f.ResistanceFactor), ("kh", f.Kh), ("kv", f.Kv), ("coefficients", factors)));
        }
        return result;
    }
    private static double GlobalNumber(JsonNode g, string key, double min = -10000, double max = 10000)
    {
        if (J.Number(g[key]) is not double number || !double.IsFinite(number) || number < min || number > max) throw new ArgumentException($"Stabilità globale: {key}, inserire un valore fra {min} e {max}.");
        return number;
    }
    /// <summary>
    /// Global stability of the confirmed profile with GPCChecker.Geotechnics (WallGlobalStability.Calculate): the wall is a rigid body of its
    /// outline, the actions are loads of the slope. Results in m, kN, kPa and degrees, with the result of the library in <see cref="SlopeResult.Source"/>.
    /// </summary>
    public static SlopeResult CalculateGlobal(JsonObject input, CancellationToken token = default)
    {
        var d = (JsonObject)input.DeepClone(); Upgrade(d); ValidateShape(d); ValidateActions(d, false);
        var g = d["global_stability"]!.AsObject();
        if (!g.B("enabled")) throw new ArgumentException("Stabilità globale non attivata.");
        if (!g.B("profile_confirmed")) throw new ArgumentException("Controllare e confermare il profilo globale e gli strati profondi: la precompilazione è soltanto una base da adattare al sito.");
        if (g.S("condition") is not ("Drenata" or "Non drenata")) throw new ArgumentException("Condizione drenata/non drenata non valida.");
        bool undrained = g.S("condition") == "Non drenata";
        GpcSlopes.SlopePoint[] Points(string key) => g.Array(key).Select(p => new GpcSlopes.SlopePoint(GlobalNumber(p!, "x") * Mm, GlobalNumber(p!, "y") * Mm)).ToArray();
        var valley = Points("valley"); var uphill = Points("uphill"); var geometry = d["geometry"]!;
        double stem = GlobalNumber(geometry, "stem_base", .15, 8), width = GlobalNumber(geometry, "toe", 0, 12) + stem + GlobalNumber(geometry, "heel", 0, 12);
        _ = GlobalNumber(geometry, "height", .2, 15); _ = GlobalNumber(geometry, "slab", .15, 4); _ = GlobalNumber(geometry, "stem_top", .15, stem);
        if (!Families.Any(f => f.Id == d.S("family") && f.Available) || d["extensions"]!.AsObject().Count != 0) throw new ArgumentException("Stabilità globale: tipologia o componenti del muro non supportati.");
        _ = GlobalNumber(d["materials"]!, "gamma", 12, 30);
        GpcSlopes.SlopeLayer[] Layers(string key) => g.Array(key).Select(row =>
        {
            var l = row ?? throw new ArgumentException("Strato globale non valido.");
            double? cu = undrained ? GlobalNumber(l, "cu", .001) : J.Number(l["cu"]) is > 0 and double value ? value : null;
            var soil = new Soil(l.S("name") is { Length: > 0 } n ? n : "Strato", GlobalNumber(l, "gamma") * KN3, GlobalNumber(l, "gamma_sat") * KN3, GlobalNumber(l, "phi", 0, 50) * Deg,
                GlobalNumber(l, "c", 0) * KPa, "ANTHEA", undrainedShearStrength: cu * KPa);
            return new GpcSlopes.SlopeLayer(soil, GlobalNumber(l, "bottom") * Mm);
        }).ToArray();
        var layers = Layers("layers");
        if (g.S("soil_mode", "Profilo unico") is not ("Profilo unico" or "Due colonne")) throw new ArgumentException("Modalità stratigrafia globale non valida.");
        bool two = g.S("soil_mode") == "Due colonne";
        var valleyLayers = two ? Layers("valley_layers") : [];
        if (two && valleyLayers.Length == 0) throw new ArgumentException("Completare gli strati globali di valle.");
        bool automatic = g.S("combination_mode") == "Automatiche";
        if (!automatic && (g.S("combination_mode") != "Personalizzate" || g.S("combination_signature") != GlobalSignature(d))) throw new ArgumentException("Combinazioni globali non allineate alle azioni o al sisma: rigenerare o confermare la matrice globale.");
        var rows = automatic ? GenerateGlobalCombinations(d) : g.Array("combinations"); var names = new HashSet<string>();
        var factors = rows.Where(c => c.B("enabled")).Select(c =>
        {
            if (c.S("name").Length == 0 || !names.Add(c.S("name")) || c.S("state") is not ("SLU" or "SISMA" or "ECCEZIONALE")) throw new ArgumentException("Nome o stato limite della combinazione globale non valido.");
            if (c!["coefficients"] is not JsonObject f || f.Count != d.Array("actions").Count) throw new ArgumentException("Matrice globale non allineata alle azioni.");
            foreach (var action in d.Array("actions"))
            {
                double factor = GlobalNumber(f, action.S("id"), 0, 5);
                if ((!action.B("enabled") || action.S("category") == "A" && c.S("state") != "ECCEZIONALE") && factor != 0) throw new ArgumentException("Fattore globale incompatibile con l’azione.");
            }
            double kh = GlobalNumber(c, "kh", 0, .5), kv = GlobalNumber(c, "kv", -.5, .5);
            if (c.S("state") == "SISMA" && !g.B("seismic") || c.S("state") != "SISMA" && (kh != 0 || kv != 0)) throw new ArgumentException("Combinazione sismica globale incompatibile con l’attivazione del sisma.");
            return new GpcSlopes.SlopeFactors(c.S("name"), GlobalNumber(c, "soil", .1, 5), GlobalNumber(c, "wall", .1, 5), GlobalNumber(c, "mphi", .1, 5), GlobalNumber(c, "mc", .1, 5),
                GlobalNumber(c, "mcu", .1, 5), GlobalNumber(c, "r", .1, 5), kh, kv, undrained, f.ToDictionary(p => p.Key, p => J.Number(p.Value)!.Value));
        }).ToArray();
        int Integer(string key) { double v = GlobalNumber(g, key); if (v % 1 != 0) throw new ArgumentException("Valore intero richiesto: " + key); return (int)v; }
        var search = new GpcSlopes.SlopeSearch(GlobalNumber(g, "exit_min") * Mm, GlobalNumber(g, "exit_max") * Mm, GlobalNumber(g, "entry_min") * Mm, GlobalNumber(g, "entry_max") * Mm,
            GlobalNumber(g, "depth_min") * Mm, GlobalNumber(g, "depth_max") * Mm, Integer("grid"), Integer("slices"), Integer("refinements"));
        var profile = new WallGlobalProfile(valley, uphill, layers, search, valleyLayers, two ? GlobalNumber(g, "soil_split_x", 0, width) * Mm : 0, g.B("water_enabled") ? Points("water") : null, undrained);
        // The wall without its seismic options: the global seismic coefficients are those of the rows.
        var plain = (JsonObject)d.DeepClone(); plain["seismic"]!["enabled"] = false;
        var result = WallGlobalStability.Calculate(ToWallInput(plain), profile, factors, token);
        var body = result.Section.Bodies[0]; var props = GpcSlopes.SlopeGeometry.Properties(body.Polygon);
        return Slope.FromLibrary(result, [GlobalHelp, $"Muro: area GPC {props.Area / (Mm * Mm):0.####} m²; baricentro ({props.Centroid.X / Mm:0.####}; {props.Centroid.Y / Mm:0.####}) m; γ={body.UnitWeight / KN3:0.###} kN/m³. Materiali del muro condivisi con le verifiche locali GPC; parametri geotecnici del profilo globale indipendenti e dichiarati."]);
    }
    /// <summary>The checks of the global stability (WallGlobalStability.Checks): demand γR, resistance F, ratio only when it is a verdict.</summary>
    public static IEnumerable<Check> GlobalChecks(SlopeResult global) => WallGlobalStability.Checks(global.Source ?? throw new ArgumentException("Risultato globale senza il calcolo della libreria."))
        .Select((c, i) => new Check("Stabilità globale · Bishop", c.Combination, c.Demand, c.Resistance, "−", c.Ratio, global.Cases[i].Status));
}
