using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace X.Desktop.Themes;

/// <summary>
/// Images of the calculation documents produced by the new views (refactoring F5.3, proposal F5 section 6.1): always in the Light
/// colours, whatever the appearance of ANTHEA, as the historic views do with <see cref="Appearance.Document"/> and
/// <see cref="Ui.DocumentSnapshot"/>. The theme tokens are palette brushes, so inside the scope they take their Light colour; the
/// view on screen returns to the current appearance when the scope ends, without being re-themed. The reports themselves (code
/// and texts) are frozen: this is only the way the new views render their figures.
/// </summary>
internal static class DocumentAppearance
{
    /// <summary>Light scope for <paramref name="root"/> (see <see cref="Appearance.Document"/>): render synchronously inside it.</summary>
    internal static IDisposable Light(DependencyObject? root = null) => Appearance.Document(root);

    /// <summary>PNG of <paramref name="element"/> in the Light colours, at 96 DPI, with its current size (<see cref="Ui.DocumentSnapshot"/>).</summary>
    internal static byte[] Image(FrameworkElement element) => Ui.DocumentSnapshot(element);

    /// <summary>PNG of an element that is not shown (a figure built only for the document), laid out at <paramref name="size"/> in
    /// the Light colours.</summary>
    internal static byte[] Image(FrameworkElement element, Size size)
    {
        using (Light(element))
        {
            element.Measure(size); element.Arrange(new Rect(size)); element.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(size.Width)), Math.Max(1, (int)Math.Ceiling(size.Height)), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
        }
    }
}
