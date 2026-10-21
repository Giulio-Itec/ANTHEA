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

    private static async Task FixturesAndExports(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var services = new Services();
        var editors = Editors(services);
        var fixtures = CompositeSectionScenarios.Fixtures(root);
        Check(fixtures.Count == 9, "Fixture F3 della sezione composta: " + fixtures.Count);
        int reports = 0, curves = 0;
        byte[] bom = Encoding.UTF8.GetPreamble();
        foreach (string id in fixtures)
        {
            string name = Module + "--" + id;
            var data = CompositeSectionScenarios.FixtureData(root, id);
            using var mvvm = Idle(() => new Hosted(editors.Open(Module, data)));
            using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: CompositeSectionSheetViewModel }, name + ": editor MVVM con la vista XAML");
            // The automatic calculation of the opening first, on both views: its publication chooses the situation that the commits save.
            await mvvm.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120)); await historic.Historic.PendingForChecks.WaitAsync(TimeSpan.FromSeconds(120));
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Check(Same(mvvm, historic), name + ": dati all'apertura, dopo il calcolo automatico, uguali a quelli della vista storica");
            // The visit of the F3 capture writes what the historic visit writes (the view state of the pages and of the tabs, the local sketch).
            Idle(() => { Visit(mvvm.Root); return 0; }); Idle(() => { Visit(historic.Root); return 0; });
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Check(Same(mvvm, historic) && mvvm.Modified == historic.Modified, $"{name}: la visita della cattura F3 scrive come la vista storica ({mvvm.Modified} Modified)");
            // The three appearances write nothing.
            string visited = mvvm.Editor.Data.ToJsonString(J.Options); int modified = mvvm.Modified;
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            mvvm.Editor.Commit();
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == visited && mvvm.Modified == modified, name + ": i tre aspetti non scrivono e non sollevano Modified");
            await Idle(() => mvvm.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(120));
            await Idle(() => historic.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(120));
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Dump(folder, name + "-calcolo", mvvm, historic);
            Check(Same(mvvm, historic) && mvvm.Editor.HasResults == historic.Editor.HasResults
                && (historic.Editor.Result is null || mvvm.Editor.Result!.ToJsonString(J.Options) == historic.Editor.Result.ToJsonString(J.Options)),
                name + ": dati e risultato dopo il calcolo uguali a quelli della vista storica");
            var bridge = historic.Historic;
            // «Esporta CSV…» under the table of the page of the phases (in the tree also when another page is shown).
            Button CsvButton() => Caption(mvvm.View.ResultsPanel, "Esporta CSV…")!;
            if (bridge.Calculation is null)
            {
                // «Esporta CSV…» is under the table of the page of the phases.
                services.Saves.Clear();
                Idle(() => mvvm.View.Pages.SelectedIndex = 1); await mvvm.Settle();
                Idle(() => { Press(CsvButton(), mvvm.Root); return 0; }); await mvvm.Settle();
                Check(mvvm.Sheet.Status == BridgeInputPresentation.WaitBeforeExport && services.Saves.Count == 0, name + ": senza risultato il CSV è rifiutato con lo stato della vista storica" + $" (stato «{mvvm.Sheet.Status}», pagina {mvvm.Sheet.Page}, pulsante {CsvButton().IsEnabled}/{CsvButton().IsVisible}/{Shown(CsvButton(), mvvm.Root)})");
                services.Saves.Clear();
                continue;
            }
            // Report Word and the PDF beside it.
            string title = "Fixture F3 · " + Module + " · " + id;
            var sections = ReportBridge.DefaultSections();
            mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
            historic.Editor.ExportReport(Path.Combine(folder, name + "-storica.docx"), title, sections);
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.docx")), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della vista storica, figure comprese (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            int attempts = 1;
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))) && attempts < 3)
            {
                attempts++;
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
                historic.Editor.ExportReport(Path.Combine(folder, name + "-storica.docx"), title, sections);
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))),
                name + ": PDF affiancato uguale a quello della vista storica (date normalizzate" + (attempts > 1 ? $", {attempts} esportazioni" : "") + ")");
            Check(Same(mvvm, historic) && mvvm.Modified == historic.Modified, name + ": il report salva le sezioni scelte come la vista storica");
            reports++;
            // «Esporta CSV…»: the bytes of the historic export (BOM and BridgeResultPresentation.Csv).
            string csv = Path.Combine(folder, name + "-tensioni.csv");
            services.SavePath = csv; services.Saves.Clear();
            int opened = mvvm.Sheet.Page;
            Idle(() => mvvm.View.Pages.SelectedIndex = 1); await mvvm.Settle();
            Idle(() => { Press(CsvButton(), mvvm.Root); return 0; });
            Check(services.Saves.Single() is { FileName: BridgeResultPresentation.CsvFileName } && File.ReadAllBytes(csv).SequenceEqual(bom.Concat(Encoding.UTF8.GetBytes(BridgeResultPresentation.Csv(bridge.Calculation)))),
                name + ": «Esporta CSV…» uguale a quello della vista storica (nome proposto, BOM, testo)");
            services.SavePath = null;
            if (opened == 2)
            {
                Idle(() => mvvm.View.Pages.SelectedIndex = 2); await mvvm.Settle();
                // The curve of the fixture that opens on the page of the curves, calculated on both views, and «Esporta punti e fibre CSV…».
                var view = mvvm.View;
                Idle(() => { Press(view.CurveButton, mvvm.Root); return 0; });
                await CurveEnded(mvvm);
                await Idle(() => bridge.CalculateResponseAsync()).WaitAsync(TimeSpan.FromSeconds(120));
                await mvvm.Settle(); await historic.Settle();
                Check(bridge.ResponseCalculation is { } response && mvvm.Sheet.ResponseCalculation is { } curve && curve.Points.Count == response.Points.Count && view.CurveExportButton.IsEnabled,
                    name + ": «Calcola curva» come la vista storica (" + bridge.ResponseCalculation?.Points.Count + " punti)");
                string points = Path.Combine(folder, name + "-curva.csv");
                services.SavePath = points; services.Saves.Clear();
                Idle(() => { Press(view.CurveExportButton, mvvm.Root); return 0; });
                Check(services.Saves.Single() is { FileName: BridgeResponsePresentation.CsvFileName }
                    && File.ReadAllBytes(points).SequenceEqual(bom.Concat(Encoding.UTF8.GetBytes(BridgeEditor.ResponseCsv(bridge.ResponseCalculation!)))),
                    name + ": «Esporta punti e fibre CSV…» uguale a quello della vista storica");
                services.SavePath = null;
                curves++;
            }
        }
        Log.Add($"   esportazioni: {reports} report, {curves} curve");
        Check(reports >= 7 && curves == 1, $"Report ({reports}) e curve ({curves}) confrontati con la vista storica");

        static bool Same(Hosted a, Hosted b) => a.Editor.Data.ToJsonString(J.Options) == b.Editor.Data.ToJsonString(J.Options);
    }

    /// <summary>The data and the results of the two views beside the exports when they differ, to read the difference.</summary>
    private static void Dump(string folder, string name, Hosted mvvm, Hosted historic)
    {
        string a = mvvm.Editor.Data.ToJsonString(J.Options), b = historic.Editor.Data.ToJsonString(J.Options);
        string? ra = mvvm.Editor.Result?.ToJsonString(J.Options), rb = historic.Editor.Result?.ToJsonString(J.Options);
        if (a == b && ra == rb) return;
        File.WriteAllText(Path.Combine(folder, name + "-dati-mvvm.json"), a); File.WriteAllText(Path.Combine(folder, name + "-dati-storica.json"), b);
        File.WriteAllText(Path.Combine(folder, name + "-risultato-mvvm.json"), ra ?? "null"); File.WriteAllText(Path.Combine(folder, name + "-risultato-storica.json"), rb ?? "null");
    }

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
        // The historic view at the same size, for the comparison by eye.
        using (var historic = Idle(() => new Hosted(new SheetEditor(Module, CompositeSectionScenarios.FixtureData(root, "cassoncino-torsione-pioli")), false, 1600, 1000)))
        {
            await historic.Historic.PendingForChecks.WaitAsync(TimeSpan.FromSeconds(120)); await historic.Settle();
            File.WriteAllBytes(Path.Combine(folder, "storica-fasi-chiara.png"), historic.Png());
            written++;
        }
        Check(written == 10, "Schermate della vista nei tre aspetti (fasi, pannello di controllo, curve) e della vista storica: " + written + " PNG in " + folder + " (144 DPI su questa macchina)");
    }

    // ---------------------------------------------------------------- 5. help

    private static async Task Help(string root)
    {
        var services = new Services();
        var data = CompositeSectionScenarios.FixtureData(root, "irrigidimenti-appoggio-piastra2");
        using var host = Idle(() => new Hosted(Editors(services).Open(Module, data)));
        using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
        await host.Settle(); await historic.Settle();
        static string[] Labels(DependencyObject root) => Walk(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
            .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
        string[] before = Labels(historic.Root), after = Labels(host.Root);
        Check(after.SequenceEqual(before), "Etichette «?» visibili uguali a quelle della vista storica (" + before.Length + ")"
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
        foreach (string id in new[] { "default", "cassoncino-torsione-pioli", "irrigidimenti-appoggio-piastra2", "storico-non-lineare-curve" })
        {
            var data = CompositeSectionScenarios.FixtureData(root, id);
            using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
            using var host = Idle(() => new Hosted(editors.Open(Module, data)));
            await historic.Settle(); await host.Settle();
            var before = new HashSet<string>(StringComparer.Ordinal); var after = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hosted in new[] { historic, host })
            {
                var names = ReferenceEquals(hosted, historic) ? before : after;
                for (int page = 0; page < 3; page++)
                {
                    var pages = Walk(hosted.Root).OfType<TabControl>().First();
                    Idle(() => pages.SelectedIndex = page); await hosted.Settle();
                    for (int pass = 0; pass < 3; pass++) { foreach (var e in Walk(hosted.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await hosted.Settle(); }
                    names.UnionWith(Names(hosted.Root));
                }
            }
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
            Check(missing.Length == 0, $"{id}: nomi di automazione della vista storica conservati ({before.Count})" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
        }
    }

    // ---------------------------------------------------------------- 7. read-only

    /// <summary>The controls that accept the user after InspectRevision, in the new view and in the historic view on the same data, on the three pages.</summary>
    private static async Task ReadOnly(string root)
    {
        foreach (string id in new[] { "default", "cassoncino-torsione-pioli" })
        {
            var data = CompositeSectionScenarios.FixtureData(root, id);
            using var host = Idle(() => new Hosted(Editors(new Services()).Open(Module, data)));
            using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
            await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120));
            await historic.Historic.PendingForChecks.WaitAsync(TimeSpan.FromSeconds(120));
            await host.Settle(); await historic.Settle();
            host.Editor.InspectRevision(); historic.Editor.InspectRevision();
            int modified = host.Modified;
            var before = new Dictionary<string, string>(StringComparer.Ordinal); var after = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var hosted in new[] { historic, host })
            {
                var map = ReferenceEquals(hosted, historic) ? before : after;
                for (int page = 0; page < 3; page++)
                {
                    var pages = Walk(hosted.Root).OfType<TabControl>().First();
                    Idle(() => pages.SelectedIndex = page); await hosted.Settle();
                    for (int pass = 0; pass < 3; pass++) { foreach (var e in Walk(hosted.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await hosted.Settle(); }
                    foreach (var (key, value) in Controls(hosted.Root)) map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
                }
            }
            var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
            var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
            Log.Add($"   sola lettura ({id}), solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), comuni: " + string.Join(", ", common.Select(k => k + " " + after[k])));
            Check(different.Length == 0 && common.Length >= 40, $"{id}: sola lettura, stessi controlli bloccati e liberi della vista storica ({common.Length} controlli comuni)"
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
            var expected = HistoricInformationTexts(root);
            Check(texts.SequenceEqual(expected), $"«Info modello…»: i {expected.Count} testi della finestra storica, nello stesso ordine"
                + (texts.SequenceEqual(expected) ? "" : ": primo diverso " + texts.Zip(expected).FirstOrDefault(p => p.First != p.Second)));
            File.WriteAllBytes(Path.Combine(output, "schermate", "info-modello.png"), host.Png((FrameworkElement)dialog.Content));
            Press(Caption(information, "Chiudi"), information); await host.Settle();
            Check(!dialog.IsVisible, "«Chiudi» chiude la finestra delle informazioni");
            host.Dialogs.Clear();
        }
    }

    /// <summary>The texts of the historic window «Info modello…» (titles of the groups, texts and notices), in the order of X.Desktop/Wpf/BridgeInformation.cs.</summary>
    private static IReadOnlyList<string> HistoricInformationTexts(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, "X.Desktop", "Wpf", "BridgeInformation.cs"));
        source = source[source.IndexOf("var content = Ui.Stack(", StringComparison.Ordinal)..source.IndexOf("var dialog = Ui.Dialog", StringComparison.Ordinal)];
        const string literal = "\"((?:[^\"\\\\]|\\\\.)*)\"";
        var pattern = new Regex(@"Group\(" + literal + @"|Ui\.Text\(" + literal + @"|Notice\(" + literal + @"\)|Notice\(BridgeEditor\.Scope\)");
        return pattern.Matches(source).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : BridgeEditor.Scope)
            .Select(t => t.Replace("\\n", "\n")).ToArray();
    }

    // ---------------------------------------------------------------- 9. bench and Release

    private static void Bench(string root)
    {
        var editors = Editors(new Services());
        using (var mvvm = editors.Open(Module, CompositeSectionScenarios.FixtureData(root, "default")))
            Check(mvvm is MvvmSheetEditor { Sheet: CompositeSectionSheetViewModel sheet } && sheet.Forms.Values.All(f => f.WikiNavigator is not null), "Con --module-impl il banco apre la vista MVVM della sezione composta");
        using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(Module, CompositeSectionScenarios.FixtureData(root, "default")))
            Check(release is SheetEditor { bridge: not null } && ModuleImplementations.Release.For(Module) == SheetImplementation.Historic, "La Release resta storica fino alla prova dell'utente");
    }
}
