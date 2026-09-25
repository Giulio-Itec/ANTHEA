namespace X.Core;

/// <summary>Common outcome semantics for UI cards and exported verification summaries.</summary>
public sealed record VerificationSummary(string Title, int Total, int Completed, int Missing, bool? Passed,
    double? Ratio, string Governing, string Status)
{
    public static VerificationSummary Create(string title, int total, IEnumerable<(string Name, double? Ratio, bool? Passed)> checks)
    {
        var rows = checks.ToArray();
        static bool Numeric(double? ratio) => ratio is double value && double.IsFinite(value);
        var numeric = rows.Where(r => Numeric(r.Ratio)).OrderByDescending(r => r.Ratio).ToArray();
        int completed = rows.Count(r => Numeric(r.Ratio) || r.Passed.HasValue), missing = Math.Max(0, total - completed);
        bool failed = rows.Any(r => r.Passed == false || Numeric(r.Ratio) && r.Ratio > 1);
        bool? passed = total == 0 ? null : failed ? false : missing > 0 ? null : true;
        return new(title, total, completed, missing, passed, numeric.FirstOrDefault().Ratio,
            numeric.Length > 0 ? numeric[0].Name : rows.FirstOrDefault(r => r.Passed == false).Name ?? "—",
            total == 0 ? "NESSUNA AZIONE" : passed == false ? "NON VERIFICATA" : passed is null ? "DA COMPLETARE" : "VERIFICATA");
    }
}
