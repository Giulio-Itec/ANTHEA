using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeRetainingWall(string directory)
    {
        testing = true; Directory.CreateDirectory(directory); WindowState = WindowState.Normal; Width = 1600; Height = 1000;
        var messages = new List<string>();
        void Check(bool ok, string text) { if (!ok) throw new Exception(text); messages.Add("OK " + text); }
        async Task Settle() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        document = Archivio.Documento(RetainingWall.Module); dirty = false; currentSheet = null; ShowSheet(document);
        var w = editor!.retainingWall!; await editor.CalculateAsync(); await Settle();
        Check(w.Calculation is not null && editor.HasResults && !editor.Busy, "Apertura e calcolo nel catalogo");
        Check(w.Family.Items.Cast<ComboBoxItem>().Count(i => i.IsEnabled) == 2, "Soltanto mensola e gravità selezionabili");
        Check(!ProjectSharedData.Fields(document).ContainsKey("diameter_mm") && !ProjectSharedData.Fields(document).ContainsKey("verticali_indagate"), "Nessun campo del palo attribuito al muro");
        File.WriteAllBytes(Path.Combine(directory, "mensola_1600.png"), Ui.Snapshot(this));
        string initial = w.Data.ToJsonString(); double force = w.Calculation!.Cases[0].Horizontal;
        Check(w.Pages.Items.Count == 2, "Due sole schede Input e Verifiche");
        w.Forms["action"].Set("value", "20"); Check(!editor.HasResults, "Invalidazione immediata"); await w.CalculateAsync();
        Check(w.Calculation!.Cases[0].Horizontal > force, "Ricalcolo sovraccarico");
        w.Forms["geometry"].Set("height", ""); await w.CalculateAsync(); Check(!editor.HasResults, "Campo vuoto non lascia risultati obsoleti");
        w.Forms["geometry"].Set("height", "3"); await w.CalculateAsync(); Check(editor.HasResults, "Ripristino dopo input incompleto");
        w.Pages.SelectedIndex = 1; w.SeismicButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Settle();
        Check(w.Pages.SelectedIndex == 0 && w.Cards["Azioni"].IsExpanded && w.Cards["Sisma"].IsExpanded && w.Forms["seismic"].Editors["enabled"].IsVisible, "Accesso diretto ai parametri sismici da Verifiche");
        Check(w.Data["seismic"].S("source") == RetainingWall.SeismicSite && w.Forms["seismic"].Editors["ag_g"].IsVisible, "Nuovi muri propongono il calcolo dai dati del sito");
        w.Forms["seismic"].Set("source", RetainingWall.SeismicManual);
        var seismicFlag = (CheckBox)w.Forms["seismic"].Editors["enabled"]; seismicFlag.IsChecked = true;
        w.Forms["seismic"].Set("kh", "0.12"); w.Forms["seismic"].Set("kv", "0.04"); await w.CalculateAsync();
        var seismicCases = w.Calculation!.Cases.Where(c => c.State == "SISMA").ToArray();
        Check(seismicCases.Length == 2 && seismicCases.All(c => c.Kh == .12 && c.PressureDetails.All(p => p.Dynamic > 0 && p.Ke > p.K)) && seismicCases.Select(c => c.Kv).Order().SequenceEqual(new[] { -.04, .04 }), "Attivazione UI genera SISMA ±kv e incremento Mononobe–Okabe");
        Check(w.SeismicButton.Content.ToString()!.Contains("2 combinazioni"), "Stato sisma riporta le combinazioni effettivamente calcolate");
        w.Combination.SelectedItem = seismicCases[0].Name; await Settle(); File.WriteAllBytes(Path.Combine(directory, "sisma-mononobe.png"), Ui.Snapshot(this));
        w.Forms["seismic"].Set("method", "Wood semplificato"); await w.CalculateAsync(); var woodCase = w.Calculation!.Cases.First(c => c.State == "SISMA");
        Check(woodCase.PressureDetails.All(p => Math.Abs(p.K - .5) < 1e-9 && Math.Abs(p.Dynamic - .12 * 18 * 3.45) < 1e-9), "Scelta Wood dalla UI aggiorna K₀ e incremento sismico");
        w.Combination.SelectedItem = woodCase.Name; await Settle(); File.WriteAllBytes(Path.Combine(directory, "sisma-wood.png"), Ui.Snapshot(this));
        w.Forms["seismic"].Set("method", "Mononobe–Okabe"); w.Forms["seismic"].Set("source", RetainingWall.SeismicSite);
        w.Forms["seismic"].Set("ag_g", "0.20"); w.Forms["seismic"].Set("f0", "2.5"); w.Forms["seismic"].Set("soil_class", "C");
        w.Forms["seismic"].Set("topography", RetainingWall.TopographySlope); w.Forms["seismic"].Set("slope", "20"); w.Forms["seismic"].Set("relief_height", "100"); w.Forms["seismic"].Set("site_height", "50");
        await w.CalculateAsync(); var siteCases = w.Calculation!.Cases.Where(c => c.State == "SISMA").ToArray();
        Check(siteCases.Length == 4 && siteCases.Where(c => c.Factors.S("purpose") == "Generale").All(c => Math.Abs(c.Kh - .11704) < 1e-9), "Input del sito genera kh da Ss e St e quattro casi SLV");
        Check(siteCases.Where(c => c.Factors.S("purpose") == "Ribaltamento").All(c => Math.Abs(c.Kh - .17556) < 1e-9), "Ribaltamento usa il coefficiente maggiorato calcolato dalla UI");
        w.Combination.SelectedItem = siteCases[0].Name; w.Forms["seismic"].Editors["source"].BringIntoView(); await Settle(); File.WriteAllBytes(Path.Combine(directory, "sisma-sito-input.png"), Ui.Snapshot(this));
        Ui.Descendants<TextBlock>(w.Cards["Sisma"]).Single(t => t.Text.StartsWith("SLV ·")).BringIntoView(); await Settle(); File.WriteAllBytes(Path.Combine(directory, "sisma-sito-coefficienti.png"), Ui.Snapshot(this));
        editor.ExportReport(Path.Combine(directory, "sisma-sito.docx"), "Sisma da parametri del sito", []);
        w.Forms["seismic"].Set("ag_g", ""); await w.CalculateAsync(); Check(!editor.HasResults, "ag/g mancante invalida il calcolo senza usare kh precedente");
        w.Forms["seismic"].Set("ag_g", "0.20"); await w.CalculateAsync(); w.Forms["seismic"].Set("source", RetainingWall.SeismicManual);
        Check(w.Forms["seismic"].Get("kh") == w.Data["seismic"].S("kh"), "Ritorno al manuale mostra il coefficiente realmente usato");
        w.Forms["seismic"].Set("kh", "0.12"); w.Forms["seismic"].Set("kv", "0.04");
        seismicFlag.IsChecked = false; w.Forms["seismic"].Set("method", "Mononobe–Okabe"); await w.CalculateAsync();
        Check(w.Calculation!.Cases.All(c => c.State != "SISMA") && w.SeismicButton.Content.ToString()!.Contains("non attivo"), "Esclusione sisma rimuove le combinazioni automatiche");
        Exception? transferError = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<SoilTransferDialog>().Single();
            try { File.WriteAllBytes(Path.Combine(directory, "trasferisci-terreno.png"), Ui.Snapshot(dialog)); dialog.SendButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            catch (Exception ex) { transferError = ex; dialog.DialogResult = false; }
        }));
        w.SoilButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); if (transferError is not null) throw transferError; await Settle();
        var pileWindow = Application.Current.Windows.OfType<MainWindow>().Single(window => !ReferenceEquals(window, this));
        Check(pileWindow.editor!.Module == "geo_palo_verticale" && pileWindow.editor.Data["stratigrafie"]![0]![0].D("angolo_attrito") == 30, "Invio terreno apre la portanza palo con il profilo del muro");
        Check(pileWindow.editor.Data["generali"].S("lunghezza") == "" && pileWindow.editor.Data["stratigrafie"]![0]![0].S("addensamento") == "", "Il nuovo palo richiede i dati specifici senza inventarli");
        File.WriteAllBytes(Path.Combine(directory, "terreno-nel-palo.png"), Ui.Snapshot(pileWindow)); pileWindow.Close();
        Check(w.Calculation is not null && ReferenceEquals(editor!.retainingWall, w), "Invio terreno conserva dati e risultati del muro");
        w.Cards["Materiali"].BringIntoView(); await Settle(); File.WriteAllBytes(Path.Combine(directory, "materiali.png"), Ui.Snapshot(this));
        w.Forms["zones"].Set("two_zones", "true"); w.Data["reinforcement"]!["two_zones"] = true; w.Forms["rebar_stem_upper"].Set("diameter", "12"); await w.CalculateAsync();
        w.Cards["Geometria"].BringIntoView(); await Settle(); File.WriteAllBytes(Path.Combine(directory, "geometria.png"), Ui.Snapshot(this));
        w.AddAction("Urto"); w.Forms["action"].Set("value", "25"); w.Forms["action"].Set("z", "2.5"); await w.CalculateAsync();
        Check(w.Calculation!.Cases.Any(c => c.State == "ECCEZIONALE"), "Urto genera combinazione eccezionale");
        var retained = w.Calculation; w.ActionGrid.Rows[^1]["visible"] = false;
        Check(ReferenceEquals(retained, w.Calculation), "Visibilità del carico indipendente dal calcolo"); w.ActionGrid.Rows[^1]["visible"] = true;
        w.Cards["Azioni"].BringIntoView(); await Settle(); File.WriteAllBytes(Path.Combine(directory, "azioni.png"), Ui.Snapshot(this));
        w.GenerateMatrix(); await w.CalculateAsync(); var row = w.MatrixGrid.Rows.First(r => r.Values.S("state") == "SLU"); row["wall"] = "1.2"; await w.CalculateAsync();
        Check(w.Data.S("combination_mode") == "Personalizzate" && w.Calculation!.Cases.Any(c => c.WallFactor == 1.2), "Modifica combinazione usata dal motore");
        seismicFlag.IsChecked = true; await w.CalculateAsync();
        Check(w.SeismicButton.Content.ToString()!.Contains("nessuna combinazione") && w.Calculation!.Cases.Any(c => c.WallFactor == 1.2), "Matrice personalizzata senza SISMA segnalata senza perdere le modifiche");
        seismicFlag.IsChecked = false; await w.CalculateAsync();
        w.Cards["Combinazioni"].BringIntoView(); await Settle(); File.WriteAllBytes(Path.Combine(directory, "combinazioni.png"), Ui.Snapshot(this));
        w.Pages.SelectedIndex = 1;
        w.Combination.SelectedItem = w.Calculation!.Cases.Last(c => c.State == "SLU").Name;
        foreach (string mode in new[] { "Geometria e carichi", "Sollecitazioni", "Forze resistenti", "Tassi di lavoro", "Armature" })
        {
            w.ViewMode.SelectedItem = mode; await Settle(); File.WriteAllBytes(Path.Combine(directory, "risultati_" + mode + ".png"), Ui.Snapshot(this));
            Check(w.Diagrams.InspectionCount > 0, "Vista interrogabile: " + mode);
        }
        w.CheckFilter.SelectedItem = "Calcolo delle spinte"; await Settle(); File.WriteAllBytes(Path.Combine(directory, "spinte.png"), Ui.Snapshot(this));
        w.CheckFilter.SelectedItem = "Sollecitazioni numeriche"; await Settle(); w.SelectedResultsGrid!.SelectedIndex = 4;
        Check(w.SendSectionButton.IsEnabled, "Tasto sezione c.a. abilitato sulla selezione");
        w.SendSectionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Settle();
        var opened = Application.Current.Windows.OfType<MainWindow>().Single(window => !ReferenceEquals(window, this));
        Check(opened.editor!.Module == "str_palo" && opened.editor.Data["input"].D("width_mm") == 1000, "Apertura del vero modulo c.a. con fascia unitaria");
        Check(opened.editor.Data["input"].D("fck_mpa") == w.Data["materials"].D("fck") && opened.editor.Data["input"].D("top_bar_diameter_mm") == 12, "Sezione c.a. mantiene materiali e armatura della zona selezionata");
        opened.Commit(); Archivio.Scrivi(Path.Combine(directory, "sezione-inviata.anthea"), opened.document);
        File.WriteAllBytes(Path.Combine(directory, "sezione-inviata.png"), Ui.Snapshot(opened)); opened.Close();
        Check(ReferenceEquals(editor!.retainingWall, w) && w.Calculation is not null, "Invio sezione conserva il muro e i suoi risultati");
        w.CheckFilter.SelectedItem = "Riepilogo completo";
        w.Pages.SelectedIndex = 0; w.Cards["Terreno"].BringIntoView();
        foreach (int size in new[] { 1366, 900 }) { Width = size; Height = 800; await Settle(); File.WriteAllBytes(Path.Combine(directory, "mensola_" + size + ".png"), Ui.Snapshot(this)); }
        Width = 1600; Height = 1000; w.Example("gravity"); await w.CalculateAsync(); await Settle();
        Check(w.Calculation!.Input.S("family") == "gravity" && w.Calculation.SteelKg == 0, "Esempio gravità e motore dedicato");
        File.WriteAllBytes(Path.Combine(directory, "gravita_1600.png"), Ui.Snapshot(this));
        editor.ExportReport(Path.Combine(directory, "gravita.docx"), "Muro a gravità", []); editor.ExportResult(Path.Combine(directory, "gravita.json"));
        w.Example("cantilever"); var pending = w.CalculateAsync(); w.Forms["action"].Set("value", "35"); await pending; await w.CalculateAsync();
        Check(w.Calculation!.Input["actions"]![0].D("value") == 35, "Una modifica durante il calcolo scarta il risultato precedente");
        Commit(); string file = Path.Combine(directory, "mensola.anthea"); Archivio.Scrivi(file, document); var loaded = Archivio.Leggi(file);
        Check(loaded["dati"]!["actions"]![0].D("value") == 35, "Salvataggio dal workspace");
        editor.ExportReport(Path.Combine(directory, "mensola.docx"), "Muro a mensola", []);
        using (var reportZip = System.IO.Compression.ZipFile.OpenRead(Path.Combine(directory, "mensola.docx")))
            Check(reportZip.Entries.Count(e => e.FullName.StartsWith("word/media/")) == 4, "Relazione Word con sezione e tre diagrammi incorporati");
        editor.ExportResult(Path.Combine(directory, "mensola.json"));
        var sameEditor = editor; ShowHome(); ResumeCalculation(); Check(ReferenceEquals(sameEditor, editor), "Riprendi conserva il workspace");
        await Settle();
        var wallData = (JsonObject)w.Data.DeepClone();
        document = ProjectDocuments.CreateArchive(); var project = ProjectDocuments.AddProject(document, "Test trasferimento muro"); var wallSheet = ProjectDocuments.AddSheet(project, RetainingWall.Module); wallSheet["dati"] = wallData;
        currentSheet = null; ShowSheet(wallSheet); await editor!.CalculateAsync(); var projectWall = editor.retainingWall!;
        var projectResult = projectWall.Calculation!; var selectedCase = projectResult.Cases[0]; var selectedForce = selectedCase.Sections.Last(s => s.Name == "Fusto");
        OpenWallConcreteSection(RetainingWall.ExportSection(projectResult, "Fusto", selectedForce.Position, selectedCase.Name), "Sezione dal muro"); await Settle();
        Check(project.Array("fogli").Count == 2 && editor!.Module == "str_palo" && wallSheet["dati"]!["actions"]![0].D("value") == 35, "Invio nel progetto crea un nuovo foglio e conserva il muro");
        OpenModuleCopy("geo_palo_verticale", SoilProfileTransfer.Create("geo_palo_verticale", SoilProfileTransfer.Extract(RetainingWall.Module, wallData)), "Terreno dal muro"); await Settle();
        Check(project.Array("fogli").Count == 3 && editor!.Module == "geo_palo_verticale", "Invio terreno nel progetto crea un foglio palo nella stessa sezione");
        File.WriteAllLines(Path.Combine(directory, "smoke.txt"), messages);
    }
}
