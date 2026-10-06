using Anthea.Calculations;

// Independent cracked elastic section: exact degree-two triangle quadrature, point rebars.
// q = [constant concrete stress, x gradient, y gradient]; compression is negative.
static class IndependentElastic
{
    public static object Solve(SezioneCA section, double n, bool deductDisplacedConcrete, double nativeC, double nativeS)
    {
        double[] target = [-2500000, 250e6, -500e6], q = [-10, 0, 0];
        double[,] Matrix(double[] plane)
        {
            double Stress(double x,double y) => plane[0]+plane[1]*x+plane[2]*y;
            var polygon = section.Outline.Select(p => (x:p[0],y:p[1])).ToList();
            var clipped = new List<(double x,double y)>();
            for(int i=0;i<polygon.Count;i++)
            {
                var a=polygon[i]; var b=polygon[(i+1)%polygon.Count];
                double sa=Stress(a.x,a.y), sb=Stress(b.x,b.y);
                if(sa<=0) clipped.Add(a);
                if((sa<0 && sb>0)||(sa>0 && sb<0))
                { double t=sa/(sa-sb); clipped.Add((a.x+t*(b.x-a.x),a.y+t*(b.y-a.y))); }
            }
            var k=new double[3,3];
            void Add(double x,double y,double weight)
            { double[] v=[1,x,y];for(int i=0;i<3;i++)for(int j=0;j<3;j++)k[i,j]+=weight*v[i]*v[j]; }
            for(int t=1;t+1<clipped.Count;t++)
            {
                var a=clipped[0];var b=clipped[t];var c=clipped[t+1];
                double area=Math.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))/2;
                Add((4*a.x+b.x+c.x)/6,(4*a.y+b.y+c.y)/6,area/3);
                Add((a.x+4*b.x+c.x)/6,(a.y+4*b.y+c.y)/6,area/3);
                Add((a.x+b.x+4*c.x)/6,(a.y+b.y+4*c.y)/6,area/3);
            }
            foreach(var bar in section.Bars) Add(bar.X,bar.Y,bar.Area*(n-(deductDisplacedConcrete && Stress(bar.X,bar.Y)<0?1:0)));
            return k;
        }
        double residual=1;
        for(int iteration=0;iteration<100;iteration++)
        {
            var k=Matrix(q);var a=new double[3,4];
            for(int i=0;i<3;i++){for(int j=0;j<3;j++)a[i,j]=k[i,j];a[i,3]=target[i];}
            for(int i=0;i<3;i++)
            {
                double pivot=a[i,i];for(int j=i;j<4;j++)a[i,j]/=pivot;
                for(int r=0;r<3;r++)if(r!=i){double factor=a[r,i];for(int j=i;j<4;j++)a[r,j]-=factor*a[i,j];}
            }
            q=[a[0,3],a[1,3],a[2,3]];
            k=Matrix(q);
            residual=Enumerable.Range(0,3).Max(i=>Math.Abs(Enumerable.Range(0,3).Sum(j=>k[i,j]*q[j])-target[i])/Math.Abs(target[i]));
            if(residual<1e-12) break;
        }
        double cmin=section.Outline.Min(p=>q[0]+q[1]*p[0]+q[2]*p[1]);
        var steel=section.Bars.Select(b=>n*(q[0]+q[1]*b.X+q[2]*b.Y)).ToArray();
        if(residual>1e-9)throw new Exception("Independent equilibrium did not converge");
        if(deductDisplacedConcrete && (Math.Abs(cmin-nativeC)>0.01 || Math.Abs(steel.Max(Math.Abs)-nativeS)>0.1))
            throw new Exception("Native stresses do not match independent equilibrium");
        if(!deductDisplacedConcrete && Math.Abs(section.Width-400)<1e-8 && Math.Abs(section.Height-700)<1e-8 &&
            (Math.Abs(cmin-(-30.39))>0.01 || Math.Abs(steel.Max()-227.1)>0.05))
            throw new Exception("Reference screenshot stresses not reproduced");
        return new {n,deductDisplacedConcrete,cmin,smin=steel.Min(),smax=steel.Max(),residual};
    }
}
