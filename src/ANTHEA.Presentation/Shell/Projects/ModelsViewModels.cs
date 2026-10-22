using System.Text.Json.Nodes;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using Anthea.Calculations;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Shell.Projects;

/// <summary>A model of a section, or «+ Modello» (null identifier), on the page «Modelli».</summary>
public sealed record ModelLink(JsonObject Container, string? Id, string Name);

/// <summary>A card of the page «Modelli»: a section of a project, its path, «N modelli · N fogli», its models and «+ Modello».</summary>
public sealed record ModelContainerCard(JsonObject Container, string Path, string Summary, IReadOnlyList<ModelLink> Models, ModelLink NewModel);

/// <summary>A model of a section asked by the shell (null identifier: a new model).</summary>
public sealed class ModelRequestEventArgs(JsonObject container, string? modelId) : EventArgs
{
    public JsonObject Container { get; } = container;
    public string? ModelId { get; } = modelId;
}

/// <summary>Texts of the models shared by the page, the tab and the notice above a sheet (MainWindow, ProjectModels.cs).</summary>
public static class ModelTexts
{
    /// <summary>The names of a section and of the sections above it, from the project down, separated by « / » (MainWindow.ContainerPath).</summary>
    public static string ContainerPath(JsonObject container)
    {
        ArgumentNullException.ThrowIfNull(container);
        var names = new List<string>();
        for (JsonObject? node = container; node is not null; node = node.Parent?.Parent as JsonObject)
            if (node["nome"] is not null) names.Add(node.S("nome"));
        names.Reverse();
        return string.Join(" / ", names);
    }
}

/// <summary>
/// The page «Modelli» (F5.13 c; MainWindow.ShowModelsHub): title, subtitle and the draft note; without a project the invitation with
/// «Apri progetto…» and «Nuovo archivio progetti»; with a project a card for every section of every project (ProjectModelStore.Containers),
/// with its models and «+ Modello», then «Organizza progetti e sottofasi». The cards follow the changes of the project. What only the shell
/// can do (open a file, a model tab, the page «Progetti») is asked through events.
/// </summary>
public sealed class ModelsPageViewModel : ViewModelBase
{
    public const string Title = "Modelli di calcolo";
    public const string Subtitle = "Mesh, volumi e risultati, accanto ai fogli di verifica della stessa fase o sottofase.";
    public const string Draft = "BOZZA · copie importate e riferimenti versionati";
    public const string NoProject = "Crea o apri un archivio progetti per organizzare i modelli.";
    public const string OpenProjectLabel = "Apri progetto…";
    public const string NewProjectLabel = "Nuovo archivio progetti";
    public const string NewModelLabel = "+ Modello";
    public const string OrganizeLabel = "Organizza progetti e sottofasi";

    private readonly WorkspaceSession workspace;
    private ProjectSession? project;
    private IReadOnlyList<ModelContainerCard> cards = [];

    public ModelsPageViewModel(WorkspaceSession workspace, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(messages);
        this.workspace = workspace;
        var guard = new CommandGuard(messages);
        OpenProjectCommand = GuardedCommands.Create(guard, () => OpenProjectRequested?.Invoke(this, EventArgs.Empty));
        NewProjectCommand = GuardedCommands.Create(guard, () => workspace.NewProject());
        OpenModelCommand = new RelayCommand<ModelLink>(link => guard.Run(() => ModelRequested?.Invoke(this, new ModelRequestEventArgs(link!.Container, link.Id))), link => link is not null);
        OrganizeCommand = GuardedCommands.Create(guard, () => ProjectsRequested?.Invoke(this, EventArgs.Empty));
        workspace.DocumentsChanged += OnDocumentsChanged;
        Attach();
        Refresh();
    }

    /// <summary>A project archive is open.</summary>
    public bool HasProject => workspace.Project is not null;

    /// <summary>The sections with their models, in the order of the projects and of the tree.</summary>
    public IReadOnlyList<ModelContainerCard> Cards { get => cards; private set => SetProperty(ref cards, value); }

    public IRelayCommand OpenProjectCommand { get; }
    public IRelayCommand NewProjectCommand { get; }
    public IRelayCommand<ModelLink> OpenModelCommand { get; }
    public IRelayCommand OrganizeCommand { get; }

    /// <summary>«Apri progetto…»: the shell runs «Apri…».</summary>
    public event EventHandler? OpenProjectRequested;

    /// <summary>A model, or a new model of a section.</summary>
    public event EventHandler<ModelRequestEventArgs>? ModelRequested;

    /// <summary>«Organizza progetti e sottofasi»: the page «Progetti».</summary>
    public event EventHandler? ProjectsRequested;

    protected override void OnDisposed()
    {
        workspace.DocumentsChanged -= OnDocumentsChanged;
        if (project is not null) project.Changed -= OnProjectChanged;
    }

    private void OnDocumentsChanged(object? sender, EventArgs e) { Attach(); Refresh(); }

    private void OnProjectChanged(object? sender, ProjectChangedEventArgs e) => Refresh();

    private void Attach()
    {
        if (ReferenceEquals(project, workspace.Project)) return;
        if (project is not null) project.Changed -= OnProjectChanged;
        project = workspace.Project;
        if (project is not null) project.Changed += OnProjectChanged;
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasProject));
        Cards = project is null ? [] : project.Projects.SelectMany(ProjectModelStore.Containers).Select(container =>
        {
            var models = ProjectModelStore.Models(container).Select(m => new ModelLink(container, m.S("id"), m.S("nome"))).ToArray();
            return new ModelContainerCard(container, ModelTexts.ContainerPath(container), $"{models.Length} modelli · {container.Array("fogli").Count} fogli", models,
                new ModelLink(container, null, NewModelLabel));
        }).ToArray();
    }
}

/// <summary>
/// The tab of a model (F5.13 c, proposal 4.1; MainWindow.ShowContainerModel): the model is read and validated before the tab opens; the title
/// is its name or «Nuovo modello»; the path of the section with « · sola lettura» during the preview of a revision; the commands of the bar
/// («← Modelli», «Progetto di appartenenza», «+ Modello», disabled in read-only, «Salva progetto»), the rename of the model, the sheets of the
/// section with the state of their link, the import and the link from the viewer, which change the project as a change of data (the open
/// sheets reconcile). The viewer stays the existing ModelViewerControl, created by the view with these values and callbacks.
/// </summary>
public sealed class ModelTabViewModel : ViewModelBase
{
    public const string NewModelTitle = "Nuovo modello";
    public const string ImportFirst = "Importare prima il modello.";
    public const string BackLabel = "← Modelli";
    public const string OwnerLabel = "Progetto di appartenenza";
    public const string NewModelLabel = "+ Modello";
    public const string SaveLabel = "Salva progetto";
    public const string RenameHeader = "Nome del modello";
    public const string RenameLabel = "Rinomina";

    private readonly ProjectSession project;
    private string title;
    private string renameText;
    private bool readOnly;

    /// <param name="project">The open project.</param>
    /// <param name="container">The section of the model, in the document on screen.</param>
    /// <param name="modelId">The model, null for a new model.</param>
    /// <exception cref="InvalidDataException">The model is not readable (MainWindow reads it before removing the page on screen).</exception>
    public ModelTabViewModel(ProjectSession project, JsonObject container, string? modelId, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(messages);
        if (!project.Contains(container)) throw new ArgumentException("Sezione non presente nel progetto aperto.", nameof(container));
        this.project = project;
        readOnly = project.IsReadOnly;
        Container = container;
        ModelId = modelId;
        Snapshot = modelId is null ? null : ProjectModelStore.Read(container, modelId);
        title = renameText = modelId is null ? NewModelTitle : ProjectModelStore.Models(container).Single(m => m.S("id") == modelId).S("nome");
        Targets = ReadTargets(container);
        var guard = new CommandGuard(messages);
        BackCommand = GuardedCommands.Create(guard, () => BackRequested?.Invoke(this, EventArgs.Empty));
        OwnerCommand = GuardedCommands.Create(guard, () => OwnerRequested?.Invoke(this, Container));
        NewModelCommand = GuardedCommands.Create(guard, () => NewModelRequested?.Invoke(this, Container), () => !IsReadOnly);
        SaveCommand = GuardedCommands.Create(guard, () => SaveRequested?.Invoke(this, EventArgs.Empty));
        RenameCommand = GuardedCommands.Create(guard, () => Rename(RenameText));
    }

    /// <summary>The section of the model.</summary>
    public JsonObject Container { get; private set; }

    /// <summary>The model, null until a new model is imported.</summary>
    public string? ModelId { get; private set; }

    /// <summary>The model read from the section, null for a new model.</summary>
    public ModelSnapshot? Snapshot { get; private set; }

    /// <summary>The sheets of the section and of the sections below it, with the state of their link to a model.</summary>
    public IReadOnlyList<SheetTarget> Targets { get; private set; }

    public string Title { get => title; private set => SetProperty(ref title, value); }

    /// <summary>The path of the section, « · sola lettura» during the preview of a revision.</summary>
    public string PathText => ModelTexts.ContainerPath(Container) + (IsReadOnly ? " · sola lettura" : "");

    /// <summary>The preview of an archived revision is on screen (raised only when it changes: the view recreates the viewer then).</summary>
    public bool IsReadOnly => readOnly;

    /// <summary>The text of the field «Nome del modello».</summary>
    public string RenameText { get => renameText; set => SetProperty(ref renameText, value ?? ""); }

    public IRelayCommand BackCommand { get; }
    public IRelayCommand OwnerCommand { get; }
    public IRelayCommand NewModelCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand RenameCommand { get; }

    /// <summary>«← Modelli».</summary>
    public event EventHandler? BackRequested;

    /// <summary>«Progetto di appartenenza»: the overview of the section.</summary>
    public event EventHandler<JsonObject>? OwnerRequested;

    /// <summary>«+ Modello»: a new model of the section.</summary>
    public event EventHandler<JsonObject>? NewModelRequested;

    /// <summary>«Salva progetto».</summary>
    public event EventHandler? SaveRequested;

    /// <summary>«Rinomina» (MainWindow: refused before the import, the name trimmed, the document modified).</summary>
    public void Rename(string name)
    {
        if (ModelId is not { } id) throw new InvalidOperationException(ImportFirst);
        if (!project.ChangeData(() => { ProjectModelStore.Rename(Container, id, name); return true; }, Container)) return;
        Title = RenameText = name.Trim();
    }

    /// <summary>The viewer imported a model: it becomes the model of the tab (a new one, or the same identifier), the document is modified.</summary>
    public void Imported(ModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string? id = ModelId;
        if (!project.ChangeData(() => { id = ProjectModelStore.Set(Container, snapshot, id); return true; }, Container)) return;
        ModelId = id;
        Snapshot = snapshot;
        Title = RenameText = ProjectModelStore.Models(Container).Single(m => m.S("id") == id).S("nome");
        Targets = ReadTargets(Container);
        OnPropertyChanged(nameof(ModelId)); OnPropertyChanged(nameof(Targets));
    }

    /// <summary>The viewer links a result of the model to a sheet of the section (refused before the import).</summary>
    public void Linked(string sheetId, int element, string result, string component)
    {
        if (ModelId is not { } id) throw new InvalidOperationException(ImportFirst);
        var sheet = ProjectModelStore.DescendantSheets(Container).Single(s => s.S("id") == sheetId);
        if (!project.ChangeData(() => { ProjectModelStore.Link(Container, sheet, id, element, result, component); return true; }, sheet)) return;
        Targets = ReadTargets(Container);
        OnPropertyChanged(nameof(Targets));
    }

    /// <summary>
    /// The document on screen changed (undo, redo, a revision): the tab follows the section with the same identifier, read again; false when
    /// the section or the model is not there any more, and the shell closes the tab.
    /// </summary>
    public bool Rebind()
    {
        if (project.Contains(Container)) { RaiseState(); return true; }
        if (project.Find(Container.S("id")) is not { } container) return false;
        if (ModelId is { } id && !ProjectModelStore.Models(container).Any(m => m.S("id") == id)) return false;
        Container = container;
        Snapshot = ModelId is null ? null : ProjectModelStore.Read(container, ModelId);
        Targets = ReadTargets(container);
        Title = RenameText = ModelId is null ? NewModelTitle : ProjectModelStore.Models(container).Single(m => m.S("id") == ModelId).S("nome");
        OnPropertyChanged(nameof(Container)); OnPropertyChanged(nameof(Snapshot)); OnPropertyChanged(nameof(Targets));
        RaiseState();
        return true;
    }

    private void RaiseState()
    {
        if (readOnly != project.IsReadOnly)
        {
            readOnly = project.IsReadOnly;
            OnPropertyChanged(nameof(IsReadOnly));
            NewModelCommand.NotifyCanExecuteChanged();
        }
        OnPropertyChanged(nameof(PathText));
    }

    private static IReadOnlyList<SheetTarget> ReadTargets(JsonObject container) => ProjectModelStore.DescendantSheets(container).Select(s => new SheetTarget(s.S("id"), s.S("nome"),
        s[ProjectModelStore.Binding] is null ? "Non collegato" : ProjectModelStore.FindOwner(s) is { } owner ? ProjectModelStore.LinkStatus(owner, s) : "Riferimento da aggiornare")).ToArray();
}

/// <summary>
/// The notice of the model linked to the sheet with the focus, above the sheet (F5.13 c; MainWindow.RefreshModelReference): the state of the
/// link, element, case and component; «Apri modello» when the section of the model is found.
/// </summary>
public sealed class ModelReferenceViewModel : ViewModelBase
{
    public const string Missing = "Riferimento da aggiornare: modello non disponibile in questa sezione";
    public const string OpenLabel = "Apri modello";

    private JsonObject? owner;
    private string? modelId;

    public ModelReferenceViewModel(IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var guard = new CommandGuard(messages);
        OpenCommand = GuardedCommands.Create(guard, () => { if (owner is not null) ModelRequested?.Invoke(this, new ModelRequestEventArgs(owner, modelId)); }, () => CanOpen);
    }

    /// <summary>The sheet with the focus has a link to a model.</summary>
    public bool IsVisible { get; private set; }

    public string Text { get; private set; } = "";

    /// <summary>The section of the model was found: «Apri modello».</summary>
    public bool CanOpen => owner is not null;

    public IRelayCommand OpenCommand { get; }

    /// <summary>«Apri modello».</summary>
    public event EventHandler<ModelRequestEventArgs>? ModelRequested;

    /// <summary>The notice of <paramref name="sheet"/>, hidden without a link.</summary>
    public void Update(SheetSession? sheet)
    {
        owner = null; modelId = null; Text = ""; IsVisible = false;
        if (sheet?.Sheet[ProjectModelStore.Binding] is JsonObject link)
        {
            owner = ProjectModelStore.FindOwner(sheet.Sheet);
            modelId = link.S("modello_id");
            string status = owner is null ? Missing : ProjectModelStore.LinkStatus(owner, sheet.Sheet);
            Text = $"{status} · elemento {link["elemento"]} · {link.S("caso")} · {link.S("componente")}";
            IsVisible = true;
        }
        OnPropertyChanged(nameof(IsVisible)); OnPropertyChanged(nameof(Text)); OnPropertyChanged(nameof(CanOpen));
        OpenCommand.NotifyCanExecuteChanged();
    }
}
