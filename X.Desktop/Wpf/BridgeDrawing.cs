using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

/// <summary>Native WPF vector viewport. Drawing coordinates and result coordinates share y=0 at the interface.</summary>
internal sealed partial class BridgeDrawing : FrameworkElement
{
    internal BridgeGeometry? Geometry { get; set; }
    internal BridgeStage? Stage { get; set; }
    internal int Mode { get; set; }
    internal double? LoadY { get; set; }
    internal Point? LoadMarker { get; private set; }
    internal JsonObject? Input { get; set; }
    internal bool ShowGeometryLabels { get; set; }
    internal bool ShowRebarLabels { get; set; }
    internal bool DetailAtSupport { get; set; }
    internal bool IsStale { get; set; }
    internal double ConcreteAmplification { get; set; } = 1;
    internal BridgeLoadPoint[] LoadPoints { get; set; } = [];
    internal List<string> VisibleTags { get; } = [];
    internal List<Rect> TagBounds { get; } = [];
    private double zoom = 1;
    private Vector pan;
    private Point? drag;
    private static readonly Brush Concrete = Ui.Brush("#DAE2E9"), Steel = Ui.Brush("#6889A6"), Bars = Ui.Brush("#C58A34"), Removed = Ui.Brush("#F19B3B");
    private static readonly Brush[] Colors = [Ui.Brush("#607D8B"), Ui.Brush("#A66A15"), Ui.Brush("#127A83"), Ui.Brush("#8856A7"), Ui.Brush("#D25557")];
    internal BridgeDrawing()
    {
        ClipToBounds = true; Focusable = true;
        ToolTip = "Mirino verde: punto di applicazione N · rotella: zoom · trascina: sposta · doppio clic: adatta";
        MouseWheel += (_, e) => { ZoomBy(e.Delta > 0 ? 1.12 : .89); e.Handled = true; };
        MouseLeftButtonDown += (_, e) => { Focus(); if (e.ClickCount == 2) { ResetView(); return; } drag = e.GetPosition(this); CaptureMouse(); Cursor = Cursors.Hand; };
        MouseMove += (_, e) => { if (drag is not { } previous) return; var current = e.GetPosition(this); pan += current - previous; drag = current; InvalidateVisual(); };
        MouseLeftButtonUp += (_, _) => { drag = null; ReleaseMouseCapture(); Cursor = Cursors.Arrow; };
        LostMouseCapture += (_, _) => { drag = null; Cursor = Cursors.Arrow; };
    }
    internal void ZoomBy(double f) { zoom = Math.Clamp(zoom * f, .6, 4); InvalidateVisual(); }
    internal void ResetView() { zoom = 1; pan = new(); InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        LoadMarker = null;
        VisibleTags.Clear(); TagBounds.Clear(); StressLabels.Clear();
        double w = ActualWidth, h = ActualHeight; if (w < 80 || h < 80) return;
        dc.DrawRectangle(Ui.Brush("#F8FAFD"), null, new Rect(0, 0, w, h));
        void Text(string s, double x, double y, Brush? brush = null, double size = 11)
        {
            var t = new FormattedText(s, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush ?? Ui.Navy, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(t, new Point(x, y));
        }
        if (Geometry is not { } g) { Text("Completa i dati geometrici per visualizzare la sezione.", 20, 35); return; }
        bool chart = Mode != 2 && Stage is not null;
        double geoWidth = chart ? w * .45 : w - 30;
        var loads = LoadPoints.Length > 0 ? LoadPoints : LoadY is { } reference ? [new BridgeLoadPoint(1, "N", reference, Stage?.Contributions.Sum(c => c.N) ?? 0)] : Array.Empty<BridgeLoadPoint>();
        double topY = Math.Max(g.SlabHeight, loads.Select(p => p.Y).DefaultIfEmpty(g.SlabHeight).Max()), bottomY = Math.Min(-g.Height, loads.Select(p => p.Y).DefaultIfEmpty(-g.Height).Min()), totalH = topY - bottomY;
        bool tags = !chart && (ShowGeometryLabels || ShowRebarLabels);
        double tagWidth = Math.Clamp(geoWidth * .24, 150, 190), margin = tags ? 2 * (tagWidth + 25) : 100;
        double scale = Math.Min(Math.Max(50, geoWidth - margin) / Math.Max(g.Width, Math.Max(g.TopWidth, g.Bottom1Width)), Math.Max(40, h - (chart ? 178 : 150)) / totalH) * zoom;
        double cx = geoWidth / 2 + pan.X, y0 = (chart ? 84 : 60) + topY * scale + pan.Y;
        Point P(double x, double y) => new(cx + (x - g.Width / 2) * scale, y0 - y * scale);
        var outline = new Pen(Ui.Navy, 1);
        Rect R(double x, double y, double width, double height) => new(P(x, y + height), new Size(Math.Max(.6, width * scale), Math.Max(.6, height * scale)));
        void Rectangle(double x, double y, double width, double height, Brush fill) => dc.DrawRectangle(SectionFill(y >= 0 ? "CLS" : "Acciaio", y, height, fill), outline, R(x, y, width, height));
        void Hatch(Rect r)
        {
            if (r.Height < .2 || r.Width < .2) return;
            dc.PushClip(new RectangleGeometry(r)); dc.DrawRectangle(Ui.Brush("#FFF2DC"), new Pen(Removed, 1), r);
            for (double k = r.Left - r.Height; k < r.Right; k += 7) dc.DrawLine(new Pen(Removed, 1.4), new(k, r.Bottom), new(k + r.Height, r.Top));
            dc.Pop();
        }
        Text(IsStale ? "ULTIMO CALCOLO · DA AGGIORNARE" : "SEZIONE  ·  mm", 12, 7, IsStale ? Ui.Brush("#8B5916") : Ui.Muted, 10);
        Rectangle(0, 0, g.Width, g.SlabHeight, Concrete);
        Rectangle((g.Width - g.TopWidth) / 2, -g.TopThickness, g.TopWidth, g.TopThickness, Steel);
        double webX = (g.Width - g.WebThickness) / 2, webBottom = -g.TopThickness - g.WebHeight;
        double missing = Stage is { } webStage ? g.WebHeight - webStage.Effective.WebTop - webStage.Effective.WebBottom : 0;
        if (Stage is { } effectiveStage && missing > 1e-3)
        {
            var effective = effectiveStage.Effective;
            if (effective.WebTop > 0) Rectangle(webX, -g.TopThickness - effective.WebTop, g.WebThickness, effective.WebTop, Steel);
            if (effective.WebBottom > 0) Rectangle(webX, webBottom, g.WebThickness, effective.WebBottom, Steel);
            // Draw the excluded material once, without an opaque web underneath.
            dc.PushOpacity(.28);
            Rectangle(webX, webBottom + effective.WebBottom, g.WebThickness, missing, Steel);
            dc.Pop();
        }
        else Rectangle(webX, webBottom, g.WebThickness, g.WebHeight, Steel);
        Rectangle((g.Width - g.Bottom1Width) / 2, -g.TopThickness - g.WebHeight - g.Bottom1Thickness, g.Bottom1Width, g.Bottom1Thickness, Steel);
        if (g.Bottom2Thickness > 0)
        {
            Rectangle((g.Width - g.Bottom2Width) / 2, -g.Height, g.Bottom2Width, g.Bottom2Thickness, Ui.Brush("#446581"));
            var pen = new Pen(Ui.Blue, 1) { DashStyle = DashStyles.Dash };
            dc.DrawRectangle(null, pen, R((g.Width - g.BottomEquivalentWidth) / 2, -g.Height, g.BottomEquivalentWidth, g.BottomEquivalentThickness));
        }
        foreach (var b in g.Bars) dc.DrawEllipse(ContourSection && Stage is {} barStage ? new SolidColorBrush(StressColor("Armatura", barStage.Contributions.Sum(c => c.Stress("Armatura", b.Y)))) : Bars, null, P(b.X, b.Y), Math.Max(1.8, b.Diameter * scale / 2), Math.Max(1.8, b.Diameter * scale / 2));
        if (Stage is { } s)
        {
            if (missing > 1e-3)
            {
                var gap = R(webX, webBottom + s.Effective.WebBottom, g.WebThickness, missing);
                dc.PushOpacity(.6);
                dc.DrawRectangle(null, new Pen(Removed, 1) { DashStyle = DashStyles.Dash }, new Rect(gap.Left - 2, gap.Top, gap.Width + 4, gap.Height));
                dc.Pop();
                Text($"Inefficace {BridgeWorkspace.F(missing)} mm", gap.Right + 14, gap.Top + gap.Height / 2 - 8, Ui.Brush("#AC650C"));
            }
            void FlangeGap(double gross, double effective, double y, double t)
            {
                double excluded = (gross - effective) / 2; if (excluded <= 1e-3) return;
                Hatch(R((g.Width - gross) / 2, y, excluded, t)); Hatch(R((g.Width + effective) / 2, y, excluded, t));
            }
            FlangeGap(g.TopWidth, s.Effective.TopWidth, -g.TopThickness, g.TopThickness);
            FlangeGap(g.BottomEquivalentWidth, s.Effective.BottomWidth, -g.Height, g.BottomEquivalentThickness);
            if (s.SteelNeutralAxis is { } na && na >= -g.Height && na <= g.SlabHeight)
            {
                double y = P(0, na).Y; dc.DrawLine(new Pen(Ui.Brush("#A04761"), 1) { DashStyle = DashStyles.Dash }, new(cx - 70, y), new(cx + 70, y));
                Text("σa = 0", cx - 111, y - 8, Ui.Brush("#A04761"));
            }
        }
        // Common reference line, dimensions, and bar row labels.
        double interfaceY = P(0, 0).Y;
        dc.DrawLine(new Pen(Ui.Muted, .7) { DashStyle = DashStyles.Dot }, new(12, interfaceY), new(geoWidth - 8, interfaceY));
        Text("y=0", 14, interfaceY + 3, Ui.Muted, 10);
        var groupedLoads = loads.GroupBy(p => Math.Round(p.Y, 6)).ToArray();
        int loadRow = 0;
        foreach (var group in groupedLoads)
        {
            // Target marks the application point; N acts normal to this section, not vertically.
            var first = group.First(); var point = P(g.Width / 2, first.Y); LoadMarker ??= point;
            var color = groupedLoads.Length == 1 ? Ui.Brush("#127A83") : Colors[(first.Index - 1) % Colors.Length]; var pen = new Pen(color, 1.6);
            dc.DrawEllipse(Brushes.White, pen, point, 6, 6);
            dc.DrawLine(pen, new(point.X - 10, point.Y), new(point.X + 10, point.Y));
            dc.DrawLine(pen, new(point.X, point.Y - 10), new(point.X, point.Y + 10));
            // Keep the readout outside the section so it cannot cover the neutral axis or dimensions.
            if (loadRow < 3)
            {
                string label = groupedLoads.Length == 1 ? "N" : "N" + string.Join(",", group.Select(p => p.Index));
                Text($"{label}  ·  y = {BridgeWorkspace.F(first.Y)} mm  ·  {BridgeWorkspace.F(group.Sum(p => p.Force))} kN", 12, h - 64 + loadRow * 14, color, 10);
            }
            loadRow++;
        }
        if (Stage is not null && (ContourSection || ContourDiagram))
        {
            Text("η = |σ|/limite", 12, 26, Ui.Navy, 10);
            double lx = 105;
            foreach (double u in new[] { 0d, .7, 1d, 1.01 })
            { dc.DrawRectangle(new SolidColorBrush(UtilizationColor(u)), null, new Rect(lx, 28, 13, 10)); Text(u > 1 ? ">1" : BridgeWorkspace.F(u), lx + 16, 26, Ui.Muted, 9); lx += 43; }
            Text("Viola: CLS teso · grigio: inattivo", 12, 42, Ui.Muted, 9);
        }
        DrawConnectors(dc, g, P, scale);
        if (tags) DrawTags(dc, g, geoWidth, h, tagWidth, P);
        if (loadRow > 3) Text("Altri punti N nella tabella Fasi e proprietà", 12, h - 19, Ui.Muted, 9);
        else
        Text(g.Bottom2Thickness > 0 ? "Due piastre reali · contorno blu: equivalente" : "Geometria reale = geometria di calcolo", 12, h - 19, Ui.Muted, 10);
        if (!chart || Stage is not { } stage) return;
        DrawStressDiagram(dc, g, stage, geoWidth, w, h, y => P(0, y).Y);
    }
}

internal sealed class BridgeIcon : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Ui.Brush("#D5DEE7"), new Pen(Ui.Navy, 1.5), new Rect(8, 12, 64, 15));
        var brush = Ui.Brush("#567C9E"); dc.DrawRectangle(brush, null, new Rect(25, 28, 30, 5));
        dc.DrawRectangle(brush, null, new Rect(37, 30, 6, 34)); dc.DrawRectangle(brush, null, new Rect(20, 63, 40, 6));
        foreach (int x in new[] { 17, 29, 41, 53, 65 }) dc.DrawEllipse(Ui.Brush("#C58A34"), null, new Point(x, 19), 2, 2);
    }
}
