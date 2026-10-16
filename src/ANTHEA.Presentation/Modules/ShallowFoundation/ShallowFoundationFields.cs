using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Application.Presentation;
using Anthea.FoundationWorkspace;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.ShallowFoundation;

/// <summary>A choice of the sheet that writes the index of the item into «input» (Method, Shape, Condition, Profile, Compression).</summary>
public sealed record ShallowFoundationChoice(string Key, string Label, IReadOnlyList<string> Items);

/// <summary>A group of inputs whose state the sheet saves in «vista_fondazioni»: key and title of the expander.</summary>
public sealed record ShallowFoundationGroup(string Key, string Title);

/// <summary>
/// The fields of the shallow foundation sheet (F5.9b): the only definition of labels, units, items and columns, read by the historic
/// view FoundationWorkspace and by the ViewModel of the sheet, in the order of the view.
/// </summary>
public static class ShallowFoundationFields
{
    /// <summary>The group «Metodo e verifica» and the groups of the numbers, then the group of the stratigraphy.</summary>
    public static ShallowFoundationGroup MethodGroup { get; } = new("metodo", "Metodo e verifica");
    public static ShallowFoundationGroup GeometryGroup { get; } = new("Geometria", "Geometria");
    public static ShallowFoundationGroup ActionsGroup { get; } = new("Azioni", "Azioni");
    public static ShallowFoundationGroup SoilGroup { get; } = new("stratigrafia", "Stratigrafia");

    /// <summary>Expanders of the view whose state is not saved.</summary>
    public const string MethodOptionsTitle = "Opzioni del metodo";
    public const string ConcreteTitle = "Calcestruzzo · dati comuni del progetto";

    /// <summary>Title of the sheet and labels of its commands.</summary>
    public const string Title = "Fondazioni superficiali";
    public const string CollectInputs = "Raccogli input", ShowInputs = "Mostra input";

    /// <summary>The four choices of the group «Metodo e verifica».</summary>
    public static IReadOnlyList<ShallowFoundationChoice> MethodChoices { get; } = [
        new("Method", "Metodo", FoundationDocument.Methods), new("Shape", "Geometria", FoundationDocument.Shapes),
        new("Condition", "Drenaggio", FoundationDocument.Conditions), new("Profile", "Verifica", FoundationDocument.Profiles)];

    /// <summary>The choice of the compressibility (options of the method).</summary>
    public static ShallowFoundationChoice Compression { get; } = new("Compression", "Compressibilità", FoundationDocument.Compressions);

    /// <summary>Numbers of the group «Geometria», with the labels of <see cref="FoundationDocument.Fields"/>.</summary>
    public static IReadOnlyList<FieldSpec> Geometry { get; } = Numbers(GeometryGroup.Key);

    /// <summary>Numbers of the group «Azioni», with the labels of <see cref="FoundationDocument.Fields"/>.</summary>
    public static IReadOnlyList<FieldSpec> Actions { get; } = Numbers(ActionsGroup.Key);

    /// <summary>The external surcharge, after the groups.</summary>
    public static FieldSpec Surcharge { get; } = new("Surcharge", "Sovraccarico esterno q [kPa]");

    /// <summary>Groundwater of the common data (generali), as the other geotechnical modules write it.</summary>
    public static IReadOnlyList<FieldSpec> Water { get; } = [new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda zf", "m")];

    /// <summary>Label of the name of the combination.</summary>
    public const string CombinationLabel = "Combinazione";

    /// <summary>The options of the method after the choice of the compressibility, in the order of the view.</summary>
    public static FieldSpec RelativeDensity { get; } = new("RelativeDensity", "Densità relativa Dr [0–1]");
    public static IReadOnlyList<FieldSpec> MethodOptions { get; } = [new("IncludeDepthFactors", "Includi fattori di profondità", Bool: true),
        new("RoughBase", "Base ruvida · EC7: δ ≥ φ′/2", Bool: true)];
    public static FieldSpec WaterUnitWeight { get; } = new("WaterUnitWeight", "Peso specifico acqua [kN/m³]");

    /// <summary>
    /// The shared concrete (sezione): class, fck and diagram. The items of the class are the catalogue, the class of the sheet when
    /// it is not in the catalogue, and «Personalizzato».
    /// </summary>
    public static FieldSpec[] Concrete(JsonObject section, IEnumerable<JsonObject> catalog) => [
        new("classe_cls", "Calcestruzzo", Choices: ShallowFoundationPresentation.ConcreteChoices(section, catalog)), new("fck_mpa", "Resistenza fck", "MPa"),
        new("cls_diagramma", "Diagramma", Choices: MaterialInputCatalog.ConcreteDiagrams)];

    /// <summary>Columns of the table of the layers of the survey examined; «__strato» is the letter of the layer.</summary>
    public static IReadOnlyList<FieldSpec> LayerColumns { get; } = [new("__strato", "Strato", ReadOnly: true), new("tipologia", "Tipologia", Choices: ["Granulare", "Coesivo"]),
        new("spessore", "Spessore S [m]"), new("peso_specifico", "γ [kN/m³]"), new("peso_specifico_saturo", "γsat [kN/m³]"), new("angolo_attrito", "φ′ [°]"),
        new("coesione_efficace", "c′ [kPa]"), new("coesione_non_drenata", "Cu [kPa]")];

    private static FieldSpec[] Numbers(string group) => FoundationDocument.Fields.Where(f => f.Group == group).Select(f => new FieldSpec(f.Key, f.Label)).ToArray();
}
