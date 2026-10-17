using System.Text.Json.Nodes;

namespace Anthea.Application.Stratigraphy;

/// <summary>The status line of the stratigraphy sheet and whether its profile is drawn (a document that is not valid is not drawn).</summary>
public sealed record StratigraphyStatus(string Text, bool Drawn);

/// <summary>
/// What the stratigraphy sheet derives from its document, without WPF (F5.6b): moved from StratigraphyWorkspace, which calls it, and
/// used by the ViewModel of the sheet. No calculation: the status is the result of <see cref="StratigraphyDocument.Warnings"/>.
/// </summary>
public static class StratigraphyPresentation
{
    /// <summary>The status of a valid document without warnings.</summary>
    public const string Valid = "Dati validi. Il modulo non esegue verifiche geotecniche.";
    /// <summary>Caption of the messages of the actions on the layers.</summary>
    public const string Caption = "Stratigrafia";
    /// <summary>Caption of the question that removes the minima of a layer.</summary>
    public const string MinimaCaption = "Valori minimi";
    /// <summary>The question that removes the minima of a layer.</summary>
    public const string MinimaRemoval = "Rimuovere i valori minimi di questo strato?";
    /// <summary>The note shown in place of the minima of a layer that has none.</summary>
    public const string MinimaNote = "Non assegnati. Non vengono copiati dai medi.";

    /// <summary>The behaviours of a layer for the transfer to the verification modules, in the order of the choice.</summary>
    public static IReadOnlyList<string> Behaviors() => ["Da definire", "Granulare", "Coesivo"];

    /// <summary>
    /// The status of the sheet after every change (Refresh of the historic view): the warnings, one per line, or <see cref="Valid"/>; a
    /// document that is not valid gives the error to correct before saving and is not drawn.
    /// </summary>
    public static StratigraphyStatus Status(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            var warnings = StratigraphyDocument.Warnings(data);
            return new(warnings.Count == 0 ? Valid : string.Join("\n", warnings), true);
        }
        catch (ArgumentException error) { return new("Correggere prima di salvare: " + error.Message, false); }
    }

    /// <summary>The item a choice shows when its key is missing or null (presentation only: nothing is written until the user chooses).</summary>
    public static string? DefaultChoice(string key) => key switch
    {
        "scenario" => "Medi",
        "soil_type" => "Non classificato",
        "behavior" => "Da definire",
        _ => null
    };

    /// <summary>
    /// «Ultimo strato esteso indefinitamente»: the layer becomes of indefinite extent without thickness, or finite again with a thickness
    /// of 1 m, written as a number.
    /// </summary>
    public static void SetInfinite(JsonObject layer, bool infinite)
    {
        ArgumentNullException.ThrowIfNull(layer);
        layer["infinite"] = infinite;
        layer["thickness"] = infinite ? null : JsonValue.Create(1.0);
    }
}
