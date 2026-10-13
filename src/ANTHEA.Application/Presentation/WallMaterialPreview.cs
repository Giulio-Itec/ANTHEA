using System.Text.Json.Nodes;
namespace Anthea.Application.Presentation;

public sealed record WallMaterialPreview(IReadOnlyDictionary<string, string> Fields, string CoverDescription)
{
    public static WallMaterialPreview Read(JsonObject data)
    {
        var input = RetainingWall.MaterialSectionInput(data);
        var values = ConcreteMaterials.DesignValues(input, J.Obj(("normativa", "NTC 2018")));
        var fields = new Dictionary<string, string> { ["__fcd"] = WallSheetPresentation.Number(values.Fcd),
            ["__fyd"] = WallSheetPresentation.Number(values.Fyd), ["__ecm"] = WallSheetPresentation.Number(ConcreteMaterials.Concrete(input).Ecm) };
        double required = RetainingWall.RequiredCover(data); var m = data["materials"]!;
        string description = $"Minimo nominale {required:0.##} mm · adottato {m.D("cover"):0.##} mm · Ø massimo {RetainingWall.MaximumBarDiameter(data):0.##} mm. " + (m.D("cover") < required ? "Copriferro insufficiente." : "Minimo rispettato.");
        return new(fields, description);
    }
}
