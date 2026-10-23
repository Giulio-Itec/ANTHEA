using System.Text.Json.Nodes;
using System.Windows;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;

namespace X.Desktop;

/// <summary>
/// The sheet of a module still historic in the Release, in a tab of the new shell (F5.13 c, night of 22-23/10; risk R5, the adapter of proposal 5.2):
/// the historic <see cref="SheetEditor"/>, unchanged, behind the contract of the shell (<see cref="ISheetViewModel"/>), the reverse of
/// <see cref="MvvmSheetEditor"/>. The shell treats it as any sheet: it confirms it (Commit), reads its data, result and state, recreates it when its data
/// change outside it (SH17), asks for its reports; the view of the tab is the control of the editor (<see cref="View"/>, the template of the shell).
/// <list type="bullet">
/// <item>The events of the editor become the commands of <see cref="ISheetHost"/>, as MainWindow handles them (LoadSheetEditor): a copy in another
/// module, the section of a wall (OpenWallConcreteSection: a copy in the RC section), the replacement of the soil, the report; the sources of the soil
/// come from the host. Their errors go to the messages, as the Safe of MainWindow.</item>
/// <item>Read-only (an archived revision): <see cref="ISheetEditor.InspectRevision"/>, as MainWindow after opening the editor; a read-only sheet stays so
/// (the shell reopens the sheet when the preview ends).</item>
/// <item>Reports: the Word document of the editor and its short report (the RC section); the files are written by the editor itself
/// (DesktopSheetExports), as MainWindow does.</item>
/// </list>
/// </summary>
internal sealed class HistoricSheetViewModel : ISheetViewModel
{
    private readonly ISheetHost host;
    private readonly CommandGuard guard;
    private bool readOnly, disposed;

    /// <param name="editor">The historic editor, created on a copy of the data of the sheet.</param>
    /// <param name="host">The window side of the sheet (the coordinator of the shell).</param>
    /// <param name="readOnly">The sheet of an archived revision.</param>
    /// <param name="messages">Where the errors of the requests of the editor go.</param>
    internal HistoricSheetViewModel(ISheetEditor editor, ISheetHost host, bool readOnly, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(messages);
        Editor = editor; this.host = host;
        guard = new CommandGuard(messages);
        Report = new HistoricReport(editor);
        editor.Modified += OnModified;
        editor.ModuleCopyRequested += OnModuleCopy;
        editor.ConcreteSectionRequested += OnConcreteSection;
        editor.SoilReplacementRequested += OnSoilReplacement;
        editor.ReportRequested += OnReport;
        editor.SoilSources = () => host.SoilSources().Select(s => (s.Label, s.Module, s.Data, s.Id));
        if (readOnly) IsReadOnly = true;
    }

    /// <summary>The hosted historic editor.</summary>
    internal ISheetEditor Editor { get; }

    /// <summary>The control of the editor, shown in the tab (HistoricSheetHost).</summary>
    internal FrameworkElement View => Editor.View;

    public string ModuleId => Editor.Module;

    public JsonObject Data => Editor.Data;

    public JsonObject PreviewData => Editor.PreviewData;

    /// <summary>Grows at every edit of the editor (its Modified).</summary>
    public long Revision { get; private set; }

    public bool IsBusy => Editor.Busy;

    public bool HasResults => Editor.HasResults;

    public JsonObject? Result => Editor.Result;

    /// <summary>True makes the editor read-only (InspectRevision), once; a read-only historic sheet cannot become editable again.</summary>
    public bool IsReadOnly
    {
        get => readOnly;
        set
        {
            if (value == readOnly) return;
            if (!value) throw new InvalidOperationException("Un foglio storico in sola lettura resta in sola lettura: la shell lo riapre sulla versione corrente.");
            readOnly = true;
            Editor.InspectRevision();
        }
    }

    public IModuleReportSource Report { get; }

    public event EventHandler? Modified;

    public void Commit() => Editor.Commit();

    /// <summary>The «Calcola» of the editor on its data, without committing the drafts (the contract of the shell).</summary>
    public Task CalculateAsync(CancellationToken cancellation = default) => Editor.CalculateAsync(commitEdits: false);

    /// <summary>Not used by the shell, which recreates a sheet whose data change outside it (SH17), as MainWindow recreates its editor.</summary>
    public void Reload(JsonObject data) => throw new NotSupportedException("Il foglio storico non si ricarica: la shell lo riapre sui dati del documento (SH17).");

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Editor.Modified -= OnModified;
        Editor.ModuleCopyRequested -= OnModuleCopy;
        Editor.ConcreteSectionRequested -= OnConcreteSection;
        Editor.SoilReplacementRequested -= OnSoilReplacement;
        Editor.ReportRequested -= OnReport;
        Editor.SoilSources = null;
        Editor.Dispose();
    }

    private void OnModified()
    {
        Revision++;
        Modified?.Invoke(this, EventArgs.Empty);
    }

    private void OnModuleCopy(string module, JsonObject data, string name) => guard.Run(() => host.OpenModuleCopy(module, data, name));

    /// <summary>MainWindow.OpenWallConcreteSection: the section of a wall opens as a copy in the RC section.</summary>
    private void OnConcreteSection(JsonObject data, string name) => guard.Run(() => host.OpenModuleCopy("str_palo", data, name));

    private void OnSoilReplacement(JsonObject data) => guard.Run(() => host.ReplaceSoil(data));

    private void OnReport() => guard.Run(host.RequestReport);

    /// <summary>The Word document and the short report of the editor (the RC section; the others refuse it with the text of the editor).</summary>
    private sealed class HistoricReport(ISheetEditor editor) : IModuleReportSource, IModuleShortReport
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) => editor.BuildReport(title, options.ToHashSet(), projectReport);

        public byte[] BuildShortReport(string title) => editor.BuildShortReport(title);
    }
}
