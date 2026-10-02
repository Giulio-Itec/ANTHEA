using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;

namespace Anthea.Calculations.Geotechnics;

// Transport records of the foundation and sliding results of the walls in the units of the documents (m, kN, kPa, mm for displacements).
// The calculation is GPCChecker.Geotechnics (mm, N, MPa); the conversions are below.

/// <summary>Seismic bearing capacity per metre (EN 1998-5 Annex F): capacity and Nmax kN/m, normalised N̄, V̄, M̄, soil inertia F̄, vertical limit and interaction.</summary>
public sealed record SeismicBearing(double Capacity, double? Ratio, double NMax, double SoilInertia,
    double NBar, double VBar, double MBar, double VerticalLimit, double? Interaction, string Status)
{
    public static SeismicBearing From(SeismicBearingResult r) => new(r.Capacity, r.Ratio, r.NMax, r.SoilInertia, r.NBar, r.VBar, r.MBar, r.VerticalLimit, r.Interaction,
        r.Status switch { SeismicBearingStatus.Satisfied => "Soddisfatta", SeismicBearingStatus.NotSatisfied => "Non soddisfatta", _ => "Non soddisfatta: dominio sismico esaurito" });
}

/// <summary>Slice of the settlement integration: depths below the foundation m, net stress and constrained modulus kPa, settlement mm.</summary>
public sealed record SettlementSlice(string Soil, double Top, double Bottom, double Stress, double Modulus, double SettlementMm)
{
    public static SettlementSlice From(GPC.Checkers.Geotechnics.Foundations.SettlementSlice s) => new(s.Soil, s.Top / Slope.Mm, s.Bottom / Slope.Mm, s.Stress / Slope.KPa,
        s.ConstrainedModulus / Slope.KPa, s.Settlement);
}

/// <summary>State of the sliding block: time s, ground acceleration g, relative velocity m/s, displacement mm.</summary>
public sealed record SlidingState(double Time, double AccelerationG, double Velocity, double DisplacementMm);

/// <summary>Newmark permanent sliding: displacement mm, peak velocity m/s, PGA of the scaled record g, history.</summary>
public sealed record SlidingHistory(double DisplacementMm, double PeakVelocity, double PgaG, IReadOnlyList<SlidingState> Points)
{
    public static SlidingHistory From(SlidingResult r) => new(r.Displacement, r.PeakVelocity / Slope.Mm, r.Pga,
        r.Points.Select(p => new SlidingState(p.Time, p.Acceleration, p.Velocity / Slope.Mm, p.Displacement)).ToArray());
}
