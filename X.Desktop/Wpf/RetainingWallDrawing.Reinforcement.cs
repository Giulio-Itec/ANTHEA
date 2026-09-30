using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallDrawing
{
    private void DrawReinforcementSection(DrawingContext dc)
    {
        if (Data is null) return;
        if (Data.S("family") != "cantilever") { Text(dc, "Muro a gravità: nessuna armatura strutturale.", 20, 20); return; }
        RetainingWall.ReinforcementDetails details;
        try { details = Calculation?.Detailing ?? RetainingWall.CalculateReinforcementDetails(Data); }
        catch (ArgumentException ex) { WrappedText(dc, ex.Message, 20, 20, ActualWidth - 40); return; }
        var g = Data["geometry"]!; double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), back = a + g.D("stem_base"), b = back + g.D("heel");
        double scale = Math.Min((ActualWidth * .56 - 50) / Math.Max(b, 1), (ActualHeight - 100) / (h + t));
        Point P(double x, double y) => new(35 + x * scale, ActualHeight - 48 - y * scale);
        Polygon(dc, RetainingWall.Outline(Data).Select(p => P(p[0], p[1])), Ui.Brush("#F1F4F7"), new Pen(Ui.Muted, 1.2));
        Text(dc, "SEZIONE ARMATURE · misure in mm", 15, 10, size: 12);
        bool two = Data["reinforcement"].B("two_zones"); double split = Data["reinforcement"].D("lower_height");
        if (two)
        {
            double lap = details.Bars.Max(x => x.Lap) / 1000;
            var box = new Rect(P(a, t + split + lap / 2), P(back, t + split - lap / 2));
            dc.DrawRectangle(Ui.Brush("#FFEAC5"), null, box); hits.Add((box, $"Giunzione a h₁={split:0.###} m; l0 massimo={lap * 1000:0} mm. Barre giuntate affiancate lungo lo sviluppo del muro; proiezioni distanziate nel disegno per leggibilità.", null));
            Text(dc, $"h₁={split:0.##} m", 4, P(0, t + split).Y, size: 10);
        }
        int index = 0;
        foreach (var bar in details.Bars)
        {
            Brush color = bar.Zone switch { "stem" => Ui.Brush("#B84539"), "stem_upper" => Ui.Brush("#C67A10"), "toe" => Ui.Brush("#1B6A9A"), _ => Ui.Brush("#298361") };
            var pen = new Pen(color, 2.2);
            // Upper bars are projected with a small screen offset: their out-of-plane lap pairing is explicit.
            Point Project(RetainingWall.BarPoint point) { var p = P(point.X, point.Y); return bar.Zone == "stem_upper" ? new(p.X + (bar.Face == "monte" ? -4 : 4), p.Y) : p; }
            var points = bar.Path.Select(Project).ToArray();
            for (int i = 1; i < points.Length; i++) dc.DrawLine(pen, points[i - 1], points[i]);
            var min = new Point(points.Min(p => p.X) - 4, points.Min(p => p.Y) - 4); var max = new Point(points.Max(p => p.X) + 4, points.Max(p => p.Y) + 4);
            string info = $"{bar.Mark} · {RetainingWall.RebarZoneName(bar.Zone)} {bar.Face}: {bar.Count} Ø{bar.Diameter}/m; As={bar.Area:0} mm²/m\nL={bar.Length:0.###} m; lbd richiesto/usato={bar.RequiredAnchor:0}/{bar.Anchor:0} mm; l0={bar.Lap:0} mm; mandrino={bar.Mandrel:0} mm; fbd={bar.Fbd:0.##} MPa";
            hits.Add((new Rect(min, max), info, null));
            Text(dc, bar.Mark, points[0].X + (bar.Face is "monte" or "superiore" ? 5 : -20), points[0].Y - 13, color, 10);
            double lx = ActualWidth * .60, ly = 39 + index++ * 34;
            Text(dc, $"{bar.Mark}  {RetainingWall.RebarZoneName(bar.Zone)} · {bar.Face}   {bar.Count} Ø{bar.Diameter}/m", lx, ly, color, 12);
            Text(dc, $"lbd {bar.Anchor:0} / l0 {bar.Lap:0} · L {bar.Length:0.00} m", lx, ly + 15, Ui.Muted, 11);
        }
        // Orthogonal bars are represented by their circular sections on both faces.
        foreach (string key in two ? new[] { "stem", "stem_upper" } : new[] { "stem" })
        {
            var r = Data["reinforcement"]![key]!; double step = r.D("secondary_spacing") / 1000, cover = (Data["materials"].D("cover") + r.D("diameter") + r.D("secondary_diameter") / 2) / 1000;
            if (step <= 0) continue;
            double low = key == "stem_upper" ? split : 0, high = key == "stem" && two ? split : h;
            for (double y = low + cover; y < high - cover; y += step)
            {
                foreach (double x in new[] { back - cover, a + (g.D("stem_base") - g.D("stem_top")) * y / h + cover })
                    dc.DrawEllipse(Brushes.White, new Pen(Ui.Muted, 1), P(x, t + y), 2, 2);
            }
        }
        if (two)
        {
            double lap = details.Bars.Max(x => x.Lap) / 1000, step = Data["detailing"].D("tie_spacing") / 1000;
            if (step > 0) for (double y = t + split - lap / 2 + step / 2; y < t + split + lap / 2; y += step)
                dc.DrawLine(new Pen(Ui.Brush("#997139"), 1), P(a + (g.D("stem_base") - g.D("stem_top")) * (y - t) / h + .06, y), P(back - .06, y));
        }
        foreach (string key in new[] { "toe", "heel" })
        {
            var arm = Data["reinforcement"]![key]!;
            double step = arm.D("secondary_spacing") / 1000;
            double c = (Data["materials"].D("cover") + arm.D("diameter") + arm.D("secondary_diameter") / 2) / 1000;
            double start = key == "toe" ? c : a + g.D("stem_base") / 2, end = key == "toe" ? a + g.D("stem_base") / 2 : b - c;
            if (step <= 0) continue;
            for (double x = start; x <= end; x += step)
                foreach (double y in new[] { c, t - c }) dc.DrawEllipse(Brushes.White, new Pen(Ui.Muted, 1), P(x, y), 2, 2);
        }
        Text(dc, $"B={b:0.##} m · H={h:0.##} m · c={Data["materials"].D("cover"):0} mm · acciaio stimato {details.SteelKg:0.0} kg/m", 15, ActualHeight - 33, size: 11);
        Text(dc, "Schema di predimensionamento · selezionare Dettagli armature per controlli e distinta", 15, ActualHeight - 17, Ui.Muted, 10);
    }
}
