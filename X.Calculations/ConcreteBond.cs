namespace Anthea.Calculations;

/// <summary>Common bond-strength expression for material sheets and anchorage checks.
/// The caller supplies fctk,0.05 with the strength-class cap appropriate to its model.</summary>
public static class ConcreteBond
{
    public sealed record Result(double Fct, double Fctd, double Eta2, double Fbd);
    public static Result Calculate(double fck, double diameter, double eta1, double alpha, double gamma)
    {
        if (!double.IsFinite(fck) || fck <= 0 || !double.IsFinite(alpha) || alpha > 1 || gamma < 1)
            throw new ArgumentException("Controllare fck (> 0), αct (0 < αct ≤ 1) e γc (≥ 1).");
        double fct = Math.Abs(ConcreteMaterialCatalog.Material(Math.Min(fck, 60)).Fctk05);
        double strength = Strength(fct, diameter, eta1, alpha, gamma);
        return new(fct, alpha * fct / gamma, diameter <= 32 ? 1 : (132 - diameter) / 100, strength);
    }
    public static double Strength(double fctk05, double diameter, double eta1, double alpha, double gamma)
    {
        if (new[] { fctk05, diameter, eta1, alpha, gamma }.Any(v => !double.IsFinite(v) || v <= 0) || diameter >= 132)
            throw new ArgumentException("Aderenza: controllare diametro, resistenza e coefficienti.");
        double eta2 = diameter <= 32 ? 1 : (132 - diameter) / 100;
        return 2.25 * eta1 * eta2 * alpha * fctk05 / gamma;
    }
}
