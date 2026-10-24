using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Anthea.Presentation.Tests;

namespace X.Desktop;

/// <summary>
/// The display lists of the drawings of the horizontal pile and micropile and of the elastic response (the reference of
/// tests/fixtures/f5-palo-orizzontale/disegni and of tests/fixtures/f5-elastico), recorded through the views. f5-pass-orizzontale: extracted
/// unchanged from the historic trace, archived with the historic view (supporto/SUPERATI/ui-storica/palo-orizzontale); the name of the class
/// stays for its callers (HorizontalPileViewChecks, CompositeSectionHistoricTrace).
/// </summary>
internal static class HorizontalPileHistoricTrace
{
    /// <summary>Folder of the drawings of the historic view, frozen before the migration.</summary>
    internal static string DrawingsReference(string root) => Path.Combine(root, "tests", "fixtures", "f5-palo-orizzontale", "disegni");

    internal static string Record(string output, string name, FrameworkElement drawing, int width, int height)
    {
        drawing.Width = width; drawing.Height = height;
        // Frozen display lists describe exact DIP sizes, independent of the host's pixel rounding.
        // At 125%, inherited rounding would turn 1750 × 650 into 1750.4 × 649.6.
        object rounding = drawing.ReadLocalValue(FrameworkElement.UseLayoutRoundingProperty);
        var lines = new List<string>();
        try
        {
            drawing.UseLayoutRounding = false;
            drawing.Measure(new Size(width, height)); drawing.Arrange(new Rect(0, 0, width, height)); drawing.UpdateLayout();
            if (VisualTreeHelper.GetDrawing(drawing) is { } content) DisplayList(content, lines);
        }
        finally
        {
            if (rounding == DependencyProperty.UnsetValue) drawing.ClearValue(FrameworkElement.UseLayoutRoundingProperty);
            else drawing.SetValue(FrameworkElement.UseLayoutRoundingProperty, rounding);
        }
        string file = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')) + ".txt";
        File.WriteAllLines(Path.Combine(output, file), lines, new UTF8Encoding(false));
        return name + " · primitivi " + lines.Count.ToString(CultureInfo.InvariantCulture) + " " + HorizontalPileTrace.Hash((object)lines);
    }

    static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    static string P(Point p) => N(p.X) + "," + N(p.Y);

    static string Brush(Brush? brush) => brush switch
    {
        null => "-",
        SolidColorBrush s => s.Color.ToString(CultureInfo.InvariantCulture) + (s.Opacity == 1 ? "" : "@" + N(s.Opacity)),
        _ => brush.GetType().Name
    };

    static string Pen(Pen? pen) => pen is null ? "-" : Brush(pen.Brush) + " " + N(pen.Thickness)
        + (pen.DashStyle is { Dashes.Count: > 0 } dash ? " tratto " + string.Join(";", dash.Dashes.Select(N)) + "+" + N(dash.Offset) : "");

    static string Geometry(System.Windows.Media.Geometry? geometry) => geometry switch
    {
        null => "-",
        LineGeometry l => "linea " + P(l.StartPoint) + " " + P(l.EndPoint),
        RectangleGeometry r => "rettangolo " + N(r.Rect.X) + "," + N(r.Rect.Y) + "," + N(r.Rect.Width) + "," + N(r.Rect.Height) + " " + N(r.RadiusX),
        EllipseGeometry e => "ellisse " + P(e.Center) + " " + N(e.RadiusX) + " " + N(e.RadiusY),
        _ => "percorso " + PathGeometry.CreateFromGeometry(geometry).ToString(CultureInfo.InvariantCulture)
    };

    static void DisplayList(Drawing drawing, List<string> lines)
    {
        switch (drawing)
        {
            case DrawingGroup group:
                lines.Add("gruppo " + N(group.Opacity) + " " + Geometry(group.ClipGeometry) + " " + (group.Transform is null ? "-" : group.Transform.Value.ToString(CultureInfo.InvariantCulture)));
                foreach (var child in group.Children) DisplayList(child, lines);
                lines.Add("fine");
                break;
            case GeometryDrawing shape:
                lines.Add("forma " + Geometry(shape.Geometry) + " riempimento " + Brush(shape.Brush) + " penna " + Pen(shape.Pen));
                break;
            case GlyphRunDrawing text:
                var run = text.GlyphRun;
                lines.Add("testo " + HorizontalPileTrace.Quote(new string(run.Characters.ToArray())) + " " + P(run.BaselineOrigin) + " " + N(run.FontRenderingEmSize)
                    + " " + Brush(text.ForegroundBrush));
                break;
            default:
                lines.Add(drawing.GetType().Name);
                break;
        }
    }
}
