using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckBridgeShrinkageUi(string directory)
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var data = BridgeSection.Defaults(); data["plate2"] = true;
        using var bridge = new BridgeWorkspace(data);
        var window = Ui.Dialog(this, "Prova proprietà, tabella e ritiro", bridge, 1600, 990);
        window.Show();
        try
        {
            async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
            async Task Wait()
            {
                var end = DateTime.UtcNow.AddSeconds(30);
                while (bridge.Calculation is null || bridge.Busy)
                { if (DateTime.UtcNow > end) throw new Exception("Calcolo UI ritiro non disponibile: " + string.Join(" | ", Ui.Descendants<TextBlock>(bridge).Select(t => t.Text).Where(t => t.Contains("Errore")))); await Task.Delay(60); }
                await Layout();
            }
            async Task Edit(int row, string key, string value)
            {
                bridge.Pages.SelectedIndex = 1; bridge.Results.SelectedIndex = 0; await Layout();
                var grid = bridge.ActionsTable; var item = grid.Rows[row]; var column = grid.Columns.Single(c => c.SortMemberPath == key);
                grid.ScrollIntoView(item, column); grid.CurrentCell = new DataGridCellInfo(item, column); grid.Focus();
                Check(grid.BeginEdit(), "Tabella: impossibile modificare " + key); await Layout();
                var editor = column.GetCellContent(item) as TextBox;
                Check(editor is not null, "Editor numerico assente per " + key);
                editor!.Focus(); editor.Text = value; grid.Commit(); bridge.Drawing.Focus();
                await Layout(); await Wait();
                Check(item.Values.S(key) == value, "Valore perso dalla tabella: " + key);
            }
            void ClickTable(string label)
            {
                var content = (DependencyObject)((TabItem)bridge.Results.Items[0]).Content;
                Ui.Descendants<Button>(content).Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            await Wait(); bridge.Pages.SelectedIndex = 0; await Layout();
            Check(bridge.Results.Items.Count == 5, "Ordine delle cinque schede incompleto.");
            Check(bridge.SectionPropertyTabs.IsVisible && bridge.SectionPropertyTabs.Items.Count == 5, "Proprietà della geometria o schede per fase assenti.");
            File.WriteAllBytes(Path.Combine(directory, "proprieta_acciaio.png"), Ui.Snapshot(window));
            bridge.SectionPropertyTabs.SelectedIndex = 1; await Layout();
            var slabForm = Ui.Descendants<InputForm>(bridge.SectionPropertyTabs).Single();
            var beforeSlab = bridge.Calculation; slabForm.Set("n", "16"); await Layout();
            Check(ReferenceEquals(beforeSlab, bridge.Calculation), "L’esplorazione della soletta modifica il calcolo delle fasi.");
            var slabSettings = data["ui_mista"]!["soletta_proprieta"]!;
            Check(slabSettings.D("n") == 16 && slabSettings.D("phi") > 0, "Omogeneizzazione della soletta non sincronizzata.");
            File.WriteAllBytes(Path.Combine(directory, "proprieta_soletta.png"), Ui.Snapshot(window));
            bridge.SectionPropertyTabs.SelectedIndex = 3; await Layout();
            File.WriteAllBytes(Path.Combine(directory, "proprieta_fase.png"), Ui.Snapshot(window));
            await Edit(1, "N", "240");
            Check(bridge.InputForms.Where(f => f.Editors.ContainsKey("Mx")).ElementAt(1).Get("N") == "240", "N della tabella non aggiorna il dettaglio.");
            await Edit(1, "n", "15");
            Check(Math.Abs(bridge.Calculation!.Stages.Last().Contributions[1].HomogenizationN - 15) < 1e-10 && data.Array("fasi")[1].D("phi") > 0, "n tabellare non aggiorna φ/calcolo.");
            await Edit(1, "phi", "1.5");
            Check(Math.Abs(bridge.Calculation!.Stages.Last().Contributions[1].Phi - 1.5) < 1e-10 && data.Array("fasi")[1].D("n") != 15, "φ tabellare non aggiorna n/calcolo.");
            var detail = bridge.InputForms.Where(f => f.Editors.ContainsKey("Mx")).ElementAt(1);
            detail.Set("Mx", "2300"); await Wait();
            Check(bridge.ActionsTable.Rows[1].Values.D("Mx") == 2300 && bridge.ActionsTable.Rows[1]["Mx"]?.ToString() == "2300", "Dettaglio non aggiorna la tabella.");
            var normalStresses = bridge.Calculation!.Stages.Last().Points.Select(p => p.Stress).ToArray();
            await Edit(1, "V", "175");
            Check(normalStresses.SequenceEqual(bridge.Calculation!.Stages.Last().Points.Select(p => p.Stress)) && bridge.Calculation.Stages.Last().Contributions.Sum(c => c.V) == 175, "V altera le tensioni normali o non viene registrato.");
            ClickTable("+ Ritiro"); await Wait();
            Check(data.Array("fasi").Last().S("tipo") == BridgeSection.ShrinkageKind && data.Array("fasi").Last().D("psi") == .55, "Nuova fase di ritiro non inizializzata.");
            await Edit(3, "epsilon_cs", "-250"); await Edit(3, "n", "18");
            var shrink = bridge.Calculation!.Stages.Last().Contributions.Last();
            Check(shrink.IsShrinkage && shrink.ShrinkageStrain == -250e-6 && shrink.ConcreteStressOffset > 0 && shrink.N == 0 && shrink.Mx == 0, "Ritiro non autoequilibrato o con segno errato.");
            Check(!bridge.InputForms.Last(f => f.Editors.ContainsKey("Mx")).Editors["Mx"].IsVisible, "Carichi esterni editabili nel dettaglio Ritiro.");
            bridge.ActionsTable.SelectedIndex = 3;
            for (int i = 0; i < 3; i++) { ClickTable("↑"); await Wait(); }
            Check(data.Array("fasi")[0].S("tipo") == BridgeSection.ShrinkageKind && bridge.Calculation!.Stages[0].Contributions.Single().IsShrinkage, "Ritiro non spostabile prima della fase di solo acciaio.");
            ClickTable("+ Ritiro"); await Wait(); await Edit(4, "epsilon_cs", "-75"); await Edit(4, "phi", "3");
            Check(bridge.Calculation!.Stages.Last().Contributions.Count(c => c.IsShrinkage) == 2, "Più fasi di ritiro non conservate.");
            var lastValid = bridge.Calculation;
            bridge.ActionsTable.Rows[4]["epsilon_cs"] = "invalido"; await Task.Delay(800); await Layout();
            Check(bridge.Calculation is null && bridge.ResultsAreStale && ReferenceEquals(lastValid, bridge.DisplayedCalculation), "Ritiro non valido perde gli ultimi risultati o viene accettato.");
            bridge.ActionsTable.Rows[4]["epsilon_cs"] = "-75"; await Wait();
            bridge.ActionsTable.Rows[4]["attiva"] = false; await Wait();
            Check(bridge.Calculation!.Stages.Count == 4, "Ritiro disattivato ancora incluso.");
            bridge.ActionsTable.Rows[4]["attiva"] = true; await Wait();
            bridge.StageChoice.SelectedIndex = bridge.Calculation.Stages.Count - 1; bridge.DisplayChoice.SelectedIndex = 1; await Layout();
            Check(bridge.Drawing.LoadPoints.Length == 3, "Il ritiro viene disegnato come carico assiale esterno.");
            foreach (int result in new[] { 0, 1, 3, 4 })
            {
                bridge.Results.SelectedIndex = result; await Layout();
                if (result == 0)
                {
                    var scroller = Ui.Descendants<ScrollViewer>(bridge.ActionsTable).First();
                    scroller.ScrollToTop(); scroller.ScrollToLeftEnd(); await Layout();
                    Check(bridge.ActionsTable.FrozenColumnCount == 2, "La fase si perde durante lo scorrimento orizzontale.");
                }
                File.WriteAllBytes(Path.Combine(directory, $"ritiro_risultati_{result}.png"), Ui.Snapshot(window));
            }
            bridge.Pages.SelectedIndex = 0; bridge.SectionPropertyTabs.SelectedIndex = 6; await Layout();
            var headerScroll = Ui.Descendants<ScrollViewer>(bridge.SectionPropertyTabs).First(s => s.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto);
            Check(headerScroll.ScrollableWidth > 0, "Schede delle proprietà non scorrevoli."); headerScroll.ScrollToRightEnd(); await Layout();
            Check(bridge.SectionPropertyTabs.Items.Count == 7, "Proprietà non allineate alle nuove fasi.");
            File.WriteAllBytes(Path.Combine(directory, "proprieta_ritiro.png"), Ui.Snapshot(window));
            bridge.Commit();
            using (var reopened = new BridgeWorkspace((JsonObject)data.DeepClone()))
            {
                await reopened.CalculateAsync();
                Check(reopened.Calculation is not null && JsonNode.DeepEquals(data["fasi"], reopened.Data["fasi"]), "Riapertura perde azioni/ritiro.");
                Check(bridge.Calculation!.Stages.Last().Points.Select(p => p.Stress).SequenceEqual(reopened.Calculation!.Stages.Last().Points.Select(p => p.Stress)), "Riapertura cambia le tensioni da ritiro.");
            }
            var report = bridge.BuildReport("Esempio con due incrementi di ritiro", ReportBridge.DefaultSections());
            File.WriteAllBytes(Path.Combine(directory, "Relazione_ritiro.docx"), report);
            using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(report)))
            using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            {
                var xml = reader.ReadToEnd(); Check(xml.Contains("Neq [kN]") && xml.Contains("ΣV [kN]") && xml.Contains("Correzione σc") && xml.Contains("−Ec,eff"), "Report incompleto per ritiro/taglio.");
            }
            File.WriteAllText(Path.Combine(directory, "ritiro-proprieta-smoke.txt"), $"OK: {checks} controlli sulle proprietà, modifica reale delle celle, sincronizzazione tabella/dettaglio, φ/n, più ritiri e riordino, taglio, grafico, archivio e report.");
        }
        finally { window.Close(); Activate(); }
    }
}
