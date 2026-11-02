using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Actions;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

/// <summary>Vector assistance common to every Actions sheet. Highlights data associations, never computes results.</summary>
public sealed class ActionInputSketch:FrameworkElement
{
    private const double CanvasWidth=1000,CanvasHeight=320,Padding=24,VerticalPadding=12,Half=2,LabelDivisor=3,LabelGap=10,LabelRise=8,DimensionLabelGap=8,TextRise=24,LabelHeight=19,LabelStep=23,LabelPadding=4,LabelOffset=100,OverlapPenalty=100;
    public static readonly DependencyProperty DrawingProperty=Property(nameof(Drawing),typeof(ActionSketch));
    public static readonly DependencyProperty ActiveKeyProperty=Property(nameof(ActiveKey),typeof(string));
    public static readonly DependencyProperty ActiveTableProperty=Property(nameof(ActiveTable),typeof(string));
    public static readonly DependencyProperty ActiveRowProperty=DependencyProperty.Register(nameof(ActiveRow),typeof(int),typeof(ActionInputSketch),new FrameworkPropertyMetadata(-1,FrameworkPropertyMetadataOptions.AffectsRender));
    private static DependencyProperty Property(string name,Type type)=>DependencyProperty.Register(name,type,typeof(ActionInputSketch),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public ActionSketch? Drawing{get=>(ActionSketch?)GetValue(DrawingProperty);set=>SetValue(DrawingProperty,value);}
    public string? ActiveKey{get=>(string?)GetValue(ActiveKeyProperty);set=>SetValue(ActiveKeyProperty,value);}
    public string? ActiveTable{get=>(string?)GetValue(ActiveTableProperty);set=>SetValue(ActiveTableProperty,value);}
    public int ActiveRow{get=>(int)GetValue(ActiveRowProperty);set=>SetValue(ActiveRowProperty,value);}
    public IReadOnlyList<string> HighlightedParts=>Drawing?.Parts.Where(Selected).Select(p=>p.Id).ToArray()??[];
    private bool Selected(ActionSketchPart p)=>p.Id.StartsWith("FireFaces:",StringComparison.Ordinal)
        ?ActiveTable=="FireFaces"&&p.Id=="FireFaces:"+ActiveRow
        :p.Keys.Split(' ').Contains(ActiveKey??"")||p.Keys.Split(' ').Contains(ActiveTable??"");
    public IReadOnlyList<Rect> LabelBounds{get;private set;}=[];
    protected override Size MeasureOverride(Size available)=>new(double.IsInfinity(available.Width)?700:available.Width,double.IsInfinity(available.Height)?250:available.Height);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);ActionDrawingStyle.Surface(this,dc);
        if(Drawing is not { } drawing)return;
        double scale=Math.Min(Math.Max(1,ActualWidth-Padding)/CanvasWidth,Math.Max(1,ActualHeight-VerticalPadding)/CanvasHeight),left=(ActualWidth-CanvasWidth*scale)/2,top=6;
        Point P(ActionSketchPoint p)=>new(left+p.X*scale,top+p.Y*scale);
        var labels=new List<(string Text,double X,double Y,Brush Colour)>();
        var strokes=new List<Rect>();
        foreach(var part in drawing.Parts.OrderBy(p=>Selected(p)))
        {
            if(part.Points.Count<2)continue;
            bool selected=Selected(part);var colour=selected?ActionDrawingStyle.Selection:part.Role switch
            {"action"=>ThemeTokens.Brush("Series.Orange"),"opposing"=>ThemeTokens.Brush("Series.Green"),"dimension"=>ThemeTokens.Brush("Series.Blue"),_=>ActionDrawingStyle.Ink};
            var pen=new Pen(colour,selected?ActionDrawingStyle.SelectionWidth:ActionDrawingStyle.LineWidth);
            var points=part.Points.Select(P).ToArray();
            for(int n=1;n<points.Length;n++)
            {
                // Only thin segment bounds, not the whole area of a polygon.
                var box=new Rect(points[n-1],points[n]);if(box.Width<LabelHeight||box.Height<LabelHeight){box.Inflate(1,1);strokes.Add(box);}
            }
            if(part.Kind is "arrow" or "dimension")ActionDrawingStyle.Arrow(dc,points[0],points[^1],colour,pen.Thickness,part.Kind=="dimension");
            else
            {
                var geometry=new StreamGeometry();using(var ctx=geometry.Open()){ctx.BeginFigure(points[0],part.Kind=="polygon",false);ctx.PolyLineTo(points.Skip(1).ToArray(),true,false);}
                if(part.Kind=="polygon"){dc.PushOpacity(selected?.12:.05);dc.DrawGeometry(colour,null,geometry);dc.Pop();}
                dc.DrawGeometry(null,pen,geometry);
            }
            if(part.Label.Length==0)continue;
            // Anchor the label away from the selected stroke; values remain legible in the common assistance card.
            var a=points[0];var b=part.Kind=="polygon"?points[1]:points[^1];double x=(a.X+b.X)/2,y=(a.Y+b.Y)/2;
            if(Math.Abs(a.X-b.X)<4){x+=LabelGap;y-=LabelRise;}else{y+=part.Kind=="dimension"?DimensionLabelGap:-TextRise;x-=Math.Min(60,Math.Abs(a.X-b.X)/LabelDivisor);}
            labels.Add(((selected?"▸ ":"")+part.Label,x,y,colour));
        }
        var bounds=new List<Rect>();
        foreach(var label in labels.OrderByDescending(l=>l.Text.StartsWith("▸",StringComparison.Ordinal)))
        {
            var text=new FormattedText(label.Text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),ActionDrawingStyle.BodySize,label.Colour,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double width=Math.Min(ActualWidth-LabelPadding-LabelPadding,text.WidthIncludingTrailingWhitespace+LabelPadding);
            Rect Candidate(double dx,double dy)=>new(Math.Clamp(label.X+dx,LabelPadding,Math.Max(LabelPadding,ActualWidth-width-LabelPadding)),Math.Clamp(label.Y+dy,0,Math.Max(0,ActualHeight-LabelHeight)),Math.Max(1,width),LabelHeight);
            var candidates=(from dy in new[]{0,-LabelStep,LabelStep,-LabelStep*2,LabelStep*2} from dx in new[]{0,-LabelOffset,LabelOffset} select Candidate(dx,dy)).ToArray();
            var box=candidates.OrderBy(r=>bounds.Count(b=>b.IntersectsWith(r))*OverlapPenalty+strokes.Count(b=>b.IntersectsWith(r))).First();
            bounds.Add(box);dc.DrawRectangle(ThemeTokens.Brush("Paper"),null,box);
            ActionDrawingStyle.Text(this,dc,label.Text,box.X,box.Y,ActionDrawingStyle.BodySize,label.Colour,box.Width);
        }
        LabelBounds=bounds;
    }
}
