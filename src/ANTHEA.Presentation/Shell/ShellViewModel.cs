using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>
/// The shell as one ViewModel (F5.13 b): the coordinator of the documents and the ViewModels of navigation, tree, catalogue of the sheets,
/// tabs and windows, commands, revision bar, status line and overview of the selected section, wired as MainWindow wires its parts:
/// <list type="bullet">
/// <item>the title of the window follows the document of the sheet with the focus, otherwise the project (MainWindow.UpdateTitle);</item>
/// <item>the overview asked by the tree shows the page «Progetti» on that section, with its revision bar (ShowProjectOverview);</item>
/// <item>a sheet of a project with the focus shows its revision bar and the status line of its shared data, refreshed at every edit
/// (ShowProjectSheet, RefreshSharedStatus);</item>
/// <item>the report asked by a sheet runs the «Report Word» command (ISheetHost.RequestReport).</item>
/// </list>
/// The views and the windows come in (c), after the checkpoint of the layout.
/// </summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly PresentationServices services;
    private RevisionBarViewModel? revisions;
    private SectionOverviewViewModel? overview;
    private ISheetViewModel? watched;
    private ProjectSession? attached;
    private string title = "ANTHEA";

    public ShellViewModel(PresentationServices services, ISheetFactory sheets, IDocumentFiles files, IWindowService windows, ISheetExports exports)
    {
        ArgumentNullException.ThrowIfNull(services);
        this.services = services;
        Workspace = new WorkspaceSession(services, sheets, files);
        Navigation = new NavigationViewModel(Workspace, services.Messages);
        Tree = new ProjectTreeViewModel(Workspace, services.Messages);
        Catalogue = new ProjectCatalogViewModel();
        Windows = new ShellWindowsViewModel(Workspace, windows, services.Messages);
        Commands = new ShellCommandsViewModel(Workspace, services, exports, files);
        Commands.ExamplesRequested += (_, _) => Navigation.Select(ShellPage.Examples);
        Tree.OverviewRequested += (_, section) => ShowOverview(section);
        Tree.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ProjectTreeViewModel.Selected) && Workspace.Active is null) Revisions?.Show(SelectedSection()); };
        Workspace.ActiveChanged += (_, _) => OnActiveChanged();
        Workspace.DocumentsChanged += (_, _) => OnDocumentsChanged();
        Workspace.SheetsReloaded += (_, _) => OnActiveChanged();
        Workspace.ReportRequested += (_, sheet) => { if (ReferenceEquals(sheet, Workspace.Active)) _ = Commands.ReportCommand.ExecuteAsync(null); };
        OnDocumentsChanged();
    }

    public WorkspaceSession Workspace { get; }
    public NavigationViewModel Navigation { get; }
    public ProjectTreeViewModel Tree { get; }
    public ProjectCatalogViewModel Catalogue { get; }
    public ShellWindowsViewModel Windows { get; }
    public ShellCommandsViewModel Commands { get; }

    /// <summary>The status line of the shared data above the sheet with the focus.</summary>
    public SharedStatusViewModel SharedStatus { get; } = new();

    /// <summary>The revision bar of the open project, null without one.</summary>
    public RevisionBarViewModel? Revisions { get => revisions; private set => SetProperty(ref revisions, value); }

    /// <summary>The overview of the section on the page «Progetti».</summary>
    public SectionOverviewViewModel? Overview { get => overview; private set => SetProperty(ref overview, value); }

    /// <summary>Title of the main window.</summary>
    public string Title { get => title; private set => SetProperty(ref title, value); }

    /// <summary>The overview of <paramref name="section"/> (the first project when null), with its revision bar (MainWindow.ShowProjectOverview).</summary>
    public void ShowOverview(JsonObject? section)
    {
        if (Workspace.EnsureProject() is not { } project) return;
        Navigation.Select(ShellPage.Projects);
        if (section?.ContainsKey("modulo_id") == true) section = section.Parent?.Parent as JsonObject;
        section ??= project.Projects.FirstOrDefault();
        Overview = new SectionOverviewViewModel(project, section);
        Revisions?.Show(section);
    }

    /// <summary>The application is closing: the questions to save every document (true closes).</summary>
    public bool ConfirmClose() => Workspace.ConfirmClose();

    protected override void OnDisposed()
    {
        Watch(null);
        if (attached is not null) { attached.StateChanged -= OnProjectState; attached.Changed -= OnProjectChanged; }
        Commands.Dispose(); Windows.Dispose(); Tree.Dispose(); Navigation.Dispose(); Catalogue.Dispose();
        Revisions?.Dispose();
        Workspace.Dispose();
    }

    private JsonObject? SelectedSection() => Tree.Selected?.Node is { } node && !node.ContainsKey("modulo_id") ? node : Tree.SelectedContainer;

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
        }
        UpdateTitle();
    }

    private void OnProjectState(object? sender, EventArgs e) => UpdateTitle();

    /// <summary>Any change of the project rebuilds the overview on screen (the section found again by identifier), as MainWindow shows it again.</summary>
    private void OnProjectChanged(object? sender, ProjectChangedEventArgs e)
    {
        if (Overview is { } shown && attached is { } project)
            Overview = new SectionOverviewViewModel(project, shown.Section is null ? null : project.Find(shown.Section.S("id")) ?? project.Projects.FirstOrDefault());
        UpdateTitle();
    }

    private void OnActiveChanged()
    {
        var active = Workspace.Active;
        Watch(active?.ViewModel);
        SharedStatus.Update(active);
        if (active?.Project is not null) Revisions?.Show(active.Sheet);
        UpdateTitle();
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
