using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Anthea.Application.Concrete;
using X.Desktop.Themes;

namespace X.Desktop.Views.QuickSection;

/// <summary>Orthographic 3D drawing. Section, bars, axes and actions share the same camera; no section calculation.</summary>
internal sealed class QuickActionsViewport : DrawingView
{
    internal ConcreteGeometry? Section { get; set; }
    internal ConcreteActionScene? Scene { get; set; }
    private double yaw = DefaultYaw, pitch = DefaultPitch, zoom = 1;
    private const double DefaultYaw = 35, DefaultPitch = -18, Roll = -8;
    // Drawing dimensions only: lengths relative to the displayed section and camera angles in degrees.
    private const double DegreesPerPixel = .5, ZoomStep = 1.12, ForceLength = .85, MomentOffset = .65, MomentRadius = .17;
    private const double ArcStartDegrees = -26, ArcSweepDegrees = 297, HeadHalfWidth = .4;
    private const int ArcSegments = 64, BarCircleSegments = 20, FullTurnDegrees = 360, FooterReserve = 27;
    private const double PrismLength = 1.6, AxisLength = .93, AxisArrowInset = .04, ForceLabelPosition = .55;
    private const double HorizontalReserve = 90, VerticalReserve = 65, FooterHeight = 24;
    private const double LabelMargin = 6, LabelGap = 8, LabelWidthReserve = 16, LabelWidthPadding = 6, LabelHeightPadding = 4;
    private const double EmptyMessageReserve = 28, FooterTextOffset = 22, FooterWidthReserve = 18;
    private Matrix3D camera = Camera(DefaultYaw, DefaultPitch);
    private Point? drag;
    private ViewFrame? frame;
    private sealed record ViewFrame(Rect3D Bounds, Size Size, Point3D Pivot, Point Anchor, double Scale);
    internal readonly record struct Projection3D(Matrix3D Camera, Point3D Pivot, Point Anchor, double Scale)
    {
        internal Point Screen(Point3D p)
        {
            var projected = Camera.Transform(p - Pivot);
            return Anchor + new Vector(projected.X, -projected.Y) * Scale;
        }
    }
    internal QuickActionsViewport()
    {
        Focusable = true;
        ToolTip = "Trascina: ruota · rotella: zoom · doppio clic: centra vista";
        MouseLeftButtonDown += (_, e) =>
        {
            Focus(); e.Handled = true;
            if (e.ClickCount == 2) { ResetView(); return; }
            drag = e.GetPosition(this); CaptureMouse();
        };
        MouseMove += (_, e) =>
        {
            if (drag is not Point previous) return;
            var next = e.GetPosition(this); Rotate((next.X - previous.X) * DegreesPerPixel, (next.Y - previous.Y) * DegreesPerPixel); drag = next;
        };
        MouseLeftButtonUp += (_, e) => { drag = null; ReleaseMouseCapture(); e.Handled = true; };
        LostMouseCapture += (_, _) => drag = null;
        MouseWheel += (_, e) => { ZoomBy(e.Delta > 0 ? ZoomStep : 1 / ZoomStep); e.Handled = true; };
    }
    internal void ResetView() { yaw = DefaultYaw; pitch = DefaultPitch; zoom = 1; camera = Camera(yaw, pitch); frame = null; InvalidateVisual(); }
    internal void Rotate(double horizontal, double vertical) { yaw += horizontal; pitch = Math.Clamp(pitch + vertical, -83, 83); camera = Camera(yaw, pitch); InvalidateVisual(); }
    internal void ZoomBy(double factor) { zoom = Math.Clamp(zoom * factor, .4, 3); InvalidateVisual(); }

    internal Point Project(Point3D p)
    {
        var point = camera.Transform(p);
        return new(point.X, -point.Y);
    }
    private static Matrix3D Camera(double yaw, double pitch)
    {
        var matrix = Matrix3D.Identity;
        matrix.Rotate(new Quaternion(new(0, 1, 0), -yaw));
        matrix.Rotate(new Quaternion(new(1, 0, 0), -pitch));
        matrix.Rotate(new Quaternion(new(0, 0, 1), Roll));
        return matrix;
    }
    private double Depth(Point3D p) => camera.Transform(p).Z;
    // Fit once for this geometry and viewport, then orbit around a fixed 3D pivot.
    // Rotation and changes of the selected action must not refit or recenter the drawing.
    internal Projection3D Projection(Size size)
    {
        var section = Section ?? throw new InvalidOperationException("Geometria non disponibile");
        double span = Math.Max(section.Width, section.Height), length = span * PrismLength;
        var bounds = new Rect3D(section.Outline.Min(p => p[0]), section.Outline.Min(p => p[1]), -length,
            section.Width, section.Height, length);
        if (frame is null || frame.Bounds != bounds || frame.Size != size)
        {
            var pivot = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
            var extent = section.Outline.SelectMany(p => new[] { new Point3D(p[0], p[1], 0), new Point3D(p[0], p[1], -length) })
                .Concat(ActionGeometry(Scene, span).SelectMany(g => g.Points))
                .Concat("xyz".Select(a => (Point3D)(Axis(a) * span * AxisLength)))
                .Select(p => Project((Point3D)(p - pivot))).ToArray();
            double minX = extent.Min(p => p.X), maxX = extent.Max(p => p.X), minY = extent.Min(p => p.Y), maxY = extent.Max(p => p.Y);
            double scale = Math.Min(Math.Max(20, size.Width - HorizontalReserve) / (maxX - minX), Math.Max(20, size.Height - VerticalReserve) / (maxY - minY));
            var anchor = new Point(size.Width / 2 - (minX + maxX) / 2 * scale,
                (size.Height - FooterHeight) / 2 - (minY + maxY) / 2 * scale);
            frame = new(bounds, size, pivot, anchor, scale);
        }
        return new(camera, frame.Pivot, frame.Anchor, frame.Scale * zoom);
    }
    private static Vector3D Axis(char axis) => axis switch { 'x' => new(1, 0, 0), 'y' => new(0, 1, 0), _ => new(0, 0, 1) };
    private static Brush AxisBrush(char axis) => ThemeTokens.Brush(axis switch { 'x' => "Series.Red", 'y' => "Series.Green", _ => "Series.Blue" });
    internal sealed record ActionGlyph(ConcreteActionVector Action, IReadOnlyList<Point3D> Points);

    /// <summary>Signed directions on the positive-z cut face. The prism lies at z &lt; 0; negative N points into it.</summary>
    internal static IReadOnlyList<ActionGlyph> ActionGeometry(ConcreteActionScene? scene, double span)
    {
        var result = new List<ActionGlyph>();
        foreach (var action in scene?.Actions ?? [])
        {
            if (action.Value == 0) continue;
            double sign = Math.Sign(action.Value);
            var axis = Axis(action.Axis);
            if (!action.IsMoment)
            {
                var end = (Point3D)(axis * span * ForceLength);
                if (action.Name == "N" && sign < 0) result.Add(new(action, [end, new()]));
                else result.Add(new(action, [new(), (Point3D)(axis * span * ForceLength * sign)]));
                continue;
            }
            // CheckerSection.Local uses -X/-Y relative to the drawn geometric axes.
            // On this +z face Mx+ compresses +y, My+ compresses -x: both couples
            // therefore rotate opposite to a positive right-hand turn about the drawn axis.
            var first = action.Axis == 'x' ? Axis('y') : Axis('z');
            var center = axis * span * MomentOffset;
            var points = Enumerable.Range(0, ArcSegments + 1).Select(i =>
            {
                var rotation = Matrix3D.Identity;
                rotation.Rotate(new Quaternion(axis, ArcStartDegrees - sign * i / ArcSegments * ArcSweepDegrees));
                return (Point3D)(center + span * MomentRadius * rotation.Transform(first));
            }).ToArray();
            result.Add(new(action, points));
        }
        return result;
    }

    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(ThemeTokens.Brush("PaperAlt"), null, new Rect(size));
        if (size.Width < 80 || size.Height < 80) return;
        if (Section is not { Outline.Count: > 2 } section)
        { Text(dc, "Inserire una geometria valida", 14, 30, width: size.Width - EmptyMessageReserve); return; }
        double span = Math.Max(section.Width, section.Height);
        if (!double.IsFinite(span) || span <= 0) return;
        double length = span * PrismLength;
        var rings = new[] { section.Outline.ToArray() }.Concat(section.Holes).ToArray();
        var glyphs = ActionGeometry(Scene, span);
        Point3D P(double[] p, double z) => new(p[0], p[1], z);
        var projection = Projection(size);
        double scale = projection.Scale;
        Point Screen(Point3D p) => projection.Screen(p);
        var edge = new Pen(ThemeTokens.Brush("InkSecondary"), 1.1);
        var fill = ThemeTokens.Brush("Header").Clone(); fill.Opacity = .45;
        var faces = new List<(Point3D[][] Rings, double Depth)>();
        foreach (double z in new[] { 0d, -length })
        {
            var cap = rings.Select(r => r.Select(p => P(p, z)).ToArray()).ToArray();
            faces.Add((cap, cap[0].Average(Depth)));
        }
        foreach (var ring in rings)
            for (int i = 0; i < ring.Length; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % ring.Length];
                Point3D[] side = [P(a, 0), P(b, 0), P(b, -length), P(a, -length)];
                faces.Add(([side], side.Average(Depth)));
            }
        foreach (var face in faces.OrderBy(f => f.Depth))
        {
            var shape = new GeometryGroup { FillRule = FillRule.EvenOdd };
            foreach (var ring in face.Rings) shape.Children.Add(Path(ring.Select(Screen), true));
            dc.DrawGeometry(fill, edge, shape);
        }
        var barBrush = ThemeTokens.Brush("InkSecondary").Clone(); barBrush.Opacity = .4;
        foreach (var bar in section.Bars)
        {
            dc.DrawLine(new Pen(barBrush, Math.Clamp(bar.Diametro * scale, 1.5, 6)), Screen(new(bar.X, bar.Y, 0)), Screen(new(bar.X, bar.Y, -length)));
            var circle = Enumerable.Range(0, BarCircleSegments).Select(i =>
            {
                var rotation = Matrix3D.Identity;
                rotation.Rotate(new Quaternion(Axis('z'), (double)i / BarCircleSegments * FullTurnDegrees));
                return Screen(new Point3D(bar.X, bar.Y, 0) + rotation.Transform(new Vector3D(bar.Diametro / 2, 0, 0)));
            });
            dc.DrawGeometry(ThemeTokens.Brush("Ink"), new Pen(ThemeTokens.Brush("Paper"), .7), Path(circle, true));
        }
        foreach (var link in section.TemplateLinks) dc.DrawGeometry(null, new Pen(barBrush, 1.5), Path(link.Select(p => Screen(P(p, 0))), true));

        var labels = new List<Rect>();
        void Label(string value, Point anchor, Brush brush, bool bold = false)
        {
            var formatted = new FormattedText(value, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                bold ? 12 : 11, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            formatted.MaxTextWidth = Math.Max(30, size.Width - LabelWidthReserve);
            double width = formatted.WidthIncludingTrailingWhitespace + LabelWidthPadding, height = formatted.Height + LabelHeightPadding;
            Rect Candidate(double x, double y) => new(Math.Clamp(x, LabelMargin, Math.Max(LabelMargin, size.Width - width - LabelMargin)), Math.Clamp(y, 5, Math.Max(5, size.Height - height - FooterReserve)), width, height);
            var candidates = new[] { Candidate(anchor.X + LabelGap, anchor.Y - height / 2), Candidate(anchor.X - width - LabelGap, anchor.Y - height / 2),
                Candidate(anchor.X - width / 2, anchor.Y - height - LabelGap), Candidate(anchor.X - width / 2, anchor.Y + LabelGap) };
            var box = candidates.FirstOrDefault(r => labels.All(l => !l.IntersectsWith(r)));
            if (box.IsEmpty || box.Width == 0) box = candidates[0];
            labels.Add(box);
            dc.DrawRoundedRectangle(ThemeTokens.Brush("Paper"), null, box, 3, 3);
            dc.DrawText(formatted, box.TopLeft + new Vector(3, 2));
        }
        var origin = Screen(new());
        foreach (char axis in "xyz")
        {
            var end = Screen((Point3D)(Axis(axis) * span * AxisLength)); var brush = AxisBrush(axis);
            dc.DrawLine(new Pen(brush, 1) { DashStyle = DashStyles.Dash }, origin, end);
            Arrow(dc, end + (origin - end) * AxisArrowInset, end, brush, 1, 6);
            Label(axis.ToString(), end, brush);
        }
        foreach (var glyph in glyphs)
        {
            var brush = AxisBrush(glyph.Action.Axis); var points = glyph.Points.Select(Screen).ToArray();
            dc.DrawGeometry(null, new Pen(brush, 2.6), Path(points, false));
            Arrow(dc, points[^2], points[^1], brush, 2.6, 10);
            var anchor = glyph.Action.IsMoment ? points[points.Length / 2] : points[0] + (points[^1] - points[0]) * ForceLabelPosition;
            Label(glyph.Action.Label, anchor, brush, true);
        }
        dc.DrawEllipse(ThemeTokens.Brush("Paper"), edge, origin, 3, 3); Label("G", origin, ThemeTokens.Brush("Muted"));
        string message = Scene?.Message is { Length: > 0 } error ? error : glyphs.Count == 0 ? "Azioni nulle nella combinazione selezionata" : "Trascina: ruota · rotella: zoom";
        Text(dc, message, 9, size.Height - FooterTextOffset, 10, ThemeTokens.Brush("Muted"), size.Width - FooterWidthReserve);
    }
    private static void Arrow(DrawingContext dc, Point from, Point to, Brush brush, double width, double head)
    {
        var direction = to - from;
        if (direction.Length < .01) return;
        direction.Normalize(); var side = new Vector(-direction.Y, direction.X);
        dc.DrawLine(new Pen(brush, width), from, to);
        dc.DrawGeometry(brush, null, Path([to, to - direction * head + side * head * HeadHalfWidth, to - direction * head - side * head * HeadHalfWidth], true));
    }
}
