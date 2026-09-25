namespace X.Core;

// Local plate reductions used by the bridge iteration. No UI or JSON dependencies.
public static partial class BridgeSection
{
    public static BridgeEffective EffectiveWidths(BridgeGeometry g, Func<double, double> stress, double fy)
    {
        double upper = stress(-g.TopThickness), lower = stress(-g.TopThickness - g.WebHeight);
        var web = InternalPlate(g.WebHeight, g.WebThickness, upper, lower, fy);
        var top = Outstand((g.TopWidth - g.WebThickness) / 2, g.TopThickness, Math.Min(stress(0), upper), fy);
        var bottom = Outstand((g.BottomEquivalentWidth - g.WebThickness) / 2, g.BottomEquivalentThickness, Math.Min(lower, stress(-g.Height)), fy);
        return new(web.EffectiveAtStart, web.EffectiveAtEnd, g.WebThickness + 2 * top.EffectiveAtStart,
            g.WebThickness + 2 * bottom.EffectiveAtStart, web, top, bottom);
    }
    /// <summary>EN 1993-1-5 §4.4 / table 4.1; negative stress denotes compression.</summary>
    public static BridgePlate InternalPlate(double b, double t, double startStress, double endStress, double fy)
    {
        bool startCompressed = startStress <= endStress;
        double s1 = Math.Min(startStress, endStress), s2 = Math.Max(startStress, endStress);
        if (s1 >= -1e-9) return new(b, t, 0, 0, 0, 1, 0, b / 2, b / 2, startStress, endStress);
        double psi = s2 / s1, bounded = Math.Clamp(psi, -3, 1);
        double k = bounded >= 0 ? 8.2 / (1.05 + bounded) : bounded >= -1 ? 7.81 - 6.29 * bounded + 9.78 * bounded * bounded : 5.98 * Math.Pow(1 - bounded, 2);
        double lambda = b / t / (28.4 * Math.Sqrt(235 / fy) * Math.Sqrt(k));
        double limit = .5 + Math.Sqrt(.085 - .055 * bounded);
        double rho = lambda <= limit ? 1 : Math.Clamp((lambda - .055 * (3 + bounded)) / (lambda * lambda), 0, 1);
        double bc = psi < 0 ? b / (1 - psi) : b, beff = rho * bc;
        double b1 = psi < 0 ? .4 * beff : 2 * beff / (5 - psi), b2 = beff - b1 + b - bc;
        return new(b, t, psi, k, lambda, rho, bc, startCompressed ? b1 : b2, startCompressed ? b2 : b1, startStress, endStress);
    }
    private static BridgePlate Outstand(double b, double t, double stress, double fy)
    {
        double lambda = b / t / (28.4 * Math.Sqrt(235 / fy) * Math.Sqrt(.43));
        double rho = stress >= -1e-9 || lambda <= .748 ? 1 : Math.Clamp((lambda - .188) / (lambda * lambda), 0, 1);
        return new(b, t, 1, .43, lambda, rho, stress < 0 ? b : 0, rho * b, 0, stress, stress);
    }
}
