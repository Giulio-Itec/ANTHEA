using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

/// <summary>ANTHEA schematic lithology symbols, in display pixels. No engineering properties or normative classification.</summary>
internal static class SoilHatchBrush
{
    private const double TilePixels = 24, InkOpacity = .55, StrokePixels = .8;
    private const int DarkColorChannelSum = 382;
    internal static Brush? Create(string soilType, Color background)
    {
        if (soilType == "Non classificato") return null;
        var group = new DrawingGroup();
        Brush ink = background.R + background.G + background.B < DarkColorChannelSum ? Brushes.White : Brushes.Black;
        var pen = new Pen(ink, StrokePixels);
        using (var dc = group.Open())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, TilePixels, TilePixels));
            void Line(double x, double y, double endX, double endY) => dc.DrawLine(pen, new Point(x, y), new Point(endX, endY));
            switch (soilType)
            {
                case "Sabbia":
                    dc.DrawEllipse(ink, null, new Point(6, 6), 1, 1);
                    dc.DrawEllipse(ink, null, new Point(18, 18), 1, 1);
                    break;
                case "Ghiaia":
                    dc.DrawEllipse(null, pen, new Point(6, 7), 4, 3);
                    dc.DrawEllipse(null, pen, new Point(18, 18), 3, 4);
                    break;
                case "Limo":
                    Line(2, 6, 10, 6); Line(14, 18, 22, 18);
                    break;
                case "Argilla":
                    Line(0, 6, 24, 6); Line(0, 18, 24, 18);
                    break;
                case "Riporto":
                    Line(3, 3, 9, 9); Line(3, 9, 9, 3);
                    Line(15, 15, 21, 21); Line(15, 21, 21, 15);
                    break;
                case "Terreno organico":
                    Line(6, 4, 6, 20); Line(6, 10, 2, 6); Line(6, 15, 10, 11);
                    Line(18, 4, 18, 20); Line(18, 10, 14, 6); Line(18, 15, 22, 11);
                    break;
                case "Roccia":
                    Line(0, 24, 24, 0); Line(0, 12, 12, 0); Line(12, 24, 24, 12);
                    break;
                default:
                    Line(4, 6, 8, 6); Line(6, 4, 6, 8);
                    Line(16, 18, 20, 18); Line(18, 16, 18, 20);
                    break;
            }
        }
        var tile = new Rect(0, 0, TilePixels, TilePixels);
        var brush = new DrawingBrush(group) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute,
            ViewboxUnits = BrushMappingMode.Absolute, Viewport = tile, Viewbox = tile, Stretch = Stretch.None, Opacity = InkOpacity };
        brush.Freeze(); return brush;
    }
}
