using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class StratigraphyDrawing
{
    internal bool HorizontalForces { get; set; }
    internal JsonObject? HorizontalResult { get; set; }

    // Only the above-hinge branch is determined by the long-pile Broms mechanism.
    // The engine's below-hinge equilibrium completion is not a physical reaction diagram.
    internal static double? ReactionLimit(JsonNode result) => result.S("meccanismo") == "Lungo"
        ? result.Array("cerniere_m").Select(J.Number).Where(z => z > 0).Min() : null;

    internal static JsonNode?[] PhysicalDiagram(JsonNode result)
    {
        double? limit = ReactionLimit(result);
        return result.Array("diagrammi").Where(r => limit is null || r.D("z") < limit ||
            (r.D("z") == limit && r.S("lato") != "dopo")).ToArray();
    }

    private void DrawHorizontalForces(DrawingContext dc, int index, double pileX, double left, double right,
        double top, double scale, double length, double height)
    {
        if (HorizontalResult is null || index >= HorizontalResult.Array("sondaggi").Count)
        { Text(dc, "Forze: dati da completare", left, top + 15, 10, width: Math.Max(15, right - left)); return; }
        var result = HorizontalResult.Array("sondaggi")[index]!;
        double available = Math.Max(12, right - left), axis = left + available / 2;
        double? limit = ReactionLimit(result);
        var rows = PhysicalDiagram(result);
        double peak = rows.Select(r => Math.Abs(r.D("p_kn_m"))).DefaultIfEmpty(0).Max();
        double magnitude = peak > 0 ? available * .44 / peak : 0;
        var green = Ui.Brush("#16703C"); var purple = Ui.Brush("#7E22CE");
        Text(dc, "p [kN/m]", left, top - 18, 10, green, available);
        dc.DrawLine(new Pen(Ui.Muted, .6), new Point(axis, top), new Point(axis, top + (limit ?? length) * scale));
        Point PointAt(JsonNode? row) => new(axis + row.D("p_kn_m") * magnitude, top + row.D("z") * scale);
        dc.DrawGeometry(null, new Pen(green, 1.7), Path(rows.Select(PointAt), false));
        // Arrow samples preserve the signed reaction; jumps stay in the continuous polyline above.
        int stride = Math.Max(1, rows.Length / 14);
        for (int i = 0; i < rows.Length; i += stride)
        {
            var point = PointAt(rows[i]); if (Math.Abs(point.X - axis) < 3) continue;
            Arrow(dc, new Point(axis, point.Y), point, green);
        }
        Text(dc, $"|p|max {peak:0.0}", left, top + length * scale + 5, 10, green, available);
        foreach (var hinge in result.Array("cerniere_m"))
        {
            double z = J.Number(hinge) ?? 0, y = top + z * scale;
            dc.DrawEllipse(Brushes.White, new Pen(purple, 2), new Point(pileX, y), 5, 5);
            dc.DrawLine(new Pen(purple, .8) { DashStyle = DashStyles.Dash }, new Point(pileX + 6, y), new Point(right, y));
            Text(dc, $"Cerniera z={z:0.0} m", left, y + 3, 10, purple, available);
        }
        double force = result.D("risultante_concentrata_kn"), depth = result.D("quota_risultante_m");
        if (limit is null && Math.Abs(force) > 1e-8)
        {
            double y = top + depth * scale;
            Arrow(dc, new Point(right, y), new Point(left, y), Brushes.DarkOrange);
            Text(dc, $"F={force:0.0} kN", left, y - 16, 10, Brushes.DarkOrange, available);
        }
        if (result.Array("cerniere_m").Count == 0)
            Text(dc, "Nessuna cerniera", left, top + 4, 10, purple, available);
        if (limit is double cut)
            Text(dc, "Sotto: reazioni non rappresentate", left, top + cut * scale + 35, 10, Ui.Muted, available);
    }

    private static void Arrow(DrawingContext dc, Point from, Point to, Brush color)
    {
        var pen = new Pen(color, 1); dc.DrawLine(pen, from, to);
        double direction = Math.Sign(to.X - from.X);
        dc.DrawLine(pen, to, new Point(to.X - direction * 4, to.Y - 3));
        dc.DrawLine(pen, to, new Point(to.X - direction * 4, to.Y + 3));
    }
}
