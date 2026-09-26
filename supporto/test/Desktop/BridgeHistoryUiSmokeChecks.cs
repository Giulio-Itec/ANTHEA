using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckBridgeHistoryUi(string directory)
    {
        var data = BridgeSection.Defaults();
        data["metodo_analisi"] = BridgeSection.CalculationMethods[1]; data["classe4"] = false;
        using var bridge = new BridgeWorkspace(data);
        var window = new Window { Title = "Test storico ponte", Owner = this, Width = 1600, Height = 1000, Content = bridge, Background = Ui.Bg };
        int checks = 0;
        void Check(bool ok, string message) { checks++; if (!ok) throw new Exception("Storico UI: " + message); }
        async Task Ready()
        {
            var deadline = DateTime.UtcNow.AddSeconds(40);
            while (bridge.Calculation is null || bridge.Busy) { if (DateTime.UtcNow > deadline) throw new Exception("Storico UI: " + string.Join(" | ", Ui.Descendants<TextBlock>(bridge).Select(t => t.Text).Where(t => t.Contains("Dati da verificare")))); await Task.Delay(80); }
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
        }
        try
        {
            window.Show(); bridge.Pages.SelectedIndex = 1; await Ready();
            Check(bridge.Calculation!.Stages.All(s => s.GetHistory() is { Nonlinear: false }), "selezione lineare storica");
            var choice = (ComboBox)bridge.InputForms.Single(f => f.Editors.ContainsKey("metodo_analisi")).Editors["metodo_analisi"];
            choice.SelectedItem = BridgeSection.CalculationMethods[2]; await Ready();
            Check(bridge.Calculation!.Stages.All(s => s.GetHistory() is { Nonlinear: true }), "selezione non lineare");
            Check(data.Array("fasi")[1]!.D("phi") == 2, "passaggio al non lineare cancella phi");
            var json = bridge.Result!;
            Check(json["Stages"]![2]!["History"]!["Fibers"]!.AsArray().Count > 100, "JSON senza fibre storiche");
            Check(json.ToJsonString().Contains("PlasticStrain"), "JSON perde lo stato plastico");
            var result = bridge.Calculation;
            for (int i = 0; i < 5; i++) { bridge.Results.SelectedIndex = i; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Check(ReferenceEquals(result, bridge.Calculation), "navigazione invalida calcolo"); }
            bridge.Results.SelectedIndex = 1;
            File.WriteAllBytes(Path.Combine(directory, "storico_non_lineare.png"), Ui.Snapshot(window));
            bridge.DetachView(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var detached = bridge.detachedWindow!; detached.UpdateLayout();
            Check(detached.IsVisible && Ui.Descendants<BridgeDrawing>(detached).Single() == bridge.Drawing, "vista non staccata realmente");
            Check(!Ui.Descendants<BridgeDrawing>(bridge).Any(), "duplicazione vista invece di distacco");
            var selection = Ui.Descendants<ComboBox>(detached).First(c => c.Items.Count == result!.Stages.Count && c != bridge.DisplayChoice);
            selection.SelectedIndex = 0; Check(bridge.StageChoice.SelectedIndex == 0 && bridge.Drawing.Stage == result!.Stages[0], "fase non sincronizzata");
            selection.SelectedIndex = 2;
            var full = Ui.Descendants<Button>(detached).Single(b => b.Content?.ToString() == "Schermo intero · F11");
            full.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(detached.WindowState == WindowState.Maximized && detached.WindowStyle == WindowStyle.None, "schermo intero");
            File.WriteAllBytes(Path.Combine(directory, "storico_schermo_intero.png"), Ui.Snapshot(detached));
            full.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bridge.Pages.SelectedIndex = 0; Check(bridge.Drawing.Stage is not null && bridge.Drawing.Mode != 2, "editing geometria spegne vista staccata delle tensioni");
            bridge.Pages.SelectedIndex = 1;
            var last = bridge.InputForms.Last(f => f.Editors.ContainsKey("Mx")); last.Set("Mx", "3100", true); data.Array("fasi")[2]!["Mx"] = 3100;
            await bridge.CalculateAsync(false); await Ready();
            Check(!ReferenceEquals(result, bridge.Calculation) && bridge.Drawing.Stage == bridge.Calculation!.Stages.Last(), "ricalcolo non aggiorna finestra staccata");
            detached.Close(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            Check(bridge.detachedWindow is null && Ui.Descendants<BridgeDrawing>(bridge).Single() == bridge.Drawing, "riaggancio");
            byte[] report = ReportBridge.Create("Prova del metodo storico non lineare", bridge.Calculation!, ReportBridge.DefaultSections());
            File.WriteAllBytes(Path.Combine(directory, "storico_non_lineare.docx"), report);
            using (var zip = new ZipArchive(new MemoryStream(report)))
            using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            { string xml = reader.ReadToEnd(); Check(xml.Contains("non lineare") && xml.Contains("εmecc") && xml.Contains("non verificati"), "report del metodo errato"); }
            choice.SelectedItem = BridgeSection.CalculationMethods[0]; await Ready();
            Check(bridge.Calculation!.Stages.All(s => s.GetHistory() is null && s.Shear is not null), "ripristino cumulativo / accessori");
            choice.SelectedItem = BridgeSection.CalculationMethods[1]; await Ready();
            Check(bridge.Calculation!.Stages.All(s => s.GetHistory() is not null && s.Shear is null), "risultati accessori precedenti conservati nel metodo storico");
            bridge.DetachView(); bridge.Dispose(); Check(bridge.detachedWindow is null, "chiusura modulo lascia finestra orfana");
            File.WriteAllText(Path.Combine(directory, "storico_ui.txt"), checks + " controlli superati");
        }
        finally { window.Close(); }
    }
}
