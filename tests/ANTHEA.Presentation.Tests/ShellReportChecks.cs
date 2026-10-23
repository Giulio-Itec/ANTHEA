using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 e: the report of a section from the shell («Genera report» of the overview, «Report Word della sezione» of the tree), with the sequence and the
/// texts of MainWindow.ExportSectionReport (ProjectReport.cs, frozen): the sheet with the focus confirmed first; no report without sheets; «Controllo prima
/// del report» with the differences and the notices («Annulla», «Apri confronto», «Genera con segnalazioni»); «Relazione nome.docx» in «Word e PDF
/// affiancati»; the snapshot of the section (never the open project) written under «Generazione del report», whose «Annulla» and closing cancel; the
/// message of today, a warning with the sheets without results; the errors through the guard. The writer is a recording one: the Word document of the
/// shell against the one of MainWindow is compared in the UiTests build (ui/shell-smoke-project-report).
/// </summary>
internal static class ShellReportChecks
{
    internal static int Run() => SingleThreadContext.Run(RunAsync);

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    /// <summary>A writer that records the calls; it can fail, wait for the cancellation, or report sheets without results.</summary>
    private sealed class RecordingReports : ISectionReports
    {
        internal List<(JsonObject Snapshot, string Path)> Written { get; } = [];
        internal int Unavailable { get; set; }
        internal Exception? Failure { get; set; }
        internal bool WaitForCancel { get; set; }
        internal List<string> Progress { get; } = [];

        public async Task<int> WriteAsync(JsonObject snapshot, string path, Action<string> progress, CancellationToken cancellation)
        {
            Written.Add((snapshot, path));
            progress("Scheda 1 di 1: prova");
            Progress.Add("Scheda 1 di 1: prova");
            if (Failure is not null) throw Failure;
            if (WaitForCancel) await Task.Delay(Timeout.Infinite, cancellation);
            await Task.Yield();
            return Unavailable;
        }
    }

    private static async Task<int> RunAsync(SingleThreadContext ui)
    {
        var expect = new Expect();
        var reports = new RecordingReports();
        SectionReportCheckViewModel? check = null;
        SectionReportProgressViewModel? progress = null;
        bool closableWhileRunning = true;
        var files = new ScriptedFileDialogs();
        var dialogs = new ScriptedDialogs();
        var bench = new ShellBench(dialogs: files, windows: dialogs);
        string examples = Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti");
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(examples), new NoWindows(), new SheetFileExports(), new ShellOptions(Reports: reports));
        using var bare = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(examples), new NoWindows(), new SheetFileExports());
        var workspace = shell.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        project.Rename(section, "Pila P1/nord: fusto");
        var empty = project.AddSection(root)!;
        var first = project.AddSheet("mat_calcestruzzo", section)!;
        var second = project.AddSheet("mat_calcestruzzo", section)!;
        string sectionId = section.S("id");
        JsonObject Section() => project.Find(sectionId)!;

        // Enabled for a section with sheets (also in its subsections), never without sheets, without a section or without the writer.
        expect.That(shell.SectionReportCommand.CanExecute(Section()) && shell.SectionReportCommand.CanExecute(root) && !shell.SectionReportCommand.CanExecute(empty)
            && !shell.SectionReportCommand.CanExecute(null), "«Genera report»: abilitato con fogli nella sezione o nelle sottosezioni");
        bare.Workspace.NewProject();
        expect.That(!bare.SectionReportCommand.CanExecute(bare.Workspace.Project!.Projects.FirstOrDefault() ?? new JsonObject()), "Senza lo scrittore dei report il comando non si abilita");
        expect.That(await Expect.Within(shell.ExportSectionReportAsync(empty)) is null && files.Requests.Count == 0 && reports.Written.Count == 0, "Sezione senza fogli: nessun report, nessuna domanda");

        // The name of the file: the characters not allowed become «_», as MainWindow.
        expect.That(SectionReportRunner.FileName(Section()) == "Relazione Pila P1_nord_ fusto.docx" && SectionReportRunner.FileName(new JsonObject()) == "Relazione Sezione.docx",
            "Nome proposto «Relazione nome.docx»: " + SectionReportRunner.FileName(Section()));

        // 1. No differences: the sheet with the focus is confirmed first; then the file and the report of the snapshot, the message of today.
        bool differencesAtStart = ProjectSharedData.Differences(Section()).Count > 0 || ProjectValidation.Warnings(Section()).Length > 0;
        if (differencesAtStart) dialogs.Then(d => { check = (SectionReportCheckViewModel)d; check.GenerateCommand.Execute(null); });
        dialogs.Then(d => progress = (SectionReportProgressViewModel)d);
        var open = workspace.OpenSheet(first);
        ShellBench.Probe(open).Label = "confermata prima del report";
        string target = Path.Combine(Path.GetTempPath(), "anthea-prova", "Relazione.docx");
        files.Then(target);
        int? written = await Expect.Within(shell.ExportSectionReportAsync(Section()));
        var request = (Anthea.Presentation.Services.SaveFileRequest)files.Requests[^1];
        expect.That(written == 0 && reports.Written.Count == 1 && reports.Written[0].Path == target && request.FileName == "Relazione Pila P1_nord_ fusto.docx"
            && request.Filters.Single().Name == SectionReportRunner.Filter, "Report scritto nel file scelto, con il nome e il filtro di oggi");
        var snapshot = reports.Written[0].Snapshot;
        expect.That(!ReferenceEquals(snapshot, Section()) && JsonNode.DeepEquals(snapshot, Anthea.Application.Reporting.ProjectReportService.Snapshot(Section()))
            && ProjectSharedData.SubtreeSheets(snapshot).Any(s => s["dati"].S("etichetta") == "confermata prima del report"),
            "Lo scrittore riceve la copia della sezione, con il foglio aperto confermato prima");
        var shown = bench.Messages.Shown[^1];
        expect.That(shown == (SectionReportRunner.MessageTitle, "Report salvato in Word e PDF:\n" + target + "\n" + Path.ChangeExtension(target, ".pdf"), false),
            "Messaggio «Report salvato in Word e PDF»: " + shown.Message);
        expect.That(progress is { Title: "Generazione del report", IsFinished: true, IsCancelled: false, Status: "Scheda 1 di 1: prova" } && !progress.CancelCommand.CanExecute(null)
            && SectionReportProgressViewModel.Starting == "Preparazione delle schede…", "«Generazione del report» con il testo del report, chiuso alla fine");

        // 2. Different common data: «Controllo prima del report» first. «Annulla»: nothing else.
        project.ChangeData(() => { Section().Array("fogli")[1]!["dati"]!["classe"] = "C40/50"; return true; });
        var differences = ProjectSharedData.Differences(Section());
        expect.That(differences.Count > 0, "Una differenza sulla classe del calcestruzzo");
        dialogs.Then(d => { check = (SectionReportCheckViewModel)d; check.CancelCommand.Execute(null); });
        int requests = files.Requests.Count;
        expect.That(await Expect.Within(shell.ExportSectionReportAsync(Section())) is null && files.Requests.Count == requests && reports.Written.Count == 1, "«Annulla» del controllo: nessun file, nessun report");
        var key = differences[0].Key;
        var lines = differences.Where(d => d.Key == key).SelectMany(d => new[] { d.First, d.Second }).Distinct()
            .Select(s => ProjectSharedData.Location(s) + " › " + s.S("nome") + ": " + ProjectSharedData.Text(ProjectSharedData.Fields(s)[key].Value)).ToArray();
        expect.That(check is { Title: "Report · Pila P1/nord: fusto" } && check.Differences[0].Label == SharedDataCoordinator.FieldLabel(key) && check.Differences[0].Lines.SequenceEqual(lines)
            && check.Notices.SequenceEqual(ProjectValidation.Warnings(Section())) && check.HasNotices == check.Notices.Count > 0
            && SectionReportCheckViewModel.Heading == "Controllo prima del report" && SectionReportCheckViewModel.CompareLabel == "Apri confronto"
            && SectionReportCheckViewModel.GenerateLabel == "Genera con segnalazioni" && SectionReportCheckViewModel.CancelLabel == "Annulla",
            "Controllo prima del report: titolo, campo, una riga per foglio, avvisi, pulsanti di oggi");

        // «Apri confronto»: the comparison of the section, no report.
        CoherenceViewModel? comparison = null;
        dialogs.Then(d => { check = (SectionReportCheckViewModel)d; check.CompareCommand.Execute(null); }).Then(d => comparison = (CoherenceViewModel)d);
        expect.That(await Expect.Within(shell.ExportSectionReportAsync(Section())) is null && comparison is { Title: "Confronto · Pila P1/nord: fusto" } && reports.Written.Count == 1
            && files.Requests.Count == requests, "«Apri confronto»: il confronto della sezione, nessun report");

        // «Genera con segnalazioni» with a file not chosen: nothing written.
        dialogs.Then(d => ((SectionReportCheckViewModel)d).GenerateCommand.Execute(null));
        files.Then(null);
        expect.That(await Expect.Within(shell.ExportSectionReportAsync(Section())) is null && reports.Written.Count == 1, "Nessun file scelto: nessun report");

        // «Genera con segnalazioni», then sheets without results: the warning with the note of today; the progress shows the text of the report.
        reports.Unavailable = 2;
        reports.WaitForCancel = false;
        dialogs.Then(d => ((SectionReportCheckViewModel)d).GenerateCommand.Execute(null))
            .Then(d => { progress = (SectionReportProgressViewModel)d; closableWhileRunning = progress.CanClose(); });
        files.Then(target);
        written = await Expect.Within(shell.ExportSectionReportAsync(Section()));
        shown = bench.Messages.Shown[^1];
        expect.That(written == 2 && reports.Written.Count == 2 && shown.Warning && shown.Message.EndsWith("\n\n2 schede contengono risultati mancanti o incompleti, segnalati nel documento.", StringComparison.Ordinal),
            "Fogli senza risultati: avviso con la nota di oggi");
        expect.That(progress is { Title: "Generazione del report", IsFinished: true, Status: SectionReportProgressViewModel.Cancelling } && !closableWhileRunning && progress.IsCancelled
            && progress.CanClose(), "«Generazione del report»: la chiusura durante il lavoro annulla («Annullamento al termine del calcolo in corso…») e la finestra resta");
        // The closing while running asked the cancellation: the recording writer does not observe it, the report ends written (as MainWindow when the last chapter ends).

        // «Annulla» of the progress: the report stops at the end of the calculation under way, nothing shown.
        reports.Unavailable = 0;
        reports.WaitForCancel = true;
        int messages = bench.Messages.Shown.Count;
        dialogs.Then(d => ((SectionReportCheckViewModel)d).GenerateCommand.Execute(null))
            .Then(d =>
            {
                progress = (SectionReportProgressViewModel)d;
                progress.CancelCommand.Execute(null);
            });
        files.Then(target);
        expect.That(await Expect.Within(shell.ExportSectionReportAsync(Section())) is null && bench.Messages.Shown.Count == messages && progress is { IsCancelled: true, IsFinished: true }
            && progress.Status == SectionReportProgressViewModel.Cancelling && !progress.CancelCommand.CanExecute(null), "«Annulla»: «Annullamento al termine del calcolo in corso…», nessun messaggio");

        // An error of the writer goes to the guard (the Safe of MainWindow).
        reports.WaitForCancel = false;
        reports.Failure = new InvalidOperationException("Scrittura non riuscita");
        // The writer fails at once: the progress is not shown (the report ended before its dialog).
        dialogs.Then(d => ((SectionReportCheckViewModel)d).GenerateCommand.Execute(null));
        files.Then(target);
        await Expect.Within(shell.SectionReportCommand.ExecuteAsync(Section()));
        expect.That(bench.Messages.Failures.Count == 1 && bench.Messages.Failures[0].Error.Message == "Scrittura non riuscita", "Errore dello scrittore mostrato dalla guardia");
        bench.Messages.Failures.Clear();
        reports.Failure = null;

        // «Report Word della sezione» of the tree: the same command on the node.
        var node = shell.Tree.Find(Section())!;
        dialogs.Then(d => ((SectionReportCheckViewModel)d).GenerateCommand.Execute(null)).Then(_ => { });
        files.Then(target);
        int before = reports.Written.Count;
        expect.That(node.CanReport && node.ReportCommand.CanExecute(null), "Nodo della sezione: «Report Word della sezione» abilitato");
        node.ReportCommand.Execute(null);
        for (int i = 0; i < 100 && shell.SectionReportCommand.IsRunning; i++) await Task.Yield();
        expect.That(reports.Written.Count == before + 1 && bench.Messages.Failures.Count == 0 && dialogs.Shown.Count > 0, "«Report Word della sezione» dall'albero: il report della sezione");
        return expect.Count;
    }
}
