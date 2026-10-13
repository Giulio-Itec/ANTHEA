namespace Anthea.Calculations;

internal static class LegacyNumerics
{
    public static double Sum(IEnumerable<double> values)
    {
        // Somma compensata: corrisponde alla somma accurata delle versioni Python recenti.
        double sum = 0, correction = 0;
        foreach (var value in values) { var next = sum + value; correction += Math.Abs(sum) >= Math.Abs(value) ? (sum - next) + value : (value - next) + sum; sum = next; }
        return sum + correction;
    }
}
