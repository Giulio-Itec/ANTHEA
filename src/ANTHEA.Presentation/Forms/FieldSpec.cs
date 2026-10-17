using Anthea.Calculations;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;

namespace Anthea.Presentation.Forms;

/// <summary>
/// Definition of a field of a form (proposal F5, section 3.2): the former <c>Field</c> of X.Desktop Ui.cs, moved in F5.2 with
/// the same parameters, in the same order. The historic views still write <c>Field</c>, an alias of this record.
/// </summary>
/// <param name="Key">Key of the value in the <see cref="JsonObject"/> of the form.</param>
/// <param name="Label">Label of the field.</param>
/// <param name="Unit">Unit shown after the editor; empty when the value has no unit.</param>
/// <param name="Choices">Values of a choice; null for a text or a boolean field.</param>
/// <param name="Bool">Boolean field (check box).</param>
/// <param name="ReadOnly">Calculated or linked value: shown, never written by typing.</param>
/// <param name="Symbol">Symbol of the symbol column; empty for the symbols table of <see cref="FieldSymbols"/>.</param>
/// <param name="Wide">Label on its own row and editor on the whole width.</param>
/// <param name="HelpTopic">Identifier (URI) of the help topic of the field, when the key alone is ambiguous in the module
/// (e.g. «modello»): see <see cref="Help.HelpTopics.Find"/>. It replaces the WPF-bound topic object of the former Field.</param>
/// <param name="Note">Notice shown under the field while it returns a text (for example a choice with known limits).</param>
/// <param name="Default">Choice shown when the key is missing in the document (sheets saved before the field): presentation
/// only, the document does not change until the user chooses.</param>
/// <param name="Write">Rule that writes the text of the field into the document. The historic InputForm always writes the raw
/// text: the other rules are those of the views that write their fields by hand (proposal F5, section 3.2) and are never
/// unified during F5.</param>
/// <param name="Multiline">Text on more lines: the editor wraps, accepts the Enter key and scrolls beyond its maximum height
/// (decision S7 of 17/10/2026, the descriptions, sources and notes of the stratigraphy). Additive: false keeps the editor of
/// InputForm.</param>
/// <param name="ToolTip">Tooltip of the label and of the editor in place of the calculation help of the key
/// (<c>CalculationHelp.Field</c>), for a key whose help belongs to another quantity (the φ′ of the stratigraphy is not the creep
/// coefficient φ of the concrete). Additive: null keeps the tooltips of InputForm.</param>
public sealed record FieldSpec(string Key, string Label, string Unit = "", string[]? Choices = null, bool Bool = false, bool ReadOnly = false,
    string Symbol = "", bool Wide = false, string? HelpTopic = null, Func<JsonObject, string?>? Note = null, string? Default = null,
    FieldWriteRule Write = FieldWriteRule.RawText, bool Multiline = false, string? ToolTip = null);

/// <summary>How the text of a field goes into the document. The saved format is part of the equivalence: one rule per field.</summary>
public enum FieldWriteRule
{
    /// <summary>The raw text, as typed (<c>InputForm.Store</c>: <c>values[key] = J.Node(text)</c>).</summary>
    RawText,
    /// <summary>A number when the text is a finite number (point or comma), otherwise the text
    /// (<see cref="ModuleSheetPreparation.NumericText"/>, horizontal pile group).</summary>
    NumberOrText,
    /// <summary>Optional field: the key is removed when the text is empty or blank, otherwise <see cref="NumberOrText"/>
    /// (horizontal pile group).</summary>
    NumberOrTextRemoveEmpty,
}

/// <summary>The write rules of <see cref="FieldWriteRule"/>, without WPF.</summary>
public static class FieldWrite
{
    /// <summary>Writes <paramref name="value"/> (the text of a field, or the state of a boolean field) under <paramref name="key"/>.
    /// A boolean is always written as a JSON boolean, as in the historic views.</summary>
    public static void Apply(JsonObject values, string key, object value, FieldWriteRule rule)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (value is not string text || rule == FieldWriteRule.RawText) { values[key] = J.Node(value); return; }
        if (rule == FieldWriteRule.NumberOrTextRemoveEmpty && string.IsNullOrWhiteSpace(text)) { values.Remove(key); return; }
        values[key] = ModuleSheetPreparation.NumericText(text);
    }
}
