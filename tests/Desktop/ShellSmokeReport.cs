using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Xml.Linq;
using Anthea.Application.Documents;
using Anthea.Presentation.Modules.Materials;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using X.Core;

namespace X.Desktop;

internal static partial class ShellSmoke
{
    /// <summary>
    /// ui/shell-smoke-project-report: ProjectReportSmokeChecks (MainWindow.SmokeProjectReport) on the new shell, with the report of the section asked from the
    /// shell («Report Word della sezione» of the tree, «Genera report» of the overview, SectionReportRunner and DesktopSectionReports) instead of the private
    /// methods of MainWindow: the reports of the materials from «Report Word» of a single file, the report enabled at every level with sheets, the check before
    /// the report, the plan of the common data, the recursive report of every module in the order of the tree with images and unique identifiers, the open
    /// project untouched, sheets without results reported, the cancellation that never overwrites the file. Then the Word report of the shell against the one of
    /// MainWindow for the same project (ProjectReport.cs, frozen): the same parts of the document, byte for byte, apart from the date and time of the cover.
    /// </summary>
    private static async Task ProjectReport(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell; var workspace = b.Workspace;
        static JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        // «Report Word…» of a single file of each material: the report of the sheet, written by the shell (fixed name of today).
        foreach (string module in new[] { "mat_calcestruzzo", RebarMaterial.Module })
        {
            var single = workspace.NewCalculation(module); await b.Idle();
            string materialPath = Path.Combine(directory, module + ".docx");
            string? proposed = null;
            b.Services.SaveFile = request => { proposed = request.FileName; return materialPath; };
            try { await shell.Commands.ReportCommand.ExecuteAsync(null); }
            finally { b.Services.SaveFile = null; }
            b.Check(proposed == (module == "mat_calcestruzzo" ? "Scheda_calcestruzzo.docx" : "Scheda_acciaio_armature.docx"), "Nome proposto del report del materiale: " + proposed);
            using (var package = ZipFile.OpenRead(materialPath))
            using (var input = package.GetEntry("word/document.xml")!.Open())
            {
                string contents = XDocument.Load(input).ToString();
                b.Check(contents.Contains("Scheda materiale") && contents.Contains("Proprietà di calcolo"), "Report del materiale completo");
                b.Check(contents.Contains(module == "mat_calcestruzzo" ? "Copriferro nominale" : "Resistenza di progetto fyd"), "Proprietà del materiale nel report");
            }
            if (module == RebarMaterial.Module)
            {
                single.ViewModel.Data["input"]!["gamma_s"] = "0";
                var content = ((IMaterialSheetViewModel)single.ViewModel).ReportContent();
                b.Check(content.Error is not null, "Acciaio non valido con avviso");
            }
            // MainWindow disposed the editor of the check: the single file closes without saving.
            b.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
            try { workspace.CloseSheet(single); }
            finally { b.Services.Confirmations.SaveChanges = null; }
        }
        var document = ProjectDocuments.CreateArchive();
        var project = J.Obj(("nome", "Ponte di prova"), ("strutture", new JsonArray()), ("fogli", new JsonArray())); document.Array("progetti").Add(project);
        var material = Sheet("mat_calcestruzzo", "Calcestruzzo di progetto"); project.Array("fogli").Add(material);
        using (var view = ModuleHostBench.SheetEditors().Open("mat_calcestruzzo", material["dati"]!.AsObject())) { view.Commit(); material["dati"] = view.Data.DeepClone(); }
        var upper = ProjectDocuments.AddSection(project, "Fondazioni"); var branch = ProjectDocuments.AddSection(upper, "Spalla A"); var nested = ProjectDocuments.AddSection(branch, "Pali");
        var empty = ProjectDocuments.AddSection(upper, "Sezione da completare"); var other = ProjectDocuments.AddSection(project, "Ramo indipendente");
        var steel = Sheet(RebarMaterial.Module, "Acciaio pali"); upper.Array("fogli").Add(steel);
        var ca = Sheet("str_palo", "Verifica sezione"); nested.Array("fogli").Add(ca);
        ca["dati"]!["input"]!["shape"] = "Circolare"; ca["dati"]!["input"]!["diameter_mm"] = "1000";
        ProjectSharedData.InheritHierarchy(ca, nested);
        var vertical = Sheet("geo_palo_verticale", "Capacità verticale"); nested.Array("fogli").Add(vertical);
        var vg = vertical["dati"]!["generali"]!; vg["lunghezza"] = "4"; vg["azione_compressione"] = "100";
        var layer = vertical["dati"]!["stratigrafie"]![0]![0]!;
        layer["spessore"] = "5"; layer["tipologia"] = "Granulare"; layer["addensamento"] = "Denso"; layer["peso_specifico"] = "18"; layer["angolo_attrito"] = "30";
        b.Check(Calcolo.Calcola(vertical["dati"]!, false).S("errore") == "", "Dati del palo verticale validi: " + Calcolo.Calcola(vertical["dati"]!, false).S("errore"));
        var horizontal = Sheet(PaloOrizzontale.Module, "Capacità orizzontale"); nested.Array("fogli").Add(horizontal);
        horizontal["dati"]!["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer());
        ProjectSharedData.InheritHierarchy(horizontal, nested);
        var cases = SmokeTestData.LoadCases();
        var micro = Sheet("geo_micropalo_verticale", "Micropalo verticale");
        micro["dati"] = cases.First(c => c.S("tipo") == "micropalo" && !c.B("atteso_errore"))!["input"]!.DeepClone(); other.Array("fogli").Add(micro);
        var chs = Sheet(MicropaloOrizzontale.Module, "Micropalo orizzontale"); chs["dati"]!["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer()); other.Array("fogli").Add(chs);
        workspace.OpenDocument(document, null, false);
        shell.Navigation.Select(ShellPage.Projects); await b.Idle();
        // «Report Word della sezione» on every level of the tree, disabled only for the section without sheets (MainWindow: a button per level).
        var sections = ProjectSharedData.Sections(project).ToArray();
        var items = sections.Select(s => b.Menu(s).Items.OfType<MenuItem>().Single(m => m.Header as string == "Report Word della sezione")).ToArray();
        b.Check(items.Length == sections.Length && items.All(i => i.Command is not null), "«Report Word della sezione» su ogni livello");
        b.Check(items.Count(i => !i.IsEnabled) == 1 && !items[Array.IndexOf(sections, empty)].IsEnabled, "Report vuoto disabilitato");
        shell.ShowOverview(project); await b.Idle();
        var generate = ShellSmokeBench.Descendants<System.Windows.Controls.Button>(b.Window).Single(button => button.Name == "SectionReport");
        b.Check(generate.IsEnabled && generate.Content as string == "Genera report", "«Genera report» del riepilogo abilitato");
        await b.Capture("pulsanti-report.png");
        // The check before the report, from «Genera report» of the overview: «Annulla».
        bool preflightShown = false;
        b.Services.DialogAsync = async d =>
        {
            var check = (SectionReportCheckViewModel)d;
            b.Check(check.Title == "Report · Ponte di prova", "Titolo del controllo prima del report");
            await b.CaptureDialog(check, check.Title, "avvisi-prima-del-report.png");
            preflightShown = true;
            check.CancelCommand.Execute(null);
            return check.Result;
        };
        try { await b.Press(generate); for (int i = 0; i < 100 && shell.SectionReportCommand.IsRunning; i++) await b.Idle(); }
        finally { b.Services.DialogAsync = null; }
        b.Check(preflightShown, "Il pulsante apre il controllo dei conflitti prima del report");
        var before = b.Project.Document.DeepClone(); var plan = new ProjectReportPlan(project);
        b.Check(plan.Common.Any(c => c.Field.Key == "diameter_mm" && c.Sheets.Contains(ca) && c.Sheets.Contains(vertical) && c.Sheets.Contains(horizontal)), "Diametro accorpato tra moduli e unità diversi");
        b.Check(plan.Common.Any(c => c.Field.Key == "esposizione" && ReferenceEquals(c.Section, project)), "Esposizione al livello superiore");
        b.Check(plan.LocalInputs(vertical).Any(v => v.Label.Contains("addensamento")), "Parametro specifico dello strato conservato");
        var sub = new ProjectReportPlan(nested);
        b.Check(sub.Common.Any(c => c.Field.Key == "esposizione" && ReferenceEquals(c.Section, nested) && ReferenceEquals(c.Source, material)), "Riferimento esterno al ramo conservato");
        var noCrossBranch = new ProjectReportPlan(other);
        b.Check(!noCrossBranch.Common.Any(c => c.Sheets.Contains(ca)), "Il report non include rami estranei");
        horizontal["dati"]!["generali"]!["diametro"] = "1.2";
        var conflict = new ProjectReportPlan(nested);
        b.Check(conflict.Conflicts.Any(c => c.Key == "diameter_mm") && !conflict.Common.Any(c => c.Field.Key == "diameter_mm"), "Conflitto non uniformato nel report");
        horizontal["dati"]!["generali"]!["diametro"] = "1";
        b.Check(JsonNode.DeepEquals(before, b.Project.Document), "La proiezione del report non modifica il progetto");
        // The report of the project from «Report Word della sezione» of the tree: «Genera con segnalazioni», the file, the progress.
        string filename = Path.Combine(directory, "relazione-progetto.docx");
        var progress = new List<string>();
        int? unavailable = await ReportFromTree(b, project, filename, progress);
        File.WriteAllLines(Path.Combine(directory, "progress.txt"), progress, new UTF8Encoding(false));
        b.Check(unavailable == 0, "Schede valide esportate: " + unavailable);
        b.Check(JsonNode.DeepEquals(before, b.Project.Document), "L'esportazione non modifica il progetto aperto");
        b.Check(b.Services.Messages.Shown.LastOrDefault() is { Title: "ANTHEA", Warning: false } shown && shown.Message.StartsWith("Report salvato in Word e PDF:\n" + filename, StringComparison.Ordinal),
            "Messaggio «Report salvato in Word e PDF»");
        using (var zip = ZipFile.OpenRead(filename))
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main", r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            using var stream = zip.GetEntry("word/document.xml")!.Open(); var xml = XDocument.Load(stream);
            string text = string.Join("\n", xml.Descendants(w + "t").Select(t => t.Value));
            foreach (var sheet in plan.Sheets) b.Check(text.Contains(sheet.S("nome")), "Scheda presente: " + sheet.S("nome"));
            b.Check(text.Contains("Resistenze di progetto") && text.Contains("Verifiche dominio") && text.Contains("Risultati") && text.Contains("Resistenza media a compressione"), "Report con i risultati dei moduli");
            b.Check(text.Contains("Sezione senza schede"), "Sottosezione vuota presente");
            b.Check(!text.Contains("Risultati non disponibili:"), "Nessun errore silenzioso in una scheda");
            var headings = xml.Descendants(w + "p").Where(p => p.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val")?.Value.StartsWith("Heading") == true).Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))).ToArray();
            int caHeading = Array.FindIndex(headings, h => h.EndsWith(" Verifica sezione")), verticalHeading = Array.FindIndex(headings, h => h.EndsWith(" Capacità verticale"));
            b.Check(caHeading >= 0 && verticalHeading > caHeading, "Ordine dei fogli nel report");
            using var relStream = zip.GetEntry("word/_rels/document.xml.rels")!.Open(); var rels = XDocument.Load(relStream).Root!.Elements().ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
            foreach (var embed in xml.Descendants().Attributes(r + "embed")) b.Check(rels.TryGetValue(embed.Value, out string? target) && zip.GetEntry("word/" + target) is not null, "Immagine conservata nell'unione");
            b.Check(xml.Descendants().Attributes(r + "embed").Count() > 3, "Grafici inclusi");
            var ids = xml.Descendants().Where(e => e.Name.LocalName == "docPr").Select(e => (string)e.Attribute("id")!).ToArray(); b.Check(ids.Distinct().Count() == ids.Length, "Identificativi delle immagini unici");
        }
        // The same project through MainWindow (ProjectReport.cs, the frozen code of the report of today): the same Word document.
        string historicPath = Path.Combine(directory, "relazione-progetto-mainwindow.docx");
        int historicUnavailable = await MainWindow.SectionReportForChecks(project, historicPath);
        var comparison = CompareReports(filename, historicPath);
        File.WriteAllLines(Path.Combine(directory, "confronto-report.txt"), comparison.Lines, new UTF8Encoding(false));
        b.Check(historicUnavailable == unavailable && comparison.Same, "Report Word della shell uguale a quello di MainWindow (" + comparison.Lines.Count(l => l.StartsWith("DIVERSO", StringComparison.Ordinal)) + " parti diverse, confronto-report.txt)");
        // F5.13 g: the call of MainWindow recorded for its archiving (ShellReferences.SectionReport) gives the same document, and the parts of the report of
        // MainWindow are those recorded in tests/fixtures/f5-shell/finestra-storica.json («report_sezione»).
        string referencePath = Path.Combine(directory, "relazione-progetto-riferimento.docx");
        int referenceUnavailable = await ShellReferences.SectionReport(project, referencePath);
        var reference = CompareReports(referencePath, historicPath);
        File.WriteAllLines(Path.Combine(directory, "confronto-report-riferimento.txt"), reference.Lines, new UTF8Encoding(false));
        b.Check(referenceUnavailable == historicUnavailable && reference.Same, "Chiamata registrata di MainWindow: stesso report Word (confronto-report-riferimento.txt)");
        var shape = ShellReferences.ReportShape(historicPath, historicUnavailable);
        ShellReferences.WriteJson(Path.Combine(directory, "report-mainwindow.json"), shape);
        b.Check(JsonNode.DeepEquals(shape, ShellReferences.Window["report_sezione"]) && JsonNode.DeepEquals(ShellReferences.ReportShape(filename, unavailable ?? -1), shape),
            "Parti del report Word e schede senza risultati come i valori registrati della finestra storica");
        // Failure and cancellation never pass for successful checks nor overwrite an existing file: a section with an incomplete sheet through the writer of the
        // shell (DesktopSectionReports), then «Annulla» of «Generazione del report».
        var broken = Anthea.Application.Reporting.ProjectReportService.Snapshot(nested); broken.Array("fogli").Clear();
        var invalid = Sheet("geo_palo_verticale", "Scheda incompleta"); broken.Array("fogli").Add(invalid);
        b.Check(await new DesktopSectionReports().WriteAsync(broken, Path.Combine(directory, "incompleto.docx"), _ => { }, CancellationToken.None) == 1, "Scheda incompleta segnalata");
        byte[] prior = File.ReadAllBytes(filename);
        bool cancelled = false;
        b.Services.SaveFile = _ => filename;
        b.Services.DialogAsync = d =>
        {
            switch (d)
            {
                case SectionReportCheckViewModel check: check.GenerateCommand.Execute(null); return Task.FromResult<object?>(check.Result);
                case SectionReportProgressViewModel running: running.CancelCommand.Execute(null); cancelled = running.IsCancelled; return Task.FromResult<object?>(running.Result);
                default: throw new InvalidOperationException("Dialogo non previsto: " + d.GetType().Name);
            }
        };
        int? written;
        try { written = await shell.ExportSectionReportAsync(project); }
        finally { b.Services.DialogAsync = null; b.Services.SaveFile = null; }
        b.Check(cancelled && written is null && prior.SequenceEqual(File.ReadAllBytes(filename)), "L'annullamento non sovrascrive il report");
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK (shell nuova): report a ogni livello, report ricorsivo di tutti i moduli, ordine gerarchico, dati comuni per proprietà e unità, parametri locali, riferimenti esterni, conflitti non uniformati, immagini e relazioni uniche, calcolo aggiornato, archivi immutati, sezioni vuote, errori espliciti e annullamento senza sovrascrittura; report Word uguale a quello di MainWindow.");
    }

    /// <summary>
    /// The report of <paramref name="section"/> from «Report Word della sezione» of its node in the tree: «Genera con segnalazioni» when the check is shown, the
    /// file, the progress until the end (its texts recorded). The sheets with missing results, null when nothing was written.
    /// </summary>
    private static async Task<int?> ReportFromTree(ShellSmokeBench b, JsonObject section, string path, List<string> progress)
    {
        b.Services.SaveFile = _ => path;
        b.Services.DialogAsync = async d =>
        {
            switch (d)
            {
                case SectionReportCheckViewModel check: check.GenerateCommand.Execute(null); return check.Result;
                case SectionReportProgressViewModel running:
                    string last = "";
                    for (int i = 0; i < 6000 && !running.IsFinished; i++)
                    {
                        if (running.Status != last) progress.Add(last = running.Status);
                        await Task.Delay(50);
                    }
                    return running.Result;
                default: throw new InvalidOperationException("Dialogo non previsto: " + d.GetType().Name);
            }
        };
        int before = b.Services.Messages.Shown.Count;
        try
        {
            var item = b.Menu(section).Items.OfType<MenuItem>().Single(m => m.Header as string == "Report Word della sezione");
            item.Command!.Execute(item.CommandParameter);
            // The command runs on: the end is its message, or the command no longer running (a failure goes to the messages of the bench, rethrown).
            for (int i = 0; i < 12000 && b.Services.Messages.Shown.Count == before && (i < 20 || b.Shell.SectionReportCommand.IsRunning); i++) await Task.Delay(50);
        }
        finally { b.Services.DialogAsync = null; b.Services.SaveFile = null; }
        if (b.Services.Messages.Shown.Count == before) return null;
        var shown = b.Services.Messages.Shown[^1].Message;
        var match = Regex.Match(shown, @"\n\n(\d+) schede contengono");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    /// <summary>
    /// Two Word reports part by part: the same parts; XML parts equal once the date and time written at the cover («dd/MM/yyyy HH:mm», DateTime.Now in the frozen
    /// generators) and the timestamps of the package are blanked; the other parts (images) byte for byte. The PDF beside each: the same size class is not
    /// compared (written from the DOCX by the same code).
    /// </summary>
    private static (bool Same, List<string> Lines) CompareReports(string shellReport, string historicReport)
    {
        var lines = new List<string> { "Confronto del report Word della sezione: shell nuova " + Path.GetFileName(shellReport) + " contro MainWindow " + Path.GetFileName(historicReport) };
        static string Normalize(string xml) => Regex.Replace(Regex.Replace(xml, @"\d{2}/\d{2}/\d{4} \d{2}:\d{2}", "<data e ora>"), @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z?", "<istante>");
        using var a = ZipFile.OpenRead(shellReport);
        using var h = ZipFile.OpenRead(historicReport);
        var names = a.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        var historicNames = h.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        bool same = names.SequenceEqual(historicNames);
        lines.Add((same ? "UGUALE" : "DIVERSO") + " elenco delle parti: " + names.Length + " contro " + historicNames.Length);
        foreach (var name in names.Intersect(historicNames))
        {
            byte[] Read(ZipArchive zip) { using var stream = zip.GetEntry(name)!.Open(); using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray(); }
            byte[] left = Read(a), right = Read(h);
            bool equal = left.SequenceEqual(right);
            string how = "byte per byte";
            if (!equal && (name.EndsWith(".xml", StringComparison.Ordinal) || name.EndsWith(".rels", StringComparison.Ordinal)))
            {
                equal = Normalize(Encoding.UTF8.GetString(left)) == Normalize(Encoding.UTF8.GetString(right));
                how = "a meno di data e ora";
            }
            same &= equal;
            lines.Add((equal ? "UGUALE " : "DIVERSO ") + name + " (" + left.Length + " e " + right.Length + " byte, " + how + ")");
        }
        lines.Add(same ? "PASS: report Word uguali" : "FAIL: report Word diversi");
        return (same, lines);
    }
}

public sealed partial class MainWindow
{
    /// <summary>
    /// F5.13 e (UiTests): the report of a section as MainWindow writes it (ExportSectionReport after its dialogs: the snapshot of the section and
    /// GenerateSectionReport of ProjectReport.cs, unchanged), for the comparison with the report of the new shell.
    /// </summary>
    internal static Task<int> SectionReportForChecks(JsonObject section, string path) =>
        GenerateSectionReport(SectionReportSnapshot(section), path, _ => { }, CancellationToken.None);
}
