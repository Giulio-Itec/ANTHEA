using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Anthea.ConcreteWorkspace;
using GPC.Design.Sections;
using GPC.Geometry;
using netDxf;
using netDxf.Entities;
using netDxf.Units;

namespace Anthea.Application.Concrete;

public sealed record ConcreteDxfEntity(string Id, string Layer, string Kind, IReadOnlyList<double[]> Points,
    double[]? Center, double Diameter, string Error)
{
    public string Label => Id + " · " + Layer + " · " + Kind;
    public bool IsContour => Points.Count >= 3 && Error.Length == 0;
    public bool IsBar => Center is not null && Diameter > 0 && Error.Length == 0;
}
public sealed record ConcreteDxfDrawing(string FileName, string Sha256, string Unit, IReadOnlyList<ConcreteDxfEntity> Entities,
    IReadOnlyList<string> Notes)
{
    public IReadOnlyList<string> Contours => Entities.Where(e => e.IsContour).Select(e => e.Label).ToArray();
    public IReadOnlyList<string> Layers => Entities.Select(e => e.Layer).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyList<string> BarLayers => Entities.Where(e => e.IsBar).Select(e => e.Layer).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToArray();
}
/// <summary>Selected CAD names; a null holes layer means that no holes were selected. UI labels are not CAD identifiers.</summary>
public sealed record ConcreteDxfSelection(string Unit, string Outline, string? HolesLayer, string BarsLayer);
public sealed record ConcreteDxfPrepared(JsonObject Input, ConcreteGeometry Geometry, string Summary);

/// <summary>CAD reading and document adaptation only. netDxf decodes/tessellates the drawing; GPC constructs and validates the section.</summary>
public static class ConcreteDxfImport
{
    public const string NoHoles = "Nessuno", NoSelection = "— scegliere —";
    // File transport limit, in bytes (20 MiB); unrelated to section dimensions.
    public const int CurveSegments = 96, MaximumEntities = 10000, MaximumBytes = 20_971_520;
    public static IReadOnlyList<string> Units { get; } = ["mm", "cm", "m", "in", "ft"];
    private static DrawingUnits Unit(string value) => value switch
    {
        "mm" => DrawingUnits.Millimeters, "cm" => DrawingUnits.Centimeters, "m" => DrawingUnits.Meters,
        "in" => DrawingUnits.Inches, "ft" => DrawingUnits.Feet, _ => throw new ArgumentException("Indicare l’unità del disegno DXF.")
    };
    public static ConcreteDxfDrawing Read(string path)
    {
        var file = new FileInfo(path);
        if (file.Length > MaximumBytes) throw new ArgumentException("Il DXF supera 20 MB: esportare soltanto la sezione da importare.");
        return Read(File.ReadAllBytes(path), Path.GetFileName(path));
    }
    public static ConcreteDxfDrawing Read(byte[] bytes, string fileName)
    {
        if (bytes.Length > MaximumBytes) throw new ArgumentException("Il DXF supera 20 MB.");
        DxfDocument document;
        try { using var stream = new MemoryStream(bytes, false); document = DxfDocument.Load(stream) ?? throw new InvalidDataException("File non leggibile."); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { throw new ArgumentException("DXF non leggibile. Salvare in formato DXF 2000 o successivo. " + error.Message, error); }
        string unit = document.DrawingVariables.InsUnits switch { DrawingUnits.Millimeters => "mm", DrawingUnits.Centimeters => "cm", DrawingUnits.Meters => "m",
            DrawingUnits.Inches => "in", DrawingUnits.Feet => "ft", _ => NoSelection };
        var entities = new List<ConcreteDxfEntity>(); var notes = new List<string>(); int count = 0;
        void Add(EntityObject entity, string? parentLayer = null, int depth = 0)
        {
            if (++count > MaximumEntities) throw new ArgumentException("Il DXF contiene troppe entità: esportare soltanto la sezione.");
            string layer = entity.Layer.Name == "0" && parentLayer is not null ? parentLayer : entity.Layer.Name;
            string id = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (entity is Insert insert)
            {
                if (depth >= 8) throw new ArgumentException("Troppi livelli di blocchi annidati nel DXF.");
                foreach (var child in insert.Explode()) Add(child, layer, depth + 1);
                return;
            }
            string error = ""; double[][] points = []; double[]? center = null; double diameter = 0;
            if (entity is Polyline2D line)
            {
                if (!line.IsClosed) error = "Polilinea aperta: chiuderla nel disegno.";
                else if (line.Elevation != 0 || line.Normal != Vector3.UnitZ) error = "Contorno fuori dal piano XY a quota zero.";
                else if (line.Vertexes.Count > ConcreteSectionTemplates.FreeSectionMaximumVertices) error = "Troppi vertici nel contorno.";
                else points = line.PolygonalVertexes(CurveSegments, 0, 0).Select(p => new[] { p.X, p.Y }).ToArray();
            }
            else if (entity is Circle circle)
            {
                if (circle.Center.Z != 0 || circle.Normal != Vector3.UnitZ) error = "Cerchio fuori dal piano XY a quota zero.";
                else
                {
                    center = [circle.Center.X, circle.Center.Y]; diameter = circle.Radius * 2;
                    points = circle.ToPolyline2D(CurveSegments).Vertexes.Select(p => new[] { p.Position.X, p.Position.Y }).ToArray();
                }
            }
            else if (entity is Text or MText or Dimension or Point) return;
            else error = "Entità non utilizzabile: convertirla in polilinea 2D chiusa o cerchio.";
            entities.Add(new(id, layer, entity is Circle ? "Cerchio" : entity is Polyline2D ? "Polilinea" : entity.Type.ToString(), points, center, diameter, error));
        }
        foreach (var entity in document.Entities.All) Add(entity);
        if (!entities.Any(e => e.IsContour)) throw new ArgumentException("Nessun contorno utilizzabile. Usare polilinee 2D chiuse o cerchi nel piano XY a quota zero.");
        foreach (var group in entities.Where(e => e.Error.Length > 0).GroupBy(e => e.Layer))
            notes.Add("Layer «" + group.Key + "»: " + group.Count() + " entità non utilizzabili. " + group.First().Error);
        if (unit == NoSelection) notes.Add("Unità non dichiarata o non gestita nel DXF: sceglierla prima di importare.");
        notes.Add("Archi e contorni circolari discretizzati con " + CurveSegments + " suddivisioni per giro. Le barre conservano il diametro dei cerchi.");
        return new(Path.GetFileName(fileName), Convert.ToHexString(SHA256.HashData(bytes)), unit, entities, notes);
    }
    public static ConcreteDxfPrepared Prepare(JsonObject currentInput, ConcreteDxfDrawing drawing, ConcreteDxfSelection selection)
    {
        double Convert(double value) => UnitHelper.ConvertUnit(value, Unit(selection.Unit), DrawingUnits.Millimeters);
        var outer = drawing.Entities.SingleOrDefault(e => e.Label == selection.Outline && e.IsContour)
            ?? throw new ArgumentException("Scegliere il contorno esterno della sezione.");
        if (!drawing.BarLayers.Contains(selection.BarsLayer)) throw new ArgumentException("Scegliere il layer dei cerchi delle armature.");
        if (selection.HolesLayer is not null && !drawing.Layers.Contains(selection.HolesLayer)) throw new ArgumentException("Scegliere un layer dei fori presente nel disegno.");
        if (selection.HolesLayer is not null && selection.HolesLayer == selection.BarsLayer) throw new ArgumentException("Fori e armature devono avere layer distinti.");
        var holes = selection.HolesLayer is null ? [] : drawing.Entities.Where(e => e.Layer == selection.HolesLayer && e.Id != outer.Id).ToArray();
        if (holes.Any(e => !e.IsContour)) throw new ArgumentException("Il layer dei fori contiene entità non utilizzabili. " + holes.First(e => !e.IsContour).Error);
        var bars = drawing.Entities.Where(e => e.Layer == selection.BarsLayer && e.Id != outer.Id).ToArray();
        if (bars.Any(e => !e.IsBar)) throw new ArgumentException("Il layer delle armature deve contenere soltanto cerchi delle barre (escluso il contorno scelto).");
        Point2d[] Points(ConcreteDxfEntity e) => e.Points.Select(p => new Point2d(Convert(p[0]), Convert(p[1]))).ToArray();
        var native = ConcreteSectionTemplates.CreateFree(new(Points(outer), holes.Select(h => (IReadOnlyList<Point2d>)Points(h)).ToArray(),
            bars.Select(b => new ConcreteFreeBar(Convert(b.Center![0]), Convert(b.Center[1]), Convert(b.Diameter))).ToArray()));
        JsonArray Polygon(IEnumerable<Point2d> points) => new(points.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray());
        var input = (JsonObject)currentInput.DeepClone();
        input["shape"] = ConcreteFreeSectionAdapter.Shape; input.Remove("barre_manuali"); input["foro_presente"] = false;
        input["staffe_presenti"] = "No";
        input["width_mm"] = native.Width; input["height_mm"] = native.Height;
        input[ConcreteFreeSectionAdapter.Key] = new JsonObject
        {
            ["versione"] = 1, ["file"] = drawing.FileName, ["sha256"] = drawing.Sha256, ["unita_origine"] = selection.Unit,
            ["staffe_precedenti"] = currentInput[ConcreteFreeSectionAdapter.Key]?["staffe_precedenti"]?.DeepClone() ?? currentInput["staffe_presenti"]?.DeepClone() ?? JsonValue.Create("Sì"),
            ["layer_contorno"] = outer.Layer, ["layer_fori"] = selection.HolesLayer, ["layer_barre"] = selection.BarsLayer,
            ["suddivisioni_curve"] = CurveSegments,
            ["origine_x_mm"] = native.InputCentroid!.X, ["origine_y_mm"] = native.InputCentroid.Y,
            ["contorno"] = Polygon(native.Shape.Fill2d.Points),
            ["fori"] = new JsonArray((native.Shape.Holes2d ?? []).Select(h => (JsonNode)Polygon(h.Points)).ToArray()),
            ["barre"] = new JsonArray(native.Bars.Select(b => (JsonNode)new JsonObject { ["x"] = b.X, ["y"] = b.Y, ["phi"] = b.Diameter }).ToArray())
        };
        var geometry = ConcretePreview.PrepareGeometry(input);
        int unused = drawing.Entities.Count - 1 - holes.Length - bars.Length;
        string summary = $"{geometry.Width:0.###} × {geometry.Height:0.###} mm · {geometry.Holes.Count} fori · {geometry.Bars.Count} barre. Coordinate riferite al baricentro del calcestruzzo. Entità non importate: {unused}.";
        return new(input, geometry, summary);
    }
}
