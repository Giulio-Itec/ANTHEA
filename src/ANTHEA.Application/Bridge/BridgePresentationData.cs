// Explicit owned projections of the native results used by the bridge views.
// Engineering methods remain in the original libraries; no serialization/reflection at runtime.
using System.Diagnostics.CodeAnalysis;
using Native = GPC.Checkers.CompositeBridge;
using History = GPC.Checkers.CompositeBridge.History;
namespace Anthea.Application.Bridge;

public sealed class BridgeSectionProperties
{
    public double Ix { get; }
    internal BridgeSectionProperties(global::GPC.Checkers.CompositeBridge.BridgeSectionProperties source, BridgeProjection map)
    {
        Ix = source.Ix;
    }
}

public sealed class WebShearResistance
{
    public double KTau { get; }
    public double TauCritical { get; }
    public double Slenderness { get; }
    public double Chi { get; }
    public double PlasticResistance { get; }
    public double BucklingResistance { get; }
    public double Resistance { get; }
    internal WebShearResistance(global::GPC.Checkers.CompositeBridge.WebShearResistance source, BridgeProjection map)
    {
        KTau = source.KTau;
        TauCritical = source.TauCritical;
        Slenderness = source.Slenderness;
        Chi = source.Chi;
        PlasticResistance = source.PlasticResistance;
        BucklingResistance = source.BucklingResistance;
        Resistance = source.Resistance;
    }
}

public sealed class TransverseStiffenerResistance
{
    public double Area { get; }
    public double Inertia { get; }
    public double AxialForce { get; }
    public double Lambda { get; }
    public double Chi { get; }
    public double InitialDeflection { get; }
    internal TransverseStiffenerResistance(global::GPC.Checkers.CompositeBridge.TransverseStiffenerResistance source, BridgeProjection map)
    {
        Area = source.Area;
        Inertia = source.Inertia;
        AxialForce = source.AxialForce;
        Lambda = source.Lambda;
        Chi = source.Chi;
        InitialDeflection = source.InitialDeflection;
    }
}

public sealed class HeadedStudResistance
{
    public double Alpha { get; }
    public double UsedFu { get; }
    public double SteelResistance { get; }
    public double ConcreteResistance { get; }
    internal HeadedStudResistance(global::GPC.Checkers.CompositeBridge.HeadedStudResistance source, BridgeProjection map)
    {
        Alpha = source.Alpha;
        UsedFu = source.UsedFu;
        SteelResistance = source.SteelResistance;
        ConcreteResistance = source.ConcreteResistance;
    }
}

public sealed class BridgeDetailValue
{
    public string Name { get; }
    public double Value { get; }
    public string Unit { get; }
    internal BridgeDetailValue(global::GPC.Checkers.CompositeBridge.BridgeDetailValue source, BridgeProjection map)
    {
        Name = source.Name;
        Value = source.Value;
        Unit = source.Unit;
    }
}

public sealed class BridgeLocalCheck
{
    public string Name { get; }
    public double Demand { get; }
    public double Resistance { get; }
    public string Unit { get; }
    public double? Ratio { get; }
    public string Note { get; }
    public string Status { get; }
    internal BridgeLocalCheck(global::GPC.Checkers.CompositeBridge.BridgeLocalCheck source, BridgeProjection map)
    {
        Name = source.Name;
        Demand = source.Demand;
        Resistance = source.Resistance;
        Unit = source.Unit;
        Ratio = source.Ratio;
        Note = source.Note;
        Status = source.Status;
    }
}

public sealed class BridgeShearFlow
{
    public string Phase { get; }
    public string Kind { get; }
    public double V { get; }
    public double StaticMoment { get; }
    public double Inertia { get; }
    public double Flow { get; }
    public double AdditionalFlow { get; }
    public string Basis { get; }
    internal BridgeShearFlow(global::GPC.Checkers.CompositeBridge.BridgeShearFlow source, BridgeProjection map)
    {
        Phase = source.Phase;
        Kind = source.Kind;
        V = source.V;
        StaticMoment = source.StaticMoment;
        Inertia = source.Inertia;
        Flow = source.Flow;
        AdditionalFlow = source.AdditionalFlow;
        Basis = source.Basis;
    }
}

public sealed class BridgeShearResult
{
    public double V { get; }
    public WebShearResistance Web { get; }
    public double TauAverage { get; }
    public double TauMaximum { get; }
    public bool UsesStiffeners { get; }
    public TransverseStiffenerResistance? Stiffener { get; }
    public List<BridgeLocalCheck> Checks { get; }
    public List<BridgeDetailValue>? Details { get; }
    public bool RigidEndPost { get; }
    internal BridgeShearResult(global::GPC.Checkers.CompositeBridge.BridgeShearResult source, BridgeProjection map)
    {
        V = source.V;
        Web = map.Read(source.Web)!;
        TauAverage = source.TauAverage;
        TauMaximum = source.TauMaximum;
        UsesStiffeners = source.UsesStiffeners;
        Stiffener = map.Read(source.Stiffener);
        Checks = source.Checks.Select(v => map.Read(v)!).ToList();
        Details = source.Details?.Select(v => map.Read(v)!).ToList();
        RigidEndPost = source.RigidEndPost;
    }
}

public sealed class BridgeStudResult
{
    public bool Enabled { get; }
    public HeadedStudResistance? Resistance { get; }
    public double Flow { get; }
    public double ForcePerStud { get; }
    public double ResistancePerStud { get; }
    public double ResistancePerLength { get; }
    public List<BridgeShearFlow> Contributions { get; }
    public List<BridgeLocalCheck> Checks { get; }
    public List<BridgeDetailValue>? Details { get; }
    internal BridgeStudResult(global::GPC.Checkers.CompositeBridge.BridgeStudResult source, BridgeProjection map)
    {
        Enabled = source.Enabled;
        Resistance = map.Read(source.Resistance);
        Flow = source.Flow;
        ForcePerStud = source.ForcePerStud;
        ResistancePerStud = source.ResistancePerStud;
        ResistancePerLength = source.ResistancePerLength;
        Contributions = source.Contributions.Select(v => map.Read(v)!).ToList();
        Checks = source.Checks.Select(v => map.Read(v)!).ToList();
        Details = source.Details?.Select(v => map.Read(v)!).ToList();
    }
}

public sealed class BridgeTorsionFlow
{
    public string Phase { get; }
    public string Kind { get; }
    public double TorqueKNm { get; }
    public double CellArea { get; }
    public double TorsionConstant { get; }
    public double Flow { get; }
    public bool Closed { get; }
    internal BridgeTorsionFlow(global::GPC.Checkers.CompositeBridge.BridgeTorsionFlow source, BridgeProjection map)
    {
        Phase = source.Phase;
        Kind = source.Kind;
        TorqueKNm = source.TorqueKNm;
        CellArea = source.CellArea;
        TorsionConstant = source.TorsionConstant;
        Flow = source.Flow;
        Closed = source.Closed;
    }
}

public sealed class BridgeDistortionResult
{
    public int Diaphragms { get; }
    public double Amplitude { get; }
    public double WarpingStressBottom { get; }
    public double BendingRatio { get; }
    public bool Included { get; }
    internal BridgeDistortionResult(global::GPC.Checkers.CompositeBridge.BridgeDistortionResult source, BridgeProjection map)
    {
        Diaphragms = source.Diaphragms;
        Amplitude = source.Amplitude;
        WarpingStressBottom = source.WarpingStressBottom;
        BendingRatio = source.BendingRatio;
        Included = source.Included;
    }
}

public sealed class BridgeTorsionResult
{
    public List<BridgeTorsionFlow> Flows { get; }
    public double WebFlow { get; }
    public double SlabFlow { get; }
    public double BracingFlow { get; }
    public BridgeDistortionResult? Distortion { get; }
    public List<BridgeLocalCheck> Checks { get; }
    public List<BridgeDetailValue> Details { get; }
    internal BridgeTorsionResult(global::GPC.Checkers.CompositeBridge.BridgeTorsionResult source, BridgeProjection map)
    {
        Flows = source.Flows.Select(v => map.Read(v)!).ToList();
        WebFlow = source.WebFlow;
        SlabFlow = source.SlabFlow;
        BracingFlow = source.BracingFlow;
        Distortion = map.Read(source.Distortion);
        Checks = source.Checks.Select(v => map.Read(v)!).ToList();
        Details = source.Details.Select(v => map.Read(v)!).ToList();
    }
}

public sealed class BridgeBar
{
    public double X { get; }
    public double Y { get; }
    public double Diameter { get; }
    public double Area { get; }
    internal BridgeBar(global::GPC.Checkers.CompositeBridge.BridgeBar source, BridgeProjection map)
    {
        X = source.X;
        Y = source.Y;
        Diameter = source.Diameter;
        Area = source.Area;
    }
}

public sealed class BridgeGeometry
{
    public int TopFlangeCount { get; }
    internal global::GPC.Checkers.CompositeBridge.BridgeGeometry Native { get; }
    public double Width { get; }
    public double SlabHeight { get; }
    public double WebHeight { get; }
    public double WebThickness { get; }
    public double TopWidth { get; }
    public double TopThickness { get; }
    public double Bottom1Width { get; }
    public double Bottom1Thickness { get; }
    public double Bottom2Width { get; }
    public double Bottom2Thickness { get; }
    public double BottomEquivalentWidth { get; }
    public double BottomEquivalentThickness { get; }
    public double Height { get; }
    public double BottomArea { get; }
    public double BottomRealCentroid { get; }
    public double BottomRealInertia { get; }
    public double SteelArea { get; }
    public double SteelInertia { get; }
    public double SteelCentroid { get; }
    public BridgeBar[] Bars { get; }
    public BridgeSteelSectionType SectionType { get; }
    public int WebCount { get; }
    public double WebAngle { get; }
    public double WebSpacingTop { get; }
    public double WebSpacingBottom { get; }
    public double PlateThickness { get; }
    public double PlateLength { get; }
    public double WebHorizontalThickness { get; }
    public double TopFlangeWidth { get; }
    public double BottomInternalWidth { get; }
    internal BridgeGeometry(global::GPC.Checkers.CompositeBridge.BridgeGeometry source, BridgeProjection map)
    {
        Native = source;
        TopFlangeCount = source.TopFlangeCount;
        Width = source.Width;
        SlabHeight = source.SlabHeight;
        WebHeight = source.WebHeight;
        WebThickness = source.WebThickness;
        TopWidth = source.TopWidth;
        TopThickness = source.TopThickness;
        Bottom1Width = source.Bottom1Width;
        Bottom1Thickness = source.Bottom1Thickness;
        Bottom2Width = source.Bottom2Width;
        Bottom2Thickness = source.Bottom2Thickness;
        BottomEquivalentWidth = source.BottomEquivalentWidth;
        BottomEquivalentThickness = source.BottomEquivalentThickness;
        Height = source.Height;
        BottomArea = source.BottomArea;
        BottomRealCentroid = source.BottomRealCentroid;
        BottomRealInertia = source.BottomRealInertia;
        SteelArea = source.SteelArea;
        SteelInertia = source.SteelInertia;
        SteelCentroid = source.SteelCentroid;
        Bars = source.Bars.Select(v => map.Read(v)!).ToArray();
        SectionType = (BridgeSteelSectionType)source.SectionType;
        WebCount = source.WebCount;
        WebAngle = source.WebAngle;
        WebSpacingTop = source.WebSpacingTop;
        WebSpacingBottom = source.WebSpacingBottom;
        PlateThickness = source.PlateThickness;
        PlateLength = source.PlateLength;
        WebHorizontalThickness = source.WebHorizontalThickness;
        TopFlangeWidth = source.TopFlangeWidth;
        BottomInternalWidth = source.BottomInternalWidth;
    }
}

public sealed class BridgeMaterialValues
{
    public double Ea { get; }
    internal BridgeMaterialValues(global::GPC.Checkers.CompositeBridge.BridgeMaterialValues source, BridgeProjection map)
    {
        Ea = source.Ea;
    }
}

public sealed class BridgePlate
{
    public double Width { get; }
    public double Thickness { get; }
    public double Psi { get; }
    public double KSigma { get; }
    public double Lambda { get; }
    public double Rho { get; }
    public double CompressedWidth { get; }
    public double EffectiveAtStart { get; }
    public double EffectiveAtEnd { get; }
    public double StartStress { get; }
    public double EndStress { get; }
    internal BridgePlate(global::GPC.Checkers.CompositeBridge.BridgePlate source, BridgeProjection map)
    {
        Width = source.Width;
        Thickness = source.Thickness;
        Psi = source.Psi;
        KSigma = source.KSigma;
        Lambda = source.Lambda;
        Rho = source.Rho;
        CompressedWidth = source.CompressedWidth;
        EffectiveAtStart = source.EffectiveAtStart;
        EffectiveAtEnd = source.EffectiveAtEnd;
        StartStress = source.StartStress;
        EndStress = source.EndStress;
    }
}

public sealed class BridgeEffective
{
    public double WebTop { get; }
    public double WebBottom { get; }
    public double TopWidth { get; }
    public double BottomWidth { get; }
    public BridgePlate Web { get; }
    public BridgePlate Top { get; }
    public BridgePlate Bottom { get; }
    public double SecondBottomWidth { get; }
    public BridgePlate? SecondBottom { get; }
    public BridgePlate? BottomOutstand { get; }
    internal BridgeEffective(global::GPC.Checkers.CompositeBridge.BridgeEffective source, BridgeProjection map)
    {
        WebTop = source.WebTop;
        WebBottom = source.WebBottom;
        TopWidth = source.TopWidth;
        BottomWidth = source.BottomWidth;
        Web = map.Read(source.Web)!;
        Top = map.Read(source.Top)!;
        Bottom = map.Read(source.Bottom)!;
        SecondBottomWidth = source.SecondBottomWidth;
        SecondBottom = map.Read(source.SecondBottom);
        BottomOutstand = map.Read(source.BottomOutstand);
    }
}

public sealed class BridgeContribution
{
    internal global::GPC.Checkers.CompositeBridge.BridgeContribution Native { get; }
    public string Name { get; }
    public string Kind { get; }
    public double N { get; }
    public double Mx { get; }
    public double N0 { get; }
    public double HomogenizationN { get; }
    public double Phi { get; }
    public double EffectivePhi { get; }
    public double Area { get; }
    public double Centroid { get; }
    public double Inertia { get; }
    public double RebarArea { get; }
    public double WBottom { get; }
    public double? WTop { get; }
    public double SolverInertia { get; }
    public double EquilibriumResidual { get; }
    public double LoadY { get; }
    public string LoadReference { get; }
    public double V { get; }
    public double ShrinkageStrain { get; }
    public double ConcreteStressOffset { get; }
    public double EquivalentN { get; }
    public double EquivalentMomentAtInterface { get; }
    public bool IsShrinkage { get; }
    public bool HasConcrete { get; }
    public double MomentAtInterface { get; }
    public double? NeutralAxis { get; }
    private readonly HistoryStressProfile? profile;
    public HistoryStressProfile? GetHistoryProfile() => profile;
    public double SteelStress(double y) => Native.SteelStress(y);
    public double Stress(string material, double y) => Native.Stress(material, y);
    public double Curvature(double elasticModulus) => GPC.Checkers.CompositeBridge.BridgeDerivedResults.Curvature(Native, elasticModulus);
    internal BridgeContribution(global::GPC.Checkers.CompositeBridge.BridgeContribution source, BridgeProjection map)
    {
        Native = source;
        Name = source.Name;
        Kind = source.Kind;
        N = source.N;
        Mx = source.Mx;
        N0 = source.N0;
        HomogenizationN = source.HomogenizationN;
        Phi = source.Phi;
        EffectivePhi = source.EffectivePhi;
        Area = source.Area;
        Centroid = source.Centroid;
        Inertia = source.Inertia;
        RebarArea = source.RebarArea;
        WBottom = source.WBottom;
        WTop = source.WTop;
        SolverInertia = source.SolverInertia;
        EquilibriumResidual = source.EquilibriumResidual;
        LoadY = source.LoadY;
        LoadReference = source.LoadReference;
        V = source.V;
        ShrinkageStrain = source.ShrinkageStrain;
        ConcreteStressOffset = source.ConcreteStressOffset;
        EquivalentN = source.EquivalentN;
        EquivalentMomentAtInterface = source.EquivalentMomentAtInterface;
        IsShrinkage = source.IsShrinkage;
        HasConcrete = source.HasConcrete;
        MomentAtInterface = source.MomentAtInterface;
        NeutralAxis = source.NeutralAxis;
        profile = map.Read(source.GetHistoryProfile());
    }
}

/// <summary>
/// Tension stiffening of a tensile bar of the cracked slab in the SLE states (PON-1; EN 1994-2 §7.2.1(1), EN 1994-1-1 §7.4.3(3)):
/// σs = σs,0 + Δσs, Δσs = 0,4 fctm/(αst ρs), ρs = As/Act, αst = A I/(Aa Ia). N, mm, MPa. Copy of the native BridgeTensionStiffening.
/// </summary>
public sealed class BridgeTensionStiffening
{
    /// <summary>σs,0: the stress of the bar in the cracked section, the sum of the phase contributions.</summary>
    public double StressAtCrack { get; }
    /// <summary>Δσs.</summary>
    public double Increment { get; }
    public double Fctm { get; }
    /// <summary>ρs = As/Act.</summary>
    public double ReinforcementRatio { get; }
    /// <summary>αst = A I/(Aa Ia).</summary>
    public double Alpha { get; }
    public double ConcreteArea { get; }
    public double ReinforcementArea { get; }
    public double CrackedArea { get; }
    public double CrackedInertia { get; }
    public double SteelArea { get; }
    public double SteelInertia { get; }
    /// <summary>σs = σs,0 + Δσs.</summary>
    public double Stress => StressAtCrack + Increment;
    internal BridgeTensionStiffening(global::GPC.Checkers.CompositeBridge.BridgeTensionStiffening source)
    {
        StressAtCrack = source.StressAtCrack; Increment = source.Increment; Fctm = source.Fctm; ReinforcementRatio = source.ReinforcementRatio; Alpha = source.Alpha;
        ConcreteArea = source.ConcreteArea; ReinforcementArea = source.ReinforcementArea; CrackedArea = source.CrackedArea; CrackedInertia = source.CrackedInertia;
        SteelArea = source.SteelArea; SteelInertia = source.SteelInertia;
    }
}

public sealed class BridgeStressPoint
{
    private readonly BridgeTensionStiffening? tensionStiffening;
    public string Name { get; }
    public string Material { get; }
    public double Y { get; }
    public double Stress { get; }
    public double Limit { get; }
    public double? Utilization { get; }
    public bool Active { get; }
    public double[] Contributions { get; }
    internal BridgeStressPoint(global::GPC.Checkers.CompositeBridge.BridgeStressPoint source, BridgeProjection map)
    {
        Name = source.Name;
        Material = source.Material;
        Y = source.Y;
        Stress = source.Stress;
        Limit = source.Limit;
        Utilization = source.Utilization;
        Active = source.Active;
        Contributions = source.Contributions.Select(v => v).ToArray();
        tensionStiffening = source.GetTensionStiffening() is { } stiffening ? new BridgeTensionStiffening(stiffening) : null;
    }
    /// <summary>
    /// The tension stiffening added to the stress of a tensile bar of the cracked slab (PON-1; null: not applied). A method, as in the library, so
    /// that the shape of the projected point does not change; here Stress ≠ ΣContributions.
    /// </summary>
    public BridgeTensionStiffening? GetTensionStiffening() => tensionStiffening;
}

public sealed class BridgeSteelProperties
{
    public double Area { get; }
    public double Centroid { get; }
    public double Inertia { get; }
    internal BridgeSteelProperties(global::GPC.Checkers.CompositeBridge.BridgeSteelProperties source, BridgeProjection map)
    {
        Area = source.Area;
        Centroid = source.Centroid;
        Inertia = source.Inertia;
    }
}

public sealed class BridgeStage
{
    public string Name { get; }
    public int Iterations { get; }
    public double Residual { get; }
    public BridgeEffective Effective { get; }
    public BridgeSteelProperties EffectiveSteel { get; }
    public List<BridgeContribution> Contributions { get; }
    public List<BridgeStressPoint> Points { get; }
    public List<string> Warnings { get; }
    public BridgeShearResult? Shear { get; }
    public BridgeStudResult? Studs { get; }
    public BridgeTorsionResult? Torsion { get; }
    public double? SteelNeutralAxis { get; }
    private readonly HistoryStageView? history;
    public HistoryStageView? GetHistory() => history;
    public Anthea.Application.Presentation.NeutralAxisPresentation NeutralAxis { get; }
    internal BridgeStage(global::GPC.Checkers.CompositeBridge.BridgeStage source, BridgeProjection map)
    {
        Name = source.Name;
        Iterations = source.Iterations;
        Residual = source.Residual;
        Effective = map.Read(source.Effective)!;
        EffectiveSteel = map.Read(source.EffectiveSteel)!;
        Contributions = source.Contributions.Select(v => map.Read(v)!).ToList();
        Points = source.Points.Select(v => map.Read(v)!).ToList();
        Warnings = source.Warnings.Select(v => v).ToList();
        Shear = map.Read(source.Shear);
        Studs = map.Read(source.Studs);
        Torsion = map.Read(source.Torsion);
        SteelNeutralAxis = source.SteelNeutralAxis;
        history = map.Read(source.GetHistory());
        NeutralAxis = Anthea.Application.Presentation.NeutralAxisPresentation.Bridge(source);
    }
}

public sealed class HistoryStressProfile
{
    internal global::GPC.Checkers.CompositeBridge.History.HistoryStressProfile Native { get; }
    public double Stress(string material, double y) => Native.Stress(material, y);
    internal HistoryStressProfile(global::GPC.Checkers.CompositeBridge.History.HistoryStressProfile source, BridgeProjection map)
    {
        Native = source;
    }
}

public sealed class HistoryStageView
{
    public HistoryStageResult State { get; }
    public bool Nonlinear { get; }
    public HistoryStressProfile Profile { get; }
    public double TransformedArea { get; }
    public double ElasticCentroid { get; }
    public double TransformedInertia { get; }
    internal HistoryStageView(global::GPC.Checkers.CompositeBridge.History.HistoryStageView source, BridgeProjection map)
    {
        State = map.Read(source.State)!;
        Nonlinear = source.Nonlinear;
        Profile = map.Read(source.Profile)!;
        TransformedArea = source.TransformedArea;
        ElasticCentroid = source.ElasticCentroid;
        TransformedInertia = source.TransformedInertia;
    }
}

public sealed class HistoryStrainPlane
{
    public double AxialStrain { get; }
    public double Curvature { get; }
    internal HistoryStrainPlane(global::GPC.Checkers.CompositeBridge.History.HistoryStrainPlane source, BridgeProjection map)
    {
        AxialStrain = source.AxialStrain;
        Curvature = source.Curvature;
    }
}

public sealed class HistoryFiber
{
    public string Id { get; }
    public double X { get; }
    public double Y { get; }
    internal HistoryFiber(global::GPC.Checkers.CompositeBridge.History.HistoryFiber source, BridgeProjection map)
    {
        Id = source.Id;
        X = source.X;
        Y = source.Y;
    }
}

public sealed class HistoryPhase
{
    public double DeltaN { get; }
    public double DeltaM { get; }
    public IReadOnlyDictionary<string, HistoryStrainPlane> ImposedStrainIncrements { get; }
    internal HistoryPhase(global::GPC.Checkers.CompositeBridge.History.HistoryPhase source, BridgeProjection map)
    {
        DeltaN = source.DeltaN;
        DeltaM = source.DeltaM;
        ImposedStrainIncrements = source.ImposedStrainIncrements.ToDictionary(p => p.Key, p => map.Read(p.Value));
    }
}

public sealed class HistoryFiberResult
{
    public string ComponentId { get; }
    public HistoryFiber Fiber { get; }
    public bool Active { get; }
    public double EffectiveArea { get; }
    public double ActivationStrain { get; }
    public double TotalStrain { get; }
    public double ImposedStrain { get; }
    public double MechanicalStrain { get; }
    public double Stress { get; }
    public double StressIncrement { get; }
    public HistoryMaterialState MaterialState { get; }
    internal HistoryFiberResult(global::GPC.Checkers.CompositeBridge.History.HistoryFiberResult source, BridgeProjection map)
    {
        ComponentId = source.ComponentId;
        Fiber = map.Read(source.Fiber)!;
        Active = source.Active;
        EffectiveArea = source.EffectiveArea;
        ActivationStrain = source.ActivationStrain;
        TotalStrain = source.TotalStrain;
        ImposedStrain = source.ImposedStrain;
        MechanicalStrain = source.MechanicalStrain;
        Stress = source.Stress;
        StressIncrement = source.StressIncrement;
        MaterialState = map.Read(source.MaterialState)!;
    }
}

public sealed class HistoryStageResult
{
    public string Name { get; }
    public HistoryStrainPlane TotalPlane { get; }
    public HistoryStrainPlane IncrementPlane { get; }
    public double N { get; }
    public double MomentAtOrigin { get; }
    public double V { get; }
    public double IncrementApplicationY { get; }
    public double IntegratedN { get; }
    public double IntegratedMomentAtOrigin { get; }
    public double ForceResidual { get; }
    public double MomentResidual { get; }
    public int NewtonIterations { get; }
    public int EffectiveIterations { get; }
    public double EffectiveResidual { get; }
    public IReadOnlyList<HistoryFiberResult> Fibers { get; }
    public HistoryPhase AppliedPhase { get; }
    internal HistoryStageResult(global::GPC.Checkers.CompositeBridge.History.HistoryStageResult source, BridgeProjection map)
    {
        Name = source.Name;
        TotalPlane = map.Read(source.TotalPlane);
        IncrementPlane = map.Read(source.IncrementPlane);
        N = source.N;
        MomentAtOrigin = source.MomentAtOrigin;
        V = source.V;
        IncrementApplicationY = source.IncrementApplicationY;
        IntegratedN = source.IntegratedN;
        IntegratedMomentAtOrigin = source.IntegratedMomentAtOrigin;
        ForceResidual = source.ForceResidual;
        MomentResidual = source.MomentResidual;
        NewtonIterations = source.NewtonIterations;
        EffectiveIterations = source.EffectiveIterations;
        EffectiveResidual = source.EffectiveResidual;
        Fibers = source.Fibers.Select(v => map.Read(v)!).ToArray();
        AppliedPhase = map.Read(source.AppliedPhase)!;
    }
}

public sealed class HistoryPlasticState : HistoryMaterialState
{
    public double PlasticStrain { get; }
    internal HistoryPlasticState(global::GPC.Checkers.CompositeBridge.History.HistoryPlasticState source, BridgeProjection map)
    {
        PlasticStrain = source.PlasticStrain;
    }
}

public sealed class SectionResponseOptions
{
    public SectionResponseControl Control { get; }
    internal SectionResponseOptions(global::GPC.Checkers.CompositeBridge.History.SectionResponseOptions source, BridgeProjection map)
    {
        Control = (SectionResponseControl)source.Control;
    }
}

public sealed class SectionResponsePoint
{
    public int Index { get; }
    public double ReferenceStrain { get; }
    public double MomentAtReference { get; }
    public HistoryStageResult State { get; }
    internal SectionResponsePoint(global::GPC.Checkers.CompositeBridge.History.SectionResponsePoint source, BridgeProjection map)
    {
        Index = source.Index;
        ReferenceStrain = source.ReferenceStrain;
        MomentAtReference = source.MomentAtReference;
        State = map.Read(source.State)!;
    }
}

public sealed class SectionResponseResult
{
    public bool Completed { get; }
    internal global::GPC.Checkers.CompositeBridge.History.SectionResponseResult Native { get; }
    public SectionResponseOptions Options { get; }
    public IReadOnlyList<SectionResponsePoint> Points { get; }
    public string Message { get; }
    internal SectionResponseResult(global::GPC.Checkers.CompositeBridge.History.SectionResponseResult source, BridgeProjection map)
    {
        Native = source;
        Completed = source.Completed;
        Options = map.Read(source.Options)!;
        Points = source.Points.Select(v => map.Read(v)!).ToArray();
        Message = source.Message;
    }
}

public enum BridgeSteelSectionType { H = 0, InclinedWebH = 1, Box = 2 }

public enum SectionResponseControl { MomentCurvature = 0, AxialForceStrain = 1 }

public class HistoryMaterialState { internal HistoryMaterialState() { } }

internal sealed class BridgeProjection
{
    private readonly Dictionary<object, object> cache = new(ReferenceEqualityComparer.Instance);
    private T Copy<T>(object source, Func<T> create) where T : class
    {
        if (cache.TryGetValue(source, out var existing)) return (T)existing;
        var copy = create(); cache.Add(source, copy); return copy;
    }
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeSectionProperties? Read(global::GPC.Checkers.CompositeBridge.BridgeSectionProperties? source) => source is null ? null : Copy(source, () => new BridgeSectionProperties(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal WebShearResistance? Read(global::GPC.Checkers.CompositeBridge.WebShearResistance? source) => source is null ? null : Copy(source, () => new WebShearResistance(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal TransverseStiffenerResistance? Read(global::GPC.Checkers.CompositeBridge.TransverseStiffenerResistance? source) => source is null ? null : Copy(source, () => new TransverseStiffenerResistance(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal HeadedStudResistance? Read(global::GPC.Checkers.CompositeBridge.HeadedStudResistance? source) => source is null ? null : Copy(source, () => new HeadedStudResistance(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeDetailValue? Read(global::GPC.Checkers.CompositeBridge.BridgeDetailValue? source) => source is null ? null : Copy(source, () => new BridgeDetailValue(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeLocalCheck? Read(global::GPC.Checkers.CompositeBridge.BridgeLocalCheck? source) => source is null ? null : Copy(source, () => new BridgeLocalCheck(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeShearFlow? Read(global::GPC.Checkers.CompositeBridge.BridgeShearFlow? source) => source is null ? null : Copy(source, () => new BridgeShearFlow(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeShearResult? Read(global::GPC.Checkers.CompositeBridge.BridgeShearResult? source) => source is null ? null : Copy(source, () => new BridgeShearResult(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeStudResult? Read(global::GPC.Checkers.CompositeBridge.BridgeStudResult? source) => source is null ? null : Copy(source, () => new BridgeStudResult(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeTorsionFlow? Read(global::GPC.Checkers.CompositeBridge.BridgeTorsionFlow? source) => source is null ? null : Copy(source, () => new BridgeTorsionFlow(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeDistortionResult? Read(global::GPC.Checkers.CompositeBridge.BridgeDistortionResult? source) => source is null ? null : Copy(source, () => new BridgeDistortionResult(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeTorsionResult? Read(global::GPC.Checkers.CompositeBridge.BridgeTorsionResult? source) => source is null ? null : Copy(source, () => new BridgeTorsionResult(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeBar? Read(global::GPC.Checkers.CompositeBridge.BridgeBar? source) => source is null ? null : Copy(source, () => new BridgeBar(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeGeometry? Read(global::GPC.Checkers.CompositeBridge.BridgeGeometry? source) => source is null ? null : Copy(source, () => new BridgeGeometry(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeMaterialValues? Read(global::GPC.Checkers.CompositeBridge.BridgeMaterialValues? source) => source is null ? null : Copy(source, () => new BridgeMaterialValues(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgePlate? Read(global::GPC.Checkers.CompositeBridge.BridgePlate? source) => source is null ? null : Copy(source, () => new BridgePlate(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeEffective? Read(global::GPC.Checkers.CompositeBridge.BridgeEffective? source) => source is null ? null : Copy(source, () => new BridgeEffective(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeContribution? Read(global::GPC.Checkers.CompositeBridge.BridgeContribution? source) => source is null ? null : Copy(source, () => new BridgeContribution(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeStressPoint? Read(global::GPC.Checkers.CompositeBridge.BridgeStressPoint? source) => source is null ? null : Copy(source, () => new BridgeStressPoint(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeSteelProperties? Read(global::GPC.Checkers.CompositeBridge.BridgeSteelProperties? source) => source is null ? null : Copy(source, () => new BridgeSteelProperties(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal BridgeStage? Read(global::GPC.Checkers.CompositeBridge.BridgeStage? source) => source is null ? null : Copy(source, () => new BridgeStage(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal HistoryStressProfile? Read(global::GPC.Checkers.CompositeBridge.History.HistoryStressProfile? source) => source is null ? null : Copy(source, () => new HistoryStressProfile(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal HistoryStageView? Read(global::GPC.Checkers.CompositeBridge.History.HistoryStageView? source) => source is null ? null : Copy(source, () => new HistoryStageView(source, this));
    internal HistoryStrainPlane Read(global::GPC.Checkers.CompositeBridge.History.HistoryStrainPlane source) => new(source, this);
    [return: NotNullIfNotNull(nameof(source))]
    internal HistoryFiber? Read(global::GPC.Checkers.CompositeBridge.History.HistoryFiber? source) => source is null ? null : Copy(source, () => new HistoryFiber(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal HistoryPhase? Read(global::GPC.Checkers.CompositeBridge.History.HistoryPhase? source) => source is null ? null : Copy(source, () => new HistoryPhase(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal HistoryFiberResult? Read(global::GPC.Checkers.CompositeBridge.History.HistoryFiberResult? source) => source is null ? null : Copy(source, () => new HistoryFiberResult(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal HistoryStageResult? Read(global::GPC.Checkers.CompositeBridge.History.HistoryStageResult? source) => source is null ? null : Copy(source, () => new HistoryStageResult(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal HistoryPlasticState? Read(global::GPC.Checkers.CompositeBridge.History.HistoryPlasticState? source) => source is null ? null : Copy(source, () => new HistoryPlasticState(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal SectionResponseOptions? Read(global::GPC.Checkers.CompositeBridge.History.SectionResponseOptions? source) => source is null ? null : Copy(source, () => new SectionResponseOptions(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal SectionResponsePoint? Read(global::GPC.Checkers.CompositeBridge.History.SectionResponsePoint? source) => source is null ? null : Copy(source, () => new SectionResponsePoint(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal SectionResponseResult? Read(global::GPC.Checkers.CompositeBridge.History.SectionResponseResult? source) => source is null ? null : Copy(source, () => new SectionResponseResult(source, this));
    [return: NotNullIfNotNull(nameof(source))]
    internal HistoryMaterialState? Read(global::GPC.Checkers.CompositeBridge.History.HistoryMaterialState? source) => source is null ? null : source is History.HistoryPlasticState p ? Read(p) : Copy(source, () => new HistoryMaterialState());
}
