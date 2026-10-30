using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Actions;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

/// <summary>Drawing only: coordinates and influence ordinates come from the application projection of GPC data.</summary>
public sealed class TrafficDistributionChart:FrameworkElement
{
    private const double DefaultWidth=700,MinimumWidth=400,DrawingHeight=800,LeftMargin=70,RightMargin=25,PlanTop=55,PlanHeight=220,SectionTop=345,InfluenceTop=470,InfluenceHeight=190,LabelSize=12,TitleSize=15,TickCount=4,LegendTop=700,LegendRow=22,LegendColumns=3,GirderDrop=30,LoadOpacity=.13,RangePadding=.08;
    public static readonly DependencyProperty DrawingProperty=DependencyProperty.Register(nameof(Drawing),typeof(TrafficDrawing),typeof(TrafficDistributionChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsMeasure));
    public TrafficDrawing? Drawing{get=>(TrafficDrawing?)GetValue(DrawingProperty);set=>SetValue(DrawingProperty,value);}
    protected override Size MeasureOverride(Size availableSize)=>new(double.IsInfinity(availableSize.Width)?DefaultWidth:Math.Max(MinimumWidth,availableSize.Width),DrawingHeight+Math.Max(0,(Drawing?.Girders.Count??0)-LegendColumns)/LegendColumns*LegendRow);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);Brush[] Colours=[ThemeTokens.Brush("Series.Blue"),ThemeTokens.Brush("Series.Red"),ThemeTokens.Brush("Series.Green"),ThemeTokens.Brush("Series.Orange"),ThemeTokens.Brush("Series.Magenta"),ThemeTokens.Brush("Series.Steel"),ThemeTokens.Brush("Series.Amber")];var d=Drawing;var ink=ThemeTokens.Brush("Ink");var border=ThemeTokens.Brush("Border");
        void Text(string value,double x,double y,double size=LabelSize,Brush? brush=null)
        {dc.DrawText(new FormattedText(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush??ink,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));}
        bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        if(d is null||!Finite(d.Left)||!Finite(d.Right)||!Finite(d.Span)||d.Right<=d.Left||d.Span<=0){Text("Completare geometria e carichi per visualizzare l’impalcato.",LeftMargin,PlanTop);return;}
        double width=Math.Max(MinimumWidth,ActualWidth)-LeftMargin-RightMargin;
        double Y(double y)=>LeftMargin+(y-d.Left)/(d.Right-d.Left)*width;
        double X(double x)=>PlanTop+x/d.Span*PlanHeight;
        void Line(double x1,double y1,double x2,double y2,Brush brush,double thickness=1)=>dc.DrawLine(new Pen(brush,thickness),new(x1,y1),new(x2,y2));
        Text("Pianta · y trasversale → · x longitudinale ↓",LeftMargin,0,TitleSize);
        Text("Scale orizzontale e verticale distinte; coordinate in m.",LeftMargin,LegendRow);
        dc.DrawRectangle(null,new Pen(border,1),new Rect(LeftMargin,PlanTop,width,PlanHeight));
        dc.PushClip(new RectangleGeometry(new Rect(LeftMargin,PlanTop,width,PlanHeight)));
        for(int i=0;i<d.Patches.Count;i++)
        {
            var p=d.Patches[i];if(!new[]{p.Left,p.Right,p.Start,p.End}.All(Finite)||p.Right<=p.Left||p.End<=p.Start)continue;
            var colour=Colours[i%Colours.Length];dc.PushOpacity(LoadOpacity);dc.DrawRectangle(colour,null,new Rect(Y(p.Left),X(p.Start),Y(p.Right)-Y(p.Left),X(p.End)-X(p.Start)));dc.Pop();
            dc.DrawRectangle(null,new Pen(colour,1),new Rect(Y(p.Left),X(p.Start),Y(p.Right)-Y(p.Left),X(p.End)-X(p.Start)));Text(p.Name,Y(p.Left),X(p.Start),LabelSize,colour);
        }
        for(int i=0;i<d.Girders.Count;i++){var g=d.Girders[i];if(Finite(g.Position))Line(Y(g.Position),PlanTop,Y(g.Position),PlanTop+PlanHeight,Colours[i%Colours.Length]);}
        foreach(var t in d.Tandems)
        {
            if(!new[]{t.CentreY,t.CentreX,t.Axles,t.Wheels,t.Length,t.Width}.All(Finite)||t.Length<=0||t.Width<=0)continue;
            foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1})
            {
                double y=t.CentreY+b*t.Wheels/2,x=t.CentreX+a*t.Axles/2;
                dc.DrawRectangle(ThemeTokens.Brush("Series.Orange"),new Pen(ink,1),new Rect(Y(y-t.Width/2),X(x-t.Length/2),t.Width/(d.Right-d.Left)*width,t.Length/d.Span*PlanHeight));
            }
            Text(t.Name,Y(t.CentreY),X(t.CentreX),LabelSize,ink);
        }
        if(Finite(d.Section))Line(LeftMargin,X(d.Section),LeftMargin+width,X(d.Section),ink,2);
        dc.Pop();
        Text("0",LabelSize,PlanTop);Text(d.Span.ToString("G5"),LabelSize,PlanTop+PlanHeight);
        Text("Sezione x = "+d.Section.ToString("G5")+" m",LeftMargin,PlanTop+PlanHeight+LegendRow);
        Text("Sezione trasversale · posizioni delle travi",LeftMargin,SectionTop-LegendRow,TitleSize);
        Line(LeftMargin,SectionTop,LeftMargin+width,SectionTop,ink,2);
        for(int i=0;i<d.Girders.Count;i++)
        {
            var g=d.Girders[i];if(!Finite(g.Position))continue;double y=Y(g.Position);
            Line(y,SectionTop,y,SectionTop+GirderDrop,Colours[i%Colours.Length],2);Text(g.Name,y,SectionTop+GirderDrop,LabelSize,Colours[i%Colours.Length]);
        }
        Text(d.Left.ToString("G5")+" m",LeftMargin,SectionTop+GirderDrop+LegendRow);Text(d.Right.ToString("G5")+" m",LeftMargin+width-LeftMargin,SectionTop+GirderDrop+LegendRow);
        Text("Influenza di M per forza verticale unitaria · ordinate in m",LeftMargin,InfluenceTop-LegendRow*2,TitleSize);
        Text("Carico unitario a x = "+d.InfluenceX.ToString("G5")+" m; momento nella sezione x = "+d.Section.ToString("G5")+" m.",LeftMargin,InfluenceTop-LegendRow);
        var ordinates=d.Influence.SelectMany(p=>p.Ordinates).Where(Finite).ToArray();
        if(ordinates.Length==0){Text("Calcolare per ottenere le linee d’influenza.",LeftMargin,InfluenceTop+LegendRow);return;}
        double lo=Math.Min(0,ordinates.Min()),hi=Math.Max(0,ordinates.Max()),range=hi-lo;if(range<=0)range=1;lo-=range*RangePadding;hi+=range*RangePadding;
        double V(double v)=>InfluenceTop+(hi-v)/(hi-lo)*InfluenceHeight;
        for(int tick=0;tick<=TickCount;tick++)
        {double v=lo+(hi-lo)*tick/TickCount;Line(LeftMargin,V(v),LeftMargin+width,V(v),border);Text(v.ToString("G3"),0,V(v));}
        Line(LeftMargin,V(0),LeftMargin+width,V(0),ink);
        for(int i=0;i<d.Girders.Count;i++)
        {
            Point? previous=null;
            foreach(var p in d.Influence)
            {if(i>=p.Ordinates.Count||!Finite(p.Position)||!Finite(p.Ordinates[i])){previous=null;continue;}var point=new Point(Y(p.Position),V(p.Ordinates[i]));if(previous is { } old)dc.DrawLine(new Pen(Colours[i%Colours.Length],2),old,point);previous=point;}
            Text(d.Girders[i].Name,LeftMargin+(i%LegendColumns)*width/LegendColumns,LegendTop+(int)(i/LegendColumns)*LegendRow,LabelSize,Colours[i%Colours.Length]);
        }
        Text("y = "+d.Left.ToString("G5"),LeftMargin,InfluenceTop+InfluenceHeight);Text("y = "+d.Right.ToString("G5"),LeftMargin+width-LeftMargin,InfluenceTop+InfluenceHeight);
    }
}
