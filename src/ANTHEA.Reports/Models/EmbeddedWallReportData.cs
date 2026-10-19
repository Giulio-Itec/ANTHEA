using GPC.Model.Checking.Contracts;
using System.Text.Json.Serialization;

namespace Anthea.Reports;

/// <summary>Immutable presentation/report snapshot. Display units: m, mm, kN, kNm, kPa. No calculation methods.</summary>
public sealed record EmbeddedWallReportData(string Name, string Fingerprint, string CaseId, string ActionBasis,
    string Status, bool Complete, string Diagnostic, string InputJson, IReadOnlyList<EmbeddedWallStageData> Stages,
    IReadOnlyList<EmbeddedWallCheckData> Checks, IReadOnlyList<string> Scope, IReadOnlyList<EmbeddedWallHydraulicData>? Hydraulics = null,
    IReadOnlyList<EmbeddedWallHydraulicCheckData>? HydraulicChecks = null, IReadOnlyList<EmbeddedWallGlobalData>? GlobalChecks = null,
    IReadOnlyList<EmbeddedWallSteelData>? SteelSupports = null, IReadOnlyList<EmbeddedWallAnchorData>? AnchorChecks = null,
    IReadOnlyList<EmbeddedWallTubeData>? SteelTubes = null, IReadOnlyList<EmbeddedWallRcPileData>? RcPiles = null,
    EmbeddedWallEmbedmentData? EmbedmentSearch = null, EmbeddedWallEnvelopeData? Envelope = null, IReadOnlyList<EmbeddedWallReportData>? Cases = null);
public sealed record EmbeddedWallStageData(string Id, int Increments, int Iterations, double ForceResidualN, double MomentResidualNmm,
    IReadOnlyList<EmbeddedWallPointData> Points, IReadOnlyList<EmbeddedWallSupportData> Supports);
public sealed record EmbeddedWallPointData(double Elevation, string Side, string Section, double Displacement, double Rotation,
    double Normal, double Shear, double Moment, double LeftPressure, double RightPressure, bool LeftPlastic, bool RightPlastic, double NetDistributedLoad = 0);
public sealed record EmbeddedWallSupportData(string Id, double AxialForce, double HorizontalReaction, double ReferenceDisplacement, bool AtCapacity);
public sealed record EmbeddedWallCheckData(string CaseId, string Stage, string Section, double Elevation, string Side,
    double Normal, double Shear, double Moment, string Method, string Unit, double Demand, double? Capacity, double? Ratio,
    EngineeringOutcome Outcome, string Diagnostic)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallHydraulicData(string Stage, string Status, bool Complete, string Diagnostic, int Iterations,
    double InflowLitresPerSecondPerMetre, double OutflowLitresPerSecondPerMetre, double BalanceLitresPerSecondPerMetre,
    double ResidualSquareMillimetresPerSecond, IReadOnlyList<EmbeddedWallHydraulicNode> Nodes, IReadOnlyList<EmbeddedWallHydraulicElement> Elements);
public sealed record EmbeddedWallHydraulicNode(int Index, double X, double Z, double Head, double Pressure, bool Draining);
public sealed record EmbeddedWallHydraulicElement(int A, int B, int C, double GradientX, double GradientZ, double FluxX, double FluxZ, double RelativeConductivity);
public sealed record EmbeddedWallHydraulicCheckData(string CaseId, string Stage, string Element, string Method, string Reference, string Source,
    string Unit, double? Demand, double? Capacity, double? Ratio, EngineeringOutcome Outcome, string Diagnostic,
    IReadOnlyDictionary<string, double> Coefficients, IReadOnlyList<EmbeddedWallHydraulicValue> Values, IReadOnlyList<EmbeddedWallHydraulicActionData> Actions)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallHydraulicValue(string Name, double Value, string Unit);
public sealed record EmbeddedWallHydraulicActionData(string Id, string Category, double CharacteristicUpwardForce, double Factor, double DesignUpwardForce, string Source)
{
    [JsonIgnore] public string CategoryText => Category switch { "PermanentStructural" => "G1", "PermanentNonStructural" => "G2", "Variable" => "Q", _ => Category };
}
/// <summary>Global coordinates: valley on left, y=0 at wall head. m, kN/m, kPa. All values supplied by GPC.</summary>
public sealed record EmbeddedWallGlobalData(string Id, string CaseId, string Stage, string Combination, string SearchStatus,
    EngineeringOutcome Outcome, string Reference, string Source, string Diagnostic, double? Demand, double? Capacity, double? Ratio,
    double? TrialRatio, double? RefinedFactor, int Tried, int Valid, int Solved, int Failures, bool Boundary, double? Residual,
    IReadOnlyDictionary<string, double> Coefficients, IReadOnlyList<EmbeddedWallGlobalActionData> Actions,
    IReadOnlyList<EmbeddedWallGlobalPoint> Surface, IReadOnlyList<EmbeddedWallGlobalBody> Bodies, EmbeddedWallGlobalCircle? Circle,
    IReadOnlyList<EmbeddedWallGlobalSlice> Slices)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallGlobalActionData(string Id, string Category, bool Unfavourable, double PartialFactor, double Psi, double CombinedFactor, string Source);
public sealed record EmbeddedWallGlobalPoint(double X, double Y);
public sealed record EmbeddedWallGlobalBody(string Name, IReadOnlyList<EmbeddedWallGlobalPoint> Points);
public sealed record EmbeddedWallGlobalCircle(double X, double Y, double Radius, double Left, double Right);
public sealed record EmbeddedWallGlobalSlice(int Index, double Left, double Right, double BaseY, double TopY, string Soil,
    double SoilWeight, double BodyWeight, double VerticalLoad, double HorizontalLoad, double Pressure,
    double NormalEffective, double Resistance, double Mobilized);
/// <summary>Member actions and resistances in kN, axial stiffness kN/m. N positive tension, per physical support.</summary>
public sealed record EmbeddedWallSteelData(string CaseId, string Stage, string Support, string Kind, double? NormalForce,
    double AnalysedAxialStiffness, bool AtModelCapacity, int? CompressionClass, EngineeringOutcome Outcome,
    string Diagnostic, string Source, IReadOnlyList<EmbeddedWallSteelCheckData> Checks)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallSteelCheckData(string Method, string Reference, double Demand, double? Capacity, double? Ratio,
    EngineeringOutcome Outcome, double? PartialFactor, double? CriticalForce, double? NormalizedSlenderness,
    double? GeometricSlenderness, double? Reduction, string Curve, string Diagnostic)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}

/// <summary>Pullout forces kN, axis lengths m, diameters mm, pressures MPa. Only mapped GPC results.</summary>
public sealed record EmbeddedWallAnchorData(string CaseId, string Stage, string Support, double? Demand, double? Mean,
    double? Minimum, double? Characteristic, double? Capacity, double? Ratio, double? MeanFactor, double? MinimumFactor,
    double? ResistanceFactor, EngineeringOutcome Outcome, string Reference, string Source, string Diagnostic,
    IReadOnlyList<EmbeddedWallAnchorSample> Samples, IReadOnlyList<EmbeddedWallAnchorSegment> Segments)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallAnchorSample(string Id, double? Resistance, bool Applicable, string Source, string Diagnostic);
public sealed record EmbeddedWallAnchorSegment(string Profile, int Layer, double Start, double End, double LimitPressure,
    double InjectionPressure, double DiameterFactor, double BulbDiameter, string Curve, double UnitResistance, double Resistance, string Source);

public sealed record EmbeddedWallTubeData(string CaseId, string Stage, string Section, double? Elevation, string Side,
    double? Normal, double? Shear, double? Moment, double? StripToMemberFactor, int? SectionClass, double? PartialFactor,
    double? ShearReduction, double? NeutralAxisDistance, double? EquilibriumResidualN, EngineeringOutcome Outcome,
    string Diagnostic, string Reference, EmbeddedWallTubeProperties? Properties, IReadOnlyList<EmbeddedWallTubeCheckData> Checks)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallTubeProperties(double Diameter, double Thickness, double Area, double Inertia, double Spacing,
    double ElasticModulus, double YieldStrength, double? EquivalentRigidity, string Source);
public sealed record EmbeddedWallTubeCheckData(string Method, string Reference, string Unit, double Demand, double? Capacity,
    double? Ratio, EngineeringOutcome Outcome, string Diagnostic)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}

public sealed record EmbeddedWallRcPileData(string CaseId, string Stage, string Section, double? Elevation, string Side,
    double? Normal, double? Shear, double? Moment, double? StripToMemberFactor, EngineeringOutcome Outcome,
    string Diagnostic, string Reference, EmbeddedWallRcPileProperties? Properties,
    IReadOnlyList<EmbeddedWallRcCheckData> Checks, IReadOnlyList<EmbeddedWallRcDetailData> Details)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallRcPileProperties(double Diameter, double Spacing, double Cover, int BarCount, double BarDiameter,
    double LinkDiameter, double LinkSpacing, double BarRadius, double ElasticModulus, double Inertia, double? EquivalentRigidity,
    int OutlineDivisions, string Source, double GammaC, double GammaS, double AlphaCC);
public sealed record EmbeddedWallRcCheckData(string Method, string Unit, double Demand, double? Capacity, double? Ratio,
    EngineeringOutcome Outcome, string Diagnostic)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallRcDetailData(string Method, double? Actual, double? Limit, string Unit,
    EngineeringOutcome Outcome, string Reference, string Diagnostic)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
    [JsonIgnore] public string Label
    {
        get
        {
            string[] parts = Method.Split(':', 2);
            string name = parts[0] switch {
                "ClearSpacing" => "Interferro", "NominalCover" => "Copriferro nominale", "BarCoverMargin" => "Margine copriferro",
                "MinimumTension" => "As minima", "MaximumTension" => "As massima", "MinimumLinks" => "Staffe minime",
                "LinkSpacing" => "Passo staffe", "CompressionBarRestraint" => "Ritegno barre", "PileSteelArea" => "Minimo As palo",
                "PileLinkDiameter" => "Minimo φ staffe palo", "PileLinkSpacing" => "Passo staffe palo", _ => parts[0] };
            return parts.Length == 2 ? name + " (" + parts[1] + ")" : name;
        }
    }
}
