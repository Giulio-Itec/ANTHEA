using System.Globalization;
namespace Anthea.Calculations;

/// <summary>
/// The rule of the decimals by unit approved by the user on 16/11/2026 («Sì, tutti i consigli», items a–e of the digits of the embedded walls):
/// the decimals of a quantity follow its unit, no exponential notation of any other kind than «1,5·10⁻⁷», never «-0», the noise below the last
/// decimal shows 0 and, in the tables, the decimals are fixed with the final zeros. Shared by the views (through
/// <c>Anthea.Presentation.Forms.NumericDisplay.Unit</c>, in the culture of the application) and by the reports (it-IT), with the same values of the
/// table of the module of the embedded walls (<c>EmbeddedWallDisplay.Decimals</c> on the branch claude/paratie-ui, bd7587b3).
/// <list type="bullet">
/// <item>m 3; mm 2; rad 5;</item>
/// <item>kN, kN/m, kNm, kNm/m, kPa, kN/m², kN/m³, °, N, Nmm, kNm², kNm²/m 2;</item>
/// <item>MPa 3; pure numbers and unknown units 3; L/s/m and mm²/s 4; % 1;</item>
/// <item>m/s (permeabilities, filtration velocities) and N/mm³ as a power of ten with two significant digits (<see cref="PowerOfTen"/>).</item>
/// </list>
/// Presentation only: documents, results and CSV files keep their values.
/// </summary>
public static class UnitNumberFormat
{
    /// <summary>Text of a missing or not finite value.</summary>
    public const string Missing = "—";

    /// <summary>Decimals of a unit as the modules write it; an unknown unit, an empty one and «-» have those of a pure number.</summary>
    public static int Decimals(string? unit) => (unit ?? "").Trim() switch
    {
        "m" => 3,
        "mm" => 2,
        "rad" => 5,
        "kN" or "kN/m" or "kNm" or "kNm/m" or "kPa" or "kN/m²" or "kN/m³" or "N" or "Nmm" or "kNm²" or "kNm²/m" or "°" => 2,
        "MPa" => 3,
        "L/s/m" or "mm²/s" => 4,
        "m/s" => 10,
        "%" => 1,
        _ => 3
    };

    /// <summary>Units shown as a power of ten (<see cref="PowerOfTen"/>): m/s (approved on 16/11) and N/mm³ (unit weight of the water in the
    /// coefficients of UPL/HYD, about 10⁻⁵: with three decimals it would show 0; to be approved).</summary>
    public static bool IsPowerOfTen(string? unit) => (unit ?? "").Trim() is "m/s" or "N/mm³";

    /// <summary>
    /// A number with the decimals of its unit (<see cref="Decimals"/>) in <paramref name="culture"/>: in a table (<paramref name="table"/>) fixed decimals
    /// with the final zeros (110,90 next to 111,09), elsewhere without them; a power of ten for the units of <see cref="IsPowerOfTen"/>.
    /// </summary>
    public static string Format(double? value, string? unit, IFormatProvider culture, bool table = false) =>
        value is not double v ? Missing : IsPowerOfTen(unit) ? PowerOfTen(v, culture) : Format(v, Decimals(unit), culture, table);

    /// <summary>A number with at most (in a table exactly) <paramref name="decimals"/> decimals in <paramref name="culture"/>; 0 when it rounds to zero,
    /// never «-0»; no separator of the thousands, no exponent.</summary>
    public static string Format(double value, int decimals, IFormatProvider culture, bool table = false)
    {
        if (!double.IsFinite(value)) return Missing;
        int places = Math.Clamp(decimals, 0, 15);
        double rounded = Math.Round(value, places, MidpointRounding.AwayFromZero);
        string format = places == 0 ? "0" : "0." + new string(table ? '0' : '#', places);
        return rounded == 0 ? (table ? 0d.ToString(format, culture) : "0") : rounded.ToString(format, culture);
    }

    /// <summary>
    /// A value as a power of ten: mantissa with two significant digits, «·10» and the exponent in superscript, the decimal separator of
    /// <paramref name="culture"/> (1,5·10⁻⁷). Zero shows 0, an exponent zero the mantissa alone, a missing value «—».
    /// </summary>
    public static string PowerOfTen(double? value, IFormatProvider culture)
    {
        if (value is not double v || !double.IsFinite(v)) return Missing;
        if (v == 0) return "0";
        string text = v.ToString("0.0E+0", CultureInfo.InvariantCulture);
        int mark = text.IndexOf('E');
        string mantissa = text[..mark].Replace(".", NumberFormatInfo.GetInstance(culture).NumberDecimalSeparator, StringComparison.Ordinal);
        string exponent = text[(mark + 1)..].TrimStart('+');
        return exponent == "0" ? mantissa : mantissa + "·10" + new string(exponent.Select(Superscript).ToArray());
    }

    /// <summary>The unit of a quantity written with the unit in brackets («Momento [kNm]» → kNm), empty without brackets.</summary>
    public static string UnitOf(string? quantity)
    {
        if (string.IsNullOrEmpty(quantity)) return "";
        int open = quantity.LastIndexOf('['), close = quantity.LastIndexOf(']');
        return open >= 0 && close > open ? quantity[(open + 1)..close] : "";
    }

    private static char Superscript(char c) => c switch
    {
        '-' => '⁻', '0' => '⁰', '1' => '¹', '2' => '²', '3' => '³', '4' => '⁴', '5' => '⁵', '6' => '⁶', '7' => '⁷', '8' => '⁸', '9' => '⁹', _ => c
    };
}
