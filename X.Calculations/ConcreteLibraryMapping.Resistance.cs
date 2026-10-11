using System.Collections.Frozen;
using GPC.Checkers.Concrete.Analysis;

namespace Anthea.Calculations;

/// <summary>
/// Strato di mappatura del punto resistente SLU (revisione di architettura R2, registro F2-17): motivi per cui la porta
/// <see cref="ISectionResistanceSolver"/> della libreria non restituisce un punto (testi inglesi di LegacySectionCalculation) → testi italiani
/// del modulo, nello stile di «Checker: punto resistente non trovato». Solo testi: nessuna formula. Un testo della libreria senza traduzione
/// è un errore di programma (<see cref="InvalidOperationException"/>). Tabella immutabile (FrozenDictionary, prova 5l).
/// </summary>
public static partial class ConcreteLibraryMapping
{
    static readonly FrozenDictionary<string, string> ResistanceRejections = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Missing domain point/strain plane."] = "punto resistente non trovato",
        ["Non-finite domain point."] = "punto resistente non finito",
        ["The returned point does not satisfy the search constraints."] = "punto resistente non convergente (vincoli della ricerca non rispettati)",
        ["Invalid working ratio."] = "tasso di lavoro non valido"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Testo italiano di una risposta della libreria senza punto resistente (stato diverso da Completed, oppure domanda nulla).</summary>
    public static string ResistanceRejection(SectionResistanceResponse response)
    {
        if (response.IsZeroDemand) return "Azione nulla";
        if (response.Diagnostics.Message is { } message && ResistanceRejections.TryGetValue(message, out var text)) return text;
        throw new InvalidOperationException($"Punto resistente: testo della libreria senza traduzione ({response.Diagnostics.Status}: «{response.Diagnostics.Message}»).");
    }

    /// <summary>Testi della libreria tradotti (per le prove della mappatura).</summary>
    public static IReadOnlyCollection<string> ResistanceRejectionTexts => ResistanceRejections.Keys;
}
