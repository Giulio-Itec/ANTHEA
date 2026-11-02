using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Actions;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

/// <summary>Geometry and pressure plots of the GPC result. Only drawing transforms are evaluated here.</summary>
public sealed class HydraulicActionsChart:FrameworkElement
{
    private const double ArrowLength=9,ArrowHalfWidth=4,PlotBottom=680,PlotHeight=225,TickOffset=8,WaterLabelOffset=20,ElementEdgeGap=16;
    private const double RightMargin=35,WallHalfWidth=15,ResultArrowTip=17,ResultArrowLength=90,GeometryLabelOffset=32,AxisLabelWidth=40,ThicknessLabelWidth=100;
    private const int TickCount=4,LoadArrowDivisions=6;
    public static readonly DependencyProperty DrawingProperty=DependencyProperty.Register(nameof(Drawing),typeof(HydraulicDrawing),typeof(HydraulicActionsChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public HydraulicDrawing? Drawing{get=>(HydraulicDrawing?)GetValue(DrawingProperty);set=>SetValue(DrawingProperty,value);}
    protected override Size MeasureOverride(Size available)=>new(double.IsInfinity(available.Width)?700:Math.Max(420,available.Width),850);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);ActionDrawingStyle.Surface(this,dc);
        var ink=ThemeTokens.Brush("Ink");var border=ThemeTokens.Brush("Border");var blue=ThemeTokens.Brush("Series.Blue");var orange=ThemeTokens.Brush("Series.Orange");var green=ThemeTokens.Brush("Series.Green");
        double w=Math.Max(420,ActualWidth),left=72,right=w-RightMargin;
        void Text(string value,double x,double y,double size=12,Brush? colour=null)=>ActionDrawingStyle.Text(this,dc,value,x,y,size,colour);
        void Line(double x0,double y0,double x1,double y1,Brush colour,double thickness=1)=>dc.DrawLine(new Pen(colour,thickness),new(x0,y0),new(x1,y1));
        void Arrow(double x0,double y0,double x1,double y1,Brush colour)
        {
            Line(x0,y0,x1,y1,colour,2);
            if(x0==x1)
            {
                double direction=y1>=y0?1:-1;
                foreach(int side in new[]{-1,1})Line(x1,y1,x1+side*ArrowHalfWidth,y1-direction*ArrowLength,colour,2);
            }
            else
            {
                double direction=x1>=x0?1:-1;
                foreach(int side in new[]{-1,1})Line(x1,y1,x1-direction*ArrowLength,y1+side*ArrowHalfWidth,colour,2);
            }
        }
        string N(double x)=>x.ToString("G5",CultureInfo.CurrentCulture);
        var d=Drawing;
        if(d is null){Text("Calcolare per visualizzare geometria e pressioni.",left,24);return;}
        bool slab=d.Element=="HorizontalSlab",pier=d.Element=="RiverPier";
        Text(slab?"Platea · forza positiva verso l’alto":pier?"Pila · forza positiva nel verso della velocità positiva":"Parete · forza positiva da sinistra a destra",left,10,15);
        Text("Schema in sezione; larghezza fuori piano B = "+N(d.Width)+" m",left,36);
        double top=75,bottom=275,centre=(left+right)/2;
        if(slab)
        {
            dc.DrawRectangle(ThemeTokens.Brush("Surface"),new Pen(ink,2),new Rect(left,155,right-left,45));
            Text("L = "+N(d.Length)+" m",left,207);Text("t = "+N(d.Thickness)+" m",right-ThicknessLabelWidth,207);
            Text("zbase = "+N(d.Bottom)+" m",left,234);
            Text("H sopra = "+N(d.RightLevel)+" m",left,83,12,orange);
            Text(d.AssignedUplift?"Sottospinta da profilo assegnato":"H sotto = "+N(d.LeftLevel)+" m",left,263,12,blue);
            for(int k=1;k<LoadArrowDivisions;k++)
            {
                double x=left+(right-left)*k/LoadArrowDivisions;
                if(d.Points.Any(p=>p.Positive>0))Arrow(x,245,x,202,blue);
                if(d.Points.Any(p=>p.Opposing>0))Arrow(x,106,x,151,orange);
            }
        }
        else
        {
            double lo=Math.Min(d.Bottom,Math.Min(d.LeftLevel,pier?d.Bottom:d.RightLevel)),hi=Math.Max(d.Top,Math.Max(d.LeftLevel,pier?d.Top:d.RightLevel));
            double Y(double z)=>bottom-(z-lo)/(hi-lo)*(bottom-top);
            dc.DrawRectangle(ThemeTokens.Brush("Surface"),new Pen(ink,2),new Rect(centre-WallHalfWidth,Y(d.Top),30,Y(d.Bottom)-Y(d.Top)));
            Line(left,Y(d.LeftLevel),centre-ElementEdgeGap,Y(d.LeftLevel),blue,2);Text("H = "+N(d.LeftLevel)+" m",left,Y(d.LeftLevel)-WaterLabelOffset,12,blue);
            if(!pier){Line(centre+ElementEdgeGap,Y(d.RightLevel),right,Y(d.RightLevel),orange,2);Text("H = "+N(d.RightLevel)+" m",centre+GeometryLabelOffset,Y(d.RightLevel)-WaterLabelOffset,12,orange);}
            Text("z1 = "+N(d.Top)+" m",centre+GeometryLabelOffset,Y(d.Top));Text("z0 = "+N(d.Bottom)+" m",centre+GeometryLabelOffset,Y(d.Bottom));
            if(d.Application is { } z&&z>=lo&&z<=hi&&d.Force!=0){double from=d.Force>0?centre-ResultArrowLength:centre+ResultArrowLength,to=d.Force>0?centre-ResultArrowTip:centre+ResultArrowTip;Arrow(from,Y(z),to,Y(z),green);}
        }
        Text("Fnet = "+N(d.Force)+" kN     M = "+N(d.Moment)+" kN·m",left,308,15,green);
        Text(d.Application is { } application?(slab?"xR":"zR")+" = "+N(application)+" m":"Risultante nulla: punto di applicazione non definito.",left,335);
        Text(slab?"Momento rispetto a x = 0; peso proprio non incluso.":"Momento rispetto alla base z0; quote riferite al datum del progetto.",left,358);
        Text("Pressioni · ordinate del calcolo",left,397,15);
        if(d.Points.Count<2)return;
        double cmin=d.Points.Min(p=>p.Position),cmax=d.Points.Max(p=>p.Position);
        double pmin=Math.Min(0,d.Points.Min(p=>Math.Min(p.Net,Math.Min(p.Positive,p.Opposing)))),pmax=Math.Max(0,d.Points.Max(p=>Math.Max(p.Net,Math.Max(p.Positive,p.Opposing))));
        if(cmax<=cmin)return;if(pmax-pmin<1e-12)pmax=pmin+1;
        double px(double p)=>left+(p-pmin)/(pmax-pmin)*(right-left);
        double py(double p)=>PlotBottom-(p-pmin)/(pmax-pmin)*PlotHeight;
        double cx(double c)=>left+(c-cmin)/(cmax-cmin)*(right-left);
        double cy(double c)=>PlotBottom-(c-cmin)/(cmax-cmin)*PlotHeight;
        for(int k=0;k<=TickCount;k++)
        {
            double c=cmin+(cmax-cmin)*k/TickCount,p=pmin+(pmax-pmin)*k/TickCount;
            if(slab){Line(left,py(p),right,py(p),border);Text(N(p),4,py(p)-TickOffset);Text(N(c),cx(c)-TickOffset,687);}
            else{Line(left,cy(c),right,cy(c),border);Text(N(c),4,cy(c)-TickOffset);Text(N(p),px(p)-TickOffset,687);}
        }
        if(slab)Line(left,py(0),right,py(0),ink);else Line(px(0),455,px(0),680,ink);
        void Curve(Func<HydraulicPointDrawing,double> ordinate,Brush colour)
        {
            Point? previous=null;
            foreach(var point in d.Points)
            {
                var next=slab?new Point(cx(point.Position),py(ordinate(point))):new Point(px(ordinate(point)),cy(point.Position));
                if(previous is { } old)dc.DrawLine(new Pen(colour,2),old,next);previous=next;
            }
        }
        Curve(p=>p.Positive,blue);Curve(p=>p.Opposing,orange);Curve(p=>p.Net,green);
        Text(slab?"p [kPa]":"z [m]",4,428);Text(slab?"x [m]":"p [kPa]",right-AxisLabelWidth,713);
        Text("p+ · faccia positiva",left,748,12,blue);Text("p− · faccia opposta (modulo)",left,772,12,orange);Text("pnet = p+ − p−",left,796,12,green);
    }
}
