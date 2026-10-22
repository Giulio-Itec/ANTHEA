using Anthea.Calculations;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 c: the detached windows without WPF (<see cref="ShellWindowTracker"/>, IWindowService): a window for each detached host, the
/// activation of a window that moves the focus of the shell (with the confirmation of the sheet that had it) without asking to activate it
/// again, the closing by the user that brings the tabs back, the closing by the shell of a window left without tabs that does not report
/// back, the main window that closes the others with it.
/// </summary>
internal static class WindowTrackerChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    /// <summary>A window that records the calls; <see cref="Show"/> activates it, as a WPF window shown with ShowActivated.</summary>
    private sealed class RecordingWindow(TabHostViewModel host, List<string> log) : IShellWindow
    {
        internal TabHostViewModel Host { get; } = host;
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

        /// <summary>The user closes the window with its button.</summary>
        internal void UserCloses() { log.Add("chiusa dall'utente"); IsActive = false; Closed?.Invoke(this, EventArgs.Empty); }
    }

    private static int RunAll()
    {
        var expect = new Expect();
        var bench = new ShellBench(new ScriptedConfirmations().Then(SaveChangesAnswer.Discard));
        var workspace = bench.Workspace;
        var log = new List<string>();
        var created = new List<RecordingWindow>();
        var tracker = new ShellWindowTracker(host => { var window = new RecordingWindow(host, log); created.Add(window); return window; });
        using var shell = new ShellWindowsViewModel(workspace, tracker, bench.Messages);
        tracker.Attach(shell);
        expect.Throws<InvalidOperationException>(() => tracker.Attach(new ShellWindowsViewModel(workspace, tracker, bench.Messages)), "Seconda shell rifiutata");
        expect.Throws<ArgumentException>(() => tracker.Open(shell.Main), "La finestra principale non si riapre");

        var first = workspace.NewCalculation("str_palo");
        var second = workspace.NewCalculation("mat_calcestruzzo");
        var secondTab = shell.TabOf(second)!;

        // «Sposta in una nuova finestra»: one window, shown and active, not activated a second time; the focus is in it.
        var host = shell.MoveToNewWindow(secondTab);
        var window = created.Single();
        expect.That(ReferenceEquals(window.Host, host) && tracker.Hosts.Single() == host && ReferenceEquals(tracker.WindowOf(host), window)
            && log.SequenceEqual(["mostra"]) && window.IsActive, "Finestra staccata mostrata una volta, attiva");
        expect.That(ReferenceEquals(shell.ActiveHost, host) && ReferenceEquals(workspace.Active, second) && secondTab.IsActive, "Fuoco nella finestra staccata");

        // The main window gets the focus: its tab takes the focus of the shell and the detached sheet is confirmed (Q9 a).
        window.Deactivate();
        ShellBench.Probe(second).Note = "bozza staccata";
        tracker.MainActivated();
        expect.That(ReferenceEquals(shell.ActiveHost, shell.Main) && ReferenceEquals(workspace.Active, first) && second.Sheet["dati"].S("nota") == "bozza staccata"
            && log.Count == 1, "Fuoco alla finestra principale, foglio staccato confermato, nessuna attivazione chiesta");

        // The user clicks the detached window: its tab takes the focus, the window is not asked to activate itself again.
        ShellBench.Probe(first).Note = "bozza principale";
        window.UserActivates();
        expect.That(ReferenceEquals(shell.ActiveHost, host) && ReferenceEquals(workspace.Active, second) && first.Sheet["dati"].S("nota") == "bozza principale"
            && log.Count == 1, "Clic sulla finestra staccata: fuoco al suo foglio, prima conferma di quello principale");

        // From the main window, selecting the detached tab activates its window, once.
        tracker.MainActivated();
        window.Deactivate();
        shell.Select(secondTab);
        expect.That(log.SequenceEqual(["mostra", "attiva"]) && ReferenceEquals(workspace.Active, second), "Selezione della scheda staccata: la sua finestra in primo piano");
        shell.Select(secondTab);
        expect.That(log.Count == 2, "Finestra già attiva: nessuna nuova attivazione");
        tracker.Open(host);
        expect.That(created.Count == 1 && log.Count == 2, "Seconda apertura dello stesso ospite: nessuna finestra nuova");

        // The user closes the detached window: its tabs go back to the main window, open; the host is gone.
        window.UserCloses();
        expect.That(tracker.Hosts.Count == 0 && shell.Detached.Count == 0 && shell.Main.Tabs.Contains(secondTab) && !secondTab.IsDetached
            && workspace.Sheets.Contains(second) && host.IsDisposed, "Finestra chiusa dall'utente: schede riportate nella principale, aperte");

        // A window left without tabs is closed by the shell, which is not told again (SH20).
        var again = shell.MoveToNewWindow(secondTab);
        var secondWindow = created[^1];
        shell.Close(secondTab);
        expect.That(created.Count == 2 && log[^1] == "chiudi" && tracker.Hosts.Count == 0 && shell.Detached.Count == 0 && again.IsDisposed
            && !workspace.Sheets.Contains(second) && shell.Main.Tabs.Count == 1, "Finestra rimasta senza schede chiusa dalla shell, una volta");
        secondWindow.UserActivates();
        expect.That(ReferenceEquals(shell.ActiveHost, shell.Main), "Una finestra già chiusa non sposta il fuoco");

        // The main window closes: the detached windows close with it, their tabs stay where they are.
        var third = workspace.NewCalculation("geo_palo_verticale");
        var thirdTab = shell.TabOf(third)!;
        var last = shell.MoveToNewWindow(thirdTab);
        tracker.CloseAll();
        expect.That(tracker.Hosts.Count == 0 && log[^1] == "chiudi" && last.Tabs.Contains(thirdTab) && shell.Detached.Contains(last), "Chiusura della principale: finestre staccate chiuse senza spostare le schede");
        expect.Throws<ArgumentNullException>(() => _ = new ShellWindowTracker(null!), "Fabbrica assente");
        return expect.Count;
    }
}
