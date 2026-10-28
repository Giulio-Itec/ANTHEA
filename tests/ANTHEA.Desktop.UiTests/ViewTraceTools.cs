using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace X.Desktop;

/// <summary>
/// Tools of the WPF checks of the XAML views that the traces of the historic views shared (UiTests): the continuations at idle priority, the
/// drawing lists of an element and the pixels of a PNG. Moved without changes from tests/Desktop/CompositeSectionHistoricTrace.cs when the
/// historic view of the composite bridge section was archived (passaggio della Release, 23/10/2026; supporto/SUPERATI/ui-storica/sezione-mista).
/// </summary>
internal static class ViewTraceTools
{
    /// <summary>
    /// The continuations of the view (the delay of the automatic calculation, the end of the engine, the publication) run at the idle priority of
    /// the dispatcher: only while the check waits, never while a step settles. The check does not depend on the time a step takes, as the
    /// ViewModel without WPF, whose continuations run only when its tests wait. <paramref name="action"/> runs with that context; the context of
    /// the check is restored after, so that the check itself never waits behind the continuations of the view.
    /// </summary>
    internal static T WithIdleContinuations<T>(Func<T> action)
    {
        var outer = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher, DispatcherPriority.ApplicationIdle));
        try { return action(); }
        finally { SynchronizationContext.SetSynchronizationContext(outer); }
    }

    /// <summary>The drawing list of an element rendered at <paramref name="width"/> × <paramref name="height"/>; its size is restored after.</summary>
    internal static string Record(string output, string name, FrameworkElement drawing, int width, int height)
    {
        object w = drawing.ReadLocalValue(FrameworkElement.WidthProperty), h = drawing.ReadLocalValue(FrameworkElement.HeightProperty);
        try { return HorizontalPileHistoricTrace.Record(output, name, drawing, width, height); }
        finally
        {
            if (w == DependencyProperty.UnsetValue) drawing.ClearValue(FrameworkElement.WidthProperty); else drawing.SetValue(FrameworkElement.WidthProperty, w);
            if (h == DependencyProperty.UnsetValue) drawing.ClearValue(FrameworkElement.HeightProperty); else drawing.SetValue(FrameworkElement.HeightProperty, h);
        }
    }

    /// <summary>The pixels of a PNG (size and hash), independent of the encoder.</summary>
    internal static string Pixels(byte[] png)
    {
        var frame = BitmapDecoder.Create(new MemoryStream(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        int stride = frame.PixelWidth * ((frame.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[stride * frame.PixelHeight]; frame.CopyPixels(pixels, stride, 0);
        return frame.PixelWidth.ToString(CultureInfo.InvariantCulture) + "x" + frame.PixelHeight.ToString(CultureInfo.InvariantCulture) + " "
            + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels))[..16];
    }

    /// <summary>
    /// Two PNG of the same drawing rendered twice in the process: same size and format, at most <paramref name="pixels"/> pixels different and
    /// each of their channels within <paramref name="levels"/> of 255. Two renderings of the same vector drawing may differ in a pixel of
    /// antialiasing (27/10/2026, sheet of the reinforcement of the horizontal pile: one pixel, 9 levels); a different content changes far more.
    /// </summary>
    internal static bool SameRendering(byte[] png, byte[] expected, int pixels = 8, int levels = 16)
    {
        static (BitmapFrame Frame, byte[] Bytes, int Bpp) Decode(byte[] png)
        {
            var frame = BitmapDecoder.Create(new MemoryStream(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            int bpp = (frame.Format.BitsPerPixel + 7) / 8, stride = frame.PixelWidth * bpp;
            var bytes = new byte[stride * frame.PixelHeight]; frame.CopyPixels(bytes, stride, 0);
            return (frame, bytes, bpp);
        }
        var (a, x, bpp) = Decode(png); var (b, y, _) = Decode(expected);
        if (a.PixelWidth != b.PixelWidth || a.PixelHeight != b.PixelHeight || a.Format != b.Format) return false;
        int different = 0;
        for (int p = 0; p < x.Length; p += bpp)
        {
            int delta = 0;
            for (int c = 0; c < bpp; c++) delta = Math.Max(delta, Math.Abs(x[p + c] - y[p + c]));
            if (delta == 0) continue;
            if (delta > levels || ++different > pixels) return false;
        }
        return true;
    }
}
