namespace Materiali;

/// <summary>
/// Facciata del copriferro NTC 2018 con la Circolare 2019 (refactoring F2.9): stessi nomi e firme di prima, nessun calcolo.
/// I corpi sono in <see cref="DurabilityLegacy.NtcCover"/>.
/// </summary>
public static class NtcCover
{
    public static int Severity(string code) => DurabilityLegacy.NtcCover.Severity(code);
    public static double DefaultCmin(Exposure[] values) => DurabilityLegacy.NtcCover.DefaultCmin(values);
    public static NtcCoverResult Calculate(Exposure[] values, double fck, CoverInput p, bool plate, bool coverQuality, double? pertinentCmin = null)
        => DurabilityLegacy.NtcCover.Calculate(values, fck, p, plate, coverQuality, pertinentCmin);
}
