namespace Anthea.Calculations.Geotechnics;

// SI-derived engineering units throughout: m, kN, kPa, degrees; per metre out of plane.
// No JSON, UI, retaining-wall or normative dependencies in these transport types.
public readonly record struct SlopePoint(double X, double Y);
public sealed record SlopeSoil(string Name, double Bottom, double Gamma, double GammaSat, double Phi, double Cohesion, double Cu);
public sealed record SlopeBody(string Name, SlopePoint[] Polygon, double Gamma);
public sealed record SlopeLoad(string Id, double Left, double Right, double Y, double Vertical, double Horizontal, double Moment, bool Distributed);
public sealed record SlopeSection(SlopePoint[] Surface, SlopeSoil[] Soils, SlopePoint[] Water, SlopeBody[] Bodies,
    SlopeLoad[] Loads, double RequiredLeft, double RequiredRight);
public sealed record SlopeFactors(string Name, double Soil, double Body, double MPhi, double MC, double MCu,
    double R, double Kh, double Kv, bool Undrained, IReadOnlyDictionary<string, double> Loads);
public sealed record SlopeSearch(double ExitMin, double ExitMax, double EntryMin, double EntryMax,
    double DepthMin, double DepthMax, int Grid, int Slices, int Refinements);
public sealed record SlipCircle(double X, double Y, double Radius, double Left, double Right)
{
    public double Base(double x) => Y - Math.Sqrt(Math.Max(0, Radius * Radius - (x - X) * (x - X)));
}
public sealed record SlopeSlice(int Index, double Left, double Right, double BaseY, double TopY, double Alpha,
    string Soil, double SoilWeight, double BodyWeight, double WeightX, double WeightY,
    double VerticalLoad, double HorizontalLoad, double U, double Phi, double Cohesion,
    double Vertical, double Driving, double NormalEffective, double Resistance, double Mobilized, double MAlpha);
public sealed record SlopeSurfaceResult(SlipCircle Circle, double Factor, double Ratio, int Iterations,
    double Residual, double Driving, double Resistance, SlopeSlice[] Slices);
public sealed record SlopeCaseResult(SlopeFactors Factors, SlopeSurfaceResult? Critical, int Tried, int GeometricallyValid,
    int Solved, int NumericalFailures, bool Boundary, string Status);
public sealed record SlopeResult(SlopeSection Section, SlopeSearch Search, SlopeCaseResult[] Cases, string[] Notes);
