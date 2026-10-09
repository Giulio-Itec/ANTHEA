using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HelixToolkit.Wpf.SharpDX;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>Composes the control's own WPF content with its Direct3D scene, including labels and legend.</summary>
internal static class ViewerImageExporter
{
    public static void Save(FrameworkElement root, Viewport3DX viewport, string path)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "anthea-view-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            viewport.SaveScreen(temporary);
            var scene = new BitmapImage(); scene.BeginInit(); scene.CacheOption = BitmapCacheOption.OnLoad;
            scene.UriSource = new Uri(temporary); scene.EndInit(); scene.Freeze();
            root.UpdateLayout(); var dpi = VisualTreeHelper.GetDpi(root);
            int width = (int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX), height = (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY);
            var surface = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32); surface.Render(root);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawImage(surface, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                var origin = viewport.TransformToAncestor(root).Transform(new Point());
                drawing.DrawImage(scene, new Rect(origin.X, origin.Y, viewport.ActualWidth, viewport.ActualHeight));
            }
            var target = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32); target.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target)); using var output = File.Create(path); encoder.Save(output);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
