using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Services;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>Why a sheet left the shell.</summary>
public enum SheetCloseReason
{
    /// <summary>The user closed the tab.</summary>
    Closed,
    /// <summary>The sheet was removed from the project (with its section).</summary>
    Removed,
    /// <summary>The document on screen changed (undo, redo, revision) and has no sheet with that identifier.</summary>
    Missing,
    /// <summary>Its document was closed.</summary>
    DocumentClosed
}

/// <summary>A sheet closed by the shell, with the reason.</summary>
public sealed class SheetClosedEventArgs(SheetSession sheet, SheetCloseReason reason) : EventArgs
{
    public SheetSession Sheet { get; } = sheet;
    public SheetCloseReason Reason { get; } = reason;
}

/// <summary>Sheets reopened on data changed outside them (proposal 4.4, point 3): the sheet whose confirmation changed them, when known.</summary>
public sealed class SheetsReloadedEventArgs(SheetSession? source, IReadOnlyList<SheetSession> sheets, ProjectChange? change) : EventArgs
{
    /// <summary>The sheet confirmed just before, whose shared data reached the others; null for the other changes.</summary>
    public SheetSession? Source { get; } = source;

    public IReadOnlyList<SheetSession> Sheets { get; } = sheets;

    /// <summary>The change of the project, null after the confirmation of a sheet.</summary>
    public ProjectChange? Change { get; } = change;
}

/// <summary>
/// Coordinator of the open documents of the shell (F5.13 a, proposal 3.5, 4.1 and 4.4): at most one project archive and any number of
/// single calculation files, each sheet in at most one tab, one active sheet.
/// <list type="bullet">
/// <item>One project at a time: opening or creating another archive closes the open one first, with the question to save it.</item>
/// <item>A file already open is activated, never opened twice; «Nuovo …» opens a new unsaved file.</item>
/// <item>Activating another sheet confirms the active one first (Q9 a): at most one sheet has unconfirmed edits, so the shared data
/// reach the other open sheets of the project, which reopen on the new data.</item>
/// <item>Undo, redo and revisions replace the document: the sheets reopen on the node with the same identifier, or close.</item>
/// </list>
/// The questions and messages are those of MainWindow, through the services of the presentation layer.
/// </summary>
public sealed class WorkspaceSession : IDisposable
{
    /// <summary>Filter of the documents of ANTHEA in the save dialog (MainWindow.Save).</summary>
    public static IReadOnlyList<FileFilter> SaveFilters { get; } = [new FileFilter("File ANTHEA", "*.programma")];

    /// <summary>Filters of the open dialog (MainWindow.Open).</summary>
    public static IReadOnlyList<FileFilter> OpenFilters { get; } = [new FileFilter("File ANTHEA", "*.programma", "*.anthea"), new FileFilter("Tutti i file", "*.*")];

    /// <summary>Title of the error of a save (MainWindow.Save).</summary>
    public const string SaveFailure = "Salvataggio non riuscito";

    /// <summary>A copy of an example saved among the installed examples (MainWindow.Save).</summary>
    public const string ExamplesFolderRefused = "Salva la copia nella cartella dei tuoi lavori, fuori dagli esempi installati.";

    private readonly PresentationServices services;
    private readonly ISheetFactory factory;
    private readonly IDocumentFiles files;
    private readonly List<CalculationDocumentSession> calculations = [];
    private readonly List<SheetSession> sheets = [];
    private bool disposed;

    public WorkspaceSession(PresentationServices services, ISheetFactory factory, IDocumentFiles files)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(files);
        this.services = services; this.factory = factory; this.files = files;
    }

    /// <summary>The open project archive, at most one.</summary>
    public ProjectSession? Project { get; private set; }

    /// <summary>The open single calculation files, in the order of opening.</summary>
    public IReadOnlyList<CalculationDocumentSession> Calculations => calculations;

    /// <summary>The open sheets, in the order of opening.</summary>
    public IReadOnlyList<SheetSession> Sheets => sheets;

    /// <summary>The sheet with the focus, null when a page (home, project overview, Wiki…) is shown.</summary>
    public SheetSession? Active { get; private set; }

    /// <summary>The open documents: the project first, then the single files.</summary>
    public IEnumerable<DocumentSession> Documents => Project is null ? calculations.ToArray() : new DocumentSession[] { Project }.Concat(calculations).ToArray();

    /// <summary>The document of the window title: of the active sheet, otherwise the project.</summary>
    public DocumentSession? CurrentDocument => Active?.Owner ?? Project;

    public event EventHandler<SheetSession>? SheetOpened;

    public event EventHandler<SheetClosedEventArgs>? SheetClosed;

    public event EventHandler? ActiveChanged;

    /// <summary>A document was opened or closed, or the project changed.</summary>
    public event EventHandler? DocumentsChanged;

    public event EventHandler<SheetsReloadedEventArgs>? SheetsReloaded;

    /// <summary>A sheet asks for its report (the «Report Word» command of the window, ISheetHost.RequestReport).</summary>
    public event EventHandler<SheetSession>? ReportRequested;

    // ---- Focus and confirmation

    /// <summary>
    /// Gives the focus to <paramref name="sheet"/> (null: a page): the active sheet is confirmed first, as when it loses the focus (Q9 a).
    /// If the confirmation fails, the focus does not move and the error goes on.
    /// </summary>
    public void Activate(SheetSession? sheet)
    {
        ThrowIfDisposed();
        if (ReferenceEquals(Active, sheet)) return;
        if (sheet is not null && !sheets.Contains(sheet)) throw new ArgumentException("Foglio non aperto nella shell.", nameof(sheet));
        CommitActive();
        SetActive(sheet);
    }

    /// <summary>Confirms the active sheet (MainWindow.Commit), then reopens the other sheets whose data changed meanwhile.</summary>
    public void CommitActive()
    {
        ThrowIfDisposed();
        if (Active is not { } sheet) return;
        sheet.Commit();
        Reconcile(null, sheet);
    }

    // ---- Documents

    /// <summary>«Nuovo …» (proposal 4.1): a new unsaved file of <paramref name="module"/> in a tab of its own, active.</summary>
    public SheetSession NewCalculation(string module)
    {
        ThrowIfDisposed();
        return AddCalculation(CalculationDocumentSession.New(module));
    }

    /// <summary>
    /// «Nuovo archivio progetti»: the open project closes first (with the question to save it); null when the user cancels.
    /// </summary>
    public ProjectSession? NewProject()
    {
        ThrowIfDisposed();
        return OpenProject(ProjectDocuments.CreateArchive(), null, false);
    }

    /// <summary>The open project, or a new archive when none is open (the «Progetti» page of MainWindow without a project).</summary>
    public ProjectSession? EnsureProject() => Project ?? NewProject();

    /// <summary>
    /// «Apri…» (MainWindow.LoadFile): an installed example opens as an unsaved copy; a file already open is activated; otherwise the
    /// document is read and validated first, then opened (a project closes the open one first). Null when the user cancels.
    /// </summary>
    public DocumentSession? OpenFile(string path)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (files.IsInExamples(path) && files.OpenExampleCopy(path) is { } example) return OpenDocument(example, null, true);
        if (Documents.FirstOrDefault(d => d.IsFile(path)) is { } open)
        {
            ActivateDocument(open);
            return open;
        }
        return OpenDocument(files.Open(path), path, false);
    }

    /// <summary>
    /// Opens a document read elsewhere (a copy of an example, the example of the Wiki): a calculation file in a new active tab, a project
    /// archive in place of the open project. Null when the user keeps the open project.
    /// </summary>
    public DocumentSession? OpenDocument(JsonObject document, string? path, bool dirty)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(document);
        if (document.S("tipo") == "calcolo")
        {
            var calculation = new CalculationDocumentSession(document, path, dirty);
            AddCalculation(calculation);
            return calculation;
        }
        return OpenProject(document, path, dirty);
    }

    /// <summary>Activates the sheet of a single file or the last opened sheet of the project, when one is open.</summary>
    public void ActivateDocument(DocumentSession document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (sheets.LastOrDefault(s => ReferenceEquals(s.Owner, document)) is { } sheet) Activate(sheet);
    }

    /// <summary>
    /// «Salva» and «Salva con nome…» (MainWindow.Save): the active sheet is confirmed; a new document or «Salva con nome» asks the file
    /// («Calcolo.programma» or the current name); the installed examples are refused; the working document is written (never the snapshot
    /// of a revision). An error is shown with the title «Salvataggio non riuscito».
    /// </summary>
    /// <returns>True when the document was written.</returns>
    public bool Save(DocumentSession document, bool asNew)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(document);
        CommitActive();
        string? target = document.Path;
        if (asNew || target is null)
        {
            target = services.Files.SaveFile(new SaveFileRequest(SaveFilters, document.Path is null ? "Calcolo.programma" : System.IO.Path.GetFileName(document.Path)));
            if (target is null) return false;
        }
        try
        {
            if (files.IsInExamples(target)) throw new InvalidOperationException(ExamplesFolderRefused);
            files.Save(target, document.WorkingDocument);
            document.MarkSaved(target);
            foreach (var sheet in sheets.Where(s => ReferenceEquals(s.Owner, document))) sheet.MarkSaved();
            return true;
        }
        catch (Exception error)
        {
            services.Messages.ReportFailure(SaveFailure, error);
            return false;
        }
    }

    /// <summary>
    /// The question before a document closes (MainWindow.ConfirmDiscard, through IConfirmationService.AskSaveChanges as planned since F5.2):
    /// the active sheet is confirmed; a saved document closes; otherwise its sheet is activated and the user saves, discards or cancels.
    /// </summary>
    /// <returns>False when the user cancels or the save fails.</returns>
    public bool ConfirmDiscard(DocumentSession document)
    {
        ArgumentNullException.ThrowIfNull(document);
        CommitActive();
        if (!document.IsDirty) return true;
        if (Active?.Owner != document) ActivateDocument(document);
        var answer = services.Confirmations.AskSaveChanges();
        return answer != SaveChangesAnswer.Cancel && (answer != SaveChangesAnswer.Save || Save(document, false));
    }

    /// <summary>Closes the project after the question to save it; its sheets close. False when the user keeps it.</summary>
    public bool CloseProject()
    {
        ThrowIfDisposed();
        if (Project is not { } project) return true;
        if (!ConfirmDiscard(project)) return false;
        foreach (var sheet in sheets.Where(s => ReferenceEquals(s.Owner, project)).ToArray()) Close(sheet, SheetCloseReason.DocumentClosed);
        project.Changed -= OnProjectChanged;
        Project = null;
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// The user closes a tab: a sheet of the project is confirmed into the document, which stays open; the sheet of a single file closes the
    /// file, after the question to save it. False when the user cancels.
    /// </summary>
    public bool CloseSheet(SheetSession sheet)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(sheet);
        if (!sheets.Contains(sheet)) return true;
        if (sheet.Owner is CalculationDocumentSession calculation)
        {
            if (!ConfirmDiscard(calculation)) return false;
            Close(sheet, SheetCloseReason.DocumentClosed);
            calculations.Remove(calculation);
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        if (ReferenceEquals(sheet, Active)) CommitActive();
        else sheet.Commit();
        Close(sheet, SheetCloseReason.Closed);
        return true;
    }

    /// <summary>
    /// Closing of the application (MainWindow.Closing → IConfirmationService.ConfirmClose): one question for every unsaved document, the
    /// project first, then the single files in order of opening; the first «Annulla» keeps everything open.
    /// </summary>
    public bool ConfirmClose()
    {
        ThrowIfDisposed();
        return services.Confirmations.ConfirmClose(() => Documents.ToArray().All(ConfirmDiscard));
    }

    // ---- Sheets of the project

    /// <summary>
    /// Opens a sheet of the project in a tab and activates it (the «Apri scheda» of the tree): the active sheet is confirmed first; a sheet
    /// already open is only activated.
    /// </summary>
    public SheetSession OpenSheet(JsonObject sheet)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(sheet);
        if (Project is not { } project || !project.Contains(sheet) || !sheet.ContainsKey("modulo_id"))
            throw new ArgumentException("Foglio non presente nel progetto aperto.", nameof(sheet));
        if (sheets.FirstOrDefault(s => ReferenceEquals(s.Sheet, sheet)) is { } open)
        {
            Activate(open);
            return open;
        }
        CommitActive();
        return Add(new SheetSession(project, sheet, factory, s => new Host(this, s)));
    }

    /// <summary>The open sheet of <paramref name="node"/>, or null.</summary>
    public SheetSession? SheetOf(JsonObject node) => sheets.FirstOrDefault(s => ReferenceEquals(s.Sheet, node));

    // ---- Requests of the sheets (ISheetHost)

    /// <summary>
    /// Copy of the data of a sheet in another module (MainWindow.OpenModuleCopy): inside an editable project a new sheet of the same section,
    /// named after <paramref name="name"/>, opened in a tab; otherwise an unsaved single file in a tab (today a second window).
    /// </summary>
    public SheetSession OpenModuleCopy(SheetSession from, string module, JsonObject data, string name)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(data);
        ModuleDirectory.ValidateData(module, data);
        CommitActive();
        if (from.Project is { IsReadOnly: false } project && from.Sheet.Parent?.Parent is JsonObject section && project.CopySheet(module, section, name, data) is { } sheet)
            return OpenSheet(sheet);
        return AddCalculation(CalculationDocumentSession.Copy(module, data, name));
    }

    /// <summary>
    /// Replaces the soil of <paramref name="sheet"/> (MainWindow.ReplaceCurrentSoil): nothing while read-only; the data are validated,
    /// written into the document, the sheet reopens on them and the document is modified. No question about the shared data, as today.
    /// </summary>
    public void ReplaceSoil(SheetSession sheet, JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (sheet.IsReadOnly || sheet.IsDisposed) return;
        ModuleDirectory.ValidateData(sheet.Module, data);
        sheet.Sheet["dati"] = data.DeepClone();
        sheet.Reopen();
        sheet.Owner.MarkDirty();
    }

    /// <summary>
    /// Other sheets of the projects of the document that can give or receive the soil (MainWindow.LoadSheetEditor, SoilSources): label
    /// «nome · modulo», live data; none for a single file.
    /// </summary>
    public IReadOnlyList<SoilSource> SoilSources(SheetSession sheet) => sheet.Owner.Document.Array("progetti").OfType<JsonObject>()
        .SelectMany(ProjectSharedData.SubtreeSheets)
        .Where(other => !ReferenceEquals(other, sheet.Sheet) && SoilProfileTransfer.Supports(other.S("modulo_id")))
        .Select(other => new SoilSource(other.S("nome") + " · " + ShellTexts.ModuleName(other.S("modulo_id")), other.S("modulo_id"), other["dati"]!.AsObject(), other.S("id")))
        .ToArray();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var sheet in sheets) sheet.Dispose();
        sheets.Clear();
        if (Project is not null) Project.Changed -= OnProjectChanged;
    }

    // ---- Internals

    private SheetSession AddCalculation(CalculationDocumentSession calculation)
    {
        CommitActive();
        calculations.Add(calculation);
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        return Add(new SheetSession(calculation, calculation.Sheet, factory, s => new Host(this, s)));
    }

    private ProjectSession? OpenProject(JsonObject document, string? path, bool dirty)
    {
        if (!CloseProject()) return null;
        var project = new ProjectSession(document, path, dirty, services.Confirmations, CommitActive);
        project.Changed += OnProjectChanged;
        Project = project;
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        return project;
    }

    private SheetSession Add(SheetSession sheet)
    {
        sheets.Add(sheet);
        SheetOpened?.Invoke(this, sheet);
        SetActive(sheet);
        return sheet;
    }

    private void SetActive(SheetSession? sheet)
    {
        if (ReferenceEquals(Active, sheet)) return;
        Active = sheet;
        ActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Close(SheetSession sheet, SheetCloseReason reason)
    {
        if (!sheets.Remove(sheet)) return;
        if (ReferenceEquals(Active, sheet)) SetActive(null);
        sheet.Dispose();
        SheetClosed?.Invoke(this, new SheetClosedEventArgs(sheet, reason));
    }

    private void OnProjectChanged(object? sender, ProjectChangedEventArgs e) => Reconcile(e.Change, null);

    /// <summary>
    /// After a change of the project (or the confirmation of <paramref name="source"/>): each other sheet of the project still in the document
    /// on screen reopens if its data changed; a sheet of a replaced document reopens on the node with the same identifier (read-only during
    /// the preview of a revision) or closes: with a notice after undo and redo (proposal 3.7, SH5), silently for the revisions (SH6), as
    /// removed after a change of the structure.
    /// </summary>
    private void Reconcile(ProjectChange? change, SheetSession? source)
    {
        if (Project is not { } project) return;
        var reloaded = new List<SheetSession>();
        var missing = new List<string>();
        foreach (var sheet in sheets.Where(s => ReferenceEquals(s.Owner, project) && !ReferenceEquals(s, source)).ToArray())
        {
            if (project.Contains(sheet.Sheet))
            {
                if (sheet.ChangedOutside)
                {
                    // SH9: after the confirmation of another sheet, the reloaded tab says which sheet changed which of its common data.
                    string? notice = source is null ? null : ReloadNotice(source, sheet);
                    sheet.Reopen();
                    sheet.SetReloadNotice(notice);
                    reloaded.Add(sheet);
                }
                else sheet.Refresh();
                continue;
            }
            bool replaced = change is ProjectChange.Restored or ProjectChange.RevisionShown or ProjectChange.RevisionDeleted or ProjectChange.PreviewExited;
            if (replaced && project.Find(sheet.Id) is { } node && node.S("modulo_id") == sheet.Module)
            {
                sheet.Reopen(node);
                reloaded.Add(sheet);
                continue;
            }
            if (change is ProjectChange.Restored) missing.Add(ShellTexts.SheetName(sheet.Sheet));
            Close(sheet, replaced ? SheetCloseReason.Missing : SheetCloseReason.Removed);
        }
        if (reloaded.Count > 0) SheetsReloaded?.Invoke(this, new SheetsReloadedEventArgs(source, reloaded, change));
        if (missing.Count > 0) services.Messages.Inform("ANTHEA", string.Join("\n", missing.Select(MissingSheet)));
    }

    /// <summary>
    /// The notice of <paramref name="sheet"/>, reloaded after the confirmation of <paramref name="source"/> (SH9): the common data of the sheet that
    /// differ from those it loaded last (ProjectSharedData.ChangedKeys), with the labels of the comparison.
    /// </summary>
    private static string ReloadNotice(SheetSession source, SheetSession sheet)
    {
        var before = (JsonObject)sheet.Sheet.DeepClone();
        before["dati"] = sheet.LoadedData?.DeepClone();
        var fields = ProjectSharedData.ChangedKeys(before, sheet.Sheet).Select(SharedDataCoordinator.FieldLabel).Distinct().Order(StringComparer.CurrentCulture).ToArray();
        return ShellTexts.ReloadNotice(ShellTexts.SheetName(source.Sheet), fields);
    }

    /// <summary>Notice of a tab closed by undo or redo (proposal 3.7; provisional text, question SH5).</summary>
    public static string MissingSheet(string name) => "Il foglio «" + name + "» non esiste più nel progetto: la scheda è stata chiusa.";

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    /// <summary>The window side of a sheet (ISheetHost), in place of the events of the historic editor.</summary>
    private sealed class Host(WorkspaceSession workspace, SheetSession sheet) : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => workspace.OpenModuleCopy(sheet, module, data, name);

        public void ReplaceSoil(JsonObject data) => workspace.ReplaceSoil(sheet, data);

        public IReadOnlyList<SoilSource> SoilSources() => workspace.SoilSources(sheet);

        public void RequestReport() => workspace.ReportRequested?.Invoke(workspace, sheet);
    }
}
