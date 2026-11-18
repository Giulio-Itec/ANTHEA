using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Presentation.Shell.Commands;
using Anthea.Reports;
using X.Core;

namespace X.Desktop;

internal static class PileFamilyReportChecks
{
    internal static async Task Run(string directory)
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "tests", "fixtures"))) root = root.Parent;
        if (root is null) throw new DirectoryNotFoundException("Fixture non trovate.");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        string Text(DocxDocument doc) => string.Concat(doc.GetXml("word/document.xml").Descendants(w + "t").Select(t => t.Value));
        foreach (var (module, fixture, elastic) in new[] {
            ("geo_micropalo_verticale", "irs-punta-inclinato", false),
            ("geo_palo_orizzontale", "stratificato-misto-manuale", false),
            ("geo_micropalo_orizzontale", "stratificato-chs-catalogo", false),
            ("geo_palo_orizzontale", "single-soil-elastic", true),
            ("geo_micropalo_orizzontale", "elastico-chs-stratificato", true) })
        {
            string name = module.Replace("geo_", "") + (elastic ? "-elastico" : "");
            var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root.FullName, "tests", "fixtures", "f3", "inputs", module + "--" + fixture + ".json")))!["data"]!.DeepClone().AsObject();
            if (module == "geo_micropalo_verticale") ModuleSheetPreparation.PrepareVertical(data, module);
            else HorizontalSheetPresentation.PrepareReport(data);
            JsonObject result;
            if (elastic)
            {
                data["vista_orizzontale"] = "elastico";
                using var session = new ElasticPileAnalysisSession();
                result = await session.CalculateAsync(data, _ => { }, cancellation: default);
            }
            else result = ModuleServices.Get(module).Calculate(data, default).Values;
            Check(result.S("errore") == "", name + ": " + result.S("errore"));
            string beforeData = data.ToJsonString(), beforeResult = result.ToJsonString();
            var selector = new ReportSectionsDialogViewModel(module, data, result);
            var choices = selector.Sections.SelectMany(c => c.Descendants()).ToDictionary(c => c.Key);
            Check(new[] { "c1", "c2", "c3", "c4", "c5" }.All(k => choices[k].Selection == true), name + ": capitoli iniziali");
            Check(new[] { "intestazione", "indice", "avvisi", "c6", "c7" }.All(k => choices[k].Selection == false), name + ": capitoli opzionali");
            choices["c2"].IsChecked = false; choices["c2"].IsChecked = true;
            Check(choices["c2"].Descendants().All(c => c.IsChecked), name + ": selezione discendenti");
            var options = PileFamilyReport.Defaults(module, data, result);
            var request = new ModuleReportRequest(name, new(module, data, result), options, false);
            var figures = await new ModuleFigureRenderer().RenderAsync(request, default);
            var full = ModuleReportService.CreateDocument(request, figures);
            string fullText = Text(full);
            Check(fullText.Contains("Oggetto e dati generali") && fullText.Contains("Teoria di calcolo") && fullText.Contains("Azioni e verifica"), name + ": capitoli assenti");
            Check(!fullText.Contains("INDICE") && !fullText.Contains("Avvisi del calcolo") && !full.GetXml("word/settings.xml").Descendants(w + "updateFields").Any(), name + ": avvisi/campi automatici");
            Check(!full.GetXml("word/document.xml").Descendants(w + "shd").Any(s => ((string?)s.Attribute(w + "fill"))?.StartsWith("E8EF") == true), name + ": tabelle azzurre");
            ReportFiles.WriteWord(Path.Combine(directory, name + "-completo.docx"), DocxWriter.Write(full));
            foreach (var (f, i) in figures.Select((f, i) => (f, i))) File.WriteAllBytes(Path.Combine(directory, name + "-figura-" + i + ".png"), f.Png);
            var all = PileFamilyReport.Outline(module, data, result).SelectMany(c => c.Descendants()).Where(c => c.Children.Count == 0).Select(c => c.Key).Append(PileReportContents.Version).ToHashSet();
            var complete = ModuleReportService.CreateDocument(request with { Options = all }, figures);
            Check(Text(complete).Contains("INDICE") && Text(complete).Contains("Dettagli del calcolo"), name + ": selezione completa");
            ReportFiles.WriteWord(Path.Combine(directory, name + "-tutti-capitoli.docx"), DocxWriter.Write(complete));
            var only = ModuleReportService.CreateDocument(request with { Options = new HashSet<string> { PileReportContents.Version, "verifiche" } }, []);
            Check(Text(only).Contains("Azioni e verifica") && !Text(only).Contains("Teoria di calcolo"), name + ": selezione singola ignorata");
            selector.ExportCommand.Execute(null);
            Check(!selector.Result!.Contains(PileReportContents.Brief), name + ": modalità breve nel selettore della W");
            HashSet<string> briefOptions = [PileReportContents.Version, PileReportContents.Brief];
            var briefRequest = request with { Options = briefOptions };
            var briefFigures = await new ModuleFigureRenderer().RenderAsync(briefRequest, default);
            Check(briefFigures.Count == 1, name + ": figure breve");
            var brief = ModuleReportService.CreateDocument(briefRequest, briefFigures);
            var xml = brief.GetXml("word/document.xml"); string text = Text(brief);
            Check(xml.Descendants(w + "pageBreakBefore").Count() == 1 && !xml.Descendants(w + "headerReference").Any(), name + ": struttura breve");
            Check(elastic ? text.Contains("non determina la capacità ultima") && text.Contains("Convergenza:") : text.Contains("Rd [kN]"), name + ": esito breve");
            if (elastic)
            {
                string small = (J.Number(result["risposta"]?["Extrema"]?["y"]?["AbsoluteMaximum"]) ?? throw new Exception("Estremo assente")).ToString("G5", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
                Check(fullText.Contains(small), name + ": piccolo spostamento arrotondato a zero");
                var failed = (JsonObject)result.DeepClone(); failed["risposta"]!["MeshConverged"] = false;
                var invalid = PileFamilyReport.Create(name, module, data, failed, briefOptions, briefFigures.Select(f => f.Image()).ToArray());
                Check(Text(invalid).Contains("NON soddisfatta: raffinare"), name + ": convergenza mancata nascosta");
            }
            else if (module != "geo_micropalo_verticale")
            {
                Check(!Text(complete).Contains("Incompleta", StringComparison.OrdinalIgnoreCase) && !text.Contains("Incompleta", StringComparison.OrdinalIgnoreCase)
                    && !HorizontalPilePresentation.Summary(result).Contains("incompleta"), name + ": etichetta generica incompleta ancora visibile");
                string[] VerificationRow(DocxDocument document) => document.GetXml("word/document.xml").Descendants(w + "tbl")
                    .Single(t => t.Elements(w + "tr").First().Elements(w + "tc").Any(c => string.Concat(c.Descendants(w + "t").Select(x => x.Value)) == "HEd/Rd"))
                    .Elements(w + "tr").Last().Elements(w + "tc").Select(c => string.Concat(c.Descendants(w + "t").Select(x => x.Value))).ToArray();
                var verificationRow = VerificationRow(full);
                string Number(string key) => result.D(key).ToString("G5", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
                Check(verificationRow.SequenceEqual(new[] { Number("resistenza_caratteristica_manuale_kn"), Number("resistenza_progetto_manuale_kn"), Number("azione_kn"), Number("utilizzo_manuale"), result.S("esito_manuale") }), name + ": tabella non coerente con i risultati");
                Check(VerificationRow(brief).SequenceEqual(verificationRow) && VerificationRow(only).SequenceEqual(verificationRow), name + ": verifica differente fra completo, breve e capitolo singolo");
                var legacy = ReportOrizzontale.CreateDocument(name, result);
                Check(!Text(legacy).Contains("Incompleta") && VerificationRow(legacy).SequenceEqual(verificationRow), name + ": riepilogo legacy non aggiornato");
                Check(fullText.Contains(HorizontalPileResistanceReport.CapacityTitle) && fullText.Contains("Momento resistente adottato My"), name + ": capacità non organizzata in tabella");
                Check(fullText.Contains(HorizontalPileResistanceReport.SteelTitle) == (result["sezione"] is not null), name + ": sezione resistente mancante o inventata per My manuale");
                int surveys = data.Array("stratigrafie").Count;
                Check(figures.Count(f => f.Category == "stratigrafia") == surveys && figures.Count(f => f.Category == "geometria_palo") == 1, name + ": immagini di geometria e stratigrafia mancanti");
                var geometryOnly = ModuleReportService.CreateDocument(request with { Options = new HashSet<string> { PileReportContents.Version, "geometria" } }, figures);
                var profilesOnly = ModuleReportService.CreateDocument(request with { Options = new HashSet<string> { PileReportContents.Version, "profili" } }, figures);
                XNamespace drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
                Check(geometryOnly.GetXml("word/document.xml").Descendants(drawing + "blip").Count() == 1, name + ": geometria non collegata al capitolo");
                Check(profilesOnly.GetXml("word/document.xml").Descendants(drawing + "blip").Count() == surveys, name + ": profili non collegati al capitolo");
                var sheetFigures = new HorizontalPileFigureRenderer().RenderReport(result, false);
                Check(sheetFigures.Count(f => f.Category == "stratigrafia") == surveys && sheetFigures.Count(f => f.Category == "geometria_palo") == 1, name + ": immagini assenti nell'export della scheda");
                Check(full.GetXml("word/document.xml").Descendants(drawing + "blip").Count() == figures.Count, name + ": immagini duplicate nel report completo");
                Check(!Text(complete).Contains("sperimental", StringComparison.OrdinalIgnoreCase) && !text.Contains("sperimental", StringComparison.OrdinalIgnoreCase), name + ": dicitura del modello non aggiornata");
                Check(!HorizontalPilePresentation.Warnings(result).Contains("sperimental", StringComparison.OrdinalIgnoreCase) && !HorizontalPilePresentation.Summary(result).Contains("sperimental", StringComparison.OrdinalIgnoreCase), name + ": dicitura nell'interfaccia");
                Check(fullText.Contains("riadattate") && fullText.Contains("Formulazione per terreno stratificato"), name + ": teoria stratificata assente");
                XNamespace math = "http://schemas.openxmlformats.org/officeDocument/2006/math";
                Check(full.GetXml("word/document.xml").Descendants(math + "nary").Any() && full.GetXml("word/document.xml").Descendants(math + "f").Any(), name + ": integrali e frazioni Word assenti");
                var missing = (JsonObject)result.DeepClone(); missing["resistenza_progetto_manuale_kn"] = null; missing["resistenza_caratteristica_manuale_kn"] = null;
                var invalid = PileFamilyReport.Create(name, module, data, missing, briefOptions, briefFigures.Select(f => f.Image()).ToArray());
                Check(Text(invalid).Contains("Rk e Rd non determinate") && !Text(invalid).Contains("Verifica soddisfatta"), name + ": esito inventato senza Rd");
                var invalidFull = PileFamilyReport.Create(name, module, data, missing, new HashSet<string> { PileReportContents.Version, "verifiche" }, []);
                Check(VerificationRow(invalidFull)[3] == "—" && VerificationRow(invalidFull)[4] == "Verifica non eseguibile" && !Text(invalidFull).Contains("Verifica soddisfatta"), name + ": verifica completa inventata senza Rd");
                var failed = result.DeepClone().AsObject(); failed["esito_manuale"] = "Verifica non soddisfatta"; failed["utilizzo_manuale"] = 1.000001;
                var failedReport = PileFamilyReport.Create(name, module, data, failed, new HashSet<string> { PileReportContents.Version, "verifiche" }, []);
                Check(VerificationRow(failedReport)[4] == "Verifica non soddisfatta", name + ": esito cambiato dall'arrotondamento");
            }
            ReportFiles.WriteWord(Path.Combine(directory, name + "-breve.docx"), DocxWriter.Write(brief));
            File.WriteAllText(Path.Combine(directory, name + "-risultato.json"), result.ToJsonString(new() { WriteIndented = true }));
            Check(beforeData == data.ToJsonString() && beforeResult == result.ToJsonString(), name + ": mutazione del calcolo");
        }
        CheckBromsCases(root.FullName, directory, Check);
        CheckConcreteResistance(root.FullName, directory, Check);
        Check(!Directory.EnumerateFiles(directory, "*.pdf").Any(), "PDF generato");
        File.WriteAllText(Path.Combine(directory, "esito-famiglia.txt"), $"PASS {checks} controlli; tre moduli, cinque analisi, Word completo e breve.");
    }

    static void CheckConcreteResistance(string root, string directory, Action<bool, string> check)
    {
        const string module = "geo_palo_orizzontale";
        var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/geo_palo_orizzontale--broms-coesivo-impedita.json")))!["data"]!.AsObject();
        data["generali"]!["origine_momento"] = "Sezione c.a.";
        var result = ModuleServices.Get(module).Calculate(data, default).Values;
        check(result.S("errore") == "", "Riepilogo c.a.: " + result.S("errore"));
        string before = result.ToJsonString();
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        string Text(DocxDocument document) => string.Concat(document.GetXml("word/document.xml").Descendants(w + "t").Select(t => t.Value));
        var full = PileFamilyReport.Create("Riepilogo sezione c.a.", module, data, result, PileFamilyReport.Defaults(module, data, result), []);
        string text = Text(full);
        check(text.Contains(HorizontalPileResistanceReport.ConcreteTitle) && text.Contains("Area dell’armatura longitudinale As") && text.Contains("minore dei momenti resistenti"), "Riepilogo c.a.: sezione o criterio mancanti");
        check(text.Contains(result.D("momento_resistente_knm").ToString("#,0.###", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))) && text.Contains(HorizontalPileResistanceReport.ConcreteNote), "Riepilogo c.a.: resistenza o nota duttilità perse");
        check(!text.Contains("GPCChecker") && !text.Contains("residuo", StringComparison.OrdinalIgnoreCase) && !text.Contains("Convenzione interna"), "Dettagli numerici nel riepilogo principale");
        var details = PileFamilyReport.Create("Controlli sezione c.a.", module, data, result, new HashSet<string> { PileReportContents.Version, "dettagli" }, []);
        check(Text(details).Contains(HorizontalPileResistanceReport.DiagnosticsTitle) && Text(details).Contains(result["sezione"].S("motore")) && Text(details).Contains("Tolleranza dell’equilibrio assiale"), "Controlli numerici non conservati nei dettagli");
        var brief = PileFamilyReport.Create("Riepilogo sezione c.a.", module, data, result, new HashSet<string> { PileReportContents.Version, PileReportContents.Brief }, []);
        check(!Text(brief).Contains("GPCChecker") && Text(brief).Contains(HorizontalPileResistanceReport.ConcreteBrief), "Sintesi c.a. con dettagli interni o criterio perso");
        check(before == result.ToJsonString(), "Riepilogo c.a.: risultato mutato");
        ReportFiles.WriteWord(Path.Combine(directory, "palo_orizzontale-sezione-ca-completo.docx"), DocxWriter.Write(full));
        ReportFiles.WriteWord(Path.Combine(directory, "palo_orizzontale-sezione-ca-dettagli.docx"), DocxWriter.Write(details));
    }

    static void CheckBromsCases(string root, string directory, Action<bool, string> check)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main", m = "http://schemas.openxmlformats.org/officeDocument/2006/math";
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/geo_palo_orizzontale--broms-coesivo-impedita.json")))!["data"]!.AsObject();
        foreach (string module in new[] { "geo_palo_orizzontale", "geo_micropalo_orizzontale" })
        foreach (string soil in new[] { "Coesivo", "Granulare" })
        foreach (string head in new[] { "Libera", "Impedita" })
        {
            var data = original.DeepClone().AsObject();
            if (module == "geo_micropalo_orizzontale")
            {
                var micro = Anthea.Calculations.MicropaloOrizzontale.Defaults();
                data["tipo_sezione"] = "CHS"; data["sezione"] = micro["sezione"]!.DeepClone();
            }
            var g = data["generali"]!;
            g["origine_momento"] = "Manuale"; g["momento_resistente"] = "500"; g["provenienza_momento"] = "Esempio di verifica del report";
            g["lunghezza"] = "6"; g["eccentricita"] = head == "Libera" ? "1" : "0"; g["vincolo"] = head;
            g["presenza_falda"] = false; g["profondita_testa"] = "0";
            data["stratigrafie"] = new JsonArray(new JsonArray(J.Obj(("tipologia", soil), ("spessore", "20"), ("peso_specifico", "18"), ("peso_specifico_saturo", "20"), ("angolo_attrito", "32"), ("coesione_non_drenata", "60"), ("coesione_efficace", "0"))));
            var result = ModuleServices.Get(module).Calculate(data, default).Values;
            string name = module.Replace("geo_", "") + "-" + soil + "-" + head;
            check(result.S("errore") == "", name + ": " + result.S("errore"));
            string before = result.ToJsonString();
            var doc = PileFamilyReport.Create(name, module, data, result, new HashSet<string> { PileReportContents.Version, "metodo" }, []);
            var xml = doc.GetXml("word/document.xml");
            string text = string.Concat(xml.Descendants(w + "t").Select(t => t.Value));
            string math = string.Concat(xml.Descendants(m + "t").Select(t => t.Value));
            check(text.Contains("Formule di Broms — terreno " + (soil == "Coesivo" ? "coesivo" : "non coesivo") + " omogeneo"), name + ": ramo omogeneo errato");
            check(text.Contains(head == "Libera" ? "Testa libera — palo corto" : "Testa incastrata — palo corto"), name + ": vincolo errato");
            check(!text.Contains(head == "Libera" ? "Testa incastrata —" : "Testa libera —"), name + ": formule del vincolo non pertinente");
            check(text.Contains("palo intermedio") == (head == "Impedita"), name + ": meccanismo intermedio improprio");
            check(math.Contains("K") == (soil == "Granulare"), name + ": legge granulare non pertinente");
            check(xml.Descendants(m + "f").Any() && xml.Descendants(m + "sSub").Any() && xml.Descendants(m + "rad").Any(), name + ": formule non strutturate");
            check(result.ToJsonString() == before, name + ": calcolo mutato dal report");
            ReportFiles.WriteWord(Path.Combine(directory, name + "-teoria.docx"), DocxWriter.Write(doc));
        }
    }
}
