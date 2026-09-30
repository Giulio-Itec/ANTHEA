using System.Text.Json.Nodes;
using Anthea.Calculations.Geotechnics;
using GpcCase = GPC.Model.LoadCases.LoadCase;
using GpcCombination = GPC.Model.Combinations.Combination;

namespace Anthea.Calculations;

/// <summary>Wall/UI document adapter and NTC policy only; the generic solver has no wall dependencies.</summary>
public static partial class RetainingWall
{
    public const string GlobalHelp = "Stabilità globale: Bishop semplificato, superfici circolari sotto l’intero muro, verso valle. "
        + "Statica: Approccio 1, combinazione 2 A2+M2+R2; γG1=1, γG2=0/1,3, γQ=0/1,3, γM,tanφ=γM,c′=1,25, γM,cu=1,4, γR=1,10. "
        + "SLV del complesso muro–terreno (§§7.11.6.2.2 e 7.11.4): γA=γM=1, γR=1,20; kh=βs·amax/g con βs=0,38 e kv=±0,5kh. "
        + "βs è distinto dal βm delle spinte. La stabilità del versante naturale e i meccanismi non circolari richiedono una valutazione dedicata.";
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
    public static JsonArray GenerateGlobalCombinations(JsonObject input)
    {
        var d = (JsonObject)input.DeepClone(); Upgrade(d); var global = d["global_stability"]!;
        // Reuse GPC-backed wall action grouping/psi enumeration, converting A1 into the A2 action set.
        d["seismic"]!["enabled"] = false;
        var ordinary = GenerateCombinations(d); var result = new JsonArray(); var seen = new HashSet<string>();
        void Add(string label, string state, JsonObject coefficients, double mphi, double mc, double mcu, double resistance, double kh = 0, double kv = 0)
        {
            var native = new GpcCombination(label); var factors = new JsonObject();
            foreach (var action in d.Array("actions"))
            {
                var loadCase = new GpcCase(action.S("id"), GpcCase.LoadCaseTypes.LiveLoad);
                native.AddLoadCaseCoefficient(loadCase, coefficients.D(action.S("id"))); factors[action.S("id")] = native.GetLoadCaseCoefficient(loadCase);
            }
            string signature = $"{state}/{kh}/{kv}/" + factors.ToJsonString(); if (!seen.Add(signature)) return;
            if (result.Count >= 256) throw new ArgumentException("Oltre 256 combinazioni globali: raggruppare le azioni correlate.");
            result.Add(J.Obj(("enabled", true), ("name", label + " " + (result.Count + 1)), ("state", state), ("soil", 1), ("wall", 1),
                ("mphi", mphi), ("mc", mc), ("mcu", mcu), ("r", resistance), ("kh", kh), ("kv", kv), ("coefficients", factors)));
        }
        foreach (var row in ordinary.Where(c => c.S("state") is "SLU" or "ECCEZIONALE"))
        {
            bool staticCase = row.S("state") == "SLU"; var coefficients = (JsonObject)row!["coefficients"]!.DeepClone();
            if (staticCase) foreach (var a in d.Array("actions")) coefficients[a.S("id")] = !a.B("enabled") ? 0 : a.S("category") == "G1" ? 1 : coefficients.D(a.S("id")) * 1.3 / 1.5;
            Add(staticCase ? "Globale A2–M2–R2" : "Globale eccezionale", row.S("state"), coefficients, staticCase ? 1.25 : 1, staticCase ? 1.25 : 1, staticCase ? 1.4 : 1, staticCase ? 1.1 : 1);
        }
        if (global.B("seismic"))
        {
            double kh, kv;
            if (global.S("seismic_source") == "Da sito · βs=0,38")
            {
                var site = DeriveSeismic(input) ?? throw new ArgumentException("Stabilità globale: per ricavare kh dal sito selezionare i parametri del sito nel pannello Sisma, oppure assegnare kh e kv globali.");
                kh = .38 * site.AmaxG; kv = .5 * kh;
            }
            else if (global.S("seismic_source") == "kh e kv assegnati") { kh = GlobalNumber(global, "kh", 0, .5); kv = GlobalNumber(global, "kv", 0, .5); }
            else throw new ArgumentException("Sorgente dei coefficienti sismici globali non valida.");
            var quasi = ordinary.First(c => c.S("state") == "SLE_QP")!["coefficients"]!.AsObject();
            foreach (double sign in new[] { -kv, kv }.Distinct()) Add("Globale SLV kv" + (sign < 0 ? "−" : "+"), "SISMA", quasi, 1, 1, 1, 1.2, kh, sign);
        }
        return result;
    }
    private static double GlobalNumber(JsonNode g, string key, double min = -10000, double max = 10000)
    {
        if (J.Number(g[key]) is not double number || !double.IsFinite(number) || number < min || number > max) throw new ArgumentException($"Stabilità globale: {key}, inserire un valore fra {min} e {max}.");
        return number;
    }
    public static SlopeResult CalculateGlobal(JsonObject input, CancellationToken token = default)
    {
        var d = (JsonObject)input.DeepClone(); Upgrade(d); ValidateShape(d); ValidateActions(d, false);
        var g = d["global_stability"]!.AsObject();
        if (!g.B("enabled")) throw new ArgumentException("Stabilità globale non attivata.");
        if (!g.B("profile_confirmed")) throw new ArgumentException("Controllare e confermare il profilo globale e gli strati profondi: la precompilazione è soltanto una base da adattare al sito.");
        if (g.S("condition") is not ("Drenata" or "Non drenata")) throw new ArgumentException("Condizione drenata/non drenata non valida.");
        SlopePoint[] Points(string key) => g.Array(key).Select(p => new SlopePoint(GlobalNumber(p!, "x"), GlobalNumber(p!, "y"))).ToArray();
        var valley = Points("valley"); var uphill = Points("uphill"); var geometry = d["geometry"]!;
        double h = GlobalNumber(geometry, "height", .2, 15), t = GlobalNumber(geometry, "slab", .15, 4), a = GlobalNumber(geometry, "toe", 0, 12), stem = GlobalNumber(geometry, "stem_base", .15, 8), back = a + stem, width = back + GlobalNumber(geometry, "heel", 0, 12);
        _ = GlobalNumber(geometry, "stem_top", .15, stem);
        if (valley.Length < 2 || uphill.Length < 2 || Math.Abs(valley[^1].X) > 1e-8 || Math.Abs(valley[^1].Y - ValleyHeight(d)) > 1e-8 || Math.Abs(uphill[0].X - back) > 1e-8 || Math.Abs(uphill[0].Y - h - t) > 1e-8 || uphill[1].X < width) throw new ArgumentException("Profilo globale: valle termina in (0;Dv), monte inizia in (a+s₀;H+t); il punto successivo deve superare la fondazione. Aggiornare il profilo dopo modifiche geometriche.");
        if (!Families.Any(f => f.Id == d.S("family") && f.Available) || d["extensions"]!.AsObject().Count != 0) throw new ArgumentException("Stabilità globale: tipologia o componenti del muro non supportati.");
        var bodyPoints = Outline(d).Select(p => new SlopePoint(p[0], p[1])).ToArray();
        var body = new SlopeBody("Muro · " + d.S("family"), bodyPoints, GlobalNumber(d["materials"]!, "gamma", 12, 30));
        var props = SlopeGeometry.Properties(bodyPoints);
        if (props.Area <= 0 || geometry.D("stem_top") > stem) throw new ArgumentException("Geometria del muro non valida.");
        var surface = GlobalSurface(d, valley, uphill);
        if (uphill.Any(p => p.Y < h + t - 1e-8 && p.X <= width)) throw new ArgumentException("Il profilo globale non può scendere attraverso il riempimento sopra la fondazione.");
        SlopeSoil[] ReadSoils(string key) => g.Array(key).Select(l => new SlopeSoil(l.S("name"), GlobalNumber(l!, "bottom"), GlobalNumber(l!, "gamma"), GlobalNumber(l!, "gamma_sat"), GlobalNumber(l!, "phi", 0, 50), GlobalNumber(l!, "c", 0), g.S("condition") == "Non drenata" ? GlobalNumber(l!, "cu", .001) : J.Number(l!["cu"]) ?? 0)).ToArray();
        var soils = ReadSoils("layers");
        if (g.S("soil_mode", "Profilo unico") is not ("Profilo unico" or "Due colonne")) throw new ArgumentException("Modalità stratigrafia globale non valida.");
        var loads = d.Array("actions").Where(l => l.B("enabled")).Select(l => l.S("type") switch
        {
            "Sovraccarico uniforme" => new SlopeLoad(l.S("id"), back, uphill[^1].X, h + t, l.D("value"), 0, 0, true),
            "Forza verticale" => new SlopeLoad(l.S("id"), l.D("x"), l.D("x"), l.D("z"), l.D("value"), 0, 0, false),
            "Momento" => new SlopeLoad(l.S("id"), back, back, l.D("z"), 0, 0, l.D("value"), false),
            "Pressione laterale" => new SlopeLoad(l.S("id"), back, back, (l.D("z") + l.D("z0")) / 2, 0, l.D("value") * (l.D("z") - l.D("z0")), 0, false),
            _ => new SlopeLoad(l.S("id"), back, back, l.D("z"), 0, l.D("value"), 0, false)
        }).ToArray();
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
            return new SlopeFactors(c.S("name"), GlobalNumber(c, "soil", .1, 5), GlobalNumber(c, "wall", .1, 5), GlobalNumber(c, "mphi", .1, 5), GlobalNumber(c, "mc", .1, 5), GlobalNumber(c, "mcu", .1, 5), GlobalNumber(c, "r", .1, 5), kh, kv, g.S("condition") == "Non drenata", f.ToDictionary(p => p.Key, p => J.Number(p.Value)!.Value));
        }).ToArray();
        int Integer(string key) { double v = GlobalNumber(g, key); if (v % 1 != 0) throw new ArgumentException("Valore intero richiesto: " + key); return (int)v; }
        var search = new SlopeSearch(GlobalNumber(g, "exit_min"), GlobalNumber(g, "exit_max"), GlobalNumber(g, "entry_min"), GlobalNumber(g, "entry_max"), GlobalNumber(g, "depth_min"), GlobalNumber(g, "depth_max"), Integer("grid"), Integer("slices"), Integer("refinements"));
        var section = new SlopeSection(surface, soils, g.B("water_enabled") ? Points("water") : [], [body], loads, 0, width)
        { ValleySoils = g.S("soil_mode") == "Due colonne" ? ReadSoils("valley_layers") : [], SoilSplitX = g.S("soil_mode") == "Due colonne" ? GlobalNumber(g, "soil_split_x", 0, width) : 0 };
        if (g.S("soil_mode") == "Due colonne" && section.ValleySoils.Length == 0) throw new ArgumentException("Completare gli strati globali di valle.");
        var result = SlopeStability.Calculate(section, search, factors, token);
        return result with { Notes = result.Notes.Concat(new[] { GlobalHelp, $"Muro: area GPC {props.Area:0.####} m²; baricentro ({props.Centroid.X:0.####}; {props.Centroid.Y:0.####}) m; γ={body.Gamma:0.###} kN/m³. Materiali del muro condivisi con le verifiche locali GPC; parametri geotecnici del profilo globale indipendenti e dichiarati." }).ToArray() };
    }
    public static IEnumerable<Check> GlobalChecks(SlopeResult global) => global.Cases.Select(c => new Check("Stabilità globale · Bishop", c.Factors.Name, c.Factors.R, c.Critical?.Factor, "−", c.Critical is not null && (c.Critical.Ratio > 1 || !c.Boundary && c.NumericalFailures == 0) ? c.Critical.Ratio : null, c.Status));
}
