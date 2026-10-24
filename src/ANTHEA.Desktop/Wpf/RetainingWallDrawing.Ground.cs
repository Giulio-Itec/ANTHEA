using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallDrawing
{
    internal string GroundSource { get; private set; } = "";
    internal static string GlobalLayerColor(JsonObject data, JsonNode? layer)
    {
        var g = data["global_stability"]!;
        var names = g.Array("layers").Concat(g.Array("valley_layers")).Select(l => l.S("name")).Distinct().ToList();
        return LayerColors[Math.Max(0, names.IndexOf(layer.S("name"))) % LayerColors.Length];
    }
    private bool HasGlobalGround => Data?["global_stability"].B("enabled") == true && Data["global_stability"]!.Array("layers").Count > 0;

    private void DrawGround(DrawingContext dc, Func<double, double, Point> p, double right, double depth)
    {
        var global = Data!["global_stability"]!;
        GroundSource = HasGlobalGround ? "Strati globali" : "Terreno di fondazione";
        double left = -.8;
        var area = new Rect(p(left, 0), p(right, -depth));
        dc.DrawRectangle(Ui.Brush("#F1F2F3"), null, area);
        dc.PushClip(new RectangleGeometry(area));
        if (!HasGlobalGround)
        {
            dc.DrawRectangle(Ui.Brush("#DDD3B9"), null, area);
            var f = Data["foundation"]!;
            string text = $"Terreno di fondazione · φ′={f.D("phi"):0.#}° · γ={f.D("gamma"):0.#} kN/m³\nSpessore grafico indicativo; definire gli strati in Stabilità globale";
            hits.Add((area, text, null));
            if (ShowLabels) WrappedText(dc, text, area.Left + 5, area.Top + 37, area.Width - 10);
        }
        else
        {
            var columns = global.S("soil_mode") == "Due colonne"
                ? new[] { ("Valle", global.Array("valley_layers"), left, global.D("soil_split_x")), ("Monte", global.Array("layers"), global.D("soil_split_x"), right) }
                : new[] { ("Profilo unico", global.Array("layers"), left, right) };
            foreach (var (name, layers, x0, x1) in columns)
            {
                double start = Math.Max(left, x0), end = Math.Min(right, x1);
                if (!double.IsFinite(start) || !double.IsFinite(end) || end <= start) continue;
                double upper = 0; int index = 0;
                foreach (var layer in layers)
                {
                    double lower = J.Number(layer?["bottom"]) ?? double.NaN;
                    if (!double.IsFinite(lower)) break;
                    if (lower < upper)
                    {
                        var box = new Rect(p(start, upper), p(end, Math.Max(lower, -depth)));
                        dc.DrawRectangle(Ui.Brush(GlobalLayerColor(Data, layer)), new Pen(Brushes.White, .8), box);
                        string resistance = global.S("condition") == "Non drenata" ? $"cu={layer.S("cu")} kPa" : $"φ′={layer.S("phi")}°; c′={layer.S("c")} kPa";
                        string label = $"{name} · {layer.S("name")} · fondo y={lower:0.##} m\n{resistance}";
                        hits.Add((box, label, null));
                        double labelY = Math.Max(box.Top + 5, area.Top + 36);
                        if (ShowLabels && box.Bottom - labelY > 27 && box.Width > 75) WrappedText(dc, label, box.Left + 4, labelY, box.Width - 8);
                    }
                    upper = Math.Min(upper, lower); index++;
                    if (upper <= -depth) break;
                }
            }
            if (global.S("soil_mode") == "Due colonne") dc.DrawLine(new Pen(Ui.Muted, 1) { DashStyle = DashStyles.Dash }, p(global.D("soil_split_x"), 0), p(global.D("soil_split_x"), -depth));
        }
        dc.Pop();
        dc.DrawLine(new Pen(Ui.Muted, 1) { DashStyle = DashStyles.Dash }, p(left, -depth), p(right, -depth));
        if (ShowLabels) Text(dc, HasGlobalGround ? $"Dettaglio strati globali fino a y=−{depth:0.##} m · profilo completo in Stabilità globale" : "Piano di posa y=0 · rappresentazione del terreno di fondazione", area.Left + 3, area.Bottom + 3, Ui.Muted, 9);
    }
}
