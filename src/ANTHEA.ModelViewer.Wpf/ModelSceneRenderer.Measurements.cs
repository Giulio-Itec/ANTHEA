using System.Numerics;
using System.Windows.Media;
using ANTHEA.ModelViewer.Presentation;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Maths;
using Color=System.Windows.Media.Color;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    private void DrawMeasurements(SceneOptions options,ExplorationOptions settings)
    {
        var allowed=options.Visible==null?null:model!.Elements.Where(e=>options.Visible.Contains(e.Id)).SelectMany(e=>e.Nodes).ToHashSet();
        bool Visible(int id)=>points.ContainsKey(id) && (allowed==null||allowed.Contains(id));
        var edges=new EdgeBuilder();var text=new BillboardText3D();float size=(float)extent*.004f;
        void Mark(int id)
        {
            var p=points[id];edges.Line(p-Vector3.UnitX*size,p+Vector3.UnitX*size);edges.Line(p-Vector3.UnitY*size,p+Vector3.UnitY*size);edges.Line(p-Vector3.UnitZ*size,p+Vector3.UnitZ*size);
        }
        foreach(var item in settings.Measurements??[])
        {
            if(!item.Nodes.All(Visible))continue;
            var p=item.Nodes.Select(id=>points[id]).ToArray();
            for(int i=1;i<p.Length;i++)edges.Line(p[i-1],p[i]);
            foreach(int node in item.Nodes)Mark(node);
            var anchor=p.Length==2?(p[0]+p[1])/2:p[1];
            var probe=new ModelProbe(0,0,null,item.Caption,0,0,0);
            if(ProbePlacement(probe,anchor,out var offset))
            {
                edges.Line(anchor,LabelLeaderEnd(anchor,offset));
                text.TextInfo.Add(new TextInfo(item.Caption,anchor){Foreground=new Color4(.7f,1,1,1),Background=new Color4(.03f,.09f,.13f,.95f),Scale=(float)(.6*options.RasterScale),Offset=offset*(float)options.RasterScale});
            }
        }
        foreach(int id in (settings.MeasurementNodes??[]).Where(Visible))Mark(id);
        if(edges.Geometry().Positions?.Count>0)AddLines(edges,Color.FromRgb(103,225,227),1.8);
        if(text.TextInfo.Count>0)viewport.Items.Add(new BillboardTextModel3D {Geometry=text,FixedSize=true,IsHitTestVisible=false,DepthBias=-10000});
    }
}
