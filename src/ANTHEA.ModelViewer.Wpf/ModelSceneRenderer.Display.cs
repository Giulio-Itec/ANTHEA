using System.Numerics;
using System.Windows.Media;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Color = System.Windows.Media.Color;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    void DrawDisplayOverlays(SceneOptions options)
    {
        var visible = model!.Elements.Where(e => (options.Visible == null || options.Visible.Contains(e.Id)) &&
            (options.Mode != 2 || options.ShowContext || e.Type == options.Family)).ToArray();
        if (options.Display.HiddenWireframe && options.Visible != null)
        {
            var ghost = new EdgeBuilder();
            foreach (var element in model.Elements.Where(e => !options.Visible.Contains(e.Id)))
            {
                var p = element.Nodes.Select(n => points[n]).ToArray();
                for (int i = 0; i < (p.Length == 2 ? 1 : p.Length); i++) ghost.Line(p[i], p[(i + 1) % p.Length]);
            }
            // Inactive objects are context only: no hit testing, values, selection or legend contribution.
            AddLines(ghost, Color.FromRgb(67, 81, 99), .6);
        }
        if (options.Display.Nodes)
        {
            var dots = new EdgeBuilder(); float radius = (float)extent * .0009f;
            foreach (int node in visible.SelectMany(e => e.Nodes).Distinct())
            {
                var p = points[node]; dots.Line(p - Vector3.UnitX * radius, p + Vector3.UnitX * radius);
                dots.Line(p - Vector3.UnitY * radius, p + Vector3.UnitY * radius);
                dots.Line(p - Vector3.UnitZ * radius, p + Vector3.UnitZ * radius);
            }
            AddLines(dots, Color.FromRgb(207, 220, 237), 2);
        }
        if (!options.Display.NodeIds && !options.Display.ElementIds && !options.Display.PropertyIds) return;
        if (options.Display.LabelsSelectedOnly) visible = visible.Where(e => options.Selection.Contains(e.Id)).ToArray();
        var text = new BillboardText3D();
        void Label(string value, Vector3 origin, Color4 color, float offset)
        {
            text.TextInfo.Add(new TextInfo(value, origin) { Foreground = color, Background = new Color4(.075f, .11f, .17f, .9f),
                Scale = (float)(Math.Clamp(options.Display.LabelScale, .6, 1.6) * .6), Offset = new Vector2(0, offset) });
        }
        if (options.Display.NodeIds)
            foreach (int node in visible.SelectMany(e => e.Nodes).Distinct()) Label("N " + node, points[node], new Color4(.58f, .86f, 1, 1), 10);
        foreach (var element in visible)
        {
            var center = element.Nodes.Select(n => points[n]).Aggregate(Vector3.Zero, (a, b) => a + b) / element.Nodes.Length;
            if (options.Display.ElementIds) Label("E " + element.Id, center, new Color4(1, .89f, .61f, 1), 0);
            if (options.Display.PropertyIds) Label((element.Type == "PLATE" ? "T " : "S ") + element.Property, center, new Color4(.8f, 1, .86f, 1), options.Display.ElementIds ? -14 : 0);
        }
        viewport.Items.Add(new BillboardTextModel3D { Geometry = text, FixedSize = true, IsTransparent = false, IsHitTestVisible = false, DepthBias = -10000 });
    }
}
