namespace Anthea.Calculations.Geotechnics;

/// <summary>Independent simplified Bishop equilibrium solver. No geometry construction or code factors.</summary>
public static class BishopSolver
{
    public const string Formula = "F = Σ{[c′d·b + (V − u·b)·tanφ′d]/mα}/D; mα = cosα + sinα·tanφ′d/F. "
        + "D = Σ[V·(xG−xc) + H·(yc−yH) + M]/R; N′ = [V−u·l·cosα−c′d·l·sinα/F]/mα; l=b/cosα. "
        + "V=(1−kv)W+Vext. In non drenato φ=0, c=cu,d, u=0. η=γR/F.";

    public static SlopeSurfaceResult? Solve(SlipCircle circle, SlopeSlice[] input, double gammaR, CancellationToken token = default)
    {
        if (input.Length == 0 || !double.IsFinite(gammaR) || gammaR <= 0) throw new ArgumentException("Conci o γR non validi.");
        double driving = input.Sum(s => s.Driving);
        if (driving <= 1e-9 || !double.IsFinite(driving)) return null;
        double Resistance(SlopeSlice s, double f)
        {
            double angle = s.Alpha * Math.PI / 180, tan = Math.Tan(s.Phi * Math.PI / 180);
            double m = Math.Cos(angle) + Math.Sin(angle) * tan / f;
            return (s.Cohesion * (s.Right - s.Left) + (s.Vertical - s.U * (s.Right - s.Left)) * tan) / m;
        }
        double Residual(double f) => input.Sum(s => Resistance(s, f)) - f * driving;
        // Stay above all mα singularities; do not converge across a pole.
        double lower = Math.Max(1e-7, input.Max(s => -Math.Tan(s.Alpha * Math.PI / 180) * Math.Tan(s.Phi * Math.PI / 180)) + 1e-7);
        double upper = Math.Max(2, lower * 2), fl = Residual(lower);
        if (!double.IsFinite(fl) || fl < 0) return null;
        while (Residual(upper) > 0 && upper < 1e5) { token.ThrowIfCancellationRequested(); upper *= 2; }
        if (Residual(upper) > 0) return null;
        int iteration = 0; double f = 0, residual = 0;
        for (; iteration < 100; iteration++)
        {
            token.ThrowIfCancellationRequested(); f = (lower + upper) / 2; residual = Residual(f);
            if (Math.Abs(residual) <= 1e-9 * Math.Max(1, f * driving)) break;
            if (residual > 0) lower = f; else upper = f;
        }
        var output = new List<SlopeSlice>();
        foreach (var s in input)
        {
            double a = s.Alpha * Math.PI / 180, tan = Math.Tan(s.Phi * Math.PI / 180), b = s.Right - s.Left, l = b / Math.Cos(a);
            double m = Math.Cos(a) + Math.Sin(a) * tan / f;
            double normal = (s.Vertical - s.U * b - s.Cohesion * l * Math.Sin(a) / f) / m;
            // No silent tensile strength or clipping of effective normal forces.
            if (normal < -1e-6 || m <= 0 || !double.IsFinite(normal)) return null;
            double r = Resistance(s, f);
            output.Add(s with { NormalEffective = normal, Resistance = r, Mobilized = r / f, MAlpha = m });
        }
        return new(circle, f, gammaR / f, iteration + 1, residual, driving, output.Sum(s => s.Resistance), output.ToArray());
    }
}
