using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

/// <summary>Shared viewport annotation. Only screen clipping is performed here.</summary>
internal static class NeutralAxisOverlay
{
    internal sealed record Display(string Caption, Point? Start, Point? End);
    internal static Display Draw(DrawingContext dc, SectionNeutralAxis axis, Func<double, double, Point> map,
        Rect sectionBounds, Rect plot, double captionY)
    {
        Point? start = null, end = null;
        string caption = axis.Caption;
        if (axis.Line is { } line)
        {
            Point p = map(line.Start.X, line.Start.Y), q = map(line.End.X, line.End.Y);
            Vector direction = q - p;
            if (double.IsFinite(direction.Length) && direction.Length > 0 && double.IsFinite(p.X) && double.IsFinite(p.Y))
            {
                direction.Normalize();
                if (!Clip(p, direction, sectionBounds, out _, out _)) caption += " · esterno all’ingombro";
                if (Clip(p, direction, plot, out var a, out var b))
                {
                    start = a; end = b;
                    dc.DrawLine(new Pen(Brushes.White, 4.5), a, b);
                    dc.DrawLine(new Pen(Ui.Brush("#9B276A"), 2) { DashStyle = DashStyles.Dash }, a, b);
                }
                else caption += " · fuori vista";
            }
            else caption += " · non rappresentabile";
        }
        else caption += " · non definito (campo uniforme)";
        var text = new FormattedText(caption, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 11, Ui.Brush("#9B276A"), 1) { MaxTextWidth = Math.Max(30, plot.Width - 14) };
        var label = new Rect(plot.Left, captionY, Math.Min(plot.Width, text.Width + 12), text.Height + 6);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(238, 255, 255, 255)), new Pen(Ui.Brush("#DFC4D4"), .7), label, 3, 3);
        dc.DrawText(text, new Point(label.X + 6, label.Y + 3));
        return new(caption, start, end);
    }

    private static bool Clip(Point p, Vector d, Rect r, out Point a, out Point b)
    {
        double t0 = double.NegativeInfinity, t1 = double.PositiveInfinity;
        bool Slab(double origin, double direction, double min, double max)
        {
            if (Math.Abs(direction) < 1e-14) return origin >= min && origin <= max;
            double u = (min - origin) / direction, v = (max - origin) / direction;
            t0 = Math.Max(t0, Math.Min(u, v)); t1 = Math.Min(t1, Math.Max(u, v)); return t0 <= t1;
        }
        a = b = default;
        if (!Slab(p.X, d.X, r.Left, r.Right) || !Slab(p.Y, d.Y, r.Top, r.Bottom)) return false;
        a = p + t0 * d; b = p + t1 * d; return true;
    }
}
