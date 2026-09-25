using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

/// <summary>Native WPF vector viewport. Drawing coordinates and result coordinates share y=0 at the interface.</summary>
internal sealed class BridgeDrawing : FrameworkElement
{
    internal BridgeGeometry? Geometry { get; set; }
    internal BridgeStage? Stage { get; set; }
    internal int Mode { get; set; }
    private double zoom = 1;
    private Vector pan;
    private Point? drag;
    private static readonly Brush Concrete = Ui.Brush("#DAE2E9"), Steel = Ui.Brush("#6889A6"), Bars = Ui.Brush("#C58A34"), Removed = Ui.Brush("#F19B3B");
    private static readonly Brush[] Colors = [Ui.Brush("#607D8B"), Ui.Brush("#A66A15"), Ui.Brush("#127A83"), Ui.Brush("#8856A7"), Ui.Brush("#D25557")];
    internal BridgeDrawing()
    {
        ClipToBounds = true; Focusable = true;
        ToolTip = "Rotella: zoom · trascina: sposta · doppio clic: adatta";
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
        double w = ActualWidth, h = ActualHeight; if (w < 80 || h < 80) return;
        dc.DrawRectangle(Ui.Brush("#F8FAFD"), null, new Rect(0, 0, w, h));
        void Text(string s, double x, double y, Brush? brush = null, double size = 11)
        {
            var t = new FormattedText(s, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush ?? Ui.Navy, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(t, new Point(x, y));
        }
        if (Geometry is not { } g) { Text("Completa i dati geometrici per visualizzare la sezione.", 20, 35); return; }
        bool chart = Mode != 2 && Stage is not null;
        double geoWidth = chart ? w * .57 : w - 30, totalH = g.Height + g.SlabHeight;
        double scale = Math.Min((geoWidth - 100) / Math.Max(g.Width, Math.Max(g.TopWidth, g.Bottom1Width)), (h - 92) / totalH) * zoom;
        double cx = geoWidth / 2 + pan.X, y0 = 45 + g.SlabHeight * scale + pan.Y;
        Point P(double x, double y) => new(cx + (x - g.Width / 2) * scale, y0 - y * scale);
        var outline = new Pen(Ui.Navy, 1);
        Rect R(double x, double y, double width, double height) => new(P(x, y + height), new Size(Math.Max(.6, width * scale), Math.Max(.6, height * scale)));
        void Rectangle(double x, double y, double width, double height, Brush fill) => dc.DrawRectangle(fill, outline, R(x, y, width, height));
        void Hatch(Rect r)
        {
            if (r.Height < .2 || r.Width < .2) return;
            dc.PushClip(new RectangleGeometry(r)); dc.DrawRectangle(Ui.Brush("#FFF2DC"), new Pen(Removed, 1), r);
            for (double k = r.Left - r.Height; k < r.Right; k += 7) dc.DrawLine(new Pen(Removed, 1.4), new(k, r.Bottom), new(k + r.Height, r.Top));
            dc.Pop();
        }
        Text("SEZIONE  ·  mm", 12, 7, Ui.Muted, 10);
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
        foreach (var b in g.Bars) dc.DrawEllipse(Bars, null, P(b.X, b.Y), Math.Max(1.8, b.Diameter * scale / 2), Math.Max(1.8, b.Diameter * scale / 2));
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
        var topLeft = P(0, g.SlabHeight); var topRight = P(g.Width, g.SlabHeight);
        double dimY = topLeft.Y - 12;
        dc.DrawLine(outline, new(topLeft.X, dimY), new(topRight.X, dimY));
        foreach (double x in new[] { topLeft.X, topRight.X }) dc.DrawLine(outline, new(x, dimY - 4), new(x, dimY + 4));
        Text($"b_eff = {BridgeWorkspace.F(g.Width)}", cx - 45, dimY - 19, Ui.Muted, 10);
        Text($"h_w {BridgeWorkspace.F(g.WebHeight)}  ·  t_w {BridgeWorkspace.F(g.WebThickness)}", Math.Max(10, cx - 165), P(0, -g.Height).Y + 8, Ui.Muted, 10);
        Text(g.Bottom2Thickness > 0 ? "Due piastre reali · contorno blu: equivalente" : "Geometria reale = geometria di calcolo", 12, h - 19, Ui.Muted, 10);
        if (!chart || Stage is not { } stage) return;
        double left = geoWidth + 12, right = w - 20, originX = (left + right) / 2;
        dc.DrawLine(new Pen(Ui.Brush("#E0E6EE"), 1), new(geoWidth, 8), new(geoWidth, h - 12));
        Text(Mode == 1 ? "CONTRIBUTI  ·  MPa" : "TENSIONI TOTALI  ·  MPa", left, 7, Ui.Muted, 10);
        double max = Math.Max(1, stage.Points.Max(p => Math.Abs(p.Stress)));
        if (Mode == 1) max = Math.Max(max, stage.Points.SelectMany(p => p.Contributions).Select(Math.Abs).DefaultIfEmpty(1).Max());
        double stressScale = Math.Max(1, (right - left) / 2 - 38) / max;
        dc.DrawLine(new Pen(Ui.Muted, .7), new(originX, 30), new(originX, h - 40));
        Text("−", left + 7, 24, Ui.Muted); Text("0", originX - 4, 24, Ui.Muted); Text("+", right - 10, 24, Ui.Muted);
        void Curve(Func<double, double> sigma, double ya, double yb, Brush color, double thickness, bool annotate)
        {
            var a = new Point(originX + sigma(ya) * stressScale, P(0, ya).Y); var b = new Point(originX + sigma(yb) * stressScale, P(0, yb).Y);
            dc.DrawLine(new Pen(color, thickness), a, b); dc.DrawEllipse(color, null, a, 2.2, 2.2); dc.DrawEllipse(color, null, b, 2.2, 2.2);
            if (annotate)
            {
                Text(BridgeWorkspace.F(sigma(ya)), Math.Clamp(a.X + 5, left, right - 46), a.Y - 15, color, 10);
                Text(BridgeWorkspace.F(sigma(yb)), Math.Clamp(b.X + 5, left, right - 46), b.Y + 2, color, 10);
            }
        }
        if (Mode == 1)
        {
            for (int i = 0; i < stage.Contributions.Count; i++)
            {
                var c = stage.Contributions[i]; Brush color = Colors[i % Colors.Length];
                Curve(c.SteelStress, 0, -g.Height, color, 1.5, false);
                if (c.Kind == "Composta") Curve(y => c.Stress("CLS", y), g.SlabHeight, 0, color, 1.5, false);
            }
            // Numbers reference the editable phase order, never assume the default names.
            int visible = Math.Min(stage.Contributions.Count, 5);
            for (int i = 0; i < visible; i++) Text($"Δσ {i + 1}", left + i * 43, h - 35, Colors[i], 9);
            if (stage.Contributions.Count > 5) Text("… tabella Fasi", left + 215, h - 35, Ui.Muted, 9);
        }
        Curve(y => stage.Contributions.Sum(c => c.SteelStress(y)), 0, -g.Height, Ui.Blue, Mode == 1 ? 1 : 2.2, Mode != 1);
        if (stage.Contributions.Any(c => c.Kind == "Composta")) Curve(y => stage.Contributions.Sum(c => c.Stress("CLS", y)), g.SlabHeight, 0, Ui.Brush("#667085"), 2.2, Mode != 1);
        foreach (var row in stage.Points.Where(p => p.Material == "Armatura" && p.Active))
        {
            var p = new Point(originX + row.Stress * stressScale, P(0, row.Y).Y); dc.DrawEllipse(Bars, null, p, 3, 3);
        }
        Text($"Scala ±{BridgeWorkspace.F(max)} MPa", left, h - 19, Ui.Muted, 10);
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
