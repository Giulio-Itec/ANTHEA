namespace Anthea.Presentation.Shell.Tabs;

/// <summary>A detached window of the shell as <see cref="ShellWindowTracker"/> sees it: the desktop wraps a WPF window, the checks record the calls.</summary>
public interface IShellWindow
{
    /// <summary>The window has the focus of the system.</summary>
    bool IsActive { get; }

    /// <summary>Shows the window (it may go to another monitor).</summary>
    void Show();

    /// <summary>Brings the window to the front with the focus.</summary>
    void Activate();

    /// <summary>Closes the window; <see cref="Closed"/> follows.</summary>
    void Close();

    /// <summary>The window got the focus (the user clicked it, or <see cref="Activate"/>).</summary>
    event EventHandler? Activated;

    /// <summary>The window closed, by the user or by <see cref="Close"/>.</summary>
    event EventHandler? Closed;
}

/// <summary>
/// The detached windows of the shell (F5.13 c, <see cref="IWindowService"/>, proposal 4.1, Q10 a, SH2, SH19, SH20): one window for each
/// secondary host of tabs, created by the desktop, with the rules that do not depend on WPF.
/// <list type="bullet">
/// <item>The activation of a window gives the focus of the shell to its tab (<see cref="ShellWindowsViewModel.WindowActivated"/>), which
/// confirms the sheet that had it (Q9 a); the request to activate the same window while it reports its activation is not repeated, nor is
/// the request for a window already active.</item>
/// <item>A window closed by the user brings its tabs back to the main window, open (<see cref="ShellWindowsViewModel.WindowClosed"/>); a
/// window closed by the shell, left without tabs, does not report back.</item>
/// <item>The main window reports its activation (<see cref="MainActivated"/>); when it closes, the detached windows close with it
/// (<see cref="CloseAll"/>) without moving their tabs.</item>
/// </list>
/// </summary>
public sealed class ShellWindowTracker : IWindowService
{
    private readonly Func<TabHostViewModel, IShellWindow> create;
    private readonly Dictionary<TabHostViewModel, IShellWindow> open = [];
    private ShellWindowsViewModel? windows;
    private TabHostViewModel? reporting;

    /// <param name="create">The window of a detached host (the desktop: a WPF window with the tabs of the host).</param>
    public ShellWindowTracker(Func<TabHostViewModel, IShellWindow> create) => this.create = create ?? throw new ArgumentNullException(nameof(create));

    /// <summary>The tabs of the shell, given after their creation because they need this service.</summary>
    public void Attach(ShellWindowsViewModel shellWindows)
    {
        ArgumentNullException.ThrowIfNull(shellWindows);
        if (windows is not null && !ReferenceEquals(windows, shellWindows)) throw new InvalidOperationException("Finestre della shell già collegate.");
        windows = shellWindows;
    }

    /// <summary>The hosts with an open window, in order of opening.</summary>
    public IReadOnlyCollection<TabHostViewModel> Hosts => open.Keys;

    /// <summary>The window of <paramref name="host"/>, null when it has none.</summary>
    public IShellWindow? WindowOf(TabHostViewModel host) => open.GetValueOrDefault(host);

    public void Open(TabHostViewModel host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.IsMain) throw new ArgumentException("La finestra principale esiste già.", nameof(host));
        if (open.ContainsKey(host)) { Activate(host); return; }
        var window = create(host) ?? throw new InvalidOperationException("Finestra non creata.");
        open.Add(host, window);
        window.Activated += (_, _) => OnActivated(host);
        window.Closed += (_, _) => OnClosed(host);
        window.Show();
    }

    public void Close(TabHostViewModel host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (open.Remove(host, out var window)) window.Close();
    }

    public void Activate(TabHostViewModel host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (ReferenceEquals(reporting, host)) return;
        if (open.TryGetValue(host, out var window) && !window.IsActive) window.Activate();
    }

    /// <summary>The main window got the focus: its tab takes the focus of the shell.</summary>
    public void MainActivated()
    {
        if (windows is not { } shell) return;
        Report(shell.Main, () => shell.WindowActivated(shell.Main));
    }

    /// <summary>The main window closes: every detached window closes with it, its tabs stay where they are.</summary>
    public void CloseAll()
    {
        foreach (var host in open.Keys.ToArray()) Close(host);
    }

    private void OnActivated(TabHostViewModel host)
    {
        if (windows is not { } shell || !open.ContainsKey(host)) return;
        Report(host, () => shell.WindowActivated(host));
    }

    private void Report(TabHostViewModel host, Action action)
    {
        var previous = reporting;
        reporting = host;
        try { action(); }
        finally { reporting = previous; }
    }

    /// <summary>A window closed: by the user when it is still tracked (its tabs go back to the main window), otherwise by the shell.</summary>
    private void OnClosed(TabHostViewModel host)
    {
        if (!open.Remove(host)) return;
        windows?.WindowClosed(host);
    }
}
