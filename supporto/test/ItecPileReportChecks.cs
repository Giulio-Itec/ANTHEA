using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Modules;
using Anthea.Application.Reporting;
using Anthea.Reports;
using Anthea.Presentation.Shell.Commands;
using X.Core;

namespace X.Desktop;

internal static class ItecPileReportChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var timings = new Dictionary<string, double>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        void Timed(string stage) { timings[stage] = Math.Round(clock.Elapsed.TotalMilliseconds, 1); clock.Restart(); }
        var data = Archivio.NuovoFoglio("geo_palo_verticale");
        var g = data["generali"]!;
        g["tipo_palo"] = "Trivellato"; g["diametro"] = "1.2"; g["lunghezza"] = "12";
        g["presenza_falda"] = true; g["profondita_falda"] = "3"; g["peso_specifico_palo"] = "25";
        g["peso_palo_sfavorevole"] = "1.35"; g["peso_palo_favorevole"] = "1.00";
        g["azione_compressione"] = "1000"; g["azione_trazione"] = "200";
        var layer = data.Array("stratigrafie")[0]!.AsArray()[0]!;
        layer["spessore"] = "6"; layer["tipologia"] = "Granulare"; layer["addensamento"] = "Denso";
        layer["peso_specifico"] = "18"; layer["peso_specifico_saturo"] = "20"; layer["angolo_attrito"] = "32";
        layer["coesione_efficace"] = "0"; layer["coesione_non_drenata"] = "0";
        var clay = layer.DeepClone(); clay["tipologia"] = "Coesivo"; clay["spessore"] = "8";
        clay["angolo_attrito"] = "24"; clay["coesione_non_drenata"] = "80"; clay["nc"] = "9";
        data.Array("stratigrafie")[0]!.AsArray().Add(clay);
        clock.Restart();
        var result = Calcolo.Calcola(data);
        Timed("calcolo_ms");
        Check(result.S("errore") == "", "Scenario non calcolabile: " + result.S("errore"));
        string originalData = data.ToJsonString(), originalResult = result.ToJsonString();
        var options = PileReportContents.All(data);
        var request = new ModuleReportRequest("Prova palo — template ITEC", new ModuleCalculation("geo_palo_verticale", data, result), options, false);
        clock.Restart();
        var figures = await new ModuleFigureRenderer().RenderAsync(request, default);
        Timed("grafici_ms");
        Check(figures.Count > 2, "Mancano i grafici reali");
        clock.Restart();
        var document = ReportWord.CreateDocument(request.Title, "geo_palo_verticale", data, result, options, figures.Select(f => f.Image()).ToList());
        Timed("composizione_completa_ms");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace m = "http://schemas.openxmlformats.org/officeDocument/2006/math";
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var xml = document.GetXml("word/document.xml");
        string text = string.Concat(xml.Descendants(w + "t").Select(t => t.Value));
        Check(document.Contains("anthea/cover.pdf"), "Copertina ITEC assente");
        Check(xml.Descendants(m + "oMath").Count() == 8, "Formule Word non native o mancanti");
        Check(!text.Contains("Annesso") && text.Contains("Parametri e tensioni lungo il palo") && text.Contains("Contributi e resistenze lungo il palo"), "Dettagli non presentati come capitoli ordinari");
        Check(!text.Contains("Parametrizzata NQ-2026-09-09:") && !text.Contains("cubiche raccordate"), "Testo Nq ancora presente");
        Check(!text.Contains("[XX]") && !text.Contains("Grafico Nq da inserire"), "Segnaposto nei risultati");
        Check(text.Contains("1,35") && text.Contains("γG favorevole"), "Coefficienti peso mancanti");
        Check(text.Contains("motore corrente considera c′"), "Teoria diversa dal motore attuale");
        Check(text.Contains("I = Ed/Rd") && text.Contains(result["curve"]!["drenante_compressione"]!.Array("progetto")[^1]![1]!.GetValue<double>()
            .ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))), "Riepilogo diverso dai valori non arrotondati del risultato");
        Check(xml.Descendants(a + "blip").Count() >= 3, "Figure non incluse nel report");
        Check(originalData == data.ToJsonString() && originalResult == result.ToJsonString(), "Esportazione modifica input o risultato");
        var path = Path.Combine(directory, "Prova palo ITEC.docx");
        clock.Restart();
        var bytes = DocxWriter.Write(document);
        Timed("pacchetto_docx_ms");
        ReportFiles.WriteWord(path, bytes);
        Timed("scrittura_file_ms");
        Check(File.Exists(path) && !File.Exists(Path.ChangeExtension(path, ".pdf")), "Esportazione non limitata al Word");
        Check(!document.GetXml("word/settings.xml").Descendants(w + "updateFields").Any(), "Aggiornamento automatico dei campi ancora attivo");
        Check(xml.Descendants(w + "hyperlink").Any() && !xml.Descendants(w + "fldSimple").Any(), "Indice non interno al documento");
        foreach (var figure in figures) File.WriteAllBytes(Path.Combine(directory, figure.Category + "-" + figure.Index + ".png"), figure.Png);
        clock.Restart();
        var defaults = ReportWord.CreateDocument(request.Title, "geo_palo_verticale", data, result, images: figures.Select(f => f.Image()).ToList());
        Timed("composizione_predefinita_ms");
        File.WriteAllText(Path.Combine(directory, "tempi.json"), System.Text.Json.JsonSerializer.Serialize(timings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        var defaultXml = defaults.GetXml("word/document.xml");
        Check(defaultXml.Descendants(w + "tbl").Any()
            && !defaultXml.ToString().Contains("Parametri e tensioni lungo il palo") && !defaultXml.ToString().Contains("Contributi e resistenze lungo il palo"), "Contenuti iniziali del report errati");
        Check(!defaultXml.ToString().Contains("INDICE") && !defaultXml.ToString().Contains("Avvisi del calcolo") && !defaultXml.Descendants(w + "headerReference").Any(), "Intestazione, indice o avvisi inclusi per impostazione iniziale");
        ReportFiles.WriteWord(Path.Combine(directory, "Prova palo predefinita.docx"), DocxWriter.Write(defaults));
        var selector = new ReportSectionsDialogViewModel("geo_palo_verticale", data);
        var template = (System.Windows.DataTemplate)System.Windows.Application.Current.FindResource(new System.Windows.DataTemplateKey(typeof(ReportSectionsDialogViewModel)));
        var view = new System.Windows.Controls.ContentPresenter { Content = selector, ContentTemplate = template };
        view.Measure(new System.Windows.Size(574, 611)); view.Arrange(new System.Windows.Rect(0, 0, 574, 611)); view.UpdateLayout();
        var screenshot = new System.Windows.Media.Imaging.RenderTargetBitmap(574, 611, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        screenshot.Render(view);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(screenshot));
        using (var output = File.Create(Path.Combine(directory, "selezione-contenuti.png"))) encoder.Save(output);
        var choices = selector.Sections.SelectMany(s => s.Descendants()).ToDictionary(c => c.Key);
        Check(new[] { "c1", "c2", "c3", "c4", "c5" }.All(key => choices[key].Descendants().All(c => c.IsChecked))
            && new[] { "intestazione", "indice", "c6", "c7", "avvisi" }.All(key => choices[key].Descendants().All(c => !c.IsChecked)), "Selezione iniziale errata");
        var selectedLeaves = choices.Values.Where(c => c.Children.Count == 0 && c.IsChecked).Select(c => c.Key).Append(PileReportContents.Version);
        Check(PileReportContents.Defaults(data).SetEquals(selectedLeaves), "Predefiniti del report diversi dal selettore");
        choices["c1"].ToggleCommand.Execute(null);
        Check(choices["c1"].Descendants().All(c => !c.IsChecked), "Il capitolo non deseleziona tutti i discendenti");
        choices["c1"].ToggleCommand.Execute(null);
        Check(choices["c1"].Descendants().All(c => c.IsChecked), "Il capitolo non seleziona tutti i discendenti");
        choices["tab_coefficienti"].IsChecked = false;
        Check(choices["c1"].Selection is null && choices["normativa"].IsChecked, "Stato parziale del capitolo errato");
        choices["c1"].IsChecked = false;
        Check(choices["c1"].Descendants().All(c => !c.IsChecked), "Deselezione del capitolo incompleta");
        foreach (var choice in selector.Sections) choice.IsChecked = false;
        Check(!selector.CanExport, "Selezione vuota esportabile");
        choices["tab_punta"].IsChecked = true;
        Check(selector.CanExport, "Una singola tabella non è esportabile");
        var onlyTable = ReportWord.CreateDocument("Solo punta", "geo_palo_verticale", data, result, [PileReportContents.Version, "tab_punta"]);
        Check(onlyTable.GetXml("word/document.xml").Descendants(w + "tbl").Count() == 1 && !onlyTable.GetXml("word/document.xml").ToString().Contains("Oggetto e dati generali"), "Selezione singola ignorata");
        File.WriteAllText(Path.Combine(directory, "dati-prova.json"), data.ToJsonString(new() { WriteIndented = true }));
        File.WriteAllText(Path.Combine(directory, "risultati-prova.json"), result.ToJsonString(new() { WriteIndented = true }));
        var calculationFile = Archivio.Documento("geo_palo_verticale"); calculationFile["dati"] = data.DeepClone();
        Anthea.Application.Documents.DocumentService.Save(Path.Combine(directory, "Prova palo ITEC.anthea"), calculationFile);
        // Partial selections, absent actions and incomplete soil coverage must not create favourable outcomes.
        var partial = ReportWord.CreateDocument("Solo risultati", "geo_palo_verticale", data, result, ["risultati"]);
        string partialText = partial.GetXml("word/document.xml").ToString();
        Check(!partialText.Contains("Annesso A") && !partialText.Contains("Teoria di calcolo"), "Selezioni ignorate");
        var incomplete = (JsonObject)result.DeepClone(); incomplete["copertura_completa"] = false;
        var invalid = ReportWord.CreateDocument("Copertura incompleta", "geo_palo_verticale", data, incomplete, ["risultati"]);
        Check(invalid.GetXml("word/document.xml").ToString().Contains("Verifica incompleta: stratigrafia insufficiente"), "Copertura incompleta nascosta");
        var absent = (JsonObject)result.DeepClone(); absent["azioni"] = new JsonObject(); absent["azioni_condizione"] = new JsonObject();
        var noActions = ReportWord.CreateDocument("Azioni assenti", "geo_palo_verticale", data, absent, ["risultati"]);
        Check(noActions.GetXml("word/document.xml").ToString().Contains("Azione non inserita"), "Azione assente interpretata come zero");
        selector.ExportCommand.Execute(null);
        Check(!selector.Result!.Contains(PileReportContents.Brief), "Il selettore della W contiene ancora la modalità breve");
        HashSet<string> briefOptions = [PileReportContents.Version, PileReportContents.Brief];
        var briefFigures = await new ModuleFigureRenderer().RenderAsync(request with { Options = briefOptions }, default);
        Check(briefFigures.Count == 1 && briefFigures[0].Category == "grafico_capacita", "Il breve disegna figure superflue");
        var briefDocument = ReportWord.CreateDocument(request.Title, "geo_palo_verticale", data, result, briefOptions, briefFigures.Select(f => f.Image()).ToArray());
        var briefXml = briefDocument.GetXml("word/document.xml");
        string briefText = string.Concat(briefXml.Descendants(w + "t").Select(t => t.Value));
        Check(briefXml.Descendants(w + "pageBreakBefore").Count() == 1 && briefXml.Descendants(a + "blip").Count() == 1
            && briefText.Contains("Non verificata") && briefText.Contains("1200,76") && briefText.Contains("1458,04") && briefText.Contains("Avviso Nq:"), "Riepilogo breve incompleto o diverso dai risultati");
        Check(!briefText.Contains("Teoria di calcolo") && !briefText.Contains("INDICE") && !briefXml.Descendants(w + "headerReference").Any(), "Contenuti del completo inseriti nel breve");
        var briefPath = Path.Combine(directory, "Prova palo breve.docx");
        ReportFiles.WriteWord(briefPath, DocxWriter.Write(briefDocument));
        Check(!File.Exists(Path.ChangeExtension(briefPath, ".pdf")), "PDF generato per il breve del palo");
        foreach (var (variant, message) in new[] { (absent, "Azione non inserita"), (incomplete, "Verifica incompleta: stratigrafia insufficiente") })
        {
            var invalidBriefDocument = ReportWord.CreateDocument("Dati incompleti", "geo_palo_verticale", data, variant, briefOptions, briefFigures.Select(f => f.Image()).ToArray());
            string briefInvalid = invalidBriefDocument.GetXml("word/document.xml").ToString();
            Check(briefInvalid.Contains(message) && !briefInvalid.Contains(">Verificata<"), "Il breve nasconde un esito non determinabile");
            ReportFiles.WriteWord(Path.Combine(directory, ReferenceEquals(variant, absent) ? "Breve azioni assenti.docx" : "Breve copertura incompleta.docx"), DocxWriter.Write(invalidBriefDocument));
        }
        var variedData = (JsonObject)data.DeepClone();
        variedData["generali"]!["diametro"] = "0.8"; variedData["generali"]!["lunghezza"] = "20"; variedData["generali"]!["azione_trazione"] = "1000";
        var variedLayers = variedData.Array("stratigrafie")[0]!.AsArray();
        variedLayers.Add(layer.DeepClone()); variedLayers.Add(clay.DeepClone());
        variedData.Array("stratigrafie").Add(variedLayers.DeepClone());
        var variedResult = Calcolo.Calcola(variedData);
        Check(variedResult.S("errore") == "", "Variante breve non calcolabile");
        var variedRequest = request with { Calculation = new ModuleCalculation("geo_palo_verticale", variedData, variedResult), Options = briefOptions };
        var variedFigures = await new ModuleFigureRenderer().RenderAsync(variedRequest, default);
        var variedDocument = ReportWord.CreateDocument("Palo con più verticali", "geo_palo_verticale", variedData, variedResult, briefOptions, variedFigures.Select(f => f.Image()).ToArray());
        string variedText = variedDocument.GetXml("word/document.xml").ToString();
        Check(variedText.Contains("Intervalli dei parametri su tutti gli strati") && variedText.Contains(">0,8<") && variedText.Contains(">20<") && !variedText.Contains(">1200,76<"), "Il breve non usa i dati del foglio o nasconde la sintesi stratigrafica");
        ReportFiles.WriteWord(Path.Combine(directory, "Prova palo breve più verticali.docx"), DocxWriter.Write(variedDocument));
        Check(originalData == data.ToJsonString() && originalResult == result.ToJsonString(), "Il report breve modifica input o risultati");
        var project = ReportWord.CreateDocument("Capitolo progetto", "geo_palo_verticale", data, result, ["risultati"], includeInputs: false);
        Check(!project.Contains("anthea/cover.pdf"), "Copertina autonoma inserita nel report di progetto");
        await PileFamilyReportChecks.Run(directory);
        File.WriteAllText(Path.Combine(directory, "esito.txt"), $"PASS {checks} controlli; solo Word; grafici reali {figures.Count}.");
    }
}
