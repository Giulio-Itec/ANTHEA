using System.Text.Json.Nodes;
namespace Anthea.Application.Materials;

/// <summary>Owned values for editors, projected from the common calculation catalog.
/// Never exposes a native material instance or mutable shared selector.</summary>
public static class MaterialInputCatalog
{
    public static JsonObject[] Concrete(string standard = "NTC 2018") => ConcreteMaterialCatalog.Concrete(standard);
    public static JsonObject[] Steel(bool tendons, string standard = "EN 1992-1-1") => ConcreteMaterialCatalog.Steel(tendons, standard);
    /// <summary>Acciaio salvato con il nome di un materiale del catalogo della norma ma con valori diversi da quelli attuali (DET-3): il testo, oppure null.</summary>
    public static string? SteelDifference(JsonObject input, string standard) => RebarMaterial.CatalogueDifference(input, standard);
    public static (string Name, double Fck)[] MaterialSheetClasses() => ConcreteMaterialCatalog.MaterialSheetClasses();
    public static string[] ConcreteDiagrams => ConcreteMaterials.ConcreteDiagrams.ToArray();
}

public sealed record MaterialEditorPreview(MaterialDiagram Curves, string Description)
{
    public static MaterialEditorPreview Read(string type, JsonObject material, JsonObject input, JsonObject workspace)
    {
        var curves = MaterialDiagram.Create(type, material, input, workspace);
        string description = "Proprietà caratteristiche dal materiale; valori di progetto con i coefficienti del foglio.";
        if (type == "Calcestruzzo")
        {
            var native = ConcreteMaterials.Concrete(material);
            description = $"Ecm = {EngineeringFormat.Number(native.E)} MPa\nεc,y = {EngineeringFormat.Number(Math.Abs(native.StrainYCompression) * 1000)} ‰\nεc,u = {EngineeringFormat.Number(Math.Abs(native.StrainUCompression) * 1000)} ‰";
        }
        return new(curves, description);
    }
}
