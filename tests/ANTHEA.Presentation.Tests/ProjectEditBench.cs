using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Tabs;
using Anthea.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The cost of an edit of a large project in the shell, without WPF (docs/sviluppo/progetto-sistema.md, step 3, 4/11/2026): the operation and
/// everything the shell does after it (tree, overview, open sheets, status line). «--banco-progetto»: the medians in banco-progetto.txt.
/// </summary>
internal static class ProjectEditBench
{
    private const int Projects = 4, Sections = 8, Subsections = 2, SheetsPerSection = 6, OpenSheets = 6, Rounds = 21;

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    internal static int Run(string output) => SingleThreadContext.Run(_ => Task.FromResult(RunAll(output)));

    /// <summary>A sheet with the data of its module, as after its first opening (an empty sheet would make the document unrealistically small).</summary>
    private static void Fill(JsonObject sheet) => sheet["dati"] = Anthea.Application.Documents.DocumentService.CreateSheet(sheet.S("modulo_id"));

    private static int RunAll(string output)
    {
        var document = ProjectDocuments.CreateArchive();
        string[] modules = ["mat_calcestruzzo", "str_palo", "geo_palo_verticale", "geo_stratigrafia"];
        for (int p = 0; p < Projects; p++)
        {
            var project = ProjectDocuments.AddProject(document, "Progetto " + (p + 1));
            for (int s = 0; s < Sections; s++)
            {
                var section = ProjectDocuments.AddSection(project, "Sezione " + (s + 1));
                for (int k = 0; k < SheetsPerSection; k++) Fill(ProjectDocuments.AddSheet(section, modules[k % modules.Length], "Foglio " + (k + 1)));
                for (int u = 0; u < Subsections; u++)
                {
                    var sub = ProjectDocuments.AddSection(section, "Sottosezione " + (u + 1));
                    for (int k = 0; k < SheetsPerSection; k++) Fill(ProjectDocuments.AddSheet(sub, modules[k % modules.Length], "Foglio " + (k + 1)));
                }
            }
        }
        var bench = new ShellBench();
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(),
            new SheetFileExports());
        var workspace = shell.Workspace;
        var session = (ProjectSession)workspace.OpenDocument(document, null, false)!;
        var first = session.Projects.First();
        var firstSection = (JsonObject)first.Array("strutture")[0]!;
        var sheets = ProjectSharedData.SubtreeSheets(firstSection).Take(OpenSheets).ToArray();
        foreach (var sheet in sheets) workspace.OpenSheet(sheet);
        shell.ShowOverview(firstSection);
        int total = session.Projects.Sum(p => ProjectSharedData.SubtreeSheets(p).Count());

        var report = new StringBuilder();
        long bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(document).LongLength;
        report.AppendLine($"Banco del progetto (passo 3), {DateTime.Now:yyyy-MM-dd HH:mm}: {Projects} progetti, {total} fogli con i dati dei moduli ({bytes / 1048576.0:F1} MB di JSON), {OpenSheets} schede aperte, riepilogo aperto; mediana di {Rounds} prove, ms");
        Console.WriteLine(report.ToString().TrimEnd());
        void Measure(string name, Action action)
        {
            var times = new double[Rounds];
            for (int i = 0; i < Rounds; i++)
            {
                var watch = Stopwatch.StartNew();
                action();
                times[i] = watch.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times);
            string line = $"{name,-46} {times[Rounds / 2],8:F2}   (min {times[0]:F2}, max {times[^1]:F2})";
            report.AppendLine(line);
            Console.WriteLine(line);
        }

        var target = ProjectSharedData.SubtreeSheets(session.Projects.Last()).Last();
        int round = 0;
        Measure("Rinomina di un foglio (tutto)", () => session.Rename(target, "Rinominato " + round++));
        var benchCache = new ProjectCoherenceCache();
        benchCache.NewReading();
        foreach (var p in session.Projects) foreach (var s in ProjectSharedData.Sections(p)) SharedDataCoordinator.Coherence(s, benchCache);
        Measure("  di cui storico: confronto senza modifiche", () => session.Record());
        Measure("  di cui storico: nuovo passo", () => { target["nome"] = "Diretto " + round++; session.Record(); });
        Measure("  di cui coerenza delle sezioni sopra il foglio", () =>
        {
            target["nome"] = "Coerenza " + round++;
            benchCache.NewReading();
            foreach (var level in ProjectSharedData.Ancestors((JsonObject)target.Parent!.Parent!).Prepend((JsonObject)target.Parent!.Parent!))
                SharedDataCoordinator.Coherence(level, benchCache);
        });
        Measure("Albero ricostruito", () => shell.Tree.Refresh());
        Measure("Coerenza di tutte le sezioni", () => { foreach (var p in session.Projects) foreach (var s in ProjectSharedData.Sections(p)) SharedDataCoordinator.Coherence(s); });
        var all = session.Projects.SelectMany(ProjectSharedData.Sections).ToArray();
        Measure("  di cui confronto (ProjectComparison)", () => { foreach (var s in all) _ = new ProjectComparison(s).Differences; });
        Measure("  di cui avvisi (ProjectValidation.Warnings)", () => { foreach (var s in all) ProjectValidation.Warnings(s); });
        Measure("  di cui strati mancanti", () => { foreach (var s in all) ProjectSharedData.MissingSoilLayers(s); });
        Measure("    campi dei fogli (Fields), per sezione", () => { foreach (var s in all) foreach (var sheet in ProjectSharedData.ContextSheets(s).Distinct()) ProjectSharedData.Fields(sheet); });
        Measure("    coppie (ComparisonPairs)", () => { foreach (var s in all) ProjectSharedData.ComparisonPairs(s).ToArray(); });
        Measure("    limitazioni (Limitations)", () => { foreach (var s in all) ProjectSharedData.Limitations(s); });
        Measure("    copriferro (CoverChecks)", () => { foreach (var s in all) ProjectValidation.CoverChecks(s); });
        var everySheet = session.Projects.SelectMany(ProjectSharedData.SubtreeSheets).ToArray();
        Measure("    impronta di tutti i fogli (JSON + hash)", () =>
        {
            foreach (var sheet in everySheet) System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(sheet));
        });
        var lastSection = (JsonObject)session.Projects.Last().Array("strutture")[^1]!;
        Measure("Nuovo foglio (tutto)", () => session.AddSheet("mat_calcestruzzo", lastSection));
        Measure("Modifica del foglio aperto e conferma", () =>
        {
            var open = workspace.SheetOf(sheets[0])!;
            workspace.Activate(open);
            ShellBench.Probe(open).Label = "L" + round++;
            workspace.CommitActive();
        });
        Measure("Annulla (tutto)", () => session.Restore(false));
        // The undo replaced the document: the nodes are found again in the one on screen.
        firstSection = session.Find(firstSection.S("id"))!;
        target = session.Find(target.S("id"))!;
        Measure("Riepilogo ricostruito", () => shell.ShowOverview(firstSection));

        // The memory of the history: 30 named steps (a rename each) on top of the project.
        session.History.Reset(session.Document);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = GC.GetTotalMemory(true);
        for (int i = 0; i < ProjectHistory.Capacity; i++) session.Rename(target, "Memoria " + i);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long after = GC.GetTotalMemory(true);
        string memory = $"Memoria dello storico pieno ({session.History.UndoCount} passi), MB {(after - before) / 1048576.0,8:F1} (prima {before / 1048576.0:F1}, dopo {after / 1048576.0:F1})";
        report.AppendLine(memory);
        Console.WriteLine(memory);
        File.WriteAllText(Path.Combine(output, "banco-progetto.txt"), report.ToString(), new UTF8Encoding(false));
        return 0;
    }
}
