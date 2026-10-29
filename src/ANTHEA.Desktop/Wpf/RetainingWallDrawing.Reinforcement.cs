using Anthea.Application.Walls;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallDrawing
{
    private void DrawReinforcementSection(DrawingContext dc)
    {
        if (Data is null) return;
        if (Data.S("family") != "cantilever") { Text(dc, "Muro a gravità: nessuna armatura strutturale.", 20, 20); return; }
        WallModel.ReinforcementDetails details;
        try { details = Calculation?.Detailing ?? WallModel.CalculateReinforcementDetails(Data); }
        catch (ArgumentException ex) { WrappedText(dc, ex.Message, 20, 20, ActualWidth - 40); return; }
        var g = Data["geometry"]!; double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), back = a + g.D("stem_base"), b = back + g.D("heel");
        double scale = Math.Min((ActualWidth * .56 - 50) / Math.Max(b, 1), (ActualHeight - 100) / (h + t));
        Point P(double x, double y) => new(35 + x * scale, ActualHeight - 48 - y * scale);
        Polygon(dc, WallModel.Outline(Data).Select(p => P(p[0], p[1])), Ui.Brush("#F1F4F7"), new Pen(Ui.Muted, 1.2));
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
        void Legend(string mark, string title, string detail, Brush color)
        {
            double lx = ActualWidth * .60, ly = 39 + index++ * 30;
            Text(dc, $"{mark}  {title}", lx, ly, color, 12);
            Text(dc, detail, lx, ly + 14, Ui.Muted, 10.5);
        }
        foreach (var bar in details.Bars)
        {
            Brush color = bar.Zone switch { "stem" => Ui.Brush("#B84539"), "stem_upper" => Ui.Brush("#C67A10"), "toe" => Ui.Brush("#1B6A9A"), _ => Ui.Brush("#298361") };
            var pen = new Pen(color, 2.2);
            // Upper bars are projected with a small screen offset: their out-of-plane lap pairing is explicit; the end bends of the top
            // foundation bars are drawn just inside those of the bottom bars, which lie in other planes along the wall.
            Point Project(WallModel.BarPoint point)
            {
                var p = P(point.X, point.Y);
                return bar.Zone == "stem_upper" ? new(p.X + (bar.Face == "monte" ? -4 : 4), p.Y) : bar.Face == "superiore" ? new(p.X + (point.X < a + g.D("stem_base") / 2 ? 2.5 : -2.5), p.Y) : p;
            }
            var points = bar.Path.Select(Project).ToArray();
            for (int i = 1; i < points.Length; i++) dc.DrawLine(pen, points[i - 1], points[i]);
            var min = new Point(points.Min(p => p.X) - 4, points.Min(p => p.Y) - 4); var max = new Point(points.Max(p => p.X) + 4, points.Max(p => p.Y) + 4);
            string zone = bar.Continuous ? "Fondazione" : WallModel.RebarZoneName(bar.Zone), foot = bar.Foot.Length > 0 ? $"; piede verso {bar.Foot}" : "";
            string info = $"pos. {bar.Mark} · {zone} {bar.Face}: {bar.Count} Ø{bar.Diameter}/m (passo {bar.Spacing:0.#} mm); As={bar.Area:0} mm²/m\nL={bar.Length:0.###} m; lbd richiesto/usato={bar.RequiredAnchor:0}/{bar.Anchor:0} mm; l0={bar.Lap:0} mm; mandrino={bar.Mandrel:0} mm; fbd={bar.Fbd:0.##} MPa{foot}";
            hits.Add((new Rect(min, max), info, null));
            Text(dc, bar.Mark, points[0].X + (bar.Face is "monte" or "superiore" ? 5 : -14), points[0].Y - 13, color, 10);
            Legend(bar.Mark, $"{zone} · {bar.Face}   {bar.Count} Ø{bar.Diameter}/m", $"lbd {bar.Anchor:0} / l0 {bar.Lap:0} · L {bar.Length:0.00} m{foot}", color);
        }
        foreach (var other in details.Others)
        {
            switch (other.Kind)
            {
                case "Forcella":
                {
                    var color = Ui.Brush("#7B3FA0"); var points = other.Path.Select(p => P(p.X, p.Y)).ToArray();
                    for (int i = 1; i < points.Length; i++) dc.DrawLine(new Pen(color, 1.6), points[i - 1], points[i]);
                    hits.Add((new Rect(new Point(points.Min(p => p.X) - 4, points.Min(p => p.Y) - 4), new Point(points.Max(p => p.X) + 4, points.Max(p => p.Y) + 4)),
                        $"pos. {other.Mark} · forcella di testa Ø{other.Diameter}/{other.Spacing:0.#} mm; L={other.Length:0.###} m", null));
                    Legend(other.Mark, $"Forcella di testa   Ø{other.Diameter}/{other.Spacing:0.#} mm", $"L {other.Length:0.00} m", color);
                    break;
                }
                case "Spillo":
                {
                    var color = Ui.Brush("#997139"); double y0 = other.Path.Average(p => p.Y);
                    foreach (var row in other.Points)
                    {
                        var points = other.Path.Select(p => P(p.X, p.Y - y0 + row.Y)).ToArray();
                        for (int i = 1; i < points.Length; i++) dc.DrawLine(new Pen(color, 1), points[i - 1], points[i]);
                    }
                    Legend(other.Mark, $"Spilli della giunzione   Ø{other.Diameter}, {other.Points.Count} file", $"L {other.Length:0.00} m", color);
                    break;
                }
                default:
                    // Longitudinal bars in their true position, inside the main bars.
                    foreach (var p in other.Points) dc.DrawEllipse(Brushes.White, new Pen(Ui.Muted, 1), P(p.X, p.Y), 2.2, 2.2);
                    break;
            }
        }
        var longitudinal = details.Others.Where(o => o.Kind == "Longitudinale").ToArray();
        if (longitudinal.Length > 0)
            Legend(longitudinal[0].Mark + "–" + longitudinal[^1].Mark, "Ferri longitudinali (cerchi)", string.Join(" · ", longitudinal.Select(o => $"{o.Points.Count} Ø{o.Diameter}")), Ui.Muted);
        Text(dc, $"B={b:0.##} m · H={h:0.##} m · c={Data["materials"].D("cover"):0} mm · acciaio {details.SteelKg:0.0} kg/m", 15, ActualHeight - 33, size: 11);
        Text(dc, "Numeri = posizioni della tavola · selezionare Distinta ferri per sagome e quantità del tratto", 15, ActualHeight - 17, Ui.Muted, 10);
    }
}
