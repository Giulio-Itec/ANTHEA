using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.Stratigraphy;

/// <summary>
/// The fields of the stratigraphy sheet (F5.6b), one definition for the historic view (StratigraphyWorkspace, until it is archived) and
/// for <see cref="StratigraphyEditorViewModel"/>: keys and labels in the order of the sheet, moved without changes from the view. Every
/// text field keeps the rule of the view, the raw text into the document; a choice shows <see cref="FieldSpec.Default"/> while its key is
/// missing (<see cref="StratigraphyPresentation.DefaultChoice"/>) and writes the chosen item.
/// <para>The arrangement of the XAML view (F5.6b, view of 17/10) is that of the historic view: every label on its own row and the editor
/// on the whole width (<see cref="FieldSpec.Wide"/>); the descriptions, the sources and the notes on more lines
/// (<see cref="FieldSpec.Multiline"/>, decision S7), as the text boxes of the view with AcceptsReturn. The historic view reads only keys,
/// labels and choices.</para>
/// </summary>
public static class StratigraphyFields
{
    /// <summary>The check box of the extent of the last layer.</summary>
    public const string InfiniteLabel = "Ultimo strato esteso indefinitamente";
    /// <summary>The check box of the minima of the layer.</summary>
    public const string MinimaLabel = "Gestisci anche i valori minimi (opzionali)";
    /// <summary>Suffix of the labels of the parameters of the minima.</summary>
    public const string MinimumSuffix = " · minimo";

    /// <summary>The general data: choice of the parameters, name, description, ground elevation, water table and its depth.</summary>
    public static FieldSpec[] General() =>
    [
        Choice("scenario", "Parametri da usare nelle verifiche", StratigraphyDocument.Scenarios),
        Text("name", "Nome della stratigrafia"),
        Text("description", "Descrizione / ubicazione"),
        Text("ground_elevation", "Quota altimetrica piano campagna [m] · facoltativa"),
        Choice("water_state", "Falda", StratigraphyDocument.WaterStates),
        Text("water_depth", "Profondità falda [m] · positiva verso il basso"),
    ];

    /// <summary>The identity of the selected layer: name, family, behaviour, description, colour.</summary>
    public static FieldSpec[] Layer() =>
    [
        Text("name", "Denominazione"),
        Choice("soil_type", "Famiglia prevalente · retino automatico", StratigraphyDocument.SoilTypes),
        Choice("behavior", "Comportamento per il trasferimento ai moduli di verifica", StratigraphyPresentation.Behaviors()),
        Text("description", "Descrizione litologica"),
        Text("color", "Colore del profilo [#RRGGBB]"),
    ];

    /// <summary>The thickness of a finite layer.</summary>
    public static FieldSpec[] Thickness() => [Text("thickness", "Spessore [m]")];

    /// <summary>The parameters (γ, γsat, φ′, c′, cu), in the order of <see cref="StratigraphyDocument.Parameters"/>.</summary>
    public static (string Key, string Label)[] Parameters() =>
    [
        ("gamma", "Peso naturale γ [kN/m³]"), ("gamma_sat", "Peso dell’unità di volume saturo γsat [kN/m³]"), ("phi", "Angolo di resistenza efficace φ′ [°]"),
        ("c", "Coesione efficace c′ [kPa]"), ("cu", "Resistenza non drenata cu [kPa]"),
    ];

    /// <summary>The means: the parameters, their qualification and their source.</summary>
    public static FieldSpec[] Means() =>
    [
        .. Parameters().Select(p => Text(p.Key, p.Label)),
        Choice("qualification", "Qualificazione dei medi", StratigraphyDocument.Qualifications),
        Text("source", "Fonte dei medi / pagine"),
    ];

    /// <summary>The minima (in the «minimum» object of the layer): the parameters, their qualification, source and notes.</summary>
    public static FieldSpec[] Minima() =>
    [
        .. Parameters().Select(p => Text(p.Key, p.Label + MinimumSuffix)),
        Choice("qualification", "Qualificazione dei minimi", StratigraphyDocument.Qualifications),
        Text("source", "Fonte dei minimi / pagine"),
        Text("notes", "Note sui minimi"),
    ];

    /// <summary>The notes of the layer, after the parameters.</summary>
    public static FieldSpec[] Notes() => [Text("notes", "Note (specificare eventuali qualificazioni diverse per parametro)")];

    /// <summary>The label of the field <paramref name="key"/> of <paramref name="fields"/>.</summary>
    public static string Label(IEnumerable<FieldSpec> fields, string key)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return fields.Single(f => f.Key == key).Label;
    }

    /// <summary>The keys of the text boxes of the historic view with AcceptsReturn: on more lines (S7).</summary>
    private static bool OnMoreLines(string key) => key is "source" or "notes" or "description";

    /// <summary>
    /// A text field, label above and editor on the whole width. The φ′ of a layer keeps its label as tooltip: the calculation help of the key
    /// «phi» is the creep coefficient of the concrete (CalculationHelp.Field), another quantity.
    /// </summary>
    private static FieldSpec Text(string key, string label) =>
        new(key, label, Wide: true, Multiline: OnMoreLines(key), ToolTip: key == "phi" ? label : null);

    private static FieldSpec Choice(string key, string label, IEnumerable<string> items) =>
        new(key, label, Choices: [.. items], Wide: true, Default: StratigraphyPresentation.DefaultChoice(key));
}
