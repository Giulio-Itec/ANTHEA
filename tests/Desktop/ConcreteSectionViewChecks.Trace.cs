using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Anthea.Application.Concrete;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Tests;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.ConcreteSection;

namespace X.Desktop;

/// <summary>
/// The traces of the 57 frozen scenarios (tests/Shared/ConcreteSectionTrace.cs) driven through the controls of the XAML view (F5.10, part «tracce» of
/// --check-sezione-view; section 22 of docs/refactoring/f5-10-sezione.md). Every step is done on the control the user would use, found by its
/// automation name: the fields and the choices of the forms (FieldForm gives every editor the label of its field), the buttons, the check boxes, the
/// combo boxes, the sliders, the tabs, the tables, the bars of the filters and the windows of the dialogs («Nuovo materiale · …», the wizard,
/// «Sollecitazioni della sezione»), shown by the service of the dialogs from their DataTemplate and driven through their controls while they are open.
/// The state after every step is the one of the frozen trace: the common part from the controls of the view (status line, main tab, progress bar,
/// the width field), the forms, the tables and the panels from the ViewModel (MU19), with the model of the layout of the historic window
/// (<see cref="ConcreteSectionSheetReader"/>, the same of Presentation.Tests). The comparison is the one of the ViewModel: every line of every
/// part, no filter. The dispatcher runs only where the historic trace runs it (the steps «attendi», «calcola», «attendi-motore»): a step and the
/// state after it are read in the same operation, as the historic trace reads them after UpdateLayout.
/// </summary>
internal static partial class ConcreteSectionViewChecks
{
    /// <summary>The sections of the report of the F3 capture and of the historic trace (every section except «dettagli» and «sle_tutte»).</summary>
    private static readonly HashSet<string> TraceReportSections = ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet();

    /// <summary>Steps driven on the ViewModel because the control of the view could not do what the historic control did (see the log).</summary>
    private static readonly List<string> TraceDeviations = [];

    /// <summary>
    /// The deviations expected, each a behaviour of the off screen historic trace that the view does not have (section 22 of
    /// docs/refactoring/f5-10-sezione.md). «barre-manuali», step 7: with the bars by coordinates the form of the reinforcement is disabled, but the
    /// historic form in the closed group «Armature» had never been laid out and WPF had not passed the disabled state to its fields: the historic trace
    /// typed into it (the ViewModel side models it with the forms laid out, ConcreteSectionSheetReader). The editor of the view follows the form even
    /// when it is not laid out, as the historic one once laid out: the step is done on the field. Any other deviation fails the check.
    /// </summary>
    private static readonly string[] ExpectedDeviations = ["ingressi/barre-manuali.txt · passo 7 «campo armatura bottom_bar_count «8»»: editor spento nella vista"];

    /// <summary>The controls laid out that do not show what the ViewModel holds, after a step (<see cref="TraceSide.CheckControls"/>).</summary>
    private static readonly List<string> TraceMismatches = [];

    /// <summary>The controls compared with the ViewModel after the steps: grids and editors of the forms.</summary>
    private static int traceControls;

    private static async Task Traces(string root, string output)
    {
        Directory.CreateDirectory(output);
        TraceDeviations.Clear(); TraceMismatches.Clear(); traceControls = 0;
        var scenarios = ConcreteSectionScenarios.All(root);
        var differences = new List<string>(); var report = new List<string>();
        int lines = 0;
        foreach (var scenario in scenarios)
        {
            var watch = Stopwatch.StartNew();
            var trace = await Trace(root, scenario);
            ConcreteSectionTrace.Write(Path.Combine(output, scenario.File), trace);
            string stored = Path.Combine(ConcreteSectionScenarios.Reference(root), scenario.File);
            string? difference = File.Exists(stored) ? ConcreteSectionTrace.FirstDifference(ConcreteSectionTrace.Read(stored), trace) : "traccia di riferimento assente";
            string name = scenario.File.Replace('\\', '/');
            report.Add(name + " · " + trace.Count.ToString(CultureInfo.InvariantCulture) + " righe · " + watch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s · "
                + (difference is null ? "uguale" : "diverso: " + difference));
            if (difference is not null) differences.Add(name + ": " + difference);
            lines += trace.Count;
        }
        File.WriteAllLines(Path.Combine(output, "confronto.txt"), report, new UTF8Encoding(false));
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(output, "deroghe.txt"), TraceDeviations, new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(output, "controlli-diversi.txt"), TraceMismatches, new UTF8Encoding(false));
        Log.Add($"   tracce: {scenarios.Count} scenari, {lines} righe, {differences.Count} diversi, {TraceDeviations.Count} passi guidati sul ViewModel");
        Check(differences.Count == 0 && scenarios.Count == 57, $"Tracce guidate dai controlli della vista XAML uguali a quelle congelate della vista storica: {scenarios.Count} scenari, {lines} righe"
            + (differences.Count == 0 ? "" : " — " + differences.Count + " diversi, il primo " + differences[0]));
        Check(TraceDeviations.SequenceEqual(ExpectedDeviations), $"Passi guidati sul ViewModel solo quelli attesi ({TraceDeviations.Count}): "
            + string.Join(" | ", TraceDeviations));
        Check(TraceMismatches.Count == 0, $"Dopo ogni passo i controlli disposti della vista mostrano il ViewModel: {traceControls} letture di tabelle e campi"
            + (TraceMismatches.Count == 0 ? "" : ", " + TraceMismatches.Count + " diverse, la prima " + TraceMismatches[0]));
    }

    private static async Task<List<string>> Trace(string root, ConcreteSectionScenario scenario)
    {
        var lines = new List<string>();
        var data = ConcreteSectionScenarios.FixtureData(root, scenario.Fixture);
        var guids = new ConcreteGuids(data);
        var memory = new ConcreteDataMemory();
        var side = new TraceSide(data, scenario.File.Replace('\\', '/'));
        try
        {
            // The state of the opening is read at once, before the dispatcher runs anything.
            lines.AddRange(side.State(scenario.Sections, null).Lines("apertura", guids, memory));
            foreach (var step in scenario.Steps)
            {
                string? message = await side.Apply(step);
                lines.AddRange(side.State(scenario.Sections, message).Lines(step.ToString(), guids, memory));
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or TimeoutException or NullReferenceException)
        { lines.Add("errore " + error.GetType().Name + ": " + error.Message); Log.Add("   " + scenario.File + ": " + error); }
        finally { await side.DisposeAsync(); }
        return lines;
    }

    // ---------------------------------------------------------------- automation names

    /// <summary>The name a client of UI Automation reads: AutomationProperties.Name, otherwise the name of the peer (the text of a button, of a check
    /// box, the header of a tab).</summary>
    internal static string AutomationName(DependencyObject element)
    {
        if (element is not UIElement ui) return "";
        string name = AutomationProperties.GetName(ui);
        if (!string.IsNullOrEmpty(name)) return name;
        try { return UIElementAutomationPeer.CreatePeerForElement(ui)?.GetName() ?? ""; }
        catch (InvalidOperationException) { return ""; }
    }

    /// <summary>Shown as the historic trace checks it: no element up to the root is collapsed or hidden (closed expanders and tabs not shown do not
    /// count: the user opens them).</summary>
    private static bool Shown(DependencyObject element)
    {
        for (DependencyObject? node = element; node is not null; node = LogicalTreeHelper.GetParent(node) ?? VisualParent(node))
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
        static DependencyObject? VisualParent(DependencyObject node) => node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(node) : null;
    }

    /// <summary>A control the user can use: enabled and shown.</summary>
    private static bool Usable(UIElement element) => element.IsEnabled && Shown(element);

    private static readonly MethodInfo ClickMethod = typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(ButtonBase), "OnClick");
    // The click of a menu item without its delay: MenuItem.OnClick queues InvokeClickAfterRender (Click, then the command) at Render priority, after
    // the menu closes; the trace runs it at once, as the historic trace ran the paste of the grid.
    private static readonly MethodInfo MenuClickMethod = typeof(MenuItem).GetMethod("InvokeClickAfterRender", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(MenuItem), "InvokeClickAfterRender");

    /// <summary>A click as the user makes it (ButtonBase.OnClick: a check box changes, then Click, then the command), only when the control is usable.</summary>
    internal static bool Click(ButtonBase? button)
    {
        if (button is null || !Usable(button)) return false;
        ClickMethod.Invoke(button, null);
        return true;
    }

    /// <summary>
    /// The side of the XAML view: the sheet opened by the bench with the option module-impl str_palo=mvvm in a window off screen (1400 × 1000, as the
    /// historic trace), its services (messages, clipboard, the dialogs shown and driven here), the steps on its controls.
    /// </summary>
    private sealed class TraceSide
    {
        private static readonly FieldInfo SessionField = typeof(ConcreteSheetViewModel).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(ConcreteSheetViewModel), "session");
        private static readonly FieldInfo Batches = typeof(ConcreteAnalysisSession).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(ConcreteAnalysisSession), "batches");

        private readonly TraceServices services = new();
        private readonly ConcreteSectionSheetReader reader;
        private readonly string scenario;
        private Hosted host = null!;
        private ConcreteSectionSheetView view = null!;
        private SemaphoreSlim? held;
        private Task? started;
        private int step;

        internal TraceSide(JsonObject data, string scenario)
        {
            this.scenario = scenario;
            reader = new(() => host.Sheet);
            Open(data);
        }

        private ConcreteSheetViewModel Sheet => host.Sheet;
        private ConcreteInputViewModel Inputs => Sheet.Inputs;
        private FrameworkElement Root => host.Root;

        private void Open(JsonObject data)
        {
            var editors = new SheetEditorFactory(ModuleHostBench.Implementations(BenchArguments), DesktopComposition.CreateModules(), services.Create(),
                new CalculationLimiter(CalculationLimiter.DefaultMaximum));
            host = new Hosted(editors.Open(Module, (JsonObject)data.DeepClone()));
            if (host.Editor is not MvvmSheetEditor) throw new InvalidOperationException("Il banco non ha aperto la vista MVVM della sezione c.a.");
            view = host.View;
            reader.Reset();
        }

        private void Close() => host.Dispose();

        internal async Task DisposeAsync()
        {
            held?.Release(); held = null;
            if (started is not null) try { await started.WaitAsync(TimeSpan.FromMinutes(1)); } catch (Exception) { }
            Close();
        }

        // ---------------------------------------------------------------- state

        internal ConcreteSectionTraceState State(IReadOnlyList<string> sections, string? message)
        {
            // The common part from the controls: the status line, the main tab, the progress of the calculation, the width field (laid out with the
            // group «Geometria»; the ViewModel when the field is not laid out).
            var width = Inputs.Geometry["width_mm"];
            bool editable = FindEditor<TextBox>(Inputs.Geometry, width, Tab(0), realize: false) is { } box ? !box.IsReadOnly : !width.IsInputReadOnly;
            var common = new ConcreteSectionCommonPart(view.StatusText.Text, view.Tabs.SelectedIndex, view.CalculationProgress.Visibility == Visibility.Visible, editable);
            CheckControls();
            return reader.Read(sections, message, host.Modified, common);
        }

        /// <summary>
        /// What the controls laid out show agrees with the ViewModel the state is read from: every grid of the sheet shows the rows of its table in
        /// their order, its selection and its read-only state; every editor of a FieldForm shows the value of its field (text, choice, check box) and
        /// a text box refuses the typing when its field does. The differences are listed and fail the check. The texts are compared with the
        /// decimal separator of either culture: in the culture state of the capture (question V-a) the text of a calculated field is formatted with
        /// the separator of the operation that published it (the point of this machine outside the flow of the check).
        /// </summary>
        private void CheckControls()
        {
            static bool SameText(string? a, string? b) => a == b || (a ?? "").Replace(',', '.') == (b ?? "").Replace(',', '.');
            void Differ(string what) => TraceMismatches.Add(scenario + " · passo " + step.ToString(CultureInfo.InvariantCulture) + ": " + what);
            foreach (var element in CompositeSectionViewChecks.Walk(Root, popups: false))
            {
                switch (element)
                {
                    case ConcreteTableGrid { Table: { } table } grid:
                    {
                        traceControls++;
                        string name = AutomationName(grid);
                        if (!grid.Items.Cast<object>().SequenceEqual(table.Shown)) Differ("tabella «" + name + "»: righe " + grid.Items.Count + " mostrate, " + table.Shown.Count + " nella tabella");
                        if (grid.SelectedIndex != table.SelectedIndex) Differ("tabella «" + name + "»: selezione " + grid.SelectedIndex + " nella vista, " + table.SelectedIndex + " nella tabella");
                        if (grid.IsReadOnly != table.IsReadOnly) Differ("tabella «" + name + "»: sola lettura " + grid.IsReadOnly + " nella vista, " + table.IsReadOnly + " nella tabella");
                        break;
                    }
                    case TextBox { DataContext: Anthea.Presentation.Forms.FieldViewModel { Kind: Anthea.Presentation.Forms.FieldKind.Text } field } box when box.TemplatedParent is not ComboBox:
                        traceControls++;
                        if (!SameText(box.Text, field.EditText)) Differ("campo «" + field.Label + "» (" + field.Key + "): «" + box.Text + "» nella vista, «" + field.EditText + "» nel campo");
                        if (box.IsReadOnly != field.IsInputReadOnly) Differ("campo «" + field.Label + "» (" + field.Key + "): sola lettura " + box.IsReadOnly + " nella vista, " + field.IsInputReadOnly + " nel campo");
                        break;
                    case ComboBox { DataContext: Anthea.Presentation.Forms.FieldViewModel { Kind: Anthea.Presentation.Forms.FieldKind.Choice } field } combo:
                        traceControls++;
                        if (!Equals(combo.SelectedItem, field.SelectedChoice)) Differ("scelta «" + field.Label + "» (" + field.Key + "): «" + combo.SelectedItem + "» nella vista, «" + field.SelectedChoice + "» nel campo");
                        break;
                    case CheckBox { DataContext: Anthea.Presentation.Forms.FieldViewModel { Kind: Anthea.Presentation.Forms.FieldKind.Bool } field } check:
                        traceControls++;
                        if (check.IsChecked != field.IsChecked) Differ("casella «" + field.Label + "» (" + field.Key + "): " + check.IsChecked + " nella vista, " + field.IsChecked + " nel campo");
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- steps

        internal async Task<string?> Apply(ConcreteSectionStep step)
        {
            this.step++;
            try
            {
                switch (step.Kind)
                {
                    case ConcreteSectionStep.Commit: host.Editor.Commit(); return null;
                    // «Calcola» of the window: the calculation of the sheet without the commit (the historic host calculated the RC section
                    // without committing, SheetEditor.CalculateAsync → ConcreteWorkspace.CalculateAllAsync; MvvmSheetEditor commits unless asked not
                    // to, question V-f of section 22 of docs/refactoring/f5-10-sezione.md).
                    case ConcreteSectionStep.Calculate: await host.Editor.CalculateAsync(commitEdits: false); return null;
                    case ConcreteSectionStep.Start: started = host.Editor.CalculateAsync(commitEdits: false); return null;
                    case ConcreteSectionStep.Wait:
                        if (started is not null) { await started.WaitAsync(TimeSpan.FromMinutes(3)); started = null; }
                        await host.Settle(); return null;
                    case ConcreteSectionStep.ReadOnly: host.Editor.InspectRevision(); return null;
                    case ConcreteSectionStep.Reopen: { var data = host.Editor.Data; Close(); Open(data); return null; }
                    case ConcreteSectionStep.Hold:
                        held = (SemaphoreSlim)Batches.GetValue(SessionField.GetValue(Sheet))!; await held.WaitAsync(); return null;
                    case ConcreteSectionStep.Entered: await EnteredAsync(); return null;
                    case ConcreteSectionStep.Release: held?.Release(); held = null; return null;
                    case ConcreteSectionStep.Visit: Visit(); return null;
                    case ConcreteSectionStep.Report: return Report(step.Text!);
                    default: return Control(step);
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return error.GetType().Name + ": " + error.Message; }
            finally { host.Layout(); reader.Observe(); }
        }

        private async Task EnteredAsync()
        {
            var watch = Stopwatch.StartNew();
            while (!Sheet.IsBusy || Sheet.IsCalculationQueued)
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); await Task.Delay(10);
                if (watch.Elapsed > TimeSpan.FromMinutes(1)) throw new TimeoutException("Nessun calcolo avviato.");
            }
        }

        /// <summary>
        /// The visit of the F3 capture (F3EquivalenceCapture.Visit) on the view: every expander opened, every tab of every tab control selected and
        /// restored; the layout of the historic window observed at every tab.
        /// </summary>
        private void Visit()
        {
            Root.UpdateLayout();
            var tabs = new HashSet<TabControl>();
            for (int pass = 0; pass < 8; pass++)
            {
                foreach (var e in CompositeSectionViewChecks.Walk(Root).OfType<Expander>().ToArray()) e.IsExpanded = true;
                var found = CompositeSectionViewChecks.Walk(Root).OfType<TabControl>().Where(t => tabs.Add(t)).ToArray(); if (found.Length == 0) break;
                foreach (var tab in found)
                {
                    int selected = tab.SelectedIndex;
                    for (int i = 0; i < tab.Items.Count; i++)
                    {
                        tab.SelectedIndex = i; Root.UpdateLayout();
                        // A tab control not shown (the families of the serviceability with another tab of the sheet): the forms of its tab are laid
                        // out off the visual tree, so that the visit reaches the groups of their fields as the historic visit reaches the forms built
                        // in code. Only the forms: a tab control laid out selects its first tab (TabControl with no selection), which the historic
                        // view, never laying it out, did not do.
                        if (!tab.IsVisible && tab.Items[i] is TabItem { Content: FrameworkElement content })
                            foreach (var form in CompositeSectionViewChecks.Walk(content, popups: false).OfType<ContentControl>()
                                .Where(c => c.Content is Anthea.Presentation.Forms.FormViewModel).ToList())
                                RealizeContent(form);
                        reader.Observe();
                    }
                    tab.SelectedIndex = selected;
                }
            }
        }

        /// <summary>
        /// The two report commands of the window, as the historic trace drives them («Report Word»: the check of MainWindow.ExportReport, then the
        /// report with the sections of the F3 capture; «Report short»): only whether the report is available, or the message shown.
        /// </summary>
        private string Report(string kind)
        {
            var editor = host.Editor;
            try
            {
                if (kind == "breve") { editor.Commit(); _ = editor.BuildShortReport("Traccia F5.10 breve"); return "report breve disponibile"; }
                editor.Commit();
                if (!editor.HasResults || editor.Busy) return "report completo: Attendere l’aggiornamento automatico e correggere gli eventuali dati non validi.";
                _ = editor.BuildReport("Traccia F5.10", TraceReportSections);
                return "report completo disponibile";
            }
            catch (Exception error) { return "report " + kind + ": " + error.Message; }
        }

        // ---------------------------------------------------------------- controls

        /// <summary>The content of the main tab <paramref name="index"/> (in the logical tree also when the tab is not shown).</summary>
        private FrameworkElement Tab(int index) => (FrameworkElement)((TabItem)view.Tabs.Items[index]).Content;

        private ConcreteStressPanelView StressView(string key) => (ConcreteStressPanelView)((TabItem)view.StressTabs.Items[Sheet.StressPanel(key).Index]).Content;

        /// <summary>Where the controls of a target of the steps are.</summary>
        private FrameworkElement Scope(string target) => target switch
        {
            "3D" or "ispezione-3D" => view.Domain3DView,
            "2D" or "ispezione-2D" => view.Domain2DView,
            "SLE" or "SLE_FREQ" or "SLE_QP" => StressView(target),
            "taglio" or "torsione" => view.ShearView,
            "dettagli" or "ancoraggi" => view.DetailingView,
            "curvatura" => view.CurvatureView,
            "ricerca" or "limiti" or "mostra" or "alternative" => view.DesignView,
            _ => Tab(0)
        };

        /// <summary>
        /// The single element of type <typeparamref name="T"/> under <paramref name="scope"/> that matches; when it is not laid out yet (a closed
        /// group, a tab not shown, the template of a table), the content under <paramref name="scope"/> is realized off the visual tree first, as the
        /// historic view built every control in code (<see cref="CompositeSectionViewChecks.Realize"/>, limited to the scope: the tab controls of
        /// the other panels are never laid out by the check).
        /// </summary>
        private T? Find<T>(FrameworkElement scope, Func<T, bool> match, string what, bool realize = true) where T : FrameworkElement
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var found = CompositeSectionViewChecks.Walk(scope, popups: false).OfType<T>().Where(match).Distinct().ToList();
                if (found.Count == 1) return found[0];
                if (found.Count > 1) throw new InvalidOperationException("Controllo non univoco nella vista: " + what + " (" + found.Count.ToString(CultureInfo.InvariantCulture) + ")");
                if (!realize) return null;
                host.Layout(); RealizeContent(scope); host.Layout();
            }
            return null;
        }

        /// <summary>
        /// The content under <paramref name="scope"/> laid out off the visual tree when WPF has not laid it out (a closed group, a tab not shown): the
        /// scope measured on its own when it is not, then every element without a visual parent and every template
        /// (<see cref="CompositeSectionViewChecks.Realize"/>). Nothing is written into the sheet.
        /// </summary>
        private static void RealizeContent(FrameworkElement scope)
        {
            if (!(scope.IsMeasureValid && scope.IsArrangeValid)) { scope.Measure(new Size(760, 700)); scope.Arrange(new Rect(0, 0, 760, 700)); }
            CompositeSectionViewChecks.Realize(scope);
        }

        private T Named<T>(FrameworkElement scope, string name) where T : FrameworkElement =>
            Find<T>(scope, e => AutomationName(e) == name, name) ?? throw new InvalidOperationException("Controllo non trovato nella vista per nome di automazione: «" + name + "»");

        /// <summary>The hosts of a form in the view (the content controls whose content is the form).</summary>
        private IEnumerable<FrameworkElement> FormHosts(FrameworkElement scope, Anthea.Presentation.Forms.FormViewModel form) =>
            CompositeSectionViewChecks.Walk(scope, popups: false).OfType<ContentControl>().Where(c => ReferenceEquals(c.Content, form)).ToList();

        /// <summary>
        /// The editor of a field in its FieldForm, found by its automation name (the label of the field), in the host of its form under
        /// <paramref name="scope"/>; only the host is laid out when needed (the scope too, when the host is in a template not built yet: the layers).
        /// </summary>
        private T? FindEditor<T>(Anthea.Presentation.Forms.FormViewModel form, Anthea.Presentation.Forms.FieldViewModel field, FrameworkElement scope, bool realize = true)
            where T : Control
        {
            var hosts = FormHosts(scope, form).ToList();
            if (hosts.Count == 0 && realize) { host.Layout(); RealizeContent(scope); host.Layout(); hosts = FormHosts(scope, form).ToList(); }
            foreach (var formHost in hosts)
                if (Find<T>(formHost, e => ReferenceEquals(e.DataContext, field) && e.TemplatedParent is not ComboBox && AutomationName(e) == field.Label, field.Label, realize) is { } editor)
                    return editor;
            return null;
        }

        private string? Control(ConcreteSectionStep step)
        {
            switch (step.Kind)
            {
                case ConcreteSectionStep.Tab:
                    SelectTab(view.Tabs, ConcreteSheetViewModel.TabTitles[step.Index]); return null;
                case ConcreteSectionStep.Field or ConcreteSectionStep.Choice or ConcreteSectionStep.Option when reader.Form(step.Target!, step.Key) is { } form:
                    FormStep(form, step, Scope(step.Target!), reader.Usable); return null;
                case ConcreteSectionStep.Pick when step.Target is "3D" or "2D" && step.Key == "stato":
                    FormStep(reader.Form(step.Target)!, step with { Kind = ConcreteSectionStep.Choice }, Scope(step.Target), reader.Usable); return null;
                case ConcreteSectionStep.Toggle: Click(ToggleOf(step)); return null;
                case ConcreteSectionStep.Button: Click(ButtonOf(step)); return null;
                case ConcreteSectionStep.Pick: Pick(step); return null;
                case ConcreteSectionStep.Slider:
                {
                    string name = step.Key switch { "trasparenza" => "Trasparenza", "dimensione_ed" => "Punti Ed · dimensione", _ => "Punti Rd · dimensione" };
                    var slider = Named<Slider>(Scope(step.Target!), name);
                    if (Usable(slider)) slider.Value = double.Parse(step.Text!, CultureInfo.InvariantCulture);
                    return null;
                }
                case ConcreteSectionStep.Cell: Cell(step); return null;
                case ConcreteSectionStep.Select:
                {
                    var grid = Grid(step.Target!);
                    if (step.Index < grid.Items.Count) grid.SelectedIndex = step.Index;
                    return null;
                }
                case ConcreteSectionStep.RowCommand:
                {
                    var grid = Grid(step.Target!);
                    if (step.Index >= 0 && step.Index < grid.Items.Count) grid.SelectedIndex = step.Index;
                    Click(Named<Button>(TableHost(step.Target!), step.Key!));
                    return null;
                }
                case ConcreteSectionStep.Paste: return Paste(step);
                case ConcreteSectionStep.Filter: Filter(step); return null;
                case ConcreteSectionStep.InnerTab: InnerTab(step); return null;
                case ConcreteSectionStep.Open: return Open(step);
                default: throw new ArgumentException("Passo sconosciuto: " + step);
            }
        }

        /// <summary>A tab of <paramref name="tabs"/> chosen by its automation name, as the click on its header selects it.</summary>
        private void SelectTab(TabControl tabs, string name)
        {
            var item = tabs.Items.OfType<TabItem>().Where(t => AutomationName(t) == name).ToList();
            if (item.Count != 1) throw new InvalidOperationException("Scheda non trovata per nome di automazione: «" + name + "»");
            item[0].IsSelected = true;
        }

        /// <summary>
        /// A field, a choice or a check box of a form, through its editor: a field the user cannot use (hidden, disabled, protected by the read-only
        /// revision; <paramref name="usable"/> gives the model of the historic layout) does nothing. When the model says the historic field was
        /// usable but the editor of the view is disabled or missing, the step is done on the ViewModel and listed among the deviations.
        /// </summary>
        private void FormStep(Anthea.Presentation.Forms.FormViewModel form, ConcreteSectionStep step, FrameworkElement scope,
            Func<Anthea.Presentation.Forms.FormViewModel, Anthea.Presentation.Forms.FieldViewModel, bool> usable)
        {
            if (!form.Contains(step.Key!)) return;
            var field = form[step.Key!];
            switch (step.Kind)
            {
                case ConcreteSectionStep.Field:
                {
                    if (field.Kind != Anthea.Presentation.Forms.FieldKind.Text || !usable(form, field) || field.IsInputReadOnly) return;
                    var box = FindEditor<TextBox>(form, field, scope);
                    if (box is not null && box.IsEnabled && !box.IsReadOnly) { box.Text = step.Text; return; }
                    Deviation(step, box is null ? "campo senza editor nella vista" : "editor spento nella vista");
                    field.EditText = step.Text!;
                    return;
                }
                case ConcreteSectionStep.Choice:
                {
                    if (field.Kind != Anthea.Presentation.Forms.FieldKind.Choice || !usable(form, field)) return;
                    var combo = FindEditor<ComboBox>(form, field, scope);
                    if (combo is not null && combo.IsEnabled)
                    {
                        // Only an item of the list can be chosen.
                        if (combo.Items.Cast<object>().FirstOrDefault(i => Equals(i, step.Text)) is { } item) combo.SelectedItem = item;
                        return;
                    }
                    Deviation(step, combo is null ? "scelta senza editor nella vista" : "scelta spenta nella vista");
                    field.SelectedChoice = step.Text;
                    return;
                }
                default:
                {
                    if (field.Kind != Anthea.Presentation.Forms.FieldKind.Bool || !usable(form, field)) return;
                    var check = FindEditor<CheckBox>(form, field, scope);
                    if (check is not null && check.IsEnabled) { Click(check); return; }
                    Deviation(step, check is null ? "casella senza editor nella vista" : "casella spenta nella vista");
                    field.IsChecked = !field.IsChecked;
                    return;
                }
            }
        }

        private void Deviation(ConcreteSectionStep step, string reason) =>
            TraceDeviations.Add(scenario + " · passo " + this.step.ToString(CultureInfo.InvariantCulture) + " «" + step + "»: " + reason);

        /// <summary>The check box of a step «spunta».</summary>
        private CheckBox ToggleOf(ConcreteSectionStep step) => step.Target switch
        {
            "foro" => Named<CheckBox>(Tab(0), step.Key!),
            "anteprima" => Named<CheckBox>(view.PreviewToolbar, step.Key!),
            _ => Named<CheckBox>(Scope(step.Target!), step.Key!)
        };

        /// <summary>The button of a step «pulsante»: by its automation name in the panel of the target (the layers: the toggle of the layer).</summary>
        private ButtonBase ButtonOf(ConcreteSectionStep step)
        {
            if (step.Target == "strati")
            {
                var layer = Inputs.Layers.Single(l => l.Key == step.Key);
                return Find<Button>(Tab(0), b => ReferenceEquals(b.DataContext, layer) && AutomationName(b) == layer.ToggleText, "strato " + step.Key)
                    ?? throw new InvalidOperationException("Pulsante dello strato non trovato: " + step.Key);
            }
            // «Forze: tutte» / «Forze: selezionata»: the same button, its text follows the option.
            if (step.Target is "3D" or "2D" && step.Key!.StartsWith("Forze", StringComparison.Ordinal))
                return Find<Button>(Scope(step.Target), b => AutomationName(b).StartsWith("Forze", StringComparison.Ordinal), "forze")
                    ?? throw new InvalidOperationException("Pulsante delle forze non trovato.");
            string name = (step.Target, step.Key) switch { ("cavo", "−") => "Elimina cavo", _ => step.Key! };
            return Named<Button>(Scope(step.Target!), name);
        }

        /// <summary>A step «menu»: the combo boxes outside the forms, by their automation names.</summary>
        private void Pick(ConcreteSectionStep step)
        {
            ComboBox combo = (step.Target, step.Key) switch
            {
                ("materiali-utente", "elenco") => Named<ComboBox>(Tab(0), "Materiali salvati"),
                ("trefoli", "standard") => Named<ComboBox>(Tab(0), "Trefoli da standard EN 1992"),
                ("3D" or "2D", "vista") => Named<ComboBox>(Scope(step.Target!), "Vista del dominio"),
                ("SLE" or "SLE_FREQ" or "SLE_QP" or "ispezione-3D" or "ispezione-2D", "contorno") => Named<ComboBox>(Scope(step.Target!), "Contorno della mappa"),
                ("SLE" or "SLE_FREQ" or "SLE_QP", "zone") => Named<ComboBox>(Scope(step.Target!), "Zona efficace"),
                ("ispezione-3D" or "ispezione-2D", "stato") => Named<ComboBox>(Scope(step.Target!), "Soluzione mostrata"),
                _ => throw new ArgumentException("Scelta sconosciuta: " + step.Target + " " + step.Key)
            };
            if (!Usable(combo)) return;
            if (step.Key == "zone") { if (int.TryParse(step.Text, out int index) && index < combo.Items.Count) combo.SelectedIndex = index; return; }
            var item = combo.Items.Cast<object>().FirstOrDefault(i => (i is ComboBoxItem c ? c.Content : i)?.ToString() == step.Text);
            if (item is not null) combo.SelectedItem = item;
        }

        // ---------------------------------------------------------------- tables

        /// <summary>The table of the actions of a target (the content control with the template of the bar, the filter and the grid).</summary>
        private ConcreteActionTableViewModel Actions(string target) => Sheet.Actions.Table(target);

        private FrameworkElement TableHost(string target)
        {
            var table = Actions(target);
            var scope = Scope(target);
            return CompositeSectionViewChecks.Walk(scope, popups: false).OfType<ContentControl>().SingleOrDefault(c => ReferenceEquals(c.Content, table))
                ?? throw new InvalidOperationException("Tabella delle azioni non trovata nella vista: " + target);
        }

        /// <summary>The grid of a table, by its automation name.</summary>
        private ConcreteTableGrid Grid(string target) => target switch
        {
            "barre" => Named<ConcreteTableGrid>(Tab(0), "Barre della sezione"),
            "trefoli" => Named<ConcreteTableGrid>(Tab(0), "Trefoli e cavi"),
            "alternative" => Named<ConcreteTableGrid>(view.DesignView, "Alternative"),
            "dialogo" => throw new ArgumentException("Tabella della finestra fuori dalla finestra."),
            _ => Named<ConcreteTableGrid>(TableHost(target), "Combinazioni")
        };

        /// <summary>
        /// A cell written as the binding of the cell writes it (the row shown by the grid, the column of the grid), then the end of the edit of the row
        /// (the table shows its rows again, queued, as the collection view of the historic grid). A read-only grid or column does nothing.
        /// </summary>
        private void Cell(ConcreteSectionStep step)
        {
            var grid = Grid(step.Target!);
            if (step.Row >= grid.Items.Count || grid.Items[step.Row] is not Anthea.Presentation.Forms.JsonRowViewModel row) return;
            var column = grid.Columns.FirstOrDefault(c => c.SortMemberPath == step.Key);
            if (grid.IsReadOnly || column is null || column.IsReadOnly || !Usable(grid)) return;
            row[step.Key!] = column is DataGridCheckBoxColumn ? step.Text == "true" : step.Text;
            grid.RowCommitted();
        }

        /// <summary>«Incolla» (or «Aggiungi dagli appunti» of the menu of the grid) with the text of the step on the clipboard; a refused paste shows its
        /// message, the message of the step.</summary>
        private string? Paste(ConcreteSectionStep step)
        {
            var grid = Grid(step.Target!);
            bool usable;
            Action press;
            if (step.Key == "aggiungi")
            {
                var item = (grid.ContextMenu?.Items.OfType<MenuItem>() ?? []).SingleOrDefault(m => AutomationName(m) == "Aggiungi dagli appunti")
                    ?? throw new InvalidOperationException("Voce «Aggiungi dagli appunti» non trovata nel menu della tabella.");
                usable = item.IsEnabled && Usable(grid);
                press = () => MenuClickMethod.Invoke(item, [false]);
            }
            else
            {
                var button = Named<Button>(TableHost(step.Target!), "Incolla");
                usable = Usable(button);
                press = () => Click(button);
            }
            if (!usable) return null;
            if (step.Index >= 0 && step.Index < grid.Items.Count) grid.SelectedIndex = step.Index;
            services.Clipboard = step.Text!;
            int shown = services.Messages.Count;
            press();
            return services.Messages.Count > shown ? services.Messages[^1].Title + ": " + services.Messages[^1].Message : null;
        }

        /// <summary>The bar of the filter of a table: column, mode, order and text, each through its control.</summary>
        private void Filter(ConcreteSectionStep step)
        {
            var host = TableHost(step.Target!);
            var column = Named<ComboBox>(host, "Colonna del filtro");
            if (!Usable(column)) return;
            column.SelectedValue = step.Key;
            Named<ComboBox>(host, "Modo del filtro").SelectedIndex = step.Index;
            Named<ComboBox>(host, "Ordine della tabella").SelectedIndex = step.Row;
            Named<TextBox>(host, "Testo del filtro").Text = step.Text ?? "";
        }

        /// <summary>The inner tabs: the families of the serviceability, the details of a family, «Dominio» and «Sezione σ / ε» of a domain panel.</summary>
        private void InnerTab(ConcreteSectionStep step)
        {
            if (step.Target == "sle") { SelectTab(view.StressTabs, Sheet.StressPanels[step.Index].Label); return; }
            if (step.Target!.StartsWith("dettagli-", StringComparison.Ordinal))
            {
                string[] names = ["Riepilogo", "Barre e trefoli", "Calcestruzzo", "Fessurazione · passaggi"];
                var details = Find<TabControl>(StressView(step.Target[9..]), t => t.Items.OfType<TabItem>().Any(i => AutomationName(i) == names[0]), "dettagli della famiglia")
                    ?? throw new InvalidOperationException("Schede dei dettagli non trovate: " + step.Target);
                SelectTab(details, names[step.Index]);
                return;
            }
            if (step.Target.StartsWith("ispezione-", StringComparison.Ordinal))
            {
                var panel = (ConcreteDomainPanelView)Scope(step.Target);
                SelectTab(panel.InnerTabs, new[] { "Dominio", "Sezione σ / ε" }[step.Index]);
                return;
            }
            throw new ArgumentException("Schede sconosciute: " + step.Target);
        }

        // ---------------------------------------------------------------- dialogs

        /// <summary>
        /// A dialog opened with its button: the service of the dialogs shows it from its DataTemplate (off screen) and the steps of the scenario are
        /// done on its controls while it is open; its state after every step is the message of the step. A dialog still open at the end is closed,
        /// as the user closing it.
        /// </summary>
        private string Open(ConcreteSectionStep step)
        {
            var button = step.Target switch
            {
                "sollecitazioni" => Named<Button>(view, "Sollecitazioni…"),
                "materiale" => Named<Button>(Tab(0), "Nuovo materiale CLS"),
                "materiale-acciaio" => Named<Button>(Tab(0), "Nuovo materiale acciaio"),
                "materiale-trefoli" => Named<Button>(Tab(0), "Nuovo materiale trefoli"),
                "wizard" => Named<Button>(Tab(0), ConcreteSectionTrace.WizardCommand),
                _ => throw new ArgumentException("Finestra sconosciuta: " + step.Target)
            };
            if (!Usable(button)) return "finestra non aperta: comando non disponibile";
            var log = new List<string>();
            bool opened = false;
            Exception? failure = null;
            services.Script = (dialog, window) =>
            {
                opened = true;
                try
                {
                    log.Add("aperta · " + DialogState(dialog));
                    foreach (var inner in step.Dialog ?? [])
                    {
                        if (!window.IsVisible) { log.Add("chiusa: passo " + inner + " non eseguito"); continue; }
                        DialogStep(dialog, window, inner);
                        window.UpdateLayout();
                        log.Add(inner + " · " + (window.IsVisible ? DialogState(dialog) : "chiusa"));
                    }
                    if (window.IsVisible) log.Add("chiusa dalla traccia");
                }
                catch (Exception error) { failure = error; }
            };
            try { Click(button); }
            finally { services.Script = null; }
            if (failure is not null) throw new InvalidOperationException("Finestra " + step.Target + ": " + failure.Message, failure);
            if (!opened) throw new InvalidOperationException("Finestra " + step.Target + " non aperta.");
            return string.Join("\n", log);
        }

        private static string DialogState(object dialog) => dialog switch
        {
            ConcreteMaterialDialogViewModel material => ConcreteSectionSheetReader.MaterialState(material),
            ConcreteTemplateWizardViewModel wizard => ConcreteSectionTrace.WizardState(wizard),
            SectionActionsViewModel actions => ConcreteSectionSheetReader.EditorState(actions),
            _ => throw new InvalidOperationException("Finestra non prevista: " + dialog.GetType().Name)
        };

        /// <summary>A step inside a dialog, on its controls: fields and choices of its form by their automation names, buttons by their text, the table
        /// of the actions (selection, a cell written as its binding writes it).</summary>
        private void DialogStep(object dialog, Window window, ConcreteSectionStep step)
        {
            FrameworkElement root = window;
            switch (step.Kind)
            {
                case ConcreteSectionStep.Button:
                    Click(Named<Button>(window, step.Key!));
                    return;
                case ConcreteSectionStep.Field or ConcreteSectionStep.Choice:
                {
                    // The fields of the window: those the user sees and can use (the wizard and the material: visible and enabled; a text not refused
                    // by its field).
                    var form = dialog switch
                    {
                        ConcreteMaterialDialogViewModel material => material.Form,
                        ConcreteTemplateWizardViewModel wizard => wizard.Form,
                        _ => throw new ArgumentException("Campo in una finestra senza form: " + step)
                    };
                    FormStep(form, step, root, (_, field) => field.IsVisible && field.IsEnabled);
                    return;
                }
                case ConcreteSectionStep.Select:
                {
                    var grid = Named<DataGrid>(window, "Sollecitazioni");
                    if (step.Index < grid.Items.Count) grid.SelectedIndex = step.Index;
                    return;
                }
                case ConcreteSectionStep.Cell:
                {
                    // The two-way binding of the cell writes the property of the row.
                    var grid = Named<DataGrid>(window, "Sollecitazioni");
                    if (step.Row < grid.Items.Count) typeof(SectionActionRow).GetProperty(step.Key!)!.SetValue(grid.Items[step.Row], step.Text);
                    return;
                }
                default: throw new ArgumentException("Passo della finestra sconosciuto: " + step.Kind);
            }
        }
    }

    /// <summary>
    /// The services of the sheets in the traces: messages and failures recorded (a refused paste shows its message), the clipboard of the steps, the
    /// dialogs shown as WpfDialogService shows them (the window around the ViewModel, its view from the DataTemplate, closed when the ViewModel asks)
    /// but off screen, never activated, driven by <see cref="Script"/> while they are open; no question and no file expected.
    /// </summary>
    private sealed class TraceServices : IConfirmationService, IDialogService, IFileDialogService, IWikiNavigator, IMessageService, IClipboardService
    {
        internal List<(string Title, string Message)> Messages { get; } = [];
        internal List<(string Title, Exception Error)> Failures { get; } = [];
        internal string Clipboard { get; set; } = "";

        /// <summary>What the user does in the dialog while it is open; null: no dialog expected.</summary>
        internal Action<object, Window>? Script { get; set; }

        internal PresentationServices Create()
        {
            var bench = ModuleHostBench.Services();
            return new PresentationServices(this, this, this, this, this, bench.Links, this);
        }

        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista: dati condivisi.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista: spostamento.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista: chiusura.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista: salvataggio.");
        public bool ConfirmDelete(string title, string question) => throw new InvalidOperationException("Domanda non prevista: " + question);

        public void ReportFailure(string title, Exception error) => Failures.Add((title, error));
        public void Inform(string title, string message) => Messages.Add((title, message));
        public void Warn(string title, string message) => Messages.Add((title, message));

        public void SetText(string text) => Clipboard = text ?? throw new ArgumentNullException(nameof(text));
        public string GetText() => Clipboard;

        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            var script = Script ?? throw new InvalidOperationException("Finestra non prevista: " + dialog.Title);
            var window = new Window
            {
                Title = dialog.Title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000
            };
            // As WpfDialogService: the presentation of the numbers of the owner (the sheet enables it).
            window.SetValue(NumericPresentation.EnabledProperty, true);
            Appearance.Watch(window);
            bool closed = false;
            void Close(object? sender, EventArgs e) { closed = true; window.Close(); }
            dialog.CloseRequested += Close;
            try
            {
                window.Show(); window.UpdateLayout();
                script(dialog, window);
            }
            finally
            {
                dialog.CloseRequested -= Close;
                if (window.IsVisible) window.Close();
            }
            return Task.FromResult(closed ? dialog.Result : default);
        }

        public string? OpenFile(OpenFileRequest request) => throw new InvalidOperationException("Dialogo di apertura non previsto.");
        public string? SaveFile(SaveFileRequest request) => throw new InvalidOperationException("Dialogo di salvataggio non previsto.");
        public string? ChooseFolder(string? title = null) => throw new InvalidOperationException("Cartella non prevista.");
        public void Show(string? uri = null) { }
    }
}
