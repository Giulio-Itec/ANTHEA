using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Design.Sections;
using GPC.Geometry;

namespace Anthea.ConcreteWorkspace;

/// <summary>Saved DXF coordinates mapped to GPC. Geometry, validation and meshing belong to CreateFree.</summary>
public static class ConcreteFreeSectionAdapter
{
    public const string Shape = "DXF", Key = "sezione_dxf";
    public static ConcreteTemplateGeometry Create(JsonObject input)
    {
        var source = input[Key] as JsonObject ?? throw new ArgumentException("Importare un file DXF prima del calcolo.");
        if (source["versione"]?.ToString() != "1") throw new ArgumentException("Versione della sezione DXF non riconosciuta.");
        double Number(JsonNode? value) => J.Number(value) is double n && double.IsFinite(n) ? n : throw new ArgumentException("Coordinate o diametro DXF non validi.");
        Point2d[] Polygon(JsonNode? node) => node is JsonArray points && points.Count <= ConcreteSectionTemplates.FreeSectionMaximumVertices
            ? points.Select(p => p is JsonArray { Count: 2 } xy ? new Point2d(Number(xy[0]), Number(xy[1])) : throw new ArgumentException("Vertice DXF non valido.")).ToArray()
            : throw new ArgumentException("Contorno DXF assente o con troppi vertici.");
        var holes = source["fori"] as JsonArray ?? throw new ArgumentException("Elenco dei fori DXF non valido.");
        if (holes.Count > 100) throw new ArgumentException("Sono ammessi al massimo 100 fori.");
        var rows = input["barre_manuali"] as JsonArray ?? source["barre"] as JsonArray ?? throw new ArgumentException("Barre DXF assenti.");
        if (rows.Count > ConcreteSectionTemplates.FreeSectionMaximumBars) throw new ArgumentException("Troppe barre nella sezione DXF.");
        var bars = rows.Select(b => new ConcreteFreeBar(Number(b?["x"]), Number(b?["y"]), Number(b?["phi"]))).ToArray();
        return ConcreteSectionTemplates.CreateFree(new(Polygon(source["contorno"]), holes.Select(h => (IReadOnlyList<Point2d>)Polygon(h)).ToArray(), bars));
    }
}
