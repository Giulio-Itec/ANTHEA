// Spostato da X.Calculations/ConcreteReinforcementDesign.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: ramo legacy mai
// raggiungibile di SearchIndices (H2). Codice storico conservato per le prove, fuori dall'installer.
namespace Anthea.Calculations;

public static partial class LegacyConcreteReinforcementDesign
{
    /// <summary>Ramo legacy di ConcreteReinforcementDesign.SearchIndices: nessun chiamante passava <c>legacy: true</c>.
    /// In a limited run, sample across the entire sorted grid instead of exhausting the lightest prefix.</summary>
    internal static IEnumerable<int> SearchIndices(int planned, int budget)
    {
        int count = Math.Min(planned, budget);
        if (count == 1) { yield return planned - 1; yield break; }
        for (int i = 0; i < count; i++) yield return (int)((long)i * (planned - 1) / (count - 1));
    }
}
