using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Reports;

namespace X.Desktop.Views.EmbeddedWalls;

/// <summary>Rendering only: terrain, bodies and the sampled slip base from the immutable result.</summary>
public sealed class EmbeddedWallGlobalDiagram : FrameworkElement
{
    // Device-independent pixels and viewport safeguards only, no engineering constants.
    private const double BottomTextMargin = 26;
    public static readonly DependencyProperty CheckProperty = DependencyProperty.Register(nameof(Check), typeof(EmbeddedWallGlobalData),
        typeof(EmbeddedWallGlobalDiagram), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public EmbeddedWallGlobalData? Check { get => (EmbeddedWallGlobalData?)GetValue(CheckProperty); set => SetValue(CheckProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var ink = (Brush)FindResource("Token.Ink"); var paper = (Brush)FindResource("Token.Paper");
        var accent = (Brush)FindResource("Token.Series.Blue");
        dc.DrawRectangle(paper, null, new Rect(RenderSize));
        void Text(string text, double x, double y) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        var result = Check;
        if (result is null || result.Surface.Count == 0) { Text("Selezionare una verifica globale con geometria disponibile.", 16, 16); return; }
        var slip = result.Slices.Select(s => new EmbeddedWallGlobalPoint((s.Left + s.Right) / 2, s.BaseY)).ToArray();
        var all = result.Surface.Concat(result.Bodies.SelectMany(b => b.Points)).Concat(slip).ToArray();
        double xmin = all.Min(p => p.X), xmax = all.Max(p => p.X), ymin = all.Min(p => p.Y), ymax = all.Max(p => p.Y);
        const double margin = 50, top = 45, bottom = 42;
        double scale = Math.Min(Math.Max(20, ActualWidth - 2 * margin) / Math.Max(.01, xmax - xmin),
            Math.Max(20, ActualHeight - top - bottom) / Math.Max(.01, ymax - ymin));
        double x0 = (ActualWidth - scale * (xmax - xmin)) / 2;
        Point Pixel(EmbeddedWallGlobalPoint p) => new(x0 + (p.X - xmin) * scale, top + (ymax - p.Y) * scale);
        void Line(IReadOnlyList<EmbeddedWallGlobalPoint> points, Pen pen, bool closed = false)
        {
            for (int i = 1; i < points.Count; i++) dc.DrawLine(pen, Pixel(points[i - 1]), Pixel(points[i]));
            if (closed && points.Count > 1) dc.DrawLine(pen, Pixel(points[^1]), Pixel(points[0]));
        }
        Line(result.Surface, new Pen(ink, 2));
        foreach (var body in result.Bodies) Line(body.Points, new Pen(ink, 2), true);
        foreach (var slice in result.Slices)
        {
            double x = (slice.Left + slice.Right) / 2;
            dc.DrawLine(new Pen(ink, .5), Pixel(new(x, slice.BaseY)), Pixel(new(x, slice.TopY)));
        }
        Line(slip, new Pen(accent, 3));
        Text(result.Stage + " · " + result.Id + " · " + result.OutcomeText, 16, 12);
        Text($"Valle ← · x = {xmin:G4} … {xmax:G4} m · y = {ymin:G4} … {ymax:G4} m · testa y=0", 16, Math.Max(top, ActualHeight - BottomTextMargin));
    }
}
