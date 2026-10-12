using System.Globalization;
using System.Text.Json;
using GPC.Converter;
using GPC.Converter.CivilNx;
using GPC.Geometry;
using GPC.Model.Sections;
using ShapeProperty = GPC.Model.Sections.SectionProperty;

namespace ANTHEA.ModelWorkspace;

public sealed record SectionPoint(double Y, double Z);
public sealed record SectionTriangle(SectionPoint A, SectionPoint B, SectionPoint C);
public sealed record SectionContour(SectionPoint[] Boundary, SectionPoint[][] Holes, int Part);
public sealed record SectionValue(string Name, double? Value, string Unit, string Origin)
{
    public string Display => Value?.ToString("G7", CultureInfo.CurrentCulture) ?? "—";
}
public sealed record SectionPartInfo(string Name, string Material, double Width, double Height, double Y, double Z)
{
    public SectionValue[] Dimensions { get; init; } = [];
}
public sealed record SectionAttribute(string Name, string Value);
public sealed record SectionMesh(SectionTriangle[] Triangles, string? Problem);

/// <summary>Read-only inspection projected from GPC. All section coordinates are in mm; no resistance calculation.</summary>
public sealed class ModelSectionDetail
{
    internal ModelSectionDetail(int id, string name, string type) { Id=id; Name=name; Type=type; }
    public int Id { get; }
    public string Name { get; }
    public string Type { get; }
    public string Label => $"{Id} · {Name}";
    public override string ToString() => Label;
    public SectionContour[] Contours { get; internal set; } = [];
    public SectionPartInfo[] Parts { get; internal set; } = [];
    public SectionValue[] Quantities { get; internal set; } = [];
    public SectionValue[] ReferenceValues { get; internal set; } = [];
    public SectionAttribute[] Attributes { get; internal set; } = [];
    public int[] Elements { get; internal set; } = [];
    public IReadOnlySet<int> ElementsWithUnresolvedEndOffset { get; internal set; } = new HashSet<int>();
    public string[] Materials { get; internal set; } = [];
    public string[] Diagnostics { get; internal set; } = [];
    public SectionPoint? Centroid { get; internal set; }
    public SectionPoint? Insertion { get; internal set; }
    public double? PrincipalAngleRadians { get; internal set; }
    public bool IsComposite { get; internal set; }
    public bool HasGeometry => Contours.Length>0;
    public string GeometryMeaning => IsComposite ? "Geometria fisica delle parti · proprietà non omogeneizzate" : "Geometria della sezione · proprietà GPC";
    public string OffsetMessage => Insertion==null ? "Punto di inserimento non risolto: volume dell’asta non disponibile." : "Punto di inserimento risolto rispetto alla geometria.";
    internal Lazy<SectionMesh> MeshFactory { get; set; } = new(() => new([], "Contorno non disponibile."));
    public SectionMesh Mesh => MeshFactory.Value;
    public bool CanExtrude(int element) => HasGeometry && Insertion!=null && !ElementsWithUnresolvedEndOffset.Contains(element) && Mesh.Problem==null;
}

public sealed class ModelSectionCatalog
{
    public IReadOnlyList<ModelSectionDetail> Sections { get; }
    private readonly IReadOnlyDictionary<int,ModelSectionDetail> byId;
    private ModelSectionCatalog(ModelSectionDetail[] sections) { Sections=Array.AsReadOnly(sections); byId=sections.ToDictionary(s=>s.Id); }
    public ModelSectionDetail? Find(int id) => byId.GetValueOrDefault(id);

    public static ModelSectionCatalog Create(ModelSnapshot model)
    {
        var sections=model.Sections.Select(property=>Read(model,property)).ToArray();
        return new(sections);
    }

    private static ModelSectionDetail Read(ModelSnapshot model, SectionProperty property)
    {
        var result=new ModelSectionDetail(property.Id,property.Name,property.Shape);
        result.Elements=model.Elements.Where(e=>ElementFamilies.IsLine(e.Type)&&e.Property==property.Id).Select(e=>e.Id).ToArray();
        // OFFS is preserved but not normalized to an analysis geometry yet. Never silently ignore an assignment.
        result.ElementsWithUnresolvedEndOffset=model.SourceTables?.TryGetValue("OFFS",out var offsets)==true
            ? result.Elements.Where(id=>offsets.TryGetProperty(id.ToString(CultureInfo.InvariantCulture),out _)).ToHashSet() : new HashSet<int>();
        result.Attributes=ModelSourceAttributes.Read(model,"SECT",property.Id).ToArray();
        result.Materials=ReadMaterials(model,result.Elements);
        try
        {
            SectionRecord source;
            var diagnostics=new List<string>();
            if(model.SourceTables?.TryGetValue("SECT",out var table)==true && table.TryGetProperty(property.Id.ToString(CultureInfo.InvariantCulture),out var raw))
            {
                if(!model.SourceTables.TryGetValue("UNIT",out var units)) throw new InvalidDataException("Unità della sorgente assenti: aggiornare gli attributi del modello.");
                // Parse each property independently so one unsupported/malformed property cannot hide the other sections.
                var tables=new Dictionary<string,JsonElement> { ["UNIT"]=units,
                    ["SECT"]=JsonSerializer.SerializeToElement(new Dictionary<string,JsonElement> { [property.Id.ToString(CultureInfo.InvariantCulture)]=raw }) };
                if(model.SourceTables.TryGetValue("SECTIONALL",out var all)) tables["SECTIONALL"]=all;
                var snapshot=MidasGpcDisplay.FromTables(tables);
                var batch=new CivilNxModelProfile().ReadSectionCatalog(snapshot,ModelConstructionStages.Identity(snapshot.Hash));
                source=batch.Sections.Single(s=>s.Id==property.Id.ToString(CultureInfo.InvariantCulture));
                diagnostics.AddRange(batch.Diagnostics.Select(d=>d.Code+": "+d.Message));
            }
            else if(property.Shape=="SB" && property.Width>0 && property.Height>0)
                source=new SectionRecord { Id=property.Id.ToString(CultureInfo.InvariantCulture),Name=property.Name,
                    Shape=SectionShapeKind.SolidRectangle,Dimensions=[property.Height*1000,property.Width*1000],
                    Reference=new() { IsUnresolved=!property.Centered } };
            else throw new NotSupportedException("Geometria della sezione non disponibile nella copia salvata. Aggiornare geometria e attributi.");
            var inspection=SectionShapeInspection.Read(source);
            result.IsComposite=inspection.HasMultipleMaterials;
            diagnostics.AddRange(inspection.Diagnostics.Select(d=>d.Code+": "+d.Message));
            if(result.ElementsWithUnresolvedEndOffset.Count>0) diagnostics.Add($"{result.ElementsWithUnresolvedEndOffset.Count} aste hanno offset di estremità non normalizzati: visualizzazione ad asse.");
            var shape=inspection.Shape;
            if(shape!=null)
            {
                var contours=new List<SectionContour>();
                for(int i=0;i<inspection.Parts.Count;i++)
                {
                    var part=inspection.Parts[i];
                    double tolerance=Math.Max(.00001,Math.Max(part.Shape.Width,part.Shape.Height)*.0001);
                    foreach(var outline in part.Shape.GetCurveOutlines())
                    {
                        var polygon=outline.ToShape(tolerance);
                        AddContours(polygon,part.X,part.Y,i,contours);
                        // A fresh polygon and mesh are used for graphics only. Existing calculation meshes remain untouched.
                    }
                }
                result.Contours=contours.ToArray();
                result.Parts=inspection.Parts.Select(p=>new SectionPartInfo(p.Name switch { "Steel girder"=>"Trave in acciaio", "Concrete slab"=>"Soletta in calcestruzzo", _=>p.Name },MaterialLabel(p.Material),p.Shape.Width,p.Shape.Height,p.X,p.Y) { Dimensions=PartDimensions(p.Shape) }).ToArray();
                if(shape.GetAvailability(ShapeProperty.Centroid)!=PropertyAvailability.NotAvailable)
                    result.Centroid=new(shape.Centroid.X,shape.Centroid.Y);
                if(result.Centroid is {} centroid && inspection.CentroidOffset is {} offset)
                    result.Insertion=new(centroid.Y-offset.X,centroid.Z-offset.Y);
                if(shape.GetAvailability(ShapeProperty.MomentsOfInertia)!=PropertyAvailability.NotAvailable && double.IsFinite(shape.AngleX1))
                    result.PrincipalAngleRadians=shape.AngleX1;
                result.Quantities=Quantities(shape,result.IsComposite);
                result.ReferenceValues=[new("G · Y",result.Centroid?.Y,"mm","Geometria GPC"),new("G · Z",result.Centroid?.Z,"mm","Geometria GPC"),
                    new("O · Y",result.Insertion?.Y,"mm","Riferimento della sorgente"),new("O · Z",result.Insertion?.Z,"mm","Riferimento della sorgente"),
                    new("Asse 1 da Y",result.PrincipalAngleRadians*180/Math.PI,"°","Geometria GPC")];
                result.MeshFactory=new(()=>Triangulate(inspection));
            }
            else if(source.Values is {} v)
                result.Quantities=[new("Area",v.Area,"mm²","Importata"),new("I₁₁",v.I11,"mm⁴","Importata"),new("I₂₂",v.I22,"mm⁴","Importata"),new("J torsionale",v.Torsion,"mm⁴","Importata"),new("Iω",v.Warping,"mm⁶","Importata")];
            result.Diagnostics=diagnostics.Distinct().ToArray();
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidDataException or InvalidOperationException or NotSupportedException or KeyNotFoundException or System.Runtime.Serialization.SerializationException)
        { result.Diagnostics=["Sezione non rappresentabile: "+ex.Message]; }
        return result;
    }

    static SectionValue[] PartDimensions(Section shape)
    {
        SectionValue V(string name,double value)=>new(name,value,"mm","Geometria GPC");
        return shape is SectionH h ? [V("Altezza anima",h.HeightWeb),V("Spessore anima",h.ThicknessWeb),
            V("Ala superiore · larghezza",h.LenghtTopFlange),V("Ala superiore · spessore",h.ThicknessTopFlange),
            V("Ala inferiore · larghezza",h.LenghtBottomFlange),V("Ala inferiore · spessore",h.ThicknessBottomFlange)] : [];
    }
    static string MaterialLabel(MaterialKind kind)=>kind switch {MaterialKind.Steel=>"Acciaio",MaterialKind.Concrete=>"Calcestruzzo",_=>"Vedi materiale assegnato"};
    static SectionValue[] Quantities(ISectionShape shape,bool composite)
    {
        string suffix=composite ? " · fisica" : "";
        SectionValue Value(string name,ShapeProperty property,Func<double> read,string unit)
        {
            var availability=shape.GetAvailability(property);
            string origin=availability switch {PropertyAvailability.Exact=>"Calcolata GPC",PropertyAvailability.Given=>"Assegnata",PropertyAvailability.Approximate=>"Approssimata GPC",PropertyAvailability.Numerical=>"Calcolo numerico richiesto",_=>"Non disponibile"};
            // Inspection must not launch an expensive torsion/warping solve merely by opening the window.
            double? number=availability is PropertyAvailability.NotAvailable or PropertyAvailability.Numerical ? null : read();
            if(number.HasValue&&!double.IsFinite(number.Value)) number=null;
            return new(name,number,unit,origin+suffix);
        }
        return [Value("Area",ShapeProperty.Area,()=>shape.Area,"mm²"),Value("Iyy",ShapeProperty.MomentsOfInertia,()=>shape.Jxx,"mm⁴"),
            Value("Izz",ShapeProperty.MomentsOfInertia,()=>shape.Jyy,"mm⁴"),Value("Iyz",ShapeProperty.MomentsOfInertia,()=>shape.Jxy,"mm⁴"),
            Value("I₁₁",ShapeProperty.MomentsOfInertia,()=>shape.J11,"mm⁴"),Value("I₂₂",ShapeProperty.MomentsOfInertia,()=>shape.J22,"mm⁴"),
            Value("J torsionale",ShapeProperty.TorsionConstant,()=>shape.Jt,"mm⁴"),Value("Iω",ShapeProperty.WarpingConstant,()=>shape.Jw,"mm⁶")];
    }
    static void AddContours(Shape polygon,double y,double z,int part,List<SectionContour> result)
    {
        SectionPoint[] Points(IEnumerable<Point3d> points)=>points.Select(p=>new SectionPoint(p.X+y,p.Y+z)).ToArray();
        result.Add(new(Points(polygon.Fill),(polygon.Holes??[]).Select(Points).ToArray(),part));
        foreach(var child in polygon.Childs??[]) AddContours(child,y,z,part,result);
    }
    static SectionMesh Triangulate(SectionShapeInspection inspection)
    {
        try
        {
            var triangles=new List<SectionTriangle>();
            foreach(var part in inspection.Parts)
                foreach(var outline in part.Shape.GetCurveOutlines())
                {
                    var polygon=outline.ToShape(Math.Max(.00001,Math.Max(part.Shape.Width,part.Shape.Height)*.0001));
                    var mesh=new Section(polygon).GetMesh(initialMeshOnly:true);
                    SectionPoint Point(Point3d p)=>new(p.X+part.X,p.Y+part.Y);
                    foreach(var face in mesh.Faces)
                    {
                        var p=mesh.GetFacePoints(face); if(p.Length is not (3 or 4)) throw new InvalidDataException("Faccia della mesh grafica non supportata.");
                        triangles.Add(new(Point(p[0]),Point(p[1]),Point(p[2])));
                        if(p.Length==4) triangles.Add(new(Point(p[0]),Point(p[2]),Point(p[3])));
                    }
                }
            return triangles.Count>0 ? new(triangles.ToArray(),null) : new([],"Mesh grafica vuota: rappresentazione ad asse.");
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or InvalidDataException or NotSupportedException)
        { return new([],"Mesh grafica non disponibile: "+ex.Message); }
    }
    static string[] ReadMaterials(ModelSnapshot model,int[] elements)
    {
        if(model.SourceTables?.TryGetValue("ELEM",out var assignments)!=true||!model.SourceTables.TryGetValue("MATL",out var materials))return [];
        return elements.Select(id=>assignments.TryGetProperty(id.ToString(CultureInfo.InvariantCulture),out var e)&&e.TryGetProperty("MATL",out var m)?m.ToString():null)
            .Where(id=>id!=null).Distinct().Select(id=>materials.TryGetProperty(id!,out var m)&&m.TryGetProperty("NAME",out var n)?id+" · "+n.GetString():id+" · nome non importato").ToArray();
    }
}

/// <summary>Shared original attribute projection for the tree and detail windows; source units stay explicit.</summary>
public static class ModelSourceAttributes
{
    public static IEnumerable<SectionAttribute> Read(ModelSnapshot model,string table,int id)
    {
        if(model.SourceTables?.TryGetValue(table,out var data)!=true||!data.TryGetProperty(id.ToString(CultureInfo.InvariantCulture),out var record))yield break;
        yield return new("Attributi originali","Unità della sorgente (vedi sotto)");
        if(model.SourceTables.TryGetValue("UNIT",out var unit))foreach(var row in Flatten(unit,"UNIT"))yield return row;
        foreach(var row in Flatten(record))yield return row;
    }
    public static IEnumerable<SectionAttribute> Flatten(JsonElement value,string path="")
    {
        if(value.ValueKind==JsonValueKind.Object)
            foreach(var property in value.EnumerateObject())
                foreach(var row in Flatten(property.Value,path.Length==0?property.Name:path+"."+property.Name))yield return row;
        else if(value.ValueKind==JsonValueKind.Array)
        {
            int i=0;foreach(var item in value.EnumerateArray())foreach(var row in Flatten(item,path+"["+i+++"]"))yield return row;
        }
        else yield return new(path,value.ToString());
    }
}
