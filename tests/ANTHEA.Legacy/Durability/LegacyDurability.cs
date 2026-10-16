// Spostato da X.Calculations/Materials/Durability.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: Strength ed EffectiveWater
// non hanno chiamanti di produzione (F2.9-D5). Codice storico conservato per le prove, fuori dall'installer.
// ValidateExposure, dallo stesso file (commit 8b9ce6d3), con la decisione E5 del 15/10/2026 (F2.11c): la usano solo le prove; delega
// all'adattatore come prima.
using Anthea.Calculations;

namespace Materiali;

/// <summary>Parte della facciata Materiali.Durability senza chiamanti di produzione, con i corpi invariati: Strength ed EffectiveWater
/// delegano al nucleo legacy, ValidateExposure all'adattatore della libreria.</summary>
public static class LegacyDurability
{
    // F2.9-D5: senza chiamanti di produzione, resta nel nucleo legacy fino a F2.11 (elenco ammesso della prova 11i).
    public static string Strength(int fck) => DurabilityLegacy.Durability.Strength(fck);
    // F2.9-D5: usata solo dai controlli di riferimento (supporto/test/Shared/DurabilityReferenceChecks.cs); resta nel nucleo legacy fino a F2.11.
    public static double EffectiveWater(double total, double absorbed) => DurabilityLegacy.Durability.EffectiveWater(total, absorbed);
    // E5 (F2.11c): usata solo dalle prove dell'adattatore (tests/ConcreteLibraryAdapter.Checks/Durability.cs).
    public static void ValidateExposure(Exposure[] values) => ConcreteDurabilityAdapter.Validate(values);
}
