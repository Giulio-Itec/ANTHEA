// Regole della fessurazione decise dall'utente il 15/10/2026 (R1, R2, F2-7 punto 3): il prodotto le usa sempre; le prove le tolgono per
// confrontare la libreria con il motore storico a regole uguali (ConcreteLibraryAdapter.Checks, 5c) e per misurare cosa cambia (5n).
namespace Anthea.Calculations;

/// <summary>
/// Regole di GPCChecker.Concrete prima del 15/10/2026 nella fessurazione della libreria: τbms per durata con β = kt nel fib MC2010,
/// kt = 0,4 della DIN per ogni durata, zona tesa senza armatura senza esito. Uso: <c>using (PreviousCrackRules.Use()) { … }</c>.
/// </summary>
public static class PreviousCrackRules
{
    public static IDisposable Use() => EngineScope.Set<IPreviousCrackRules>(Rules.Instance);

    /// <summary>Regole del prodotto (nessun aggancio) fino alla fine dell'ambito, anche dentro un ambito di <see cref="Use"/>.</summary>
    public static IDisposable Off() => EngineScope.Set<IPreviousCrackRules>(null);

    /// <summary>Vero dentro un ambito di <see cref="Use"/>.</summary>
    public static bool Active => EngineHook.Get<IPreviousCrackRules>() is not null;

    sealed class Rules : IPreviousCrackRules
    {
        internal static readonly Rules Instance = new();
    }
}
