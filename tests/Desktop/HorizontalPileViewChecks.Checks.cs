using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.Common;
using X.Desktop.Views.HorizontalPile;

namespace X.Desktop;

/// <summary>The checks of the view outside the traces: visit and exports, F5-02, help, automation names, read-only, soil dialog, bench.</summary>
internal static partial class HorizontalPileViewChecks
{
    /// <summary>Every F3 fixture of the two modules (21), as the bench opens them.</summary>
    private static (string Module, string Id)[] Fixtures(string root) =>
        new[] { Pile, Micro }.SelectMany(m => HorizontalPileScenarios.Fixtures(root, m).Select(id => (m, id))).ToArray();

    /// <summary>A copy of a document without the times measured with a stopwatch (tempi_ms) of the elastic response.</summary>
    private static string WithoutTimes(JsonNode? node)
    {
        var copy = node?.DeepClone();
        static void Strip(JsonNode? value)
        {
            if (value is JsonObject o) { o.Remove("tempi_ms"); foreach (var (_, child) in o) Strip(child); }
            else if (value is JsonArray a) foreach (var item in a) Strip(item);
        }
        Strip(copy);
        return copy?.ToJsonString(J.Options) ?? "null";
    }

    // ---------------------------------------------------------------- 2. visit and exports

    private static async Task FixturesAndExports(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var services = new Services();
        var editors = Editors(services);
        var fixtures = Fixtures(root);
        Check(fixtures.Length == 21, "Fixture F3 dei pali orizzontali: " + fixtures.Length);
        foreach (var (module, id) in fixtures)
        {
            string name = module + "--" + id;
            var data = HorizontalPileScenarios.FixtureData(root, module, id);
            using var mvvm = new Hosted(editors.Open(module, data));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: HorizontalPileSheetViewModel }, name + ": editor MVVM con la vista XAML");
            await mvvm.Settle();
            string opened = mvvm.Editor.Data.ToJsonString(J.Options);
            // The visit without the tabs of the analyses and of the stratigraphies: «Estendi»/«Riduci» of the three cards, the groups and the
            // options opened and closed, «Diagrammi e dettagli» with every tab, the three appearances.
            foreach (string card in new[] { HorizontalPileSheetViewModel.SurveysCard, HorizontalPileSheetViewModel.ProfileCard, HorizontalPileSheetViewModel.SectionCard })
            {
                mvvm.Sheet.ToggleCardCommand.Execute(card); await mvvm.Settle();
                mvvm.Sheet.ToggleCardCommand.Execute(card); await mvvm.Settle();
            }
            foreach (var expander in Visual(mvvm.View.CapacityPage).OfType<Expander>().Where(e => !e.IsExpanded).ToArray())
            {
                expander.IsExpanded = true; await mvvm.Settle();
                expander.IsExpanded = false; await mvvm.Settle();
            }
            if (mvvm.View.DetailsButton.IsEnabled)
            {
                Press(mvvm.View.DetailsButton);
                await mvvm.Settle();
                foreach (var tabs in Visual(mvvm.Dialogs.Single()).OfType<TabControl>().ToArray())
                    for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; await mvvm.Settle(); }
                mvvm.CloseDialogs();
            }
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == opened && mvvm.Modified == 0, name + ": la visita (schede, gruppi, «Estendi», diagrammi, tre aspetti) non scrive e non solleva Modified");
            // The tabs of the analyses and of the stratigraphies, as the F3 visit: the same writes and Modified of the historic view.
            using var historic = new Hosted(new SheetEditor(module, data));
            await historic.Settle();
            var historicView = historic.Editor.View;
            void Tabs(TabControl tabs, Action layout) { int selected = tabs.SelectedIndex; for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; layout(); } tabs.SelectedIndex = selected; layout(); }
            Tabs(mvvm.View.Analyses, mvvm.Layout);
            Tabs(mvvm.View.Surveys, mvvm.Layout);
            var historicTabs = Visual(historicView).OfType<TabControl>().ToList();
            Tabs(historicTabs.First(t => t.Items.Count == 2 && t.Items.Cast<TabItem>().Any(i => Equals(i.Header, "Risposta elastica · trave su molle"))), historic.Layout);
            var surveyTabs = Visual(historicView).OfType<TabControl>().FirstOrDefault(t => t.Items.Cast<TabItem>().All(i => i.Header is Panel));
            if (surveyTabs is not null) Tabs(surveyTabs, historic.Layout);
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == historic.Editor.Data.ToJsonString(J.Options) && mvvm.Modified == historic.Modified,
                name + $": le schede delle analisi e delle stratigrafie scrivono come la vista storica ({mvvm.Modified} Modified)");
            // The automatic calculations of the two tabs first, on both sides: «Calcola» then finds the same state of the sessions (the counter of
            // the values calculated again, nuovi_valori_N, depends on it).
            var historicSheet = ((SheetEditor)historic.Editor).HorizontalForChecks;
            await Task.WhenAll(mvvm.Sheet.PendingCalculation, mvvm.Sheet.Elastic.PendingCalculation, historicSheet.PendingForChecks, historicSheet.ElasticForChecks.PendingForChecks)
                .WaitAsync(TimeSpan.FromSeconds(120));
            mvvm.Editor.Commit(); await mvvm.Editor.CalculateAsync(); mvvm.Editor.Commit();
            historic.Editor.Commit(); await historic.Editor.CalculateAsync(); historic.Editor.Commit();
            await mvvm.Settle(); await historic.Settle();
            if (WithoutTimes(mvvm.Editor.Data) != WithoutTimes(historic.Editor.Data) || WithoutTimes(mvvm.Editor.Result) != WithoutTimes(historic.Editor.Result))
                foreach (var (side, editor) in new[] { ("mvvm", mvvm.Editor), ("storica", historic.Editor) })
                {
                    File.WriteAllText(Path.Combine(folder, name + "-" + side + "-dati.json"), WithoutTimes(editor.Data));
                    File.WriteAllText(Path.Combine(folder, name + "-" + side + "-risultato.json"), WithoutTimes(editor.Result));
                }
            Check(WithoutTimes(mvvm.Editor.Data) == WithoutTimes(historic.Editor.Data) && mvvm.Editor.HasResults == historic.Editor.HasResults
                && WithoutTimes(mvvm.Editor.Result) == WithoutTimes(historic.Editor.Result),
                name + ": dati e risultato della scheda mostrata dopo «Calcola» uguali a quelli della vista storica (tempi esclusi)");
            if (!historic.Editor.HasResults)
            {
                Check(Fails(() => mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json")), out string message)
                    && Fails(() => historic.Editor.ExportResult(Path.Combine(folder, name + "-storica.json")), out string expected) && message == expected,
                    name + ": senza risultato l'esportazione è rifiutata con il messaggio della vista storica («" + message + "»)");
                continue;
            }
            string title = "Fixture F3 · " + module + " · " + id;
            mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json"));
            historic.Editor.ExportResult(Path.Combine(folder, name + "-storica.json"));
            Check(WithoutTimes(JsonNode.Parse(File.ReadAllText(Path.Combine(folder, name + "-mvvm.json")))) == WithoutTimes(JsonNode.Parse(File.ReadAllText(Path.Combine(folder, name + "-storica.json")))),
                name + ": JSON del risultato uguale a quello della vista storica (tempi esclusi)");
            mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, []);
            historic.Editor.ExportReport(Path.Combine(folder, name + "-storica.docx"), title, []);
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.docx")), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della vista storica, figure comprese (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            int attempts = 1;
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))) && attempts < 3)
            {
                attempts++;
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, []);
                historic.Editor.ExportReport(Path.Combine(folder, name + "-storica.docx"), title, []);
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))),
                name + ": PDF affiancato uguale a quello della vista storica (date normalizzate" + (attempts > 1 ? $", {attempts} esportazioni" : "") + ")");
            // «Esporta CSV» of the capacity: the file chosen in the dialog, the tables of the historic button.
            if (mvvm.Sheet.Result is { } capacity && historic.Editor is SheetEditor { HorizontalForChecks.Result: { } historicCapacity })
            {
                string csv = Path.Combine(folder, name + "-capacita.csv");
                services.SavePath = csv;
                Press(mvvm.View.CsvButton);
                services.SavePath = null;
                Check(File.Exists(csv) && File.ReadAllBytes(csv).SequenceEqual(Encoding.UTF8.GetBytes(HorizontalPileEditor.Csv(historicCapacity))),
                    name + ": «Esporta CSV» scrive le tabelle del bottone storico");
            }
        }
    }

    private static bool Fails(Action action, out string message)
    {
        try { action(); message = ""; return false; }
        catch (InvalidOperationException error) { message = error.Message; return true; }
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

    // ---------------------------------------------------------------- 3. F5-02 (O6)

    /// <summary>
    /// F5-02: after a custom value of the steel the choice «Acciaio» of the view shows «Personalizzato», as the data, while the historic view keeps the
    /// previous class; after a class of the catalogue the note of the view is the one of the values of the catalogue (the historic one is computed before).
    /// </summary>
    private static async Task CustomSteel(string root)
    {
        var data = HorizontalPileScenarios.FixtureData(root, Pile, "single-soil-layer");
        using var host = new Hosted(Editors(new Services()).Open(Pile, data));
        using var historic = new Hosted(new SheetEditor(Pile, data));
        await host.Settle(); await historic.Settle();
        var side = new CapacitySide(host, null, new Services());
        side.Attach();
        var form = ((SheetEditor)historic.Editor).HorizontalForChecks.SectionFormForChecks;
        var section = host.Editor.Data["sezione"]!.AsObject();
        var choice = (ComboBox)Visual(host.View.CapacityPage).OfType<ComboBox>().Single(c => c.DataContext is FieldViewModel { Key: "classe_acciaio" });
        // A custom value of the steel: the data say «Personalizzato» on both sides; the new view shows it, the historic one keeps «B450C».
        await side.Apply(new HorizontalPileStep(HorizontalPileStep.Field, "steel_fu_mpa", "560"));
        ((TextBox)form.Editors["steel_fu_mpa"]).Text = "560";
        Check(section.S("classe_acciaio") == "Personalizzato" && (string?)choice.SelectedItem == "Personalizzato"
            && historic.Editor.Data["sezione"].S("classe_acciaio") == "Personalizzato" && (string?)((ComboBox)form.Editors["classe_acciaio"]).SelectedItem == "B450C",
            "F5-02: dopo un valore dell'acciaio personalizzato la scelta «Acciaio» della vista mostra «Personalizzato» (la storica «B450C»), dati uguali");
        // A class of the catalogue after the custom values: the note of the new view is the one of the values of the catalogue. The historic note is
        // computed before the catalogue writes them, but with the name of the custom material still in the data it is empty as the final one: the
        // second, probable part of O6 does not show (logged).
        await side.Apply(new HorizontalPileStep(HorizontalPileStep.Field, "steel_modulus_mpa", "210000"));
        ((TextBox)form.Editors["steel_modulus_mpa"]).Text = "210000";
        await side.Apply(new HorizontalPileStep(HorizontalPileStep.Choice, "steel_diagramma", "Incrudente"));
        ((ComboBox)form.Editors["steel_diagramma"]).SelectedItem = "Incrudente";
        await side.Apply(new HorizontalPileStep(HorizontalPileStep.Choice, "classe_acciaio", "B450A"));
        ((ComboBox)form.Editors["classe_acciaio"]).SelectedItem = "B450A";
        string? expectedNote = MaterialInputCatalog.SteelDifference(section, "NTC 2018");
        var note = Visual(host.View.CapacityPage).OfType<Grid>().Single(g => g.Name == "Row" && g.DataContext is FieldViewModel { Key: "classe_acciaio" })
            .Children.OfType<Border>().Single(b => b.Name == "NoteBox");
        string shown = note.Visibility == Visibility.Visible ? ((TextBlock)note.Child).Text : "";
        string historicNote = HistoricNote(form, "classe_acciaio");
        Log.Add($"   F5-02 nota della classe dal catalogo: vista nuova «{shown}», storica «{historicNote}», attesa «{expectedNote ?? ""}»");
        Check(WithoutTimes(host.Editor.Data) == WithoutTimes(historic.Editor.Data) && (string?)choice.SelectedItem == "B450A" && shown == (expectedNote ?? ""),
            "F5-02: nota della classe dell'acciaio scelta dal catalogo calcolata sui valori del catalogo, dati uguali alla vista storica"
            + (historicNote != (expectedNote ?? "") ? " (la storica mostra la nota dei valori precedenti)" : " (anche la storica, la parte probabile di O6 non si presenta)"));
    }

    /// <summary>The text of the note of a field of a historic InputForm as shown («» when hidden), read through its private notes.</summary>
    private static string HistoricNote(InputForm form, string key)
    {
        var notes = (System.Collections.IDictionary)typeof(InputForm).GetField("notes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        if (notes[key] is not { } note) return "";
        var text = (TextBlock)note.GetType().GetField("Item1")!.GetValue(note)!;
        var box = (Border)note.GetType().GetField("Item2")!.GetValue(note)!;
        return box.Visibility == Visibility.Visible ? text.Text : "";
    }

    // ---------------------------------------------------------------- 5. help

    /// <summary>
    /// The «?» labels shown are those of the historic view; every «?» and every F1 on a field of every form of the tab reaches the navigator of the
    /// services (the topic of the field, or the guide of the module when the field has none); F1 elsewhere opens the guide; «Modello e dati comuni…»
    /// the theory page; also in a read-only revision.
    /// </summary>
    private static async Task Help(string root)
    {
        foreach (var (module, id) in new[] { (Pile, "broms-coesivo-impedita"), (Micro, "stratificato-chs-catalogo") })
        {
            var services = new Services();
            var data = HorizontalPileScenarios.FixtureData(root, module, id);
            string guide = Anthea.Wiki.WikiCatalog.ForModule(module)?.Id ?? throw new InvalidOperationException("Guida assente nella Wiki: " + module);
            using var host = new Hosted(Editors(services).Open(module, data));
            using var historic = new Hosted(new SheetEditor(module, data));
            await host.Settle(); await historic.Settle();
            var side = new CapacitySide(host, null, services);
            side.Attach();
            static string[] Labels(DependencyObject root) => Visual(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
                .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
            string[] before = Labels(historic.Root), after = Labels(host.Root);
            Check(after.SequenceEqual(before), module + ": etichette «?» visibili uguali a quelle della vista storica (" + before.Length + "): " + string.Join(", ", after));
            foreach (bool readOnly in new[] { false, true })
            {
                if (readOnly) { host.Editor.InspectRevision(); await host.Settle(); }
                // Every «?» of every form (the groups are opened as the user does, then closed again).
                var expanders = Visual(host.View.CapacityPage).OfType<Expander>().Where(e => !e.IsExpanded).ToArray();
                foreach (var expander in expanders) expander.IsExpanded = true;
                await host.Settle();
                services.Wiki.Clear();
                var helps = Visual(host.View.CapacityPage).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?").ToArray();
                foreach (var help in helps) help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
                Check(helps.Length > 0 && services.Wiki.Count == helps.Length && services.Wiki.All(u => u is not null),
                    module + (readOnly ? " (sola lettura)" : "") + $": ogni «?» ({helps.Length}) arriva al navigatore della Wiki");
                // F1 on every editor of every form: the topic of the field, otherwise the guide of the module.
                services.Wiki.Clear();
                var fields = Visual(host.View.CapacityPage).OfType<Control>().Where(e => e is TextBox or ComboBox or CheckBox && e.DataContext is FieldViewModel && e.TemplatedParent is not ComboBox && e.IsVisible).ToArray();
                var expected = fields.Select(e => ((FieldViewModel)e.DataContext).HelpUri ?? guide).ToList();
                foreach (var field in fields) F1(field);
                Check(fields.Length > 20 && services.Wiki.SequenceEqual(expected), module + (readOnly ? " (sola lettura)" : "") + $": F1 su ogni campo ({fields.Length}) arriva al navigatore della Wiki");
                foreach (var expander in expanders) expander.IsExpanded = false;
                await host.Settle();
                services.Wiki.Clear();
                F1(host.View.Surveys);
                Press(Visual(host.Root).OfType<Button>().Single(b => Equals(b.Content, CalculationGuide.Caption)));
                var article = Anthea.Wiki.WikiCatalog.ForModule(module)!;
                string theory = article.Related.Select(Anthea.Wiki.WikiCatalog.Resolve).FirstOrDefault(a => a is not null && a.Type == "theory" && a.Modules.Contains(module))?.Id ?? article.Id;
                Check(services.Wiki.SequenceEqual([guide, theory]), module + (readOnly ? " (sola lettura)" : "") + ": F1 fuori dai campi e «Modello e dati comuni…» dal navigatore: " + string.Join(", ", services.Wiki));
            }
            Check(host.Modified == 0, module + ": l'aiuto non modifica il foglio");
        }
    }

    // ---------------------------------------------------------------- 6. automation names

    private static async Task AutomationNames(string root)
    {
        static HashSet<string> Names(DependencyObject root) => Visual(root).OfType<UIElement>().Where(e => e.IsVisible)
            .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
        var editors = Editors(new Services());
        foreach (var (module, id) in new[] { (Pile, "broms-coesivo-impedita"), (Pile, "single-soil-layer"), (Micro, "stratificato-chs-catalogo"), (Micro, "single-soil-layer") })
        {
            var data = HorizontalPileScenarios.FixtureData(root, module, id);
            using var historic = new Hosted(new SheetEditor(module, data));
            using var host = new Hosted(editors.Open(module, data));
            await historic.Settle(); await host.Settle();
            var before = Names(historic.Root); var after = Names(host.Root);
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi nuovi ({module} {id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
            Check(missing.Length == 0, $"{module} {id}: nomi di automazione della vista storica conservati ({before.Count})" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
            // ui/smoke-horizontal (HorizontalLayoutChecks): the thickness of a layer by its name, the description of the calculation by its Tag.
            Check(after.Contains("Spessore [m]") && Visual(host.Root).OfType<FrameworkElement>().Any(e => Equals(e.Tag, "horizontal-calculation-description")),
                $"{module} {id}: «Spessore [m]» e il Tag «horizontal-calculation-description» letti da ui/smoke-horizontal");
        }
    }

    // ---------------------------------------------------------------- 7. read-only

    /// <summary>The controls of the tab of the capacity that accept the user after InspectRevision, in the new view and in the historic view.</summary>
    private static async Task ReadOnly(string root)
    {
        foreach (var (module, id) in new[] { (Pile, "broms-coesivo-impedita"), (Micro, "stratificato-chs-catalogo") })
        {
            var services = new Services();
            var data = HorizontalPileScenarios.FixtureData(root, module, id);
            using var host = new Hosted(Editors(services).Open(module, data));
            using var historic = new Hosted(new SheetEditor(module, data));
            await host.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
            await historic.Settle();
            await historic.Editor.CalculateAsync();
            host.Editor.InspectRevision(); historic.Editor.InspectRevision();
            await host.Settle(); await historic.Settle();
            var page = (DependencyObject)host.View.CapacityPage;
            var historicPage = ((SheetEditor)historic.Editor).HorizontalForChecks;
            var before = Controls(historic.Root); var after = Controls(host.Root);
            var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
            var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
            Log.Add($"   sola lettura ({module}), solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({module}), solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({module}), comuni: " + string.Join(", ", common.Select(k => k + " " + after[k])));
            Check(different.Length == 0 && common.Length >= 20, $"{module}: sola lettura, stessi controlli bloccati e liberi della vista storica ({common.Length} controlli comuni)"
                + (different.Length == 0 ? "" : ": " + string.Join("; ", different)));
            string data0 = host.Editor.Data.ToJsonString(J.Options);
            var side = new CapacitySide(host, null, services);
            side.Attach();
            foreach (var step in new HorizontalPileStep[] { new(HorizontalPileStep.Field, "lunghezza", "9"), new(HorizontalPileStep.AddSurvey), new(HorizontalPileStep.AddLayer, Index: 0),
                new(HorizontalPileStep.Layer, "spessore", "7", 0, 0), new(HorizontalPileStep.Soil, HorizontalPileStep.SoilValue, "1", 0, 0), new(HorizontalPileStep.RemoveSurvey, Index: 0) })
                await side.Apply(step);
            Check(host.Editor.Data.ToJsonString(J.Options) == data0 && host.Modified == 0, module + ": sola lettura, digitazione, strati, terreno e comandi rifiutati");
            Check(host.View.Surveys.IsEnabled && host.View.DetailsButton.IsEnabled && host.View.CsvButton.IsEnabled
                && !Visual(host.Root).OfType<Button>().Single(b => Equals(b.Content, "Invia / carica terreno…")).IsEnabled,
                module + ": sola lettura, «Diagrammi e dettagli» ed «Esporta CSV» disponibili, «Invia / carica terreno…» spento come nella vista storica");
        }
    }

    /// <summary>The state of the visible input controls, keyed by kind and name (automation name, content or binding).</summary>
    private static Dictionary<string, string> Controls(DependencyObject root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in Visual(root).OfType<Control>().Where(e => e.IsVisible))
        {
            string? key = element switch
            {
                TextBox t when t.TemplatedParent is not Control && AutomationProperties.GetName(t) is { Length: > 0 } name => "testo " + name,
                ComboBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "scelta " + name,
                CheckBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "spunta " + name,
                CheckBox c when c.Content is string text => "spunta " + text,
                Button b when b.Content is string text && text != "?" && (b.TemplatedParent is not Control || b.TemplatedParent is TabControl) => "pulsante " + text,
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

    // ---------------------------------------------------------------- 8. soil transfer (T5, O7)

    /// <summary>«Invia / carica terreno…» of the pile and of the micropile: the shared dialog with the soil sheets of the project; a copy and a replacement to the window.</summary>
    private static async Task SoilDialog(string root)
    {
        var sources = new List<SoilSource>();
        foreach (var (module, id) in new[] { (Pile, "single-soil-layer"), ("geo_palo_verticale", "default"), ("geo_muri_sostegno", "default") })
        {
            string file = HorizontalPileScenarios.FixturePath(root, module, id);
            if (File.Exists(file)) sources.Add(new SoilSource(module + " · " + id, module, HorizontalPileScenarios.FixtureData(root, module, id), "id-" + module));
        }
        foreach (var (module, id) in new[] { (Pile, "broms-coesivo-impedita"), (Micro, "stratificato-chs-catalogo") })
        {
            var services = new Services();
            using var host = new Hosted(Editors(services).Open(module, HorizontalPileScenarios.FixtureData(root, module, id)));
            await host.Settle();
            host.Editor.SoilSources = () => sources.Select(s => (s.Label, s.Module, s.Data, s.Id));
            var button = Visual(host.Root).OfType<Button>().Single(b => Equals(b.Content, "Invia / carica terreno…"));
            SoilTransferDialogViewModel? shown = null;
            services.SoilTransfer = dialog => { shown = dialog; dialog.SelectedDestination = "geo_stratigrafia"; dialog.SendCommand.Execute(null); return dialog.Result; };
            Press(button);
            await host.Settle();
            Check(shown is not null && shown.SourceLabels.Count == sources.Count && host.Copies.Count == 1 && host.Copies[0].Module == "geo_stratigrafia"
                && host.Copies[0].Name.StartsWith("Terreno da ", StringComparison.Ordinal) && host.Modified == 0,
                module + ": «Invia / carica terreno…» apre il dialogo condiviso con le fonti del progetto e manda la copia alla finestra (" + host.Copies.FirstOrDefault().Name + ")");
            services.SoilTransfer = dialog => { dialog.ApplyCommand.Execute(null); return dialog.Result; };
            Press(button);
            await host.Settle();
            Check(host.Replacements.Count == 1 && services.Soils == 2, module + ": la sostituzione del terreno va alla finestra (SoilReplacementRequested)");
            host.Editor.InspectRevision();
            await host.Settle();
            Check(!button.IsEnabled, module + ": «Invia / carica terreno…» spento in sola lettura, come la vista storica");
        }
    }

    // ---------------------------------------------------------------- 9. bench and Release

    private static void Bench(string root)
    {
        var editors = Editors(new Services());
        foreach (string module in new[] { Pile, Micro })
        {
            using (var mvvm = editors.Open(module, HorizontalPileScenarios.FixtureData(root, module, "single-soil-layer")))
                Check(mvvm is MvvmSheetEditor { Sheet: HorizontalPileSheetViewModel sheet } && sheet.Forms.All(f => f.Form.WikiNavigator is not null),
                    module + ": con --module-impl il banco apre la vista MVVM, ogni form con il navigatore della Wiki");
            using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(module, HorizontalPileScenarios.FixtureData(root, module, "single-soil-layer")))
                Check(release is SheetEditor && ModuleImplementations.Release.For(module) == SheetImplementation.Historic, module + ": la Release resta storica fino alla prova dell'utente");
        }
    }
}

internal sealed partial class HorizontalWorkspace
{
    /// <summary>UiTests (F5.7 view checks): the form of the section of the historic view.</summary>
    internal InputForm SectionFormForChecks => sectionFields;
}

