using System.Numerics;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed class SurfaceBuilder
{
    internal Vector3Collection Positions = new(), Normals = new();
    internal Vector2Collection UV = new();
    internal IntCollection Indices = new();
    internal List<int> ElementIds = new();
    internal void Triangle(Vector3 a, Vector3 b, Vector3 c, float ua = 0, float ub = 0, float uc = 0, int id = 0)
    {
        var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        if (!float.IsFinite(normal.X)) return;
        int i = Positions.Count;
        Positions.Add(a); Positions.Add(b); Positions.Add(c);
        Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
        UV.Add(new Vector2(ua, 0)); UV.Add(new Vector2(ub, 0)); UV.Add(new Vector2(uc, 0));
        Indices.Add(i); Indices.Add(i + 1); Indices.Add(i + 2); ElementIds.Add(id);
    }
    internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int id = 0) { Triangle(a, b, c, id: id); Triangle(a, c, d, id: id); }
    internal MeshGeometry3D Mesh() => new() { Positions = Positions, Normals = Normals, Indices = Indices, TextureCoordinates = UV };
    internal void Prism(Vector3[] polygon, Vector3 extrusion, int id = 0, bool[]? sides = null)
    {
        for (int i = 1; i + 1 < polygon.Length; i++) { Triangle(polygon[0], polygon[i + 1], polygon[i], id: id); Triangle(polygon[0] + extrusion, polygon[i] + extrusion, polygon[i + 1] + extrusion, id: id); }
        for (int i = 0; i < polygon.Length; i++) { if (sides != null && !sides[i]) continue; int j = (i + 1) % polygon.Length; Quad(polygon[i], polygon[j], polygon[j] + extrusion, polygon[i] + extrusion, id); }
    }
}
internal sealed class EdgeBuilder
{
    readonly Vector3Collection positions = new(); readonly IntCollection indices = new();
    internal int Count => positions.Count / 2;
    internal void Line(Vector3 a, Vector3 b) { int i = positions.Count; positions.Add(a); positions.Add(b); indices.Add(i); indices.Add(i + 1); }
    internal void Arrow(Vector3 a, Vector3 b)
    {
        Line(a, b); var delta = b - a;
        if (delta.LengthSquared() < 1e-16) return;
        var direction = Vector3.Normalize(delta);
        var side = Vector3.Normalize(Vector3.Cross(direction, Math.Abs(direction.Z) < .9 ? Vector3.UnitZ : Vector3.UnitY)) * delta.Length() * .12f;
        Line(b, b - delta * .25f + side); Line(b, b - delta * .25f - side);
    }
    internal LineGeometry3D Geometry() => new() { Positions = positions, Indices = indices };
}
internal static class Contours
{
    // Graphic interpolation only, independently within each element; no averaging over element boundaries.
    internal static void Surface(SurfaceBuilder mesh, EdgeBuilder lines, Vector3[] p, double[] values, int id, double min, double max, int subdivisions, bool isolines)
    {
        void Triangle(Vector3 a, Vector3 b, Vector3 c, double va, double vb, double vc)
        {
            float U(double value) => (float)((value - min) / (max == min ? 1 : max - min));
            mesh.Triangle(a, b, c, U(va), U(vb), U(vc), id);
            if (!isolines || max <= min) return;
            for (int level = 1; level < 12; level++) if (Segment([a, b, c], [va, vb, vc], min + (max - min) * level / 12) is { } segment) lines.Line(segment.A, segment.B);
        }
        if (p.Length == 3) { Triangle(p[0], p[1], p[2], values[0], values[1], values[2]); return; }
        (Vector3 p, double v) At(double u, double v)
        {
            double[] w = [(1 - u) * (1 - v), u * (1 - v), u * v, (1 - u) * v];
            Vector3 position = Vector3.Zero; double value = 0;
            for (int k = 0; k < 4; k++) { position += p[k] * (float)w[k]; value += values[k] * w[k]; }
            return (position, value);
        }
        for (int i = 0; i < subdivisions; i++) for (int j = 0; j < subdivisions; j++)
            {
                var a = At((double)i / subdivisions, (double)j / subdivisions); var b = At((double)(i + 1) / subdivisions, (double)j / subdivisions);
                var c = At((double)(i + 1) / subdivisions, (double)(j + 1) / subdivisions); var d = At((double)i / subdivisions, (double)(j + 1) / subdivisions);
                Triangle(a.p, b.p, c.p, a.v, b.v, c.v); Triangle(a.p, c.p, d.p, a.v, c.v, d.v);
            }
    }
    internal static (Vector3 A, Vector3 B)? Segment(Vector3[] p, double[] v, double level)
    {
        var hits = new List<Vector3>();
        for (int i = 0; i < 3; i++) { int j = (i + 1) % 3; if ((v[i] <= level && v[j] > level) || (v[j] <= level && v[i] > level)) hits.Add(Vector3.Lerp(p[i], p[j], (float)((level - v[i]) / (v[j] - v[i])))); }
        return hits.Count == 2 && Vector3.DistanceSquared(hits[0], hits[1]) > 1e-14 ? (hits[0], hits[1]) : null;
    }
    internal static Color4 Color(double t)
    {
        var colors = new[] { new Vector3(.12f, .23f, .62f), new Vector3(.08f, .64f, .85f), new Vector3(.25f, .78f, .65f), new Vector3(.97f, .90f, .36f), new Vector3(.91f, .25f, .16f) };
        t = Math.Clamp(t, 0, 1) * 4; int i = Math.Min(3, (int)t); var c = Vector3.Lerp(colors[i], colors[i + 1], (float)(t - i)); return new Color4(c.X, c.Y, c.Z, 1);
    }
}
