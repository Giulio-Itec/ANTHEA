using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json.Nodes;
using System.Windows;
using System.Xml.Linq;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Reports;
using X.Core;

internal static class Program
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    [STAThread]
    static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
        string appDirectory = Environment.GetEnvironmentVariable("ANTHEA_REPORT_APP") ?? Path.Combine(root, "app");
        AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(appDirectory, name.Name + ".dll"))
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(appDirectory, name.Name + ".dll")) : null;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int exit = 0;
        app.Startup += async (_, _) => {
            try { int fileOption = Array.IndexOf(args, "--palo-file"); await Generate(root, appDirectory, output, args.Contains("--orizzontali"), fileOption >= 0 ? args[fileOption + 1] : null); }
            catch (Exception ex) { Console.Error.WriteLine(ex); exit = 1; }
            finally { app.Shutdown(); }
        };
        app.Run();
        return exit;
    }
    static async Task Generate(string root, string appDirectory, string output, bool horizontalOnly, string? pileFile)
    {
        Directory.CreateDirectory(output);
        var desktop = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(appDirectory, "ANTHEA.dll"));
        var renderer = (IFigureRenderer)Activator.CreateInstance(desktop.GetType("X.Desktop.ModuleFigureRenderer", true)!, true)!;
        foreach (var (module, fixture, name) in new[] {
            ("geo_palo_verticale", "", "01 Palo verticale"),
            ("geo_micropalo_verticale", "irs-punta-inclinato", "02 Micropalo verticale"),
            ("geo_palo_orizzontale", "stratificato-misto-manuale", "03 Palo orizzontale"),
            ("geo_micropalo_orizzontale", "stratificato-chs-catalogo", "04 Micropalo orizzontale") })
        {
            if (horizontalOnly && !module.EndsWith("_orizzontale")) continue;
            var data = module == "geo_palo_verticale"
                ? JsonNode.Parse(File.ReadAllText(Path.Combine(root, "supporto/artefatti/ci/20261116-145422-micropalo-report-finale/ui_check-itec-pile-report/dati-prova.json")))!.AsObject()
                : JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs", module + "--" + fixture + ".json")))!["data"]!.DeepClone().AsObject();
            if (module == "geo_palo_orizzontale" && pileFile is not null)
            {
                var saved = JsonNode.Parse(File.ReadAllText(pileFile))!;
                if (saved["modulo_id"]?.ToString() != module) throw new ArgumentException("Il file deve contenere un palo orizzontale.");
                data = saved["dati"]!.DeepClone().AsObject();
            }
            if (module.EndsWith("_verticale"))
            {
                data["generali"]!["peso_palo_sfavorevole"] = "1.35";
                data["generali"]!["peso_palo_favorevole"] = "1.00";
                ModuleSheetPreparation.PrepareVertical(data, module);
            }
            else HorizontalSheetPresentation.PrepareReport(data);
            var result = ModuleServices.Get(module).Calculate(data, default).Values;
            if ((result["errore"]?.ToString() ?? "") != "") throw new Exception(module + ": " + (result["errore"]?.ToString() ?? ""));
            var options = PileFamilyReport.Defaults(module, data, result);
            var request = new ModuleReportRequest("Esempio " + name[3..].ToLowerInvariant(), new(module, data, result), options, false);
            var figures = await renderer.RenderAsync(request, default);
            var doc = ModuleReportService.CreateDocument(request, figures);
            RemoveResultTables(doc);
            ReportFiles.WriteWord(Path.Combine(output, name + ".docx"), DocxWriter.Write(doc));
            File.WriteAllText(Path.Combine(output, "..", module + "-dati.json"), data.ToJsonString(new() { WriteIndented = true }));
            File.WriteAllText(Path.Combine(output, "..", module + "-risultati.json"), result.ToJsonString(new() { WriteIndented = true }));
            Console.WriteLine(name + ": Word generato senza tabelle di dettaglio dei risultati.");
        }
    }
    static string Text(XElement node) => string.Concat(node.Descendants(W + "t").Select(t => t.Value));
    static void RemoveResultTables(DocxDocument doc)
    {
        var xml = doc.GetXml("word/document.xml"); var body = xml.Root!.Element(W + "body")!;
        var bodyParagraph = body.Elements(W + "p").First(p => Text(p).StartsWith("Verifica geotecnica") || Text(p).StartsWith("La presente relazione") || Text(p).Contains("capacità portante orizzontale"));
        XElement Paragraph(string text)
        {
            var p = new XElement(bodyParagraph);
            p.Elements().Where(e => e.Name != W + "pPr").Remove();
            p.Add(new XElement(W + "r", new XElement(W + "t", text)));
            return p;
        }
        int chapter = 0;
        foreach (var block in body.Elements().ToArray())
        {
            string text = Text(block);
            if (block.Name == W + "p" && text.Length > 2 && text[1] == ' ' && char.IsDigit(text[0])) chapter = text[0] - '0';
            if (block.Name != W + "tbl" || chapter < 4) continue;
            var caption = block.PreviousNode as XElement;
            string title = caption is null ? "" : Text(caption);
            if (title.Contains("Coefficienti") || title.Contains("Efficienza") || title.Contains("Azioni assegnate") || title.Contains(HorizontalPileVerificationReport.Title)
                || title.Contains(HorizontalPileResistanceReport.CapacityTitle) || title.Contains(HorizontalPileResistanceReport.ConcreteTitle) || title.Contains(HorizontalPileResistanceReport.SteelTitle)) continue;
            var rows = block.Elements(W + "tr").Select(r => r.Elements(W + "tc").Select(Text).ToArray()).ToArray();
            if (rows[0].Any(c => c is "Esito"))
            {
                foreach (var row in rows.Skip(1))
                {
                    var fields = rows[0].Zip(row, (label, value) => (label, value)).ToArray();
                    string sentence = row[0] + ": " + string.Join("; ", fields.Skip(1).Select(f => f.label + " = " + f.value)) + ".";
                    block.AddBeforeSelf(Paragraph(sentence));
                }
            }
            if (caption is not null && title.StartsWith("Tabella ")) caption.Remove();
            block.Remove();
        }
        // Captions remain sequential within each chapter after suppressing output tables.
        var numbers = new Dictionary<string, int>();
        foreach (var p in body.Elements(W + "p").Where(p => Text(p).StartsWith("Tabella ")))
        {
            var text = Text(p); int space = text.IndexOf(' ', 8); string old = text[8..space]; string key = old.Split('.')[0];
            numbers.TryGetValue(key, out int count); numbers[key] = ++count;
            foreach (var t in p.Descendants(W + "t")) t.Value = t.Value.Replace("Tabella " + old, "Tabella " + key + "." + count);
        }
        foreach (var t in body.Descendants(W + "t"))
            t.Value = t.Value.Replace("la tabella degli esiti riporta l’azione specifica di ciascuna condizione", "il riepilogo degli esiti riporta l’azione specifica di ciascuna condizione");
        doc.SetXml("word/document.xml", xml.ToString());
    }
}
