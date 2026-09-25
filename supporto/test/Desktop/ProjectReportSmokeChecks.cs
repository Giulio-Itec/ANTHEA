using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeProjectReport(string directory, bool materialsOnly = false)
    {
        testing = true; Directory.CreateDirectory(directory);
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        foreach (string module in new[] { "mat_calcestruzzo", RebarMaterial.Module })
        {
            using var materialEditor = new SheetEditor(module, Archivio.NuovoFoglio(module));
            string materialPath = Path.Combine(directory, module + ".docx");
            materialEditor.ExportReport(materialPath, module == "mat_calcestruzzo" ? "Calcestruzzo" : "Acciaio per armature", []);
            using var package = ZipFile.OpenRead(materialPath);
            using var input = package.GetEntry("word/document.xml")!.Open();
            string contents = XDocument.Load(input).ToString();
            Check(contents.Contains("Scheda materiale") && contents.Contains("Proprietà di calcolo"), "Report materiale incompleto");
            Check(contents.Contains(module == "mat_calcestruzzo" ? "Copriferro nominale" : "Resistenza di progetto fyd"), "Proprietà materiale mancanti");
            if (module == RebarMaterial.Module)
            {
                materialEditor.Data["input"]!["gamma_s"] = "0";
                Check(materialEditor.MaterialReportContent().Error is not null, "Acciaio non valido senza avviso");
            }
        }
        if (materialsOnly)
        {
            File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: report singoli CLS e acciaio, proprietà correnti e segnalazione dati acciaio non validi.");
            return;
        }
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        var project = J.Obj(("nome", "Ponte di prova"), ("strutture", new JsonArray()), ("fogli", new JsonArray())); document.Array("progetti").Add(project);
        var material = Sheet("mat_calcestruzzo", "Calcestruzzo di progetto"); project.Array("fogli").Add(material);
        using (var view = new SheetEditor("mat_calcestruzzo", material["dati"]!.AsObject())) { view.Commit(); material["dati"] = view.Data.DeepClone(); }
        var upper = CreateProjectSection(project, "Fondazioni"); var branch = CreateProjectSection(upper, "Spalla A"); var nested = CreateProjectSection(branch, "Pali");
        var empty = CreateProjectSection(upper, "Sezione da completare"); var other = CreateProjectSection(project, "Ramo indipendente");
        var steel = Sheet(RebarMaterial.Module, "Acciaio pali"); upper.Array("fogli").Add(steel);
        var ca = Sheet("str_palo", "Verifica sezione"); nested.Array("fogli").Add(ca);
        ca["dati"]!["input"]!["shape"] = "Circolare"; ca["dati"]!["input"]!["diameter_mm"] = "1000";
        ProjectSharedData.InheritHierarchy(ca, nested);
        var vertical = Sheet("geo_palo_verticale", "Capacità verticale"); nested.Array("fogli").Add(vertical);
        var vg = vertical["dati"]!["generali"]!; vg["lunghezza"] = "4"; vg["azione_compressione"] = "100";
        var layer = vertical["dati"]!["stratigrafie"]![0]![0]!;
        layer["spessore"] = "5"; layer["tipologia"] = "Granulare"; layer["addensamento"] = "Denso"; layer["peso_specifico"] = "18"; layer["angolo_attrito"] = "30";
        Check(Calcolo.Calcola(vertical["dati"]!, false).S("errore") == "", "Fixture verticale: " + Calcolo.Calcola(vertical["dati"]!, false).S("errore"));
        var horizontal = Sheet(PaloOrizzontale.Module, "Capacità orizzontale"); nested.Array("fogli").Add(horizontal);
        horizontal["dati"]!["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer());
        ProjectSharedData.InheritHierarchy(horizontal, nested);
        var cases = SmokeTestData.LoadCases();
        var micro = Sheet("geo_micropalo_verticale", "Micropalo verticale");
        micro["dati"] = cases.First(c => c.S("tipo") == "micropalo" && !c.B("atteso_errore"))!["input"]!.DeepClone(); other.Array("fogli").Add(micro);
        var chs = Sheet(MicropaloOrizzontale.Module, "Micropalo orizzontale"); chs["dati"]!["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer()); other.Array("fogli").Add(chs);
        ShowProjects(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        var buttons = Ui.Descendants<Button>(tree).Where(b => System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("Genera report")).ToArray();
        Check(buttons.Length == ProjectSharedData.Sections(project).Count(), "Pulsante report assente su un livello");
        Check(buttons.Count(b => !b.IsEnabled) == 1, "Report vuoto non disabilitato");
        File.WriteAllBytes(Path.Combine(directory, "pulsanti-report.png"), Ui.Snapshot(this));
        bool preflightShown = false;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "Report · Ponte di prova");
            dialog.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "avvisi-prima-del-report.png"), Ui.Snapshot(dialog));
            preflightShown = true;
            Ui.Descendants<Button>(dialog).Single(b => b.Content?.ToString() == "Annulla").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(preflightShown, "Pulsante non apre il controllo dei conflitti prima del report");
        var before = document.DeepClone(); var plan = new ProjectReportPlan(project);
        Check(plan.Common.Any(c => c.Field.Key == "diameter_mm" && c.Sheets.Contains(ca) && c.Sheets.Contains(vertical) && c.Sheets.Contains(horizontal)), "Diametro non accorpato tra moduli/unità diversi");
        Check(plan.Common.Any(c => c.Field.Key == "esposizione" && ReferenceEquals(c.Section, project)), "Esposizione non al livello superiore");
        Check(plan.LocalInputs(vertical).Any(v => v.Label.Contains("addensamento")), "Parametro specifico dello strato perso");
        var sub = new ProjectReportPlan(nested);
        Check(sub.Common.Any(c => c.Field.Key == "esposizione" && ReferenceEquals(c.Section, nested) && ReferenceEquals(c.Source, material)), "Riferimento esterno al ramo perso");
        var noCrossBranch = new ProjectReportPlan(other);
        Check(!noCrossBranch.Common.Any(c => c.Sheets.Contains(ca)), "Report include rami estranei");
        // A conflicting value stays local; exporting never aligns any source data.
        horizontal["dati"]!["generali"]!["diametro"] = "1.2";
        var conflict = new ProjectReportPlan(nested);
        Check(conflict.Conflicts.Any(c => c.Key == "diameter_mm") && !conflict.Common.Any(c => c.Field.Key == "diameter_mm"), "Conflitto uniformato nel report");
        horizontal["dati"]!["generali"]!["diametro"] = "1";
        Check(JsonNode.DeepEquals(before, document), "Proiezione report modifica il progetto");
        string filename = Path.Combine(directory, "relazione-progetto.docx");
        int unavailable = await GenerateSectionReport(SectionReportSnapshot(project), filename, text => File.AppendAllText(Path.Combine(directory, "progress.txt"), text + Environment.NewLine), CancellationToken.None);
        Check(unavailable == 0, "Schede valide non esportate: " + unavailable);
        Check(JsonNode.DeepEquals(before, document), "Esportazione modifica il progetto aperto");
        using (var zip = ZipFile.OpenRead(filename))
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main", r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            using var stream = zip.GetEntry("word/document.xml")!.Open(); var xml = XDocument.Load(stream);
            string text = string.Join("\n", xml.Descendants(w + "t").Select(t => t.Value));
            foreach (var sheet in plan.Sheets) Check(text.Contains(sheet.S("nome")), "Scheda omessa: " + sheet.S("nome"));
            Check(text.Contains("Resistenze di progetto") && text.Contains("Verifiche dominio") && text.Contains("Risultati") && text.Contains("Resistenza media a compressione"), "Report senza risultati dei moduli");
            Check(text.Contains("Sezione senza schede"), "Sottosezione vuota omessa");
            Check(!text.Contains("Risultati non disponibili:"), "Errore silenzioso in una scheda");
            var headings = xml.Descendants(w + "p").Where(p => p.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val")?.Value.StartsWith("Heading") == true).Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))).ToArray();
            int caHeading = Array.FindIndex(headings, h => h.EndsWith(" Verifica sezione")), verticalHeading = Array.FindIndex(headings, h => h.EndsWith(" Capacità verticale"));
            Check(caHeading >= 0 && verticalHeading > caHeading, "Ordine dei fogli nel report errato");
            using var relStream = zip.GetEntry("word/_rels/document.xml.rels")!.Open(); var rels = XDocument.Load(relStream).Root!.Elements().ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
            foreach (var embed in xml.Descendants().Attributes(r + "embed")) Check(rels.TryGetValue(embed.Value, out string? target) && zip.GetEntry("word/" + target) is not null, "Immagine persa durante il merge");
            Check(xml.Descendants().Attributes(r + "embed").Count() > 3, "Grafici non inclusi");
            var ids = xml.Descendants().Where(e => e.Name.LocalName == "docPr").Select(e => (string)e.Attribute("id")!).ToArray(); Check(ids.Distinct().Count() == ids.Length, "Identificativi immagini duplicati");
        }
        // Failure and cancellation cannot masquerade as successful checks or overwrite an existing file.
        var broken = SectionReportSnapshot(nested); broken.Array("fogli").Clear();
        var invalid = Sheet("geo_palo_verticale", "Scheda incompleta"); broken.Array("fogli").Add(invalid);
        Check(await GenerateSectionReport(broken, Path.Combine(directory, "incompleto.docx"), _ => { }, CancellationToken.None) == 1, "Scheda incompleta omessa senza avviso");
        byte[] prior = File.ReadAllBytes(filename); bool cancelled = false;
        try { await GenerateSectionReport(SectionReportSnapshot(project), filename, _ => { }, new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && prior.SequenceEqual(File.ReadAllBytes(filename)), "Annullamento sovrascrive il report");
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: pulsanti a ogni livello, report ricorsivo di tutti i moduli, ordine gerarchico, dati comuni per proprietà e unità, parametri locali, riferimenti esterni, conflitti non uniformati, immagini e relazioni uniche, calcolo aggiornato, archivi immutati, sezioni vuote, errori espliciti e annullamento senza sovrascrittura.");
    }
}
