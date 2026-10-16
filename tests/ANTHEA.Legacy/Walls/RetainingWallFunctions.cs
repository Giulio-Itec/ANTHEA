// Spostato da X.Calculations/RetainingWall.Library.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: metodi della libreria dei muri
// nelle unità del documento usati solo dalle prove (RetainingWall.Checks, F4WallPortChecks, cattura densa dei muri). Corpi invariati; costanti
// e conversioni (Mm, KPa, KN3, Deg, ToContact, ToWallInput) restano nel prodotto.
// I record SoilMass e ValleyBand, prima annidati in RetainingWall (X.Calculations/RetainingWall.SoilGeometry.cs e RetainingWall.Soils.cs,
// commit 8b9ce6d3), sono qui dalla decisione E5 del 15/10/2026 (F2.11c): li restituiscono solo questi metodi. Campi invariati.
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;
using GPC.Checkers.Geotechnics.Walls;
using GPC.Model.Geotechnics;
using Anthea.Calculations.Geotechnics;
using static Anthea.Calculations.RetainingWall;

namespace Anthea.Calculations;

/// <summary>Metodi della libreria dei muri nelle unità del documento (m, kN/m, kNm/m, kPa, gradi) per le prove.</summary>
public static class RetainingWallFunctions
{
    /// <summary>A band of the soil in front of the wall (m above the base, kN/m³, degrees, σ′v in kPa), from the library.</summary>
    public sealed record ValleyBand(double Top, double Bottom, double Gamma, double Effective, double Phi, double SigmaTop, double SigmaBottom);
    public sealed record SoilMass(double Weight, double MomentX, double MomentY);
    public static double Ka(double phi) => EarthPressure.RankineActive(phi * Deg);
    /// <summary>Horizontal Coulomb / Mononobe–Okabe coefficient, vertical back and horizontal fill (angles in degrees).</summary>
    public static double ActiveHorizontal(double phi, double delta, double kh = 0, double kv = 0) => EarthPressure.ActiveHorizontal(phi * Deg, delta * Deg, kh, kv);
    public static Contact ContactLaw(double width, double n, double x) => ToContact(WallContact.Law(width * Mm, n, x * Mm));
    public static List<ValleyBand> ValleyBands(JsonObject d) => RetainingWallAnalysis.ValleyBands(ToWallInput(d))
        .Select(b => new ValleyBand(b.Top / Mm, b.Bottom / Mm, b.UnitWeight / KN3, b.EffectiveUnitWeight / KN3, b.FrictionAngle / Deg, b.SigmaTop / KPa, b.SigmaBottom / KPa)).ToList();
    /// <summary>Weight (kN/m) and static moments (kNm/m) of the soil in front of the wall, or of the wedge over the face above the elevation (m).</summary>
    public static SoilMass FrontSoilMass(JsonObject d, double above = 0, bool wedgeOnly = false)
    {
        var input = ToWallInput(d); var m = RetainingWallAnalysis.FrontSoilMass(input.Geometry, RetainingWallAnalysis.ValleyBands(input), above * Mm, wedgeOnly);
        return new(m.Weight, m.MomentX / Mm, m.MomentY / Mm);
    }
    /// <summary>Rotation and displacement of the stem fixed at the base from the curvatures (heights m, curvatures 1/m, displacements mm).</summary>
    public static IReadOnlyList<DisplacementPoint> IntegrateCurvature(IEnumerable<CurvaturePoint> values, double height)
        => WallDisplacementPoint.Integrate(values.Select(p => new WallCurvaturePoint(p.Y * Mm, p.Curvature / Mm)), height * Mm)
            .Select(p => new DisplacementPoint(p.Height / Mm, p.Curvature * Mm, p.Rotation, p.Displacement)).ToList();
}
