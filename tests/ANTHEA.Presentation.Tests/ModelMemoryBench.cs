using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using Anthea.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Rilievi del 16/11/2026 sul modello MIDAS vero (R1, R5, R6, R8): the path of the shell without WPF, on the thread of the interface of the
/// checks (<see cref="SingleThreadContext"/>), with the time the thread stays blocked, the working set and the private bytes of the process
/// sampled every 200 ms, the managed heap after a full compacting collection. «--banco-modelli &lt;uscita&gt; --modello &lt;.antheamodel&gt;» imports the
/// copy in a new project, opens the catalogue and runs «Calcola tutte» (or the first N with «--combinazioni N»); «--progetto &lt;.programma&gt;»
/// opens a saved project and its model instead. «--fasi» chooses the phases after it: «libera» («Libera tutte»), «annulla» («↶ Annulla» of the
/// project), «salva» and «riapri» (save into the output folder and open it again in a new shell). Measures only: no expected value, never part
/// of a stage of build/ci.ps1. The model data stay where they are; banco-modelli.md and banco-modelli.json go into the output folder.
/// </summary>
internal static class ModelMemoryBench
{
    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    /// <summary>The services of the viewer for the bench: «Importa modello…» gives the copy read from the file.</summary>
    private sealed class BenchViewerServices(ModelSnapshot? next, long limit) : IModelViewerServices
    {
        public ModelSnapshot? Next { get; set; } = next;
        public Task<ModelSnapshot?> ImportAsync(CancellationToken cancellationToken) => Task.FromResult(Next);
        public void FitView() { }
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
        public long? LoadCombinationLimit() => limit;
    }

    private sealed record Phase(string Name, double Seconds, double BlockedSeconds, double PeakWorkingSetGb, double PeakPrivateGb,
        double EndWorkingSetGb, double EndPrivateGb, double LiveManagedGb, double WorkingSetAfterCollectGb, string Note);

    /// <summary>Samples of the process (working set, private bytes) and of the thread of the interface (the longest gap of a 50 ms heartbeat).</summary>
    private sealed class Monitor : IDisposable
    {
        private readonly Thread sampler;
        private volatile bool stop;
        private long peakWorkingSet, peakPrivate;
        private double longestGap;
        private readonly Stopwatch since = Stopwatch.StartNew();
        private readonly StreamWriter trace;

        internal Monitor(string path)
        {
            trace = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
            trace.WriteLine("s;fase;ws_gb;private_gb;gc_heap_gb");
            sampler = new Thread(Sample) { IsBackground = true, Priority = ThreadPriority.AboveNormal, Name = "banco-memoria" };
            sampler.Start();
        }

        internal string Current { get; set; } = "";

        private void Sample()
        {
            var process = Process.GetCurrentProcess();
            while (!stop)
            {
                process.Refresh();
                long ws = process.WorkingSet64, priv = process.PrivateMemorySize64;
                InterlockedMax(ref peakWorkingSet, ws); InterlockedMax(ref peakPrivate, priv);
                trace.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{since.Elapsed.TotalSeconds:F1};{Current};{ws / 1e9:F2};{priv / 1e9:F2};{GC.GetTotalMemory(false) / 1e9:F2}"));
                Thread.Sleep(200);
            }
        }

        private static void InterlockedMax(ref long target, long value)
        {
            long seen;
            while (value > (seen = Interlocked.Read(ref target)) && Interlocked.CompareExchange(ref target, value, seen) != seen) { }
        }

        /// <summary>The heartbeat on the thread of the interface: every turn records the gap since the previous one.</summary>
        internal async Task Heartbeat(CancellationToken token)
        {
            var watch = Stopwatch.StartNew();
            while (!token.IsCancellationRequested)
            {
                double before = watch.Elapsed.TotalSeconds;
                try { await Task.Delay(50, token); } catch (OperationCanceledException) { return; }
                double gap = watch.Elapsed.TotalSeconds - before - .05;
                if (gap > longestGap) longestGap = gap;
            }
        }

        internal void Reset()
        {
            var process = Process.GetCurrentProcess(); process.Refresh();
            Interlocked.Exchange(ref peakWorkingSet, process.WorkingSet64); Interlocked.Exchange(ref peakPrivate, process.PrivateMemorySize64); longestGap = 0;
        }

        internal (double Ws, double Priv, double Gap) Peaks() => (Interlocked.Read(ref peakWorkingSet) / 1e9, Interlocked.Read(ref peakPrivate) / 1e9, longestGap);

        public void Dispose() { stop = true; sampler.Join(); trace.Dispose(); }
    }

    private static bool aggressiveCollections;
    // «--scena»: the viewer is told that its scene is ready, as ModelViewerControl does after its first render (R7).
    private static bool sceneReady;

    internal static int Run(string output, string[] args)
    {
        string? Option(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        string? modelPath = Option("--modello"), projectPath = Option("--progetto");
        int count = int.Parse(Option("--combinazioni") ?? "0", CultureInfo.InvariantCulture);
        long limit = long.Parse(Option("--limite") ?? "8589934592", CultureInfo.InvariantCulture);
        var phases = (Option("--fasi") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        if (modelPath is null == (projectPath is null)) { Console.Error.WriteLine("Indica --modello <.antheamodel> oppure --progetto <.programma>."); return 2; }
        aggressiveCollections = args.Contains("--gc-aggressivo");
        sceneReady = args.Contains("--scena");
        Directory.CreateDirectory(output);
        return SingleThreadContext.Run(_ => RunAll(output, modelPath, projectPath, count, limit, phases));
    }

    private static async Task<int> RunAll(string output, string? modelPath, string? projectPath, int count, long limit, IReadOnlySet<string> phases)
    {
        var results = new List<Phase>();
        var notes = new List<string>();
        using var monitor = new Monitor(Path.Combine(output, "banco-modelli-campioni.csv"));
        using var beat = new CancellationTokenSource();
        var heartbeat = monitor.Heartbeat(beat.Token);
        var process = Process.GetCurrentProcess();
        string Gb(long bytes) => (bytes / 1e9).ToString("F2", CultureInfo.InvariantCulture);
        async Task<T> Measure<T>(string name, Func<Task<T>> body, Func<T, string>? note = null)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} fase {name}…");
            monitor.Current = name; monitor.Reset();
            var watch = Stopwatch.StartNew();
            var value = await body();
            double seconds = watch.Elapsed.TotalSeconds;
            await Task.Delay(300);
            var (ws, priv, gap) = monitor.Peaks();
            process.Refresh(); long endWs = process.WorkingSet64, endPriv = process.PrivateMemorySize64;
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, true, true); GC.WaitForPendingFinalizers(); GC.Collect(2, GCCollectionMode.Forced, true, true);
            long live = GC.GetTotalMemory(false);
            long committed = GC.GetGCMemoryInfo(GCKind.Any).TotalCommittedBytes;
            process.Refresh(); long afterWs = process.WorkingSet64;
            // What a collection that gives the free memory back to the system costs and returns (GCCollectionMode.Aggressive).
            // Only with «--gc-aggressivo»: it changes what the next phase starts from (the application never does it before 16/11/2026).
            var aggressive = Stopwatch.StartNew();
            if (aggressiveCollections) GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            double aggressiveSeconds = aggressive.Elapsed.TotalSeconds;
            process.Refresh(); long aggressiveWs = process.WorkingSet64;
            // The heartbeat records the collections above before the next phase starts.
            await Task.Delay(200);
            var phase = new Phase(name, seconds, gap, ws, priv, endWs / 1e9, endPriv / 1e9, live / 1e9, afterWs / 1e9,
                $"GC impegnata {committed / 1e9:F2} GB; GC aggressivo {aggressiveSeconds:F1} s → WS {aggressiveWs / 1e9:F2} GB · " + (note?.Invoke(value) ?? ""));
            results.Add(phase);
            Console.WriteLine($"  {seconds:F1} s · interfaccia bloccata al massimo {gap:F1} s · picco WS {ws:F2} GB, privata {priv:F2} GB · fine WS {Gb(endWs)} GB · gestita viva {Gb(live)} GB · WS dopo GC {Gb(afterWs)} GB · {phase.Note}");
            return value;
        }

        var bench = new ShellBench();
        var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var services = new BenchViewerServices(null, limit);
        ProjectSession session;
        ModelTabViewModel tab;
        ModelViewerViewModel? viewer = null;
        void Create(ModelTabViewModel model)
        {
            // As ModelTabView.CreateViewer: no viewer while the copy is read on another thread (R5, R8).
            viewer?.Dispose(); viewer = null;
            if (model.IsLoading || model.LoadFailure is not null) return;
            viewer = new ModelViewerViewModel(model.Snapshot, model.Targets, model.IsReadOnly, services, model.Imported, model.Linked)
                { CombinationArchive = true, UndoProjectChange = model.UndoChange };
            viewer.SetDisplayName(model.Title);
            // As ModelViewerControl after its first render (R7).
            if (sceneReady) viewer.SceneReady();
        }
        void Bind(ModelTabViewModel model)
        {
            // As ModelTabView: the viewer is created again when the copy of the tab is read again (undo, redo, a previous copy).
            model.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(ModelTabViewModel.Snapshot) or nameof(ModelTabViewModel.IsReadOnly)) Create(model); };
            Create(model);
        }
        // The viewer on the copy read: at once, or at the end of the reading on another thread.
        async Task<ModelViewerViewModel> Ready(ModelTabViewModel model)
        {
            while (model.IsLoading || viewer is null) { if (model.LoadFailure is { } failure) throw new InvalidOperationException(failure); await Task.Delay(20); }
            return viewer;
        }

        if (modelPath is not null)
        {
            var snapshot = await Measure("lettura del file .antheamodel", () => Task.Run(() => ModelSnapshotFiles.Read(modelPath)),
                s => $"{s.Nodes.Length:N0} nodi, {s.Elements.Length:N0} elementi, {s.Results.Length} casi");
            var document = ProjectDocuments.CreateArchive();
            var section = ProjectDocuments.AddSection(ProjectDocuments.AddProject(document, "Progetto"), "Sezione");
            session = (ProjectSession)shell.Workspace.OpenDocument(document, null, true)!;
            section = session.Find(section.S("id"))!;
            tab = shell.ShowModel(section, null)!;
            Bind(tab);
            services.Next = snapshot;
            await Measure("importazione nel progetto (Importa modello…)", async () => { await viewer!.ImportCommand.ExecuteAsync(null); return viewer.Status; }, s => s);
        }
        else
        {
            session = await Measure("apertura del progetto (File › Apri)", () => Task.FromResult((ProjectSession)shell.Workspace.OpenFile(projectPath!)!), _ => "");
            var container = session.Projects.SelectMany(ProjectModelStore.Containers).First(c => ProjectModelStore.Models(c).Any());
            string id = ProjectModelStore.Models(container).First().S("id");
            var opened = await Measure("apertura del modello (scheda Modelli) e visore", async () =>
            {
                var shown = shell.ShowModel(container, id)!; Bind(shown);
                return (Tab: shown, Viewer: await Ready(shown));
            }, t => $"{t.Tab.Snapshot?.Results.Length} insiemi di risultati · {t.Viewer.CombinationSummary}");
            tab = opened.Tab;
        }
        // R7: the scene of the copy is on screen; the base cases are prepared in the background (the time the window stays free meanwhile).
        if (sceneReady)
            await Measure("preparazione dei casi base in background (R7)", async () =>
            {
                viewer!.SceneReady();
                while (viewer.IsPreparingCombinations) await Task.Delay(100);
                return viewer.CombinationPreparationText;
            }, s => s);
        notes.Add($"Modello: {viewer!.Summary}; {viewer.CombinationSummary}; limite {ModelCombinations.CopyLimit(viewer.CombinationCopyBytes)}.");

        if (phases.Contains("serie"))
        {
            // The catalogue: «Combinazioni», then the manager (the preparation of GPC starts in the background, R7).
            viewer.OpenCombinationManagerCommand.Execute(null); viewer.OpenCombinationManagerCommand.Execute(null);
            var pending = viewer.Combinations.Where(c => c.CanCalculate && c.State != "Calcolata").Select(c => c.Definition.Id).ToArray();
            if (count > 0) pending = pending.Take(count).ToArray();
            double firstDone = 0; var series = Stopwatch.StartNew();
            viewer.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ModelViewerViewModel.CombinationMessage) && firstDone == 0 && viewer.CombinationMessage.StartsWith("Combinazione 2 di", StringComparison.Ordinal))
                    firstDone = series.Elapsed.TotalSeconds;
            };
            viewer.SelectedCombinationIds = pending;
            await Measure($"serie: Calcola selezionate ({pending.Length})", async () => { series.Restart(); await viewer.CalculateSelectedCombinationsCommand.ExecuteAsync(null); return viewer.CombinationMessage; },
                m => m.Split('\n')[0] + $" · prima combinazione {firstDone:F1} s · {viewer.CombinationSpaceText}");
            notes.Add($"Spazio dopo la serie: {viewer.CombinationSpaceBytes:N0} byte · archivio non salvato {session.Results.UnsavedBytes:N0} byte compressi.");
            Census(viewer, notes);
            // The bit-identity of every result before and after a change: the fingerprint of the whole copy (geometry, cases, definitions and
            // every derived result), and the revision the document entry carries, which must be the same.
            string fingerprint = await Task.Run(() => viewer.Snapshot!.Fingerprint());
            string? revision = ProjectModelStore.Models(tab.Container).Single(m => m.S("id") == tab.ModelId)["revisione"]?.GetValue<string>();
            notes.Add($"Impronta della copia dopo la serie: {fingerprint}; revisione nel documento {(revision == fingerprint ? "uguale" : "DIVERSA: " + revision)}.");
        }
        if (phases.Contains("salva"))
        {
            string saved = Path.Combine(output, "Calcolo.programma");
            await Measure("salvataggio (Salva con nome nella cartella di uscita)", () =>
            {
                bench.Dialogs.Then(saved);
                return Task.FromResult(shell.Workspace.Save(session, true));
            }, s => s ? "salvato" : "NON salvato: " + string.Join(" | ", bench.Messages.Failures.Select(f => f.Title + ": " + f.Error.Message)));
        }
        if (phases.Contains("libera"))
            await Measure("Libera tutte", async () => { await viewer!.FreeAllCombinationResultsCommand.ExecuteAsync(null); return viewer.CombinationMessage; }, m => m + " · " + viewer!.CombinationSpaceText);
        if (phases.Contains("gpc"))
            await Measure("cache GPC del visore svuotata (diagnosi)", () =>
            {
                var field = typeof(ModelViewerViewModel).GetField("combinationService", BindingFlags.NonPublic | BindingFlags.Instance)!;
                ((GpcModelCombinationService)field.GetValue(viewer)!).ClearCache();
                return Task.FromResult("");
            });
        if (phases.Contains("annulla"))
            await Measure("↶ Annulla del progetto", async () =>
            {
                bool restored = session.Restore(false);
                // The viewer of the tab, recreated by the change of the copy (as ModelTabView), shows the restored results.
                var shown = await Ready(tab);
                return (restored, shown.CombinationSummary);
            }, r => (r.restored ? "ripristinato" : "niente da annullare") + " · " + r.CombinationSummary);
        if (phases.Contains("riapri"))
        {
            string saved = Path.Combine(output, "Calcolo.programma");
            viewer?.Dispose(); viewer = null;
            shell.Dispose();
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            var again = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
            var reopened = await Measure("riapertura del progetto salvato", () => Task.FromResult((ProjectSession)again.Workspace.OpenFile(saved)!), _ => "");
            var container = reopened.Projects.SelectMany(ProjectModelStore.Containers).First(c => ProjectModelStore.Models(c).Any());
            string id = ProjectModelStore.Models(container).First().S("id");
            await Measure("riapertura del modello (scheda Modelli) e visore", async () =>
            {
                var shown = again.ShowModel(container, id)!; Bind(shown);
                return (Tab: shown, Viewer: await Ready(shown));
            }, t => $"{t.Tab.Snapshot?.Results.Length} insiemi di risultati · {t.Viewer.CombinationSummary}");
            again.Dispose();
        }
        beat.Cancel(); await heartbeat;
        var text = new StringBuilder();
        text.AppendLine($"# Banco del modulo Modelli · {DateTime.Now:yyyy-MM-dd HH:mm}").AppendLine();
        text.AppendLine($"{Environment.ProcessorCount} processori, memoria {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1e9:F1} GB, GC {(GCSettings.IsServerGC ? "server" : "workstation")} {(GCSettings.LatencyMode)}, .NET {Environment.Version}.").AppendLine();
        foreach (var note in notes) text.AppendLine("- " + note);
        text.AppendLine().AppendLine("| Fase | s | interfaccia bloccata s | picco WS GB | picco privata GB | fine WS GB | gestita viva GB | WS dopo GC GB | nota |").AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var p in results)
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {p.Name} | {p.Seconds:F1} | {p.BlockedSeconds:F1} | {p.PeakWorkingSetGb:F2} | {p.PeakPrivateGb:F2} | {p.EndWorkingSetGb:F2} | {p.LiveManagedGb:F2} | {p.WorkingSetAfterCollectGb:F2} | {p.Note.Replace('|', '/').Replace('\n', ' ')} |"));
        File.WriteAllText(Path.Combine(output, "banco-modelli.md"), text.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "banco-modelli.json"), JsonSerializer.Serialize(new { Notes = notes, Phases = results }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine(text);
        viewer?.Dispose();
        return 0;
    }

    /// <summary>What the results of the copy hold: result sets, plate corners, governing records and their vectors, the distinct strings.</summary>
    private static void Census(ModelViewerViewModel viewer, List<string> notes)
    {
        var snapshot = viewer.Snapshot!;
        long corners = 0, governing = 0, fieldValues = 0, displacements = 0;
        var vectors = new HashSet<double[]>(ReferenceEqualityComparer.Instance);
        var strings = new HashSet<string>(ReferenceEqualityComparer.Instance);
        foreach (var r in snapshot.Results.Where(r => r.Combination != null))
        {
            corners += r.Values.Length; fieldValues += r.Fields?.Sum(f => (long)f.Values.Length) ?? 0; displacements += r.Displacements?.Length ?? 0;
            foreach (var g in r.Combination!.Governing.Concat(r.Combination.PrincipalGoverning ?? []))
            {
                governing++; vectors.Add(g.ConcomitantValues); strings.Add(g.Alternative); strings.Add(g.Family); strings.Add(g.Component);
            }
        }
        notes.Add($"Risultati delle combinazioni: {snapshot.Results.Count(r => r.Combination != null)} insiemi, {corners:N0} angoli di piastra, {fieldValues:N0} valori di campo, " +
            $"{displacements:N0} spostamenti, {governing:N0} governanti con {vectors.Count:N0} vettori distinti e {strings.Count:N0} stringhe distinte (per riferimento).");
    }
}
