using GPC.Checkers.Concrete.Detailing;

namespace Anthea.Calculations;

/// <summary>Common bond-strength expression for material sheets and anchorage checks.
/// The caller supplies fctk,0.05 with the strength-class cap appropriate to its model.</summary>
public static class ConcreteBond
{
    public sealed record Result(double Fct, double Fctd, double Eta2, double Fbd)
    {
        [System.Text.Json.Serialization.JsonIgnore] public bool Capped { get; init; }
    }
    public static Result Calculate(double fck, double diameter, double eta1, double alpha, double gamma)
    {
        try
        {
            var value = AnchorageCalculator.Bond(fck, diameter, eta1, alpha, gamma);
            return new(value.Fctk05, value.Fctd, value.Eta2, value.Fbd) { Capped = value.Capped };
        }
        catch (ArgumentException e) { throw Italian(e); }
    }
    public static double Strength(double fctk05, double diameter, double eta1, double alpha, double gamma)
    {
        try { return AnchorageCalculator.BondStrength(fctk05, diameter, eta1, alpha, gamma); }
        catch (ArgumentException e) { throw Italian(e); }
    }
    private static ArgumentException Italian(ArgumentException error) => new(error.Message switch
    {
        "Bond: check fck (> 0), αct (0 < αct ≤ 1) and γc (≥ 1)." => "Controllare fck (> 0), αct (0 < αct ≤ 1) e γc (≥ 1).",
        "Bond: check diameter, strength and coefficients." => "Aderenza: controllare diametro, resistenza e coefficienti.",
        _ => error.Message
    });
}
