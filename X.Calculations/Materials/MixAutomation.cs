using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata dei limiti di composizione UNI 11104 e dell'aria per XF2-XF4 (refactoring F2.9): stessi nomi e firme di prima; nessun
/// calcolo, delega a <see cref="ConcreteDurabilityAdapter"/>. Il motore facoltativo finale (F2.9-D2) è uscito dal prodotto con la
/// decisione D3 del 15/10/2026: le prove agganciano il legacy da tests/ANTHEA.Legacy.
/// Le citazioni della norma (registro D7-e) sono nel nucleo legacy e in GPCChecker.Concrete.
/// </summary>
public static class AtecapMix
{
    public static (double? Ratio,int? Cement) Limits(string code) => ConcreteDurabilityAdapter.MixLimits(code);
    public static (double? Ratio,int? Cement) Required(Exposure[] active) => ConcreteDurabilityAdapter.Mix(active);
    public static double? Air(Exposure[] active, double dmax) => ConcreteDurabilityAdapter.Air(active, dmax);
}
