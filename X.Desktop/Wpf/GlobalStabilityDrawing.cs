using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using Anthea.Calculations.Geotechnics;

namespace X.Desktop;

internal sealed class GlobalStabilityDrawing : FrameworkElement
{
    internal JsonObject? Data;
    internal SlopeResult? Result;
    internal SlopeCaseResult? Case;
    internal bool FocusCritical = true;
    internal int SelectedSlice = -1;
    private readonly List<(Rect Rect, string Text)> hits = [];
    internal GlobalStabilityDrawing() { ClipToBounds = true; MouseMove += (_, e) => ToolTip = hits.LastOrDefault(h => h.Rect.Contains(e.GetPosition(this))).Text; }
    protected override void OnRender(DrawingContext dc)
    {
        hits.Clear(); dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
        void Text(string text, double x, double y, Brush? color = null, double size = 11) => dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, color ?? Ui.Navy, 1), new Point(x, y));
        if (Data is null || ActualWidth < 150) return;
        var g = Data["global_stability"]; var valley = g.Array("valley").Select(p => new SlopePoint(p.D("x"), p.D("y"))).ToArray(); var uphill = g.Array("uphill").Select(p => new SlopePoint(p.D("x"), p.D("y"))).ToArray();
        if (valley.Length < 2 || uphill.Length < 2 || g.Array("layers").Count == 0) { Text("Precompilare e completare il profilo globale", 12, 20); return; }
        var geometry = Data["geometry"]!; double t = geometry.D("slab"), a = geometry.D("toe"), back = a + geometry.D("stem_base"), ht = geometry.D("height") + t;
        var surface = Result?.Section.Surface ?? valley.Concat(new[] { new SlopePoint(0, t), new SlopePoint(a, t), new SlopePoint(back - geometry.D("stem_top"), ht) }).Concat(uphill).Distinct().ToArray();
        var chosen = Case ?? Result?.Cases.OrderBy(c => c.Critical?.Factor ?? double.PositiveInfinity).FirstOrDefault();
        double xmin = surface.Min(p => p.X), xmax = surface.Max(p => p.X), ymin = Math.Max(g.Array("layers").Min(l => l.D("bottom")), -Math.Max(1, g.D("depth_max")) * 1.15), ymax = surface.Max(p => p.Y);
        if (FocusCritical && chosen?.Critical is { } focus)
        {
            double margin = (focus.Circle.Right - focus.Circle.Left) * .12;
            xmin = Math.Max(xmin, focus.Circle.Left - margin); xmax = Math.Min(xmax, focus.Circle.Right + margin);
            ymin = focus.Circle.Y - focus.Circle.Radius - Math.Max(.5, (ymax - focus.Circle.Y + focus.Circle.Radius) * .15);
        }
        if (xmax <= xmin || ymax <= ymin) return;
        double scale = Math.Min((ActualWidth - 80) / (xmax - xmin), (ActualHeight - 100) / (ymax - ymin));
        if (scale <= 0) return;
        double xOffset = (ActualWidth - (xmax - xmin) * scale) / 2;
        Point P(double x, double y) => new(xOffset + (x - xmin) * scale, 48 + (ymax - y) * scale);
        StreamGeometry Polygon(IEnumerable<SlopePoint> vertices)
        {
            var pts = vertices.Select(p => P(p.X, p.Y)).ToArray(); var shape = new StreamGeometry(); using var ctx = shape.Open(); ctx.BeginFigure(pts[0], true, true); ctx.PolyLineTo(pts.Skip(1).ToArray(), true, false); return shape;
        }
        dc.PushClip(new RectangleGeometry(new Rect(20, 45, ActualWidth - 40, ActualHeight - 90)));
        var contour = Polygon(surface.Concat(new[] { new SlopePoint(surface[^1].X, ymin), new SlopePoint(surface[0].X, ymin) }));
        dc.PushClip(contour); double upper = ymax + 1; int layerIndex = 0;
        foreach (var layer in g.Array("layers"))
        {
            double lower = layer.D("bottom"); if (lower < upper)
            {
                var rect = new Rect(P(surface[0].X, upper), P(surface[^1].X, lower)); dc.DrawRectangle(Ui.Brush(RetainingWallDrawing.LayerColors[layerIndex++ % RetainingWallDrawing.LayerColors.Length]), new Pen(Brushes.White, .8), rect);
                hits.Add((rect, $"{layer.S("name")} · fondo y={lower:0.###} m\nγ={layer.D("gamma"):0.##}; γsat={layer.D("gamma_sat"):0.##} kN/m³; φ′={layer.D("phi"):0.##}°; c′={layer.D("c"):0.##}; cu={layer.S("cu")} kPa"));
            }
            upper = lower;
        }
        dc.Pop(); dc.DrawGeometry(null, new Pen(Ui.Navy, 1.3), contour);
        dc.DrawGeometry(Ui.Brush("#CAD2DD"), new Pen(Ui.Navy, 1.6), Polygon(RetainingWall.Outline(Data).Select(p => new SlopePoint(p[0], p[1]))));
        if (g.B("water_enabled"))
        {
            var water = g.Array("water").ToArray(); var pen = new Pen(Ui.Blue, 2) { DashStyle = DashStyles.Dash };
            for (int i = 1; i < water.Length; i++) dc.DrawLine(pen, P(water[i - 1].D("x"), water[i - 1].D("y")), P(water[i].D("x"), water[i].D("y")));
        }
        dc.DrawEllipse(Ui.Navy, null, P(0, 0), 3, 3); Text("0;0", P(0, 0).X + 4, P(0, 0).Y + 3);
        dc.Pop();
        if (chosen?.Critical is { } critical)
        {
            var c = critical.Circle; Brush color = UtilizationPalette.Brush(critical.Ratio <= 1 && (chosen.Boundary || chosen.NumericalFailures > 0) ? null : critical.Ratio);
            for (int i = 0; i < 160; i++) { double x0 = c.Left + (c.Right - c.Left) * i / 160, x1 = c.Left + (c.Right - c.Left) * (i + 1) / 160; dc.DrawLine(new Pen(color, 3), P(x0, c.Base(x0)), P(x1, c.Base(x1))); }
            foreach (var s in critical.Slices)
            {
                var rect = new Rect(P(s.Left, s.TopY), P(s.Right, s.BaseY));
                dc.DrawLine(new Pen(s.Index - 1 == SelectedSlice ? Ui.Blue : Ui.Brush("#667788"), s.Index - 1 == SelectedSlice ? 2 : .5), P(s.Left, c.Base(s.Left)), P(s.Left, SlopeGeometry.Height(surface, s.Left)));
                hits.Add((rect, $"Concio {s.Index} · {s.Soil}\nWterra={s.SoilWeight:0.###}; Wmuro={s.BodyWeight:0.###} kN/m\nu={s.U:0.###} kPa; α={s.Alpha:0.###}°\nN′={s.NormalEffective:0.###}; R={s.Resistance:0.###}; T={s.Mobilized:0.###} kN/m"));
            }
            Text($"{chosen.Factors.Name} · F={critical.Factor:0.000} · γR={chosen.Factors.R:0.00} · η={critical.Ratio:0.000}", 12, 6, color, 12);
            Text($"xc={c.X:0.##}; yc={c.Y:0.##}; R={c.Radius:0.##} m · {chosen.Status}", 12, 25);
        }
        else Text("Profilo globale · x e y in metri · origine al piede di valle", 12, 8);
        Text(UtilizationPalette.Legend, 12, ActualHeight - 28, Ui.Muted, 10);
    }
}
