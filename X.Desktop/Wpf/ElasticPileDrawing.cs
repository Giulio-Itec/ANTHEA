using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using X.Core;
namespace X.Desktop;
internal sealed class ElasticPileDrawing : FrameworkElement
{
    JsonObject? result;
    internal void Set(JsonObject? value){result=value;InvalidateVisual();}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);dc.DrawRectangle(Brushes.White,null,new Rect(0,0,ActualWidth,ActualHeight));if(result is null)return;
        var input=result["input"]!;var rows=result["risposta"]!.Array("Points");double length=input.D("lunghezza"),free=input.D("libero");
        double top=64,bottom=ActualHeight-42,h=bottom-top,w=(ActualWidth-140)/6;
        double Y(double x)=>top+h*x/length;
        void Text(string s,double x,double y,int size=11)=>dc.DrawText(new FormattedText(s,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,Ui.Navy,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        Text("x [m] ↓",0,26);double depth=free;
        foreach(var (layer,i) in input.Array("strati").Select((p,i)=>(p!,i))){double end=Math.Min(length,depth+layer.D("spessore"));if(end<=depth)break;dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40,(byte)(100+i*31%100),150,190)),null,new Rect(38,Y(depth),55,Y(end)-Y(depth)));Text((i+1).ToString(),43,(Y(depth)+Y(end))/2);depth=end;}
        dc.DrawLine(new Pen(Ui.Navy,7),new Point(102,Y(0)),new Point(102,Y(length)));
        Text($"H={input.D("H"):G4} kN",38,5);Text($"C={result["risposta"].D("AppliedHeadMoment"):G4} kNm",38,21);Text(input.S("vincolo"),90,40);Text(input.S("punta"),55,bottom+14);
        for(int j=0;j<=5;j++){double x=length*j/5;Text(x.ToString("0.##"),0,Y(x)-6);dc.DrawLine(new Pen(Brushes.LightGray,.5),new Point(35,Y(x)),new Point(ActualWidth,Y(x)));}
        dc.DrawLine(new Pen(Brushes.SaddleBrown,1),new Point(32,Y(free)),new Point(ActualWidth,Y(free)));Text("p.c.",2,Y(free)+5);
        if(input.B("falda") && free+input.D("z_falda")<=length){double water=Y(free+input.D("z_falda"));dc.DrawLine(new Pen(Brushes.SteelBlue,1){DashStyle=DashStyles.Dash},new Point(32,water),new Point(ActualWidth,water));Text("falda",35,water-15);}
        var keys=new[]{("DistributedStiffness","k [kN/m²]"),("Displacement","y [m]"),("Rotation","θ [rad]"),("Shear","V [kN]"),("Moment","M [kNm]"),("SoilReaction","q [kN/m]")};
        for(int j=0;j<keys.Length;j++)
        {
            var (key,label)=keys[j];double center=140+w*(j+.5),max=rows.Max(p=>Math.Abs(p.D(key)));if(max==0)max=1;
            Text(label,center-w/2+8,5,12);Text($"±{max:G3}",center-w/2+8,24);dc.DrawLine(new Pen(Brushes.Gray,.6),new Point(center,top),new Point(center,bottom));
            Point? previous=null;double? prevDepth=null;
            foreach(var row in rows){double x=row.D("Depth");var point=new Point(center+row.D(key)/max*w*.39,Y(x));if(previous.HasValue){var pen=new Pen(j==4?Brushes.DarkRed:Ui.Blue,1.5);if(prevDepth==x)pen.DashStyle=DashStyles.Dot;dc.DrawLine(pen,previous.Value,point);}previous=point;prevDepth=x;}
        }
        for(int i=0;i<12;i++){double x=free+(length-free)*(i+.5)/12;dc.DrawLine(new Pen(Ui.Muted,.8),new Point(105,Y(x)),new Point(128,Y(x)));}
    }
}
