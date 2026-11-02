using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Actions;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

/// <summary>Display-only plot of ordinates supplied by the application projection.</summary>
public sealed class ActionHistoryChart:FrameworkElement
{
    private const double DefaultWidth=700,MinimumWidth=420,Left=95,Right=35,Top=90,PlotHeight=300,LegendTop=465,RowHeight=28,TextGap=24,LabelGap=8,LabelWidth=80,DotRadius=3,UnitRange=1;
    private const int Ticks=4;
    private const double LabelZeroTolerance=1e-13;
    public static readonly DependencyProperty DrawingProperty=DependencyProperty.Register(nameof(Drawing),typeof(ActionPlotDrawing),typeof(ActionHistoryChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsMeasure));
    public ActionPlotDrawing? Drawing{get=>(ActionPlotDrawing?)GetValue(DrawingProperty);set=>SetValue(DrawingProperty,value);}
    protected override Size MeasureOverride(Size available)=>new(double.IsInfinity(available.Width)?DefaultWidth:Math.Max(MinimumWidth,available.Width),LegendTop+(Drawing?.Series.Count??1)*RowHeight+TextGap);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);ActionDrawingStyle.Surface(this,dc);var ink=ThemeTokens.Brush("Ink");var grid=ThemeTokens.Brush("Border");Brush[] colours=ActionDrawingStyle.Palette;
        void Text(string s,double x,double y,double size=12,Brush? colour=null)=>ActionDrawingStyle.Text(this,dc,s,x,y,size,colour);
        void Line(double x,double y,double xx,double yy,Brush colour,double thickness=1)=>dc.DrawLine(new Pen(colour,thickness),new(x,y),new(xx,yy));
        string N(double x)=>Math.Abs(x)<LabelZeroTolerance?"0":x.ToString("G5",CultureInfo.CurrentCulture);
        var d=Drawing;if(d is null){Text("Storia disponibile dopo un calcolo completato.",Left,TextGap);return;}
        var points=d.Series.SelectMany(s=>s.Points).ToArray();if(points.Length==0)return;
        double xmin=points.Min(p=>p.X),xmax=points.Max(p=>p.X),ymin=Math.Min(0,points.Min(p=>p.Y)),ymax=Math.Max(0,points.Max(p=>p.Y));
        if(xmax<=xmin)xmax=xmin+UnitRange;if(ymax<=ymin)ymax=ymin+UnitRange;
        double right=Math.Max(MinimumWidth,ActualWidth)-Right,bottom=Top+PlotHeight,width=right-Left;
        double X(double x)=>Left+(x-xmin)/(xmax-xmin)*width;double Y(double y)=>bottom-(y-ymin)/(ymax-ymin)*PlotHeight;
        Text(d.Name,Left,TextGap,ActionDrawingStyle.TitleSize);Text(d.YLabel,Left,Top-TextGap);
        for(int n=0;n<=Ticks;n++)
        {
            double x=xmin+(xmax-xmin)*n/Ticks,y=ymin+(ymax-ymin)*n/Ticks;
            Line(X(x),Top,X(x),bottom,grid);Line(Left,Y(y),right,Y(y),grid);
            Text(N(x),X(x)-LabelGap,bottom+LabelGap);Text(N(y),Left-LabelWidth,Y(y)-LabelGap);
        }
        Text(d.XLabel,right-LabelWidth,bottom+RowHeight);Text(d.Note,Left,LegendTop-RowHeight);
        for(int index=0;index<d.Series.Count;index++)
        {
            var series=d.Series[index];var colour=colours[index%colours.Length];
            var geometry=new StreamGeometry();using(var ctx=geometry.Open())
            {
                if(series.Points.Count>0){ctx.BeginFigure(new(X(series.Points[0].X),Y(series.Points[0].Y)),false,false);ctx.PolyLineTo(series.Points.Skip(1).Select(p=>new Point(X(p.X),Y(p.Y))).ToArray(),true,false);}
            }
            dc.DrawGeometry(null,new Pen(colour,2),geometry);
            if(series.Points.LastOrDefault() is { } last){dc.DrawEllipse(colour,null,new(X(last.X),Y(last.Y)),DotRadius,DotRadius);Text(series.Name+" · ultimo valore = "+N(last.Y),Left,LegendTop+index*RowHeight,12,colour);}
        }
    }
}
