using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

/// <summary>Icon of the reinforcing steel module, moved unchanged from the end of RebarMaterialView.cs when the historic view was archived (f5-pass-geo).</summary>
internal sealed class RebarIcon : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        var rib = new Pen(Ui.Brush("#C7953E"), 3);
        for (int bar = 0; bar < 3; bar++)
        {
            double y = 20 + bar * 19;
            dc.DrawLine(new Pen(Appearance.Ink, 9), new Point(12, y + 12), new Point(68, y - 5));
            for (int i = 0; i < 6; i++) { double x = 16 + i * 9, cy = y + 12 - (x - 12) * 17 / 56; dc.DrawLine(rib, new Point(x - 2, cy - 4), new Point(x + 2, cy + 4)); }
        }
    }
}
