using Anthea.Reports;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Model.Geotechnics;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

public sealed partial class EmbeddedWallModule
{
    private static EmbeddedWallEnvelopeData Envelopes(WallAnalysisResult analysis, CancellationToken cancellation)
    {
        // Two drawing subdivisions in each common mesh interval, plus every polynomial stationary point.
        // GPC alone selects extrema and concomitant actions. The application converts display units.
        var native = EmbeddedWallAnalysis.Envelope([analysis], 2, cancellation);
        return new(native.IsComplete, native.Diagnostic, native.Top / SoilUnits.Metre, native.Bottom / SoilUnits.Metre,
            native.TributaryWidth / SoilUnits.Metre, native.Subdivisions,
            Array.AsReadOnly(native.Histories.Select(h => new EmbeddedWallEnvelopeHistoryData(h.CaseId, h.Complete,
                h.FailedStage, h.IncludedStages, h.Diagnostic)).ToArray()),
            Array.AsReadOnly(native.Series.Select(s =>
            {
                var (label, unit, factor) = s.Quantity switch
                {
                    WallResultQuantity.Displacement => ("Spostamento", "mm", 1d),
                    WallResultQuantity.Rotation => ("Rotazione", "rad", 1d),
                    WallResultQuantity.NormalForce => ("Normale", "kN", NewtonsPerKilonewton),
                    WallResultQuantity.Shear => ("Taglio", "kN", NewtonsPerKilonewton),
                    WallResultQuantity.Moment => ("Momento", "kNm", NmmPerKilonewtonMetre),
                    WallResultQuantity.LeftPressure => ("Pressione sinistra", "kPa", SoilUnits.KiloPascal),
                    WallResultQuantity.RightPressure => ("Pressione destra", "kPa", SoilUnits.KiloPascal),
                    WallResultQuantity.NetDistributedLoad => ("Carico netto", "kN/m", 1d),
                    _ => throw new ArgumentOutOfRangeException(nameof(s.Quantity))
                };
                EmbeddedWallEnvelopeObservationData Map(WallEnvelopeObservation o) => new(o.CaseId, o.StageId, o.Value / factor, MapPoint(o.Point));
                return new EmbeddedWallEnvelopeSeriesData(s.Quantity.ToString(), label, unit, label + " [" + unit + "]",
                    Array.AsReadOnly(s.Stations.Select(p => new EmbeddedWallEnvelopeStationData(p.Elevation / SoilUnits.Metre, p.Side,
                        Map(p.Minimum), Map(p.Maximum))).ToArray()), Map(s.Minimum), Map(s.Maximum));
            }).ToArray()));
    }
    private static EmbeddedWallPointData MapPoint(WallPointResult p) => new(p.Elevation / SoilUnits.Metre, p.Side, p.SectionId,
        p.Displacement, p.Rotation, p.NormalForce / NewtonsPerKilonewton, p.Shear / NewtonsPerKilonewton,
        p.Moment / NmmPerKilonewtonMetre, p.LeftPressure / SoilUnits.KiloPascal, p.RightPressure / SoilUnits.KiloPascal,
        p.LeftPlastic, p.RightPlastic, p.NetDistributedLoad);
}
