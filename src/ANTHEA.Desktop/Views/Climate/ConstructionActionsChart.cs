using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Actions;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

/// <summary>Schedule and plan of the load patches supplied by GPC. No structural calculation.</summary>
public sealed class ConstructionActionsChart:FrameworkElement
{
    private const double DefaultWidth=700,MinimumWidth=420,MarginLeft=90,MarginRight=35,TimelineTop=65,PhaseRow=30,PhaseBar=14,PlanGap=100,PlanHeight=260,LegendGap=65,LegendRow=25,TextGap=20,TickLabel=8,LabelWidth=55,DotRadius=4;
    private const int TickCount=4;
    public static readonly DependencyProperty DrawingProperty=DependencyProperty.Register(nameof(Drawing),typeof(ConstructionDrawing),typeof(ConstructionActionsChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsMeasure));
    public ConstructionDrawing? Drawing{get=>(ConstructionDrawing?)GetValue(DrawingProperty);set=>SetValue(DrawingProperty,value);}
    protected override Size MeasureOverride(Size available)=>new(double.IsInfinity(available.Width)?DefaultWidth:Math.Max(MinimumWidth,available.Width),TimelineTop+(Drawing?.Phases.Count??1)*PhaseRow+PlanGap+PlanHeight+LegendGap+(Drawing?.Loads.Sum(l=>l.Patches.Count+2)??1)*LegendRow);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);ActionDrawingStyle.Surface(this,dc);
        var ink=ThemeTokens.Brush("Ink");var border=ThemeTokens.Brush("Border");Brush[] colours=ActionDrawingStyle.Palette;
        double right=Math.Max(MinimumWidth,ActualWidth)-MarginRight,width=right-MarginLeft;
        void Text(string value,double x,double y,double size=12,Brush? colour=null)=>ActionDrawingStyle.Text(this,dc,value,x,y,size,colour);
        void Line(double x0,double y0,double x1,double y1,Brush colour,double thickness=1)=>dc.DrawLine(new Pen(colour,thickness),new(x0,y0),new(x1,y1));
        string N(double v)=>v.ToString("G5",CultureInfo.CurrentCulture);
        var d=Drawing;if(d is null){Text("Calcolare per visualizzare fasi e aree caricate.",MarginLeft,TextGap);return;}
        Text("Sequenza di esecuzione · durate in giorni",MarginLeft,TextGap,15);
        double last=d.Phases.Max(p=>p.End),timeRange=last>0?last:1;
        double T(double day)=>MarginLeft+day/timeRange*width;
        for(int i=0;i<d.Phases.Count;i++)
        {
            var p=d.Phases[i];double y=TimelineTop+i*PhaseRow;var colour=i==d.Selected?colours[0]:border;
            Text((i+1).ToString(),MarginLeft-LabelWidth,y);
            if(p.End>p.Start)dc.DrawRectangle(colour,null,new Rect(T(p.Start),y,T(p.End)-T(p.Start),PhaseBar));else Line(T(p.Start),y,T(p.Start),y+PhaseBar,colour,2);
            Text(p.Name+" · "+N(p.Start)+" → "+N(p.End),MarginLeft,y+PhaseBar,10);
        }
        double planTop=TimelineTop+d.Phases.Count*PhaseRow+PlanGap,planBottom=planTop+PlanHeight;
        Text("Pianta · "+d.Name,MarginLeft,planTop-TextGap*2,15);
        Text("Pressioni verticali: positive verso il basso. Scale x e y distinte.",MarginLeft,planTop-TextGap);
        var patches=d.Loads.SelectMany(l=>l.Patches).ToArray();
        if(patches.Length==0){Text("Nessun carico attivo nella fase.",MarginLeft,planTop+TextGap);return;}
        var pointLoads=d.Loads.Where(l=>l.PointLoad!=0||l.Hx!=0||l.Hy!=0).ToArray();
        double xmin=patches.Select(p=>p.X).Concat(pointLoads.Select(l=>l.PointX)).Min(),xmax=patches.Select(p=>p.X+p.Length).Concat(pointLoads.Select(l=>l.PointX)).Max();
        double ymin=patches.Select(p=>p.Y).Concat(pointLoads.Select(l=>l.PointY)).Min(),ymax=patches.Select(p=>p.Y+p.Width).Concat(pointLoads.Select(l=>l.PointY)).Max();
        if(xmax<=xmin||ymax<=ymin)return;
        double X(double x)=>MarginLeft+(x-xmin)/(xmax-xmin)*width;
        double Y(double y)=>planBottom-(y-ymin)/(ymax-ymin)*PlanHeight;
        for(int tick=0;tick<=TickCount;tick++)
        {
            double x=xmin+(xmax-xmin)*tick/TickCount,y=ymin+(ymax-ymin)*tick/TickCount;
            Line(X(x),planTop,X(x),planBottom,border);Line(MarginLeft,Y(y),right,Y(y),border);
            Text(N(x),X(x)-TickLabel,planBottom+TickLabel);Text(N(y),MarginLeft-LabelWidth,Y(y)-TickLabel);
        }
        Text("x [m]",right-LabelWidth,planBottom+PhaseRow);Text("y [m]",MarginLeft-LabelWidth,planTop-TextGap);
        double legend=planBottom+LegendGap;
        for(int i=0;i<d.Loads.Count;i++)
        {
            var l=d.Loads[i];var colour=colours[i%colours.Length];string number=(i+1).ToString();
            foreach(var p in l.Patches)
            {
                var rect=new Rect(X(p.X),Y(p.Y+p.Width),p.Length/(xmax-xmin)*width,p.Width/(ymax-ymin)*PlanHeight);
                dc.PushOpacity(.12);dc.DrawRectangle(colour,null,rect);dc.Pop();dc.DrawRectangle(null,new Pen(colour,2),rect);
                Text(number,X(p.X)+TickLabel,Y(p.Y+p.Width)+TickLabel,12,colour);
            }
            if(l.PointLoad!=0||l.Hx!=0||l.Hy!=0){dc.DrawEllipse(colour,new Pen(ink,1),new(X(l.PointX),Y(l.PointY)),DotRadius,DotRadius);Text("F"+number,X(l.PointX)+TickLabel,Y(l.PointY)-TextGap,12,colour);}
            Text(number+" · "+l.Name+" · "+l.Nature,MarginLeft,legend,12,colour);legend+=LegendRow;
            Text("V = "+N(l.Vertical)+" kN · F = "+N(l.PointLoad)+" kN · Hx = "+N(l.Hx)+" kN · Hy = "+N(l.Hy)+" kN",MarginLeft,legend);legend+=LegendRow;
            foreach(var p in l.Patches){Text(p.Name+": "+N(p.Pressure)+" kPa · "+N(p.Length)+" × "+N(p.Width)+" m · origine ("+N(p.X)+"; "+N(p.Y)+") m",MarginLeft,legend);legend+=LegendRow;}
        }
    }
}
