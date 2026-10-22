using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>Kind of an open document: a single calculation file («calcolo») or a project archive («progetti»).</summary>
public enum DocumentKind
{
    Calculation,
    Project
}

/// <summary>
/// An open document of the shell (F5.13 a, proposal 3.5 and 3.6): its JSON, its file and the «not saved» state that MainWindow keeps
/// in the fields document, path and dirty. The JSON is changed only on the UI thread, by the sessions; the calculations work on
/// copies. Events of the instance, no static state (proposal 3.4).
/// </summary>
public abstract class DocumentSession
{
    private JsonObject document;

    protected DocumentSession(JsonObject document, string? path, bool dirty)
    {
        ArgumentNullException.ThrowIfNull(document);
        this.document = document;
        Path = path;
        IsDirty = dirty;
    }

    public abstract DocumentKind Kind { get; }

    /// <summary>The document on screen: for a project, the snapshot of an archived revision while it is shown.</summary>
    public JsonObject Document
    {
        get => document;
        protected set => document = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The document that «Salva» writes: the working document, never the snapshot of a revision (MainWindow.WorkingProjectDocument).</summary>
    public virtual JsonObject WorkingDocument => Document;

    /// <summary>File of the document, null for a new document or the copy of an example.</summary>
    public string? Path { get; private set; }

    /// <summary>True when the document has changes not saved (the « *» of the title).</summary>
    public bool IsDirty { get; private set; }

    /// <summary>True while the document cannot change (an archived revision of a project on screen).</summary>
    public virtual bool IsReadOnly => false;

    /// <summary>Number of the archived revision on screen, null for the current version.</summary>
    public virtual int? DisplayedRevision => null;

    /// <summary>Title of the window for this document (<see cref="ShellTexts.WindowTitle"/>).</summary>
    public string Title => ShellTexts.WindowTitle(Path, IsDirty, DisplayedRevision);

    /// <summary>The file, the «not saved» state, the read-only state or the document on screen changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// A change of the document (MainWindow.MarkDirty): nothing while read-only; otherwise the document is not saved any more and the
    /// project records the change in its history.
    /// </summary>
    public void MarkDirty()
    {
        if (IsReadOnly) return;
        IsDirty = true;
        OnMarkedDirty();
        RaiseStateChanged();
    }

    /// <summary>True when the document is the file at <paramref name="path"/>: full paths, case ignored (Windows paths).</summary>
    public bool IsFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Path is not null && string.Equals(System.IO.Path.GetFullPath(Path), System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The document was written to <paramref name="path"/>: it becomes its file and is saved.</summary>
    public void MarkSaved(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Path = path;
        IsDirty = false;
        RaiseStateChanged();
    }

    /// <summary>Sets the «not saved» state without recording a change (the restore of the history sets it as MainWindow does).</summary>
    protected void SetDirty(bool dirty)
    {
        IsDirty = dirty;
        RaiseStateChanged();
    }

    /// <summary>After <see cref="MarkDirty"/> changed the state: the project records the change in its history.</summary>
    protected virtual void OnMarkedDirty() { }

    protected void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A single calculation file (proposal 4.1): the document is its only sheet («tipo» = «calcolo», «modulo_id», «dati»), as the branch
/// of MainWindow that shows the document itself. No history: undo and redo belong to the projects, as today.
/// </summary>
public sealed class CalculationDocumentSession : DocumentSession
{
    /// <param name="document">A document of kind «calcolo».</param>
    /// <param name="path">Its file, null for a new document.</param>
    /// <param name="dirty">True for data not saved yet (a copy of an example or of another sheet).</param>
    public CalculationDocumentSession(JsonObject document, string? path = null, bool dirty = false) : base(document, path, dirty)
    {
        if (document.S("tipo") != "calcolo") throw new ArgumentException("Documento di calcolo atteso: tipo «" + document.S("tipo") + "».", nameof(document));
    }

    public override DocumentKind Kind => DocumentKind.Calculation;

    /// <summary>The sheet of the file: the document itself.</summary>
    public JsonObject Sheet => Document;

    /// <summary>Module of the sheet.</summary>
    public string Module => Document.S("modulo_id");

    /// <summary>A new file of <paramref name="module"/>, with the default data of the module (MainWindow.NewCalculation).</summary>
    public static CalculationDocumentSession New(string module) => new(DocumentService.CreateDocument(module));

    /// <summary>
    /// An unsaved file of <paramref name="module"/> holding a copy of <paramref name="data"/> named <paramref name="name"/>: the copy of a
    /// sheet in another module outside a project (MainWindow.OpenModuleCopy, today a second window), validated first.
    /// </summary>
    public static CalculationDocumentSession Copy(string module, JsonObject data, string name)
    {
        ArgumentNullException.ThrowIfNull(data);
        Anthea.Application.Modules.ModuleDirectory.ValidateData(module, data);
        var document = DocumentService.CreateDocument(module);
        document["dati"] = data.DeepClone(); document["nome"] = name;
        return new(document, null, dirty: true);
    }
}
