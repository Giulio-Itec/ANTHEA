using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Actions;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

public sealed class FireBoundaryChart:FrameworkElement
{
    private const double DefaultWidth=700,MinimumWidth=420,Margin=70,Top=75,PlotHeight=230,LegendTop=355,Row=26,Gap=10,Half=.5,SelectedOutline=7;
    public static readonly DependencyProperty DrawingProperty=DependencyProperty.Register(nameof(Drawing),typeof(FireDrawing),typeof(FireBoundaryChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty SelectedFaceProperty=DependencyProperty.Register(nameof(SelectedFace),typeof(int),typeof(FireBoundaryChart),new FrameworkPropertyMetadata(-1,FrameworkPropertyMetadataOptions.AffectsRender));
    public int SelectedFace{get=>(int)GetValue(SelectedFaceProperty);set=>SetValue(SelectedFaceProperty,value);}
    public FireDrawing? Drawing{get=>(FireDrawing?)GetValue(DrawingProperty);set=>SetValue(DrawingProperty,value);}
    protected override Size MeasureOverride(Size available)=>new(double.IsInfinity(available.Width)?DefaultWidth:Math.Max(MinimumWidth,available.Width),LegendTop+(Drawing?.Faces.Count??1)*Row+Row);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);ActionDrawingStyle.Surface(this,dc);var ink=ThemeTokens.Brush("Ink");Brush[] colours=ActionDrawingStyle.Palette;
        void Text(string s,double x,double y,Brush? colour=null)=>ActionDrawingStyle.Text(this,dc,s,x,y,12,colour);
        var d=Drawing;if(d is null){Text("Calcolare per visualizzare le facce e le condizioni al contorno.",Margin,Gap);return;}
        Text("Facce assegnate · "+d.Name,Margin,Gap);Text("Geometria in metri, scala uniforme. Colori identificativi delle facce.",Margin,Gap+Row);
        double xmin=d.Faces.SelectMany(f=>new[]{f.X0,f.X1}).Min(),xmax=d.Faces.SelectMany(f=>new[]{f.X0,f.X1}).Max(),ymin=d.Faces.SelectMany(f=>new[]{f.Y0,f.Y1}).Min(),ymax=d.Faces.SelectMany(f=>new[]{f.Y0,f.Y1}).Max();
        double xr=xmax-xmin,yr=ymax-ymin;if(xr==0)xr=1;if(yr==0)yr=1;double width=Math.Max(MinimumWidth,ActualWidth)-Margin-Margin,scale=Math.Min(width/xr,PlotHeight/yr);
        double X(double x)=>Margin+Half*(width-scale*xr)+(x-xmin)*scale;double Y(double y)=>Top+PlotHeight-(y-ymin)*scale;
        for(int n=0;n<d.Faces.Count;n++)
        {
            var f=d.Faces[n];var colour=colours[n%colours.Length];if(n==SelectedFace)dc.DrawLine(new Pen(ActionDrawingStyle.Selection,SelectedOutline),new(X(f.X0),Y(f.Y0)),new(X(f.X1),Y(f.Y1)));dc.DrawLine(new Pen(colour,3),new(X(f.X0),Y(f.Y0)),new(X(f.X1),Y(f.Y1)));Text((n+1).ToString(),Half*(X(f.X0)+X(f.X1))+Gap,Half*(Y(f.Y0)+Y(f.Y1))+Gap,colour);
            Text((n==SelectedFace?"▸ ":"")+(n+1)+" · "+f.Name+" · "+f.Exposure+" · α="+f.Alpha.ToString("G5",CultureInfo.CurrentCulture)+" W/(m² K)"+(f.Flux.HasValue?" · hnet="+f.Flux.Value.ToString("G6",CultureInfo.CurrentCulture)+" W/m²":" · flusso non calcolato"),Margin,LegendTop+n*Row,colour);
        }
    }
}
