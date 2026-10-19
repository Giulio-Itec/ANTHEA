using GPC.Model.Checking.Contracts;

namespace Anthea.Reports;

/// <summary>Immutable presentation/report snapshot. Display units: m, mm, kN, kNm, kPa. No calculation methods.</summary>
public sealed record EmbeddedWallReportData(string Name, string Fingerprint, string CaseId, string ActionBasis,
    string Status, bool Complete, string Diagnostic, string InputJson, IReadOnlyList<EmbeddedWallStageData> Stages,
    IReadOnlyList<EmbeddedWallCheckData> Checks, IReadOnlyList<string> Scope);
public sealed record EmbeddedWallStageData(string Id, int Increments, int Iterations, double ForceResidualN, double MomentResidualNmm,
    IReadOnlyList<EmbeddedWallPointData> Points, IReadOnlyList<EmbeddedWallSupportData> Supports);
public sealed record EmbeddedWallPointData(double Elevation, string Side, string Section, double Displacement, double Rotation,
    double Normal, double Shear, double Moment, double LeftPressure, double RightPressure, bool LeftPlastic, bool RightPlastic);
public sealed record EmbeddedWallSupportData(string Id, double AxialForce, double HorizontalReaction, double ReferenceDisplacement, bool AtCapacity);
public sealed record EmbeddedWallCheckData(string CaseId, string Stage, string Section, double Elevation, string Side,
    double Normal, double Shear, double Moment, string Method, string Unit, double Demand, double? Capacity, double? Ratio,
    EngineeringOutcome Outcome, string Diagnostic);
