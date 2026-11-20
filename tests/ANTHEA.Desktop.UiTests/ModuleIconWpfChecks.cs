using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Anthea.Application.Modules;

namespace X.Desktop;

/// <summary>
/// The icons of the modules (19/11/2026): every module of the catalogue has its own icon, the tile of the vertical pile only for the vertical
/// pile; the board of all the icons is saved for the visual check.
/// </summary>
internal static class ModuleIconWpfChecks
{
    private const int Columns = 6, Cell = 120, Icon = 80;

    internal static void Run(string directory, Action<bool, string> check)
    {
        var modules = ModuleDirectory.All.Select(m => m.Id).Distinct().ToArray();
        var fallback = modules.Where(m => m != "geo_palo_verticale" && Ui.ModuleIcon(m) is Image { Source: CroppedBitmap { SourceRect.X: 0, SourceRect.Y: 0 } }).ToArray();
        check(fallback.Length == 0, "Icone dei moduli: nessun modulo con l'icona del palo verticale al posto della sua" + (fallback.Length > 0 ? " (" + string.Join(", ", fallback) + ")" : ""));
        check(modules.Count(ModuleGlyphIcon.Supports) == 22, "Icone dei moduli: le 22 icone nuove disegnate (" + modules.Count(ModuleGlyphIcon.Supports) + ")");
        var board = new UniformGrid { Columns = Columns, Background = Brushes.White };
        foreach (string module in modules)
        {
            var icon = Ui.ModuleIcon(module); icon.Width = Icon; icon.Height = Icon;
            var label = new TextBlock { Text = module, FontSize = 10, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
            board.Children.Add(new StackPanel { Width = Cell, Margin = new Thickness(4), Children = { icon, label } });
        }
        int rows = (modules.Length + Columns - 1) / Columns;
        var size = new Size(Columns * (Cell + 8), rows * (Cell + 8));
        board.Measure(size); board.Arrange(new Rect(size)); board.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(board);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(directory, "icone-moduli.png"))) encoder.Save(stream);
        check(File.Exists(Path.Combine(directory, "icone-moduli.png")), "Icone dei moduli: tavola di tutte le icone salvata");
    }
}
