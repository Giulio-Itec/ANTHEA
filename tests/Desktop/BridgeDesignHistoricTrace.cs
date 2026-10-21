using System.Collections;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Application.BridgeDesign;
using Anthea.Presentation.Modules.BridgeDesign;
using Anthea.Presentation.Tests;

namespace X.Desktop;

/// <summary>
/// --check-bridge-design-historic &lt;radice&gt; &lt;cartella&gt; and --check-bridge-design-historic-capture &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.12,
/// proposal F5 section 7.2): the scenarios of tests/Shared/BridgeDesignTrace.cs on the historic Bridge Design view
/// (<see cref="BridgeDesignWorkspace"/>), created as SheetEditor creates it, on a copy of the data, with the calculation service of the trace
/// (<see cref="HeldModuleService"/>, which can hold the background calculation), off screen; the window of the price editor is kept off screen
/// and never activated (<see cref="OffscreenWindows"/>). Every step is done as the user does it, through the controls: a control that the user
/// cannot use (not on the page or the tab shown, hidden, disabled, or protected by the read-only revision) does nothing. Three steps call what
/// the control calls, because the control hides it: «Avvia ottimizzazione» (its handler awaits <see cref="BridgeDesignWorkspace.RunOptimization"/>,
/// the trace keeps the task to wait for it), «Ponte casuale» (the button draws from the shared generator, the trace from a seeded one) and the
/// click on a point of the cloud (<see cref="BridgeOptimizationPlot.SelectAt"/> at the point of the rank). After the opening the whole state seen
/// by the user is written, then after every step what changed (<see cref="BridgeDesignTrace.Lines"/>). Then the drawing lists (lines, shapes,
/// pens, brushes, texts) of the elevation and of the cross section, of the moment diagram, of the icons of the typologies and of the charts of
/// the search, and the pixels of the figures of the report, for every F3 fixture of the module (disegni/elenco.txt). The capture writes the
/// traces in the folder; the check also compares them with those frozen in tests/fixtures/f5-bridge-design, which the ViewModel tests without
/// WPF compare with the ViewModel. The check stays in the ui stage until the historic view is archived (Q11). Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class BridgeDesignHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        using var windows = new OffscreenWindows();
        var scenarios = BridgeDesignScenarios.All(root);
        string reference = BridgeDesignScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            Console.WriteLine("Traccia Bridge Design: " + scenario.Name);
            var trace = await Trace(root, scenario);
            BridgeDesignTrace.Write(Path.Combine(output, scenario.Name + ".txt"), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? BridgeDesignTrace.FirstDifference(BridgeDesignTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
        }
        string folder = Path.Combine(output, "disegni");
        var drawings = await Drawings(root, folder);
        BridgeDesignTrace.Write(Path.Combine(folder, "elenco.txt"), drawings);
        if (check)
        {
            var names = scenarios.Select(s => s.Name + ".txt").ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt").Select(n => Path.GetFileName(n)).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            string stored = Path.Combine(BridgeDesignScenarios.Drawings(root), "elenco.txt");
            string? difference = File.Exists(stored) ? BridgeDesignTrace.FirstDifference(BridgeDesignTrace.Read(stored), drawings) : "riferimento dei disegni assente";
            if (difference is not null) differences.Add("disegni/elenco.txt: " + difference);
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Vista storica di Bridge Design diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica, {drawings.Count} disegni"
            + (check ? " uguali a quelli congelati" : " catturati"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, BridgeDesignScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(BridgeDesignScenarios.Module);
        var side = new HistoricSide(BridgeDesignScenarios.FixtureData(root, scenario.Fixture), engine);
        try
        {
            // The state of the opening is read at once, before the dispatcher runs anything.
            var previous = side.State();
            lines.AddRange(BridgeDesignTrace.Lines("apertura", null, previous));
            foreach (var step in scenario.Steps)
            {
                await side.Apply(step);
                var current = side.State();
                lines.AddRange(BridgeDesignTrace.Lines(step.ToString(), previous, current));
                previous = current;
            }
        }
        finally { engine.Release(); side.Dispose(); }
        return lines;
    }

    /// <summary>The historic sheet (as SheetEditor hosts it) driven through its controls, as the user does.</summary>
    internal sealed class HistoricSide : IDisposable
    {
        private readonly HeldModuleService engine;
        private readonly Border host = new();
        private Action? release;
        private Task search = Task.CompletedTask;
        internal BridgeDesignWorkspace View { get; private set; } = null!;
        internal int Modified { get; private set; }
        internal string Message { get; private set; } = "";

        internal HistoricSide(JsonObject data, HeldModuleService engine)
        {
            this.engine = engine;
            Open(data);
        }

        /// <summary>
        /// The continuations of the view (the delay of the automatic calculation, the end of the engine, the publication, the search and its
        /// progress) run at the idle priority of the dispatcher: only while the trace waits, never while a step settles. The trace does not
        /// depend on the time a step takes, as the ViewModel without WPF, whose continuations run only when its tests wait.
        /// </summary>
        internal static T WithIdleContinuations<T>(Func<T> action)
        {
            var outer = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher, DispatcherPriority.ApplicationIdle));
            try { return action(); }
            finally { SynchronizationContext.SetSynchronizationContext(outer); }
        }

        private void Open(JsonObject data)
        {
            // SheetEditor: a copy of the data, the view as its content, Modified forwarded.
            View = WithIdleContinuations(() => new BridgeDesignWorkspace((JsonObject)data.DeepClone(), engine));
            Modified = 0;
            View.Modified += () => Modified++;
            host.Child = View;
            Layout();
        }

        internal void Layout() { host.Measure(new Size(1400, 1000)); host.Arrange(new Rect(0, 0, 1400, 1000)); host.UpdateLayout(); }

        internal async Task Settle()
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            Layout();
        }

        internal BridgeDesignTraceState State() => View.TraceForChecks(Modified, Message, release is not null);

        internal async Task Apply(BridgeDesignStep step)
        {
            Message = "";
            // The user opens the page and the tabs that hold the control; the layout lets the protection of a revision reach it.
            if (View.PlaceForChecks(step)) Layout();
            var wait = WithIdleContinuations(() => Start(step));
            if (wait is not null) await wait;
            await Settle();
        }

        /// <summary>The step, done at once; the task to wait for the steps that wait.</summary>
        private Task? Start(BridgeDesignStep step)
        {
            switch (step.Kind)
            {
                case BridgeDesignStep.Commit: return null;
                case BridgeDesignStep.Calculate: return View.CalculateAsync();
                case BridgeDesignStep.Start: _ = View.CalculateAsync(); return null;
                case BridgeDesignStep.Wait: return View.WaitForCalculationAsync();
                case BridgeDesignStep.ReadOnly: release = RevisionInspection.Protect(host); return null;
                case BridgeDesignStep.Reopen:
                    var data = View.Data; release?.Invoke(); release = null; View.Dispose(); Open(data); return null;
                case BridgeDesignStep.Hold: engine.Hold(); return null;
                case BridgeDesignStep.Entered: return engine.EnteredAsync();
                case BridgeDesignStep.Release: engine.Release(); return null;
                case BridgeDesignStep.Visit: Visit(host); return null;
                case BridgeDesignStep.WaitSearch: return search;
                case BridgeDesignStep.Report:
                    try { View.BuildReportDocument("Traccia F5.12"); Message = "report disponibile"; }
                    catch (Exception error) when (error is InvalidOperationException or ArgumentException) { Message = "report non disponibile: " + error.Message; }
                    return null;
                default:
                    var result = View.ApplyForChecks(step);
                    Message = result.Message;
                    if (result.Search is { } started) search = started;
                    return null;
            }
        }

        /// <summary>The visit of the F3 capture (F3EquivalenceCapture.Visit): every expander opened, every page of every tab control chosen.</summary>
        internal static void Visit(FrameworkElement root)
        {
            root.Measure(new Size(1400, 1000)); root.Arrange(new Rect(0, 0, 1400, 1000)); root.UpdateLayout();
            var tabs = new HashSet<TabControl>();
            for (int pass = 0; pass < 8; pass++)
            {
                foreach (var e in Walk(root).OfType<Expander>().ToArray()) e.IsExpanded = true;
                var found = Walk(root).OfType<TabControl>().Where(t => tabs.Add(t)).ToArray(); if (found.Length == 0) break;
                foreach (var tab in found)
                {
                    int selected = tab.SelectedIndex;
                    for (int i = 0; i < tab.Items.Count; i++) { tab.SelectedIndex = i; root.UpdateLayout(); }
                    tab.SelectedIndex = selected;
                }
            }
        }

        static IEnumerable<DependencyObject> Walk(DependencyObject root)
        {
            var pending = new Stack<DependencyObject>(); var seen = new HashSet<DependencyObject>(); pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop(); if (!seen.Add(current)) continue;
                yield return current;
                var children = LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>().ToList();
                if (current is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) children.Add(VisualTreeHelper.GetChild(current, i));
                foreach (var child in children.AsEnumerable().Reverse()) pending.Push(child);
            }
        }

        public void Dispose() { release?.Invoke(); View.Dispose(); }
    }

    // ---------------------------------------------------------------- drawings

    /// <summary>
    /// For every F3 fixture of the module, opened and calculated off screen: the drawing lists of the elevation and of the cross section, of
    /// the moment diagram of the details, and the pixels of the two figures of the report; for the first fixture also the icons of the
    /// typologies and, after a short search, the charts of the search (the cloud, the explored variables one by one, the preview). The full
    /// lists go into <paramref name="output"/>; the returned lines (one per drawing: primitives and hash) are compared with disegni/elenco.txt.
    /// </summary>
    static async Task<List<string>> Drawings(string root, string output)
    {
        Directory.CreateDirectory(output);
        var list = new List<string>();
        bool first = true;
        foreach (string id in BridgeDesignScenarios.Fixtures(root))
        {
            Console.WriteLine("Disegni Bridge Design: " + id);
            var engine = new HeldModuleService(BridgeDesignScenarios.Module);
            var side = new HistoricSide(BridgeDesignScenarios.FixtureData(root, id), engine);
            try
            {
                await side.Settle();
                await side.Apply(new(BridgeDesignStep.Wait));
                await side.Apply(new(BridgeDesignStep.Calculate));
                list.AddRange(await side.View.DrawingsForChecks(id, output, side, first));
                first = false;
            }
            finally { side.Dispose(); }
        }
        return list;
    }

    /// <summary>
    /// Keeps every window of the thread off screen and never activated while the traces run (the window of the price editor): the windows are
    /// created at −20000 (WH_CBT, HCBT_CREATEWND), every later move is brought back off screen (WH_CALLWNDPROC, WM_WINDOWPOSCHANGING), and
    /// activation is refused (HCBT_ACTIVATE). A copy of the hooks of the composite section traces (tests/Desktop/CompositeSectionHistoricTrace.cs).
    /// </summary>
    private sealed class OffscreenWindows : IDisposable
    {
        private const int WH_CBT = 5, WH_CALLWNDPROC = 4, HCBT_CREATEWND = 3, HCBT_ACTIVATE = 5, WS_CHILD = 0x40000000, Position = -20000;
        private const uint WM_WINDOWPOSCHANGING = 0x0046, SWP_NOMOVE = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct CREATESTRUCT { public IntPtr lpCreateParams, hInstance, hMenu, hwndParent; public int cy, cx, y, x, style; public IntPtr lpszName, lpszClass; public int dwExStyle; }
        [StructLayout(LayoutKind.Sequential)]
        private struct CWPSTRUCT { public IntPtr lParam, wParam; public uint message; public IntPtr hwnd; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }
        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        private readonly HookProc cbt, call;
        private readonly IntPtr cbtHook, callHook;
        private readonly HashSet<IntPtr> windows = [];

        internal OffscreenWindows()
        {
            cbt = Cbt; call = Call;
            uint thread = GetCurrentThreadId();
            cbtHook = SetWindowsHookEx(WH_CBT, cbt, IntPtr.Zero, thread);
            callHook = SetWindowsHookEx(WH_CALLWNDPROC, call, IntPtr.Zero, thread);
            if (cbtHook == IntPtr.Zero || callHook == IntPtr.Zero) throw new InvalidOperationException("Agganci delle finestre non installati: " + Marshal.GetLastWin32Error());
        }

        private IntPtr Cbt(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code == HCBT_CREATEWND)
            {
                var pointer = Marshal.ReadIntPtr(lParam);
                var cs = Marshal.PtrToStructure<CREATESTRUCT>(pointer);
                if ((cs.style & WS_CHILD) == 0) { cs.x = Position; cs.y = Position; Marshal.StructureToPtr(cs, pointer, false); windows.Add(wParam); }
            }
            else if (code == HCBT_ACTIVATE && windows.Contains(wParam)) return (IntPtr)1;
            return CallNextHookEx(cbtHook, code, wParam, lParam);
        }

        private IntPtr Call(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                var message = Marshal.PtrToStructure<CWPSTRUCT>(lParam);
                if (message.message == WM_WINDOWPOSCHANGING && windows.Contains(message.hwnd) && message.lParam != IntPtr.Zero)
                {
                    var position = Marshal.PtrToStructure<WINDOWPOS>(message.lParam);
                    if ((position.flags & SWP_NOMOVE) == 0 && (position.x != Position || position.y != Position))
                    { position.x = Position; position.y = Position; Marshal.StructureToPtr(position, message.lParam, false); }
                }
            }
            return CallNextHookEx(callHook, code, wParam, lParam);
        }

        public void Dispose() { UnhookWindowsHookEx(cbtHook); UnhookWindowsHookEx(callHook); }
    }
}

internal sealed partial class BridgeOptimizationPlot
{
    /// <summary>UiTests (F5.12 traces): the data the chart draws.</summary>
    internal (int Trials, int Solutions, BridgeDesignModel.Result? Baseline) DataForChecks => (trials.Length, solutions.Length, baseline);
}

internal sealed partial class BridgeDesignWorkspace
{
    // ---------------------------------------------------------------- steps

    /// <summary>True when the user can reach and use <paramref name="element"/>: in the sheet, on the page and in the tabs shown, visible, enabled.</summary>
    private bool Usable(FrameworkElement? element)
    {
        if (element is null || !element.IsEnabled) return false;
        DependencyObject? node = element;
        while (node is not null)
        {
            if (ReferenceEquals(node, this)) return true;
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
            if (node is TabItem { IsSelected: false }) return false;
            node = LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);
        }
        return false;
    }

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    private static T? Find<T>(object? root, Func<T, bool> match) where T : DependencyObject
    {
        if (root is null) return null;
        var pending = new Stack<object>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is T found && match(found)) return found;
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element).Cast<object>().Reverse()) pending.Push(child);
        }
        return null;
    }

    private static Button? ButtonIn(object? root, string caption) => Find<Button>(root, b => Equals(b.Content, caption));

    /// <summary>UiTests (F5.12 traces): opens the page and the tabs that hold the control of the step; true when something changed.</summary>
    internal bool PlaceForChecks(BridgeDesignStep step)
    {
        var (page, input, output) = BridgeDesignScenarios.PlaceOf(step);
        bool changed = false;
        void Select(TabControl tabs, int? index) { if (index is int i && tabs.SelectedIndex != i) { tabs.SelectedIndex = i; changed = true; } }
        Select(WorkspaceTabs, page);
        Select(Inputs, input);
        Select(Outputs, output);
        return changed;
    }

    /// <summary>UiTests (F5.12 traces): the limit of the search named by the trace.</summary>
    private TextBox LimitForChecks(string key) => key switch
    {
        "campate-min" => optimizationMin, "campate-max" => optimizationMax, "altezza-min" => optimizationMinDepth, "altezza-max" => optimizationDepth,
        "altezza-da" => depthFrom, "altezza-a" => depthTo, "altezza-passo" => depthStep, "pali-da" => pileFrom, "pali-a" => pileTo, "pali-passo" => pileStep,
        "prime" => OptimizationTop, _ => throw new ArgumentException("Limite sconosciuto: " + key)
    };

    /// <summary>UiTests (F5.12 traces): a step of the user on the controls of the view; a control the user cannot use does nothing.</summary>
    internal (string Message, Task? Search) ApplyForChecks(BridgeDesignStep step)
    {
        switch (step.Kind)
        {
            case BridgeDesignStep.Page: if (Usable(WorkspaceTabs)) WorkspaceTabs.SelectedIndex = step.Index; break;
            case BridgeDesignStep.InputTab: if (Usable(Inputs)) Inputs.SelectedIndex = step.Index; break;
            case BridgeDesignStep.OutputTab: if (Usable(Outputs)) Outputs.SelectedIndex = step.Index; break;
            case BridgeDesignStep.Field:
                if (Editors.TryGetValue(step.Key!, out var edit) && Usable(edit) && !edit.IsReadOnly) edit.Text = step.Text;
                break;
            case BridgeDesignStep.Slider:
                if (Editors.TryGetValue(step.Key!, out var owner) && owner.Parent is Grid row && row.Parent is StackPanel panel
                    && panel.Children.OfType<Slider>().SingleOrDefault() is { } slider && Usable(slider))
                    slider.Value = double.Parse(step.Text!, CultureInfo.InvariantCulture);
                break;
            case BridgeDesignStep.Choice:
            {
                string label = BridgeDesignScenarios.Choices.Single(c => c.Key == step.Key).Label;
                var combo = Find<ComboBox>(Inputs, c => AutomationProperties.GetName(c) == label && Usable(c));
                if (combo is not null) combo.SelectedItem = step.Text;
                break;
            }
            case BridgeDesignStep.Option:
            {
                string label = BridgeDesignScenarios.Options.Single(c => c.Key == step.Key).Label;
                if (Find<CheckBox>(Inputs, c => Equals(c.Content, label)) is { } check && Usable(check)) check.IsChecked = check.IsChecked != true;
                break;
            }
            case BridgeDesignStep.Family:
            {
                string name = BridgeDesignModel.Families.Single(f => f.Id == step.Text).Name;
                if (Find<Button>(sectionPanel, b => AutomationProperties.GetName(b) == name) is { } button && Usable(button)) Click(button);
                break;
            }
            case BridgeDesignStep.Button:
            {
                object root = step.Key == "ricerca" ? OptimizationScroll : DesignScroll;
                if (step.Key == "ricerca" && step.Text == "Avvia ottimizzazione")
                    return ("", Usable(optimizationRun) ? RunOptimization() : null);
                if (ButtonIn(root, step.Text!) is { } button && Usable(button)) Click(button);
                break;
            }
            case BridgeDesignStep.Random:
                if (ButtonIn(DesignScroll, "Ponte casuale") is { } random && Usable(random)) Randomize(new Random(step.Index));
                break;
            case BridgeDesignStep.Alternative:
            {
                var buttons = (advice.Content as Panel)?.Children.OfType<Button>().Where(b => !Equals(b.Content, "Ripristina fattori ambientali ordinari")).ToArray() ?? [];
                if (step.Index < buttons.Length && Usable(buttons[step.Index])) Click(buttons[step.Index]);
                break;
            }
            case BridgeDesignStep.Prices: return (PricesForChecks(step.Text!), null);
            case BridgeDesignStep.Print:
            {
                if (Calculation is null) return ("stampa non disponibile", null);
                var page = (Viewbox)PrintPage(1000, 700);
                return ("stampa " + string.Join(" | ", ((Panel)page.Child).Children.OfType<TextBlock>().Select(t => BridgeDesignTrace.Quote(t.Text))), null);
            }
            case BridgeDesignStep.Objective: if (Usable(optimizationObjective)) optimizationObjective.SelectedIndex = step.Index; break;
            case BridgeDesignStep.Lock:
                if (optimizationLocks.TryGetValue(step.Key!, out var lockBox) && Usable(lockBox)) lockBox.IsChecked = lockBox.IsChecked != true;
                break;
            case BridgeDesignStep.Limit:
            {
                var limit = LimitForChecks(step.Key!);
                if (!Usable(limit) || limit.IsReadOnly) break;
                limit.Text = step.Text;
                // Leaving the field: only «Mostra le prime N» reacts (the ranking is shown again).
                limit.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, limit, null) { RoutedEvent = LostKeyboardFocusEvent });
                break;
            }
            case BridgeDesignStep.Rank:
                if (optimizationRanking is { } ranking && Usable(ranking) && step.Index < ranking.Items.Count) ranking.SelectedIndex = step.Index;
                break;
            case BridgeDesignStep.Point:
                if (Usable(OptimizationCloud) && OptimizationCloud.Points.FirstOrDefault(p => p.Rank == step.Index) is { Rank: > 0 } point) OptimizationCloud.SelectAt(point.Point);
                break;
            case BridgeDesignStep.Variable:
            {
                var variable = Find<ComboBox>(OptimizationScroll, c => c.ItemsSource is IEnumerable<string> items && items.Contains("Costo [M€]"));
                if (variable is not null && Usable(variable)) variable.SelectedIndex = step.Index;
                break;
            }
            case BridgeDesignStep.Filter:
            {
                var filter = Find<ComboBox>(OptimizationScroll, c => c.ItemsSource is IEnumerable<string> items && items.Contains("Tutte le tipologie"));
                if (filter is not null && Usable(filter)) filter.SelectedIndex = step.Index;
                break;
            }
            case BridgeDesignStep.Pareto:
                if (Find<CheckBox>(OptimizationScroll, c => Equals(c.Content, "Solo frontiera Pareto")) is { } pareto && Usable(pareto)) pareto.IsChecked = pareto.IsChecked != true;
                break;
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
        return ("", null);
    }

    /// <summary>
    /// «Gestisci prezzario…» with the script of the trace. The window of the editor is modal: the script runs from the dispatcher while
    /// ShowDialog waits, on the ViewModel the window is bound to, then «Applica prezzario» (which closes it) or the closing of the window.
    /// </summary>
    private string PricesForChecks(string script)
    {
        if (ButtonIn(prices.Content, "Gestisci prezzario…") is not { } open || !Usable(open)) return "";
        string? described = null;
        var operation = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            var window = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext is PriceCatalogueViewModel);
            if (window is null) return;
            var editor = (PriceCatalogueViewModel)window.DataContext;
            if (BridgeDesignPriceScripts.Run(script, editor) && editor.ApplyCommand.CanExecute(null)) editor.ApplyCommand.Execute(null);
            described = BridgeDesignPriceScripts.Describe(editor);
            if (window.IsVisible) window.Close();
        }));
        string before = status.Text;
        Click(open);
        if (operation.Status == DispatcherOperationStatus.Pending) operation.Abort();
        return described ?? "prezzario non aperto · stato " + BridgeDesignTrace.Quote(status.Text == before ? "" : status.Text);
    }

    // ---------------------------------------------------------------- state

    /// <summary>UiTests (F5.12 traces): the state of the view, read from its controls without changing them.</summary>
    internal BridgeDesignTraceState TraceForChecks(int modified, string message, bool readOnly)
    {
        var s = new BridgeDesignTraceState();
        s.Data(Data);
        s.Add("modificato", BridgeDesignTrace.Number(modified));
        s.Add("messaggio", message);
        s.Add("calcolo", "occupato " + BridgeDesignTrace.YesNo(Busy) + " · risultato " + (Result is { } result ? BridgeDesignTrace.Hash(result) : "-")
            + " · confronto " + (analysis is null ? "-" : analysis.Comparison is { } comparisonResult ? BridgeDesignTrace.Hash(comparisonResult.Json())
                : analysis.ComparisonError is { } error ? "errore " + BridgeDesignTrace.Quote(error) : "nessuno")
            + " · alternative " + (analysis is null ? "-" : string.Join("|", analysis.Alternatives.Select(a => a.Label))));
        s.Add("stato", BridgeDesignTrace.Quote(status.Text) + (ReferenceEquals(status.Foreground, Appearance.Foreground(Brushes.Firebrick)) ? " · errore" : ""));
        for (int k = 0; k < values.Length; k++) s.Add("metrica." + k, BridgeDesignTrace.Quote(values[k].Text) + " · " + BridgeDesignTrace.Quote(captions[k].Text));
        s.Add("configurazione", BridgeDesignTrace.Quote(configuration.Text));
        s.Add("pagina", BridgeDesignTrace.Number(WorkspaceTabs.SelectedIndex) + " · ingressi " + BridgeDesignTrace.Number(Inputs.SelectedIndex)
            + " · risultati " + BridgeDesignTrace.Number(Outputs.SelectedIndex));
        s.Add("annulla", BridgeDesignTrace.Number(history.Count));
        s.Add("disegno", "sezione " + BridgeDesignTrace.YesNo(Drawing.Section) + " · dati " + (Drawing.Data is null ? "-" : ReferenceEquals(Drawing.Data, Data) ? "foglio" : BridgeDesignTrace.Hash(Drawing.Data))
            + " · risultato " + (Drawing.Result is { } drawn ? BridgeDesignTrace.Hash(drawn.Json()) : "-"));
        s.Add("ingressi", BridgeDesignTrace.Quote(string.Join("|", Inputs.Items.Cast<TabItem>().Select(t => t.Header as string))));
        for (int i = 0; i < Inputs.Items.Count; i++) s.Panel("ingressi." + i, Blocks(((TabItem)Inputs.Items[i]).Content));
        s.Add("risultati", BridgeDesignTrace.Quote(string.Join("|", Outputs.Items.Cast<TabItem>().Select(t => t.Header as string))));
        Panel(s, "pannello.quantita", quantities);
        Panel(s, "pannello.dettagli", detail);
        Panel(s, "pannello.confronto", comparison);
        Panel(s, "pannello.prezzario", prices);
        s.Panel("pannello.ipotesi", Blocks(((TabItem)Outputs.Items[4]).Content));
        Panel(s, "pannello.quote", technical);
        Panel(s, "pannello.consigli", advice);
        // The search of the optimum.
        s.Add("ricerca", "obiettivo " + BridgeDesignTrace.Number(optimizationObjective.SelectedIndex) + " · blocchi "
            + string.Join(" ", BridgeDesignScenarios.Locks.Select(k => k + "=" + BridgeDesignTrace.YesNo(optimizationLocks[k].IsChecked == true)))
            + " · limiti " + string.Join(" ", BridgeDesignScenarios.Limits.Select(k => LimitForChecks(k) is var box ? k + "=" + BridgeDesignTrace.Quote(box.Text) + (box.IsEnabled ? "" : "(spento)") : "")));
        s.Add("ricerca.stato", BridgeDesignTrace.Quote(optimizationStatus.Text));
        s.Add("ricerca.migliore", BridgeDesignTrace.Quote(optimizationBest.Text));
        s.Add("ricerca.selezione", BridgeDesignTrace.Quote(optimizationSelection.Text) + " · rango " + BridgeDesignTrace.Number(selectedSolution + 1));
        s.Add("ricerca.avanzamento", BridgeDesignTrace.Number(optimizationProgress.Value) + "/" + BridgeDesignTrace.Number(optimizationProgress.Maximum));
        // A read-only revision disables the buttons that change the sheet: their own state is read only when the user can use them.
        s.Add("ricerca.comandi", readOnly ? "-" : "avvia " + BridgeDesignTrace.YesNo(optimizationRun.IsEnabled) + " · applica " + BridgeDesignTrace.YesNo(optimizationApply.IsEnabled));
        s.Add("ricerca.risultato", Optimization is not { } found ? "-" : "valutati " + BridgeDesignTrace.Number(found.Evaluated) + " · ammessi " + BridgeDesignTrace.Number(found.Admissible)
            + " · soluzioni " + BridgeDesignTrace.Number(found.Solutions.Length) + " · tentativi " + BridgeDesignTrace.Number(found.Trials.Length)
            + " · riferimento " + BridgeDesignTrace.Hash(found.Baseline.Json()));
        s.Add("ricerca.fonte", optimizationSource is null ? "-" : BridgeDesignTrace.Hash(optimizationSource));
        Panel(s, "pannello.ricerca", optimizationResults);
        Panel(s, "pannello.variazioni", optimizationChanges);
        s.Add("ricerca.anteprima", "sezione " + BridgeDesignTrace.YesNo(OptimizationPreview.Section) + " · dati " + (OptimizationPreview.Data is null ? "-" : BridgeDesignTrace.Hash(OptimizationPreview.Data))
            + " · risultato " + (OptimizationPreview.Result is { } preview ? BridgeDesignTrace.Hash(preview.Json()) : "-"));
        var cloud = OptimizationCloud.DataForChecks;
        s.Add("ricerca.nuvola", "tentativi " + BridgeDesignTrace.Number(cloud.Trials) + " · soluzioni " + BridgeDesignTrace.Number(cloud.Solutions)
            + " · riferimento " + (cloud.Baseline is { } cloudBaseline ? BridgeDesignTrace.Hash(cloudBaseline.Json()) : "-") + " · scelto " + BridgeDesignTrace.Number(OptimizationCloud.SelectedRank)
            + " · tipologia " + (OptimizationCloud.FamilyFilter ?? "-") + " · pareto " + BridgeDesignTrace.YesNo(OptimizationCloud.ParetoOnly));
        var trace = OptimizationTrace.DataForChecks;
        s.Add("ricerca.traccia", "tentativi " + BridgeDesignTrace.Number(trace.Trials) + " · soluzioni " + BridgeDesignTrace.Number(trace.Solutions)
            + " · riferimento " + (trace.Baseline is { } traceBaseline ? BridgeDesignTrace.Hash(traceBaseline.Json()) : "-") + " · variabile " + BridgeDesignTrace.Number(OptimizationTrace.Variable));
        return s;
    }

    private static void Panel(BridgeDesignTraceState s, string name, ContentControl control) => s.Panel(name, control.Content is null ? null : Blocks(control.Content));

    /// <summary>The role of a text as the user reads it: titles and highlighted texts by their weight and size, notes, warnings, errors and accents by their colour.</summary>
    private static string Role(TextBlock text)
    {
        var parts = new List<string>();
        if (text.FontWeight == FontWeights.SemiBold) parts.Add(text.FontSize >= 15 ? "titolo" : "evidenza");
        var brush = text.Foreground;
        if (ReferenceEquals(brush, Appearance.Foreground(Ui.Muted))) parts.Add("nota");
        else if (ReferenceEquals(brush, Appearance.Foreground(Ui.Brush("#895C17")))) parts.Add("avviso");
        else if (ReferenceEquals(brush, Appearance.Foreground(Brushes.Firebrick))) parts.Add("errore");
        else if (ReferenceEquals(brush, Appearance.Foreground(Ui.Blue))) parts.Add("accento");
        return string.Join(" ", parts);
    }

    /// <summary>The content of a panel as blocks, in reading order: texts, tables, buttons, groups, parameters, choices, options, typologies, charts.</summary>
    internal static List<BridgeDesignBlock> Blocks(object? content)
    {
        var blocks = new List<BridgeDesignBlock>();
        void Add(object? node)
        {
            switch (node)
            {
                case null: break;
                case TextBlock text: blocks.Add(new(BridgeDesignBlock.TextKind, text.Text, Role(text))); break;
                case Button button when button.Content is string caption: blocks.Add(new(BridgeDesignBlock.ButtonKind, caption)); break;
                case UniformGrid families when families.Children.Count > 0 && families.Children.Cast<UIElement>().All(c => c is Button { Content: not string }):
                    blocks.Add(new(BridgeDesignBlock.FamiliesKind, string.Join("|", families.Children.Cast<Button>().Select(b => AutomationProperties.GetName(b)
                        + (ReferenceEquals(b.Background, Appearance.Selected) ? "*" : "")))));
                    break;
                case StackPanel parameter when parameter.Children.Count > 0 && parameter.Children[0] is Grid row && row.Children.OfType<TextBox>().FirstOrDefault() is { Tag: string tag } edit:
                {
                    var texts = row.Children.OfType<TextBlock>().ToArray();
                    var slider = parameter.Children.OfType<Slider>().SingleOrDefault();
                    blocks.Add(new(BridgeDesignBlock.FieldKind, edit.Text, Key: tag, Label: texts[0].Text, Unit: texts.Length > 1 ? texts[1].Text : "",
                        Invalid: ReferenceEquals(edit.BorderBrush, Appearance.Outline("#CD5C5C")), Slider: slider?.Value));
                    break;
                }
                case StackPanel choice when choice.Children.Count == 2 && choice.Children[0] is TextBlock label && choice.Children[1] is ComboBox combo:
                    blocks.Add(new(BridgeDesignBlock.ChoiceKind, combo.SelectedItem as string ?? "-",
                        Key: BridgeDesignScenarios.Choices.SingleOrDefault(c => c.Label == label.Text).Key ?? "?", Label: label.Text,
                        Choices: ((IEnumerable)combo.ItemsSource).Cast<object>().Select(o => o.ToString()!).ToArray()));
                    break;
                case CheckBox option:
                    string optionLabel = option.Content as string ?? "";
                    blocks.Add(new(BridgeDesignBlock.OptionKind, BridgeDesignTrace.YesNo(option.IsChecked == true),
                        Key: BridgeDesignScenarios.Options.SingleOrDefault(c => c.Label == optionLabel).Key ?? "?", Label: optionLabel));
                    break;
                case DataGrid grid:
                    blocks.Add(new(BridgeDesignBlock.TableKind, Columns: grid.Columns.Select(c => c.Header as string ?? "").ToArray(),
                        Rows: ((IEnumerable)grid.ItemsSource).Cast<string[]>().ToArray(), Selected: grid.SelectedIndex));
                    break;
                case Expander expander: blocks.Add(new(BridgeDesignBlock.GroupKind, expander.Header as string ?? "", Children: Blocks(expander.Content))); break;
                case BridgeConceptMomentPlot plot: blocks.Add(new(BridgeDesignBlock.ChartKind, "momenti " + (plot.Result is { } r ? BridgeDesignTrace.Hash(r.Json()) : "-"))); break;
                case DockPanel dock:
                    var children = dock.Children.Cast<UIElement>().ToList();
                    foreach (var child in children.Where(c => DockPanel.GetDock(c) == Dock.Top)) Add(child);
                    foreach (var child in children.Where(c => DockPanel.GetDock(c) is not (Dock.Top or Dock.Bottom))) Add(child);
                    foreach (var child in children.Where(c => DockPanel.GetDock(c) == Dock.Bottom)) Add(child);
                    break;
                case Panel panel: foreach (var child in panel.Children) Add(child); break;
                case ContentControl control: Add(control.Content); break;
                case Decorator decorator: Add(decorator.Child); break;
                default: blocks.Add(new(BridgeDesignBlock.TextKind, "<" + node.GetType().Name + ">")); break;
            }
        }
        Add(content);
        return blocks;
    }

    // ---------------------------------------------------------------- drawings

    /// <summary>UiTests (F5.12 drawings): the drawing lists of this view, opened and calculated, and the pixels of the figures of the report.</summary>
    internal async Task<List<string>> DrawingsForChecks(string id, string output, BridgeDesignHistoricTrace.HistoricSide side, bool first)
    {
        var list = new List<string>();
        bool section = Drawing.Section;
        // The drawing is rendered again only when asked (as the buttons «Prospetto» and «Sezione» do).
        Drawing.Section = false; Drawing.InvalidateVisual();
        list.Add(CompositeSectionHistoricTrace.Record(output, id + " prospetto 1000x400", Drawing, 1000, 400));
        Drawing.Section = true; Drawing.InvalidateVisual();
        list.Add(CompositeSectionHistoricTrace.Record(output, id + " sezione 1000x400", Drawing, 1000, 400));
        Drawing.Section = section; Drawing.InvalidateVisual();
        if (Find<BridgeConceptMomentPlot>(detail.Content, _ => true) is { } moments)
        { moments.InvalidateVisual(); list.Add(CompositeSectionHistoricTrace.Record(output, id + " momenti 800x180", moments, 800, 180)); }
        if (Calculation is not null)
        {
            list.Add(id + " figura prospetto · " + CompositeSectionHistoricTrace.Pixels(Drawing.Png(false)));
            list.Add(id + " figura sezione · " + CompositeSectionHistoricTrace.Pixels(Drawing.Png(true)));
        }
        if (!first) return list;
        Inputs.SelectedIndex = 2; await side.Settle();
        foreach (var icon in sectionPanel.Children.OfType<UniformGrid>().SelectMany(g => g.Children.OfType<Button>())
            .Select(b => Find<BridgeFamilyIcon>(b.Content, _ => true)!))
        { icon.InvalidateVisual(); list.Add(CompositeSectionHistoricTrace.Record(output, id + " icona " + icon.Family + " 112x32", icon, 112, 32)); }
        WorkspaceTabs.SelectedIndex = 1; await side.Settle();
        await RunOptimization(new BridgeDesignModel.OptimizationOptions { KeepPier = true, KeepFoundation = true, MinSpans = 3, MaxSpans = 6,
            DepthPercentMin = 100, DepthPercentMax = 115, DepthPercentStep = 15, PilePercentMin = 100, PilePercentMax = 150, PilePercentStep = 25 });
        await side.Settle();
        OptimizationCloud.InvalidateVisual();
        list.Add(CompositeSectionHistoricTrace.Record(output, id + " nuvola 900x310", OptimizationCloud, 900, 310));
        int variable = OptimizationTrace.Variable;
        for (int v = 0; v < 9; v++)
        {
            OptimizationTrace.Variable = v; OptimizationTrace.InvalidateVisual();
            list.Add(CompositeSectionHistoricTrace.Record(output, id + " traccia " + v + " 900x310", OptimizationTrace, 900, 310));
        }
        OptimizationTrace.Variable = variable;
        OptimizationPreview.InvalidateVisual();
        list.Add(CompositeSectionHistoricTrace.Record(output, id + " anteprima 900x265", OptimizationPreview, 900, 265));
        return list;
    }
}
