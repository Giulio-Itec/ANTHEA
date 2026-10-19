using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;
using X.Desktop.Views.EmbeddedWalls;

namespace X.Desktop;

internal static class EmbeddedWallViewChecks
{
    internal static async Task Run(string root, string output)
    {
        Directory.CreateDirectory(output);
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
        var data = EmbeddedWallDocument.Create(); data["name"] = "Paratia — esempio sintetico";
        foreach (string side in new[] { "left_soils", "right_soils" })
        {
            var soil = data[side]![0]!.AsObject(); soil["name"] = "Sabbia di esempio"; soil["gamma"] = 18; soil["gamma_sat"] = 20; soil["phi"] = 30;
            soil["c"] = 0; soil["k0"] = .5; soil["kh"] = .001; soil["source"] = "Dati sintetici per collaudo"; soil["stiffness_source"] = "Parametro assegnato nel collaudo";
        }
        data["segments"]![0]!["ei"] = 200000; data["segments"]![0]!["source"] = "EI assegnato nel collaudo";
        var excavation = EmbeddedWallDocument.NewRow("stages"); excavation["id"] = "F2"; excavation["right_ground"] = -4; data["stages"]!.AsArray().Add(excavation);
        var factory = ModuleHostBench.SheetEditors([]);
        using var editor = factory.Open(EmbeddedWallDocument.Module, data);
        Check(editor is MvvmSheetEditor, "Il nuovo modulo non apre il contenitore MVVM.");
        var sheet = (EmbeddedWallSheetViewModel)((MvvmSheetEditor)editor).Sheet;
        var window = new Window { Content = editor.View, Width = 1450, Height = 950, Left = -32000, Top = -32000,
            WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false };
        Appearance.Watch(window); window.Show();
        async Task Settle() { window.UpdateLayout(); await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle); window.UpdateLayout(); }
        void Capture(string name)
        {
            var bitmap = new RenderTargetBitmap((int)editor.View.ActualWidth, (int)editor.View.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(editor.View);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file);
        }
        try
        {
            await Settle(); Check(Visual(editor.View).OfType<EmbeddedWallSheetView>().Count() == 1, "Vista XAML non risolta."); Capture("modello");
            await editor.CalculateAsync(); Check(sheet.Current?.Complete == true && sheet.Stages.Count == 2, "Scavo a due fasi non convergente nella vista.");
            var tabs = Visual(editor.View).OfType<TabControl>().First(); tabs.SelectedIndex = 1; await Settle();
            var tables = Visual(editor.View).OfType<TabControl>().Last(); tables.SelectedIndex = 4; await Settle(); Capture("fasi");
            Check(Visual(editor.View).OfType<EmbeddedWallTableView>().Any(), "Editor tabellare assente.");
            tabs.SelectedIndex = 2; sheet.Diagram = "Momento [kNm]"; sheet.ComparisonStage = sheet.Stages[0]; await Settle(); Capture("momenti");
            var plot = Visual(editor.View).OfType<EmbeddedWallDiagram>().Single();
            Check(ReferenceEquals(plot.Points, sheet.Points) && ReferenceEquals(plot.Comparison, sheet.Stages[0].Points), "Grafico non associato al risultato corrente.");
            Appearance.Set(AppAppearance.Dark, false); await Settle(); Capture("momenti-scuro");
            Appearance.Set(AppAppearance.Light, false);
            sheet.Tables.First(t => t.Key == "stages").Rows[1]["right_ground"] = "-3"; await Settle();
            Check(plot.Points?.Count == 0 && sheet.Current is null, "Grafico superato rimasto visibile.");
            editor.InspectRevision(); var original = sheet.Data.ToJsonString(); sheet.Tables[0].AddCommand.Execute(null);
            Check(sheet.Data.ToJsonString() == original, "Editor in sola lettura modificato.");
            File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli WPF paratie, senza attivare finestre sul desktop");
        }
        finally { window.Content = null; window.Close(); Appearance.Set(AppAppearance.Light, false); }
    }
    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var item in Visual(VisualTreeHelper.GetChild(root, i))) yield return item;
    }
}
