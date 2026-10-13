using System.Globalization;
namespace Anthea.Calculations;

public static class EngineeringFormat
{
    public static string Number(double? value)
    {
        if (value is not double v || !double.IsFinite(v)) return "—";
        return v == 0 || Math.Abs(v) >= .01 ? v.ToString("0.00", CultureInfo.CurrentCulture) : v.ToString("0.00E+0", CultureInfo.CurrentCulture);
    }
}
