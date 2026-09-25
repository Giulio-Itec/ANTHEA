using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckBridgeShearUi(string directory)
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var data = BridgeSection.Defaults(); data["pioli"] = true; data["irrigidimenti"] = true; data["t_irr"] = 25;
        data.Array("fasi")[1]!["V"] = 600; data.Array("fasi")[2]!["V"] = 500;
        using var bridge = new BridgeWorkspace(data);
        var window = Ui.Dialog(this, "Prova taglio pioli e contouring", bridge, 1600, 990); window.Show();
        try
        {
            async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
            async Task Wait()
            {
                var deadline = DateTime.UtcNow.AddSeconds(45);
                while (bridge.Calculation is null || bridge.Busy)
                { if (DateTime.UtcNow > deadline) throw new Exception("Calcolo UI taglio non disponibile."); await Task.Delay(60); }
                await Layout();
            }
            void Add()
            {
                var content = (DependencyObject)((TabItem)bridge.Results.Items[0]).Content;
                Ui.Descendants<Button>(content).Single(b => b.Content?.ToString() == "+ Carico").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            await Wait(); bridge.Pages.SelectedIndex = 1; bridge.Results.SelectedIndex = 0; await Layout();
            Check(bridge.Calculation!.Stages.Last().Studs!.Enabled && bridge.Calculation.Stages.Last().Shear!.UsesStiffeners, "Nuovi dati non ricevuti dal calcolo.");
            for (int i = 0; i < 8; i++) Add();
            await Wait();
            Check(data.Array("fasi").Count == 11 && bridge.ActionsTable.Items.Count == 11, "Fasi perse dopo aggiunte rapide.");
            Check(bridge.StageChoice.SelectedIndex == 10 && bridge.Calculation!.Stages.Last().Contributions.Count == 11, "Nuove fasi escluse dalla situazione visualizzata.");
            bridge.ActionsTable.Rows[3]["Mx"] = "240"; await Wait();
            Check(bridge.StageChoice.SelectedIndex == 10 && bridge.ActionsTable.Items.Count == 11, "Modifica tabellare perde le fasi.");
            bridge.ActionsTable.Rows[10]["attiva"] = false; await Wait();
            Check(bridge.StageChoice.SelectedIndex == 9 && bridge.ActionsTable.Items.Count == 11, "Fase inattiva rimossa dagli ingressi.");
            bridge.StageChoice.SelectedIndex = 2; bridge.ActionsTable.Rows[2]["Mx"] = "3200"; await Wait();
            Check(bridge.StageChoice.SelectedIndex == 2, "La scelta esplicita di una situazione precedente non è conservata.");
            bridge.StageChoice.SelectedIndex = bridge.StageChoice.Items.Count - 1; bridge.Commit();
            using (var reopened = new BridgeWorkspace((JsonObject)data.DeepClone()))
            {
                await reopened.CalculateAsync();
                Check(reopened.ActionsTable.Rows.Count == 11 && reopened.StageChoice.SelectedIndex == 9, "Archivio riaperto perde fasi/selezione.");
            }
            // Exercise independent view switches without recalculating.
            var original = bridge.Calculation;
            foreach (var toggle in new[] { bridge.ContourSectionToggle, bridge.ContourDiagramToggle, bridge.StressLimitsToggle })
            { toggle.IsChecked = true; toggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); }
            await Layout();
            Check(bridge.Drawing.ContourSection && bridge.Drawing.ContourDiagram && bridge.Drawing.ShowStressLimits, "Opzioni contouring non applicate.");
            Check(ReferenceEquals(original, bridge.Calculation), "La vista invalida il calcolo.");
            Check(BridgeDrawing.UtilizationColor(1.1).R > BridgeDrawing.UtilizationColor(1.1).G, "Superamento non rosso.");
            Check(BridgeDrawing.UtilizationColor(null, true) != BridgeDrawing.UtilizationColor(.5), "CLS teso appare verificato.");
            Check(data["ui_mista"].B("contour_sezione") && data["ui_mista"].B("limiti_tensioni"), "Preferenze non persistite.");
            bridge.Results.SelectedIndex = 4; await Layout();
            Check(Ui.Descendants<DataGrid>((DependencyObject)((TabItem)bridge.Results.Items[4]).Content).Count() == 3, "Risultati taglio/scorrimento incompleti.");
            File.WriteAllBytes(Path.Combine(directory, "taglio_pioli_contour.png"), Ui.Snapshot(window));
            window.Width = 1366; window.Height = 900; await Layout();
            File.WriteAllBytes(Path.Combine(directory, "taglio_pioli_1366.png"), Ui.Snapshot(window));
            // A compact example for report review; also remove phases while calculation is pending.
            bridge.Results.SelectedIndex = 0; await Layout();
            while (data.Array("fasi").Count > 3)
            {
                bridge.ActionsTable.SelectedIndex = bridge.ActionsTable.Rows.Count - 1;
                var content = (DependencyObject)((TabItem)bridge.Results.Items[0]).Content;
                Ui.Descendants<Button>(content).Single(b => b.Content?.ToString() == "Elimina").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            await Wait();
            Check(bridge.ActionsTable.Items.Count == 3 && bridge.Calculation!.Stages.Count == 3, "Rimozione fasi non riconciliata.");
            bridge.ActionsTable.Rows[2]["tipo"] = "Soletta esclusa"; await Wait();
            bridge.InputForms.First(f => f.Editors.ContainsKey("normativa")).Set("normativa", BridgeSection.Standards[1]); await Wait();
            Check(bridge.InputForms.Count(f => f.Editors.ContainsKey("phi")) == 2, "φ/n della connessione EC non disponibili dopo il cambio norma.");
            var crackedNormal = bridge.Calculation!.Stages.Last().Points.Select(p => p.Stress).ToArray();
            bridge.ActionsTable.Rows[2]["n"] = "19"; await Wait();
            Check(crackedNormal.SequenceEqual(bridge.Calculation!.Stages.Last().Points.Select(p => p.Stress)), "n dello scorrimento modifica la fase a soletta esclusa.");
            Check(bridge.Calculation.Stages.Last().Studs!.Contributions.Last().Basis.StartsWith("EC4"), "Cambio norma non propagato alla connessione.");
            bridge.InputForms.First(f => f.Editors.ContainsKey("normativa")).Set("normativa", BridgeSection.Standards[0]);
            bridge.ActionsTable.Rows[2]["tipo"] = "Composta"; await Wait();
            File.WriteAllBytes(Path.Combine(directory, "taglio_pioli_report.docx"), bridge.BuildReport("Verifica taglio e connessione", new HashSet<string> { "taglio", "geometria", "grafici" }));
            bridge.Pages.SelectedIndex = 0; await Layout();
            File.WriteAllBytes(Path.Combine(directory, "taglio_pioli_geometria.png"), Ui.Snapshot(window));
            File.WriteAllText(Path.Combine(directory, "taglio_pioli_ui.txt"), $"OK: {checks} controlli; 11 fasi, aggiunte rapide, modifiche, inattivazione, selezione, riapertura, contouring indipendente, limiti e report.");
        }
        finally { window.Close(); }
    }
}
