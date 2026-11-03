using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Application.Documents;

/// <summary>A problem found when a document was opened: where (Progetto › Sezione › Foglio) and why.</summary>
public sealed record DocumentProblem(string Where, string Reason)
{
    /// <summary>«Ponte › Pila P1 › Verifica sezione: Sezione GPC e parametri dell'editor non corrispondono…».</summary>
    public override string ToString() => Where + ": " + Reason;
}

/// <summary>
/// Sheets and revisions that could not be read when the project was opened (docs/sviluppo/progetto-sistema.md, step 2b, 3/11/2026). Until
/// then a single such sheet refused the whole project. Now the node keeps its data exactly as in the file, with the reason in the property
/// <see cref="Property"/> while the project is open: the sheet opens read-only, the save writes it back as it was (no check, no
/// conversion), and the property is never written, so the problem is found again at the next opening. The rule of ConcreteSectionArchive
/// holds: a damaged section is never replaced by a reconstruction.
/// </summary>
public static class DocumentQuarantine
{
    /// <summary>The property of the reason, only in memory.</summary>
    public const string Property = "#non-leggibile";

    /// <summary>True when <paramref name="node"/> (a sheet or a revision) could not be read.</summary>
    public static bool IsQuarantined(JsonNode? node) => node is JsonObject obj && obj.ContainsKey(Property);

    /// <summary>Why <paramref name="node"/> could not be read, null when it was read.</summary>
    public static string? Reason(JsonNode? node) => node is JsonObject obj && obj[Property] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>Marks <paramref name="node"/> as not readable for <paramref name="reason"/> and records where it is.</summary>
    internal static void Mark(JsonObject node, string reason, List<DocumentProblem> problems)
    {
        if (IsQuarantined(node)) return;
        node[Property] = reason;
        problems.Add(new DocumentProblem(Where(node), reason));
    }

    /// <summary>The markers leave a document about to be written (a copy: the open document keeps them). Only the tree of projects,
    /// sections, sheets and revisions is walked: the data of the sheets, the models and the pool of the revisions carry no marker.</summary>
    internal static void Strip(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            obj.Remove(Property);
            foreach (var pair in obj)
                if (pair.Key is not ("dati" or "archivio_revisioni" or "modelli_calcolo")) Strip(pair.Value);
        }
        else if (node is JsonArray array)
            foreach (var child in array) Strip(child);
    }

    /// <summary>The names from the project down to <paramref name="node"/>: «Ponte › Pila P1 › Verifica sezione».</summary>
    public static string Where(JsonObject node)
    {
        var names = new List<string>();
        for (JsonNode? current = node; current is not null; current = current.Parent)
        {
            if (current is not JsonObject obj) continue;
            if (obj.ContainsKey("modulo_id")) names.Add(obj.S("nome") is { Length: > 0 } name ? name : obj.S("modulo_id"));
            else if (obj.ContainsKey("numero") && obj.ContainsKey("contesto")) names.Add("revisione " + obj["numero"]?.ToJsonString());
            else if (obj.S("nome") is { Length: > 0 } section && (obj.ContainsKey("strutture") || obj.ContainsKey("fogli"))) names.Add(section);
        }
        names.Reverse();
        return names.Count == 0 ? "documento" : string.Join(" › ", names);
    }
}
