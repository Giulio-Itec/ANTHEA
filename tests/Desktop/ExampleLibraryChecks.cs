using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Presentation.Examples;
using X.Desktop.Views.Examples;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task CheckExamples(string output)
    {
        Directory.CreateDirectory(output);
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        ShowExamples();
        UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        var view = (ExampleLibraryView)dashboardBody.Content;
        var vm = (ExampleLibraryViewModel)view.DataContext;
        var modules = (ListBox)view.FindName("ModuleList");
        var examples = (ListBox)view.FindName("ExampleList");
        Check(modules.Items.Count == 15, "Moduli del catalogo");
        foreach (var group in vm.Groups)
        {
            modules.SelectedItem = group;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Check(examples.Items.Count >= 2 && vm.Examples.All(e => e.Module == group.Module), "Filtro: " + group.Module);
        }
        modules.SelectedItem = vm.Groups.Single(g => g.Module == "str_palo");
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        UpdateLayout();
        Check(view.ActualWidth > 700 && examples.ActualHeight > 100, "Layout non disponibile");
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(output, "catalogo.png"))) encoder.Save(file);
        // Real command, then a direct file open as from Start/Explorer: both leave an unsaved copy.
        dirty = false;
        vm.SelectedGroup = vm.Groups.Single(g => g.Module == "mat_acciaio_armatura");
        string original = Path.Combine(AppContext.BaseDirectory, "Esempi", vm.SelectedExample!.Path);
        byte[] bytes = File.ReadAllBytes(original);
        vm.OpenCommand.Execute(null);
        Check(path is null && dirty && editor?.Module == "mat_acciaio_armatura", "Comando non apre una copia");
        dirty = false;
        LoadFile(original);
        Check(path is null && dirty, "Doppio clic non apre una copia");
        Check(bytes.SequenceEqual(File.ReadAllBytes(original)), "Originale modificato");
        dirty = false;
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli WPF esempi\n");
    }
}

internal static class ExampleLibraryChecks
{
    internal static async Task Run(string output)
    {
        var window = new MainWindow { Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try { await window.CheckExamples(output); }
        finally { window.Close(); }
    }
}
