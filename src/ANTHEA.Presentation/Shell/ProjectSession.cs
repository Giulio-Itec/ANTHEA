using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Services;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>What changed in a project document: the shell reconciles its open sheets after each change.</summary>
public enum ProjectChange
{
    /// <summary>Projects, sections or sheets added, renamed, moved or removed.</summary>
    Structure,
    /// <summary>Data of sheets changed outside the open sheet (comparison, links, moves).</summary>
    Data,
    /// <summary>Undo or redo: another copy of the document is shown.</summary>
    Restored,
    /// <summary>An archived revision (read-only) or the current version is shown.</summary>
    RevisionShown,
    /// <summary>A revision was deleted: the document is the updated copy.</summary>
    RevisionDeleted,
    /// <summary>The preview of an archived revision ended: the working document is shown again.</summary>
    PreviewExited
}

/// <summary>A change of a project document, with the node to show next when the window shows one (the overview section).</summary>
public sealed class ProjectChangedEventArgs(ProjectChange change, JsonObject? node = null) : EventArgs
{
    public ProjectChange Change { get; } = change;

    /// <summary>The node concerned (new section, moved sheet…), or null.</summary>
    public JsonObject? Node { get; } = node;
}

/// <summary>
/// A project archive open in the shell (F5.13 a, proposal 3.7, 4.1 and 4.3), one at a time. It holds what MainWindow keeps for a project:
/// the history (<see cref="ProjectHistory"/>), the confirmation of the shared data (<see cref="SharedDataCoordinator"/>), the preview of
/// an archived revision (working document, revision and section on screen, read-only) and the operations on the structure, with the
/// same conditions, names, questions and order of MainWindow (ProjectWorkspace, ProjectHierarchy, ProjectSectionReorder,
/// ProjectMovePreview, ProjectRevisionNavigation). Every operation that changes the document confirms the open sheet first through
/// the action given by the coordinator of the documents, as MainWindow.Commit; it then raises <see cref="Changed"/>.
/// </summary>
public sealed class ProjectSession : DocumentSession
{
    /// <summary>Question before removing a node of the tree (MainWindow.Delete).</summary>
    public const string DeleteTitle = "Elimina";

    /// <inheritdoc cref="DeleteTitle"/>
    public const string DeleteQuestion = "Eliminare l'elemento selezionato e i suoi contenuti dal documento? Il file su disco resta invariato fino al salvataggio.";

    /// <summary>Error of a revision switch towards a section removed meanwhile (MainWindow.SwitchProjectRevision).</summary>
    public const string MissingScope = "La sezione non è più presente nel progetto.";

    private readonly IConfirmationService confirmations;
    private readonly Action commitOpenSheet;
    private JsonObject? working;
    private bool readOnly;
    private int? displayedRevision;

    /// <param name="document">A project archive («tipo» = «progetti»).</param>
    /// <param name="path">Its file, null for a new archive or the copy of an example.</param>
    /// <param name="dirty">True for an archive not saved yet (a copy of an example).</param>
    /// <param name="confirmations">Questions of the shared data, of the moves and of the removals.</param>
    /// <param name="commitOpenSheet">Confirms the open sheet into the document (MainWindow.Commit), before every operation.</param>
    public ProjectSession(JsonObject document, string? path, bool dirty, IConfirmationService confirmations, Action commitOpenSheet) : base(document, path, dirty)
    {
        ArgumentNullException.ThrowIfNull(confirmations);
        ArgumentNullException.ThrowIfNull(commitOpenSheet);
        if (document.S("tipo") != "progetti") throw new ArgumentException("Archivio progetti atteso: tipo «" + document.S("tipo") + "».", nameof(document));
        this.confirmations = confirmations;
        this.commitOpenSheet = commitOpenSheet;
        Shared = new SharedDataCoordinator(confirmations);
        // As MainWindow.ShowProjectOverview at the opening of an archive: a new history on the document, ids completed.
        History.Reset(document);
    }

    public override DocumentKind Kind => DocumentKind.Project;

    /// <summary>The working document; while an archived revision is shown, <see cref="DocumentSession.Document"/> is its snapshot.</summary>
    public override JsonObject WorkingDocument => working ?? Document;

    /// <summary>True while an archived revision is shown (MainWindow.projectReadOnly).</summary>
    public override bool IsReadOnly => readOnly;

    /// <summary>Number of the archived revision on screen, null for the current version.</summary>
    public override int? DisplayedRevision => displayedRevision;

    /// <summary>True while the snapshot of an archived revision is shown.</summary>
    public bool IsPreviewingRevision => working is not null;

    /// <summary>Section of the revisions on screen (MainWindow.revisionScopeId), null when none.</summary>
    public string? RevisionScopeId { get; private set; }

    /// <summary>Undo and redo of the project, 30 copies (F5.2).</summary>
    public ProjectHistory History { get; } = new();

    /// <summary>Confirmation of the shared data of the sheets (F5.2).</summary>
    public SharedDataCoordinator Shared { get; }

    public bool CanUndo => History.CanUndo(readOnly);

    public bool CanRedo => History.CanRedo(readOnly);

    /// <summary>The structure, the data of the sheets, the history or the revision on screen changed.</summary>
    public event EventHandler<ProjectChangedEventArgs>? Changed;

    /// <summary>The projects of the document on screen.</summary>
    public IEnumerable<JsonObject> Projects => Document.Array("progetti").OfType<JsonObject>();

    /// <summary>The node <paramref name="id"/> of the document on screen (section or sheet), or null.</summary>
    public JsonObject? Find(string? id) => id is null ? null : ProjectRevisions.Find(Document, id);

    /// <summary>True when <paramref name="node"/> belongs to the document on screen.</summary>
    public bool Contains(JsonObject node) => ReferenceEquals(node?.Root, Document);

    /// <summary>Records the current document in the history when it changed (MainWindow.RecordProjectHistory).</summary>
    public bool Record() => History.Record(Document, readOnly);

    protected override void OnMarkedDirty() => Record();

    // ---- Structure (MainWindow, ProjectHierarchy, ProjectSectionReorder, ProjectMovePreview)

    /// <summary>«+ Progetto» (MainWindow.AddProject): a project named «Progetto n»; null while read-only.</summary>
    public JsonObject? AddProject()
    {
        if (readOnly) return null;
        string name = ProjectDocuments.NextName(Document.Array("progetti"), "Progetto");
        commitOpenSheet();
        var project = ProjectDocuments.AddProject(Document, name);
        MarkDirty(); Raise(ProjectChange.Structure, project);
        return project;
    }

    /// <summary>«+ Sezione» inside <paramref name="parent"/> (MainWindow.AddStructureTo): a section named «Sezione n»; null while read-only.</summary>
    public JsonObject? AddSection(JsonObject parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (readOnly) return null;
        string name = ProjectDocuments.NextName(parent.Array("strutture"), "Sezione");
        commitOpenSheet();
        var section = ProjectDocuments.AddSection(parent, name);
        MarkDirty(); Raise(ProjectChange.Structure, section);
        return section;
    }

    /// <summary>True when a sheet can be added to <paramref name="section"/>: a project or a section (MainWindow.AddSheetTo).</summary>
    public static bool CanHoldSheets(JsonObject? section) => section is not null && (section.ContainsKey("fogli") || section.ContainsKey("strutture"));

    /// <summary>A sheet of <paramref name="module"/> in <paramref name="section"/> (MainWindow.AddSheetTo); null while read-only.</summary>
    /// <exception cref="ArgumentException">The section cannot hold sheets (<see cref="CanHoldSheets"/>).</exception>
    public JsonObject? AddSheet(string module, JsonObject section)
    {
        ArgumentException.ThrowIfNullOrEmpty(module);
        if (readOnly) return null;
        if (!CanHoldSheets(section)) throw new ArgumentException("Selezionare una sezione nell'albero dei progetti.", nameof(section));
        commitOpenSheet();
        var sheet = ProjectDocuments.AddSheet(section, module);
        MarkDirty(); Raise(ProjectChange.Structure, sheet);
        return sheet;
    }

    /// <summary>
    /// The copy of a sheet in another module inside the section of the sheet that asked it (MainWindow.OpenModuleCopy in a project): named
    /// «<paramref name="name"/> n», with a copy of <paramref name="data"/>; null while read-only.
    /// </summary>
    public JsonObject? CopySheet(string module, JsonObject section, string name, JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(data);
        if (readOnly) return null;
        commitOpenSheet();
        var sheet = ProjectDocuments.AddSheet(section, module, ProjectDocuments.NextName(section.Array("fogli"), name));
        sheet["dati"] = data.DeepClone();
        MarkDirty(); Raise(ProjectChange.Structure, sheet);
        return sheet;
    }

    /// <summary>
    /// Rename of a node (the end of the rename in the tree, ProjectHierarchy.BeginProjectRename): the text trimmed, nothing when empty or
    /// unchanged or while read-only. No confirmation of the open sheet, as today.
    /// </summary>
    /// <returns>True when the name changed.</returns>
    public bool Rename(JsonObject node, string name)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (readOnly || name is null) return false;
        name = name.Trim();
        if (name.Length == 0 || name == node.S("nome")) return false;
        ProjectDocuments.Rename(node, name);
        MarkDirty(); Raise(ProjectChange.Structure, node);
        return true;
    }

    /// <summary>«Duplica sezione» (MainWindow.DuplicateProjectSection): a copy without history, named «nome copia n», after the section.</summary>
    public JsonObject? Duplicate(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (readOnly || section.Parent is not JsonArray siblings) return null;
        commitOpenSheet();
        var copy = ProjectRevisions.Duplicate(section);
        copy["nome"] = ProjectDocuments.NextName(siblings, section.S("nome") + " copia");
        ProjectModelStore.ReidentifyCopy(copy);
        siblings.Insert(siblings.IndexOf(section) + 1, copy);
        MarkDirty(); Raise(ProjectChange.Structure, copy);
        return copy;
    }

    /// <summary>
    /// «Elimina» (MainWindow.Delete and DeleteProjectNode): the question first; then the open sheet is confirmed and the node removed with
    /// its contents. The sheets removed with it close (the coordinator reconciles them).
    /// </summary>
    /// <returns>False when read-only or when the user keeps the node; true when removed.</returns>
    public bool Delete(JsonObject node, out JsonObject? overview)
    {
        ArgumentNullException.ThrowIfNull(node);
        overview = null;
        if (readOnly || !confirmations.ConfirmDelete(DeleteTitle, DeleteQuestion)) return false;
        commitOpenSheet();
        var parent = node.Parent?.Parent as JsonObject;
        if (node.Parent is JsonArray list) list.Remove(node);
        overview = parent?.ContainsKey("strutture") == true ? parent : null;
        MarkDirty(); Raise(ProjectChange.Structure, overview);
        return true;
    }

    /// <summary>
    /// A module of the catalogue dropped on <paramref name="target"/> (the drop effect of ProjectHierarchy): a project or section, a known
    /// module, not while read-only.
    /// </summary>
    public bool CanDropModule(JsonObject target, string module) => !readOnly && CanHoldSheets(target) && Archivio.Moduli.Contains(module);

    /// <summary>A sheet dropped inside <paramref name="target"/> (ProjectHierarchy): a sheet of this document from another list of sheets.</summary>
    public bool CanDropSheet(JsonObject target, JsonObject sheet) => !readOnly && CanHoldSheets(target) && sheet.Parent is JsonArray list &&
        ReferenceEquals(sheet.Root, Document) && !ReferenceEquals(list, target["fogli"]);

    /// <summary>A sheet dropped before or after the sheet <paramref name="anchor"/> (ProjectHierarchy.EnableSheetReorder).</summary>
    public bool CanReorderSheet(JsonObject anchor, JsonObject sheet) => !readOnly && sheet.ContainsKey("modulo_id") && sheet.Parent is JsonArray &&
        ReferenceEquals(sheet.Root, Document) && !ReferenceEquals(sheet, anchor);

    /// <summary>
    /// Moves a sheet into <paramref name="destination"/>, at the end or before/after <paramref name="anchor"/> (MainWindow.MoveProjectSheet),
    /// with the same checks and the same index. Towards another section the move is previewed and confirmed first
    /// (<see cref="SharedDataCoordinator.PreviewMove"/>) and the sheet takes the inherited data.
    /// </summary>
    /// <returns>True when the sheet moved.</returns>
    public bool MoveSheet(JsonObject sheet, JsonObject destination, JsonObject? anchor = null, bool after = false)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(destination);
        if (readOnly) return false;
        if (destination["fogli"] is null && destination.ContainsKey("strutture")) destination["fogli"] = new JsonArray();
        if (sheet.Parent is not JsonArray source || destination["fogli"] is not JsonArray target) return false;
        if (anchor is null && ReferenceEquals(source, target) || ReferenceEquals(sheet, anchor)) return false;
        if (anchor is not null && !ReferenceEquals(anchor.Parent, target)) return false;
        int index = anchor is null ? target.Count : target.IndexOf(anchor) + (after ? 1 : 0);
        if (ReferenceEquals(source, target))
        {
            int previous = source.IndexOf(sheet);
            if (previous < index) index--;
            if (previous == index) return false;
        }
        commitOpenSheet();
        JsonObject? proposal = null;
        if (!ReferenceEquals(source, target))
        {
            proposal = Shared.PreviewMove(Document, sheet, destination);
            if (proposal is null) return false;
        }
        source.Remove(sheet); target.Insert(index, sheet);
        if (proposal is not null) sheet["dati"] = proposal["dati"]!.DeepClone();
        MarkDirty(); Raise(ProjectChange.Structure, sheet);
        return true;
    }

    /// <summary>A section dropped on the edge of <paramref name="anchor"/>: same parent, never a sheet (MainWindow.CanReorderProjectSection).</summary>
    public bool CanReorderSection(JsonObject section, JsonObject anchor) => !readOnly &&
        !ReferenceEquals(section, anchor) && !section.ContainsKey("modulo_id") && !anchor.ContainsKey("modulo_id") &&
        ReferenceEquals(section.Root, Document) && section.Parent is JsonArray siblings &&
        ReferenceEquals(anchor.Parent, siblings) && siblings.Parent is JsonObject owner &&
        (ReferenceEquals(owner["strutture"], siblings) || ReferenceEquals(Document["progetti"], siblings));

    /// <summary>Moves a section before or after <paramref name="anchor"/> among its siblings (MainWindow.MoveProjectSection).</summary>
    /// <returns>True when the order changed.</returns>
    public bool ReorderSection(JsonObject section, JsonObject anchor, bool after)
    {
        if (!CanReorderSection(section, anchor)) return false;
        var siblings = (JsonArray)section.Parent!;
        int previous = siblings.IndexOf(section), index = siblings.IndexOf(anchor) + (after ? 1 : 0);
        if (previous < index) index--;
        if (previous == index) return false;
        commitOpenSheet(); siblings.Remove(section); siblings.Insert(index, section);
        MarkDirty(); Raise(ProjectChange.Structure, section);
        return true;
    }

    /// <summary>A section dropped in the middle of <paramref name="target"/>: never a project, never into itself or its parent (MainWindow.CanNestSection).</summary>
    public bool CanNestSection(JsonObject source, JsonObject target) => !readOnly && !source.ContainsKey("modulo_id") &&
        !target.ContainsKey("modulo_id") && ReferenceEquals(source.Root, Document) && ReferenceEquals(target.Root, Document) &&
        !ReferenceEquals(source.Parent, Document["progetti"]) && !ProjectSharedData.Sections(source).Contains(target) && !ReferenceEquals(source.Parent?.Parent, target);

    /// <summary>Moves a section inside <paramref name="target"/>, previewed and confirmed, with the inherited data (MainWindow.NestProjectSection).</summary>
    /// <returns>True when the section moved.</returns>
    public bool NestSection(JsonObject source, JsonObject target)
    {
        if (!CanNestSection(source, target)) return false;
        commitOpenSheet();
        var preview = Shared.PreviewMove(Document, source, target);
        if (preview is null) return false;
        if (target["strutture"] is not JsonArray) target["strutture"] = new JsonArray();
        ((JsonArray)source.Parent!).Remove(source); target.Array("strutture").Add(source);
        var data = ProjectSharedData.SubtreeSheets(preview).ToDictionary(s => s.S("id"));
        foreach (var sheet in ProjectSharedData.SubtreeSheets(source)) sheet["dati"] = data[sheet.S("id")]["dati"]!.DeepClone();
        MarkDirty(); Raise(ProjectChange.Structure, source);
        return true;
    }

    /// <summary>
    /// A change of the data of sheets outside the open sheet («Uniforma a questo», copy of missing layers, link of the stratigraphies in the
    /// comparison, models): the open sheet is confirmed first, as at the opening of the comparison; when <paramref name="change"/> reports a
    /// change the document is modified and the open sheets reconcile. Nothing while read-only.
    /// </summary>
    public bool ChangeData(Func<bool> change, JsonObject? node = null)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (readOnly) return false;
        commitOpenSheet();
        if (!change()) return false;
        MarkDirty(); Raise(ProjectChange.Data, node);
        return true;
    }

    // ---- History (MainWindow.ProjectWorkspace)

    /// <summary>
    /// «↶ Annulla» or «↷ Ripristina» (MainWindow.RestoreProjectHistory): the open sheet is confirmed first, then the copy becomes the
    /// document, which is not saved. Nothing while read-only or without copies.
    /// </summary>
    public bool Restore(bool redo)
    {
        if (History.Restore(redo, readOnly, commitOpenSheet, () => Document) is not JsonObject restored) return false;
        Document = restored;
        SetDirty(true);
        Raise(ProjectChange.Restored);
        return true;
    }

    // ---- Revisions (MainWindow.ProjectRevisionNavigation)

    /// <summary>
    /// Section of the revision bar for <paramref name="node"/> (MainWindow.RevisionScope): while a revision is shown, the section on
    /// screen in the working document; for a section, itself; for a sheet, the nearest section above it with a revision, otherwise its
    /// section.
    /// </summary>
    public JsonObject? RevisionScope(JsonObject? node)
    {
        if (working is not null) return RevisionScopeId is null ? null : ProjectRevisions.Find(working, RevisionScopeId);
        if (node?.ContainsKey("modulo_id") != true) return node;
        node = node.Parent?.Parent as JsonObject;
        if (node is null) return null;
        return new[] { node }.Concat(ProjectSharedData.Ancestors(node)).FirstOrDefault(s => s["revisione"] is JsonObject) ?? node;
    }

    /// <summary>The section of the revisions just switched to (the bar during MainWindow.SwitchProjectRevision): in the working document.</summary>
    public JsonObject? CurrentRevisionScope => RevisionScopeId is null ? null : ProjectRevisions.Find(WorkingDocument, RevisionScopeId);

    /// <summary>«Crea revisione» (MainWindow.CompleteProjectRevision): the current version is archived and a new revision starts.</summary>
    public bool NewRevision(JsonObject section, string note)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (readOnly) return false;
        commitOpenSheet();
        ProjectRevisions.NewRevision(Document, section, note ?? "");
        MarkDirty(); Raise(ProjectChange.Structure, section);
        return true;
    }

    /// <summary>
    /// Shows revision <paramref name="number"/> of the section <paramref name="scopeId"/> read-only, or the current version when null
    /// (MainWindow.SwitchProjectRevision): the snapshot holds only the project of the section, with the revision shown; the working document
    /// and its history stay untouched. Nothing when the same revision is already shown.
    /// </summary>
    /// <exception cref="ArgumentException">The section is no longer in the project (<see cref="MissingScope"/>).</exception>
    public bool SwitchRevision(string scopeId, int? number)
    {
        ArgumentException.ThrowIfNullOrEmpty(scopeId);
        if (number == displayedRevision && scopeId == RevisionScopeId && (number is null || readOnly)) return false;
        commitOpenSheet();
        var current = WorkingDocument;
        var scope = ProjectRevisions.Find(current, scopeId) ?? throw new ArgumentException(MissingScope);
        JsonObject destination = current;
        if (number is int requested)
        {
            var entry = scope.Array("revisioni").OfType<JsonObject>().Single(r => ShellTexts.RevisionNumber(r) == requested);
            destination = Snapshot(current, entry, scopeId, requested);
        }
        working = number is null ? null : current;
        Document = destination; displayedRevision = number; RevisionScopeId = scopeId; readOnly = number is not null;
        RaiseStateChanged(); Raise(ProjectChange.RevisionShown, scope);
        return true;
    }

    /// <summary>
    /// The document of an archived revision (MainWindow.SwitchProjectRevision): the snapshot of the project of the section, with the section
    /// marked with the revision shown (number, date, note), in an archive of its own.
    /// </summary>
    public static JsonObject Snapshot(JsonObject document, JsonObject entry, string scopeId, int number)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(entry);
        var snapshot = ProjectRevisions.Snapshot(document, entry);
        var target = ProjectSharedData.Sections(snapshot).Single(s => s.S("id") == scopeId);
        target["revisione"] = J.Obj(("numero", number), ("data", entry.S("data")), ("nota", entry.S("nota")));
        return J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray(snapshot)));
    }

    /// <summary>
    /// «Elimina revisione» (MainWindow.DeleteProjectRevision): prepared and validated on a copy before touching the document; the preview
    /// ends, the copy becomes the working document of the same history and the change can be undone.
    /// </summary>
    /// <returns>False when the section is missing or has no archived revision.</returns>
    public bool DeleteRevision(string scopeId, int number)
    {
        ArgumentException.ThrowIfNullOrEmpty(scopeId);
        var current = WorkingDocument;
        var scope = ProjectRevisions.Find(current, scopeId);
        if (scope is null || scope.Array("revisioni").Count == 0) return false;
        commitOpenSheet();
        var updated = (JsonObject)current.DeepClone();
        ProjectRevisions.DeleteRevision(updated, scopeId, number);
        Archivio.Valida(updated); ProjectRevisions.Validate(updated);
        LeavePreview();
        Document = updated; History.Track(updated); readOnly = false; RevisionScopeId = scopeId;
        MarkDirty(); RaiseStateChanged(); Raise(ProjectChange.RevisionDeleted, ProjectRevisions.Find(updated, scopeId));
        return true;
    }

    /// <summary>
    /// Leaves the preview of an archived revision (MainWindow.ExitRevisionPreview): the working document comes back, editable. The revision
    /// and the section on screen are forgotten in any case.
    /// </summary>
    /// <returns>True when a revision was on screen.</returns>
    public bool ExitPreview()
    {
        bool previewing = LeavePreview();
        RaiseStateChanged();
        if (previewing) Raise(ProjectChange.PreviewExited);
        return previewing;
    }

    private bool LeavePreview()
    {
        bool previewing = working is not null;
        if (previewing) { Document = working!; working = null; readOnly = false; }
        displayedRevision = null; RevisionScopeId = null;
        return previewing;
    }

    private void Raise(ProjectChange change, JsonObject? node = null) => Changed?.Invoke(this, new ProjectChangedEventArgs(change, node));
}
