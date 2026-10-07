using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Geotechnics;
using Anthea.Calculations.Geotechnics;
using GpcSlopes = GPC.Checkers.Geotechnics.Slopes;

namespace Anthea.Calculations;

/// <summary>
/// Adapter of the wall document to GPCChecker.Geotechnics (GPC.Checkers.Geotechnics.Walls): the document is in m, kN/m, kPa, kN/m³ and degrees,
/// the library in mm, N/mm, MPa, N/mm³ and rad with the soils of Model. Every pressure, equilibrium, combination and check is calculated by the library;
/// here the document is read and the results are written back in the units and words of the reports.
/// </summary>
public static partial class RetainingWall
{
    private const double Mm = Slope.Mm, KPa = Slope.KPa, KN3 = Slope.KN3, Deg = Slope.Deg;
    /// <summary>
    /// γRd on the soil inertia F̄ of EN 1998-5 Annex F: false, as in (F.7), where γRd enters only N̄, V̄ and M̄ (decision D7-c of 6/10/2026;
    /// until then ANTHEA applied it also to F̄, and the library keeps that option for its fixtures).
    /// </summary>
    public const bool ModelFactorOnSoilInertia = false;

    static double Need(JsonNode? n, string key) => J.Number(n?[key]) ?? throw new ArgumentException("Dato mancante: " + key);
    static double Value(JsonNode? n, string key) => J.Number(n?[key]) ?? double.NaN;

    /// <summary>A granular Model soil (c′ = 0) from γ, γsat (kN/m³) and φ′ (degrees); the checks of Model are reported with the name of the soil.</summary>
    static Soil GranularSoil(string name, double gamma, double gammaSat, double phi)
    {
        try { return new Soil(name, gamma * KN3, gammaSat * KN3, phi * Deg, 0, "ANTHEA"); }
        catch (ArgumentException ex) { throw new ArgumentException($"Terreno {name}: γ, γsat ≥ γ e φ′ non validi ({ex.Message}).", ex); }
    }

    /// <summary>A Model profile of the layers of a column from its ground at the elevation (m above the base) downwards.</summary>
    static SoilProfile Column(JsonArray layers, double ground, string name)
    {
        var list = new List<SoilLayer>(); double top = ground;
        foreach (var l in layers)
        {
            double bottom = top - Need(l, "thickness");
            var soil = GranularSoil(l.S("name") is { Length: > 0 } n ? n : "Strato", Need(l, "gamma"), Need(l, "gamma_sat"), Need(l, "phi"));
            list.Add(new SoilLayer(soil, top * Mm, bottom * Mm)); top = bottom;
        }
        return new SoilProfile(name, list, "ANTHEA");
    }

    static WallInterface Interface(JsonObject d, bool wall)
    {
        var i = d["interfaces"];
        var friction = i.S(wall ? "wall_mode" : "base_mode", "Assegnato") switch
        {
            "Assegnato" => WallFriction.Assigned, "Gettato in opera" => WallFriction.CastInPlace, "Prefabbricato liscio" => WallFriction.PrecastSmooth, "Liscio" => WallFriction.Smooth,
            _ => throw new ArgumentException("Modalità di attrito non valida.")
        };
        double angle = friction == WallFriction.Assigned ? (wall ? (i is null ? 0 : Value(i, "wall_delta")) : Value(d["foundation"], "delta")) * Deg : 0;
        double cv = friction is WallFriction.CastInPlace or WallFriction.PrecastSmooth ? Value(i, wall ? "wall_phi_cv" : "base_phi_cv") * Deg : 0;
        return new WallInterface(friction, angle, cv);
    }

    // ActionTypes and States are in the order of WallActionType and WallLimitState.
    static WallActionType ActionType(string type) => Array.IndexOf(ActionTypes, type) is int index and >= 0 ? (WallActionType)index : throw new ArgumentException("Tipo o natura dell’azione non supportati.");
    static double ActionUnit(WallActionType type) => type is WallActionType.UniformSurcharge or WallActionType.LateralPressure ? KPa : type == WallActionType.Moment ? Mm : 1;
    static WallLimitState State(string state) => Array.IndexOf(States, state) is int index and >= 0 ? (WallLimitState)index : throw new ArgumentException("Stato limite non valido.");
    static string StateName(WallLimitState state) => States[(int)state];

    /// <summary>The NTC site amplification of the seismic panel (SLV).</summary>
    static NtcSiteAmplification Site(JsonNode s)
    {
        double ag = J.Number(s["ag_g"]) ?? throw new ArgumentException("Sisma: ag/g allo SLV (es. 0,20, non 20), inserire un valore fra 0 e 1.");
        double? ss = null; (NtcSoilCategory, double)? soil = null;
        if (s.S("ss_mode") == AmplificationAssigned) ss = J.Number(s["ss"]) ?? throw new ArgumentException("Sisma: Ss assegnato, inserire un valore fra 0.1 e 5.");
        else if (s.S("ss_mode") == AmplificationCalculated)
        {
            string category = s.S("soil_class");
            if (category is not ("A" or "B" or "C" or "D" or "E")) throw new ArgumentException("Sisma: scegliere la categoria di sottosuolo A–E dalla relazione geotecnica.");
            var c = Enum.Parse<NtcSoilCategory>(category);
            soil = (c, c == NtcSoilCategory.A ? 0 : J.Number(s["f0"]) ?? throw new ArgumentException("Sisma: F₀ allo SLV, inserire un valore fra 2.2 e 10."));
        }
        else throw new ArgumentException("Sisma: scegliere come definire Ss.");
        double? st = null; (NtcTopography, double, double, double)? topography = null;
        if (s.S("st_mode") == AmplificationAssigned) st = J.Number(s["st"]) ?? throw new ArgumentException("Sisma: St assegnato, inserire un valore fra 1 e 5.");
        else if (s.S("st_mode") == AmplificationCalculated)
        {
            var shape = s.S("topography") switch
            {
                TopographyFlat => NtcTopography.Flat, TopographySlope => NtcTopography.Slope, TopographyRidge => NtcTopography.Ridge,
                _ => throw new ArgumentException("Sisma: scegliere pianeggiante, pendio o rilievo a cresta stretta.")
            };
            topography = (shape, Value(s, "slope") * Deg, Value(s, "relief_height") * Mm, Value(s, "site_height") * Mm);
        }
        else throw new ArgumentException("Sisma: scegliere come definire St.");
        return NtcSiteAmplification.Create(ag, soil, ss, topography, st);
    }

    static WallSeismicMethod SeismicMethod(JsonNode s) => s.S("method", "Mononobe–Okabe") switch
    {
        "Mononobe–Okabe" => WallSeismicMethod.MononobeOkabe, "Wood semplificato" => WallSeismicMethod.Wood, _ => throw new ArgumentException("Metodo sismico non riconosciuto.")
    };

    /// <summary>
    /// The typed input of the library from a completed document (version 2, or 1 with its three loads as correlated actions q, hq, nq with ψ = 1).
    /// </summary>
    public static WallInput ToWallInput(JsonObject d)
    {
        var g = d["geometry"]!; double ht = Need(g, "height") + Need(g, "slab");
        var geometry = new WallGeometry(Need(g, "height") * Mm, Need(g, "stem_base") * Mm, Need(g, "stem_top") * Mm, Need(g, "slab") * Mm, Need(g, "toe") * Mm, Need(g, "heel") * Mm);
        var f = d["foundation"]!;
        var foundation = GranularSoil("Terreno di posa", Need(f, "gamma"), Need(f, "gamma_sat"), Need(f, "phi"));
        var backfill = Column(d.Array("layers"), ht, "Monte");
        var v = d["valley"]; double dv = ValleyHeight(d);
        var valley = new WallValley(dv * Mm, Column(ValleyLayers(d), dv, "Valle"), v.B("passive"), v is null ? 0 : Value(v, "mobilization"));
        var water = d["water"].B("enabled") ? new WallWater(Need(d["water"], "depth") * Mm, Need(d["water"], "front_head") * Mm) : null;
        var actions = new List<WallAction>();
        if (d.D("version") < 2)
        {
            var l = d["loads"]!; double a = Need(g, "toe"), s = Need(g, "stem_base"), top = Need(g, "stem_top");
            actions.Add(new WallAction("q", "Sovraccarico", WallActionType.UniformSurcharge, WallActionCategory.Q, Need(l, "surcharge") * KPa, ht * Mm, Need(g, "slab") * Mm, 0, 1, 1, 1, "legacy"));
            actions.Add(new WallAction("hq", "Forza orizzontale", WallActionType.HorizontalForce, WallActionCategory.Q, Need(l, "horizontal"), ht * Mm, Need(g, "slab") * Mm, 0, 1, 1, 1, "legacy"));
            actions.Add(new WallAction("nq", "Forza verticale", WallActionType.VerticalForce, WallActionCategory.Q, Need(l, "vertical"), ht * Mm, Need(g, "slab") * Mm, (a + s - top / 2) * Mm, 1, 1, 1, "legacy"));
        }
        else
            foreach (var x in d.Array("actions"))
            {
                var type = ActionType(x.S("type"));
                var category = Enum.TryParse<WallActionCategory>(x.S("category"), out var c) ? c : throw new ArgumentException("Tipo o natura dell’azione non supportati.");
                actions.Add(new WallAction(x.S("id"), x.S("name"), type, category, Value(x, "value") * ActionUnit(type), Value(x, "z") * Mm, Value(x, "z0") * Mm, Value(x, "x") * Mm,
                    Value(x, "psi0"), Value(x, "psi1"), Value(x, "psi2"), x.S("group"), x.B("enabled")));
            }
        WallSeismic? seismic = null;
        var sd = d["seismic"]!;
        if (sd.B("enabled"))
        {
            // A missing model factor is the default 1.15 of the document; a blank one is an error of the bearing capacity.
            var b = d["bearing_seismic"]; double factor = b?["model_factor"] is null ? 1.15 : Value(b, "model_factor");
            var bearing = b is null || b.S("source", "Da sito") == "Da sito" ? WallSeismicBearing.FromSite(factor, ModelFactorOnSoilInertia)
                : b.S("source") == "Assegnata" ? WallSeismicBearing.Assigned(Value(b, "ground_kh"), Value(b, "ground_kv"), factor, ModelFactorOnSoilInertia)
                : WallSeismicBearing.Assigned(double.NaN, double.NaN, factor, ModelFactorOnSoilInertia);
            string source = sd.S("source", SeismicManual);
            seismic = source == SeismicManual ? WallSeismic.Assigned(SeismicMethod(sd), Need(sd, "kh"), Need(sd, "kv"), bearing)
                : source == SeismicSite ? WallSeismic.FromSite(SeismicMethod(sd), Site(sd), bearing) : throw new ArgumentException("Sisma: modalità di definizione non riconosciuta.");
        }
        var r = d["reinforcement"];
        double? split = r.B("two_zones") ? Need(r, "lower_height") * Mm : null;
        return new WallInput(d.S("family") == "gravity" ? WallFamily.Gravity : WallFamily.Cantilever, geometry, Need(d["materials"], "gamma") * KN3, backfill, foundation, valley,
            Interface(d, true), Interface(d, false), water, actions, seismic, split);
    }

    /// <summary>A combination row of the document.</summary>
    static WallCombination ToCombination(JsonObject c)
    {
        var purpose = c.S("purpose") switch
        {
            "" => WallSeismicPurpose.None, "Generale" => WallSeismicPurpose.General, "Ribaltamento" => WallSeismicPurpose.Overturning,
            _ => throw new ArgumentException("Destinazione della combinazione sismica non valida.")
        };
        double soil = Value(c, "soil");
        return new WallCombination(c.S("name"), State(c.S("state")), Value(c, "wall"), soil, J.Number(c["valley_soil"]) ?? soil, Value(c, "water"), Value(c, "mphi"), Value(c, "rslide"),
            Value(c, "rover"), Value(c, "rbearing"), Value(c, "kh"), Value(c, "kv"), c["coefficients"]!.AsObject().ToDictionary(p => p.Key, p => J.Number(p.Value) ?? double.NaN), purpose, c.S("approach"));
    }

    /// <summary>A combination of the library as a row of the document (the factors of every action of the document, disabled ones 0).</summary>
    static JsonObject ToRow(JsonObject d, WallCombination c)
    {
        var coefficients = new JsonObject();
        foreach (var a in d.Array("actions")) coefficients[a.S("id")] = a.B("enabled") && c.Coefficients.TryGetValue(a.S("id"), out double f) ? f : 0;
        var row = J.Obj(("enabled", true), ("name", c.Name), ("state", StateName(c.State)), ("approach", c.Approach), ("wall", c.Wall), ("soil", c.Soil), ("valley_soil", c.ValleySoil),
            ("water", c.Water), ("mphi", c.FrictionFactor), ("rslide", c.SlidingFactor), ("rover", c.OverturningFactor), ("rbearing", c.BearingFactor), ("kh", c.Kh), ("kv", c.Kv), ("coefficients", coefficients));
        if (c.Purpose != WallSeismicPurpose.None) row["purpose"] = c.Purpose == WallSeismicPurpose.General ? "Generale" : "Ribaltamento";
        return row;
    }

    /// <summary>
    /// The fixed cases of a version 1 document: characteristic, frequent (ψ1) and quasi permanent (ψ2) on the three loads; 8 or 16 ULS with γR 1.1,
    /// 1.15, 1.4; seismic ±kv with ψ2 and the seismic bearing γR 1.2. Returned with the factor of the loads of every case.
    /// </summary>
    static List<(WallCombination Combination, double Live)> LegacyCombinations(JsonObject d)
    {
        var loads = d["loads"]!; bool water = d["water"].B("enabled");
        var result = new List<(WallCombination, double)>();
        void Add(string name, WallLimitState state, double fc, double fs, double fq, double fw = 1, double kh = 0, double kv = 0)
        {
            bool design = state is WallLimitState.Ultimate or WallLimitState.Seismic;
            result.Add((new WallCombination(name, state, fc, fs, fs, fw, 1, design ? 1.1 : 1, design ? 1.15 : 1, state == WallLimitState.Seismic ? 1.2 : design ? 1.4 : 1, kh, kv,
                new Dictionary<string, double> { ["q"] = fq, ["hq"] = fq, ["nq"] = fq }), fq));
        }
        Add("Caratteristica", WallLimitState.Characteristic, 1, 1, 1); Add("Frequente", WallLimitState.Frequent, 1, 1, loads.D("psi1")); Add("Quasi permanente", WallLimitState.QuasiPermanent, 1, 1, loads.D("psi2"));
        int index = 0;
        foreach (double fc in new[] { 1d, 1.3 }) foreach (double fs in new[] { 1d, 1.3 }) foreach (double fq in new[] { 0d, 1.5 }) foreach (double fw in water ? new[] { 1d, 1.3 } : new[] { 1d })
            Add("SLU " + ++index, WallLimitState.Ultimate, fc, fs, fq, fw);
        if (d["seismic"].B("enabled")) foreach (double kv in new[] { -d["seismic"].D("kv"), d["seismic"].D("kv") }.Distinct())
            Add(kv < 0 ? "Sisma kv−" : "Sisma kv+", WallLimitState.Seismic, 1, 1, loads.D("psi2"), 1, d["seismic"].D("kh"), kv);
        return result;
    }

    static readonly string[] MemberNames = ["Fusto", "Valle", "Monte"];
    static PressureSegment Segment(WallPressureSegment p) => new(p.Z0 / Mm, p.Z1 / Mm, p.P0 / KPa, p.P1 / KPa);
    static PressureDetail Detail(WallPressureDetail p) => new(p.Z0 / Mm, p.Z1 / Mm, p.FrictionAngle / Deg, p.DesignFrictionAngle / Deg, p.K, p.Ke, p.Sigma0 / KPa, p.Sigma1 / KPa,
        p.Soil0 / KPa, p.Soil1 / KPa, p.Surcharge / KPa, p.Water0 / KPa, p.Water1 / KPa, p.Dynamic / KPa, p.Total0 / KPa, p.Total1 / KPa);
    static Contact ToContact(WallContact c) => new(c.Start / Mm, c.End / Mm, c.Toe / KPa, c.Heel / KPa, c.Peak / KPa, c.Valid) { Law = c };

    /// <summary>A combination of the library in the units of the document: lengths m, forces kN/m, moments kNm/m, pressures kPa, angles in degrees.</summary>
    static LoadCase ToLoadCase(JsonObject d, WallCaseResult r, JsonObject? definition, double live)
    {
        var c = r.Combination; var s = r.Soil;
        var actions = definition is null ? [] : r.Actions.Select(a =>
        {
            var x = d.Array("actions").First(n => n.S("id") == a.Action.Id);
            return new AppliedAction(a.Action.Id, x.S("name"), x.S("type"), a.Factor, x.D("value"), x.D("z"), x.D("z0"), x.D("x"));
        }).ToList();
        return new LoadCase(c.Name, StateName(c.State), c.Wall, c.Soil, live, c.Water, c.Kh, c.Kv, r.Horizontal, r.Vertical, r.Uplift, r.Stabilizing / Mm, r.Overturning / Mm, r.X / Mm,
            r.Eccentricity / Mm, r.EffectiveWidth / Mm, ToContact(r.Contact), r.SlidingResistance, r.OverturningResistance / Mm, r.BearingResistance, r.Pressures.Select(Segment).ToList(),
            r.Sections.Select(f => new SectionForce(MemberNames[(int)f.Member], f.Position / Mm, f.Thickness / Mm, f.N, f.M / Mm, f.V)).ToList())
        {
            SeismicBearing = r.SeismicBearing is null ? null : Geotechnics.SeismicBearing.From(r.SeismicBearing), SeismicBearingError = r.SeismicBearingError,
            Factors = definition is null ? null : (JsonObject)definition.DeepClone(), PressureDetails = r.PressureDetails.Select(Detail).ToList(), Actions = actions,
            StemPressures = r.StemPressures.Select(Segment).ToList(), ValleyPressures = r.ValleyPressures.Select(Segment).ToList(), StemPressureDetails = r.StemPressureDetails.Select(Detail).ToList(),
            SoilAudit = J.Obj(("Dv_m", s.ValleyHeight / Mm), ("Hlib_m", s.FreeHeight / Mm), ("peso_valle_kN_m", s.ValleyWeight), ("momento_peso_valle_kNm_m", s.ValleyMoment / Mm),
                ("q_ricoprimento_kPa", s.Overburden / KPa), ("delta_muro_d_gradi", s.WallFriction / Deg), ("delta_base_d_gradi", s.BaseFriction / Deg), ("mu_base", s.BaseFrictionCoefficient),
                ("delta_piano_equilibrio_gradi", s.EquilibriumPlaneFriction / Deg), ("passiva_disponibile_kN_m", s.PassiveAvailable), ("passiva_usata_kN_m", s.PassiveUsed),
                ("passiva_frazione", s.PassiveFraction), ("passiva_limite_equilibrio", s.PassiveScale), ("Nq", s.Nq), ("Ngamma", s.Ngamma), ("iq", s.Iq), ("igamma", s.Igamma), ("base_ruvida", s.RoughBase))
        };
    }

    /// <summary>A check of the library with the name and unit of the reports (demand and resistance converted to kN/m, kNm/m, m, mm, rad).</summary>
    static Check ToCheck(WallCheck c, string? record = null)
    {
        (string name, string unit, double scale) = c.Kind switch
        {
            WallCheckKind.Sliding => ("Scorrimento", "kN/m", 1d), WallCheckKind.Overturning => ("Ribaltamento", "kNm/m", Mm), WallCheckKind.Bearing => ("Capacità portante", "kN/m", 1),
            WallCheckKind.Contact => ("Contatto fondazione", "m", Mm),
            WallCheckKind.Settlement => (c.Resistance is null ? "Cedimenti" : "Cedimento edometrico finale", "mm", 1),
            WallCheckKind.Rotation => ("Rotazione da cedimenti differenziali", "rad", 1),
            WallCheckKind.StemDisplacement => ("Spostamento elastico fusto · base fissa", "mm", 1),
            WallCheckKind.HeadDisplacement => (c.Resistance is null ? "Spostamento totale in testa" : "Spostamento totale in testa · stima disaccoppiata", "mm", 1),
            WallCheckKind.Newmark => (c.Resistance is null ? "Spostamento Newmark" : "Scorrimento permanente Newmark · " + record, "mm", 1),
            _ => ("Stabilità globale · Bishop", "−", 1)
        };
        return new Check(name, c.Combination, c.Demand / scale, c.Resistance / scale, unit, c.Ratio, c.Message);
    }

    // Methods of the library in the units of the document, for the drawings, the tables and the checks of ANTHEA.

    public static double Ka(double phi) => EarthPressure.RankineActive(phi * Deg);
    public static double SeismicKa(double phi, double kh, double kv) => EarthPressure.MononobeOkabe(phi * Deg, kh, kv);
    /// <summary>Horizontal Coulomb / Mononobe–Okabe coefficient, vertical back and horizontal fill (angles in degrees).</summary>
    public static double ActiveHorizontal(double phi, double delta, double kh = 0, double kv = 0) => EarthPressure.ActiveHorizontal(phi * Deg, delta * Deg, kh, kv);
    /// <summary>Design friction of the back or of the base, degrees.</summary>
    public static double InterfaceDelta(JsonObject d, bool wall, double mphi = 1) => Interface(d, wall).Design(mphi) / Deg;
    public static Contact ContactLaw(double width, double n, double x) => ToContact(WallContact.Law(width * Mm, n, x * Mm));
    /// <summary>Contact pressure at x (m), kPa.</summary>
    public static double Pressure(Contact c, double x) => c.Law is { } law ? law.Pressure(x * Mm) / KPa : 0;
    public static (double Force, double Moment) Integrate(IEnumerable<PressureSegment> pieces, double left, double right, double root)
    {
        var r = WallPressureSegment.Integrate(pieces.Select(p => new WallPressureSegment(p.Z0 * Mm, p.Z1 * Mm, p.P0 * KPa, p.P1 * KPa)), left * Mm, right * Mm, root * Mm);
        return (r.Force, r.Moment / Mm);
    }
    public static List<ValleyBand> ValleyBands(JsonObject d) => RetainingWallAnalysis.ValleyBands(ToWallInput(d))
        .Select(b => new ValleyBand(b.Top / Mm, b.Bottom / Mm, b.UnitWeight / KN3, b.EffectiveUnitWeight / KN3, b.FrictionAngle / Deg, b.SigmaTop / KPa, b.SigmaBottom / KPa)).ToList();
    /// <summary>Weight (kN/m) and static moments (kNm/m) of the soil in front of the wall, or of the wedge over the face above the elevation (m).</summary>
    public static SoilMass FrontSoilMass(JsonObject d, double above = 0, bool wedgeOnly = false)
    {
        var input = ToWallInput(d); var m = RetainingWallAnalysis.FrontSoilMass(input.Geometry, RetainingWallAnalysis.ValleyBands(input), above * Mm, wedgeOnly);
        return new(m.Weight, m.MomentX / Mm, m.MomentY / Mm);
    }
    /// <summary>Rotation and displacement of the stem fixed at the base from the curvatures (heights m, curvatures 1/m, displacements mm).</summary>
    public static IReadOnlyList<DisplacementPoint> IntegrateCurvature(IEnumerable<CurvaturePoint> values, double height)
        => WallDisplacementPoint.Integrate(values.Select(p => new WallCurvaturePoint(p.Y * Mm, p.Curvature / Mm)), height * Mm)
            .Select(p => new DisplacementPoint(p.Height / Mm, p.Curvature * Mm, p.Rotation, p.Displacement)).ToList();
}
