using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Actions;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

/// <summary>Plan of the supplied point actions; all forces and torsions are calculated in GPC.</summary>
public sealed class CraneActionsChart:FrameworkElement
{
    private const double MinimumWidth=450,DefaultWidth=700,Left=85,Right=90,Top=110,HeightPlan=260,Legend=510,Row=25,Gap=15,Radius=5,ArrowLength=48,Head=6,TextSize=12,TitleSize=15,Half=.5;
    public static readonly DependencyProperty DrawingProperty=DependencyProperty.Register(nameof(Drawing),typeof(CraneDrawing),typeof(CraneActionsChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsMeasure));
    public CraneDrawing? Drawing{get=>(CraneDrawing?)GetValue(DrawingProperty);set=>SetValue(DrawingProperty,value);}
    protected override Size MeasureOverride(Size available)=>new(double.IsInfinity(available.Width)?DefaultWidth:Math.Max(MinimumWidth,available.Width),Legend+(Drawing?.Points.Count??1)*Row+Row);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);ActionDrawingStyle.Surface(this,dc);var ink=ThemeTokens.Brush("Ink");var blue=ThemeTokens.Brush("Series.Blue");var orange=ThemeTokens.Brush("Series.Orange");var border=ThemeTokens.Brush("Border");
        void Text(string s,double x,double y,Brush? colour=null,double size=TextSize)=>ActionDrawingStyle.Text(this,dc,s,x,y,size,colour);
        void Line(double x,double y,double xx,double yy,Brush c)=>dc.DrawLine(new Pen(c,1),new(x,y),new(xx,yy));
        string N(double v)=>v.ToString("G5",CultureInfo.CurrentCulture);
        var d=Drawing;if(d is null){Text("Azioni sulle vie disponibili dopo un calcolo completato.",Left,Gap);return;}
        Text("Pianta delle vie · azioni del gruppo selezionato",Left,Gap,size:TitleSize);
        Text("x lungo la via; y verso rotaia 2. Q positivo verso il basso.",Left,Gap+Row);
        double right=Math.Max(MinimumWidth,ActualWidth)-Right,width=right-Left;
        double min=d.Points.Min(p=>p.X),max=d.Points.Max(p=>p.X);if(max<=min){min-=Half;max+=Half;}
        double ymin=Math.Min(0,d.Points.Min(p=>p.Y)),ymax=Math.Max(d.Width,d.Points.Max(p=>p.Y));
        double X(double x)=>Left+(x-min)/(max-min)*width;double Y(double y)=>Top+HeightPlan-(y-ymin)/(ymax-ymin)*HeightPlan;
        Line(Left,Y(0),right,Y(0),border);Line(Left,Y(d.Width),right,Y(d.Width),border);
        Text("R1 · y=0 m",Left,Y(0)+Row);Text("R2 · y="+N(d.Width)+" m",Left,Y(d.Width)-Row);
        Text("x="+N(min)+" m",Left,Top+HeightPlan+Row+Row);Text("x="+N(max)+" m",right-Right,Top+HeightPlan+Row+Row);
        double hmax=d.Points.SelectMany(p=>new[]{Math.Abs(p.Hx),Math.Abs(p.Hy)}).DefaultIfEmpty(0).Max();
        void Arrow(double x,double y,double dx,double dy)
        {
            if(dx==0&&dy==0)return;Line(x,y,x+dx,y+dy,orange);
            if(dx!=0){double sign=dx>0?1:-1;Line(x+dx,y,x+dx-sign*Head,y-Head,orange);Line(x+dx,y,x+dx-sign*Head,y+Head,orange);}
            else{double sign=dy>0?1:-1;Line(x,y+dy,x-Head,y+dy-sign*Head,orange);Line(x,y+dy,x+Head,y+dy-sign*Head,orange);}
        }
        for(int index=0;index<d.Points.Count;index++)
        {
            var p=d.Points[index];double x=X(p.X),y=Y(p.Y);if(p.Rail!=0){dc.DrawEllipse(blue,null,new(x,y),Radius,Radius);Text(p.Name+": "+N(p.Q)+" kN",x-Row,y+(p.Rail==1?-Row:Gap),blue);}
            if(hmax>0){Arrow(x,y,ArrowLength*p.Hx/hmax,0);Arrow(x,y,0,-ArrowLength*p.Hy/hmax);}
            Text(p.Name+" · ("+N(p.X)+"; "+N(p.Y)+") m · Q="+N(p.Q)+" · Hx="+N(p.Hx)+" · Hy="+N(p.Hy)+" kN"+(p.Rail!=0?" · T=±"+N(p.Torsion)+" kN·m":""),Left,Legend+index*Row,p.Rail!=0?blue:ink);
        }
        Text("Scale planimetriche distinte. Frecce proporzionali a H; torsioni locali ±e alternative.",Left,Legend-Row);
    }
}
