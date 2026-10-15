// Spostato da X.Calculations/Materials/NtcCover.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: DefaultCmin non ha chiamanti
// di produzione (F2.9-D5). Codice storico conservato per le prove, fuori dall'installer.
namespace Materiali;

/// <summary>Parte della facciata Materiali.NtcCover senza chiamanti di produzione, con il corpo invariato: delega al nucleo legacy.</summary>
public static class LegacyNtcCover
{
    // F2.9-D5: senza chiamanti di produzione, resta nel nucleo legacy fino a F2.11 (elenco ammesso della prova 11i).
    public static double DefaultCmin(Exposure[] values) => DurabilityLegacy.NtcCover.DefaultCmin(values);
}
