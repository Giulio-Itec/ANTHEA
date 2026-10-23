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
using System.Windows.Threading;
using Anthea.Presentation.Tests;

namespace X.Desktop;

/// <summary>
/// --check-stratigrafia-historic &lt;radice&gt; &lt;cartella&gt; and --check-stratigrafia-historic-capture &lt;radice&gt; &lt;cartella&gt;
/// (UiTests, F5.6b, proposal F5 section 7.2): the scenarios of tests/Shared/StratigraphyTrace.cs on the historic view of the
/// stratigraphy (<see cref="StratigraphyWorkspace"/> in <see cref="SheetEditor"/>), opened as the window opens it, in a window off
/// screen. Every step is done as the user does it, through the controls: a control that the user cannot use (disabled, or protected by
/// the read-only revision) does nothing. The message boxes of the view are answered by <see cref="MessageBoxHook"/>, off screen; the
/// soil transfer dialog (<see cref="SoilTransferDialog"/>, shared by the soil modules) is driven through its controls while it is open.
/// After the opening and after every step the state seen by the user is written as text: data in memory order, Modified, messages,
/// import button, general fields, actions, layers, fields of the selected layer, status, profile and transfer. The capture writes the
/// traces in the folder; the check also compares them with the traces frozen in tests/fixtures/f5-stratigrafia/storica, which the
/// ViewModel tests without WPF compare with the new sheet. The check stays in the ui stage until the historic view is archived (Q11).
/// Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class StratigraphyHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = StratigraphyScenarios.All(root);
        string reference = StratigraphyScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            var trace = await Trace(root, scenario, Path.Combine(output, "passi.log"));
            StratigraphyTrace.Write(Path.Combine(output, scenario.File), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.File);
            string? difference = File.Exists(stored) ? StratigraphyTrace.FirstDifference(StratigraphyTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.File + ": " + difference);
        }
        if (check)
        {
            var names = scenarios.Select(s => s.File).ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt").Select(n => Path.GetFileName(n)).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Vista storica della stratigrafia diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica della stratigrafia"
            + (check ? " uguali alle tracce congelate" : " catturate"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, StratigraphyScenario scenario, string log)
    {
        var lines = new List<string>();
        using var hook = new MessageBoxHook();
        var sources = scenario.Source is null ? Array.Empty<(string, string, JsonObject, string)>()
            : new[] { (scenario.SourceLabel, StratigraphyScenarios.Module, StratigraphyScenarios.FixtureData(root, scenario.Source), StratigraphyScenarios.SourceId) };
        var data = StratigraphyScenarios.FixtureData(root, scenario.Fixture);
        var guids = new StratigraphyGuids([data, .. sources.Select(s => s.Item3)]);
        var side = new HistoricSide(data, sources, hook);
        try
        {
            lines.AddRange(side.State(null).Lines("apertura", guids));
            foreach (var step in scenario.Steps)
            {
                // The step about to run: a dialog that does not close leaves its name at the end of the log.
                File.AppendAllText(log, scenario.Name + ": " + step + "\n", new UTF8Encoding(false));
                string? error = await side.Apply(step);
                lines.AddRange(side.State(error).Lines(step.ToString(), guids));
            }
        }
        finally { side.Dispose(); }
        return lines;
    }

    /// <summary>The historic sheet in a window off screen, driven through its controls, as the user does.</summary>
    private sealed class HistoricSide : IDisposable
    {
        private const double Offscreen = -20000;
        private readonly (string Label, string Module, JsonObject Data, string Id)[] sources;
        private readonly MessageBoxHook hook;
        private readonly List<string> transfer = [];
        private Window host = null!;
        private SheetEditor editor = null!;
        private int modified, sourcesGiven;
        private (string Module, JsonObject Data, string Name)? copy;
        private JsonObject? replacement;

        internal HistoricSide(JsonObject data, (string, string, JsonObject, string)[] sources, MessageBoxHook hook)
        {
            this.sources = sources; this.hook = hook;
            Open(data);
        }

        private void Open(JsonObject data)
        {
            editor = new SheetEditor(StratigraphyScenarios.Module, data);
            modified = 0;
            editor.Modified += () => modified++;
            editor.SoilSources = () => { sourcesGiven = sources.Length; return sources; };
            editor.ModuleCopyRequested += (module, output, name) => copy = (module, output, name);
            editor.SoilReplacementRequested += output => replacement = output;
            // The message boxes of the view need its window (MessageBox.Show(Window.GetWindow(this), …)); the window stays off screen.
            host = new Window
            {
                Content = editor, Width = 1400, Height = 1000, Left = Offscreen, Top = Offscreen, WindowStartupLocation = WindowStartupLocation.Manual,
                WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false
            };
            host.Show();
            Layout();
        }

        private void Layout() => host.UpdateLayout();

        private void Close()
        {
            host.Content = null; host.Close();
            editor.Dispose();
        }

        internal StratigraphyTraceState State(string? error)
        {
            var state = editor.StratigraphyTraceForChecks(modified);
            state.Messages.AddRange(hook.Take());
            state.Error = error;
            state.Transfer.AddRange(transfer);
            return state;
        }

        internal async Task<string?> Apply(StratigraphyStep step)
        {
            hook.Reset(); transfer.Clear();
            try
            {
                switch (step.Kind)
                {
                    case StratigraphyStep.Commit: editor.Commit(); break;
                    case StratigraphyStep.Calculate: await editor.CalculateAsync(); break;
                    case StratigraphyStep.ReadOnly: editor.InspectRevision(); break;
                    case StratigraphyStep.Reopen:
                        var data = editor.Data; Close(); Open(data); break;
                    case StratigraphyStep.Send or StratigraphyStep.Replace: Transfer(step); break;
                    default: editor.StratigraphyForChecks.ApplyForChecks(step, hook); break;
                }
                return null;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return StratigraphyTrace.Error(error); }
            finally { Layout(); }
        }

        /// <summary>
        /// «Invia / importa terreno da ANTHEA…»: the modal dialog is driven by an operation of the dispatcher, which runs inside its loop.
        /// The dialog keeps the text of its errors; then it is closed and nothing reaches the window.
        /// </summary>
        private void Transfer(StratigraphyStep step)
        {
            var button = editor.StratigraphyTransferForChecks;
            if (!button.IsEnabled) return;
            copy = null; replacement = null; sourcesGiven = -1;
            bool done = false, opened = false, sheetData = false;
            string module = "", status = "";
            Exception? failure = null;
            editor.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (done || System.Windows.Application.Current.Windows.OfType<SoilTransferDialog>().SingleOrDefault() is not { } dialog) return;
                opened = true;
                try { Drive(dialog); }
                catch (Exception error) { failure = error; dialog.Close(); }
            }));
            try { Press(button); }
            finally { done = true; }
            if (failure is not null) throw new InvalidOperationException("Finestra del trasferimento del terreno: " + failure.Message, failure);
            if (!opened) throw new InvalidOperationException("Finestra del trasferimento del terreno non aperta.");
            transfer.AddRange(StratigraphyTrace.Transfer(module, sheetData, sourcesGiven, copy?.Module, copy?.Name, copy?.Data ?? replacement, replacement is not null, status));

            void Drive(SoilTransferDialog dialog)
            {
                module = (string)Private(dialog, "module");
                sheetData = JsonNode.DeepEquals((JsonObject)Private(dialog, "original"), editor.Data);
                if (step.Kind == StratigraphyStep.Replace && step.Key == "progetto") Press(DialogButton(dialog, "Importa dal foglio selezionato"));
                var choice = StratigraphyWorkspace.Controls<ComboBox>(dialog).Single(c => c.ItemsSource is IEnumerable<string> items && items.SequenceEqual(["Medi", "Minimi"]));
                choice.SelectedItem = step.Text;
                if (step.Kind == StratigraphyStep.Send) { dialog.Destination.SelectedValue = step.Key; Press(dialog.SendButton); }
                else { hook.Answer(true); Press(DialogButton(dialog, "Sostituisci il terreno del foglio corrente")); }
                if (!dialog.IsVisible) return;
                status = ((TextBlock)Private(dialog, "status")).Text;
                dialog.Close();
            }
        }

        private static object Private(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
            ?? throw new MissingFieldException(owner.GetType().Name, name);

        private static Button DialogButton(DependencyObject root, string text) => StratigraphyWorkspace.Controls<Button>(root).Single(b => Equals(b.Content, text));

        private static void Press(Button button) { if (button.IsEnabled) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); }

        public void Dispose() => Close();
    }
}

internal sealed partial class SheetEditor
{
    /// <summary>UiTests (F5.6b): the historic stratigraphy view of this sheet.</summary>
    internal StratigraphyWorkspace StratigraphyForChecks => soilWorkspace ?? throw new InvalidOperationException("Foglio senza la vista storica della stratigrafia.");

    /// <summary>UiTests (F5.6b): the «Invia / importa terreno da ANTHEA…» button under the stratigraphy.</summary>
    internal Button StratigraphyTransferForChecks => StratigraphyWorkspace.Controls<Button>(this).Single(b => Equals(b.Content, "Invia / importa terreno da ANTHEA…"));

    /// <summary>UiTests (F5.6b): the state of the stratigraphy sheet for the trace.</summary>
    internal StratigraphyTraceState StratigraphyTraceForChecks(int modifiedCount)
    {
        var state = StratigraphyForChecks.TraceForChecks(Data, modifiedCount);
        var transfer = StratigraphyTransferForChecks;
        state.TransferLabel = (string)transfer.Content; state.TransferUsable = transfer.IsEnabled;
        return state;
    }
}

internal sealed partial class StratigraphyWorkspace
{
    /// <summary>Labels of the general fields (the AutomationProperties.Name of their controls) and their keys.</summary>
    private static readonly Dictionary<string, string> GeneralKeys = new(StringComparer.Ordinal)
    {
        ["Parametri da usare nelle verifiche"] = "scenario", ["Nome della stratigrafia"] = "name", ["Descrizione / ubicazione"] = "description",
        ["Quota altimetrica piano campagna [m] · facoltativa"] = "ground_elevation", ["Falda"] = "water_state",
        ["Profondità falda [m] · positiva verso il basso"] = "water_depth",
    };

    /// <summary>Labels of the fields of the selected layer and their keys; the minima are «minimum.&lt;key&gt;».</summary>
    private static readonly Dictionary<string, string> LayerKeys = BuildLayerKeys();

    private static Dictionary<string, string> BuildLayerKeys()
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Denominazione"] = "name", ["Famiglia prevalente · retino automatico"] = "soil_type",
            ["Comportamento per il trasferimento ai moduli di verifica"] = "behavior", ["Descrizione litologica"] = "description",
            ["Colore del profilo [#RRGGBB]"] = "color", ["Spessore [m]"] = "thickness", ["Qualificazione dei medi"] = "qualification",
            ["Fonte dei medi / pagine"] = "source", ["Note (specificare eventuali qualificazioni diverse per parametro)"] = "notes",
            ["Qualificazione dei minimi"] = "minimum.qualification", ["Fonte dei minimi / pagine"] = "minimum.source", ["Note sui minimi"] = "minimum.notes",
            ["Ultimo strato esteso indefinitamente"] = "infinite", ["Gestisci anche i valori minimi (opzionali)"] = "minimum",
        };
        foreach (var (key, label) in new[] { ("gamma", "Peso naturale γ [kN/m³]"), ("gamma_sat", "Peso dell’unità di volume saturo γsat [kN/m³]"),
            ("phi", "Angolo di resistenza efficace φ′ [°]"), ("c", "Coesione efficace c′ [kPa]"), ("cu", "Resistenza non drenata cu [kPa]") })
        {
            keys[label] = key; keys[label + " · minimo"] = "minimum." + key;
        }
        return keys;
    }

    private const string ImportLabel = "Importa relazione PDF con AI…", MinimaNoteText = "Non assegnati. Non vengono copiati dai medi.";
    private static readonly string[] ActionLabels = ["+ Strato", "Duplica", "Elimina", "↑", "↓"];

    /// <summary>The controls of <paramref name="root"/> of type <typeparamref name="T"/>, in the order of the logical tree.</summary>
    internal static List<T> Controls<T>(DependencyObject root, DependencyObject? skip = null) where T : DependencyObject => Walk(root, skip).OfType<T>().ToList();

    private static IEnumerable<DependencyObject> Walk(DependencyObject root, DependencyObject? skip)
    {
        var pending = new Stack<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (ReferenceEquals(node, skip)) continue;
            yield return node;
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Reverse()) pending.Push(child);
        }
    }

    private static string Label(DependencyObject control) => AutomationProperties.GetName(control);

    private static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    private static StratigraphyControl ReadControl(Control control, IReadOnlyDictionary<string, string> keys)
    {
        string label = control is CheckBox check ? (string)check.Content : Label(control);
        string key = keys.TryGetValue(label, out var found) ? found : "?" + label;
        return control switch
        {
            TextBox box => new("testo", key, label, box.Text, Usable(box) && !box.IsReadOnly),
            ComboBox combo => new("scelta", key, label, combo.SelectedItem as string ?? "-", Usable(combo),
                combo.Items.Cast<object>().Select(i => i.ToString()!).ToArray()),
            CheckBox box => new("opzione", key, label, StratigraphyTrace.YesNo(box.IsChecked == true), Usable(box)),
            _ => throw new InvalidOperationException("Controllo inatteso: " + control.GetType().Name)
        };
    }

    private TextBox? Box(string key, bool general) => Controls<TextBox>(general ? this : fields, general ? fields : null)
        .SingleOrDefault(b => (general ? GeneralKeys : LayerKeys).GetValueOrDefault(Label(b)) == key);

    private ComboBox? Combo(string key, bool general) => Controls<ComboBox>(general ? this : fields, general ? fields : null)
        .SingleOrDefault(b => (general ? GeneralKeys : LayerKeys).GetValueOrDefault(Label(b)) == key);

    private CheckBox? Check(string key) => Controls<CheckBox>(fields).SingleOrDefault(c => LayerKeys.GetValueOrDefault((string)c.Content) == key);

    private Button? ActionButton(string label) => Controls<Button>(this, fields).SingleOrDefault(b => Equals(b.Content, label));

    /// <summary>UiTests (F5.6b traces): a step of the user on the controls of the stratigraphy; a control the user cannot use does nothing.</summary>
    internal void ApplyForChecks(StratigraphyStep step, MessageBoxHook hook)
    {
        static void Type(TextBox? box, string text) { if (box is not null && Usable(box) && !box.IsReadOnly) box.Text = text; }
        static void Choose(ComboBox? combo, string text) { if (combo is not null && Usable(combo)) combo.SelectedItem = text; }
        static void Click(Button? button) { if (button is not null && Usable(button)) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); }
        // The click of the user: the check box changes, then Click (ToggleButton.OnClick).
        static void Toggle(CheckBox? box)
        {
            if (box is null || !Usable(box)) return;
            box.IsChecked = box.IsChecked != true;
            box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, box));
        }
        switch (step.Kind)
        {
            case StratigraphyStep.Field: Type(Box(step.Key!, general: true), step.Text!); break;
            case StratigraphyStep.Choice: Choose(Combo(step.Key!, general: true), step.Text!); break;
            case StratigraphyStep.Select: layers.SelectedIndex = step.Index; break;
            case StratigraphyStep.LayerField: Type(Box(step.Key!, general: false), step.Text!); break;
            case StratigraphyStep.LayerChoice: Choose(Combo(step.Key!, general: false), step.Text!); break;
            case StratigraphyStep.MinimumField: Type(Box("minimum." + step.Key, general: false), step.Text!); break;
            case StratigraphyStep.MinimumChoice: Choose(Combo("minimum." + step.Key, general: false), step.Text!); break;
            case StratigraphyStep.Infinite: Toggle(Check("infinite")); break;
            case StratigraphyStep.Minima:
                if (step.Text is not null) hook.Answer(step.Text == "sì");
                Toggle(Check("minimum"));
                break;
            case StratigraphyStep.Add: Click(ActionButton("+ Strato")); break;
            case StratigraphyStep.Duplicate: Click(ActionButton("Duplica")); break;
            case StratigraphyStep.Remove: Click(ActionButton("Elimina")); break;
            case StratigraphyStep.Up: Click(ActionButton("↑")); break;
            case StratigraphyStep.Down: Click(ActionButton("↓")); break;
            case StratigraphyStep.Import: Click(ActionButton(ImportLabel)); break;
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
    }

    /// <summary>UiTests (F5.6b traces): what the controls show, read without changing them.</summary>
    internal StratigraphyTraceState TraceForChecks(JsonObject sheetData, int modifiedCount)
    {
        var state = new StratigraphyTraceState { Data = sheetData, Modified = modifiedCount };
        var outside = Walk(this, fields).ToList();
        if (outside.OfType<Button>().FirstOrDefault(b => Equals(b.Content, ImportLabel)) is { } import)
        {
            state.ImportVisible = true; state.ImportLabel = ImportLabel; state.ImportEnabled = import.IsEnabled; state.ImportToolTip = import.ToolTip as string ?? "";
        }
        state.General = outside.OfType<Control>().Where(c => c is TextBox or ComboBox && GeneralKeys.ContainsKey(Label(c))).Select(c => ReadControl(c, GeneralKeys)).ToList();
        state.Actions = ActionLabels.Select(label => outside.OfType<Button>().Single(b => Equals(b.Content, label))).Select(b => ((string)b.Content, Usable(b))).ToList();
        state.Layers = layers.Items.Cast<ListBoxItem>().Select(i => i.Content?.ToString() ?? "").ToList();
        state.Selected = layers.SelectedIndex;
        foreach (var node in Walk(fields, null))
        {
            if (node is TextBox or ComboBox or CheckBox) state.Layer.Add(ReadControl((Control)node, LayerKeys));
            else if (node is TextBlock { Text: MinimaNoteText }) state.MinimaNote = MinimaNoteText;
        }
        state.Status = status.Text;
        state.Profile = drawing.Data is not null; state.ProfileSelection = drawing.Selection;
        return state;
    }
}

/// <summary>
/// UiTests (F5.6b): the message boxes of the historic views answered without the screen. A WH_CBT hook of this thread moves every new
/// system dialog (#32770) off screen when it is created and refuses its activation, so the focus never moves; a WH_CALLWNDPROCRET hook
/// sees the end of its WM_INITDIALOG, when caption and text are set: it reads and records them (<see cref="StratigraphyTrace.Message"/>,
/// <see cref="StratigraphyTrace.Question"/>) and presses the answer, for a question the next scripted one (<see cref="Answer"/>; No when
/// none is scripted), for a message OK.
/// </summary>
internal sealed class MessageBoxHook : IDisposable
{
    private const int WH_CBT = 5, WH_CALLWNDPROCRET = 12, HCBT_CREATEWND = 3, HCBT_ACTIVATE = 5, WS_CHILD = 0x40000000, Offscreen = -20000;
    private const int IDCANCEL = 2, IDYES = 6, IDNO = 7, MessageTextId = 0xFFFF;
    private const uint WM_COMMAND = 0x0111, WM_INITDIALOG = 0x0110;

    [StructLayout(LayoutKind.Sequential)]
    private struct CREATESTRUCT { public IntPtr lpCreateParams, hInstance, hMenu, hwndParent; public int cy, cx, y, x, style; public IntPtr lpszName, lpszClass; public int dwExStyle; }
    [StructLayout(LayoutKind.Sequential)]
    private struct CWPRETSTRUCT { public IntPtr lResult, lParam, wParam; public uint message; public IntPtr hwnd; }
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint GetDlgItemText(IntPtr dialog, int id, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr dialog, int id);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    private readonly HookProc cbt, returned;
    private readonly IntPtr cbtHook, returnHook;
    private readonly HashSet<IntPtr> dialogs = [];
    private readonly Queue<bool> answers = new();
    private readonly List<string> shown = [];

    internal MessageBoxHook()
    {
        cbt = Cbt; returned = Returned;
        uint thread = GetCurrentThreadId();
        cbtHook = SetWindowsHookEx(WH_CBT, cbt, IntPtr.Zero, thread);
        returnHook = SetWindowsHookEx(WH_CALLWNDPROCRET, returned, IntPtr.Zero, thread);
        if (cbtHook == IntPtr.Zero || returnHook == IntPtr.Zero) throw new InvalidOperationException("Agganci dei messaggi non installati: " + Marshal.GetLastWin32Error());
    }

    /// <summary>The answer to the next question: Yes or No.</summary>
    internal void Answer(bool yes) => answers.Enqueue(yes);

    /// <summary>Forgets the answers not used by the previous step.</summary>
    internal void Reset() => answers.Clear();

    /// <summary>The messages and questions shown since the last call, in order.</summary>
    internal List<string> Take() { var list = shown.ToList(); shown.Clear(); return list; }

    private static string ClassName(IntPtr hwnd) { var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity); return name.ToString(); }

    private IntPtr Cbt(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code == HCBT_CREATEWND && ClassName(wParam) == "#32770")
        {
            var pointer = Marshal.ReadIntPtr(lParam);
            var cs = Marshal.PtrToStructure<CREATESTRUCT>(pointer);
            if ((cs.style & WS_CHILD) == 0)
            {
                cs.x = Offscreen; cs.y = Offscreen; Marshal.StructureToPtr(cs, pointer, false);
                dialogs.Add(wParam);
            }
        }
        else if (code == HCBT_ACTIVATE && dialogs.Contains(wParam)) return (IntPtr)1;
        return CallNextHookEx(cbtHook, code, wParam, lParam);
    }

    private IntPtr Returned(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = Marshal.PtrToStructure<CWPRETSTRUCT>(lParam);
            if (message.message == WM_INITDIALOG && dialogs.Remove(message.hwnd)) Answer(message.hwnd);
        }
        return CallNextHookEx(returnHook, code, wParam, lParam);
    }

    private void Answer(IntPtr dialog)
    {
        var caption = new StringBuilder(512); GetWindowText(dialog, caption, caption.Capacity);
        var text = new StringBuilder(4096); GetDlgItemText(dialog, MessageTextId, text, text.Capacity);
        if (GetDlgItem(dialog, IDYES) != IntPtr.Zero)
        {
            bool yes = answers.Count > 0 && answers.Dequeue();
            shown.Add(StratigraphyTrace.Question(caption.ToString(), text.ToString(), yes));
            PostMessage(dialog, WM_COMMAND, yes ? IDYES : IDNO, IntPtr.Zero);
        }
        else
        {
            shown.Add(StratigraphyTrace.Message(caption.ToString(), text.ToString()));
            // The only button of a message has the identifier of Cancel: the box returns OK.
            PostMessage(dialog, WM_COMMAND, IDCANCEL, IntPtr.Zero);
        }
    }

    public void Dispose()
    {
        UnhookWindowsHookEx(returnHook); UnhookWindowsHookEx(cbtHook);
    }
}
