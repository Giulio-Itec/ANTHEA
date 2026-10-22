using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Modules;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>
/// A sheet open in a tab of the shell (F5.13 a, proposal 3.5 and 3.6): the node of the sheet in its document, the ViewModel that edits a
/// copy of its data and the baseline of the shared data, as MainWindow keeps them for its only sheet (currentSheet, editor,
/// sharedBaseline). <see cref="Commit"/> is the sequence of MainWindow.Commit; the coordinator of the documents reopens the sheet when its
/// data change outside it (shared data, undo, revisions), as MainWindow recreates its editor.
/// </summary>
public sealed class SheetSession : IDisposable
{
    private readonly ISheetFactory factory;
    private readonly ISheetHost host;
    private JsonObject? baseline;
    private JsonNode? loaded;
    private JsonNode? saved;
    private bool disposed;

    internal SheetSession(DocumentSession owner, JsonObject sheet, ISheetFactory factory, Func<SheetSession, ISheetHost> host)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(host);
        Owner = owner; Sheet = sheet; this.factory = factory; this.host = host(this);
        Module = sheet.S("modulo_id");
        saved = sheet["dati"]?.DeepClone();
        ViewModel = Create();
    }

    /// <summary>The document of the sheet.</summary>
    public DocumentSession Owner { get; }

    /// <summary>The project of the sheet, null for a single calculation file.</summary>
    public ProjectSession? Project => Owner as ProjectSession;

    /// <summary>The node of the sheet in the document on screen (for a single file, the document itself).</summary>
    public JsonObject Sheet { get; private set; }

    /// <summary>Identifier of the sheet in its project; empty for a single file.</summary>
    public string Id => Sheet.S("id");

    public string Module { get; }

    /// <summary>The ViewModel of the sheet; replaced when the sheet reopens (<see cref="ViewModelReplaced"/>).</summary>
    public ISheetViewModel ViewModel { get; private set; }

    /// <summary>Read-only: the sheet of an archived revision.</summary>
    public bool IsReadOnly => Owner.IsReadOnly;

    /// <summary>Heading of the tab and of the sheet bar (<see cref="ShellTexts.SheetHeading"/>).</summary>
    public string Heading => ShellTexts.SheetHeading(Sheet, Owner.DisplayedRevision);

    /// <summary>
    /// The confirmed data differ from those at the opening or at the last save (proposal 3.6, second level: the «•» of the tab and of the
    /// tree); computed at the confirmation and at the save, not at every key.
    /// </summary>
    public bool IsUnsaved { get; private set; }

    /// <summary>The baseline of the shared data: the sheet at its last confirmation (MainWindow.sharedBaseline).</summary>
    public JsonObject? Baseline => baseline;

    public bool IsDisposed => disposed;

    /// <summary>The ViewModel was recreated: the tab shows the new one (the old one is disposed).</summary>
    public event EventHandler? ViewModelReplaced;

    /// <summary>Heading, read-only or «not saved» state changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Confirms the sheet into its document (MainWindow.Commit): nothing while read-only; the drafts of the fields are written, the
    /// proposal (the sheet with the data of the ViewModel) goes through the confirmation of the shared data of the project, the data are
    /// written into the document, the proposal becomes the baseline and the history records the change. When the update of the linked sheets
    /// fails, the data of every sheet of the section are restored before the error goes on (deferred from F5.2 to F5.13; the historic
    /// window restores only the open sheet).
    /// </summary>
    public void Commit()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsReadOnly) return;
        ViewModel.Commit();
        var proposed = (JsonObject)Sheet.DeepClone(); proposed["dati"] = ViewModel.Data.DeepClone();
        if (Project is { } project)
        {
            var section = Sheet.Parent?.Parent as JsonObject;
            var before = section is null ? null : ProjectSharedData.SubtreeSheets(section).Select(s => (Sheet: s, Data: s["dati"]?.DeepClone())).ToArray();
            try { project.Shared.ConfirmSharedChanges(project.Document, Sheet, baseline, proposed, project.MarkDirty); }
            catch
            {
                foreach (var (sheet, data) in before ?? []) sheet["dati"] = data;
                throw;
            }
        }
        Sheet["dati"] = ViewModel.Data.DeepClone();
        baseline = (JsonObject)proposed.DeepClone();
        loaded = Sheet["dati"]!.DeepClone();
        (Owner as ProjectSession)?.Record();
        UpdateUnsaved();
    }

    /// <summary>The document was saved: the data confirmed now are the saved ones.</summary>
    internal void MarkSaved()
    {
        saved = Sheet["dati"]?.DeepClone();
        UpdateUnsaved();
    }

    /// <summary>True when the data of the sheet in the document differ from those the ViewModel last loaded or confirmed.</summary>
    internal bool ChangedOutside => !JsonNode.DeepEquals(Sheet["dati"], loaded);

    /// <summary>
    /// Reopens the sheet on <paramref name="sheet"/> (the same node or the node with the same id in a restored document): a new ViewModel
    /// on its data, read-only as the document, as MainWindow.LoadSheetEditor recreates its editor. Pending drafts are lost, as there.
    /// </summary>
    internal void Reopen(JsonObject? sheet = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sheet is not null && sheet.S("modulo_id") != Module) throw new ArgumentException("Foglio di un altro modulo: " + sheet.S("modulo_id"), nameof(sheet));
        bool sameNode = sheet is null || ReferenceEquals(sheet, Sheet);
        Sheet = sheet ?? Sheet;
        if (!sameNode) saved = Sheet["dati"]?.DeepClone();
        var previous = ViewModel;
        ViewModel = Create();
        previous.Dispose();
        ViewModelReplaced?.Invoke(this, EventArgs.Empty);
        UpdateUnsaved(force: true);
    }

    /// <summary>Heading or read-only state may have changed (rename, revision): the tab refreshes.</summary>
    internal void Refresh() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ViewModel.Dispose();
    }

    /// <summary>MainWindow.LoadSheetEditor: the data of the sheet or, for a sheet without data, the defaults of the module; the baseline.</summary>
    private ISheetViewModel Create()
    {
        var data = Sheet["dati"] as JsonObject ?? DocumentService.CreateSheet(Module);
        var sheet = factory.Open(Module, data, Owner.IsReadOnly, host);
        sheet.Modified += (_, _) => Owner.MarkDirty();
        baseline = (JsonObject)Sheet.DeepClone(); baseline["dati"] = sheet.Data.DeepClone();
        loaded = Sheet["dati"]?.DeepClone();
        return sheet;
    }

    private void UpdateUnsaved(bool force = false)
    {
        bool unsaved = !JsonNode.DeepEquals(Sheet["dati"], saved);
        if (unsaved == IsUnsaved && !force) return;
        IsUnsaved = unsaved;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
