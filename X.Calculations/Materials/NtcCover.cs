using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata del copriferro NTC 2018 con la Circolare 2019 (refactoring F2.9): stessi nomi e firme di prima; nessun calcolo, delega a
/// <see cref="ConcreteDurabilityAdapter"/>. Il motore facoltativo finale (F2.9-D2) è uscito dal prodotto con la decisione D3 del
/// 15/10/2026: le prove agganciano il legacy da tests/ANTHEA.Legacy. DefaultCmin, senza chiamanti di produzione (F2.9-D5), è
/// passata con la stessa decisione in tests/ANTHEA.Legacy (LegacyNtcCover), insieme al nucleo legacy.
/// </summary>
public static class NtcCover
{
    public static int Severity(string code) => ConcreteDurabilityAdapter.Severity(code);
    public static NtcCoverResult Calculate(Exposure[] values, double fck, CoverInput p, bool plate, bool coverQuality, double? pertinentCmin = null)
        => ConcreteDurabilityAdapter.NtcCover(values, fck, p, plate, coverQuality, pertinentCmin);
}
