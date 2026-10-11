namespace ANTHEA.ModelViewer.Presentation;

public readonly record struct ScreenPoint(double X,double Y);
/// <summary>Screen-space selection only; independent from WPF and engineering geometry.</summary>
public static class ScreenSelection
{
    public static bool Contains(IReadOnlyList<ScreenPoint> polygon,ScreenPoint p)
    {
        bool inside=false;
        for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
        {
            var a=polygon[j];var b=polygon[i];
            if(OnSegment(a,b,p))return true;
            if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
        }
        return inside;
    }
    public static bool Matches(IReadOnlyList<ScreenPoint> region,IReadOnlyList<ScreenPoint> shape,bool crossing)
    {
        if(region.Count<3||shape.Count==0||shape.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)))return false;
        if(!crossing)return shape.All(p=>Contains(region,p));
        if(shape.Any(p=>Contains(region,p))||shape.Count>=3&&region.Any(p=>Contains(shape,p)))return true;
        for(int i=0;i<region.Count;i++) for(int j=0;j<(shape.Count==2?1:shape.Count);j++)
            if(Intersects(region[i],region[(i+1)%region.Count],shape[j],shape[(j+1)%shape.Count]))return true;
        return false;
    }
    static double Cross(ScreenPoint a,ScreenPoint b,ScreenPoint p)=>(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
    static bool OnSegment(ScreenPoint a,ScreenPoint b,ScreenPoint p)=>Math.Abs(Cross(a,b,p))<1e-7&&p.X>=Math.Min(a.X,b.X)-1e-7&&p.X<=Math.Max(a.X,b.X)+1e-7&&p.Y>=Math.Min(a.Y,b.Y)-1e-7&&p.Y<=Math.Max(a.Y,b.Y)+1e-7;
    static bool Intersects(ScreenPoint a,ScreenPoint b,ScreenPoint c,ScreenPoint d)=>(Cross(a,b,c)*Cross(a,b,d)<0&&Cross(c,d,a)*Cross(c,d,b)<0)||OnSegment(a,b,c)||OnSegment(a,b,d)||OnSegment(c,d,a)||OnSegment(c,d,b);
}
