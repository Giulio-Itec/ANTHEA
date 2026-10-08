using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata della classe minima di resistenza per esposizione, UNI 11104 (refactoring F2.9): stessi nomi e firme di prima, più il
/// motore facoltativo finale; nessun calcolo, delega a <see cref="ConcreteDurabilityAdapter"/> (motore predefinito senza argomento).
/// Le citazioni della norma (registro D7-e) sono nel nucleo legacy e in GPCChecker.Concrete.
/// </summary>
public static class MinimumConcrete
{
    public static int Fck(string code, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.MinimumFck(code, engine);
    public static int Required(Exposure[] exposures, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.MinimumStrength(exposures, engine);
    public static string Label(int fck, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.Label(fck, engine);
}
