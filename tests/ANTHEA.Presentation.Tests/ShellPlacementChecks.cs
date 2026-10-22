using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 c, after the user's checkpoint of 22/10 («dall'albero si possono aprire a pagina intera o staccare dalla finestra?»), without WPF:
/// <list type="bullet">
/// <item>«Apri in una nuova finestra» of a sheet of the tree: the sheet opens, or its open tab is taken with the same ViewModel, in a new detached
/// window shown once; a sheet already in a detached window brings that window to the front, without a new one;</item>
/// <item>«Apri a pagina intera» and the toggle of the sheet bar: navigation and tree hidden, «Esci da pagina intera» and Esc back with the tree as
/// it was, the pages of the navigation, «← Torna al progetto», a page tab and the closing of the sheet end it (no hidden state); a sheet in a
/// detached window brings that window to the front and the main window does not change;</item>
/// <item>the sheets of an archived revision still open both ways, read-only.</item>
/// </list>
/// </summary>
internal static class ShellPlacementChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    /// <summary>A detached window that records the calls; <see cref="Show"/> activates it, as a WPF window shown with ShowActivated.</summary>
    private sealed class RecordingWindow(List<string> log) : IShellWindow
    {
        public bool IsActive { get; private set; }
        public event EventHandler? Activated;
        public event EventHandler? Closed;

        public void Show() { log.Add("mostra"); UserActivates(); }
        public void Activate() { log.Add("attiva"); UserActivates(); }
        public void Close() { log.Add("chiudi"); IsActive = false; Closed?.Invoke(this, EventArgs.Empty); }

        /// <summary>The user clicks the window.</summary>
        internal void UserActivates() { IsActive = true; Activated?.Invoke(this, EventArgs.Empty); }

        /// <summary>The focus goes to another window.</summary>
        internal void Deactivate() => IsActive = false;
    }

    private static int RunAll()
    {
        var expect = new Expect();
        var bench = new ShellBench();
        var log = new List<string>();
        var created = new List<RecordingWindow>();
        var tracker = new ShellWindowTracker(_ => { var window = new RecordingWindow(log); created.Add(window); return window; });
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), tracker, new SheetFileExports());
        tracker.Attach(shell.Windows);
        var workspace = shell.Workspace;
        var tree = shell.Tree;

        // A bridge: an abutment with two sheets, a pier with one.
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var abutment = project.AddSection(root)!;
        var pier = project.AddSection(root)!;
        var concrete = project.AddSheet("mat_calcestruzzo", abutment)!;
        var piles = project.AddSheet("geo_palo_verticale", abutment)!;
        var column = project.AddSheet("str_palo", pier)!;

        expect.That(ProjectTreeViewModel.OpenInNewWindowLabel == "Apri in una nuova finestra" && ProjectTreeViewModel.OpenFullPageLabel == "Apri a pagina intera"
            && ShellViewModel.FullPageLabel == "Pagina intera" && ShellViewModel.ExitFullPageLabel == "Esci da pagina intera"
            && ShellViewModel.ExitFullPageToolTip.EndsWith("(Esc)", StringComparison.Ordinal), "Etichette del menu del foglio e della barra");
        var concreteNode = tree.Find(concrete)!;
        expect.That(concreteNode.IsSheet && concreteNode.OpenInNewWindowCommand.CanExecute(null) && concreteNode.OpenFullPageCommand.CanExecute(null)
            && !tree.Find(abutment)!.OpenInNewWindowCommand.CanExecute(null) && !tree.Find(abutment)!.OpenFullPageCommand.CanExecute(null)
            && !tree.Roots[0].IsSheet && !tree.Roots[0].OpenFullPageCommand.CanExecute(null), "Solo i nodi dei fogli si aprono in una nuova finestra o a pagina intera");
        expect.That(!shell.IsFullPage && !shell.CanEnterFullPage && !shell.ToggleFullPageCommand.CanExecute(null) && !shell.EnterFullPageCommand.CanExecute(null)
            && !shell.ExitFullPageCommand.CanExecute(null) && shell.FullPageText == ShellViewModel.FullPageLabel && shell.FullPageToolTipText == ShellViewModel.FullPageToolTip,
            "Senza un foglio: nessuna pagina intera, Esc non usato");

        // «Apri in una nuova finestra» on a sheet not open: it opens, its tab goes to a new window shown and active, once.
        concreteNode.OpenInNewWindowCommand.Execute(null);
        var concreteSheet = workspace.SheetOf(concrete)!;
        var concreteTab = shell.Windows.TabOf(concreteSheet)!;
        var window = created.Single();
        var host = shell.Windows.Detached.Single();
        expect.That(workspace.Sheets.Count == 1 && concreteTab.IsDetached && host.Tabs.Single() == concreteTab && !shell.Windows.Main.Tabs.Contains(concreteTab)
            && ReferenceEquals(workspace.Active, concreteSheet) && concreteTab.IsActive && ReferenceEquals(tracker.WindowOf(host), window) && window.IsActive
            && log.SequenceEqual(["mostra"]) && bench.Messages.Failures.Count == 0, "«Apri in una nuova finestra»: il foglio aperto in una finestra staccata, mostrata e attiva una volta");

        // The same sheet again while the main window has the focus: its window comes to the front, no new window, the sheet that had the focus confirmed.
        tracker.MainActivated();
        window.Deactivate();
        tree.Find(piles)!.OpenCommand.Execute(null);
        var pilesSheet = workspace.SheetOf(piles)!;
        var pilesTab = shell.Windows.TabOf(pilesSheet)!;
        expect.That(ReferenceEquals(pilesTab.Host, shell.Windows.Main) && ReferenceEquals(workspace.Active, pilesSheet), "«Apri»: il foglio dei pali nella principale");
        ShellBench.Probe(pilesSheet).Note = "bozza dei pali";
        tree.Find(concrete)!.OpenInNewWindowCommand.Execute(null);
        expect.That(created.Count == 1 && shell.Windows.Detached.Single() == host && log.SequenceEqual(["mostra", "attiva"]) && window.IsActive
            && ReferenceEquals(workspace.Active, concreteSheet) && workspace.Sheets.Count == 2 && pilesSheet.Sheet["dati"].S("nota") == "bozza dei pali",
            "Foglio già in una finestra staccata: quella finestra in primo piano, nessuna finestra nuova, il foglio che aveva il fuoco confermato");

        // A sheet open in the main window: the same tab, with the same ViewModel, in a new window.
        tracker.MainActivated();
        window.Deactivate();
        var pilesModel = pilesSheet.ViewModel;
        tree.Find(piles)!.OpenInNewWindowCommand.Execute(null);
        expect.That(created.Count == 2 && workspace.Sheets.Count == 2 && ReferenceEquals(shell.Windows.TabOf(pilesSheet), pilesTab) && ReferenceEquals(pilesTab.Content, pilesModel)
            && pilesTab.IsDetached && !ReferenceEquals(pilesTab.Host, host) && shell.Windows.Main.Tabs.Count == 0 && ReferenceEquals(workspace.Active, pilesSheet)
            && created[1].IsActive, "Foglio aperto nella principale: la stessa scheda, con lo stesso ViewModel, in una finestra nuova");
        expect.Throws<ArgumentException>(() => shell.Place(new ShellBench().Workspace.NewCalculation("str_palo"), SheetPlacement.NewWindow), "Foglio di un'altra shell rifiutato");

        // Back in the main window for the full page.
        shell.Windows.Reattach(pilesTab);
        shell.Windows.Reattach(concreteTab);
        expect.That(shell.Windows.Detached.Count == 0 && tracker.Hosts.Count == 0 && shell.Windows.Main.Tabs.Count == 2 && ReferenceEquals(shell.Windows.ActiveHost, shell.Windows.Main),
            "Riagganciate le due schede, finestre staccate chiuse");

        // «Apri a pagina intera» on a sheet not open: it opens in the main window, navigation and tree hidden, «Esci da pagina intera» on the bar.
        expect.That(shell.IsTreeOpen, "Albero aperto prima della pagina intera");
        tree.Find(column)!.OpenFullPageCommand.Execute(null);
        var columnSheet = workspace.SheetOf(column)!;
        var columnTab = shell.Windows.TabOf(columnSheet)!;
        expect.That(shell.IsFullPage && !shell.IsTreeOpen && ReferenceEquals(shell.Center, columnTab) && ReferenceEquals(columnTab.Host, shell.Windows.Main)
            && ReferenceEquals(workspace.Active, columnSheet) && shell.Navigation.Page is null && shell.SheetBar.IsVisible
            && shell.FullPageText == ShellViewModel.ExitFullPageLabel && shell.FullPageToolTipText == ShellViewModel.ExitFullPageToolTip
            && shell.ExitFullPageCommand.CanExecute(null) && shell.ToggleFullPageCommand.CanExecute(null) && !shell.EnterFullPageCommand.CanExecute(null),
            "«Apri a pagina intera»: il foglio al centro, navigazione e albero nascosti, «Esci da pagina intera» nella barra");

        // Esc: back, the tree as before, the same sheet in the centre; outside the full page Esc does nothing.
        shell.ExitFullPageCommand.Execute(null);
        expect.That(!shell.IsFullPage && shell.IsTreeOpen && ReferenceEquals(shell.Center, columnTab) && ReferenceEquals(workspace.Active, columnSheet)
            && shell.FullPageText == ShellViewModel.FullPageLabel && !shell.ExitFullPageCommand.CanExecute(null) && shell.CanEnterFullPage, "Esc: navigazione e albero di nuovo, lo stesso foglio");

        // The toggle of the bar, with the tree closed before: «▸» opens it meanwhile, the exit closes it again as it was.
        shell.ToggleTreeCommand.Execute(null);
        shell.ToggleFullPageCommand.Execute(null);
        expect.That(shell.IsFullPage && !shell.IsTreeOpen && ReferenceEquals(shell.Center, columnTab), "«Pagina intera» della barra del foglio");
        shell.ToggleTreeCommand.Execute(null);
        expect.That(shell.IsFullPage && shell.IsTreeOpen, "«▸» apre l'albero anche a pagina intera");
        shell.ToggleFullPageCommand.Execute(null);
        expect.That(!shell.IsFullPage && !shell.IsTreeOpen, "«Esci da pagina intera»: l'albero chiuso com'era prima");
        shell.ToggleTreeCommand.Execute(null);

        // A sheet already open: its tab at full page, nothing reopened; another sheet from the tree at full page closes the tree again.
        tree.Find(concrete)!.OpenFullPageCommand.Execute(null);
        expect.That(workspace.Sheets.Count == 3 && ReferenceEquals(shell.Center, concreteTab) && ReferenceEquals(workspace.Active, concreteSheet) && shell.IsFullPage && !shell.IsTreeOpen,
            "Foglio già aperto: la sua scheda a pagina intera");
        shell.ToggleTreeCommand.Execute(null);
        tree.Find(piles)!.OpenFullPageCommand.Execute(null);
        expect.That(shell.IsFullPage && !shell.IsTreeOpen && ReferenceEquals(shell.Center, pilesTab), "Un altro foglio a pagina intera dall'albero aperto con «▸»: l'albero si richiude");
        shell.ExitFullPage();
        expect.That(shell.IsTreeOpen, "Uscita: l'albero aperto come prima della prima pagina intera");

        // The pages of the navigation end the full page (no hidden navigation); the tree as before.
        foreach (var page in new[] { ShellPage.Home, ShellPage.Projects, ShellPage.Wiki, ShellPage.Modules, ShellPage.Models, ShellPage.Examples })
        {
            shell.Windows.Select(concreteTab);
            expect.That(shell.EnterFullPage() && shell.IsFullPage, "Pagina intera prima di " + page);
            shell.Navigation.Select(page);
            expect.That(!shell.IsFullPage && shell.IsTreeOpen && !ReferenceEquals(shell.Center, concreteTab), "La pagina " + page + " esce dalla pagina intera, l'albero com'era");
        }

        // «← Torna al progetto», a page tab and the closing of the sheet end it too.
        shell.Windows.Select(concreteTab);
        shell.EnterFullPage();
        shell.SheetBar.BackCommand.Execute(null);
        expect.That(!shell.IsFullPage && shell.Navigation.Page == ShellPage.Projects && ReferenceEquals(shell.Center, shell.Overview), "«← Torna al progetto» esce dalla pagina intera");
        shell.Windows.Select(concreteTab);
        shell.EnterFullPage();
        var wikiTab = shell.Windows.Pages.Single(p => p.Kind == ShellTabKind.Wiki);
        shell.Windows.Select(wikiTab);
        expect.That(!shell.IsFullPage && ReferenceEquals(shell.Center, wikiTab) && !shell.CanEnterFullPage, "Una scheda senza foglio (Wiki) esce dalla pagina intera");
        shell.Windows.Close(wikiTab);
        shell.Windows.Select(columnTab);
        shell.EnterFullPage();
        shell.Windows.Close(columnTab);
        expect.That(!shell.IsFullPage && workspace.SheetOf(column) is null && shell.IsTreeOpen, "Chiusa la scheda a pagina intera: si esce, nessuno stato nascosto");

        // A sheet in a detached window: «Apri a pagina intera» brings its window to the front, the main window does not change.
        shell.Windows.Select(concreteTab);
        var detached = shell.Windows.MoveToNewWindow(concreteTab);
        var detachedWindow = created[^1];
        tracker.MainActivated();
        detachedWindow.Deactivate();
        int logged = log.Count;
        tree.Find(concrete)!.OpenFullPageCommand.Execute(null);
        expect.That(!shell.IsFullPage && shell.IsTreeOpen && ReferenceEquals(workspace.Active, concreteSheet) && ReferenceEquals(concreteTab.Host, detached)
            && log.Skip(logged).SequenceEqual(["attiva"]) && ReferenceEquals(shell.Center, pilesTab), "Foglio staccato «a pagina intera»: la sua finestra in primo piano, niente pagina intera");
        expect.That(shell.SheetBar.IsVisible && !shell.CanEnterFullPage && !shell.ToggleFullPageCommand.CanExecute(null), "Barra della principale con il foglio staccato: «Pagina intera» non disponibile");

        // Full page in the main window, then the focus to the detached window: the main window stays at full page.
        tracker.MainActivated();
        detachedWindow.Deactivate();
        expect.That(shell.EnterFullPage() && ReferenceEquals(shell.Center, pilesTab), "Pagina intera nella principale con una finestra staccata");
        detachedWindow.UserActivates();
        expect.That(shell.IsFullPage && ReferenceEquals(shell.Center, pilesTab) && ReferenceEquals(workspace.Active, concreteSheet) && shell.ExitFullPageCommand.CanExecute(null),
            "Fuoco alla finestra staccata: la principale resta a pagina intera, «Esci» disponibile");
        shell.ExitFullPage();
        shell.Windows.Reattach(concreteTab);

        // An archived revision of the abutment (its snapshot holds the abutment and its ancestors): the sheets still open both ways, read-only.
        shell.Windows.Close(concreteTab);
        project.NewRevision(project.Find(abutment.S("id"))!, "Prima emissione");
        expect.That(project.SwitchRevision(abutment.S("id"), 0) && project.IsReadOnly && tree.IsReadOnly, "Revisione 0 in sola lettura");
        var archivedPiles = tree.Find(project.Find(piles.S("id"))!)!;
        var archivedConcrete = tree.Find(project.Find(concrete.S("id"))!)!;
        expect.That(!archivedPiles.CanRename && archivedPiles.OpenInNewWindowCommand.CanExecute(null) && archivedPiles.OpenFullPageCommand.CanExecute(null)
            && archivedConcrete.OpenInNewWindowCommand.CanExecute(null), "In sola lettura i fogli si aprono ancora in una nuova finestra e a pagina intera");
        archivedPiles.OpenFullPageCommand.Execute(null);
        expect.That(shell.IsFullPage && pilesSheet.IsReadOnly && ReferenceEquals(workspace.Active, pilesSheet) && ReferenceEquals(shell.Center, pilesTab)
            && pilesSheet.Heading.EndsWith(" · sola lettura", StringComparison.Ordinal), "Foglio aperto della revisione a pagina intera, in sola lettura");
        tree.Find(project.Find(concrete.S("id"))!)!.OpenInNewWindowCommand.Execute(null);
        var archivedConcreteSheet = workspace.SheetOf(project.Find(concrete.S("id"))!)!;
        expect.That(archivedConcreteSheet.IsReadOnly && shell.Windows.TabOf(archivedConcreteSheet)!.IsDetached && ReferenceEquals(workspace.Active, archivedConcreteSheet)
            && bench.Messages.Failures.Count == 0, "Foglio chiuso della revisione in una nuova finestra, in sola lettura");
        shell.ExitFullPage();
        expect.That(project.ExitPreview() && !pilesSheet.IsReadOnly && !archivedConcreteSheet.IsReadOnly && !shell.IsFullPage, "Uscita dalla revisione");
        return expect.Count;
    }
}
