using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;
using X.Desktop.Views.ConcreteSection;

namespace X.Desktop;

/// <summary>
/// Passaggio della Release della sezione c.a. (23/10/2026): the RC section as the UI suites drive it, on the XAML view
/// (<see cref="ConcreteSectionSheetView"/> hosted by <see cref="MvvmSheetEditor"/>), in place of the historic ConcreteWorkspace (archived in
/// supporto/SUPERATI/ui-storica/sezione-ca). The checks of the historic partials (ConcreteSmokeChecks.VerifyWorkspace, ConcreteExtensionSmokeChecks,
/// ConcreteFeatureChecks, SectionActionsSmokeChecks, the c.a. part of NeutralAxisSmokeChecks, ConcreteDesignDesktopChecks, ValidationCaptureChecks,
/// F3ConcreteFigureChecks, ConcreteTemplateViewChecks, the c.a. part of ReportAppearanceChecks) keep their messages and are done here on the view
/// and its ViewModel:
/// <list type="bullet">
/// <item>what the historic checks did on the controls (typing with a focus change, choices of combo boxes, buttons, the grid in edit, the tabs, the
/// windows of the view, the drawings) is done on the controls of the view, found by their automation names;</item>
/// <item>what they wrote through InputForm.Set is written through <see cref="FormViewModel.Set"/> (the same write and the same callbacks), the rows
/// of the tables through their cells (<see cref="JsonRowViewModel"/>) and their commands;</item>
/// <item>the results are read from the export of the sheet (<see cref="ConcreteSheetViewModel.Result"/>, the historic contract) and from the
/// panels;</item>
/// <item>the checks on the native objects kept in cache (identity of domains, meshes, stress states: question CA-P1, decided provisionally by the
/// coordinator on 23/10/2026) are done on the observable effects: the checks queued after the change (<see cref="ConcreteCalculation.Pending"/>,
/// read at once, before the queued recalculation runs), the result kept or invalidated, the values of the checks not recalculated equal to those
/// before, the status lines of the calculation (the single preparation of the section);</item>
/// <item>where the historic checks wrote the document directly (an incomplete cable), the data go to the sheet with
/// <see cref="Anthea.Presentation.Modules.SheetViewModelBase.Reload"/> (question P1 of docs/refactoring/f5-suite-passaggi.md).</item>
/// </list>
/// The dialogs of the sheet are shown as WpfDialogService shows them, off screen, and driven through their controls (<see cref="BenchSheetServices.Dialog"/>);
/// the windows of the view («Espandi», the properties) off screen, without waiting.
/// </summary>
internal sealed partial class ConcreteSectionSmoke : IDisposable
{

    private readonly List<Window> windows = [];
    private readonly Window? host;
    private readonly bool ownsEditor;

    internal ConcreteSectionSmoke(MvvmSheetEditor editor, BenchSheetServices services) : this(editor, services, null, false) { }

    private ConcreteSectionSmoke(MvvmSheetEditor editor, BenchSheetServices services, Window? host, bool ownsEditor)
    {
        Editor = editor; Services = services; this.host = host; this.ownsEditor = ownsEditor;
        Sheet = (ConcreteSheetViewModel)editor.Sheet;
        services.Clipboard ??= new ScriptedClipboard();
        if (Find<ConcreteSectionSheetView>(editor.View, _ => true, realize: false) is { } view)
        {
            view.ShowWindow = w => { Away(w); windows.Add(w); w.Show(); w.UpdateLayout(); return null; };
            view.ShowModeless = w => { Away(w); windows.Add(w); w.Show(); w.UpdateLayout(); };
            view.ChooseImagePath = _ => null;
        }
    }

    /// <summary>The open sheet is the RC section with the XAML view.</summary>
    internal static bool Handles(ISheetEditor? editor) => editor is MvvmSheetEditor { Sheet: ConcreteSheetViewModel };

    /// <summary>The editors of the bench (the Release: the RC section with the XAML view) and their scriptable services.</summary>
    internal static (SheetEditorFactory Factory, BenchSheetServices Services) Editors()
    {
        var factory = ModuleHostBench.SheetEditors([]);
        return (factory, ModuleHostBench.ScriptedServices(factory)!);
    }

    /// <summary>
    /// A sheet of the RC section opened by the editors of the bench in a window of its own, off screen (never activated), <paramref name="width"/> ×
    /// <paramref name="height"/>; the window and the editor are closed with <see cref="Dispose"/>.
    /// </summary>
    internal static ConcreteSectionSmoke Open(JsonObject data, double width = 1400, double height = 1000, string title = "Sezione c.a.")
    {
        var (factory, services) = Editors();
        var editor = (MvvmSheetEditor)factory.Open(ConcreteSectionSheetModule.Id, data);
        var window = new Window
        {
            Title = title, Content = editor.View, Width = width, Height = height, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false
        };
        Appearance.Watch(window);
        window.Show(); window.UpdateLayout();
        return new ConcreteSectionSmoke(editor, services, window, true);
    }

    /// <summary>A sheet of the RC section opened by the editors of the bench without a window (the view laid out by the caller); disposed with <see cref="Dispose"/>.</summary>
    internal static ConcreteSectionSmoke OpenDetached(JsonObject data)
    {
        var (factory, services) = Editors();
        return new ConcreteSectionSmoke((MvvmSheetEditor)factory.Open(ConcreteSectionSheetModule.Id, data), services, null, true);
    }

    /// <summary>A second sheet on <paramref name="data"/> (the reopening of the historic checks: <c>new ConcreteWorkspace(copy)</c>), without a window.</summary>
    internal static ConcreteSheetViewModel Reopen(JsonObject data, out IDisposable editor)
    {
        var mvvm = (MvvmSheetEditor)Editors().Factory.Open(ConcreteSectionSheetModule.Id, data);
        editor = mvvm;
        return (ConcreteSheetViewModel)mvvm.Sheet;
    }

    internal MvvmSheetEditor Editor { get; }
    internal BenchSheetServices Services { get; }
    internal ConcreteSheetViewModel Sheet { get; }

    internal ConcreteSectionSheetView View => Find<ConcreteSectionSheetView>(Editor.View, _ => true, realize: false)
        ?? throw new InvalidOperationException("Vista XAML della sezione c.a. non trovata.");

    /// <summary>The window that holds the view (the window of the suite, or the one of <see cref="Open"/>).</summary>
    internal Window Window => Window.GetWindow(Editor.View) ?? throw new InvalidOperationException("Vista della sezione c.a. fuori da una finestra.");

    internal JsonObject Data => Sheet.Data;
    internal JsonObject Input => Sheet.Input;
    internal JsonObject Settings => Sheet.Settings;
    internal JsonObject? Result => Sheet.Result;
    internal JsonObject ShearOptions => Settings["taglio"]!.AsObject();
    internal bool Busy => Sheet.IsBusy;
    internal bool Queued => Sheet.IsCalculationQueued;
    internal ConcreteInputViewModel Inputs => Sheet.Inputs;
    internal ConcreteDomainPanelViewModel Three => Sheet.Domain3D;
    internal ConcreteDomainPanelViewModel Two => Sheet.Domain2D;
    internal ConcreteStressPanelViewModel Stress(string key) => Sheet.StressPanel(key);
    internal JsonRowCollection Rows(string key) => (JsonRowCollection)Sheet.ActionRowSets[key];
    internal JsonRowCollection ShearRows => Sheet.ShearRowCollection;

    /// <summary>The checks still to calculate (copied: the set changes with the calculation).</summary>
    internal string[] Pending => Sheet.Calculation.Pending.Order(StringComparer.Ordinal).ToArray();

    internal static string PendingText(IEnumerable<string> pending) => "{" + string.Join(", ", pending) + "}";

    private static void Away(Window w)
    {
        w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = -32000; w.Top = -32000; w.ShowActivated = false; w.ShowInTaskbar = false;
    }

    // ---------------------------------------------------------------- waiting

    internal void Layout() { Window.GetWindow(Editor.View)?.UpdateLayout(); Editor.View.UpdateLayout(); }

    internal async Task Idle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Layout();
    }

    /// <summary>The automatic recalculation finished (Automatic of the historic checks: the queue and the calculation, within 2 minutes).</summary>
    internal async Task Automatic()
    {
        var watch = Stopwatch.StartNew();
        do { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); await Task.Delay(20); }
        while ((Queued || Busy) && watch.Elapsed < TimeSpan.FromMinutes(2));
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        if (Queued || Busy) throw new TimeoutException("Ricalcolo automatico non terminato: " + Sheet.Status);
        Layout();
    }

    /// <summary>Nothing left to do: the calculation, the queue and the work of the panels of the sheet (Hosted.Settle of --check-sezione-view).</summary>
    internal async Task Settle()
    {
        Layout();
        var watch = Stopwatch.StartNew(); int quiet = 0;
        while (quiet < 4)
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Task.Delay(20);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var s = Sheet;
            quiet = !s.IsBusy && !s.IsCalculationQueued && !s.Inputs.IsQuickResistanceRunning && !s.Curvature.IsRunning && !s.Design.IsRunning
                && !s.Domain3D.Inspection.IsRunning && !s.Domain2D.Inspection.IsRunning && !s.Status.StartsWith("Aggiornamento della sola mesh", StringComparison.Ordinal) ? quiet + 1 : 0;
            if (watch.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("La sezione c.a. non si ferma: " + s.Status);
        }
        Layout();
    }

    /// <summary>The status lines of the sheet while <paramref name="work"/> runs.</summary>
    internal async Task<List<string>> Statuses(Func<Task> work)
    {
        var seen = new List<string>();
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { if (e.PropertyName == nameof(ConcreteSheetViewModel.Status)) seen.Add(Sheet.Status); }
        Sheet.PropertyChanged += Changed;
        try { await work(); }
        finally { Sheet.PropertyChanged -= Changed; }
        return seen;
    }

    // ---------------------------------------------------------------- images

    /// <summary>PNG of the window of the view (Ui.Snapshot of the historic checks).</summary>
    internal async Task Capture(string directory, string name)
    {
        await Idle();
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(Window));
    }

    // ---------------------------------------------------------------- controls

    /// <summary>The single element of type <typeparamref name="T"/> under <paramref name="scope"/> that matches; the content not laid out yet is
    /// realized off the visual tree first (as the historic view built every control in code).</summary>
    internal static T? Find<T>(FrameworkElement scope, Func<T, bool> match, bool realize = true) where T : FrameworkElement
    {
        for (int pass = 0; pass < 3; pass++)
        {
            var found = CompositeSectionViewChecks.Walk(scope, popups: false).OfType<T>().Where(match).Distinct().ToList();
            if (found.Count == 1) return found[0];
            if (found.Count > 1) throw new InvalidOperationException("Controllo non univoco nella vista: " + typeof(T).Name + " (" + found.Count + ")");
            if (!realize) return null;
            if (!(scope.IsMeasureValid && scope.IsArrangeValid)) { scope.Measure(new Size(760, 700)); scope.Arrange(new Rect(0, 0, 760, 700)); }
            CompositeSectionViewChecks.Realize(scope);
        }
        return null;
    }

    /// <summary>A control by its automation name.</summary>
    internal static T Named<T>(FrameworkElement scope, string name) where T : FrameworkElement =>
        Find<T>(scope, e => ConcreteSectionViewChecks.AutomationName(e) == name)
        ?? throw new InvalidOperationException("Controllo non trovato nella vista per nome di automazione: «" + name + "»");

    /// <summary>The editor of a field of a form under <paramref name="scope"/> (the text box, the choice or the check box bound to the field).</summary>
    internal static T FieldEditor<T>(FormViewModel form, string key, FrameworkElement scope) where T : Control
    {
        var field = form[key];
        return Find<T>(scope, e => ReferenceEquals(e.DataContext, field) && e.TemplatedParent is not ComboBox)
            ?? throw new InvalidOperationException("Campo non disposto nella vista: " + key);
    }

    /// <summary>The user chooses <paramref name="item"/> in the choice of a field.</summary>
    internal static void Choose(FormViewModel form, string key, string item, FrameworkElement scope)
    {
        var combo = FieldEditor<ComboBox>(form, key, scope);
        combo.SelectedItem = combo.Items.Cast<object>().FirstOrDefault(i => Equals(i, item)) ?? throw new InvalidOperationException("Scelta assente: " + key + " = " + item);
    }

    /// <summary>A click as the user makes it (ButtonBase.OnClick: a check box changes, then Click, then the command), when the control is usable.</summary>
    internal static void Click(ButtonBase button)
    {
        if (!ConcreteSectionViewChecks.Click(button)) throw new InvalidOperationException("Pulsante non utilizzabile: " + ConcreteSectionViewChecks.AutomationName(button));
    }

    /// <summary>The content of the main tab <paramref name="index"/>.</summary>
    internal FrameworkElement TabContent(int index) => (FrameworkElement)((TabItem)View.Tabs.Items[index]).Content;

    /// <summary>The main tab chosen by its header, as the click of the user.</summary>
    internal void Tab(int index)
    {
        ((TabItem)View.Tabs.Items[index]).IsSelected = true;
        Layout();
    }

    /// <summary>The family of the serviceability chosen by its tab (tab 04).</summary>
    internal void Family(string key)
    {
        ((TabItem)View.StressTabs.Items[Stress(key).Index]).IsSelected = true;
        Layout();
    }

    /// <summary>The view of a family of the serviceability.</summary>
    internal ConcreteStressPanelView StressView(string key) => (ConcreteStressPanelView)((TabItem)View.StressTabs.Items[Stress(key).Index]).Content;

    /// <summary>The host of a table of the actions (the bar, the filter and the grid) under <paramref name="scope"/>.</summary>
    internal static FrameworkElement TableHost(FrameworkElement scope, ConcreteActionTableViewModel table) =>
        Find<ContentControl>(scope, c => ReferenceEquals(c.Content, table)) ?? throw new InvalidOperationException("Tabella delle azioni non trovata nella vista: " + table.Family);

    /// <summary>The grid of a table of the actions under <paramref name="scope"/>.</summary>
    internal static ConcreteTableGrid Grid(FrameworkElement scope, ConcreteActionTableViewModel table) => Named<ConcreteTableGrid>(TableHost(scope, table), "Combinazioni");

    /// <summary>The 3D domain drawn by the view (DomainViewport3D of the historic panel).</summary>
    internal DomainViewport3D Viewport3D => (DomainViewport3D)(View.Domain3DView.Drawing ?? throw new InvalidOperationException("Dominio 3D non disegnato."));

    /// <summary>The 2D domain drawn by the view (Plot of the historic panel).</summary>
    internal Plot Plot2D => (Plot)(View.Domain2DView.Drawing ?? throw new InvalidOperationException("Dominio 2D non disegnato."));

    // ---------------------------------------------------------------- results (the export of the sheet)

    /// <summary>The checks of a domain («3D:SLU»…) in the export, by id.</summary>
    internal JsonObject DomainResults(string name) => Result?["domini"]?[name] as JsonObject ?? throw new InvalidOperationException("Dominio senza risultati: " + name + " · " + Sheet.Status);

    /// <summary>The utilizations of the checks of a domain in the export (null where not available).</summary>
    internal double?[] Utilizations(string name) => DomainResults(name).Select(p => p.Value?["Utilization"] is JsonValue v ? v.GetValue<double>() : (double?)null).ToArray();

    /// <summary>The outcomes of a family of the serviceability in the export, by id.</summary>
    internal JsonObject StressResults(string key) => Result?["tensioni"]?[key] as JsonObject ?? throw new InvalidOperationException("Famiglia SLE senza risultati: " + key + " · " + Sheet.Status);

    /// <summary>The shear checks in the export, by id of the row.</summary>
    internal JsonObject ShearResults => Result?["taglio"] as JsonObject ?? throw new InvalidOperationException("Taglio senza risultati · " + Sheet.Status);

    /// <summary>The text of a part of the export, to compare values kept or calculated again.</summary>
    internal string Part(params string[] path)
    {
        JsonNode? node = Result ?? throw new InvalidOperationException("Risultato assente · " + Sheet.Status);
        foreach (string key in path) node = node?[key];
        return node is null ? "null" : Canonical(node);
    }

    /// <summary>The text of a node with the keys of every object in order (the families and the checks are published in the order the batch ends).</summary>
    private static string Canonical(JsonNode node) => node switch
    {
        JsonObject o => "{" + string.Join(",", o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => JsonValue.Create(p.Key)!.ToJsonString() + ":" + (p.Value is null ? "null" : Canonical(p.Value)))) + "}",
        JsonArray a => "[" + string.Join(",", a.Select(v => v is null ? "null" : Canonical(v))) + "]",
        _ => node.ToJsonString()
    };

    // ---------------------------------------------------------------- dialogs

    /// <summary>
    /// The dialogs of the sheet shown while <paramref name="action"/> runs, as WpfDialogService shows them (the window around the ViewModel, its
    /// view from the DataTemplate, closed when the ViewModel asks) but off screen, never activated, driven by <paramref name="script"/> while open.
    /// </summary>
    internal async Task WithDialogs(Action<object, Window> script, Func<Task> action)
    {
        var previous = Services.Dialog;
        Services.Dialog = dialog => ShowDialog(dialog, script);
        try { await action(); }
        finally { Services.Dialog = previous; }
    }

    /// <summary>A dialog ViewModel in a window off screen, driven by <paramref name="script"/>; its result when it closed itself, otherwise null.</summary>
    internal static object? ShowDialog(object dialog, Action<object, Window> script)
    {
        var type = dialog.GetType();
        var window = new Window
        {
            Title = (string?)type.GetProperty("Title")?.GetValue(dialog) ?? "", Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false,
            ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000
        };
        window.SetValue(NumericPresentation.EnabledProperty, true);
        Appearance.Watch(window);
        bool closed = false;
        var closeEvent = type.GetEvent("CloseRequested") ?? throw new InvalidOperationException("Finestra senza CloseRequested: " + type.Name);
        EventHandler close = (_, _) => { closed = true; window.Close(); };
        closeEvent.AddEventHandler(dialog, close);
        try { window.Show(); window.UpdateLayout(); script(dialog, window); }
        finally { closeEvent.RemoveEventHandler(dialog, close); if (window.IsVisible) window.Close(); }
        return closed ? type.GetProperty("Result")?.GetValue(dialog) : null;
    }

    /// <summary>The windows of the view shown so far («Espandi», «Proprietà / report…», «Reologia del calcestruzzo…»).</summary>
    internal IReadOnlyList<Window> ViewWindows => windows;

    // ---------------------------------------------------------------- a calculation held at its start (the batches of the analysis session)

    private static readonly FieldInfo SessionField = typeof(ConcreteSheetViewModel).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ConcreteSheetViewModel), "session");
    private static readonly FieldInfo BatchesField = typeof(ConcreteAnalysisSession).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ConcreteAnalysisSession), "batches");

    /// <summary>
    /// The batches of the analysis session of the sheet held (the step «trattieni» of the traces, ConcreteSectionViewChecks.Trace): a calculation
    /// started now waits at its start, busy, until the handle is disposed.
    /// </summary>
    internal Task<IDisposable> HoldCalculations() => HoldCalculations(Sheet);

    /// <summary>The batches of the analysis session of <paramref name="sheet"/> held (see <see cref="HoldCalculations()"/>).</summary>
    internal static async Task<IDisposable> HoldCalculations(ConcreteSheetViewModel sheet)
    {
        var batches = (SemaphoreSlim)BatchesField.GetValue(SessionField.GetValue(sheet))!;
        await batches.WaitAsync();
        return new Release(batches);
    }

    private sealed class Release(SemaphoreSlim batches) : IDisposable
    {
        private bool released;
        public void Dispose() { if (released) return; released = true; batches.Release(); }
    }

    // ---------------------------------------------------------------- closing

    /// <summary>Closes the windows of the view; the window and the editor of <see cref="Open"/>.</summary>
    public void Dispose()
    {
        foreach (var window in windows.ToArray()) if (window.IsVisible) window.Close();
        windows.Clear();
        if (host is not null) { host.Content = null; host.Close(); }
        if (ownsEditor) Editor.Dispose();
    }
}

/// <summary>The clipboard of the sheets in the suites that copy and paste (the c.a. section): the text written and read.</summary>
internal sealed class ScriptedClipboard
{
    internal string Text { get; set; } = "";
}
