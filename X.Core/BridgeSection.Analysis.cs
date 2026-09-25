using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace X.Core;

// Orchestration and Checker adapter, isolated for review before migration to Checker.
// Phase load references added separately from the existing effective-width iteration.
public static partial class BridgeSection
{
    public static BridgeResult Calculate(JsonObject data, CancellationToken cancellation = default)
    {
        var g = Geometry(data); var mat = Materials(data);
        if (!Standards.Contains(data.S("normativa"))) throw new ArgumentException("Normativa non supportata.");
        if (data.S("stato") is not ("SLU" or "SLE rara" or "SLE quasi permanente")) throw new ArgumentException("Stato limite non supportato.");
        foreach (string k in new[] { "gamma_m0", "gamma_c", "gamma_s", "alpha_cc" }) data.Required(k, strict: true);
        var phases = data.Array("fasi").OfType<JsonObject>().Where(p => p.B("attiva")).ToArray();
        if (phases.Length == 0) throw new ArgumentException("Attivare almeno una fase.");
        bool compositeSeen = false;
        foreach (var p in phases)
        {
            if (!PhaseKinds.Contains(p.S("tipo"))) throw new ArgumentException("Tipo di fase sconosciuto.");
            if (p.S("tipo") == "Solo acciaio" && compositeSeen) throw new ArgumentException("Le fasi di solo acciaio devono precedere le fasi composte.");
            compositeSeen |= p.S("tipo") is "Composta" or "Soletta esclusa";
            if (p.S("tipo") == ShrinkageKind) Number(p, "epsilon_cs");
            else { Number(p, "N"); Number(p, "Mx"); if (p.ContainsKey("V")) Number(p, "V"); }
            if (!LoadReferences.Contains(LoadReference(p))) throw new ArgumentException("Riferimento di N sconosciuto.");
        }
        // null means follow the effective centroid inside Solve; fixed points are resolved once.
        var applicationPoints = phases.ToDictionary(p => p, p => p.S("tipo") == ShrinkageKind ? null : LoadReference(p) switch
        {
            CommonLoadReference => (double?)Number(data, "y_ref"),
            GrossLoadReference => GrossPhaseCentroid(data, p),
            _ => null
        });
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
                contributions = included.Select(p => Solve(data, g, effective, p, applicationPoints[p])).ToList();
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
            if (contributions.Any(c => c.IsShrinkage)) warnings.Add("Ritiro uniforme imposto al solo CLS: effetti primari autoequilibrati. Eventuali azioni secondarie da vincoli esterni vanno inserite come fasi N–Mx separate.");
            if (g.Bottom2Thickness > 0) warnings.Add("Due piastre inferiori sostituite nel calcolo dal rettangolo equivalente ad area e spessore totali uguali. Baricentro e inerzia delle piastre reali sono riportati per confronto.");
            var effectiveParts = SteelPieces(g, effective, mat.Steel);
            double effectiveArea = effectiveParts.Sum(p => p.Section.Area);
            double effectiveCentroid = effectiveParts.Sum(p => p.Section.Area * p.PositionToGlobal(p.Section.Centroid).Y) / effectiveArea;
            double effectiveInertia = effectiveParts.Sum(p => p.Section.Jxx * Math.Pow(Math.Cos(p.Rotation), 2) + p.Section.Jyy * Math.Pow(Math.Sin(p.Rotation), 2)
                + p.Section.Area * Math.Pow(p.PositionToGlobal(p.Section.Centroid).Y - effectiveCentroid, 2));
            var materialValues = new BridgeMaterialValues(mat.Concrete.Name, Math.Abs(mat.Concrete.Fck), mat.Concrete.ElasticModulusCompression,
                mat.Steel.Name, mat.Steel.Fyk, mat.Steel.ElasticModulusTension, mat.Rebar.Name, mat.Rebar.Fyk, mat.Rebar.ElasticModulusTension);
            var accessory = CalculateShear(data, g, materialValues, effective, contributions, included, warnings);
            stages.Add(new(phases[end].S("nome"), iter, error, effective, new(effectiveArea, effectiveCentroid, effectiveInertia), contributions, points, warnings, accessory.Shear, accessory.Studs));
        }
        return new(Method, Scope, (JsonObject)data.DeepClone(), g,
            new(mat.Concrete.Name, Math.Abs(mat.Concrete.Fck), mat.Concrete.ElasticModulusCompression, mat.Steel.Name, mat.Steel.Fyk, mat.Steel.ElasticModulusTension,
                mat.Rebar.Name, mat.Rebar.Fyk, mat.Rebar.ElasticModulusTension), stages);
    }
    private static double Number(JsonObject d, string k) => J.Number(d[k]) ?? throw new ArgumentException(k + ": numero finito richiesto.");
    private static BridgeContribution Solve(JsonObject d, BridgeGeometry g, BridgeEffective e, JsonObject p, double? applicationPoint)
    {
        if (p.S("tipo") == ShrinkageKind) return SolveShrinkage(d, g, e, p);
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
        double yref = applicationPoint ?? cy;
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
            kind == "Solo acciaio" ? 0 : g.Bars.Sum(b => b.Area), inertia / Math.Abs(-g.Height - cy), Math.Abs(cy) < 1e-9 ? null : inertia / Math.Abs(cy), solverInertia, equilibrium, yref, LoadReference(p), p.ContainsKey("V") ? Number(p, "V") : 0,
            ConnectionFlowExtra: kind != "Solo acciaio" && p.ContainsKey("q_conn") ? Number(p, "q_conn") : 0);
    }
    private static BridgeContribution SolveShrinkage(JsonObject d, BridgeGeometry g, BridgeEffective effective, JsonObject phase)
    {
        // Input in microstrain: contraction is negative. Bars do not receive an eigenstrain.
        double strain = Number(phase, "epsilon_cs") * 1e-6;
        double ec = Materials(d).Steel.ElasticModulusTension / Homogenization(d, phase).N;
        double ac = g.Width * g.SlabHeight - g.Bars.Sum(b => b.Area);
        double yc = (g.Width * g.SlabHeight * g.SlabHeight / 2 - g.Bars.Sum(b => b.Area * b.Y)) / ac;
        double equivalentForce = ec * ac * strain;
        var fictitious = (JsonObject)phase.DeepClone(); fictitious["tipo"] = "Composta";
        fictitious["N"] = equivalentForce / 1000; fictitious["Mx"] = 0; fictitious["V"] = 0;
        var response = Solve(d, g, effective, fictitious, yc);
        // The equivalent force solves compatibility. Removing the free-strain stress in
        // concrete restores zero external N and M; omitting this term is NOT shrinkage.
        return response with { Kind = ShrinkageKind, N = 0, Mx = 0, V = 0, ShrinkageStrain = strain,
            ConcreteStressOffset = -ec * strain, EquivalentN = equivalentForce / 1000,
            EquivalentMomentAtInterface = -equivalentForce * yc / 1e6, LoadReference = "Ritiro · CLS netto", LoadY = yc };
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
    private static List<BridgeStressPoint> StressPoints(BridgeGeometry g, List<BridgeContribution> c, JsonObject d, double fck, double fy, double fys)
    {
        var points = new List<BridgeStressPoint>();
        double lc = d.S("stato") == "SLU" ? d.D("alpha_cc") * fck / d.D("gamma_c") : (d.S("stato") == "SLE rara" ? .6 : .45) * fck;
        double la = d.S("stato") == "SLU" ? fy / d.D("gamma_m0") : fy;
        double ls = d.S("stato") == "SLU" ? fys / d.D("gamma_s") : .8 * fys;
        void Add(string label, string material, double y, double limit)
        {
            var values = c.Select(p => p.Stress(material, y)).ToArray(); double stress = values.Sum();
            bool active = material switch { "Acciaio" => true, "CLS" => c.Any(x => x.HasConcrete), _ => c.Any(x => x.Kind != "Solo acciaio") };
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
