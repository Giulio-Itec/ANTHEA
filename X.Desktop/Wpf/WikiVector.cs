using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace X.Desktop;

/// <summary>Packaged, script-free SVG subset. Native WPF vectors retain clarity at any DPI.</summary>
internal sealed class WikiVector : FrameworkElement
{
    private readonly XElement svg;
    private readonly double width, height;
    internal WikiVector(string name)
    {
        using var stream = WikiCatalog.Resource("figures." + name);
        svg = XElement.Load(stream);
        var box = svg.Attribute("viewBox")!.Value.Split(' ').Select(Parse).ToArray();
        width = box[2]; height = box[3];
        System.Windows.Automation.AutomationProperties.SetName(this, svg.Elements().First(e => e.Name.LocalName == "title").Value);
    }
    private static double Parse(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    protected override Size MeasureOverride(Size available)
    {
        var w = double.IsFinite(available.Width) ? Math.Min(available.Width, width) : width;
        return new Size(w, w * height / width);
    }
    protected override void OnRender(DrawingContext dc)
    {
        var scale = Math.Min(ActualWidth / width, ActualHeight / height);
        dc.PushTransform(new ScaleTransform(scale, scale));
        foreach (var e in svg.Elements())
        {
            double N(string key, double fallback = 0) => e.Attribute(key) is { } a ? Parse(a.Value) : fallback;
            Brush? Color(string key, string fallback = "none")
            {
                var c = e.Attribute(key)?.Value ?? fallback;
                return c == "none" ? null : c == "currentColor" ? WikiPalette.Ink : c == "#0B5CAD" ? WikiPalette.Accent : Ui.Brush(c);
            }
            var stroke = Color("stroke"); var pen = stroke is null ? null : new Pen(stroke, N("stroke-width", 2));
            if (pen is not null && e.Attribute("stroke-dasharray") is not null) pen.DashStyle = DashStyles.Dash;
            var fill = Color("fill");
            switch (e.Name.LocalName)
            {
                case "line": dc.DrawLine(pen!, new(N("x1"), N("y1")), new(N("x2"), N("y2"))); break;
                case "rect": dc.DrawRectangle(fill, pen, new(N("x"), N("y"), N("width"), N("height"))); break;
                case "circle": dc.DrawEllipse(fill, pen, new(N("cx"), N("cy")), N("r"), N("r")); break;
                case "polyline": case "polygon":
                    var points = e.Attribute("points")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split(',').Select(Parse).ToArray()).ToArray();
                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    { ctx.BeginFigure(new(points[0][0], points[0][1]), fill is not null, e.Name.LocalName == "polygon"); ctx.PolyLineTo(points.Skip(1).Select(p => new Point(p[0], p[1])).ToArray(), true, false); }
                    dc.DrawGeometry(fill, pen, geometry); break;
                case "text":
                    var text = new FormattedText(e.Value, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), N("font-size", 16), Color("fill", "currentColor")!, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(text, new(N("x"), N("y") - text.Height)); break;
            }
        }
        dc.Pop();
    }
}

internal static class WikiPalette
{
    internal static bool Dark { get => Appearance.Current != AppAppearance.Light; set => Appearance.Set(value ? AppAppearance.Dark : AppAppearance.Light, false); }
    internal static Brush Ink => Appearance.Ink;
    internal static Brush Accent => Appearance.Accent;
    internal static Brush Paper => Appearance.Paper;
    internal static Brush Surface => Appearance.Surface;
    internal static Brush Muted => Appearance.Ink;
}
