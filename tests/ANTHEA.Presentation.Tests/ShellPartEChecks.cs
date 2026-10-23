using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 e: what the suites of MainWindow transferred to the new shell found in the shell (docs/refactoring/f5-13-shell.md, «Parte (e)»), checked here
/// without WPF. The activation of the main window while it shows a page of the navigation keeps the page: before, it selected the tab under the page
/// again, the sheet took the focus and the tree was rebuilt (ui/shell-smoke-projects: the rename in place ended when the rename box took the focus).
/// </summary>
internal static class ShellPartEChecks
{
    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    internal static int Run()
    {
        var expect = new Expect();
        var bench = new ShellBench();
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var workspace = shell.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        var first = project.AddSheet("mat_calcestruzzo", section)!;
        var second = project.AddSheet("str_palo", section)!;

        // A sheet in its tab, then the page «Progetti»: the main window activated keeps the page, no sheet takes the focus, the tree is not rebuilt.
        var sheet = workspace.OpenSheet(first);
        expect.That(!shell.Windows.MainShowsPage && shell.Center is SheetTabViewModel, "Foglio a schermo: la principale non mostra una pagina");
        shell.Navigation.Select(ShellPage.Projects);
        var node = shell.Tree.Find(section)!;
        expect.That(shell.Windows.MainShowsPage && workspace.Active is null && shell.Center is SectionOverviewViewModel, "Pagina Progetti: la principale mostra una pagina");
        shell.Windows.WindowActivated(shell.Windows.Main);
        expect.That(workspace.Active is null && shell.Center is SectionOverviewViewModel && ReferenceEquals(shell.Tree.Find(section), node) && ReferenceEquals(shell.Windows.ActiveHost, shell.Windows.Main),
            "Attivazione della principale sulla pagina: resta la pagina, nessun foglio con il fuoco, albero non ricostruito");

        // On its tab the activation gives the focus to the sheet of the tab, as before.
        shell.Windows.Select(shell.Windows.TabOf(sheet)!);
        shell.Windows.WindowActivated(shell.Windows.Main);
        expect.That(ReferenceEquals(workspace.Active, sheet) && !shell.Windows.MainShowsPage && shell.Center is SheetTabViewModel { Session: var shown } && ReferenceEquals(shown, sheet),
            "Attivazione della principale sulla scheda: il suo foglio con il fuoco");

        // A detached window activated gives the focus to its sheet as before (the page of the main window gives way to its tab, as for any sheet with the
        // focus); the main window activated then gives it to its own tab.
        var other = workspace.OpenSheet(second);
        var detached = shell.Windows.MoveToNewWindow(shell.Windows.TabOf(other)!);
        shell.Navigation.Select(ShellPage.Home);
        ShellBench.Probe(other).Label = "da confermare";
        shell.Windows.WindowActivated(detached);
        expect.That(ReferenceEquals(workspace.Active, other) && !shell.Windows.MainShowsPage, "Finestra staccata attivata: il suo foglio con il fuoco");
        shell.Windows.WindowActivated(shell.Windows.Main);
        expect.That(ReferenceEquals(workspace.Active, sheet) && other.Sheet["dati"]?["etichetta"]?.ToString() == "da confermare",
            "Principale attivata: il foglio della sua scheda con il fuoco, quello staccato confermato");
        return expect.Count;
    }
}
