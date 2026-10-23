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

    /// <summary>
    /// MainWindow.dirty: «non salvato» of the document of the shell. Cleared by a suite (MainWindow then replaced its document without asking): from then on
    /// the questions about unsaved documents are answered «Non salvare».
    /// </summary>
    internal bool dirty
    {
        get => Bench.Workspace.CurrentDocument?.IsDirty ?? false;
        set { if (!value) Bench.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard; }
    }

    /// <summary>MainWindow.currentSheet: the node of the sheet with the focus (assigning it does nothing: the shell chooses the sheet).</summary>
    internal JsonObject? currentSheet { get => Bench.Workspace.Active?.Sheet; set { } }

    /// <summary>MainWindow.path: the file of the document of the shell.</summary>
    internal string? path => Bench.Workspace.CurrentDocument?.Path;

    /// <summary>
    /// MainWindow.editor: the sheet with the focus, the same object for the same tab. Set to null (MainWindow dropped its editor before replacing the
    /// document): the tab with the focus closes without saving.
    /// </summary>
    internal ISheetEditor? editor
    {
        // While a page is shown MainWindow kept its editor: the last sheet that had the focus («Riprendi»).
        get => (Bench.Workspace.Active ?? Bench.Shell.Navigation.Last) is { } sheet ? EditorOf(sheet) : null;
        set
        {
            if (value is not null) throw new InvalidOperationException("La shell sceglie il foglio: editor si assegna solo a null.");
            if ((Bench.Workspace.Active ?? Bench.Shell.Navigation.Last) is not { } sheet) return;
            // MainWindow dropped the editor without confirming it: the tab goes back to the data of its node, then closes (the shell confirms on closing).
            if (sheet.Project is not null && sheet.Sheet["dati"] is JsonObject data) sheet.ViewModel.Reload((JsonObject)data.DeepClone());
            var answer = Bench.Services.Confirmations.SaveChanges;
            Bench.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
            try { Bench.Workspace.CloseSheet(sheet); }
            finally { Bench.Services.Confirmations.SaveChanges = answer; }
            // As closing the second window of MainWindow: back to the sheet that remains, if any.
            if (Bench.Workspace.Active is null && Bench.Shell.Navigation.Last is not null) ResumeCalculation();
            UpdateLayout();
        }
    }

    /// <summary>MainWindow.body, moduleView, dashboardViewport: what the centre of the window shows, the sheet with the focus or a page.</summary>
    internal ShellBody body => new(this);
    internal static readonly object moduleView = new(), dashboardViewport = new();

    /// <summary>The centre of the shell, read as MainWindow.body.Content; set to moduleView it is «Riprendi».</summary>
    internal sealed class ShellBody(ShellSmokeWindow owner)
    {
        internal object Content
        {
            get => owner.Bench.Shell.Center is Anthea.Presentation.Shell.Tabs.SheetTabViewModel ? moduleView : dashboardViewport;
            set { if (ReferenceEquals(value, moduleView)) owner.ResumeCalculation(); else owner.ShowHome(); }
        }
    }

    /// <summary>MainWindow.dashboardBody: the page shown in the centre of the shell.</summary>
    internal FrameworkElement dashboardBody => (FrameworkElement)Window.FindName("CenterContent");

    /// <summary>The «Apri» or «Riprendi» button of the card of <paramref name="module"/> in «Moduli singoli» (MainWindow: the button of the module).</summary>
    internal System.Windows.Controls.Button ModuleButton(string module) => Ui.Descendants<System.Windows.Controls.Button>(Window)
        .Single(b => b.Name == "Open" && b.CommandParameter is ModuleCard card && card.Id == module && b.IsVisible);

    /// <summary>A button pressed as the user presses it (its click and its command).</summary>
    internal Task Press(System.Windows.Controls.Button button) => Bench.Press(button);

    /// <summary>MainWindow.Scripted: the scripted answers of the bench.</summary>
    internal ScriptedConfirmations Scripted => Bench.Services.Confirmations;

    /// <summary>MainWindow.CreateProjectSection: ProjectDocuments.AddSection, as MainWindow.</summary>
    internal static JsonObject CreateProjectSection(JsonObject parent, string name) => ProjectDocuments.AddSection(parent, name);

    /// <summary>MainWindow.InheritSectionData (MainWindow.Tests.cs): ProjectSharedData.InheritHierarchy.</summary>
    internal static void InheritSectionData(JsonObject sheet, JsonObject section) => ProjectSharedData.InheritHierarchy(sheet, section);

    /// <summary>MainWindow.MoveProjectSheet: the move of the project of the shell (ProjectSession.MoveSheet, with the questions of the shared data).</summary>
    internal void MoveProjectSheet(JsonObject sheet, JsonObject destination, JsonObject? anchor = null, bool after = false)
    {
        Bench.Project.MoveSheet(sheet, destination, anchor, after);
        UpdateLayout();
    }

    /// <summary>
    /// MainWindow.OpenModuleCopy and OpenWallConcreteSection: the copy in another module from the sheet with the focus, as its view asks the shell
    /// (ISheetHost.OpenModuleCopy): in a project a new sheet of the same section, outside a project a new file in a tab (MainWindow: a second window).
    /// </summary>
    internal void OpenModuleCopy(string module, JsonObject data, string name)
    {
        Bench.Workspace.OpenModuleCopy(Bench.Workspace.Active ?? throw new InvalidOperationException("Nessun foglio con il fuoco nella shell."), module, data, name);
        UpdateLayout();
    }
    internal void OpenWallConcreteSection(JsonObject data, string name) => OpenModuleCopy("str_palo", data, name);

    /// <summary>A node selected in the tree of the project, as the user selects it (the tree of the shell).</summary>
    internal void SelectInTree(JsonObject node) { Bench.Item(node).IsSelected = true; UpdateLayout(); }

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
    internal void ShowModules(string discipline = "Tutti")
    {
        OpenPending(); Bench.Shell.Navigation.Select(ShellPage.Modules); Bench.Shell.Navigation.Modules.Discipline = discipline; UpdateLayout();
    }

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
