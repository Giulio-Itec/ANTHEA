using System.Text.Json.Nodes;
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

namespace X.Core;

/// <summary>Unit/DTO adapter only. All section equilibrium and domain searches belong to the shipped Checker DLL.</summary>
public sealed class CheckerSection
{
    public ReinforcedConcreteSection Section { get; }
    public CoordinateSystem Local { get; }
    public CoordinateSystem ForceAxes { get; }
    public SectionCheckerModelCode2010 Checker { get; }
    public SezioneCA Geometry { get; } // Existing parametric geometry only; never used for resistance/stress.
    public JsonObject Options { get; }
    private readonly double compressionReduction;
    private readonly StandardModelCode2010 standard;
    public static readonly string[] Criteria = ["N costante", "Eccentricità costante", "Mx–My costanti", "N e Mx costanti", "N e My costanti"];
    public static SectionSolver.FailureAnalysisTypes Criterion(string text) => text switch
    {
        "N costante" => SectionSolver.FailureAnalysisTypes.ConstantN,
        "Eccentricità costante" => SectionSolver.FailureAnalysisTypes.ConstantEccentricity,
        "Mx–My costanti" => SectionSolver.FailureAnalysisTypes.ConstantMxMy,
        "N e Mx costanti" => SectionSolver.FailureAnalysisTypes.ConstantNMx,
        "N e My costanti" => SectionSolver.FailureAnalysisTypes.ConstantNMy,
        _ => throw new ArgumentException("Criterio Checker non riconosciuto.")
    };
    public CheckerSection(JsonObject input, JsonObject workspace, JsonObject options, string state = "SLU")
    {
        if (workspace.S("normativa", "NTC 2018") != "NTC 2018") throw new ArgumentException("Questo collegamento è validato per NTC 2018: selezionare NTC 2018 nel pannello di controllo.");
        Options = (JsonObject)options.DeepClone(); Geometry = new SezioneCA(input);
        compressionReduction = input.S("gettato_sottile", "No") == "Sì" ? .8 : 1;
        var concrete = new ConcreteMaterialEN1992(input.S("classe_cls"), input.Required("fck_mpa", strict: true), ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
        var steel = new SteelMaterial("Armatura", input.Required("steel_modulus_mpa", strict: true), input.Required("fyk_mpa", strict: true), input.Required("fyk_mpa", strict: true), steelType: SteelMaterial.SteelTypes.Rebar);
        Section = new ReinforcedConcreteSection(new Shape2d(new Polygon2d(Geometry.Outline.Select(p => new Point2d(p[0], p[1])).ToArray())), concrete);
        Section.AddRebars(Geometry.Bars.Select(b => new ReinforcedConcreteRebar(new RebarSectionCircular(b.Diametro, steel), new Point2d(b.X, b.Y))).ToArray());
        foreach (var t in workspace.Array("trefoli"))
        {
            double area = t!.Required("area", strict: true), ep = t.Required("Ep", strict: true), fpy = t.Required("fpyk", strict: true), fpu = t.Required("fpk", strict: true);
            double strain = t.Required("eps_u", strict: true) / 1000, sigma = t.Required("sigma0");
            if (fpu < fpy || strain <= fpy / ep || sigma >= fpu) throw new ArgumentException("Trefolo: controllare fpk, fpyk, εpu e σp0.");
            var material = new SteelMaterial(t.S("id"), ep, fpy, fpu, strain, SteelMaterial.StressStrainCurveType.ElasticHardening, SteelMaterial.SteelTypes.Tendon);
            Section.AddRebars([new ReinforcedConcreteRebar(new RebarSectionCircular(Math.Sqrt(4 * area / Math.PI), material), new Point2d(SectionWorkspace.Number(t.S("x"), "x trefolo"), SectionWorkspace.Number(t.S("y"), "y trefolo")), sigma)]);
        }
        Local = new CoordinateSystem(Section.Centroid, new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
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
        standard = new StandardNTC2018Concrete();
        standard.AlphaCC = input.Required("alpha_cc", strict: true) * compressionReduction; standard.GammaC = input.Required("gamma_c", strict: true); standard.GammaS = input.Required("gamma_s", strict: true);
        bool tensile = options.S("trazione_cls", "No") == "Sì";
        int subdivisions = SectionWorkspace.Subdivisions(options.S("angoli", "32"), "Direzioni angolari", 4, 360);
        var settings = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(Local, Criterion(options.S("criterio", "N costante")),
            state == "SLV" ? SectionSolver.FailureDomainTypes.Elastic : SectionSolver.FailureDomainTypes.Plastic,
            options.S("modello", "Non lineare") == "Lineare" ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear,
            Nonnegative("phi"), Nonnegative("phi_trefoli"), tensile, subdivisions);
        Checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(Section, null, null), settings, standard, tensile);
        Checker.SetDomainPointStrategy(options.S("strategia", "Iterativo") == "Intersezione" ? SectionSolver.DomainPointStrategyTypes.Intersection : SectionSolver.DomainPointStrategyTypes.Iterative);
    }
    private double N(string key) => SectionWorkspace.Number(Options.S(key, "0"), key);
    internal SectionResponse? Describe(FailureDomain.FailureDomainPoint point)
    {
        // Reporting only: use the same native strain plane that generated the resistance.
        try
        {
            var force = new ResultBeamForces(point.NRd, 0, 0, 0, point.MxRd, point.MyRd, Local);
            return SectionResponse.From(point.StrainPlane.CalculateStrainPlaneResult(Section, force, Checker.SectionSolver, standard));
        }
        catch (Exception) { return null; } // A missing report must not alter the native resistance result.
    }
    private double Nonnegative(string key) { double v = N(key); if (v < 0) throw new ArgumentException(key + " deve essere non negativo."); return v; }
    public ResultBeamForces Force(ActionPoint p, int id = 1) => new ResultBeamForces(p.N * 1000, 0, 0, 0, p.Mx * 1e6, p.My * 1e6, ForceAxes, id).ToCoordinateSystemWithEccentricity(Local);
    public static ActionPoint Point(ResultBeamForces p) => new(p.N / 1000, p.M1 / 1e6, p.M2 / 1e6);
    public static ActionPoint Point(FailureDomain.FailureDomainPoint p) => new(p.NRd / 1000, p.MxRd / 1e6, p.MyRd / 1e6);
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
        FailureDomainResult2d result;
        // CheckerUI CalcDomain2d: direct meridian for the local principal directions; otherwise library section of 3D domain.
        if (nm && Math.Abs(Math.Sin(2 * theta)) < 1e-10 && Options.S("assi", "Locali") == "Locali")
            result = Checker.SectionCheckerOptions.FailureDomainType == SectionSolver.FailureDomainTypes.Plastic
                ? Checker.SectionSolver.GetPlasticFailureDomainResult2d(theta) : Checker.SectionSolver.GetElasticFailureDomainResult2d(theta);
        else
        {
            var d = Checker.GetFailureDomainResult();
            result = nm ? d.CalculateDomainConstantMomentsRatio(Force(new(0, Math.Cos(theta), Math.Sin(theta))))
                : d.CalculateDomainConstantAxialForce(Force(new(axial, 0, 0)));
        }
        token.ThrowIfCancellationRequested();
        if (result?.Domain?.DomainPoints is not { Length: > 2 } points || points.Any(p => p is null)) throw new ArgumentException("Checker: sezione del dominio assente; controllare N e direzione.");
        result.ClearForces();
        return new(this, result, nm, theta, axial);
    }
    public CheckerStressState Stress(ActionPoint action, string set)
    {
        // Correct public single-force overload (the old bulk-async overload has inverted linear/nonlinear branches).
        var result = Checker.GetTensionAnalysisResult(Force(action));
        if (result?.StrainPlane is null || Section.Shape.GetPoints2d().Any(p => !double.IsFinite(result.StrainPlane.GetStrain(p))))
            throw new ArgumentException("Checker: analisi tensionale non convergente.");
        bool linear = result.LinearElasticAnalysis; double phi = Checker.SectionCheckerOptions.PsiCoefficientRebar, phiT = Checker.SectionCheckerOptions.PsiCoefficientTendon;
        var bars = linear ? result.GetRebarsTension(phi, phiT) : result.GetRebarsTension();
        var concrete = linear ? result.GetConcreteVerticesTension(phi) : result.GetConcreteVerticesTension();
        var fibers = Geometry.Fibers.Select(f => linear ? result.GetConcreteTension(phi, new Point2d(f.X, f.Y)) : result.GetConcreteTension(new Point2d(f.X, f.Y))).ToArray();
        if (bars.Any(b => !double.IsFinite(b.tension)) || concrete.Any(c => !double.IsFinite(c.tension)) || fibers.Any(v => !double.IsFinite(v))) throw new ArgumentException("Checker: tensioni non finite.");
        double? ratio = null;
        if (set == "SLE")
        {
            var c = linear ? result.ConcreteServiceabilityCharacteristicCheck(phi) : result.ConcreteServiceabilityCharacteristicCheck();
            var s = linear ? result.SteelServiceabilityCharacteristicCheck(phi, phiT) : result.SteelServiceabilityCharacteristicCheck();
            ratio = Math.Max(c.Where(p => p.tension < 0).Select(p => p.workingRatio / compressionReduction).DefaultIfEmpty(0).Max(), s.Max(p => p.workingRatio));
        }
        if (set == "SLE_QP") ratio = (linear ? result.ConcreteServiceabilityQuasiPermanentCheck(phi) : result.ConcreteServiceabilityQuasiPermanentCheck()).Where(p => p.tension < 0).Select(p => p.workingRatio / compressionReduction).DefaultIfEmpty(0).Max();
        if (ratio.HasValue && !double.IsFinite(ratio.Value)) throw new ArgumentException("Checker: tasso tensionale non valido.");
        var material = (ConcreteMaterialEuropeanCommon)Section.ConcreteMaterial;
        var response = SectionResponse.From(result.CalculateStrainPlaneResult(linear, linear ? phi : 0, linear ? phiT : 0));
        return new(concrete.Min(c => c.tension), bars.Max(b => Math.Abs(b.tension)), bars.Select(b => b.tension).ToArray(), fibers, ratio,
            set == "SLE_FREQ" ? "Tensioni calcolate · limite non previsto" : ratio <= 1 ? "Entro limiti NTC 2018" : "Oltre limiti NTC 2018", result)
        {
            Response = response,
            ConcreteCompressionStrength = Math.Abs(material.CalculateFcd(standard)), ConcreteTensionStrength = Math.Abs(material.CalculateFctd(standard)),
            // Plot normalization is a material strength ratio, not a SLE compliance check.
            BarStrengths = Section.Rebars.Select(r => Math.Abs(r.RebarMaterial.CalculateFyd(standard))).ToArray(),
            FiberStrains = Geometry.Fibers.Select(f => result.StrainPlane.GetStrain(new Point2d(f.X, f.Y)) * 1000).ToArray(),
            ConcreteStressLimit = set == "SLE" ? .6 * Math.Abs(material.Fck) * compressionReduction : set == "SLE_QP" ? .45 * Math.Abs(material.Fck) * compressionReduction : null
        };
    }
}
public sealed record CheckerStressState(double sigma_cls, double sigma_acciaio, double[] tensioni_barre, double[] FiberStresses,
    double? Ratio, string Status, [property: System.Text.Json.Serialization.JsonIgnore] StressAnalysisResult Native)
{
    public SectionResponse? Response { get; init; }
    public double ConcreteCompressionStrength { get; init; }
    public double ConcreteTensionStrength { get; init; }
    public double? ConcreteStressLimit { get; init; }
    public double[] BarStrengths { get; init; } = [];
    public double[] FiberStrains { get; init; } = [];
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
    public SectionDomainMesh Mesh { get; } = new(section.Checker.SectionCheckerOptions.FailureDomainType.ToString(), native.Domain.GetMesh(out _));
    public DomainCheck Check(ActionPoint action)
    {
        var force = Section.Force(action);
        if (action.Length == 0 && !Section.Section.Rebars.Any(r => r.EpsilonP != 0)) return new(0, null, "Azione nulla");
        var p = Native.CalculateForce(force);
        if (p is null) return new(null, null, "Checker: punto resistente non trovato");
        return Outcome(p.CalculateWorkingRatio(Native.FailureAnalysisType, force, 1e6, 1000), CheckerSection.Point(p)) with { Response = Section.Describe(p) };
    }
    internal static DomainCheck Outcome(double ratio, ActionPoint p)
        => !double.IsFinite(ratio) || ratio < 0 || !double.IsFinite(p.N + p.Mx + p.My) ? new(null, null, "Checker: resistenza non determinata")
         : new(ratio, p, ratio <= 1 ? "Entro il dominio" : "Fuori dominio");
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
    private double SignedMoment(ActionPoint p)
    {
        // FailureDomain2d.DomainPoints2dAssociation defines sign by Mx (or My for 90 degrees).
        double sign = Math.Abs(Math.Cos(localTheta)) > 1e-8 ? Math.Sign(p.Mx) : Math.Sign(p.My);
        return sign * double.Hypot(p.Mx, p.My);
    }
    public DomainCheck Check(ActionPoint action)
    {
        var force = Section.Force(action); var local = CheckerSection.Point(force);
        bool inPlane = NM ? Math.Abs(-local.Mx * Math.Sin(localTheta) + local.My * Math.Cos(localTheta)) < 1e-6 * Math.Max(1, double.Hypot(local.Mx, local.My)) : Math.Abs(local.N - Axial) < 1e-6;
        bool project = Section.Options.S("proietta") == "Sì";
        if (!inPlane && !project) return new(null, null, "Fuori dal piano selezionato");
        if (NM)
        {
            double m = local.Mx * Math.Cos(localTheta) + local.My * Math.Sin(localTheta);
            // The library expects N and the signed 2D moment in M1, as in CheckerUI.Force2d.
            double sign = Math.Abs(Math.Cos(localTheta)) > 1e-8 ? Math.Sign(Math.Cos(localTheta)) : Math.Sign(Math.Sin(localTheta));
            force = new ResultBeamForces(local.N * 1000, 0, 0, 0, m * sign * 1e6, 0, Section.Local, 1);
            local = new(local.N, m * Math.Cos(localTheta), m * Math.Sin(localTheta));
        }
        else { force = new ResultBeamForces(Axial * 1000, 0, 0, 0, force.M1, force.M2, Section.Local, 1); local = local with { N = Axial }; }
        Native.ClearForces(); var p = Native.AddForce(force);
        if (p?.FailureDomainPoint is null) return new(null, null, "Checker: punto resistente non trovato");
        var point = p.FailureDomainPoint;
        // Use the native working-ratio calculation on collinear actions in section coordinates.
        var comparable = new ResultBeamForces(local.N * 1000, 0, 0, 0, local.Mx * 1e6, local.My * 1e6, Section.Local);
        var check = CheckerDomain3D.Outcome(point.CalculateWorkingRatio(NM ? SectionSolver.FailureAnalysisTypes.ConstantEccentricity : SectionSolver.FailureAnalysisTypes.ConstantN, comparable, 1e6, 1000), CheckerSection.Point(point));
        check = check with { Response = Section.Describe(point) };
        return !inPlane && project ? check with { Status = check.Status + " · azione proiettata" } : check;
    }
}
