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
        return expect.Count + QuickSections() + Reorder();
    }

    private static int Reorder()
    {
        var expect = new Expect(); var bench = new ShellBench();
        using var workspace = bench.Workspace; var windows = new RecordingWindows();
        using var shell = new ShellWindowsViewModel(workspace, windows, bench.Messages);
        var first = workspace.NewCalculation("mat_calcestruzzo"); var second = workspace.NewCalculation("str_palo");
        var a = shell.TabOf(first)!; var b = shell.TabOf(second)!;
        var wiki = shell.ShowWiki(null); var model = shell.ShowPage(new PageTabViewModel(ShellTabKind.Model, "Modello", null));
        shell.Select(a); var instance = first.ViewModel; string data = first.ViewModel.Data.ToJsonString();
        first.ViewModel.IsReadOnly = true;
        bool dirty = first.Owner.IsDirty; long revision = first.ViewModel.Revision; int calls = windows.Calls.Count, changes = 0;
        shell.Main.Tabs.CollectionChanged += (_, _) => changes++;
        expect.That(shell.Reorder(a, wiki, true) && shell.Main.Tabs.SequenceEqual([b, wiki, a, model]), "Ordine schede: spostamento verso destra oltre una pagina Wiki");
        expect.That(shell.Reorder(model, b, false) && shell.Main.Tabs.SequenceEqual([model, b, wiki, a]), "Ordine schede: pagina Modello verso sinistra");
        expect.That(shell.Main.Selected == a && workspace.Active == first && a.IsActive && a.IsSelected && ReferenceEquals(first.ViewModel, instance)
            && first.ViewModel.Data.ToJsonString() == data && first.ViewModel.Revision == revision && first.Owner.IsDirty == dirty && windows.Calls.Count == calls,
            "Ordine schede: selezione, focus, ViewModel, dati, revisione e salvataggio invariati");
        expect.That(!shell.Reorder(a, wiki, true) && !shell.Reorder(a, a, false) && !shell.Reorder(null, b, false) && changes == 2,
            "Ordine schede: rilascio nella stessa posizione, sulla stessa scheda o senza origine non modifica nulla");
        var otherHost = shell.MoveToNewWindow(b);
        expect.That(!shell.CanReorder(a, b, true) && !shell.Reorder(a, b, true), "Ordine schede: trascinamento fra finestre diverse rifiutato");
        var third = workspace.NewCalculation("str_palo"); var c = shell.TabOf(third)!;
        expect.That(shell.Reorder(c, b, false) && otherHost.Tabs.SequenceEqual([c, b]) && otherHost.Selected == c && workspace.Active == third,
            "Ordine schede: finestra staccata, selezione e focus conservati");
        shell.WindowClosed(otherHost);
        expect.That(shell.Main.Tabs.TakeLast(2).SequenceEqual([c, b]) && !otherHost.Tabs.Any(), "Ordine schede conservato riagganciando una finestra");
        shell.Close(model);
        expect.That(!shell.Reorder(model, a, false) && !shell.CanReorder(a, model, true), "Ordine schede: origine o destinazione chiusa rifiutata");
        using var foreignShell = new ShellWindowsViewModel(workspace, new RecordingWindows(), bench.Messages);
        expect.That(!foreignShell.Reorder(a, b, false), "Ordine schede: schede di un altro coordinatore rifiutate");
        var before = shell.Main.Tabs.ToArray(); shell.Dispose();
        expect.That(!shell.Reorder(a, b, false) && shell.Main.Tabs.SequenceEqual(before), "Ordine schede: coordinatore chiuso protetto");
        return expect.Count;
    }

    private static int QuickSections()
    {
        var expect = new Expect();
        var bench = new ShellBench();
        using var workspace = bench.Workspace;
        var windows = new RecordingWindows();
        using var shell = new ShellWindowsViewModel(workspace, windows, bench.Messages);
        var first = workspace.NewCalculation(SheetLayout.QuickSectionModule);
        var host = shell.Detached.Single();
        expect.That(host.IsQuickSectionSelected && host.SelectedSheet == first && first.Heading == "Sezione 1", "Prima sezione rapida, numerata nella propria finestra");
        ShellBench.Probe(first).Note = "sezione originale";
        shell.WindowActivated(shell.Main);
        shell.NewQuickSectionCommand.Execute(host);
        var second = workspace.Active!;
        expect.That(shell.Detached.Single() == host && host.Tabs.Count == 2 && windows.Calls.Count(c => c.Action == "apri") == 1,
            "Nuova sezione nella finestra richiesta anche se il fuoco era nella principale, senza aprire altre finestre");
        expect.That(second != first && second.Heading == "Sezione 2" && host.SelectedSheet == second
            && !ReferenceEquals(first.ViewModel, second.ViewModel) && !ReferenceEquals(first.ViewModel.Data, second.ViewModel.Data), "Sezioni con documenti e ViewModel indipendenti");
        ShellBench.Probe(second).Note = "seconda sezione";
        shell.Select(shell.TabOf(first)!);
        expect.That(first.ViewModel.Data.S("nota") == "sezione originale" && second.Sheet["dati"].S("nota") == "seconda sezione" && host.SelectedSheet == first,
            "Il cambio di scheda conserva dati distinti");
        var material = workspace.NewCalculation("mat_calcestruzzo");
        expect.That(!host.IsQuickSectionSelected && host.SelectedSheet == material, "Comandi della sezione rapida nascosti su un altro modulo");
        shell.Select(shell.TabOf(second)!);
        var third = workspace.NewCalculation(SheetLayout.QuickSectionModule);
        expect.That(shell.Detached.Single() == host && third.Heading == "Sezione 3" && host.SelectedSheet == third, "Ulteriore sezione nella stessa finestra");
        var wiki = shell.ShowWiki(null);
        expect.That(!host.IsQuickSectionSelected && host.SelectedSheet is null, "Wiki senza destinazione per i report della sezione");
        shell.Close(wiki);
        expect.That(host.IsQuickSectionSelected && host.SelectedSheet == third, "Comandi ripristinati tornando alla sezione");
        expect.That(shell.Close(shell.TabOf(first)!) && host.Tabs.Count == 3 && !second.IsDisposed
            && ((QuickSectionsSession)second.Owner).Sections.Count == 3, "Chiudere una scheda conserva tutte le sezioni nel documento");
        expect.That(shell.Close(shell.TabOf(second)!) && host.Tabs.Count == 2, "La raccolta resta aperta con l’ultima scheda");
        bench.Confirmations.Then(SaveChangesAnswer.Cancel);
        expect.That(!shell.Close(shell.TabOf(third)!) && host.Tabs.Count == 2, "Annulla la chiusura dell’ultima scheda conserva la raccolta modificata");
        shell.WindowClosed(host);
        expect.That(shell.Detached.Count == 0 && shell.Main.Tabs.Count == 2 && !third.IsDisposed, "Chiudere la finestra riaggancia tutte le schede aperte");
        expect.That(!shell.NewQuickSectionCommand.CanExecute(host), "Nessuna nuova sezione in una finestra chiusa");
        return expect.Count;
    }
}
