using System.Numerics;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    private bool preparingAnimation;
    private SceneOptions? animationOptions;
    private readonly List<GeometryMorph> animationGeometry = [];
    private sealed record GeometryMorph(Geometry3D Geometry, Vector3[] Start, Vector3[] End, Vector3[]? StartNormals, Vector3[]? EndNormals);

    internal int AnimationVertexCount => animationGeometry.Sum(g => g.Start.Length);
    internal bool PrepareAnimation(SceneOptions options)
    {
        if (options.Exploration is not { Displacements.Count: > 0, Clip: null } settings) return false;
        if(settings.ClipPlanes is {Count:>0})return false;
        preparingAnimation = true;
        try
        {
            // Keep topology, scalar textures, materials and picking IDs stable throughout the cycle.
            var display = options.Display with { NodeIds = false, ElementIds = false, PropertyIds = false };
            var frame = options with { Display = display, BoundaryLabels = false, Exploration = settings with { Probes = [], DiagramValues = false } };
            Render(frame with { Exploration = frame.Exploration! with { DeformationScale = 0 } });
            Geometry3D[] Geometries() => viewport.Items.OfType<GeometryModel3D>().Where(m => m is MeshGeometryModel3D or LineGeometryModel3D).Select(m => m.Geometry).OfType<Geometry3D>().ToArray();
            var original = Geometries().Select(g => (Type: g.GetType(), Positions: g.Positions?.ToArray() ?? [], Normals: (g as MeshGeometry3D)?.Normals?.ToArray(), Indices: g.Indices?.ToArray() ?? [])).ToArray();
            Render(frame);
            var target = Geometries(); animationGeometry.Clear();
            if (original.Length != target.Length || original.Where((g, i) => g.Type != target[i].GetType() || g.Positions.Length != (target[i].Positions?.Count ?? 0) || !g.Indices.SequenceEqual(target[i].Indices ?? [])).Any())
                return false;
            for (int i = 0; i < target.Length; i++)
                if (original[i].Positions.Length > 0)
                    animationGeometry.Add(new(target[i], original[i].Positions, target[i].Positions!.ToArray(), original[i].Normals, (target[i] as MeshGeometry3D)?.Normals?.ToArray()));
            animationOptions = frame;
            return true;
        }
        finally { preparingAnimation = false; }
    }
    internal void RenderAnimationFrame(double phase)
    {
        if (animationOptions?.Exploration is not {} settings) return;
        float t = (float)Math.Clamp(phase, 0, 1);
        foreach (var morph in animationGeometry)
        {
            for (int i = 0; i < morph.Start.Length; i++) morph.Geometry.Positions![i] = Vector3.Lerp(morph.Start[i], morph.End[i], t);
            if (morph.Geometry is MeshGeometry3D mesh && morph.StartNormals is {} first && morph.EndNormals is {} last && first.Length == last.Length)
                for (int i = 0; i < first.Length; i++)
                {
                    var normal = Vector3.Lerp(first[i], last[i], t);
                    mesh.Normals![i] = normal.LengthSquared() > 1e-12 ? Vector3.Normalize(normal) : last[i];
                }
            morph.Geometry.UpdateVertices(); morph.Geometry.UpdateBounds(); morph.Geometry.ClearOctree();
        }
        lastOptions = animationOptions with { Exploration = settings with { DeformationScale = settings.DeformationScale * phase } };
        UpdateDeformedPoints(lastOptions);
        viewport.InvalidateRender();
    }
    void ClearAnimation()
    {
        animationGeometry.Clear(); animationOptions = null;
    }
}
