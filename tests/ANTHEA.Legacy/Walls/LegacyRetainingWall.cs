// Spostato da X.Calculations/RetainingWall.Structures.cs, RetainingWall.Reinforcement.cs e RetainingWall.BarSchedule.cs (commit 4144fe9e)
// con la decisione D3 del 15/10/2026: percorso storico dei muri (verifiche strutturali delle sezioni, dettagli d'armatura, distinta e
// ricerca delle armature precedenti), sostituito da GPC nel prodotto. Codice storico conservato per le prove, fuori dall'installer.
// I corpi sono invariati salvo: motore SLE dall'aggancio (ServiceabilityEngines) invece del parametro, scelta del ramo della gravità
// dal modo dell'aggancio, chiamate al prodotto qualificate con RetainingWall.
using System.Text.Json.Nodes;
using Materiali;
using static Anthea.Calculations.RetainingWall;

namespace Anthea.Calculations;

/// <summary>Motore dei muri: GPC (prodotto) o percorso storico (solo prove). Spostato da RetainingWall.Library.Details.cs (decisione D3).</summary>
public enum WallCalculationEngine { Library, Legacy }

/// <summary>
/// Percorso storico dei muri a mensola e a gravità (decisione D3 del 15/10/2026, modo b). Le prove lo agganciano al calcolo del muro con
/// <see cref="Use"/> o con i metodi che riproducono le firme di prima (<see cref="Calculate"/>, <see cref="DesignReinforcement"/>).
/// </summary>
public static partial class LegacyRetainingWall
{
    /// <summary>
    /// Aggancia il percorso storico al calcolo del muro (RetainingWall.Calculate) per la durata dell'ambito, come il parametro wallEngine di
    /// prima: Legacy dà verifiche strutturali (anche della gravità) e dettagli storici; Library o null tolgono l'aggancio.
    /// </summary>
    public static IDisposable Use(WallCalculationEngine? engine) => Use(engine == WallCalculationEngine.Legacy, false);

    // Prima della decisione D3 un motore SLE esplicito (anche Library) portava le sezioni in c.a. sul percorso storico, lasciando GPC per
    // la gravità e per i dettagli; wallEngine Legacy portava sul percorso storico tutto.
    static IDisposable Use(bool wallLegacy, bool explicitServiceability)
        => EngineScope.Set<ILegacyWalls>(wallLegacy || explicitServiceability ? new Hook(wallLegacy) : null);

    /// <summary>Sezioni in c.a. dei muri sul percorso storico, gravità e dettagli con GPC: l'effetto di un motore SLE esplicito prima della
    /// decisione D3 (il motore SLE si sceglie a parte con ServiceabilityEngines).</summary>
    public static IDisposable UseExplicitServiceability() => Use(false, true);

    /// <summary>RetainingWall.Calculate con il motore SLE e il motore dei muri, con la semantica dei parametri di prima della decisione D3.</summary>
    public static Result Calculate(JsonObject data, CancellationToken token = default, ServiceabilityEngine? serviceabilityEngine = null,
        WallCalculationEngine wallEngine = WallCalculationEngine.Library)
    {
        using var walls = Use(wallEngine == WallCalculationEngine.Legacy, serviceabilityEngine is not null);
        using var serviceability = ServiceabilityEngines.Use(serviceabilityEngine);
        return RetainingWall.Calculate(data, token);
    }

    sealed class Hook(bool wallLegacy) : ILegacyWalls
    {
        public List<Check> StructuralChecks(JsonObject d, List<LoadCase> cases, CancellationToken token, out double steelKg)
            => LegacyRetainingWall.StructuralChecks(d, cases, token, out steelKg, wallLegacy);

        public ReinforcementDetails? ReinforcementDetails(JsonObject d) => wallLegacy ? CalculateReinforcementDetails(d) : null;
    }

    /// <summary>Verifiche strutturali storiche delle sezioni (prima RetainingWall.StructuralChecks). Il motore SLE è quello dell'aggancio
    /// corrente. Restano regole dei muri, fuori dall'adattatore, armatura minima e massima, taglio senza staffe e criterio «Non fessurata»
    /// (F2.7-D4). <paramref name="wallLegacy"/> falso: muro a gravità con GPC, come con il solo motore SLE esplicito.</summary>
    static List<Check> StructuralChecks(JsonObject d, List<LoadCase> cases, CancellationToken token, out double steelKg, bool wallLegacy)
    {
        var result = new List<Check>(); steelKg = 0; var m = d["materials"]!;
        void Stamp(int start, SectionForce force) { for (int i = start; i < result.Count; i++) result[i] = result[i] with { Member = force.Name, Position = force.Position }; }
        if (d.S("family") == "gravity")
        {
            if (!wallLegacy) return RetainingWall.GravityChecksNative(d, cases, token);
            if (d["gravity_design"].S("type") != "Resistenze assegnate") return GravityChecks(d, cases);
            foreach (var c in cases.Where(c => c.State is "SLU" or "SISMA" or "ECCEZIONALE")) foreach (var f in c.Sections)
            {
                int firstCheck = result.Count;
                if (f.Name != "Fusto" && !c.Contact.Valid) { result.Add(CheckValue(f.Name + " · resistenza", c.Name, 0, null, "MPa", "Reazioni non disponibili: perdita di equilibrio")); continue; }
                if (f.Position <= 0 && f.N == 0 && f.V == 0 && f.M == 0) continue;
                string label = f.Name + " z=" + f.Position.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m";
                double max = f.N / f.Thickness / 1000 + 6 * Math.Abs(f.M) / (f.Thickness * f.Thickness) / 1000;
                result.Add(CheckValue(label + " · compressione", c.Name, max, m.D("compression_rd"), "MPa"));
                if (f.Name == "Fusto") result.Add(CheckValue(label + " · assenza trazione", c.Name, Math.Abs(f.M), f.N * f.Thickness / 6, "kNm/m"));
                else result.Add(CheckValue(label + " · flessione fondazione", c.Name, max, m.D("tension_rd"), "MPa"));
                result.Add(CheckValue(label + " · taglio", c.Name, 1.5 * Math.Abs(f.V) / f.Thickness / 1000, m.D("shear_rd"), "MPa"));
                Stamp(firstCheck, f);
            }
            return result;
        }
        var geometry = d["geometry"]!;
        bool two = d["reinforcement"].B("two_zones"); double lower = two ? d["reinforcement"].D("lower_height") : geometry.D("height");
        foreach (var (key, length) in new[] { ("stem", lower), ("stem_upper", two ? geometry.D("height") - lower : 0), ("toe", geometry.D("toe") + geometry.D("stem_base") / 2), ("heel", geometry.D("heel") + geometry.D("stem_base") / 2) })
        {
            if (length == 0) continue;
            var arm = d["reinforcement"]![key]!;
            double other = arm.B("symmetric", true) ? arm.D("count") * Math.Pow(arm.D("diameter"), 2) : arm.D("opposite_count") * Math.Pow(arm.D("opposite_diameter"), 2);
            steelKg += (arm.D("count") * Math.Pow(arm.D("diameter"), 2) + other) * Math.PI / 4 * 1e-6 * length * 7850;
        }
        var cache = new Dictionary<(string, double), (JsonObject Input, JsonObject Ws, CheckerSection Ultimate, CheckerSection Service, JsonObject Options)>();
        var moments = new Dictionary<(string, double, double, int), double?>();
        var uncracked = new Dictionary<(string, double), CheckerSection>();
        foreach (var c in cases) foreach (var f in c.Sections.Where(s => s.Position > 0 || s.N != 0 || s.V != 0 || s.M != 0))
        {
            int firstCheck = result.Count;
            token.ThrowIfCancellationRequested();
            string label = f.Name + " z=" + f.Position.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m";
            if (f.Name != "Fusto" && !c.Contact.Valid) { result.Add(CheckValue(label + " · resistenza", c.Name, 0, null, "kNm/m", "Reazioni non disponibili: perdita di equilibrio")); continue; }
            // Input errors propagate. Native convergence failures produce an explicit missing verification.
            var input = SectionInput(d, f.Name, f.Thickness, f.Position); string memberKey = ReinforcementKey(d, f.Name, f.Position);
            string tension = f.M >= 0 ? "bottom" : "top";
            double dia = input.D(tension + "_bar_diameter_mm"), asFace = input.D(tension + "_bar_count") * Math.PI * dia * dia / 4;
            double eff = f.Thickness * 1000 - m.D("cover") - dia / 2;
            if (!cache.TryGetValue((memberKey, f.Thickness), out var engines))
            {
                var def = SezioneCA.DefaultData(); def["input"] = input.DeepClone();
                var ws = SectionWorkspace.Prepare(def);
                var opt = ws["sle"]!["SLE_QP"]!.AsObject(); opt["modello"] = "Lineare"; opt["trazione_cls"] = "No"; opt["phi"] = m.D("creep");
                opt["esposizione"] = m.S("exposure"); opt["sensibilita"] = "Non sensibile"; opt["aderenza"] = "Migliorata"; opt["durata"] = "Lunga";
                opt["spaziatura_fessure"] = new[] { "top", "bottom" }.Max(face => (1000 - 2 * m.D("cover") - input.D(face + "_bar_diameter_mm")) / (input.D(face + "_bar_count") - 1)); opt["copriferro_fessure"] = m.D("cover");
                engines = (input, ws, new CheckerSection(input, ws, J.Obj(("criterio", "N costante"), ("modello", "Non lineare"))), new CheckerSection(input, ws, opt, "SLU"), opt);
                cache[(memberKey, f.Thickness)] = engines;
                double minimumDepth = f.Thickness * 1000 - m.D("cover") - Math.Min(input.D("top_bar_diameter_mm"), input.D("bottom_bar_diameter_mm")) / 2;
                double minimum = Math.Max(.26 * ConcreteMaterials.Concrete(input).Fctm / m.D("fyk"), .0013) * 1000 * minimumDepth;
                if (f.Name == "Fusto") minimum = Math.Max(minimum, .001 * 1000 * f.Thickness * 1000);
                double topAs = input.D("top_bar_count") * Math.PI * Math.Pow(input.D("top_bar_diameter_mm"), 2) / 4;
                double bottomAs = input.D("bottom_bar_count") * Math.PI * Math.Pow(input.D("bottom_bar_diameter_mm"), 2) / 4;
                result.Add(CheckValue(label + " · armatura minima per faccia", "Dettagli", minimum, Math.Min(topAs, bottomAs), "mm²/m"));
                result.Add(CheckValue(label + " · armatura massima", "Dettagli", topAs + bottomAs, .04 * 1000 * f.Thickness * 1000, "mm²/m"));
            }
            try
            {
                if (c.State is "SLU" or "SISMA" or "ECCEZIONALE")
                {
                    int sign = f.M >= 0 ? 1 : -1; var key = (memberKey, f.Thickness, f.N, sign);
                    if (!moments.TryGetValue(key, out double? resistance))
                    {
                        var value = SectionMomentResistance.Calculate(engines.Ultimate, -f.N, [("Mx", sign, 0d)], token)[0];
                        resistance = value.Moment is double mv ? Math.Abs(mv) : null; moments[key] = resistance;
                    }
                    result.Add(CheckValue(label + " · N–M GPC", c.Name, Math.Abs(f.M), resistance, "kNm/m", "GPC: resistenza non convergente o N fuori dominio"));
                    double k = Math.Min(2, 1 + Math.Sqrt(200 / eff)), rho = Math.Min(.02, asFace / (1000 * eff));
                    double vrd = Math.Max(.18 / input.D("gamma_c") * k * Math.Pow(100 * rho * m.D("fck"), 1d / 3), .035 * Math.Pow(k, 1.5) * Math.Sqrt(m.D("fck"))) * 1000 * eff / 1000;
                    result.Add(CheckValue(label + " · taglio senza staffe", c.Name, Math.Abs(f.V), vrd, "kN/m"));
                }
                else
                {
                    var action = new ActionPoint(-f.N, f.M, 0); var state = engines.Service.Stress(action, c.State);
                    // GPC stores strain gradients: Mx bends across Y, hence ChiY (not ChiX).
                    if (f.Name == "Fusto") c.Curvatures.Add(new(d["geometry"].D("height") - f.Position, Math.Sign(f.M) * Math.Abs(state.Native.StrainPlane.ChiY) * 1000));
                    if (state.ConcreteStressLimit is double limit) result.Add(CheckValue(label + " · tensione CLS", c.Name, Math.Max(0, -state.sigma_cls), limit, "MPa"));
                    if (c.State == "SLE") result.Add(CheckValue(label + " · tensione acciaio", c.Name, state.sigma_acciaio, state.SteelStressLimit, "MPa"));
                    if (c.State != "SLE")
                    {
                        var crack = ConcreteServiceabilityAdapter.Cracking(engines.Service, state, action, input, engines.Ws, engines.Options, c.State);
                        // In weakly stressed sections the no-tension solution may have no tensile
                        // reinforcement in the effective zone. Establish absence of cracking with
                        // a separate GPC uncracked analysis, never turn arbitrary missing checks into passes.
                        if (crack.Width is null && crack.Status is "Nessuna armatura tesa" or "Armatura/area efficace assente")
                        {
                            if (!uncracked.TryGetValue((memberKey, f.Thickness), out var elastic))
                            {
                                var elasticOptions = (JsonObject)engines.Options.DeepClone(); elasticOptions["trazione_cls"] = "Sì";
                                elastic = new CheckerSection(input, engines.Ws, elasticOptions, "SLU"); uncracked[(memberKey, f.Thickness)] = elastic;
                            }
                            var elasticState = elastic.Stress(action, c.State);
                            double tensile = elasticState.Native.GetConcreteVerticesTension(m.D("creep")).Max(x => x.tension);
                            double fctk = ConcreteMaterials.Concrete(input).Fctk05;
                            if (tensile <= fctk)
                            {
                                result.Add(CheckValue(label + " · fessurazione", c.Name, 0, crack.Limit, "mm") with { Status = $"Non fessurata: σt,el={tensile:0.###} ≤ fctk,0.05={fctk:0.###} MPa (GPC)" });
                                Stamp(firstCheck, f); continue;
                            }
                        }
                        result.Add(crack.Width is double wk ? CheckValue(label + " · fessurazione", c.Name, wk, crack.Limit, "mm", crack.Status) : CheckValue(label + " · fessurazione", c.Name, 0, null, "mm", crack.Status));
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { result.Add(CheckValue(label + " · " + c.State, c.Name, Math.Abs(f.M), null, "kNm/m", "Non calcolata: " + ex.Message)); }
            Stamp(firstCheck, f);
        }
        return result;
    }

    /// <summary>Dettagli d'armatura storici (prima RetainingWall.CalculateReinforcementDetails con WallCalculationEngine.Legacy).</summary>
    public static ReinforcementDetails CalculateReinforcementDetails(JsonObject input)
    {
        var d = (JsonObject)input.DeepClone(); CompleteAdvancedInput(d);
        var g = d["geometry"]!; var m = d["materials"]!; var opt = d["detailing"]!; var r = d["reinforcement"]!;
        double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), s = g.D("stem_base"), b = a + s + g.D("heel"), back = a + s;
        bool two = r.B("two_zones"); double split = two ? r.D("lower_height") : h;
        var section = SectionInput(d, "Fusto", s); var workspace = SectionWorkspace.Prepare(SezioneCA.DefaultData());
        var strengths = ConcreteMaterials.DesignValues(section, workspace); var concrete = ConcreteMaterials.Concrete(section);
        var bond = new ConcreteAnchorageCalculator(); var bars = new List<BarDetail>(); var checks = new List<Check>(); double kg = 0;
        double aggregate = AdvancedNumber(opt, "aggregate", 4, 63), clearLap = AdvancedNumber(opt, "lap_clear", 0, 100);
        double cover = m.D("cover"), percent = AdvancedNumber(opt, "lap_percent", 1, 100);
        void Max(string name, double actual, double limit) => checks.Add(CheckValue(name, "Dettagli", actual, limit, "mm"));
        void Min(string name, double actual, double required, string unit = "mm") => checks.Add(CheckValue(name, "Dettagli", required, actual, unit));
        foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
        {
            if (key == "stem_upper" && !two || key == "toe" && a == 0 || key == "heel" && g.D("heel") == 0) continue;
            var arm = r[key]!; bool stem = key.StartsWith("stem"), upper = key == "stem_upper";
            double secondaryDia = AdvancedNumber(arm, "secondary_diameter", 6, 32), secondaryStep = AdvancedNumber(arm, "secondary_spacing", 50, 500);
            for (int face = 0; face < 2; face++)
            {
                double dia = arm.D(face == 0 || arm.B("symmetric", true) ? "diameter" : "opposite_diameter");
                double count = arm.D(face == 0 || arm.B("symmetric", true) ? "count" : "opposite_count");
                if (count < 2 || count > 30 || count % 1 != 0 || dia < 6 || dia > 40) throw new ArgumentException("Armature: diametri 6–40 mm, 2–30 barre intere per metro.");
                string label = key + " · " + (stem ? face == 0 ? "monte" : "valle" : face == 0 ? "inferiore" : "superiore");
                double spacing = (1000 - 2 * cover - dia) / (count - 1), area = count * Math.PI * dia * dia / 4;
                Min(label + " · interferro", spacing - dia, Math.Max(20, Math.Max(dia, aggregate + 5)));
                Max(label + " · interasse principale", spacing, stem ? Math.Min(3 * g.D("stem_top") * 1000, 400) : Math.Min(2 * t * 1000, 250));
                int life = (int)AdvancedNumber(opt, "life", 50, 100); if (life is not (50 or 100)) throw new ArgumentException("Vita per il copriferro: 50 o 100 anni.");
                Min(label + " · copriferro", cover, MaterialCover.Required(CoverMaterialState(d), m.D("fck"), Math.Max(dia, secondaryDia)));
                var dev = bond.Calculate(new(dia, strengths.Fyd, concrete.Fctk05, section.D("gamma_c"), opt.B("good_bond"), 0, false, percent, clearLap));
                var lap = bond.Calculate(new(dia, strengths.Fyd, concrete.Fctk05, section.D("gamma_c"), opt.B("good_bond"), 0, true, percent, clearLap));
                double lapUsed = stem && two ? (arm.D("lap_length") == 0 ? Math.Ceiling(lap.RequiredLength / 10) * 10 : AdvancedNumber(arm, "lap_length", 0, 10000)) : 0;
                if (stem && two)
                {
                    var twin = r[upper ? "stem" : "stem_upper"]!;
                    double twinDia = twin.D(face == 0 || twin.B("symmetric", true) ? "diameter" : "opposite_diameter");
                    double twinRequired = bond.Calculate(new(twinDia, strengths.Fyd, concrete.Fctk05, section.D("gamma_c"), opt.B("good_bond"), 0, true, percent, clearLap)).RequiredLength;
                    double commonRequired = Math.Max(lap.RequiredLength, twinRequired);
                    if (arm.D("lap_length") == 0) lapUsed = Math.Ceiling(commonRequired / 10) * 10;
                    double twinLength = twin.D("lap_length") == 0 ? Math.Ceiling(commonRequired / 10) * 10 : AdvancedNumber(twin, "lap_length", 0, 10000);
                    Min(label + " · sovrapposizione effettiva comune", (lapUsed + twinLength) / 2, commonRequired);
                    Min(label + " · interferro tra coppie giuntate", spacing - dia - twinDia - clearLap, Math.Max(20, Math.Max(Math.Max(dia, twinDia), aggregate + 5)));
                    Max(label + " · distanza barre giuntate", clearLap, Math.Min(50, 4 * dia));
                    Min(label + " · percentuale giuntata alla quota h₁", percent, 100, "%");
                    Max(label + " · ingombro sovrapposizione", lapUsed, Math.Max(0, 2 * Math.Min(split, h - split) * 1000 - 2 * cover));
                    var other = r[upper ? "stem" : "stem_upper"]!;
                    double otherCount = other.D(face == 0 || other.B("symmetric", true) ? "count" : "opposite_count");
                    checks.Add(CheckValue(label + " · corrispondenza barre nelle due zone", "Dettagli", Math.Abs(count - otherCount), 0, "barre"));
                }
                double secArea = Math.PI * secondaryDia * secondaryDia / 4 * 1000 / secondaryStep;
                double thickness = stem ? (upper ? s - (s - g.D("stem_top")) * split / h : s) : t;
                Min(label + " · armatura secondaria", secArea, stem ? Math.Max(.25 * area, .0005 * thickness * 1e6) : .2 * area, "mm²/m");
                Max(label + " · passo secondaria", secondaryStep, stem ? 400 : Math.Min(3 * t * 1000, 400));
                double c = (cover + dia / 2) / 1000, anchor = arm.D("anchor_length") == 0 ? Math.Ceiling(dev.RequiredLength / 10) * 10 : AdvancedNumber(arm, "anchor_length", 0, 10000);
                var path = new List<BarPoint>(); double mandrel = 0;
                if (stem)
                {
                    double y0 = upper ? t + split - lapUsed / 2000 : t;
                    double y1 = upper || !two ? t + h - c : t + split + lapUsed / 2000;
                    double X(double y) => face == 0 ? back - c : a + (s - g.D("stem_top")) * Math.Max(0, y - t) / h + c * Math.Sqrt(1 + Math.Pow((s - g.D("stem_top")) / h, 2));
                    path.Add(new(X(y1), y1)); path.Add(new(X(y0), y0));
                    if (!upper)
                    {
                        double ab = Math.Min(cover + dia / 2, spacing / 2);
                        double requiredMandrel = Math.Max((dia <= 16 ? 4 : 7) * dia, Math.PI * dia * dia / 4 * strengths.Fyd / strengths.Fcd * (1 / ab + 1 / (2 * dia)));
                        mandrel = arm.D("bend_diameter") == 0 ? Math.Ceiling(requiredMandrel / 10) * 10 : AdvancedNumber(arm, "bend_diameter", 1, 2000);
                        Min(label + " · mandrino (acciaio e pressione CLS)", mandrel, requiredMandrel);
                        double radius = (mandrel + dia) / 2000, vertical = t - c - radius, direction = face == 0 ? 1 : -1;
                        Min(label + " · altezza per la piega", (t - 2 * c) * 1000, radius * 1000);
                        double tail = Math.Max(5 * dia / 1000, anchor / 1000 - Math.Max(0, vertical) - radius * Math.PI / 2);
                        double x = X(t), y = c + radius;
                        path.Add(new(x, y));
                        for (int j = 1; j <= 12; j++) { double angle = j * Math.PI / 24; path.Add(new(x + direction * radius * (1 - Math.Cos(angle)), y - radius * Math.Sin(angle))); }
                        path.Add(new(x + direction * (radius + tail), c));
                        double available = face == 0 ? b - c - x : x - c;
                        Max(label + " · ingombro coda", (radius + tail) * 1000, Math.Max(0, available) * 1000);
                        anchor = Math.Max(0, vertical) * 1000 + radius * Math.PI / 2 * 1000 + tail * 1000;
                        Min(label + " · ancoraggio al piede", anchor, dev.RequiredLength);
                    }
                    else anchor = 0;
                }
                else
                {
                    double root = key == "toe" ? a : back, direction = key == "toe" ? 1 : -1, end = key == "toe" ? c : b - c;
                    double y = face == 0 ? c : t - c;
                    path.Add(new(end, y)); path.Add(new(root + direction * anchor / 1000, y));
                    Min(label + " · ancoraggio oltre la radice", anchor, dev.RequiredLength);
                    Max(label + " · ingombro ancoraggio", anchor, Math.Max(0, key == "toe" ? b - c - root : root - c) * 1000);
                }
                double length = path.Zip(path.Skip(1), (p, q) => double.Hypot(p.X - q.X, p.Y - q.Y)).Sum();
                // The sampled arc is only for drawing; cut length uses its exact centre-line development.
                if (mandrel > 0) length += (mandrel + dia) / 2000 * (Math.PI / 2 - 24 * Math.Sin(Math.PI / 48));
                bars.Add(new("P" + (bars.Count + 1), key, label.Split(" · ")[1], dia, count, spacing, area, length, upper ? 0 : dev.RequiredLength, anchor, stem && two ? lap.RequiredLength : 0, lapUsed, mandrel, dev.Fbd, path));
                kg += area * 1e-6 * length * 7850;
                double regionLength = stem ? (upper ? h - split : split) : key == "toe" ? a + s / 2 : g.D("heel") + s / 2;
                kg += secArea * 1e-6 * regionLength * 7850;
            }
        }
        if (two)
        {
            double phi = bars.Where(x => x.Zone.StartsWith("stem")).Max(x => x.Diameter), l0 = bars.Max(x => x.Lap);
            double ties = AdvancedNumber(opt, "tie_diameter", 6, 32), step = AdvancedNumber(opt, "tie_spacing", 50, 400);
            Max("Giunzione · passo collegamenti fra facce", step, Math.Min(150, 12 * phi));
            Min("Giunzione · area trasversale in ciascun terzo estremo", Math.Floor(l0 / 3 / step) * Math.PI * ties * ties / 4, .5 * Math.PI * phi * phi / 4, "mm²");
            kg += 2 * (s + .1) * Math.Ceiling(l0 / step) * Math.PI * ties * ties / 4 * 1e-6 * 7850;
        }
        return new(bars, checks, kg, DetailingScopeNote);
    }

    /// <summary>Distinta storica (prima RetainingWall.CalculateBarSchedule con WallCalculationEngine.Legacy; nessun chiamante dopo F4).</summary>
    public static BarSchedule CalculateBarSchedule(JsonObject input)
    {
        var d = (JsonObject)input.DeepClone(); Upgrade(d);
        if (d.S("family") != "cantilever") throw new ArgumentException("La distinta ferri richiede un muro a mensola in calcestruzzo armato.");
        var o = d["bar_schedule"]!; var g = d["geometry"]!; var r = d["reinforcement"]!;
        foreach (var field in GeometryFields) AdvancedNumber(g, field.Key, field.Min, field.Max);
        if (g.D("stem_top") > g.D("stem_base")) throw new ArgumentException("Lo spessore in testa deve essere ≤ quello al piede.");
        double length = AdvancedNumber(o, "panel_length", .2, 100), endCover = AdvancedNumber(o, "end_cover", 0, 200);
        double stock = AdvancedNumber(o, "stock_length", 1, 18), cover = d["materials"].D("cover") / 1000;
        double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), s = g.D("stem_base"), heel = g.D("heel");
        bool two = r.B("two_zones"); double split = two ? r.D("lower_height") : h;
        if (two && (!double.IsFinite(split) || split <= 0 || split >= h)) throw new ArgumentException("Altezza dell’armatura inferiore compresa fra 0 e H.");
        var details = CalculateReinforcementDetails(d); var bars = new List<ScheduledBar>(); var warnings = new List<string>();
        foreach (var b in details.Bars)
        {
            double span = length * 1000 - 2 * endCover - b.Diameter;
            if (span <= 0 || b.Spacing <= 0) throw new ArgumentException("Tratto troppo corto per il copriferro di estremità e le barre.");
            int count = (int)Math.Ceiling(span / b.Spacing - 1e-10) + 1;
            var legs = new List<BarLeg>();
            if (b.Mandrel > 0)
            {
                legs.Add(new("A", Distance(b.Path[0], b.Path[1])));
                legs.Add(new("B", Distance(b.Path[1], b.Path[2])));
                legs.Add(new("arco 90°", (b.Mandrel + b.Diameter) / 2000 * Math.PI / 2));
                legs.Add(new("C", Distance(b.Path[^2], b.Path[^1])));
            }
            else legs.Add(new("A", b.Length));
            bars.Add(new(b.Mark, b.Zone, b.Face, "Principale", b.Diameter, count, legs.Sum(l => l.Length),
                span / (count - 1), b.Mandrel, b.Lap, legs, b.Path,
                b.Zone == "stem" && b.Face == "valle" && s != g.D("stem_top") ? "Raccordo del tratto inclinato al piede da dettagliare." : ""));
        }
        int secondaryMark = 0;
        foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
        {
            if (key == "stem_upper" && !two || key == "toe" && a == 0 || key == "heel" && heel == 0) continue;
            var arm = r[key]!; double dia = arm.D("secondary_diameter"), step = arm.D("secondary_spacing");
            bool stem = key.StartsWith("stem"), upper = key == "stem_upper";
            double start = stem ? upper ? split : cover + dia / 2000 : key == "toe" || a == 0 ? cover + dia / 2000 : a + s / 2;
            double end = stem ? upper || !two ? h - cover - dia / 2000 : split : key == "toe" && heel > 0 ? a + s / 2 : a + s + heel - cover - dia / 2000;
            double span = end - start; if (span <= 0) throw new ArgumentException("Zona troppo corta per la rete secondaria: " + RebarZoneName(key));
            // Each shared boundary belongs to the following zone; no duplicate bar at h1 or slab mid-stem.
            bool omitLast = key == "stem" && two || key == "toe" && heel > 0;
            int intervals = Math.Max(1, (int)Math.Ceiling(span * 1000 / step - 1e-10));
            int count = intervals + (omitLast ? 0 : 1); double cut = length - 2 * endCover / 1000;
            if (cut <= 0) throw new ArgumentException("Lunghezza netta delle barre secondarie non positiva.");
            for (int face = 0; face < 2; face++)
                bars.Add(new("S" + (++secondaryMark), key, stem ? face == 0 ? "monte" : "valle" : face == 0 ? "inferiore" : "superiore",
                    "Secondaria", dia, count, cut, span * 1000 / intervals, 0, 0, [new("A", cut)], [new(0, 0), new(cut, 0)],
                    "Rettilinea lungo muro; estremità e sovrapposizioni longitudinali da dettagliare. Zona " + start.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "–" + end.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m."));
        }
        if (two)
        {
            double tiesPerM = AdvancedNumber(o, "ties_per_m", 1, 30), cut = AdvancedNumber(o, "tie_cut_length", 0, 10000);
            if (tiesPerM % 1 != 0) throw new ArgumentException("Collegamenti per fila: inserire un numero intero per metro.");
            int rows = (int)Math.Ceiling(details.Bars.Max(b => b.Lap) / d["detailing"].D("tie_spacing")) + 1;
            int count = rows * (int)Math.Ceiling(tiesPerM * length - 1e-10);
            bars.Add(new("C1", "stem", "fra facce", "Collegamento", d["detailing"].D("tie_diameter"), count,
                cut == 0 ? null : cut / 1000, d["detailing"].D("tie_spacing"), 0, 0, [], [],
                $"{rows} file nella sovrapposizione, {tiesPerM:0}/m per fila. Sagoma e ganci da definire; sviluppo " + (cut == 0 ? "mancante." : "assegnato dall’utente.")));
        }
        int failed = details.Checks.Count(c => c.Ratio is null || c.Ratio > 1);
        if (failed > 0) warnings.Add($"{failed} controlli dei dettagli non soddisfatti o non disponibili: consultare le verifiche prima dell’esecuzione.");
        if (!d["detailing"].B("enabled")) warnings.Add("Verifica dei dettagli non attivata nel riepilogo del muro.");
        foreach (var b in bars.Where(b => b.CutLength > stock)) warnings.Add($"{b.Mark}: sviluppo {b.CutLength:0.###} m maggiore della barra commerciale {stock:0.###} m; definire un giunto, nessun taglio automatico.");
        if (bars.Any(b => b.CutLength is null)) warnings.Add("Peso parziale: manca lo sviluppo dei collegamenti C1.");
        warnings.Add("Distinta preliminare senza sfridi. Quote all’asse delle barre; archi sviluppati al raggio (mandrino + Ø)/2. Estremità lungo muro, disposizione degli strati, raccordi e ganci richiedono il dettaglio esecutivo.");
        return new(length, endCover, stock, bars, warnings);
    }
    private static double Distance(BarPoint p, BarPoint q) => double.Hypot(p.X - q.X, p.Y - q.Y);

    /// <summary>
    /// Ricerca delle armature precedente (prima RetainingWall.DesignReinforcement con legacy o con un motore SLE esplicito). Senza motore SLE
    /// le verifiche dei candidati sono quelle GPC del prodotto; con un motore esplicito quelle storiche, come prima.
    /// </summary>
    public static ReinforcementProposal DesignReinforcement(JsonObject input, CancellationToken token = default, ServiceabilityEngine? serviceabilityEngine = null, bool legacy = false)
    {
        if (!legacy && serviceabilityEngine is null) return RetainingWall.DesignReinforcement(input, token);
        using var walls = Use(false, serviceabilityEngine is not null);
        using var serviceability = ServiceabilityEngines.Use(serviceabilityEngine);
        var d = (JsonObject)input.DeepClone(); Upgrade(d);
        if (d.S("family") != "cantilever") throw new ArgumentException("Il predimensionamento delle armature richiede un muro a mensola in c.a.");
        var original = Calculate(d, token, serviceabilityEngine); var opt = d["detailing"]!;
        int maxCount = (int)AdvancedNumber(opt, "max_count", 4, 30); double maxDia = AdvancedNumber(opt, "max_diameter", 8, 40), target = AdvancedNumber(opt, "target_ratio", .1, 1);
        if (opt.D("max_count") != maxCount) throw new ArgumentException("Numero massimo di barre: inserire un intero.");
        var failures = new List<string>();
        foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
        {
            if (key == "stem_upper" && !d["reinforcement"].B("two_zones")) continue;
            var selected = original.Cases.Where(c => c.Factors.S("purpose") != "Ribaltamento").Select(c => c with { Sections = c.Sections.Where(f => ReinforcementKey(d, f.Name, f.Position) == key).ToList(), Curvatures = [] }).Where(c => c.Sections.Count > 0).ToList();
            if (selected.Count == 0) continue;
            var candidates = (from dia in new double[] { 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 40 } where dia <= maxDia from n in Enumerable.Range(4, maxCount - 3) orderby n * dia * dia, dia select (dia, n)).ToArray();
            bool found = false;
            var previousArm = d["reinforcement"]![key]!.DeepClone();
            foreach (var (dia, count) in candidates)
            {
                token.ThrowIfCancellationRequested();
                double area = count * Math.PI * dia * dia / 4;
                if (selected.SelectMany(c => c.Sections).Any(f => Math.Abs(f.M) * 1e6 > area * d["materials"].D("fyk") / MaterialSectionInput(d).D("gamma_s") * f.Thickness * 1000)) continue;
                var arm = d["reinforcement"]![key]!; arm["diameter"] = dia; arm["count"] = count; arm["symmetric"] = true;
                List<Check> values;
                try { values = RetainingWall.StructuralChecks(d, selected, token, out _); } catch (ArgumentException) { continue; }
                if (values.Any(c => c.Ratio is null || c.Ratio > (c.Combination == "Dettagli" ? 1 : target))) continue;
                arm["secondary_diameter"] = 10;
                double required = key.StartsWith("stem") ? Math.Max(.25 * area, .0005 * d["geometry"].D("stem_base") * 1e6) : .2 * area;
                arm["secondary_spacing"] = Math.Max(50, Math.Min(200, Math.Floor(Math.PI * 100 / 4 * 1000 / required / 10) * 10));
                found = true; break;
            }
            if (!found) { failures.Add(key + ": nessuna armatura verificata nei limiti scelti"); d["reinforcement"]![key] = previousArm; }
        }
        if (d["reinforcement"].B("two_zones"))
        {
            double n = Math.Max(d["reinforcement"]!["stem"].D("count"), d["reinforcement"]!["stem_upper"].D("count"));
            d["reinforcement"]!["stem"]!["count"] = n; d["reinforcement"]!["stem_upper"]!["count"] = n;
        }
        d["detailing"]!["enabled"] = true;
        var final = Calculate(d, token, serviceabilityEngine); bool pass = failures.Count == 0 && final.Structural.All(c => c.Ratio is double v && v <= (c.Combination == "Dettagli" ? 1 : target));
        return new(d, final.Structural, pass, pass ? "Proposta verificata per le combinazioni inserite; controllare il dettaglio costruttivo prima dell’esecuzione." : string.Join("; ", failures.Append("Predimensionamento con controlli non soddisfatti o incompleti: consultare la tabella; può essere necessario aumentare gli spessori.")));
    }
}
