using System.Globalization;
namespace Anthea.Calculations;

public static class EngineeringFormat
{
    public static string Number(double? value)
    {
        if (value is not double v || !double.IsFinite(v)) return "—";
        return v == 0 || Math.Abs(v) >= .01 ? v.ToString("0.00", CultureInfo.CurrentCulture) : v.ToString("0.00E+0", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// A length in mm in a report: fixed point with two decimals, never E-notation. A value that rounds to zero at that precision (the
    /// 2,27E-13 mm of solver rounding in «Anima inefficace») shows 0,00, without sign (user decision of 8/11/2026, ANTHEA 1.19.0).
    /// Equal to <see cref="Number"/> for |value| ≥ 0,01 and for +0.
    /// </summary>
    public static string Length(double? value)
    {
        if (value is not double v || !double.IsFinite(v)) return "—";
        return (Math.Round(v, 2, MidpointRounding.AwayFromZero) == 0 ? 0d : v).ToString("0.00", CultureInfo.CurrentCulture);
    }
}
