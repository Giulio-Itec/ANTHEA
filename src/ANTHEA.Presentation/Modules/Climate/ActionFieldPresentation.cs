using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Application.Modules;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Help;

namespace Anthea.Presentation.Modules.Climate;

/// <summary>
/// Presentation of the fields of the Actions sheets (review of the interface, 2/11/2026): the numbers with the decimal separator of the
/// application, an error under a number field that holds text, the «?» of the guide of the sheet. No calculation: the document keeps the GPC
/// contract and what the user typed.
/// </summary>
internal static class ActionFieldPresentation
{
    internal static FieldSpec Spec(ClimateField f,string module)=>new(f.Key,f.Label,f.Unit,Choices:f.Choices,Bool:f.Boolean,Wide:f.Choices is not null||!f.Numeric&&!f.Boolean,
        HelpTopic:HelpTopics.ActionsGuide(module)?.Uri,Write:f.Numeric?FieldWriteRule.NumberOrText:FieldWriteRule.RawText,Multiline:!f.Numeric&&!f.Boolean&&f.Choices is null,ToolTip:f.Help);

    /// <summary>The numbers of <paramref name="values"/>, the display copy written by a form, as text with the separator of the application.</summary>
    internal static JsonObject Localize(JsonObject values,IEnumerable<ClimateField> fields)
    {
        foreach(var f in fields.Where(f=>f.Numeric))
            if(Value(values[f.Key]) is { } number)values[f.Key]=number.ToString(CultureInfo.CurrentCulture);
        return values;
    }

    /// <summary>A number of the document with the separator of the application; any other value as it is.</summary>
    internal static string Text(JsonNode? node)=>Value(node)?.ToString(CultureInfo.CurrentCulture)??node?.ToString()??"";

    private static double? Value(JsonNode? node)=>node is JsonValue v&&v.GetValueKind()==JsonValueKind.Number&&double.TryParse(v.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture,out double d)?d:null;

    /// <summary>Error under a number field whose text is not a number, or not an integer: the text stays in the field and in the document.</summary>
    internal sealed class NumberValidator(IReadOnlyList<ClimateField> fields):IFieldValidator
    {
        public IEnumerable<FieldError> Validate(JsonObject values)
        {
            foreach(var f in fields.Where(f=>f.Numeric))
            {
                double? number=Value(values[f.Key])??Value(ModuleSheetPreparation.NumericText(values[f.Key]?.ToString()??""));
                if(number is null)yield return new(f.Key,f.Integer?"Inserire un numero intero.":"Inserire un numero (separatore decimale virgola o punto).");
                else if(f.Integer&&(number!=Math.Truncate(number.Value)||Math.Abs(number.Value)>int.MaxValue))yield return new(f.Key,"Inserire un numero intero.");
            }
        }
    }
}
