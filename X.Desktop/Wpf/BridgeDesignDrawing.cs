using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class BridgeDesignDrawing : FrameworkElement
{
    internal BridgeConcept.Result? Result;
    internal JsonObject? Data;
    internal bool Section;
    private static readonly Brush Ink = Ui.Navy, Line = Ui.Brush("#536F87"), Concrete = Ui.Brush("#D5E1EA"), Steel = Ui.Brush("#4C789B"), Accent = Ui.Brush("#168B94");
    internal byte[] Png(bool? section = null)
    {
        var drawing = new BridgeDesignDrawing { Data = Data, Result = Result, Section = section ?? Section, Width = 1200, Height = 480 };
        drawing.Measure(new Size(1200, 480)); drawing.Arrange(new Rect(0, 0, 1200, 480)); return Ui.Snapshot(drawing);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(Ui.Brush("#F8FAFD"), null, new Rect(0, 0, ActualWidth, ActualHeight));
        double scale = Math.Min(ActualWidth / 1000, ActualHeight / 420);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.PushTransform(new TranslateTransform((ActualWidth - 1000 * scale) / 2, (ActualHeight - 420 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        if (Result is not { } r || Data is null) Text(dc, "Completa i dati per visualizzare il ponte", 300, 190, 18, Line);
        else
        {
            Text(dc, Section ? "SEZIONE TRASVERSALE" : "PROSPETTO GENERALE", 24, 18, 11, Line, true);
            Text(dc, "SCHEMA DI CONCETTO · NON IN SCALA", 727, 18, 10, Line);
            if (Section) DrawSection(dc, r, Data["input"]!); else DrawElevation(dc, r, Data);
        }
        dc.Pop(); dc.Pop(); dc.Pop();
    }
    private static void DrawElevation(DrawingContext dc, BridgeConcept.Result r, JsonObject data)
    {
        var i = data["input"]!; var random = new Random((int)data.D("scene", 1));
        // Seed affects scenery only. All bridge coordinates come from the calculation result.
        for (int band = 0; band < 3; band++)
        {
            var pts = new List<Point> { new(0, 340) };
            for (int x = 0; x <= 1000; x += 50) pts.Add(new(x, 180 + band * 36 - random.Next(20, 70)));
            pts.Add(new(1000, 420)); pts.Add(new(0, 420));
            Poly(dc, pts, Ui.Brush(new[] { "#EBF0F6", "#E3EBF1", "#DCE6EA" }[band]));
        }
        double start = 90, spanScale = 820 / r.Length, deckY = BridgeConcept.HasUpperStructure(r.Family.Id) ? 185 : 148, ground = 285, visualHeight = ground - deckY;
        double depth = Math.Clamp(r.Depth / i.D("height") * visualHeight, 9, 58);
        double pierDepth = Math.Clamp(r.PierDepth / i.D("height") * visualHeight, depth, 70);
        bool obstacle = i.S("obstacle") != "Nessuno" && i.D("obstacle_width") > 0;
        double obstacleHalf = i.D("obstacle_width") * spanScale / 2;
        Poly(dc, [new(0, 275), new(55, 260), new(90, deckY + depth + 8), new(116, deckY + depth + 8), new(174, ground), new(826, ground), new(884, deckY + depth + 8), new(914, deckY + depth + 8), new(950, 260), new(1000, 275), new(1000, 420), new(0, 420)], Ui.Brush("#E3E7DF"));
        if (obstacle)
        {
            if (i.S("obstacle") == "Fiume")
            {
                Poly(dc, [new(500 - obstacleHalf, ground), new(500 + obstacleHalf, ground), new(500 + obstacleHalf + 65, 420), new(500 - obstacleHalf - 65, 420)], Ui.Brush("#BDDBE6"));
                for (int k = 0; k < 8; k++)
                { double y = 300 + k * 14; dc.DrawLine(new Pen(Ui.Brush("#95C1D2"), 1), new Point(500 - obstacleHalf / 2 - k * 2, y), new Point(500 + obstacleHalf / 2 + k * 2, y)); }
            }
            else
            {
                Poly(dc, [new(500 - obstacleHalf, ground), new(500 + obstacleHalf, ground), new(500 + obstacleHalf + 30, 420), new(500 - obstacleHalf - 30, 420)], Ui.Brush("#B6C1CC"));
                var roadPen = new Pen(Brushes.White, 2) { DashStyle = DashStyles.Dash }; dc.DrawLine(roadPen, new Point(500, ground), new Point(500, 420));
            }
            Text(dc, i.S("obstacle") + " · " + F(i.D("obstacle_width")) + " m", 510 + obstacleHalf, 338, 11, Line);
        }
        for (int tree = 0; tree < 16; tree++)
        {
            double x = random.Next(10, 990); if (Math.Abs(x - 500) < obstacleHalf + 15 || x > 65 && x < 155 || x > 845 && x < 935) continue;
            double y = 280 + random.Next(6, 38), ht = random.Next(10, 24);
            dc.DrawLine(new Pen(Ui.Brush("#81998D"), 1), new Point(x, y), new Point(x, y - ht));
            Poly(dc, [new(x - 5, y - ht / 3), new(x, y - ht), new(x + 5, y - ht / 3)], Ui.Brush("#AFBEB3"));
        }
        foreach (var s in r.Supports)
        {
            double x = start + s.X * spanScale, baseY = s.Type == "Spalla" ? deckY + depth + 36 : ground;
            double pw = Math.Clamp(s.PierSize * 5, 7, 22), ph = baseY - deckY - pierDepth;
            bool wall = s.Type == "Setto";
            if (s.Type == "Spalla") Box(dc, x - 6, deckY + depth, 12, 36, Concrete);
            else
            {
                Box(dc, x - pw / 2, deckY + pierDepth + 7, pw, Math.Max(5, ph - 7), Concrete);
                if (!wall) Box(dc, x - 15, deckY + pierDepth + 3, 30, 9, Concrete);
            }
            double fw = Math.Clamp(s.FootingSize * 5, 24, 65);
            Box(dc, x - fw / 2, baseY, fw, 9, Concrete);
            if (s.Piles > 0)
            {
                double pl = Math.Clamp(r.PileLength * 2, 22, 80);
                for (int j = 0; j < Math.Min(4, s.Piles); j++)
                { double px = s.Piles == 1 ? x : x - fw * .35 + j * fw * .7 / (Math.Min(4, s.Piles) - 1); Box(dc, px - 2, baseY + 9, 4, pl, Ui.Brush("#E9EFF3")); }
            }
            dc.DrawRectangle(Accent, null, new Rect(x - 4, deckY + pierDepth, 8, 4));
        }
        double offset = start;
        for (int k = 0; k < r.Spans.Length; k++)
        {
            double l = r.Spans[k] * spanScale;
            if (r.Family.Id == "fcm")
            {
                var points = new List<Point> { new(offset, deckY), new(offset + l, deckY) };
                for (int j = 20; j >= 0; j--) { double t = j / 20.0; points.Add(new(offset + t * l, deckY + depth + (pierDepth - depth) * Math.Pow(2 * t - 1, 2))); }
                Poly(dc, points, Concrete, new Pen(Ink, 1.3));
            }
            else Box(dc, offset, deckY, l, depth, Concrete);
            if (!i.B("continuous") && k > 0) dc.DrawLine(new Pen(Brushes.White, 3), new Point(offset, deckY), new Point(offset, deckY + depth));
            Dimension(dc, offset, offset + l, BridgeConcept.HasUpperStructure(r.Family.Id) ? 348 : deckY - 34, F(r.Spans[k]) + " m"); offset += l;
        }
        if (r.Advanced is { } advanced) DrawUpperStructure(dc, r, advanced, start, spanScale, deckY, ground);
        dc.DrawLine(new Pen(Ink, 1.5), new Point(start - 18, deckY - 9), new Point(930, deckY - 9));
        for (int x = 75; x <= 930; x += 18) dc.DrawLine(new Pen(Line, .7), new Point(x, deckY - 9), new Point(x, deckY));
        dc.DrawLine(new Pen(Line, .7), new Point(75, deckY - 5), new Point(930, deckY - 5));
        Text(dc, "d = " + F(r.Depth, "0.00") + " m", 440, deckY + depth + 14, 12, Accent, true);
        Text(dc, "H ≈ " + F(i.D("height"), "0") + " m", 22, 210, 12, Line);
        Text(dc, r.Family.Name, 24, 374, 16, Ink, true);
        Text(dc, $"{r.Spans.Length} campate   /   {r.Foundation}" + (r.PileLength > 0 ? " × " + F(r.PileLength, "0") + " m" : ""), 24, 399, 11, Line);
        Text(dc, "L = " + F(r.Length) + " m", 834, 399, 11, Line);
    }
    private static void DrawSection(DrawingContext dc, BridgeConcept.Result r, JsonNode i)
    {
        if (r.Advanced is not null) { DrawExtendedSection(dc, r, i); return; }
        double x0 = 120, width = 760, scale = width / r.Width, top = 150;
        double verticalScale = Math.Min(95, 160 / r.Depth), d = r.Depth * verticalScale, slab = r.Slab * verticalScale;
        Dimension(dc, x0, x0 + width, 95, "W = " + F(r.Width, "0.00") + " m");
        Box(dc, x0, top, width, slab, Concrete);
        double g = width / r.Girders, h = d - slab;
        string family = r.Family.Id;
        if (family is "psc_box" or "fcm")
        {
            double bw = width * i.D("bottom_ratio"), left = 500 - bw / 2, bt = r.Bottom * verticalScale;
            Box(dc, left, top + d - bt, bw, bt, Concrete);
            int cells = (int)i.D("cells"); double wt = Math.Max(6, r.Web * scale);
            for (int c = 0; c <= cells; c++) Box(dc, left + c * (bw - wt) / cells, top + slab, wt, h - bt, Concrete);
        }
        else if (family == "steel_box")
        {
            for (int j = 0; j < r.Girders; j++)
            {
                double x = x0 + (j + .5) * g, bw = g * .4, tw = Math.Max(3, i.D("web_mm") * verticalScale / 1000), ft = Math.Max(4, i.D("flange_mm") * verticalScale / 1000);
                double run = Math.Max(0, r.Depth - r.Slab - 2 * i.D("flange_mm") / 1000) * i.D("web_slope") / 4 * scale;
                Box(dc, x - bw / 2, top + d - ft, bw, ft, Steel);
                dc.DrawLine(new Pen(Steel, tw), new Point(x - bw / 2 - run, top + slab), new Point(x - bw / 2, top + d - ft));
                dc.DrawLine(new Pen(Steel, tw), new Point(x + bw / 2 + run, top + slab), new Point(x + bw / 2, top + d - ft));
                double flangeWidth = i.D("flange_width") * scale;
                Box(dc, x - bw / 2 - run - flangeWidth / 2, top + slab, flangeWidth, ft, Steel); Box(dc, x + bw / 2 + run - flangeWidth / 2, top + slab, flangeWidth, ft, Steel);
            }
        }
        else if (family != "slab")
        {
            for (int j = 0; j < r.Girders; j++)
            {
                double x = x0 + (j + .5) * g;
                if (family == "psc_u")
                {
                    double ut = i.D("u_top") * scale, ub = i.D("u_bottom") * scale;
                    Poly(dc, [new(x - ut / 2, top + slab), new(x - ub / 2, top + d), new(x + ub / 2, top + d), new(x + ut / 2, top + slab), new(x + ut / 2 - 9, top + slab), new(x + ub / 2 - 7, top + d - 12), new(x - ub / 2 + 7, top + d - 12), new(x - ut / 2 + 9, top + slab)], Concrete, new Pen(Ink, 1.2));
                }
                else if (family == "tee") Box(dc, x - r.Web * scale / 2, top + slab, r.Web * scale, h, Concrete);
                else
                {
                    double fw = family == "steel_i" ? Math.Min(g * .7, i.D("flange_width") * scale) : Math.Min(g * .65, .7 * scale);
                    double ft = family == "steel_i" ? Math.Max(4, i.D("flange_mm") / 1000 * verticalScale) : Math.Min(14, h / 4);
                    double wt = family == "steel_i" ? 3 : Math.Min(10, fw / 3); var brush = family == "steel_i" ? Steel : Concrete;
                    Box(dc, x - fw / 2, top + slab, fw, ft, brush); Box(dc, x - wt / 2, top + slab + ft, wt, Math.Max(1, h - 2 * ft), brush); Box(dc, x - fw / 2, top + d - ft, fw, ft, brush);
                }
            }
        }
        Box(dc, x0, top - 4, width, 4, Ui.Brush("#778895"));
        foreach (double x in i.D("median") > 0 ? new[] { x0, 500, x0 + width - 10 } : new[] { x0, x0 + width - 10 })
            Poly(dc, [new(x, top - 4), new(x + 3, top - 26), new(x + 8, top - 26), new(x + 11, top - 4)], Concrete, new Pen(Line, 1));
        Text(dc, "d = " + F(r.Depth, "0.00") + " m", 890, top + d / 2, 12, Accent, true);
        string dimensions = family switch {
            "steel_i" or "steel_box" => $"Soletta {F(r.Slab * 1000, "0")} mm · bf {F(i.D("flange_width") * 1000, "0")} mm · tf {F(i.D("flange_mm"), "0.#")} mm · tw {F(i.D("web_mm"), "0.#")} mm",
            "psc_box" or "fcm" => $"Soletta {F(r.Slab * 1000, "0")} mm · anime {F(r.Web * 1000, "0")} mm · fondo {F(r.Bottom * 1000, "0")} mm · b inf {F(r.Width * i.D("bottom_ratio"), "0.00")} m · {i.D("cells"):0} celle",
            "psc_u" => $"Soletta {F(r.Slab * 1000, "0")} mm · anime {F(r.Web * 1000, "0")} mm · fondo {F(r.Bottom * 1000, "0")} mm · U {F(i.D("u_top"), "0.00")} / {F(i.D("u_bottom"), "0.00")} m",
            "tee" => $"Soletta {F(r.Slab * 1000, "0")} mm · anima {F(r.Web * 1000, "0")} mm · s rif {F(r.Spacing, "0.00")} m",
            "psc_i" => $"Soletta {F(r.Slab * 1000, "0")} mm · rialzo {F(i.D("haunch") * 1000, "0")} mm · s rif {F(r.Spacing, "0.00")} m · profilo rettangolare equivalente",
            _ => $"Soletta piena · spessore {F(r.Slab * 1000, "0")} mm" };
        Text(dc, dimensions, 120, 337, 12, Line);
        Text(dc, r.Family.Name, 120, 367, 17, Ink, true);
        Text(dc, $"{r.Girders} elementi longitudinali · {i.D("lanes"):0} corsie · quote geometriche indicative", 120, 393, 12, Line);
    }
    internal static void Box(DrawingContext dc, double x, double y, double w, double h, Brush fill)
    { if (w > 0 && h > 0) dc.DrawRectangle(fill, new Pen(Ink, 1), new Rect(x, y, w, h)); }
    internal static void Poly(DrawingContext dc, IEnumerable<Point> points, Brush fill, Pen? pen = null)
    {
        var p = points.ToArray(); if (p.Length < 3) return; var geometry = new StreamGeometry();
        using (var c = geometry.Open()) { c.BeginFigure(p[0], true, true); c.PolyLineTo(p.Skip(1).ToArray(), true, false); }
        geometry.Freeze(); dc.DrawGeometry(fill, pen, geometry);
    }
    internal static void Text(DrawingContext dc, string text, double x, double y, double size, Brush brush, bool bold = false)
    {
        var type = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
        dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, type, size, brush, 1), new Point(x, y));
    }
    private static void Dimension(DrawingContext dc, double a, double b, double y, string label)
    {
        var pen = new Pen(Line, .8); dc.DrawLine(pen, new Point(a + 1, y), new Point(b - 1, y));
        foreach (double x in new[] { a, b }) dc.DrawLine(pen, new Point(x, y - 4), new Point(x, y + 5));
        Text(dc, label, (a + b) / 2 - label.Length * 2.8, y - 20, 11, Line);
    }
    private static string F(double value, string fmt = "0.0") => BridgeDesignWorkspace.F(value, fmt);
}

internal sealed class BridgeFamilyIcon : FrameworkElement
{
    internal string Family = "psc_i";
    internal Brush Ink = Ui.Navy;
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight; var p = new Pen(Ink, 1.5); dc.DrawRectangle(null, p, new Rect(8, 5, w - 16, 5));
        if (BridgeConcept.HasUpperStructure(Family))
        {
            dc.DrawRectangle(Ui.Bg, null, new Rect(0, 0, w, h)); double y = h - 6;
            dc.DrawLine(p, new(8, y), new(w - 8, y));
            if (Family == "tied_arch")
            {
                for (int k = 0; k < 16; k++) { double x1 = 8 + (w - 16) * k / 16, x2 = 8 + (w - 16) * (k + 1) / 16;
                    double z1 = y - (h - 10) * 4 * k / 16 * (1 - k / 16d), z2 = y - (h - 10) * 4 * (k + 1) / 16 * (1 - (k + 1) / 16d);
                    dc.DrawLine(p, new(x1, z1), new(x2, z2)); if (k % 3 == 0) dc.DrawLine(p, new(x1, z1), new(x1, y)); }
            }
            else if (Family == "truss") { dc.DrawLine(p, new(8, 6), new(w - 8, 6)); for (int k = 0; k < 6; k++) dc.DrawLine(p, new(8 + k * (w - 16) / 6, k % 2 == 0 ? 6 : y), new(8 + (k + 1) * (w - 16) / 6, k % 2 == 0 ? y : 6)); }
            else foreach (double x in new[] { w * .25, w * .75 }) { dc.DrawLine(p, new(x, 3), new(x, y)); for (int k = -2; k <= 2; k++) dc.DrawLine(p, new(x, 3), new(x + k * w / 10, Family == "suspension" && Math.Abs(k) == 1 ? y - 7 : y)); }
            return;
        }
        if (Family == "filler_beam") { dc.DrawRectangle(null, p, new Rect(8, 5, w - 16, h - 10)); for (int k = 1; k < 6; k++) { double x = 8 + k * (w - 16) / 6; dc.DrawLine(p, new(x, 9), new(x, h - 9)); dc.DrawLine(p, new(x - 4, 9), new(x + 4, 9)); dc.DrawLine(p, new(x - 4, h - 9), new(x + 4, h - 9)); } return; }
        if (Family == "orthotropic") { for (int k = 1; k < 7; k++) { double x = 8 + k * (w - 16) / 7; dc.DrawLine(p, new(x - 4, 10), new(x - 2, 17)); dc.DrawLine(p, new(x - 2, 17), new(x + 2, 17)); dc.DrawLine(p, new(x + 2, 17), new(x + 4, 10)); } dc.DrawRectangle(null, p, new Rect(23, 10, w - 46, h - 15)); return; }
        if (Family == "slab") return;
        if (Family is "psc_box" or "fcm" or "steel_box")
        { dc.DrawLine(p, new Point(18, 10), new Point(27, h - 5)); dc.DrawLine(p, new Point(w - 18, 10), new Point(w - 27, h - 5)); dc.DrawLine(p, new Point(27, h - 5), new Point(w - 27, h - 5)); if (Family == "psc_box") dc.DrawLine(p, new Point(w / 2, 10), new Point(w / 2, h - 5)); return; }
        foreach (double x in new[] { w * .3, w * .7 })
        {
            if (Family == "psc_u") { dc.DrawLine(p, new Point(x - 12, 10), new Point(x - 7, h - 5)); dc.DrawLine(p, new Point(x + 12, 10), new Point(x + 7, h - 5)); }
            else dc.DrawLine(p, new Point(x, 10), new Point(x, h - 5));
            if (Family != "tee") dc.DrawLine(p, new Point(x - 7, h - 5), new Point(x + 7, h - 5));
        }
    }
}

internal sealed class BridgeConceptMomentPlot : FrameworkElement
{
    internal BridgeConcept.Result? Result;
    protected override void OnRender(DrawingContext dc)
    {
        if (Result is not { } r || ActualWidth < 100) return;
        if (r.Stations.Length == 0) { BridgeDesignDrawing.Text(dc, "Diagrammi globali non disponibili per questa tipologia.", 12, 50, 12, Ui.Muted); BridgeDesignDrawing.Text(dc, "Consultare equilibrio, forze e quantità nei dettagli del modello.", 12, 75, 12, Ui.Muted); return; }
        double max = r.Stations.Max(s => Math.Abs(s.Moment)); if (max == 0) max = 1;
        double xs = (ActualWidth - 100) / r.Length, ys = 55 / max, axis = 80;
        dc.DrawLine(new Pen(Ui.Muted, 1), new Point(50, axis), new Point(ActualWidth - 50, axis));
        foreach (var span in r.Stations.GroupBy(s => s.Span))
        {
            var pts = span.Select(s => new Point(50 + s.X * xs, axis + s.Moment * ys)).ToArray();
            BridgeDesignDrawing.Poly(dc, new[] { new Point(pts[0].X, axis) }.Concat(pts).Append(new Point(pts[^1].X, axis)), Ui.Brush("#DDECF7"));
            for (int k = 1; k < pts.Length; k++) dc.DrawLine(new Pen(Ui.Blue, 1.8), pts[k - 1], pts[k]);
        }
        BridgeDesignDrawing.Text(dc, "M · γG G + γQ Q · intero impalcato [kNm]", 12, 8, 12, Ui.Navy, true);
        BridgeDesignDrawing.Text(dc, $"M+ {BridgeDesignWorkspace.F(r.Stations.Max(s => s.Moment), "N0")}   /   M− {BridgeDesignWorkspace.F(r.Stations.Min(s => s.Moment), "N0")}", 12, 155, 12, Ui.Muted);
    }
}
