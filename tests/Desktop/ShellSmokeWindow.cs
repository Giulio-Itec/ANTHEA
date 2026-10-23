using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Navigation;
using X.Core;

namespace X.Desktop;

/// <summary>
/// F5.13 g (UiTests): the container suites of MainWindow on the new shell. The suites passed to the shell are partials of this class in place of
/// MainWindow, with their steps and assertions; the members of MainWindow they used are here as the commands of the shell that the user presses, on the bench
/// of part (e) (<see cref="ShellSmokeBench"/>, off screen, never activated unless a suite asks for the keyboard focus):
/// <list type="bullet">
/// <item>the window (Width, Height, WindowState, Dispatcher, UpdateLayout, snapshots through <see cref="Window"/>) is the window of the shell;</item>
/// <item><c>document</c> assigned by a suite opens with the next ShowSheet or page (a calculation file in a tab, an archive as the project of the shell), as
/// MainWindow showed its document; read, it is the working document of the shell;</item>
/// <item><c>editor</c> is the sheet with the focus (<see cref="ShellMvvmEditor"/>, one per tab, so that «Riprendi» gives the same object), <c>openSheet</c>
/// its checks (<see cref="OpenSheet"/>);</item>
/// <item><c>dirty</c> is the state of the document of the shell; the suites that cleared it to close without questions get «Non salvare» at the end.</item>
/// </list>
/// </summary>
internal sealed partial class ShellSmokeWindow : IDisposable
{
    private readonly Dictionary<SheetSession, ShellMvvmEditor> editors = [];
    private JsonObject? pending;

    private ShellSmokeWindow(ShellSmokeBench bench) => Bench = bench;

    internal static async Task<ShellSmokeWindow> OpenAsync(string output) => new(await ShellSmokeBench.OpenAsync(output));

    internal ShellSmokeBench Bench { get; }

    /// <summary>The window of the shell, for the snapshots, the owners of the dialogs and the sizes.</summary>
    internal Window Window => Bench.Window;

    // ---------------------------------------------------------------- the window

    internal Dispatcher Dispatcher => Window.Dispatcher;
    internal void UpdateLayout() => Window.UpdateLayout();
    internal double Width { get => Window.Width; set => Window.Width = value; }
    internal double Height { get => Window.Height; set => Window.Height = value; }
    internal WindowState WindowState { get => Window.WindowState; set => Window.WindowState = value; }
    internal string Title => Window.Title;
    internal bool IsActive => Window.IsActive;

    /// <summary>MainWindow.Activate, for the suites that need the keyboard focus on a field (ui/smoke-bridge, CheckBridgeOptions): the window of the shell.</summary>
    internal bool Activate() => Window.Activate();

    // ---------------------------------------------------------------- document and sheet

    /// <summary>MainWindow.document: the document assigned by the suite until it opens; then the working document of the shell.</summary>
    internal JsonObject document
    {
        get => pending ?? Bench.Workspace.CurrentDocument?.WorkingDocument ?? throw new InvalidOperationException("Nessun documento aperto nella shell.");
        set => pending = value;
    }

    /// <summary>MainWindow.dirty: «non salvato» of the document of the shell (the suites that clear it get «Non salvare» when the window closes).</summary>
    internal bool dirty { get => Bench.Workspace.CurrentDocument?.IsDirty ?? false; set { } }

    /// <summary>MainWindow.currentSheet: the node of the sheet with the focus (assigning it does nothing: the shell chooses the sheet).</summary>
    internal JsonObject? currentSheet { get => Bench.Workspace.Active?.Sheet; set { } }

    /// <summary>MainWindow.path: the file of the document of the shell.</summary>
    internal string? path => Bench.Workspace.CurrentDocument?.Path;

    /// <summary>MainWindow.editor: the sheet with the focus, the same object for the same tab.</summary>
    internal ISheetEditor? editor => Bench.Workspace.Active is { } sheet ? EditorOf(sheet) : null;

    /// <summary>MainWindow.openSheet: the checks of the sheet with the focus.</summary>
    internal OpenSheet openSheet => new(editor ?? throw new InvalidOperationException("Nessun foglio con il fuoco nella shell."), Bench.Services);

    /// <summary>MainWindow.sheetEditors of the WPF checks: the factory of the bench.</summary>
    internal SheetEditorFactory sheetEditors { get; } = ModuleHostBench.SheetEditors();

    private ShellMvvmEditor EditorOf(SheetSession sheet)
    {
        foreach (var closed in editors.Keys.Where(s => s.IsDisposed).ToArray()) editors.Remove(closed);
        if (!editors.TryGetValue(sheet, out var editor)) editors[sheet] = editor = new ShellMvvmEditor(new ShellSheet(Bench, sheet).Hosted);
        return editor;
    }

    /// <summary>The document assigned by the suite, opened in the shell as MainWindow showed it.</summary>
    private void OpenPending()
    {
        if (pending is not { } next) return;
        pending = null;
        if (!ReferenceEquals(Bench.Workspace.CurrentDocument?.WorkingDocument, next)) Bench.Workspace.OpenDocument(next, null, false);
        UpdateLayout();
    }

    /// <summary>MainWindow.ShowSheet: the calculation file in its tab, or the sheet of the project in a tab.</summary>
    internal void ShowSheet(JsonObject sheet)
    {
        bool own = ReferenceEquals(sheet, pending);
        OpenPending();
        if (!own && Bench.Workspace.Project is { } project && project.Contains(sheet)) Bench.Workspace.OpenSheet(sheet);
        UpdateLayout();
    }

    /// <summary>MainWindow.Commit: the sheet with the focus confirmed into its document.</summary>
    internal void Commit() => Bench.Workspace.CommitActive();

    /// <summary>MainWindow.ShowHome, ShowModules, ShowProjects, ShowWiki: the pages of the navigation.</summary>
    internal void ShowHome() { OpenPending(); Bench.Shell.Navigation.Select(ShellPage.Home); UpdateLayout(); }
    internal void ShowProjects() { OpenPending(); Bench.Shell.Navigation.Select(ShellPage.Projects); UpdateLayout(); }
    internal void ShowModules() { OpenPending(); Bench.Shell.Navigation.Select(ShellPage.Modules); UpdateLayout(); }

    /// <summary>MainWindow.ResumeCalculation: «Riprendi» of the home page.</summary>
    internal void ResumeCalculation() { Bench.Shell.Navigation.ResumeCommand.Execute(null); UpdateLayout(); }

    /// <summary>MainWindow.OpenModule: «Riprendi» on the module of the sheet with the focus, otherwise a new file of the module (ShellViewModel.OpenModule).</summary>
    internal void OpenModule(string module) { OpenPending(); Bench.Shell.OpenModule(module); UpdateLayout(); }

    /// <summary>MainWindow.NewCalculation: a new file of the module in a tab.</summary>
    internal void NewCalculation(string module) { OpenPending(); Bench.Workspace.NewCalculation(module); UpdateLayout(); }

    /// <summary>The ViewModel of a sheet of <c>editor</c> (the tab of the shell), as the suites read MvvmSheetEditor.Sheet.</summary>
    internal static Anthea.Presentation.Modules.ISheetViewModel? SheetOf(ISheetEditor? sheet) => sheet switch
    {
        MvvmSheetEditor mvvm => mvvm.Sheet,
        ShellMvvmEditor tab => tab.Hosted.Sheet,
        _ => null
    };

    /// <summary>A suite of this class on a new bench, closed at the end without questions.</summary>
    internal static async Task Run(string output, Func<ShellSmokeWindow, Task> suite)
    {
        using var window = await OpenAsync(output);
        await suite(window);
    }

    /// <summary>MainWindow.LoadFile: the opening of a file, as from Start or Explorer.</summary>
    internal void LoadFile(string file) { pending = null; Bench.Workspace.OpenFile(file); UpdateLayout(); }

    /// <summary>MainWindow.GenerateSectionReport: the writer of the report of a section of the shell (the same call, report code unchanged).</summary>
    internal static Task<int> GenerateSectionReport(JsonObject snapshot, string file, Action<string> progress, CancellationToken cancellation) =>
        new DesktopSectionReports().WriteAsync(snapshot, file, progress, cancellation);

    /// <summary>MainWindow.FinishSmoke: no question when the window closes.</summary>
    internal void FinishSmoke() => Bench.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;

    public void Dispose()
    {
        FinishSmoke();
        Bench.Dispose();
    }
}
