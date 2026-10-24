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
}
