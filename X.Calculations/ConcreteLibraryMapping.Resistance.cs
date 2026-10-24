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
    internal static readonly FrozenDictionary<string, string> ResistanceRejections = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Missing domain point/strain plane."] = "punto resistente non trovato",
        ["Non-finite domain point."] = "punto resistente non finito",
        ["The returned point does not satisfy the search constraints."] = "punto resistente non convergente (vincoli della ricerca non rispettati)",
        // GPCChecker.Concrete 0.20 (62ecbc83/c611b709): piano di deformazione che non riproduce un punto entro i vincoli (registro AG-CA-03).
        ["The returned strain plane does not satisfy the search constraints."] = "punto resistente non convergente (piano di deformazione fuori dai vincoli della ricerca)",
        ["Invalid working ratio."] = "tasso di lavoro non valido"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Testo italiano di una risposta della libreria senza punto resistente (stato diverso da Completed, oppure domanda nulla).</summary>
    public static string ResistanceRejection(SectionResistanceResponse response)
    {
        if (response.IsZeroDemand) return "Azione nulla";
        if (response.Diagnostics.Message is { } message && ResistanceRejections.TryGetValue(message, out var text)) return text;
        throw new InvalidOperationException($"Punto resistente: testo della libreria senza traduzione ({response.Diagnostics.Status}: «{response.Diagnostics.Message}»).");
    }

    /// <summary>Stato della verifica del dominio con l'esito tipizzato della libreria «N fuori dal campo» dei criteri a N assegnato
    /// (<see cref="SectionResistanceResponse.AxialForceOutsideDomainMode"/>, SLU-2 del 15/10/2026): nessun punto esiste a quell'N, la sezione
    /// non è verificata per nessun momento. Prima: «Checker: punto resistente non convergente…», riepilogo «DA COMPLETARE».</summary>
    public const string AxialForceOutsideDomainStatus = "Fuori dominio (N fuori dal campo [NRd,c; NRd,t])";

    /// <summary>Stato della verifica del dominio con l'esito tipizzato «momento nullo» di «N costante»
    /// (<see cref="SectionResistanceResponse.ZeroMomentDemandMode"/>, SLU-2): N nel campo del dominio e momento di domanda nullo, tasso 0.</summary>
    public const string ZeroMomentDemandStatus = "Entro il dominio (momento nullo)";

    /// <summary>
    /// Stato di un punto resistente con un esito tipizzato a N assegnato (SLU-2), null per gli altri punti (stato dal tasso). Ogni esito ha il suo
    /// contratto: fuori dal campo con tasso maggiore di 1, momento nullo con tasso 0; un esito che non lo rispetta è un errore di programma.
    /// </summary>
    public static string? ResistanceOutcomeStatus(ResistancePoint point, double ratio)
    {
        if (point.AxialForceOutsideDomain)
            return ratio > 1 && point.AxialCompressionResistance is not null && point.AxialTensionResistance is not null ? AxialForceOutsideDomainStatus
                : throw new InvalidOperationException($"Punto resistente: esito «N fuori dal campo» con tasso {ratio} o senza il campo di N.");
        if (point.ZeroMomentDemand)
            return ratio == 0 ? ZeroMomentDemandStatus : throw new InvalidOperationException($"Punto resistente: esito «momento nullo» con tasso {ratio}.");
        return null;
    }

    /// <summary>Campo resistente di N di un esito «N fuori dal campo» in kN (NRd,c; NRd,t), null per gli altri punti.</summary>
    public static (double Compression, double Tension)? AxialForceRangeKn(ResistancePoint point)
        => point.AxialForceOutsideDomain && point.AxialCompressionResistance is double c && point.AxialTensionResistance is double t
            ? (KilonewtonsFromNewtons(c), KilonewtonsFromNewtons(t)) : null;

    /// <summary>Rifiuto delle resistenze a N assegnato (resistenza a N costante, palo di Broms, punto limite del M–χ) quando N è fuori dal campo
    /// del dominio (SLU-2): il campo nei numeri della cultura corrente, in kN, senza punto finale.</summary>
    public static string AxialForceOutsideRange(double compressionKn, double tensionKn)
        => $"N fuori dal campo resistente [NRd,c; NRd,t] = [{compressionKn:F2}; {tensionKn:F2}] kN";
}
