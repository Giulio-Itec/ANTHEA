using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata dei limiti di composizione UNI 11104 e dell'aria per XF2-XF4 (refactoring F2.9): stessi nomi e firme di prima, più il
/// motore facoltativo finale; nessun calcolo, delega a <see cref="ConcreteDurabilityAdapter"/> (motore predefinito senza argomento).
/// Le citazioni della norma (registro D7-e) sono nel nucleo legacy e in GPCChecker.Concrete.
/// </summary>
public static class AtecapMix
{
    public static (double? Ratio,int? Cement) Limits(string code, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.MixLimits(code, engine);
    public static (double? Ratio,int? Cement) Required(Exposure[] active, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.Mix(active, engine);
    public static double? Air(Exposure[] active, double dmax, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.Air(active, dmax, engine);
}
