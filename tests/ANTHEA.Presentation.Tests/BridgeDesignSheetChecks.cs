using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.BridgeDesign;
using Anthea.Presentation.Services;
using Anthea.Reports;
using Anthea.Testing.Comparison;
using Anthea.Testing.Normalization;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of Bridge Design (F5.12) against its historic view, without WPF. This is the evidence of equivalence that survives the
/// archiving of the historic view:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/BridgeDesignTrace.cs (the path of the F3 capture for each fixture of the module, the inputs of every
/// tab, the results and the comparison with the alternative A, the alternatives, undo, the random bridge, the prices and the price editor, the
/// search of the optimum, read-only, the background calculation with a held engine) gives the trace of the historic view frozen in
/// tests/fixtures/f5-bridge-design/storica, line by line;</item>
/// <item>for each F3 fixture of the module the data after opening, visit, calculation, export and reopening, the result, the status and the
/// text and structure of the report are byte-identical to the F3 reference of the historic view, with the F4 transitions of the two old
/// fixtures admitted exactly as ui/f3-equivalence admits them;</item>
/// <item>module and keys of interface state; the background calculation with barriers (thread, busy until the engine returns, only the
/// current revision published, closing during a calculation and during the delay); read-only from the context; Reload; the exports.</item>
/// </list>
/// One "UI" thread (SingleThreadContext), as on the dispatcher.
/// </summary>
internal static class BridgeDesignSheetChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    private static readonly BridgeDesignSheetModule Module = new(new PlanFigures());

    private static BridgeDesignSheetViewModel Open(JsonObject data, IModuleService? service = null, bool readOnly = false, PresentationServices? services = null) =>
        (BridgeDesignSheetViewModel)Module.CreateSheet(new SheetContext((JsonObject)data.DeepClone(), readOnly, new Host(), services ?? Scripted.Services(),
            service ?? ModuleServices.Get(BridgeDesignScenarios.Module), new CalculationLimiter(CalculationLimiter.DefaultMaximum)));

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "bridge-design");
            Directory.CreateDirectory(traces);
            foreach (var scenario in BridgeDesignScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                BridgeDesignTrace.Write(Path.Combine(traces, scenario.Name + ".txt"), lines);
                string stored = Path.Combine(BridgeDesignScenarios.Reference(root), scenario.Name + ".txt");
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                string? difference = BridgeDesignTrace.FirstDifference(BridgeDesignTrace.Read(stored), lines);
                expect.That(difference is null, "Bridge Design MVVM diverso dalla vista storica, scenario " + scenario.Name + ", " + difference);
            }
            var fixtures = BridgeDesignScenarios.Fixtures(root);
            expect.That(fixtures.Count == 10, "Fixture F3 di Bridge Design: " + fixtures.Count);
            var transitions = Transition.Load(Path.Combine(root, "tests", "fixtures", "f3", "transitions-f4.json"))
                .Where(t => t.File.StartsWith("sheets/" + BridgeDesignScenarios.Module + "/", StringComparison.Ordinal)).ToList();
            foreach (string id in fixtures) await F3Async(root, traces, id, transitions.Select(t => t.File).ToHashSet(StringComparer.Ordinal), expect);
            CompareTransitions(root, traces, transitions, expect);
            Contract(expect);
            await StateKeysAsync(root, expect);
            await BackgroundAsync(root, expect);
            await ReadOnlyAsync(root, expect);
            await ReloadAsync(root, expect);
            await ExportsAsync(root, output, expect);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, BridgeDesignScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(BridgeDesignScenarios.Module);
        var side = new Side(BridgeDesignScenarios.FixtureData(root, scenario.Fixture), engine);
        try
        {
            var previous = side.State();
            lines.AddRange(BridgeDesignTrace.Lines("apertura", null, previous));
            foreach (var step in scenario.Steps)
            {
                await side.ApplyAsync(step);
                var state = side.State();
                lines.AddRange(BridgeDesignTrace.Lines(step.ToString(), previous, state));
                previous = state;
            }
        }
        // A step the ViewModel cannot apply ends the trace: the comparison then shows the first different line before it.
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
        { lines.Add("errore " + error.GetType().Name + ": " + error.Message + " " + error.StackTrace?.Split('\n').FirstOrDefault()?.Trim()); }
        finally { engine.Release(); side.Dispose(); }
        return lines;
    }

    /// <summary>The price editor shown by the dialog service: the script of the step, then «Applica prezzario» or «Annulla».</summary>
    private sealed class PriceDialogs : IDialogService
    {
        internal string? Script { get; set; }
        internal string? Described { get; set; }

        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            if (dialog is not PriceCatalogueViewModel editor || Script is null) throw new InvalidOperationException("Dialogo non previsto: " + dialog.Title);
            if (BridgeDesignPriceScripts.Run(Script, editor) && editor.ApplyCommand.CanExecute(null)) editor.ApplyCommand.Execute(null);
            else editor.CancelCommand.Execute(null);
            Described = BridgeDesignPriceScripts.Describe(editor);
            return Task.FromResult(dialog.Result);
        }
    }

    /// <summary>The ViewModel driven as its view would drive it, step by step: a control the user cannot use does nothing.</summary>
    private sealed class Side : IDisposable
    {
        private readonly HeldModuleService engine;
        private PriceDialogs prices = new();
        private int modified;
        private string message = "";
        private Task search = Task.CompletedTask;

        internal Side(JsonObject data, HeldModuleService engine) { this.engine = engine; Attach(data); }

        internal BridgeDesignSheetViewModel Sheet { get; private set; } = null!;

        private void Attach(JsonObject data)
        {
            prices = new PriceDialogs();
            Sheet = Open(data, engine, services: new PresentationServices(new ScriptedMessages(), new ScriptedConfirmations(), new ScriptedFileDialogs(), prices,
                new RecordingClipboard(), new RecordingLinks(), new RecordingWiki()));
            modified = 0;
            Sheet.Modified += (_, _) => modified++;
        }

        public void Dispose() => Sheet.Dispose();

        /// <summary>The items the user sees on the page and in the tabs shown.</summary>
        private IEnumerable<BridgeDesignItem> Visible()
        {
            var s = Sheet;
            if (s.Page != 0) return [];
            IEnumerable<BridgeDesignItem> output = s.OutputTab switch
            {
                0 => s.Quantities ?? [], 1 => s.Details ?? [], 2 => s.Comparison ?? [], 3 => s.Prices, 4 => s.Assumptions, _ => s.Technical ?? []
            };
            return s.Inputs[s.InputTab].Concat(output).Concat(s.Advice ?? []);
        }

        private BridgeDesignButton? Button(string caption) => Visible().OfType<BridgeDesignButton>().FirstOrDefault(b => b.Caption == caption);

        private static void Run(System.Windows.Input.ICommand command) { if (command.CanExecute(null)) command.Execute(null); }

        internal async Task ApplyAsync(BridgeDesignStep step)
        {
            var s = Sheet;
            message = "";
            var (page, input, output) = BridgeDesignScenarios.PlaceOf(step);
            if (page is int p) s.Page = p;
            if (input is int i) s.InputTab = i;
            if (output is int o) s.OutputTab = o;
            switch (step.Kind)
            {
                case BridgeDesignStep.Commit: s.Commit(); break;
                case BridgeDesignStep.Calculate: await Expect.Within(s.CalculateAsync()); break;
                case BridgeDesignStep.Start: _ = s.CalculateAsync(); break;
                case BridgeDesignStep.Wait: await Expect.Within(s.WaitForCalculationAsync()); break;
                case BridgeDesignStep.ReadOnly: s.IsReadOnly = true; break;
                case BridgeDesignStep.Reopen:
                    var data = s.Data; s.Dispose(); Attach((JsonObject)data.DeepClone()); break;
                case BridgeDesignStep.Hold: engine.Hold(); break;
                case BridgeDesignStep.Entered: await engine.EnteredAsync(); break;
                case BridgeDesignStep.Release: engine.Release(); break;
                case BridgeDesignStep.Visit: break;
                case BridgeDesignStep.WaitSearch: await Expect.Within(search); break;
                case BridgeDesignStep.Report:
                    try { s.BuildReportDocument("Traccia F5.12"); message = "report disponibile"; }
                    catch (Exception error) when (error is InvalidOperationException or ArgumentException) { message = "report non disponibile: " + error.Message; }
                    break;
                case BridgeDesignStep.Print:
                    message = s.PrintLines is { } lines ? "stampa " + string.Join(" | ", lines.Select(BridgeDesignTrace.Quote)) : "stampa non disponibile";
                    break;
                case BridgeDesignStep.Page: s.Page = step.Index; break;
                case BridgeDesignStep.InputTab: s.InputTab = step.Index; break;
                case BridgeDesignStep.OutputTab: s.OutputTab = step.Index; break;
                case BridgeDesignStep.Field:
                    if (s.Parameters.TryGetValue(step.Key!, out var field) && Visible().Contains(field)) field.Text = step.Text!;
                    break;
                case BridgeDesignStep.Slider:
                    if (s.Parameters.TryGetValue(step.Key!, out var slider) && slider.HasSlider && Visible().Contains(slider))
                        slider.SliderValue = double.Parse(step.Text!, CultureInfo.InvariantCulture);
                    break;
                case BridgeDesignStep.Choice:
                    if (Visible().OfType<BridgeDesignChoice>().FirstOrDefault(c => c.Key == step.Key) is { } choice) choice.Selected = step.Text;
                    break;
                case BridgeDesignStep.Option:
                    if (Visible().OfType<BridgeDesignOption>().FirstOrDefault(c => c.Key == step.Key) is { } option) option.IsChecked = !option.IsChecked;
                    break;
                case BridgeDesignStep.Family:
                    if (Visible().OfType<BridgeDesignFamilies>().FirstOrDefault() is { } families && families.SelectCommand.CanExecute(step.Text)) families.SelectCommand.Execute(step.Text);
                    break;
                case BridgeDesignStep.Button when step.Key == "ricerca":
                    if (s.Page != 1) break;
                    switch (step.Text)
                    {
                        case "Suggerisci campate dalla lunghezza": Run(s.SuggestSpansCommand); break;
                        case "Avvia ottimizzazione": if (s.CanRunOptimization) search = s.RunOptimizationAsync(); break;
                        case "Interrompi": Run(s.StopOptimizationCommand); break;
                        case "Prospetto": Run(s.SolutionElevationCommand); break;
                        case "Sezione": Run(s.SolutionSectionCommand); break;
                        case "Applica soluzione selezionata": Run(s.ApplySolutionCommand); break;
                        case "Aggiorna elenco": Run(s.RefreshRankingCommand); break;
                        default: throw new ArgumentException("Pulsante sconosciuto: " + step.Text);
                    }
                    break;
                case BridgeDesignStep.Button:
                    if (s.Page != 0) break;
                    switch (step.Text)
                    {
                        case "Prospetto": Run(s.ElevationCommand); break;
                        case "Sezione": Run(s.SectionCommand); break;
                        case "Nuovo paesaggio": Run(s.NewSceneCommand); break;
                        case "Ottimizza": Run(s.OptimizeCommand); break;
                        case "Fissa A": Run(s.PinCommand); break;
                        case "Annulla": Run(s.UndoCommand); break;
                        case "Sezioni e quote": Run(s.DimensionsCommand); break;
                        default: if (Button(step.Text!) is { } button) Run(button.Command); break;
                    }
                    break;
                case BridgeDesignStep.Random: if (s.RandomCommand.CanExecute(null)) s.Randomize(new Random(step.Index)); break;
                case BridgeDesignStep.Alternative:
                {
                    var buttons = (s.Advice ?? []).OfType<BridgeDesignButton>().Where(b => b.Caption != BridgeDesignPresentation.RestoreEnvironmentButton).ToArray();
                    if (step.Index < buttons.Length) Run(buttons[step.Index].Command);
                    break;
                }
                case BridgeDesignStep.Prices:
                {
                    if (Button(BridgeDesignPresentation.ManagePricesButton) is not { } open || !open.Command.CanExecute(null)) break;
                    prices.Script = step.Text;
                    string before = s.Status;
                    await s.ManagePricesAsync();
                    message = prices.Described ?? "prezzario non aperto · stato " + BridgeDesignTrace.Quote(s.Status == before ? "" : s.Status);
                    prices.Script = null; prices.Described = null;
                    break;
                }
                case BridgeDesignStep.Objective: s.Objective = step.Index; break;
                case BridgeDesignStep.Lock: s.SetLock(step.Key!, !s.Locks[step.Key!]); break;
                case BridgeDesignStep.Limit:
                    if (!s.CanEdit || !s.IsLimitEnabled(step.Key!)) break;
                    s.SetLimit(step.Key!, step.Text!); s.LeaveLimit(step.Key!);
                    break;
                case BridgeDesignStep.Rank:
                    if (s.SearchResults?.OfType<BridgeDesignTable>().FirstOrDefault() is { } ranking && step.Index < ranking.Rows.Length) ranking.SelectedIndex = step.Index;
                    break;
                case BridgeDesignStep.Point:
                {
                    var drawn = s.Cloud.Solutions.FirstOrDefault(c => c.Rank == step.Index && (s.FamilyFilter is null || c.Trial.Family == s.FamilyFilter) && (!s.ParetoOnly || c.Pareto));
                    if (drawn is not null) s.SelectSolution(drawn.Rank - 1);
                    break;
                }
                case BridgeDesignStep.Variable: s.Variable = step.Index; break;
                case BridgeDesignStep.Filter: s.FamilyFilter = step.Index <= 0 ? null : BridgeDesignModel.Families[step.Index - 1].Id; break;
                case BridgeDesignStep.Pareto: s.ParetoOnly = !s.ParetoOnly; break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        internal BridgeDesignTraceState State()
        {
            var s = Sheet;
            var t = new BridgeDesignTraceState();
            t.Data(s.Data);
            t.Add("modificato", BridgeDesignTrace.Number(modified));
            t.Add("messaggio", message);
            var a = s.Analysis;
            t.Add("calcolo", "occupato " + BridgeDesignTrace.YesNo(s.IsBusy) + " · risultato " + (s.Calculation is { } result ? BridgeDesignTrace.Hash(result.Json()) : "-")
                + " · confronto " + (a is null ? "-" : a.Comparison is { } comparison ? BridgeDesignTrace.Hash(comparison.Json()) : a.ComparisonError is { } error ? "errore " + BridgeDesignTrace.Quote(error) : "nessuno")
                + " · alternative " + (a is null ? "-" : string.Join("|", a.Alternatives.Select(x => x.Label))));
            t.Add("stato", BridgeDesignTrace.Quote(s.Status) + (s.StatusIsError ? " · errore" : ""));
            for (int k = 0; k < s.Metrics.Count; k++) t.Add("metrica." + k, BridgeDesignTrace.Quote(s.Metrics[k].Value) + " · " + BridgeDesignTrace.Quote(s.Metrics[k].Caption));
            t.Add("configurazione", BridgeDesignTrace.Quote(s.Configuration));
            t.Add("pagina", BridgeDesignTrace.Number(s.Page) + " · ingressi " + BridgeDesignTrace.Number(s.InputTab) + " · risultati " + BridgeDesignTrace.Number(s.OutputTab));
            t.Add("annulla", BridgeDesignTrace.Number(s.UndoCount));
            t.Add("disegno", "sezione " + BridgeDesignTrace.YesNo(s.SectionView) + " · dati " + (s.DrawingData is null ? "-" : ReferenceEquals(s.DrawingData, s.Data) ? "foglio" : BridgeDesignTrace.Hash(s.DrawingData))
                + " · risultato " + (s.DrawingResult is { } drawn ? BridgeDesignTrace.Hash(drawn.Json()) : "-"));
            t.Add("ingressi", BridgeDesignTrace.Quote(string.Join("|", BridgeDesignPresentation.InputTabs)));
            for (int i = 0; i < s.Inputs.Count; i++) t.Panel("ingressi." + i, Blocks(s.Inputs[i]));
            t.Add("risultati", BridgeDesignTrace.Quote(string.Join("|", BridgeDesignPresentation.OutputTabs)));
            t.Panel("pannello.quantita", Blocks(s.Quantities));
            t.Panel("pannello.dettagli", Blocks(s.Details));
            t.Panel("pannello.confronto", Blocks(s.Comparison));
            t.Panel("pannello.prezzario", Blocks(s.Prices));
            t.Panel("pannello.ipotesi", Blocks(s.Assumptions));
            t.Panel("pannello.quote", Blocks(s.Technical));
            t.Panel("pannello.consigli", Blocks(s.Advice));
            t.Add("ricerca", "obiettivo " + BridgeDesignTrace.Number(s.Objective) + " · blocchi "
                + string.Join(" ", BridgeDesignScenarios.Locks.Select(k => k + "=" + BridgeDesignTrace.YesNo(s.Locks[k])))
                + " · limiti " + string.Join(" ", BridgeDesignScenarios.Limits.Select(k => k + "=" + BridgeDesignTrace.Quote(s.Limits[k]) + (s.IsLimitEnabled(k) ? "" : "(spento)"))));
            t.Add("ricerca.stato", BridgeDesignTrace.Quote(s.SearchStatus));
            t.Add("ricerca.migliore", BridgeDesignTrace.Quote(s.SearchBest));
            t.Add("ricerca.selezione", BridgeDesignTrace.Quote(s.SearchSelection) + " · rango " + BridgeDesignTrace.Number(s.SelectedRank));
            t.Add("ricerca.avanzamento", BridgeDesignTrace.Number(s.Progress) + "/" + BridgeDesignTrace.Number(s.ProgressMaximum));
            var (run, apply) = s.SearchButtons;
            t.Add("ricerca.comandi", s.IsReadOnly ? "-" : "avvia " + BridgeDesignTrace.YesNo(run) + " · applica " + BridgeDesignTrace.YesNo(apply));
            t.Add("ricerca.risultato", s.Optimization is not { } found ? "-" : "valutati " + BridgeDesignTrace.Number(found.Evaluated) + " · ammessi " + BridgeDesignTrace.Number(found.Admissible)
                + " · soluzioni " + BridgeDesignTrace.Number(found.Solutions.Length) + " · tentativi " + BridgeDesignTrace.Number(found.Trials.Length)
                + " · riferimento " + BridgeDesignTrace.Hash(found.Baseline.Json()));
            t.Add("ricerca.fonte", s.OptimizationSource is null ? "-" : BridgeDesignTrace.Hash(s.OptimizationSource));
            t.Panel("pannello.ricerca", Blocks(s.SearchResults));
            t.Panel("pannello.variazioni", Blocks(s.SearchChanges));
            t.Add("ricerca.anteprima", "sezione " + BridgeDesignTrace.YesNo(s.SolutionSection) + " · dati " + (s.SolutionData is null ? "-" : BridgeDesignTrace.Hash(s.SolutionData))
                + " · risultato " + (s.SolutionResult is { } preview ? BridgeDesignTrace.Hash(preview.Json()) : "-"));
            t.Add("ricerca.nuvola", "tentativi " + BridgeDesignTrace.Number(s.Cloud.Trials.Count) + " · soluzioni " + BridgeDesignTrace.Number(s.Cloud.Solutions.Count)
                + " · riferimento " + (s.Cloud.Baseline is { } cloudBaseline ? BridgeDesignTrace.Hash(cloudBaseline.Json()) : "-") + " · scelto " + BridgeDesignTrace.Number(s.CloudRank)
                + " · tipologia " + (s.FamilyFilter ?? "-") + " · pareto " + BridgeDesignTrace.YesNo(s.ParetoOnly));
            t.Add("ricerca.traccia", "tentativi " + BridgeDesignTrace.Number(s.Trace.Trials.Count) + " · soluzioni " + BridgeDesignTrace.Number(s.Trace.Solutions.Count)
                + " · riferimento " + (s.Trace.Baseline is { } traceBaseline ? BridgeDesignTrace.Hash(traceBaseline.Json()) : "-") + " · variabile " + BridgeDesignTrace.Number(s.Variable));
            return t;
        }
    }

    /// <summary>The items of a panel as the blocks of the trace (null: no panel), shared with the checks of the XAML view.</summary>
    internal static List<BridgeDesignBlock>? Blocks(IReadOnlyList<BridgeDesignItem>? items) => BridgeDesignItemBlocks.Blocks(items);

    // ---------------------------------------------------------------- F3

    private static async Task F3Async(string root, string output, string id, IReadOnlySet<string> withTransitions, Expect expect)
    {
        string folder = Path.Combine(root, "tests", "fixtures", "f3", "expected", "sheets", BridgeDesignScenarios.Module, id);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        var guids = new GuidMap();
        var data = BridgeDesignScenarios.FixtureData(root, id);
        CanonicalJson.Register(data, guids);
        string Canonical(JsonNode? node) { CanonicalJson.Register(node, guids); return CanonicalJson.Serialize(node, guids, normalizer).Replace("\r\n", "\n"); }
        string Expected(string name) => File.ReadAllText(Path.Combine(folder, name), Encoding.UTF8).Replace("\r\n", "\n");
        void Same(string name, string actual)
        {
            string file = "sheets/" + BridgeDesignScenarios.Module + "/" + id + "/" + name;
            if (withTransitions.Contains(file))
            {
                foreach (var (side, text) in new[] { ("f3-riferimento", Expected(name)), ("f3-transizioni", actual) })
                {
                    string path = Path.Combine(output, side, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                }
                return;
            }
            if (actual != Expected(name)) File.WriteAllText(Path.Combine(output, "f3-" + id + "-" + name), actual, new UTF8Encoding(false));
            expect.That(actual == Expected(name), id + ": " + name + " diverso dal riferimento F3 della vista storica");
        }
        using var sheet = Open(data);
        sheet.Commit(); Same("opened.json", Canonical(sheet.Data));
        // The visit of the capture opens every tab of the view: nothing of it is saved in the data.
        for (int p = 1; p >= 0; p--) sheet.Page = p;
        for (int i = 4; i >= 0; i--) sheet.InputTab = i;
        for (int o = 5; o >= 0; o--) sheet.OutputTab = o;
        sheet.Commit(); Same("visited.json", Canonical(sheet.Data));
        await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        Same("calculated-data.json", Canonical(sheet.Data)); Same("result.json", Canonical(sheet.Result));
        var status = new JsonObject { ["hasResults"] = sheet.HasResults };
        try
        {
            byte[] report = sheet.Report.BuildReport("Fixture F3 · " + BridgeDesignScenarios.Module + " · " + id, new HashSet<string>());
            Same("report.txt", DocxText.Extract(report, normalizer, guids).Replace("\r\n", "\n"));
            Same("report-structure.json", Canonical(Structure(report)));
            status["report"] = "available";
        }
        catch (Exception error) when (error is not IOException && error is not UnauthorizedAccessException)
        { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
        Same("exported-data.json", Canonical(sheet.Data)); Same("status.json", Canonical(status));
        using var reopened = Open(sheet.Data);
        reopened.Commit(); Same("reopened.json", Canonical(reopened.Data));
    }

    /// <summary>The files with F4 transitions compared as ui/f3-equivalence compares them, with exactly those transitions admitted.</summary>
    private static void CompareTransitions(string root, string output, IReadOnlyList<Transition> transitions, Expect expect)
    {
        var comparer = new BaselineComparer(Tolerances.Load(Path.Combine(root, "tests", "ANTHEA.Testing", "tolerances.json")));
        comparer.Admit(transitions);
        var report = comparer.Compare(Path.Combine(output, "f3-riferimento"), Path.Combine(output, "f3-transizioni"));
        File.WriteAllText(Path.Combine(output, "f3-confronto.json"), report.ToJsonString(J.Options));
        expect.That(transitions.Count > 0 && comparer.NotAdmitted == 0, "File con transizioni F4: " + transitions.Count + " transizioni, "
            + comparer.NotAdmitted + " differenze non ammesse o transizioni assenti");
    }

    /// <summary>Tables, rows, cells, images and styles of the report, as F3EquivalenceCapture.CaptureReport counts them.</summary>
    private static JsonObject Structure(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var document = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        return new JsonObject
        {
            ["tables"] = document.Descendants(w + "tbl").Count(),
            ["rows"] = document.Descendants(w + "tr").Count(),
            ["cells"] = document.Descendants(w + "tc").Count(),
            ["images"] = new JsonArray(document.Descendants(wp + "extent").Select(e => (JsonNode)new JsonObject {
                ["cx"] = (string?)e.Attribute("cx"), ["cy"] = (string?)e.Attribute("cy") }).ToArray()),
            ["styles"] = new JsonArray(document.Descendants(w + "pStyle").Select(e => JsonValue.Create((string?)e.Attribute(w + "val"))).ToArray())
        };
    }

    // ---------------------------------------------------------------- contract

    private static void Contract(Expect expect)
    {
        expect.That(Module.Info == ModuleDirectory.Get(BridgeDesignScenarios.Module) && Module.Reports == new ModuleReportCapabilities(false)
            && Module.Help.GuideId == ModuleHelp.For(BridgeDesignScenarios.Module).GuideId, "Modulo di Bridge Design");
        expect.That(Module.Persistence.UiStateKeys.SequenceEqual(["scene"]) && BridgeDesignSheetViewModel.UiStateKeys.SequenceEqual(["scene"]), "Chiave di stato dell'interfaccia: scene");
        var registry = new ModuleRegistry([Module]);
        expect.That(registry.SheetModuleIds.SequenceEqual([BridgeDesignScenarios.Module]) && ReferenceEquals(registry.SheetModule(BridgeDesignScenarios.Module), Module),
            "Registro con Bridge Design");
        expect.Throws<ArgumentNullException>(() => new BridgeDesignSheetModule(null!), "Modulo senza disegni");
    }

    /// <summary>Pages, tabs, views of the drawing and of the search, selections write nothing into the data and keep the result.</summary>
    private static async Task StateKeysAsync(string root, Expect expect)
    {
        using var sheet = Open(BridgeDesignScenarios.FixtureData(root, "default"));
        await Expect.Within(sheet.WaitForCalculationAsync());
        sheet.Page = 1;
        await Expect.Within(sheet.RunOptimizationAsync(new BridgeDesignModel.OptimizationOptions { KeepPier = true, KeepFoundation = true, MinSpans = 3, MaxSpans = 6,
            DepthPercentMin = 100, DepthPercentMax = 115, DepthPercentStep = 15, PilePercentMin = 100, PilePercentMax = 150, PilePercentStep = 25 }));
        string before = sheet.Data.ToJsonString();
        var result = sheet.Calculation;
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        for (int i = 0; i < 5; i++) sheet.InputTab = i;
        for (int o = 0; o < 6; o++) sheet.OutputTab = o;
        sheet.SectionView = true; sheet.SolutionSection = true; sheet.Variable = 0; sheet.FamilyFilter = "psc_i"; sheet.ParetoOnly = true; sheet.ParetoOnly = false;
        sheet.SelectSolution(1); sheet.SelectSolution(0); sheet.RefreshRanking(); sheet.SuggestSpans(); sheet.Page = 0;
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && ReferenceEquals(result, sheet.Calculation), "Viste e selezioni scritte nei dati o risultato perso");
        sheet.NewScene();
        expect.That(modified == 1 && sheet.Data.D("scene") == 2 && sheet.Calculation is null, "«Nuovo paesaggio»: modifica del documento con ricalcolo");
    }

    // ---------------------------------------------------------------- background calculation

    /// <summary>
    /// The calculation on a background thread through the service of the context: busy while the engine runs, also for a request overtaken
    /// by an edit; only the current revision is published; closing during a calculation and during the delay publishes nothing.
    /// </summary>
    private static async Task BackgroundAsync(string root, Expect expect)
    {
        var engine = new HeldModuleService(BridgeDesignScenarios.Module);
        var counting = new ThreadService(engine);
        var sheet = Open(BridgeDesignScenarios.FixtureData(root, "default"), counting);
        int ui = Environment.CurrentManagedThreadId;
        expect.That(sheet.CalculationMode == SheetCalculationMode.Background && sheet.AutomaticDelay == BridgeDesignPresentation.RecalculationDelay
            && BridgeDesignPresentation.RecalculationDelay == TimeSpan.FromMilliseconds(450), "Calcolo sullo sfondo con il ritardo di 450 ms");
        expect.That(sheet.IsBusy && sheet.Status == BridgeDesignPresentation.StatusRunning, "Calcolo dell'apertura avviato subito");
        await Expect.Within(sheet.WaitForCalculationAsync());
        expect.That(sheet.Calculation is not null && counting.Threads.All(t => t != ui) && !sheet.IsBusy, "Calcolo all'apertura fuori dal thread dell'interfaccia");
        // An edit while the engine is held: busy until the engine returns, the overtaken result never published.
        engine.Hold();
        sheet.Parameters["input/length"].Text = "120";
        expect.That(sheet.Calculation is null && !sheet.IsBusy && sheet.Status == BridgeDesignPresentation.StatusPending, "Modifica: risultati cancellati, ricalcolo in attesa");
        await Expect.Within(engine.EnteredAsync());
        expect.That(sheet.IsBusy, "Occupato mentre il motore lavora");
        sheet.Parameters["input/length"].Text = "125";
        engine.Release();
        await Expect.Within(sheet.WaitForCalculationAsync());
        expect.That(sheet.Calculation?.Length == 125 && !sheet.IsBusy, "Pubblicata solo la revisione corrente");
        // Closing during a calculation and during the delay.
        engine.Hold();
        sheet.Parameters["input/length"].Text = "130";
        await Expect.Within(engine.EnteredAsync());
        var pending = sheet.PendingWork;
        sheet.Dispose(); engine.Release();
        await Expect.Within(pending);
        expect.That(sheet.Calculation is null, "Risultato tardivo dopo la chiusura durante il calcolo");
        var delayed = Open(BridgeDesignScenarios.FixtureData(root, "default"));
        await Expect.Within(delayed.WaitForCalculationAsync());
        delayed.Parameters["input/length"].Text = "135";
        var waiting = delayed.PendingWork;
        delayed.Dispose();
        await Expect.Within(waiting);
        expect.That(delayed.Calculation is null, "Risultato tardivo dopo la chiusura durante il ritardo");
    }

    // ---------------------------------------------------------------- read-only and reload

    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        // F5.15, BD4 (F5-59): the price list opens also in a read-only revision, without «Applica prezzario».
        bool pricesReadOnly = false;
        var dialogs = new ScriptedDialogs().Then(d =>
        {
            var prices = (PriceCatalogueViewModel)d;
            pricesReadOnly = prices.IsReadOnly && !prices.ApplyCommand.CanExecute(null) && prices.Status.StartsWith(PriceCatalogueViewModel.ReadOnlyStatus, StringComparison.Ordinal);
            prices.ApplyCommand.Execute(null);
        });
        using var sheet = Open(BridgeDesignScenarios.FixtureData(root, "confronto-alternativa-a"), readOnly: true,
            services: Scripted.Services(dialogs: dialogs));
        await Expect.Within(sheet.WaitForCalculationAsync());
        string before = sheet.Data.ToJsonString();
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        sheet.Parameters["input/length"].Text = "150"; sheet.Parameters["input/height"].SliderValue = 30;
        foreach (var choice in sheet.Inputs[0].OfType<BridgeDesignChoice>()) choice.Selected = choice.Choices[^1];
        foreach (var option in sheet.Inputs[1].OfType<BridgeDesignOption>()) option.IsChecked = !option.IsChecked;
        sheet.SelectFamily("slab"); sheet.AutoSize(); sheet.ApplyRecommendedTraffic(); sheet.ApplyIndicativeRates(); sheet.NewScene(); sheet.Randomize(new Random(1));
        sheet.Pin(); sheet.Undo(); sheet.RestoreA(); sheet.RemoveComparison(); sheet.RestoreEnvironment();
        await sheet.ManagePricesAsync();
        sheet.Objective = 1; sheet.SetLock("family", true); sheet.SetLimit("campate-min", "2"); sheet.Variable = 3;
        sheet.SuggestSpans();
        await sheet.RunOptimizationAsync();
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && sheet.Calculation is not null && sheet.Objective == 0 && !sheet.Locks["family"]
            && sheet.Optimization is null, "Modifiche accettate in sola lettura");
        expect.That(pricesReadOnly && dialogs.Shown.Count == 1, "Sola lettura: prezzario consultabile senza «Applica prezzario» (BD4)");
        expect.That(!sheet.NewSceneCommand.CanExecute(null) && sheet.ManagePricesCommand.CanExecute(null) && sheet.ElevationCommand.CanExecute(null)
            && sheet.ExportCsvCommand.CanExecute(null) && !sheet.SuggestSpansCommand.CanExecute(null), "Comandi della sola lettura (BD4, BD10 punto 5)");
        await Expect.Within(sheet.CalculateAsync());
        expect.That(sheet.Calculation is not null && sheet.BuildReportDocument("t") is not null && sheet.PrintLines is not null, "Calcolo e report in sola lettura");
        sheet.IsReadOnly = false;
        sheet.Parameters["input/length"].Text = "150";
        expect.That(modified == 1 && sheet.Data["input"]!["length"]!.GetValue<string>() == "150", "Ritorno alla modifica");
    }

    /// <summary>Reload: the same sheet as a new opening on the new data, after the calculation.</summary>
    private static async Task ReloadAsync(string root, Expect expect)
    {
        var data = BridgeDesignScenarios.FixtureData(root, "confronto-alternativa-a");
        using var sheet = Open(BridgeDesignScenarios.FixtureData(root, "default"));
        await Expect.Within(sheet.WaitForCalculationAsync());
        sheet.Page = 1; sheet.InputTab = 3; sheet.SectionView = true; sheet.Parameters["input/length"].Text = "140";
        sheet.Reload(data);
        await Expect.Within(sheet.WaitForCalculationAsync());
        using var opened = Open(data);
        await Expect.Within(opened.WaitForCalculationAsync());
        string Strip(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\d{4}-\d{2}-\d{2}T[^""]*", "<DATA>");
        expect.That(Strip(sheet.Data.ToJsonString()) == Strip(opened.Data.ToJsonString()) && sheet.Calculation is not null
            && sheet.Result!.ToJsonString() == opened.Result!.ToJsonString() && sheet.UndoCount == 0 && sheet.Page == 0 && sheet.InputTab == 0 && !sheet.SectionView
            && sheet.Configuration == opened.Configuration && sheet.Metrics.SequenceEqual(opened.Metrics), "Reload diverso da una riapertura");
    }

    // ---------------------------------------------------------------- exports

    private static async Task ExportsAsync(string root, string output, Expect expect)
    {
        string folder = Path.Combine(output, "bridge-design-esportazioni");
        Directory.CreateDirectory(folder);
        string csv = Path.Combine(folder, "quantita.csv"), technical = Path.Combine(folder, "quote.csv"), png = Path.Combine(folder, "ponte.png");
        var files = new ScriptedFileDialogs().Then(csv).Then(technical).Then(png).Then(null);
        using var sheet = Open(BridgeDesignScenarios.FixtureData(root, "cassone-acciaio-prezzario"), services: Scripted.Services(files: files));
        sheet.ExportCsv();
        expect.That(files.Requests.Count == 0, "Esportazione senza risultato");
        await Expect.Within(sheet.WaitForCalculationAsync());
        var r = sheet.Calculation!;
        sheet.ExportCsv(); sheet.ExportTechnicalCsv(); sheet.SectionView = true; sheet.ExportPng(); sheet.ExportCsv();
        byte[] Bom(string text) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
        expect.That(File.ReadAllBytes(csv).SequenceEqual(Bom(BridgeDesignModel.Csv(r))) && File.ReadAllBytes(technical).SequenceEqual(Bom(BridgeDesignModel.TechnicalCsv(sheet.Data, r)))
            && File.ReadAllText(png) == "sezione " + r.Family.Id && files.Requests.Count == 4, "Esportazioni CSV e PNG");
        var report = DocxWriter.Write(sheet.BuildReportDocument("Report"));
        var expected = DocxWriter.Write(BridgeDesignModel.ReportDocument("Report", sheet.Data, r, Encoding.UTF8.GetBytes("prospetto " + r.Family.Id), Encoding.UTF8.GetBytes("sezione " + r.Family.Id)));
        expect.That(DocxText.Extract(report, new TextNormalizer([]), new GuidMap()) == DocxText.Extract(expected, new TextNormalizer([]), new GuidMap()), "Report di Bridge Design");
    }

    /// <summary>The figures of the report as plain texts: the checks without WPF compare what the sheet asks of the renderer.</summary>
    private sealed class PlanFigures : IBridgeDesignFigureRenderer
    {
        public byte[] Drawing(JsonObject data, BridgeDesignModel.Result result, bool section) => Encoding.UTF8.GetBytes((section ? "sezione " : "prospetto ") + result.Family.Id);
    }

    private sealed class ThreadService(IModuleService inner) : IModuleService
    {
        internal System.Collections.Concurrent.ConcurrentQueue<int> Threads { get; } = new();
        public string ModuleId => inner.ModuleId;
        public JsonObject Prepare(JsonObject source) => inner.Prepare(source);
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Threads.Enqueue(Environment.CurrentManagedThreadId);
            return inner.Calculate(source, cancellation);
        }
    }

    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException("Copia non prevista: " + module);
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() { }
    }
}
