using System.Numerics;
using System.Windows.Media;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    private Dictionary<int, Vector3> undeformedPoints = [];
    private HashSet<int> incompleteDeformations = [];
    private bool deformed;

    static bool MatchesFamily(ModelElement element, SceneOptions options) => options.Family == "*" || element.Type == options.Family;
    Vector3[] ElementPoints(ModelElement element) => element.Nodes.Select(n =>
        incompleteDeformations.Contains(element.Id) ? undeformedPoints[n] : points[n]).ToArray();
    Vector3 PlateNormal(ModelElement element, Vector3[] p)
    {
        // Only translations are imported. Preserve the original thickness/offset direction;
        // inventing a new section rotation would open cracks between adjacent deformed plates.
        if (deformed) p = element.Nodes.Select(n => undeformedPoints[n]).ToArray();
        return Vector3.Normalize(Vector3.Cross(p[1] - p[0], p[2] - p[0]));
    }
    internal Vector3 ProbePoint(ModelProbe probe)
    {
        if (!deformed) return new((float)(probe.X - modelOrigin.X), (float)(probe.Y - modelOrigin.Y), (float)(probe.Z - modelOrigin.Z));
        var element = model!.Elements.FirstOrDefault(e => e.Id == probe.Element);
        if (element is null) return new((float)(probe.X - modelOrigin.X), (float)(probe.Y - modelOrigin.Y), (float)(probe.Z - modelOrigin.Z));
        var source = incompleteDeformations.Contains(element.Id) ? undeformedPoints : points;
        return probe.Node > 0 && source.TryGetValue(probe.Node, out var p) ? p : Vector3.Lerp(source[element.Nodes[0]], source[element.Nodes[^1]], (float)(probe.Station ?? 0));
    }

    void UpdateDeformedPoints(SceneOptions options)
    {
        incompleteDeformations.Clear();
        deformed = options.Exploration?.Displacements is { Count: > 0 };
        if (!deformed) { points = undeformedPoints; return; }
        var settings = options.Exploration!;
        double scale = double.IsFinite(settings.DeformationScale) ? Math.Clamp(settings.DeformationScale, -1_000_000, 1_000_000) : 0;
        var moved = new Dictionary<int, Vector3>();
        foreach (var d in settings.Displacements!)
        {
            if (!undeformedPoints.TryGetValue(d.Node, out var p)) continue;
            var target = new Vector3((float)(p.X + d.X * scale), (float)(p.Y + d.Y * scale), (float)(p.Z + d.Z * scale));
            if (float.IsFinite(target.X) && float.IsFinite(target.Y) && float.IsFinite(target.Z)) moved[d.Node] = target;
        }
        points = undeformedPoints.ToDictionary(p => p.Key, p => moved.GetValueOrDefault(p.Key, p.Value));
        foreach (var e in model!.Elements.Where(e => e.Nodes.Any(n => !moved.ContainsKey(n)))) incompleteDeformations.Add(e.Id);
    }
    void DrawUndeformed(SceneOptions options)
    {
        if (!deformed || options.Exploration is not { ShowUndeformed: true } settings) return;
        var ghost = new EdgeBuilder();
        foreach (var element in model!.Elements.Where(e => (options.Visible == null || options.Visible.Contains(e.Id)) &&
            (!options.ShowResults || options.ShowContext || MatchesFamily(e, options))))
        {
            var p = element.Nodes.Select(n => undeformedPoints[n]).ToArray();
            bool[]? sides = !settings.UndeformedMesh && element.Type == "PLATE" ? plateSides[element.Id] : null;
            if (!options.Extruded && sides != null)
            {
                for (int i = 0; i < p.Length; i++) if (sides[i]) ghost.Line(p[i], p[(i + 1) % p.Length]);
            }
            else ElementOutline(ghost, element, p, options.Extruded, sides);
        }
        // Reference geometry is never selectable and does not participate in color limits.
        AddLines(ghost, Color.FromArgb((byte)(255 * Math.Clamp(settings.UndeformedOpacity, .05, 1)), 183, 197, 212), 1);
    }
}
