using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Anthea.Application.Presentation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.CompositeSection;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.CompositeSection;

namespace X.Desktop;

/// <summary>The F3 fixtures, the exports, the screenshots, the help, the automation names, read-only, the windows and the bench (F5.11).</summary>
internal static partial class CompositeSectionViewChecks
{
    // ---------------------------------------------------------------- 3. F3 fixtures and exports

    /// <summary>
    /// For the 9 F3 fixtures, on the path of the F3 capture: data and Modified after the automatic calculation of the opening, after the visit and after
    /// the calculation, the result, the sections saved by the report, equal to the values of the historic view (recorded before its archiving in
    /// tests/fixtures/f5-sezione-mista/vista-storica.json); the three appearances write nothing; the Word report and the PDF beside it equal to those of
    /// the recipe of the historic view (BridgeReport.BuildReportDocument: BridgeResultView.Report with the figures of ModuleFigureRenderer.BridgeFigures,
    /// then ReportFiles as SheetEditor.ExportReport) on the result; «Esporta CSV…» and «Esporta punti e fibre CSV…» with the BOM and the texts of the
    /// historic view.
    /// </summary>
    private static async Task FixturesAndExports(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var services = new Services();
        var editors = Editors(services);
        var recorded = Recorded(root)["esportazioni"]!.AsObject();
        var fixtures = CompositeSectionScenarios.Fixtures(root);
        Check(fixtures.Count == 9 && fixtures.Order(StringComparer.Ordinal).SequenceEqual(recorded.Select(p => p.Key).Order(StringComparer.Ordinal)),
            "Fixture F3 della sezione composta: " + fixtures.Count + ", tutte con i valori registrati della vista storica");
        int reports = 0, curves = 0;
        byte[] bom = Encoding.UTF8.GetPreamble();
        foreach (string id in fixtures)
        {
            string name = Module + "--" + id;
            var historic = recorded[id]!.AsObject();
            var data = CompositeSectionScenarios.FixtureData(root, id);
            using var mvvm = Idle(() => new Hosted(editors.Open(Module, data)));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: CompositeSectionSheetViewModel }, name + ": editor MVVM con la vista XAML");
            // The automatic calculation of the opening first: its publication chooses the situation that the commits save.
            await mvvm.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120));
            await mvvm.Settle();
            mvvm.Editor.Commit();
            Check(Same(mvvm, historic, "apertura"), name + ": dati all'apertura, dopo il calcolo automatico, uguali a quelli della vista storica (registrati)");
            // The visit of the F3 capture writes what the historic visit wrote (the view state of the pages and of the tabs, the local sketch).
            Idle(() => { Visit(mvvm.Root); return 0; });
            await mvvm.Settle();
            mvvm.Editor.Commit();
            Check(Same(mvvm, historic, "visita"), $"{name}: la visita della cattura F3 scrive come la vista storica ({mvvm.Modified} Modified, registrati)");
            // The three appearances write nothing.
            string visited = mvvm.Editor.Data.ToJsonString(J.Options); int modified = mvvm.Modified;
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            mvvm.Editor.Commit();
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == visited && mvvm.Modified == modified, name + ": i tre aspetti non scrivono e non sollevano Modified");
            await Idle(() => mvvm.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(120));
            await mvvm.Settle();
            mvvm.Editor.Commit();
            Dump(folder, name + "-calcolo", mvvm, historic);
            string? result = historic["risultato"]?.GetValue<string>();
            Check(Same(mvvm, historic, "calcolo") && mvvm.Editor.HasResults == historic["risultati"]!.GetValue<bool>()
                && (result is null ? mvvm.Editor.Result is null : mvvm.Editor.Result is { } r && Sha(r.ToJsonString(J.Options)) == result),
                name + ": dati e risultato dopo il calcolo uguali a quelli della vista storica (registrati)");
            var calculation = mvvm.Sheet.Calculation;
            // «Esporta CSV…» under the table of the page of the phases (in the tree also when another page is shown).
            Button CsvButton() => Caption(mvvm.View.ResultsPanel, "Esporta CSV…")!;
            if (result is null)
            {
                Check(calculation is null, name + ": senza risultato nessun calcolo mostrato, come la vista storica");
                services.Saves.Clear();
                Idle(() => mvvm.View.Pages.SelectedIndex = 1); await mvvm.Settle();
                Idle(() => { Press(CsvButton(), mvvm.Root); return 0; }); await mvvm.Settle();
                Check(mvvm.Sheet.Status == BridgeInputPresentation.WaitBeforeExport && services.Saves.Count == 0, name + ": senza risultato il CSV è rifiutato con lo stato della vista storica" + $" (stato «{mvvm.Sheet.Status}», pagina {mvvm.Sheet.Page}, pulsante {CsvButton().IsEnabled}/{CsvButton().IsVisible}/{Shown(CsvButton(), mvvm.Root)})");
                services.Saves.Clear();
                continue;
            }
            // Report Word and the PDF beside it, against the recipe of the historic view on the same result (after the export, which saves the
            // sections chosen in the view settings the figures read, as the historic view saved them before drawing).
            string title = "Fixture F3 · " + Module + " · " + id;
            var sections = ReportBridge.DefaultSections();
            void Export()
            {
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
                var images = ModuleFigureRenderer.BridgeFigures(calculation!, mvvm.Editor.Data["ui_mista"]!.AsObject(), sections).Select(f => f.Image()).ToList();
                Anthea.Reports.ReportFiles.Write(Path.Combine(folder, name + "-attesa.docx"), Anthea.Reports.DocxWriter.Write(calculation!.Report(title, sections, images)), title);
            }
            Export();
            // The state after the first export, the one the historic view recorded: an export repeated below for the PDF (cover across a
            // minute) saves the sections again and counts one more modification.
            bool savedAsHistoric = Same(mvvm, historic, "report");
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.docx")), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della ricetta della vista storica, figure comprese (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            int attempts = 1;
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))) && attempts < 3)
            {
                attempts++;
                Export();
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))),
                name + ": PDF affiancato uguale a quello della ricetta della vista storica (date normalizzate" + (attempts > 1 ? $", {attempts} esportazioni" : "") + ")");
            Check(savedAsHistoric, name + ": il report salva le sezioni scelte come la vista storica (registrati)");
            reports++;
            // «Esporta CSV…»: the bytes of the historic export (BOM and BridgeResultPresentation.Csv).
            string csv = Path.Combine(folder, name + "-tensioni.csv");
            services.SavePath = csv; services.Saves.Clear();
            int opened = mvvm.Sheet.Page;
            Idle(() => mvvm.View.Pages.SelectedIndex = 1); await mvvm.Settle();
            Idle(() => { Press(CsvButton(), mvvm.Root); return 0; });
            Check(services.Saves.Single() is { FileName: BridgeResultPresentation.CsvFileName } && WithBom(File.ReadAllBytes(csv), bom, historic["csv"]!.GetValue<string>()),
                name + ": «Esporta CSV…» uguale a quello della vista storica (nome proposto, BOM, testo registrato)");
            services.SavePath = null;
            Check((opened == 2) == historic.ContainsKey("curva_punti"), name + ": pagina delle curve all'apertura come nella vista storica");
            if (opened == 2)
            {
                Idle(() => mvvm.View.Pages.SelectedIndex = 2); await mvvm.Settle();
                // The curve of the fixture that opens on the page of the curves and «Esporta punti e fibre CSV…».
                var view = mvvm.View;
                Idle(() => { Press(view.CurveButton, mvvm.Root); return 0; });
                await CurveEnded(mvvm);
                await mvvm.Settle();
                int points = historic["curva_punti"]!.GetValue<int>();
                Check(mvvm.Sheet.ResponseCalculation is { } curve && curve.Points.Count == points && view.CurveExportButton.IsEnabled,
                    name + ": «Calcola curva» come la vista storica (" + points + " punti)");
                string path = Path.Combine(folder, name + "-curva.csv");
                services.SavePath = path; services.Saves.Clear();
                Idle(() => { Press(view.CurveExportButton, mvvm.Root); return 0; });
                Check(services.Saves.Single() is { FileName: BridgeResponsePresentation.CsvFileName } && WithBom(File.ReadAllBytes(path), bom, historic["curva_csv"]!.GetValue<string>()),
                    name + ": «Esporta punti e fibre CSV…» uguale a quello della vista storica (testo registrato)");
                services.SavePath = null;
                curves++;
            }
        }
        Log.Add($"   esportazioni: {reports} report, {curves} curve");
        Check(reports >= 7 && curves == 1, $"Report ({reports}) e curve ({curves}) confrontati con la vista storica");

        static bool Same(Hosted mvvm, JsonObject historic, string step) =>
            Sha(mvvm.Editor.Data.ToJsonString(J.Options)) == historic[step]!.GetValue<string>() && mvvm.Modified == historic[step + "_modified"]!.GetValue<int>();
        static bool WithBom(byte[] bytes, byte[] bom, string sha) => bytes.AsSpan().StartsWith(bom) && Sha(Encoding.UTF8.GetString(bytes, bom.Length, bytes.Length - bom.Length)) == sha;
    }

    /// <summary>The data and the result of the view beside the exports when they differ from the recorded values, to read the difference.</summary>
    private static void Dump(string folder, string name, Hosted mvvm, JsonObject historic)
    {
        string data = mvvm.Editor.Data.ToJsonString(J.Options); string? result = mvvm.Editor.Result?.ToJsonString(J.Options);
        if (Sha(data) == historic["calcolo"]?.GetValue<string>() && (result is null ? historic["risultato"] is null : Sha(result) == historic["risultato"]?.GetValue<string>())) return;
        File.WriteAllText(Path.Combine(folder, name + "-dati-mvvm.json"), data);
        File.WriteAllText(Path.Combine(folder, name + "-risultato-mvvm.json"), result ?? "null");
    }

    /// <summary>
    /// The values of the historic view recorded by this check before its archiving (passaggio della Release, 23/10/2026):
    /// tests/fixtures/f5-sezione-mista/vista-storica.json.
    /// </summary>
    private static JsonObject Recorded(string root) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "f5-sezione-mista", "vista-storica.json")))!.AsObject();

    /// <summary>SHA-256 of the UTF-8 text, as the values of <see cref="Recorded"/>.</summary>
    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>First part of two Word documents that differs, with the dates of core.xml and of the text normalized as in F3; null when equal.</summary>
    private static string? DifferentPart(byte[] a, byte[] b, TextNormalizer normalizer)
    {
        using var first = new ZipArchive(new MemoryStream(a)); using var second = new ZipArchive(new MemoryStream(b));
        var names = first.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(second.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal))) return "elenco delle parti";
        foreach (string name in names)
        {
            byte[] x = Read(first.GetEntry(name)!), y = Read(second.GetEntry(name)!);
            bool equal = name is "docProps/core.xml" or "word/document.xml"
                ? Normalize(name, x, normalizer) == Normalize(name, y, normalizer) : x.SequenceEqual(y);
            if (!equal) return name;
        }
        return null;
        static byte[] Read(ZipArchiveEntry entry) { using var stream = entry.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray(); }
        static string Normalize(string name, byte[] bytes, TextNormalizer normalizer) => name == "docProps/core.xml"
            ? normalizer.Normalize(Encoding.UTF8.GetString(bytes)) : normalizer.NormalizeReportTimestamps(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>Two PDF files equal but for what changes at every rendering (dates, identifiers, tags of the font subsets).</summary>
    private static bool SamePdf(byte[] a, byte[] b)
    {
        static string Text(byte[] bytes)
        {
            string text = Encoding.Latin1.GetString(bytes);
            text = Regex.Replace(text, @"\(D:[0-9+\-Z']+\)", "(D:)");
            text = Regex.Replace(text, @"/ID\s*\[[^\]]*\]", "/ID[]");
            text = Regex.Replace(text, @"(?<=/(?:FontName|BaseFont)/)[A-Z]{6}\+", "SUBSET+");
            text = Regex.Replace(text, @"(<xmp:\w*Date>)[^<]*(<)", "$1$2");
            return Regex.Replace(text, @"uuid:[0-9a-fA-F-]+", "uuid:");
        }
        return a.SequenceEqual(b) || Text(a) == Text(b);
    }

    // ---------------------------------------------------------------- 4. screenshots

    private static async Task Screenshots(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            using (var host = Idle(() => new Hosted(editors.Open(Module, CompositeSectionScenarios.FixtureData(root, "cassoncino-torsione-pioli")), mode != AppAppearance.Light, 1600, 1000)))
            {
                await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120)); await host.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"vista-fasi-{suffix}.png"), host.Png());
                Idle(() => host.View.Pages.SelectedIndex = 0); await host.Settle();
                foreach (var e in Walk(host.Root).OfType<Expander>().ToArray()) e.IsExpanded = true;
                await host.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"vista-pannello-{suffix}.png"), host.Png());
                written += 2;
            }
            using (var host = Idle(() => new Hosted(editors.Open(Module, CompositeSectionScenarios.FixtureData(root, "storico-non-lineare-curve")), mode != AppAppearance.Light, 1600, 1000)))
            {
                await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120)); await host.Settle();
                Idle(() => { Press(host.View.CurveButton, host.Root); return 0; });
                await CurveEnded(host); await host.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"vista-curve-{suffix}.png"), host.Png());
                written++;
            }
        }
        Appearance.Set(AppAppearance.Light, false);
        // The historic view is archived (23/10/2026): its screenshots stay in the artifacts of the runs before the switch.
        Check(written == 9, "Schermate della vista nei tre aspetti (fasi, pannello di controllo, curve): " + written + " PNG in " + folder + " (144 DPI su questa macchina)");
    }

    // ---------------------------------------------------------------- 5. help

    private static async Task Help(string root)
    {
        var services = new Services();
        var data = CompositeSectionScenarios.FixtureData(root, "irrigidimenti-appoggio-piastra2");
        using var host = Idle(() => new Hosted(Editors(services).Open(Module, data)));
        await host.Settle();
        static string[] Labels(DependencyObject root) => Walk(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
            .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
        // The labels of the historic view on the same fixture, recorded before its archiving.
        string[] before = Recorded(root)["aiuto"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray(), after = Labels(host.Root);
        Check(after.SequenceEqual(before), "Etichette «?» visibili uguali a quelle della vista storica, registrate (" + before.Length + ")"
            + (after.SequenceEqual(before) ? "" : ": storica " + string.Join(", ", before.Except(after)) + "; nuova " + string.Join(", ", after.Except(before))));
        // The three pages, every expander opened: every «?» and every F1 on a field reaches the navigator of the services (lesson 1 of 17/10).
        int presses = 0, fields = 0;
        var forms = new HashSet<FormViewModel>();
        services.Wiki.Clear();
        for (int page = 0; page < 3; page++)
        {
            Idle(() => host.View.Pages.SelectedIndex = page); await host.Settle();
            for (int pass = 0; pass < 3; pass++) { foreach (var e in Walk(host.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await host.Settle(); }
            foreach (var button in Walk(host.Root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?").ToArray())
            { button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); presses++; }
            var editors = Walk(host.Root).OfType<Control>().Where(e => e.IsVisible && e.DataContext is FieldViewModel { HelpUri: not null } && e is TextBox or ComboBox or CheckBox && e.TemplatedParent is not ComboBox).ToArray();
            foreach (var editor in editors) { F1(editor); presses++; forms.Add(((FieldViewModel)editor.DataContext).Form); }
            fields += editors.Length;
        }
        Check(presses > 50 && services.Wiki.Count == presses && services.Wiki.All(u => u is not null),
            $"«?» e F1 su {fields} campi di {forms.Count} form nelle tre pagine: ogni pressione arriva al navigatore della Wiki dei servizi ({services.Wiki.Count}/{presses})");
        Check(host.Sheet.Forms.Values.Concat(host.Sheet.Phases.SelectMany(c => c.Homogenization is null ? new[] { c.Form } : [c.Form, c.Homogenization]))
            .Append(host.Sheet.SlabPropertiesForm!).Append(host.Sheet.CurveForm!).All(f => f.WikiNavigator is not null), "Ogni form del foglio ha il navigatore della Wiki dei servizi");
        string guide = Anthea.Wiki.WikiCatalog.ForModule(Module)?.Id ?? throw new InvalidOperationException("Guida assente nella Wiki: " + Module);
        services.Wiki.Clear();
        Idle(() => host.View.Pages.SelectedIndex = 1); await host.Settle();
        F1(host.View.Pages); F1(host.View.StatusText);
        Check(services.Wiki.SequenceEqual([guide, guide]), "F1 fuori dai campi: la guida del modulo dal navigatore (" + guide + ")");
        host.Editor.InspectRevision(); await host.Settle();
        services.Wiki.Clear();
        var help = Walk(host.Root).OfType<Button>().First(b => b.IsVisible && b.Content as string == "?");
        Check(help.IsEnabled, "«?» utilizzabile in sola lettura");
        int before2 = host.Modified;
        help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
        F1(Walk(host.Root).OfType<TextBox>().First(t => t.IsVisible && t.DataContext is FieldViewModel { HelpUri: not null }));
        Check(services.Wiki.Count == 2 && host.Modified == before2, "«?» e F1 anche in sola lettura, senza modificare il foglio");
    }

    // ---------------------------------------------------------------- 6. automation names

    private static async Task AutomationNames(string root)
    {
        static HashSet<string> Names(DependencyObject root) => Walk(root).OfType<UIElement>().Where(e => e.IsVisible)
            .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
        var editors = Editors(new Services());
        var recorded = Recorded(root)["nomi"]!.AsObject();
        foreach (string id in new[] { "default", "cassoncino-torsione-pioli", "irrigidimenti-appoggio-piastra2", "storico-non-lineare-curve" })
        {
            var data = CompositeSectionScenarios.FixtureData(root, id);
            using var host = Idle(() => new Hosted(editors.Open(Module, data)));
            await host.Settle();
            // The names of the historic view on the three pages, every expander opened, recorded before its archiving.
            var before = recorded[id]!.AsArray().Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal); var after = new HashSet<string>(StringComparer.Ordinal);
            for (int page = 0; page < 3; page++)
            {
                var pages = Walk(host.Root).OfType<TabControl>().First();
                Idle(() => pages.SelectedIndex = page); await host.Settle();
                for (int pass = 0; pass < 3; pass++) { foreach (var e in Walk(host.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await host.Settle(); }
                after.UnionWith(Names(host.Root));
            }
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
            Check(missing.Length == 0, $"{id}: nomi di automazione della vista storica conservati ({before.Count}, registrati)" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
        }
    }

    // ---------------------------------------------------------------- 7. read-only

    /// <summary>
    /// The controls that accept the user after InspectRevision, in the new view on the three pages, against those of the historic view on the same
    /// data, recorded before its archiving.
    /// </summary>
    private static async Task ReadOnly(string root)
    {
        var recorded = Recorded(root)["sola_lettura"]!.AsObject();
        foreach (string id in new[] { "default", "cassoncino-torsione-pioli" })
        {
            var data = CompositeSectionScenarios.FixtureData(root, id);
            using var host = Idle(() => new Hosted(Editors(new Services()).Open(Module, data)));
            await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120));
            await host.Settle();
            host.Editor.InspectRevision();
            int modified = host.Modified;
            var before = recorded[id]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>(), StringComparer.Ordinal);
            var after = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int page = 0; page < 3; page++)
            {
                var pages = Walk(host.Root).OfType<TabControl>().First();
                Idle(() => pages.SelectedIndex = page); await host.Settle();
                for (int pass = 0; pass < 3; pass++) { foreach (var e in Walk(host.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await host.Settle(); }
                foreach (var (key, value) in Controls(host.Root)) after[key] = after.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
            }
            var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
            var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
            Log.Add($"   sola lettura ({id}), solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), comuni: " + string.Join(", ", common.Select(k => k + " " + after[k])));
            Check(different.Length == 0 && common.Length >= 40, $"{id}: sola lettura, stessi controlli bloccati e liberi della vista storica, registrati ({common.Length} controlli comuni)"
                + (different.Length == 0 ? "" : ": " + string.Join("; ", different)));
            Check(host.Editor.HasResults, id + ": sola lettura, il risultato resta");
        }
    }

    /// <summary>The state of the visible input controls, keyed by kind and name (automation name or content).</summary>
    private static Dictionary<string, string> Controls(DependencyObject root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in Walk(root).OfType<Control>().Where(e => e.IsVisible))
        {
            string? key = element switch
            {
                TextBox t when t.TemplatedParent is not Control && AutomationProperties.GetName(t) is { Length: > 0 } name => "testo " + name,
                ComboBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "scelta " + name,
                CheckBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "spunta " + name,
                CheckBox c when c.Content is string text => "spunta " + text,
                Button b when b.Content is string text && text != "?" && b.TemplatedParent is not Control => "pulsante " + text,
                Slider => "cursore",
                _ => null
            };
            if (key is null) continue;
            string value = element switch
            {
                TextBox t => t.IsReadOnly || !t.IsEnabled ? "bloccato" : "libero",
                _ => element.IsEnabled ? "libero" : "bloccato"
            };
            map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
        }
        return map;
    }

    // ---------------------------------------------------------------- 8. the windows of the view

    /// <summary>
    /// The detached view (SM3): the window of the historic view (title, size), the drawing and its toolbar inside, the situation chosen there, the
    /// placeholder in the sheet, «Riaggancia» from the window and from the sheet, the window closed with the sheet. «Espandi», «PNG» and
    /// «Info modello…» (the texts of the historic window, the four sketches).
    /// </summary>
    private static async Task Windows(string root, string output)
    {
        var services = new Services();
        using (var host = Idle(() => new Hosted(Editors(services).Open(Module, CompositeSectionScenarios.FixtureData(root, "default")))))
        {
            await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120)); await host.Settle();
            var view = host.View; var sheet = host.Sheet;
            int modified = host.Modified;
            Idle(() => { Press(view.DetachButton, host.Root); return 0; }); await host.Settle();
            var window = view.DetachedWindow;
            Check(window is { Title: CompositeSectionSheetView.DetachedTitle, Width: 1400, Height: 900, MinWidth: 880, MinHeight: 550 } && sheet.IsDetached
                && ReferenceEquals(Walk(window).OfType<Border>().FirstOrDefault(b => ReferenceEquals(b, view.Viewport)), view.Viewport)
                && view.DetachedPlaceholder.Visibility == Visibility.Visible && view.ViewportHost.Content is null,
                "«Stacca vista»: la finestra della vista storica (titolo, 1400×900, minimo 880×550) con il disegno e la sua barra; nel foglio il segnaposto");
            Check(sheet.Page == 1 && sheet.DisplayIndex == 0 && view.DisplayChoice.IsVisible && host.Modified > modified, "«Stacca vista» mostra la pagina delle fasi e le tensioni totali, come la vista storica");
            File.WriteAllBytes(Path.Combine(output, "schermate", "vista-staccata.png"), host.Png((FrameworkElement)window!.Content));
            var detached = view.DetachedView!;
            Idle(() => detached.SituationChoice.SelectedIndex = 0); await host.Settle();
            Check(sheet.SelectedSituation == 0 && view.StageChoice.SelectedIndex == 0 && view.Drawing.Stage == sheet.DisplayedCalculation?.Stages[0], "Situazione scelta nella finestra staccata: il foglio e il disegno la seguono");
            Idle(() => { Press(Caption(detached, "Riaggancia"), detached); return 0; }); await host.Settle();
            Check(view.DetachedWindow is null && !sheet.IsDetached && ReferenceEquals(view.ViewportHost.Content, view.Viewport) && view.DetachedPlaceholder.Visibility == Visibility.Collapsed,
                "«Riaggancia» della finestra: il disegno torna nel foglio");
            Idle(() => { Press(view.DetachButton, host.Root); return 0; }); await host.Settle();
            Idle(() => { Press(Caption(view.DetachedPlaceholder, "Riaggancia vista"), host.Root); return 0; }); await host.Settle();
            Check(view.DetachedWindow is null && !sheet.IsDetached, "«Riaggancia vista» del segnaposto: il disegno torna nel foglio");
            Idle(() => { Press(view.DetachButton, host.Root); return 0; }); await host.Settle();
            window = view.DetachedWindow!;
            bool closed = false; window.Closed += (_, _) => closed = true;
            // «Espandi»: the drawing in a dialog of the view, then back in its frame.
            Idle(() => { Press(Caption(view.Viewport, "Espandi"), view.Viewport); return 0; }); await host.Settle();
            var expanded = host.Dialogs.LastOrDefault();
            Check(expanded is { Title: CompositeSectionSheetViewModel.DrawingTitle, Width: 1200, Height: 800 } && ReferenceEquals(expanded.Content, view.Drawing), "«Espandi»: il disegno in una finestra di 1200×800 col titolo del riquadro");
            expanded!.Close(); host.Dialogs.Remove(expanded); await host.Settle();
            Check(ReferenceEquals(view.DrawingHost.Content, view.Drawing), "Chiusa la finestra di «Espandi» il disegno torna nel riquadro");
            // «PNG»: the image of the drawing written by the sheet in the file chosen.
            string png = Path.Combine(output, "disegno.png");
            services.SavePath = png; services.Saves.Clear();
            Idle(() => { Press(Caption(view.Viewport, "PNG"), view.Viewport); return 0; });
            Check(services.Saves.Single() is { FileName: "ANTHEA_Sezione_composta_·_geometria_e_tensioni.png" } request && FileFilter.Join(request.Filters) == "Immagine PNG|*.png"
                && BitmapDecoder.Create(new MemoryStream(File.ReadAllBytes(png)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0].PixelWidth > 100,
                "«PNG»: l'immagine del disegno nel file scelto, col nome proposto della vista storica");
            services.SavePath = null;
            host.Dispose();
            Check(closed, "La finestra staccata si chiude con il foglio");
        }
        // «Info modello…»: the texts of the historic window (ShowModelInformation) and the four sketches.
        using (var host = Idle(() => new Hosted(Editors(services).Open(Module, CompositeSectionScenarios.FixtureData(root, "default")))))
        {
            await host.Settle();
            Idle(() => { Press(host.View.InformationButton, host.Root); return 0; }); await host.Settle();
            var dialog = host.Dialogs.Single();
            var information = Walk(dialog).OfType<CompositeSectionInformationView>().Single();
            Check(dialog.Title == CompositeSectionSheetView.InformationTitle && dialog.Width == 1020 && dialog.Height == 800 && Walk(dialog).OfType<BridgeMethodSketch>().Select(s => s.Kind).SequenceEqual([0, 1, 2, 3]),
                "«Info modello…»: la finestra della vista storica (titolo, 1020×800) con i quattro schemi del metodo");
            var texts = Walk(information).OfType<TextBlock>().Where(t => t.TemplatedParent is null).Select(t => t.Text).Where(t => t.Length > 0 && t != "Chiudi").ToArray();
            // The texts of the historic window (ShowModelInformation of X.Desktop/Wpf/BridgeInformation.cs), recorded before its archiving.
            var expected = Recorded(root)["info_modello"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
            Check(texts.SequenceEqual(expected), $"«Info modello…»: i {expected.Length} testi della finestra storica, nello stesso ordine (registrati)"
                + (texts.SequenceEqual(expected) ? "" : ": primo diverso " + texts.Zip(expected).FirstOrDefault(p => p.First != p.Second)));
            File.WriteAllBytes(Path.Combine(output, "schermate", "info-modello.png"), host.Png((FrameworkElement)dialog.Content));
            Press(Caption(information, "Chiudi"), information); await host.Settle();
            Check(!dialog.IsVisible, "«Chiudi» chiude la finestra delle informazioni");
            host.Dialogs.Clear();
        }
    }

    // ---------------------------------------------------------------- 9. bench and Release

    private static void Bench(string root)
    {
        var editors = Editors(new Services());
        using (var mvvm = editors.Open(Module, CompositeSectionScenarios.FixtureData(root, "default")))
            Check(mvvm is MvvmSheetEditor { Sheet: CompositeSectionSheetViewModel sheet } && sheet.Forms.Values.All(f => f.WikiNavigator is not null), "Con --module-impl il banco apre la vista MVVM della sezione composta");
        using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(Module, CompositeSectionScenarios.FixtureData(root, "default")))
            Check(release is MvvmSheetEditor { Sheet: CompositeSectionSheetViewModel } && ModuleImplementations.Release.For(Module) == SheetImplementation.Mvvm,
                "La Release apre la sezione composta da ponte con la vista MVVM (passaggio del 23/10/2026)");
        bool refused = false;
        try { ModuleHostBench.Implementations([ModuleHostBench.Argument, Module + "=historic"]); }
        catch (ArgumentException error) when (error.Message.Contains("archiviata", StringComparison.Ordinal)) { refused = true; }
        Check(refused, "Banco: la vista storica della sezione composta è archiviata, «historic» rifiutato");
    }
}
