using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Application.Concrete;

/// <summary>
/// The texts of the section σ/ε of the domain panels of the RC section (UpdateSectionInspection of the historic view, moved in F5.10 S7): the
/// two solutions shown (the plane already calculated at the limit point, the analysis of the action entered), the messages of every state and the
/// values of the solution.
/// </summary>
public static class ConcreteInspectionPresentation
{
    /// <summary>The solutions of the choice above the section σ/ε, in order: the action entered (0), the limit point (1).</summary>
    public static ImmutableArray<string> States { get; } = ["Azione inserita", "Punto limite"];
    /// <summary>The solution chosen when the panel is built.</summary>
    public const int DefaultState = 1;
    /// <summary>The message before any solution.</summary>
    public const string NoSelection = "Selezionare una combinazione";
    /// <summary>The values while no solution is current.</summary>
    public const string NoSolution = "Nessuna soluzione aggiornata disponibile.";
    /// <summary>The message without a calculated row selected.</summary>
    public const string SelectCalculated = "Selezionare una combinazione calcolata.";

    /// <summary>The message while the solution is read or calculated.</summary>
    public static string Pending(bool limit) => limit ? "Lettura del piano già calcolato al limite…" : "Analisi dell’azione inserita…";
    /// <summary>The message of the solution shown.</summary>
    public static string Completed(bool limit) => limit ? "Piano nativo del punto resistente · nessuna nuova ricerca di Rd." : "Azione inserita completa · leggi dei materiali della sezione; compressione negativa.";
    /// <summary>The message (and the values) of a solution that could not be calculated.</summary>
    public static string Unavailable(string error) => "Soluzione non disponibile: " + error;

    /// <summary>
    /// The values of the solution: at the limit point the resistance of the check in the local axes, otherwise the action entered in the axes of
    /// the options of the panel.
    /// </summary>
    public static string Values(ConcreteResults.StressState state, ConcreteResults.Check check, ConcreteResults.Action action, bool limit, JsonObject options, ConcreteGeometry section)
    {
        ArgumentNullException.ThrowIfNull(check);
        var force = limit ? check.Resistance!.Value : action;
        return ConcreteResponsePresentation.InspectionText(state, force, limit ? "PUNTO LIMITE · assi locali" : "AZIONE INSERITA · assi " + options.S("assi"), section);
    }
}
