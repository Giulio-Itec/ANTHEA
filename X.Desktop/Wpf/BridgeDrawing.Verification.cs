using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeDrawing
{
    internal bool ContourSection { get; set; }
    internal bool ContourDiagram { get; set; }
    internal bool ShowStressLimits { get; set; }
    internal double StressLimit(string material) => Stage?.Points.FirstOrDefault(p => p.Material == material && p.Active)?.Limit ?? 0;
    internal static Color UtilizationColor(double? ratio, bool concreteTension = false)
    {
        if (concreteTension) return Color.FromRgb(151, 85, 174);
        if (ratio is null) return Color.FromRgb(190, 200, 209);
        double u = Math.Max(0, ratio.Value);
        Color a, b; double t;
        if (u <= .7) { a = Color.FromRgb(211, 238, 231); b = Color.FromRgb(44, 156, 127); t = u / .7; }
        else if (u <= 1) { a = Color.FromRgb(44, 156, 127); b = Color.FromRgb(244, 182, 62); t = (u - .7) / .3; }
        else { a = Color.FromRgb(222, 68, 65); b = Color.FromRgb(125, 30, 51); t = Math.Min(1, (u - 1) / .5); }
        return Color.FromRgb((byte)(a.R + t * (b.R - a.R)), (byte)(a.G + t * (b.G - a.G)), (byte)(a.B + t * (b.B - a.B)));
    }
    private Color StressColor(string material, double stress)
    {
        double limit = StressLimit(material);
        return UtilizationColor(limit > 0 ? Math.Abs(stress) / limit : null, material == "CLS" && limit > 0 && stress > 1e-6);
    }
    private Brush ContourBrush(string material, double topStress, double bottomStress)
    {
        var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1) };
        // Include all band boundaries so overload turns red exactly at eta > 1.
        var samples = Enumerable.Range(0, 33).Select(i => i / 32d).ToList();
        double limit = StressLimit(material), delta = bottomStress - topStress;
        if (Math.Abs(delta) > 1e-12)
            foreach (double u in new[] { -1.5001, -1.000001, -1, -.7, 0, .7, 1, 1.000001, 1.5001 })
            { double t = (u * limit - topStress) / delta; if (t > 0 && t < 1) samples.Add(t); }
        foreach (double t in samples.Distinct().OrderBy(x => x)) brush.GradientStops.Add(new(StressColor(material, topStress + delta * t), t));
        brush.Freeze(); return brush;
    }
    private Brush SectionFill(string material, double bottom, double height, Brush fallback)
    {
        if (!ContourSection || Stage is not { } stage) return fallback;
        if (stage.GetHistory() is { } history)
        {
            var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1) };
            for (int i = 0; i <= 128; i++) brush.GradientStops.Add(new(StressColor(material, history.Profile.Stress(material, bottom + height * (1 - i / 128d))), i / 128d));
            brush.Freeze(); return brush;
        }
        return ContourBrush(material, stage.Contributions.Sum(c => c.Stress(material, bottom + height)), stage.Contributions.Sum(c => c.Stress(material, bottom)));
    }
    private void DrawConnectors(DrawingContext dc, BridgeGeometry g, Func<double, double, Point> point, double scale)
    {
        if (Input is not { } data) return;
        if (data.B("pioli"))
        {
            int number = Math.Clamp((int)data.D("n_pioli", 2), 1, 20);
            double spacing = Math.Clamp(data.D("passo_trasv_pioli", 150), 0, g.TopWidth);
            double height = Math.Clamp(data.D("h_pioli", 150), 0, g.SlabHeight * 1.2);
            var pen = new Pen(Ui.Brush("#526176"), Math.Clamp(data.D("d_pioli", 22) * scale, 1.2, 8));
            for (int i = 0; i < number; i++)
            {
                double x = g.Width / 2 + (i - (number - 1) / 2d) * spacing;
                dc.DrawLine(pen, point(x, 0), point(x, height));
                double head = Math.Clamp(data.D("d_testa_pioli", 35) / 2, 1, 60);
                dc.DrawLine(new Pen(pen.Brush, Math.Clamp(data.D("t_testa_pioli", 12) * scale, 1.5, 5)), point(x - head, height), point(x + head, height));
            }
        }
        string suffix = DetailAtSupport ? "app" : "irr";
        if (data.B(DetailAtSupport ? "appoggio" : "irrigidimenti"))
        {
            (double LeftWidth, double LeftThickness, double RightWidth, double RightThickness) plates;
            try { plates = BridgeSection.StiffenerPlates(data, suffix); } catch (ArgumentException) { return; }
            var pen = new Pen(Ui.Brush("#788C9C"), 1) { DashStyle = DashStyles.Dot };
            foreach (int side in new[] { -1, 1 })
            {
                double b = side < 0 ? plates.LeftWidth : plates.RightWidth;
                if (b <= 0) continue;
                b = Math.Min(b, g.TopWidth);
                double x1 = g.Width / 2 + side * g.WebThickness / 2, x2 = x1 + side * b;
                var a = point(Math.Min(x1, x2), -g.TopThickness);
                var z = point(Math.Max(x1, x2), -g.TopThickness - g.WebHeight);
                dc.DrawRectangle(Ui.Brush("#18788C9C"), pen, new Rect(a, z));
            }
        }
    }
}
