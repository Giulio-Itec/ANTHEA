using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Anthea.Reports;

namespace X.Desktop.Views.EmbeddedWalls;

/// <summary>Display coordinates only; consumes the frozen engineering result without recomputing it.</summary>
public sealed class EmbeddedWallDiagram : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points), typeof(IReadOnlyList<EmbeddedWallPointData>), typeof(EmbeddedWallDiagram), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ComparisonProperty = DependencyProperty.Register(nameof(Comparison), typeof(IReadOnlyList<EmbeddedWallPointData>), typeof(EmbeddedWallDiagram), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty QuantityProperty = DependencyProperty.Register(nameof(Quantity), typeof(string), typeof(EmbeddedWallDiagram), new FrameworkPropertyMetadata("Spostamento [mm]", FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<EmbeddedWallPointData>? Points { get => (IReadOnlyList<EmbeddedWallPointData>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public IReadOnlyList<EmbeddedWallPointData>? Comparison { get => (IReadOnlyList<EmbeddedWallPointData>?)GetValue(ComparisonProperty); set => SetValue(ComparisonProperty, value); }
    public string Quantity { get => (string)GetValue(QuantityProperty); set => SetValue(QuantityProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var ink = TryFindResource("Token.Ink") as Brush ?? Brushes.Black;
        var paper = TryFindResource("Token.Paper") as Brush ?? Brushes.White;
        dc.DrawRectangle(paper, null, new Rect(RenderSize));
        void Text(string s, double x, double y) => dc.DrawText(new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        if (Points is null || Points.Count == 0) { Text("Calcolare il modello e selezionare una fase.", 20, 20); return; }
        double Value(EmbeddedWallPointData p) => Quantity switch { "Momento [kNm]" => p.Moment, "Taglio [kN]" => p.Shear, "Normale [kN]" => p.Normal,
            "Pressione sinistra [kPa]" => p.LeftPressure, "Pressione destra [kPa]" => p.RightPressure, _ => p.Displacement };
        var all = Points.Concat(Comparison ?? []).ToArray(); double top = all.Max(p => p.Elevation), bottom = all.Min(p => p.Elevation);
        double max = Math.Max(1e-10, all.Max(p => Math.Abs(Value(p))));
        double left = 90, right = Math.Max(left + 50, ActualWidth - 35), h = Math.Max(50, ActualHeight - 75), centre = (left + right) / 2;
        Point Pixel(EmbeddedWallPointData p) => new(centre + Value(p) / max * (right - left) / 2, 35 + (top - p.Elevation) / Math.Max(1e-10, top - bottom) * h);
        dc.DrawLine(new Pen(ink, 1), new Point(centre, 35), new Point(centre, 35 + h));
        for (int i = 0; i <= 4; i++)
        { double y = 35 + h * i / 4; Text((top - (top - bottom) * i / 4).ToString("G5") + " m", 5, y - 7); dc.DrawLine(new Pen(ink, .2), new Point(left, y), new Point(right, y)); }
        Text(Quantity + " — blu: fase selezionata; arancio: confronto", left, 8);
        Text((-max).ToString("G5"), left, 40 + h); Text("0", centre, 40 + h); Text(max.ToString("G5"), right - 45, 40 + h);
        void Curve(IReadOnlyList<EmbeddedWallPointData>? points, Brush brush)
        {
            if (points is null || points.Count < 2) return;
            var geometry = new StreamGeometry(); using (var c = geometry.Open()) { c.BeginFigure(Pixel(points[0]), false, false); c.PolyLineTo(points.Skip(1).Select(Pixel).ToArray(), true, false); }
            geometry.Freeze(); dc.DrawGeometry(null, new Pen(brush, 2), geometry);
        }
        Curve(Comparison, TryFindResource("Token.Series.Orange") as Brush ?? ink); Curve(Points, TryFindResource("Token.Series.Blue") as Brush ?? ink);
    }
}
