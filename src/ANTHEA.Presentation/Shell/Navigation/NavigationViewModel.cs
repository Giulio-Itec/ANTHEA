using Anthea.Application.Modules;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Shell.Navigation;

/// <summary>A page of the navigation bar (MainWindow.BuildShell): the order and the names of today.</summary>
public enum ShellPage
{
    Home,
    Projects,
    Models,
    Modules,
    Examples,
    Wiki
}

/// <summary>A button of the navigation bar: label, page and selection (Ui.SetSelected).</summary>
public sealed class NavigationItemViewModel(string title, ShellPage page, IRelayCommand select) : ViewModelBase
{
    private bool isSelected;

    public string Title { get; } = title;
    public ShellPage Page { get; } = page;
    public IRelayCommand SelectCommand { get; } = select;

    public bool IsSelected { get => isSelected; internal set => SetProperty(ref isSelected, value); }
}

/// <summary>A card of the home page (MainWindow.ShowHome): title, description, badge and the button that opens a page.</summary>
public sealed record HomeCard(string Title, string Description, string Badge, string Button, ShellPage Page);

/// <summary>
/// Navigation of the shell (F5.13 b, proposal 4.2 and 4.3; MainWindow.BuildShell, SelectNavigation, ShowHome): the pages Home, Progetti,
/// Modelli, Moduli singoli, Esempi and Wiki. Every page confirms the active sheet first and takes the focus from it; every page but
/// «Progetti» ends the preview of an archived revision, as SelectNavigation does (SH7); «Progetti» without a project opens a new
/// archive (MainWindow.ShowProjectOverview → NewProjects). The home offers «Riprendi» on the last sheet that had the focus (SH13).
/// </summary>
public sealed class NavigationViewModel : ViewModelBase
{
    /// <summary>Subtitle of the navigation bar.</summary>
    public const string Subtitle = "Strumenti di calcolo";

    /// <summary>Title and text of the band of the home page.</summary>
    public const string HomeTitle = "Strumenti di calcolo per l'ingegneria";

    /// <inheritdoc cref="HomeTitle"/>
    public const string HomeText = "Organizza i progetti, apri un modulo o approfondisci teoria ed esempi nella Wiki.";

    private readonly WorkspaceSession workspace;
    private readonly CommandGuard guard;
    private ShellPage? page = ShellPage.Home;
    private SheetSession? last;

    public NavigationViewModel(WorkspaceSession workspace, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(messages);
        this.workspace = workspace;
        guard = new CommandGuard(messages);
        Items = new (string Title, ShellPage Page)[] { ("Home", ShellPage.Home), ("Progetti", ShellPage.Projects), ("Modelli", ShellPage.Models),
            ("Moduli singoli", ShellPage.Modules), ("Esempi", ShellPage.Examples), ("Wiki", ShellPage.Wiki) }
            .Select(item => new NavigationItemViewModel(item.Title, item.Page, GuardedCommands.Create(guard, () => Select(item.Page)))).ToArray();
        Items[0].IsSelected = true;
        int count = Archivio.Moduli.Length;
        Footer = $"Moduli disponibili: {count} di {count + 1}";
        HomeCards =
        [
            new("Moduli singoli", "Usa un modulo direttamente, senza creare un progetto.", $"{count} moduli disponibili", "Apri i moduli", ShellPage.Modules),
            new("Progetti", "Raggruppa i fogli per opera, spalla, pila o altra struttura.", "Struttura gerarchica libera", "Gestisci i progetti", ShellPage.Projects),
            new("Wiki", "Comprendi il comportamento, consulta le guide e prova gli esempi nei moduli.", "Manuale di ingegneria · Guide Anthea", "Esplora la Wiki", ShellPage.Wiki)
        ];
        OpenCardCommand = new RelayCommand<HomeCard>(card => guard.Run(() => Select(card!.Page)), card => card is not null);
        ResumeCommand = GuardedCommands.Create(guard, Resume, () => Last is not null);
        Modules = new ModuleCatalogViewModel(workspace, guard, messages, () => Last);
        workspace.ActiveChanged += OnActiveChanged;
        workspace.SheetClosed += OnSheetClosed;
    }

    /// <summary>The buttons of the bar, in the order of today.</summary>
    public IReadOnlyList<NavigationItemViewModel> Items { get; }

    /// <summary>The page on screen; null while a sheet has the focus.</summary>
    public ShellPage? Page { get => page; private set { if (SetProperty(ref page, value)) foreach (var item in Items) item.IsSelected = item.Page == value; } }

    /// <summary>Text at the foot of the bar.</summary>
    public string Footer { get; }

    public IReadOnlyList<HomeCard> HomeCards { get; }

    public IRelayCommand<HomeCard> OpenCardCommand { get; }

    /// <summary>The last sheet that had the focus, still open: «Riprendi» of the home and of the catalogue of the modules.</summary>
    public SheetSession? Last { get => last; private set { if (SetProperty(ref last, value)) { OnPropertyChanged(nameof(ResumeText)); ResumeCommand.NotifyCanExecuteChanged(); Modules.Refresh(); } } }

    /// <summary>«Riprendi · nome» of the home page (MainWindow.ShowHome), null without a sheet.</summary>
    public string? ResumeText => Last is null ? null : "Riprendi · " + ShellTexts.SheetName(Last.Sheet);

    public IRelayCommand ResumeCommand { get; }

    /// <summary>The page «Moduli singoli».</summary>
    public ModuleCatalogViewModel Modules { get; }

    /// <summary>
    /// Shows <paramref name="target"/>: the active sheet is confirmed and loses the focus; the preview of a revision ends except for «Progetti»;
    /// «Progetti» without a project opens a new archive.
    /// </summary>
    public void Select(ShellPage target)
    {
        ThrowIfDisposed();
        workspace.Activate(null);
        if (target == ShellPage.Projects)
        {
            if (workspace.EnsureProject() is null) return;
        }
        else workspace.Project?.ExitPreview();
        Page = target;
    }

    /// <summary>A tab without a sheet (the Wiki, a model) is shown in place of the page (F5.13 c): no page is on screen.</summary>
    public void ShowTab()
    {
        ThrowIfDisposed();
        Page = null;
    }

    /// <summary>«Riprendi»: the last sheet takes the focus again.</summary>
    public void Resume()
    {
        if (Last is not { IsDisposed: false } sheet) return;
        workspace.Activate(sheet);
    }

    protected override void OnDisposed()
    {
        workspace.ActiveChanged -= OnActiveChanged;
        workspace.SheetClosed -= OnSheetClosed;
        Modules.Dispose();
    }

    private void OnActiveChanged(object? sender, EventArgs e)
    {
        if (workspace.Active is not { } sheet) return;
        Last = sheet;
        Page = null;
    }

    private void OnSheetClosed(object? sender, SheetClosedEventArgs e)
    {
        if (ReferenceEquals(e.Sheet, Last)) Last = workspace.Sheets.LastOrDefault();
    }
}

/// <summary>A card of the catalogue of the modules (MainWindow.ShowModules).</summary>
/// <param name="Id">Module, empty for a module in preparation.</param>
/// <param name="Status">«Disponibile» or «In preparazione».</param>
/// <param name="Title">Description of the module, the large text of the card.</param>
/// <param name="Subtitle">«area · elemento».</param>
/// <param name="Owner">«Referente: GPC» for the structures, «Referente: GSC» otherwise.</param>
/// <param name="Icon">Key of the icon of the module.</param>
public sealed record ModuleCard(string Id, string Status, string Title, string Subtitle, string Owner, string Icon)
{
    public bool IsAvailable => Id.Length > 0;

    /// <summary>
    /// The label of the button of the card (F5.13 c, night of 22-23/10): «Dettagli» in preparation, «Riprendi» for the module of the last sheet, otherwise
    /// «Apri» (<see cref="ModuleCatalogViewModel.ButtonText"/>, MainWindow.ShowModules).
    /// </summary>
    public string Button { get; init; } = "Apri";
}

/// <summary>A group of cards of the same element, with its header in capitals.</summary>
public sealed record ModuleGroup(string Header, IReadOnlyList<ModuleCard> Cards);

/// <summary>An area of the catalogue (Geotecnica, Strutture…) with its groups.</summary>
public sealed record ModuleArea(string Title, IReadOnlyList<ModuleGroup> Groups);

/// <summary>
/// The page «Moduli singoli» (MainWindow.ShowModules): disciplines, areas, groups by element and cards from <see cref="ModuleDirectory"/>;
/// «Apri» opens a new single file, «Riprendi» the last sheet of the same module (SH13).
/// </summary>
public sealed class ModuleCatalogViewModel : ViewModelBase
{
    public const string Title = "Moduli singoli";
    public const string Intro = "Scegli disciplina, elemento e verifica. I dati restano indipendenti da un progetto.";
    public const string DisciplinesTitle = "DISCIPLINE";
    /// <summary>Message of a module in preparation (MainWindow.ShowModules).</summary>
    public const string InPreparation = "Modulo in preparazione, come nella versione originale.";

    private readonly WorkspaceSession workspace;
    private readonly IMessageService messages;
    private readonly Func<SheetSession?> last;
    private string discipline = "Tutti";

    internal ModuleCatalogViewModel(WorkspaceSession workspace, CommandGuard guard, IMessageService messages, Func<SheetSession?> last)
    {
        this.workspace = workspace; this.messages = messages; this.last = last;
        SelectDisciplineCommand = new RelayCommand<string>(name => guard.Run(() => Discipline = name!), name => name is not null);
        OpenCommand = new RelayCommand<ModuleCard>(card => guard.Run(() => Open(card!)), card => card is not null);
        Areas = Build("Tutti", null);
    }

    /// <summary>The filters, in the order of today.</summary>
    public IReadOnlyList<string> Disciplines { get; } = ["Tutti", "Geotecnica", "Strutture", "Materiali", "Azioni"];

    public string Discipline
    {
        get => discipline;
        set
        {
            if (!Disciplines.Contains(value)) throw new ArgumentException("Disciplina sconosciuta: " + value, nameof(value));
            if (SetProperty(ref discipline, value)) { Areas = Build(value, last()?.Module); OnPropertyChanged(nameof(Areas)); }
        }
    }

    public IReadOnlyList<ModuleArea> Areas { get; private set; }

    public IRelayCommand<string> SelectDisciplineCommand { get; }

    public IRelayCommand<ModuleCard> OpenCommand { get; }

    /// <summary>Label of the button of a card: «Dettagli» in preparation, «Riprendi» for the module of the last sheet, otherwise «Apri».</summary>
    public string ButtonText(ModuleCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !card.IsAvailable ? "Dettagli" : last()?.Module == card.Id ? "Riprendi" : "Apri";
    }

    /// <summary>
    /// The button of a card (MainWindow.OpenModule): the last sheet of the module, or a new single file; a module in preparation shows
    /// the note of today, without a title.
    /// </summary>
    public void Open(ModuleCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!card.IsAvailable) { messages.Inform("", InPreparation); return; }
        if (last() is { IsDisposed: false } sheet && sheet.Module == card.Id) workspace.Activate(sheet);
        else workspace.NewCalculation(card.Id);
    }

    /// <summary>
    /// The labels of the buttons depend on the last sheet: the cards are built again with their labels («Riprendi» for the module of the last sheet),
    /// as MainWindow.ShowModules builds the page every time (F5.13 c, night of 22-23/10; before, the view always wrote «Apri»).
    /// </summary>
    internal void Refresh()
    {
        Areas = Build(discipline, last()?.Module);
        OnPropertyChanged(nameof(Areas));
    }

    /// <summary>MainWindow.ShowModules: areas in fixed order, groups by element in the order of the directory, two columns in the view.</summary>
    private static IReadOnlyList<ModuleArea> Build(string discipline, string? resumed)
    {
        var modules = ModuleDirectory.All;
        return new[] { "Geotecnica", "Strutture", "Materiali", "Azioni" }.Where(a => discipline == "Tutti" || a == discipline)
            .Select(area => new ModuleArea(area, modules.Where(m => m.Area == area).GroupBy(m => m.Element)
                .Select(group => new ModuleGroup(group.Key.ToUpperInvariant(), group.Select(m => Card(area, m, resumed)).ToArray())).ToArray()))
            .ToArray();
    }

    private static ModuleCard Card(string area, ModuleInfo module, string? resumed)
    {
        string id = module.Id, title = module.Element;
        string icon = id != "" ? id : area == "Strutture" ? "str_micropalo" : title == "Palo" ? "geo_palo_orizzontale" : "geo_micropalo_orizzontale";
        return new(id, id == "" ? "In preparazione" : "Disponibile", module.Description, area + " · " + title,
            "Referente: " + (area == "Strutture" ? "GPC" : "GSC"), icon) { Button = id == "" ? "Dettagli" : id == resumed ? "Riprendi" : "Apri" };
    }
}
