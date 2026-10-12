using System.Numerics;
using ANTHEA.ModelWorkspace;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    private ModelSectionCatalog? sections;
    // Faces and loops share the same mm geometry as the 2D inspector. Conversion here is graphical only.
    internal sealed record LineSectionProfile(Vector3[][] Loops,Vector3[][] Caps)
    {
        public IEnumerable<Vector3> Points=>Loops.SelectMany(p=>p);
    }
    LineSectionProfile? LineProfile(ModelElement element,Vector3[] p)
    {
        var section=sections?.Find(element.Property);
        if(section==null||!section.CanExtrude(element.Id)||section.Insertion is not {} reference)return null;
        var basis=LineBasis(element,p);
        Vector3 Place(SectionPoint point)=>p[0]+basis.Y*(float)((point.Y-reference.Y)*.001)+basis.Z*(float)((point.Z-reference.Z)*.001);
        Vector3[] Loop(SectionPoint[] source,bool hole)
        {
            var points=source.Length>1&&source[0]==source[^1]?source[..^1]:source;
            double area=0;for(int i=0;i<points.Length;i++){var a=points[i];var b=points[(i+1)%points.Length];area+=a.Y*b.Z-b.Y*a.Z;}
            if(hole?area>0:area<0)points=points.Reverse().ToArray();
            return points.Select(Place).ToArray();
        }
        return new(section.Contours.SelectMany(c=>new[]{Loop(c.Boundary,false)}.Concat(c.Holes.Select(h=>Loop(h,true)))).ToArray(),
            section.Mesh.Triangles.Select(t=>
            {
                bool ccw=(t.B.Y-t.A.Y)*(t.C.Z-t.A.Z)-(t.C.Y-t.A.Y)*(t.B.Z-t.A.Z)>0;
                return ccw?new[]{Place(t.A),Place(t.B),Place(t.C)}:new[]{Place(t.A),Place(t.C),Place(t.B)};
            }).ToArray());
    }
    static void SectionPrism(SurfaceBuilder target,LineSectionProfile profile,Vector3 delta,int id,double from=0,double to=1)
    {
        foreach(var cap in profile.Caps)
        {
            var a=delta*(float)from;var b=delta*(float)to;
            target.Triangle(cap[0]+a,cap[2]+a,cap[1]+a,id:id);
            target.Triangle(cap[0]+b,cap[1]+b,cap[2]+b,id:id);
        }
        foreach(var loop in profile.Loops)
            for(int i=0;i<loop.Length;i++)
            {int j=(i+1)%loop.Length;target.Quad(loop[i]+delta*(float)from,loop[j]+delta*(float)from,loop[j]+delta*(float)to,loop[i]+delta*(float)to,id);}
    }
    static void SectionContourSegment(SurfaceBuilder target,EdgeBuilder edges,LineSectionProfile profile,Vector3 delta,int id,
        double from,double to,double a,double b,double minimum,double maximum,bool first,bool last)
    {
        foreach(var loop in profile.Loops)
            for(int i=0;i<loop.Length;i++)
            {
                int j=(i+1)%loop.Length;
                Contours.Surface(target,edges,[loop[i]+delta*(float)from,loop[j]+delta*(float)from,loop[j]+delta*(float)to,loop[i]+delta*(float)to],
                    [a,a,b,b],id,minimum,maximum,1,false);
            }
        foreach(var cap in profile.Caps)
        {
            if(first)Contours.Surface(target,edges,[cap[0]+delta*(float)from,cap[2]+delta*(float)from,cap[1]+delta*(float)from],[a,a,a],id,minimum,maximum,1,false);
            if(last)Contours.Surface(target,edges,cap.Select(p=>p+delta*(float)to).ToArray(),[b,b,b],id,minimum,maximum,1,false);
        }
    }
}
