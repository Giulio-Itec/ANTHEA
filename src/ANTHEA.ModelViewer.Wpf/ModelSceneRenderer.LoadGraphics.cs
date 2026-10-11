using System.Numerics;
using System.Windows.Media;
using ANTHEA.ModelWorkspace;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    void DrawLoadGraphics(SceneOptions options,ExplorationOptions settings)
    {
        var visibleNodes=model!.Elements.Where(e=>options.Visible==null||options.Visible.Contains(e.Id)).SelectMany(e=>e.Nodes).ToHashSet();
        if(settings.Loads is {Count:>0} loads)
        {
            var forces=new EdgeBuilder();var moments=new EdgeBuilder();
            static Vector3 Vector(ModelDirection v)=>new((float)v.X,(float)v.Y,(float)v.Z);
            var maxima=loads.GroupBy(l=>l.Unit).ToDictionary(g=>g.Key,g=>g.Max(l=>Vector(l.Vector).Length()));
            foreach(var load in loads)
            {
                Vector3 origin;
                if(load.Node>0) {if(!visibleNodes.Contains(load.Node))continue;origin=points[load.Node];}
                else
                {
                    if(load.Element is not int id||options.Visible!=null&&!options.Visible.Contains(id))continue;
                    var e=model.Elements.First(e=>e.Id==id);origin=e.Nodes.Length==2?Vector3.Lerp(points[e.Nodes[0]],points[e.Nodes[1]],(float)load.Station):e.Nodes.Select(n=>points[n]).Aggregate(Vector3.Zero,(a,b)=>a+b)/e.Nodes.Length;
                }
                var vector=Vector(load.Vector);float magnitude=vector.Length();if(magnitude==0||maxima[load.Unit]==0)continue;
                float size=(float)(extent*.06*Math.Clamp(settings.LoadScale,.1,5))*magnitude/maxima[load.Unit];
                if(!load.Moment)forces.Arrow(origin-vector/magnitude*size,origin);
                else
                {
                    var normal=vector/magnitude;var u=Vector3.Normalize(Vector3.Cross(normal,Math.Abs(normal.Z)<.9?Vector3.UnitZ:Vector3.UnitY));var v=Vector3.Cross(normal,u);
                    Vector3 At(float a)=>origin+(u*MathF.Cos(a)+v*MathF.Sin(a))*size*.4f;
                    for(int i=0;i<16;i++)moments.Line(At(i*MathF.PI/10),At((i+1)*MathF.PI/10));moments.Arrow(At(15*MathF.PI/10),At(16*MathF.PI/10));
                }
            }
            AddLines(forces,Color.FromRgb(91,210,147),2);AddLines(moments,Color.FromRgb(230,143,232),2);
        }
        if(settings.Displacements is {Count:>0} displacements)
        {
            var delta=displacements.ToDictionary(d=>d.Node);var edges=new EdgeBuilder();
            foreach(var e in model.Elements.Where(e=>options.Visible==null||options.Visible.Contains(e.Id)))
            {
                if(e.Nodes.Any(n=>!delta.ContainsKey(n)))continue;
                var p=e.Nodes.Select(n=>points[n]+new Vector3((float)delta[n].X,(float)delta[n].Y,(float)delta[n].Z)*(float)Math.Clamp(settings.DeformationScale,0,100000)).ToArray();
                for(int i=0;i<(p.Length==2?1:p.Length);i++)edges.Line(p[i],p[(i+1)%p.Length]);
            }
            AddLines(edges,Color.FromRgb(247,120,190),2);
        }
    }
}
