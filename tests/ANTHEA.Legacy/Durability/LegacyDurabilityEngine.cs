// Spostato da X.Calculations/ConcreteDurabilityAdapter.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: i 12 rami legacy
// dell'adattatore e la scelta del motore servono solo alle prove. Codice storico conservato per le prove, fuori dall'installer.
using System.Collections.Immutable;
using Materiali;

namespace Anthea.Calculations;

/// <summary>
/// Motore legacy della durabilità e dei copriferri agganciato all'adattatore (decisione D3 del 15/10/2026, modo b): le 12 operazioni di
/// <see cref="ConcreteDurabilityAdapter"/> delegano al nucleo legacy <see cref="DurabilityLegacy"/> con le stesse chiamate dei rami
/// legacy che l'adattatore aveva fino al commit 4144fe9e. Uso: <c>using (LegacyDurabilityEngine.Use(DurabilityEngine.Legacy)) { … }</c>;
/// nell'ambito ogni operazione dell'adattatore delega qui e la sonda registra «Legacy»; fuori dall'ambito, o con
/// <see cref="DurabilityEngine.Library"/> o null (prima il motore predefinito), la libreria.
/// </summary>
public sealed class LegacyDurabilityEngine : ILegacyDurability
{
    static readonly LegacyDurabilityEngine Instance = new();

    LegacyDurabilityEngine() { }

    /// <summary>Aggancia il motore richiesto fino alla fine dell'ambito, che rimette gli agganci precedenti: <see cref="DurabilityEngine.Legacy"/>
    /// il nucleo legacy, <see cref="DurabilityEngine.Library"/> o null la libreria (nessun aggancio, come prima il motore predefinito).</summary>
    public static IDisposable Use(DurabilityEngine? engine) => EngineScope.Set<ILegacyDurability>(engine == DurabilityEngine.Legacy ? Instance : null);

    ImmutableArray<Exposure> ILegacyDurability.Exposures() => DurabilityLegacy.Durability.Exposures;
    int ILegacyDurability.Severity(string code) => DurabilityLegacy.NtcCover.Severity(code);
    int ILegacyDurability.MinimumFck(string code) => DurabilityLegacy.MinimumConcrete.Fck(code);
    (double? Ratio, int? Cement) ILegacyDurability.MixLimits(string code) => DurabilityLegacy.AtecapMix.Limits(code);
    string ILegacyDurability.Label(int fck) => DurabilityLegacy.MinimumConcrete.Label(fck);
    void ILegacyDurability.Validate(Exposure[] values) => DurabilityLegacy.Durability.ValidateExposure(values);
    int ILegacyDurability.MinimumStrength(Exposure[] exposures) => DurabilityLegacy.MinimumConcrete.Required(exposures);
    (double? Ratio, int? Cement) ILegacyDurability.Mix(Exposure[] active) => DurabilityLegacy.AtecapMix.Required(active);
    double? ILegacyDurability.Air(Exposure[] active, double dmax) => DurabilityLegacy.AtecapMix.Air(active, dmax);
    int ILegacyDurability.StructuralClass(Exposure e, double fck, CoverInput p) => DurabilityLegacy.Durability.StructuralClass(e, fck, p);
    CoverResult ILegacyDurability.Cover(Exposure[] values, double fck, CoverInput p) => DurabilityLegacy.Durability.Cover(values, fck, p);
    NtcCoverResult ILegacyDurability.NtcCover(Exposure[] values, double fck, CoverInput p, bool plate, bool coverQuality, double? pertinentCmin)
        => DurabilityLegacy.NtcCover.Calculate(values, fck, p, plate, coverQuality, pertinentCmin);
}
