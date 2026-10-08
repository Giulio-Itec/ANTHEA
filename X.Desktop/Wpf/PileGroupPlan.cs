using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using X.Core;
namespace X.Desktop;
internal sealed class PileGroupPlan : FrameworkElement
{
    JsonObject? data; JsonNode? result; JsonArray? cap;
    internal void Set(JsonObject d, JsonNode? r, JsonArray? c) { data=d; result=r; cap=c; InvalidateVisual(); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == Appearance.DarkProperty) InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); if(data is null || ActualWidth<100) return;
        // Technical drawing with the Light colours: on white also in the dark appearances, as the other plots
        // (in Light it lies on the white card, which stays the only background).
        if (Appearance.GetDark(this)) dc.DrawRectangle(Brushes.White,null,new Rect(0,0,ActualWidth,ActualHeight));
        void Text(string s, double x,double y, Brush? b=null) => dc.DrawText(new FormattedText(s,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,b??Brushes.DimGray,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        var piles=data.Array("pali").ToArray(); if(piles.Length==0)return;
        double d=data.D("diametro",1); if(d<=0)return;
        var all=piles.Select(p=>new Point(p.D("x"),p.D("y"))).Concat((cap??new JsonArray()).Select(p=>new Point(p.D("X"),p.D("Y")))).ToArray();
        double xmin=all.Min(p=>p.X)-d,xmax=all.Max(p=>p.X)+d,ymin=all.Min(p=>p.Y)-d,ymax=all.Max(p=>p.Y)+d;
        double scale=Math.Min((ActualWidth-140)/(xmax-xmin),(ActualHeight-75)/(ymax-ymin));
        Point P(double x,double y)=>new(65+(ActualWidth-130-(xmax-xmin)*scale)/2+(x-xmin)*scale,15+(ymax-y)*scale);
        if(cap is {Count:>2}) { var g=new StreamGeometry(); using(var c=g.Open()){c.BeginFigure(P(cap[0].D("X"),cap[0].D("Y")),true,true); c.PolyLineTo(cap.Skip(1).Select(v=>P(v.D("X"),v.D("Y"))).ToArray(),true,false);} dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(235,239,244)),new Pen(Brushes.SlateGray,1.5),g); }
        foreach(var p in piles){var f=result?.Array("Piles").FirstOrDefault(v=>v.S("Id")==p.S("id"));double? v=J.Number(f?["Factor"]);var point=P(p.D("x"),p.D("y")); dc.DrawEllipse(v.HasValue?ColorAt(v.Value):Brushes.LightGray,new Pen(Brushes.White,1),point,d*scale/2,d*scale/2);Text(p.S("id")+(v.HasValue?$" · {v:0.00}":""),point.X+d*scale/2+3,point.Y-8);}
        double angle=result.D("Angle")*Math.PI/180; var a=new Point(ActualWidth-62,42);var b=new Point(a.X+32*Math.Cos(angle),a.Y-32*Math.Sin(angle));dc.DrawLine(new Pen(Brushes.DarkBlue,2),a,b);dc.DrawEllipse(Brushes.DarkBlue,null,b,3,3);Text("H",a.X-6,a.Y+8);
        double w=Math.Min(300,ActualWidth-150), x0=65, y0=ActualHeight-36;
        for(int i=0;i<100;i++)dc.DrawRectangle(ColorAt(i/99d),null,new Rect(x0+i*w/100,y0,w/100+1,9));
        Text("0 · riduzione 100%",x0,y0+11);Text("1 · nessuna riduzione",x0+w-125,y0+11);
    }
    static Brush ColorAt(double v){v=Math.Clamp(v,0,1);return new SolidColorBrush(Color.FromRgb((byte)(210*(1-v)+38*v),(byte)(55*(1-v)+160*v),65));}
}
