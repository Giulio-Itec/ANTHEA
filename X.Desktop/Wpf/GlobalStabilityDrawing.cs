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
    internal bool ShowSearch;
    internal int SelectedSoil = -1;
    internal string SelectedColumn = "monte";
    internal int SoilLabelsDrawn { get; private set; }
    internal bool SearchDrawn { get; private set; }
    private readonly List<(Rect Rect, string Text)> hits = [];
    internal GlobalStabilityDrawing() { ClipToBounds = true; MouseMove += (_, e) => ToolTip = hits.LastOrDefault(h => h.Rect.Contains(e.GetPosition(this))).Text; }
    protected override void OnRender(DrawingContext dc)
    {
        hits.Clear(); SoilLabelsDrawn = 0; SearchDrawn = false; dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
        void Text(string text, double x, double y, Brush? color = null, double size = 11) => dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, color ?? Ui.Navy, 1), new Point(x, y));
        if (Data is null || ActualWidth < 150) return;
        var g = Data["global_stability"]; var valley = g.Array("valley").Select(p => new SlopePoint(p.D("x"), p.D("y"))).ToArray(); var uphill = g.Array("uphill").Select(p => new SlopePoint(p.D("x"), p.D("y"))).ToArray();
        if (valley.Length < 2 || uphill.Length < 2 || g.Array("layers").Count == 0) { Text("Precompilare e completare il profilo globale", 12, 20); return; }
        if (valley.Concat(uphill).Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) { Text("Completa le coordinate del profilo", 12, 20); return; }
        var geometry = Data["geometry"]!; double t = geometry.D("slab"), a = geometry.D("toe"), back = a + geometry.D("stem_base"), ht = geometry.D("height") + t;
        var surface = Result?.Section.Surface ?? RetainingWall.GlobalSurface(Data, valley, uphill);
        var chosen = Case ?? Result?.Cases.OrderByDescending(c => c.Critical?.Ratio ?? double.PositiveInfinity).FirstOrDefault();
        var allLayers = g.Array("layers").Concat(g.S("soil_mode") == "Due colonne" ? g.Array("valley_layers") : new JsonArray());
        double knownBottom = allLayers.Select(l => J.Number(l?["bottom"]) ?? 0).Where(double.IsFinite).DefaultIfEmpty(0).Min();
        double xmin = surface.Min(p => p.X), xmax = surface.Max(p => p.X), ymin = Math.Min(-1.5, Math.Max(knownBottom, -Math.Max(2, g.D("depth_max")) * 1.15)), ymax = surface.Max(p => p.Y);
        if (FocusCritical && chosen?.Critical is { } focus)
        {
            double margin = (focus.Circle.Right - focus.Circle.Left) * .12;
            xmin = Math.Max(xmin, focus.Circle.Left - margin); xmax = Math.Min(xmax, focus.Circle.Right + margin);
            ymin = focus.Circle.Y - focus.Circle.Radius - Math.Max(.5, (ymax - focus.Circle.Y + focus.Circle.Radius) * .15);
        }
        if (xmax <= xmin || ymax <= ymin) return;
        double scale = Math.Min((ActualWidth - 80) / (xmax - xmin), (ActualHeight - 120) / (ymax - ymin));
        if (scale <= 0) return;
        double xOffset = (ActualWidth - (xmax - xmin) * scale) / 2;
        Point P(double x, double y) => new(xOffset + (x - xmin) * scale, 65 + (ymax - y) * scale);
        StreamGeometry Polygon(IEnumerable<SlopePoint> vertices)
        {
            var pts = vertices.Select(p => P(p.X, p.Y)).ToArray(); var shape = new StreamGeometry(); using var ctx = shape.Open(); ctx.BeginFigure(pts[0], true, true); ctx.PolyLineTo(pts.Skip(1).ToArray(), true, false); return shape;
        }
        dc.PushClip(new RectangleGeometry(new Rect(20, 45, ActualWidth - 40, ActualHeight - 90)));
        var contour = Polygon(surface.Concat(new[] { new SlopePoint(surface[^1].X, ymin), new SlopePoint(surface[0].X, ymin) }));
        dc.PushClip(contour); dc.DrawGeometry(Ui.Brush("#EFF1F4"), null, contour);
        foreach (var column in g.S("soil_mode") == "Due colonne" ? new[] { (Name: "valle", Layers: g.Array("valley_layers"), Left: surface[0].X, Right: g.D("soil_split_x")), (Name: "monte", Layers: g.Array("layers"), Left: g.D("soil_split_x"), Right: surface[^1].X) } : new[] { (Name: "monte", Layers: g.Array("layers"), Left: surface[0].X, Right: surface[^1].X) })
        {
        double upper = ymax + 1; int layerIndex = 0;
        if (!double.IsFinite(column.Left) || !double.IsFinite(column.Right) || column.Right <= column.Left) continue;
        foreach (var layer in column.Layers)
        {
            double lower = J.Number(layer?["bottom"]) ?? double.NaN; if (!double.IsFinite(lower)) break;
            if (lower < upper)
            {
                var rect = new Rect(P(column.Left, upper), P(column.Right, lower));
                bool selected = SelectedSoil == layerIndex && SelectedColumn == column.Name;
                dc.DrawRectangle(Ui.Brush(RetainingWallDrawing.GlobalLayerColor(Data, layer)), new Pen(selected ? Ui.Blue : Brushes.White, selected ? 2 : .8), rect);
                hits.Add((rect, $"{layer.S("name")} · fondo y={lower:0.###} m\nγ={layer.D("gamma"):0.##}; γsat={layer.D("gamma_sat"):0.##} kN/m³; φ′={layer.D("phi"):0.##}°; c′={layer.D("c"):0.##}; cu={layer.S("cu")} kPa"));
                double labelX = Math.Max(column.Left, xmin) + .25;
                double labelTop = Math.Min(upper, Slope.Height(surface, labelX));
                double labelBottom = Math.Max(lower, ymin);
                double availableWidth = P(Math.Min(column.Right, xmax), 0).X - P(labelX, 0).X - 8;
                if ((labelTop - labelBottom) * scale > 26 && availableWidth > 70)
                {
                    var label = new FormattedText($"{layer.S("name")}\nfondo y = {lower:0.##} m", CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Ui.Navy, 1)
                        { MaxTextWidth = availableWidth, MaxTextHeight = 27, Trimming = TextTrimming.CharacterEllipsis };
                    dc.DrawText(label, P(labelX, labelTop - Math.Min(.3, (labelTop - labelBottom) * .15))); SoilLabelsDrawn++;
                }
            }
            upper = lower; layerIndex++;
        }
        }
        dc.Pop(); dc.DrawGeometry(null, new Pen(Ui.Navy, 1.3), contour);
        var referencePen = new Pen(Ui.Muted, .8) { DashStyle = DashStyles.Dash };
        dc.DrawLine(referencePen, P(xmin, 0), P(xmax, 0));
        Text("y=0 · piano di posa", P(xmax, 0).X - 115, P(xmax, 0).Y - 15, Ui.Muted, 10);
        if (ShowSearch && new[] { "exit_min", "exit_max", "entry_min", "entry_max", "depth_max" }.All(k => J.Number(g?[k]) is double n && double.IsFinite(n)))
        {
            var pen = new Pen(Ui.Blue, 3);
            foreach (var range in new[] { ("exit_min", "exit_max", "Uscite a valle"), ("entry_min", "entry_max", "Ingressi a monte") })
            {
                double start = Math.Max(xmin, g.D(range.Item1)), end = Math.Min(xmax, g.D(range.Item2));
                if (end <= start) continue;
                for (int i = 0; i < 50; i++) { double x0 = start + (end - start) * i / 50, x1 = start + (end - start) * (i + 1) / 50; dc.DrawLine(pen, P(x0, Slope.Height(surface, x0)), P(x1, Slope.Height(surface, x1))); }
                var middle = P((start + end) / 2, Slope.Height(surface, (start + end) / 2));
                Text(range.Item3, middle.X - 34, middle.Y - 17, Ui.Blue, 10);
            }
            double depth = g.D("depth_max");
            if (depth > 0) dc.DrawLine(new Pen(Ui.Blue, 1) { DashStyle = DashStyles.Dash }, P(xmin, -depth), P(xmax, -depth));
            SearchDrawn = true;
        }
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
                dc.DrawLine(new Pen(s.Index - 1 == SelectedSlice ? Ui.Blue : Ui.Brush("#667788"), s.Index - 1 == SelectedSlice ? 2 : .5), P(s.Left, c.Base(s.Left)), P(s.Left, Slope.Height(surface, s.Left)));
                hits.Add((rect, $"Concio {s.Index} · {s.Soil}\nWterra={s.SoilWeight:0.###}; Wmuro={s.BodyWeight:0.###} kN/m\nu={s.U:0.###} kPa; α={s.Alpha:0.###}°\nN′={s.NormalEffective:0.###}; R={s.Resistance:0.###}; T={s.Mobilized:0.###} kN/m"));
            }
            Text($"{chosen.Factors.Name} · F={critical.Factor:0.000} · γR={chosen.Factors.R:0.00} · η={critical.Ratio:0.000}", 12, 6, color, 12);
            Text($"xc={c.X:0.##}; yc={c.Y:0.##}; R={c.Radius:0.##} m · {chosen.Status}", 12, 25);
        }
        else Text("Profilo globale · x e y in metri · origine al piede di valle", 12, 8);
        Text(ShowSearch ? $"Blu: ricerca · profondità max {g.S("depth_max")} m sotto il piano di posa" : UtilizationPalette.Legend, 12, ActualHeight - 32, Ui.Muted, 10);
        if (chosen is null) Text("Grigio chiaro: terreno non definito · seleziona uno strato o passa sul disegno per i dati", 12, ActualHeight - 17, Ui.Muted, 9);
    }
}
