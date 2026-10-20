using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace X.Desktop.Views.EmbeddedWalls;

/// <summary>Interactive input section. All transformations below are display coordinates, not engineering calculations.</summary>
public sealed class EmbeddedWallModelSketch : FrameworkElement
{
    // All lengths here are device-independent pixels; none are dimensions of the engineering model.
    private const double RightMargin = 18, Footer = 42, PlotTop = 104, MessageInset = 32;
    private const double TextPadding = 2, TickOffset = 8, SoilTextInset = 9, SoilLabelTopLimit = 32, SoilLabelHalfHeight = 7;
    private const double WallGap = 9, WallHalfWidth = 7, WallWidth = 14, SegmentHalfWidth = 8, SegmentLabelInset = 20;
    private const double GroundLabelOffset = 23, GroundHitOffset = 24, GroundHitHeight = 29, LabelInset = 6, LabelInsets = 12;
    private const double SurchargeTextOffset = 65, SurchargeArrowStart = 46, SurchargeArrowEnd = 29, SurchargeIntervals = 5;
    private const double ArrowHeadLength = 6, ArrowHeadHalfWidth = 3, WaterLabelWidth = 90, WaterLabelOffset = 4, WaterHitOffset = 3;
    private const double ToeLabelLeft = 55, ToeLabelBelow = 12, SupportLabelOffset = 38, SupportLabelRight = 30, SupportLabelLeft = 5;
    private const double SymbolSpanFraction = .28, SupportEndHalfWidth = 4, SupportEndHalfHeight = 6, PickPadding = 8;
    private const double LoadArrowStart = 48, LoadArrowEnd = 10, DistributedOffset = 45, LoadTextOffset = 26;
    private const double VerticalLoadOffset = 18, VerticalArrowLength = 30, LoadHitLeft = 52, LoadHitAbove = 20, LoadHitHeight = 24;
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(EmbeddedWallSketch),
        typeof(EmbeddedWallModelSketch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PickCommandProperty = DependencyProperty.Register(nameof(PickCommand), typeof(ICommand), typeof(EmbeddedWallModelSketch));
    public EmbeddedWallSketch? Model { get => (EmbeddedWallSketch?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public ICommand? PickCommand { get => (ICommand?)GetValue(PickCommandProperty); set => SetValue(PickCommandProperty, value); }
    private readonly List<(Rect Bounds, WallSketchPick Pick)> hits = [];
    public WallSketchPick? PickAt(Point point) => hits.LastOrDefault(h => h.Bounds.Contains(point)).Pick;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pick = PickAt(e.GetPosition(this));
        Cursor = pick is null ? Cursors.Arrow : Cursors.Hand;
        ToolTip = pick is null ? "Quote in metri. Dimensioni orizzontali schematiche." : "Modifica: " + EmbeddedWallFields.Title(pick.Table);
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pick = PickAt(e.GetPosition(this));
        if (pick is not null && PickCommand?.CanExecute(pick) == true) { PickCommand.Execute(pick); e.Handled = true; }
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); hits.Clear();
        Brush Resource(string name) => TryFindResource(name) as Brush ?? (Brush)FindResource("Token.Ink");
        var ink = Resource("Token.Ink"); var paper = Resource("Token.Paper");
        var blue = Resource("Token.Series.Blue"); var orange = Resource("Token.Series.Orange");
        dc.DrawRectangle(paper, null, new Rect(RenderSize));
        var labels = new List<(FormattedText Text, Rect Bounds, bool Backing)>();
        void PaintLabels()
        {
            foreach (var label in labels)
            {
                if (label.Backing) { var box = label.Bounds; box.Inflate(TextPadding, 1); dc.DrawRectangle(paper, null, box); }
                dc.DrawText(label.Text, label.Bounds.TopLeft);
            }
        }
        void Label(string text, double x, double y, double width = 200, Brush? color = null, bool backing = false)
        {
            if (width < 1) return;
            var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, color ?? ink, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                { MaxTextWidth = width, MaxLineCount = 2, Trimming = TextTrimming.CharacterEllipsis };
            var box = new Rect(x, y, Math.Min(width, ft.Width), ft.Height);
            // Keep nearby water/support/stratum labels readable, without moving their geometric symbols.
            for (int attempt = 0; attempt < 8 && labels.Any(l => l.Bounds.IntersectsWith(box)); attempt++)
                box.Y += ft.Height + TextPadding;
            labels.Add((ft, box, backing));
        }
        if (Model is not { } m) return;
        if (ActualWidth < 310 || ActualHeight < 200) { Label("Allargare la vista per leggere la sezione.", 12, 12); PaintLabels(); return; }
        var elevations = new[] { m.Top, m.Bottom, m.LeftGround, m.RightGround, m.LeftWater, m.RightWater }
            .Where(v => v.HasValue).Select(v => v!.Value).Concat(m.Supports.Select(s => s.Elevation))
            .Concat(m.Loads.SelectMany(l => new[] { l.Top, l.Bottom })).ToArray();
        if (elevations.Length < 2) { Label("Assegna le quote di testa, piede e terreno.", 16, 20, ActualWidth - MessageInset); PaintLabels(); return; }
        double zmax = elevations.Max(), zmin = elevations.Min(), span = zmax - zmin;
        if (!double.IsFinite(span) || span < 1e-9) { Label("Quote non rappresentabili: controllare la geometria.", 16, 20, ActualWidth - MessageInset); PaintLabels(); return; }
        double left = 72, right = ActualWidth - RightMargin, middle = (left + right) / 2;
        double top = PlotTop, bottom = ActualHeight - Footer, height = bottom - top;
        double Y(double z) => top + (zmax - z) / span * height;
        // Bound rectangles before mapping: very deep strata must not overflow WPF display coordinates.
        double ClippedY(double z) => Y(Math.Clamp(z, zmin, zmax));
        Label("SINISTRA", left + LabelInsets, 12); Label("DESTRA", middle + GroundHitOffset, 12);
        Label("z [m]", 9, 12, 55);
        var palettes = new[] { "Token.Series.Orange", "Token.Series.Blue", "Token.Series.Green", "Token.Series.Purple" };
        foreach (string side in new[] { "left_soils", "right_soils" })
        {
            var ground = side == "left_soils" ? m.LeftGround : m.RightGround;
            if (ground is null) continue;
            double x = side == "left_soils" ? left : middle + WallGap;
            double w = (right - left) / 2 - WallGap;
            int color = 0;
            foreach (var band in m.Bands.Where(b => b.Table == side))
            {
                double upper = Math.Min(band.Top, ground.Value), lower = band.Bottom;
                var brush = Resource(palettes[color++ % palettes.Length]);
                if (upper <= lower || upper < zmin || lower > zmax) continue;
                var rect = new Rect(x, ClippedY(upper), w, Math.Max(0, ClippedY(lower) - ClippedY(upper)));
                dc.PushOpacity(.16); dc.DrawRectangle(brush, null, rect); dc.Pop();
                dc.DrawLine(new Pen(brush, 1), rect.BottomLeft, rect.BottomRight);
                if (rect.Height >= 20) Label(band.Name, x + SoilTextInset, rect.Top + Math.Min(rect.Height / 2 - SoilLabelHalfHeight, SoilLabelTopLimit), w - 2 * SoilTextInset, backing: true);
                hits.Add((rect, new(side, band.Row)));
            }
        }
        for (int i = 0; i <= 4; i++)
        {
            const int tickIntervals = 4;
            double z = zmax - span * i / tickIntervals, y = Y(z);
            Label(EmbeddedWallSketch.Format(z), 7, y - TickOffset, 57);
            dc.PushOpacity(.18); dc.DrawLine(new Pen(ink, .6), new(left, y), new(right, y)); dc.Pop();
        }
        void Surface(double? elevation, double? q, bool isLeft)
        {
            if (elevation is not { } z) return;
            double x1 = isLeft ? left : middle + WallGap, x2 = isLeft ? middle - WallGap : right, y = Y(z);
            dc.DrawLine(new Pen(ink, 2), new(x1, y), new(x2, y));
            Label("Terreno " + EmbeddedWallSketch.Format(z) + " m", x1 + LabelInset, y - GroundLabelOffset, x2 - x1 - LabelInsets, backing: true);
            hits.Add((new Rect(x1, y - GroundHitOffset, x2 - x1, GroundHitHeight), new("stages", m.StageRow)));
            if (q is { } surcharge && surcharge != 0)
            {
                for (int i = 1; i <= 4; i++)
                {
                    double x = x1 + (x2 - x1) * i / SurchargeIntervals;
                    Arrow(new(x, y - SurchargeArrowStart), new(x, y - SurchargeArrowEnd), orange);
                }
                Label("q = " + EmbeddedWallSketch.Format(q) + " kPa", x1 + LabelInset, y - SurchargeTextOffset, x2 - x1 - LabelInsets, orange, true);
            }
        }
        void Arrow(Point a, Point b, Brush color)
        {
            dc.DrawLine(new Pen(color, 1.5), a, b);
            var v = a - b; if (v.Length < 1) return; v.Normalize();
            var n = new Vector(-v.Y, v.X);
            dc.DrawLine(new Pen(color, 1.5), b, b + v * ArrowHeadLength + n * ArrowHeadHalfWidth);
            dc.DrawLine(new Pen(color, 1.5), b, b + v * ArrowHeadLength - n * ArrowHeadHalfWidth);
        }
        Surface(m.LeftGround, m.LeftSurcharge, true); Surface(m.RightGround, m.RightSurcharge, false);
        void Water(double? elevation, bool isLeft)
        {
            if (elevation is not { } z) return;
            double x1 = isLeft ? left : middle + WallGap, x2 = isLeft ? middle - WallGap : right, y = Y(z);
            dc.DrawLine(new Pen(blue, 1.8) { DashStyle = DashStyles.Dash }, new(x1, y), new(x2, y));
            Label("▽ " + EmbeddedWallSketch.Format(z) + " m", x2 - WaterLabelWidth, y + WaterLabelOffset, 85, blue, true);
            hits.Add((new Rect(x1, y - WaterHitOffset, x2 - x1, 26), new("stages", m.StageRow)));
        }
        Water(m.LeftWater, true); Water(m.RightWater, false);
        if (m.Top is { } wallTop && m.Bottom is { } wallBottom)
        {
            var wall = new Rect(middle - WallHalfWidth, Y(wallTop), WallWidth, Y(wallBottom) - Y(wallTop));
            dc.PushOpacity(.25); dc.DrawRectangle(ink, null, wall); dc.Pop();
            dc.DrawRectangle(null, new Pen(ink, 1.5), wall);
            foreach (var segment in m.Bands.Where(b => b.Table == "segments"))
            {
                double hi = Math.Min(segment.Top, wallTop), lo = Math.Max(segment.Bottom, wallBottom);
                if (hi <= lo) continue;
                var rect = new Rect(middle - SegmentHalfWidth, Y(hi), 16, Y(lo) - Y(hi));
                dc.DrawRectangle(blue, new Pen(paper, 1), rect);
                hits.Add((new Rect(rect.X - WallHalfWidth, rect.Y, rect.Width + WallWidth, rect.Height), new("segments", segment.Row)));
                Label(segment.Name, middle + SegmentLabelInset, rect.Top + rect.Height / 2, 70, backing: true);
            }
            Label("Piede " + EmbeddedWallSketch.Format(wallBottom) + " m", middle - ToeLabelLeft, Y(wallBottom) + ToeLabelBelow, 160, backing: true);
        }
        foreach (var s in m.Supports)
        {
            // +u extends an anchor on the left and compresses a strut on the right.
            int side = s.Kind == "Strut" ? s.Direction : -s.Direction;
            double length = Math.Min(120, (right - left) * SymbolSpanFraction);
            var symbol = new RotateTransform(s.Angle).Transform(new Point(length, 0));
            Point a = new(middle + side * WallGap, Y(s.Elevation));
            Point b = new(a.X + side * symbol.X, a.Y + symbol.Y);
            // Clip the symbol to the panel; elevation at its head remains exact.
            b.Y = Math.Clamp(b.Y, top, bottom);
            dc.DrawLine(new Pen(orange, s.Kind == "Strut" ? 5 : 2), a, b);
            dc.DrawEllipse(paper, new Pen(orange, 2), a, 4, 4);
            dc.DrawLine(new Pen(orange, 5), new(b.X - SupportEndHalfWidth, b.Y - SupportEndHalfHeight), new(b.X + SupportEndHalfWidth, b.Y + SupportEndHalfHeight));
            string kind = s.Kind == "Strut" ? "Puntone" : s.Kind == "Anchor" ? "Tirante" : "Bilaterale";
            double x = side < 0 ? left + SupportLabelLeft : middle + SupportLabelRight;
            Label($"{s.Id} · {kind}\nP₀ {EmbeddedWallSketch.Format(s.Prestress)} kN", x, a.Y - SupportLabelOffset, (right - left) / 2 - SupportLabelOffset, orange, true);
            var hit = new Rect(a, b); hit.Inflate(8, 8); hits.Add((hit, new("supports", s.Row)));
        }
        foreach (var load in m.Loads)
        {
            double y = Y(load.Top);
            bool distributed = load.Top != load.Bottom;
            var values = new List<string>();
            if (load.HorizontalTop is { } h && h != 0)
            {
                double from = middle - Math.Sign(h) * LoadArrowStart;
                Arrow(new(from, y), new(middle - Math.Sign(h) * LoadArrowEnd, y), orange);
                values.Add(distributed ? $"q {EmbeddedWallSketch.Format(h)} → {EmbeddedWallSketch.Format(load.HorizontalBottom)} kN/m" : $"H {EmbeddedWallSketch.Format(h)} kN");
            }
            else if (distributed) values.Add($"q {EmbeddedWallSketch.Format(load.HorizontalTop)} → {EmbeddedWallSketch.Format(load.HorizontalBottom)} kN/m");
            if (distributed) dc.DrawLine(new Pen(orange, 2) { DashStyle = DashStyles.Dot }, new(middle - DistributedOffset, y), new(middle - DistributedOffset, Y(load.Bottom)));
            if (load.Downward is { } v && v != 0)
            { Arrow(new(middle + VerticalLoadOffset, y - Math.Sign(v) * VerticalArrowLength), new(middle + VerticalLoadOffset, y), orange); values.Add($"F↓ {EmbeddedWallSketch.Format(v)} kN"); }
            if (load.Moment is { } moment && moment != 0) values.Add($"M {EmbeddedWallSketch.Format(moment)} kNm");
            if (values.Count == 0) continue;
            Label(string.Join(" · ", values), middle + LoadTextOffset, y - VerticalLoadOffset, right - middle - SupportLabelRight, orange, true);
            hits.Add((new Rect(middle - LoadHitLeft, y - LoadHitAbove, right - middle + LoadHitLeft, Math.Max(LoadHitHeight, Y(load.Bottom) - y + LoadHitHeight)), new(distributed ? "distributed_loads" : "loads", load.Row)));
        }
        PaintLabels();
    }
}
