using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.BridgeDesign;
using Anthea.Presentation.Modules.CompositeSection;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.Materials;
using Anthea.Presentation.Modules.PileGroup;
using Anthea.Presentation.Modules.RetainingWall;
using Anthea.Presentation.Modules.ShallowFoundation;
using Anthea.Presentation.Modules.VerticalPile;
using Anthea.Presentation.Shell;
using Anthea.Testing.Comparison;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 (d), exit test of proposal 4.6 («Calcoli in parallelo senza stato statico»): the F3 fixtures of the bridge (Bridge Design and the
/// composite bridge section), of the walls, of the piles (vertical and horizontal, pile and micropile, the pile group), of the shallow
/// foundations and of a material (the reinforcing steel) are calculated through the sheets of the new shell (<see cref="WorkspaceSession"/>,
/// <see cref="SheetSession"/>, the existing ViewModels) and the instance limiter of the application (<see cref="CalculationLimiter"/>,
/// processors − 1):
/// <list type="bullet">
/// <item>in series: one sheet at a time, each calculation awaited before the next sheet opens;</item>
/// <item>in parallel: every fixture opened twice (the same module in two sheet sessions, of two documents) next to the fixtures of the other
/// modules, all the background calculations started together; meanwhile the UI thread calculates the modules that calculate on it (pile
/// group and foundations, decision Q8 a) and the material sheets. A gate holds the first background calculations until as many as the limiter
/// admits are running together, so that the parallel round is really parallel on every run.</item>
/// </list>
/// Every sheet of the parallel rounds must give the same bits as the serial one: state, message and warnings of the calculation, the result,
/// the data after the calculation, the elastic response of the horizontal piles that open on it, the values of the material. The only keys left
/// out are the volatile ones of tests/ANTHEA.Testing/tolerances.json (the times measured with Stopwatch), as in the F3 comparison. The peak
/// of the calculations running together never exceeds the limiter.
/// <para>
/// Since F5.13 (d2) also the RC section (<c>str_palo</c>, <see cref="ConcreteSheetViewModel"/>): its sheet calculates with a coordinator of its
/// own (decision C1, mode None of the sheet: one preparation and parallel steps of <c>ConcreteAnalysisSession</c> on worker threads, the shear
/// on the UI thread), not through the module service and the limiter. Its fixtures come first, so that in the parallel rounds all its batches
/// are running when the calculations of the other modules start; its fingerprint adds the status line, the errors of the checks and the quick
/// resistances (also on worker threads). A fixture without the ids of the rows of its actions gets them when the sheet opens: the fingerprint
/// numbers them by their first appearance (<see cref="Fingerprint"/>). The batches of the section running together and those running when a
/// calculation of the other modules enters are counted apart from the limiter: one and none in the series, at least two and at least one in
/// the parallel rounds.
/// </para>
/// </summary>
internal static class ParallelChecks
{
    /// <summary>The modules of the test with their F3 fixtures (tests/fixtures/f3/inputs), in this order.</summary>
    internal static IReadOnlyList<string> Modules { get; } = [ConcreteSectionSheetModule.Id, "str_bridge_design", "str_mista_ponte", "geo_muri_sostegno", VerticalPileSheetViewModel.PileModule,
        VerticalPileSheetViewModel.MicropileModule, HorizontalPileSheetViewModel.PileModule, HorizontalPileSheetViewModel.MicropileModule, "geo_efficienza_orizzontale",
        "geo_fondazioni_superficiali", "mat_acciaio_armatura"];

    /// <summary>Upper bound of a whole round: a deadlock fails the suite instead of hanging the runner. Not a pause.</summary>
    private static readonly TimeSpan RoundDeadline = TimeSpan.FromMinutes(15);

    private sealed record Case(string Module, string Id, JsonObject Data)
    {
        public string Name => Module + "--" + Id;
        /// <summary>The identifiers (GUIDs) written in the fixture: they stay in the fingerprint as they are.</summary>
        public IReadOnlySet<string> Ids { get; } = Identifier.Matches(Data.ToJsonString()).Select(m => IdentifierKey(m.Value)).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Runs the serial round and <paramref name="rounds"/> parallel rounds; writes the serial fingerprints and a summary in <paramref name="output"/>.</summary>
    internal static int Run(string root, string output, int rounds = 2) => SingleThreadContext.Run(ui => RunAsync(ui, root, output, rounds));

    private static async Task<int> RunAsync(SingleThreadContext ui, string root, string output, int rounds)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the F3 captures: texts and numbers of the results read as the sheets write them.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            var tolerances = Tolerances.Load(Path.Combine(root, "tests", "ANTHEA.Testing", "tolerances.json"));
            var cases = Cases(root);
            expect.That(cases.Count > 0 && Modules.All(m => cases.Any(c => c.Module == m)), "Fixture F3 mancanti per qualche modulo della prova");
            var limiter = new CalculationLimiter(CalculationLimiter.DefaultMaximum);
            var engines = new Engines(ui.ThreadId);
            string folder = Path.Combine(output, "paralleli");
            Directory.CreateDirectory(Path.Combine(folder, "serie"));

            // Series: one sheet at a time.
            var serial = new Dictionary<string, string>(StringComparer.Ordinal);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using (var bench = new Bench(limiter, engines))
                foreach (var item in cases)
                {
                    var sheet = bench.Open(item);
                    await Within(CalculateAsync(item, sheet));
                    serial[item.Name] = Fingerprint(item, sheet, tolerances);
                    File.WriteAllText(Path.Combine(folder, "serie", item.Name + ".txt"), serial[item.Name], new UTF8Encoding(false));
                }
            expect.That(engines.Peak == 1, "Calcoli contemporanei nella serie: " + engines.Peak);
            expect.That(engines.ConcretePeak == 1 && engines.Overlap == 0,
                $"Sezione c.a. nella serie: {engines.ConcretePeak} lotti insieme, {engines.Overlap} in corso all'ingresso di un calcolo dei moduli");
            var summary = new List<string> { $"processori {Environment.ProcessorCount}; limite {limiter.Maximum}; casi {cases.Count}; serie {watch.Elapsed.TotalSeconds:0.0} s" };

            for (int round = 1; round <= rounds; round++)
            {
                watch.Restart();
                using var bench = new Bench(limiter, engines);
                var sheets = new List<(Case Item, SheetSession Sheet)>();
                // Every fixture twice: two documents, two sessions of the same module next to the other modules (the RC section first).
                var background = cases.Where(c => !OnInterfaceThread(c.Module)).ToList();
                foreach (var item in background) { sheets.Add((item, bench.Open(item))); sheets.Add((item, bench.Open(item))); }
                // The gate counts the calculations of the module services only: the RC section does not pass through them.
                int counted = sheets.Count(s => s.Item.Module != ConcreteSectionSheetModule.Id);
                engines.Arm(Math.Min(limiter.Maximum, counted));
                var running = sheets.Select(s => CalculateAsync(s.Item, s.Sheet)).ToList();
                // Meanwhile the UI thread: the pile group and the foundations calculate on it (Q8 a), the material sheet at every refresh.
                foreach (var item in cases.Where(c => OnInterfaceThread(c.Module)))
                    for (int copy = 0; copy < 2; copy++)
                    {
                        var sheet = bench.Open(item);
                        await Within(CalculateAsync(item, sheet));
                        sheets.Add((item, sheet));
                    }
                await Task.WhenAll(running).WaitAsync(RoundDeadline);
                int peak = engines.Peak, concretePeak = engines.ConcretePeak, overlap = engines.Overlap;
                bool gate = engines.Disarm();
                expect.That(gate, $"Giro parallelo {round}: il cancello non ha visto {Math.Min(limiter.Maximum, counted)} calcoli contemporanei");
                expect.That(peak <= limiter.Maximum, $"Giro parallelo {round}: {peak} calcoli contemporanei oltre il limite {limiter.Maximum}");
                expect.That(limiter.Maximum == 1 || peak >= 2, $"Giro parallelo {round}: nessun calcolo contemporaneo (picco {peak})");
                expect.That(concretePeak >= 2, $"Giro parallelo {round}: lotti della sezione c.a. mai insieme (picco {concretePeak})");
                expect.That(overlap >= 1, $"Giro parallelo {round}: nessun lotto della sezione c.a. in corso accanto ai calcoli dei moduli");
                foreach (var (item, sheet) in sheets)
                {
                    string actual = Fingerprint(item, sheet, tolerances);
                    if (actual != serial[item.Name])
                    {
                        string parallel = Path.Combine(folder, "parallelo-" + round.ToString(CultureInfo.InvariantCulture));
                        Directory.CreateDirectory(parallel);
                        File.WriteAllText(Path.Combine(parallel, item.Name + ".txt"), actual, new UTF8Encoding(false));
                    }
                    expect.That(actual == serial[item.Name], $"Giro parallelo {round}, {item.Name}: risultato diverso dalla serie ({FirstDifference(serial[item.Name], actual)})");
                }
                summary.Add($"giro parallelo {round}: {sheets.Count} fogli, picco {peak} calcoli contemporanei, sezione c.a. {concretePeak} lotti insieme"
                    + $" e {overlap} in corso all'ingresso di un calcolo dei moduli, {watch.Elapsed.TotalSeconds:0.0} s");
            }
            summary.AddRange(cases.Select(c => c.Name + " " + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serial[c.Name])))[..16]));
            File.WriteAllLines(Path.Combine(folder, "riepilogo.txt"), summary, new UTF8Encoding(false));
            foreach (string line in summary.Take(rounds + 1)) Console.WriteLine(line);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    /// <summary>The F3 fixtures of <see cref="Modules"/>, module by module, each in ordinal order.</summary>
    private static List<Case> Cases(string root)
    {
        string inputs = Path.Combine(root, "tests", "fixtures", "f3", "inputs");
        return Modules.SelectMany(module => Directory.EnumerateFiles(inputs, module + "--*.json")
                .Select(path => new Case(module, Path.GetFileNameWithoutExtension(path)[(module.Length + 2)..], JsonNode.Parse(File.ReadAllText(path))!["data"]!.AsObject()))
                .OrderBy(c => c.Id, StringComparer.Ordinal))
            .ToList();
    }

    /// <summary>The modules that calculate on the UI thread (decision Q8 a) and the material sheet, which calculates at every refresh.</summary>
    private static bool OnInterfaceThread(string module) => module is "geo_efficienza_orizzontale" or "geo_fondazioni_superficiali" or "mat_acciaio_armatura";

    private static bool OpensElastic(JsonObject data) => data["vista_orizzontale"]?.ToString() == "elastico";

    private static Task Within(Task task) => task.WaitAsync(RoundDeadline);

    /// <summary>
    /// The path of the F3 capture through the ViewModel: commit, «Calcola» awaited, then the elastic response of a horizontal pile that opens
    /// on it, then the automatic recalculations still pending, commit. The RC section is also left to settle before and after the commit.
    /// </summary>
    private static async Task CalculateAsync(Case item, SheetSession sheet)
    {
        var vm = sheet.ViewModel;
        vm.Commit();
        await vm.CalculateAsync();
        if (vm is HorizontalPileSheetViewModel horizontal && OpensElastic(item.Data)) await horizontal.Elastic.CalculateAsync();
        if (vm is SheetViewModelBase common) await common.PendingCalculation;
        if (vm is HorizontalPileSheetViewModel pile) await pile.Elastic.PendingCalculation;
        if (vm is ConcreteSheetViewModel concrete) await SettleAsync(concrete);
        vm.Commit();
        if (vm is ConcreteSheetViewModel committed) await SettleAsync(committed);
    }

    /// <summary>
    /// The RC section at rest: no batch running or queued (a batch ended after a change queues the next one) and no quick resistance running
    /// (worker threads with a scheduler of their own, started by the preview of the opening). Woken by the notifications of the sheet and of its
    /// inputs, never by a pause; a section that never settles fails the round at <see cref="RoundDeadline"/>.
    /// </summary>
    private static async Task SettleAsync(ConcreteSheetViewModel sheet)
    {
        bool Quiet() => !sheet.IsBusy && !sheet.IsCalculationQueued && !sheet.Inputs.IsQuickResistanceRunning;
        while (!Quiet())
        {
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Changed(object? sender, PropertyChangedEventArgs e) => changed.TrySetResult();
            var inputs = sheet.Inputs;
            sheet.PropertyChanged += Changed; inputs.PropertyChanged += Changed;
            try { if (!Quiet()) await Within(changed.Task); }
            finally { sheet.PropertyChanged -= Changed; inputs.PropertyChanged -= Changed; }
        }
    }

    // ---------------------------------------------------------------- fingerprints

    /// <summary>
    /// What a sheet shows of its calculation, in a text that keeps every bit of the numbers ("R") and the order of the keys. The identifiers
    /// that the sheet generates when it opens (Guid.NewGuid of the rows of the actions without an id in the fixture: an identity, not a
    /// result, different at every opening) become «&lt;id generato n&gt;» in the order of their first appearance, as the F3 capture numbers
    /// them (GuidMap); the identifiers written in the fixture stay as they are.
    /// </summary>
    private static string Fingerprint(Case item, SheetSession sheet, Tolerances tolerances)
    {
        var vm = sheet.ViewModel;
        var text = new StringBuilder();
        text.Append("modulo ").Append(vm.ModuleId).Append('\n');
        if (vm is SheetViewModelBase common)
        {
            text.Append("stato ").Append(common.CalculationState).Append('\n');
            text.Append("messaggio ").Append(common.CalculationMessage).Append('\n');
            text.Append("avvisi ").Append(string.Join(" | ", common.Warnings)).Append('\n');
        }
        text.Append("risultati ").Append(vm.HasResults).Append('\n');
        text.Append("risultato "); Json(text, vm.Result, tolerances); text.Append('\n');
        text.Append("dati "); Json(text, vm.Data, tolerances); text.Append('\n');
        if (vm is HorizontalPileSheetViewModel horizontal)
        {
            text.Append("elastico "); Json(text, horizontal.Elastic.Result, tolerances); text.Append('\n');
            text.Append("risposta "); Json(text, horizontal.Elastic.Response, tolerances); text.Append('\n');
        }
        if (vm is ConcreteSheetViewModel concrete)
        {
            text.Append("stato sezione ").Append(concrete.Status).Append('\n');
            text.Append("errori ").Append(string.Join(" | ", concrete.Calculation.Errors.Select(e => e.Key + ": " + e.Value))).Append('\n');
            text.Append("resistenze rapide ").Append(concrete.Inputs.QuickResistanceText.Replace("\n", " | ", StringComparison.Ordinal)).Append('\n');
        }
        if (vm is RebarMaterialSheetViewModel rebar)
        {
            text.Append("valori ").Append(rebar.Results).Append('\n');
            text.Append("stato materiale ").Append(rebar.Status).Append('\n');
            text.Append("diagramma ").Append(string.Join(";", rebar.Diagram.Select(p => string.Join(",", p.Select(Number))))).Append('\n');
        }
        var generated = new Dictionary<string, string>(StringComparer.Ordinal);
        return Identifier.Replace(text.ToString(), match =>
        {
            string key = IdentifierKey(match.Value);
            if (item.Ids.Contains(key)) return match.Value;
            if (!generated.TryGetValue(key, out string? name)) generated[key] = name = "<id generato " + (generated.Count + 1).ToString(CultureInfo.InvariantCulture) + ">";
            return name;
        });
    }

    /// <summary>A GUID with or without dashes (the pattern of TextNormalizer of the F3 capture).</summary>
    private static readonly System.Text.RegularExpressions.Regex Identifier = new(@"\b[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\b");

    private static string IdentifierKey(string guid) => guid.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// The instant written by the sheet when it opens (the date of the pinned prices of Bridge Design, DateTimeOffset.UtcNow in ISO 8601): not a
    /// result, different in every run; the F3 capture replaces it too (TextNormalizer).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex Timestamp = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?$");

    /// <summary>
    /// JSON in the order of the keys, numbers as doubles with every bit ("R"), without the volatile keys of the tolerances and with the instants
    /// of <see cref="Timestamp"/> as «data e ora».
    /// </summary>
    private static void Json(StringBuilder text, JsonNode? node, Tolerances tolerances)
    {
        switch (node)
        {
            case null:
                text.Append("null");
                break;
            case JsonObject o:
                text.Append('{');
                bool first = true;
                foreach (var (key, value) in o)
                {
                    if (tolerances.IsVolatile(key, "") is not null) continue;
                    if (!first) text.Append(',');
                    first = false;
                    text.Append(JsonSerializer.Serialize(key)).Append(':');
                    Json(text, value, tolerances);
                }
                text.Append('}');
                break;
            case JsonArray a:
                text.Append('[');
                for (int i = 0; i < a.Count; i++)
                {
                    if (i > 0) text.Append(',');
                    Json(text, a[i], tolerances);
                }
                text.Append(']');
                break;
            case JsonValue v:
                if (v.TryGetValue<double>(out double number)) text.Append(Number(number));
                else if (v.TryGetValue<string>(out string? value) && Timestamp.IsMatch(value)) text.Append("\"<data e ora>\"");
                else text.Append(v.ToJsonString());
                break;
        }
    }

    private static string FirstDifference(string expected, string actual)
    {
        string[] a = expected.Split('\n'), b = actual.Split('\n');
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            string left = i < a.Length ? a[i] : "", right = i < b.Length ? b[i] : "";
            if (left == right) continue;
            int column = 0;
            while (column < Math.Min(left.Length, right.Length) && left[column] == right[column]) column++;
            string Around(string s) => s.Length <= column ? "" : s.Substring(Math.Max(0, column - 40), Math.Min(120, s.Length - Math.Max(0, column - 40)));
            return $"riga {i + 1}, colonna {column + 1}: «{Around(left)}» / «{Around(right)}»";
        }
        return "nessuna";
    }

    // ---------------------------------------------------------------- shell

    /// <summary>The workspace of the shell with the sheet modules of the test, the scripted services and the limiter of the application.</summary>
    private sealed class Bench : IDisposable
    {
        private readonly WorkspaceSession workspace;
        private readonly Engines engines;

        internal Bench(CalculationLimiter limiter, Engines engines)
        {
            this.engines = engines;
            var services = Scripted.Services();
            var registry = new ModuleRegistry([new ConcreteSectionSheetModule(new NoFigures()), new BridgeDesignSheetModule(new NoFigures()), new CompositeSectionSheetModule(new NoFigures()),
                new RetainingWallSheetModule(new NoFigures()), new VerticalPileSheetModule(VerticalPileSheetViewModel.PileModule, new NoFigures()),
                new VerticalPileSheetModule(VerticalPileSheetViewModel.MicropileModule, new NoFigures()),
                new HorizontalPileSheetModule(HorizontalPileSheetViewModel.PileModule, new NoFigures()),
                new HorizontalPileSheetModule(HorizontalPileSheetViewModel.MicropileModule, new NoFigures()), new PileGroupSheetModule(),
                new ShallowFoundationSheetModule(), new RebarMaterialSheetModule()]);
            workspace = new WorkspaceSession(services, new RegistrySheetFactory(registry, services, limiter, engines.Get),
                new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")));
        }

        /// <summary>A new calculation file of the module with the data of the fixture, in a tab of its own (the copy of an example).</summary>
        internal SheetSession Open(Case item)
        {
            var document = DocumentService.CreateDocument(item.Module);
            document["dati"] = item.Data.DeepClone();
            workspace.OpenDocument(document, null, dirty: true);
            var sheet = workspace.Active ?? throw new InvalidOperationException("Foglio non aperto: " + item.Name);
            if (sheet.ViewModel is ConcreteSheetViewModel concrete) engines.Watch(concrete);
            return sheet;
        }

        public void Dispose() => workspace.Dispose();
    }

    /// <summary>
    /// The calculation services of the modules (ModuleServices), with the count of the calculations running together. Armed for a parallel
    /// round, a gate holds the first calculations off the UI thread until <c>target</c> of them run together (the UI thread is never held).
    /// The batches of the RC section (which do not pass through the services) are counted apart, from the busy state of their sheets.
    /// </summary>
    private sealed class Engines(int uiThread)
    {
        private readonly object sync = new();
        private int running, peak, arrivals, target, concreteRunning, concretePeak, overlap;
        private ManualResetEventSlim? gate;
        private bool timedOut;

        internal int Peak { get { lock (sync) return peak; } }
        /// <summary>The most batches of the RC section running together.</summary>
        internal int ConcretePeak { get { lock (sync) return concretePeak; } }
        /// <summary>The most batches of the RC section running when a calculation of the module services entered.</summary>
        internal int Overlap { get { lock (sync) return overlap; } }

        internal IModuleService Get(string module) => new Counted(ModuleServices.Get(module), this);

        /// <summary>Counts the batches of an RC section sheet: its busy state changes on the UI thread, when a batch starts and ends.</summary>
        internal void Watch(ConcreteSheetViewModel sheet)
        {
            bool busy = sheet.IsBusy;
            if (busy) Concrete(1);
            sheet.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ConcreteSheetViewModel.IsBusy) || sheet.IsBusy == busy) return;
                busy = sheet.IsBusy;
                Concrete(busy ? 1 : -1);
            };
        }

        private void Concrete(int change)
        {
            lock (sync) { concreteRunning += change; concretePeak = Math.Max(concretePeak, concreteRunning); }
        }

        internal void Arm(int count)
        {
            lock (sync)
            {
                target = count; arrivals = 0; peak = running; timedOut = false; gate = new ManualResetEventSlim(false);
                concretePeak = concreteRunning; overlap = 0;
            }
        }

        /// <summary>Opens the gate; true when it opened because <c>target</c> calculations ran together.</summary>
        internal bool Disarm()
        {
            lock (sync)
            {
                bool reached = gate is { IsSet: true } && !timedOut;
                gate?.Set();
                gate = null; target = 0;
                peak = 0; concretePeak = 0; overlap = 0;
                return reached;
            }
        }

        private void Enter()
        {
            ManualResetEventSlim? wait = null;
            lock (sync)
            {
                running++;
                peak = Math.Max(peak, running);
                overlap = Math.Max(overlap, concreteRunning);
                if (gate is { } g && Environment.CurrentManagedThreadId != uiThread && ++arrivals <= target)
                {
                    if (arrivals == target) g.Set();
                    else wait = g;
                }
            }
            if (wait is not null && !wait.Wait(Expect.Deadline)) lock (sync) timedOut = true;
        }

        private void Leave() { lock (sync) running--; }

        private sealed class Counted(IModuleService inner, Engines owner) : IModuleService
        {
            public string ModuleId => inner.ModuleId;
            public JsonObject Prepare(JsonObject source) => inner.Prepare(source);
            public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
            {
                owner.Enter();
                try { return inner.Calculate(source, cancellation); }
                finally { owner.Leave(); }
            }
        }
    }

    /// <summary>Figures of the reports: the test builds no report.</summary>
    private sealed class NoFigures : IBridgeDesignFigureRenderer, ICompositeSectionFigureRenderer, IRetainingWallFigureRenderer, IVerticalPileFigureRenderer, IHorizontalPileFigureRenderer,
        IConcreteSectionFigureRenderer
    {
        private static NotSupportedException None() => new("Nessun report nella prova dei calcoli in parallelo.");
        public IReadOnlyList<Anthea.Application.Reporting.ReportFigure> Render(ConcreteReportDrawings drawings, IReadOnlySet<string> options) => throw None();
        public byte[] Drawing(JsonObject data, Anthea.Application.BridgeDesign.BridgeDesignModel.Result result, bool section) => throw None();
        public IReadOnlyList<Anthea.Application.Reporting.ReportFigure> Render(Anthea.Application.Bridge.BridgeResultView result, JsonObject viewSettings, IReadOnlySet<string> options) => throw None();
        public IReadOnlyList<Anthea.Application.Reporting.ReportFigure> Render(Anthea.Application.Walls.WallModel.Result calculation, Anthea.Application.Reporting.WallReportPresentation context) => throw None();
        public Anthea.Application.Reporting.ReportFigure RenderGlobal(JsonObject input, Anthea.Application.Walls.SlopeResult result, Anthea.Application.Walls.SlopeCaseResult? shown) => throw None();
        public IReadOnlyList<Anthea.Application.Reporting.ReportFigure> Render(VerticalPileFigureRequest request) => throw None();
        public IReadOnlyList<X.Core.ReportOrizzontale.Figure> Render(JsonObject result) => throw None();
    }
}
