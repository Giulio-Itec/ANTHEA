using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

/// <summary>Reusable viewport frame: navigation belongs to the view, analysis belongs to the workspace.</summary>
internal sealed class ViewportFrame : Border
{
    internal readonly ContentControl Host;
    internal readonly WrapPanel Toolbar = new();
    internal ViewportFrame(string title, FrameworkElement viewport, Action fit)
    {
        Background = Brushes.White; BorderBrush = Ui.Brush("#DCE2E9"); BorderThickness = new Thickness(1); Padding = new Thickness(8);
        Host = new ContentControl { Content = viewport, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        Toolbar.Children.Add(Ui.Button("Adatta", fit));
        Toolbar.Children.Add(Ui.Button("PNG", () =>
        {
            var dialog = new SaveFileDialog { Filter = "Immagine PNG|*.png", FileName = "ANTHEA_" + title.Replace(' ', '_') + ".png" };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true) try { Archivio.ScriviAtomico(dialog.FileName, viewport is DrawingView drawing ? drawing.Png() : Ui.Snapshot(viewport)); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione immagine"); }
        }));
        Toolbar.Children.Add(Ui.Button("Espandi", () =>
        {
            Host.Content = null;
            var window = Ui.Dialog(this, title, viewport, 1200, 800);
            window.Closed += (_, _) => { window.Content = null; Host.Content = viewport; };
            window.ShowDialog();
        }));
        var heading = Ui.Stack(Ui.Text(title, 15, true), Toolbar); heading.Margin = new Thickness(2, 0, 2, 6);
        Child = Ui.Dock(Host, heading);
    }
}

internal sealed record TendonPoint(string Id, double X, double Y, double Area);
internal sealed partial class ConcreteSectionViewport : DrawingView
{
    internal SezioneCA? Section { get; set; }
    internal List<TendonPoint> Tendons { get; set; } = [];
    internal CheckerStressState? Stress { get; set; }
    internal static readonly string[] Contours = ["CLS · gradiente tensioni", "CLS · bande tensioni", "CLS · scala resistenza", "Barre · tensioni", "Barre · tasso resistenza", "Sezione · tasso resistenza", "CLS · deformazioni", "Solo geometria"];
    internal string Contour { get; set; } = Contours[0];
    internal string ContourLegend { get; private set; } = "";
    internal bool Labels { get; set; } = true;
    internal bool Axes { get; set; } = true;
    internal bool BarValues { get; set; }
    internal bool TendonValues { get; set; }
    internal bool ConcreteValues { get; set; }
    internal JsonObject? Stirrups { get; set; }
    private CheckerStressState? rasterState;
    private string rasterContour = "";
    private BitmapSource? rasterImage;
    internal string SelectedBar { get; set; } = "";
    internal ConcreteEffectiveRegion? EffectiveRegion { get; set; }
    internal string Message { get; set; } = "Inserire la geometria della sezione";
    internal event Action<int>? BarSelected;
    private readonly List<Point> barLocations = [];
    private double zoom = 1;
    private Vector pan;
    private Point? drag;
    internal ConcreteSectionViewport()
    {
        Focusable = true; ToolTip = "Rotella: zoom · trascina: sposta · doppio clic: adatta · clic su barra: identifica";
        MouseWheel += (_, e) => { zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), .3, 20); InvalidateVisual(); e.Handled = true; };
        MouseLeftButtonDown += (_, e) =>
        {
            Focus(); if (e.ClickCount == 2) { ResetView(); return; }
            var p = e.GetPosition(this); int closest = barLocations.FindIndex(q => (q - p).Length < 10);
            if (closest >= 0) { SelectedBar = "B" + (closest + 1).ToString("D2"); BarSelected?.Invoke(closest); InvalidateVisual(); }
            drag = p; CaptureMouse();
        };
        MouseMove += (_, e) => { if (drag is Point p) { var next = e.GetPosition(this); pan += next - p; drag = next; InvalidateVisual(); } };
        MouseLeftButtonUp += (_, _) => { drag = null; ReleaseMouseCapture(); };
        LostMouseCapture += (_, _) => drag = null;
    }
    internal void ResetView() { zoom = 1; pan = default; InvalidateVisual(); }
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Ui.Brush("#F8FAFD"), null, new Rect(size)); barLocations.Clear();
        if (Section is not SezioneCA section || size.Width < 100 || size.Height < 100) { Text(dc, Message, 18, size.Height / 2, 13, width: size.Width - 36); return; }
        double xmin = section.Outline.Min(p => p[0]), xmax = section.Outline.Max(p => p[0]), ymin = section.Outline.Min(p => p[1]), ymax = section.Outline.Max(p => p[1]);
        bool hasLegend = Stress is not null && Contour != "Solo geometria";
        double plotWidth = size.Width - (hasLegend ? 105 : 0);
        bool dimensioned = Dimensions || CoverDimensions || SpacingDimensions;
        double scale = Math.Max(.01, Math.Min((plotWidth - (dimensioned ? 160 : 85)) / (xmax - xmin), (size.Height - (dimensioned ? 180 : 105)) / (ymax - ymin))) * zoom;
        Point P(double x, double y) => new(plotWidth / 2 + (x - (xmin + xmax) / 2) * scale + pan.X, (size.Height - 20) / 2 - (y - (ymin + ymax) / 2) * scale + pan.Y);
        var shape = new GeometryGroup { FillRule=FillRule.EvenOdd };
        shape.Children.Add(Path(section.Outline.Select(p => P(p[0], p[1])), true));
        foreach(var hole in section.Holes)shape.Children.Add(Path(hole.Select(p=>P(p[0],p[1])),true));
        dc.DrawGeometry(Ui.Brush("#E3EAF1"), new Pen(Ui.Navy, 1.6), shape);
        double[] concreteValues = [], barValues = []; double lower = 0, upper = 1; bool ratios = false, bands = false;
        if (Stress is CheckerStressState stress && Contour != "Solo geometria")
        {
            bool onlyBars = Contour.StartsWith("Barre"); ratios = Contour.Contains("tasso"); bands = Contour.Contains("bande");
            bool strains = Contour.Contains("deformazioni"), fixedScale = Contour.Contains("scala resistenza");
            if (!onlyBars)
            {
                // Include boundary extrema: internal samples miss the extreme strains at the edges.
                var samples = strains
                    ? stress.FiberStrains.Concat(stress.ConcreteVertices.Select(v => v.Strain))
                    : stress.FiberStresses.Concat(stress.ConcreteVertices.Select(v => v.Stress));
                concreteValues = (ratios ? samples.Select(v => Math.Abs(v) / Math.Max(1e-12, v < 0 ? stress.ConcreteCompressionStrength : stress.ConcreteTensionStrength)) : samples).ToArray();
            }
            if (onlyBars || ratios) barValues = ratios ? stress.tensioni_barre.Select((v, i) => Math.Abs(v) / Math.Max(1e-12, stress.BarStrengths.ElementAtOrDefault(i))).ToArray() : stress.tensioni_barre;
            var values = concreteValues.Concat(barValues).ToArray();
            lower = fixedScale ? -stress.ConcreteCompressionStrength : ratios ? 0 : Math.Min(0, values.DefaultIfEmpty(0).Min());
            upper = fixedScale ? stress.ConcreteTensionStrength : Math.Max(ratios ? 1 : 0, values.DefaultIfEmpty(0).Max());
            if (lower == upper) upper = lower + 1;
            if (!onlyBars && stress.Raster is StressRaster raster)
            {
                if (!ReferenceEquals(rasterState, stress) || rasterContour != Contour)
                {
                    var pixels = new byte[raster.Size * raster.Size * 4];
                    for (int i = 0; i < raster.Stresses.Length; i++)
                    {
                        double v = strains ? raster.Strains[i] : raster.Stresses[i];
                        if (ratios) v = Math.Abs(v) / Math.Max(1e-12, v < 0 ? stress.ConcreteCompressionStrength : stress.ConcreteTensionStrength);
                        var c = ContourRgb(v, lower, upper, ratios, bands); int k = i * 4; pixels[k] = c.B; pixels[k + 1] = c.G; pixels[k + 2] = c.R; pixels[k + 3] = 255;
                    }
                    rasterImage = BitmapSource.Create(raster.Size, raster.Size, 96, 96, PixelFormats.Bgra32, null, pixels, raster.Size * 4); rasterImage.Freeze();
                    rasterState = stress; rasterContour = Contour;
                }
                dc.PushClip(shape);
                RenderOptions.SetBitmapScalingMode(this, bands ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
                dc.DrawImage(rasterImage, new Rect(P(raster.XMin, raster.YMax), P(raster.XMax, raster.YMin)));
                dc.Pop(); dc.DrawGeometry(null, new Pen(Ui.Navy, 1.5), shape);
            }
            ContourLegend = $"{EngineeringFormat.Number(lower)} … {EngineeringFormat.Number(upper)} {(strains ? "‰" : ratios ? "[-]" : "MPa")}";
            Text(dc, Contour, 12, 10, 11, Ui.Navy, plotWidth - 18, true);
            double legendHeight = Math.Max(45, size.Height - 150), lx = size.Width - 95, ly = 52;
            Text(dc, strains ? "ε [‰]" : ratios ? "η [-]" : "σ [MPa]", lx, 28, 10);
            if (ratios)
            {
                var ranges = new[] { (1.1, ">1,00"), (.95, "0,90–1,00"), (.8, "0,70–0,90"), (.6, "0,50–0,70"), (.25, "0–0,50") };
                for (int i = 0; i < ranges.Length; i++) { double y = ly + i * legendHeight / 5; dc.DrawRectangle(UtilizationPalette.Brush(ranges[i].Item1), null, new Rect(lx, y, 16, legendHeight / 5)); Text(dc, ranges[i].Item2, lx + 20, y + 3, 9, width: 80); }
                ContourLegend = UtilizationPalette.Legend;
            }
            else
            {
            for (int i = 0; i < 100; i++) dc.DrawRectangle(ContourColor(upper - (upper - lower) * i / 99, lower, upper, ratios, bands), null, new Rect(lx, ly + legendHeight * i / 100, 16, legendHeight / 100 + 1));
            var labelPositions = new List<double>();
            foreach (double value in new[] { upper, lower }.Concat(lower < 0 && upper > 0 ? new[] { 0d } : Array.Empty<double>()).Append((upper + lower) / 2).Distinct())
            {
                double y = ly + (upper - value) / (upper - lower) * legendHeight;
                if (labelPositions.Any(previous => Math.Abs(previous - y) < 14)) continue;
                labelPositions.Add(y);
                Text(dc, EngineeringFormat.Number(value), lx + 22, y - 6, 10, width: 73);
            }
            }
        }
        else ContourLegend = "";
        DrawStirrups(dc, section, P, scale);
        if(EffectiveRegion is { } region && region.Outline.Length>=3)
        {
            dc.PushClip(shape);dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(75,230,150,30)),new Pen(Brushes.DarkOrange,2),Path(region.Outline.Select(v=>P(v[0],v[1])),true));dc.Pop();
            foreach(int index in region.BarIndices)if(index<section.Bars.Count){var bar=section.Bars[index];dc.DrawEllipse(null,new Pen(Brushes.DarkOrange,3),P(bar.X,bar.Y),Math.Max(5,bar.Diametro*scale/2+3),Math.Max(5,bar.Diametro*scale/2+3));}
        }
        if (Axes)
        {
            var origin = P(0, 0); double arm = Math.Min(size.Width, size.Height) * .19;
            var px = origin + new Vector(arm, 0); var py = origin + new Vector(0, -arm);
            dc.DrawLine(new Pen(Ui.Brush("#D45A49"), 1.3) { DashStyle = DashStyles.Dash }, origin - new Vector(arm * .3, 0), px);
            dc.DrawLine(new Pen(Ui.Brush("#228765"), 1.3) { DashStyle = DashStyles.Dash }, origin + new Vector(0, arm * .3), py);
            Text(dc, "x", px.X + 4, px.Y - 8, 13, Ui.Brush("#D45A49"), bold: true); Text(dc, "y", py.X + 5, py.Y - 14, 13, Ui.Brush("#228765"), bold: true);
            dc.DrawEllipse(Brushes.White, new Pen(Ui.Navy, 1), origin, 3, 3); Text(dc, "G", origin.X + 4, origin.Y + 3, 10);
        }
        for (int i = 0; i < section.Bars.Count; i++)
        {
            var bar = section.Bars[i]; var p = P(bar.X, bar.Y); barLocations.Add(p); double radius = Math.Max(3, bar.Diametro * scale / 2);
            bool chosen = SelectedBar == "B" + (i + 1).ToString("D2"); Brush color = i < barValues.Length ? ContourColor(barValues[i], lower, upper, ratios, bands) : Ui.Navy;
            dc.DrawEllipse(color, new Pen(chosen ? Brushes.Orange : Brushes.White, chosen ? 3 : 1), p, radius, radius);
            if (BarValues && Stress is { } bs) ValueLabel(dc, p, "B" + (i + 1).ToString("D2"), bs.tensioni_barre.ElementAtOrDefault(i), bs.BarStrains.ElementAtOrDefault(i));
            else if (Labels)
            {
                double dx = bar.X < 0 ? -26 - radius : radius + 3;
                double dy = Math.Abs(bar.Y - (ymin + ymax) / 2) > (ymax - ymin) * .3 ? bar.Y > (ymin + ymax) / 2 ? -21 - i % 2 * 10 : 8 + i % 2 * 10 : -7;
                Text(dc, "B" + (i + 1).ToString("D2"), p.X + dx, p.Y + dy, 10, Ui.Navy);
            }
        }
        for (int ti = 0; ti < Tendons.Count; ti++)
        {
            var t = Tendons[ti]; int index = section.Bars.Count + ti;
            var p = P(t.X, t.Y); dc.DrawEllipse(index < barValues.Length ? ContourColor(barValues[index], lower, upper, ratios, bands) : Brushes.White, new Pen(SelectedBar == t.Id ? Brushes.Orange : Ui.Brush("#B77918"), 2), p, 5, 5);
            dc.DrawLine(new Pen(Ui.Brush("#B77918"), 1), p - new Vector(7, 0), p + new Vector(7, 0));
            if (TendonValues && Stress is { } ts) ValueLabel(dc, p, t.Id, ts.tensioni_barre.ElementAtOrDefault(index), ts.BarStrains.ElementAtOrDefault(index));
            else if (Labels) Text(dc, t.Id, p.X + 8, p.Y - 8, 10, Ui.Brush("#B77918"));
        }
        if (ConcreteValues && Stress is { } cs) foreach (var v in cs.ConcreteVertices) ValueLabel(dc, P(v.X, v.Y), v.Id, v.Stress, v.Strain);
        DrawDimensions(dc, section, P);
        Text(dc, $"{section.Width:0.#} × {section.Height:0.#} mm  ·  Ac = {section.AreaCls / 100:0.0} cm²  ·  As = {section.AreaSteel / 100:0.0} cm²", 12, size.Height - 46, 11, width: size.Width - 24);
        Text(dc, Stress is null ? "Assi geometrici x/y · N < 0: compressione" : Contour == "Solo geometria" ? "Nessun contouring · risultati nel riepilogo" : ratios ? "Rapporto alla resistenza · NON esito SLE" : "Rosso: compressione (−) · blu: trazione (+) · valori Checker", 12, size.Height - 26, 11, width: size.Width - 24);
    }
    private static Brush ContourColor(double value, double lower, double upper, bool ratio, bool bands)
        => new SolidColorBrush(ContourRgb(value, lower, upper, ratio, bands));
    private static Color ContourRgb(double value, double lower, double upper, bool ratio, bool bands)
    {
        double fraction = Math.Clamp(value < 0 ? value / Math.Min(-1e-12, lower) : value / Math.Max(1e-12, upper), 0, 1);
        if (bands) fraction = Math.Round(fraction * 10) / 10;
        if (ratio) return UtilizationPalette.Color(value);
        Color end = value < 0 ? Color.FromRgb(197, 51, 48) : Color.FromRgb(35, 90, 183);
        return Color.FromRgb((byte)(255 + (end.R - 255) * fraction), (byte)(255 + (end.G - 255) * fraction), (byte)(255 + (end.B - 255) * fraction));
    }
    private void ValueLabel(DrawingContext dc, Point p, string id, double stress, double strain)
    {
        string label = $"{id}  σ {EngineeringFormat.Number(stress)} MPa\nε {EngineeringFormat.Number(strain)} ‰";
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), new Pen(Ui.Brush("#D7DFE8"), .5), new Rect(p.X + 7, p.Y - 13, 150, 31), 3, 3);
        Text(dc, label, p.X + 10, p.Y - 12, 9, Ui.Navy, 148);
    }
    private void DrawStirrups(DrawingContext dc, SezioneCA section, Func<double, double, Point> p, double scale)
    {
        if (Stirrups is null || section.Input.S("staffe_presenti","Sì")=="No") return;
        double cover = section.Input.D("cover_mm"), phi = section.Input.D("transverse_bar_diameter_mm"), inset = cover + phi / 2;
        if (!double.IsFinite(phi) || phi <= 0) return;
        // The centreline is offset by cover + Ø/2; draw the actual diameter in section units.
        var pen = new Pen(Ui.Brush("#687C86"), phi * scale);
        double xmin = section.Outline.Min(v => v[0]) + inset, xmax = section.Outline.Max(v => v[0]) - inset, ymin = section.Outline.Min(v => v[1]) + inset, ymax = section.Outline.Max(v => v[1]) - inset;
        if (xmax <= xmin || ymax <= ymin) return;
        if (section.Shape == "Circolare")
        {
            double r = section.Radius - inset; dc.DrawEllipse(null, pen, p(0, 0), r * scale, r * scale);
            if (Stirrups.S("tipo_staffa") == "Spirale") dc.DrawEllipse(null, new Pen(pen.Brush, 1) { DashStyle = DashStyles.Dash }, p(0, 0), (r - phi) * scale, (r - phi) * scale);
            int count = Math.Clamp((int)Stirrups.D("rami_interni"), 0, 100);
            double angle = Stirrups.D("rotazione_staffa") * Math.PI / 180;
            Point Rot(double x, double y) => p(x * Math.Cos(angle) - y * Math.Sin(angle), x * Math.Sin(angle) + y * Math.Cos(angle));
            for (int i = 0; i < count; i++)
            {
                double x = -r + 2 * r * (i + 1) / (count + 1);
                if (Stirrups.S("schema_interno", "Bracci paralleli") == "Staffe chiuse sovrapposte")
                {
                    double left = Math.Max(-.85 * r, x - .55 * r), right = Math.Min(.85 * r, x + .55 * r);
                    double y = Math.Sqrt(r * r - Math.Pow(Math.Max(Math.Abs(left), Math.Abs(right)), 2));
                    dc.DrawGeometry(null, pen, Path(new[] { Rot(left, -y), Rot(right, -y), Rot(right, y), Rot(left, y) }, true));
                }
                else
                {
                    double y = Math.Sqrt(r * r - x * x); dc.DrawLine(pen, Rot(x, -y), Rot(x, y));
                }
            }
        }
        else
        {
            double webLeft = xmin, webRight = xmax;
            if (section.Shape == "A T")
            {
                webLeft = -section.Input.D("web_width_mm") / 2 + inset; webRight = -webLeft;
                double flangeBottom = ymax - section.Input.D("flange_thickness_mm") + 2 * inset;
                if (flangeBottom < ymax) dc.DrawRectangle(null, pen, new Rect(p(xmin, ymax), p(xmax, flangeBottom)));
            }
            dc.DrawRectangle(null, pen, new Rect(p(webLeft, ymax), p(webRight, ymin)));
            int nx = Math.Clamp((int)Stirrups.D("rami_x", 2), 2, 100), ny = Math.Clamp((int)Stirrups.D("rami_y", 2), 2, 100);
            for (int i = 1; i < nx - 1; i++) { double y = ymin + (ymax - ymin) * i / (nx - 1); dc.DrawLine(pen, p(webLeft, y), p(webRight, y)); }
            for (int i = 1; i < ny - 1; i++) { double x = webLeft + (webRight - webLeft) * i / (ny - 1); dc.DrawLine(pen, p(x, ymin), p(x, ymax)); }
        }
    }
}

internal sealed class DomainViewport3D : Grid
{
    private readonly Viewport3D viewport = new();
    private readonly PerspectiveCamera camera = new() { FieldOfView = 40, NearPlaneDistance = .01, FarPlaneDistance = 100 };
    private readonly Model3DGroup surfaces = new(), markings = new(), wire = new();
    private readonly Canvas labels = new() { IsHitTestVisible = false };
    private readonly DomainWireOverlay wireOverlay = new() { IsHitTestVisible = false };
    private readonly TextBlock note = Ui.Text("Dominio in attesa di aggiornamento automatico", 12, color: Ui.Muted);
    private readonly Dictionary<GeometryModel3D, string> pickTargets = new();
    private readonly List<(string Id, ActionPoint Force, bool Pass)> actionPoints = [];
    private readonly List<(string Id, Point Position)> screenPoints = [];
    private string? selectedId;
    private SectionDomainMesh? mesh;
    private MeshGeometry3D? surfaceGeometry;
    private GeometryModel3D? surfaceModel;
    private double yaw = -40, elevation = 25, distance = 5.5;
    private double fitDistance = 5.5, scaleMx = 1, scaleN = 1, scaleMy = 1;
    internal bool FitIncludesActions { get; set; }
    internal void SetFitActions(bool value) => FitIncludesActions = value;
    internal double Zoom { get => fitDistance / distance; set { userNavigated = true; distance = fitDistance / Math.Clamp(value, .1, 20); UpdateCamera(); } }
    internal void SetAxisScale(double mx, double n, double my)
    {
        mx = Math.Clamp(mx, .25, 4); n = Math.Clamp(n, .25, 4); my = Math.Clamp(my, .25, 4);
        if ((scaleMx, scaleN, scaleMy) == (mx, n, my)) return;
        (scaleMx, scaleN, scaleMy) = (mx, n, my);
        var actions = actionPoints.ToArray(); var id = selectedId; var resistance = SelectedResistance;
        SetMesh(mesh, true); SetActions(actions, id, resistance);
    }
    private Point? drag;
    private Point3D target;
    private bool panning;
    private bool userNavigated;
    private readonly SolidColorBrush frontBrush = new(Color.FromRgb(104, 165, 198)), backBrush = new(Color.FromRgb(163, 200, 222));
    internal double SurfaceOpacity { get => frontBrush.Opacity; set { frontBrush.Opacity = backBrush.Opacity = Math.Clamp(value, 0, 1); SortSurface(); } }
    internal bool ShowActions { get; set; } = true;
    internal bool OnlySelectedActions { get; set; }
    internal bool ShowResistance { get; set; } = true;
    internal bool ShowVerificationLines { get; set; } = true;
    internal string VerificationCriterion { get; set; } = "N costante";
    internal double ActionPointSize { get; set; } = 5;
    internal double ResistancePointSize { get; set; } = 5;
    internal bool ColorByRatio { get; set; }
    internal IReadOnlyDictionary<string, double?>? Ratios { get; set; }
    internal IReadOnlyDictionary<string, ActionPoint>? Resistances { get; set; }
    private Brush ActionColor(string id, bool chosen, bool pass)
    {
        if (!ColorByRatio) return chosen ? Ui.Brush("#E09620") : Ui.Blue;
        double? ratio = Ratios is null ? pass ? 0 : 2 : Ratios.GetValueOrDefault(id);
        return UtilizationPalette.Brush(ratio);
    }
    internal int VisibleActionCount => ShowActions ? actionPoints.Count(p => !OnlySelectedActions || p.Id == selectedId) : 0;
    internal event Action<string>? ActionSelected;
    internal bool Wireframe { get; private set; }
    internal int TriangleCount => mesh?.Triangles.Count / 3 ?? 0;
    internal ActionPoint? SelectedAction { get; private set; }
    internal ActionPoint? SelectedResistance { get; private set; }
    internal DomainViewport3D()
    {
        Background = Ui.Brush("#F8FAFD"); ClipToBounds = true; MinHeight = 180;
        viewport.Camera = camera;
        var world = new Model3DGroup(); world.Children.Add(new AmbientLight(Color.FromRgb(145, 155, 175))); world.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -3)));
        world.Children.Add(surfaces); world.Children.Add(wire); world.Children.Add(markings); viewport.Children.Add(new ModelVisual3D { Content = world });
        Children.Add(viewport); Children.Add(wireOverlay); Children.Add(labels); note.VerticalAlignment = VerticalAlignment.Bottom; note.Margin = new Thickness(12); note.IsHitTestVisible = false; Children.Add(note);
        MouseWheel += (_, e) => { Zoom *= e.Delta > 0 ? 1.1 : 1 / 1.1; e.Handled = true; };
        MouseDown += (_, e) =>
        {
            if (e.ClickCount == 2) { ResetView(); return; }
            drag = e.GetPosition(this); panning = e.ChangedButton == MouseButton.Right || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift); CaptureMouse();
            var near = screenPoints.FirstOrDefault(p => (p.Position - e.GetPosition(this)).Length < 12);
            if (!panning && near.Id is not null) { ActionSelected?.Invoke(near.Id); e.Handled = true; return; }
            if (!panning) VisualTreeHelper.HitTest(viewport, null, hit =>
            {
                if (hit is RayMeshGeometry3DHitTestResult result && result.ModelHit is GeometryModel3D model && pickTargets.TryGetValue(model, out string? id)) { ActionSelected?.Invoke(id); return HitTestResultBehavior.Stop; }
                return HitTestResultBehavior.Continue;
            }, new PointHitTestParameters(e.GetPosition(viewport)));
            e.Handled = true;
        };
        MouseMove += (_, e) =>
        {
            if (drag is not Point p) return; var next = e.GetPosition(this); var delta = next - p; drag = next; userNavigated = true;
            if (panning)
            {
                var forward = camera.LookDirection; forward.Normalize(); var right = Vector3D.CrossProduct(forward, camera.UpDirection); right.Normalize(); var up = Vector3D.CrossProduct(right, forward);
                target += (-right * delta.X + up * delta.Y) * distance / Math.Max(300, ActualHeight);
            }
            else { yaw += delta.X * .4; elevation = Math.Clamp(elevation + delta.Y * .35, -85, 85); }
            UpdateCamera();
        };
        MouseUp += (_, _) => { drag = null; ReleaseMouseCapture(); }; LostMouseCapture += (_, _) => drag = null;
        SizeChanged += (_, _) => { if (!userNavigated) ResetView(); else UpdateLabels(); }; UpdateCamera();
        ToolTip = "Trascina: orbita · tasto destro / Maiusc: sposta · rotella: zoom · doppio clic: adatta";
    }
    internal void ResetView()
    {
        yaw = -40; elevation = 25; FitView();
    }
    internal void FitView()
    {
        userNavigated = false;
        var points = (mesh?.Vertices.Select(World) ?? []).Concat(AxisLines().SelectMany(a => new[] { a.Start, a.End })).ToList();
        if (FitIncludesActions) { if (ShowActions) points.AddRange(actionPoints.Select(p => World(p.Force))); if (ShowResistance && SelectedResistance is ActionPoint r) points.Add(World(r)); if (ShowResistance && Resistances is not null) points.AddRange(Resistances.Values.Select(World)); }
        target = new Point3D((points.Min(p => p.X) + points.Max(p => p.X)) / 2, (points.Min(p => p.Y) + points.Max(p => p.Y)) / 2, (points.Min(p => p.Z) + points.Max(p => p.Z)) / 2);
        double half = camera.FieldOfView * Math.PI / 360;
        double verticalHalf = Math.Atan(Math.Tan(half) / Math.Max(.1, ActualWidth / Math.Max(1, ActualHeight)));
        double a = yaw * Math.PI / 180, b = elevation * Math.PI / 180;
        var forward = new Vector3D(-Math.Cos(b) * Math.Sin(a), -Math.Sin(b), -Math.Cos(b) * Math.Cos(a));
        var right = Vector3D.CrossProduct(forward, new Vector3D(0, 1, 0)); right.Normalize(); var up = Vector3D.CrossProduct(right, forward);
        distance = fitDistance = Math.Max(.5, points.Max(p =>
        {
            var v = p - target;
            return 1.12 * Math.Max(Math.Abs(Vector3D.DotProduct(v, right)) / Math.Tan(half), Math.Abs(Vector3D.DotProduct(v, up)) / Math.Tan(verticalHalf)) - Vector3D.DotProduct(v, forward);
        }));
        camera.FarPlaneDistance = Math.Max(100, distance * 30);
        UpdateCamera();
    }
    internal void StandardView(string axis)
    { userNavigated = true; (yaw, elevation) = axis switch { "Mx–My" => (0, 85), "N–Mx" => (0, 0), "N–My" => (90, 0), _ => (-40, 25) }; UpdateCamera(); }
    internal void SetMesh(SectionDomainMesh? value, bool force = false)
    {
        if (!force && ReferenceEquals(mesh, value) && value is not null) return;
        bool first = mesh is null; mesh = value; surfaceGeometry = null; surfaceModel = null; surfaces.Children.Clear(); wire.Children.Clear(); markings.Children.Clear(); pickTargets.Clear(); actionPoints.Clear(); SelectedAction = SelectedResistance = null;
        if (mesh is null) { note.Text = "Dominio da calcolare · nessuna mesh valida"; UpdateLabels(); return; }
        var geometry = new MeshGeometry3D { Positions = new Point3DCollection(mesh.Vertices.Select(World)), TriangleIndices = new Int32Collection(mesh.Triangles) };
        var center = new Point3D(geometry.Positions.Average(p => p.X), geometry.Positions.Average(p => p.Y), geometry.Positions.Average(p => p.Z));
        var normals = new Vector3D[geometry.Positions.Count];
        for (int i = 0; i + 2 < mesh.Triangles.Count; i += 3)
        {
            int a = mesh.Triangles[i], b = mesh.Triangles[i + 1], c = mesh.Triangles[i + 2];
            var n = Vector3D.CrossProduct(geometry.Positions[b] - geometry.Positions[a], geometry.Positions[c] - geometry.Positions[a]);
            if (Vector3D.DotProduct(n, geometry.Positions[a] - center) < 0) { geometry.TriangleIndices[i + 1] = c; geometry.TriangleIndices[i + 2] = b; n = -n; }
            normals[a] += n; normals[b] += n; normals[c] += n;
        }
        for (int i = 0; i < normals.Length; i++) if (normals[i].Length > 1e-12) normals[i].Normalize();
        geometry.Normals = new Vector3DCollection(normals); geometry.Freeze();
        var material = new DiffuseMaterial(frontBrush); var back = new DiffuseMaterial(backBrush);
        surfaceGeometry = geometry; surfaceModel = new GeometryModel3D(geometry, material) { BackMaterial = back }; surfaces.Children.Add(surfaceModel); SortSurface();
        if (Wireframe) RebuildWire();
        note.Text = $"{TriangleCount:N0} triangoli · N [kN], Mx / My [kNm] · assi scalati separatamente";
        SetActions([], null, null); if (first && !userNavigated) ResetView(); else UpdateLabels();
    }
    internal void ToggleWireframe() { Wireframe = !Wireframe; RebuildWire(); }
    private void SortSurface()
    {
        if (surfaceGeometry is null || surfaceModel is null) return;
        if (SurfaceOpacity >= .999) { surfaceModel.Geometry = surfaceGeometry; return; }
        // WPF does not depth-sort transparent mesh faces. Sort from back to front
        // after every camera change instead of relying on the native mesh's face order.
        var source = surfaceGeometry; var look = camera.LookDirection;
        double Depth(int i)
        {
            Point3D a = source.Positions[source.TriangleIndices[i]], b = source.Positions[source.TriangleIndices[i + 1]], c = source.Positions[source.TriangleIndices[i + 2]];
            return Vector3D.DotProduct(new Vector3D(a.X + b.X + c.X, a.Y + b.Y + c.Y, a.Z + b.Z + c.Z), look);
        }
        var indices = Enumerable.Range(0, source.TriangleIndices.Count / 3).Select(i => i * 3).OrderByDescending(Depth)
            .SelectMany(i => new[] { source.TriangleIndices[i], source.TriangleIndices[i + 1], source.TriangleIndices[i + 2] });
        var sorted = new MeshGeometry3D { Positions = source.Positions, Normals = source.Normals, TriangleIndices = new Int32Collection(indices) }; sorted.Freeze(); surfaceModel.Geometry = sorted;
    }
    private void RebuildWire()
    {
        wire.Children.Clear(); UpdateLabels();
    }
    private Point3D World(ActionPoint p) => mesh is null ? new() : new Point3D(p.Mx / mesh.Scale.Mx * scaleMx, p.N / mesh.Scale.N * scaleN, p.My / mesh.Scale.My * scaleMy);
    private IEnumerable<(Point3D Start, Point3D End, Point3D LabelPoint, Brush Color, string Label)> AxisLines()
    {
        var points = mesh?.Vertices.ToArray() ?? [new ActionPoint(-1, -1, -1), new ActionPoint(1, 1, 1)];
        foreach (var (axis, name, color) in new[] { (0, "Mx", Ui.Brush("#BF5545")), (1, "N", Ui.Brush("#218463")), (2, "My", Ui.Blue) })
        {
            double Value(ActionPoint p) => axis == 0 ? p.Mx : axis == 1 ? p.N : p.My;
            double min = Math.Min(0, points.Min(Value)), max = Math.Max(0, points.Max(Value)), label = Math.Abs(min) > Math.Abs(max) ? min : max;
            Point3D P(double v) => mesh is null ? axis == 0 ? new(v, 0, 0) : axis == 1 ? new(0, v, 0) : new(0, 0, v) : World(axis == 0 ? new(0, v, 0) : axis == 1 ? new(v, 0, 0) : new(0, 0, v));
            yield return (P(min * 1.08), P(max * 1.08), P(label * 1.08), color, name + "  " + EngineeringFormat.Number(label) + (axis == 1 ? " kN" : " kNm"));
        }
    }
    internal void SetActions(IEnumerable<(string Id, ActionPoint Force, bool Pass)> actions, string? selected, ActionPoint? resistant)
    {
        markings.Children.Clear(); pickTargets.Clear(); actionPoints.Clear(); actionPoints.AddRange(actions); selectedId = selected; SelectedAction = null; SelectedResistance = resistant; if (mesh is null) { UpdateLabels(); return; }
        foreach (var line in AxisLines())
        { var axis = new MeshGeometry3D(); Tube(axis, line.Start, line.End, .005); markings.Children.Add(Model(axis, line.Color)); }
        var grid = new MeshGeometry3D();
        for (int i = -4; i <= 4; i++) { double v = i * .25; Tube(grid, new Point3D(v, 0, -1), new Point3D(v, 0, 1), .001); Tube(grid, new Point3D(-1, 0, v), new Point3D(1, 0, v), .001); }
        markings.Children.Add(Model(grid, Ui.Brush("#B9C7D4")));
        if (ShowResistance && Resistances is not null)
            foreach (var (id, r) in Resistances) { if (id == selected) continue; var model = Model(Sphere(World(r), .0064 * ResistancePointSize), UtilizationPalette.Brush(Ratios?.GetValueOrDefault(id))); markings.Children.Add(model); pickTargets[model] = id; }
        foreach (var action in actionPoints)
        {
            bool chosen = action.Id == selected; var point = World(action.Force); var sphere = Sphere(point, ActionPointSize * (chosen ? .009 : .0048));
            var model = Model(sphere, ActionColor(action.Id, chosen, action.Pass));
            if (ShowActions && (!OnlySelectedActions || chosen)) { markings.Children.Add(model); pickTargets[model] = action.Id; }
            if (chosen || Resistances?.ContainsKey(action.Id) == true)
            {
                if (chosen) SelectedAction = action.Force; var vector = new MeshGeometry3D(); Tube(vector, World(SectionMomentResistance.VerificationOrigin(action.Force, VerificationCriterion)), point, .006); if (ShowVerificationLines) markings.Children.Add(Model(vector, Ui.Brush("#E09620")));
                if ((chosen ? resistant : Resistances?.GetValueOrDefault(action.Id)) is ActionPoint r)
                { var rp = World(r); if (ShowResistance && chosen) markings.Children.Add(Model(Sphere(rp, .01 * ResistancePointSize), UtilizationPalette.Brush(Ratios?.GetValueOrDefault(selected ?? "")))); var segment = new MeshGeometry3D(); Tube(segment, point, rp, .006); if (ShowVerificationLines) markings.Children.Add(Model(segment, Ui.Brush("#A23BC4"))); }
            }
        }
        UpdateLabels();
    }
    private void UpdateCamera()
    {
        double a = yaw * Math.PI / 180, b = elevation * Math.PI / 180;
        var offset = new Vector3D(distance * Math.Cos(b) * Math.Sin(a), distance * Math.Sin(b), distance * Math.Cos(b) * Math.Cos(a));
        camera.Position = target + offset; camera.LookDirection = -offset; camera.UpDirection = new Vector3D(0, 1, 0); SortSurface(); UpdateLabels();
    }
    private void UpdateLabels()
    {
        labels.Children.Clear(); screenPoints.Clear(); wireOverlay.Segments.Clear(); wireOverlay.InvalidateVisual(); if (ActualWidth < 10 || ActualHeight < 10 || mesh is null) return;
        var forward = camera.LookDirection; forward.Normalize(); var right = Vector3D.CrossProduct(forward, camera.UpDirection); right.Normalize(); var up = Vector3D.CrossProduct(right, forward);
        Point? Project(Point3D p)
        {
            var vector = p - camera.Position; double depth = Vector3D.DotProduct(vector, forward); if (depth <= 0) return null;
            double factor = ActualWidth / (2 * Math.Tan(camera.FieldOfView * Math.PI / 360) * depth);
            return new Point(ActualWidth / 2 + Vector3D.DotProduct(vector, right) * factor, ActualHeight / 2 - Vector3D.DotProduct(vector, up) * factor);
        }
        if (Wireframe && surfaceGeometry is { } surface)
        {
            var edges = new HashSet<(int, int)>();
            for (int i = 0; i < surface.TriangleIndices.Count; i += 3)
            {
                int a = surface.TriangleIndices[i], b = surface.TriangleIndices[i + 1], c = surface.TriangleIndices[i + 2];
                Point3D p = surface.Positions[a], q = surface.Positions[b], r = surface.Positions[c];
                if (Vector3D.DotProduct(Vector3D.CrossProduct(q - p, r - p), p - camera.Position) >= 0) continue;
                foreach (var (start, end) in new[] { (a, b), (b, c), (c, a) })
                    if (edges.Add((Math.Min(start, end), Math.Max(start, end))) && Project(surface.Positions[start]) is Point p1 && Project(surface.Positions[end]) is Point p2)
                        wireOverlay.Segments.Add((p1, p2));
            }
        }
        void Marker(Point point, Brush color, string text, double radius = 5)
        {
            var dot = new System.Windows.Shapes.Ellipse { Width = 2 * radius, Height = 2 * radius, Fill = color, Stroke = Brushes.White, StrokeThickness = 1.5 };
            Canvas.SetLeft(dot, point.X - radius); Canvas.SetTop(dot, point.Y - radius); labels.Children.Add(dot);
            if (text != "") { var label = Ui.Text(text, 11, true, color); label.Background = Brushes.White; Canvas.SetLeft(label, point.X + 9); Canvas.SetTop(label, point.Y - 17); labels.Children.Add(label); }
        }
        foreach (var action in actionPoints)
        {
            if (Project(World(action.Force)) is not Point point) continue; if (ShowActions && (!OnlySelectedActions || action.Id == selectedId)) screenPoints.Add((action.Id, point));
            bool chosen = action.Id == selectedId;
            if (ShowVerificationLines && (chosen || Resistances?.ContainsKey(action.Id) == true)
                && Project(World(SectionMomentResistance.VerificationOrigin(action.Force, VerificationCriterion))) is Point origin)
                labels.Children.Add(new System.Windows.Shapes.Line { X1 = origin.X, Y1 = origin.Y, X2 = point.X, Y2 = point.Y, Stroke = Ui.Brush("#E09620"), StrokeThickness = 1.5 });
            if ((chosen ? SelectedResistance : Resistances?.GetValueOrDefault(action.Id)) is ActionPoint r && Project(World(r)) is Point rp)
            {
                if (ShowVerificationLines) labels.Children.Add(new System.Windows.Shapes.Line { X1 = point.X, Y1 = point.Y, X2 = rp.X, Y2 = rp.Y, Stroke = Ui.Brush("#A23BC4"), StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection([4, 3]) });
                if (ShowResistance && chosen) Marker(rp, UtilizationPalette.Brush(Ratios?.GetValueOrDefault(action.Id)), "Rd", ResistancePointSize + 1);
            }
            if (ShowActions && (!OnlySelectedActions || chosen)) Marker(point, ActionColor(action.Id, chosen, action.Pass), chosen ? "Ed" : "", ActionPointSize + (chosen ? 1 : 0));
        }
        if (ShowResistance && Resistances is not null)
            foreach (var (id, resistance) in Resistances)
                if (id != selectedId && Project(World(resistance)) is Point rp)
                { Marker(rp, UtilizationPalette.Brush(Ratios?.GetValueOrDefault(id)), "", ResistancePointSize); screenPoints.Add((id, rp)); }
        foreach (var (p, text) in AxisLines().Select(line => (line.LabelPoint, line.Label)))
        {
            var vector = p - camera.Position; double depth = Vector3D.DotProduct(vector, forward); if (depth <= 0) continue;
            double factor = ActualWidth / (2 * Math.Tan(camera.FieldOfView * Math.PI / 360) * depth);
            var label = Ui.Text(text, 11, true); Canvas.SetLeft(label, Math.Clamp(ActualWidth / 2 + Vector3D.DotProduct(vector, right) * factor, 4, Math.Max(4, ActualWidth - 125))); Canvas.SetTop(label, Math.Clamp(ActualHeight / 2 - Vector3D.DotProduct(vector, up) * factor, 4, Math.Max(4, ActualHeight - 50))); labels.Children.Add(label);
        }
    }
    private static GeometryModel3D Model(MeshGeometry3D mesh, Brush brush) { mesh.Freeze(); var material = new DiffuseMaterial(brush); return new(mesh, material) { BackMaterial = material }; }
    private static MeshGeometry3D Sphere(Point3D center, double radius)
    {
        var mesh = new MeshGeometry3D();
        for (int i = 0; i <= 8; i++) for (int j = 0; j <= 12; j++) { double a = Math.PI * i / 8, b = 2 * Math.PI * j / 12; mesh.Positions.Add(center + new Vector3D(Math.Sin(a) * Math.Cos(b), Math.Cos(a), Math.Sin(a) * Math.Sin(b)) * radius); }
        for (int i = 0; i < 8; i++) for (int j = 0; j < 12; j++) { int p = i * 13 + j; foreach (int index in new[] { p, p + 13, p + 1, p + 1, p + 13, p + 14 }) mesh.TriangleIndices.Add(index); }
        return mesh;
    }
    private static void Tube(MeshGeometry3D mesh, Point3D start, Point3D end, double radius)
    {
        var direction = end - start; if (direction.Length < 1e-10) return; direction.Normalize(); var u = Vector3D.CrossProduct(direction, Math.Abs(direction.Y) < .9 ? new Vector3D(0, 1, 0) : new Vector3D(1, 0, 0)); u.Normalize(); var v = Vector3D.CrossProduct(direction, u);
        int first = mesh.Positions.Count;
        for (int i = 0; i < 6; i++) { var offset = radius * (u * Math.Cos(i * Math.PI / 3) + v * Math.Sin(i * Math.PI / 3)); mesh.Positions.Add(start + offset); mesh.Positions.Add(end + offset); }
        for (int i = 0; i < 6; i++) { int a = first + 2 * i, b = first + 2 * ((i + 1) % 6); foreach (int index in new[] { a, b, a + 1, a + 1, b, b + 1 }) mesh.TriangleIndices.Add(index); }
    }
}

// Screen-space strokes avoid depth fighting with the transparent surface at dense discretizations.
internal sealed class DomainWireOverlay : DrawingView
{
    internal readonly List<(Point A, Point B)> Segments = [];
    protected override void Render(DrawingContext dc, Size size)
    {
        var pen = new Pen(Ui.Brush("#708EA1"), .55);
        foreach (var (a, b) in Segments) dc.DrawLine(pen, a, b);
    }
}
