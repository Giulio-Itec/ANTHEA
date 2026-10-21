using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Presentation.Tests;
using X.Core;

namespace X.Desktop;

/// <summary>
/// --check-sezione-mista-historic &lt;radice&gt; &lt;cartella&gt; and --check-sezione-mista-historic-capture &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.11,
/// proposal F5 section 7.2): the scenarios of tests/Shared/CompositeSectionTrace.cs on the historic view of the composite bridge section
/// (<see cref="BridgeWorkspace"/>), created as SheetEditor creates it, on a copy of the data, with the calculation service of the trace
/// (<see cref="HeldModuleService"/>, which can hold the background calculation), off screen; the detached window is kept off screen and is
/// never activated (<see cref="OffscreenWindows"/>). Every step is done as the user does it, through the controls: a control that the user
/// cannot use (not on the page shown, hidden, disabled, or protected by the read-only revision) does nothing. After the opening the whole
/// state seen by the user is written, then after every step what changed (<see cref="CompositeSectionTrace.Lines"/>). Then the drawing lists
/// (lines, shapes, pens, brushes, texts) of the section, of the stress diagrams, of the local sketch and of the curves, and the pixels of the
/// figures of the report, for every F3 fixture of the module (disegni/elenco.txt). The capture writes the traces in the folder; the check
/// also compares them with those frozen in tests/fixtures/f5-sezione-mista, which the ViewModel tests without WPF compare with the ViewModel.
/// The check stays in the ui stage until the historic view is archived (Q11). Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class CompositeSectionHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        using var windows = new OffscreenWindows();
        var scenarios = CompositeSectionScenarios.All(root);
        string reference = CompositeSectionScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            Console.WriteLine("Traccia sezione mista: " + scenario.Name);
            var trace = await Trace(root, scenario);
            CompositeSectionTrace.Write(Path.Combine(output, scenario.Name + ".txt"), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? CompositeSectionTrace.FirstDifference(CompositeSectionTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
        }
        string folder = Path.Combine(output, "disegni");
        var drawings = await Drawings(root, folder);
        CompositeSectionTrace.Write(Path.Combine(folder, "elenco.txt"), drawings);
        if (check)
        {
            var names = scenarios.Select(s => s.Name + ".txt").ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt").Select(n => Path.GetFileName(n)).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            string stored = Path.Combine(CompositeSectionScenarios.Drawings(root), "elenco.txt");
            string? difference = File.Exists(stored) ? CompositeSectionTrace.FirstDifference(CompositeSectionTrace.Read(stored), drawings) : "riferimento dei disegni assente";
            if (difference is not null) differences.Add("disegni/elenco.txt: " + difference);
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Vista storica della sezione composta diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica, {drawings.Count} disegni"
            + (check ? " uguali a quelli congelati" : " catturati"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, CompositeSectionScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(CompositeSectionScenarios.Module);
        var side = new HistoricSide(CompositeSectionScenarios.FixtureData(root, scenario.Fixture), engine);
        try
        {
            // The state of the opening is read at once, before the dispatcher runs anything.
            var previous = side.State();
            lines.AddRange(CompositeSectionTrace.Lines("apertura", null, previous));
            foreach (var step in scenario.Steps)
            {
                await side.Apply(step);
                var current = side.State();
                lines.AddRange(CompositeSectionTrace.Lines(step.ToString(), previous, current));
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
        internal BridgeWorkspace View { get; private set; } = null!;
        internal int Modified { get; private set; }
        internal string Message { get; private set; } = "";

        internal HistoricSide(JsonObject data, HeldModuleService engine)
        {
            this.engine = engine;
            Open(data);
        }

        /// <summary>
        /// The continuations of the view (the delay of the automatic calculation, the end of the engine, the publication) run at the
        /// idle priority of the dispatcher: only while the trace waits (a «attendi», «calcola», «attendi-motore» or «calcola-curva»
        /// step), never while a step settles. The trace does not depend on the time a step takes, as the ViewModel without WPF, whose
        /// continuations run only when its tests wait. <paramref name="action"/> runs with that context; the context of the trace is
        /// restored after, so that the trace itself never waits behind the continuations of the view.
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
            View = WithIdleContinuations(() => new BridgeWorkspace((JsonObject)data.DeepClone(), engine));
            View.SetValue(WikiContextHelp.ModuleProperty, CompositeSectionScenarios.Module);
            Modified = 0;
            View.Modified += () => Modified++;
            host.Child = View;
            Layout();
        }

        internal void Layout() { host.Measure(new Size(1400, 1000)); host.Arrange(new Rect(0, 0, 1400, 1000)); host.UpdateLayout(); }

        /// <summary>
        /// The operations queued by the view (rebuilt cards, coefficients of the standard, the table rebuilt at background priority), then
        /// the layout. The continuations of the view wait at idle priority (<see cref="WithIdleContinuations"/>).
        /// </summary>
        internal async Task Settle()
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            Layout();
        }

        internal CompositeSectionTraceState State() => View.TraceForChecks(Modified, Message);

        internal async Task Apply(CompositeSectionStep step)
        {
            Message = "";
            var wait = WithIdleContinuations(() => Start(step));
            if (wait is not null) await wait;
            await Settle();
        }

        /// <summary>The step, done at once; the task to wait for the steps that wait.</summary>
        private Task? Start(CompositeSectionStep step)
        {
            switch (step.Kind)
            {
                case CompositeSectionStep.Commit: View.Commit(); return null;
                case CompositeSectionStep.Calculate: return View.CalculateAsync();
                case CompositeSectionStep.Start: _ = View.CalculateAsync(); return null;
                case CompositeSectionStep.Wait: return View.PendingForChecks;
                case CompositeSectionStep.ReadOnly: release = RevisionInspection.Protect(host); return null;
                case CompositeSectionStep.Reopen:
                    var data = View.Data; release?.Invoke(); release = null; View.Dispose(); Open(data); return null;
                case CompositeSectionStep.Hold: engine.Hold(); return null;
                case CompositeSectionStep.Entered: return engine.EnteredAsync();
                case CompositeSectionStep.Release: engine.Release(); return null;
                case CompositeSectionStep.Visit: Visit(host); return null;
                case CompositeSectionStep.Report:
                    try
                    {
                        var options = step.Text is null ? ReportBridge.DefaultSections() : step.Text.Split(',').ToHashSet();
                        View.BuildReport("Traccia F5.11", options);
                        Message = "report disponibile";
                    }
                    catch (Exception error) when (error is InvalidOperationException or ArgumentException) { Message = "report non disponibile: " + error.Message; }
                    return null;
                case CompositeSectionStep.CurveCalculate: return View.CurveUsableForChecks ? View.CalculateResponseAsync() : null;
                default: View.ApplyForChecks(step); return null;
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
    /// For every F3 fixture of the module, opened and calculated off screen: the drawing lists of the viewport as the sheet shows it, of the
    /// geometry (control panel), of the total stresses and of the contributions of the first and of the last situation, of the local sketch
    /// (when the sheet shows it), of the curves of the section (computed for the fixture that opens on that page), and the pixels of every
    /// figure of the report with all the sections. The full lists go into <paramref name="output"/>; the returned lines (one per drawing:
    /// primitives and hash) are compared with disegni/elenco.txt.
    /// </summary>
    static async Task<List<string>> Drawings(string root, string output)
    {
        Directory.CreateDirectory(output);
        var list = new List<string>();
        foreach (string id in CompositeSectionScenarios.Fixtures(root))
        {
            Console.WriteLine("Disegni sezione mista: " + id);
            var engine = new HeldModuleService(CompositeSectionScenarios.Module);
            var side = new HistoricSide(CompositeSectionScenarios.FixtureData(root, id), engine);
            try
            {
                await side.Settle();
                await side.Apply(new(CompositeSectionStep.Wait));
                await side.Apply(new(CompositeSectionStep.Calculate));
                var view = side.View;
                list.AddRange(await view.DrawingsForChecks(id, output, side));
            }
            finally { side.Dispose(); }
        }
        return list;
    }

    /// <summary>The drawing list of an element rendered at <paramref name="width"/> × <paramref name="height"/>; its size is restored after.</summary>
    internal static string Record(string output, string name, FrameworkElement drawing, int width, int height)
    {
        object w = drawing.ReadLocalValue(FrameworkElement.WidthProperty), h = drawing.ReadLocalValue(FrameworkElement.HeightProperty);
        try { return HorizontalPileHistoricTrace.Record(output, name, drawing, width, height); }
        finally
        {
            if (w == DependencyProperty.UnsetValue) drawing.ClearValue(FrameworkElement.WidthProperty); else drawing.SetValue(FrameworkElement.WidthProperty, w);
            if (h == DependencyProperty.UnsetValue) drawing.ClearValue(FrameworkElement.HeightProperty); else drawing.SetValue(FrameworkElement.HeightProperty, h);
        }
    }

    /// <summary>The pixels of a PNG (size and hash), independent of the encoder.</summary>
    internal static string Pixels(byte[] png)
    {
        var frame = BitmapDecoder.Create(new MemoryStream(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        int stride = frame.PixelWidth * ((frame.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[stride * frame.PixelHeight]; frame.CopyPixels(pixels, stride, 0);
        return frame.PixelWidth.ToString(CultureInfo.InvariantCulture) + "x" + frame.PixelHeight.ToString(CultureInfo.InvariantCulture) + " "
            + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels))[..16];
    }

    /// <summary>
    /// Keeps every window of the thread off screen and never activated while the traces run (the detached view): the windows are created at
    /// −20000 (WH_CBT, HCBT_CREATEWND), every later move is brought back off screen (WH_CALLWNDPROC, WM_WINDOWPOSCHANGING), and activation is
    /// refused (HCBT_ACTIVATE). A copy of the hooks of the RC section traces (tests/Desktop/ConcreteSectionHistoricTrace.cs).
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

internal sealed partial class BridgeWorkspace
{
    private static readonly FieldInfo FormRows = typeof(InputForm).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "rows");

    /// <summary>UiTests (F5.11 traces): the last calculation requested (the automatic one or the one of «Calcola»).</summary>
    internal Task PendingForChecks => scheduledCalculation;

    /// <summary>UiTests (F5.11 traces): «Calcola curva» can be pressed (on the page shown, enabled).</summary>
    internal bool CurveUsableForChecks => Usable(responseRun);

    // ---------------------------------------------------------------- steps

    /// <summary>True when the user can reach and use <paramref name="element"/>: in the sheet (on the page shown), visible up to the sheet, enabled.</summary>
    private bool Usable(FrameworkElement? element)
    {
        if (element is null || !element.IsEnabled) return false;
        DependencyObject? node = element;
        while (node is not null)
        {
            if (ReferenceEquals(node, this)) return true;
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
            node = LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);
        }
        return detachedWindow is not null && IsInside(element, detachedWindow);
    }

    private static bool IsInside(DependencyObject element, DependencyObject root)
    {
        for (DependencyObject? node = element; node is not null; node = LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null))
            if (ReferenceEquals(node, root)) return true;
        return false;
    }

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

    private static T? Find<T>(DependencyObject? root, Func<T, bool> match) where T : DependencyObject
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

    private Button? ButtonIn(DependencyObject? root, string content) => Find<Button>(root, b => Equals(b.Content, content));

    /// <summary>The form named by the trace: a static form by its key, the form or the homogenization of the phase <paramref name="phase"/>, the slab properties, the curves.</summary>
    private InputForm? FormForChecks(string name, int phase)
    {
        switch (name)
        {
            case CompositeSectionScenarios.PhaseForm:
                return phase >= 0 ? phaseInputForms.Where(f => f.Editors.ContainsKey("riferimento_N")).ElementAtOrDefault(phase) : null;
            case CompositeSectionScenarios.HomogenizationForm:
                var phases = Data.Array("fasi");
                return phase >= 0 && phase < phases.Count ? homogenizationForms.FirstOrDefault(h => ReferenceEquals(h.Phase, phases[phase])).Form : null;
            case CompositeSectionScenarios.SlabPropertiesForm: return slabPropertyForm;
            case CompositeSectionScenarios.CurveForm: return ResponseForm;
            default:
                string key = CompositeSectionScenarios.Forms.Single(f => f.Name == name).Key;
                return inputForms.Except(phaseInputForms).SingleOrDefault(f => f.Editors.ContainsKey(key));
        }
    }

    /// <summary>UiTests (F5.11 traces): a step of the user on the controls of the view; a control the user cannot use does nothing.</summary>
    internal void ApplyForChecks(CompositeSectionStep step)
    {
        switch (step.Kind)
        {
            case CompositeSectionStep.Page: if (Usable(Pages)) Pages.SelectedIndex = step.Index; break;
            case CompositeSectionStep.Field:
            {
                var form = FormForChecks(step.Form!, step.Index);
                if (form?.Editors.GetValueOrDefault(step.Key!) is TextBox text && Usable(text) && !text.IsReadOnly) form.Set(step.Key!, step.Text!);
                break;
            }
            case CompositeSectionStep.Choice:
            {
                var form = FormForChecks(step.Form!, step.Index);
                if (form?.Editors.GetValueOrDefault(step.Key!) is ComboBox choice && Usable(choice)) choice.SelectedItem = step.Text;
                break;
            }
            case CompositeSectionStep.Option:
            {
                var form = FormForChecks(step.Form!, step.Index);
                if (form?.Editors.GetValueOrDefault(step.Key!) is CheckBox option && Usable(option)) { option.IsChecked = option.IsChecked != true; Click(option); }
                break;
            }
            case CompositeSectionStep.AddPhase:
                if (ButtonIn(pageInputs[1], "+ Aggiungi fase") is { } add && Usable(add)) Click(add);
                break;
            case CompositeSectionStep.AddShrinkage:
                if (ButtonIn(pageInputs[1], "+ Ritiro") is { } shrinkage && Usable(shrinkage)) Click(shrinkage);
                break;
            case CompositeSectionStep.MovePhase:
            case CompositeSectionStep.DeletePhase:
            {
                if (step.Index < 0 || step.Index >= phaseForms.Children.Count) break;
                var card = phaseForms.Children[step.Index];
                string content = step.Kind == CompositeSectionStep.DeletePhase ? "Elimina" : step.Row < 0 ? "↑" : "↓";
                if (ButtonIn(card, content) is { } button && Usable(button)) Click(button);
                break;
            }
            case CompositeSectionStep.Cell:
            {
                if (!Usable(ActionsTable) || ActionsTable.IsReadOnly || step.Row < 0 || step.Row >= ActionsTable.Rows.Count) break;
                var row = ActionsTable.Rows[step.Row];
                var column = ActionsTable.Columns.Single(c => c.SortMemberPath == step.Key);
                if (column.Visibility != Visibility.Visible || DisabledKinds(column).Contains(row.Values.S("tipo"))) break;
                // The bindings write the row when the cell is committed.
                if (step.Text is null) row[step.Key!] = !(row[step.Key!] is true);
                else row[step.Key!] = step.Text;
                break;
            }
            case CompositeSectionStep.SelectRow: if (Usable(ActionsTable) && step.Row < ActionsTable.Rows.Count) ActionsTable.SelectedIndex = step.Row; break;
            case CompositeSectionStep.TableLoad: ClickIn(Results.Items[0], "+ Carico"); break;
            case CompositeSectionStep.TableShrinkage: ClickIn(Results.Items[0], "+ Ritiro"); break;
            case CompositeSectionStep.TableUp: ClickIn(Results.Items[0], "↑"); break;
            case CompositeSectionStep.TableDown: ClickIn(Results.Items[0], "↓"); break;
            case CompositeSectionStep.TableDelete: ClickIn(Results.Items[0], "Elimina"); break;
            case CompositeSectionStep.Situation:
            {
                // With the view detached the situation is chosen in the window of the view (its choice is bound to StageChoice).
                var choice = detachedWindow is not null ? Find<ComboBox>(detachedWindow, c => ReferenceEquals(BindingOperations.GetBinding(c, ItemsControl.ItemsSourceProperty)?.Source, StageChoice)) : StageChoice;
                if (Usable(choice) && step.Index < choice!.Items.Count) choice.SelectedIndex = step.Index;
                break;
            }
            case CompositeSectionStep.Latest: ClickIn(stageControls, "Mostra tutte le fasi → ultima situazione"); break;
            case CompositeSectionStep.Display: if (Usable(DisplayChoice)) DisplayChoice.SelectedIndex = step.Index; break;
            case CompositeSectionStep.Results: if (Usable(Results)) Results.SelectedIndex = step.Index; break;
            case CompositeSectionStep.Switch: Switch(step.Key!); break;
            case CompositeSectionStep.Scale:
                if (Usable(ConcreteScaleEditor) && !ConcreteScaleEditor.IsReadOnly) { ConcreteScaleEditor.Text = step.Text; CommitStressScale(); }
                break;
            case CompositeSectionStep.DetailSection: if (Usable(DetailSectionChoice)) DetailSectionChoice.SelectedIndex = step.Index; break;
            case CompositeSectionStep.PropertyTab: if (Usable(SectionPropertyTabs) && step.Index < SectionPropertyTabs.Items.Count) SectionPropertyTabs.SelectedIndex = step.Index; break;
            case CompositeSectionStep.EffectiveDetails: ClickIn(analysisSidebar, "Dettagli sezione efficace ↓"); break;
            case CompositeSectionStep.CurvePhase: if (Usable(ResponsePhase) && step.Index < ResponsePhase.Items.Count) ResponsePhase.SelectedIndex = step.Index; break;
            case CompositeSectionStep.CurveFiber: if (Usable(ResponseFiber) && step.Index < ResponseFiber.Items.Count) ResponseFiber.SelectedIndex = step.Index; break;
            case CompositeSectionStep.CurvePoint: if (Usable(ResponsePoint)) ResponsePoint.Value = step.Index; break;
            case CompositeSectionStep.Detach: ClickIn(Viewport, "Stacca vista"); break;
            case CompositeSectionStep.Reattach: detachedWindow?.Close(); break;
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
    }

    private void ClickIn(object? root, string content)
    {
        if (root is DependencyObject node && ButtonIn(node, content) is { } button && Usable(button)) Click(button);
    }

    private void Switch(string key)
    {
        switch (key)
        {
            case "quote": Toggle(GeometryLabels); break;
            case "info-armature": Toggle(RebarLabels); break;
            case "asse-neutro": Toggle(NeutralAxisToggle); break;
            case "scala-auto": Toggle(ConcreteScaleAuto); break;
            case "contorno-sezione": Menu(ContourSectionToggle); break;
            case "contorno-tensioni": Menu(ContourDiagramToggle); break;
            case "limiti-tensioni": Menu(StressLimitsToggle); break;
            case "prospetto": if (Usable(detailExpander)) detailExpander.IsExpanded = !detailExpander.IsExpanded; break;
            default: throw new ArgumentException("Interruttore sconosciuto: " + key);
        }
        void Toggle(CheckBox box) { if (Usable(box)) { box.IsChecked = box.IsChecked != true; Click(box); } }
        // The switches of «Verifiche ▾»: the menu opens from its button.
        void Menu(MenuItem item)
        {
            if (ButtonIn(Viewport, "Verifiche ▾") is not { } button || !Usable(button)) return;
            item.IsChecked = !item.IsChecked; item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
        }
    }

    /// <summary>The kinds of phase whose cells of <paramref name="column"/> are disabled (the triggers of its cell style).</summary>
    private static IReadOnlyList<string> DisabledKinds(DataGridColumn column) => column.CellStyle?.Triggers.OfType<DataTrigger>()
        .Where(t => t.Setters.OfType<Setter>().Any(s => s.Property == IsEnabledProperty && Equals(s.Value, false))).Select(t => (string)t.Value).ToArray() ?? [];

    // ---------------------------------------------------------------- state

    /// <summary>UiTests (F5.11 traces): the state of the view, read from its controls without changing them.</summary>
    internal CompositeSectionTraceState TraceForChecks(int modified, string message)
    {
        var s = new CompositeSectionTraceState();
        s.Data(Data);
        s.Add("modificato", CompositeSectionTrace.Number(modified));
        s.Add("messaggio", message);
        s.Add("calcolo", "occupato " + CompositeSectionTrace.YesNo(Busy) + " · risultato " + (Result is { } result ? CompositeSectionTrace.Hash(result) : "-")
            + " · mostrato " + (DisplayedCalculation is null ? "-" : ReferenceEquals(DisplayedCalculation, Calculation) ? "corrente" : "da aggiornare")
            + " · esportabile " + CompositeSectionTrace.YesNo(Calculation is not null && !Busy));
        s.Add("stato", CompositeSectionTrace.Quote(status.Text));
        s.Add("avviso", resultNotice.Visibility == Visibility.Visible ? CompositeSectionTrace.Quote(resultNotice.Text) : "-");
        s.Add("avvisi", CompositeSectionTrace.Quote(warnings.Text));
        s.Add("panoramica", CompositeSectionTrace.Quote(overview.Text));
        s.Add("riepilogo", CompositeSectionTrace.Quote(summaryCards.Text));
        s.Add("materiali", CompositeSectionTrace.Quote(materialInfo.Text));
        s.Add("altezza", CompositeSectionTrace.Quote(steelHeightInfo?.Text));
        s.Add("soletta", CompositeSectionTrace.Quote(slabLayoutInfo.Text));
        s.Add("metodo", CompositeSectionTrace.Quote(methodNotice.Text));
        s.Add("convenzione", CompositeSectionTrace.Quote(loadConvention.Text));
        s.Add("fasi", CompositeSectionTrace.Quote(phaseCount.Text));
        s.Add("convergenza", CompositeSectionTrace.Quote(details.Text));
        s.Add("pagina", CompositeSectionTrace.Number(Pages.SelectedIndex) + " · titolo " + CompositeSectionTrace.Quote(inputTitle.Text) + " · " + CompositeSectionTrace.Quote(inputSubtitle.Text)
            + " · pannello " + (ReferenceEquals(sidebarHost.Content, analysisSidebar) ? "riepilogo" : "proprietà") + " · tabelle " + CompositeSectionTrace.YesNo(Splits["righe"].Children[1].Visibility == Visibility.Visible)
            + " · staccata " + CompositeSectionTrace.YesNo(detachedWindow is not null));
        s.Add("situazioni", CompositeSectionTrace.Quote(string.Join("|", StageChoice.Items.Cast<object>())) + " · scelta " + CompositeSectionTrace.Number(StageChoice.SelectedIndex)
            + " · visibile " + CompositeSectionTrace.YesNo(stageControls.Visibility == Visibility.Visible) + " · attiva " + CompositeSectionTrace.YesNo(StageChoice.IsEnabled));
        s.Add("vista", CompositeSectionTrace.Number(DisplayChoice.SelectedIndex) + " · visibile " + CompositeSectionTrace.YesNo(DisplayChoice.Visibility == Visibility.Visible)
            + " · risultati " + CompositeSectionTrace.Number(Results.SelectedIndex));
        s.Add("interruttori", "quote " + Check(GeometryLabels) + " · armature " + Check(RebarLabels) + " · asse neutro " + Check(NeutralAxisToggle)
            + " " + CompositeSectionTrace.Quote(NeutralAxisToggle.ToolTip as string) + " · contorno sezione " + CompositeSectionTrace.YesNo(ContourSectionToggle.IsChecked)
            + " · contorno tensioni " + CompositeSectionTrace.YesNo(ContourDiagramToggle.IsChecked) + " · limiti " + CompositeSectionTrace.YesNo(StressLimitsToggle.IsChecked)
            + " · menu " + CompositeSectionTrace.YesNo(ButtonIn(Viewport, "Verifiche ▾")!.IsEnabled));
        s.Add("scala", CompositeSectionTrace.Quote(ConcreteScaleEditor.Text) + " · auto " + Check(ConcreteScaleAuto) + " · visibile " + CompositeSectionTrace.YesNo(concreteScaleControls.Visibility == Visibility.Visible)
            + " · errore " + CompositeSectionTrace.YesNo(ConcreteScaleEditor.ReadLocalValue(Control.BorderBrushProperty) != DependencyProperty.UnsetValue)
            + " · " + CompositeSectionTrace.Quote(ConcreteScaleAuto.ToolTip as string) + " · modificabile " + CompositeSectionTrace.YesNo(!ConcreteScaleEditor.IsReadOnly));
        s.Add("prospetto", "visibile " + CompositeSectionTrace.YesNo(detailExpander.Visibility == Visibility.Visible) + " · aperto " + CompositeSectionTrace.YesNo(detailExpander.IsExpanded)
            + " · sezione " + CompositeSectionTrace.Number(DetailSectionChoice.SelectedIndex) + " " + CompositeSectionTrace.YesNo(DetailSectionChoice.IsEnabled)
            + " · schizzo appoggio " + CompositeSectionTrace.YesNo(DetailSketch.AtSupport));
        var plate = FormForChecks("piastra", -1)!;
        s.Add("contenitori", "piastra " + Shown(plate) + " · nota piastra " + Shown(Sibling(plate)) + " · trasversale " + Shown(Owner(FormForChecks("trasversale", -1)))
            + " · fatica " + Shown(Owner(FormForChecks("fatica", -1))) + " · cassoncino " + Shown(boxInputs) + " · discretizzazione " + Shown(meshControls)
            + " · torsione risultati " + Shown(torsionGroup));
        foreach (var (name, _) in CompositeSectionScenarios.Forms) s.Form(name, FieldStates(FormForChecks(name, -1)));
        var phases = Data.Array("fasi");
        s.Add("schede", CompositeSectionTrace.Number(phaseForms.Children.Count));
        for (int i = 0; i < phaseForms.Children.Count; i++)
        {
            var card = (Expander)phaseForms.Children[i];
            var body = (StackPanel)card.Content;
            var notes = body.Children.OfType<TextBlock>().Select(t => t.Text);
            s.Add("scheda." + i, CompositeSectionTrace.Quote(((TextBlock)card.Header).Text) + " · note " + CompositeSectionTrace.Quote(string.Join("|", notes))
                + " · comandi " + string.Join(" ", ((Panel)body.Children[^1]).Children.OfType<Button>().Select(b => b.Content + (b.IsEnabled ? "" : "(spento)"))));
            s.Form(CompositeSectionScenarios.PhaseForm + "." + i, FieldStates(FormForChecks(CompositeSectionScenarios.PhaseForm, i)));
            var homogenization = i < phases.Count ? homogenizationForms.FirstOrDefault(h => ReferenceEquals(h.Phase, phases[i])) : default;
            s.Form(CompositeSectionScenarios.HomogenizationForm + "." + i, FieldStates(homogenization.Form));
            if (homogenization.Info is not null) s.Add("scheda." + i + ".omogeneizzazione", CompositeSectionTrace.Quote(homogenization.Info.Text));
        }
        s.Add("comandi-fasi", string.Join(" ", new[] { "+ Aggiungi fase", "+ Ritiro" }.Select(b => b + (ButtonIn(pageInputs[1], b)!.IsEnabled ? "" : "(spento)"))));
        // The table of the actions: rows (the phases themselves, in the data), selection, the column of the torque, the disabled cells.
        s.Add("azioni", "righe " + CompositeSectionTrace.Number(ActionsTable.Rows.Count) + " · selezione " + CompositeSectionTrace.Number(ActionsTable.SelectedIndex)
            + " · ΔT " + CompositeSectionTrace.YesNo(ActionsTable.Columns.Single(c => c.SortMemberPath == "T").Visibility == Visibility.Visible)
            + " · bloccata " + CompositeSectionTrace.YesNo(ActionsTable.IsReadOnly)
            + " · stessi oggetti " + CompositeSectionTrace.YesNo(ActionsTable.Rows.Select(r => r.Values).SequenceEqual(phases.OfType<JsonObject>())));
        for (int i = 0; i < ActionsTable.Rows.Count; i++)
        {
            string kind = ActionsTable.Rows[i].Values.S("tipo");
            s.Add("azioni." + i, "spente " + string.Join(",", ActionsTable.Columns.Where(c => DisabledKinds(c).Contains(kind)).Select(c => c.SortMemberPath)));
        }
        s.Add("comandi-azioni", string.Join(" ", new[] { "+ Carico", "+ Ritiro", "↑", "↓", "Elimina" }.Select(b => b + (ButtonIn((DependencyObject)Results.Items[0], b)!.IsEnabled ? "" : "(spento)"))));
        s.Panel("pannello.tensioni", Blocks(stressTable.Content));
        s.Panel("pannello.verifiche", Blocks(verificationTable.Content));
        s.Panel("pannello.omogeneizzazione", Blocks(propertiesTable.Content));
        s.Panel("pannello.equilibrio", Blocks(phaseTable.Content));
        s.Panel("pannello.classe4", Blocks(classTable.Content));
        s.Panel("pannello.geometria", Blocks(geometryTable.Content));
        s.Panel("pannello.taglio", Blocks(shearResults.Content));
        s.Panel("pannello.pioli", Blocks(studResults.Content));
        s.Panel("pannello.torsione", Blocks(torsionResults.Content));
        // The properties of the section (the side panel of the control panel).
        s.Add("proprieta", CompositeSectionTrace.Quote(string.Join("|", SectionPropertyTabs.Items.Cast<TabItem>().Select(t => t.Header as string))) + " · scelta "
            + CompositeSectionTrace.Number(SectionPropertyTabs.SelectedIndex) + " · nota " + CompositeSectionTrace.Quote(propertyNotice.Text));
        s.Panel("proprieta.acciaio", Blocks(steelPropertyBody.Content));
        s.Form(CompositeSectionScenarios.SlabPropertiesForm, FieldStates(slabPropertyForm));
        s.Panel("proprieta.soletta", Blocks(slabPropertyBody.Content));
        for (int i = 2; i < SectionPropertyTabs.Items.Count; i++) s.Panel("proprieta.fase." + (i - 2), Blocks(((TabItem)SectionPropertyTabs.Items[i]).Content));
        // The drawing and the local sketch as data.
        var shown = DisplayedCalculation;
        s.Add("disegno", "modo " + CompositeSectionTrace.Number(Drawing.Mode) + " · da aggiornare " + CompositeSectionTrace.YesNo(Drawing.IsStale)
            + " · quote " + CompositeSectionTrace.YesNo(Drawing.ShowGeometryLabels) + " · armature " + CompositeSectionTrace.YesNo(Drawing.ShowRebarLabels)
            + " · asse neutro " + CompositeSectionTrace.YesNo(Drawing.ShowNeutralAxis) + " · contorni " + CompositeSectionTrace.YesNo(Drawing.ContourSection) + CompositeSectionTrace.YesNo(Drawing.ContourDiagram)
            + " · limiti " + CompositeSectionTrace.YesNo(Drawing.ShowStressLimits) + " · CLS × " + CompositeSectionTrace.Number(Drawing.ConcreteAmplification)
            + " · appoggio " + CompositeSectionTrace.YesNo(Drawing.DetailAtSupport));
        s.Add("disegno.geometria", (Drawing.Geometry is null ? "-" : ReferenceEquals(Drawing.Geometry, shown?.Geometry) ? "risultato" : "anteprima") + " " + CompositeSectionTrace.HashObject(Drawing.Geometry));
        s.Add("disegno.ingresso", Drawing.Input is null ? "-" : (ReferenceEquals(Drawing.Input, shown?.Input) ? "risultato " : "anteprima ") + CompositeSectionTrace.Hash(Drawing.Input));
        s.Add("disegno.fase", Drawing.Stage is null ? "-" : CompositeSectionTrace.Number(shown?.Stages.IndexOf(Drawing.Stage) ?? -1) + " " + CompositeSectionTrace.Quote(Drawing.Stage.Name));
        s.Add("disegno.carichi", string.Join(" | ", Drawing.LoadPoints.Select(p => CompositeSectionTrace.Number(p.Index) + " " + CompositeSectionTrace.Quote(p.Name) + " "
            + CompositeSectionTrace.Number(p.Y) + " " + CompositeSectionTrace.Number(p.Force))));
        // The page of the curves.
        s.Form(CompositeSectionScenarios.CurveForm, FieldStates(ResponseForm));
        s.Add("curva", "fasi " + CompositeSectionTrace.Quote(string.Join("|", ResponsePhase.Items.Cast<object>())) + " · scelta " + CompositeSectionTrace.Number(ResponsePhase.SelectedIndex)
            + " · visibile " + CompositeSectionTrace.YesNo(ResponsePhase.Visibility == Visibility.Visible) + " · calcola " + CompositeSectionTrace.YesNo(responseRun.IsEnabled)
            + " · esporta " + CompositeSectionTrace.YesNo(responseExport.IsEnabled) + " · curva " + (ResponseCalculation is null ? "-" : CompositeSectionTrace.Number(ResponseCalculation.Points.Count) + " punti"));
        s.Add("curva.avviso", CompositeSectionTrace.Quote(responseNotice.Text));
        s.Add("curva.punto", CompositeSectionTrace.Quote(responsePointInfo.Text) + " · cursore " + CompositeSectionTrace.Number(ResponsePoint.Value) + "/" + CompositeSectionTrace.Number(ResponsePoint.Maximum));
        s.Add("curva.fibre", CompositeSectionTrace.Quote(string.Join("|", ResponseFiber.Items.Cast<object>())) + " · scelta " + CompositeSectionTrace.Number(ResponseFiber.SelectedIndex));
        s.Add("curva.grafico", CompositeSectionTrace.Quote(responsePlot.XLabel) + " " + CompositeSectionTrace.Quote(responsePlot.YLabel) + " · punti " + CompositeSectionTrace.Number(responsePlot.Points.Length)
            + " " + CompositeSectionTrace.HashObject(responsePlot.Points.Select(p => new[] { p.X, p.Y })) + " · scelto " + CompositeSectionTrace.Number(responsePlot.Selected));
        s.Add("curva.fibra", CompositeSectionTrace.Quote(fiberPlot.XLabel) + " " + CompositeSectionTrace.Quote(fiberPlot.YLabel) + " · punti " + CompositeSectionTrace.Number(fiberPlot.Points.Length)
            + " " + CompositeSectionTrace.HashObject(fiberPlot.Points.Select(p => new[] { p.X, p.Y })) + " · scelto " + CompositeSectionTrace.Number(fiberPlot.Selected));
        s.Panel("curva.tabella", Blocks(responseTable.Content));
        return s;
    }

    private static string Check(CheckBox box) => CompositeSectionTrace.YesNo(box.IsChecked == true) + (box.Visibility == Visibility.Visible ? "" : "(nascosto)") + (box.IsEnabled ? "" : "(spento)");

    private static string Shown(UIElement? element) => element is null ? "-" : CompositeSectionTrace.YesNo(element.Visibility == Visibility.Visible);

    /// <summary>The element after <paramref name="element"/> in its panel.</summary>
    private static UIElement? Sibling(UIElement element) => LogicalTreeHelper.GetParent(element) is Panel panel && panel.Children.IndexOf(element) is var i && i >= 0 && i + 1 < panel.Children.Count
        ? panel.Children[i + 1] : null;

    /// <summary>The expander that holds <paramref name="element"/>.</summary>
    private static Expander? Owner(DependencyObject? element)
    {
        for (var node = element is null ? null : LogicalTreeHelper.GetParent(element); node is not null; node = LogicalTreeHelper.GetParent(node))
            if (node is Expander expander) return expander;
        return null;
    }

    private static List<CompositeSectionFieldState>? FieldStates(InputForm? form)
    {
        if (form is null) return null;
        var rows = (Dictionary<string, List<FrameworkElement>>)FormRows.GetValue(form)!;
        return form.Editors.Select(pair =>
        {
            // The first element of the row is its label: InputForm.Enable and ShowField change every element of the row, while the
            // protection of the revision disables or locks only the editors.
            var label = rows[pair.Key][0];
            string name = AutomationProperties.GetName(pair.Value);
            bool enabled = label.IsEnabled, dimmed = label.Opacity < 1, visible = label.Visibility == Visibility.Visible;
            return pair.Value switch
            {
                TextBox text => new CompositeSectionFieldState(pair.Key, "campo", name, form.Get(pair.Key), null, enabled, dimmed, visible, !text.IsReadOnly && text.IsEnabled),
                ComboBox choice => new CompositeSectionFieldState(pair.Key, "scelta", name, form.Get(pair.Key), ((IEnumerable)choice.ItemsSource).Cast<object>().Select(o => o.ToString()!).ToArray(),
                    enabled, dimmed, visible, choice.IsEnabled),
                CheckBox option => new CompositeSectionFieldState(pair.Key, "opzione", name, CompositeSectionTrace.YesNo(option.IsChecked == true), null, enabled, dimmed, visible, option.IsEnabled),
                _ => throw new InvalidOperationException("Editor non previsto: " + pair.Value.GetType().Name)
            };
        }).ToList();
    }

    /// <summary>The content of a panel of results as blocks, in reading order: texts, tables, groups.</summary>
    internal static List<CompositeSectionBlock> Blocks(object? content)
    {
        var blocks = new List<CompositeSectionBlock>();
        void Add(object? node)
        {
            switch (node)
            {
                case null: break;
                case TextBlock text: blocks.Add(new(CompositeSectionBlock.TextKind, text.Text)); break;
                case TextBox box: blocks.Add(new(CompositeSectionBlock.TextKind, box.Text)); break;
                case DataGrid grid:
                    blocks.Add(new(CompositeSectionBlock.TableKind, "", grid.Columns.Select(c => c.Header as string ?? "").ToArray(),
                        ((IEnumerable)grid.ItemsSource).Cast<string[]>().ToArray()));
                    break;
                case Expander expander: blocks.Add(new(CompositeSectionBlock.GroupKind, (expander.Header as TextBlock)?.Text ?? expander.Header as string ?? "", Children: Blocks(expander.Content))); break;
                case DockPanel dock:
                    var children = dock.Children.Cast<UIElement>().ToList();
                    foreach (var child in children.Where(c => DockPanel.GetDock(c) == Dock.Top)) Add(child);
                    foreach (var child in children.Where(c => DockPanel.GetDock(c) is not (Dock.Top or Dock.Bottom))) Add(child);
                    foreach (var child in children.Where(c => DockPanel.GetDock(c) == Dock.Bottom)) Add(child);
                    break;
                case Panel panel: foreach (var child in panel.Children) Add(child); break;
                case ContentControl control: Add(control.Content); break;
                case Decorator decorator: Add(decorator.Child); break;
                default: blocks.Add(new(CompositeSectionBlock.TextKind, "<" + node.GetType().Name + ">")); break;
            }
        }
        Add(content);
        return blocks;
    }

    // ---------------------------------------------------------------- drawings

    /// <summary>UiTests (F5.11 drawings): the drawing lists of this view, opened and calculated, and the pixels of the figures of the report.</summary>
    internal async Task<List<string>> DrawingsForChecks(string id, string output, CompositeSectionHistoricTrace.HistoricSide side)
    {
        var list = new List<string>();
        list.Add(CompositeSectionHistoricTrace.Record(output, id + " vista 1000x640", Drawing, 1000, 640));
        if (detailExpander.Visibility == Visibility.Visible)
            list.Add(CompositeSectionHistoricTrace.Record(output, id + " prospetto 800x165", DetailSketch, 800, 165));
        if (Pages.SelectedIndex == 2 && Usable(responseRun))
        {
            await CalculateResponseAsync(); await side.Settle();
            list.Add(CompositeSectionHistoricTrace.Record(output, id + " curva 700x420", responsePlot, 700, 420));
            list.Add(CompositeSectionHistoricTrace.Record(output, id + " fibra 700x420", fiberPlot, 700, 420));
        }
        if (Calculation is not { } result) return list;
        Pages.SelectedIndex = 0; await side.Settle();
        list.Add(CompositeSectionHistoricTrace.Record(output, id + " geometria 1000x640", Drawing, 1000, 640));
        Pages.SelectedIndex = 1; await side.Settle();
        foreach (int stage in new[] { 0, result.Stages.Count - 1 }.Distinct())
        {
            StageChoice.SelectedIndex = stage;
            foreach (int display in new[] { 0, 1 })
            {
                DisplayChoice.SelectedIndex = display; await side.Settle();
                list.Add(CompositeSectionHistoricTrace.Record(output, $"{id} situazione {stage + 1} vista {display} 1000x640", Drawing, 1000, 640));
            }
        }
        foreach (var figure in ModuleFigureRenderer.BridgeFigures(result, viewSettings, ReportBridge.DefaultSections()))
            list.Add($"{id} figura {figure.Category} {CompositeSectionTrace.Quote(figure.Caption)} · {CompositeSectionHistoricTrace.Pixels(figure.Png)}");
        return list;
    }
}
