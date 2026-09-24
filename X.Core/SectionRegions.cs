namespace X.Core;

public sealed record ConcreteEffectiveRegion(string Name,double Qx,double Qy,double Level,double Area,
    int[] BarIndices,double SteelArea,double? Width,double[][] Outline,double[][][] Holes);
/// <summary>Polygon half-plane clipping, independent of native meshing and UI.</summary>
public static class SectionRegions
{
    public static double BarCover(SezioneCA section,Barra bar)
    {
        double clearance=double.PositiveInfinity;
        bool Inside(double[][] contour)
        {
            bool inside=false;
            for(int i=0,j=contour.Length-1;i<contour.Length;j=i++)
            {
                var a=contour[j];var b=contour[i];
                if((a[1]>bar.Y)!=(b[1]>bar.Y)&&bar.X<(b[0]-a[0])*(bar.Y-a[1])/(b[1]-a[1])+a[0])inside=!inside;
                double dx=b[0]-a[0],dy=b[1]-a[1],f=Math.Clamp(((bar.X-a[0])*dx+(bar.Y-a[1])*dy)/(dx*dx+dy*dy),0,1);
                clearance=Math.Min(clearance,double.Hypot(bar.X-a[0]-f*dx,bar.Y-a[1]-f*dy));
            }
            return inside;
        }
        bool inConcrete=Inside(section.Outline.ToArray());
        foreach(var hole in section.Holes)if(Inside(hole))inConcrete=false;
        return (inConcrete?clearance:-clearance)-bar.Diametro/2;
    }
    public static double[][] Clip(IReadOnlyList<double[]> polygon,double qx,double qy,double level)
    {
        var result=new List<double[]>();
        for(int i=0;i<polygon.Count;i++)
        {
            var a=polygon[i];var b=polygon[(i+1)%polygon.Count];double da=qx*a[0]+qy*a[1]-level,db=qx*b[0]+qy*b[1]-level;
            if(da>=0)result.Add([a[0],a[1]]);
            if((da<0&&db>0)||(da>0&&db<0)){double t=da/(da-db);result.Add([a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])]);}
        }
        return result.ToArray();
    }
    public static double Area(IReadOnlyList<double[]> polygon)
    {double a=0;for(int i=0;i<polygon.Count;i++){var p=polygon[i];var q=polygon[(i+1)%polygon.Count];a+=p[0]*q[1]-q[0]*p[1];}return Math.Abs(a)/2;}
    public static ConcreteEffectiveRegion Region(SezioneCA section,string name,double qx,double qy,double level,int[] bars,double? width=null)
    {
        var outline=Clip(section.Outline,qx,qy,level);
        var holes=section.Holes.Select(h=>Clip(h,qx,qy,level)).Where(h=>h.Length>=3).ToArray();
        return new(name,qx,qy,level,Area(outline)-holes.Sum(Area),bars,bars.Sum(i=>section.Bars[i].Area),width,outline,holes);
    }
}
