using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace X.Desktop.Views.EmbeddedWalls;

/// <summary>Interactive input section. All transformations below are display coordinates, not engineering calculations.</summary>
/// <remarks>
/// Organizzazione del modulo paratie (progetto del 16/11), step 5. A technical drawing of ANTHEA: white paper and the same colours in every
/// appearance (tokens Drawing.* and Swatch.Soil1–6, identity role). In the drawing: the header (wall, case, phase) at the top left, the zones
/// SINISTRA and DESTRA, the soils filled with one tint per soil and their legend at the top right, the excavated soil as an outline, the wall
/// as a double line with its segments, the water with ▽ and its level, the surcharges, the supports and the loads with their values. An anchor
/// is drawn to scale only when «Sfilamento dei tiranti» gives its free and bonded lengths; otherwise a dashed schematic symbol (R08).
/// The element chosen is highlighted with Drawing.Accent; the pointer shows its data with table and units (C7). The wheel and the buttons
/// of the page Vista magnify the drawing (1–8 times, state of the view, not saved); a drag pans it when magnified.
/// </remarks>
public sealed class EmbeddedWallModelSketch : FrameworkElement
{
    // All lengths here are device-independent pixels; none are dimensions of the engineering model.
    private const double RightMargin = 18, Footer = 42, PlotTop = 124, PlotLeft = 72, MessageInset = 32;
    private const double TextPadding = 2, TickOffset = 8, SoilTextInset = 9, SoilLabelTopLimit = 32, SoilLabelHalfHeight = 7;
    private const double WallGap = 9, WallHalfWidth = 7, WallWidth = 14, SegmentLabelInset = 20;
    private const double GroundLabelOffset = 23, GroundHitOffset = 24, GroundHitHeight = 29, LabelInset = 6, LabelInsets = 12;
    private const double SurchargeTextOffset = 59, SurchargeArrowStart = 42, SurchargeArrowEnd = 29, SurchargeIntervals = 5;
    private const double ArrowHeadLength = 6, ArrowHeadHalfWidth = 3, WaterLabelWidth = 90, WaterLabelOffset = 4, WaterHitOffset = 3, WaterHitHeight = 26;
    private const double ToeLabelLeft = 55, ToeLabelBelow = 12, SupportLabelOffset = 38, SupportLabelRight = 30, SupportLabelLeft = 5;
    private const double SymbolSpanFraction = .28, SymbolMaximum = 120, SupportEndHalfWidth = 4, SupportEndHalfHeight = 6, PickPadding = 8;
    private const double LoadArrowStart = 48, LoadArrowEnd = 10, DistributedOffset = 45, LoadTextOffset = 26;
    private const double VerticalLoadOffset = 18, VerticalArrowLength = 30, LoadHitLeft = 52, LoadHitAbove = 20, LoadHitHeight = 24;
    private const double HeaderLeft = 12, HeaderTop = 8, HeaderLine = 18, HeaderBottom = 46, ZoneTop = 48, AxisLabelLeft = 9, AxisLabelWidth = 55;
    private const double TickLabelLeft = 7, TickLabelWidth = 57, TickIntervals = 4, HaloWidth = 7, HeadRadius = 4, BulbWidth = 7, FreeWidth = 1.5;
    private const double LegendMargin = 8, LegendPadding = 6, LegendRow = 17, LegendSwatch = 12, LegendGap = 6, LegendName = 118, LegendValue = 34;
    private const double LegendSides = 38, LegendGaps = 4, LegendMaximumFraction = .5, LegendTitleGap = 3, ZoomNoteHeight = 18, ToeLabelWidth = 160, LegendMinimumName = 40;
    private const double FontSize = 12, SmallFontSize = 11, HeaderFontSize = 13, LabelWidth = 200, MinimumWidth = 310, MinimumHeight = 200;
    private const int LabelAttempts = 8;

    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(EmbeddedWallSketch),
        typeof(EmbeddedWallModelSketch), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PickCommandProperty = DependencyProperty.Register(nameof(PickCommand), typeof(ICommand), typeof(EmbeddedWallModelSketch));
    /// <summary>Magnification of the drawing, 1 (fit) to 8; two-way with the state of the view of the sheet.</summary>
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(EmbeddedWallModelSketch),
        new FrameworkPropertyMetadata(EmbeddedWallSketchZoom.Minimum, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((EmbeddedWallModelSketch)d).Rezoom((double)e.OldValue, (double)e.NewValue), (_, value) => EmbeddedWallSketchZoom.Clamp((double)value)));
    public static readonly DependencyProperty ShowLegendProperty = DependencyProperty.Register(nameof(ShowLegend), typeof(bool), typeof(EmbeddedWallModelSketch),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    /// <summary>The row chosen in the editor of the sheet: the element drawn from it is highlighted.</summary>
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(nameof(Selected), typeof(object), typeof(EmbeddedWallModelSketch),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public EmbeddedWallSketch? Model { get => (EmbeddedWallSketch?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public ICommand? PickCommand { get => (ICommand?)GetValue(PickCommandProperty); set => SetValue(PickCommandProperty, value); }
    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public bool ShowLegend { get => (bool)GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public object? Selected { get => GetValue(SelectedProperty); set => SetValue(SelectedProperty, value); }
    private readonly List<(Rect Bounds, WallSketchPick Pick)> hits = [];
    private Vector pan;
    private Point? zoomAnchor, dragStart;
    private Vector dragPan;

    /// <summary>Tints of the soils, one per soil (by name) in turn: the identification tints of the soil drawings of ANTHEA (ThemeTokens).
    /// Until step 5 they were series tokens at 16 %; before that the fourth was «Token.Series.Purple», which the catalogue does not have (R09).</summary>
    internal static IReadOnlyList<string> SoilPalette { get; } = Array.AsReadOnly(new[]
        { "Token.Swatch.Soil1", "Token.Swatch.Soil2", "Token.Swatch.Soil3", "Token.Swatch.Soil4", "Token.Swatch.Soil5", "Token.Swatch.Soil6" });
    public WallSketchPick? PickAt(Point point) => hits.LastOrDefault(h => h.Bounds.Contains(point)).Pick;

    /// <summary>The bounds of the legend drawn last, empty when it is hidden (for the checks off screen).</summary>
    internal Rect LegendBounds { get; private set; } = Rect.Empty;

    private Rect BaseRect() => new(PlotLeft, PlotTop, Math.Max(1, ActualWidth - RightMargin - PlotLeft), Math.Max(1, ActualHeight - Footer - PlotTop));

    /// <summary>Keeps the point under the pointer (wheel) or the centre (buttons) where it is when the magnification changes.</summary>
    private void Rezoom(double before, double after)
    {
        var b = BaseRect();
        var anchor = zoomAnchor ?? new Point(b.X + b.Width / 2, b.Y + b.Height / 2);
        double u = (anchor.X - b.X - pan.X) / (b.Width * before), v = (anchor.Y - b.Y - pan.Y) / (b.Height * before);
        pan = Clamped(new Vector(anchor.X - u * b.Width * after - b.X, anchor.Y - v * b.Height * after - b.Y), after);
    }
    private Vector Clamped(Vector offset, double zoom)
    {
        var b = BaseRect();
        return new(Math.Clamp(offset.X, b.Width * (1 - zoom), 0), Math.Clamp(offset.Y, b.Height * (1 - zoom), 0));
    }
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); pan = Clamped(pan, Zoom); }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Model is null || e.Delta == 0) return;
        zoomAnchor = e.GetPosition(this);
        try { SetCurrentValue(ZoomProperty, Zoom * (e.Delta > 0 ? EmbeddedWallSketchZoom.WheelStep : 1 / EmbeddedWallSketchZoom.WheelStep)); }
        finally { zoomAnchor = null; }
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        if (dragStart is { } start && IsMouseCaptured)
        {
            pan = Clamped(dragPan + (point - start), Zoom); InvalidateVisual(); return;
        }
        var pick = PickAt(point);
        Cursor = pick is not null ? Cursors.Hand : Zoom > EmbeddedWallSketchZoom.Minimum ? Cursors.SizeAll : Cursors.Arrow;
        ToolTip = pick is null ? "Quote in metri. Dimensioni orizzontali schematiche." + (Zoom > EmbeddedWallSketchZoom.Minimum ? " Trascina per spostare il disegno." : "")
            : (string.IsNullOrWhiteSpace(pick.Detail) ? "" : pick.Detail + Environment.NewLine) + "Clic: modifica in «" + EmbeddedWallFields.Title(pick.Table) + "».";
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pick = PickAt(e.GetPosition(this));
        if (pick is not null && PickCommand?.CanExecute(pick) == true) { PickCommand.Execute(pick); e.Handled = true; }
        else if (pick is null) StartDrag(e);
    }
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle) StartDrag(e);
    }
    private void StartDrag(MouseButtonEventArgs e)
    {
        if (Zoom <= EmbeddedWallSketchZoom.Minimum || !CaptureMouse()) return;
        dragStart = e.GetPosition(this); dragPan = pan; e.Handled = true;
    }
    protected override void OnMouseUp(MouseButtonEventArgs e) { base.OnMouseUp(e); if (dragStart is not null) { ReleaseMouseCapture(); e.Handled = true; } }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); dragStart = null; }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); hits.Clear(); LegendBounds = Rect.Empty;
        Brush Resource(string name) => TryFindResource(name) as Brush ?? (Brush)FindResource("Token.Ink");
        var ink = Resource("Token.Drawing.Ink"); var paper = Resource("Token.Drawing.Paper"); var muted = Resource("Token.Drawing.Muted");
        var accent = Resource("Token.Drawing.Accent"); var guide = Resource("Token.Drawing.Guide"); var grid = Resource("Token.Drawing.Grid");
        var water = accent;
        dc.DrawRectangle(paper, null, new Rect(RenderSize));
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        FormattedText Text(string text, double width, Brush color, double size = FontSize, bool bold = false, int lines = 2) =>
            new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, color, dpi)
                { MaxTextWidth = Math.Max(1, width), MaxLineCount = lines, Trimming = TextTrimming.CharacterEllipsis };
        var labels = new List<(FormattedText Text, Rect Bounds, bool Backing)>();
        var blocked = new List<Rect>();
        void PaintLabels()
        {
            foreach (var label in labels)
            {
                if (label.Backing) { var box = label.Bounds; box.Inflate(TextPadding, 1); dc.DrawRectangle(paper, null, box); }
                dc.DrawText(label.Text, label.Bounds.TopLeft);
            }
        }
        void Label(string text, double x, double y, double width = LabelWidth, Brush? color = null, bool backing = false)
        {
            if (width < 1) return;
            var ft = Text(text, width, color ?? ink);
            var start = new Rect(x, y, Math.Min(width, ft.Width), ft.Height);
            bool Free(Rect r) => !labels.Any(l => l.Bounds.IntersectsWith(r)) && !blocked.Any(b => b.IntersectsWith(r));
            // Keep nearby water/support/stratum labels readable, without moving their geometric symbols: below, then above, inside the
            // drawing; when no place is free the label stays where it was asked (never pushed out of sight).
            var box = start;
            for (int attempt = 1; attempt <= LabelAttempts && !Free(box); attempt++)
            {
                double step = attempt * (ft.Height + TextPadding);
                var below = new Rect(start.X, start.Y + step, start.Width, start.Height);
                var above = new Rect(start.X, start.Y - step, start.Width, start.Height);
                if (below.Bottom <= ActualHeight && Free(below)) box = below;
                else if (above.Top >= HeaderBottom && Free(above)) box = above;
            }
            labels.Add((ft, box, backing));
        }
        if (Model is not { } m) return;
        // Header of the drawing: wall, case and phase, at the top left, outside the magnification.
        var title = Text(string.IsNullOrWhiteSpace(m.Name) ? "Paratia" : m.Name, ActualWidth / 2, ink, HeaderFontSize, true, 1);
        var subtitle = Text($"Caso «{m.CaseId}» · fase {m.Phase}", ActualWidth / 2, muted, SmallFontSize, false, 1);
        dc.DrawText(title, new Point(HeaderLeft, HeaderTop)); dc.DrawText(subtitle, new Point(HeaderLeft, HeaderTop + HeaderLine));
        blocked.Add(new Rect(HeaderLeft, HeaderTop, Math.Max(title.Width, subtitle.Width), HeaderLine + subtitle.Height));
        if (ActualWidth < MinimumWidth || ActualHeight < MinimumHeight) { Label("Allargare la vista per leggere la sezione.", HeaderLeft, HeaderBottom); PaintLabels(); return; }
        var elevations = new[] { m.Top, m.Bottom, m.LeftGround, m.RightGround, m.LeftWater, m.RightWater }
            .Where(v => v.HasValue).Select(v => v!.Value).Concat(m.Supports.Select(s => s.Elevation))
            .Concat(m.Loads.SelectMany(l => new[] { l.Top, l.Bottom })).ToArray();
        if (elevations.Length < 2) { Label("Assegna le quote di testa, piede e terreno.", HeaderLeft, HeaderBottom, ActualWidth - MessageInset); PaintLabels(); return; }
        double zmax = elevations.Max(), zmin = elevations.Min(), span = zmax - zmin;
        if (!double.IsFinite(span) || span < 1e-9) { Label("Quote non rappresentabili: controllare la geometria.", HeaderLeft, HeaderBottom, ActualWidth - MessageInset); PaintLabels(); return; }
        // The magnified drawing is the plot rectangle enlarged and moved: symbols and texts keep their size in pixels.
        double zoom = Zoom; var plot = BaseRect();
        double left = plot.X + pan.X, right = left + plot.Width * zoom, middle = (left + right) / 2;
        double top = plot.Y + pan.Y, bottom = top + plot.Height * zoom, height = bottom - top, scale = height / span;
        double Y(double z) => top + (zmax - z) * scale;
        // Bound rectangles before mapping: very deep strata must not overflow WPF display coordinates.
        double ClippedY(double z) => Y(Math.Clamp(z, zmin, zmax));
        var content = new Rect(0, HeaderBottom, ActualWidth, Math.Max(0, ActualHeight - HeaderBottom));
        void Hit(Rect bounds, WallSketchPick pick) { bounds.Intersect(content); if (!bounds.IsEmpty) hits.Add((bounds, pick)); }
        bool IsSelected(params EmbeddedWallRow?[] rows) => Selected is not null && rows.Any(r => r is not null && ReferenceEquals(r, Selected));
        void Halo(Rect bounds) { bounds.Inflate(TextPadding, TextPadding); dc.PushOpacity(.35); dc.DrawRectangle(null, new Pen(accent, HaloWidth), bounds); dc.Pop(); dc.DrawRectangle(null, new Pen(accent, TextPadding), bounds); }
        // Legend of the soils at the top right (outside the magnification), measured first so that the labels keep clear of it.
        Rect legend = Rect.Empty;
        double nameWidth = LegendName;
        // At most half the drawing: in a narrow drawing the column of the sides goes first (it stays in the hint of the line).
        double room = ActualWidth * LegendMaximumFraction, fixedWidth = LegendPadding * 2 + LegendSwatch + LegendGap * LegendGaps + LegendValue * 2 + LegendSides;
        bool sides = room - fixedWidth >= LegendMinimumName;
        if (!sides) fixedWidth -= LegendSides + LegendGap;
        nameWidth = Math.Min(LegendName, room - fixedWidth);
        if (ShowLegend && m.Soils.Count > 0 && nameWidth >= LegendSwatch)
        {
            double width = fixedWidth + nameWidth, rows = m.Soils.Count + 2;
            legend = new Rect(ActualWidth - RightMargin - width, LegendMargin, width, LegendPadding * 2 + LegendRow * rows + LegendTitleGap);
            blocked.Add(legend);
        }
        dc.PushClip(new RectangleGeometry(content));
        Label("SINISTRA", Math.Max(left, 0) + LabelInsets, ZoneTop); Label("DESTRA", middle + GroundHitOffset, ZoneTop);
        Label("z [m]", AxisLabelLeft, ZoneTop, AxisLabelWidth, muted);
        foreach (string side in new[] { "left_soils", "right_soils" })
        {
            var ground = side == "left_soils" ? m.LeftGround : m.RightGround;
            if (ground is null) continue;
            double x = side == "left_soils" ? left : middle + WallGap;
            double w = (right - left) / 2 - WallGap;
            foreach (var band in m.Bands.Where(b => b.Table == side))
            {
                double upper = Math.Min(band.Top, ground.Value), lower = band.Bottom;
                var brush = Resource(SoilPalette[Math.Max(0, band.Tint) % SoilPalette.Count]);
                // The soil above the level of the phase is removed: its outline only, dashed.
                if (band.Top > ground.Value && band.Top > lower)
                {
                    var removed = new Rect(x, ClippedY(band.Top), w, Math.Max(0, ClippedY(Math.Max(ground.Value, lower)) - ClippedY(band.Top)));
                    dc.DrawRectangle(null, new Pen(guide, 1) { DashStyle = DashStyles.Dash }, removed);
                }
                if (upper <= lower || upper < zmin || lower > zmax) continue;
                var rect = new Rect(x, ClippedY(upper), w, Math.Max(0, ClippedY(lower) - ClippedY(upper)));
                dc.DrawRectangle(brush, null, rect);
                dc.PushOpacity(.35); dc.DrawLine(new Pen(ink, 1), rect.BottomLeft, rect.BottomRight); dc.Pop();
                if (rect.Height >= HeaderLine + TextPadding) Label(band.Name, x + SoilTextInset, rect.Top + Math.Min(rect.Height / 2 - SoilLabelHalfHeight, SoilLabelTopLimit), w - 2 * SoilTextInset);
                if (IsSelected(band.Row)) Halo(rect);
                Hit(rect, new(side, band.Row, band.Detail));
            }
        }
        // Elevations of the part on screen.
        double zTop = zmax - (plot.Top - top) / scale, zBottom = zmax - (plot.Bottom - top) / scale;
        for (int i = 0; i <= TickIntervals; i++)
        {
            double z = zTop - (zTop - zBottom) * i / TickIntervals, y = Y(z);
            Label(EmbeddedWallSketch.Format(z), TickLabelLeft, y - TickOffset, TickLabelWidth, muted);
            dc.PushOpacity(.18); dc.DrawLine(new Pen(ink, .6), new(left, y), new(right, y)); dc.Pop();
        }
        void Arrow(Point a, Point b, Brush color)
        {
            dc.DrawLine(new Pen(color, FreeWidth), a, b);
            var v = a - b; if (v.Length < 1) return; v.Normalize();
            var n = new Vector(-v.Y, v.X);
            dc.DrawLine(new Pen(color, FreeWidth), b, b + v * ArrowHeadLength + n * ArrowHeadHalfWidth);
            dc.DrawLine(new Pen(color, FreeWidth), b, b + v * ArrowHeadLength - n * ArrowHeadHalfWidth);
        }
        void Surface(double? elevation, double? q, bool isLeft)
        {
            if (elevation is not { } z) return;
            double x1 = isLeft ? left : middle + WallGap, x2 = isLeft ? middle - WallGap : right, y = Y(z);
            dc.DrawLine(new Pen(ink, 2), new(x1, y), new(x2, y));
            Label("Terreno " + EmbeddedWallSketch.Format(z) + " m", x1 + LabelInset, y - GroundLabelOffset, x2 - x1 - LabelInsets, backing: true);
            Hit(new Rect(x1, y - GroundHitOffset, x2 - x1, GroundHitHeight), new("stages", m.StageRow, m.StageDetail));
            if (q is { } surcharge && surcharge != 0)
            {
                for (int i = 1; i < SurchargeIntervals; i++)
                {
                    double x = x1 + (x2 - x1) * i / SurchargeIntervals;
                    Arrow(new(x, y - SurchargeArrowStart), new(x, y - SurchargeArrowEnd), ink);
                }
                Label("q = " + EmbeddedWallSketch.Format(q) + " kPa", x1 + LabelInset, y - SurchargeTextOffset, x2 - x1 - LabelInsets, ink, true);
            }
        }
        Surface(m.LeftGround, m.LeftSurcharge, true); Surface(m.RightGround, m.RightSurcharge, false);
        void Water(double? elevation, bool isLeft)
        {
            if (elevation is not { } z) return;
            double x1 = isLeft ? left : middle + WallGap, x2 = isLeft ? middle - WallGap : right, y = Y(z);
            dc.DrawLine(new Pen(water, FreeWidth + TextPadding / 2) { DashStyle = DashStyles.Dash }, new(x1, y), new(x2, y));
            Label("▽ " + EmbeddedWallSketch.Format(z) + " m", x2 - WaterLabelWidth, y + WaterLabelOffset, WaterLabelWidth - LabelInset + 1, water, true);
            Hit(new Rect(x1, y - WaterHitOffset, x2 - x1, WaterHitHeight), new("stages", m.StageRow, m.StageDetail));
        }
        Water(m.LeftWater, true); Water(m.RightWater, false);
        if (m.Top is { } wallTop && m.Bottom is { } wallBottom)
        {
            // The wall as a double line; its segments as bands of the wall, divided by a line at each change.
            var wall = new Rect(middle - WallHalfWidth, Y(wallTop), WallWidth, Y(wallBottom) - Y(wallTop));
            dc.DrawRectangle(paper, new Pen(ink, 1), wall);
            int index = 0;
            foreach (var segment in m.Bands.Where(b => b.Table == "segments"))
            {
                double hi = Math.Min(segment.Top, wallTop), lo = Math.Max(segment.Bottom, wallBottom);
                if (hi <= lo) continue;
                var rect = new Rect(wall.X, Y(hi), WallWidth, Y(lo) - Y(hi));
                if (index++ % 2 == 1) dc.DrawRectangle(grid, null, rect);
                dc.DrawLine(new Pen(ink, 1), rect.TopLeft, rect.TopRight); dc.DrawLine(new Pen(ink, 1), rect.BottomLeft, rect.BottomRight);
                if (IsSelected(segment.Row)) Halo(rect);
                Hit(new Rect(rect.X - WallHalfWidth, rect.Y, rect.Width + WallWidth, rect.Height), new("segments", segment.Row, segment.Detail));
                Label(segment.Name, middle + SegmentLabelInset, rect.Top + rect.Height / 2, AxisLabelWidth + LabelInsets + TextPadding + 1, backing: true);
            }
            dc.DrawLine(new Pen(ink, FreeWidth + TextPadding / 2), wall.TopLeft, wall.BottomLeft); dc.DrawLine(new Pen(ink, FreeWidth + TextPadding / 2), wall.TopRight, wall.BottomRight);
            Label("Piede " + EmbeddedWallSketch.Format(wallBottom) + " m", middle - ToeLabelLeft, Y(wallBottom) + ToeLabelBelow, ToeLabelWidth, backing: true);
        }
        foreach (var s in m.Supports)
        {
            // +u extends an anchor on the left and compresses a strut on the right.
            int side = s.Kind == "Strut" ? s.Direction : -s.Direction;
            Point a = new(middle + side * WallGap, Y(s.Elevation));
            var rotation = new RotateTransform(s.Angle);
            bool scaled = s.FreeLength is not null && s.BondLength is not null;
            Point Along(double length) { var p = rotation.Transform(new Point(length, 0)); return new(a.X + side * p.X, a.Y + p.Y); }
            Point b;
            if (scaled)
            {
                // R08: free and bonded lengths along the axis of «Sfilamento dei tiranti», at the scale of the elevations of the drawing.
                var f = Along(s.FreeLength!.Value * scale); b = Along((s.FreeLength.Value + s.BondLength!.Value) * scale);
                dc.DrawLine(new Pen(ink, FreeWidth), a, f);
                dc.DrawLine(new Pen(ink, BulbWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, f, b);
            }
            else
            {
                b = Along(Math.Min(SymbolMaximum, (right - left) * SymbolSpanFraction / zoom));
                // Clip the symbol to the panel; elevation at its head remains exact.
                b.Y = Math.Clamp(b.Y, top, bottom);
                var pen = new Pen(ink, s.Kind == "Strut" ? WallHalfWidth - 2 : 2);
                if (s.Kind == "Anchor") pen.DashStyle = DashStyles.Dash;
                dc.DrawLine(pen, a, b);
                dc.DrawLine(new Pen(ink, WallHalfWidth - 2), new(b.X - SupportEndHalfWidth, b.Y - SupportEndHalfHeight), new(b.X + SupportEndHalfWidth, b.Y + SupportEndHalfHeight));
            }
            dc.DrawEllipse(paper, new Pen(ink, 2), a, HeadRadius, HeadRadius);
            var hit = new Rect(a, b); hit.Inflate(PickPadding, PickPadding);
            if (IsSelected(s.Row, s.Activation, s.AnchorCheck)) Halo(hit);
            string kind = s.Kind == "Strut" ? "Puntone" : s.Kind == "Anchor" ? scaled ? "Tirante" : "Tirante · schematico" : "Bilaterale";
            string lengths = scaled ? $" · L {EmbeddedWallSketch.Format(s.FreeLength)} + {EmbeddedWallSketch.Format(s.BondLength)} m" : "";
            double x = side < 0 ? left + SupportLabelLeft : middle + SupportLabelRight;
            Label($"{s.Id} · {kind}\nP₀ {EmbeddedWallSketch.Format(s.Prestress)} kN{lengths}", x, a.Y - SupportLabelOffset, (right - left) / 2 - SupportLabelOffset, ink, true);
            Hit(hit, new("supports", s.Row, s.Detail));
            if (s.AnchorCheck is not null && scaled) { var bulb = new Rect(Along(s.FreeLength!.Value * scale), b); bulb.Inflate(PickPadding, PickPadding); Hit(bulb, new("anchor_checks", s.AnchorCheck, s.Detail)); }
        }
        foreach (var load in m.Loads)
        {
            double y = Y(load.Top);
            bool distributed = load.Top != load.Bottom;
            var values = new List<string>();
            if (load.HorizontalTop is { } h && h != 0)
            {
                double from = middle - Math.Sign(h) * LoadArrowStart;
                Arrow(new(from, y), new(middle - Math.Sign(h) * LoadArrowEnd, y), ink);
                values.Add(distributed ? $"q {EmbeddedWallSketch.Format(h)} → {EmbeddedWallSketch.Format(load.HorizontalBottom)} kN/m" : $"H {EmbeddedWallSketch.Format(h)} kN");
            }
            else if (distributed) values.Add($"q {EmbeddedWallSketch.Format(load.HorizontalTop)} → {EmbeddedWallSketch.Format(load.HorizontalBottom)} kN/m");
            if (distributed) dc.DrawLine(new Pen(ink, 2) { DashStyle = DashStyles.Dot }, new(middle - DistributedOffset, y), new(middle - DistributedOffset, Y(load.Bottom)));
            if (load.Downward is { } v && v != 0)
            { Arrow(new(middle + VerticalLoadOffset, y - Math.Sign(v) * VerticalArrowLength), new(middle + VerticalLoadOffset, y), ink); values.Add($"F↓ {EmbeddedWallSketch.Format(v)} kN"); }
            if (load.Moment is { } moment && moment != 0) values.Add($"M {EmbeddedWallSketch.Format(moment)} kNm");
            if (values.Count == 0) continue;
            Label(string.Join(" · ", values), middle + LoadTextOffset, y - VerticalLoadOffset, right - middle - SupportLabelRight, ink, true);
            var area = new Rect(middle - LoadHitLeft, y - LoadHitAbove, right - middle + LoadHitLeft, Math.Max(LoadHitHeight, Y(load.Bottom) - y + LoadHitHeight));
            if (IsSelected(load.Row)) Halo(area);
            Hit(area, new(distributed ? "distributed_loads" : "loads", load.Row, load.Detail));
        }
        PaintLabels();
        dc.Pop();
        if (zoom > EmbeddedWallSketchZoom.Minimum)
        {
            var note = Text($"Ingrandimento {zoom.ToString("0.0", CultureInfo.CurrentCulture)}× · «Adatta» nella pagina Vista", ActualWidth / 2, muted, SmallFontSize, false, 1);
            dc.DrawText(note, new Point(ActualWidth - RightMargin - note.Width, ActualHeight - ZoomNoteHeight));
        }
        if (!legend.IsEmpty) DrawLegend(dc, m, legend, nameWidth, sides, ink, muted, guide, paper, Text, Resource);
    }

    /// <summary>The legend of the soils: tint, name, γ [kN/m³] and φ′ [°] as written in the tables (units in the title), and the sides. A line opens its soil.</summary>
    private void DrawLegend(DrawingContext dc, EmbeddedWallSketch m, Rect legend, double nameWidth, bool sides, Brush ink, Brush muted, Brush guide, Brush paper,
        Func<string, double, Brush, double, bool, int, FormattedText> text, Func<string, Brush> resource)
    {
        LegendBounds = legend;
        dc.DrawRectangle(paper, new Pen(guide, 1), legend);
        double x = legend.X + LegendPadding, y = legend.Y + LegendPadding;
        double name = x + LegendSwatch + LegendGap, gamma = name + nameWidth + LegendGap, phi = gamma + LegendValue + LegendGap, side = phi + LegendValue + LegendGap;
        var heading = text("Terreni", legend.Width, ink, SmallFontSize, true, 1); dc.DrawText(heading, new Point(x, y));
        y += LegendRow + LegendTitleGap;
        dc.DrawText(text("Strato", nameWidth, muted, SmallFontSize, false, 1), new Point(name, y));
        dc.DrawText(text("γ", LegendValue, muted, SmallFontSize, false, 1), new Point(gamma, y));
        dc.DrawText(text("φ′", LegendValue, muted, SmallFontSize, false, 1), new Point(phi, y));
        if (sides) dc.DrawText(text("Lato", LegendSides, muted, SmallFontSize, false, 1), new Point(side, y));
        foreach (var soil in m.Soils)
        {
            y += LegendRow;
            dc.DrawRectangle(resource(SoilPalette[Math.Max(0, soil.Tint) % SoilPalette.Count]), new Pen(ink, .5), new Rect(x, y + TextPadding, LegendSwatch, LegendSwatch - TextPadding));
            dc.DrawText(text(string.IsNullOrWhiteSpace(soil.Name) ? "—" : soil.Name, nameWidth, ink, SmallFontSize, false, 1), new Point(name, y));
            dc.DrawText(text(soil.Gamma, LegendValue, ink, SmallFontSize, false, 1), new Point(gamma, y));
            dc.DrawText(text(soil.Phi, LegendValue, ink, SmallFontSize, false, 1), new Point(phi, y));
            if (sides) dc.DrawText(text(soil.Sides, LegendSides, ink, SmallFontSize, false, 1), new Point(side, y));
            var first = soil.Bands[0];
            hits.Add((new Rect(legend.X, y, legend.Width, LegendRow), new(first.Table, first.Row, first.Detail)));
        }
    }
}
