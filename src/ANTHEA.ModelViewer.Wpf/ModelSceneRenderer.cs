using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.SharpDX;
using HelixToolkit.Maths;
using Color = System.Windows.Media.Color;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using Material = HelixToolkit.Wpf.SharpDX.Material;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed record SceneOptions(int Mode, IReadOnlyList<ResultValue> Values, string Family, int Subdivisions, bool ShowMesh, bool ShowIsolines,
    IReadOnlySet<int>? Visible, IReadOnlySet<int> Selection, bool ShowLocalAxes, bool ShowRestraints, bool ShowConstraints, bool ShowLinks, bool ShowReleases, bool ShowContext,
    DisplayOptions Display, double ColorMinimum, double ColorMaximum, bool ShowNodeAxes, bool OverlaysOnlySelection, IReadOnlySet<int> SelectedNodes,
    int Lighting = 1, int Palette = 0, int IsolineCount = 8, double IsolineWidth = .5, ExplorationOptions? Exploration = null, double RasterScale = 1);
internal sealed record DisplayOptions(IReadOnlyDictionary<int, ElementDisplay> Colors, int SurfaceMode, bool HiddenWireframe,
    bool NodeIds, bool ElementIds, bool PropertyIds, bool Nodes, bool LabelsSelectedOnly, double LabelScale, double LineWidth);
internal sealed record SceneStatistics(TimeSpan Preparation, int Triangles, int MissingElements);

/// <summary>Owns the Helix scene and GPU resources. No dialogs, document writes or verification calculations.</summary>
internal sealed partial class ModelSceneRenderer : IDisposable
{
    private readonly Viewport3DX viewport;
    private ModelSnapshot? model;
    private Dictionary<int, Vector3> points = new();
    private readonly Dictionary<int, bool[]> plateSides = new();
    private double extent = 1;
    private bool disposed;
    private SceneOptions? lastOptions;
    private readonly Dictionary<MeshGeometryModel3D, IReadOnlyList<int>> meshElements = new();
    private readonly Dictionary<LineGeometryModel3D, IReadOnlyList<int>> lineElements = new();
    public ModelSceneRenderer(Viewport3DX viewport) { this.viewport = viewport; viewport.EffectsManager = new DefaultEffectsManager(); }
    public void SetModel(ModelSnapshot snapshot)
    {
        model = snapshot;
        double cx = (snapshot.Nodes.Min(n => n.X) + snapshot.Nodes.Max(n => n.X)) / 2, cy = (snapshot.Nodes.Min(n => n.Y) + snapshot.Nodes.Max(n => n.Y)) / 2, cz = (snapshot.Nodes.Min(n => n.Z) + snapshot.Nodes.Max(n => n.Z)) / 2;
        modelOrigin=new(cx,cy,cz);
        points = snapshot.Nodes.ToDictionary(n => n.Id, n => new Vector3((float)(n.X - cx), (float)(n.Y - cy), (float)(n.Z - cz)));
        extent = Math.Max(1, (points.Values.Aggregate(Vector3.Max) - points.Values.Aggregate(Vector3.Min)).Length());
        plateSides.Clear();
        var edges = new Dictionary<(int, int, int), List<(int element, int edge, Vector3 normal)>>();
        foreach (var element in snapshot.Elements.Where(e => e.Type == "PLATE"))
        {
            plateSides[element.Id] = Enumerable.Repeat(true, element.Nodes.Length).ToArray();
            var p = element.Nodes.Select(n => points[n]).ToArray(); var normal = Vector3.Normalize(Vector3.Cross(p[1] - p[0], p[2] - p[0]));
            for (int i = 0; i < p.Length; i++)
            {
                int a = element.Nodes[i], b = element.Nodes[(i + 1) % p.Length]; var key = (element.Property, Math.Min(a, b), Math.Max(a, b));
                if (!edges.TryGetValue(key, out var list)) edges[key] = list = new(); list.Add((element.Id, i, normal));
            }
        }
        // Remove only matching internal side faces of coplanar plates with the same physical property.
        // Different normals, properties and non-manifold junctions retain their full volume boundaries.
        foreach (var edge in edges.Values.Where(e => e.Count == 2))
            if (Vector3.Dot(edge[0].normal, edge[1].normal) > 0.999999f)
            { plateSides[edge[0].element][edge[0].edge] = false; plateSides[edge[1].element][edge[1].edge] = false; }
    }
    void AddMesh(SurfaceBuilder builder, Color4 color, bool contour = false)
    {
        if(clipping is {} clip) builder=clip.Apply(builder);
        if (builder.Indices.Count == 0) return;
        float opacity=(float)Math.Clamp(lastOptions?.Exploration?.Opacity??1,.1,1);color.Alpha=opacity;
        Material material = contour ? new ColorStripeMaterial { DiffuseColor = new Color4(1, 1, 1, opacity), ColorStripeX = Enumerable.Range(0, 256).Select(i => Contours.DisplayColor(i / 255.0, lastOptions?.Palette ?? 0,lastOptions?.Exploration?.ReversePalette??false,lastOptions?.Exploration?.Bands??0)).ToList(), ColorStripeXEnabled = true, ColorStripeYEnabled = false, ColorStripeSampler = (lastOptions?.Exploration?.Bands??0)>0?new SharpDX.Direct3D11.SamplerStateDescription { Filter=SharpDX.Direct3D11.Filter.MinMagMipPoint,AddressU=SharpDX.Direct3D11.TextureAddressMode.Clamp,AddressV=SharpDX.Direct3D11.TextureAddressMode.Clamp,AddressW=SharpDX.Direct3D11.TextureAddressMode.Clamp,MaximumLod=float.MaxValue }:HelixToolkit.SharpDX.Shaders.DefaultSamplers.LinearSamplerClampAni4 }
            : lastOptions?.Lighting == 0 ? new HelixToolkit.Wpf.SharpDX.DiffuseMaterial { DiffuseColor = color, EnableUnLit = true }
            : lastOptions?.Lighting == 2 ? new PBRMaterial { AlbedoColor = color, MetallicFactor = 0, RoughnessFactor = .7 }
            : new PhongMaterial { DiffuseColor = color, AmbientColor = new Color4(.35f, .35f, .35f, 1) };
        var mesh = new MeshGeometryModel3D { Geometry = builder.Mesh(), Material = material, CullMode = SharpDX.Direct3D11.CullMode.None, IsTransparent=opacity<1 };
        meshElements.Add(mesh, builder.ElementIds); viewport.Items.Add(mesh);
    }
    LineGeometryModel3D AddLines(EdgeBuilder edges, Color color, double thickness = .8, IReadOnlyList<int>? ids = null)
    {
        if(clipping is {} clip)
        {
            var geometry=edges.Geometry();var clipped=new EdgeBuilder();var kept=new List<int>();
            for(int i=0;i<geometry.Positions!.Count;i+=2) { var a=geometry.Positions[i];var b=geometry.Positions[i+1]; if(clip.Segment(ref a,ref b)) { clipped.Line(a,b);if(ids!=null)kept.Add(ids[i/2]); } }
            edges=clipped;if(ids!=null)ids=kept;
        }
        var line = new LineGeometryModel3D { Geometry = edges.Geometry(), Color = color, Thickness = thickness, Smoothness = 1, DepthBias = -5, IsHitTestVisible = ids != null };
        viewport.Items.Add(line); if (ids != null) lineElements.Add(line, ids); return line;
    }
    public SceneStatistics Render(SceneOptions options)
    {
        if (disposed || model == null) return new(TimeSpan.Zero, 0, 0);
        lastOptions = options;
        clipping=options.Exploration?.Clip is {} plane?new SceneClip(new Vector3((float)plane.X,(float)plane.Y,(float)plane.Z),(float)(plane.Distance-plane.X*modelOrigin.X-plane.Y*modelOrigin.Y-plane.Z*modelOrigin.Z)):null;
        labelBounds.Clear();hoverLine=null;
        var clock = System.Diagnostics.Stopwatch.StartNew(); int view = options.Mode;
        var values = options.Values.Where(v => v.Node > 0).ToDictionary(v => (v.Element, v.Node), v => v.Value);
        var samples = options.Values.ToLookup(v => v.Element);
        double min = options.ColorMinimum, max = options.ColorMaximum;

        meshElements.Clear(); lineElements.Clear();
        viewport.Items.Clear(); viewport.Items.Add(new AmbientLight3D { Color = Color.FromRgb(160, 160, 160) }); viewport.Items.Add(new DirectionalLight3D { Color = Colors.White, Direction = new Vector3D(2, 1, -1) }); viewport.Items.Add(new DirectionalLight3D { Color = Color.FromRgb(180, 190, 210), Direction = new Vector3D(-1, -2, -3) });
        var faces = new SurfaceBuilder(); var missing = new SurfaceBuilder(); var solids = new Dictionary<int, SurfaceBuilder>(); var bars = new SurfaceBuilder(); var edges = new EdgeBuilder(); var barEdges = new EdgeBuilder(); var contours = new EdgeBuilder(); int missingElements = 0;
        var highlight = new EdgeBuilder(); var axesX = new EdgeBuilder(); var axesY = new EdgeBuilder(); var axesZ = new EdgeBuilder();
        var frames = model.LocalFrames?.ToDictionary(f => f.Element);
        var beamColors = new Dictionary<int, EdgeBuilder>(); var beamIds = new List<int>();
        var beamColorIds = new Dictionary<int, List<int>>();
        var categoryFaces = new Dictionary<string, SurfaceBuilder>();
        var categoryEdges = new Dictionary<string, EdgeBuilder>();
        var categoryLineIds = new Dictionary<string, List<int>>();
        bool wireframe = view != 2 && options.Display.SurfaceMode == 2;
        SurfaceBuilder CategoryMesh(string color) { if (!categoryFaces.TryGetValue(color, out var mesh)) categoryFaces[color] = mesh = new(); return mesh; }
        void CategoryLine(string color, Vector3 first, Vector3 last, int id)
        {
            if (!categoryEdges.TryGetValue(color, out var line)) { categoryEdges[color] = line = new(); categoryLineIds[color] = []; }
            line.Line(first, last); categoryLineIds[color].Add(id);
        }
        foreach (var e in model.Elements.Where(e => options.Visible == null || options.Visible.Contains(e.Id)))
        {
            if (view == 2 && !options.ShowContext && e.Type != options.Family) continue;
            var p = e.Nodes.Select(n => points[n]).ToArray();
            string elementColor = options.Display.Colors.TryGetValue(e.Id, out var style) ? style.Color : "#71B6E8";
            if (options.Selection.Contains(e.Id))
                for (int i = 0; i < (p.Length == 2 ? 1 : p.Length); i++) highlight.Line(p[i], p[(i + 1) % p.Length]);
            if (options.ShowLocalAxes && frames?.TryGetValue(e.Id, out var frame) == true && (!options.OverlaysOnlySelection || options.Selection.Contains(e.Id)))
            {
                var origin = p.Aggregate(Vector3.Zero, (a, b) => a + b) / p.Length;
                float size = (float)Math.Min(extent * .025, Vector3.Distance(p[0], p[1]) * .32);
                Vector3 Direction(ModelDirection d) => new((float)d.X, (float)d.Y, (float)d.Z);
                axesX.Arrow(origin, origin + Direction(frame.X) * size); axesY.Arrow(origin, origin + Direction(frame.Y) * size); axesZ.Arrow(origin, origin + Direction(frame.Z) * size);
            }
            if (ElementFamilies.IsLine(e.Type))
            {
                if (view == 2) { barEdges.Line(p[0], p[1]); beamIds.Add(e.Id); }
                else CategoryLine(elementColor, p[0], p[1], e.Id);
                var s = model.Sections.First(s => s.Id == e.Property);
                if (view == 2 && options.Family == e.Type)
                {
                    var beamSamples = samples[e.Id].Select(v => (Station: v.Station ?? (v.Node == e.Nodes[0] ? 0 : 1), v.Value)).OrderBy(v => v.Station).ToArray();
                    if (beamSamples.Length > 0)
                    {
                        if (beamSamples.Length == 1) beamSamples = [(0, beamSamples[0].Value), (1, beamSamples[0].Value)];
                        for (int sample = 0; sample + 1 < beamSamples.Length; sample++)
                        for (int i = 0; i < 8; i++)
                        {
                            var first = beamSamples[sample]; var last = beamSamples[sample + 1];
                            double value = first.Value + (last.Value - first.Value) * (i + .5) / 8;
                            int color = (int)Math.Clamp((value - min) / (max == min ? 1 : max - min) * 63, 0, 63);
                            if (!beamColors.TryGetValue(color, out var line)) { beamColors.Add(color, line = new()); beamColorIds[color] = []; }
                            beamColorIds[color].Add(e.Id);
                            line.Line(Vector3.Lerp(p[0], p[1], (float)(first.Station + (last.Station - first.Station) * i / 8)),
                                Vector3.Lerp(p[0], p[1], (float)(first.Station + (last.Station - first.Station) * (i + 1) / 8)));
                        }
                    }
                    else missingElements++;
                }
                if (view == 1 && !wireframe && e.Type != "CABLE" && s.Centered && s.Shape == "SB")
                {
                    var axis = Vector3.Normalize(p[1] - p[0]); var u = Vector3.Normalize(Vector3.Cross(axis, Math.Abs(axis.Z) > .9 ? Vector3.UnitY : Vector3.UnitZ)); var v = Vector3.Cross(axis, u); float angle = (float)(e.Angle * Math.PI / 180); var ur = u * MathF.Cos(angle) + v * MathF.Sin(angle); var vr = -u * MathF.Sin(angle) + v * MathF.Cos(angle); u = ur * (float)s.Width / 2; v = vr * (float)s.Height / 2;
                    if (frames?.TryGetValue(e.Id, out var beamFrame) == true)
                    { u = new Vector3((float)beamFrame.Y.X, (float)beamFrame.Y.Y, (float)beamFrame.Y.Z) * (float)s.Width / 2; v = new Vector3((float)beamFrame.Z.X, (float)beamFrame.Z.Y, (float)beamFrame.Z.Z) * (float)s.Height / 2; }
                    CategoryMesh(elementColor).Prism(new[] { p[0] - u - v, p[0] + u - v, p[0] + u + v, p[0] - u + v }, p[1] - p[0], e.Id);
                }
                continue;
            }
            var normal = Vector3.Normalize(Vector3.Cross(p[1] - p[0], p[2] - p[0]));
            if (wireframe)
            { for (int i = 0; i < p.Length; i++) CategoryLine(elementColor, p[i], p[(i + 1) % p.Length], e.Id); continue; }
            if (view == 1)
            {
                var prop = model.Plates.First(t => t.Id == e.Property); var solid = CategoryMesh(elementColor);
                solid.Prism(p.Select(x => x + normal * (float)(prop.Offset - prop.Thickness / 2)).ToArray(), normal * (float)prop.Thickness, e.Id, options.Visible == null ? plateSides[e.Id] : null); continue;
            }
            bool available = e.Nodes.All(n => values.ContainsKey((e.Id, n))); if (view == 2 && options.Family == "PLATE" && !available) missingElements++;
            var target = view == 0 ? CategoryMesh(elementColor) : !available ? missing : faces;
            var nodal = e.Nodes.Select(n => available ? values[(e.Id, n)] : 0).ToArray();
            Contours.Surface(target, contours, p, nodal, e.Id, min, max, view == 2 && available ? options.Subdivisions : 1, view == 2 && available && options.ShowIsolines, Math.Clamp(options.IsolineCount, 4, 20));
            if (options.ShowMesh && (view == 2 || options.Display.SurfaceMode == 0)) for (int i = 0; i < p.Length; i++) edges.Line(p[i], p[(i + 1) % p.Length]);
        }
        foreach (var category in categoryFaces) { var c = (Color)ColorConverter.ConvertFromString(category.Key); AddMesh(category.Value, new Color4(c.R / 255f, c.G / 255f, c.B / 255f, 1)); }
        AddMesh(faces, new Color4(.3f, .7f, .75f, 1), view == 2); AddMesh(missing, new Color4(.5f, .5f, .5f, 1));
        AddLines(edges, Color.FromRgb(35, 61, 77), options.Display.LineWidth);
        AddLines(barEdges, Color.FromRgb(153, 166, 181), 1.6, beamIds);
        foreach (var category in categoryEdges) AddLines(category.Value, (Color)ColorConverter.ConvertFromString(category.Key), options.Display.LineWidth * 1.6, categoryLineIds[category.Key]);
        foreach (var pair in beamColors) { var c = Contours.DisplayColor(pair.Key / 63.0, options.Palette,options.Exploration?.ReversePalette??false,options.Exploration?.Bands??0); AddLines(pair.Value, Color.FromRgb((byte)Math.Round(c.Red*255), (byte)Math.Round(c.Green*255), (byte)Math.Round(c.Blue*255)), 4, beamColorIds[pair.Key]); }
        AddLines(contours, Color.FromArgb(135, 25, 47, 57), Math.Clamp(options.IsolineWidth, .25, 1.5));
        AddLines(highlight, Color.FromRgb(255, 214, 102), 3);
        AddLines(axesX, Color.FromRgb(236, 116, 111), 2); AddLines(axesY, Color.FromRgb(111, 206, 157), 2); AddLines(axesZ, Color.FromRgb(113, 177, 247), 2);
        DrawBoundaries(options);
        DrawNodeAxes(options);
        DrawExploration(options); DrawDisplayOverlays(options);
        return new(clock.Elapsed, viewport.Items.OfType<MeshGeometryModel3D>().Sum(m => m.Geometry?.Indices?.Count / 3 ?? 0), missingElements);
    }
    private void DrawBoundaries(SceneOptions options)
    {
        var restraints = new EdgeBuilder(); var constraints = new EdgeBuilder(); var links = new EdgeBuilder(); var releases = new EdgeBuilder();
        float size = (float)(extent * .008);
        var visibleNodes = options.Visible == null ? null : model!.Elements.Where(e => options.Visible.Contains(e.Id)).SelectMany(e => e.Nodes).ToHashSet();
        foreach (var boundary in model!.Boundaries ?? [])
        {
            if (visibleNodes != null && !boundary.Nodes.Any(visibleNodes.Contains)) continue;
            if (options.OverlaysOnlySelection && !boundary.Nodes.Any(options.SelectedNodes.Contains) && !(boundary.Element is int element && options.Selection.Contains(element))) continue;
            var p = points[boundary.Nodes[0]];
            if (boundary.Kind == "RESTRAIN" && options.ShowRestraints)
            {
                var a = p + new Vector3(-size, -size, -size * 1.6f); var b = p + new Vector3(size, -size, -size * 1.6f);
                var c = p + new Vector3(size, size, -size * 1.6f); var d = p + new Vector3(-size, size, -size * 1.6f);
                restraints.Line(p, a); restraints.Line(p, b); restraints.Line(p, c); restraints.Line(p, d);
                restraints.Line(a,b); restraints.Line(b,c); restraints.Line(c,d); restraints.Line(d,a);
            }
            if (boundary.Kind == "CONSTRAINT" && options.ShowConstraints && boundary.Nodes.Length == 2)
                constraints.Line(p, points[boundary.Nodes[1]]);
            if (boundary.Kind == "LINK" && options.ShowLinks && boundary.Nodes.Length == 2)
                links.Line(p, points[boundary.Nodes[1]]);
            if (boundary.Kind == "RELEASE" && options.ShowReleases)
                for (int i = 0; i < 24; i++)
                    releases.Line(p + new Vector3(MathF.Cos(i * MathF.PI / 12), MathF.Sin(i * MathF.PI / 12), 0) * size,
                        p + new Vector3(MathF.Cos((i + 1) * MathF.PI / 12), MathF.Sin((i + 1) * MathF.PI / 12), 0) * size);
        }
        AddLines(restraints, Color.FromRgb(111,206,157), 2.5); AddLines(constraints, Color.FromRgb(201,155,238), 1.2);
        AddLines(links, Color.FromRgb(84,211,221), 2); AddLines(releases, Color.FromRgb(255,204,112), 2);
    }
    private void DrawNodeAxes(SceneOptions options)
    {
        if (!options.ShowNodeAxes || model!.NodeFrames == null) return;
        var x = new EdgeBuilder(); var y = new EdgeBuilder(); var z = new EdgeBuilder();
        var visibleNodes = options.Visible == null ? null : model.Elements.Where(e => options.Visible.Contains(e.Id)).SelectMany(e => e.Nodes).ToHashSet();
        float size = (float)(extent * .009);
        static Vector3 Direction(ModelDirection value) => new((float)value.X, (float)value.Y, (float)value.Z);
        foreach (var frame in model.NodeFrames)
        {
            if (visibleNodes != null && !visibleNodes.Contains(frame.Node) || options.OverlaysOnlySelection && !options.SelectedNodes.Contains(frame.Node)) continue;
            var origin = points[frame.Node];
            x.Arrow(origin, origin + Direction(frame.X) * size); y.Arrow(origin, origin + Direction(frame.Y) * size); z.Arrow(origin, origin + Direction(frame.Z) * size);
        }
        AddLines(x, Color.FromRgb(236,116,111), 1.5); AddLines(y, Color.FromRgb(111,206,157), 1.5); AddLines(z, Color.FromRgb(113,177,247), 1.5);
    }
    public int? ElementAt(HelixToolkit.SharpDX.HitTestResult? hit)
    {
        if (hit?.ModelHit is MeshGeometryModel3D mesh && meshElements.TryGetValue(mesh, out var ids) && hit.TriangleIndices is { } triangle)
        { int index = triangle.Item1 / 3; if (index >= 0 && index < ids.Count) return ids[index]; }
        if (hit?.ModelHit is LineGeometryModel3D line && lineElements.TryGetValue(line, out var beamIds) && hit is LineHitTestResult lineHit && lineHit.LineIndex >= 0 && lineHit.LineIndex < beamIds.Count)
            return beamIds[lineHit.LineIndex];
        return null;
    }
    public void Fit(bool includeHidden = false)
    {
        if (model == null || disposed) return;
        var visiblePoints = !includeHidden && lastOptions?.Visible is { } visible
            ? model.Elements.Where(e => visible.Contains(e.Id)).SelectMany(e => e.Nodes).Distinct().Select(id => points[id]).ToArray() : points.Values.ToArray();
        if (visiblePoints.Length == 0) return;
        var min = visiblePoints.Aggregate(Vector3.Min); var max = visiblePoints.Aggregate(Vector3.Max); var center = (min + max) / 2;
        double radius = Math.Max(.1, (max - min).Length());
        var d = new Vector3D(-1.35, -1.15, .95); d.Normalize(); d *= radius * 1.35;
        var position = new Point3D(center.X + d.X, center.Y + d.Y, center.Z + d.Z);
        viewport.Camera = viewport.Orthographic
            ? new HelixToolkit.Wpf.SharpDX.OrthographicCamera { Position = position, LookDirection = -d, UpDirection = new Vector3D(0, 0, 1), Width = radius * 1.2, NearPlaneDistance = .01, FarPlaneDistance = Math.Max(10000, extent * 10) }
            : new PerspectiveCamera { Position = position, LookDirection = -d, UpDirection = new Vector3D(0, 0, 1), FieldOfView = 42, NearPlaneDistance = .01, FarPlaneDistance = Math.Max(10000, extent * 10) };
    }
    public void SetCameraPreset(string preset)
    {
        var direction = preset switch
        {
            "front" => new Vector3D(0,1,0), "back" => new Vector3D(0,-1,0),
            "left" => new Vector3D(1,0,0), "right" => new Vector3D(-1,0,0),
            "top" => new Vector3D(0,0,-1), "bottom" => new Vector3D(0,0,1),
            "iso-ne" => new Vector3D(-1,-1,-Math.Sqrt(2)), "iso-nw" => new Vector3D(1,-1,-Math.Sqrt(2)),
            "iso-se" => new Vector3D(-1,1,-Math.Sqrt(2)), _ => new Vector3D(1,1,-Math.Sqrt(2))
        };
        viewport.ChangeDirection(direction, preset is "top" or "bottom" ? new Vector3D(0,1,0) : new Vector3D(0,0,1), 250);
    }
    public void SaveImage(string path) => viewport.SaveScreen(path);
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        viewport.Items.Clear();
        viewport.EffectsManager?.Dispose();
        viewport.EffectsManager = null;
    }
}
