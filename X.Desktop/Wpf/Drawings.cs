using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X.Core;

namespace X.Desktop;

internal abstract class DrawingView : FrameworkElement
{
    protected DrawingView() { ClipToBounds = true; SnapsToDevicePixels = true; }
    protected override void OnRender(DrawingContext dc) => Render(dc, new Size(ActualWidth, ActualHeight));
    protected abstract void Render(DrawingContext dc, Size size);
    protected static void Text(DrawingContext dc, string value, double x, double y, double size = 12, Brush? brush = null, double width = double.PositiveInfinity, bool bold = false)
    {
        if (width <= 0) return;
        var text = new FormattedText(value, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, brush ?? Ui.Muted, 1);
        if (double.IsFinite(width)) text.MaxTextWidth = width;
        dc.DrawText(text, new Point(x, y));
    }
    protected static StreamGeometry Path(IEnumerable<Point> points, bool closed)
    {
        var p = points.ToArray(); var geometry = new StreamGeometry();
        using (var ctx = geometry.Open()) if (p.Length > 0) { ctx.BeginFigure(p[0], closed, closed); ctx.PolyLineTo(p.Skip(1).ToArray(), true, false); }
        geometry.Freeze(); return geometry;
    }
    internal byte[] Png(int width = 1200, int height = 750)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) Render(dc, new Size(width, height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
}

internal sealed record Serie(string Name, List<double[]> Points, Brush Color, bool Dashed = false, bool Highlighted = false, DashStyle? DashPattern = null);
internal sealed record PlotMarker(double X, double Y, string Label, Brush Color, bool ProjectToAxes = false, string? XCaption = null, string? YCaption = null, double Radius = 5);

internal sealed class Plot : DrawingView
{
    internal bool VerticalLegend { get; set; }
    internal double FitPadding { get; set; }
    internal bool NegateAxisLabels { get; set; }
    internal List<Serie> Series { get; set; } = [];
    internal (double[] A, double[] B)[] Segments { get; set; } = [];
    internal List<PlotMarker> Markers { get; set; } = [];
    internal List<(double[] A, double[] B)> VerificationSegments { get; set; } = [];
    internal bool CenteredAxes { get; set; }
    internal bool FitIncludesMarkers { get; set; } = true;
    internal double ScaleX { get; set; } = 1;
    internal double ScaleY { get; set; } = 1;
    internal double Zoom { get => zoom; set { zoom = Math.Clamp(value, .1, 20); InvalidateVisual(); } }
    internal event Action? ViewReset;
    internal (double X, double Y) AxisHalfRange { get; private set; }
    internal static double NiceHalfRange(double extent)
    {
        if (!double.IsFinite(extent) || extent <= 0) return 10;
        double desiredStep = extent / 2, power = Math.Pow(10, Math.Floor(Math.Log10(desiredStep)));
        double step = new[] { 1d, 2d, 5d, 10d }.First(v => desiredStep <= v * power) * power;
        return step * 2;
    }
    internal string Title { get; set; } = "Premere Calcola";
    internal string XLabel { get; set; } = "Forza assiale [kN]";
    internal string YLabel { get; set; } = "Profondità z [m]";
    internal string? AxisNumberFormat { get; set; }
    internal string Note { get; set; } = "";
    internal bool InvertY { get; set; } = true;
    internal bool Capacity { get; set; }
    internal double CapacityDepth { get; set; }
    internal double? XMinimum { get; set; }
    internal string EmptyMessage { get; set; } = "Premere Calcola";
    private double zoom = 1;
    private Vector offset;
    private Point? drag;
    internal Plot()
    {
        MouseWheel += (_, e) => { Zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.2 : 1 / 1.2), CenteredAxes ? .1 : 1, 20); e.Handled = true; };
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) ResetView(); else { drag = e.GetPosition(this); CaptureMouse(); } };
        MouseMove += (_, e) => { if (!CenteredAxes && drag is Point p) { var q = e.GetPosition(this); offset += q - p; drag = q; InvalidateVisual(); } };
        MouseLeftButtonUp += (_, _) => { drag = null; ReleaseMouseCapture(); };
        ToolTip = "Rotella: zoom · trascina: sposta · doppio clic: adatta";
    }
    internal void ResetView() { zoom = 1; ScaleX = ScaleY = 1; offset = default; ViewReset?.Invoke(); InvalidateVisual(); }
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size));
        if (size.Width < 100 || size.Height < 100) return;
        Text(dc, Title, 12, 7, 14, Ui.Navy, size.Width - 24, true);
        var points = Series.SelectMany(s => s.Points).Concat(Segments.SelectMany(s => new[] { s.A, s.B })).Concat(FitIncludesMarkers ? Markers.Select(m => new[] { m.X, m.Y }) : []).Where(p => p.Length >= 2 && double.IsFinite(p[0]) && double.IsFinite(p[1])).ToArray();
        if (points.Length == 0) { Text(dc, EmptyMessage, 18, size.Height / 2, 13, width: size.Width - 36); return; }
        double xmin = XMinimum ?? (Capacity ? 0 : points.Min(p => p[0])), xmax = points.Max(p => p[0]);
        double ymin = Capacity ? 0 : points.Min(p => p[1]), ymax = Capacity && CapacityDepth > 0 ? CapacityDepth : points.Max(p => p[1]);
        if (Capacity && xmax > 0) { double power = Math.Pow(10, Math.Floor(Math.Log10(xmax))); xmax = new[] { 1d, 2d, 5d, 10d }.First(v => xmax / power <= v) * power; }
        if (xmax <= xmin) xmax = xmin + 1; if (ymax <= ymin) ymax = ymin + 1;
        if (!CenteredAxes && !Capacity && FitPadding > 0)
        {
            double dx = (xmax - xmin) * FitPadding, dy = (ymax - ymin) * FitPadding;
            if (xmin != 0) xmin -= dx; if (xmax != 0) xmax += dx;
            if (ymin != 0) ymin -= dy; if (ymax != 0) ymax += dy;
        }
        if (CenteredAxes)
        {
            xmax = NiceHalfRange(Math.Max(Math.Abs(xmin), Math.Abs(xmax)) * 1.05) / (zoom * Math.Clamp(ScaleX, .25, 4)); xmin = -xmax;
            ymax = NiceHalfRange(Math.Max(Math.Abs(ymin), Math.Abs(ymax)) * 1.05) / (zoom * Math.Clamp(ScaleY, .25, 4)); ymin = -ymax;
            AxisHalfRange = (xmax, ymax); offset = default;
        }
        int legendColumns = VerticalLegend ? 1 : Math.Max(1, (int)((size.Width - 24) / 170));
        double legendHeight = !Capacity && Series.Count > 1 ? Math.Ceiling(Series.Count / (double)legendColumns) * 18 : 0;
        var area = new Rect(58, 47, Math.Max(20, size.Width - 77), Math.Max(20, size.Height - (Note == "" ? 105 : 130) - legendHeight));
        double viewZoom = CenteredAxes ? 1 : zoom;
        Point P(double x, double y) => new(area.Left + (x - xmin) / (xmax - xmin) * area.Width * viewZoom + offset.X,
            area.Top + (InvertY ? (y - ymin) / (ymax - ymin) : (ymax - y) / (ymax - ymin)) * area.Height * viewZoom + offset.Y);
        string F(double v) { if (NegateAxisLabels && v != 0) v = -v; if(AxisNumberFormat is not null)return v.ToString(AxisNumberFormat);return Math.Abs(v) >= 10000 ? (v / 1000).ToString("0.#") + "k" : v.ToString("0.##"); }
        for (int i = 0; i <= 4; i++)
        {
            double t = i / 4d, x = area.Left + t * area.Width, y = area.Top + t * area.Height;
            var pen = new Pen(Ui.Brush("#E8EDF3"), 1);
            dc.DrawLine(pen, new Point(x, area.Top), new Point(x, area.Bottom)); dc.DrawLine(pen, new Point(area.Left, y), new Point(area.Right, y));
            Text(dc, F(xmin + (t - offset.X / area.Width) / viewZoom * (xmax - xmin)), x - 15, area.Bottom + 5, 10);
            double ordinate = (t - offset.Y / area.Height) / viewZoom;
            Text(dc, F(InvertY ? ymin + ordinate * (ymax - ymin) : ymax - ordinate * (ymax - ymin)), 1, y - 7, 10, width: 53);
        }
        dc.DrawRectangle(null, new Pen(Ui.Navy, 1), area);
        Text(dc, YLabel, 10, 28, 10, width: size.Width - 20); Text(dc, XLabel, area.Left + 15, area.Bottom + 23, 11, width: area.Width - 15);
        dc.PushClip(new RectangleGeometry(area));
        if (CenteredAxes)
        {
            var axisPen = new Pen(Ui.Brush("#8796A8"), 1);
            dc.DrawLine(axisPen, P(xmin, 0), P(xmax, 0)); dc.DrawLine(axisPen, P(0, ymin), P(0, ymax));
            var origin = P(0, 0); dc.DrawEllipse(Brushes.White, new Pen(Ui.Navy, 1), origin, 3, 3); Text(dc, "0,0", origin.X + 5, origin.Y + 4, 10);
        }
        foreach (var segment in VerificationSegments) dc.DrawLine(new Pen(Ui.Brush("#9664B5"), 1.4) { DashStyle = DashStyles.Dash }, P(segment.A[0], segment.A[1]), P(segment.B[0], segment.B[1]));
        foreach (var segment in Segments) dc.DrawLine(new Pen(Ui.Blue, 1.8), P(segment.A[0], segment.A[1]), P(segment.B[0], segment.B[1]));
        foreach (var s in Series)
        {
            var valid = s.Points.Where(p => p.Length >= 2 && double.IsFinite(p[0]) && double.IsFinite(p[1])).Select(p => P(p[0], p[1])).ToArray();
            var pen = new Pen(s.Color, s.Highlighted ? 3 : 1.7) { DashStyle = s.DashPattern ?? (s.Dashed ? DashStyles.Dash : DashStyles.Solid) };
            if (valid.Length > 1) dc.DrawGeometry(null, pen, Path(valid, false));
        }
        foreach (var m in Markers)
        {
            var p = P(m.X, m.Y);
            if (m.ProjectToAxes && area.Contains(p))
            {
                var guide = new Pen(m.Color, .8);
                dc.DrawLine(guide, p, new Point(area.Left, p.Y)); dc.DrawLine(guide, p, new Point(p.X, area.Bottom));
            }
            dc.DrawEllipse(m.Color, new Pen(Brushes.White, 1), p, m.Radius, m.Radius);
            if (!m.ProjectToAxes) Text(dc, m.Label, p.X + 7, p.Y - 16, 10, m.Color);
        }
        dc.Pop();
        foreach (var m in Markers.Where(m => m.ProjectToAxes))
        {
            var p = P(m.X, m.Y); if (!area.Contains(p)) continue;
            double labelX = Math.Clamp(p.X - 28, area.Left, Math.Max(area.Left, area.Right - 66));
            dc.DrawRectangle(Brushes.White, null, new Rect(labelX, area.Bottom + 2, 66, 18));
            Text(dc, m.XCaption ?? $"φ={m.X:0.#}°", labelX, area.Bottom + 3, 11, m.Color, 66, true);
            dc.DrawRectangle(Brushes.White, null, new Rect(0, p.Y - 15, area.Left - 2, 31));
            Text(dc, m.YCaption ?? $"Nq\n{m.Y:0.#}", 1, p.Y - 15, 10, m.Color, area.Left - 3, true);
        }
        if (legendHeight > 0)
        {
            for (int i = 0; i < Series.Count; i++)
            {
                double x = 12 + i % legendColumns * (size.Width - 24) / legendColumns, y = area.Bottom + 47 + i / legendColumns * 18;
                var s = Series[i]; dc.DrawLine(new Pen(s.Color, s.Highlighted ? 3 : 2), new Point(x, y + 6), new Point(x + 16, y + 6));
                Text(dc, s.Name, x + 21, y, 10, width: (size.Width - 24) / legendColumns - 25);
            }
        }
        if (Note != "") Text(dc, Note, 8, size.Height - 33, 10, width: size.Width - 16);
    }
}

internal sealed class SectionDrawing : DrawingView
{
    internal List<double[]> Outline { get; set; } = [];
    internal List<Barra> Bars { get; set; } = [];
    internal double[]? Plane { get; set; }
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size)); if (Outline.Count < 3 || size.Width < 70 || size.Height < 85) return;
        double xmin = Outline.Min(p => p[0]), xmax = Outline.Max(p => p[0]), ymin = Outline.Min(p => p[1]), ymax = Outline.Max(p => p[1]);
        double scale = Math.Min((size.Width - 60) / (xmax - xmin), (size.Height - 75) / (ymax - ymin));
        Point P(double x, double y) => new(size.Width / 2 + (x - (xmin + xmax) / 2) * scale, (size.Height - 30) / 2 - (y - (ymin + ymax) / 2) * scale);
        var shape = Path(Outline.Select(p => P(p[0], p[1])), true); dc.DrawGeometry(Ui.Brush("#E5E7EB"), null, shape);
        if (Plane is { Length: 3 } q)
        {
            double Stress(double[] p) => q[0] + q[1] * p[1] - q[2] * p[0]; var clipped = new List<double[]>();
            for (int i = 0; i < Outline.Count; i++)
            {
                var a = Outline[i]; var b = Outline[(i + 1) % Outline.Count]; double sa = Stress(a), sb = Stress(b);
                if (sa >= 0) clipped.Add(a);
                if ((sa >= 0) != (sb >= 0)) { double t = sa / (sa - sb); clipped.Add([a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1])]); }
            }
            if (clipped.Count >= 3) dc.DrawGeometry(Ui.Brush("#FCA5A5"), null, Path(clipped.Select(p => P(p[0], p[1])), true));
        }
        dc.DrawGeometry(null, new Pen(Ui.Navy, 1.5), shape);
        foreach (var b in Bars)
        {
            bool tension = Plane is { Length: 3 } p && p[0] + p[1] * b.Y - p[2] * b.X < 0;
            double r = Math.Max(2, b.Diametro * scale / 2); dc.DrawEllipse(tension ? Brushes.RoyalBlue : Ui.Navy, new Pen(Brushes.White, 1), P(b.X, b.Y), r, r);
        }
        Text(dc, Plane is null ? "Geometria della sezione · nessun risultato di verifica" : "Sezione [mm] · CLS compresso · barre blu: tese", 8, size.Height - 32, 11, width: size.Width - 16);
    }
}

internal sealed partial class StratigraphyDrawing : DrawingView
{
    internal static readonly string[] LayerColors = ["#F4C95D", "#DFA06E", "#A8C686", "#8FB8DE", "#C6A0D5", "#C9B79C"];
    internal JsonObject? Data { get; set; }
    internal bool Micro { get; set; }
    internal int SelectedIndex { get; set; }
    internal bool ShowAll { get; set; } = true;
    internal int[] VisibleIndices => Data is null ? [] : Enumerable.Range(0, Data.Array("stratigrafie").Count).Where(i => ShowAll || i == SelectedIndex).ToArray();
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size)); if (Data is null || size.Width < 80 || size.Height < 110) return;
        var indices = VisibleIndices; var g = Data["generali"]!; var sets = Data.Array("stratigrafie");
        double length = g.D("lunghezza"), angle = Micro ? g.D("inclinazione") * Math.PI / 180 : 0;
        double maximum = indices.Select(i => sets[i]!.AsArray().Sum(r => Math.Max(0, r.D("spessore")))).DefaultIfEmpty(0).Max();
        maximum = Math.Max(maximum, length * Math.Cos(angle));
        if (maximum <= 0) { Text(dc, "Inserire la stratigrafia", 8, 30, width: size.Width - 16); return; }
        double top = 34, band = size.Width / Math.Max(1, indices.Length), scale = (size.Height - 90) / maximum;
        if (Micro && length * Math.Sin(angle) > 0) scale = Math.Min(scale, Math.Max(4, (band - 96) * .48 - 20) / (length * Math.Sin(angle)));
        var colors = LayerColors;
        for (int p = 0; p < indices.Length; p++)
        {
            int index = indices[p]; double left = p * band, x = left + 37, width = Math.Max(24, band - 96 - (HorizontalForces ? band * .36 : 0)), z = 0; int i = 0;
            double pileWidth = Math.Clamp(g.D("diametro") * scale, 8, Math.Max(8, Math.Min(24, width * .22)));
            double run = Micro ? length * Math.Sin(angle) * scale : 0;
            double textWidth = Math.Max(8, width - run - pileWidth - 22);
            Text(dc, $"Stratigrafia {index + 1}", left + 6, 3, 12, bold: true, width: band - 12);
            foreach (var row in sets[index]!.AsArray())
            {
                double h = row.D("spessore"); if (h <= 0) { i++; continue; } double y = top + z * scale, ph = h * scale;
                dc.DrawRectangle(Ui.Brush(colors[i % colors.Length]), new Pen(Ui.Muted, 0.6), new Rect(x, y, width, ph));
                Text(dc, z.ToString("0.##"), left, y - 6, 10, width: 35);
                string name = row.S("strato", ((char)('A' + i % 26)).ToString());
                string description = ph >= 130 ? (Micro ? $"Strato {name}\n{row.S("terreno")}\nS = {h:0.##} m\nα = {row.S("alpha")}" : $"Strato {name}\n{row.S("tipologia")} · {row.S("addensamento")}\nφ′ = {row.S("angolo_attrito")}°\nγ = {row.S("peso_specifico")}\nCu = {row.S("coesione_non_drenata")} kPa") : ph >= 65 ? $"Strato {name}\nS = {h:0.##} m" : $"Strato {name}";
                dc.PushClip(new RectangleGeometry(new Rect(x + 1, y + 1, Math.Max(1, textWidth + 5), Math.Max(1, ph - 2))));
                if (ph >= 18) Text(dc, description, x + 5, y + 4, 11, Brushes.Black, textWidth);
                dc.Pop();
                Text(dc, $"{h:0.##} m", x + width + 5, y + ph / 2 - 7, 10, width: 49);
                z += h; i++;
            }
            Text(dc, z.ToString("0.##"), left, top + z * scale - 6, 10, width: 35);
            Text(dc, $"Totale: {z:0.##} m", left + 3, size.Height - 45, 12, bold: true, width: band - 6);
            if (!Micro && length > 0)
            {
                double px = x + width - pileWidth - 7;
                dc.DrawRectangle(Ui.Brush("#96999D"), new Pen(Ui.Brush("#62666B"), 1), new Rect(px, top, pileWidth, length * scale));
                Text(dc, "Palo", px - 4, top - 18, 10, Ui.Navy, pileWidth + 16);
            }
            if (Micro && length > 0)
            {
                double px = x + width - run - pileWidth / 2 - 7; Point P(double s) => new(px + s * Math.Sin(angle) * scale, top + s * Math.Cos(angle) * scale);
                dc.DrawLine(new Pen(Ui.Brush("#96999D"), pileWidth), P(0), P(length));
                double start = Math.Clamp(g.D("inizio_aderenza"), 0, length);
                dc.DrawLine(new Pen(Ui.Brush("#62666B"), Math.Max(2, pileWidth * .35)), P(start), P(length));
                Text(dc, "Micropalo", Math.Max(x, px - 28), top - 18, 10, Ui.Navy, 70);
            }
            if (!Micro && g.B("presenza_falda"))
            {
                double y = top + g.D("profondita_falda") * scale;
                if (y <= top + z * scale) { dc.DrawLine(new Pen(Brushes.DodgerBlue, 2) { DashStyle = DashStyles.Dash }, new Point(x, y), new Point(x + width, y)); Text(dc, "Falda", x + 3, y - 17, 11, Brushes.DodgerBlue); }
            }
            if (HorizontalForces) DrawHorizontalForces(dc, index, x + width - pileWidth / 2 - 7,
                x + width + 48, left + band - 5, top, scale, length, size.Height);
        }
        Text(dc, HorizontalForces ? "z [m] ↓ · Stato ultimo · ○ cerniera plastica" : "Quote z [m] · positive verso il basso", 3, size.Height - 22, 11, width: size.Width - 6);
    }
}
