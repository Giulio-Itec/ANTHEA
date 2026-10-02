using System.Text.Json.Serialization;
using Gpc = GPC.Checkers.Geotechnics.Slopes;

namespace Anthea.Calculations.Geotechnics;

// Transport records of the global stability in the units of the documents: m, kN, kPa, degrees; per metre out of plane.
// The calculation is GPCChecker.Geotechnics (GPC.Checkers.Geotechnics.Slopes, mm, N, MPa, rad); FromLibrary converts its results.
public readonly record struct SlopePoint(double X, double Y);
public sealed record SlopeSoil(string Name, double Bottom, double Gamma, double GammaSat, double Phi, double Cohesion, double Cu);
public sealed record SlopeBody(string Name, SlopePoint[] Polygon, double Gamma);
public sealed record SlopeLoad(string Id, double Left, double Right, double Y, double Vertical, double Horizontal, double Moment, bool Distributed);
public sealed record SlopeSection(SlopePoint[] Surface, SlopeSoil[] Soils, SlopePoint[] Water, SlopeBody[] Bodies,
    SlopeLoad[] Loads, double RequiredLeft, double RequiredRight)
{
    public SlopeSoil[] ValleySoils { get; init; } = [];
    public double SoilSplitX { get; init; }
    // Views of the columns kept in the results of the documents.
    public IEnumerable<SlopeSoil[]> SoilColumns => ValleySoils.Length > 0 ? new[] { ValleySoils, Soils } : new[] { Soils };
    public double CoveredBottom => SoilColumns.Max(l => l[^1].Bottom);
}
public sealed record SlopeFactors(string Name, double Soil, double Body, double MPhi, double MC, double MCu,
    double R, double Kh, double Kv, bool Undrained, IReadOnlyDictionary<string, double> Loads);
public sealed record SlopeSearch(double ExitMin, double ExitMax, double EntryMin, double EntryMax,
    double DepthMin, double DepthMax, int Grid, int Slices, int Refinements);
public sealed record SlipCircle(double X, double Y, double Radius, double Left, double Right)
{
    /// <summary>Elevation of the lower arc at x (m), from the circle of the library.</summary>
    public double Base(double x) => new Gpc.SlipCircle(X * Slope.Mm, Y * Slope.Mm, Radius * Slope.Mm, Left * Slope.Mm, Right * Slope.Mm).Base(x * Slope.Mm) / Slope.Mm;
}
public sealed record SlopeSlice(int Index, double Left, double Right, double BaseY, double TopY, double Alpha,
    string Soil, double SoilWeight, double BodyWeight, double WeightX, double WeightY,
    double VerticalLoad, double HorizontalLoad, double U, double Phi, double Cohesion,
    double Vertical, double Driving, double NormalEffective, double Resistance, double Mobilized, double MAlpha);
public sealed record SlopeSurfaceResult(SlipCircle Circle, double Factor, double Ratio, int Iterations,
    double Residual, double Driving, double Resistance, SlopeSlice[] Slices);
public sealed record SlopeCaseResult(SlopeFactors Factors, SlopeSurfaceResult? Critical, int Tried, int GeometricallyValid,
    int Solved, int NumericalFailures, bool Boundary, string Status);
public sealed record SlopeResult(SlopeSection Section, SlopeSearch Search, SlopeCaseResult[] Cases, string[] Notes)
{
    /// <summary>The result of the library (mm, N, MPa, rad), for the tools that evaluate other circles of the same section.</summary>
    [JsonIgnore] public Gpc.SlopeResult? Source { get; init; }
}

/// <summary>Conversion of the slope results of GPCChecker.Geotechnics to the transport records, and the texts of the reports.</summary>
public static class Slope
{
    /// <summary>mm per m; kN/m and N/mm coincide, 1 kPa = 0.001 MPa, 1 kN/m³ = 1e-6 N/mm³, 1 kNm/m = 1000 N·mm/mm.</summary>
    public const double Mm = 1000, KPa = 1e-3, KN3 = 1e-6, Deg = Math.PI / 180;

    public const string Formula = "F = Σ{[c′d·b + (V − u·b)·tanφ′d]/mα}/D; mα = cosα + sinα·tanφ′d/F. "
        + "D = Σ[V·(xG−xc) + H·(yc−yH) + M]/R; N′ = [V−u·l·cosα−c′d·l·sinα/F]/mα; l=b/cosα. "
        + "V=(1−kv)W+Vext. In non drenato φ=0, c=cu,d, u=0. η=γR/F.";
    public static readonly string[] Notes = [Formula,
        "Superfici circolari verso valle, ramo inferiore con centro fra gli estremi, anche con tangente verticale all’ingresso, passanti sotto l’intero muro; interstrato orizzontale. Ricerca a griglia e sulla famiglia tangente, raffinamento da sei minimi distinti: non garantisce il minimo assoluto. Ampliare il dominio e confrontare densità di ricerca e conci.",
        "Bishop semplificato: equilibrio dei momenti e verticale dei conci; forze tangenziali interconcio trascurate, equilibrio orizzontale non imposto. Non sono modellate fessure di trazione, superfici non circolari, pressioni idrodinamiche o degradazione ciclica.",
        "Il muro è una massa rigida; spinte muro–terreno e reazioni di fondazione sono interne alla massa e non si aggiungono. GPC.Geometry per area/baricentro dei corpi; carichi esterni senza inerzia aggiunta alla massa dei sovraccarichi."];

    public static SlopePoint Point(Gpc.SlopePoint p) => new(p.X / Mm, p.Y / Mm);
    public static Gpc.SlopePoint Point(SlopePoint p) => new(p.X * Mm, p.Y * Mm);
    public static SlipCircle Circle(Gpc.SlipCircle c) => new(c.X / Mm, c.Y / Mm, c.Radius / Mm, c.Left / Mm, c.Right / Mm);
    public static Gpc.SlipCircle Circle(SlipCircle c) => new(c.X * Mm, c.Y * Mm, c.Radius * Mm, c.Left * Mm, c.Right * Mm);

    static SlopeSoil Soil(Gpc.SlopeLayer l) => new(l.Name, l.Bottom / Mm, l.Soil.UnitWeight / KN3, l.Soil.SaturatedUnitWeight / KN3, l.Soil.FrictionAngle / Deg,
        l.Soil.EffectiveCohesion / KPa, (l.Soil.UndrainedShearStrength ?? 0) / KPa);

    /// <summary>Distributed loads are pressures (MPa → kPa), concentrated ones forces (N/mm = kN/m) and moments (N·mm/mm → kNm/m).</summary>
    static SlopeLoad Load(Gpc.SlopeLoad l) => new(l.Id, l.Left / Mm, l.Right / Mm, l.Y / Mm, l.Distributed ? l.Vertical / KPa : l.Vertical,
        l.Distributed ? l.Horizontal / KPa : l.Horizontal, l.Moment / Mm, l.Distributed);

    public static SlopeSection Section(Gpc.SlopeSection s) => new(s.Surface.Select(Point).ToArray(), s.Layers.Select(Soil).ToArray(), s.Water.Select(Point).ToArray(),
        s.Bodies.Select(b => new SlopeBody(b.Name, b.Polygon.Select(Point).ToArray(), b.UnitWeight / KN3)).ToArray(), s.Loads.Select(Load).ToArray(), s.RequiredLeft / Mm, s.RequiredRight / Mm)
        { ValleySoils = s.ValleyLayers.Select(Soil).ToArray(), SoilSplitX = s.SoilSplitX / Mm };

    public static SlopeFactors Factors(Gpc.SlopeFactors f) => new(f.Name, f.SoilWeight, f.BodyWeight, f.TanFrictionAngle, f.EffectiveCohesion, f.UndrainedShearStrength,
        f.ResistanceFactor, f.Kh, f.Kv, f.Undrained, f.Loads.ToDictionary(p => p.Key, p => p.Value));

    public static SlopeSearch Search(Gpc.SlopeSearch q) => new(q.ExitMin / Mm, q.ExitMax / Mm, q.EntryMin / Mm, q.EntryMax / Mm, q.DepthMin / Mm, q.DepthMax / Mm, q.Grid, q.Slices, q.Refinements);

    /// <summary>A slice: lengths m, forces kN/m (= N/mm), pressures and cohesion kPa, angles in degrees.</summary>
    public static SlopeSlice Slice(Gpc.SlopeSlice s) => new(s.Index, s.Left / Mm, s.Right / Mm, s.BaseY / Mm, s.TopY / Mm, s.Alpha / Deg, s.Soil, s.SoilWeight, s.BodyWeight,
        s.WeightX / Mm, s.WeightY / Mm, s.VerticalLoad, s.HorizontalLoad, s.PorePressure / KPa, s.FrictionAngle / Deg, s.Cohesion / KPa, s.Vertical, s.Driving,
        s.NormalEffective, s.Resistance, s.Mobilized, s.MAlpha);

    public static SlopeSurfaceResult Surface(Gpc.SlopeSurfaceResult r) => new(Circle(r.Circle), r.Factor, r.Ratio, r.Iterations, r.Residual, r.Driving, r.Resistance, r.Slices.Select(Slice).ToArray());

    /// <summary>The status of the search in the words of the reports; a ratio above 1 is always reported as not satisfied.</summary>
    public static string Status(Gpc.SlopeCaseResult c)
    {
        string status = c.Status switch
        {
            Gpc.SlopeSearchStatus.NoSurface => "Nessuna superficie risolta: estendere dominio/profondità o correggere il modello.",
            Gpc.SlopeSearchStatus.Incomplete => "Ricerca incompleta: superfici con trazione o equilibrio non risolto.",
            Gpc.SlopeSearchStatus.BoundaryMinimum => "Minimo sul bordo della ricerca: estendere il dominio.",
            Gpc.SlopeSearchStatus.NotConverged => "Discretizzazione non convergente: aumentare i conci.",
            _ => c.Critical!.Ratio <= 1 ? "Soddisfatta nel dominio esplorato" : "Non soddisfatta"
        };
        return c.Critical?.Ratio > 1 && !status.StartsWith("Non soddisfatta") ? "Non soddisfatta; " + status : status;
    }

    public static SlopeCaseResult Case(Gpc.SlopeCaseResult c) => new(Factors(c.Factors), c.Critical is null ? null : Surface(c.Critical), c.Tried, c.GeometricallyValid, c.Solved,
        c.NumericalFailures, c.Boundary, Status(c));

    /// <summary>The result of the library in the units of the documents, with the Italian notes of the method followed by the given ones.</summary>
    public static SlopeResult FromLibrary(Gpc.SlopeResult r, IEnumerable<string> notes) => new(Section(r.Section), Search(r.Search), r.Cases.Select(Case).ToArray(), Notes.Concat(notes).ToArray()) { Source = r };

    /// <summary>Elevation of a polyline at x for the drawings (m), with the interpolation of the library.</summary>
    public static double Height(IReadOnlyList<SlopePoint> line, double x) => Gpc.SlopeGeometry.Height(line.Select(Point).ToArray(), x * Mm) / Mm;
}
