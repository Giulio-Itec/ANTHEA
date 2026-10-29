using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;
using Anthea.Application.Documents;

namespace X.Desktop;

internal sealed partial class ShellSmokeWindow
{
    internal async Task SmokeRetainingWall(string directory)
    {
        Directory.CreateDirectory(directory); WindowState = WindowState.Normal; Width = 1600; Height = 1000;
        var messages = new List<string>();
        void Check(bool ok, string text) { if (!ok) throw new Exception(text); messages.Add("OK " + text); }
        async Task Settle() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        document = Archivio.Documento(RetainingWall.Module); dirty = false; currentSheet = null; ShowSheet(document);
        // f5-suite-ui: the wall through WallSmoke, with the historic workspace or with the XAML view (docs/refactoring/f5-suite-ui.md).
        var wallEditor = editor; var w = openSheet.Wall; await editor!.CalculateAsync(); await Settle();
        Check(w.Calculation is not null && editor.HasResults && !editor.Busy, "Apertura e calcolo nel catalogo");
        Check(w.AvailableFamilies == 2, "Soltanto mensola e gravità selezionabili");
        Check(!ProjectSharedData.Fields(document).ContainsKey("diameter_mm") && !ProjectSharedData.Fields(document).ContainsKey("verticali_indagate"), "Nessun campo del palo attribuito al muro");
        File.WriteAllBytes(Path.Combine(directory, "mensola_1600.png"), Ui.Snapshot(Window));
        Check(w.Rows("valle") == 1 && w.Rows("monte") == 1, "Due colonne terreno disponibili");
        w.Set("valley", "height_mode", "Assegnato"); w.Set("valley", "free_height", "2");
        w.SetCell("valle", 0, "phi", "26"); await w.CalculateAsync();
        Check(w.Calculation is not null && w.Data["layers"]![0].D("phi") == 30 && w.Calculation.Cases[0].SoilAudit.D("peso_valle_kN_m") > 0, "Input valle indipendente e peso effettivamente calcolato");
        w.Tick("valley", "linked", true); w.SetCell("monte", 0, "phi", "28");
        Check(w.Row("valle", 0).D("phi") == 28, "Modifica monte propagata alla valle collegata");
        w.SetCell("valle", 0, "phi", "27");
        Check(w.Row("monte", 0).D("phi") == 27, "Modifica valle propagata al monte collegato");
        w.Tick("valley", "linked", false); w.Set("interfaces", "base_mode", "Prefabbricato liscio"); w.Set("interfaces", "base_phi_cv", "30");
        await w.CalculateAsync(); Check(w.Calculation is not null && Math.Abs(w.Calculation.Cases[0].SoilAudit.D("delta_base_d_gradi") - 20) < 1e-9, "Attrito automatico dalla UI usato nel calcolo");
        w.BringCardIntoView("Terreno"); await Settle(); File.WriteAllBytes(Path.Combine(directory, "due-colonne-input.png"), Ui.Snapshot(Window));
        bool parametersClosed = await w.ApplyParameterAsync(directory, "valley/free_height", "1.8");
        Check(parametersClosed && Math.Abs(w.Data["valley"].D("free_height") - 1.8) < 1e-9, "Finestra valori applica gli input e ricalcola");
        await w.CalculateAsync();
        File.WriteAllBytes(Path.Combine(directory, "due-colonne-disegno.png"), Ui.Snapshot(w.Drawing));
        w.Example("cantilever"); await w.CalculateAsync(); await Settle();
        string initial = w.Data.ToJsonString(); double force = w.Calculation!.Cases[0].Horizontal;
        Check(w.PageCount == 2, "Due sole schede Input e Verifiche");
        w.Set("action", "value", "20"); Check(!editor.HasResults, "Invalidazione immediata"); await w.CalculateAsync();
        Check(w.Calculation!.Cases[0].Horizontal > force, "Ricalcolo sovraccarico");
        w.Set("geometry", "height", ""); await w.CalculateAsync(); Check(!editor.HasResults, "Campo vuoto non lascia risultati obsoleti");
        w.Set("geometry", "height", "3"); await w.CalculateAsync(); Check(editor.HasResults, "Ripristino dopo input incompleto");
        w.Page = 1; w.ShowSeismic(); await Settle();
        Check(w.Page == 0 && w.IsExpanded("Azioni") && w.IsExpanded("Sisma") && w.IsVisible("seismic", "enabled"), "Accesso diretto ai parametri sismici da Verifiche");
        Check(w.Data["seismic"].S("source") == RetainingWall.SeismicSite && w.IsVisible("seismic", "ag_g"), "Nuovi muri propongono il calcolo dai dati del sito");
        w.Set("seismic", "source", RetainingWall.SeismicManual);
        w.Tick("seismic", "enabled", true);
        w.Set("seismic", "kh", "0.12"); w.Set("seismic", "kv", "0.04"); await w.CalculateAsync();
        // MUR-1 (15-16/10/2026): ogni combinazione sismica con il sovraccarico variabile ha la gemella «b» senza q sulla mensola.
        var seismicAll = w.Calculation!.Cases.Where(c => c.State == "SISMA").ToArray(); var seismicCases = seismicAll.Where(c => c.VariableSurchargeOnHeel).ToArray();
        // Censimento D6 a (28/10/2026): per ogni segno q presente (ψ₂) e assente, quindi 4 righe proprie; le gemelle solo per le righe con q.
        Check(seismicCases.Length == 4 && seismicCases.All(c => c.Kh == .12 && c.PressureDetails.All(p => p.Dynamic > 0 && p.Ke > p.K)) && seismicCases.Select(c => c.Kv).Order().SequenceEqual(new[] { -.04, -.04, .04, .04 }), "Attivazione UI genera SISMA ±kv e incremento Mononobe–Okabe, con q presente e assente");
        Check(seismicAll.Length == seismicCases.Length + 2 && seismicAll.Where(c => !c.VariableSurchargeOnHeel).Select(c => c.Kv).Order().SequenceEqual(new[] { -.04, .04 }), "Gemelle «b» delle combinazioni sismiche senza q sulla mensola (MUR-1)");
        Check(w.SeismicButtonText.Contains($"{seismicAll.Length} combinazioni"), "Stato sisma riporta le combinazioni effettivamente calcolate");
        w.Combination = seismicCases[0].Name; await Settle(); File.WriteAllBytes(Path.Combine(directory, "sisma-mononobe.png"), Ui.Snapshot(Window));
        w.Set("seismic", "method", "Wood semplificato"); await w.CalculateAsync(); var woodCase = w.Calculation!.Cases.First(c => c.State == "SISMA");
        Check(woodCase.PressureDetails.All(p => Math.Abs(p.K - .5) < 1e-9 && Math.Abs(p.Dynamic - .12 * 18 * 3.45) < 1e-9), "Scelta Wood dalla UI aggiorna K₀ e incremento sismico");
        w.Combination = woodCase.Name; await Settle(); File.WriteAllBytes(Path.Combine(directory, "sisma-wood.png"), Ui.Snapshot(Window));
        w.Set("seismic", "method", "Mononobe–Okabe"); w.Set("seismic", "source", RetainingWall.SeismicSite);
        w.Set("seismic", "ag_g", "0.20"); w.Set("seismic", "f0", "2.5"); w.Set("seismic", "soil_class", "C");
        w.Set("seismic", "topography", RetainingWall.TopographySlope); w.Set("seismic", "slope", "20"); w.Set("seismic", "relief_height", "100"); w.Set("seismic", "site_height", "50");
        await w.CalculateAsync(); var siteCases = w.Calculation!.Cases.Where(c => c.State == "SISMA" && c.VariableSurchargeOnHeel).ToArray();
        Check(siteCases.Length == 8 && siteCases.Where(c => c.Factors.S("purpose") == "Generale").All(c => Math.Abs(c.Kh - .11704) < 1e-9), "Input del sito genera kh da Ss e St e otto casi SLV (quattro con q presente e quattro assente, D6 a)");
        Check(siteCases.Where(c => c.Factors.S("purpose") == "Ribaltamento").All(c => Math.Abs(c.Kh - .17556) < 1e-9), "Ribaltamento usa il coefficiente maggiorato calcolato dalla UI");
        w.Combination = siteCases[0].Name; w.BringIntoView("seismic", "source"); await Settle(); File.WriteAllBytes(Path.Combine(directory, "sisma-sito-input.png"), Ui.Snapshot(Window));
        w.BringTextIntoView("Sisma", "SLV ·"); await Settle(); File.WriteAllBytes(Path.Combine(directory, "sisma-sito-coefficienti.png"), Ui.Snapshot(Window));
        editor.ExportReport(Path.Combine(directory, "sisma-sito.docx"), "Sisma da parametri del sito", []);
        w.Set("seismic", "ag_g", ""); await w.CalculateAsync(); Check(!editor.HasResults, "ag/g mancante invalida il calcolo senza usare kh precedente");
        w.Set("seismic", "ag_g", "0.20"); await w.CalculateAsync(); w.Set("seismic", "source", RetainingWall.SeismicManual);
        Check(w.Get("seismic", "kh") == w.Data["seismic"].S("kh"), "Ritorno al manuale mostra il coefficiente realmente usato");
        w.Set("seismic", "kh", "0.12"); w.Set("seismic", "kv", "0.04");
        w.Tick("seismic", "enabled", false); w.Set("seismic", "method", "Mononobe–Okabe"); await w.CalculateAsync();
        Check(w.Calculation!.Cases.All(c => c.State != "SISMA") && w.SeismicButtonText.Contains("non attivo"), "Esclusione sisma rimuove le combinazioni automatiche");
        await w.SendSoilAsync(directory); await Settle();
        // F5.13 g: outside a project the copy opens in a new tab of the shell (MainWindow opened a second window; more tabs, 12.5); the tab closes after.
        var pileEditor = editor!;
        Check(pileEditor.Module == "geo_palo_verticale" && pileEditor.Data["stratigrafie"]![0]![0].D("angolo_attrito") == 30, "Invio terreno apre la portanza palo con il profilo del muro");
        Check(pileEditor.Data["generali"].S("lunghezza") == "" && pileEditor.Data["stratigrafie"]![0]![0].S("addensamento") == "", "Il nuovo palo richiede i dati specifici senza inventarli");
        File.WriteAllBytes(Path.Combine(directory, "terreno-nel-palo.png"), Ui.Snapshot(Window)); editor = null; await Settle();
        Check(w.Calculation is not null && ReferenceEquals(editor, wallEditor), "Invio terreno conserva dati e risultati del muro");
        w.BringCardIntoView("Materiali"); await Settle(); File.WriteAllBytes(Path.Combine(directory, "materiali.png"), Ui.Snapshot(Window));
        w.EnableTwoZones(); w.Set("rebar_stem_upper", "diameter", "12"); await w.CalculateAsync();
        w.BringCardIntoView("Geometria"); await Settle(); File.WriteAllBytes(Path.Combine(directory, "geometria.png"), Ui.Snapshot(Window));
        w.AddAction("Urto"); w.Set("action", "value", "25"); w.Set("action", "z", "2.5"); await w.CalculateAsync();
        Check(w.Calculation!.Cases.Any(c => c.State == "ECCEZIONALE"), "Urto genera combinazione eccezionale");
        var retained = w.Calculation; int lastAction = w.Rows("azioni") - 1; w.SetCell("azioni", lastAction, "visible", false);
        Check(ReferenceEquals(retained, w.Calculation), "Visibilità del carico indipendente dal calcolo"); w.SetCell("azioni", lastAction, "visible", true);
        w.BringCardIntoView("Azioni"); await Settle(); File.WriteAllBytes(Path.Combine(directory, "azioni.png"), Ui.Snapshot(Window));
        w.GenerateMatrix(); await w.CalculateAsync();
        int row = Enumerable.Range(0, w.Rows("matrice")).First(r => w.Row("matrice", r).S("state") == "SLU"); w.SetCell("matrice", row, "wall", "1.2"); await w.CalculateAsync();
        Check(w.Data.S("combination_mode") == "Personalizzate" && w.Calculation!.Cases.Any(c => c.WallFactor == 1.2), "Modifica combinazione usata dal motore");
        w.Tick("seismic", "enabled", true); await w.CalculateAsync();
        Check(w.SeismicButtonText.Contains("nessuna combinazione") && w.Calculation!.Cases.Any(c => c.WallFactor == 1.2), "Matrice personalizzata senza SISMA segnalata senza perdere le modifiche");
        w.Tick("seismic", "enabled", false); await w.CalculateAsync();
        w.BringCardIntoView("Combinazioni"); await Settle(); File.WriteAllBytes(Path.Combine(directory, "combinazioni.png"), Ui.Snapshot(Window));
        w.Page = 1;
        w.Combination = w.Calculation!.Cases.Last(c => c.State == "SLU").Name;
        foreach (string mode in new[] { "Geometria e carichi", "Sollecitazioni", "Forze resistenti", "Tassi di lavoro", "Armature" })
        {
            w.ViewMode = mode; await Settle(); File.WriteAllBytes(Path.Combine(directory, "risultati_" + mode + ".png"), Ui.Snapshot(Window));
            Check(w.Inspections > 0, "Vista interrogabile: " + mode);
        }
        w.CheckFilter = "Calcolo delle spinte"; await Settle(); File.WriteAllBytes(Path.Combine(directory, "spinte.png"), Ui.Snapshot(Window));
        w.CheckFilter = "Sollecitazioni numeriche"; await Settle(); w.SelectResultRow(4);
        Check(w.CanSendSection, "Tasto sezione c.a. abilitato sulla selezione");
        w.SendSection(); await Settle();
        // F5.13 g: the RC section opens in a new tab of the shell (MainWindow: a second window), saved and closed as the second window was.
        var opened = editor!;
        Check(opened.Module == "str_palo" && opened.Data["input"].D("width_mm") == 1000, "Apertura del vero modulo c.a. con fascia unitaria");
        Check(opened.Data["input"].D("fck_mpa") == w.Data["materials"].D("fck") && opened.Data["input"].D("top_bar_diameter_mm") == 12, "Sezione c.a. mantiene materiali e armatura della zona selezionata");
        Commit(); DocumentService.Save(Path.Combine(directory, "sezione-inviata.anthea"), document);
        File.WriteAllBytes(Path.Combine(directory, "sezione-inviata.png"), Ui.Snapshot(Window)); editor = null; await Settle();
        Check(ReferenceEquals(editor, wallEditor) && w.Calculation is not null, "Invio sezione conserva il muro e i suoi risultati");
        w.CheckFilter = "Riepilogo completo";
        w.Page = 0; w.BringCardIntoView("Terreno");
        foreach (int size in new[] { 1366, 900 }) { Width = size; Height = 800; await Settle(); File.WriteAllBytes(Path.Combine(directory, "mensola_" + size + ".png"), Ui.Snapshot(Window)); }
        Width = 1600; Height = 1000; w.Example("gravity"); await w.CalculateAsync(); await Settle();
        Check(w.Calculation!.Input.S("family") == "gravity" && w.Calculation.SteelKg == 0, "Esempio gravità e motore dedicato");
        File.WriteAllBytes(Path.Combine(directory, "gravita_1600.png"), Ui.Snapshot(Window));
        editor.ExportReport(Path.Combine(directory, "gravita.docx"), "Muro a gravità", []); editor.ExportResult(Path.Combine(directory, "gravita.json"));
        w.Example("cantilever"); var pending = w.StartCalculation(); w.Set("action", "value", "35"); await pending; await w.CalculateAsync();
        Check(w.Calculation!.Input["actions"]![0].D("value") == 35, "Una modifica durante il calcolo scarta il risultato precedente");
        Commit(); string file = Path.Combine(directory, "mensola.anthea"); DocumentService.Save(file, document); var loaded = DocumentService.Open(file);
        Check(loaded["dati"]!["actions"]![0].D("value") == 35, "Salvataggio dal workspace");
        editor.ExportReport(Path.Combine(directory, "mensola.docx"), "Muro a mensola", []);
        var reportCaptions = w.ReportFigureCaptions(global: false);
        Check(WallAdvancedChecks.ReportHasFigures(File.ReadAllBytes(Path.Combine(directory, "mensola.docx")), reportCaptions), "Relazione Word con sezione, tre diagrammi, sezione armata e tavole della distinta: " + string.Join(" | ", reportCaptions));
        editor.ExportResult(Path.Combine(directory, "mensola.json"));
        var sameEditor = editor; ShowHome(); ResumeCalculation(); Check(ReferenceEquals(sameEditor, editor), "Riprendi conserva il workspace");
        await Settle();
        var wallData = (JsonObject)w.Data.DeepClone();
        document = ProjectDocuments.CreateArchive(); var project = ProjectDocuments.AddProject(document, "Test trasferimento muro"); var wallSheet = ProjectDocuments.AddSheet(project, RetainingWall.Module); wallSheet["dati"] = wallData;
        currentSheet = null; ShowSheet(wallSheet); await editor!.CalculateAsync(); var projectWall = openSheet.Wall;
        var projectResult = projectWall.Calculation!; var selectedCase = projectResult.Cases[0]; var selectedForce = selectedCase.Sections.Last(s => s.Name == "Fusto");
        OpenWallConcreteSection(Anthea.Application.Walls.WallModel.ExportSection(projectResult, "Fusto", selectedForce.Position, selectedCase.Name), "Sezione dal muro"); await Settle();
        Check(project.Array("fogli").Count == 2 && editor!.Module == "str_palo" && wallSheet["dati"]!["actions"]![0].D("value") == 35, "Invio nel progetto crea un nuovo foglio e conserva il muro");
        OpenModuleCopy("geo_palo_verticale", SoilProfileTransfer.Create("geo_palo_verticale", SoilProfileTransfer.Extract(RetainingWall.Module, wallData)), "Terreno dal muro"); await Settle();
        Check(project.Array("fogli").Count == 3 && editor!.Module == "geo_palo_verticale", "Invio terreno nel progetto crea un foglio palo nella stessa sezione");
        File.WriteAllLines(Path.Combine(directory, "smoke.txt"), messages);
    }
}
