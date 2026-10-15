using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata della classe minima di resistenza per esposizione, UNI 11104 (refactoring F2.9): stessi nomi e firme di prima; nessun
/// calcolo, delega a <see cref="ConcreteDurabilityAdapter"/>. Il motore facoltativo finale (F2.9-D2) è uscito dal prodotto con la
/// decisione D3 del 15/10/2026: le prove agganciano il legacy da tests/ANTHEA.Legacy.
/// Le citazioni della norma (registro D7-e) sono nel nucleo legacy e in GPCChecker.Concrete.
/// </summary>
public static class MinimumConcrete
{
    public static int Fck(string code) => ConcreteDurabilityAdapter.MinimumFck(code);
    public static int Required(Exposure[] exposures) => ConcreteDurabilityAdapter.MinimumStrength(exposures);
    public static string Label(int fck) => ConcreteDurabilityAdapter.Label(fck);
}
