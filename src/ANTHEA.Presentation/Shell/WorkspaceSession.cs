using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Projects;
using Anthea.Projects;
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

    /// <summary>Title of the warning of a save that left out results the archive did not have (ProjectFile, 3/11/2026).</summary>
    public const string SaveWarning = "Salvataggio con risultati mancanti";

    /// <summary>A copy of an example saved among the installed examples (MainWindow.Save).</summary>
    public const string ExamplesFolderRefused = "Salva la copia nella cartella dei tuoi lavori, fuori dagli esempi installati.";

    /// <summary>Caption of the question when the file to open is open in another ANTHEA (step 2d, 3/11/2026).</summary>
    public const string LockedTitle = "File già aperto";

    /// <summary>Caption of the question when the file to save changed on disk after the opening or the last save.</summary>
    public const string ChangedTitle = "File modificato su disco";

    /// <summary>The question when <paramref name="file"/> is open <paramref name="holder"/> (<see cref="DocumentLockOwner.Description"/>).</summary>
    public static string LockedQuestion(string file, string holder) =>
        "«" + file + "» è già aperto " + holder + ".\n\n" +
        "Apri una copia: lavori su una copia senza nome, da salvare con un altro nome; il file non cambia.\n" +
        "Apri comunque: lavori sul file stesso; salvando puoi sovrascrivere le modifiche dell'altra apertura.";

    /// <summary>The question when <paramref name="file"/> changed on disk after it was opened or saved.</summary>
    public static string ChangedQuestion(string file) =>
        "«" + file + "» è stato modificato su disco dopo l'apertura o l'ultimo salvataggio (da un altro programma o da un'altra apertura di ANTHEA).\n\n" +
        "Sovrascriverlo con il documento di questa finestra? Con «No» il salvataggio si ferma e puoi salvare con un altro nome.";

    /// <summary>The refusal of «Salva con nome…» on <paramref name="file"/>, open <paramref name="holder"/>.</summary>
    public static string LockedTarget(string file, string holder) => "«" + file + "» è aperto " + holder + ": salva con un altro nome.";

    private readonly PresentationServices services;
    private readonly ISheetFactory factory;
    private readonly IDocumentFiles files;
    private readonly List<CalculationDocumentSession> calculations = [];
    private readonly List<SheetSession> sheets = [];
    /// <summary>The sheets closed by the preview of an archived revision, with the focus they had (F5.13 e).</summary>
    private readonly List<(string Id, bool Active)> previewClosed = [];
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
        ActiveCommitted?.Invoke(this, sheet);
    }

    /// <summary>
    /// The sheet with the focus was confirmed into its document (F5.13 e): the window refreshes what it shows from the document, as MainWindow.Commit
    /// refreshes the status line of the shared data.
    /// </summary>
    public event EventHandler<SheetSession>? ActiveCommitted;

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
        return OpenProject(ProjectDocuments.CreateArchive(), null, false, null);
    }

    /// <summary>The open project, or a new archive when none is open (the «Progetti» page of MainWindow without a project).</summary>
    public ProjectSession? EnsureProject() => Project ?? NewProject();

    /// <summary>
    /// «Apri…» (MainWindow.LoadFile): an installed example opens as an unsaved copy; a file already open is activated; otherwise the lock of
    /// the file is taken (step 2d: a file open in another ANTHEA asks whether to open a copy without a name, the file anyway or nothing),
    /// then the document is read and validated, then opened (a project closes the open one first). Null when the user cancels.
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
        var acquired = DocumentLock.Acquire(path);
        bool copy = false;
        if (acquired.Holder is { } holder)
        {
            var answer = services.Confirmations.AskLockedDocument(LockedTitle, LockedQuestion(System.IO.Path.GetFileName(path), holder.Description));
            if (answer == LockedDocumentAnswer.Cancel) return null;
            copy = answer == LockedDocumentAnswer.OpenCopy;
        }
        DocumentSession? opened = null;
        try
        {
            // The file before it is read: a change written by another program while it is read is found by «Salva».
            var stamp = FileStamp.Of(path);
            var document = files.Open(path);
            // A copy reads the results of its models from the archive beside the file, which it never writes.
            opened = copy ? OpenDocument(document, null, true, path) : OpenDocument(document, path, false, null);
            if (opened is not null && !copy) opened.AttachFile(acquired.Lock, stamp);
            return opened;
        }
        finally
        {
            if (opened is null) acquired.Lock?.Dispose();
        }
    }

    /// <summary>
    /// «Riprendi» of a recovery copy at the start (step 2e): the document of the copy, not saved, on its file when the copy has one and no
    /// other ANTHEA holds it (otherwise as a copy without a name), with the result sets of its models not saved yet; «Salva» asks before
    /// overwriting a file changed after the copy was taken. The copy is deleted once the document is open. Null when the user keeps the open
    /// project.
    /// </summary>
    public DocumentSession? Resume(RecoveryEntry entry)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(entry);
        var document = entry.ReadDocument();
        string? path = entry.Info.File;
        DocumentLock? taken = null;
        if (path is not null && Documents.Any(d => d.IsFile(path))) path = null;
        bool copy = false;
        if (path is not null)
        {
            var acquired = DocumentLock.Acquire(path);
            copy = acquired.Holder is not null;
            taken = acquired.Lock;
        }
        DocumentSession? opened = null;
        try
        {
            opened = OpenDocument(document, copy ? null : path, true, copy ? path : null);
            if (opened is null) return null;
            if (!copy && path is not null) opened.AttachFile(taken, entry.Info.Stamp);
            var failures = opened is ProjectSession project ? entry.ResultArchives.Select(project.Results.AddArchive).OfType<string>().ToArray() : [];
            entry.Delete();
            if (failures.Length > 0) services.Messages.Warn(RecoveryTitle, RecoveryResultsMissing(failures));
            return opened;
        }
        finally
        {
            if (opened is null) taken?.Dispose();
        }
    }

    /// <summary>Caption of the questions and messages of the recovery copies (step 2e).</summary>
    public const string RecoveryTitle = "Recupero dei documenti";

    /// <summary>The question at the start for a copy of <paramref name="title"/> written at <paramref name="written"/>.</summary>
    public static string RecoveryQuestion(string title, DateTimeOffset written) =>
        "ANTHEA non si è chiuso correttamente. Ci sono modifiche non salvate di «" + title + "», copiate alle " +
        written.ToLocalTime().ToString("HH:mm 'del' dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) + ".\n\n" +
        "Riprendi: riapre il documento con quelle modifiche, da salvare.\nScarta: elimina la copia.\nPiù tardi: la tiene per il prossimo avvio.";

    /// <summary>The warning when the result sets of the copy could not be read: their combinations are to be calculated again.</summary>
    public static string RecoveryResultsMissing(IReadOnlyList<string> failures) =>
        "Documento ripreso, ma una parte dei risultati dei modelli della copia non si è potuta leggere (" + string.Join("; ", failures) +
        "): le combinazioni interessate risultano da calcolare.";

    /// <summary>
    /// Opens a document read elsewhere (a copy of an example, the example of the Wiki): a calculation file in a new active tab, a project
    /// archive in place of the open project. Null when the user keeps the open project.
    /// </summary>
    public DocumentSession? OpenDocument(JsonObject document, string? path, bool dirty) => OpenDocument(document, path, dirty, null);

    private DocumentSession? OpenDocument(JsonObject document, string? path, bool dirty, string? resultsFrom)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(document);
        if (document.S("tipo") == "calcolo")
        {
            var calculation = new CalculationDocumentSession(document, path, dirty);
            AddCalculation(calculation);
            return calculation;
        }
        return OpenProject(document, path, dirty, resultsFrom);
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
    /// of a revision). An error is shown with the title «Salvataggio non riuscito». Step 2d (3/11/2026): a new file is locked first and a
    /// file open in another ANTHEA or in another tab is refused; the same file, changed on disk since it was opened or saved, asks first.
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
        DocumentLock? taken = null;
        try
        {
            if (files.IsInExamples(target)) throw new InvalidOperationException(ExamplesFolderRefused);
            string name = System.IO.Path.GetFileName(target);
            bool same = document.IsFile(target);
            if (!same)
            {
                if (Documents.Any(d => !ReferenceEquals(d, document) && d.IsFile(target))) throw new InvalidOperationException(LockedTarget(name, "in un'altra scheda di questa finestra"));
                var acquired = DocumentLock.Acquire(target);
                if (acquired.Holder is { } holder) throw new InvalidOperationException(LockedTarget(name, holder.Description));
                taken = acquired.Lock;
            }
            else if (document.Stamp is { } stamp && FileStamp.Of(target) is { } now && now != stamp
                && !services.Confirmations.ConfirmOverwrite(ChangedTitle, ChangedQuestion(name)))
                return false;
            // 2/11/2026: a project writes its results archive first, beside the file, then the document (ProjectFile, 3/11/2026: the result sets
            // the archive could not find are reported instead of ignored).
            string? warning = null;
            if (document is ProjectSession project) warning = ProjectFile.Save(target, document.WorkingDocument, project.Results, files.Save).Warning;
            else files.Save(target, document.WorkingDocument);
            document.MarkSaved(target);
            document.AttachFile(same ? document.Lock : taken, FileStamp.Of(target));
            taken = null;
            foreach (var sheet in sheets.Where(s => ReferenceEquals(s.Owner, document))) sheet.MarkSaved();
            if (warning is not null) services.Messages.Warn(SaveWarning, warning);
            return true;
        }
        catch (Exception error)
        {
            services.Messages.ReportFailure(SaveFailure, error);
            return false;
        }
        finally
        {
            taken?.Dispose();
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
        project.ReleaseFile();
        previewClosed.Clear();
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
            calculation.ReleaseFile();
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
        foreach (var document in Documents) document.ReleaseFile();
    }

    // ---- Internals

    private SheetSession AddCalculation(CalculationDocumentSession calculation)
    {
        CommitActive();
        calculations.Add(calculation);
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        return Add(new SheetSession(calculation, calculation.Sheet, factory, s => new Host(this, s)));
    }

    private ProjectSession? OpenProject(JsonObject document, string? path, bool dirty, string? resultsFrom)
    {
        if (!CloseProject()) return null;
        var project = new ProjectSession(document, path, dirty, services.Confirmations, CommitActive, resultsFrom);
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
            // F5.13 e: a sheet without a node in the archived revision on screen comes back with the current version (MainWindow keeps the sheet of the
            // navigation, revisionNavigationSheetId, and shows it again on the current version).
            if (change is ProjectChange.RevisionShown && project.IsPreviewingRevision && !previewClosed.Any(p => p.Id == sheet.Id))
                previewClosed.Add((sheet.Id, ReferenceEquals(sheet, Active)));
            Close(sheet, replaced ? SheetCloseReason.Missing : SheetCloseReason.Removed);
        }
        if (change is ProjectChange.RevisionShown or ProjectChange.RevisionDeleted or ProjectChange.PreviewExited && !project.IsPreviewingRevision)
            ReopenClosedByPreview(project, activate: change is not ProjectChange.PreviewExited, reloaded);
        if (reloaded.Count > 0) SheetsReloaded?.Invoke(this, new SheetsReloadedEventArgs(source, reloaded, change));
        if (missing.Count > 0) services.Messages.Inform("ANTHEA", string.Join("\n", missing.Select(MissingSheet)));
    }

    /// <summary>
    /// The current version is on screen again (F5.13 e, ui/shell-smoke-project-workspace): the sheets closed because the archived revision had no node with
    /// their identifier reopen on the current one, in their order; the sheet that had the focus takes it again after a switch of the revision bar (not when
    /// a page of the navigation ended the preview: the page stays). The others still missing are forgotten.
    /// </summary>
    private void ReopenClosedByPreview(ProjectSession project, bool activate, List<SheetSession> reopened)
    {
        if (previewClosed.Count == 0) return;
        var closed = previewClosed.ToArray();
        previewClosed.Clear();
        SheetSession? focus = null;
        foreach (var (id, active) in closed)
        {
            if (project.Find(id) is not { } node || !node.ContainsKey("modulo_id") || sheets.Any(s => ReferenceEquals(s.Sheet, node))) continue;
            var sheet = new SheetSession(project, node, factory, s => new Host(this, s));
            sheets.Add(sheet);
            SheetOpened?.Invoke(this, sheet);
            reopened.Add(sheet);
            if (active) focus = sheet;
        }
        if (activate && focus is not null && Active is null) SetActive(focus);
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
