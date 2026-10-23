using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.Shell;

namespace X.Desktop;

/// <summary>
/// --check-shell-smoke-&lt;suite&gt; &lt;cartella&gt; [--module-impl modulo=mvvm|historic …] (UiTests, F5.13 e): the suites of MainWindow transferred to the new
/// shell (docs/refactoring/f5-13-shell.md, «Parte (e)»). The same scenarios and the same assertions of ui/smoke-projects, ui/smoke-project-workspace,
/// ui/smoke-hierarchy, ui/smoke-sharing, ui/smoke-project-report and ui/smoke-display, driven through the new shell: ShellWindow on ShellViewModel from its
/// composition root (ShellComposition), off screen and never activated, with the modules opened as the Release chooses them (the historic ones in their
/// SheetEditor, R5) and the bench pairs <c>--module-impl</c> of the suites <c>-mvvm</c>. Where the historic suite called a method of MainWindow, this one
/// uses the control or the command of the shell that the user presses (tree, overview, revision bar, sheet bar, menu); where it called the code of the
/// application directly (ProjectSharedData, DocumentService…), the same call. The answers of the questions and of the dialogs are scripted through
/// <see cref="BenchSheetServices"/>, the services of the sheets of the bench, as TestServices scripts those of MainWindow.
/// Writes the PNG of the historic suite, smoke.txt, test.txt and esito.txt («PASS n controlli»).
/// </summary>
internal static partial class ShellSmoke
{
    /// <summary>The flag of the suites; the rest is the name of the suite.</summary>
    internal const string Prefix = "--check-shell-smoke-";

    internal static async Task Run(string flag, string output)
    {
        string suite = flag[Prefix.Length..];
        Directory.CreateDirectory(output);
        // As StartSmoke: no tooltip popups in the harness.
        EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler((_, args) => args.Handled = true));
        var bench = await ShellSmokeBench.OpenAsync(output);
        try
        {
            switch (suite)
            {
                case "projects": await Projects(bench); break;
                case "project-workspace": await ProjectWorkspace(bench); break;
                case "hierarchy": await Hierarchy(bench); break;
                case "sharing": await Sharing(bench); break;
                case "project-report": await ProjectReport(bench); break;
                case "display": await Display(bench); break;
                default: throw new ArgumentException("Suite della shell sconosciuta: " + suite);
            }
            bench.Pass(suite);
        }
        finally
        {
            bench.WriteLog();
            bench.Dispose();
        }
    }
}

/// <summary>The new shell of the suites ui/shell-smoke-* (<see cref="ShellSmoke"/>): the window off screen, its ViewModel, the scripted services and the helpers.</summary>
internal sealed class ShellSmokeBench : IDisposable
{
    private readonly List<string> log = [];

    private ShellSmokeBench(string output, BenchSheetServices services, PresentationServices presentation, ShellParts parts, List<QuietShellWindow> detached)
    {
        Output = output; Services = services; Presentation = presentation; Shell = parts.Shell; Window = parts.Window; Detached = detached;
    }

    internal string Output { get; }

    /// <summary>The scripted answers: questions (Confirmations), dialogs (Dialog), save dialogs (SaveFile); the messages in Messages.Shown.</summary>
    internal BenchSheetServices Services { get; }
    internal PresentationServices Presentation { get; }
    internal ShellViewModel Shell { get; }
    internal ShellWindow Window { get; }
    internal List<QuietShellWindow> Detached { get; }
    internal WorkspaceSession Workspace => Shell.Workspace;
    internal ProjectSession Project => Workspace.Project ?? throw new InvalidOperationException("Nessun archivio progetti aperto nella shell.");
    internal TreeView Tree => (TreeView)Window.FindName("Tree");

    /// <summary>The shell from its composition root with the modules of the product, as the Release chooses them with the bench pairs of the command line.</summary>
    internal static async Task<ShellSmokeBench> OpenAsync(string output)
    {
        var services = new BenchSheetServices();
        var presentation = ShellComposition.WithShellWiki(services.Create());
        var detached = new List<QuietShellWindow>();
        var parts = ShellComposition.Create(presentation, DesktopComposition.CreateModules(), new CalculationLimiter(CalculationLimiter.DefaultMaximum), ShellComposition.ExamplesFolder,
            window => { var quiet = new QuietShellWindow(Offscreen(window)); detached.Add(quiet); return quiet; }, ModuleHostBench.Implementations(Environment.GetCommandLineArgs()));
        var bench = new ShellSmokeBench(output, services, presentation, parts, detached);
        Offscreen(parts.Window).Show();
        await bench.Idle();
        return bench;
    }

    internal void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        log.Add("OK " + message);
    }

    /// <summary>A note of the suite (a declared difference, a value read), in test.txt, not counted.</summary>
    internal void Note(string text) => log.Add("NOTA " + text);

    internal void Pass(string suite) =>
        File.WriteAllText(Path.Combine(Output, "esito.txt"), $"PASS {log.Count(l => l.StartsWith("OK ", StringComparison.Ordinal))} controlli della suite {suite} sulla shell nuova (F5.13 e)\n", new UTF8Encoding(false));

    internal void WriteLog() => File.WriteAllLines(Path.Combine(Output, "test.txt"), log, new UTF8Encoding(false));

    internal async Task Idle()
    {
        Window.UpdateLayout();
        await Window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await Window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        Window.UpdateLayout();
    }

    /// <summary>The main window as Ui.Snapshot draws MainWindow in the historic suites.</summary>
    internal async Task Capture(string name)
    {
        await Idle();
        File.WriteAllBytes(Path.Combine(Output, name), Ui.Snapshot(Window));
    }

    /// <summary>A dialog of the shell drawn as WpfDialogService shows it (a window around the ViewModel, its view from the resources of the application), off screen.</summary>
    internal async Task CaptureDialog(object dialog, string title, string name, Action<Window>? inspect = null)
    {
        var window = Offscreen(new Window { Title = title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight });
        window.Show();
        try
        {
            window.UpdateLayout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            File.WriteAllBytes(Path.Combine(Output, name), Ui.Snapshot(window));
            inspect?.Invoke(window);
        }
        finally { window.Content = null; window.Close(); }
    }

    /// <summary>The node of <paramref name="node"/> in the tree of the shell.</summary>
    internal TreeViewItem Item(JsonObject node) =>
        Descendants<TreeViewItem>(Window).SingleOrDefault(i => i.DataContext is ProjectNodeViewModel { Node: var value } && ReferenceEquals(value, node))
        ?? throw new InvalidOperationException("Nodo non trovato nell'albero della shell: " + node.S("nome"));

    internal ProjectNodeViewModel Node(JsonObject node) => (ProjectNodeViewModel)Item(node).DataContext;

    /// <summary>The name of a node in the tree.</summary>
    internal TextBlock Label(JsonObject node)
    {
        var item = Item(node);
        return Descendants<TextBlock>(item).First(t => t.Name == "Label" && ReferenceEquals(t.DataContext, item.DataContext));
    }

    /// <summary>The box of the rename in place of a node, when visible.</summary>
    internal TextBox? RenameBox(JsonObject node)
    {
        var item = Item(node);
        return Descendants<TextBox>(item).SingleOrDefault(b => b.Name == "Rename" && ReferenceEquals(b.DataContext, item.DataContext) && b.IsVisible);
    }

    /// <summary>The button with the icon of a sheet in the tree («Apri scheda nome»).</summary>
    internal Button OpenButton(JsonObject sheet) => Descendants<Button>(Tree).Single(b => AutomationProperties.GetName(b) == "Apri scheda " + sheet.S("nome"));

    /// <summary>
    /// The context menu of a node, with the data of its header as when it opens (the menu takes the DataContext of the element it opens on; its bindings,
    /// as the visibility of the items, are evaluated then). Not opened: no popup off screen.
    /// </summary>
    internal ContextMenu Menu(JsonObject node)
    {
        var item = Item(node);
        var header = Descendants<DockPanel>(item).First(d => d.ContextMenu is not null && ReferenceEquals(d.DataContext, item.DataContext));
        var menu = header.ContextMenu!;
        menu.PlacementTarget = header;
        menu.DataContext = header.DataContext;
        menu.UpdateLayout();
        return menu;
    }

    /// <summary>
    /// A drag event on the node of <paramref name="target"/> at <paramref name="relativeY"/> of the height of its row (0 top, 1 bottom), raised as the
    /// historic suites raise them on the TreeViewItem of MainWindow: the handlers of the view (ProjectTreeInput) take it.
    /// </summary>
    internal DragEventArgs Drag(JsonObject target, string format, object payload, RoutedEvent routedEvent, double relativeY = .5)
    {
        var item = Item(target);
        var row = ProjectTreeInput.Row(item);
        var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [new DataObject(format, payload), DragDropKeyStates.None, DragDropEffects.Copy | DragDropEffects.Move, row, new Point(8, Math.Max(1, row.ActualHeight * relativeY))], null)!;
        args.RoutedEvent = routedEvent;
        item.RaiseEvent(args);
        return args;
    }

    /// <summary>A click of <paramref name="button"/> through its automation peer (no mouse, no focus), then the dispatcher idle; a disabled button fails.</summary>
    internal async Task Press(Button button)
    {
        if (!button.IsEnabled) throw new InvalidOperationException("Pulsante disabilitato: " + (button.Content ?? AutomationProperties.GetName(button)));
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        await Idle();
    }

    /// <summary>The button of the main window bound to <paramref name="command"/>, visible.</summary>
    internal Button Button(System.Windows.Input.ICommand command) => Descendants<Button>(Window).First(b => ReferenceEquals(b.Command, command) && b.IsVisible);

    /// <summary>A mouse button event of the left button with <paramref name="clicks"/> clicks raised on <paramref name="element"/> (no mouse).</summary>
    internal static void Mouse(UIElement element, RoutedEvent routedEvent, int clicks = 1)
    {
        var args = new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = routedEvent };
        typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!.SetValue(args, clicks);
        element.RaiseEvent(args);
    }

    /// <summary>The double click of <paramref name="element"/>: the command of its «LeftDoubleClick» mouse binding (the view binds the gesture to the ViewModel).</summary>
    internal void DoubleClick(UIElement element)
    {
        var binding = element.InputBindings.OfType<MouseBinding>().Single(m => m.Gesture is MouseGesture { MouseAction: MouseAction.LeftDoubleClick });
        if (!binding.Command.CanExecute(binding.CommandParameter)) throw new InvalidOperationException("Doppio clic non eseguibile");
        binding.Command.Execute(binding.CommandParameter);
    }

    /// <summary>A key pressed on <paramref name="element"/> (no keyboard).</summary>
    internal void Key(UIElement element, Key key) =>
        element.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(Window), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });

    /// <summary>The element that shows the view of an MVVM sheet in the shell (the presenter of the tab).</summary>
    internal FrameworkElement ViewOf(ISheetViewModel sheet) =>
        Descendants<ContentPresenter>(Window).FirstOrDefault(p => ReferenceEquals(p.Content, sheet))
        ?? Detached.SelectMany(d => Descendants<ContentPresenter>(d.Window)).FirstOrDefault(p => ReferenceEquals(p.Content, sheet))
        ?? throw new InvalidOperationException("Vista del foglio non a schermo: " + sheet.ModuleId);

    /// <summary>The sheet with the focus.</summary>
    internal ShellSheet Sheet => new(this, Workspace.Active ?? throw new InvalidOperationException("Nessun foglio con il fuoco nella shell."));

    /// <summary>The sheet of a node of the project, open in its tab.</summary>
    internal ShellSheet SheetOf(JsonObject node) => new(this, Workspace.SheetOf(node) ?? throw new InvalidOperationException("Foglio non aperto: " + node.S("nome")));

    /// <summary>The window of the main window by reflection: Window.OnDeactivated (the user goes to another window), as MainWindow.OnDeactivated in the historic suite.</summary>
    internal void Deactivate() => typeof(Window).GetMethod("OnDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Window, [EventArgs.Empty]);

    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject => Ui.Descendants<T>(root);

    internal static T Offscreen<T>(T window) where T : Window
    {
        window.Left = -32000; window.Top = -32000;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowActivated = false; window.ShowInTaskbar = false;
        return window;
    }

    public void Dispose()
    {
        Window.Close();
        Shell.Dispose();
    }
}

/// <summary>
/// A sheet of the shell as the suites drive it: the XAML view of its module (until F5.13 g also the historic SheetEditor of the ospite R5, archived in
/// supporto/SUPERATI/ui-storica/shell).
/// </summary>
internal sealed class ShellSheet(ShellSmokeBench bench, SheetSession session)
{
    internal SheetSession Session { get; } = session;
    internal ISheetViewModel ViewModel => Session.ViewModel;

    /// <summary>The XAML view of the sheet in its tab.</summary>
    internal HostedSheet Hosted => new(ViewModel, () => bench.ViewOf(ViewModel));

    internal JsonObject Data => ViewModel.Data;

    /// <summary>The control that shows the sheet: the presenter of the XAML view.</summary>
    internal FrameworkElement View => bench.ViewOf(ViewModel);

    /// <summary>SetGeometryForSmoke: the diameter of a vertical pile as the user types it.</summary>
    internal void SetGeometry(string value) => new VerticalPileSmoke(Hosted, bench.Services).SetGeometry(value);

    /// <summary>The state of the concrete set through the controls of its XAML view (the only one since the Release switch of the materials).</summary>
    internal void SetConcreteState(JsonObject state) => new ConcreteMaterialSmoke(Hosted).Apply(state);

    /// <summary>The user types in a field of the concrete (before the commit: the preview of the sheet follows).</summary>
    internal void TypeConcreteNumber(string key, string text) => new ConcreteMaterialSmoke(Hosted).Type(key, text);

    /// <summary>The type of the reinforcing steel, chosen as the user chooses it.</summary>
    internal SteelSmoke Steel => new(new ShellMvvmEditor(Hosted));

    /// <summary>WaitForAutomatic of the sheet.</summary>
    internal Task WaitForAutomatic() => MvvmSheets.WaitForAutomatic(Hosted);
}

/// <summary>
/// The minimum ISheetEditor that SteelSmoke reads from a sheet with the XAML view in the shell (its view and its data): nothing else is used.
/// </summary>
internal sealed class ShellMvvmEditor(HostedSheet sheet) : ISheetEditor
{
    public string Module => sheet.Module;
    public JsonObject Data => sheet.Data;
    public JsonObject PreviewData => sheet.Sheet.PreviewData;
    public JsonObject? Result => sheet.Result;
    public bool HasResults => sheet.Sheet.HasResults;
    public bool Busy => sheet.Busy;
    public FrameworkElement View => sheet.View;
    public event Action? Modified { add { } remove { } }
    public event Action<JsonObject, string>? ConcreteSectionRequested { add { } remove { } }
    public event Action? ReportRequested { add { } remove { } }
    public event Action<string, JsonObject, string>? ModuleCopyRequested { add { } remove { } }
    public event Action<JsonObject>? SoilReplacementRequested { add { } remove { } }
    public Func<IEnumerable<(string Label, string Module, JsonObject Data, string Id)>>? SoilSources { get; set; }
    public void Commit() => sheet.Sheet.Commit();
    public Task CalculateAsync(bool commitEdits = true) => sheet.CalculateAsync();
    public void InspectRevision() => sheet.Sheet.IsReadOnly = true;
    public void ExportResult(string filename) => throw new NotSupportedException();
    public void ExportReport(string filename, string title, HashSet<string> options, bool projectReport = false) => throw new NotSupportedException();
    public byte[] BuildReport(string title, HashSet<string> options, bool projectReport = false) => throw new NotSupportedException();
    public byte[] BuildShortReport(string title) => throw new NotSupportedException();
    public ReportProject.SheetContent MaterialReportContent() => throw new NotSupportedException();
    public void Dispose() { }
}

/// <summary>A detached window of the shell as the tracker sees it, never activated (the checks of the night share the desktop), as in ShellDraftChecks.</summary>
internal sealed class QuietShellWindow : IShellWindow
{
    internal QuietShellWindow(DetachedShellWindow window)
    {
        Window = window;
        Window.Closed += (_, _) => IsActive = false;
    }

    internal DetachedShellWindow Window { get; }
    public bool IsActive { get; private set; }
    public void Show() => Window.Show();

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        Activated?.Invoke(this, EventArgs.Empty);
    }

    public void Close() => Window.Close();
    public event EventHandler? Activated;
    public event EventHandler? Closed { add => Window.Closed += value; remove => Window.Closed -= value; }
}
