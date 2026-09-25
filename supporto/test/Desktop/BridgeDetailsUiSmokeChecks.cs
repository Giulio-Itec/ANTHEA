using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckBridgeDetailsUi(string directory)
    {
        int checks = 0;
        void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
        var d = BridgeSection.Defaults(); BridgeSection.EnsureAccessoryDefaults(d);
        d["fasi"] = new JsonArray(BridgeSection.Phase("Combinazione di verifica", "Composta", 500, 2000));
        d.Array("fasi")[0]!["V"] = 1600;
        d["irrigidimenti"] = true; d["t_irr"] = 30; d["lati_irr"] = "Solo sinistra";
        d["appoggio"] = true; d["R_app"] = 500; d["pos_app"] = "Estremità sinistra"; d["c_app"] = 250;
        d["terminale_rigido"] = true; d["e_term"] = 500; d["pioli"] = true;
        d["armatura_trasv"] = true; d["fatica_pioli"] = true; d["q_fat_min"] = -100; d["q_fat_max"] = 200;
        d["flangia_fat_tesa"] = true; d["dsigma_fat"] = 30;
        using var bridge = new BridgeWorkspace(d);
        var window = Ui.Dialog(this, "Verifica dettagli sezione da ponte", bridge, 1600, 990); window.Show();
        try
        {
            async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout(); }
            async Task Wait()
            {
                var deadline = DateTime.UtcNow.AddSeconds(45);
                while (bridge.Calculation is null || bridge.Busy)
                { if (DateTime.UtcNow > deadline) throw new Exception("Aggiornamento dettagli non completato."); await Task.Delay(60); }
                await Layout();
            }
            InputForm Form(string key) => bridge.InputForms.Single(f => f.Editors.ContainsKey(key));
            await Wait(); bridge.Pages.SelectedIndex = 0; await Layout();
            Check(bridge.Pages.Items.Count == 2 && bridge.Results.Items.Count == 5, "Nuove schede non richieste.");
            Check(bridge.DetailSketch.Labels.Any(s => s.Contains("Solo sinistra")), "Prospetto intermedio non aggiornato.");
            Check(bridge.Drawing.VisibleTags.Any(s => s.Contains("SX 150×30") && s.Contains("DX assente")), "Piatti monolaterali non rappresentati.");
            Form("lati_irr").Set("lati_irr", "Bilaterali diversi"); await Wait();
            Check(Form("b_irr_dx").Editors["b_irr_dx"].Visibility == Visibility.Visible, "Piatto destro non editabile.");
            Form("b_irr_dx").Set("b_irr_dx", "180"); Form("t_irr_dx").Set("t_irr_dx", "25");
            ((CheckBox)Form("pannelli_uguali").Editors["pannelli_uguali"]).IsChecked = false;
            Form("a_irr_dx").Set("a_irr_dx", "4500"); await Wait();
            Check(bridge.DetailSketch.Labels.Any(s => s.Contains("4500")), "Pannelli disuguali non visibili.");
            var previous = bridge.Calculation;
            bridge.DetailSectionChoice.SelectedIndex = 1; await Layout();
            Check(ReferenceEquals(previous, bridge.Calculation), "La scelta della sezione locale ricalcola le fasi.");
            Check(bridge.Drawing.DetailAtSupport && bridge.DetailSketch.Labels.Contains("2 coppie"), "Terminale non rappresentato.");
            Check(Form("a_app_sx").Editors["a_app_sx"].Visibility == Visibility.Collapsed, "Pannello oltre l’estremità ancora modificabile.");
            foreach (var expander in Ui.Descendants<Expander>(bridge).Where(e => (e.Header as TextBlock)?.Text == "Appoggi e montanti terminali"))
            { expander.IsExpanded = true; expander.BringIntoView(); }
            await Layout(); File.WriteAllBytes(Path.Combine(directory, "dettagli_appoggio.png"), Ui.Snapshot(window));
            window.Width = 1366; window.Height = 900; await Layout();
            var boxes = bridge.Drawing.TagBounds;
            for (int i = 0; i < boxes.Count; i++) for (int j = 0; j < i; j++) Check(!boxes[i].IntersectsWith(boxes[j]), "Tag dettagli sovrapposti.");
            File.WriteAllBytes(Path.Combine(directory, "dettagli_1366.png"), Ui.Snapshot(window));
            Form("pos_app").Set("pos_app", "Appoggio interno"); await Wait();
            Check(!bridge.Calculation!.Stages.Last().Shear!.RigidEndPost, "Appoggio interno eredita il terminale rigido nascosto.");
            Check(Form("e_term").Editors["e_term"].Visibility == Visibility.Collapsed, "Interasse terminale visibile all’appoggio interno.");
            Form("pos_app").Set("pos_app", "Estremità sinistra"); await Wait();
            bridge.Commit();
            using (var reopened = new BridgeWorkspace((JsonObject)d.DeepClone()))
            {
                await reopened.CalculateAsync();
                Check(reopened.DetailSectionChoice.SelectedIndex == 1 && reopened.Data.S("lati_irr") == "Bilaterali diversi", "Archivio perde dettaglio selezionato.");
                Check(reopened.Data.D("a_irr_dx") == 4500 && reopened.Data.D("q_fat_min") == -100, "Archivio perde ingressi locali.");
            }
            var stresses = bridge.Calculation!.Stages.Last().Points.Select(p => p.Stress).ToArray();
            Form("q_fat_max").Set("q_fat_max", "400"); await Wait();
            Check(stresses.SequenceEqual(bridge.Calculation!.Stages.Last().Points.Select(p => p.Stress)), "Fatica modifica le tensioni delle fasi.");
            bridge.Pages.SelectedIndex = 1; bridge.Results.SelectedIndex = 4; await Layout();
            Check(!bridge.DetailSketch.IsVisible, "Prospetto geometrico invade la scheda tensioni.");
            Check(bridge.Calculation!.Stages.Last().Studs!.Checks.Any(c => c.Name.StartsWith("Fatica · interazione")), "Esito di interazione a fatica mancante.");
            File.WriteAllBytes(Path.Combine(directory, "dettagli_verifiche.png"), Ui.Snapshot(window));
            var bytes = bridge.BuildReport("Appoggi irrigidimenti e connessione della sezione da ponte", new HashSet<string> { "taglio", "geometria", "grafici" });
            File.WriteAllBytes(Path.Combine(directory, "dettagli_ponte.docx"), bytes);
            using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes)))
            {
                using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); string xml = reader.ReadToEnd();
                Check(xml.Contains("Fatica pioli") && xml.Contains("Appoggio · R inviluppo") && xml.Contains("Soletta b–b"), "Report perde dati/verifiche.");
                Check(zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) == 4, "Report senza i due prospetti locali.");
                var documentXml = System.Xml.Linq.XDocument.Parse(xml);
                System.Xml.Linq.XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
                Check(documentXml.Descendants(wp + "extent").Take(2).All(e => Math.Abs((double)e.Attribute("cy")! / (double)e.Attribute("cx")! - 165d / 800) < .001), "Il report distorce il rapporto d’aspetto dei prospetti.");
            }
            File.WriteAllText(Path.Combine(directory, "dettagli_ui.txt"), $"OK: {checks} controlli; lati, pannelli, appoggi, terminali, persistenza, fatica indipendente, report e viste.");
        }
        finally { window.Close(); }
    }
}
