using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Analysis;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;

namespace Anthea.Calculations;

/// <summary>Unit/DTO adapter only. All section equilibrium and domain searches belong to the shipped Checker DLL.</summary>
public sealed class CheckerSectionModel
{
    public ReinforcedConcreteSection Section { get; }
    public CoordinateSystem Local { get; }
    public SezioneCA Geometry { get; }
    internal CheckerSectionModel(ReinforcedConcreteSection section, CoordinateSystem local, SezioneCA geometry)
        => (Section, Local, Geometry) = (section, local, geometry);
}

public sealed partial class CheckerSection
{
    public CheckerSectionModel Model { get; }
    public ReinforcedConcreteSection Section { get; }
    public CoordinateSystem Local { get; }
    public CoordinateSystem ForceAxes { get; }
    public SectionCheckerModelCode2010 Checker { get; }
    public SezioneCA Geometry { get; } // Existing parametric geometry only; never used for resistance/stress.
    public JsonObject Options { get; }
    private readonly double compressionReduction;
    private readonly StandardModelCode2010 standard;
    public static readonly System.Collections.Immutable.ImmutableArray<string> Criteria = ["N costante", "Eccentricità costante", "Mx–My costanti", "N e Mx costanti", "N e My costanti"];
    public static SectionSolver.FailureAnalysisTypes Criterion(string text) => text switch
    {
        "N costante" => SectionSolver.FailureAnalysisTypes.ConstantN,
        "Eccentricità costante" => SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
        "Mx–My costanti" => SectionSolver.FailureAnalysisTypes.ConstantMxMy,
        "N e Mx costanti" => SectionSolver.FailureAnalysisTypes.ConstantNMx,
        "N e My costanti" => SectionSolver.FailureAnalysisTypes.ConstantNMy,
        _ => throw new ArgumentException("Criterio Checker non riconosciuto.")
    };
    public static CheckerSectionModel PrepareModel(JsonObject input, JsonObject workspace)
    {
        var geometry = new SezioneCA(input);
        var concrete = ConcreteMaterials.Concrete(input);
        var steel = ConcreteMaterials.Rebar(input);
        var shape = SectionGeometry.Shape(geometry);
        var section = new ReinforcedConcreteSection(shape, concrete);
        var boundary = SectionGeometry.Polygon(geometry.Outline); var holes = geometry.Holes.Select(SectionGeometry.Polygon).ToArray();
        for (int i = 0; i < geometry.Bars.Count; i++)
        {
            var b = geometry.Bars[i]; ValidateRebarPosition(boundary, holes, section, b.X, b.Y, b.Diametro / 2, "B" + (i + 1).ToString("D2"));
            section.AddRebars([new ReinforcedConcreteRebar(new RebarSectionCircular(b.Diametro, steel), new Point2d(b.X, b.Y))]);
        }
        foreach (var t in workspace.Array("trefoli"))
        {
            double area = t!.Required("area", strict: true), ep = t.Required("Ep", strict: true), fpy = t.Required("fpyk", strict: true), fpu = t.Required("fpk", strict: true);
            double strain = t.Required("eps_u", strict: true) / 1000, sigma = t.Required("sigma0");
            if (fpu < fpy || strain <= fpy / ep || sigma >= fpu) throw new ArgumentException("Trefolo: controllare fpk, fpyk, εpu e σp0.");
            var material = new SteelMaterial(t.S("id"), ep, fpy, fpu, strain, t.S("diagramma", "Incrudente") == "Elastoplastico" ? SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic : SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Tendon);
            double x = SectionWorkspace.Number(t.S("x"), "x trefolo"), y = SectionWorkspace.Number(t.S("y"), "y trefolo"), radius = Math.Sqrt(area / Math.PI);
            ValidateRebarPosition(boundary, holes, section, x, y, radius, t.S("id"));
            section.AddRebars([new ReinforcedConcreteRebar(new RebarSectionCircular(2 * radius, material), new Point2d(x, y), sigma)]);
        }
        return new(section, new CoordinateSystem(section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0)), geometry);
    }
    private readonly Lazy<ISectionResistanceSolver> resistanceSolver;
    /// <summary>Sessione della porta della libreria per il punto resistente SLU (<see cref="ConcreteResistanceAdapter"/>: dominio 3D, resistenza
    /// a N assegnato e progetto delle armature): una sola per sezione, creata al primo uso.</summary>
    internal ISectionResistanceSolver ResistanceSolver => resistanceSolver.Value;
    public CheckerSection(JsonObject input, JsonObject workspace, JsonObject options, string state = "SLU")
        : this(PrepareModel(input, workspace), input, workspace, options, state) { }
    public CheckerSection(CheckerSectionModel model, JsonObject input, JsonObject workspace, JsonObject options, string state = "SLU")
    {
        resistanceSolver = new(() => new LegacySectionCalculation(Checker));
        Model = model; Options = (JsonObject)options.DeepClone(); Geometry = model.Geometry; Section = model.Section; Local = model.Local;
        ForceAxes = new CoordinateSystem(Local);
        switch (options.S("assi", "Locali"))
        {
            case "Principali": ForceAxes.RotateV3(Section.AngleX1); break;
            case "Personalizzati":
                ForceAxes = new CoordinateSystem(new Point2d(N("origine_x"), N("origine_y")), new Vector2d(-1, 0), new Vector2d(0, -1));
                ForceAxes.RotateV3(N("rotazione") * Math.PI / 180); break;
            case "Locali": break;
            default: throw new ArgumentException("Sistema di riferimento non riconosciuto.");
        }
        standard = ConcreteStandards.Effective(input, workspace);
        // Getti sottili (refactoring F2.7b, commit A3, rilievo M14): fattore della norma dalla libreria attraverso la mappatura, stessa fonte
        // di fcd in ConcreteMaterials.DesignValues; vale per αcc e per il limite SLE del calcestruzzo. Si calcola dopo Effective, così con
        // assi o norma non validi il primo rifiuto resta quello di prima. 'gettato_sottile' si legge solo se la norma riduce (fattore
        // diverso da 1), come nella regola legacy: stessi ripieghi registrati da JsonFallbackTrace.
        double thinCasting = ConcreteLibraryMapping.ThinCastingFactor(standard);
        compressionReduction = thinCasting != 1 && input.S("gettato_sottile", "No") == "Sì" ? thinCasting : 1;
        standard.AlphaCC *= compressionReduction;
        bool tensile = options.S("trazione_cls", "No") == "Sì";
        int subdivisions = SectionWorkspace.Subdivisions(options.S("angoli", "32"), "Direzioni angolari", 4, 360);
        var settings = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(Local, Criterion(options.S("criterio", "N costante")),
            options.S("modello_resistente", state == "SLV" ? "Elastico" : "Plastico") switch
            {
                "Elastico" => SectionSolver.FailureDomainTypes.Elastic,
                "Plastico" => SectionSolver.FailureDomainTypes.Plastic,
                _ => throw new ArgumentException("Criterio resistente: scegliere Plastico o Elastico.")
            },
            options.S("modello", "Non lineare") == "Lineare" ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear,
            Nonnegative("phi"), workspace.Array("trefoli").Count > 0 ? Nonnegative("phi_trefoli") : 0, tensile, subdivisions);
        // GPC owns the per-section mesh cache lock; independent checker instances may be constructed concurrently.
        // Captured usual meshes and serial/parallel stress states are bit-identical (F2 point 4).
        Checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(Section, null, null), settings, standard, tensile);
        Checker.SetDomainPointStrategy(options.S("strategia", "Iterativo") == "Intersezione" ? SectionSolver.DomainPointStrategyTypes.Intersection : SectionSolver.DomainPointStrategyTypes.Iterative);
    }
    private static void ValidateRebarPosition(Polygon2d boundary, Polygon2d[] holes, ReinforcedConcreteSection section, double x, double y, double radius, string id)
    {
        var point = new Point2d(x, y);
        if (SectionGeometry.SignedCover(boundary, holes, point, radius) < -1e-8)
            throw new ArgumentException($"Armatura {id}: area esterna al contorno o interferente con un foro della sezione.");
        foreach (var bar in section.Rebars)
        {
            // Cross-section radii include ordinary bars and tendons already inserted.
            if (point.DistanceTo(new Point2d(bar.Position.X, bar.Position.Y)) < radius + ReinforcementGeometry.EquivalentDiameter(bar.Area) / 2 - 1e-8)
                throw new ArgumentException($"Armatura {id}: sovrapposizione con un’altra armatura.");
        }
    }
    private double N(string key) => SectionWorkspace.Number(Options.S(key, "0"), key);
    internal SectionResponse? Describe(FailureDomain.FailureDomainPoint point)
        => point is null ? null : Describe(point.NRd, point.MxRd, point.MyRd, point.StrainPlane);
    internal SectionResponse? Describe(ResistancePoint point) => Describe(point.NRd, point.MxRd, point.MyRd, point.StrainPlane);
    private SectionResponse? Describe(double nRd, double mxRd, double myRd, StrainPlane plane)
    {
        // Reporting only: use the same native strain plane that generated the resistance.
        try
        {
            var force = new ResultBeamForces(nRd, 0, 0, 0, mxRd, myRd, Local);
            return SectionResponse.From(plane.CalculateStrainPlaneResult(Section, force, Checker.SectionSolver, standard));
        }
        catch (Exception) { return null; } // A missing report must not alter the native resistance result.
    }
    private double Nonnegative(string key) { double v = N(key); if (v < 0) throw new ArgumentException(key + " deve essere non negativo."); return v; }
    public ResultBeamForces Force(ActionPoint p, int id = 1) => new ResultBeamForces(p.N * 1000, 0, 0, 0, p.Mx * 1e6, p.My * 1e6, ForceAxes, id).ToCoordinateSystemWithEccentricity(Local);
    public static ActionPoint Point(ResultBeamForces p) => new(p.N / 1000, p.M1 / 1e6, p.M2 / 1e6);
    public static ActionPoint Point(FailureDomain.FailureDomainPoint p) => new(p.NRd / 1000, p.MxRd / 1e6, p.MyRd / 1e6);
    public static ActionPoint Point(ResistancePoint p) => new(p.NRd / 1000, p.MxRd / 1e6, p.MyRd / 1e6);
    public CheckerDomain3D Domain3D(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        FailureDomainResult result;
        try { result = Checker.GetFailureDomainResult() ?? throw new ArgumentException("Checker non ha restituito il dominio."); }
        catch (Exception ex) { throw new ArgumentException("Checker: " + ex.Message + " · " + string.Join("; ", Checker.SectionSolver.GetLog().Distinct()), ex); }
        token.ThrowIfCancellationRequested();
        result.Domain.ForceLinearInterpolation = Options.S("interpolazione") == "Lineare";
        result.Domain.AxialForceSubdivision = SectionWorkspace.Subdivisions(Options.S("suddivisioni_n", "50"), "Suddivisioni N (mesh)", 5, 200);
        return new(this, result);
    }
    public CheckerDomain2D Domain2D(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        bool nm = Options.S("tipo") == "N–M";
        double theta = N("theta") * Math.PI / 180, axial = N("N");
        // N–M: sempre il meridiano della direzione del momento della libreria (CalculateDomainConstantMomentsRatio), per ogni θ e ogni sistema di
        // assi, SLU (plastico) e SLV (elastico): le resistenze hanno il momento nel piano θ (SLU-1 del 15/10/2026). Prima, a θ multiplo di 90° in
        // assi locali, si usava come nel CheckerUI il meridiano dell'asse neutro fisso (GetPlastic/ElasticFailureDomainResult2d): nelle sezioni non
        // simmetriche rispetto al piano di flessione ha un momento trasversale (fino a 42 kNm nella 2Ø16/3Ø20 a θ = 90°) e la sua sezione N–M
        // sovrastimava la resistenza di circa il 5%. Mx–My a N costante: sezione del dominio 3D, invariata.
        var d = Checker.GetFailureDomainResult();
        FailureDomainResult2d result = nm ? d.CalculateDomainConstantMomentsRatio(Force(new(0, Math.Cos(theta), Math.Sin(theta))))
            : d.CalculateDomainConstantAxialForce(Force(new(axial, 0, 0)));
        token.ThrowIfCancellationRequested();
        if (result?.Domain?.DomainPoints is not { Length: > 2 } points || points.Any(p => p is null)) throw new ArgumentException("Checker: sezione del dominio assente; controllare N e direzione.");
        result.ClearForces();
        return new(this, result, nm, theta, axial);
    }
    public CheckerStressState Stress(ActionPoint action, string set)
    {
        if (Section.Rebars.Any(b => b.RebarMaterial.SteelType == SteelMaterial.SteelTypes.Tendon && b.EpsilonP == 0))
            throw new ArgumentException("SLE trefolo con σp0 nullo: la DLL distingue il trefolo tramite la predeformazione; classificazione e limiti da completare. Nessun esito automatico.");
        // Correct public single-force overload (the old bulk-async overload has inverted linear/nonlinear branches).
        var result = Checker.GetTensionAnalysisResult(Force(action));
        return DescribeStress(result, set);
    }
    public CheckerStressState LimitState(FailureDomain.FailureDomainPoint point) => LimitState(point.NRd, point.MxRd, point.MyRd, point.StrainPlane);
    public CheckerStressState LimitState(ResistancePoint point) => LimitState(point.NRd, point.MxRd, point.MyRd, point.StrainPlane);
    private CheckerStressState LimitState(double nRd, double mxRd, double myRd, StrainPlane plane)
    {
        var force = new ResultBeamForces(nRd, 0, 0, 0, mxRd, myRd, Local);
        // Sample the saved limit plane. Never solve equilibrium again at Rd.
        var result = new StressAnalysisResult(Section, force, plane, Checker.SectionSolver, standard, false, null, null, 0, null);
        return DescribeStress(result, "LIMITE");
    }
    private CheckerStressState DescribeStress(StressAnalysisResult result, string set)
    {
        if (result?.StrainPlane is null || Section.ConcreteShape.GetPoints2d().Any(p => !double.IsFinite(result.StrainPlane.GetStrain(p))))
            throw new ArgumentException("Checker: analisi tensionale non convergente.");
        bool linear = result.LinearElasticAnalysis; double phi = Checker.SectionCheckerOptions.PsiCoefficientRebar, phiT = Checker.SectionCheckerOptions.PsiCoefficientTendon;
        var bars = linear ? result.GetRebarsTension(phi, phiT) : result.GetRebarsTension();
        var concrete = linear ? result.GetConcreteVerticesTension(phi) : result.GetConcreteVerticesTension();
        var fibers = Geometry.Fibers.Select(f => linear ? result.GetConcreteTension(phi, new Point2d(f.X, f.Y)) : result.GetConcreteTension(new Point2d(f.X, f.Y))).ToArray();
        if (bars.Any(b => !double.IsFinite(b.tension)) || concrete.Any(c => !double.IsFinite(c.tension)) || fibers.Any(v => !double.IsFinite(v))) throw new ArgumentException("Checker: tensioni non finite.");
        // Tasso, stato e limiti SLE dall'adattatore SLE (refactoring F2.7b, commit A4; rilievi M4, M5): la costruzione dello stato (tensioni,
        // deformazioni, raster) resta qui, invariata.
        var limits = ConcreteServiceabilityAdapter.StressLimits(result, set, standard, Section, phi, phiT, compressionReduction);
        var material = (ConcreteMaterialEuropeanCommon)Section.ConcreteMaterial;
        var response = SectionResponse.From(result.CalculateStrainPlaneResult(linear, linear ? phi : 0, linear ? phiT : 0));
        return new(concrete.Min(c => c.tension), bars.Max(b => Math.Abs(b.tension)), bars.Select(b => b.tension).ToArray(), fibers, limits.Ratio, limits.Status, result)
        {
            Response = response,
            // Origine degli assi delle forze, punto di riferimento dell'integrazione del solutore: ε0 del M–χ (SLU-4).
            StrainReference = Checker.SectionCheckerOptions.ForceReferenceCoordinateSystem.Origin,
            ConcreteCompressionStrength = Math.Abs(material.CalculateFcd(standard)), ConcreteTensionStrength = Math.Abs(material.CalculateFctd(standard)),
            // Plot normalization is a material strength ratio, not a SLE compliance check.
            BarStrengths = Section.Rebars.Select(r => Math.Abs(r.RebarMaterial.CalculateFyd(standard))).ToArray(),
            FiberStrains = Geometry.Fibers.Select(f => (linear ? result.GetVerticeStrain(new Point2d(f.X, f.Y), phi) : result.GetVerticeStrain(new Point2d(f.X, f.Y))) * 1000).ToArray(),
            ConcreteStressLimit = limits.ConcreteStressLimit,
            SteelStressLimit = limits.SteelStressLimit,
            ConcreteVertices = Geometry.Outline.Select((p, i) => new StressPoint("C" + (i + 1), p[0], p[1], linear ? result.GetConcreteTension(phi, new Point2d(p[0], p[1])) : result.GetConcreteTension(new Point2d(p[0], p[1])), (linear ? result.GetVerticeStrain(new Point2d(p[0], p[1]), phi) : result.GetVerticeStrain(new Point2d(p[0], p[1]))) * 1000)).ToArray(),
            BarStrains = Section.Rebars.Select(b => (linear ? result.GetRebarStrain(b, b.EpsilonP != 0 ? phiT : phi) : result.GetRebarStrain(b)) * 1000).ToArray()
            ,RasterFactory = new(() => StressRaster.Sample(Geometry, p => linear ? result.GetConcreteTension(phi, p) : result.GetConcreteTension(p), p => (linear ? result.GetVerticeStrain(p, phi) : result.GetVerticeStrain(p)) * 1000))
        };
    }
}
public sealed record CheckerStressState(double sigma_cls, double sigma_acciaio, double[] tensioni_barre, double[] FiberStresses,
    double? Ratio, string Status, [property: System.Text.Json.Serialization.JsonIgnore] StressAnalysisResult Native)
{
    public SectionResponse? Response { get; init; }
    /// <summary>Punto (assi della sezione, mm) dove il M–χ legge ε0 di ogni stato: l'origine degli assi delle forze del solutore, cioè degli
    /// assi locali (SLU-4 del 15/10/2026). Il piano del punto limite viene dal dominio e ha il riferimento nel vertice pivot. Non serializzato.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public Point2d? StrainReference { get; init; }
    public double ConcreteCompressionStrength { get; init; }
    public double ConcreteTensionStrength { get; init; }
    public double? ConcreteStressLimit { get; init; }
    public double SteelStressLimit { get; init; }
    public StressPoint[] ConcreteVertices { get; init; } = [];
    public double[] BarStrains { get; init; } = [];
    internal Lazy<StressRaster>? RasterFactory { get; init; }
    [System.Text.Json.Serialization.JsonIgnore] public StressRaster? Raster => RasterFactory?.Value;
    [System.Text.Json.Serialization.JsonIgnore] public bool IsRasterCreated => RasterFactory?.IsValueCreated == true;
    public double[] BarStrengths { get; init; } = [];
    public double[] FiberStrains { get; init; } = [];
}
public sealed record StressPoint(string Id, double X, double Y, double Stress, double Strain);
public sealed record StressRaster(int Size, double XMin, double YMin, double XMax, double YMax, double[] Stresses, double[] Strains)
{
    public static StressRaster Sample(SezioneCA geometry, Func<Point2d, double> stress, Func<Point2d, double> strain)
    {
        const int n = 96;
        double x0 = geometry.Outline.Min(p => p[0]), x1 = geometry.Outline.Max(p => p[0]), y0 = geometry.Outline.Min(p => p[1]), y1 = geometry.Outline.Max(p => p[1]);
        var stresses = new double[n * n]; var strains = new double[n * n];
        for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
        { var p = new Point2d(x0 + (i + .5) * (x1 - x0) / n, y1 - (j + .5) * (y1 - y0) / n); stresses[j * n + i] = stress(p); strains[j * n + i] = strain(p); }
        return new(n, x0, y0, x1, y1, stresses, strains);
    }
}

public sealed record SectionResponse(double? CMin, double? CMax, double? SMin, double? SMax, double? PMin, double? PMax,
    double? EcMin, double? EcMax, double? EsMin, double? EsMax, double? EpMin, double? EpMax, double? UsefulDepth, double? NeutralDistance, double? NeutralAngle)
{
    public static SectionResponse From(GPC.Checker.Results.ResultType.StrainPlaneResult r)
    {
        static double? Clean(double value, double scale = 1) => double.IsFinite(value) && Math.Abs(value) < 1e100 ? value * scale : null;
        return new(Clean(r.SigmaCMin), Clean(r.SigmaCMax), Clean(r.SigmaSMin), Clean(r.SigmaSMax), Clean(r.SigmaPMin), Clean(r.SigmaPMax),
            Clean(r.EpsilonCMin, 1000), Clean(r.EpsilonCMax, 1000), Clean(r.EpsilonSMin, 1000), Clean(r.EpsilonSMax, 1000), Clean(r.EpsilonPMin, 1000), Clean(r.EpsilonPMax, 1000),
            Clean(r.NetHeight), Clean(r.NeutralAxisDistance), Clean(r.NeutralAxisAngle));
    }
}

public sealed class CheckerDomain3D(CheckerSection section, FailureDomainResult native)
{
    public CheckerSection Section { get; } = section;
    public FailureDomainResult Native { get; } = native;
    private readonly Dictionary<(bool Linear, int Divisions), SectionDomainMesh> displayMeshes = new();
    public SectionDomainMesh Mesh => DisplayMesh(Section.Options);
    public bool IsMeshCreated => displayMeshes.Count > 0;
    public SectionDomainMesh DisplayMesh(JsonObject options)
    {
        var key = (options.S("interpolazione") == "Lineare", SectionWorkspace.Subdivisions(options.S("suddivisioni_n", "50"), "Suddivisioni N (mesh)", 5, 200));
        lock (displayMeshes)
        {
            if (!displayMeshes.TryGetValue(key, out var mesh))
            {
                // A separate native domain keeps display interpolation out of resistance searches.
                var display = new FailureDomain(Native.Domain.DomainPoints, Native.Domain.FailureDomainAnalysisTypes)
                { ForceLinearInterpolation = key.Item1, AxialForceSubdivision = key.Item2 };
                mesh = new(Section.Checker.SectionCheckerOptions.FailureDomainType.ToString(), display.GetMesh(out _));
                displayMeshes[key] = mesh;
            }
            return mesh;
        }
    }
    public void ConfigureVerification(JsonObject options)
    {
        Native.FailureAnalysisType = CheckerSection.Criterion(options.S("criterio", "N costante"));
        Section.Checker.SetDomainPointStrategy(options.S("strategia", "Iterativo") == "Intersezione" ? SectionSolver.DomainPointStrategyTypes.Intersection : SectionSolver.DomainPointStrategyTypes.Iterative);
        foreach (string key in new[] { "criterio", "strategia" }) Section.Options[key] = options[key]?.DeepClone();
    }
    public DomainCheck Check(ActionPoint action)
    {
        var force = Section.Force(action);
        if (action.Length == 0 && !Section.Section.Rebars.Any(r => r.EpsilonP != 0)) return new(0, null, "Azione nulla");
        // Punto dall'adattatore SLU, motore della libreria (revisione di architettura R2, registro F2-17).
        return Describe(ConcreteResistanceAdapter.Point(Section, force));
    }
    /// <summary>Ordered batch on a configured domain; do not reconfigure it while this call is running.</summary>
    public DomainCheck[] CheckMany(IReadOnlyList<ActionPoint> actions, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        // Exact value equality, not rounding: different demands must never share a check.
        var indices = new Dictionary<ActionPoint, int>();
        var unique = new List<ActionPoint>(); var map = new int[actions.Count];
        for (int i = 0; i < actions.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            if (!indices.TryGetValue(actions[i], out int index))
            { index = unique.Count; indices.Add(actions[i], index); unique.Add(actions[i]); }
            map[i] = index;
        }
        var checks = new DomainCheck[unique.Count];
        bool prestressed = Section.Section.Rebars.Any(r => r.EpsilonP != 0);
        // The points come from the ULS adapter, library engine (architecture review R2, register F2-17). Small chunks bound temporary memory
        // and cancellation latency, without any UI refresh per force.
        const int batchSize = 64;
        for (int start = 0; start < unique.Count; start += batchSize)
        {
            token.ThrowIfCancellationRequested();
            var pending = new List<int>(); var forces = new List<ResultBeamForces>();
            for (int i = start; i < Math.Min(start + batchSize, unique.Count); i++)
            {
                var action = unique[i];
                if (!double.IsFinite(action.N) || !double.IsFinite(action.Mx) || !double.IsFinite(action.My))
                { checks[i] = new(null, null, "Checker: azione non finita"); continue; }
                if (action.Length == 0 && !prestressed) { checks[i] = new(0, null, "Azione nulla"); continue; }
                pending.Add(i); forces.Add(Section.Force(action, i + 1));
            }
            if (pending.Count == 0) continue;
            ResistanceOutcome[] points;
            try { points = ConcreteResistanceAdapter.Points(Section, forces, token); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One native failure must not discard every other combination.
                foreach (int i in pending)
                {
                    token.ThrowIfCancellationRequested();
                    try { checks[i] = Check(unique[i]); }
                    catch (Exception error) when (error is not OperationCanceledException) { checks[i] = new(null, null, "Checker: " + error.Message); }
                }
                continue;
            }
            for (int i = 0; i < pending.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                try { checks[pending[i]] = Describe(points[i]); }
                catch (Exception ex) when (ex is not OperationCanceledException) { checks[pending[i]] = new(null, null, "Checker: " + ex.Message); }
            }
        }
        token.ThrowIfCancellationRequested();
        return map.Select(i => checks[i]).ToArray();
    }
    private DomainCheck Describe(ResistanceOutcome outcome)
    {
        if (outcome.Point is not { } p) return new(null, null, "Checker: " + (outcome.Rejection ?? "punto resistente non trovato"));
        double ratio = p.WorkingRatio(Native.FailureAnalysisType);
        // Esiti tipizzati della libreria a N assegnato (SLU-2 del 15/10/2026): N fuori dal campo [NRd,c; NRd,t] e momento nullo con N costante.
        return Outcome(ratio, CheckerSection.Point(p), ConcreteLibraryMapping.ResistanceOutcomeStatus(p, ratio))
            with { Response = Section.Describe(p), LimitState = new(() => Section.LimitState(p)), AxialForceOutsideRange = ConcreteLibraryMapping.AxialForceRangeKn(p) };
    }
    internal static DomainCheck Outcome(double ratio, ActionPoint p, string? typedStatus = null)
        => !double.IsFinite(ratio) || ratio < 0 || !double.IsFinite(p.N + p.Mx + p.My) ? new(null, null, "Checker: resistenza non determinata")
         : new(ratio, p, typedStatus ?? (ratio <= 1 ? "Entro il dominio" : "Fuori dominio"));
}
public sealed class CheckerDomain2D
{
    public CheckerSection Section { get; }
    public FailureDomainResult2d Native { get; }
    public bool NM { get; }
    public double Theta { get; }
    public double Axial { get; }
    public List<DomainSegment> Segments { get; } = [];
    private readonly double localTheta;
    public CheckerDomain2D(CheckerSection section, FailureDomainResult2d native, bool nm, double theta, double axial)
    {
        Section = section; Native = native; NM = nm; Theta = theta; Axial = axial;
        var direction = section.Force(new(0, Math.Cos(theta), Math.Sin(theta)));
        localTheta = Math.Atan2(direction.M2, direction.M1);
        var points = native.Domain.DomainPoints.Select(CheckerSection.Point).ToArray();
        for (int i = 0; i < points.Length; i++) Segments.Add(new(points[i], points[(i + 1) % points.Length]));
    }
    public ActionPoint LocalAction(ActionPoint action) => CheckerSection.Point(Section.Force(action));
    public double[] Project(ActionPoint local) => NM ? [SignedMoment(local), local.N] : [local.Mx, local.My];
    /// <summary>
    /// Punto del grafico dell'azione locale <paramref name="local"/>: quello che <see cref="Check"/> verifica. Nel piano N–M, con «proietta» e
    /// un'azione fuori dal meridiano di θ, la sua proiezione Mθ = Mx cos θ + My sin θ con N invariato (seguito di SLU-1, 16/10/2026: prima il grafico
    /// disegnava sign(Mx)·√(Mx² + My²), un punto non verificato e, con Mx di segno opposto a Mθ, dall'altra parte del grafico). Negli altri casi
    /// come <see cref="Project"/>: un'azione nel piano (entro la tolleranza di <see cref="Check"/>) resta dov'era, bit per bit; nel piano Mx–My
    /// la proiezione cambia solo N, che il grafico non mostra.
    /// </summary>
    public double[] ProjectAction(ActionPoint local) => Project(NM && !InPlane(local) && Projects ? OnMeridian(local) : local);
    private bool Projects => Section.Options.S("proietta") == "Sì";
    private bool InPlane(ActionPoint local) => NM
        ? Math.Abs(-local.Mx * Math.Sin(localTheta) + local.My * Math.Cos(localTheta)) < 1e-6 * Math.Max(1, double.Hypot(local.Mx, local.My))
        : Math.Abs(local.N - Axial) < 1e-6;
    /// <summary>Componente del momento sul meridiano di θ, Mθ = Mx cos θ + My sin θ (assi locali).</summary>
    private double MeridianMoment(ActionPoint local) => local.Mx * Math.Cos(localTheta) + local.My * Math.Sin(localTheta);
    /// <summary>Azione locale proiettata sul meridiano di θ: N invariato, momento Mθ nella direzione θ.</summary>
    private ActionPoint OnMeridian(ActionPoint local)
    {
        double m = MeridianMoment(local);
        return new(local.N, m * Math.Cos(localTheta), m * Math.Sin(localTheta));
    }
    private double SignedMoment(ActionPoint p)
    {
        // FailureDomain2d.DomainPoints2dAssociation (SLU-1 del 15/10/2026) prende il segno dalla proiezione sull'asse del meridiano, MomentAxis in
        // (−90°; 90°]: per i punti del meridiano della direzione del momento, l'unico usato da Domain2D, coincide con il segno di Mx (di My a 90°).
        double sign = Math.Abs(Math.Cos(localTheta)) > 1e-8 ? Math.Sign(p.Mx) : Math.Sign(p.My);
        return sign * double.Hypot(p.Mx, p.My);
    }
    public DomainCheck Check(ActionPoint action)
    {
        var force = Section.Force(action); var local = CheckerSection.Point(force);
        bool inPlane = InPlane(local);
        bool project = Projects;
        if (!inPlane && !project) return new(null, null, "Fuori dal piano selezionato");
        if (NM)
        {
            double m = MeridianMoment(local);
            // The library expects N and the signed 2D moment in M1, as in CheckerUI.Force2d.
            double sign = Math.Abs(Math.Cos(localTheta)) > 1e-8 ? Math.Sign(Math.Cos(localTheta)) : Math.Sign(Math.Sin(localTheta));
            force = new ResultBeamForces(local.N * 1000, 0, 0, 0, m * sign * 1e6, 0, Section.Local, 1);
            // Lo stesso punto che il grafico disegna con ProjectAction (azione fuori piano con «proietta»).
            local = OnMeridian(local);
        }
        else { force = new ResultBeamForces(Axial * 1000, 0, 0, 0, force.M1, force.M2, Section.Local, 1); local = local with { N = Axial }; }
        Native.ClearForces(); var p = Native.AddForce(force);
        if (p?.FailureDomainPoint is null) return new(null, null, "Checker: punto resistente non trovato");
        var point = p.FailureDomainPoint;
        // Use the native working-ratio calculation on collinear actions in section coordinates.
        var comparable = new ResultBeamForces(local.N * 1000, 0, 0, 0, local.Mx * 1e6, local.My * 1e6, Section.Local);
        var check = CheckerDomain3D.Outcome(point.CalculateWorkingRatio(NM ? SectionSolver.FailureAnalysisTypes.ConstantEccentricity : SectionSolver.FailureAnalysisTypes.ConstantN, comparable, 1e6, 1000), CheckerSection.Point(point));
        check = check with { Response = Section.Describe(point), LimitState = new(() => Section.LimitState(point)) };
        return !inPlane && project ? check with { Status = check.Status + " · azione proiettata" } : check;
    }
}
