using System.Numerics;

namespace ANTHEA.ModelViewer.Wpf;

internal readonly record struct SceneClip(Vector3 Normal, float Distance)
{
    internal float Side(Vector3 p) => Vector3.Dot(Normal,p)-Distance;
    internal SurfaceBuilder Apply(SurfaceBuilder source)
    {
        var target=new SurfaceBuilder();
        for(int i=0;i<source.Positions.Count;i+=3)
        {
            var input=new List<(Vector3 P,float U)> { (source.Positions[i],source.UV[i].X),(source.Positions[i+1],source.UV[i+1].X),(source.Positions[i+2],source.UV[i+2].X) };
            var output=new List<(Vector3 P,float U)>();
            for(int j=0;j<3;j++)
            {
                var a=input[j]; var b=input[(j+1)%3]; float da=Side(a.P), db=Side(b.P);
                if(da>=0) output.Add(a);
                if((da>=0)!=(db>=0)) { float t=da/(da-db); output.Add((Vector3.Lerp(a.P,b.P,t),a.U+(b.U-a.U)*t)); }
            }
            for(int j=1;j+1<output.Count;j++) target.Triangle(output[0].P,output[j].P,output[j+1].P,output[0].U,output[j].U,output[j+1].U,source.ElementIds[i/3]);
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
