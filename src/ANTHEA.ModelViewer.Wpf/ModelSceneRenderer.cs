using ANTHEA.ModelWorkspace;
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

internal sealed record SceneOptions(int Mode, string? Case, string Component, int Subdivisions, bool ShowMesh, bool ShowIsolines);
internal sealed record SceneStatistics(TimeSpan Preparation, int Triangles, int MissingElements);

/// <summary>Owns the Helix scene and GPU resources. No dialogs, document writes or verification calculations.</summary>
internal sealed class ModelSceneRenderer : IDisposable
{
    private readonly Viewport3DX viewport;
    private ModelSnapshot? model;
    private Dictionary<int, Vector3> points = new();
    private readonly Dictionary<int, bool[]> plateSides = new();
    private double extent = 1;
    private bool disposed;
    public ModelSceneRenderer(Viewport3DX viewport) { this.viewport = viewport; viewport.EffectsManager = new DefaultEffectsManager(); }
    public void SetModel(ModelSnapshot snapshot)
    {
        model = snapshot;
        double cx = (snapshot.Nodes.Min(n => n.X) + snapshot.Nodes.Max(n => n.X)) / 2, cy = (snapshot.Nodes.Min(n => n.Y) + snapshot.Nodes.Max(n => n.Y)) / 2, cz = (snapshot.Nodes.Min(n => n.Z) + snapshot.Nodes.Max(n => n.Z)) / 2;
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
        Material material = contour ? new ColorStripeMaterial { DiffuseColor = new Color4(1, 1, 1, 1), ColorStripeX = Enumerable.Range(0, 256).Select(i => Contours.Color(i / 255.0)).ToList(), ColorStripeXEnabled = true, ColorStripeYEnabled = false } : new PhongMaterial { DiffuseColor = color, AmbientColor = new Color4(.35f, .35f, .35f, 1) };
        viewport.Items.Add(new MeshGeometryModel3D { Geometry = builder.Mesh(), Material = material, CullMode = SharpDX.Direct3D11.CullMode.None });
    }
    void AddLines(EdgeBuilder edges, Color color, double thickness = .8) => viewport.Items.Add(new LineGeometryModel3D { Geometry = edges.Geometry(), Color = color, Thickness = thickness, Smoothness = 1, DepthBias = -5 });
    public SceneStatistics Render(SceneOptions options)
    {
        if (disposed || model == null) return new(TimeSpan.Zero, 0, 0);
        var clock = System.Diagnostics.Stopwatch.StartNew(); int view = options.Mode; string field = options.Component; int column = Array.IndexOf(ModelSnapshot.Components, field);
        var selected = model.Results.FirstOrDefault(r => r.Name == options.Case); var values = selected?.Values.ToDictionary(v => (v.Element, v.Node), v => v.Values[column]) ?? new();
        double min = values.Count > 0 ? values.Values.Min() : 0, max = values.Count > 0 ? values.Values.Max() : 0;

        viewport.Items.Clear(); viewport.Items.Add(new AmbientLight3D { Color = Color.FromRgb(160, 160, 160) }); viewport.Items.Add(new DirectionalLight3D { Color = Colors.White, Direction = new Vector3D(2, 1, -1) }); viewport.Items.Add(new DirectionalLight3D { Color = Color.FromRgb(180, 190, 210), Direction = new Vector3D(-1, -2, -3) });
        var faces = new SurfaceBuilder(); var missing = new SurfaceBuilder(); var solids = new Dictionary<int, SurfaceBuilder>(); var bars = new SurfaceBuilder(); var edges = new EdgeBuilder(); var barEdges = new EdgeBuilder(); var contours = new EdgeBuilder(); int missingElements = 0;
        foreach (var e in model.Elements)
        {
            var p = e.Nodes.Select(n => points[n]).ToArray();
            if (e.Type == "BEAM")
            {
                barEdges.Line(p[0], p[1]); var s = model.Sections.First(s => s.Id == e.Property);
                if (view == 1 && s.Centered && s.Shape == "SB")
                {
                    var axis = Vector3.Normalize(p[1] - p[0]); var u = Vector3.Normalize(Vector3.Cross(axis, Math.Abs(axis.Z) > .9 ? Vector3.UnitY : Vector3.UnitZ)); var v = Vector3.Cross(axis, u); float angle = (float)(e.Angle * Math.PI / 180); var ur = u * MathF.Cos(angle) + v * MathF.Sin(angle); var vr = -u * MathF.Sin(angle) + v * MathF.Cos(angle); u = ur * (float)s.Width / 2; v = vr * (float)s.Height / 2;
                    bars.Prism(new[] { p[0] - u - v, p[0] + u - v, p[0] + u + v, p[0] - u + v }, p[1] - p[0], e.Id);
                }
                continue;
            }
            var normal = Vector3.Normalize(Vector3.Cross(p[1] - p[0], p[2] - p[0]));
            if (view == 1)
            {
                var prop = model.Plates.First(t => t.Id == e.Property); if (!solids.TryGetValue(e.Property, out var solid)) solids[e.Property] = solid = new SurfaceBuilder();
                solid.Prism(p.Select(x => x + normal * (float)(prop.Offset - prop.Thickness / 2)).ToArray(), normal * (float)prop.Thickness, e.Id, plateSides[e.Id]); continue;
            }
            bool available = e.Nodes.All(n => values.ContainsKey((e.Id, n))); if (view == 2 && !available) missingElements++;
            var target = view == 2 && !available ? missing : faces;
            var nodal = e.Nodes.Select(n => available ? values[(e.Id, n)] : 0).ToArray();
            Contours.Surface(target, contours, p, nodal, e.Id, min, max, view == 2 && available ? options.Subdivisions : 1, view == 2 && available && options.ShowIsolines);
            if (options.ShowMesh) for (int i = 0; i < p.Length; i++) edges.Line(p[i], p[(i + 1) % p.Length]);
        }
        if (view == 1) { foreach (var solid in solids) AddMesh(solid.Value, Contours.Color((solid.Key % 8) / 8.0)); AddMesh(bars, new Color4(.95f, .68f, .3f, 1)); }
        else { AddMesh(faces, new Color4(.3f, .7f, .75f, 1), view == 2); if (missing.Indices.Count > 0) AddMesh(missing, new Color4(.5f, .5f, .5f, 1)); }
        AddLines(edges, Color.FromRgb(35, 61, 77)); AddLines(barEdges, Color.FromRgb(238, 186, 94), 1.2); AddLines(contours, Colors.Black, 1);
        return new(clock.Elapsed, viewport.Items.OfType<MeshGeometryModel3D>().Sum(m => m.Geometry?.Indices?.Count / 3 ?? 0), missingElements);
    }
    public void Fit() { if (model == null || disposed) return; var d = new Vector3D(-1.35, -1.15, .95); d.Normalize(); d *= extent * 1.35; viewport.Camera = new PerspectiveCamera { Position = new Point3D(d.X, d.Y, d.Z), LookDirection = -d, UpDirection = new Vector3D(0, 0, 1), FieldOfView = 42, NearPlaneDistance = .01, FarPlaneDistance = Math.Max(10000, extent * 10) }; }
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
