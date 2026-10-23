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
            // f5-pass-orizzontale: the historic view is archived (supporto/SUPERATI/ui-storica/palo-orizzontale); the comparisons with it are done and
            // ui/f3-equivalence compares the exports with the frozen F3 reference. «Calcola» of the window after the automatic calculations of the two
            // tabs, then the exports of the result shown: JSON, Word with the PDF beside it, «Esporta CSV» with the function of the historic button.
            await Task.WhenAll(mvvm.Sheet.PendingCalculation, mvvm.Sheet.Elastic.PendingCalculation).WaitAsync(TimeSpan.FromSeconds(120));
            mvvm.Editor.Commit(); await mvvm.Editor.CalculateAsync(); mvvm.Editor.Commit();
            await mvvm.Settle();
            if (!mvvm.Editor.HasResults)
            {
                Check(Fails(() => mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json")), out string message) && message == Anthea.Presentation.Shell.Commands.SheetFileExports.CalculateFirst,
                    name + ": senza risultato l'esportazione è rifiutata con il messaggio della vista storica («" + message + "»)");
                continue;
            }
            string title = "Fixture F3 · " + module + " · " + id;
            mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json"));
            Check(WithoutTimes(JsonNode.Parse(File.ReadAllText(Path.Combine(folder, name + "-mvvm.json")))) == WithoutTimes(mvvm.Editor.Result),
                name + ": JSON del risultato mostrato dopo «Calcola» (tempi esclusi)");
            mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, []);
            Check(new FileInfo(Path.Combine(folder, name + "-mvvm.docx")).Length > 0 && new FileInfo(Path.Combine(folder, name + "-mvvm.pdf")).Length > 0,
                name + ": report Word e PDF affiancato scritti");
            // «Esporta CSV» of the capacity: the file chosen in the dialog, the tables of the historic button (HorizontalPileEditor.Csv).
            if (mvvm.Sheet.Result is { } capacity)
            {
                string csv = Path.Combine(folder, name + "-capacita.csv");
                services.SavePath = csv;
                Press(mvvm.View.CsvButton);
                services.SavePath = null;
                Check(File.Exists(csv) && File.ReadAllBytes(csv).SequenceEqual(Encoding.UTF8.GetBytes(HorizontalPileEditor.Csv(capacity))),
                    name + ": «Esporta CSV» scrive le tabelle del bottone storico");
            }
        }
    }

    private static bool Fails(Action action, out string message)
    {
        try { action(); message = ""; return false; }
        catch (InvalidOperationException error) { message = error.Message; return true; }
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
        await host.Settle();
        var side = new CapacitySide(host, null, new Services());
        side.Attach();
        var section = host.Editor.Data["sezione"]!.AsObject();
        var choice = (ComboBox)Visual(host.View.CapacityPage).OfType<ComboBox>().Single(c => c.DataContext is FieldViewModel { Key: "classe_acciaio" });
        // A custom value of the steel: the data say «Personalizzato» and the new view shows it (the historic view kept «B450C»).
        await side.Apply(new HorizontalPileStep(HorizontalPileStep.Field, "steel_fu_mpa", "560"));
        Check(section.S("classe_acciaio") == "Personalizzato" && (string?)choice.SelectedItem == "Personalizzato",
            "F5-02: dopo un valore dell'acciaio personalizzato la scelta «Acciaio» della vista mostra «Personalizzato», come i dati");
        // A class of the catalogue after the custom values: the note of the new view is the one of the values of the catalogue.
        await side.Apply(new HorizontalPileStep(HorizontalPileStep.Field, "steel_modulus_mpa", "210000"));
        await side.Apply(new HorizontalPileStep(HorizontalPileStep.Choice, "steel_diagramma", "Incrudente"));
        await side.Apply(new HorizontalPileStep(HorizontalPileStep.Choice, "classe_acciaio", "B450A"));
        string? expectedNote = MaterialInputCatalog.SteelDifference(section, "NTC 2018");
        var note = Visual(host.View.CapacityPage).OfType<Grid>().Single(g => g.Name == "Row" && g.DataContext is FieldViewModel { Key: "classe_acciaio" })
            .Children.OfType<Border>().Single(b => b.Name == "NoteBox");
        string shown = note.Visibility == Visibility.Visible ? ((TextBlock)note.Child).Text : "";
        Log.Add($"   F5-02 nota della classe dal catalogo: vista nuova «{shown}», attesa «{expectedNote ?? ""}»");
        Check(section.S("classe_acciaio") == "B450A" && (string?)choice.SelectedItem == "B450A" && shown == (expectedNote ?? ""),
            "F5-02: nota della classe dell'acciaio scelta dal catalogo calcolata sui valori del catalogo");
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
            await host.Settle();
            var side = new CapacitySide(host, null, services);
            side.Attach();
            static string[] Labels(DependencyObject root) => Visual(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
                .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
            string[] after = Labels(host.Root);
            Check(after.Length > 0 && after.All(l => l.Length > 0), module + ": etichette «?» visibili (" + after.Length + "): " + string.Join(", ", after));
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
            using var host = new Hosted(editors.Open(module, data));
            await host.Settle();
            var after = Names(host.Root);
            Log.Add($"   nomi di automazione ({module} {id}, {after.Count}): " + string.Join(", ", after.Order(StringComparer.Ordinal)));
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
            await host.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
            host.Editor.InspectRevision();
            await host.Settle();
            var after = Controls(host.Root);
            Log.Add($"   sola lettura ({module}): " + string.Join(", ", after.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value)));
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
                    module + ": il banco apre la vista MVVM, ogni form con il navigatore della Wiki");
            using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(module, HorizontalPileScenarios.FixtureData(root, module, "single-soil-layer")))
                Check(release is MvvmSheetEditor { Sheet: HorizontalPileSheetViewModel } && ModuleImplementations.Release.For(module) == SheetImplementation.Mvvm,
                    module + ": la Release apre la vista MVVM (passaggio del 23/10/2026, vista storica archiviata)");
        }
    }
}

