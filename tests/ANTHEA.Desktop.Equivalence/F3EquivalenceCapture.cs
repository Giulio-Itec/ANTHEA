using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Anthea.Testing.Normalization;
using Anthea.Presentation.Tests;
using X.Core;

namespace X.Desktop;

/// <summary>F3's UI baseline. Compiled only in UiTests; the shared toolkit owns all normalization and comparisons.</summary>
internal static class F3EquivalenceCapture
{
    internal static async Task Run(string root, string output)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        var normalizer = new TextNormalizer([(root, "<RADICE>"), (output, "<USCITA>"), (Path.GetTempPath(), "<TEMP>")]);
        int count = 0;
        // Sheets open through the factory of the bench (F5.2): the implementations of the Release, or the MVVM one chosen with
        // --module-impl for the direct comparison of a migrated module (proposal 7.2, tools/qa/f5_confronto_diretto.py).
        var editors = ModuleHostBench.SheetEditors();
        foreach (string input in Directory.EnumerateFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*.json").Order(StringComparer.Ordinal))
        {
            var fixture = JsonNode.Parse(File.ReadAllText(input))!.AsObject();
            string module = fixture.S("module"), id = fixture.S("id");
            string folder = Path.Combine(output, "sheets", module, id);
            Directory.CreateDirectory(folder);
            var guids = new GuidMap(); var data = fixture["data"]!.AsObject();
            CanonicalJson.Register(data, guids);
            void Save(string name, JsonNode? value)
            {
                CanonicalJson.Register(value, guids);
                File.WriteAllText(Path.Combine(folder, name + ".json"), CanonicalJson.Serialize(value, guids, normalizer), new UTF8Encoding(false));
            }
            Console.WriteLine("F3 " + module + "/" + id);
            using var editor = editors.Open(module, data);
            editor.Commit(); Save("opened", editor.Data);
            Visit(editor.View); editor.Commit(); Save("visited", editor.Data);
            await editor.CalculateAsync(); editor.Commit(); Save("calculated-data", editor.Data);
            Save("result", module == ShallowFoundationScenarios.Module
                ? ShallowFoundationVersionProjection.Result(editor.Result, ShallowFoundationVersionProjection.F3TransitionVersion)
                : editor.Result);
            var status = new JsonObject { ["hasResults"] = editor.HasResults };
            string raw = Path.Combine(output, "raw", module, id); Directory.CreateDirectory(raw);
            try
            {
                var options = module == BridgeSection.Module ? ReportBridge.DefaultSections() : module == "str_palo"
                    ? ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet()
                    : ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();
                string report = Path.Combine(raw, "report.docx");
                editor.ExportReport(report, "Fixture F3 · " + module + " · " + id, options);
                CaptureReport(report, folder, normalizer, guids);
                if (module == ShallowFoundationScenarios.Module)
                {
                    // Only the comparison text uses the approved F3 metadata. The actual DOCX/PDF retain runtime provenance.
                    string textPath = Path.Combine(folder, "report.txt");
                    File.WriteAllText(textPath, ShallowFoundationVersionProjection.Text(File.ReadAllText(textPath),
                        ShallowFoundationVersionProjection.F3TransitionVersion), new UTF8Encoding(false));
                }
                status["report"] = "available";
                if (module == "str_palo")
                {
                    string shortReport = Path.Combine(raw, "short.docx");
                    File.WriteAllBytes(shortReport, editor.BuildShortReport("Fixture F3 breve · " + id));
                    CaptureReport(shortReport, folder, normalizer, guids, "short");
                }
            }
            catch (Exception ex) when (ex is not IOException && ex is not UnauthorizedAccessException)
            { status["report"] = "unavailable"; status["reason"] = ex.GetType().Name + ": " + ex.Message; }
            Save("exported-data", editor.Data); Save("status", status);
            using var reopened = editors.Open(module, editor.Data); reopened.Commit(); Save("reopened", reopened.Data);
            count++;
        }
        string wiki = Path.Combine(output, "wiki"); Directory.CreateDirectory(wiki);
        foreach (var article in WikiCatalog.Articles)
        {
            string body = WikiCatalog.Body(article);
            File.WriteAllText(Path.Combine(wiki, article.Id.Replace('/', '_') + ".md"), body, new UTF8Encoding(false));
            var panel = new StackPanel(); WikiEditorial.Render(panel, body, _ => { }, (title, _, _) => Ui.Text(title));
            var rendered = Walk(panel).Select(n => n switch {
                TextBlock t => "text: " + new TextRange(t.ContentStart, t.ContentEnd).Text,
                _ when n.GetType().Name == "FormulaControl" => "formula: " + n.GetType().GetProperty("Formula")?.GetValue(n),
                _ => null
            }).Where(t => t is not null);
            File.WriteAllText(Path.Combine(wiki, article.Id.Replace('/', '_') + ".txt"), string.Join("\n", rendered), new UTF8Encoding(false));
        }
        string[] queries = ["calcestruzzo", "acciaio", "palo", "micropalo", "muro", "fondazione", "terreno", "Nq", "Broms", "fessurazione", "tensioni", "taglio", "torsione", "dominio", "curvatura", "copriferro", "ancoraggio", "armature", "durabilità", "stabilità", "sisma", "ponte", "combinazioni", "inviluppo", "reologia", "stage", "sezioni", "report", "φ", "σ"];
        var search = new JsonObject(); foreach (string q in queries) search[q] = new JsonArray(WikiCatalog.Search(q).Select(a => JsonValue.Create(a.Id)).ToArray());
        File.WriteAllText(Path.Combine(wiki, "search.json"), CanonicalJson.Serialize(search, new(), normalizer));
        await MainWindow.CaptureF3Project(root, output, normalizer);
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {count} fixture; {WikiCatalog.Articles.Length} articoli; {queries.Length} ricerche; report progetto.");
        string expected = Path.Combine(root, "tests/fixtures/f3/expected");
        if (Directory.Exists(expected))
        {
            var comparer = new Anthea.Testing.Comparison.BaselineComparer(Anthea.Testing.Comparison.Tolerances.Load(Path.Combine(root, "tests/ANTHEA.Testing/tolerances.json")));
            // Transitions of F4 admitted one by one by the user (D2, 15/10/2026); any other difference, or a listed one that does not occur, fails.
            string transitions = Path.Combine(root, "tests/fixtures/f3/transitions-f4.json");
            if (File.Exists(transitions)) comparer.Admit(Anthea.Testing.Comparison.Transition.WithEditorialUpdate(
                Anthea.Testing.Comparison.Transition.Load(transitions),
                Anthea.Testing.Comparison.Transition.Load(Path.Combine(root, "tests/fixtures/f3/transitions-f5-wiki.json"))));
            var comparison = comparer.Compare(expected, output);
            File.WriteAllText(Path.Combine(output, "confronto.json"), comparison.ToJsonString(J.Options));
            if (comparer.NotAdmitted > 0) throw new InvalidOperationException($"Equivalenza F3: {comparer.NotAdmitted} differenze non ammesse. Leggere confronto.json.");
        }
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>(); var seen = new HashSet<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop(); if (!seen.Add(current)) continue;
            yield return current;
            var children = LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>().ToList();
            if (current is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) children.Add(VisualTreeHelper.GetChild(current, i));
            foreach (var child in children.AsEnumerable().Reverse()) pending.Push(child);
        }
    }

    static void Visit(FrameworkElement editor)
    {
        editor.Measure(new Size(1400, 1000)); editor.Arrange(new Rect(0, 0, 1400, 1000)); editor.UpdateLayout();
        var tabs = new HashSet<TabControl>();
        for (int pass = 0; pass < 8; pass++)
        {
            foreach (var e in Walk(editor).OfType<Expander>().ToArray()) e.IsExpanded = true;
            var found = Walk(editor).OfType<TabControl>().Where(t => tabs.Add(t)).ToArray(); if (found.Length == 0) break;
            foreach (var tab in found)
            {
                int selected = tab.SelectedIndex;
                for (int i = 0; i < tab.Items.Count; i++) { tab.SelectedIndex = i; editor.UpdateLayout(); }
                tab.SelectedIndex = selected;
            }
        }
    }

    internal static void CaptureReport(string path, string folder, TextNormalizer normalizer, GuidMap guids, string name = "report")
    {
        byte[] bytes = File.ReadAllBytes(path);
        File.WriteAllText(Path.Combine(folder, name + ".txt"), DocxText.Extract(bytes, normalizer, guids));
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("word/document.xml")!.Open(); var document = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        var structures = new JsonObject {
            ["tables"] = document.Descendants(w + "tbl").Count(),
            ["rows"] = document.Descendants(w + "tr").Count(),
            ["cells"] = document.Descendants(w + "tc").Count(),
            ["images"] = new JsonArray(document.Descendants(wp + "extent").Select(e => (JsonNode)new JsonObject {
                ["cx"] = (string?)e.Attribute("cx"), ["cy"] = (string?)e.Attribute("cy") }).ToArray()),
            ["styles"] = new JsonArray(document.Descendants(w + "pStyle").Select(e => JsonValue.Create((string?)e.Attribute(w + "val"))).ToArray())
        };
        File.WriteAllText(Path.Combine(folder, name + "-structure.json"), CanonicalJson.Serialize(structures, guids, normalizer));
    }
}

public sealed partial class MainWindow
{
    internal static async Task CaptureF3Project(string root, string output, TextNormalizer normalizer)
    {
        var project = new JsonObject { ["nome"] = "Progetto F3", ["fogli"] = new JsonArray(), ["strutture"] = new JsonArray() };
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*--default.json").Order(StringComparer.Ordinal))
        {
            var input = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            project["fogli"]!.AsArray().Add(new JsonObject { ["id"] = "000000000000000000000000" + (project["fogli"]!.AsArray().Count + 1).ToString("D8", CultureInfo.InvariantCulture), ["nome"] = input.S("module"),
                ["modulo_id"] = input.S("module"), ["dati"] = input["data"]!.DeepClone() });
        }
        var before = project.DeepClone(); var snapshot = SectionReportSnapshot(project);
        string folder = Path.Combine(output, "project"); Directory.CreateDirectory(folder);
        string raw = Path.Combine(output, "raw", "project"); Directory.CreateDirectory(raw);
        string filename = Path.Combine(raw, "report.docx");
        int warnings = await GenerateSectionReport(snapshot, filename, _ => { }, CancellationToken.None);
        if (!JsonNode.DeepEquals(before, project)) throw new InvalidOperationException("Il report ha modificato il progetto sorgente.");
        var guids = new GuidMap(); CanonicalJson.Register(project, guids); CanonicalJson.Register(snapshot, guids);
        File.WriteAllText(Path.Combine(folder, "snapshot.json"), CanonicalJson.Serialize(snapshot, guids, normalizer));
        File.WriteAllText(Path.Combine(folder, "status.json"), CanonicalJson.Serialize(new JsonObject { ["unavailable"] = warnings }, guids, normalizer));
        F3EquivalenceCapture.CaptureReport(filename, folder, normalizer, guids);
    }
}
