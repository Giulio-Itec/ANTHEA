using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Presentation.Modules.Climate;

namespace X.Desktop.Views.Climate;

/// <summary>Signed bars of already calculated quantities in one unit. Scaling is graphical only.</summary>
public sealed class ClimateCaseChart : Control
{
    private const double RowHeight=24, HeaderHeight=36, LabelFraction=.32, BarHalfWidthFraction=.46;
    private const double PlotPadding=24, BottomPadding=4, ValueGap=10, LabelPadding=14, TitlePadding=16, FirstRowTop=32;
    public static readonly DependencyProperty ValuesProperty=DependencyProperty.Register(nameof(Values),typeof(IReadOnlyList<ClimateQuantityRow>),typeof(ClimateCaseChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<ClimateQuantityRow>? Values{get=>(IReadOnlyList<ClimateQuantityRow>?)GetValue(ValuesProperty);set=>SetValue(ValuesProperty,value);}
    public ClimateCaseChart(){SetResourceReference(BackgroundProperty,"Token.Paper");SetResourceReference(ForegroundProperty,"Token.Ink");SetResourceReference(BorderBrushProperty,"Token.Border");ClipToBounds=true;}
    private ClimateQuantityRow[] Plotted()
    {
        var source=Values??[];string? unit=source.FirstOrDefault()?.Unit;
        return unit is null?[]:source.Where(v=>v.Unit==unit).ToArray();
    }
    protected override Size MeasureOverride(Size available)=>new(double.IsInfinity(available.Width)?600:available.Width,Math.Max(80,HeaderHeight+Plotted().Length*RowHeight));
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var rows=Plotted();if(rows.Length==0)return;
        dc.DrawRectangle(Background,null,new Rect(0,0,ActualWidth,ActualHeight));
        double labelWidth=Math.Min(200,ActualWidth*LabelFraction),valueWidth=100,plotWidth=Math.Max(20,ActualWidth-labelWidth-valueWidth-PlotPadding),zero=labelWidth+plotWidth/2;
        double max=rows.Max(r=>Math.Abs(r.Value));if(max==0)max=1;
        var blue=TryFindResource("Token.Series.Blue") as Brush??Foreground;var orange=TryFindResource("Token.Series.Orange") as Brush??Foreground;
        void Text(string s,double x,double y,double width)
        {var t=new FormattedText(s,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,Foreground,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=Math.Max(1,width),MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis};dc.DrawText(t,new Point(x,y));}
        Text("Valori del caso ["+rows[0].Unit+"]",8,4,ActualWidth-TitlePadding);Text("−",labelWidth,4,20);Text("+",labelWidth+plotWidth-LabelPadding,4,20);
        dc.DrawLine(new Pen(BorderBrush,1),new Point(zero,27),new Point(zero,ActualHeight-BottomPadding));
        for(int i=0;i<rows.Length;i++)
        {
            var row=rows[i];double y=FirstRowTop+i*RowHeight,length=Math.Abs(row.Value)/max*plotWidth*BarHalfWidthFraction;
            Text(row.Symbol,8,y,labelWidth-LabelPadding);dc.DrawRectangle(row.Value<0?orange:blue,null,new Rect(row.Value<0?zero-length:zero,y+2,length,13));
            Text(row.DisplayValue, labelWidth+plotWidth+ValueGap,y,valueWidth);
        }
    }
}
