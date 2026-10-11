using System.Numerics;

namespace ANTHEA.ModelViewer.Wpf;

internal readonly record struct SceneClip(Vector3 Normal, float Distance)
{
    internal float Side(Vector3 p) => Vector3.Dot(Normal,p)-Distance;
    internal SurfaceBuilder Apply(SurfaceBuilder source)
    {
        var target=new SurfaceBuilder();
        Span<(Vector3 P,float U)> input=stackalloc (Vector3,float)[3];
        Span<(Vector3 P,float U)> output=stackalloc (Vector3,float)[4];
        for(int i=0;i<source.Positions.Count;i+=3)
        {
            for(int j=0;j<3;j++)input[j]=(source.Positions[i+j],source.UV[i+j].X);
            int count=0;
            for(int j=0;j<3;j++)
            {
                var a=input[j]; var b=input[(j+1)%3]; float da=Side(a.P), db=Side(b.P);
                if(da>=0) output[count++]=a;
                if((da>=0)!=(db>=0)) { float t=da/(da-db); output[count++]=(Vector3.Lerp(a.P,b.P,t),a.U+(b.U-a.U)*t); }
            }
            for(int j=1;j+1<count;j++) target.Triangle(output[0].P,output[j].P,output[j+1].P,output[0].U,output[j].U,output[j+1].U,source.ElementIds[i/3]);
        }
        return target;
    }
    internal bool Segment(ref Vector3 a,ref Vector3 b)
    {
        float da=Side(a),db=Side(b); if(da<0&&db<0) return false;
        if((da>=0)!=(db>=0)) { var p=Vector3.Lerp(a,b,da/(da-db)); if(da<0)a=p;else b=p; }
        return true;
    }
}

/// <summary>Intersection of graphical half-spaces; IDs and scalar texture coordinates follow every cut.</summary>
internal sealed class SceneClipSet(IReadOnlyList<SceneClip> planes)
{
    internal bool Contains(Vector3 point)=>planes.All(p=>p.Side(point)>=-1e-5);
    internal SurfaceBuilder Apply(SurfaceBuilder source)
    {
        foreach(var plane in planes)
        {
            if(source.Positions.Count==0)break;
            if(source.Positions.All(p=>plane.Side(p)>=0))continue;
            source=plane.Apply(source);
        }
        return source;
    }
    internal bool Segment(ref Vector3 a,ref Vector3 b)
    {
        foreach(var plane in planes)if(!plane.Segment(ref a,ref b))return false;
        return true;
    }
    internal Vector3[] Polygon(IReadOnlyList<Vector3> points)
    {
        if(points.Count==2){var a=points[0];var b=points[1];return Segment(ref a,ref b)?[a,b]:[];}
        var polygon=points.ToArray();
        foreach(var plane in planes)
        {
            var output=new List<Vector3>();
            for(int i=0;i<polygon.Length;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Length];float da=plane.Side(a),db=plane.Side(b);
                if(da>=0)output.Add(a);
                if((da>=0)!=(db>=0))output.Add(Vector3.Lerp(a,b,da/(da-db)));
            }
            polygon=output.ToArray();if(polygon.Length==0)break;
        }
        return polygon;
    }
}
