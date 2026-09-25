using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    // The working document and its undo history remain untouched while viewing a snapshot.
    private JsonObject? revisionWorkingDocument;
    private string? revisionScopeId, revisionNavigationSheetId;
    private int? displayedRevision;
    private bool switchingProjectRevision;
    private readonly Border projectRevisionHost = new() { Background = Brushes.White, BorderBrush = Ui.Brush("#DCE5EF"), BorderThickness = new Thickness(0, 0, 0, 1), Visibility = Visibility.Collapsed };
    private JsonObject WorkingProjectDocument => revisionWorkingDocument ?? document;

    private JsonObject? RevisionScope(JsonObject? node)
    {
        if (revisionWorkingDocument is not null)
            return revisionScopeId is null ? null : ProjectRevisions.Find(revisionWorkingDocument, revisionScopeId);
        if (switchingProjectRevision && revisionScopeId is not null) return ProjectRevisions.Find(document, revisionScopeId);
        if (node?.ContainsKey("modulo_id") != true) return node;
        node = node.Parent?.Parent as JsonObject;
        if (node is null) return null;
        return new[] { node }.Concat(ProjectSharedData.Ancestors(node)).FirstOrDefault(s => s["revisione"] is JsonObject) ?? node;
    }

    private void UpdateProjectRevisionBar(JsonObject? selected)
    {
        var scope = RevisionScope(selected);
        projectRevisionHost.Visibility = scope is null ? Visibility.Collapsed : Visibility.Visible;
        if (scope is null) return;
        revisionScopeId = scope.S("id");
        int current = (int)(scope["revisione"]?.D("numero") ?? 0);
        var row = new DockPanel { Margin = new Thickness(20, 9, 20, 9) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        int selectedNumber = displayedRevision ?? current;
        var remove = ProjectButton("Elimina revisione", () => Safe(() => DeleteProjectRevision(scope.S("id"), selectedNumber)));
        remove.Margin = new Thickness(0, 0, 8, 0); remove.IsEnabled = scope.Array("revisioni").Count > 0;
        remove.ToolTip = !remove.IsEnabled ? "L’unica versione rimasta non può essere eliminata." :
            selectedNumber == current ? $"Elimina Rev. {current} e rende modificabile la precedente. Puoi annullare l’operazione." :
            $"Elimina Rev. {selectedNumber} dallo storico. La versione attuale resta invariata. Puoi annullare l’operazione.";
        actions.Children.Add(remove);
        var add = ProjectButton("+ Nuova revisione", () => Safe(() => CreateProjectRevision(scope)), true);
        add.IsEnabled = !projectReadOnly; add.ToolTip = projectReadOnly ? "Torna alla revisione attuale per crearne una nuova." : "Conserva la versione attuale e continua nella nuova revisione.";
        actions.Children.Add(add); DockPanel.SetDock(actions, Dock.Right); row.Children.Add(actions);
        var caption = Ui.Text("Revisioni · " + scope.S("nome"), 13, true); caption.MaxWidth = 240; caption.TextWrapping = TextWrapping.NoWrap; caption.TextTrimming = TextTrimming.CharacterEllipsis;
        caption.ToolTip = scope.S("nome"); caption.Margin = new Thickness(0, 0, 16, 0); DockPanel.SetDock(caption, Dock.Left); row.Children.Add(caption);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        void Tab(int number, bool live)
        {
            bool selectedTab = live ? !projectReadOnly : displayedRevision == number;
            var button = ProjectButton($"Rev. {number}" + (live ? " · attuale" : ""), () => Safe(() => SwitchProjectRevision(scope.S("id"), live ? null : number)), selectedTab);
            button.Tag = (scope.S("id"), live ? (int?)null : number);
            button.Margin = new Thickness(0, 0, 6, 0); button.MinWidth = 70;
            System.Windows.Automation.AutomationProperties.SetName(button, $"Revisione {number}" + (live ? " attuale" : " archiviata"));
            buttons.Children.Add(button);
            if (selectedTab) button.Loaded += (_, _) => button.BringIntoView();
        }
        foreach (var entry in scope.Array("revisioni").OfType<JsonObject>()) Tab((int)entry.D("numero"), false);
        Tab(current, true);
        row.Children.Add(new ScrollViewer { Content = buttons, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 12, 0) });
        var metadata = displayedRevision is int number
            ? scope.Array("revisioni").OfType<JsonObject>().FirstOrDefault(r => (int)r.D("numero") == number)
            : scope["revisione"] as JsonObject;
        string details = projectReadOnly ? "Versione archiviata · sola lettura" : "Versione attuale · modificabile";
        if (metadata is not null) details += "  ·  " + RevisionDate(metadata) + (metadata.S("nota").Length > 0 ? "  ·  " + metadata.S("nota") : "");
        var description = Ui.Text(details, 12, color: projectReadOnly ? Ui.Brush("#94611E") : Ui.Muted);
        description.TextWrapping = TextWrapping.NoWrap; description.TextTrimming = TextTrimming.CharacterEllipsis;
        description.ToolTip = details; description.Margin = new Thickness(20, 0, 20, 9);
        projectRevisionHost.Child = Ui.Stack(row, description);
    }

    private void CompleteProjectRevision(JsonObject section, string note)
    {
        if (projectReadOnly) return;
        finishProjectRename?.Invoke(true); Commit();
        bool sheetOpen = ReferenceEquals(projectContent.Content, moduleView);
        ProjectRevisions.NewRevision(document, section, note); MarkDirty();
        if (sheetOpen && currentSheet is not null) ShowSheet(currentSheet);
        else ShowProjectOverview(section);
    }

    private void SwitchProjectRevision(string scopeId, int? number)
    {
        if (number == displayedRevision && scopeId == revisionScopeId && (number is null || projectReadOnly)) return;
        CancelProjectSectionClick(); finishProjectRename?.Invoke(true); Commit();
        var working = WorkingProjectDocument;
        var scope = ProjectRevisions.Find(working, scopeId) ?? throw new ArgumentException("La sezione non è più presente nel progetto.");
        JsonObject destination = working;
        if (number is int requested)
        {
            var entry = scope.Array("revisioni").OfType<JsonObject>().Single(r => (int)r.D("numero") == requested);
            var snapshot = ProjectRevisions.Snapshot(working, entry);
            var target = ProjectSharedData.Sections(snapshot).Single(s => s.S("id") == scopeId);
            target["revisione"] = J.Obj(("numero", requested), ("data", entry.S("data")), ("nota", entry.S("nota")));
            destination = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray(snapshot)));
        }
        string? preferredSheet = revisionNavigationSheetId;
        string? selection = selectedProjectId;
        DisposeRevisionEditor();
        revisionWorkingDocument = number is null ? null : working;
        document = destination; displayedRevision = number; revisionScopeId = scopeId; projectReadOnly = number is not null;
        projectLayoutSheetMode = null;
        switchingProjectRevision = true;
        try
        {
            if (preferredSheet is not null && ProjectRevisions.Find(document, preferredSheet) is JsonObject sheet && sheet.ContainsKey("modulo_id")) ShowSheet(sheet);
            else
            {
                var section = selection is null ? null : ProjectRevisions.Find(document, selection);
                ShowProjectOverview(section?.ContainsKey("modulo_id") == false ? section : ProjectRevisions.Find(document, scopeId));
                revisionNavigationSheetId = preferredSheet;
            }
        }
        finally { switchingProjectRevision = false; }
        UpdateHistoryButtons(); UpdateTitle();
    }

    private void DisposeRevisionEditor()
    {
        editor?.Dispose(); editor = null; currentSheet = null; sharedBaseline = null; sheetContent.Content = null;
    }

    private void DeleteProjectRevision(string scopeId, int number)
    {
        var working = WorkingProjectDocument;
        var scope = ProjectRevisions.Find(working, scopeId);
        if (scope is null || scope.Array("revisioni").Count == 0) return;
        CancelProjectSectionClick(); finishProjectRename?.Invoke(true); Commit();
        // Prepare and validate the deletion before touching the live document or editor.
        var updated = (JsonObject)working.DeepClone();
        ProjectRevisions.DeleteRevision(updated, scopeId, number);
        Archivio.Valida(updated); ProjectRevisions.Validate(updated);
        string? preferredSheet = revisionNavigationSheetId, selection = selectedProjectId;
        ExitRevisionPreview(); DisposeRevisionEditor();
        document = updated; historyDocument = updated; projectReadOnly = false;
        revisionScopeId = scopeId; projectLayoutSheetMode = null;
        MarkDirty(); switchingProjectRevision = true;
        try
        {
            var restored = ProjectRevisions.Find(document, scopeId)!;
            var sheet = preferredSheet is null ? null : ProjectSharedData.SubtreeSheets(restored).FirstOrDefault(s => s.S("id") == preferredSheet);
            if (sheet is not null) ShowSheet(sheet);
            else
            {
                var section = selection is null ? null : ProjectSharedData.Sections(restored).FirstOrDefault(s => s.S("id") == selection);
                ShowProjectOverview(section ?? restored);
            }
        }
        finally { switchingProjectRevision = false; }
        UpdateHistoryButtons(); UpdateTitle();
    }

    private void ExitRevisionPreview()
    {
        if (revisionWorkingDocument is not null)
        {
            DisposeRevisionEditor(); document = revisionWorkingDocument; revisionWorkingDocument = null;
            projectReadOnly = false; projectLayoutSheetMode = null;
        }
        displayedRevision = null; revisionScopeId = null; revisionNavigationSheetId = null;
        UpdateTitle();
    }
}
