using System.Globalization;
using System.Windows;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed record BridgeLoadPoint(int Index, string Name, double Y, double Force);

internal sealed partial class BridgeDrawing
{
    private sealed record SectionTag(string Title, string Value, Point Anchor, bool Left, Brush Color);
    private void DrawTags(DrawingContext dc, BridgeGeometry g, double width, double height, double tagWidth, Func<double, double, Point> point)
    {
        var tags = new List<SectionTag>();
        string F(double v) => BridgeWorkspace.F(v);
        if (ShowGeometryLabels)
        {
            tags.Add(new("Soletta collaborante", $"{F(g.Width)} × {F(g.SlabHeight)} mm", point(0, g.SlabHeight / 2), true, Ui.Navy));
            tags.Add(new("Piattabanda superiore", $"{F(g.TopWidth)} × {F(g.TopThickness)} mm", point((g.Width + g.TopWidth) / 2, -g.TopThickness / 2), false, Steel));
            tags.Add(new("Anima · h libera × t", $"{F(g.WebHeight)} × {F(g.WebThickness)} mm", point((g.Width + g.WebThickness) / 2, -g.TopThickness - g.WebHeight / 2), false, Steel));
            tags.Add(new("Piattabanda inferiore 1", $"{F(g.Bottom1Width)} × {F(g.Bottom1Thickness)} mm", point((g.Width + g.Bottom1Width) / 2, -g.TopThickness - g.WebHeight - g.Bottom1Thickness / 2), false, Steel));
            if (g.Bottom2Thickness > 0) tags.Add(new("Piattabanda inferiore 2", $"{F(g.Bottom2Width)} × {F(g.Bottom2Thickness)} mm", point((g.Width + g.Bottom2Width) / 2, -g.Height + g.Bottom2Thickness / 2), false, Steel));
            if (Input is {} data && data.B(DetailAtSupport ? "appoggio" : "irrigidimenti"))
            {
                try
                {
                    var p = BridgeSection.StiffenerPlates(data, DetailAtSupport ? "app" : "irr");
                    string left = p.LeftWidth > 0 ? $"SX {F(p.LeftWidth)}×{F(p.LeftThickness)}" : "SX assente";
                    string right = p.RightWidth > 0 ? $"DX {F(p.RightWidth)}×{F(p.RightThickness)}" : "DX assente";
                    tags.Add(new(DetailAtSupport ? "Irrigidimento d’appoggio" : "Irrigidimento intermedio", left + "\n" + right + " mm",
                        point(g.Width / 2 - g.WebThickness / 2 - p.LeftWidth, -g.TopThickness - g.WebHeight * .65), true, Ui.Brush("#567D87")));
                }
                catch (ArgumentException) { /* geometry edit may be incomplete */ }
            }
        }
        if (ShowRebarLabels)
        {
            foreach (string side in new[] { "top", "bottom" })
            {
                if (Input is null || !Input.B("rebars_" + side)) continue;
                double cover = Input.D("cover_" + side), y = side == "top" ? g.SlabHeight - cover : cover;
                var bars = g.Bars.Where(b => Math.Abs(b.Y - y) < 1e-6).OrderBy(b => b.X).ToArray();
                if (bars.Length == 0) continue;
                tags.Add(new(side == "top" ? "Armatura superiore" : "Armatura inferiore",
                    $"Ø{F(Input.D("d_" + side))} / {F(Input.D("pitch_" + side))} mm\n{bars.Length} barre · asse {F(cover)} mm", point(bars[0].X, y), true, Bars));
            }
        }
        foreach (bool left in new[] { true, false })
        {
            var lane = tags.Where(t => t.Left == left).OrderBy(t => t.Anchor.Y).ToArray();
            var positions = new double[lane.Length]; var heights = lane.Select(t => t.Value.Contains('\n') ? 70d : 54d).ToArray();
            double next = 40;
            for (int i = 0; i < lane.Length; i++) { positions[i] = Math.Max(next, lane[i].Anchor.Y - heights[i] / 2); next = positions[i] + heights[i] + 10; }
            double bottom = height - 78;
            for (int i = lane.Length - 1; i >= 0; i--) { positions[i] = Math.Min(positions[i], bottom - heights[i]); bottom = positions[i] - 10; }
            for (int i = 0; i < lane.Length; i++)
            {
                var tag = lane[i]; double x = left ? 12 : width - tagWidth - 12;
                var box = new Rect(x, Math.Max(26, positions[i]), tagWidth, heights[i]); TagBounds.Add(box); VisibleTags.Add(tag.Title + " · " + tag.Value);
                var edge = new Point(left ? box.Right : box.Left, box.Top + box.Height / 2);
                var elbow = new Point(edge.X + (left ? 12 : -12), edge.Y);
                var pen = new Pen(tag.Color, 1);
                dc.DrawLine(pen, tag.Anchor, elbow); dc.DrawLine(pen, elbow, edge); dc.DrawEllipse(tag.Color, null, tag.Anchor, 2.2, 2.2);
                dc.DrawRoundedRectangle(Brushes.White, new Pen(Ui.Brush("#CED8E3"), 1), box, 4, 4);
                void Text(string text, double y, bool bold)
                {
                    var value = new FormattedText(text, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                        11, bold ? tag.Color : Ui.Navy, VisualTreeHelper.GetDpi(this).PixelsPerDip) { MaxTextWidth = tagWidth - 14 };
                    dc.DrawText(value, new Point(box.X + 7, y));
                }
                Text(tag.Title, box.Top + 7, true); Text(tag.Value, box.Top + 28, false);
            }
        }
    }
}
