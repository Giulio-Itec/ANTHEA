using System.Numerics;
using System.Windows.Media;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Color = System.Windows.Media.Color;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    private void DrawOverlayDetails(SceneOptions options)
    {
        if (!options.ElementDirections && !options.BoundaryLabels) return;
        var elements = model!.Elements.Where(e => (options.Visible == null || options.Visible.Contains(e.Id)) &&
            (!options.ShowResults || options.ShowContext || MatchesFamily(e, options))).ToArray();
        if (options.ElementDirections)
        {
            var arrows = new EdgeBuilder();
            foreach (var element in elements)
            {
                if (options.OverlaysOnlySelection && !options.Selection.Contains(element.Id)) continue;
                if (incompleteDeformations.Contains(element.Id)) continue;
                var p = ElementPoints(element);
                var direction = p[1] - p[0];
                float length = direction.Length();
                if (length < 1e-8f) continue;
                var center = (p[0] + p[1]) / 2;
                var half = direction / length * (float)Math.Min(length * .4, extent * .015 * options.OverlayScale);
                arrows.Arrow(center - half, center + half);
            }
            AddLines(arrows, Color.FromRgb(103, 225, 227), 2.2);
        }
        if (!options.BoundaryLabels) return;
        var nodes = options.Visible == null && (!options.ShowResults || options.ShowContext) ? null : elements.SelectMany(e => e.Nodes).ToHashSet();
        bool Enabled(ModelBoundary b) => b.Kind switch
        {
            "RESTRAIN" => options.ShowRestraints, "CONSTRAINT" => options.ShowConstraints,
            "LINK" => options.ShowLinks, "RELEASE" => options.ShowReleases, _ => false
        };
        bool Selected(ModelBoundary b) => b.Nodes.Any(options.SelectedNodes.Contains) || b.Element is int id && options.Selection.Contains(id);
        var groups = (model.Boundaries ?? []).Where(b => Enabled(b) && (nodes == null || b.Nodes.Any(nodes.Contains)) && (!options.OverlaysOnlySelection || Selected(b)))
            .GroupBy(b => (b.Kind, Node: b.Nodes[0], b.Element, b.Dofs))
            .OrderByDescending(g => g.Any(Selected)).ThenBy(g => g.Key.Kind is "RESTRAIN" or "RELEASE" ? 0 : 1)
            .ThenBy(g => g.Key.Node).ThenBy(g => g.Key.Element);
        var labels = new BillboardText3D(); var leaders = new EdgeBuilder();
        foreach (var group in groups)
        {
            if (labels.TextInfo.Count >= 40) break;
            var p = points[group.Key.Node];
            string details = string.IsNullOrWhiteSpace(group.Key.Dofs) ? "Informazioni non disponibili" : group.Key.Dofs;
            if (details.Length > 180) details = details[..177] + "...";
            var parts = details.Split(',', StringSplitOptions.TrimEntries);
            details = string.Join("\n", parts.Chunk(3).Select(chunk => string.Join(", ", chunk)));
            string caption = $"{group.Key.Kind} - N {group.Key.Node}" + (group.Key.Element is int element ? $" / E {element}" : "") +
                (group.Count() > 1 ? $" ({group.Count()} collegamenti)" : "") + "\n" + details;
            if (!ProbePlacement(new ModelProbe(0, 0, null, caption, 0, 0, 0), p, out var offset)) continue;
            leaders.Line(p, LabelLeaderEnd(p, offset));
            labels.TextInfo.Add(new TextInfo(caption, p)
            {
                Foreground = new Color4(.92f, .96f, 1, 1), Background = new Color4(.045f, .075f, .11f, .96f),
                Scale = (float)(.55 * options.RasterScale), Offset = offset * (float)options.RasterScale
            });
        }
        if (labels.TextInfo.Count == 0) return;
        AddLines(leaders, Color.FromRgb(189, 210, 232), 1);
        viewport.Items.Add(new BillboardTextModel3D { Geometry = labels, FixedSize = true, IsHitTestVisible = false, DepthBias = -10000 });
    }
}
