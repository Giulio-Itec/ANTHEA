using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 b5: tabs and windows without WPF (proposal 4.1 and 4.4, Q10 a): a tab for every open sheet in the window with the focus; the
/// selection gives the focus to the sheet and confirms the previous one, also across windows; «Sposta in una nuova finestra» keeps the
/// same ViewModel; «Riaggancia»; closing a secondary window brings its tabs back; a window left empty closes; the Wiki in one tab; titles
/// and «•» follow the sheets.
/// </summary>
internal static class TabsChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private sealed class RecordingWindows : IWindowService
    {
        internal List<(string Action, TabHostViewModel Host)> Calls { get; } = [];
        public void Open(TabHostViewModel host) => Calls.Add(("apri", host));
        public void Close(TabHostViewModel host) => Calls.Add(("chiudi", host));
        public void Activate(TabHostViewModel host) => Calls.Add(("attiva", host));
    }

    private static int RunAll()
    {
        var expect = new Expect();
        var bench = new ShellBench(new ScriptedConfirmations().Then(SaveChangesAnswer.Discard));
        var workspace = bench.Workspace;
        var windows = new RecordingWindows();
        var early = workspace.NewCalculation("str_palo");
        using var shell = new ShellWindowsViewModel(workspace, windows, bench.Messages);
        var earlyTab = shell.TabOf(early)!;
        expect.That(shell.Main.IsMain && shell.Main.Tabs.Single() == earlyTab && earlyTab.IsSelected && earlyTab.IsActive && ReferenceEquals(earlyTab.Host, shell.Main)
            && !earlyTab.IsDetached && earlyTab.Kind == ShellTabKind.Sheet && earlyTab.Title == early.Heading && ReferenceEquals(earlyTab.Content, early.ViewModel), "Scheda di un foglio già aperto");

        // New sheets open in the window with the focus and are selected.
        var second = workspace.NewCalculation("mat_calcestruzzo");
        var secondTab = shell.TabOf(second)!;
        expect.That(shell.Main.Tabs.Count == 2 && ReferenceEquals(shell.Main.Selected, secondTab) && secondTab.IsActive && !earlyTab.IsActive && !earlyTab.IsSelected, "Nuova scheda");

        // Selecting a tab gives the focus to its sheet and confirms the previous one (Q9 a).
        ShellBench.Probe(second).Note = "bozza";
        shell.SelectCommand.Execute(earlyTab);
        expect.That(ReferenceEquals(workspace.Active, early) && second.Sheet["dati"].S("nota") == "bozza" && earlyTab.IsActive && ReferenceEquals(shell.Main.Selected, earlyTab), "Selezione di una scheda");

        // «Sposta in una nuova finestra»: the same ViewModel, a new window, the focus there.
        var model = second.ViewModel;
        shell.MoveToNewWindowCommand.Execute(secondTab);
        var detached = shell.Detached.Single();
        expect.That(!detached.IsMain && detached.Tabs.Single() == secondTab && secondTab.IsDetached && ReferenceEquals(secondTab.Content, model)
            && shell.Main.Tabs.Single() == earlyTab && ReferenceEquals(shell.ActiveHost, detached) && ReferenceEquals(workspace.Active, second)
            && windows.Calls[0] == ("apri", detached), "Scheda staccata");
        expect.That(shell.ReattachCommand.CanExecute(secondTab) && !shell.ReattachCommand.CanExecute(earlyTab), "Riaggancia disponibile solo per le schede staccate");

        // A new sheet opens in the detached window, which has the focus (SH19); the focus moves across windows with a confirmation.
        var third = workspace.NewCalculation("str_palo");
        var thirdTab = shell.TabOf(third)!;
        expect.That(ReferenceEquals(thirdTab.Host, detached) && detached.Tabs.Count == 2, "Nuova scheda nella finestra con il fuoco");
        ShellBench.Probe(third).Note = "altra finestra";
        shell.WindowActivated(shell.Main);
        expect.That(ReferenceEquals(workspace.Active, early) && third.Sheet["dati"].S("nota") == "altra finestra" && ReferenceEquals(shell.ActiveHost, shell.Main), "Fuoco a un'altra finestra");

        // «Riaggancia», then a window left without tabs closes (SH20).
        shell.ReattachCommand.Execute(thirdTab);
        expect.That(ReferenceEquals(thirdTab.Host, shell.Main) && detached.Tabs.Single() == secondTab && ReferenceEquals(workspace.Active, third), "Riaggancia");
        shell.ReattachCommand.Execute(secondTab);
        expect.That(shell.Detached.Count == 0 && windows.Calls.Contains(("chiudi", detached)) && shell.Main.Tabs.Count == 3 && ReferenceEquals(shell.ActiveHost, shell.Main), "Finestra vuota chiusa");

        // Closing a secondary window brings its tabs back, open.
        var other = shell.MoveToNewWindow(thirdTab);
        shell.WindowClosed(other);
        expect.That(shell.Detached.Count == 0 && ReferenceEquals(thirdTab.Host, shell.Main) && !third.IsDisposed && shell.Main.Tabs.Count == 3, "Chiusura di una finestra secondaria");

        // Titles and «•» follow the sheets; closing a tab closes its sheet (a single file asks to save it).
        var project = workspace.NewProject()!;
        var section = project.AddSection(project.AddProject()!)!;
        var sheet = project.AddSheet("str_palo", section)!;
        var projectTab = shell.TabOf(workspace.OpenSheet(sheet))!;
        var titles = new List<string?>();
        projectTab.PropertyChanged += (_, e) => titles.Add(e.PropertyName);
        project.Rename(sheet, "Verifica pila");
        expect.That(projectTab.Title == "Verifica pila" && titles.Contains(nameof(ShellTabViewModel.Title)), "Titolo dopo la rinomina");
        ShellBench.Probe(projectTab.Session).Label = "modificato";
        shell.SelectCommand.Execute(earlyTab);
        expect.That(projectTab.IsUnsaved && titles.Contains(nameof(SheetTabViewModel.IsUnsaved)), "Marcatore del foglio non salvato");
        ShellBench.Probe(early).Label = "x";
        shell.CloseCommand.Execute(earlyTab);
        expect.That(early.IsDisposed && shell.TabOf(early) is null && !shell.Main.Tabs.Contains(earlyTab) && bench.Confirmations.Asked.Single() == "salvataggio", "Chiusura della scheda di un file singolo");
        bench.Confirmations.Then(true);
        project.Delete(sheet, out _);
        expect.That(shell.TabOf(projectTab.Session) is null && !shell.Main.Tabs.Contains(projectTab), "Scheda del foglio eliminato");

        // The Wiki in one tab (Q18 a): a page, not a sheet; the sheets lose the focus.
        var wiki = shell.ShowWiki("contenuto");
        expect.That(wiki.Kind == ShellTabKind.Wiki && wiki.Title == "Wiki" && ReferenceEquals(shell.ShowWiki(null), wiki) && workspace.Active is null && wiki.IsActive
            && shell.Main.Tabs.Count(t => t.Kind == ShellTabKind.Wiki) == 1, "Scheda della Wiki");
        expect.That(shell.Close(wiki) && !shell.Tabs.Contains(wiki) && shell.ShowWiki(null) is { } again && !ReferenceEquals(again, wiki), "Chiusura della Wiki");
        expect.Throws<ArgumentException>(() => _ = new PageTabViewModel(ShellTabKind.Sheet, "x", null), "Pagina come foglio");
        expect.That(ShellWindowsViewModel.MoveLabel == "Sposta in una nuova finestra" && ShellWindowsViewModel.ReattachLabel == "Riaggancia", "Etichette");
        return expect.Count;
    }
}
