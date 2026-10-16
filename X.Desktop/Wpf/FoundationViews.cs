using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using Anthea.FoundationWorkspace;

namespace X.Desktop;

/// <summary>Drawing only: geometry, samples and pressures are provided by the GPC library.</summary>
internal abstract class FoundationPlot : FrameworkElement
{
    protected Brush Ink=>Appearance.Ink;
    protected Brush Muted=>Appearance.Foreground(Ui.Muted);
    protected Pen Outline=>new(Muted,1.2);
    protected static double N(JsonNode? n)=>n?.GetValue<double>()??0;
    protected static string F(double n,string format="0.###")=>n.ToString(format,CultureInfo.GetCultureInfo("it-IT"));
    protected FoundationPlot() { Margin=new Thickness(8);Height=370;MinWidth=280;ClipToBounds=true; }
    protected void Text(DrawingContext dc,string s,double x,double y,double size=12,Brush? brush=null,double width=0)
    {
        var text=new FormattedText(s,CultureInfo.GetCultureInfo("it-IT"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush??Ink,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if(width>0)text.MaxTextWidth=width;dc.DrawText(text,new Point(x,y));
    }
    protected void Frame(DrawingContext dc,string title)
    { dc.DrawRectangle(Appearance.Paper,new Pen(Appearance.Outline("#D8E0EB"),1),new Rect(0,0,ActualWidth,ActualHeight));Text(dc,title,16,12,ActualWidth<340?13:17,width:Math.Max(30,ActualWidth-32)); }
    protected static Geometry Polygon(IEnumerable<Point> points)
    {
        var values=points.ToArray();var g=new StreamGeometry();
        if(values.Length>0){using var c=g.Open();c.BeginFigure(values[0],true,true);c.PolyLineTo(values.Skip(1).ToArray(),true,false);}return g;
    }
    protected static void Line(DrawingContext dc,IEnumerable<Point> points,Pen pen)
    {Point? previous=null;foreach(var p in points){if(previous.HasValue)dc.DrawLine(pen,previous.Value,p);previous=p;}}
    protected static Color Heat(double v)=>Color.FromRgb((byte)(40+v*190),(byte)(135-v*75),(byte)(205-v*160));
}

internal sealed class FoundationPlanView(JsonObject graphics,bool pressures) : FoundationPlot
{
    Rect plot;double scale,cx,cy;
    protected override void OnRender(DrawingContext dc)
    {
        Frame(dc,pressures?"Pressioni applicate · pianta":"Pianta · area efficace");
        double b=N(graphics["Width"]),l=N(graphics["Length"]),ex=N(graphics["CentreX"]),ey=N(graphics["CentreY"]);
        scale=Math.Min((ActualWidth-130)/b,(ActualHeight-165)/l);cx=ActualWidth/2;cy=70+l*scale/2;
        Point P(double x,double y)=>new(cx+x*scale,cy-y*scale);
        plot=new Rect(P(-b/2,l/2),P(b/2,-l/2));bool circle=graphics["Circular"]!.GetValue<bool>();
        Geometry boundary=circle?new EllipseGeometry(plot):new RectangleGeometry(plot);
        dc.DrawGeometry(Appearance.Surface,Outline,boundary);
        double max=N(graphics["MaximumPressure"]);
        if(pressures)
        {
            dc.PushClip(boundary);
            foreach(var node in graphics["Grid"]!.AsArray())
            {
                var p=P(N(node!["X"]),N(node["Y"]));double q=N(node["Pressure"]);
                var brush=q==0?Appearance.Surface:new SolidColorBrush(Heat(max>0?q/max:0));
                dc.DrawRectangle(brush,null,new Rect(p.X-b*scale/64,p.Y-l*scale/64,b*scale/32+.3,l*scale/32+.3));
            }
            dc.Pop();dc.DrawGeometry(null,Outline,boundary);
            for(int i=0;i<100;i++)dc.DrawRectangle(new SolidColorBrush(Heat(i/99d)),null,new Rect(20+i*(ActualWidth-40)/100,ActualHeight-60,(ActualWidth-40)/100+1,9));
            Text(dc,"0 kPa",20,ActualHeight-47);Text(dc,F(max*1000)+" kPa",ActualWidth-100,ActualHeight-47);
            Text(dc,"Passa il puntatore per leggere un campione di pressione.",16,ActualHeight-24,11,Muted);
        }
        else
        {
            double be=N(graphics["EffectiveWidthX"]),le=N(graphics["EffectiveLengthY"]);
            Geometry effective=circle?boundary:new RectangleGeometry(new Rect(P(ex-be/2,ey+le/2),P(ex+be/2,ey-le/2)));
            dc.DrawGeometry(Appearance.Background("#DBEBF7"),new Pen(Appearance.Accent,2),effective);
            if(!circle)dc.DrawGeometry(null,new Pen(Appearance.Series(Brushes.DarkOrange),2){DashStyle=DashStyles.Dash},Polygon(graphics["ContactPolygon"]!.AsArray().Select(n=>P(N(n!["X"]),N(n["Y"])))));
            var centre=P(ex,ey);dc.DrawEllipse(Appearance.Accent,null,centre,4,4);Text(dc,"N",centre.X+7,centre.Y-18,13);
            Text(dc,"Azzurro: area efficace A′   ·   tratteggio: area compressa",16,ActualHeight-64,11,Muted,ActualWidth-30);
            Text(dc,"B′X = "+F(be/1000)+" m     L′Y = "+F(le/1000)+" m",16,ActualHeight-40);
            Text(dc,"ex = "+F(ex/1000)+" m     ey = "+F(ey/1000)+" m",16,ActualHeight-22,11,Muted);
        }
        Text(dc,"X · B = "+F(b/1000)+" m",Math.Max(16,plot.Left),plot.Bottom+7,11,Muted);
        Text(dc,"Y · L = "+F(l/1000)+" m",16,45,11,Muted);
        if(!circle && pressures)foreach(var corner in graphics["Corners"]!.AsArray())
        {
            double x=N(corner!["X"]),y=N(corner["Y"]);var p=P(x,y);
            Text(dc,F(N(corner["Pressure"])*1000,"0.#"),p.X+(x<0?-45:5),p.Y+(y>0?-17:2),11);
        }
    }
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);if(!pressures||!plot.Contains(e.GetPosition(this))){ToolTip=null;return;}
        var mouse=e.GetPosition(this);double x=(mouse.X-cx)/scale,y=(cy-mouse.Y)/scale;
        var nearest=graphics["Grid"]!.AsArray().OrderBy(n=>Math.Pow(N(n!["X"])-x,2)+Math.Pow(N(n["Y"])-y,2)).First();
        ToolTip="Campione x = "+F(N(nearest!["X"])/1000)+" m; y = "+F(N(nearest["Y"])/1000)+" m\np = "+F(N(nearest["Pressure"])*1000)+" kPa";
    }
}

internal sealed class FoundationPressureSection(JsonObject graphics,bool alongX) : FoundationPlot
{
    protected override void OnRender(DrawingContext dc)
    {
        Frame(dc,"Pressioni · sezione "+(alongX?"X":"Y")+" per la risultante");
        var samples=graphics[alongX?"SectionX":"SectionY"]!.AsArray();string key=alongX?"X":"Y";
        double length=N(graphics[alongX?"Width":"Length"]),max=N(graphics["MaximumPressure"]);
        double left=70,right=ActualWidth-25,top=52,bottom=ActualHeight-55;
        Point P(JsonNode? n)=>new(left+(N(n![key])/length+.5)*(right-left),bottom-(max>0?N(n["Pressure"])/max:0)*(bottom-top));
        dc.DrawLine(Outline,new(left,top),new(left,bottom));dc.DrawLine(Outline,new(left,bottom),new(right,bottom));
        var pts=samples.Select(P).ToArray();dc.DrawGeometry(Appearance.Background("#DBEBF7"),null,Polygon(new[]{new Point(left,bottom)}.Concat(pts).Append(new Point(right,bottom))));
        Line(dc,pts,new Pen(Appearance.Accent,2));
        Text(dc,F(max*1000,"0.#"),5,top-7,11);Text(dc,"kPa",12,top+9,10,Muted);Text(dc,"0",45,bottom-8,11);
        Text(dc,F(-length/2000)+" m",left-15,bottom+8,11);Text(dc,F(length/2000)+" m",right-30,bottom+8,11);
        Text(dc,(alongX?"y":"x")+" = "+F(N(graphics[alongX?"CentreY":"CentreX"])/1000)+" m · modello senza trazione",16,ActualHeight-24,11,Muted);
    }
}

internal sealed class FoundationMechanismView(JsonObject mechanism) : FoundationPlot
{
    protected override void OnRender(DrawingContext dc)
    {
        Frame(dc,"Schema di rottura · sezione "+mechanism["Axis"]);
        var left=mechanism["Left"]!.AsArray();var right=mechanism["Right"]!.AsArray();var wedge=mechanism["Wedge"]!.AsArray();
        double xmin=left.Min(n=>N(n!["X"])),xmax=right.Max(n=>N(n!["X"])),depth=right.Max(n=>N(n!["Y"]));
        double scale=Math.Min((ActualWidth-50)/(xmax-xmin),(ActualHeight-135)/depth),cx=ActualWidth/2,centre=N(mechanism["Centre"]),width=N(mechanism["EffectiveWidth"]);
        Point P(double x,double z)=>new(cx+(x-centre)*scale,75+z*scale);
        Point V(JsonNode? n)=>P(N(n!["X"]),N(n["Y"]));
        dc.DrawRectangle(Appearance.Background("#EFE9D8"),null,new Rect(16,75,ActualWidth-32,ActualHeight-110));
        dc.DrawLine(Outline,new(16,75),new(ActualWidth-16,75));
        dc.DrawGeometry(Appearance.Background("#D6B992"),Outline,Polygon(wedge.Select(V)));
        foreach(var points in new[]{left,right})Line(dc,points.Select(V),new Pen(Appearance.Series(Brushes.DarkOrange),2));
        foreach(var n in mechanism["Fan"]!.AsArray())
        {
            double x=N(n!["X"]),z=N(n["Y"]);
            dc.DrawLine(Outline,P(centre+width/2,0),P(x,z));dc.DrawLine(Outline,P(centre-width/2,0),P(2*centre-x,z));
        }
        dc.DrawRectangle(Appearance.Surface,Outline,new Rect(P(centre-width/2,0).X,60,width*scale,15));
        Text(dc,"Base efficace · "+F(width/1000)+" m",16,40,11,Muted);
        Text(dc,"Cuneo centrale • ventaglio logaritmico • zona passiva",16,ActualHeight-29,11,Muted);
    }
}

internal sealed class FoundationSoilView : FoundationPlot
{
    internal JsonObject? Data {get;set;}
    protected override void OnRender(DrawingContext dc)
    {
        Frame(dc,"Stratigrafia · vista laterale");if(Data is null||ActualHeight<160)return;
        try
        {
            var profile=FoundationPresentation.SoilProfile(Data)??throw new ArgumentException();
            var input=Data["input"]!;double d=FoundationDocument.Number(input["Depth"],"D"),b=FoundationDocument.Number(input["Width"],"B");
            if(d<0||b<=0)throw new ArgumentException();
            bool wide=ActualWidth>440;double left=48,right=wide?ActualWidth*.48:ActualWidth-18,y0=72,h=ActualHeight-170;
            double extent=Math.Max(-profile.Base/1000,d+.1),scale=h/extent;
            int i=0;double lastLabel=-100;
            foreach(var layer in profile.Layers)
            {
                double top=-layer.Top/1000,bottom=-layer.Bottom/1000,y=y0+top*scale,hh=(bottom-top)*scale;
                var color=StratigraphyDrawing.LayerColors[i%StratigraphyDrawing.LayerColors.Length];
                dc.DrawRectangle(Appearance.Background(color),Outline,new Rect(left,y,right-left,hh));
                if(y-lastLabel>17){Text(dc,F(top),3,y-6,10,Muted);lastLabel=y;}
                if(hh>20)Text(dc,Anthea.Application.Presentation.ShallowFoundationPresentation.LayerLetter(i),left+7,y+3,12);
                if(wide && hh>25)
                {
                    string name=Anthea.Application.Presentation.ShallowFoundationPresentation.LayerLetter(i)+" · "+layer.Soil.Name;
                    Text(dc,name,right+10,y+2,12,width:ActualWidth-right-22);
                    if(hh>55)Text(dc,"γ "+F(layer.Soil.UnitWeight*1e6)+" / γsat "+F(layer.Soil.SaturatedUnitWeight*1e6)+" kN/m³\nφ′ "+F(layer.Soil.FrictionAngle*180/Math.PI)+"° · c′ "+F(layer.Soil.EffectiveCohesion*1000)+" kPa",right+10,y+22,11,Muted,ActualWidth-right-22);
                }
                i++;
            }
            Text(dc,"z [m]",4,48,10,Muted);Text(dc,F(-profile.Base/1000),3,y0+h-6,10,Muted);
            Text(dc,"Piano campagna",left,50,11,Muted);
            double yb=y0+d*scale,centre=(left+right)/2,half=Math.Min((right-left)*.36,b*scale/2);
            dc.DrawRectangle(Appearance.Surface,Outline,new Rect(centre-half,yb-12,2*half,12));
            dc.DrawLine(new Pen(Appearance.Accent,2),new(centre,yb-43),new(centre,yb-14));
            dc.DrawGeometry(Appearance.Accent,null,Polygon(new[]{new Point(centre,yb-13),new Point(centre-4,yb-22),new Point(centre+4,yb-22)}));
            Text(dc,"N",centre+6,yb-40,11);Text(dc,"D = "+F(d)+" m",left,y0+h+9,12);
            if(profile.GroundwaterElevation.HasValue)
            {
                double water=-profile.GroundwaterElevation.Value/1000,yw=y0+water*scale;
                if(yw>=y0 && yw<=y0+h)dc.DrawLine(new Pen(Appearance.Series(Brushes.SteelBlue),2){DashStyle=DashStyles.Dash},new(left,yw),new(right,yw));
                Text(dc,"Falda zf = "+F(water)+" m"+(water>extent?" · sotto vista":""),left,y0+h+29,11,Appearance.Series(Brushes.SteelBlue),ActualWidth-left-12);
            }
            else Text(dc,"Falda assente",left,y0+h+29,11,Muted);
            Text(dc,"↓ Semispazio omogeneo assunto sotto la base. Quote dal piano campagna.",12,ActualHeight-47,11,Muted,ActualWidth-24);
        }
        catch(ArgumentException){Text(dc,"Completa gli strati per visualizzare il profilo.",16,65,12,Muted,ActualWidth-32);}
    }
}

internal sealed class FoundationInputPlan : FoundationPlot
{
    internal JsonObject? Data {get;set;}
    protected override void OnRender(DrawingContext dc)
    {
        Frame(dc,"Geometria · pianta");if(Data is null||ActualHeight<180)return;
        try
        {
            var input=Data["input"]!;double Read(string key)=>FoundationDocument.Number(input[key],key);
            double b=Read("Width"),l=input["Shape"]!.GetValue<int>()==0?Read("Length"):input["Shape"]!.GetValue<int>()==1?1:b;
            if(b<=0||l<=0)throw new ArgumentException();
            double s=Math.Min((ActualWidth-85)/b,(ActualHeight-190)/l),cx=ActualWidth/2,cy=82+l*s/2;
            var rect=new Rect(cx-b*s/2,cy-l*s/2,b*s,l*s);
            if(input["Shape"]!.GetValue<int>()==2)dc.DrawEllipse(Appearance.Surface,Outline,new(cx,cy),b*s/2,l*s/2);else dc.DrawRectangle(Appearance.Surface,Outline,rect);
            dc.DrawLine(Outline,new(cx,rect.Bottom+10),new(cx,rect.Top-18));dc.DrawLine(Outline,new(rect.Left-10,cy),new(rect.Right+18,cy));
            Text(dc,"X",rect.Right+9,cy+4,11);Text(dc,"Y",cx+5,rect.Top-25,11);
            Text(dc,"B = "+F(b)+" m",rect.Left,rect.Bottom+16,12);Text(dc,"L = "+F(l)+" m",16,52,12);
            Text(dc,"N = "+input["Normal"]+" kN\nHx / Hy = "+input["HorizontalX"]+" / "+input["HorizontalY"]+" kN\nMx / My = "+input["MomentX"]+" / "+input["MomentY"]+" kNm",16,ActualHeight-87,11,Muted,ActualWidth-32);
        }
        catch(ArgumentException){Text(dc,"Completa la geometria.",16,65,12,Muted);}
    }
}
