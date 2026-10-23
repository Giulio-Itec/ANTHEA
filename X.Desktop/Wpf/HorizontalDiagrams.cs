using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

/// <summary>
/// Coordinated depth plots. Dashed lower branches are equilibrium completions. The data come from
/// <see cref="HorizontalPileDiagrams"/> (ANTHEA.Application, F5.7); this class only draws them.
/// </summary>
internal sealed class HorizontalDiagrams : DrawingView
{
    private readonly Func<HorizontalDiagram> model;

    internal HorizontalDiagrams(JsonObject result, int surveyIndex, bool soilView) => model = () => HorizontalPileDiagrams.Build(result, surveyIndex, soilView);

    /// <summary>The diagrams of a survey already prepared (the ViewModel of the sheet gives them).</summary>
    internal HorizontalDiagrams(HorizontalDiagram diagram) => model = () => diagram;

    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size));
        if (size.Width < 700 || size.Height < 450) return;
        var d = model();
        const double top = 159, left = 142;
        double bottom = size.Height - 152, right = size.Width - 18;
        double Y(double z) => top + z / d.Length * (bottom - top);
        Text(dc, d.Title, 16, 10, 18, Ui.Navy, size.Width - 32, true);
        Text(dc, d.Subtitle, 16, 39, 12, width: size.Width - 32);
        Text(dc, d.Basis, 16, 60, 11, width: size.Width - 32);
        Text(dc, "Strati / z [m]", 16, 96, 12, Ui.Navy, 120, true);
        foreach (var layer in d.Layers)
        {
            var color = Ui.Brush(StratigraphyDrawing.LayerColors[layer.Index % StratigraphyDrawing.LayerColors.Length].ToString()!);
            dc.DrawRectangle(color, null, new Rect(45, Y(layer.Top), 25, Y(layer.End) - Y(layer.Top)));
            var wash = color.Clone(); wash.Opacity = .065;
            dc.DrawRectangle(wash, null, new Rect(left, Y(layer.Top), right - left, Y(layer.End) - Y(layer.Top)));
            dc.DrawLine(new Pen(Ui.Brush("#CBD5E1"), .8), new Point(left, Y(layer.Top)), new Point(right, Y(layer.Top)));
            Text(dc, layer.TopLabel, 8, Y(layer.Top) - 7, 10, width: 35);
            if (Y(layer.End) - Y(layer.Top) >= 27)
            {
                Text(dc, layer.Label, 75, (Y(layer.Top) + Y(layer.End)) / 2 - 13, 10, Ui.Navy, 65);
            }
        }
        Text(dc, d.LengthLabel, 8, bottom - 7, 10, width: 35);
        if (d.Hinge is double zh)
        {
            var shade = Ui.Brush("#C4B5D4").Clone(); shade.Opacity = .16;
            dc.DrawRectangle(shade, null, new Rect(left, Y(zh), right - left, bottom - Y(zh)));
        }
        if (d.ShearDepth is double shear)
        {
            Text(dc, d.ShearLabel!, 16, 115, 10, Ui.Brush("#B45309"), 123);
            dc.DrawLine(new Pen(Ui.Brush("#B45309"), .7) { DashStyle = DashStyles.Dot }, new Point(42, Y(shear)), new Point(right, Y(shear)));
        }
        double column = (right - left) / d.Panels.Count;
        for (int j = 0; j < d.Panels.Count; j++)
        {
            var panel = d.Panels[j]; double x0 = left + j * column + 12, width = column - 30;
            Text(dc, panel.Title, x0, 87, 12, Ui.Navy, width + 8, true);
            int legend = 0;
            foreach (var c in panel.Curves.Where(c => c.Label != ""))
            {
                double ly = 111 + 15 * legend++; var pen = new Pen(Ui.Brush(c.Color), 2) { DashStyle = c.Dashed ? DashStyles.Dash : DashStyles.Solid };
                dc.DrawLine(pen, new Point(x0, ly + 6), new Point(x0 + 15, ly + 6));
                Text(dc, c.Label, x0 + 19, ly, 10, width: width - 14);
            }
            double min = panel.Minimum, max = panel.Maximum;
            double X(double v) => x0 + (v - min) / (max - min) * width;
            var area = new Rect(x0, top, width, bottom - top);
            for (int tick = 0; tick <= 4; tick++)
            {
                double xx = x0 + width * tick / 4;
                dc.DrawLine(new Pen(Ui.Brush("#E2E8F0"), .6), new Point(xx, top), new Point(xx, bottom));
                if (width >= 230 || tick % 2 == 0)
                {
                    var number = new FormattedText(panel.Ticks[tick], System.Globalization.CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Ui.Muted, 1);
                    dc.DrawText(number, new Point(Math.Clamp(xx - number.Width / 2, x0, Math.Max(x0, x0 + width - number.Width)), bottom + 5));
                }
            }
            dc.DrawRectangle(null, new Pen(Ui.Brush("#CBD5E1"), 1), area);
            dc.DrawLine(new Pen(Ui.Brush("#64748B"), .8), new Point(X(0), top), new Point(X(0), bottom));
            dc.PushClip(new RectangleGeometry(area));
            if (panel.MomentLimit is double my) foreach (int sign in new[] { -1, 1 })
                dc.DrawLine(new Pen(Ui.Brush("#64748B"), 1) { DashStyle = DashStyles.Dot }, new Point(X(sign * my), top), new Point(X(sign * my), bottom));
            foreach (var c in panel.Curves)
                foreach (var segment in c.Segments)
                {
                    var pen = new Pen(Ui.Brush(c.Color), c.Signed ? 2.2 : 1.6) { DashStyle = segment.Dashed ? DashStyles.Dash : DashStyles.Solid };
                    dc.DrawLine(pen, new Point(X(segment.FromValue), Y(segment.FromDepth)), new Point(X(segment.ToValue), Y(segment.ToDepth)));
                }
            if (panel.Capacity is { } capacity)
            {
                dc.DrawLine(new Pen(Ui.Brush("#7E22CE"), 1) { DashStyle = DashStyles.Dot }, new Point(X(capacity.Value), top), new Point(X(capacity.Value), bottom));
                dc.DrawEllipse(Ui.Brush("#7E22CE"), null, new Point(X(capacity.Value), Y(capacity.Depth)), 4, 4);
                Text(dc, "Q(z_f) = Hu", x0 + 5, top + 4, 10, Ui.Navy, width - 10);
            }
            foreach (var point in panel.Hinges)
                dc.DrawEllipse(Brushes.White, new Pen(Ui.Brush("#7E22CE"), 2), new Point(X(point.Value), Y(point.Depth)), 5, 5);
            dc.Pop();
            if (panel.Message is string message)
                Text(dc, message, x0 + 5, top + 30, 11, Ui.Navy, width - 10);
        }
        foreach (var depth in d.Depths)
        {
            var pen = new Pen(Ui.Brush(depth.Color), 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(pen, new Point(42, Y(depth.Depth)), new Point(right, Y(depth.Depth)));
            // Labels live below the plots to avoid overlap at close interfaces.
            double offset = depth.Slot switch { 1 => 240, 2 => 480, 3 => 720, _ => 0 };
            Text(dc, depth.Label, 16 + offset, bottom + 32, 11, Ui.Brush(depth.Color), 235);
        }
        Text(dc, d.Note, 16, bottom + 57, 11, width: size.Width - 32);
        Text(dc, d.HingeNote, 16, bottom + 89, 11, width: size.Width - 32);
        if (d.BromsNote is string broms)
            Text(dc, broms, 16, bottom + 121, 11, Brushes.DarkOrange, size.Width - 32);
    }
}

