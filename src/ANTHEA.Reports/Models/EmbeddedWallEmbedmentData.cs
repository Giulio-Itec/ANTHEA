using System.Text.Json.Serialization;
using GPC.Model.Checking.Contracts;

namespace Anthea.Reports;

/// <summary>Lengths/elevations m; criterion units explicit. Candidate reports are the original evaluated snapshots.</summary>
public sealed record EmbeddedWallEmbedmentData(bool Completed, bool Cancelled, bool MinimumCertified,
    double ReferenceElevation, double Increment, string Source, IReadOnlyList<double> PlannedEmbedments,
    double? BestEmbedment, double? BestToe, string Diagnostic, IReadOnlyList<EmbeddedWallEmbedmentCandidate> Candidates);
public sealed record EmbeddedWallEmbedmentCandidate(double Embedment, double ToeElevation, EngineeringOutcome Outcome,
    string Diagnostic, IReadOnlyList<EmbeddedWallEmbedmentCriterion> Criteria, EmbeddedWallReportData? Result)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
}
public sealed record EmbeddedWallEmbedmentCriterion(string CaseId, string Id, EngineeringOutcome Outcome,
    double? Demand, double? Limit, double? Ratio, string Unit, string Method, string Diagnostic, string? Stage, double? Elevation)
{
    [JsonIgnore] public string OutcomeText => EmbeddedWallReport.OutcomeText(Outcome);
    [JsonIgnore] public string Label => Id switch { "HeadDisplacement" => "Spostamento in testa", "ConcreteStrips" => "Fasce in c.a.",
        "SteelSupports" => "Sostegni metallici", "Anchors" => "Sfilamento tiranti", "SteelTubes" => "Tubi CHS", "RcPiles" => "Pali in c.a.",
        "HydraulicChecks" => "UPL / HYD", "GlobalStability" => "Stabilità globale", _ => Id };
}
