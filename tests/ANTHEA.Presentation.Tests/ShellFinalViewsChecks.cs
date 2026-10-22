using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 c, after the checkpoint of 22/10: the pieces of the final views without WPF. Ctrl+Z and Ctrl+Y of the project where MainWindow showed
/// the project workspace (ProjectWorkspace.PreviewKeyDown); the links of «Fogli e sottosezioni» and «Duplica sezione» of the overview
/// (ProjectPresentation.BuildSectionOverview, DuplicateProjectSection); the Wiki of the services that opens the tab of the shell.
/// </summary>
internal static class ShellFinalViewsChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    private sealed class RecordingWiki : IWikiNavigator
    {
        internal List<string?> Shown { get; } = [];
        public void Show(string? uri = null) => Shown.Add(uri);
    }

    private static int RunAll()
    {
        var expect = new Expect();
        var bench = new ShellBench(new ScriptedConfirmations().Then(SaveChangesAnswer.Discard));
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var workspace = shell.Workspace;

        // Ctrl+Z / Ctrl+Y: not without a project, not on a single file or on the home page.
        var single = workspace.NewCalculation("str_palo");
        expect.That(!shell.HistoryKeysApply(shell.Center) && !shell.HistoryKeysApply(shell.Navigation) && !shell.HistoryKeysApply(null), "Ctrl+Z senza progetto: nessun effetto");
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        var sheet = project.AddSheet("mat_calcestruzzo", section)!;
        shell.ShowOverview(section);
        expect.That(shell.HistoryKeysApply(shell.Center) && shell.Center is SectionOverviewViewModel, "Ctrl+Z sulla pagina Progetti");
        var projectSheet = workspace.OpenSheet(sheet);
        expect.That(shell.HistoryKeysApply(shell.Center) && shell.Center is SheetTabViewModel, "Ctrl+Z su un foglio del progetto");
        workspace.Activate(single);
        expect.That(!shell.HistoryKeysApply(shell.Center), "Ctrl+Z su un file singolo: nessun effetto");
        shell.Navigation.Select(ShellPage.Modules);
        expect.That(!shell.HistoryKeysApply(shell.Center), "Ctrl+Z sui Moduli singoli: nessun effetto");

        // Ctrl+Z restores the rename, Ctrl+Y redoes it; with nothing to undo nothing happens.
        project.Rename(sheet, "Calcestruzzo della pila");
        shell.RestoreHistory(false);
        expect.That(project.Find(sheet.S("id"))!.S("nome") != "Calcestruzzo della pila" && project.CanRedo, "Ctrl+Z annulla la rinomina");
        shell.RestoreHistory(true);
        expect.That(project.Find(sheet.S("id"))!.S("nome") == "Calcestruzzo della pila" && !project.CanRedo && !projectSheet.IsDisposed, "Ctrl+Y la ripristina");
        int failures = bench.Messages.Failures.Count;
        shell.RestoreHistory(true);
        expect.That(project.Find(sheet.S("id"))!.S("nome") == "Calcestruzzo della pila" && bench.Messages.Failures.Count == failures, "Ctrl+Y senza nulla da ripristinare: nessun effetto, nessun errore");

        // The preview of a revision is read-only: Ctrl+Z does nothing there.
        var live = project.Find(section.S("id"))!;
        project.NewRevision(live, "Prima emissione");
        project.SwitchRevision(live.S("id"), 0);
        shell.ShowOverview(project.Find(live.S("id")));
        expect.That(!shell.HistoryKeysApply(shell.Center) && !shell.DuplicateSectionCommand.CanExecute(project.Find(live.S("id"))), "Revisione in sola lettura: niente Ctrl+Z né «Duplica sezione»");
        project.ExitPreview();

        // The links of the overview: a sheet opens in its tab, a subsection shows its overview.
        var current = project.Find(section.S("id"))!;
        var sub = project.AddSection(current)!;
        shell.ShowOverview(current);
        var overview = shell.Overview!;
        expect.That(overview.Links.Count == 2 && overview.Links[0].ContainsKey("modulo_id") && ReferenceEquals(overview.Links[1], sub), "Fogli e sottosezioni: prima i fogli");
        expect.That(overview.HasLinks && overview.LinkItems.Select(l => (l.Name, l.IsSheet)).SequenceEqual([(overview.Links[0].S("nome"), true), (sub.S("nome"), false)])
            && SectionOverviewViewModel.LinksTitle == "Fogli e sottosezioni" && SectionOverviewViewModel.DuplicateLabel == "Duplica sezione"
            && SectionOverviewViewModel.ReportLabel == "Genera report", "Pulsanti dei link con i nomi di oggi, etichette del riepilogo");
        expect.That(overview.FirstCommonData.SequenceEqual(overview.CommonData.Take(SectionOverviewViewModel.FirstRows))
            && overview.MoreCommonData.SequenceEqual(overview.CommonData.Skip(SectionOverviewViewModel.FirstRows)) && overview.HasMoreCommonData == overview.CommonData.Count > SectionOverviewViewModel.FirstRows
            && SectionOverviewViewModel.MoreCommonDataTitle == "Tutti i dati comuni", "Dati comuni: i primi cinque, gli altri in «Tutti i dati comuni»");
        int sheets = workspace.Sheets.Count;
        shell.OpenLinkCommand.Execute(overview.Links[0]);
        expect.That(ReferenceEquals(workspace.Active, projectSheet) && ReferenceEquals(shell.Center, shell.Windows.TabOf(projectSheet)) && workspace.Sheets.Count == sheets,
            "Link di un foglio già aperto: la sua scheda");
        shell.OpenLinkCommand.Execute(sub);
        expect.That(shell.Navigation.Page == ShellPage.Projects && ReferenceEquals(shell.Overview?.Section, sub) && workspace.Active is null, "Link di una sottosezione: il suo riepilogo");

        // «Duplica sezione»: the copy after the section, selected in the tree and shown.
        shell.ShowOverview(current);
        var copy = shell.DuplicateSection(current)!;
        var siblings = (JsonArray)current.Parent!;
        expect.That(copy.S("nome").StartsWith(current.S("nome") + " copia", StringComparison.Ordinal) && siblings.IndexOf(copy) == siblings.IndexOf(current) + 1 && ReferenceEquals(shell.Overview?.Section, copy)
            && ReferenceEquals(shell.Tree.Selected?.Node, copy) && project.IsDirty, "«Duplica sezione»: la copia dopo la sezione, nel riepilogo e nell'albero");
        expect.That(shell.DuplicateSectionCommand.CanExecute(copy) && !shell.DuplicateSectionCommand.CanExecute(null) && !shell.OpenLinkCommand.CanExecute(null), "Comandi del riepilogo");

        // The Wiki of the services: before the shell the historic window, then the tab of the shell.
        var historic = new RecordingWiki();
        var navigator = new ShellWikiNavigator(historic);
        navigator.Show("pali");
        expect.That(historic.Shown.SequenceEqual(["pali"]), "Wiki prima della shell: la finestra storica");
        navigator.Attach(shell);
        navigator.Show("muri");
        expect.That(historic.Shown.Count == 1 && shell.Windows.Pages.Single(p => p.Kind == ShellTabKind.Wiki).Content is WikiTabViewModel { Uri: "muri" }
            && shell.Navigation.Page is null, "Wiki con la shell: la sua scheda sull'articolo");
        using var other = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.GetTempPath()), new NoWindows(), new SheetFileExports());
        expect.Throws<InvalidOperationException>(() => navigator.Attach(other), "Seconda shell rifiutata");
        shell.Dispose();
        navigator.Show("ponti");
        expect.That(historic.Shown.SequenceEqual(["pali", "ponti"]), "Shell chiusa: di nuovo la finestra storica");
        return expect.Count;
    }
}
