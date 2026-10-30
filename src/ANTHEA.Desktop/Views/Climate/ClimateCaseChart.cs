using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Presentation.Modules.Climate;

namespace X.Desktop.Views.Climate;

/// <summary>Signed bars of already calculated quantities in one unit. Scaling is graphical only.</summary>
public sealed class ClimateCaseChart : Control
{
    public static readonly DependencyProperty ValuesProperty=DependencyProperty.Register(nameof(Values),typeof(IReadOnlyList<ClimateQuantityRow>),typeof(ClimateCaseChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<ClimateQuantityRow>? Values{get=>(IReadOnlyList<ClimateQuantityRow>?)GetValue(ValuesProperty);set=>SetValue(ValuesProperty,value);}
    public ClimateCaseChart(){SetResourceReference(BackgroundProperty,"Token.Paper");SetResourceReference(ForegroundProperty,"Token.Ink");SetResourceReference(BorderBrushProperty,"Token.Border");ClipToBounds=true;}
    private ClimateQuantityRow[] Plotted()
    {
        var source=Values??[];string? unit=source.FirstOrDefault()?.Unit;
        return unit is null?[]:source.Where(v=>v.Unit==unit).ToArray();
    }
    protected override Size MeasureOverride(Size available)=>new(double.IsInfinity(available.Width)?600:available.Width,Math.Max(80,36+Plotted().Length*24));
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var rows=Plotted();if(rows.Length==0)return;
        dc.DrawRectangle(Background,null,new Rect(0,0,ActualWidth,ActualHeight));
        double labelWidth=Math.Min(200,ActualWidth*.32),valueWidth=100,plotWidth=Math.Max(20,ActualWidth-labelWidth-valueWidth-24),zero=labelWidth+plotWidth/2;
        double max=rows.Max(r=>Math.Abs(r.Value));if(max==0)max=1;
        var blue=TryFindResource("Token.Series.Blue") as Brush??Foreground;var orange=TryFindResource("Token.Series.Orange") as Brush??Foreground;
        void Text(string s,double x,double y,double width)
        {var t=new FormattedText(s,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,Foreground,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=Math.Max(1,width),MaxLineCount=1,Trimming=TextTrimming.CharacterEllipsis};dc.DrawText(t,new Point(x,y));}
        Text("Valori del caso ["+rows[0].Unit+"]",8,4,ActualWidth-16);Text("−",labelWidth,4,20);Text("+",labelWidth+plotWidth-14,4,20);
        dc.DrawLine(new Pen(BorderBrush,1),new Point(zero,27),new Point(zero,ActualHeight-4));
        for(int i=0;i<rows.Length;i++)
        {
            var row=rows[i];double y=32+i*24,length=Math.Abs(row.Value)/max*plotWidth*.46;
            Text(row.Symbol,8,y,labelWidth-14);dc.DrawRectangle(row.Value<0?orange:blue,null,new Rect(row.Value<0?zero-length:zero,y+2,length,13));
            Text(row.DisplayValue, labelWidth+plotWidth+10,y,valueWidth);
        }
    }
}
