using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteSectionViewport
{
    internal bool Dimensions { get; set; }
    internal bool CoverDimensions { get; set; }
    internal bool SpacingDimensions { get; set; }
    internal readonly List<string> DimensionLabels = [];
    private void DrawDimensions(DrawingContext dc, SezioneCA s, Func<double, double, Point> p)
    {
        DimensionLabels.Clear();
        var brush = Ui.Brush("#66788A"); var pen = new Pen(brush, .8);
        void Dimension(Point a, Point b, string label, double offset)
        {
            var direction = b - a; if (direction.Length < .01) return; direction.Normalize();
            var normal = new Vector(-direction.Y, direction.X);
            Point q1 = a + normal * offset, q2 = b + normal * offset;
            dc.DrawLine(pen, a, q1 + normal * 4); dc.DrawLine(pen, b, q2 + normal * 4); dc.DrawLine(pen, q1, q2);
            foreach (Point q in new[] { q1, q2 }) dc.DrawLine(pen, q - (direction + normal) * 3, q + (direction + normal) * 3);
            double width = label.Length * 5.7 + 10; Point middle = q1 + (q2 - q1) / 2;
            // Rotate vertical labels along the dimension so they fit even in narrow previews.
            bool vertical = Math.Abs(direction.Y) > .8;
            if (vertical) { middle += normal * 12; dc.PushTransform(new RotateTransform(-90, middle.X, middle.Y)); }
            double x = middle.X - width / 2;
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(x - 2, middle.Y - 9, width, 18), 2, 2);
            Text(dc, label, x, middle.Y - 7, 10, brush, width);
            if (vertical) dc.Pop();
            DimensionLabels.Add(label);
        }
        double xmin = s.Outline.Min(v => v[0]), xmax = s.Outline.Max(v => v[0]), ymin = s.Outline.Min(v => v[1]), ymax = s.Outline.Max(v => v[1]);
        if (Dimensions)
        {
            if(s.Holes.Count>0)
            {
                if(s.Shape=="Circolare")Dimension(p(-s.Input.D("inner_diameter_mm")/2,0),p(s.Input.D("inner_diameter_mm")/2,0),$"Di {s.Input.D("inner_diameter_mm"):0.##} mm",-10);
                else {double bi=s.Input.D("inner_width_mm"),hi=s.Input.D("inner_height_mm");Dimension(p(-bi/2,-hi/2),p(bi/2,-hi/2),$"bi {bi:0.##} mm",-15);Dimension(p(bi/2,-hi/2),p(bi/2,hi/2),$"hi {hi:0.##} mm",-15);}
            }
            if (s.Shape == "Circolare") Dimension(p(xmin, 0), p(xmax, 0), $"D = {EngineeringFormat.Number(s.Width)} mm", (ymax - ymin) / 2 * (p(xmax, 0).X - p(xmin, 0).X) / s.Width + 26);
            else
            {
                double widthY = s.Shape == "A T" ? ymax : ymin;
                Dimension(p(xmin, widthY), p(xmax, widthY), $"{(s.Shape == "A T" ? "bf" : "b")} = {EngineeringFormat.Number(s.Width)} mm", s.Shape == "A T" ? -26 : 26);
                Dimension(p(xmax, ymin), p(xmax, ymax), $"h = {EngineeringFormat.Number(s.Height)} mm", 20);
                if (s.Shape == "A T")
                {
                    double bw = s.Input.D("web_width_mm"), hf = s.Input.D("flange_thickness_mm");
                    Dimension(p(-bw / 2, ymin), p(bw / 2, ymin), $"bw = {EngineeringFormat.Number(bw)} mm", 49);
                    Dimension(p(xmin, ymax), p(xmin, ymax - hf), $"hf = {EngineeringFormat.Number(hf)} mm", 24);
                }
            }
        }
        if (CoverDimensions)
        {
            double c = s.Input.D("cover_mm"), y = s.Shape == "Circolare" ? 0 : s.Shape == "A T" ? ymax - s.Input.D("flange_thickness_mm") / 2 : ymax - Math.Max(c * 2, (ymax - ymin) * .15);
            Dimension(p(xmax - c, y), p(xmax, y), $"c = {EngineeringFormat.Number(c)} mm", -22);
        }
        if (SpacingDimensions && s.Bars.Count > 1)
        {
            (Barra A, Barra B, double Gap)? nearest = null;
            for (int i = 0; i < s.Bars.Count; i++) for (int j = i + 1; j < s.Bars.Count; j++)
            {
                var a = s.Bars[i]; var b = s.Bars[j]; double gap = double.Hypot(a.X - b.X, a.Y - b.Y) - (a.Diametro + b.Diametro) / 2;
                if (nearest is null || gap < nearest.Value.Gap) nearest = (a, b, gap);
            }
            var n = nearest!.Value; double distance = double.Hypot(n.A.X - n.B.X, n.A.Y - n.B.Y);
            if (distance > 0)
            {
                double ux = (n.B.X - n.A.X) / distance, uy = (n.B.Y - n.A.Y) / distance;
                Dimension(p(n.A.X + ux * n.A.Diametro / 2, n.A.Y + uy * n.A.Diametro / 2), p(n.B.X - ux * n.B.Diametro / 2, n.B.Y - uy * n.B.Diametro / 2), $"i min = {EngineeringFormat.Number(n.Gap)} mm", 20);
            }
        }
    }
}
