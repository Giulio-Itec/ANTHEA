using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata del copriferro NTC 2018 con la Circolare 2019 (refactoring F2.9): stessi nomi e firme di prima, più il motore facoltativo
/// finale; nessun calcolo, delega a <see cref="ConcreteDurabilityAdapter"/> (motore predefinito senza argomento).
/// </summary>
public static class NtcCover
{
    public static int Severity(string code, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.Severity(code, engine);
    // F2.9-D5: senza chiamanti di produzione, resta nel nucleo legacy fino a F2.11 (elenco ammesso della prova 11i).
    public static double DefaultCmin(Exposure[] values) => DurabilityLegacy.NtcCover.DefaultCmin(values);
    public static NtcCoverResult Calculate(Exposure[] values, double fck, CoverInput p, bool plate, bool coverQuality, double? pertinentCmin = null, DurabilityEngine? engine = null)
        => ConcreteDurabilityAdapter.NtcCover(values, fck, p, plate, coverQuality, pertinentCmin, engine);
}
