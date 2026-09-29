using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallDrawing : FrameworkElement
{
    internal JsonObject? Data;
    internal RetainingWall.Result? Calculation;
    internal RetainingWall.LoadCase? Case;
    internal bool Diagrams;
    private static void Text(DrawingContext dc, string text, double x, double y, Brush? color = null, double size = 12)
        => dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, color ?? Ui.Navy, 1), new Point(x, y));
    private static void Polygon(DrawingContext dc, IEnumerable<Point> points, Brush fill, Pen? pen = null)
    {
        var pts = points.ToArray(); if (pts.Length < 3) return;
        var shape = new StreamGeometry(); using (var ctx = shape.Open()) { ctx.BeginFigure(pts[0], true, true); ctx.PolyLineTo(pts.Skip(1).ToArray(), true, false); }
        dc.DrawGeometry(fill, pen, shape);
    }
    protected override void OnRender(DrawingContext dc)
    {
        hits.Clear(); plots.Clear();
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (Data is null || ActualWidth < 120) return;
        var g = Data["geometry"]!; double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), s = g.D("stem_base"), b = a + s + g.D("heel");
        if (!(h > 0 && t > 0 && b > 0) || h + t > 50 || b > 50) { Text(dc, "Completare la geometria", 18, 20); return; }
        if (Diagrams) { DrawDiagrams(dc, h); return; }
        double height = ActualHeight, width = ActualWidth, drawingWidth = width * .69;
        double scale = Math.Min((drawingWidth - 70) / (b + 1), (height - 145) / (h + t));
        if (scale <= 0) return;
        Point P(double x, double y) => new(38 + x * scale, height - 100 - y * scale);
        double back = a + s, extra = Math.Max(.5, (drawingWidth - 40) / scale - b);
        string[] colors = LayerColors;
        double z = 0; int i = 0;
        foreach (var l in Data.Array("layers"))
        {
            double bottom = Math.Min(h + t, z + l.D("thickness"));
            if (bottom > z)
            {
                var rect = new Rect(P(back, h + t - z), P(b + extra, h + t - bottom));
                dc.DrawRectangle(Ui.Brush(colors[i % colors.Length]), new Pen(i == SelectedLayer ? Ui.Blue : Brushes.White, i == SelectedLayer ? 2 : 1), rect);
                string label = $"{++i}. {l.S("name")}\nγ {l.D("gamma"):0.#} / γsat {l.D("gamma_sat"):0.#} kN/m³\nφ′ {l.D("phi"):0.#}° · z {z:0.##}–{bottom:0.##} m";
                hits.Add((rect, label, null));
                if (ShowLabels && rect.Height > 42) WrappedText(dc, label, rect.X + 6, rect.Y + rect.Height / 2 - 22, rect.Width - 12);
            }
            z = bottom; if (z >= h + t) break;
        }
        dc.DrawLine(new Pen(Ui.Muted, 1), P(-.25, 0), P(b + extra, 0));
        Polygon(dc, RetainingWall.Outline(Data).Select(p => P(p[0], p[1])), Ui.Brush("#CCD5DE"), new Pen(Ui.Navy, 1.7));
 
        hits.Add((new Rect(P(a, h + t), P(back, t)), $"H={h:0.###} m; s₀={s:0.###} m; s₁={g.D("stem_top"):0.###} m; t={t:0.###} m; a={a:0.###} m; b={g.D("heel"):0.###} m\nγ muro={Data["materials"].D("gamma"):0.##} kN/m³; fck={Data["materials"].D("fck"):0.#} MPa; fyk={Data["materials"].D("fyk"):0.#} MPa", null));
        if (Mode == "Tassi di lavoro" && Calculation is not null && Case is not null) DrawUtilization(dc, P, back, h + t, width * .72);
        if (ShowRebar && Data.S("family") == "cantilever")
        {
            double cover = Data["materials"].D("cover") / 1000;
            var barPen = new Pen(Ui.Brush("#B75043"), 2);
            dc.DrawLine(barPen, P(a + cover, t + cover), P(back - g.D("stem_top") + cover, h + t - cover));
            dc.DrawLine(barPen, P(back - cover, t + cover), P(back - cover, h + t - cover));
            dc.DrawLine(barPen, P(cover, cover), P(b - cover, cover)); dc.DrawLine(barPen, P(cover, t - cover), P(b - cover, t - cover));
            DrawRebarData(dc, P, h, t, a, back, cover);
        }
        if (Data["water"].B("enabled"))
        {
            double yw = h + t - Data["water"].D("depth"); var waterPen = new Pen(Ui.Blue, 1.5) { DashStyle = DashStyles.Dash };
            dc.DrawLine(waterPen, P(back, yw), P(b + extra, yw)); Text(dc, "Falda", P(b + extra, yw).X - 42, P(0, yw).Y - 17, Ui.Blue, 11);
        }
        if (ShowLoads) DrawLoads(dc, P, back, b + extra, h + t);
        if (ShowLoads && Data["seismic"].B("enabled") && (!CombinedLoads || Case?.State == "SISMA"))
        {
            bool seismicReady = true; double? siteKh = null, siteKv = null;
            try { if (RetainingWall.DeriveSeismic(Data) is { } site) { siteKh = site.Kh; siteKv = site.Kv; } }
            catch (ArgumentException) { seismicReady = false; }
            if (seismicReady)
            {
            var mid = P(a + s / 2, t + h / 2); Arrow(dc, new(mid.X, mid.Y - 18), new(mid.X - 38, mid.Y - 18), Ui.Blue, 2);
            double kh = CombinedLoads ? Case!.Kh : siteKh ?? Data["seismic"].D("kh"), kv = CombinedLoads ? Case!.Kv : siteKv ?? Data["seismic"].D("kv");
            Arrow(dc, new(mid.X - 15, mid.Y), new(mid.X - 15, mid.Y + (kv < 0 ? 25 : -25)), Ui.Blue, 2);
            string seismic = $"{(Data["seismic"].S("method") == "Wood semplificato" ? "Wood (sempl.)" : "MO")}\nkh={kh:0.###}; {(CombinedLoads ? "kv" : "|kv|")}={kv:0.###}";
            Text(dc, seismic, 7, 26, Ui.Blue, 10); hits.Add((new Rect(5, 25, 160, 42), RetainingWall.SeismicHelp, null));
            }
            else Text(dc, "Sisma: input\nincompleti", 7, 26, Brushes.DarkGoldenrod, 10);
        }
        double q = Data.D("version") == 1 && ShowLoads ? Data["loads"].D("surcharge") : 0;
        if (q > 0)
        {
            for (double x = back + .12; x < b + extra; x += Math.Max(.2, (b + extra - back) / 7))
            {
                var p = P(x, h + t); dc.DrawLine(new Pen(Ui.Navy, 1), new(p.X, p.Y - 20), p);
                dc.DrawLine(new Pen(Ui.Navy, 1), p, new(p.X - 3, p.Y - 6)); dc.DrawLine(new Pen(Ui.Navy, 1), p, new(p.X + 3, p.Y - 6));
            }
            Text(dc, $"qk = {RetainingWallWorkspace.F(q)} kPa", P(back, h + t).X, P(0, h + t).Y - 38, size: 11);
        }
        Text(dc, "Valle", 8, height - 52, size: 11); Text(dc, "Monte", P(b, 0).X - 30, height - 52, size: 11);
        Text(dc, $"H = {h:0.00} m", 3, P(0, (h + t) / 2).Y, size: 11);
        Text(dc, $"B = {b:0.00} m", P(b / 2, 0).X - 35, height - 23, size: 11);
        Text(dc, Data.S("family") == "gravity" ? "GRAVITÀ" : "MENSOLA IN C.A.", 10, 7, size: 12);
        if (ShowLabels)
        {
            Text(dc, $"s₁={g.D("stem_top"):0.##}", P(back - g.D("stem_top"), h + t).X - 35, P(0, h + t).Y - 17, size: 10);
            Text(dc, $"a={a:0.##} · s₀={s:0.##} · b={g.D("heel"):0.##} · t={t:0.##} m", 10, height - 43, size: 10);
        }

        if (Calculation is null || Case is null) return;
        var c = Case; double right = width * .77, max = c.Pressures.SelectMany(p => new[] { p.P0, p.P1 }).DefaultIfEmpty(1).Max();
        if (Mode == "Forze resistenti") { DrawResistance(dc, P, b, right); return; }
        double pw = Math.Max(20, width - right - 15);
        if (ShowPressures && Mode != "Tassi di lavoro")
        {
        Text(dc, "Spinta [kPa]", right - 7, 10, size: 11);
        foreach (var (seg, idx) in c.PressureDetails.Select((p, idx) => (p, idx)))
        {
            double y0 = P(0, h + t - seg.Z0).Y, y1 = P(0, h + t - seg.Z1).Y;
            Polygon(dc, [new(right, y0), new(right + pw * seg.Total0 / Math.Max(1, max), y0), new(right + pw * seg.Total1 / Math.Max(1, max), y1), new(right, y1)], Ui.Brush(idx == SelectedPressure ? "#82BFDC" : "#C8E0EE"), new Pen(Ui.Blue, 1));
            plots.Add((new Rect(right, y0, pw, Math.Max(1, y1 - y0)), point => { double f = Math.Clamp((point.Y - y0) / Math.Max(.001, y1 - y0), 0, 1); return $"{c.Name}: z={seg.Z0 + f * (seg.Z1 - seg.Z0):0.000} m · p={seg.Total0 + f * (seg.Total1 - seg.Total0):0.000} kPa · φd={seg.PhiDesign:0.00}° · K={seg.K:0.0000} · Kae={seg.Ke:0.0000} · Δp={seg.Dynamic:0.000} kPa"; }));
        }
        Text(dc, RetainingWallWorkspace.F(max), right, height - 54, Ui.Blue, 11);
        }
        if (c.Contact.Valid)
        {
            var x0 = P(c.Contact.Start, 0); var x1 = P(c.Contact.End, 0);
            Polygon(dc, [x0, x1, new(x1.X, x1.Y + 32 * c.Contact.Heel / c.Contact.Peak), new(x0.X, x0.Y + 32 * c.Contact.Toe / c.Contact.Peak)], Ui.Brush("#E7C4BA"), new Pen(Ui.Brush("#B75043"), 1));
            Text(dc, $"pmax = {c.Contact.Peak:0.0} kPa", width * .5, height - 20, size: 10);
            plots.Add((new Rect(P(0, 0).X, P(0, 0).Y, b * scale, 32), point => $"Contatto: x={(point.X - 38) / scale:0.000} m; p={RetainingWall.Pressure(c.Contact, (point.X - 38) / scale):0.000} kPa; e={c.Eccentricity:0.000} m; B′={c.EffectiveWidth:0.000} m"));
        }
        else Text(dc, "PERDITA DI EQUILIBRIO", 75, height - 36, Brushes.Firebrick, 12);
    }
    private void DrawDiagrams(DrawingContext dc, double h)
    {
        if (Calculation is null || Case is null) { Text(dc, "Diagrammi disponibili dopo il calcolo", 20, 25); return; }
        if (Member != "Fusto" && !Case.Contact.Valid) { Text(dc, "Reazioni non disponibili: perdita di equilibrio", 20, 25, Brushes.Firebrick); return; }
        var cuts = Case.Sections.Where(s => s.Name == Member).OrderBy(s => s.Position).ToArray(); if (cuts.Length == 0) { Text(dc, "Elemento assente", 20, 25); return; }
        h = Math.Max(.01, cuts.Max(s => s.Position));
        foreach (var (title, selector, col, color) in new (string, Func<RetainingWall.SectionForce, double>, int, Brush)[] {
            ("M " + Member + " [kNm/m]", s => s.M, 0, Ui.Blue), ("V " + Member + " [kN/m]", s => s.V, 1, Ui.Brush("#AA5544")), ("N " + Member + " [kN/m]", s => s.N, 2, Ui.Brush("#407D62")) })
        {
            double w = ActualWidth / 3, left = w * col + 30, y = 40, hh = ActualHeight - 90;
            double minimum = Math.Min(0, cuts.Min(selector)), maximum = Math.Max(0, cuts.Max(selector)), span = Math.Max(20, w - 80), range = Math.Max(1, maximum - minimum);
            double x = left - span * minimum / range;
            Text(dc, title, left, 12, color, 11); dc.DrawLine(new Pen(Ui.Muted, 1), new(x, y), new(x, y + hh));
            Text(dc, "0", left - 22, y - 6, Ui.Muted, 10); Text(dc, $"{h:0.#} m", left - 25, y + hh - 7, Ui.Muted, 10);
            var pts = new List<Point> { new(x, y) }; pts.AddRange(cuts.Select(s => new Point(x + span * selector(s) / range, y + hh * s.Position / h))); pts.Add(new(x, y + hh));
            var fill = color.Clone(); fill.Opacity = .15; Polygon(dc, pts, fill, new Pen(color, 1.5));
            Text(dc, $"min {minimum:0.00} · max {maximum:0.00}", left, ActualHeight - 32, color, 11);
            plots.Add((new Rect(w * col, y, w, hh), point => { double z = Math.Clamp((point.Y - y) / hh * h, 0, h); var upper = cuts.FirstOrDefault(s => s.Position >= z) ?? cuts[^1]; var lower = cuts.LastOrDefault(s => s.Position <= z) ?? cuts[0]; double f = upper.Position == lower.Position ? 0 : (z - lower.Position) / (upper.Position - lower.Position); return $"{Case.Name} · {Member} · z/l={z:0.000} m · {title}={selector(lower) + f * (selector(upper) - selector(lower)):0.000}"; }));
        }
        Text(dc, Member == "Fusto" ? "z dalla testa al piede · N positivo a compressione · M positivo: trazione a monte" : "l dal bordo libero alla radice · M positivo: trazione inferiore", 20, ActualHeight - 15, Ui.Muted, 10);
    }
}




