using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.BridgeDesign;
using X.Core;

namespace X.Desktop;
internal sealed partial class ShellSmokeWindow
{
    /// <summary>
    /// ui/smoke-bridge-traffic on the XAML view of Bridge Design, as the Release opens it since the switch of 23/10/2026 (BridgeDesignSmoke): the same
    /// checks as on the historic view; the editor of γQ is the text box whose DataContext is the parameter (the historic one had the key in Tag).
    /// </summary>
    internal async Task SmokeBridgeTraffic(string directory)
    {
        Directory.CreateDirectory(directory); int checks = 0; var previous = Appearance.Current;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        try
        {
            Width = 1600; Height = 1000; WindowState = WindowState.Normal;
            document = Archivio.Documento(BridgeConcept.Module); dirty = false; currentSheet = null; ShowSheet(document);
            var smoke = BridgeDesignSmoke.Of(editor ?? throw new Exception("Bridge Design non aperto."), Window);
            var sheet = smoke.Sheet; var view = smoke.View;
            async Task Settle() { await sheet.WaitForCalculationAsync(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
            await Settle(); Check(sheet.Calculation?.Traffic is not null, "New sheet does not calculate traffic envelope");
            view.InputTabs.SelectedIndex = 4; view.OutputTabs.SelectedIndex = 1; await Settle();
            ComboBox Model() => Ui.Descendants<ComboBox>(view).Single(c => AutomationProperties.GetName(c) == "Modello di traffico");
            Check(view.InputTabs.Items.Count == 5, "Traffic tab missing");
            // D8 (1.16.0): a new document takes the ultimate factors from the road bridge rows of Model, without the γQ editor; the
            // project multipliers (the choice of the documents saved before) keep the user factor checked below.
            ComboBox Factors() => Ui.Descendants<ComboBox>(view).Single(c => AutomationProperties.GetName(c) == BridgeDesignPresentation.PartialFactorsLabel);
            Check(Equals(Factors().SelectedItem, BridgeDesignModel.ProfileFactors), "New sheet does not use the factors of the Model profile");
            Check(!Ui.Descendants<TextBox>(view).Any(t => t.DataContext is BridgeDesignParameter { Group: "assumptions", Key: "gamma_q" }), "γQ editor shown with the Model profile");
            Factors().SelectedItem = BridgeDesignModel.PartialFactorChoices.Single(c => c != BridgeDesignModel.ProfileFactors); await Settle();
            smoke.Type("assumptions/gamma_q", "1,42"); await Settle();
            Model().SelectedItem = BridgeDesignModel.UniformLegacy; await Settle();
            Check(sheet.Calculation?.Traffic is null && sheet.Data["assumptions"].D("gamma_q") == 1.42, "Switch overwrote factor or kept stale envelope");
            Model().SelectedItem = BridgeDesignModel.Schema1; await Settle();
            Check(sheet.Calculation!.Traffic!.GammaQ == 1.42, "User factor lost when switching to Schema 1");
            smoke.Press("Applica γQ NTC consigliato");
            await Settle(); Check(sheet.Calculation!.Traffic!.GammaQ == 1.35, "Recommended factor not applied");
            Check(sheet.Calculation.Stations.Any(s => s.MomentMinimum < s.Moment), "No distinct min/max curve");
            foreach (var mode in Enum.GetValues<AppAppearance>())
            {
                Appearance.Set(mode, false); await Settle();
                view.OutputTabs.BringIntoView(); UpdateLayout();
                File.WriteAllBytes(Path.Combine(directory, "traffic-" + mode + ".png"), Ui.Snapshot(Window));
                Check(Ui.Descendants<TextBox>(view).Count(t => t.DataContext is BridgeDesignParameter { Group: "assumptions", Key: "gamma_q" }) == 1, "Duplicate traffic coefficient editor");
            }
        }
        finally { Appearance.Set(previous, false); }
        File.WriteAllText(Path.Combine(directory, "traffic-checks.txt"), $"PASS {checks} checks");
    }
}
