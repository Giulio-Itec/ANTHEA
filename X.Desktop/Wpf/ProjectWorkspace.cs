using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private readonly ContentControl projectContent = new();
    private readonly TextBlock projectDropHint = Ui.Text("", 12, true);
    private DockPanel? projectWorkspace;
    private DockPanel projectCommandBar = null!;
    private WrapPanel projectHistoryActions = null!;
    private readonly ContentControl projectHistoryHost = new() { VerticalAlignment = VerticalAlignment.Center };
    private Grid projectLayout = null!;
    private Border projectTreePane = null!, projectDetailPane = null!, projectCatalogPane = null!;
    private GridSplitter projectColumnSplitter = null!, projectCatalogSplitter = null!;
    private Button projectTreeToggle = null!;
    private bool projectSheetTreeVisible;
    private double projectSheetTreeWidth = 330;
    private bool? projectLayoutSheetMode;
    private bool refreshingProjectTree, projectReadOnly, projectDragged;
    private string? selectedProjectId;
    private readonly HashSet<string> collapsedProjectNodes = [];
    private JsonObject? historyBaseline;
    private JsonObject? historyDocument;
    private readonly List<JsonObject> projectUndo = [], projectRedo = [];
    private Button? undoProjectButton, redoProjectButton;
    private FrameworkElement projectStructureActions = null!;

    private void ResetProjectHistory()
    {
        projectUndo.Clear(); projectRedo.Clear(); historyDocument = document;
        ProjectRevisions.EnsureIds(document); historyBaseline = (JsonObject)document.DeepClone(); UpdateHistoryButtons();
    }
    private void RecordProjectHistory()
    {
        if (projectReadOnly || document.S("tipo") != "progetti") return;
        if (!ReferenceEquals(historyDocument, document)) { ResetProjectHistory(); return; }
        if (historyBaseline is null || JsonNode.DeepEquals(historyBaseline, document)) return;
        projectUndo.Add(historyBaseline); if (projectUndo.Count > 30) projectUndo.RemoveAt(0);
        projectRedo.Clear(); historyBaseline = (JsonObject)document.DeepClone(); UpdateHistoryButtons();
    }
    private void UpdateHistoryButtons()
    {
        if (undoProjectButton is not null) undoProjectButton.IsEnabled = !projectReadOnly && projectUndo.Count > 0;
        if (redoProjectButton is not null) redoProjectButton.IsEnabled = !projectReadOnly && projectRedo.Count > 0;
    }
    private void RestoreProjectHistory(bool redo)
    {
        if (projectReadOnly) return;
        Commit(); var source = redo ? projectRedo : projectUndo; var target = redo ? projectUndo : projectRedo;
        if (source.Count == 0) return;
        string? selection = selectedProjectId;
        target.Add((JsonObject)document.DeepClone()); document = source[^1]; source.RemoveAt(source.Count - 1);
        historyDocument = document; historyBaseline = (JsonObject)document.DeepClone();
        editor?.Dispose(); editor = null; currentSheet = null; sharedBaseline = null; sheetContent.Content = null;
        dirty = true; UpdateTitle(); ShowProjectOverview(selection is null ? null : ProjectRevisions.Find(document, selection)); UpdateHistoryButtons();
    }
    private void EnsureProjectWorkspace()
    {
        if (projectWorkspace is not null) return;
        var actions = projectCommandBar = new DockPanel { Background = Ui.Navy, MinHeight = 60 };
        var back = CommandButton("← Torna ad ANTHEA", () => Safe(() => { Commit(); ShowHome(); }));
        undoProjectButton = CommandButton("↶ Annulla", () => Safe(() => RestoreProjectHistory(false)));
        redoProjectButton = CommandButton("↷ Ripristina", () => Safe(() => RestoreProjectHistory(true)));
        undoProjectButton.ToolTip = "Annulla l’ultima operazione · Ctrl+Z"; redoProjectButton.ToolTip = "Ripristina · Ctrl+Y";
        PreviewKeyDown += (_, e) =>
        {
            if (projectReadOnly || !ReferenceEquals(body.Content, projectWorkspace) || Keyboard.Modifiers != ModifierKeys.Control ||
                Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or ComboBox) return;
            if (e.Key is Key.Z or Key.Y) { e.Handled = true; Safe(() => RestoreProjectHistory(e.Key == Key.Y)); }
        };
        projectHistoryActions = Ui.Bar(undoProjectButton, redoProjectButton);
        DockPanel.SetDock(projectHistoryHost, Dock.Right); actions.Children.Add(projectHistoryHost);
        var files = FileCommands(true, includeReport: false); DockPanel.SetDock(files, Dock.Right); actions.Children.Add(files);
        DockPanel.SetDock(back, Dock.Right); actions.Children.Add(back);
        var title = Ui.Text("ANTHEA   |   Progetti", 21, true, System.Windows.Media.Brushes.White); title.Margin = new Thickness(20, 10, 20, 10); actions.Children.Add(title);
        projectTreeToggle = CommandButton("▸", () => SetProjectSheetTreeVisible(!projectSheetTreeVisible));
        projectTreeToggle.Width = 32; projectTreeToggle.FontSize = 20; projectTreeToggle.Padding = new Thickness(0);
        sheetProjectTreeControl.Content = projectTreeToggle;
        var hintStyle = new Style(typeof(TextBlock));
        var emptyHint = new Trigger { Property = TextBlock.TextProperty, Value = "" };
        emptyHint.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed)); hintStyle.Triggers.Add(emptyHint);
        projectDropHint.Style = hintStyle;
        projectDropHint.TextWrapping = TextWrapping.NoWrap;
        projectDropHint.TextTrimming = TextTrimming.CharacterEllipsis;
        projectDropHint.Background = System.Windows.Media.Brushes.White;
        projectDropHint.IsHitTestVisible = false;
        var grid = projectLayout = new Grid { Margin = new Thickness(14, 12, 14, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        if (tree.Parent is Panel old) old.Children.Remove(tree);
        var treeTitle = Ui.Text("Struttura del progetto", 22, true); treeTitle.Margin = new Thickness(0, 0, 0, 12);
        var treeHeader = Ui.Stack(treeTitle);
        treeHeader.Margin = new Thickness(0, 0, 0, 14);
        var treeBody = new Grid(); treeBody.Children.Add(tree);
        projectEmptyState = new Border { Child = BuildEmptyProjectTree(), Background = System.Windows.Media.Brushes.White, Visibility = Visibility.Collapsed }; treeBody.Children.Add(projectEmptyState);
        projectTreeSummary = Ui.Text("", 11, color: Ui.Muted); projectTreeSummary.Margin = new Thickness(0, 10, 0, 0);
        var left = Ui.Dock(treeBody, treeHeader, projectTreeSummary);
        ScrollViewer.SetHorizontalScrollBarVisibility(tree, ScrollBarVisibility.Disabled);
        var addProject = ProjectButton("+ Progetto", () => Safe(AddProject), true); addProject.Margin = new Thickness(0, 0, 8, 0);
        projectStructureActions = Ui.Bar(addProject, ProjectButton("+ Sezione", () => Safe(AddStructure)));
        treeHeader.Children.Add(projectStructureActions);
        // Drag feedback must never move the target out from under the pointer.
        // Reuse the instruction line, with fixed height even for long section names.
        var instructionLine = new Grid { Height = 18, Margin = new Thickness(0, 10, 0, 0), IsHitTestVisible = false };
        var instruction = Ui.Text("Organizza sezioni e fogli con il trascinamento.", 12, color: Ui.Muted);
        instruction.TextWrapping = TextWrapping.NoWrap; instruction.TextTrimming = TextTrimming.CharacterEllipsis;
        instructionLine.Children.Add(instruction); instructionLine.Children.Add(projectDropHint); treeHeader.Children.Add(instructionLine);
        projectTreePane = ProjectPanel(left, 14); grid.Children.Add(projectTreePane);
        projectColumnSplitter = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch, Background = Ui.Brush("#E6ECF3"), ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
        Grid.SetColumn(projectColumnSplitter, 1); grid.Children.Add(projectColumnSplitter);
        projectCatalogSplitter = new GridSplitter { Width = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Stretch, Background = Ui.Brush("#E6ECF3"), ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
        Grid.SetColumn(projectCatalogSplitter, 3); grid.Children.Add(projectCatalogSplitter);
        projectDetailPane = ProjectPanel(projectContent, 14); grid.Children.Add(projectDetailPane);
        projectCatalogPane = ProjectPanel(BuildProjectCatalog(), 14);
        Grid.SetColumn(projectCatalogPane, 4); grid.Children.Add(projectCatalogPane);
        projectWorkspace = Ui.Dock(grid, Ui.Stack(actions, projectRevisionHost));
        tree.SelectedItemChanged += (_, _) =>
        {
            if (refreshingProjectTree || !ReferenceEquals(body.Content, projectWorkspace) || tree.SelectedItem is not TreeViewItem { Tag: JsonObject selected }) return;
            selectedProjectId = selected.S("id");
        };
    }
    private void ArrangeProjectWorkspace(bool sheetOpen)
    {
        if (projectLayoutSheetMode == sheetOpen) return;
        if (projectLayoutSheetMode == true && projectSheetTreeVisible && projectTreePane.ActualWidth >= 220)
            projectSheetTreeWidth = projectTreePane.ActualWidth;
        projectLayoutSheetMode = sheetOpen;
        projectStructureActions.Visibility = projectReadOnly ? Visibility.Collapsed : Visibility.Visible;
        projectCommandBar.Visibility = sheetOpen ? Visibility.Collapsed : Visibility.Visible;
        sheetProjectActions.Content = null; projectHistoryHost.Content = null;
        if (!projectReadOnly) (sheetOpen ? sheetProjectActions : projectHistoryHost).Content = projectHistoryActions;
        bool catalogVisible = !sheetOpen && !projectReadOnly;
        projectLayout.ColumnDefinitions[0].Width = sheetOpen ? new GridLength(330) : new GridLength(23, GridUnitType.Star);
        projectLayout.ColumnDefinitions[0].MinWidth = sheetOpen ? 220 : 190;
        projectLayout.ColumnDefinitions[1].Width = new GridLength(12);
        projectLayout.ColumnDefinitions[2].Width = new GridLength(sheetOpen ? 1 : 47, GridUnitType.Star);
        projectLayout.ColumnDefinitions[2].MinWidth = sheetOpen ? 0 : 260;
        projectLayout.ColumnDefinitions[3].Width = new GridLength(catalogVisible ? 12 : 0);
        projectLayout.ColumnDefinitions[4].Width = catalogVisible ? new GridLength(30, GridUnitType.Star) : new GridLength(0);
        projectLayout.ColumnDefinitions[4].MinWidth = catalogVisible ? 220 : 0;
        Grid.SetColumn(projectTreePane, sheetOpen ? 0 : 2);
        Grid.SetColumn(projectDetailPane, sheetOpen ? 2 : 0);
        projectDetailPane.Padding = new Thickness(sheetOpen ? 0 : 14);
        projectDetailPane.Background = sheetOpen ? System.Windows.Media.Brushes.White : Ui.Brush("#F6F9FC");
        projectTreePane.Visibility = projectColumnSplitter.Visibility = Visibility.Visible;
        projectTreeToggle.Visibility = sheetOpen ? Visibility.Visible : Visibility.Collapsed;
        projectCatalogPane.Visibility = projectCatalogSplitter.Visibility = catalogVisible ? Visibility.Visible : Visibility.Collapsed;
        if (sheetOpen) ApplyProjectSheetTreeVisibility();
    }
    private void SetProjectSheetTreeVisible(bool visible)
    {
        if (projectLayoutSheetMode != true) return;
        if (projectSheetTreeVisible && projectTreePane.ActualWidth >= 220) projectSheetTreeWidth = projectTreePane.ActualWidth;
        projectSheetTreeVisible = visible;
        ApplyProjectSheetTreeVisibility();
    }
    private void ApplyProjectSheetTreeVisibility()
    {
        bool visible = projectSheetTreeVisible;
        projectLayout.ColumnDefinitions[0].MinWidth = visible ? 220 : 0;
        projectLayout.ColumnDefinitions[0].Width = new GridLength(visible ? projectSheetTreeWidth : 0);
        projectLayout.ColumnDefinitions[1].Width = new GridLength(visible ? 12 : 0);
        projectLayout.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        projectTreePane.Visibility = projectColumnSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        projectTreeToggle.Content = visible ? "◂" : "▸";
        string label = visible ? "Nascondi struttura del progetto" : "Mostra struttura del progetto";
        projectTreeToggle.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(projectTreeToggle, label);
        if (visible) tree.UpdateLayout();
    }
    private void ShowProjectSheet()
    {
        EnsureProjectWorkspace(); ArrangeProjectWorkspace(sheetOpen: true);
        body.Content = null; projectContent.Content = moduleView; body.Content = projectWorkspace;
        selectedProjectId = currentSheet!.S("id"); RefreshTree(currentSheet);
        sheetContent.IsEnabled = true; confirmShared.Visibility = projectReadOnly ? Visibility.Collapsed : Visibility.Visible;
        revisionNavigationSheetId = currentSheet.S("id");
        UpdateProjectRevisionBar(currentSheet);
    }
    private string SheetHeading(JsonObject sheet)
    {
        string title = sheet.S("nome", ModuleName(sheet.S("modulo_id")));
        if (displayedRevision is int selectedRevision) return title + " · Rev. " + selectedRevision + " · sola lettura";
        for (var owner = sheet.Parent?.Parent as JsonObject; owner is not null; owner = owner.Parent?.Parent as JsonObject)
            if (owner["revisione"] is JsonObject revision) return title + " · Rev. " + (int)revision.D("numero");
        return title;
    }
    private void ShowProjectOverview(JsonObject? section = null)
    {
        if (document.S("tipo") != "progetti") { NewProjects(); return; }
        if (!projectReadOnly && !ReferenceEquals(historyDocument, document)) ResetProjectHistory();
        EnsureProjectWorkspace();
        section ??= selectedProjectId is null ? null : ProjectRevisions.Find(document, selectedProjectId);
        if (section?.ContainsKey("modulo_id") == true) section = section.Parent?.Parent as JsonObject;
        section ??= document.Array("progetti").OfType<JsonObject>().FirstOrDefault();
        selectedProjectId = section?.S("id");
        body.Content = null; projectContent.Content = null; body.Content = projectWorkspace;
        ArrangeProjectWorkspace(sheetOpen: false);
        var panel = new StackPanel();
        if (section is not null) BuildSectionOverview(panel, section);
        else BuildEmptyProjectOverview(panel);
        projectContent.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        RefreshTree(section); UpdateHistoryButtons();
        if (!switchingProjectRevision) revisionNavigationSheetId = null;
        UpdateProjectRevisionBar(section);
    }
    private void DuplicateProjectSection(JsonObject section)
    {
        if (projectReadOnly || section.Parent is not JsonArray siblings) return;
        Commit(); var copy = ProjectRevisions.Duplicate(section); copy["nome"] = NextProjectName(siblings, section.S("nome") + " copia");
        siblings.Insert(siblings.IndexOf(section) + 1, copy); MarkDirty(); ShowProjectOverview(copy);
    }
    private static string RevisionDate(JsonObject revision) => DateTime.TryParse(revision.S("data"), out var time) ? time.ToString("dd/MM/yyyy HH:mm") : revision.S("data");
    private void CreateProjectRevision(JsonObject section)
    {
        if (projectReadOnly) return; Commit();
        var note = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 65, Margin = new Thickness(0, 10, 0, 10) };
        var panel = Ui.Stack(Ui.Text("La versione attuale verrà archiviata. Continuerai a lavorare nella nuova revisione.", 15), Ui.Text("Nota della nuova revisione (facoltativa)", 13), note); panel.Margin = new Thickness(18);
        var dialog = Ui.Dialog(this, "Nuova revisione · " + section.S("nome"), panel, 570, 300);
        panel.Children.Add(Ui.Bar(Ui.Button("Crea revisione", () => dialog.DialogResult = true, true), Ui.Button("Annulla", () => dialog.DialogResult = false)));
        if (dialog.ShowDialog() != true) return;
        CompleteProjectRevision(section, note.Text.Trim());
    }
    private void ShowProjectRevisions(JsonObject section)
    {
        Commit(); var panel = new StackPanel { Margin = new Thickness(18) };
        var dialog = Ui.Dialog(this, "Revisioni · " + section.S("nome"), new ScrollViewer { Content = panel, Background = Ui.Bg, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 820, 620);
        var current = section["revisione"]!.AsObject();
        panel.Children.Add(Ui.Text($"Rev. {(int)current.D("numero")} · corrente · {RevisionDate(current)}", 20, true));
        if (current.S("nota").Length > 0) panel.Children.Add(Ui.Text(current.S("nota"), 14));
        foreach (string change in ProjectRevisions.Changes(WorkingProjectDocument, section)) panel.Children.Add(Ui.Text("• " + change, 13));
        foreach (var entry in section.Array("revisioni").OfType<JsonObject>().Reverse())
        {
            var block = Ui.Stack(Ui.Text($"Rev. {(int)entry.D("numero")} · {RevisionDate(entry)}", 18, true), Ui.Text(entry.S("nota"), 13));
            foreach (var change in entry.Array("modifiche")) block.Children.Add(Ui.Text("• " + change, 13));
            block.Children.Add(Ui.Button("Mostra questa revisione", () => Safe(() => { dialog.Close(); SwitchProjectRevision(section.S("id"), (int)entry.D("numero")); })));
            var card = Ui.Paper(block, 15); card.Margin = new Thickness(0, 18, 0, 0); panel.Children.Add(card);
        }
        dialog.ShowDialog();
    }
}
