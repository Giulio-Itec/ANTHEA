namespace Anthea.Reports;

/// <summary>Frozen display values from the GPC envelope. No result combination or engineering calculation.</summary>
public sealed record EmbeddedWallEnvelopeData(bool Complete, string Diagnostic, double Top, double Bottom,
    double TributaryWidth, int Subdivisions, IReadOnlyList<EmbeddedWallEnvelopeHistoryData> Histories,
    IReadOnlyList<EmbeddedWallEnvelopeSeriesData> Series);
public sealed record EmbeddedWallEnvelopeHistoryData(string CaseId, bool Complete, string? FailedStage,
    IReadOnlyList<string> IncludedStages, string Diagnostic);
public sealed record EmbeddedWallEnvelopeSeriesData(string Quantity, string Label, string Unit, string Diagram,
    IReadOnlyList<EmbeddedWallEnvelopeStationData> Stations, EmbeddedWallEnvelopeObservationData Minimum,
    EmbeddedWallEnvelopeObservationData Maximum);
public sealed record EmbeddedWallEnvelopeStationData(double Elevation, string Side,
    EmbeddedWallEnvelopeObservationData Minimum, EmbeddedWallEnvelopeObservationData Maximum);
public sealed record EmbeddedWallEnvelopeObservationData(string CaseId, string Stage, double Value, EmbeddedWallPointData Point);
