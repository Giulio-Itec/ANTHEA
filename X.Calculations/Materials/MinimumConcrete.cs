namespace Materiali;

/// <summary>
/// Facciata della classe minima di resistenza per esposizione, UNI 11104 (refactoring F2.9): stessi nomi e firme di prima, nessun
/// calcolo. I corpi, con le citazioni della norma (registro D7-e), sono in <see cref="DurabilityLegacy.MinimumConcrete"/>.
/// </summary>
public static class MinimumConcrete
{
    public static int Fck(string code) => DurabilityLegacy.MinimumConcrete.Fck(code);
    public static int Required(Exposure[] exposures) => DurabilityLegacy.MinimumConcrete.Required(exposures);
    public static string Label(int fck) => DurabilityLegacy.MinimumConcrete.Label(fck);
}
