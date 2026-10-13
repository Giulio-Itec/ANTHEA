using Anthea.Application.Concrete;
using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;
internal sealed class ConcreteStrainPlaneView : DrawingView
{
    internal ConcreteStrainDiagram? Diagram { get; set; }
    protected override void Render(DrawingContext dc,Size size)
    {
        dc.DrawRectangle(Ui.Brush("#F8FAFD"),null,new Rect(size));
        if(Diagram is null){Text(dc,"Selezionare una soluzione calcolata",15,30,12);return;}
        var plane=Diagram;var outline=plane.Outline;
        double span=Math.Max(plane.Width,plane.Height),scale=Math.Min(size.Width/(span*1.8),size.Height/(span*1.65));
        double strainMax=outline.Max(v=>Math.Abs(v.Strain));
        double amplitude=Math.Min(100,size.Height*.22);
        Point P(double x,double y,double e)=>new(size.Width*.48+(x-.55*y)*scale,size.Height*.62+(.26*x+.45*y)*scale-(strainMax>0?e/strainMax*amplitude:0));
        var baseShape=new GeometryGroup{FillRule=FillRule.EvenOdd};baseShape.Children.Add(Path(outline.Select(v=>P(v.X,v.Y,0)),true));
        var strainShape=new GeometryGroup{FillRule=FillRule.EvenOdd};strainShape.Children.Add(Path(outline.Select(v=>P(v.X,v.Y,v.Strain)),true));
        foreach(var h in plane.Holes){baseShape.Children.Add(Path(h.Select(v=>P(v.X,v.Y,0)),true));strainShape.Children.Add(Path(h.Select(v=>P(v.X,v.Y,v.Strain)),true));}
        dc.DrawGeometry(Ui.Brush("#E3EAF1"),new Pen(Ui.Muted,1),baseShape);
        foreach(var v in outline.Where((_,i)=>outline.Count<10||i%Math.Max(1,outline.Count/12)==0))dc.DrawLine(new Pen(Ui.Muted,.7){DashStyle=DashStyles.Dash},P(v.X,v.Y,0),P(v.X,v.Y,v.Strain));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(100,47,113,170)),new Pen(Ui.Blue,1.5),strainShape);
        foreach(var v in outline.Where((_,i)=>outline.Count<10||i%Math.Max(1,outline.Count/4)==0))
        {
            double e=v.Strain;var q=P(v.X,v.Y,e);
            Text(dc,$"{e*1000:0.###} ‰",q.X+3,q.Y-18,10,e<0?Brushes.Firebrick:Ui.Blue);
        }
        var origin=P(0,0,0);var x=P(span*.45,0,0);var y=P(0,span*.45,0);
        dc.DrawLine(new Pen(Brushes.Firebrick,1.5),origin,x);dc.DrawLine(new Pen(Brushes.SeaGreen,1.5),origin,y);
        Text(dc,"x",x.X+4,x.Y,11,Brushes.Firebrick);Text(dc,"y",y.X+4,y.Y,11,Brushes.SeaGreen);
        Text(dc,"Piano delle deformazioni ε(x,y)",15,12,14,Ui.Navy,bold:true);
        Text(dc,$"ε0 = {plane.StrainReferencePoint*1000:0.######} ‰\ngx = {plane.ChiX:G5} mm⁻¹ · gy = {plane.ChiY:G5} mm⁻¹",15,37,11,width:Math.Max(100,size.Width-30));
        Text(dc,"Base grigia: ε = 0 · altezza amplificata per la lettura\nCoordinate geometriche in mm · compressione negativa",15,size.Height-48,11,width:Math.Max(100,size.Width-30));
    }
}
