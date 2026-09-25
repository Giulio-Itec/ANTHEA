using System.Reflection;
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace X.Core;

/// <summary>Bridge N–Mx section, mm/N/MPa internally. Model owns geometry and materials;
/// Checker solves composite elastic stresses; ANTHEA iterates EN 1993-1-5 effective widths and superposes stage contributions.</summary>
public static class BridgeSection
{
    public const string Module = "str_mista_ponte";
    public const string Method = "Sezione composta N–Mx · larghezze efficaci EN 1993-1-5:2006 · v1";
    public static readonly string[] Standards = ["NTC 2018 / EN 1994-2:2005", "EN 1994-2:2005"];
    public static readonly string[] ConcreteNames = Catalog<ConcreteMaterialEN1992>(typeof(ConcreteMaterialEN1992Data)).Keys.ToArray();
    public static readonly string[] SteelNames = ["S235", "S275", "S355", "S420"];
    public static readonly string[] RebarNames = ["B450A", "B450C", "B500A", "B500B", "B500C"];
    public static readonly string[] PhaseKinds = ["Solo acciaio", "Composta", "Soletta esclusa"];
    public static readonly string[] HomoModes = ["Da φ", "Da n"];
    public const string Scope = "Analisi elastica N–Mx con connessione completa, sezione simmetrica e anima senza irrigidimenti longitudinali. " +
        "b_eff della soletta è un dato di ingresso. Le fasi sono incrementi di carico già combinati; per ogni situazione la sezione efficace è comune ai contributi sommati. " +
        "Non è un'analisi evolutiva con redistribuzione viscosa. Taglio, torsione, connettori, fatica e instabilità globale non sono verificati.";
    public static JsonObject Defaults() => J.Obj(("versione_mista", 1), ("nome", "Sezione composta da ponte"),
        ("normativa", Standards[0]), ("gamma_m0", "1.05"), ("gamma_c", "1.5"), ("alpha_cc", "0.85"), ("gamma_s", "1.15"),
        ("stato", "SLU"), ("classe4", true), ("classe_cls", "C35/45"), ("acciaio", "S355"), ("armatura", "B450C"),
        ("fy_override", false), ("fy", "355"), ("b_cls", "3000"), ("h_cls", "250"), ("h_web", "1800"), ("t_web", "14"),
        ("b_top", "500"), ("t_top", "25"), ("b_bottom", "700"), ("t_bottom", "30"), ("plate2", false), ("b_bottom2", "500"), ("t_bottom2", "20"),
        ("rebars_top", true), ("d_top", "16"), ("pitch_top", "150"), ("cover_top", "45"),
        ("rebars_bottom", true), ("d_bottom", "16"), ("pitch_bottom", "150"), ("cover_bottom", "45"), ("y_ref", "0"),
        ("fasi", new JsonArray(Phase("G1 · getto e carpenteria", "Solo acciaio", 0, 1500, 0, 1),
            Phase("G2 · permanenti portati", "Composta", 0, 2000, 2, 1.1), Phase("Q · variabili", "Composta", 0, 3000, 0, 1))));
    public static JsonObject Phase(string name = "Nuova fase", string kind = "Composta", double n = 0, double m = 0, double phi = 0, double psi = 1) =>
        J.Obj(("nome", name), ("tipo", kind), ("attiva", true), ("N", n), ("Mx", m), ("modo", "Da φ"), ("phi", phi), ("psi", psi), ("n", "18"));
    public static void ValidateShape(JsonObject d)
    {
        if (d.D("versione_mista") != 1 || d["fasi"] is not JsonArray p || p.Count is < 1 or > 20 || p.Any(x => x is not JsonObject))
            throw new ArgumentException("Formato della sezione composta non valido (versione 1, da 1 a 20 fasi).");
    }
    private static Dictionary<string, T> Catalog<T>(Type type) where T : Material => type.GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => typeof(T).IsAssignableFrom(p.PropertyType)).Select(p => (T)p.GetValue(null)!).ToDictionary(m => m.Name);
    public static (ConcreteMaterialEN1992 Concrete, SteelMaterial Steel, SteelMaterial Rebar) Materials(JsonObject d)
    {
        var c = Catalog<ConcreteMaterialEN1992>(typeof(ConcreteMaterialEN1992Data));
        var s = Catalog<SteelMaterialEN1993>(typeof(SteelMaterialEN1993Data));
        var r = Catalog<SteelMaterialEN1992>(typeof(SteelMaterialEN1992Data));
        if (!c.TryGetValue(d.S("classe_cls"), out var concrete) || !s.TryGetValue(d.S("acciaio"), out var steel) || !r.TryGetValue(d.S("armatura"), out var rebar))
            throw new ArgumentException("Selezionare materiali presenti nei cataloghi Model.");
        SteelMaterial a = steel;
        if (d.B("fy_override"))
        {
            double fy = d.Required("fy", strict: true);
            if (fy > steel.Fu) throw new ArgumentException("fy adottato non può superare fu del materiale.");
            a = new SteelMaterial(steel.Name, steel.ElasticModulusTension, fy, steel.Fu, .15, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural);
        }
        return (concrete, a, rebar);
    }
    public static BridgeGeometry Geometry(JsonObject d)
    {
        ValidateShape(d);
        double P(string k) => d.Required(k, strict: true);
        double b = P("b_cls"), tc = P("h_cls"), hw = P("h_web"), tw = P("t_web"), bt = P("b_top"), tt = P("t_top"), b1 = P("b_bottom"), t1 = P("t_bottom");
        double b2 = d.B("plate2") ? P("b_bottom2") : 0, t2 = d.B("plate2") ? P("t_bottom2") : 0;
        if (Math.Min(bt, b1) <= tw || b2 > b1 || b2 != 0 && b2 <= tw || hw <= tw)
            throw new ArgumentException("Controllare anima e piattabande: b > tw; la seconda piastra inferiore deve essere contenuta nella prima.");
        var mat = Materials(d);
        double tb = t1 + t2, bb = (b1 * t1 + b2 * t2) / tb, h = hw + tt + tb;
        IRebarSection? Bar(string side)
        {
            if (!d.B("rebars_" + side)) return null;
            double diameter = P("d_" + side), pitch = P("pitch_" + side), cover = P("cover_" + side);
            if (pitch < diameter || cover < diameter / 2 || cover + diameter / 2 > tc || diameter >= b || b / pitch > 500)
                throw new ArgumentException("Armatura " + side + ": controllare diametro, passo e distanza dell'asse dalla faccia (massimo 501 barre/fila).");
            return new RebarSectionCircular(diameter, mat.Rebar);
        }
        var top = Bar("top"); var bottom = Bar("bottom");
        if (top is not null && bottom is not null && tc - d.D("cover_top") - d.D("cover_bottom") < (top.Diameter + bottom.Diameter) / 2)
            throw new ArgumentException("Le file di armature devono avere assi distinti e distanza almeno pari alla somma dei raggi.");
        var shape = new SectionH(h, tw, bt, tt, bb, tb, "H saldato · piattabanda equivalente");
        var section = new ReinforcedConcreteSection(b, tc, mat.Concrete, top!, d.D("pitch_top"), d.D("cover_top"),
            bottom!, d.D("pitch_bottom"), shape, mat.Steel, d.D("cover_bottom"));
        var bars = section.Rebars.Select((r, i) => new BridgeBar("B" + (i + 1), r.Position.X, r.Position.Y, r.RebarSection.Diameter, r.Area)).ToArray();
        for (int i = 0; i < bars.Length; i++) for (int j = 0; j < i; j++)
            if (double.Hypot(bars[i].X - bars[j].X, bars[i].Y - bars[j].Y) < (bars[i].Diameter + bars[j].Diameter) / 2 - 1e-8)
                throw new ArgumentException("Le due file di armature si sovrappongono: correggere le quote degli assi.");
        // Resultant rectangle preserves area and overall thickness; exact two-plate inertia is reported separately.
        double y1 = -tt - hw - t1 / 2, y2 = -h + t2 / 2;
        double ab = b1 * t1 + b2 * t2, yb = (b1 * t1 * y1 + b2 * t2 * y2) / ab;
        double ib = b1 * Math.Pow(t1, 3) / 12 + b1 * t1 * Math.Pow(y1 - yb, 2) + b2 * Math.Pow(t2, 3) / 12 + b2 * t2 * Math.Pow(y2 - yb, 2);
        return new(b, tc, hw, tw, bt, tt, b1, t1, b2, t2, bb, tb, h, ab, yb, ib, shape.Area, shape.Jxx, shape.Centroid.Y - h, bars);
    }
    public static ReinforcedConcreteSection NativeSection(JsonObject d)
    {
        var g = Geometry(d); var m = Materials(d);
        return new ReinforcedConcreteSection(g.Width, g.SlabHeight, m.Concrete,
            d.B("rebars_top") ? new RebarSectionCircular(d.D("d_top"), m.Rebar) : null!, d.D("pitch_top"), d.D("cover_top"),
            d.B("rebars_bottom") ? new RebarSectionCircular(d.D("d_bottom"), m.Rebar) : null!, d.D("pitch_bottom"),
            new SectionH(g.Height, g.WebThickness, g.TopWidth, g.TopThickness, g.BottomEquivalentWidth, g.BottomEquivalentThickness, "H"), m.Steel, d.D("cover_bottom"));
    }
    public static (double N0, double N, double PhiEffective, double Phi) Homogenization(JsonObject data, JsonObject phase)
    {
        var m = Materials(data); double n0 = m.Steel.ElasticModulusTension / m.Concrete.ElasticModulusCompression;
        if (!HomoModes.Contains(phase.S("modo"))) throw new ArgumentException("Modo di omogeneizzazione sconosciuto.");
        double psi = phase.Required("psi", strict: true);
        if (phase.S("modo") == "Da n")
        {
            double n = phase.Required("n", strict: true);
            double effective = ReinforcedConcreteSection.CalculateHomogenizedFactorPhi(n, m.Steel, m.Concrete);
            if (effective < -1e-9) throw new ArgumentException("n deve essere almeno n₀ = Ea/Ecm per una viscosità non negativa.");
            return (n0, n, Math.Max(0, effective), Math.Max(0, effective) / psi);
        }
        double phi = phase.Required("phi"); return (n0, n0 * (1 + psi * phi), psi * phi, phi);
    }
    public static BridgeResult Calculate(JsonObject data, CancellationToken cancellation = default)
    {
        var g = Geometry(data); var mat = Materials(data);
        if (!Standards.Contains(data.S("normativa"))) throw new ArgumentException("Normativa non supportata.");
        if (data.S("stato") is not ("SLU" or "SLE rara" or "SLE quasi permanente")) throw new ArgumentException("Stato limite non supportato.");
        foreach (string k in new[] { "gamma_m0", "gamma_c", "gamma_s", "alpha_cc" }) data.Required(k, strict: true);
        double yref = Number(data, "y_ref");
        var phases = data.Array("fasi").OfType<JsonObject>().Where(p => p.B("attiva")).ToArray();
        if (phases.Length == 0) throw new ArgumentException("Attivare almeno una fase.");
        bool compositeSeen = false;
        foreach (var p in phases)
        {
            if (!PhaseKinds.Contains(p.S("tipo"))) throw new ArgumentException("Tipo di fase sconosciuto.");
            if (p.S("tipo") == "Solo acciaio" && compositeSeen) throw new ArgumentException("Le fasi di solo acciaio devono precedere le fasi composte.");
            compositeSeen |= p.S("tipo") != "Solo acciaio";
            Number(p, "N"); Number(p, "Mx");
        }
        var stages = new List<BridgeStage>();
        for (int end = 0; end < phases.Length; end++)
        {
            cancellation.ThrowIfCancellationRequested();
            var included = phases.Take(end + 1).ToArray();
            var effective = BridgeEffective.Full(g);
            List<BridgeContribution> contributions = [];
            double error = 0; int iter = 0; bool converged = false;
            for (iter = 1; iter <= 120; iter++)
            {
                cancellation.ThrowIfCancellationRequested();
                contributions = included.Select(p => Solve(data, g, effective, p, yref)).ToList();
                double Sigma(double y) => contributions.Sum(c => c.SteelStress(y));
                var next = data.B("classe4") ? EffectiveWidths(g, Sigma, mat.Steel.Fyk) : BridgeEffective.Full(g);
                error = effective.Distance(next, g);
                if (error < 1e-7) { converged = true; break; }
                effective = effective.Relax(next, .55);
            }
            if (!converged) throw new InvalidOperationException($"{phases[end].S("nome")}: sezione efficace non convergente dopo 120 iterazioni. Nessun esito utilizzabile.");
            // Keep properties and stresses on the same converged geometry. Diagnostic plate data are recomputed on those stresses.
            var diagnostics = data.B("classe4") ? EffectiveWidths(g, y => contributions.Sum(c => c.SteelStress(y)), mat.Steel.Fyk) : BridgeEffective.Full(g);
            effective = effective with { Web = diagnostics.Web, Top = diagnostics.Top, Bottom = diagnostics.Bottom };
            var points = StressPoints(g, contributions, data, Math.Abs(mat.Concrete.Fck), mat.Steel.Fyk, mat.Rebar.Fyk);
            var warnings = new List<string>();
            if (points.Any(p => p.Material == "CLS" && p.Stress > 1e-6)) warnings.Add("CLS teso nel modello non fessurato: valutare una situazione con soletta esclusa; non è attestata la verifica del CLS in trazione.");
            if (!data.B("classe4")) warnings.Add("Sezione lorda: riduzioni locali disattivate. Risultato di confronto, non verifica di classe 4.");
            if (g.Bottom2Thickness > 0) warnings.Add("Due piastre inferiori sostituite nel calcolo dal rettangolo equivalente ad area e spessore totali uguali. Baricentro e inerzia delle piastre reali sono riportati per confronto.");
            var effectiveParts = SteelPieces(g, effective, mat.Steel);
            double effectiveArea = effectiveParts.Sum(p => p.Section.Area);
            double effectiveCentroid = effectiveParts.Sum(p => p.Section.Area * p.PositionToGlobal(p.Section.Centroid).Y) / effectiveArea;
            double effectiveInertia = effectiveParts.Sum(p => p.Section.Jxx * Math.Pow(Math.Cos(p.Rotation), 2) + p.Section.Jyy * Math.Pow(Math.Sin(p.Rotation), 2)
                + p.Section.Area * Math.Pow(p.PositionToGlobal(p.Section.Centroid).Y - effectiveCentroid, 2));
            stages.Add(new(phases[end].S("nome"), iter, error, effective, new(effectiveArea, effectiveCentroid, effectiveInertia), contributions, points, warnings));
        }
        return new(Method, Scope, (JsonObject)data.DeepClone(), g,
            new(mat.Concrete.Name, Math.Abs(mat.Concrete.Fck), mat.Concrete.ElasticModulusCompression, mat.Steel.Name, mat.Steel.Fyk, mat.Steel.ElasticModulusTension,
                mat.Rebar.Name, mat.Rebar.Fyk, mat.Rebar.ElasticModulusTension), stages);
    }
    private static double Number(JsonObject d, string k) => J.Number(d[k]) ?? throw new ArgumentException(k + ": numero finito richiesto.");
    private static BridgeContribution Solve(JsonObject d, BridgeGeometry g, BridgeEffective e, JsonObject p, double yref)
    {
        var mat = Materials(d); string kind = p.S("tipo");
        var homo = kind == "Composta" ? Homogenization(d, p) : (0d, 0d, 0d, 0d);
        double n = homo.Item2, area, cy, inertia;
        ReinforcedConcreteSection? composite = null;
        var steel = SteelPieces(g, e, mat.Steel);
        if (kind == "Composta")
        {
            var section = composite = NativeSection(d); section.SteelSections.Clear();
            foreach (var part in steel) section.AddSteelSection(part);
            // Native Model transforms into concrete; divide by n to report in structural steel.
            var props = section.GetHomogeneizedMechanicalProperties(homo.Item3);
            area = props.areaH / n; cy = props.centroidH.Y; inertia = props.JxxH / n;
        }
        else
        {
            var parts = steel.Select(s => (A: s.Section.Area, Y: s.PositionToGlobal(s.Section.Centroid).Y,
                I: s.Section.Jxx * Math.Pow(Math.Cos(s.Rotation), 2) + s.Section.Jyy * Math.Pow(Math.Sin(s.Rotation), 2))).ToList();
            if (kind == "Soletta esclusa")
                parts.AddRange(g.Bars.Select(b => (b.Area * mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension, b.Y,
                    Math.PI * Math.Pow(b.Diameter, 4) / 64 * mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension)));
            area = parts.Sum(x => x.A); cy = parts.Sum(x => x.A * x.Y) / area;
            inertia = parts.Sum(x => x.I + x.A * Math.Pow(x.Y - cy, 2));
        }
        if (!double.IsFinite(inertia) || inertia <= 0 || !double.IsFinite(area) || area <= 0) throw new InvalidOperationException("Proprietà della sezione non valide.");
        double force = Number(p, "N") * 1000, moment = Number(p, "Mx") * 1e6;
        double mg = moment + force * (cy - yref), uniform = force / area, slope = -mg / inertia;
        double solverInertia = inertia;
        if (composite is not null && (force != 0 || moment != 0))
        {
            // Same axes and linear API as Checker MixedSectionTest; psi in its API is psiL*phi.
            var axes = new CoordinateSystem(new Point2d(g.Width / 2, yref), new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Elastic, SectionSolver.StressAnalysisTypes.Linear, homo.Item3, 0, true, 16);
            SectionCheckerModelCode2010 checker;
            lock (CheckerSection.NativeSolverConstruction)
                checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(composite), options,
                    ConcreteStandards.Create(d.S("normativa").StartsWith("NTC") ? "NTC 2018" : "EN 1992-1-1"), true, -1, new StandardEN1993p11());
            var response = checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(force, 0, 0, 0, moment, 0, axes), homo.Item3, 0);
            var vertices = response.GetStructuralSteelVerticesTension(homo.Item3).OrderBy(v => v.point.Y).ToArray();
            var low = vertices.First(); var high = vertices.Last();
            slope = (high.tension - low.tension) / (high.point.Y - low.point.Y);
            uniform = low.tension + slope * (cy - low.point.Y);
            if (!double.IsFinite(uniform) || !double.IsFinite(slope)) throw new InvalidOperationException("Checker non ha restituito un campo tensionale finito.");
            // Independent equilibrium audit of Checker's line-wall / point-rebar integration.
            solverInertia -= e.TopWidth * Math.Pow(g.TopThickness, 3) / 12 + e.BottomWidth * Math.Pow(g.BottomEquivalentThickness, 3) / 12
                + g.Bars.Sum(b => Math.PI * Math.Pow(b.Diameter, 4) / 64) * (mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension - 1 / n);
        }
        double calculatedN = uniform * area, calculatedM = -slope * solverInertia - calculatedN * (cy - yref);
        double span = g.Height + g.SlabHeight;
        double equilibrium = Math.Max(Math.Abs(calculatedN - force) / Math.Max(1, Math.Max(Math.Abs(force), Math.Abs(moment) / span)),
            Math.Abs(calculatedM - moment) / Math.Max(1, Math.Max(Math.Abs(moment), Math.Abs(force) * span)));
        if (!double.IsFinite(equilibrium) || equilibrium > 1e-5) throw new InvalidOperationException($"Equilibrio della fase {p.S("nome")} non soddisfatto (residuo {equilibrium:E2}).");
        return new(p.S("nome"), kind, force / 1000, moment / 1e6, homo.Item1, n, homo.Item4, homo.Item3,
            area, cy, inertia, uniform, slope, mat.Rebar.ElasticModulusTension / mat.Steel.ElasticModulusTension,
            kind == "Solo acciaio" ? 0 : g.Bars.Sum(b => b.Area), inertia / Math.Abs(-g.Height - cy), Math.Abs(cy) < 1e-9 ? null : inertia / Math.Abs(cy), solverInertia, equilibrium);
    }
    private static List<SteelSectionPosition> SteelPieces(BridgeGeometry g, BridgeEffective e, SteelMaterial material)
    {
        var pieces = new List<SteelSectionPosition>();
        void Add(double width, double height, double y, bool web = false)
        {
            if (width <= 1e-8 || height <= 1e-8) return;
            // Checker integrates along the ThinWall centreline: orient vertical webs along their length,
            // never represent them as a horizontal short line with the web height as its thickness.
            var rectangle = web ? new SectionRectangular(width, height) : new SectionRectangular(height, width);
            pieces.Add(new SteelSectionPosition(new SteelSection(rectangle, material), Point2d.Origin, web ? Math.PI / 2 : 0,
                new Point2d(g.Width / 2, y + height / 2), InsertionPointType.Centroid) { IsInsideConcrete = false });
        }
        Add(e.TopWidth, g.TopThickness, -g.TopThickness);
        Add(g.WebThickness, e.WebTop, -g.TopThickness - e.WebTop, true);
        Add(g.WebThickness, e.WebBottom, -g.TopThickness - g.WebHeight, true);
        Add(e.BottomWidth, g.BottomEquivalentThickness, -g.Height);
        return pieces;
    }
    public static BridgeEffective EffectiveWidths(BridgeGeometry g, Func<double, double> stress, double fy)
    {
        double upper = stress(-g.TopThickness), lower = stress(-g.TopThickness - g.WebHeight);
        var web = InternalPlate(g.WebHeight, g.WebThickness, upper, lower, fy);
        var top = Outstand((g.TopWidth - g.WebThickness) / 2, g.TopThickness, Math.Min(stress(0), upper), fy);
        var bottom = Outstand((g.BottomEquivalentWidth - g.WebThickness) / 2, g.BottomEquivalentThickness, Math.Min(lower, stress(-g.Height)), fy);
        return new(web.EffectiveAtStart, web.EffectiveAtEnd, g.WebThickness + 2 * top.EffectiveAtStart,
            g.WebThickness + 2 * bottom.EffectiveAtStart, web, top, bottom);
    }
    /// <summary>EN 1993-1-5 §4.4 / table 4.1; negative stress denotes compression.</summary>
    public static BridgePlate InternalPlate(double b, double t, double startStress, double endStress, double fy)
    {
        bool startCompressed = startStress <= endStress;
        double s1 = Math.Min(startStress, endStress), s2 = Math.Max(startStress, endStress);
        if (s1 >= -1e-9) return new(b, t, 0, 0, 0, 1, 0, b / 2, b / 2, startStress, endStress);
        double psi = s2 / s1, bounded = Math.Clamp(psi, -3, 1);
        double k = bounded >= 0 ? 8.2 / (1.05 + bounded) : bounded >= -1 ? 7.81 - 6.29 * bounded + 9.78 * bounded * bounded : 5.98 * Math.Pow(1 - bounded, 2);
        double lambda = b / t / (28.4 * Math.Sqrt(235 / fy) * Math.Sqrt(k));
        double limit = .5 + Math.Sqrt(.085 - .055 * bounded);
        double rho = lambda <= limit ? 1 : Math.Clamp((lambda - .055 * (3 + bounded)) / (lambda * lambda), 0, 1);
        double bc = psi < 0 ? b / (1 - psi) : b, beff = rho * bc;
        double b1 = psi < 0 ? .4 * beff : 2 * beff / (5 - psi), b2 = beff - b1 + b - bc;
        return new(b, t, psi, k, lambda, rho, bc, startCompressed ? b1 : b2, startCompressed ? b2 : b1, startStress, endStress);
    }
    private static BridgePlate Outstand(double b, double t, double stress, double fy)
    {
        double lambda = b / t / (28.4 * Math.Sqrt(235 / fy) * Math.Sqrt(.43));
        double rho = stress >= -1e-9 || lambda <= .748 ? 1 : Math.Clamp((lambda - .188) / (lambda * lambda), 0, 1);
        return new(b, t, 1, .43, lambda, rho, stress < 0 ? b : 0, rho * b, 0, stress, stress);
    }
    private static List<BridgeStressPoint> StressPoints(BridgeGeometry g, List<BridgeContribution> c, JsonObject d, double fck, double fy, double fys)
    {
        var points = new List<BridgeStressPoint>();
        double lc = d.S("stato") == "SLU" ? d.D("alpha_cc") * fck / d.D("gamma_c") : (d.S("stato") == "SLE rara" ? .6 : .45) * fck;
        double la = d.S("stato") == "SLU" ? fy / d.D("gamma_m0") : fy;
        double ls = d.S("stato") == "SLU" ? fys / d.D("gamma_s") : .8 * fys;
        void Add(string label, string material, double y, double limit)
        {
            var values = c.Select(p => p.Stress(material, y)).ToArray(); double stress = values.Sum();
            bool active = material switch { "Acciaio" => true, "CLS" => c.Any(x => x.Kind == "Composta"), _ => c.Any(x => x.Kind != "Solo acciaio") };
            double? utilization = !active || material == "CLS" && stress > 1e-6 ? null : Math.Abs(stress) / limit;
            points.Add(new(label, material, y, stress, limit, utilization, active, values));
        }
        Add("Soletta · estradosso", "CLS", g.SlabHeight, lc); Add("Soletta · intradosso", "CLS", 0, lc);
        foreach (var row in g.Bars.GroupBy(b => b.Y).OrderByDescending(x => x.Key)) Add("Armatura · y=" + row.Key.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "Armatura", row.Key, ls);
        Add("Acciaio · estradosso", "Acciaio", 0, la); Add("Anima · sommità", "Acciaio", -g.TopThickness, la);
        Add("Anima · piede", "Acciaio", -g.TopThickness - g.WebHeight, la); Add("Acciaio · intradosso", "Acciaio", -g.Height, la);
        return points;
    }
}

public sealed record BridgeBar(string Id, double X, double Y, double Diameter, double Area);
public sealed record BridgeGeometry(double Width, double SlabHeight, double WebHeight, double WebThickness, double TopWidth, double TopThickness,
    double Bottom1Width, double Bottom1Thickness, double Bottom2Width, double Bottom2Thickness, double BottomEquivalentWidth, double BottomEquivalentThickness,
    double Height, double BottomArea, double BottomRealCentroid, double BottomRealInertia, double SteelArea, double SteelInertia, double SteelCentroid, BridgeBar[] Bars);
public sealed record BridgeMaterialValues(string Concrete, double Fck, double Ec, string Steel, double Fy, double Ea, string Rebar, double Fys, double Es);
public sealed record BridgePlate(double Width, double Thickness, double Psi, double KSigma, double Lambda, double Rho, double CompressedWidth,
    double EffectiveAtStart, double EffectiveAtEnd, double StartStress, double EndStress);
public sealed record BridgeEffective(double WebTop, double WebBottom, double TopWidth, double BottomWidth, BridgePlate Web, BridgePlate Top, BridgePlate Bottom)
{
    public static BridgeEffective Full(BridgeGeometry g)
    {
        var plate = new BridgePlate(g.WebHeight, g.WebThickness, 0, 0, 0, 1, 0, g.WebHeight / 2, g.WebHeight / 2, 0, 0);
        return new(g.WebHeight / 2, g.WebHeight / 2, g.TopWidth, g.BottomEquivalentWidth, plate,
            plate with { Width = (g.TopWidth - g.WebThickness) / 2, Thickness = g.TopThickness, EffectiveAtStart = (g.TopWidth - g.WebThickness) / 2, EffectiveAtEnd = 0 },
            plate with { Width = (g.BottomEquivalentWidth - g.WebThickness) / 2, Thickness = g.BottomEquivalentThickness, EffectiveAtStart = (g.BottomEquivalentWidth - g.WebThickness) / 2, EffectiveAtEnd = 0 });
    }
    public double Distance(BridgeEffective b, BridgeGeometry g) => new[] { Math.Abs(WebTop - b.WebTop) / g.WebHeight, Math.Abs(WebBottom - b.WebBottom) / g.WebHeight,
        Math.Abs(TopWidth - b.TopWidth) / g.TopWidth, Math.Abs(BottomWidth - b.BottomWidth) / g.BottomEquivalentWidth }.Max();
    public BridgeEffective Relax(BridgeEffective b, double f) => this with { WebTop = WebTop * (1 - f) + b.WebTop * f, WebBottom = WebBottom * (1 - f) + b.WebBottom * f,
        TopWidth = TopWidth * (1 - f) + b.TopWidth * f, BottomWidth = BottomWidth * (1 - f) + b.BottomWidth * f };
}
public sealed record BridgeContribution(string Name, string Kind, double N, double Mx, double N0, double HomogenizationN, double Phi, double EffectivePhi,
    double Area, double Centroid, double Inertia, double UniformStress, double StressSlope, double RebarRatio, double RebarArea, double WBottom, double? WTop, double SolverInertia, double EquilibriumResidual)
{
    public double SteelStress(double y) => UniformStress + StressSlope * (y - Centroid);
    public double Stress(string material, double y) => material == "CLS" ? Kind == "Composta" ? SteelStress(y) / HomogenizationN : 0 :
        material == "Armatura" ? Kind == "Solo acciaio" ? 0 : SteelStress(y) * RebarRatio : SteelStress(y);
    public double? NeutralAxis => Math.Abs(StressSlope) < 1e-15 ? null : Centroid - UniformStress / StressSlope;
}
public sealed record BridgeStressPoint(string Name, string Material, double Y, double Stress, double Limit, double? Utilization, bool Active, double[] Contributions);
public sealed record BridgeSteelProperties(double Area, double Centroid, double Inertia);
public sealed record BridgeStage(string Name, int Iterations, double Residual, BridgeEffective Effective, BridgeSteelProperties EffectiveSteel, List<BridgeContribution> Contributions,
    List<BridgeStressPoint> Points, List<string> Warnings)
{
    public double MaxUtilization => Points.Where(p => p.Utilization.HasValue).Select(p => p.Utilization!.Value).DefaultIfEmpty().Max();
    public double? SteelNeutralAxis
    {
        get { double slope = Contributions.Sum(c => c.StressSlope); return Math.Abs(slope) < 1e-15 ? null : -Contributions.Sum(c => c.SteelStress(0)) / slope; }
    }
}
public sealed record BridgeResult(string Method, string Scope, JsonObject Input, BridgeGeometry Geometry, BridgeMaterialValues Materials, List<BridgeStage> Stages)
{
    public JsonObject Json() => J.Node(this)!.AsObject();
}
