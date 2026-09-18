using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
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
internal sealed class ConcreteSectionViewport : DrawingView
{
    internal SezioneCA? Section { get; set; }
    internal List<TendonPoint> Tendons { get; set; } = [];
    internal CheckerStressState? Stress { get; set; }
    internal static readonly string[] Contours = ["CLS · gradiente tensioni", "CLS · bande tensioni", "CLS · scala resistenza", "Barre · tensioni", "Barre · tasso resistenza", "Sezione · tasso resistenza", "CLS · deformazioni", "Solo geometria"];
    internal string Contour { get; set; } = Contours[0];
    internal string ContourLegend { get; private set; } = "";
    internal bool Labels { get; set; } = true;
    internal bool Axes { get; set; } = true;
    internal string SelectedBar { get; set; } = "";
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
            if (closest >= 0) { SelectedBar = "B" + (closest + 1); BarSelected?.Invoke(closest); InvalidateVisual(); }
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
        double scale = Math.Min((size.Width - 110) / (xmax - xmin), (size.Height - 140) / (ymax - ymin)) * zoom;
        Point P(double x, double y) => new(size.Width / 2 + (x - (xmin + xmax) / 2) * scale + pan.X, (size.Height - 30) / 2 - (y - (ymin + ymax) / 2) * scale + pan.Y);
        var shape = Path(section.Outline.Select(p => P(p[0], p[1])), true);
        dc.DrawGeometry(Ui.Brush("#E3EAF1"), new Pen(Ui.Navy, 1.6), shape);
        double[] concreteValues = [], barValues = []; double lower = 0, upper = 1; bool ratios = false, bands = false;
        if (Stress is CheckerStressState stress && Contour != "Solo geometria")
        {
            bool onlyBars = Contour.StartsWith("Barre"); ratios = Contour.Contains("tasso"); bands = Contour.Contains("bande");
            bool strains = Contour.Contains("deformazioni"), fixedScale = Contour.Contains("scala resistenza");
            if (!onlyBars) concreteValues = strains ? stress.FiberStrains : ratios ? stress.FiberStresses.Select(v => Math.Abs(v) / Math.Max(1e-12, v < 0 ? stress.ConcreteCompressionStrength : stress.ConcreteTensionStrength)).ToArray() : stress.FiberStresses;
            if (onlyBars || ratios) barValues = ratios ? stress.tensioni_barre.Select((v, i) => Math.Abs(v) / Math.Max(1e-12, stress.BarStrengths.ElementAtOrDefault(i))).ToArray() : stress.tensioni_barre;
            var values = concreteValues.Concat(barValues).ToArray();
            lower = fixedScale ? -stress.ConcreteCompressionStrength : ratios ? 0 : Math.Min(0, values.DefaultIfEmpty(0).Min());
            upper = fixedScale ? stress.ConcreteTensionStrength : Math.Max(ratios ? 1 : 0, values.DefaultIfEmpty(0).Max());
            if (lower == upper) upper = lower + 1;
            double tile = Math.Sqrt(section.AreaCls / section.Fibers.Count) * scale * 1.8;
            if (concreteValues.Length == section.Fibers.Count)
            {
                dc.PushClip(shape);
                for (int fi = 0; fi < section.Fibers.Count; fi++)
                {
                    var f = section.Fibers[fi]; var p = P(f.X, f.Y);
                    dc.DrawRectangle(ContourColor(concreteValues[fi], lower, upper, ratios, bands), null, new Rect(p.X - tile / 2, p.Y - tile / 2, tile, tile));
                }
                dc.Pop(); dc.DrawGeometry(null, new Pen(Ui.Navy, 1.5), shape);
            }
            ContourLegend = $"{lower:0.###} … {upper:0.###} {(strains ? "‰" : ratios ? "[-]" : "MPa")}";
            Text(dc, Contour + " · " + ContourLegend, 12, 10, 11, Ui.Navy, size.Width - 24, true);
            double legendWidth = Math.Min(190, size.Width - 40);
            for (int i = 0; i < 40; i++) dc.DrawRectangle(ContourColor(lower + (upper - lower) * i / 39, lower, upper, ratios, bands), null, new Rect(12 + legendWidth * i / 40, 36, legendWidth / 40 + 1, 8));
        }
        else ContourLegend = "";
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
            bool chosen = SelectedBar == "B" + (i + 1); Brush color = i < barValues.Length ? ContourColor(barValues[i], lower, upper, ratios, bands) : Ui.Navy;
            dc.DrawEllipse(color, new Pen(chosen ? Brushes.Orange : Brushes.White, chosen ? 3 : 1), p, radius, radius);
            if (Labels) Text(dc, "B" + (i + 1), p.X + radius + 3, p.Y - 7, 10, Ui.Navy);
        }
        for (int ti = 0; ti < Tendons.Count; ti++)
        {
            var t = Tendons[ti]; int index = section.Bars.Count + ti;
            var p = P(t.X, t.Y); dc.DrawEllipse(index < barValues.Length ? ContourColor(barValues[index], lower, upper, ratios, bands) : Brushes.White, new Pen(SelectedBar == t.Id ? Brushes.Orange : Ui.Brush("#B77918"), 2), p, 5, 5);
            dc.DrawLine(new Pen(Ui.Brush("#B77918"), 1), p - new Vector(7, 0), p + new Vector(7, 0));
            if (Labels) Text(dc, t.Id, p.X + 8, p.Y - 8, 10, Ui.Brush("#B77918"));
        }
        Text(dc, $"{section.Width:0.#} × {section.Height:0.#} mm  ·  Ac = {section.AreaCls / 100:0.0} cm²  ·  As = {section.AreaSteel / 100:0.0} cm²", 12, size.Height - 46, 11, width: size.Width - 24);
        Text(dc, Stress is null ? "Assi geometrici x/y · N < 0: compressione" : Contour == "Solo geometria" ? "Nessun contouring · risultati nel riepilogo" : ratios ? "Rapporto alla resistenza · NON esito SLE" : "Blu: negativo · rosso: positivo · valori Checker", 12, size.Height - 26, 11, width: size.Width - 24);
    }
    private static Brush ContourColor(double value, double lower, double upper, bool ratio, bool bands)
    {
        double fraction = Math.Clamp(value < 0 ? value / Math.Min(-1e-12, lower) : value / Math.Max(1e-12, upper), 0, 1);
        if (bands) fraction = Math.Round(fraction * 10) / 10;
        Color end = !ratio && value < 0 ? Color.FromRgb(35, 90, 183) : Color.FromRgb(197, 51, 48);
        return new SolidColorBrush(Color.FromRgb((byte)(244 + (end.R - 244) * fraction), (byte)(246 + (end.G - 246) * fraction), (byte)(248 + (end.B - 248) * fraction)));
    }
}

internal sealed class DomainViewport3D : Grid
{
    private readonly Viewport3D viewport = new();
    private readonly PerspectiveCamera camera = new() { FieldOfView = 40, NearPlaneDistance = .01, FarPlaneDistance = 100 };
    private readonly Model3DGroup surfaces = new(), markings = new(), wire = new();
    private readonly Canvas labels = new() { IsHitTestVisible = false };
    private readonly TextBlock note = Ui.Text("Dominio in attesa di aggiornamento automatico", 12, color: Ui.Muted);
    private readonly Dictionary<GeometryModel3D, string> pickTargets = new();
    private readonly List<(string Id, ActionPoint Force, bool Pass)> actionPoints = [];
    private readonly List<(string Id, Point Position)> screenPoints = [];
    private string? selectedId;
    private SectionDomainMesh? mesh;
    private double yaw = -40, elevation = 25, distance = 5.5;
    private Point? drag;
    private Point3D target;
    private bool panning;
    private bool userNavigated;
    private readonly SolidColorBrush frontBrush = new(Color.FromRgb(104, 165, 198)), backBrush = new(Color.FromRgb(163, 200, 222));
    internal double SurfaceOpacity { get => frontBrush.Opacity; set { frontBrush.Opacity = backBrush.Opacity = Math.Clamp(value, 0, 1); } }
    internal int VisibleActionCount => actionPoints.Count;
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
        Children.Add(viewport); Children.Add(labels); note.VerticalAlignment = VerticalAlignment.Bottom; note.Margin = new Thickness(12); note.IsHitTestVisible = false; Children.Add(note);
        MouseWheel += (_, e) => { userNavigated = true; distance = Math.Clamp(distance * (e.Delta > 0 ? .9 : 1.1), 1.5, 30); UpdateCamera(); e.Handled = true; };
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
        userNavigated = false; yaw = -40; elevation = 25; distance = 3.3 * Math.Max(1, ActualWidth / Math.Max(1, ActualHeight));
        target = mesh is null ? new() : new Point3D(0, (mesh.Vertices.Max(p => p.N) + mesh.Vertices.Min(p => p.N)) / (2 * mesh.Scale.N), 0);
        UpdateCamera();
    }
    internal void StandardView(string axis)
    { userNavigated = true; (yaw, elevation) = axis switch { "Mx–My" => (0, 85), "N–Mx" => (0, 0), "N–My" => (90, 0), _ => (-40, 25) }; UpdateCamera(); }
    internal void SetMesh(SectionDomainMesh? value)
    {
        if (ReferenceEquals(mesh, value) && value is not null) return;
        bool first = mesh is null; mesh = value; surfaces.Children.Clear(); wire.Children.Clear(); markings.Children.Clear(); pickTargets.Clear(); actionPoints.Clear(); SelectedAction = SelectedResistance = null;
        if (mesh is null) { note.Text = "Dominio da calcolare · nessuna mesh valida"; UpdateLabels(); return; }
        var geometry = new MeshGeometry3D { Positions = new Point3DCollection(mesh.Vertices.Select(World)), TriangleIndices = new Int32Collection(mesh.Triangles) }; geometry.Freeze();
        var material = new DiffuseMaterial(frontBrush); var back = new DiffuseMaterial(backBrush);
        surfaces.Children.Add(new GeometryModel3D(geometry, material) { BackMaterial = back });
        if (Wireframe) RebuildWire();
        note.Text = $"{TriangleCount:N0} triangoli · N [kN], Mx / My [kNm] · assi scalati separatamente";
        SetActions([], null, null); if (first && !userNavigated) ResetView(); else UpdateLabels();
    }
    internal void ToggleWireframe() { Wireframe = !Wireframe; RebuildWire(); }
    private void RebuildWire()
    {
        wire.Children.Clear(); if (!Wireframe || mesh is null) return;
        var lines = new MeshGeometry3D(); int stride = Math.Max(1, mesh.Triangles.Count / 3600) * 3;
        for (int i = 0; i < mesh.Triangles.Count; i += stride)
        {
            for (int j = 0; j < 3; j++) Tube(lines, World(mesh.Vertices[mesh.Triangles[i + j]]), World(mesh.Vertices[mesh.Triangles[i + (j + 1) % 3]]), .0015);
        }
        wire.Children.Add(Model(lines, Ui.Brush("#406980")));
    }
    private Point3D World(ActionPoint p) => mesh is null ? new() : new Point3D(p.Mx / mesh.Scale.Mx, p.N / mesh.Scale.N, p.My / mesh.Scale.My);
    internal void SetActions(IEnumerable<(string Id, ActionPoint Force, bool Pass)> actions, string? selected, ActionPoint? resistant)
    {
        markings.Children.Clear(); pickTargets.Clear(); actionPoints.Clear(); actionPoints.AddRange(actions); selectedId = selected; SelectedAction = null; SelectedResistance = resistant; if (mesh is null) { UpdateLabels(); return; }
        foreach (var (end, color) in new[] { (new Point3D(1.3, 0, 0), Ui.Brush("#BF5545")), (new Point3D(0, 1.3, 0), Ui.Brush("#218463")), (new Point3D(0, 0, 1.3), Ui.Blue) })
        { var axis = new MeshGeometry3D(); Tube(axis, new Point3D(-end.X, -end.Y, -end.Z), end, .005); markings.Children.Add(Model(axis, color)); }
        foreach (var action in actionPoints)
        {
            bool chosen = action.Id == selected; var point = World(action.Force); var sphere = Sphere(point, chosen ? .045 : .024);
            var model = Model(sphere, chosen ? Ui.Brush("#E09620") : action.Pass ? Ui.Brush("#257761") : Ui.Brush("#CE4C4C")); markings.Children.Add(model); pickTargets[model] = action.Id;
            if (chosen)
            {
                SelectedAction = action.Force; var vector = new MeshGeometry3D(); Tube(vector, new(), point, .008); markings.Children.Add(Model(vector, Ui.Brush("#E09620")));
                if (resistant is ActionPoint r)
                { var rp = World(r); markings.Children.Add(Model(Sphere(rp, .05), Ui.Brush("#A23BC4"))); var segment = new MeshGeometry3D(); Tube(segment, point, rp, .006); markings.Children.Add(Model(segment, Ui.Brush("#A23BC4"))); }
            }
        }
        UpdateLabels();
    }
    private void UpdateCamera()
    {
        double a = yaw * Math.PI / 180, b = elevation * Math.PI / 180;
        var offset = new Vector3D(distance * Math.Cos(b) * Math.Sin(a), distance * Math.Sin(b), distance * Math.Cos(b) * Math.Cos(a));
        camera.Position = target + offset; camera.LookDirection = -offset; camera.UpDirection = new Vector3D(0, 1, 0); UpdateLabels();
    }
    private void UpdateLabels()
    {
        labels.Children.Clear(); screenPoints.Clear(); if (ActualWidth < 10 || ActualHeight < 10 || mesh is null) return;
        var forward = camera.LookDirection; forward.Normalize(); var right = Vector3D.CrossProduct(forward, camera.UpDirection); right.Normalize(); var up = Vector3D.CrossProduct(right, forward);
        Point? Project(Point3D p)
        {
            var vector = p - camera.Position; double depth = Vector3D.DotProduct(vector, forward); if (depth <= 0) return null;
            double factor = ActualWidth / (2 * Math.Tan(camera.FieldOfView * Math.PI / 360) * depth);
            return new Point(ActualWidth / 2 + Vector3D.DotProduct(vector, right) * factor, ActualHeight / 2 - Vector3D.DotProduct(vector, up) * factor);
        }
        void Marker(Point point, Brush color, string text, double radius = 5)
        {
            var dot = new System.Windows.Shapes.Ellipse { Width = 2 * radius, Height = 2 * radius, Fill = color, Stroke = Brushes.White, StrokeThickness = 1.5 };
            Canvas.SetLeft(dot, point.X - radius); Canvas.SetTop(dot, point.Y - radius); labels.Children.Add(dot);
            if (text != "") { var label = Ui.Text(text, 11, true, color); label.Background = Brushes.White; Canvas.SetLeft(label, point.X + 9); Canvas.SetTop(label, point.Y - 17); labels.Children.Add(label); }
        }
        foreach (var action in actionPoints)
        {
            if (Project(World(action.Force)) is not Point point) continue; screenPoints.Add((action.Id, point));
            bool chosen = action.Id == selectedId;
            if (chosen && SelectedResistance is ActionPoint r && Project(World(r)) is Point rp)
            {
                labels.Children.Add(new System.Windows.Shapes.Line { X1 = point.X, Y1 = point.Y, X2 = rp.X, Y2 = rp.Y, Stroke = Ui.Brush("#A23BC4"), StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection([4, 3]) });
                Marker(rp, Ui.Brush("#A23BC4"), "Rd", 6);
            }
            Marker(point, chosen ? Ui.Brush("#E09620") : action.Pass ? Ui.Brush("#257761") : Ui.Brush("#CE4C4C"), chosen ? "Ed" : "", chosen ? 6 : 4);
        }
        foreach (var (p, text) in new[] { (new Point3D(1.4, 0, 0), $"Mx  {mesh.Scale.Mx:0} kNm"), (new Point3D(0, 1.4, 0), $"N  {mesh.Scale.N:0} kN"), (new Point3D(0, 0, 1.4), $"My  {mesh.Scale.My:0} kNm") })
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
