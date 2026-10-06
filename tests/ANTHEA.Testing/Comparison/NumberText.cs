using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Anthea.Testing.Comparison;

/// <summary>
/// Number-aware comparison of printed text (reports, CSV cells, strings of the results). The text without its numbers (the skeleton,
/// numbers replaced by '#') must be identical; the numbers are compared pairwise:
/// - integers (no decimal separator, no exponent: counts, classes, identifiers, indexes) must be equal, apart from the quantity tolerance;
/// - decimals may differ by half a unit of the last printed digit of each of the two values (rounding of the presentation)
///   plus the tolerance of the quantity.
/// Numbers glued to letters (C30, B450C, Ø16, 2e) belong to the skeleton and are compared as text.
/// Both "1.234,5" and "1234.5" are read; a single separator is decimal, a repeated one groups thousands.
/// </summary>
public static class NumberText
{
    static readonly Regex Token = new(@"(?<![\p{L}\p{N}_.,])[-+−]?\d+(?:[.,]\d+)*(?:[eE][-+]?\d+)?(?![\p{L}\p{N}_])", RegexOptions.Compiled);

    public readonly record struct Printed(double Value, double HalfUnit, bool Integer, string Text);

    public static (string Skeleton, List<Printed> Numbers) Split(string text)
    {
        var numbers = new List<Printed>();
        string skeleton = Token.Replace(text, m =>
        {
            if (Parse(m.Value) is { } p) { numbers.Add(p); return "#"; }
            return m.Value;
        });
        return (skeleton, numbers);
    }

    public static Printed? Parse(string token)
    {
        string t = token.Replace('−', '-');
        int sign = 1;
        if (t.StartsWith('-')) { sign = -1; t = t[1..]; } else if (t.StartsWith('+')) t = t[1..];
        int exponent = 0; int e = t.IndexOfAny(['e', 'E']);
        if (e >= 0)
        {
            if (!int.TryParse(t[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) return null;
            t = t[..e];
        }
        int dots = t.Count(c => c == '.'), commas = t.Count(c => c == ',');
        char? decimalSeparator = null;
        if (dots > 0 && commas > 0) decimalSeparator = t.LastIndexOf('.') > t.LastIndexOf(',') ? '.' : ',';
        else if (dots == 1) decimalSeparator = '.';
        else if (commas == 1) decimalSeparator = ',';
        string integerPart = t, fraction = "";
        if (decimalSeparator is char sep) { int i = t.LastIndexOf(sep); integerPart = t[..i]; fraction = t[(i + 1)..]; }
        integerPart = integerPart.Replace(".", "").Replace(",", "");
        if (fraction.Contains('.') || fraction.Contains(',')) return null;
        string canonical = integerPart + (fraction.Length > 0 ? "." + fraction : "") + (e >= 0 ? "E" + exponent.ToString(CultureInfo.InvariantCulture) : "");
        if (!double.TryParse(canonical, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
        bool integer = decimalSeparator is null && e < 0;
        double half = integer ? 0 : 0.5 * Math.Pow(10, exponent - fraction.Length);
        return new Printed(sign * value, half, integer, token);
    }

    public enum Outcome { Equal, WithinTolerance, Rounding, Number, Text }

    public sealed record NumberDifference(int Index, Printed A, Printed B, double Delta, double Allowance, Outcome Outcome);

    /// <summary>Compares two printed texts. Returns Text when the skeletons differ, otherwise the worst outcome of the numbers.</summary>
    public static (Outcome Outcome, List<NumberDifference> Numbers) Compare(string a, string b, Quantity quantity)
    {
        if (a == b) return (Outcome.Equal, []);
        var (sa, na) = Split(a); var (sb, nb) = Split(b);
        if (sa != sb || na.Count != nb.Count) return (Outcome.Text, []);
        var differences = new List<NumberDifference>(); var worst = Outcome.Equal;
        for (int i = 0; i < na.Count; i++)
        {
            var x = na[i]; var y = nb[i];
            if (x.Value == y.Value) continue;
            double delta = Math.Abs(x.Value - y.Value), tolerance = quantity.Allowance(x.Value, y.Value), print = x.HalfUnit + y.HalfUnit;
            // The printed values are decimal: their binary difference may exceed the printed step by a few ulps (12,36 − 12,35).
            double magnitude = Math.Max(Math.Abs(x.Value), Math.Abs(y.Value)), slack = print > 0 ? 8 * (Math.BitIncrement(magnitude) - magnitude) : 0;
            var outcome = delta <= tolerance ? Outcome.WithinTolerance : delta <= tolerance + print + slack ? Outcome.Rounding : Outcome.Number;
            differences.Add(new(i, x, y, delta, tolerance + print, outcome));
            if (outcome > worst) worst = outcome;
        }
        // Equal values written differently (1,50 against 1,5): same number, different text.
        if (worst == Outcome.Equal) worst = Outcome.Text;
        return (worst, differences);
    }

    public static string Skeleton(string text) => Split(text).Skeleton;
}
