using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Examples;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Examples;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>
/// What the composition root gives to the shell besides the services (F5.13 c): the appearance of the application (menu «Aspetto») and the
/// catalogue of the installed examples (page «Esempi»). Without them the menu and the page are absent (the checks of parts a and b).
/// </summary>
public sealed record ShellOptions(IAppearanceService? Appearance = null, Func<ExampleCatalog>? Examples = null);

/// <summary>
/// The shell as one ViewModel (F5.13 b, c): the coordinator of the documents and the ViewModels of navigation, tree, catalogue of the sheets,
/// tabs and windows, commands, revision bar, status line and overview of the selected section, wired as MainWindow wires its parts:
/// <list type="bullet">
/// <item>the title of the window follows the document of the sheet with the focus, otherwise the project (MainWindow.UpdateTitle);</item>
/// <item>the overview asked by the tree shows the page «Progetti» on that section, with its revision bar (ShowProjectOverview);</item>
/// <item>a sheet of a project with the focus shows its revision bar and the status line of its shared data, refreshed at every edit
/// (ShowProjectSheet, RefreshSharedStatus);</item>
/// <item>the report asked by a sheet runs the «Report Word» command (ISheetHost.RequestReport).</item>
/// </list>
/// Part (c) adds what the window needs and does not depend on its layout: the centre of the main window (<see cref="Center"/>: the page of the
/// navigation, otherwise the tab selected in the main window, otherwise the home page); the bar above the sheet; the notice of the linked model;
/// the Wiki in its tab with the examples that open a module and the return to the work (MainWindow.ShowWiki, OpenWikiModule); the page «Modelli»
/// and the model tabs (ShowModelsHub, ShowContainerModel); the page «Esempi»; the menu «Aspetto»; the command line (App.OnStartup).
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    /// <summary>The toggle of the sheet bar (F5.13 c, after the checkpoint of 22/10): label and tooltip outside and inside the full page.</summary>
    public const string FullPageLabel = "Pagina intera";
    /// <inheritdoc cref="FullPageLabel"/>
    public const string ExitFullPageLabel = "Esci da pagina intera";
    /// <inheritdoc cref="FullPageLabel"/>
    public const string FullPageToolTip = "Nasconde la navigazione e l'albero del progetto: il foglio prende tutta la finestra";
    /// <inheritdoc cref="FullPageLabel"/>
    public const string ExitFullPageToolTip = "Torna alla navigazione e all'albero del progetto (Esc)";

    private readonly PresentationServices services;
    private readonly ShellOptions options;
    private readonly CommandGuard guard;
    private RevisionBarViewModel? revisions;
    private SectionOverviewViewModel? overview;
    private ISheetViewModel? watched;
    private ProjectSession? attached;
    private ExampleLibraryViewModel? examples;
    private WikiTabViewModel? wiki;
    private object? center;
    private bool isTreeOpen = true;
    private bool isFullPage, treeBeforeFullPage;
    private bool showingWiki;
    private string title = "ANTHEA";

    public ShellViewModel(PresentationServices services, ISheetFactory sheets, IDocumentFiles files, IWindowService windows, ISheetExports exports, ShellOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        this.services = services;
        this.options = options ?? new ShellOptions();
        guard = new CommandGuard(services.Messages);
        Workspace = new WorkspaceSession(services, sheets, files);
        Navigation = new NavigationViewModel(Workspace, services.Messages);
        Tree = new ProjectTreeViewModel(Workspace, services.Messages);
        Catalogue = new ProjectCatalogViewModel();
        Windows = new ShellWindowsViewModel(Workspace, windows, services.Messages);
        Commands = new ShellCommandsViewModel(Workspace, services, exports, files);
        Models = new ModelsPageViewModel(Workspace, services.Messages);
        ModelReference = new ModelReferenceViewModel(services.Messages);
        SheetBar = new SheetBarViewModel(Workspace, services.Messages, Back, uri => ShowWiki(uri));
        Appearance = this.options.Appearance is { } appearance ? new AppearanceMenuViewModel(appearance, services.Messages) : null;
        ToggleTreeCommand = new RelayCommand(() => IsTreeOpen = !IsTreeOpen);
        EnterFullPageCommand = new RelayCommand(() => EnterFullPage(), () => !IsFullPage && CanEnterFullPage);
        ExitFullPageCommand = new RelayCommand(ExitFullPage, () => IsFullPage);
        ToggleFullPageCommand = new RelayCommand(() => { if (IsFullPage) ExitFullPage(); else EnterFullPage(); }, () => IsFullPage || CanEnterFullPage);
        Tree.PlacementRequested += (_, e) => guard.Run(() => Place(e.Sheet, e.Placement));
        OpenLinkCommand = new RelayCommand<JsonObject>(node => guard.Run(() => OpenLink(node!)), node => node is not null);
        DuplicateSectionCommand = new RelayCommand<JsonObject>(section => guard.Run(() => DuplicateSection(section!)),
            section => section is not null && Workspace.Project is { IsReadOnly: false });
        CompareCommand = new AsyncRelayCommand<JsonObject>(section => guard.RunAsync(_ => ShowCoherenceAsync(section!)), section => section is not null && Workspace.Project is not null);
        RevisionSummaryCommand = new AsyncRelayCommand<JsonObject>(scope => guard.RunAsync(_ => ShowRevisionSummaryAsync(scope!)),
            scope => scope?["revisione"] is JsonObject && Workspace.Project is not null);
        OpenOverviewModelCommand = new RelayCommand<OverviewModel>(model => guard.Run(() => ShowModel(model!.Container, model.ModelId)), model => model is not null);
        AddCatalogSheetCommand = new RelayCommand<ProjectCatalogCard>(card => guard.Run(() => Tree.AddSheet(card!.Module)), card => card is not null && Workspace.Project is { IsReadOnly: false });
        Commands.ExamplesRequested += (_, _) => Navigation.Select(ShellPage.Examples);
        Tree.OverviewRequested += (_, section) => ShowOverview(section);
        Tree.CompareRequested += (_, section) => _ = CompareCommand.ExecuteAsync(section);
        Tree.ModelRequested += (_, model) => guard.Run(() => ShowModel(model.Container, model.ModelId));
        Tree.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ProjectTreeViewModel.Selected) && Workspace.Active is null) Revisions?.Show(SelectedSection()); };
        Models.OpenProjectRequested += (_, _) => Commands.Open();
        Models.ModelRequested += (_, e) => ShowModel(e.Container, e.ModelId);
        Models.ProjectsRequested += (_, _) => Navigation.Select(ShellPage.Projects);
        ModelReference.ModelRequested += (_, e) => ShowModel(e.Container, e.ModelId);
        Navigation.PropertyChanged += OnNavigationChanged;
        Windows.TabSelected += (_, tab) => { if (tab is PageTabViewModel) Navigation.ShowTab(); UpdateCenter(); };
        Windows.Main.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TabHostViewModel.Selected)) UpdateCenter(); };
        Workspace.ActiveChanged += (_, _) => OnActiveChanged();
        Workspace.DocumentsChanged += (_, _) => OnDocumentsChanged();
        Workspace.SheetsReloaded += (_, _) => OnActiveChanged();
        Workspace.ReportRequested += (_, sheet) => { if (ReferenceEquals(sheet, Workspace.Active)) _ = Commands.ReportCommand.ExecuteAsync(null); };
        OnDocumentsChanged();
        UpdateCenter();
    }

    public WorkspaceSession Workspace { get; }
    public NavigationViewModel Navigation { get; }
    public ProjectTreeViewModel Tree { get; }
    public ProjectCatalogViewModel Catalogue { get; }
    public ShellWindowsViewModel Windows { get; }
    public ShellCommandsViewModel Commands { get; }

    /// <summary>The page «Modelli».</summary>
    public ModelsPageViewModel Models { get; }

    /// <summary>The notice of the model linked to the sheet with the focus.</summary>
    public ModelReferenceViewModel ModelReference { get; }

    /// <summary>The bar above the sheet with the focus.</summary>
    public SheetBarViewModel SheetBar { get; }

    /// <summary>The menu «Aspetto», null without the appearance service.</summary>
    public AppearanceMenuViewModel? Appearance { get; }

    /// <summary>The status line of the shared data above the sheet with the focus.</summary>
    public SharedStatusViewModel SharedStatus { get; } = new();

    /// <summary>The revision bar of the open project, null without one.</summary>
    public RevisionBarViewModel? Revisions { get => revisions; private set => SetProperty(ref revisions, value); }

    /// <summary>The overview of the section on the page «Progetti».</summary>
    public SectionOverviewViewModel? Overview { get => overview; private set { if (SetProperty(ref overview, value)) UpdateCenter(); } }

    /// <summary>The page «Esempi», read from the installed catalogue the first time it is shown; null without a catalogue.</summary>
    public ExampleLibraryViewModel? Examples => examples;

    /// <summary>Title of the main window.</summary>
    public string Title { get => title; private set => SetProperty(ref title, value); }

    /// <summary>
    /// The centre of the main window: the ViewModel of the page on screen (home: <see cref="Navigation"/>; «Moduli singoli»: its catalogue;
    /// «Progetti»: <see cref="Overview"/>; «Modelli»: <see cref="Models"/>; «Esempi»: <see cref="Examples"/>), otherwise the tab selected in the
    /// main window, otherwise the home page (proposal 4.2: the home is the start page when no tab is open).
    /// </summary>
    public object? Center { get => center; private set => SetProperty(ref center, value); }

    /// <summary>A project archive is open: the tree shows beside the centre.</summary>
    public bool HasProject => Workspace.Project is not null;

    /// <summary>The tree of the project is open (the toggle «▸» of the sheet bar of today).</summary>
    public bool IsTreeOpen { get => isTreeOpen; set => SetProperty(ref isTreeOpen, value); }

    public IRelayCommand ToggleTreeCommand { get; }

    // ---- Full page (F5.13 c, after the checkpoint of 22/10)

    /// <summary>
    /// The main window shows its sheet at full page: the navigation and the tree of the project are hidden («Apri a pagina intera» of the tree,
    /// «Pagina intera» of the sheet bar). It holds only while the centre is a sheet tab and a sheet has the focus, so that the sheet bar with
    /// «Esci da pagina intera» is on screen: a page of the navigation, a page tab or the loss of every sheet end it. A detached window shows its
    /// sheet alone already: the full page does not apply to it.
    /// </summary>
    public bool IsFullPage
    {
        get => isFullPage;
        private set
        {
            if (!SetProperty(ref isFullPage, value)) return;
            OnPropertyChanged(nameof(FullPageText)); OnPropertyChanged(nameof(FullPageToolTipText));
            RefreshFullPageCommands();
        }
    }

    /// <summary>Label of the toggle of the sheet bar: «Pagina intera», or «Esci da pagina intera» at full page.</summary>
    public string FullPageText => IsFullPage ? ExitFullPageLabel : FullPageLabel;

    /// <inheritdoc cref="FullPageText"/>
    public string FullPageToolTipText => IsFullPage ? ExitFullPageToolTip : FullPageToolTip;

    /// <summary>The sheet with the focus is the tab on screen in the main window: it can go to full page.</summary>
    public bool CanEnterFullPage => Workspace.Active is { } active && Windows.TabOf(active) is { Host.IsMain: true } tab && ReferenceEquals(Center, tab);

    /// <summary>Enters the full page (no effect at full page or without a sheet of the main window on screen).</summary>
    public IRelayCommand EnterFullPageCommand { get; }

    /// <summary>«Esci da pagina intera» and Esc: back to the navigation, with the tree as it was before.</summary>
    public IRelayCommand ExitFullPageCommand { get; }

    /// <summary>The toggle «Pagina intera» / «Esci da pagina intera» of the sheet bar.</summary>
    public IRelayCommand ToggleFullPageCommand { get; }

    /// <summary>
    /// Enters the full page on the sheet on screen: the state of the tree is kept and the tree closes (it can be opened again with «▸» meanwhile);
    /// at full page already, the tree closes again. False when no sheet of the main window is on screen with the focus.
    /// </summary>
    public bool EnterFullPage()
    {
        if (!CanEnterFullPage) return false;
        if (!IsFullPage) treeBeforeFullPage = IsTreeOpen;
        IsTreeOpen = false;
        IsFullPage = true;
        return true;
    }

    /// <summary>Leaves the full page: the navigation comes back and the tree is open or closed as before the full page.</summary>
    public void ExitFullPage()
    {
        if (!IsFullPage) return;
        IsFullPage = false;
        IsTreeOpen = treeBeforeFullPage;
    }

    /// <summary>
    /// A sheet asked from the tree, already open in its tab (<see cref="ProjectTreeViewModel.PlacementRequested"/>):
    /// <list type="bullet">
    /// <item>«Apri in una nuova finestra»: a tab of the main window moves to a new window; in a detached window already, that window is activated;</item>
    /// <item>«Apri a pagina intera»: the tab is selected in the main window and the full page starts; in a detached window, that window is activated
    /// (it shows the sheet alone) and the main window does not change.</item>
    /// </list>
    /// </summary>
    public void Place(SheetSession sheet, SheetPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var tab = Windows.TabOf(sheet) ?? throw new ArgumentException("Foglio non aperto nella shell.", nameof(sheet));
        if (placement == SheetPlacement.NewWindow) { Windows.ShowInNewWindow(tab); return; }
        Windows.Select(tab);
        if (!tab.IsDetached) EnterFullPage();
    }

    /// <summary>
    /// The overview of <paramref name="section"/> (the first project when null), with its revision bar (MainWindow.ShowProjectOverview). As there, the
    /// tree selects the section (RefreshTree(section)), so that «+ Sezione» and the double click of «Schede da aggiungere» go into it (night of 22-23/10).
    /// </summary>
    public void ShowOverview(JsonObject? section)
    {
        if (Workspace.EnsureProject() is not { } project) return;
        Navigation.Select(ShellPage.Projects);
        if (section?.ContainsKey("modulo_id") == true) section = section.Parent?.Parent as JsonObject;
        section ??= project.Projects.FirstOrDefault();
        Overview = new SectionOverviewViewModel(project, section);
        if (section is not null && !ReferenceEquals(Tree.Selected?.Node, section)) Tree.Refresh(section);
        Revisions?.Show(section);
    }

    // ---- Part (c) after the checkpoint: Ctrl+Z / Ctrl+Y, the commands of the overview

    /// <summary>
    /// Ctrl+Z and Ctrl+Y of the project (ProjectWorkspace.PreviewKeyDown) apply where MainWindow showed the project workspace: with an editable
    /// project, on the page «Progetti» (the overview) or on a sheet of the project. <paramref name="shown"/> is what the window shows: the centre
    /// of the main window or the tab of a detached window. Not on a single file, on the other pages, on a model, in the preview of a revision.
    /// </summary>
    public bool HistoryKeysApply(object? shown) =>
        Workspace.Project is { IsReadOnly: false } && (shown is SectionOverviewViewModel || shown is SheetTabViewModel { Session.Project: not null });

    /// <summary>Ctrl+Z (<paramref name="redo"/> false) or Ctrl+Y: «↶ Annulla» or «↷ Ripristina» of the project bar, when it can run.</summary>
    public void RestoreHistory(bool redo)
    {
        var command = redo ? Commands.RedoCommand : Commands.UndoCommand;
        if (command.CanExecute(null)) command.Execute(null);
    }

    /// <summary>
    /// A link of «Fogli e sottosezioni» of the overview (ProjectPresentation.BuildSectionOverview): the active sheet is confirmed; a sheet opens in
    /// its tab, a subsection shows its overview.
    /// </summary>
    public IRelayCommand<JsonObject> OpenLinkCommand { get; }

    /// <summary>«Duplica sezione» of the overview (ProjectWorkspace.DuplicateProjectSection): the copy after the section, then its overview.</summary>
    public IRelayCommand<JsonObject> DuplicateSectionCommand { get; }

    /// <summary>The link of the overview (see <see cref="OpenLinkCommand"/>).</summary>
    public void OpenLink(JsonObject node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Workspace.CommitActive();
        if (node.ContainsKey("modulo_id")) Workspace.OpenSheet(node);
        else ShowOverview(node);
    }

    /// <summary>«Duplica sezione» (see <see cref="DuplicateSectionCommand"/>): nothing while read-only; the tree selects the copy.</summary>
    public JsonObject? DuplicateSection(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (Workspace.Project is not { IsReadOnly: false } project || project.Duplicate(section) is not { } copy) return null;
        Tree.Refresh(copy);
        ShowOverview(copy);
        return copy;
    }

    // ---- Part (c) after the checkpoint: the comparison, the summary of the revisions and the models of the overview

    /// <summary>
    /// «Confronto e avvisi» of the overview and the coherence badge of the tree (ProjectSharing.AddCoherenceBadge): the comparison of a section in
    /// its dialog (<see cref="ShowCoherenceAsync"/>).
    /// </summary>
    public IAsyncRelayCommand<JsonObject> CompareCommand { get; }

    /// <summary>«Riepilogo modifiche» of the card «Revisioni» of the overview: the summary of the revisions of a section in its dialog.</summary>
    public IAsyncRelayCommand<JsonObject> RevisionSummaryCommand { get; }

    /// <summary>A button of the card «Modelli di calcolo» of the overview: the model, or a new one, in its tab (<see cref="ShowModel"/>).</summary>
    public IRelayCommand<OverviewModel> OpenOverviewModelCommand { get; }

    /// <summary>
    /// The double click on a card of «Schede da aggiungere» (ProjectPresentation.BuildProjectCatalog, MainWindow.AddSheet): a sheet of the module in the
    /// selected section, opened in its tab; without a section the note of today. The cards are also dragged on the tree (the view).
    /// </summary>
    public IRelayCommand<ProjectCatalogCard> AddCatalogSheetCommand { get; }

    /// <summary>
    /// The comparison of <paramref name="section"/> (MainWindow.ShowCoherence): the active sheet is confirmed, then the dialog «Confronto · sezione»;
    /// its changes go through the project, which reloads the open sheets, the tree and the overview, as MainWindow does after each one. True when
    /// the document changed.
    /// </summary>
    public async Task<bool> ShowCoherenceAsync(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (Workspace.Project is not { } project) return false;
        Workspace.CommitActive();
        using var coherence = new CoherenceViewModel(project, section, services);
        return await services.Dialogs.ShowDialogAsync(coherence);
    }

    /// <summary>
    /// «Riepilogo modifiche» of <paramref name="scope"/> (MainWindow.ShowProjectRevisions): the active sheet is confirmed, then the dialog «Revisioni ·
    /// sezione»; «Mostra questa revisione» closes it and shows that revision, with the revision bar of the section switched to. True when a revision
    /// was shown.
    /// </summary>
    public async Task<bool> ShowRevisionSummaryAsync(JsonObject scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (Workspace.Project is not { } project) return false;
        Workspace.CommitActive();
        using var summary = new RevisionSummaryViewModel(project, scope, services.Messages);
        summary.Shown += (_, _) => Revisions?.Show(null, afterSwitch: true);
        return await services.Dialogs.ShowDialogAsync(summary);
    }

    /// <summary>The application is closing: the questions to save every document (true closes).</summary>
    public bool ConfirmClose() => Workspace.ConfirmClose();

    // ---- Part (c): Wiki, modules, models, command line

    /// <summary>
    /// The Wiki in its tab (MainWindow.ShowWiki, Q18 a): the active sheet is confirmed and the preview of a revision ends, as for the page «Wiki»;
    /// the tab opens or is selected and shows <paramref name="uri"/> when given.
    /// </summary>
    public void ShowWiki(string? uri = null)
    {
        showingWiki = true;
        try { Navigation.Select(ShellPage.Wiki); }
        finally { showingWiki = false; }
        // The requests of the Wiki view are commands of the window: their errors go to the messages (MainWindow.ShowWiki wraps them in Safe).
        if (wiki is null || wiki.IsDisposed) wiki = new WikiTabViewModel((module, example) => guard.Run(() => OpenWikiModule(module, example)), () => guard.Run(ReturnToWork));
        var tab = Windows.ShowWiki(wiki);
        if (!ReferenceEquals(tab.Content, wiki)) wiki = (WikiTabViewModel)tab.Content!;
        if (uri is not null) wiki.Navigate(uri);
    }

    /// <summary>
    /// A module asked by the Wiki (MainWindow.OpenWikiModule): without an example as «Apri» of the catalogue; with an example a new unsaved single
    /// file with the data of the example (WikiExamples), validated before anything opens.
    /// </summary>
    public void OpenWikiModule(string module, string? example)
    {
        if (example is null) { OpenModule(module); return; }
        var data = WikiExamples.Create(module, example);
        ModuleDirectory.ValidateData(module, data);
        var document = Archivio.Documento(module);
        document["dati"] = data;
        Workspace.OpenDocument(document, null, true);
    }

    /// <summary>A module of the catalogue (MainWindow.OpenModule): the last sheet of the module takes the focus again, otherwise a new single file.</summary>
    public void OpenModule(string module)
    {
        ArgumentException.ThrowIfNullOrEmpty(module);
        if (Navigation.Last is { IsDisposed: false } last && last.Module == module) Workspace.Activate(last);
        else Workspace.NewCalculation(module);
    }

    /// <summary>«Torna al lavoro» of the Wiki (MainWindow.ShowWiki): the last sheet, otherwise the project, otherwise the home page.</summary>
    public void ReturnToWork()
    {
        if (Navigation.Last is { IsDisposed: false }) Navigation.Resume();
        else if (Workspace.Project is not null) Navigation.Select(ShellPage.Projects);
        else Navigation.Select(ShellPage.Home);
    }

    /// <summary>
    /// A model of a section in its tab (MainWindow.ShowContainerModel): the active sheet is confirmed, the model is read and validated before the
    /// tab opens; an open tab of the same model is selected. The tree selects the section and the revision bar follows it.
    /// </summary>
    public ModelTabViewModel? ShowModel(JsonObject container, string? modelId)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (Workspace.Project is not { } project) return null;
        if (Windows.Pages.FirstOrDefault(p => p.Content is ModelTabViewModel m && m.ModelId == modelId
            && m.Container.S("id") == container.S("id")) is { } open)
        {
            Windows.Select(open);
            return (ModelTabViewModel)open.Content!;
        }
        var model = new ModelTabViewModel(project, container, modelId, services.Messages);
        Workspace.CommitActive();
        var tab = new PageTabViewModel(ShellTabKind.Model, model.Title, model);
        model.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ModelTabViewModel.Title)) tab.Rename(model.Title); };
        model.BackRequested += (_, _) => Navigation.Select(ShellPage.Models);
        model.OwnerRequested += (_, section) => ShowOverview(section);
        model.NewModelRequested += (_, section) => ShowModel(section, null);
        model.SaveRequested += (_, _) => Workspace.Save(project, false);
        Windows.ShowPage(tab);
        Tree.Refresh(container);
        Revisions?.Show(container);
        return model;
    }

    /// <summary>The command line (App.OnStartup with MainWindow.Loaded): the Wiki, or the document and then the page «Modelli»; errors as messages.</summary>
    public void Start(StartupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Action)
        {
            case StartupAction.Wiki:
                guard.Run(() => ShowWiki(request.Target));
                break;
            case StartupAction.OpenFile:
                guard.Run(() =>
                {
                    Workspace.OpenFile(request.Target!);
                    if (request.Models) Navigation.Select(ShellPage.Models);
                });
                break;
        }
    }

    protected override void OnDisposed()
    {
        Watch(null);
        if (attached is not null) { attached.StateChanged -= OnProjectState; attached.Changed -= OnProjectChanged; }
        Navigation.PropertyChanged -= OnNavigationChanged;
        Commands.Dispose(); Windows.Dispose(); Tree.Dispose(); Navigation.Dispose(); Catalogue.Dispose();
        Models.Dispose(); ModelReference.Dispose(); SheetBar.Dispose(); Appearance?.Dispose(); examples?.Dispose();
        Revisions?.Dispose();
        Workspace.Dispose();
    }

    private JsonObject? SelectedSection() => Tree.Selected?.Node is { } node && !node.ContainsKey("modulo_id") ? node : Tree.SelectedContainer;

    /// <summary>«← Torna…» of the sheet bar (MainWindow): the overview of the section of the sheet in a project, otherwise the home page.</summary>
    private void Back(SheetSession sheet)
    {
        Workspace.CommitActive();
        if (sheet.Project is not null) ShowOverview(sheet.Sheet);
        else Navigation.Select(ShellPage.Home);
    }

    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationViewModel.Page)) return;
        // A page of the navigation (Home, Progetti, Wiki…) ends the full page: the navigation is on screen again.
        if (Navigation.Page is not null) ExitFullPage();
        switch (Navigation.Page)
        {
            case ShellPage.Wiki:
                // The page «Wiki» is the Wiki tab (Q18 a): ShowWiki selects it, which leaves the page.
                if (!showingWiki) guard.Run(() => ShowWiki());
                return;
            case ShellPage.Projects when Overview is null && Workspace.Project is { } project:
                Overview = new SectionOverviewViewModel(project, SelectedSection() ?? project.Projects.FirstOrDefault());
                Revisions?.Show(Overview.Section);
                break;
            case ShellPage.Examples when examples is null && options.Examples is { } catalogue:
                var loaded = catalogue();
                examples = new ExampleLibraryViewModel(loaded, entry => guard.Run(() => Workspace.OpenDocument(loaded.OpenCopy(entry), null, true)), services.Messages);
                OnPropertyChanged(nameof(Examples));
                break;
        }
        UpdateCenter();
    }

    private void UpdateCenter()
    {
        Center = Navigation.Page switch
        {
            ShellPage.Home => Navigation,
            ShellPage.Modules => Navigation.Modules,
            ShellPage.Projects => Overview,
            ShellPage.Models => Models,
            ShellPage.Examples => (object?)examples ?? Navigation,
            _ => Windows.Main.Selected ?? (object)Navigation
        };
        KeepFullPageReachable();
    }

    /// <summary>The full page ends when its sheet bar would leave the screen: no sheet tab in the centre or no sheet with the focus (no hidden state).</summary>
    private void KeepFullPageReachable()
    {
        if (IsFullPage && !(Center is SheetTabViewModel && Workspace.Active is not null)) ExitFullPage();
        RefreshFullPageCommands();
    }

    private void RefreshFullPageCommands()
    {
        OnPropertyChanged(nameof(CanEnterFullPage));
        EnterFullPageCommand.NotifyCanExecuteChanged(); ExitFullPageCommand.NotifyCanExecuteChanged(); ToggleFullPageCommand.NotifyCanExecuteChanged();
    }

    private void OnDocumentsChanged()
    {
        var project = Workspace.Project;
        if (!ReferenceEquals(attached, project))
        {
            if (attached is not null) { attached.StateChanged -= OnProjectState; attached.Changed -= OnProjectChanged; }
            Revisions?.Dispose();
            attached = project;
            Revisions = project is null ? null : new RevisionBarViewModel(project, services);
            if (project is not null) { project.StateChanged += OnProjectState; project.Changed += OnProjectChanged; }
            Overview = null;
            foreach (var model in Windows.Pages.Where(p => p.Content is ModelTabViewModel).ToArray()) Windows.Close(model);
            OnPropertyChanged(nameof(HasProject));
            // As MainWindow after NewProjects, LoadFile and the copy of an example: an archive that opens shows the page «Progetti».
            if (project is not null) ShowOverview(null);
        }
        UpdateTitle();
        UpdateCenter();
    }

    private void OnProjectState(object? sender, EventArgs e) => UpdateTitle();

    /// <summary>
    /// Any change of the project rebuilds the overview on screen (the section found again by identifier), as MainWindow shows it again; the model
    /// tabs follow the document on screen or close.
    /// </summary>
    private void OnProjectChanged(object? sender, ProjectChangedEventArgs e)
    {
        if (Overview is { } shown && attached is { } project)
            Overview = new SectionOverviewViewModel(project, shown.Section is null ? null : project.Find(shown.Section.S("id")) ?? project.Projects.FirstOrDefault());
        foreach (var page in Windows.Pages.Where(p => p.Content is ModelTabViewModel model && !model.Rebind()).ToArray()) Windows.Close(page);
        ModelReference.Update(Workspace.Active);
        UpdateTitle();
    }

    private void OnActiveChanged()
    {
        var active = Workspace.Active;
        Watch(active?.ViewModel);
        SharedStatus.Update(active);
        ModelReference.Update(active);
        SheetBar.Update();
        if (active?.Project is not null) Revisions?.Show(active.Sheet);
        UpdateTitle();
        UpdateCenter();
    }

    private void Watch(ISheetViewModel? sheet)
    {
        if (watched is not null) watched.Modified -= OnModified;
        watched = sheet;
        if (watched is not null) watched.Modified += OnModified;
    }

    private void OnModified(object? sender, EventArgs e) { SharedStatus.Update(Workspace.Active); UpdateTitle(); }

    private void UpdateTitle() => Title = Workspace.CurrentDocument?.Title ?? "ANTHEA";
}
