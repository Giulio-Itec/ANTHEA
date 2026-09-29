using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeBridgePredalle(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        await CheckBridgePredalleUi(directory);
    }
    private async Task CheckBridgePredalleUi(string directory)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception("Predalle UI: " + message); checks++; }
        var data = BridgeSection.Defaults(); using var bridge = new BridgeWorkspace(data);
        var window = Ui.Dialog(this, "Predalle · geometria e offset delle armature", bridge, 1600, 990); window.Show();
        try
        {
            async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
            async Task Wait()
            {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (bridge.Calculation is null || bridge.Busy)
                { if (DateTime.UtcNow > deadline) throw new Exception("Predalle: aggiornamento UI non disponibile."); await Task.Delay(60); }
                await Layout();
            }
            await Wait(); bridge.Pages.SelectedIndex = 0; await Layout();
            var form = bridge.InputForms.Single(f => f.Editors.ContainsKey("predalle"));
            var toggle = (CheckBox)form.Editors["predalle"];
            var reference = (ComboBox)form.Editors["rif_ferri_inf"];
            Check(bridge.Drawing.PredalleBounds is null && !data.B("predalle"), "predalle attiva nei vecchi ingressi");
            toggle.IsChecked = true; await Wait();
            Check(bridge.Drawing.PredalleBounds is { Height: > 0 }, "fascia non disegnata");
            Check(bridge.Drawing.Geometry!.Bars.Min(b => b.Y) == 105, "ferri non spostati");
            Check(bridge.Drawing.VisibleTags.Any(t => t.StartsWith("Predalle")) && bridge.Drawing.VisibleTags.Any(t => t.StartsWith("Armatura inferiore") && t.Contains("105 mm")), "tag non aggiornati");
            Check(bridge.Calculation!.Stages.Count == 3, "numero fasi alterato");
            foreach (var size in new[] { (1600d, 990d), (1366d, 768d) })
            {
                window.Width = size.Item1; window.Height = size.Item2; await Layout();
                var boxes = bridge.Drawing.TagBounds;
                Check(boxes.All(b => b.Left >= 0 && b.Top >= 0 && b.Right <= bridge.Drawing.ActualWidth && b.Bottom < bridge.Drawing.ActualHeight), "tag fuori dal viewport");
                for (int i = 0; i < boxes.Count; i++) for (int j = 0; j < i; j++) Check(!boxes[i].IntersectsWith(boxes[j]), "tag sovrapposti");
                File.WriteAllBytes(Path.Combine(directory, $"predalle-{size.Item1:0}.png"), Ui.Snapshot(window));
            }
            window.Width = 1600; window.Height = 990;
            var retained = bridge.Calculation;
            bridge.GeometryLabels.IsChecked = false; await Layout();
            Check(!bridge.Drawing.VisibleTags.Any(t => t.StartsWith("Predalle")) && bridge.Drawing.PredalleBounds is not null && ReferenceEquals(retained, bridge.Calculation), "nascondere tag altera geometria/calcolo");
            bridge.GeometryLabels.IsChecked = true;
            reference.SelectedItem = BridgeSection.SlabBottomReference; await Wait();
            Check(bridge.Drawing.Geometry!.Bars.Min(b => b.Y) == 45, "cambio riferimento non aggiornato");
            reference.SelectedItem = BridgeSection.PredalleTopReference; form.Set("h_predalle", "80"); await Wait();
            Check(bridge.Drawing.Geometry!.Bars.Min(b => b.Y) == 125 && data.D("cover_bottom") == 45, "spessore e offset non sincronizzati");
            bridge.Pages.SelectedIndex = 1; await Layout();
            Check(bridge.Drawing.Stage is not null && bridge.Drawing.PredalleBounds is not null && bridge.Drawing.Geometry!.Bars.Min(b => b.Y) == 125, "vista tensioni usa quote precedenti");
            var contour = bridge.ContourSectionToggle; contour.IsChecked = true; contour.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Layout();
            File.WriteAllBytes(Path.Combine(directory, "predalle-contour.png"), Ui.Snapshot(window));
            form.Set("h_predalle", "250"); await Task.Delay(850); await Layout();
            Check(bridge.Calculation is null && bridge.ResultsAreStale && bridge.Drawing.PredalleBounds is not null, "input invalido cancella l'ultimo disegno");
            form.Set("h_predalle", "60"); await Wait();
            bridge.Pages.SelectedIndex = 0; bridge.SectionPropertyTabs.SelectedIndex = 1; await Layout();
            form.Editors["predalle"].BringIntoView(); await Layout();
            File.WriteAllBytes(Path.Combine(directory, "predalle-input-proprieta.png"), Ui.Snapshot(window));
            var bottom = bridge.InputForms.Single(f => f.Editors.ContainsKey("rebars_bottom"));
            ((CheckBox)bottom.Editors["rebars_bottom"]).IsChecked = false; await Wait();
            Check(bridge.Drawing.Geometry!.Bars.Length == 20 && !bridge.Drawing.VisibleTags.Any(t => t.StartsWith("Armatura inferiore")), "fila assente disegnata");
            ((CheckBox)bottom.Editors["rebars_bottom"]).IsChecked = true; await Wait();
            using (var reopened = new BridgeWorkspace((JsonObject)JsonNode.Parse(data.ToJsonString())!))
            {
                await reopened.CalculateAsync();
                Check(reopened.Calculation!.Geometry.Bars.Min(b => b.Y) == 105, "riapertura perde riferimento");
            }
            File.WriteAllBytes(Path.Combine(directory, "predalle-report.docx"), bridge.BuildReport("Predalle · geometria", ["geometria", "grafici"]));
            toggle.IsChecked = false; await Wait();
            Check(bridge.Drawing.PredalleBounds is null && bridge.Drawing.Geometry!.Bars.Min(b => b.Y) == 45 && data.D("h_predalle") == 60, "disattivazione cancella dati o conserva offset");
            File.WriteAllText(Path.Combine(directory, "predalle-smoke.txt"), $"PASS: {checks} controlli su quote, disegno, aggiornamenti, input invalidi, file opzionali, riapertura e report.");
        }
        finally { window.Close(); }
    }
}
