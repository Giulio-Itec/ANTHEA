using System.Globalization;
using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeDrawing
{
    private static readonly Brush Compression = Ui.Brush("#226293"), Tension = Ui.Brush("#B44D43");
    internal readonly List<(string Text, Rect Bounds)> StressLabels = [];

    private void DrawStressDiagram(DrawingContext dc, BridgeGeometry g, BridgeStage stage, double divider, double w, double h, Func<double, double> Y)
    {
        StressLabels.Clear();
        double left = divider + 16, right = w - 18, origin = (left + right) / 2;
        double amp = double.IsFinite(ConcreteAmplification) && ConcreteAmplification > 0 ? ConcreteAmplification : 1;
        bool composite = stage.Contributions.Any(c => c.HasConcrete);
        double SteelSigma(double y) => stage.Contributions.Sum(c => c.SteelStress(y));
        double ConcreteSigma(double y) => stage.Contributions.Sum(c => c.Stress("CLS", y));
        double max = Math.Max(1, stage.Points.Where(p => p.Active).Select(p => Math.Abs(p.Stress) * (p.Material == "CLS" ? amp : 1)).DefaultIfEmpty(1).Max());
        if (Mode == 1)
            max = Math.Max(max, stage.Points.Where(p => p.Active).SelectMany(p => p.Contributions.Select(v => Math.Abs(v) * (p.Material == "CLS" ? amp : 1))).DefaultIfEmpty(1).Max());
        if (ShowStressLimits)
            max = Math.Max(max, Math.Max(Math.Max(StressLimit("Acciaio"), StressLimit("Armatura")), composite ? StressLimit("CLS") * amp : 0) * 1.05);
        double scale = Math.Max(5, (right - left) / 2 - 24) / max;
        FormattedText Format(string text, Brush color, double size = 11) => new(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, color, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        void Text(string text, double x, double y, Brush color, double size = 11)
        {
            var t = Format(text, color, size); t.MaxTextWidth = Math.Max(1, right - x); dc.DrawText(t, new(x, y));
        }
        string F(double v) => v.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
        dc.DrawLine(new Pen(Ui.Brush("#DCE3EB"), 1), new(divider, 8), new(divider, h - 12));
        Text(Mode == 1 ? "TENSIONI · somma e contributi" : "TENSIONI NORMALI", left, 7, Ui.Navy, 11);
        Text("− compressione", left, 29, Compression, 10);
        Text("trazione +", Math.Max(origin + 13, right - 65), 29, Tension, 10);
        Text("0", origin - 3, 50, Ui.Muted, 10);
        double yTop = Y(g.SlabHeight), yInterface = Y(0), yBottom = Y(-g.Height);
        foreach (double y in new[] { yTop, yInterface, yBottom })
            dc.DrawLine(new Pen(Ui.Brush("#DCE3EB"), .7) { DashStyle = DashStyles.Dot }, new(left, y), new(right, y));
        dc.DrawLine(new Pen(Ui.Brush("#8191A4"), 1), new(origin, Math.Min(65, yTop - 8)), new(origin, Math.Max(yBottom + 12, 70)));

        // Split each linear stress block at zero so opposite signs never share a fill.
        void Region(double ya, double yb, double a, double b, double factor, string material)
        {
            if (Math.Abs(a) + Math.Abs(b) < 1e-12) return;
            if (a * b < 0)
            {
                double zero = ya + (yb - ya) * (-a / (b - a));
                Region(ya, zero, a, 0, factor, material); Region(zero, yb, 0, b, factor, material); return;
            }
            Brush color = a + b < 0 ? Compression : Tension;
            var A = new Point(origin + a * factor * scale, Y(ya)); var B = new Point(origin + b * factor * scale, Y(yb));
            var area = new StreamGeometry();
            using (var pen = area.Open()) { pen.BeginFigure(new(origin, A.Y), true, true); pen.LineTo(A, true, false); pen.LineTo(B, true, false); pen.LineTo(new(origin, B.Y), true, false); }
            Brush fill = ContourDiagram ? ContourBrush(material, ya > yb ? a : b, ya > yb ? b : a) : color;
            dc.PushOpacity(ContourDiagram ? .75 : .16); dc.DrawGeometry(fill, null, area); dc.Pop();
            dc.PushClip(area);
            for (double y = Math.Min(A.Y, B.Y) + 9; y < Math.Max(A.Y, B.Y); y += 12)
            {
                double t = (y - A.Y) / (B.Y - A.Y), edge = A.X + t * (B.X - A.X);
                dc.PushOpacity(.5); dc.DrawLine(new Pen(color, .7), new(origin, y), new(edge, y)); dc.Pop();
            }
            dc.Pop();
            dc.DrawLine(new Pen(ContourDiagram ? fill : color, 2.5), A, B);
            dc.DrawLine(new Pen(color, 1), new(origin, A.Y), A); dc.DrawLine(new Pen(color, 1), new(origin, B.Y), B);
        }
        Region(0, -g.Height, SteelSigma(0), SteelSigma(-g.Height), 1, "Acciaio");
        if (composite) Region(g.SlabHeight, 0, ConcreteSigma(g.SlabHeight), ConcreteSigma(0), amp, "CLS");

        if (ShowStressLimits)
        {
            void Limit(string material, double ya, double yb, double factor, bool compressionOnly)
            {
                double value = StressLimit(material); if (value <= 0) return;
                foreach (int sign in compressionOnly ? new[] { -1 } : new[] { -1, 1 })
                {
                    double x = origin + sign * value * factor * scale;
                    dc.DrawLine(new Pen(Ui.Brush("#B34D43"), 1) { DashStyle = DashStyles.Dash }, new(x, Y(ya)), new(x, Y(yb)));
                    Text((sign < 0 ? "−" : "+") + F(value), Math.Clamp(x - 14, left, right - 45), Y(yb) + 22, Ui.Brush("#B34D43"), 9);
                }
            }
            Limit("Acciaio", 0, -g.Height, 1, false);
            if (composite) Limit("CLS", g.SlabHeight, 0, amp, true);
            double barLimit = StressLimit("Armatura");
            if (barLimit > 0)
                foreach (var bar in stage.Points.Where(p => p.Material == "Armatura" && p.Active))
                    foreach (int sign in new[] { -1, 1 })
                    {
                        double x = origin + sign * barLimit * scale, y = Y(bar.Y);
                        dc.DrawLine(new Pen(Bars, 1.4), new(x, y - 4), new(x, y + 4));
                        dc.DrawLine(new Pen(Bars, 1.4), new(x - 3, y), new(x + 3, y));
                    }
        }

        void PhaseCurve(Func<double, double> sigma, double ya, double yb, double factor, Brush color)
        {
            dc.DrawLine(new Pen(color, 1.2) { DashStyle = DashStyles.Dash },
                new(origin + sigma(ya) * factor * scale, Y(ya)), new(origin + sigma(yb) * factor * scale, Y(yb)));
        }
        if (Mode == 1)
            for (int i = 0; i < stage.Contributions.Count; i++)
            {
                var c = stage.Contributions[i]; var color = Colors[i % Colors.Length];
                PhaseCurve(c.SteelStress, 0, -g.Height, 1, color);
                if (c.HasConcrete) PhaseCurve(y => c.Stress("CLS", y), g.SlabHeight, 0, amp, color);
            }

        // Values are always physical MPa, including the amplified concrete diagram.
        void Label(string name, double value, double y, double factor, bool above, int concreteFace = -1)
        {
            Brush color = value < 0 ? Compression : value > 0 ? Tension : Ui.Muted;
            var point = new Point(origin + value * factor * scale, Y(y));
            string text = name + " " + F(value);
            var formatted = Format(text, color, 10);
            double width = formatted.Width + 8, height = formatted.Height + 4;
            var box = new Rect(Math.Clamp(point.X - (value < 0 ? width : 0), left, Math.Max(left, right - width)), point.Y + (above ? -height - 3 : 3), width, height);
            // Separate callouts remain legible even when a shallow slab occupies only a few pixels.
            if (concreteFace >= 0) box = new Rect(concreteFace == 0 ? left : right - width, yTop - height - 6, width, height);
            int tries = 0;
            while (StressLabels.Any(label => label.Bounds.IntersectsWith(box)) && tries++ < 12) box.Y += above ? -height - 2 : height + 2;
            dc.DrawLine(new Pen(color, .7), point, new(Math.Clamp(point.X, box.Left, box.Right), above ? box.Bottom : box.Top));
            dc.DrawRoundedRectangle(Brushes.White, null, box, 2, 2);
            dc.DrawText(formatted, new(box.X + 4, box.Y + 2)); StressLabels.Add((text, box));
            dc.DrawEllipse(color, new Pen(Brushes.White, .7), point, 2.6, 2.6);
        }
        // The concrete labels sit above their faces; steel starts below the interface.
        if (composite)
        {
            Label("σc,sup", ConcreteSigma(g.SlabHeight), g.SlabHeight, amp, true, 0);
            Label("σc,inf", ConcreteSigma(0), 0, amp, true, 1);
        }
        else Text("CLS non attivo", left, Math.Max(63, yTop - 20), Ui.Muted, 10);
        Label("σa", SteelSigma(0), 0, 1, false);
        Label("σa", SteelSigma(-g.Height), -g.Height, 1, false);
        foreach (var row in stage.Points.Where(p => p.Material == "Armatura" && p.Active))
        {
            var p = new Point(origin + row.Stress * scale, Y(row.Y));
            dc.DrawLine(new Pen(Bars, 1.3), new(p.X - 3, p.Y - 3), new(p.X + 3, p.Y + 3));
            dc.DrawLine(new Pen(Bars, 1.3), new(p.X - 3, p.Y + 3), new(p.X + 3, p.Y - 3));
        }
        double footer = h - (Mode == 1 ? 76 : 59);
        Text($"CLS ×{F(amp)} · valori reali in MPa", left, footer, Ui.Navy, 10);
        Text($"Scala acciaio ±{F(max)} · CLS ±{F(max / amp)} MPa", left, footer + 16, Ui.Muted, 10);
        if (Mode == 1)
        {
            int visible = Math.Min(stage.Contributions.Count, Math.Max(1, (int)((right - left) / 46)));
            for (int i = 0; i < visible; i++) Text($"- Δσ{i + 1}", left + i * 46, footer + 32, Colors[i % Colors.Length], 10);
        }
        Text(ContourDiagram ? "Campitura: η della somma · × armature" : "Campitura: somma · × armature", left, h - 22, Ui.Muted, 10);
    }
}
