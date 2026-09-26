using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal sealed class BridgeResponsePlot : FrameworkElement
{
    internal Point[] Points = [];
    internal string XLabel = "", YLabel = "";
    internal int Selected;
    internal Brush CurveBrush = Ui.Blue;
    internal BridgeResponsePlot() { MinHeight = 260; ClipToBounds = true; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); double w = ActualWidth, h = ActualHeight;
        if (w < 180 || h < 140) return;
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
        var box = new Rect(72, 20, w - 92, h - 76);
        void Text(string value, double x, double y, double size = 11, Brush? color = null) => dc.DrawText(new FormattedText(value, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, color ?? Ui.Muted, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        Text(YLabel, 8, 0, 12, Ui.Navy); Text(XLabel, Math.Max(72, w / 2 - 70), h - 23, 12, Ui.Navy);
        if (Points.Length == 0) { Text("Calcolare la curva per visualizzare i punti", 85, h / 2); return; }
        double xmin = Math.Min(0, Points.Min(p => p.X)), xmax = Math.Max(0, Points.Max(p => p.X));
        double ymin = Math.Min(0, Points.Min(p => p.Y)), ymax = Math.Max(0, Points.Max(p => p.Y));
        if (xmax == xmin) { xmin -= 1; xmax += 1; } if (ymax == ymin) { ymin -= 1; ymax += 1; }
        double dx = (xmax - xmin) * .04, dy = (ymax - ymin) * .08; xmin -= dx; xmax += dx; ymin -= dy; ymax += dy;
        Point Map(Point p) => new(box.X + (p.X - xmin) / (xmax - xmin) * box.Width, box.Bottom - (p.Y - ymin) / (ymax - ymin) * box.Height);
        var grid = new Pen(Ui.Brush("#E8EDF3"), 1);
        for (int i = 0; i <= 4; i++)
        {
            double x = xmin + (xmax - xmin) * i / 4, y = ymin + (ymax - ymin) * i / 4;
            double px = Map(new(x, 0)).X, py = Map(new(0, y)).Y;
            dc.DrawLine(grid, new(px, box.Top), new(px, box.Bottom)); dc.DrawLine(grid, new(box.Left, py), new(box.Right, py));
            Text(x.ToString("G3", CultureInfo.GetCultureInfo("it-IT")), px - 20, box.Bottom + 7);
            Text(y.ToString("G4", CultureInfo.GetCultureInfo("it-IT")), 4, py - 8);
        }
        var zero = Map(new()); var axis = new Pen(Ui.Brush("#94A3B8"), 1);
        dc.DrawLine(axis, new(box.Left, zero.Y), new(box.Right, zero.Y)); dc.DrawLine(axis, new(zero.X, box.Top), new(zero.X, box.Bottom));
        var path = new StreamGeometry(); using (var c = path.Open()) { c.BeginFigure(Map(Points[0]), false, false); c.PolyLineTo(Points.Skip(1).Select(Map).ToArray(), true, false); }
        dc.DrawGeometry(null, new Pen(CurveBrush, 2.4), path);
        foreach (var p in Points.Where((_, i) => i % Math.Max(1, Points.Length / 25) == 0)) dc.DrawEllipse(CurveBrush, null, Map(p), 2, 2);
        var selected = Map(Points[Math.Clamp(Selected, 0, Points.Length - 1)]);
        dc.DrawEllipse(Brushes.White, new Pen(CurveBrush, 2.5), selected, 5, 5);
    }
}
