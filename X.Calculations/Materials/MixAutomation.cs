namespace Materiali;

/// <summary>
/// Facciata dei limiti di composizione UNI 11104 e dell'aria per XF2-XF4 (refactoring F2.9): stessi nomi e firme di prima, nessun
/// calcolo. I corpi, con le citazioni della norma (registro D7-e), sono in <see cref="DurabilityLegacy.AtecapMix"/>.
/// </summary>
public static class AtecapMix
{
    public static (double? Ratio,int? Cement) Limits(string code) => DurabilityLegacy.AtecapMix.Limits(code);
    public static (double? Ratio,int? Cement) Required(Exposure[] active) => DurabilityLegacy.AtecapMix.Required(active);
    public static double? Air(Exposure[] active, double dmax) => DurabilityLegacy.AtecapMix.Air(active, dmax);
}
