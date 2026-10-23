using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Anthea.Presentation.Tests;
using X.Core;

namespace X.Desktop;

/// <summary>
/// The display list of a drawing view (DrawingView of the drawings of the RC section), the frozen form of tests/fixtures/f5-sezione/disegni: moved
/// unchanged from ConcreteSectionHistoricTrace (archived with the historic view in supporto/SUPERATI/ui-storica/sezione-ca at the switch of the Release
/// of 23/10/2026), used by the part «disegni» of ui/check-sezione-view.
/// </summary>
internal static class ConcreteSectionDrawingRecord
{
    private static readonly MethodInfo RenderMethod = typeof(DrawingView).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(DrawingContext), typeof(Size)], null)
        ?? throw new MissingMethodException(nameof(DrawingView), "Render");

    /// <summary>The display list of a drawing view rendered at <paramref name="width"/> × <paramref name="height"/>, as its Png does.</summary>
    internal static string Record(string output, string name, DrawingView view, int width, int height)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open()) RenderMethod.Invoke(view, [dc, new Size(width, height)]);
        var lines = new List<string>();
        DisplayList(group, lines);
        string file = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')) + ".txt";
        File.WriteAllLines(Path.Combine(output, file), lines, new UTF8Encoding(false));
        return name + " · primitivi " + lines.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(lines);
    }

    static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    static string P(Point p) => N(p.X) + "," + N(p.Y);

    static string Brush(Brush? brush) => brush switch
    {
        null => "-",
        SolidColorBrush s => s.Color.ToString(CultureInfo.InvariantCulture) + (s.Opacity == 1 ? "" : "@" + N(s.Opacity)),
        ImageBrush => "immagine",
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
                lines.Add("testo " + ConcreteSectionTrace.Quote(new string(run.Characters.ToArray())) + " " + P(run.BaselineOrigin) + " " + N(run.FontRenderingEmSize)
                    + " " + Brush(text.ForegroundBrush));
                break;
            case ImageDrawing image:
                // The stress raster: its pixels are compared through their hash.
                lines.Add("immagine " + N(image.Rect.X) + "," + N(image.Rect.Y) + "," + N(image.Rect.Width) + "," + N(image.Rect.Height) + " " + Pixels(image.ImageSource));
                break;
            default:
                lines.Add(drawing.GetType().Name);
                break;
        }
    }

    static string Pixels(ImageSource source)
    {
        if (source is not BitmapSource bitmap) return source.GetType().Name;
        int stride = bitmap.PixelWidth * ((bitmap.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(pixels, stride, 0);
        return bitmap.PixelWidth.ToString(CultureInfo.InvariantCulture) + "x" + bitmap.PixelHeight.ToString(CultureInfo.InvariantCulture) + " "
            + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels))[..16];
    }
}
