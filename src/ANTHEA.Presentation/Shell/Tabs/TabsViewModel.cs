using System.Collections.ObjectModel;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Shell.Tabs;

/// <summary>Kind of a tab of the shell (proposal 4.1): a sheet, the Wiki (Q18 a) or a model.</summary>
public enum ShellTabKind
{
    Sheet,
    Wiki,
    Model
}

/// <summary>
/// A tab of the shell (F5.13 b, proposal 4.1): title, the window that holds it, selection and focus. The state lives in the ViewModel, so a
/// tab moved to another window keeps its data and results; the views are recreated (proposal 4.1).
/// </summary>
public abstract class ShellTabViewModel : ViewModelBase
{
    private bool isSelected, isActive;
    private TabHostViewModel? host;

    public abstract ShellTabKind Kind { get; }

    public abstract string Title { get; }

    /// <summary>The window that holds the tab.</summary>
    public TabHostViewModel? Host { get => host; internal set => SetProperty(ref host, value); }

    /// <summary>The tab shown in its window.</summary>
    public bool IsSelected { get => isSelected; internal set => SetProperty(ref isSelected, value); }

    /// <summary>The tab with the focus of the shell (at most one in all the windows).</summary>
    public bool IsActive { get => isActive; internal set => SetProperty(ref isActive, value); }

    /// <summary>The window is detached: «Riaggancia» is offered.</summary>
    public bool IsDetached => Host is { IsMain: false };

    internal void RaiseTitle() => OnPropertyChanged(nameof(Title));

    internal void RaiseDetached() => OnPropertyChanged(nameof(IsDetached));
}

/// <summary>
/// The tab of an open sheet: its heading, the «•» of the data not saved (proposal 3.6, SH15), read-only, and the ViewModel shown, which
/// changes when the sheet reopens.
/// </summary>
public sealed class SheetTabViewModel : ShellTabViewModel
{
    internal SheetTabViewModel(SheetSession session)
    {
        Session = session;
        session.StateChanged += OnStateChanged;
        session.ViewModelReplaced += OnReplaced;
    }

    public SheetSession Session { get; }

    public override ShellTabKind Kind => ShellTabKind.Sheet;

    public override string Title => Session.IsDisposed ? "" : Session.Heading;

    /// <summary>The ViewModel of the sheet (its view is found by type, decision Q3).</summary>
    public ISheetViewModel Content => Session.ViewModel;

    public string Module => Session.Module;

    public bool IsUnsaved => Session.IsUnsaved;

    public bool IsReadOnly => Session.IsReadOnly;

    protected override void OnDisposed()
    {
        Session.StateChanged -= OnStateChanged;
        Session.ViewModelReplaced -= OnReplaced;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        RaiseTitle(); OnPropertyChanged(nameof(IsUnsaved)); OnPropertyChanged(nameof(IsReadOnly));
    }

    private void OnReplaced(object? sender, EventArgs e) { OnPropertyChanged(nameof(Content)); OnStateChanged(sender, e); }
}

/// <summary>A tab that is not a sheet: the Wiki (Q18 a, one tab) or a model, with the content given by the shell.</summary>
public sealed class PageTabViewModel(ShellTabKind kind, string title, object? content) : ShellTabViewModel
{
    private string title = title ?? throw new ArgumentNullException(nameof(title));

    public override ShellTabKind Kind { get; } = kind == ShellTabKind.Sheet ? throw new ArgumentException("Scheda di un foglio senza sessione.", nameof(kind)) : kind;

    public override string Title => title;

    public object? Content { get; } = content;

    public void Rename(string value) { title = value ?? throw new ArgumentNullException(nameof(value)); RaiseTitle(); }

    /// <summary>The content of a closed tab is released with it (F5.13 c: the Wiki and the model tabs own their ViewModel).</summary>
    protected override void OnDisposed() => (Content as IDisposable)?.Dispose();
}

/// <summary>The tabs of one window (proposal 4.1, TabHostViewModel): the main window or a detached one.</summary>
public sealed class TabHostViewModel : ViewModelBase
{
    private ShellTabViewModel? selected;

    internal TabHostViewModel(bool isMain) => IsMain = isMain;

    public bool IsMain { get; }

    public ObservableCollection<ShellTabViewModel> Tabs { get; } = [];

    /// <summary>The tab shown; null when the window has no tab (the main window then shows the pages).</summary>
    public ShellTabViewModel? Selected
    {
        get => selected;
        internal set
        {
            if (ReferenceEquals(selected, value)) return;
            if (selected is not null) selected.IsSelected = false;
            selected = value;
            if (value is not null) value.IsSelected = true;
            OnPropertyChanged();
        }
    }
}

/// <summary>Windows of the shell (proposal 3.3, IWindowService): a detached window shows a host of tabs; the main window exists already.</summary>
public interface IWindowService
{
    /// <summary>Opens a new window for <paramref name="host"/>, which may go to another monitor.</summary>
    void Open(TabHostViewModel host);

    /// <summary>Closes the window of <paramref name="host"/> (left without tabs).</summary>
    void Close(TabHostViewModel host);

    /// <summary>Brings the window of <paramref name="host"/> to the front.</summary>
    void Activate(TabHostViewModel host);
}

/// <summary>
/// The windows and tabs of the shell (F5.13 b, proposal 4.1 and 4.4, Q10 a): every open sheet of the coordinator has a tab in one window;
/// the selection of a tab or of a window gives the focus to its sheet, which confirms the previous one (Q9 a, also across windows); «Sposta
/// in una nuova finestra» moves the same ViewModel to a new window; «Riaggancia» brings it back; closing a secondary window brings its tabs
/// back to the main one, without closing them. New tabs open in the window with the focus (SH19); a secondary window left without tabs
/// closes (SH20). The windows themselves are drawn in (c).
/// </summary>
public sealed class ShellWindowsViewModel : ViewModelBase
{
    public const string MoveLabel = "Sposta in una nuova finestra";
    public const string ReattachLabel = "Riaggancia";

    private readonly WorkspaceSession workspace;
    private readonly IWindowService windows;
    private readonly Dictionary<SheetSession, SheetTabViewModel> sheets = [];
    private TabHostViewModel activeHost;
    private PageTabViewModel? wiki;

    public ShellWindowsViewModel(WorkspaceSession workspace, IWindowService windows, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(messages);
        this.workspace = workspace; this.windows = windows;
        Main = activeHost = new TabHostViewModel(true);
        var guard = new CommandGuard(messages);
        SelectCommand = new RelayCommand<ShellTabViewModel>(tab => guard.Run(() => Select(tab!)), tab => tab is not null);
        CloseCommand = new RelayCommand<ShellTabViewModel>(tab => guard.Run(() => Close(tab!)), tab => tab is not null);
        MoveToNewWindowCommand = new RelayCommand<ShellTabViewModel>(tab => guard.Run(() => MoveToNewWindow(tab!)), tab => tab is not null);
        ReattachCommand = new RelayCommand<ShellTabViewModel>(tab => guard.Run(() => Reattach(tab!)), tab => tab is { IsDetached: true });
        foreach (var sheet in workspace.Sheets) Add(sheet);
        workspace.SheetOpened += OnSheetOpened;
        workspace.SheetClosed += OnSheetClosed;
        workspace.ActiveChanged += OnActiveChanged;
        OnActiveChanged(this, EventArgs.Empty);
    }

    /// <summary>The main window.</summary>
    public TabHostViewModel Main { get; }

    /// <summary>The detached windows, in order of opening.</summary>
    public ObservableCollection<TabHostViewModel> Detached { get; } = [];

    /// <summary>The window with the focus, where new tabs open.</summary>
    public TabHostViewModel ActiveHost => activeHost;

    public IRelayCommand<ShellTabViewModel> SelectCommand { get; }
    public IRelayCommand<ShellTabViewModel> CloseCommand { get; }
    public IRelayCommand<ShellTabViewModel> MoveToNewWindowCommand { get; }
    public IRelayCommand<ShellTabViewModel> ReattachCommand { get; }

    /// <summary>The tab of an open sheet.</summary>
    public SheetTabViewModel? TabOf(SheetSession sheet) => sheets.GetValueOrDefault(sheet);

    /// <summary>All the tabs, main window first.</summary>
    public IEnumerable<ShellTabViewModel> Tabs => new[] { Main }.Concat(Detached).SelectMany(h => h.Tabs);

    /// <summary>Selects a tab: a sheet takes the focus (the active one is confirmed first); a page leaves the sheets without focus.</summary>
    public void Select(ShellTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var host = tab.Host ?? throw new ArgumentException("Scheda non aperta.", nameof(tab));
        workspace.Activate(tab is SheetTabViewModel sheet ? sheet.Session : null);
        host.Selected = tab;
        Focus(host);
        foreach (var other in Tabs) other.IsActive = ReferenceEquals(other, tab);
        TabSelected?.Invoke(this, tab);
    }

    /// <summary>A tab was selected (F5.13 c): the main window shows it in place of a page.</summary>
    public event EventHandler<ShellTabViewModel>? TabSelected;

    /// <summary>The page tabs open (the Wiki, the models), main window first.</summary>
    public IEnumerable<PageTabViewModel> Pages => Tabs.OfType<PageTabViewModel>();

    /// <summary>
    /// A page tab (F5.13 c, a model): an open tab is selected, a new one opens in the window with the focus (SH19) and is selected.
    /// </summary>
    public PageTabViewModel ShowPage(PageTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab.Kind == ShellTabKind.Wiki) throw new ArgumentException("La Wiki ha la sua scheda (ShowWiki).", nameof(tab));
        if (tab.Host is null) Add(tab, activeHost);
        Select(tab);
        return tab;
    }

    /// <summary>A window got the focus (the user clicked it): its tab takes the focus of the shell.</summary>
    public void WindowActivated(TabHostViewModel host)
    {
        ArgumentNullException.ThrowIfNull(host);
        activeHost = host;
        OnPropertyChanged(nameof(ActiveHost));
        if (host.Selected is { } tab) Select(tab);
    }

    /// <summary>Closes a tab: a sheet through the coordinator (a single file asks to save it); a page at once.</summary>
    public bool Close(ShellTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab is SheetTabViewModel sheet) return workspace.CloseSheet(sheet.Session);
        if (ReferenceEquals(tab, wiki)) wiki = null;
        Remove(tab);
        tab.Dispose();
        return true;
    }

    /// <summary>«Sposta in una nuova finestra»: the same ViewModel in a new window, selected.</summary>
    public TabHostViewModel MoveToNewWindow(ShellTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var host = new TabHostViewModel(false);
        Detached.Add(host);
        Move(tab, host);
        windows.Open(host);
        activeHost = host;
        OnPropertyChanged(nameof(ActiveHost));
        Select(tab);
        return host;
    }

    /// <summary>
    /// «Apri in una nuova finestra» of the tree (F5.13 c, after the checkpoint of 22/10): a tab of the main window moves to a new window
    /// (<see cref="MoveToNewWindow"/>); a tab already in a detached window is selected there and its window comes to the front, without a new one.
    /// </summary>
    public TabHostViewModel ShowInNewWindow(ShellTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        var host = tab.Host ?? throw new ArgumentException("Scheda non aperta.", nameof(tab));
        if (host.IsMain) return MoveToNewWindow(tab);
        Select(tab);
        return host;
    }

    /// <summary>«Riaggancia»: the tab back in the main window, selected.</summary>
    public void Reattach(ShellTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (!tab.IsDetached) return;
        Move(tab, Main);
        activeHost = Main;
        OnPropertyChanged(nameof(ActiveHost));
        Select(tab);
    }

    /// <summary>A secondary window is closing: its tabs go back to the main window, open.</summary>
    public void WindowClosed(TabHostViewModel host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.IsMain || !Detached.Contains(host)) return;
        foreach (var tab in host.Tabs.ToArray()) Move(tab, Main, closeEmpty: false);
        Detached.Remove(host);
        if (ReferenceEquals(activeHost, host)) { activeHost = Main; OnPropertyChanged(nameof(ActiveHost)); }
        if (Main.Selected is null && Main.Tabs.Count > 0) Main.Selected = Main.Tabs[^1];
        host.Dispose();
    }

    /// <summary>The Wiki in a tab (Q18 a): the existing one is selected, otherwise a new one in the window with the focus.</summary>
    public PageTabViewModel ShowWiki(object? content)
    {
        if (wiki is null)
        {
            wiki = new PageTabViewModel(ShellTabKind.Wiki, "Wiki", content);
            Add(wiki, activeHost);
        }
        Select(wiki);
        return wiki;
    }

    protected override void OnDisposed()
    {
        workspace.SheetOpened -= OnSheetOpened;
        workspace.SheetClosed -= OnSheetClosed;
        workspace.ActiveChanged -= OnActiveChanged;
        foreach (var tab in Tabs.ToArray()) tab.Dispose();
    }

    private void Focus(TabHostViewModel host)
    {
        if (!ReferenceEquals(activeHost, host)) { activeHost = host; OnPropertyChanged(nameof(ActiveHost)); }
        if (!host.IsMain) windows.Activate(host);
    }

    private void Add(SheetSession sheet)
    {
        var tab = new SheetTabViewModel(sheet);
        sheets.Add(sheet, tab);
        Add(tab, activeHost);
    }

    private static void Add(ShellTabViewModel tab, TabHostViewModel host)
    {
        host.Tabs.Add(tab);
        tab.Host = host;
        tab.RaiseDetached();
    }

    private void Move(ShellTabViewModel tab, TabHostViewModel target, bool closeEmpty = true)
    {
        var source = tab.Host;
        if (ReferenceEquals(source, target)) return;
        if (source is not null) Remove(tab, closeEmpty);
        Add(tab, target);
    }

    private void Remove(ShellTabViewModel tab, bool closeEmpty = true)
    {
        if (tab.Host is not { } host) return;
        int index = host.Tabs.IndexOf(tab);
        host.Tabs.Remove(tab);
        tab.Host = null;
        tab.IsActive = false;
        if (ReferenceEquals(host.Selected, tab)) host.Selected = host.Tabs.Count == 0 ? null : host.Tabs[Math.Min(index, host.Tabs.Count - 1)];
        if (closeEmpty && !host.IsMain && host.Tabs.Count == 0 && Detached.Remove(host))
        {
            if (ReferenceEquals(activeHost, host)) { activeHost = Main; OnPropertyChanged(nameof(ActiveHost)); }
            windows.Close(host);
            host.Dispose();
        }
    }

    private void OnSheetOpened(object? sender, SheetSession sheet) => Add(sheet);

    private void OnSheetClosed(object? sender, SheetClosedEventArgs e)
    {
        if (!sheets.Remove(e.Sheet, out var tab)) return;
        Remove(tab);
        tab.Dispose();
    }

    /// <summary>The focus moved in the coordinator (a sheet opened, «Riprendi», a page): the tab of the sheet is selected in its window.</summary>
    private void OnActiveChanged(object? sender, EventArgs e)
    {
        var active = workspace.Active is { } sheet ? TabOf(sheet) : null;
        foreach (var tab in Tabs) tab.IsActive = ReferenceEquals(tab, active);
        if (active?.Host is not { } host) return;
        host.Selected = active;
        Focus(host);
    }
}
