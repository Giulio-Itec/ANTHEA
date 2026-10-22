using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using Anthea.Calculations;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Shell.Projects;

/// <summary>Kind of a node of the tree of the project.</summary>
public enum ProjectNodeKind
{
    Project,
    Section,
    Sheet,
    Model
}

/// <summary>Where a dragged item would go: nowhere, inside the node, or before/after it (the line of the drop indicator).</summary>
public enum ProjectDropPlace
{
    None,
    Inside,
    Before,
    After
}

/// <summary>Where a sheet asked from the tree is shown besides its tab (F5.13 c, after the checkpoint of 22/10).</summary>
public enum SheetPlacement
{
    /// <summary>«Apri in una nuova finestra»: its tab in a new detached window.</summary>
    NewWindow,

    /// <summary>«Apri a pagina intera»: the main window without the navigation and the tree.</summary>
    FullPage
}

/// <summary>A sheet of the tree opened (or taken, if already open) to be shown in a new window or at full page.</summary>
public sealed class SheetPlacementEventArgs(SheetSession sheet, SheetPlacement placement) : EventArgs
{
    public SheetSession Sheet { get; } = sheet;
    public SheetPlacement Placement { get; } = placement;
}

/// <summary>Outcome of a drag over a node: the place and the hint of the tree header («Inserisci in: …», «Dopo …»).</summary>
public sealed record ProjectDrop(ProjectDropPlace Place, string Hint)
{
    public static ProjectDrop None { get; } = new(ProjectDropPlace.None, "");

    /// <summary>A module dropped from the catalogue copies; sheets and sections move.</summary>
    public bool Copies { get; init; }
}

/// <summary>
/// A node of the tree of the project (MainWindow.RefreshTree and ProjectHierarchy.ConfigureProjectNode): name with the fallback of today,
/// depth, expansion and selection, the commands of the context menu (only outside read-only), the add and report buttons of a section,
/// the coherence badge, the tooltips and automation names of today.
/// </summary>
public sealed class ProjectNodeViewModel : ViewModelBase
{
    private readonly ProjectTreeViewModel tree;
    private bool isExpanded, isSelected, isRenaming, isUnsaved;
    private string renameText = "";

    internal ProjectNodeViewModel(ProjectTreeViewModel tree, ProjectNodeKind kind, JsonObject? node, int depth, bool readOnly,
        JsonObject? container = null, string? modelId = null, string? modelName = null)
    {
        this.tree = tree; Kind = kind; Node = node; Depth = depth; Container = container; ModelId = modelId;
        bool editable = !readOnly && kind != ProjectNodeKind.Model;
        Name = kind switch
        {
            ProjectNodeKind.Model => "▧  " + modelName,
            ProjectNodeKind.Sheet => ShellTexts.SheetName(node!),
            ProjectNodeKind.Project => node!.S("nome", "Progetto"),
            _ => node!.S("nome", "Sezione")
        };
        Module = kind == ProjectNodeKind.Sheet ? node!.S("modulo_id") : null;
        CanRename = editable;
        CanDelete = editable;
        CanDuplicate = editable && kind is ProjectNodeKind.Project or ProjectNodeKind.Section;
        CanAddSection = editable && kind is ProjectNodeKind.Project or ProjectNodeKind.Section;
        if (kind is ProjectNodeKind.Project or ProjectNodeKind.Section)
        {
            CanReport = ProjectSharedData.SubtreeSheets(node!).Any();
            ReportToolTip = CanReport ? "Report Word della sezione, comprese tutte le sottosezioni" : "Aggiungi una scheda alla sezione o alle sue sottosezioni";
            ReportAutomationName = "Genera report " + node!.S("nome");
            Badge = SharedDataCoordinator.Coherence(node!);
            ToolTip = readOnly ? "Apri il riepilogo" : (depth == 0 ? "Trascina per riordinare i progetti" : "Trascina sul bordo per riordinare, al centro per inserire in una sezione") + " · Doppio clic per rinominare · F2";
        }
        else if (kind == ProjectNodeKind.Sheet)
        {
            ToolTip = "Doppio clic per rinominare · F2";
            OpenToolTip = "Apri scheda · " + Name;
            OpenAutomationName = "Apri scheda " + node!.S("nome");
        }
        else ToolTip = "Apri mesh, volumi e risultati";
        OpenCommand = GuardedCommands.Create(tree.Guard, () => tree.Open(this));
        // F5.13 c, after the checkpoint of 22/10: a sheet opens also in a new window or at full page, read-only revisions included.
        OpenInNewWindowCommand = GuardedCommands.Create(tree.Guard, () => tree.OpenElsewhere(this, SheetPlacement.NewWindow), () => IsSheet);
        OpenFullPageCommand = GuardedCommands.Create(tree.Guard, () => tree.OpenElsewhere(this, SheetPlacement.FullPage), () => IsSheet);
        BeginRenameCommand = GuardedCommands.Create(tree.Guard, () => tree.BeginRename(this), () => CanRename);
        // The double click on the name (ProjectHierarchy): the rename in place, nothing while read-only; a model opens (it has no name to edit).
        DoubleClickCommand = GuardedCommands.Create(tree.Guard, () => { if (kind == ProjectNodeKind.Model) tree.Open(this); else tree.BeginRename(this); });
        DuplicateCommand = GuardedCommands.Create(tree.Guard, () => tree.Duplicate(this), () => CanDuplicate);
        DeleteCommand = GuardedCommands.Create(tree.Guard, () => tree.Delete(this), () => CanDelete);
        AddSectionCommand = GuardedCommands.Create(tree.Guard, () => tree.AddSectionTo(this), () => CanAddSection);
        ReportCommand = GuardedCommands.Create(tree.Guard, () => tree.RequestReport(this), () => CanReport);
    }

    public ProjectNodeKind Kind { get; }

    /// <summary>The project, section or sheet; null for a model.</summary>
    public JsonObject? Node { get; }

    /// <summary>Section of a model.</summary>
    public JsonObject? Container { get; }

    public string? ModelId { get; }

    /// <summary>Identifier of the project, section or sheet; empty for a model.</summary>
    public string Id => Node?.S("id") ?? "";

    public string Name { get; }

    /// <summary>0 for a project, 1 for its sections, and so on (the size of the text in the view).</summary>
    public int Depth { get; }

    /// <summary>Module of a sheet (its icon).</summary>
    public string? Module { get; }

    /// <summary>The node is a sheet: «Apri in una nuova finestra» and «Apri a pagina intera» are offered.</summary>
    public bool IsSheet => Kind == ProjectNodeKind.Sheet;

    public ObservableCollection<ProjectNodeViewModel> Children { get; } = [];

    public bool IsExpanded { get => isExpanded; set => SetProperty(ref isExpanded, value); }

    public bool IsSelected
    {
        get => isSelected;
        set { if (SetProperty(ref isSelected, value) && value) tree.Selected = this; }
    }

    public bool CanRename { get; }
    public bool CanDelete { get; }
    public bool CanDuplicate { get; }
    public bool CanAddSection { get; }
    public bool CanReport { get; }
    public string? ReportToolTip { get; }
    public string? ReportAutomationName { get; }
    public CoherenceBadge? Badge { get; }

    /// <summary>Tooltip of the label of the node.</summary>
    public string ToolTip { get; }

    /// <summary>Tooltip and automation name of the button with the icon of a sheet.</summary>
    public string? OpenToolTip { get; }

    /// <inheritdoc cref="OpenToolTip"/>
    public string? OpenAutomationName { get; }

    /// <summary>The sheet is open in a tab with data not saved (proposal 3.6, the «•» of the tree; SH15).</summary>
    public bool IsUnsaved { get => isUnsaved; internal set => SetProperty(ref isUnsaved, value); }

    /// <summary>True while the name is edited in place.</summary>
    public bool IsRenaming { get => isRenaming; internal set => SetProperty(ref isRenaming, value); }

    /// <summary>Text of the rename in place, the current name at its start.</summary>
    public string RenameText { get => renameText; set => SetProperty(ref renameText, value ?? ""); }

    public IRelayCommand OpenCommand { get; }

    /// <summary>«Apri in una nuova finestra» of a sheet: its tab (opened or taken) in a new detached window.</summary>
    public IRelayCommand OpenInNewWindowCommand { get; }

    /// <summary>«Apri a pagina intera» of a sheet: its tab (opened or activated) with the navigation and the tree hidden.</summary>
    public IRelayCommand OpenFullPageCommand { get; }

    public IRelayCommand BeginRenameCommand { get; }

    /// <summary>Double click on the name: «Doppio clic per rinominare» (sheets, sections, projects), «Apri» for a model.</summary>
    public IRelayCommand DoubleClickCommand { get; }
    public IRelayCommand DuplicateCommand { get; }
    public IRelayCommand DeleteCommand { get; }
    public IRelayCommand AddSectionCommand { get; }
    public IRelayCommand ReportCommand { get; }

    /// <summary>Ends the rename in place: Enter, focus lost or a click outside save (true), Esc cancels (false).</summary>
    public void EndRename(bool save) => tree.EndRename(this, save);
}

/// <summary>
/// The tree of the open project (F5.13 b, proposal 4.3; MainWindow.RefreshTree, ProjectHierarchy, ProjectSectionReorder,
/// ProjectMovePreview, ProjectPresentation): nodes rebuilt from the document on screen with the expansion remembered by identifier and
/// the ancestors of the selection expanded; the selection follows the sheet with the focus; summary and empty state; the commands of the
/// nodes and of the header, with the questions of the project session; the rules and the hints of the drag and drop. The pages that
/// MainWindow shows after a command (the overview of a section, a model, the report of a section) are asked through events.
/// </summary>
public sealed class ProjectTreeViewModel : ViewModelBase
{
    public const string Title = "Struttura del progetto";
    public const string Instruction = "Organizza sezioni e fogli con il trascinamento.";
    public const string EmptyTitle = "Il tuo progetto parte da qui";
    public const string EmptyText = "Crea un progetto, aggiungi le sezioni e trascina i fogli dal catalogo.";
    public const string CreateProject = "+  Crea progetto";
    public const string AddProjectLabel = "+ Progetto";
    public const string AddSectionLabel = "+ Sezione";
    public const string AddSectionToolTip = "Aggiungi sottosezione";
    /// <summary>Message of a sheet added without a section (MainWindow.AddSheetTo).</summary>
    public const string SelectSection = "Selezionare una sezione nell'albero dei progetti.";
    /// <summary>Items of the menu of a sheet (F5.13 c, after the checkpoint of 22/10).</summary>
    public const string OpenInNewWindowLabel = "Apri in una nuova finestra";
    /// <inheritdoc cref="OpenInNewWindowLabel"/>
    public const string OpenFullPageLabel = "Apri a pagina intera";

    private readonly WorkspaceSession workspace;
    private readonly IMessageService messages;
    private readonly HashSet<string> collapsed = [];
    private ProjectSession? project;
    private ProjectNodeViewModel? selected;
    private ProjectNodeViewModel? renaming;
    private string summary = "";
    private bool isEmpty = true;

    public ProjectTreeViewModel(WorkspaceSession workspace, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(messages);
        this.workspace = workspace; this.messages = messages;
        Guard = new CommandGuard(messages);
        AddProjectCommand = GuardedCommands.Create(Guard, () => AddProject(), () => project is { IsReadOnly: false });
        AddSectionCommand = GuardedCommands.Create(Guard, AddSection, () => project is { IsReadOnly: false });
        workspace.DocumentsChanged += OnDocumentsChanged;
        workspace.ActiveChanged += OnActiveChanged;
        workspace.SheetOpened += OnSheetOpened;
        workspace.SheetClosed += OnSheetsChanged;
        Attach();
    }

    internal CommandGuard Guard { get; }

    /// <summary>The project of the tree, null without one.</summary>
    public ProjectSession? Project => project;

    public ObservableCollection<ProjectNodeViewModel> Roots { get; } = [];

    /// <summary>The selected node (MainWindow.selectedProjectId); the overview and the revision bar follow it.</summary>
    public ProjectNodeViewModel? Selected
    {
        get => selected;
        set
        {
            if (ReferenceEquals(selected, value)) return;
            if (selected is not null) selected.IsSelected = false;
            selected = value;
            if (value is not null) value.IsSelected = true;
            OnPropertyChanged();
        }
    }

    /// <summary>«n progetti · n sezioni · n fogli», hidden when the tree is empty.</summary>
    public string Summary { get => summary; private set => SetProperty(ref summary, value); }

    /// <summary>No project in the archive: the empty state with «+  Crea progetto» (not while read-only).</summary>
    public bool IsEmpty { get => isEmpty; private set => SetProperty(ref isEmpty, value); }

    /// <summary>The structure buttons and the empty state button are hidden while read-only.</summary>
    public bool IsReadOnly => project?.IsReadOnly ?? false;

    public IRelayCommand AddProjectCommand { get; }

    /// <summary>«+ Sezione»: in the selected project or section (the section of a selected sheet), otherwise a new project.</summary>
    public IRelayCommand AddSectionCommand { get; }

    /// <summary>A command shows the overview of a section (null: the first project), as MainWindow.ShowProjectOverview.</summary>
    public event EventHandler<JsonObject?>? OverviewRequested;

    /// <summary>The report of a section is asked (the report button of a section, MainWindow.ExportSectionReport).</summary>
    public event EventHandler<JsonObject>? ReportRequested;

    /// <summary>A model of a section is opened (MainWindow.ShowContainerModel).</summary>
    public event EventHandler<(JsonObject Container, string ModelId)>? ModelRequested;

    /// <summary>
    /// A sheet was opened, or taken if already open, by «Apri in una nuova finestra» or «Apri a pagina intera»: the shell moves its tab or hides
    /// the navigation and the tree. Without a shell the sheet stays in its tab.
    /// </summary>
    public event EventHandler<SheetPlacementEventArgs>? PlacementRequested;

    /// <summary>
    /// Rebuilds the tree from the document on screen (MainWindow.RefreshTree): an open rename ends saving; the expansion is remembered by
    /// identifier; the ancestors of <paramref name="selection"/> (by default the selected node, else the sheet with the focus) expand; the
    /// children are the models, the sheets and the sections, in this order.
    /// </summary>
    public void Refresh(JsonObject? selection = null)
    {
        if (renaming is not null) EndRename(renaming, true);
        foreach (var root in Roots) Remember(root);
        selection ??= Selected?.Node is { } node && project?.Find(node.S("id")) is { } found ? found : workspace.Active?.Sheet;
        for (var parent = selection?.Parent?.Parent as JsonObject; parent is not null; parent = parent.Parent?.Parent as JsonObject) collapsed.Remove(parent.S("id"));
        selected = null;
        Roots.Clear();
        ProjectNodeViewModel? match = null;
        if (project is not null)
        {
            bool readOnly = project.IsReadOnly;
            ProjectNodeViewModel Build(JsonObject value, ProjectNodeKind kind, int depth)
            {
                var item = new ProjectNodeViewModel(this, kind, value, depth, readOnly) { IsExpanded = !collapsed.Contains(value.S("id")) };
                foreach (var model in ProjectModelStore.Models(value))
                    item.Children.Add(new ProjectNodeViewModel(this, ProjectNodeKind.Model, null, depth + 1, readOnly, value, model.S("id"), model.S("nome")));
                foreach (var sheet in value.Array("fogli").OfType<JsonObject>())
                {
                    var leaf = new ProjectNodeViewModel(this, ProjectNodeKind.Sheet, sheet, depth + 1, readOnly) { IsUnsaved = workspace.SheetOf(sheet)?.IsUnsaved == true };
                    item.Children.Add(leaf);
                    if (ReferenceEquals(sheet, selection)) match = leaf;
                }
                foreach (var child in value.Array("strutture").OfType<JsonObject>()) item.Children.Add(Build(child, ProjectNodeKind.Section, depth + 1));
                if (ReferenceEquals(value, selection)) match = item;
                return item;
            }
            foreach (var root in project.Projects) Roots.Add(Build(root, ProjectNodeKind.Project, 0));
            var roots = project.Projects.ToArray();
            int sections = roots.Sum(p => ProjectSharedData.Sections(p).Count() - 1), sheets = roots.Sum(p => ProjectSharedData.SubtreeSheets(p).Count());
            Summary = $"{roots.Length} {(roots.Length == 1 ? "progetto" : "progetti")} · {sections} {(sections == 1 ? "sezione" : "sezioni")} · {sheets} {(sheets == 1 ? "foglio" : "fogli")}";
        }
        else Summary = "";
        IsEmpty = Roots.Count == 0;
        OnPropertyChanged(nameof(IsReadOnly));
        AddProjectCommand.NotifyCanExecuteChanged(); AddSectionCommand.NotifyCanExecuteChanged();
        Selected = match;
    }

    /// <summary>The node of <paramref name="node"/> in the tree, or null.</summary>
    public ProjectNodeViewModel? Find(JsonObject node)
    {
        IEnumerable<ProjectNodeViewModel> All(IEnumerable<ProjectNodeViewModel> items) => items.SelectMany(i => new[] { i }.Concat(All(i.Children)));
        return All(Roots).FirstOrDefault(i => ReferenceEquals(i.Node, node));
    }

    /// <summary>The project or section of the selection (MainWindow.SelectedProjectContainer): the section of a selected sheet; null for a model.</summary>
    public JsonObject? SelectedContainer => Selected?.Node is not { } value ? null : value.ContainsKey("modulo_id") ? value.Parent?.Parent as JsonObject : value;

    /// <summary>«+ Progetto» (MainWindow.AddProject): a new project, then its overview.</summary>
    public JsonObject? AddProject()
    {
        if (workspace.EnsureProject() is not { } session || session.AddProject() is not { } added) return null;
        Refresh(added);
        OverviewRequested?.Invoke(this, added);
        return added;
    }

    /// <summary>«+ Sezione» (MainWindow.AddStructure): in the selected container, or a new project when nothing is selected.</summary>
    public void AddSection()
    {
        if (SelectedContainer is not { } target) { AddProject(); return; }
        AddSectionTo(target);
    }

    /// <summary>
    /// A sheet of <paramref name="module"/> (MainWindow.AddSheetTo): in <paramref name="section"/>, by default the selected container; without a
    /// section the note of today. Opened in a tab when <paramref name="open"/>, otherwise the overview of the section is shown.
    /// </summary>
    public JsonObject? AddSheet(string module, JsonObject? section = null, bool open = true)
    {
        if (project is not { IsReadOnly: false } session) return null;
        section ??= SelectedContainer;
        if (!ProjectSession.CanHoldSheets(section)) { messages.Inform("", SelectSection); return null; }
        var sheet = session.AddSheet(module, section!);
        Refresh(sheet);
        if (sheet is null) return null;
        if (open) workspace.OpenSheet(sheet);
        else OverviewRequested?.Invoke(this, section);
        return sheet;
    }

    // ---- Commands of the nodes

    internal void Open(ProjectNodeViewModel item)
    {
        switch (item.Kind)
        {
            case ProjectNodeKind.Sheet: workspace.OpenSheet(item.Node!); break;
            case ProjectNodeKind.Model: workspace.CommitActive(); ModelRequested?.Invoke(this, (item.Container!, item.ModelId!)); break;
            default: workspace.Activate(null); Selected = item; OverviewRequested?.Invoke(this, item.Node); break;
        }
    }

    /// <summary>
    /// «Apri in una nuova finestra», «Apri a pagina intera»: the sheet opens as with «Apri» (an open sheet is only activated, the active one
    /// confirmed first; during the preview of a revision read-only), then the shell places it.
    /// </summary>
    internal void OpenElsewhere(ProjectNodeViewModel item, SheetPlacement placement)
    {
        if (!item.IsSheet) return;
        var sheet = workspace.OpenSheet(item.Node!);
        PlacementRequested?.Invoke(this, new SheetPlacementEventArgs(sheet, placement));
    }

    internal void AddSectionTo(ProjectNodeViewModel item) => AddSectionTo(item.Node!);

    private void AddSectionTo(JsonObject parent)
    {
        if (project?.AddSection(parent) is not { } section) return;
        Refresh(section);
        OverviewRequested?.Invoke(this, section);
    }

    internal void Duplicate(ProjectNodeViewModel item)
    {
        if (project?.Duplicate(item.Node!) is not { } copy) return;
        Refresh(copy);
        OverviewRequested?.Invoke(this, copy);
    }

    internal void Delete(ProjectNodeViewModel item)
    {
        if (project is null || !project.Delete(item.Node!, out var overview)) return;
        Refresh(overview);
        OverviewRequested?.Invoke(this, overview);
    }

    internal void RequestReport(ProjectNodeViewModel item) => ReportRequested?.Invoke(this, item.Node!);

    /// <summary>Starts the rename in place (F2, double click, «Rinomina»): another rename ends first, saving.</summary>
    internal void BeginRename(ProjectNodeViewModel item)
    {
        if (project is not { IsReadOnly: false } || !item.CanRename) return;
        if (ReferenceEquals(renaming, item)) return;
        if (renaming is not null)
        {
            // Saving the open rename rebuilds the tree: continue on the new node of the same element.
            EndRename(renaming, true);
            item = Find(item.Node!) ?? item;
        }
        item.RenameText = item.Node!.S("nome", item.Name);
        item.IsRenaming = true;
        renaming = item;
    }

    /// <summary>Ends a rename (the Finish of ProjectHierarchy): saved when asked, the name trimmed, not empty and changed.</summary>
    internal void EndRename(ProjectNodeViewModel item, bool save)
    {
        if (!item.IsRenaming) return;
        item.IsRenaming = false;
        if (ReferenceEquals(renaming, item)) renaming = null;
        if (save && project is not null && project.Rename(item.Node!, item.RenameText)) Refresh(item.Node);
    }

    /// <summary>Ends an open rename, saving it (before a revision switch, a deletion or a refresh, as finishProjectRename).</summary>
    public void FinishRename()
    {
        if (renaming is not null) EndRename(renaming, true);
    }

    // ---- Drag and drop (ProjectHierarchy, ProjectSectionReorder)

    /// <summary>
    /// What a drop of <paramref name="payload"/> (a module id from the catalogue, or a sheet or section of the document) on
    /// <paramref name="target"/> would do, at <paramref name="relativeY"/> of the height of its row (0 top, 1 bottom):
    /// <list type="bullet">
    /// <item>a section on a section: inside in the middle half when it can nest («Sposta dentro: …»), otherwise before or after its siblings;</item>
    /// <item>a module or a sheet on a project or section: inside («Inserisci in: …»);</item>
    /// <item>a sheet on a sheet: before or after it («Prima di …», «Dopo …», with the section).</item>
    /// </list>
    /// </summary>
    public ProjectDrop PreviewDrop(ProjectNodeViewModel target, object payload, double relativeY)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (project is not { } session || target.Node is not { } anchor) return ProjectDrop.None;
        bool after = relativeY >= .5;
        if (target.Kind == ProjectNodeKind.Sheet)
            return payload is JsonObject sheet && session.CanReorderSheet(anchor, sheet)
                ? new(after ? ProjectDropPlace.After : ProjectDropPlace.Before, (after ? "Dopo " : "Prima di ") + anchor.S("nome") + " · " + anchor.Parent?.Parent?.S("nome"))
                : ProjectDrop.None;
        if (payload is JsonObject section && !section.ContainsKey("modulo_id"))
        {
            if (relativeY is > .25 and < .75 && session.CanNestSection(section, anchor)) return new(ProjectDropPlace.Inside, "Sposta dentro: " + anchor.S("nome"));
            return session.CanReorderSection(section, anchor) ? new(after ? ProjectDropPlace.After : ProjectDropPlace.Before, (after ? "Dopo " : "Prima di ") + anchor.S("nome")) : ProjectDrop.None;
        }
        if (payload is string module && session.CanDropModule(anchor, module)) return new(ProjectDropPlace.Inside, "Inserisci in: " + anchor.S("nome")) { Copies = true };
        if (payload is JsonObject moved && session.CanDropSheet(anchor, moved)) return new(ProjectDropPlace.Inside, "Inserisci in: " + anchor.S("nome"));
        return ProjectDrop.None;
    }

    /// <summary>Drops <paramref name="payload"/> on <paramref name="target"/> as <see cref="PreviewDrop"/> says; true when the project changed.</summary>
    public bool Drop(ProjectNodeViewModel target, object payload, double relativeY)
    {
        var drop = PreviewDrop(target, payload, relativeY);
        if (drop.Place == ProjectDropPlace.None || project is not { } session) return false;
        var anchor = target.Node!;
        bool after = drop.Place == ProjectDropPlace.After;
        if (target.Kind == ProjectNodeKind.Sheet)
        {
            var sheet = (JsonObject)payload;
            var destination = (JsonObject)anchor.Parent!.Parent!;
            return Moved(session.MoveSheet(sheet, destination, anchor, after), sheet, destination);
        }
        if (payload is string module) return AddSheet(module, anchor, false) is not null;
        var node = (JsonObject)payload;
        if (node.ContainsKey("modulo_id")) return Moved(session.MoveSheet(node, anchor), node, anchor);
        if (drop.Place == ProjectDropPlace.Inside) return Moved(session.NestSection(node, anchor), node, node);
        return Moved(session.ReorderSection(node, anchor, after), node, null);
    }

    private bool Moved(bool moved, JsonObject node, JsonObject? overview)
    {
        if (!moved) return false;
        Refresh(node);
        if (overview is not null && workspace.Active is null) OverviewRequested?.Invoke(this, overview);
        return true;
    }

    protected override void OnDisposed()
    {
        workspace.DocumentsChanged -= OnDocumentsChanged;
        workspace.ActiveChanged -= OnActiveChanged;
        workspace.SheetOpened -= OnSheetOpened;
        workspace.SheetClosed -= OnSheetsChanged;
        if (project is not null) project.Changed -= OnProjectChanged;
    }

    private void Attach()
    {
        if (ReferenceEquals(project, workspace.Project)) return;
        if (project is not null) project.Changed -= OnProjectChanged;
        project = workspace.Project;
        if (project is not null) project.Changed += OnProjectChanged;
        collapsed.Clear();
        Refresh();
    }

    private void Remember(ProjectNodeViewModel item)
    {
        if (item.Node is { } node && item.Kind != ProjectNodeKind.Sheet) { if (item.IsExpanded) collapsed.Remove(node.S("id")); else collapsed.Add(node.S("id")); }
        foreach (var child in item.Children) Remember(child);
    }

    private void OnDocumentsChanged(object? sender, EventArgs e) => Attach();

    private void OnProjectChanged(object? sender, ProjectChangedEventArgs e) => Refresh(e.Node);

    private void OnActiveChanged(object? sender, EventArgs e)
    {
        if (workspace.Active is { Project: { } } sheet && ReferenceEquals(sheet.Project, project)) Refresh(sheet.Sheet);
    }

    private void OnSheetOpened(object? sender, SheetSession e) => OnSheetsChanged(sender, EventArgs.Empty);

    private void OnSheetsChanged(object? sender, EventArgs e)
    {
        if (project is not null) Refresh();
    }
}

/// <summary>A card of the catalogue of the sheets to add (ProjectPresentation.BuildProjectCatalog).</summary>
public sealed class ProjectCatalogCard(string module, string title, string subtitle, string search) : ViewModelBase
{
    private bool isVisible = true;

    public string Module { get; } = module;
    public string Title { get; } = title;
    public string Subtitle { get; } = subtitle;
    public string ToolTip => "Trascina nella sezione oppure fai doppio clic per aggiungere.";
    internal string Search { get; } = search;
    public bool IsVisible { get => isVisible; internal set => SetProperty(ref isVisible, value); }
}

/// <summary>A group of the catalogue: the area in capitals, expanded at the start and when a search finds its cards.</summary>
public sealed class ProjectCatalogGroup(string title, IReadOnlyList<ProjectCatalogCard> cards) : ViewModelBase
{
    private bool isVisible = true, isExpanded = true;

    public string Title { get; } = title;
    public IReadOnlyList<ProjectCatalogCard> Cards { get; } = cards;
    public bool IsVisible { get => isVisible; internal set => SetProperty(ref isVisible, value); }
    public bool IsExpanded { get => isExpanded; set => SetProperty(ref isExpanded, value); }
}

/// <summary>
/// The catalogue «Schede da aggiungere» beside the tree (ProjectPresentation.BuildProjectCatalog): the modules of the archive by area, with
/// project title and subtitle; the search ignores case and accents in the current culture, hides the empty groups, expands the groups
/// with results and shows «Nessuna scheda corrispondente.» when nothing matches.
/// </summary>
public sealed class ProjectCatalogViewModel : ViewModelBase
{
    public const string Title = "Schede da aggiungere";
    public const string SearchPlaceholder = "Cerca una scheda…";
    public const string SearchAutomationName = "Cerca una scheda";
    public const string Hint = "Trascina una scheda nella struttura.";
    public const string NoResults = "Nessuna scheda corrispondente.";

    private string filter = "";
    private bool hasNoResults;

    public ProjectCatalogViewModel()
    {
        Groups = Archivio.Moduli.GroupBy(id => Anthea.Application.Modules.ModuleDirectory.Get(id).Area.ToUpperInvariant())
            .Select(group => new ProjectCatalogGroup(group.Key, group.Select(id =>
            {
                var module = Anthea.Application.Modules.ModuleDirectory.Get(id);
                return new ProjectCatalogCard(id, module.ProjectTitle, module.ProjectSubtitle,
                    group.Key + " " + module.ProjectTitle + " " + module.ProjectSubtitle + " " + ShellTexts.ModuleName(id));
            }).ToArray())).ToArray();
    }

    public IReadOnlyList<ProjectCatalogGroup> Groups { get; }

    /// <summary>Text of the search; the cards and groups follow it.</summary>
    public string Filter
    {
        get => filter;
        set
        {
            if (!SetProperty(ref filter, value ?? "")) return;
            string text = filter.Trim();
            int matches = 0;
            foreach (var group in Groups)
            {
                int visible = 0;
                foreach (var card in group.Cards)
                {
                    bool match = System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(card.Search, text,
                        System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
                    card.IsVisible = match;
                    if (match) visible++;
                }
                group.IsVisible = visible > 0;
                if (text.Length > 0 && visible > 0) group.IsExpanded = true;
                matches += visible;
            }
            HasNoResults = matches == 0;
            OnPropertyChanged(nameof(ShowsPlaceholder));
        }
    }

    /// <summary>The placeholder shows while the search is empty.</summary>
    public bool ShowsPlaceholder => filter.Trim().Length == 0;

    public bool HasNoResults { get => hasNoResults; private set => SetProperty(ref hasNoResults, value); }
}
