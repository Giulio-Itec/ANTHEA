using GPC.Model.Checking.Contracts;
using System.Text.Json.Serialization;

namespace Anthea.Reports;

/// <summary>Immutable presentation/report snapshot. Display units: m, mm, kN, kNm, kPa. No calculation methods.</summary>
public sealed record EmbeddedWallReportData(string Name, string Fingerprint, string CaseId, string ActionBasis,
    string Status, bool Complete, string Diagnostic, string InputJson, IReadOnlyList<EmbeddedWallStageData> Stages,
    IReadOnlyList<EmbeddedWallCheckData> Checks, IReadOnlyList<string> Scope, IReadOnlyList<EmbeddedWallHydraulicData>? Hydraulics = null,
    IReadOnlyList<EmbeddedWallHydraulicCheckData>? HydraulicChecks = null);
public sealed record EmbeddedWallStageData(string Id, int Increments, int Iterations, double ForceResidualN, double MomentResidualNmm,
    IReadOnlyList<EmbeddedWallPointData> Points, IReadOnlyList<EmbeddedWallSupportData> Supports);
public sealed record EmbeddedWallPointData(double Elevation, string Side, string Section, double Displacement, double Rotation,
    double Normal, double Shear, double Moment, double LeftPressure, double RightPressure, bool LeftPlastic, bool RightPlastic);
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
