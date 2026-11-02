using System.Globalization;
using System.Windows;
using System.Windows.Media;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

/// <summary>Shared visual language of Actions: surface, text, series order and selection. Drawing only.</summary>
internal static class ActionDrawingStyle
{
    internal const double BodySize=12,TitleSize=15,LineWidth=2,SelectionWidth=4;
    private const double TextPadding=12,ArrowLength=8,ArrowHalfWidth=4,HeadLimitDivisor=3;
    internal static Brush Ink=>ThemeTokens.Brush("Ink");
    internal static Brush Selection=>ThemeTokens.Brush("Series.Magenta");
    internal static Brush[] Palette=>[ThemeTokens.Brush("Series.Blue"),ThemeTokens.Brush("Series.Orange"),ThemeTokens.Brush("Series.Green"),ThemeTokens.Brush("Series.Magenta"),ThemeTokens.Brush("Series.Red"),ThemeTokens.Brush("Series.Steel"),ThemeTokens.Brush("Series.Amber")];
    internal static void Surface(FrameworkElement view,DrawingContext dc)=>dc.DrawRectangle(ThemeTokens.Brush("Paper"),null,new Rect(0,0,Math.Max(0,view.ActualWidth),Math.Max(0,view.ActualHeight)));
    internal static void Text(FrameworkElement view,DrawingContext dc,string text,double x,double y,double size=BodySize,Brush? brush=null,double? width=null)
    {
        var formatted=new FormattedText(text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush??Ink,VisualTreeHelper.GetDpi(view).PixelsPerDip)
        {MaxTextWidth=Math.Max(1,width??view.ActualWidth-x-TextPadding),MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis};
        dc.DrawText(formatted,new Point(x,y));
    }
    internal static void Arrow(DrawingContext dc,Point start,Point end,Brush brush,double width=LineWidth,bool both=false)
    {
        var pen=new Pen(brush,width);dc.DrawLine(pen,start,end);
        void Head(Point a,Point b)
        {
            var v=b-a;if(v.Length<1e-6)return;double length=Math.Min(ArrowLength,v.Length/HeadLimitDivisor),half=length*ArrowHalfWidth/ArrowLength;v.Normalize();var normal=new Vector(-v.Y,v.X);
            dc.DrawLine(pen,b,b-v*length+normal*half);dc.DrawLine(pen,b,b-v*length-normal*half);
        }
        Head(start,end);if(both)Head(end,start);
    }
}
