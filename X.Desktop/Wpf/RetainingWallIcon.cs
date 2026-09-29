using System.Windows;
using System.Windows.Media;
namespace X.Desktop;
internal sealed class RetainingWallIcon : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Ui.Brush("#E9DDC7"), null, new Rect(34, 15, 38, 48));
        var path = new StreamGeometry(); using (var p = path.Open()) { p.BeginFigure(new(9, 68), true, true); p.PolyLineTo([new(72, 68), new(72, 58), new(37, 58), new(37, 12), new(29, 12), new(23, 58), new(9, 58)], true, false); }
        dc.DrawGeometry(Ui.Brush("#CAD6E0"), new Pen(Ui.Navy, 2), path);
    }
}

