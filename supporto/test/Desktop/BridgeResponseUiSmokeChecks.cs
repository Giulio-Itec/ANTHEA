using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;
namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeBridgeResponse(string directory)
    { testing = true; Directory.CreateDirectory(directory); await CheckBridgeResponseUi(directory); File.WriteAllText(Path.Combine(directory, "smoke.txt"), "Curve della sezione: completato"); }
    private async Task CheckBridgeResponseUi(string directory)
    {
        var data = BridgeSection.Defaults();
        using var bridge = new BridgeWorkspace(data);
        var window = new Window { Title = "ANTHEA · Curve della sezione", Owner = this, Width = 1600, Height = 1000, Content = bridge, Background = Ui.Bg };
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Curve UI: " + message); }
        async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
        void Set(string key, string value) { bridge.ResponseData[key] = value; bridge.ResponseForm.Set(key, value, true); }
        try
        {
            window.Show(); bridge.Pages.SelectedIndex = 2; await Layout();
            Check(bridge.Pages.Items.Count == 3 && bridge.ResponseForm.IsVisible, "scheda nuova assente");
            Set("punti", "30"); Set("incremento_k", "0.003");
            await bridge.CalculateResponseAsync(); await Layout();
            Check(bridge.ResponseCalculation is { Completed: true }, "M-kappa non calcolato: " + (bridge.ResponseCalculation?.Message ?? string.Join(" | ", Ui.Descendants<TextBlock>(bridge).Select(t => t.Text).Where(t => t.Contains("Curva") || t.Contains("annullato")))));
            Check(bridge.ResponseCalculation!.Points.Count == 31, "numero punti");
            Check(((TextBox)bridge.ResponseForm.Editors["incremento_k"]).Text != "0", "curvatura arrotondata a zero");
            Check(Ui.Descendants<BridgeResponsePlot>(bridge).Count() == 2, "grafici di sezione e fibra");
            Check(bridge.ResponseFiber.Items.Count > 100, "fibre disponibili");
            var mc = bridge.ResponseCalculation;
            bridge.ResponsePoint.Value = 8; bridge.ResponseFiber.SelectedIndex = 0; await Layout();
            Check(Ui.Descendants<BridgeResponsePlot>(bridge).All(p => p.Selected == 8), "cursore non sincronizzato");
            File.WriteAllBytes(Path.Combine(directory, "curve_momento_curvatura.png"), Ui.Snapshot(window));
            ((ComboBox)bridge.ResponseForm.Editors["tipo"]).SelectedItem = BridgeSection.ResponseModes[1];
            Check(Ui.Descendants<TextBlock>(bridge).Any(t => t.Text.Contains("curva precedente da ricalcolare")), "cambio modo non segnala curva vecchia");
            Set("incremento_e", "-2000"); await bridge.CalculateResponseAsync(); await Layout();
            Check(bridge.ResponseCalculation is { Completed: true } && bridge.ResponseCalculation != mc, "N-epsilon non calcolato");
            Check(bridge.ResponseCalculation!.Points.Last().State.N < 0, "segno compressione");
            Check(bridge.ResponseCalculation.Points.All(p => p.State.TotalPlane.Curvature == 0), "vincolo curvatura");
            File.WriteAllBytes(Path.Combine(directory, "curve_forza_deformazione.png"), Ui.Snapshot(window));
            ((ComboBox)bridge.ResponseForm.Editors["origine"]).SelectedItem = BridgeSection.ResponseOrigins[1];
            bridge.ResponsePhase.SelectedIndex = 1; Set("incremento_e", "-100");
            await bridge.CalculateResponseAsync(); await Layout();
            Check(bridge.ResponseCalculation is { Completed: true }, "ripresa dopo fase");
            Check(bridge.ResponseCalculation!.Points[0].State.Fibers.Any(f => f.ActivationStrain != 0), "riferimento al getto perso");
            bridge.Pages.SelectedIndex = 0; await Layout();
            Check(bridge.Drawing.IsVisible && !bridge.ResponseForm.IsVisible, "ritorno al pannello geometrico");
            bridge.Pages.SelectedIndex = 2; await Layout();
            Check(bridge.ResponseForm.IsVisible && (int)data["ui_mista"]!.D("tab") == 2, "ripristino scheda");
            Check(data["curve_sezione"]!.S("tipo") == BridgeSection.ResponseModes[1], "preferenze non salvate");
            using var reopened = new BridgeWorkspace((System.Text.Json.Nodes.JsonObject)data.DeepClone());
            Check(reopened.Pages.SelectedIndex == 2, "riapertura perde la scheda");
            File.WriteAllText(Path.Combine(directory, "curve_ui.txt"), checks + " controlli superati");
        }
        finally { window.Close(); }
    }
}
