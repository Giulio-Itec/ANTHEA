using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Anthea.Application.BridgeDesign;
using X.Core;

namespace X.Desktop;
public sealed partial class MainWindow
{
    internal async Task SmokeBridgeTraffic(string directory)
    {
        Directory.CreateDirectory(directory); int checks = 0; var previous = Appearance.Current;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        try
        {
            Width = 1600; Height = 1000; WindowState = WindowState.Normal;
            document = Archivio.Documento(BridgeConcept.Module); dirty = false; currentSheet = null; ShowSheet(document);
            var view = historicEditor!.bridgeDesign!;
            async Task Settle() { await view.WaitForCalculationAsync(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
            await Settle(); Check(view.Calculation?.Traffic is not null, "New sheet does not calculate traffic envelope");
            view.Inputs.SelectedIndex = 4; view.Outputs.SelectedIndex = 1;
            ComboBox Model() => Ui.Descendants<ComboBox>(view).Single(c => AutomationProperties.GetName(c) == "Modello di traffico");
            Check(view.Inputs.Items.Count == 5, "Traffic tab missing");
            view.Editors["assumptions/gamma_q"].Text = "1,42"; await Settle();
            Model().SelectedItem = BridgeDesignModel.UniformLegacy; await Settle();
            Check(view.Calculation?.Traffic is null && view.Data["assumptions"].D("gamma_q") == 1.42, "Switch overwrote factor or kept stale envelope");
            Model().SelectedItem = BridgeDesignModel.Schema1; await Settle();
            Check(view.Calculation!.Traffic!.GammaQ == 1.42, "User factor lost when switching to Schema 1");
            Ui.Descendants<Button>(view).Single(b => b.Content is string s && s == "Applica γQ NTC consigliato").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Settle(); Check(view.Calculation!.Traffic!.GammaQ == 1.35, "Recommended factor not applied");
            Check(view.Calculation.Stations.Any(s => s.MomentMinimum < s.Moment), "No distinct min/max curve");
            foreach (var mode in Enum.GetValues<AppAppearance>())
            {
                Appearance.Set(mode, false); await Settle();
                view.Outputs.BringIntoView(); UpdateLayout();
                File.WriteAllBytes(Path.Combine(directory, "traffic-" + mode + ".png"), Ui.Snapshot(this));
                Check(Ui.Descendants<TextBox>(view).Count(t => t.Tag is string s && s == "assumptions/gamma_q") == 1, "Duplicate traffic coefficient editor");
            }
        }
        finally { Appearance.Set(previous, false); }
        File.WriteAllText(Path.Combine(directory, "traffic-checks.txt"), $"PASS {checks} checks");
    }
}
