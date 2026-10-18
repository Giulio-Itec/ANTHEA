using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Actions;
using Anthea.Presentation.Modules.Spectra;
using X.Desktop.Views.Spectra;

namespace X.Desktop;

internal static class SpectraViewChecks
{
    internal static async Task Run(string root, string output)
    {
        Directory.CreateDirectory(output); int checks = 0; void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks++; }
        var data = SpectraDocument.Create(); string example = Path.Combine(root, "supporto/artefatti/spettri-ntc/fonti/isolamento-ntc18.xlsx"); if (File.Exists(example)) data = IsolationExampleImport.Read(example, data); else data["isolation"] = true;
        var factory = ModuleHostBench.SheetEditors([]); using var editor = factory.Open(SpectraDocument.Module, data);
        Check(editor is MvvmSheetEditor, "Spettri deve aprirsi con MVVM nella Release"); var sheet = (SpectraSheetViewModel)((MvvmSheetEditor)editor).Sheet;
        int modified = 0; editor.Modified += () => modified++;
        var window = new Window { Content = editor.View, Width = 1400, Height = 1000, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.Manual }; Appearance.Watch(window);
        var bindings = new StringWriter(); var listener = new TextWriterTraceListener(bindings); PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            window.Show(); await editor.CalculateAsync(); window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Check(sheet.HasResults, "Calcolo del foglio aperto"); Check(Visual(editor.View).OfType<SpectraSheetView>().Count() == 1, "Vista XAML risolta dal template");
            var selector = Visual(editor.View).OfType<ComboBox>().Single(c => ReferenceEquals(c.ItemsSource, sheet.Stages));
            foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
            {
                Appearance.Set(mode, false);
                for (int stage = 0; stage < 4; stage++)
                {
                    selector.SelectedIndex = stage; window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); window.UpdateLayout();
                    Check(sheet.Stage == stage && sheet.Curves.Count > 0, "Scelta fase e curve " + stage);
                    Check(Visual(editor.View).OfType<DataGrid>().Any(g => g.Items.Count > 0), "Tabella dei risultati " + stage);
                    var summary = Visual(editor.View).OfType<DataGrid>().First(g => g.IsVisible && g.Items.Count > 0);
                    string expected = sheet.Summary[0][1].ToString()!;
                    Check(Visual(summary).OfType<TextBlock>().Any(t => t.Text == expected), "Valore numerico nella tabella: " + expected);
                    File.WriteAllBytes(Path.Combine(output, $"spettri-{mode}-{stage}.png"), Png(editor.View));
                }
            }
            Check(modified == 0, "Visitare le fasi e i temi non deve modificare il documento");
            editor.InspectRevision(); Check(sheet.SiteForm.IsReadOnly && sheet.IsolationForm.IsReadOnly && !sheet.ImportCommand.CanExecute(null), "Sola lettura estesa a input e importazione");
            Appearance.Set(AppAppearance.Light, false); window.Width = 900; sheet.Stage = 2; window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); File.WriteAllBytes(Path.Combine(output, "spettri-stretta.png"), Png(editor.View));
            listener.Flush(); Check(!bindings.ToString().Contains("Error:"), "Errori di binding: " + bindings);
            File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli vista Spettri, tre temi e finestra stretta");
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); listener.Dispose(); window.Content = null; window.Close(); Appearance.Set(AppAppearance.Light, false); }
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root) { yield return root; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Visual(VisualTreeHelper.GetChild(root, i))) yield return child; }
    private static byte[] Png(FrameworkElement view) { var image = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray(); }
}
