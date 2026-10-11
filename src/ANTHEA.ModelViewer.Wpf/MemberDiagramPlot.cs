using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ANTHEA.ModelViewer.Presentation;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>Pure WPF plot: draws supplied samples and translates pointer coordinates into a station.</summary>
public sealed class MemberDiagramPlot : Control
{
    public static readonly DependencyProperty SeriesProperty=DependencyProperty.Register(nameof(Series),typeof(MemberDiagramSeries),typeof(MemberDiagramPlot),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StationProperty=DependencyProperty.Register(nameof(Station),typeof(double),typeof(MemberDiagramPlot),new FrameworkPropertyMetadata(.5,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty InvertProperty=DependencyProperty.Register(nameof(Invert),typeof(bool),typeof(MemberDiagramPlot),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillDiagramProperty=DependencyProperty.Register(nameof(FillDiagram),typeof(bool),typeof(MemberDiagramPlot),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CursorLockedProperty=DependencyProperty.Register(nameof(CursorLocked),typeof(bool),typeof(MemberDiagramPlot),new PropertyMetadata(false));
    public MemberDiagramSeries? Series {get=>(MemberDiagramSeries?)GetValue(SeriesProperty);set=>SetValue(SeriesProperty,value);}
    public double Station {get=>(double)GetValue(StationProperty);set=>SetValue(StationProperty,value);}
    public bool Invert {get=>(bool)GetValue(InvertProperty);set=>SetValue(InvertProperty,value);}
    public bool FillDiagram {get=>(bool)GetValue(FillDiagramProperty);set=>SetValue(FillDiagramProperty,value);}
    public bool CursorLocked {get=>(bool)GetValue(CursorLockedProperty);set=>SetValue(CursorLockedProperty,value);}
    Rect plot;
    static readonly Brush Blue=new SolidColorBrush(Color.FromRgb(64,157,220));
    static readonly Brush Orange=new SolidColorBrush(Color.FromRgb(230,148,74));
    public MemberDiagramPlot(){Focusable=true;Cursor=Cursors.Cross;MinHeight=160;}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRoundedRectangle(Background??Brushes.Transparent,null,new Rect(0,0,ActualWidth,ActualHeight),4,4);
        if(Series is not {} series || ActualWidth<150 || ActualHeight<100)return;
        var ink=Foreground??Brushes.Black;var grid=ink.Clone();grid.Opacity=.16;
        void Text(string text,double x,double y,double size=10,Brush? color=null,bool bold=false)
        {
            var ft=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size*FontSize/12,color??ink,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(ft,new Point(x,y));
        }
        Text($"{series.Component} [{series.Unit}]",12,8,12,bold:true);
        if(series.Samples.Length==0)return;
        double low=Math.Min(0,series.Minimum),high=Math.Max(0,series.Maximum);
        if(high==low){low=-1;high=1;}
        double padding=(high-low)*.12;low-=padding;high+=padding;
        double left=66*FontSize/12,top=36*FontSize/12,bottom=31*FontSize/12;
        plot=new Rect(left,top,Math.Max(1,ActualWidth-left-20),Math.Max(1,ActualHeight-top-bottom));
        Point PointAt(double station,double value)=>new(plot.Left+plot.Width*station,plot.Top+plot.Height*(Invert?(value-low)/(high-low):1-(value-low)/(high-low)));
        string Number(double value)=>value==0?"0":Math.Abs(value)<.001 || Math.Abs(value)>=1e5?value.ToString("0.##E+0",CultureInfo.CurrentCulture):value.ToString("0.###",CultureInfo.CurrentCulture);
        for(int i=0;i<=4;i++)
        {
            double x=plot.Left+plot.Width*i/4;
            dc.DrawLine(new Pen(grid,1),new Point(x,plot.Top),new Point(x,plot.Bottom));
            Text((i/4.0).ToString("0.##",CultureInfo.CurrentCulture),x-7,plot.Bottom+7);
        }
        var labeled=new List<double>();
        var extremes=Math.Abs(series.Minimum)>Math.Abs(series.Maximum)?new[]{0,series.Minimum,series.Maximum}:new[]{0,series.Maximum,series.Minimum};
        foreach(double value in extremes.Distinct())
        {
            double y=PointAt(0,value).Y;
            dc.DrawLine(new Pen(grid,value==0?1.5:1),new Point(plot.Left,y),new Point(plot.Right,y));
            if(labeled.All(prior=>Math.Abs(prior-y)>16*FontSize/12)){Text(Number(value),6,y-7);labeled.Add(y);}
        }
        Text("x/L",plot.Right-17,plot.Top-18);
        dc.PushClip(new RectangleGeometry(new Rect(plot.Left-4,plot.Top-4,plot.Width+8,plot.Height+8)));
        if(!series.HasAmbiguousStations)
        {
            for(int i=1;i<series.Samples.Length;i++)
            {
                var a=series.Samples[i-1];var b=series.Samples[i];
                void Segment(double x0,double v0,double x1,double v1)
                {
                    var color=v0+v1>=0?Blue:Orange;
                    if(FillDiagram)
                    {
                        var geometry=new StreamGeometry();
                        using(var c=geometry.Open()) {c.BeginFigure(PointAt(x0,0),true,true);c.LineTo(PointAt(x0,v0),true,false);c.LineTo(PointAt(x1,v1),true,false);c.LineTo(PointAt(x1,0),true,false);}
                        var fill=color.Clone();fill.Opacity=.20;dc.DrawGeometry(fill,null,geometry);
                    }
                    dc.DrawLine(new Pen(color,1.8),PointAt(x0,v0),PointAt(x1,v1));
                }
                if(a.Value*b.Value<0)
                {
                    double zero=a.Station+(b.Station-a.Station)*(-a.Value/(b.Value-a.Value));
                    Segment(a.Station,a.Value,zero,0);Segment(zero,0,b.Station,b.Value);
                }
                else Segment(a.Station,a.Value,b.Station,b.Value);
            }
        }
        foreach(var point in series.Samples)dc.DrawEllipse(point.Value>=0?Blue:Orange,new Pen(Background??Brushes.White,1),PointAt(point.Station,point.Value),3,3);
        double station=Math.Clamp(Station,0,1);double cursorX=PointAt(station,0).X;
        dc.DrawLine(new Pen(ink,1){DashStyle=DashStyles.Dash},new Point(cursorX,plot.Top),new Point(cursorX,plot.Bottom));
        var read=series.At(station);
        if(read.Value is double val)dc.DrawEllipse(ink,null,PointAt(station,val),3.5,3.5);
        dc.Pop();
        Text(read.Value is double cursorValue?$"{(read.Imported?"●":"≈")} {Number(cursorValue)}":"—",Math.Max(12,ActualWidth-135*FontSize/12),8,11,bold:true);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point=e.GetPosition(this);
        if(!CursorLocked && plot.Width>0 && plot.Contains(point))SetCurrentValue(StationProperty,Math.Clamp((point.X-plot.Left)/plot.Width,0,1));
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);Focus();
        var point=e.GetPosition(this);
        if(plot.Width>0 && plot.Contains(point)){SetCurrentValue(StationProperty,Math.Clamp((point.X-plot.Left)/plot.Width,0,1));e.Handled=true;}
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.Key is Key.Left or Key.Right){SetCurrentValue(StationProperty,Math.Clamp(Station+(e.Key==Key.Left?-.01:.01),0,1));e.Handled=true;}
        else base.OnKeyDown(e);
    }
}
