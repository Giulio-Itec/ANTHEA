using System.Numerics;
using ANTHEA.ModelWorkspace;
using HelixToolkit.Maths;
using HelixToolkit.Wpf.SharpDX;
using Color = System.Windows.Media.Color;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    IEnumerable<Vector3> DisplayPoints(ModelElement element)
    {
        var p=element.Nodes.Select(n=>points[n]).ToArray();
        if(lastOptions?.Extruded!=true) return p;
        if(element.Type=="PLATE")
        {
            var prop=model!.Plates.First(t=>t.Id==element.Property);
            var n=Vector3.Normalize(Vector3.Cross(p[1]-p[0],p[2]-p[0]));
            return p.Select(v=>v+n*(float)(prop.Offset-prop.Thickness/2)).Concat(p.Select(v=>v+n*(float)(prop.Offset+prop.Thickness/2)));
        }
        return lastOptions.Display.SurfaceMode!=2 && LineProfile(element,p) is {} profile ? profile.Concat(profile.Select(v=>v+p[1]-p[0])) : p;
    }
    (Vector3 Y, Vector3 Z) LineBasis(ModelElement element, Vector3[] p)
    {
        if (model!.LocalFrames?.FirstOrDefault(f => f.Element == element.Id) is {} frame)
            return (new((float)frame.Y.X,(float)frame.Y.Y,(float)frame.Y.Z), new((float)frame.Z.X,(float)frame.Z.Y,(float)frame.Z.Z));
        // Graphical fallback uses connectivity and imported roll; it does not transform force components.
        var x = Vector3.Normalize(p[1]-p[0]);
        var y = Vector3.Normalize(Vector3.Cross(x, Math.Abs(x.Z)>.9 ? Vector3.UnitY : Vector3.UnitZ));
        var z = Vector3.Cross(x,y); float angle=(float)(element.Angle*Math.PI/180);
        return (y*MathF.Cos(angle)+z*MathF.Sin(angle), -y*MathF.Sin(angle)+z*MathF.Cos(angle));
    }
    Vector3[]? LineProfile(ModelElement element, Vector3[] p)
    {
        var section=model!.Sections.First(s=>s.Id==element.Property);
        if (!section.Centered || section.Shape!="SB") return null;
        var basis=LineBasis(element,p); var u=basis.Y*(float)section.Width/2; var v=basis.Z*(float)section.Height/2;
        return [p[0]-u-v,p[0]+u-v,p[0]+u+v,p[0]-u+v];
    }
    static (double Station,double Value)[] LineSamples(ModelElement element,IEnumerable<ResultValue> values) => values
        .Select(v=>(Station:v.Station??(v.Node==element.Nodes[0]?0:1),v.Value)).OrderBy(v=>v.Station).ToArray();

    int DrawLineElement(ModelElement element,Vector3[] p,SceneOptions options,IEnumerable<ResultValue> values,string color,
        SurfaceBuilder contour,SurfaceBuilder neutral,EdgeBuilder edges,EdgeBuilder axes,List<int> axisIds,
        Dictionary<int,EdgeBuilder> colors,Dictionary<int,List<int>> colorIds,
        Func<string,SurfaceBuilder> categoryMesh,Action<string,Vector3,Vector3,int> categoryLine)
    {
        bool active=options.ShowResults && options.Family==element.Type;
        var samples=active?LineSamples(element,values):[];
        var profile=options.Extruded && options.Display.SurfaceMode!=2 ? LineProfile(element,p):null;
        var delta=p[1]-p[0]; double min=options.ColorMinimum,max=options.ColorMaximum;
        void ColoredLine(Vector3 a,Vector3 b,double value)
        {
            int key=(int)Math.Clamp((value-min)/(max==min?1:max-min)*255,0,255);
            if(!colors.TryGetValue(key,out var line)){colors[key]=line=new();colorIds[key]=[];}
            line.Line(a,b);colorIds[key].Add(element.Id);
        }
        if(profile!=null)
        {
            var body=options.ShowResults?neutral:categoryMesh(color);
            bool fullContour=active && options.LineContour && samples.Length>1;
            if(fullContour)
            {
                for(int k=0;k+1<samples.Length;k++)
                {
                    var a=samples[k];var b=samples[k+1];
                    var first=profile.Select(v=>v+delta*(float)a.Station).ToArray();
                    var last=profile.Select(v=>v+delta*(float)b.Station).ToArray();
                    for(int i=0;i<4;i++)
                    {
                        int j=(i+1)%4;
                        Contours.Surface(contour,edges,[first[i],first[j],last[j],last[i]],[a.Value,a.Value,b.Value,b.Value],element.Id,min,max,1,false);
                    }
                    if(k==0) Contours.Surface(contour,edges,first.Reverse().ToArray(),Enumerable.Repeat(a.Value,4).ToArray(),element.Id,min,max,1,false);
                    if(k==samples.Length-2) Contours.Surface(contour,edges,last,Enumerable.Repeat(b.Value,4).ToArray(),element.Id,min,max,1,false);
                }
                // Uncovered parts retain their neutral geometry. Do not extend imported samples to absent endpoints.
                if(samples[0].Station>0) body.Prism(profile,delta*(float)samples[0].Station,element.Id);
                if(samples[^1].Station<1) body.Prism(profile.Select(v=>v+delta*(float)samples[^1].Station).ToArray(),delta*(float)(1-samples[^1].Station),element.Id);
            }
            else body.Prism(profile,delta,element.Id);
            if(active && options.LineContour && samples.Length==1)
                for(int i=0;i<4;i++) ColoredLine(profile[i]+delta*(float)samples[0].Station,profile[(i+1)%4]+delta*(float)samples[0].Station,samples[0].Value);
            if(options.ShowMesh || options.Exploration?.Outlines==true)
                for(int i=0;i<4;i++){int j=(i+1)%4;edges.Line(profile[i],profile[j]);edges.Line(profile[i]+delta,profile[j]+delta);edges.Line(profile[i],profile[i]+delta);}
        }
        else
        {
            if(options.ShowResults){axes.Line(p[0],p[1]);axisIds.Add(element.Id);}else categoryLine(color,p[0],p[1],element.Id);
            if(active && options.LineContour)
            {
                if(samples.Length==1)
                {
                    var center=p[0]+delta*(float)samples[0].Station;
                    var side=LineBasis(element,p).Y*(float)Math.Min(extent*.004,delta.Length()*.06);
                    ColoredLine(center-side,center+side,samples[0].Value);
                }
                for(int k=0;k+1<samples.Length;k++)
                {
                    var a=samples[k];var b=samples[k+1];
                    // Short colored segments approximate a continuous axis gradient without adding structural samples.
                    const int steps=32;
                    for(int i=0;i<steps;i++) ColoredLine(p[0]+delta*(float)(a.Station+(b.Station-a.Station)*i/steps),
                        p[0]+delta*(float)(a.Station+(b.Station-a.Station)*(i+1)/steps),a.Value+(b.Value-a.Value)*(i+.5)/steps);
                }
            }
        }
        return active && samples.Length==0?1:0;
    }

    void DrawBeamDiagram(SceneOptions options,ExplorationOptions settings)
    {
        var lines=new EdgeBuilder();var fill=new SurfaceBuilder();var labels=new HelixToolkit.SharpDX.BillboardText3D();
        double max=options.Values.Count>0?options.Values.Max(v=>Math.Abs(v.Value)):0;
        foreach(var group in options.Values.GroupBy(v=>v.Element))
        {
            var element=model!.Elements.First(e=>e.Id==group.Key);
            if(!ElementFamilies.IsLine(element.Type)||element.Type!=options.Family||options.Visible!=null&&!options.Visible.Contains(element.Id))continue;
            var p=element.Nodes.Select(n=>points[n]).ToArray(); var basis=LineBasis(element,p);
            var direction=settings.DiagramAlongY?basis.Y:basis.Z;
            Vector3? previousAxis=null,previousTip=null;double previousValue=0;
            foreach(var value in LineSamples(element,group))
            {
                var a=Vector3.Lerp(p[0],p[1],(float)value.Station);
                var b=a+direction*(float)(max==0?0:value.Value/max*extent*.08*Math.Clamp(settings.DiagramScale,.1,5));
                lines.Line(a,b);
                if(previousTip is {} oldTip && previousAxis is {} oldAxis)
                {
                    lines.Line(oldTip,b);
                    if(previousValue*value.Value<0)
                    {
                        var zero=Vector3.Lerp(oldAxis,a,(float)(Math.Abs(previousValue/max)/(Math.Abs(previousValue/max)+Math.Abs(value.Value/max))));
                        fill.Triangle(oldAxis,oldTip,zero);fill.Triangle(zero,b,a);
                    }
                    else fill.Quad(oldAxis,oldTip,b,a);
                }
                if(settings.DiagramValues && LabelFits(b)) labels.TextInfo.Add(new HelixToolkit.SharpDX.TextInfo(value.Value.ToString("G5"),b)
                    {Foreground=new Color4(1,.91f,.62f,1),Background=new Color4(.04f,.07f,.1f,.88f),Scale=(float)(.65*options.RasterScale),Offset=new(0,12)});
                previousAxis=a;previousTip=b;previousValue=value.Value;
            }
        }
        if(clipping is {} clip)fill=clip.Apply(fill);
        if(fill.Indices.Count>0)viewport.Items.Add(new MeshGeometryModel3D {Geometry=fill.Mesh(),Material=new DiffuseMaterial {DiffuseColor=new Color4(.95f,.76f,.43f,.24f),EnableUnLit=true},
            IsTransparent=true,CullMode=SharpDX.Direct3D11.CullMode.None,IsHitTestVisible=false});
        AddLines(lines,Color.FromRgb(242,194,109),1.8);
        if(labels.TextInfo.Count>0)viewport.Items.Add(new BillboardTextModel3D {Geometry=labels,FixedSize=true,IsHitTestVisible=false,DepthBias=-10000});
    }
}
