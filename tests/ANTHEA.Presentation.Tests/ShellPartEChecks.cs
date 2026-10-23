using Anthea.Calculations;
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
        Revisions(expect);

        // A change of the data of a sheet («Uniforma a questo» of the comparison) keeps the selection of the tree; a change of the structure selects its node.
        shell.Navigation.Select(ShellPage.Projects);
        shell.Tree.Find(section)!.IsSelected = true;
        project.ChangeData(() => { first["dati"]!["classe"] = "C45/55"; return true; }, first);
        expect.That(ReferenceEquals(shell.Tree.Selected?.Node, section), "Cambio dei dati di un foglio: la selezione dell'albero resta sulla sezione");
        var renamed = project.Find(second.S("id"))!;
        project.Rename(renamed, "Sezione rinominata");
        expect.That(ReferenceEquals(shell.Tree.Selected?.Node, renamed), "Cambio della struttura: il nodo cambiato selezionato");
        StatusAfterCommit(expect);
        return expect.Count;
    }

    /// <summary>
    /// ui/shell-smoke-hierarchy: the status line of the shared data follows the confirmation of the sheet with the focus (MainWindow.Commit refreshes it), also
    /// when the data reached the sheet without an edit of its view.
    /// </summary>
    private static void StatusAfterCommit(Expect expect)
    {
        var bench = new ShellBench(new ScriptedConfirmations().Then(false));
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var project = shell.Workspace.NewProject()!;
        var section = project.AddSection(project.AddProject()!)!;
        var first = project.AddSheet("mat_calcestruzzo", section)!;
        project.AddSheet("mat_calcestruzzo", section);
        var sheet = shell.Workspace.OpenSheet(first);
        expect.That(!shell.SharedStatus.Text.Contains("Dati diversi tra i fogli", StringComparison.Ordinal), "Fogli uguali: nessuna differenza nella riga di stato");
        sheet.ViewModel.Data["classe"] = "C40/50";
        shell.Workspace.CommitActive();
        expect.That(shell.SharedStatus.IsVisible && shell.SharedStatus.Text.Contains("Dati diversi tra i fogli", StringComparison.Ordinal),
            "Conferma con «Solo questo foglio»: la riga di stato mostra la differenza");
        // ui/shell-capture: «Uniforma a questo» from the sheet with the focus changes the other sheet; the status line follows the project.
        var other = section.Array("fogli").OfType<System.Text.Json.Nodes.JsonObject>().Single(s => !ReferenceEquals(s, sheet.Sheet));
        project.ChangeData(() => { other["dati"] = sheet.Sheet["dati"]!.DeepClone(); return true; }, other);
        expect.That(!(shell.SharedStatus.IsVisible && shell.SharedStatus.Text.Contains("Dati diversi tra i fogli", StringComparison.Ordinal)), "Dopo «Uniforma a questo» sull'altro foglio la riga di stato non mostra più la differenza");
    }

    /// <summary>
    /// ui/shell-smoke-project-workspace (CheckProjectRevisionNavigation): the sheet with the focus that has no node in the archived revision shown closes and
    /// the overview of the section of the revisions takes the centre (MainWindow shows the structure; before, the home page); back on the current version the
    /// sheet reopens with the focus (MainWindow keeps the sheet of the navigation). A page that ends the preview reopens it without the focus. Undo that removes
    /// the sheet with the focus: the notice, then the overview of its section.
    /// </summary>
    private static void Revisions(Expect expect)
    {
        var bench = new ShellBench(new ScriptedConfirmations());
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        var workspace = shell.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        string sectionId = section.S("id");
        var first = workspace.OpenSheet(project.AddSheet("mat_calcestruzzo", section)!);
        project.NewRevision(project.Find(sectionId)!, "Prima emissione");
        var added = workspace.OpenSheet(project.AddSheet("str_palo", project.Find(sectionId)!)!);
        string addedId = added.Id;
        expect.That(ReferenceEquals(workspace.Active, added) && shell.Center is SheetTabViewModel, "Foglio aggiunto dopo la revisione, con il fuoco");

        project.SwitchRevision(sectionId, 0);
        expect.That(workspace.Active is null && workspace.Sheets.All(s => s.Id != addedId) && shell.Center is SectionOverviewViewModel { Section: var shown } && shown?.S("id") == sectionId
            && shell.Navigation.Page == ShellPage.Projects && project.IsPreviewingRevision && workspace.SheetOf(project.Find(first.Id)!) is { IsReadOnly: true },
            "Revisione senza il foglio con il fuoco: scheda chiusa, riepilogo della sezione, gli altri fogli in sola lettura");
        project.SwitchRevision(sectionId, null);
        expect.That(workspace.Active is { } back && back.Id == addedId && !back.IsReadOnly && shell.Center is SheetTabViewModel { Session: var tab } && ReferenceEquals(tab, back),
            "Ritorno alla versione corrente: il foglio riaperto con il fuoco");

        project.SwitchRevision(sectionId, 0);
        shell.Navigation.Select(ShellPage.Home);
        expect.That(!project.IsPreviewingRevision && workspace.Sheets.Any(s => s.Id == addedId) && workspace.Active is null && shell.Center is NavigationViewModel,
            "Una pagina chiude l'anteprima: il foglio riaperto senza il fuoco, la pagina resta");

        // Undo of the addition of a sheet with the focus: the notice of the closed tab, then the overview of its section.
        var later = workspace.OpenSheet(project.AddSheet("geo_stratigrafia", project.Find(sectionId)!)!);
        int notices = bench.Messages.Shown.Count;
        shell.Commands.UndoCommand.Execute(null);
        expect.That(workspace.Active is null && workspace.Sheets.All(s => !ReferenceEquals(s, later)) && bench.Messages.Shown.Count == notices + 1
            && shell.Center is SectionOverviewViewModel { Section: var owner } && owner?.S("id") == sectionId, "Annulla toglie il foglio con il fuoco: avviso e riepilogo della sezione");
    }
}
