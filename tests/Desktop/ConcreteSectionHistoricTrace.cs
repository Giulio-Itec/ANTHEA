using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Concrete;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Tests;
using X.Core;

namespace X.Desktop;

/// <summary>
/// --check-sezione-historic &lt;radice&gt; &lt;cartella&gt; and --check-sezione-historic-capture &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.10,
/// proposal F5 section 7.2): the scenarios of tests/Shared/ConcreteSectionTrace.cs on the historic view of the RC section
/// (<see cref="ConcreteWorkspace"/> inside <see cref="SheetEditor"/>), opened as the window opens it, in a window off screen. Every step
/// is done as the user does it, through the controls: a control that the user cannot use (disabled, hidden, or protected by the
/// read-only revision) does nothing. The modal dialogs of the view (the editor of the actions, the new material) are driven through their
/// controls while they are open; every window of the thread stays off screen and is never activated (<see cref="OffscreenWindows"/>).
/// After the opening and after every step the state seen by the user is written as text, the common part and the parts of the
/// sub-steps of the scenario. Then the drawings: for every F3 fixture of the module, the display lists of the 2D views (section,
/// stress maps, shear, inspection, strain plane, 2D domain, M–χ) at fixed sizes and the plain data of the 3D domain. The capture writes
/// traces and drawings in the folder; the check also compares them with those frozen in tests/fixtures/f5-sezione (the traces line by
/// line, the drawings through their hashes in disegni/elenco.txt, the 3D data in disegni/domini-3d.txt). The check stays in the ui stage
/// until the historic view is archived (Q11). Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class ConcreteSectionHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = ConcreteSectionScenarios.All(root);
        string reference = ConcreteSectionScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        using var windows = new OffscreenWindows();
        foreach (var scenario in scenarios)
        {
            var trace = await Trace(root, scenario, Path.Combine(output, "passi.log"));
            ConcreteSectionTrace.Write(Path.Combine(output, "storica", scenario.File), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.File);
            string? difference = File.Exists(stored) ? ConcreteSectionTrace.FirstDifference(ConcreteSectionTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.File + ": " + difference);
        }
        var (drawings, three) = await Drawings(root, Path.Combine(output, "disegni"));
        ConcreteSectionTrace.Write(Path.Combine(output, "disegni", "elenco.txt"), drawings);
        ConcreteSectionTrace.Write(Path.Combine(output, "disegni", "domini-3d.txt"), three);
        if (check)
        {
            var names = scenarios.Select(s => s.File.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt", SearchOption.AllDirectories)
                .Select(n => Path.GetRelativePath(reference, n).Replace('\\', '/')).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            foreach (var (name, actual) in new[] { ("elenco.txt", drawings), ("domini-3d.txt", three) })
            {
                string stored = Path.Combine(ConcreteSectionScenarios.Drawings(root), name);
                string? difference = File.Exists(stored) ? ConcreteSectionTrace.FirstDifference(ConcreteSectionTrace.Read(stored), actual) : "riferimento dei disegni assente";
                if (difference is not null) differences.Add("disegni/" + name + ": " + difference);
            }
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Vista storica della sezione c.a. diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica della sezione c.a., "
            + $"{drawings.Count} disegni 2D, {three.Count} righe dei domini 3D" + (check ? " uguali a quelli congelati" : " catturati"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, ConcreteSectionScenario scenario, string log)
    {
        var lines = new List<string>();
        var data = ConcreteSectionScenarios.FixtureData(root, scenario.Fixture);
        var guids = new ConcreteGuids(data);
        var memory = new ConcreteDataMemory();
        var side = new HistoricSide(data);
        try
        {
            await side.Layout();
            lines.AddRange(side.State(scenario.Sections, null).Lines("apertura", guids, memory));
            foreach (var step in scenario.Steps)
            {
                // The step about to run: a dialog that does not close leaves its name at the end of the log.
                File.AppendAllText(log, scenario.File + ": " + step + "\n", new UTF8Encoding(false));
                string? message = await side.Apply(step);
                lines.AddRange(side.State(scenario.Sections, message).Lines(step.ToString(), guids, memory));
            }
        }
        finally { await side.DisposeAsync(); }
        return lines;
    }

    /// <summary>The historic sheet in a window off screen, driven through its controls, as the user does.</summary>
    private sealed class HistoricSide
    {
        private const double Offscreen = -20000;
        private Window host = null!;
        private SheetEditor editor = null!;
        private int modified;
        private SemaphoreSlim? held;
        private Task? started;

        internal HistoricSide(JsonObject data) => Open(data);

        private ConcreteWorkspace Sheet => editor.ConcreteForChecks;

        private void Open(JsonObject data)
        {
            editor = new SheetEditor(ConcreteSectionScenarios.Module, data);
            modified = 0;
            editor.Modified += () => modified++;
            host = new Window
            {
                Content = editor, Width = 1400, Height = 1000, Left = Offscreen, Top = Offscreen, WindowStartupLocation = WindowStartupLocation.Manual,
                WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false
            };
            host.Show();
            host.UpdateLayout();
        }

        /// <summary>The layout of the window, as after a step of the user (the dispatcher is not run: Loaded and the queued work wait).</summary>
        internal Task Layout() { host.UpdateLayout(); return Task.CompletedTask; }

        internal ConcreteSectionTraceState State(IReadOnlyList<string> sections, string? message) => Sheet.TraceForChecks(sections, modified, message);

        internal Task Draw(string id, string output, List<string> list, List<string> three, bool allContours) =>
            Sheet.DrawForChecks(id, output, list, three, allContours, () => host.UpdateLayout());

        internal async Task<string?> Apply(ConcreteSectionStep step)
        {
            try
            {
                switch (step.Kind)
                {
                    case ConcreteSectionStep.Commit: editor.Commit(); return null;
                    case ConcreteSectionStep.Calculate: await editor.CalculateAsync(); return null;
                    case ConcreteSectionStep.Start: started = editor.CalculateAsync(); return null;
                    case ConcreteSectionStep.Wait:
                        if (started is not null) { await started.WaitAsync(TimeSpan.FromMinutes(3)); started = null; }
                        await Sheet.SettleForChecks(); return null;
                    case ConcreteSectionStep.ReadOnly: editor.InspectRevision(); return null;
                    case ConcreteSectionStep.Reopen:
                        var data = editor.Data; Close(); Open(data); return null;
                    case ConcreteSectionStep.Hold:
                        held = Sheet.BatchGateForChecks; await held.WaitAsync(); return null;
                    case ConcreteSectionStep.Entered: await Sheet.EnteredForChecks(); return null;
                    case ConcreteSectionStep.Release: held?.Release(); held = null; return null;
                    case ConcreteSectionStep.Report: return Report(step.Text!);
                    case ConcreteSectionStep.Visit: Visit(editor); return null;
                    default: return Sheet.ApplyForChecks(step);
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return error.GetType().Name + ": " + error.Message; }
            finally { host.UpdateLayout(); }
        }

        /// <summary>
        /// The two report commands of the window, without their dialogs: «Report Word» (MainWindow.ExportReport: its check, then
        /// SheetEditor.ExportReport) with the sections of the F3 capture, and «Report short» (MainWindow.ExportShortReport); only whether the
        /// report is available, or the message the window shows, is written.
        /// </summary>
        private string Report(string kind)
        {
            try
            {
                if (kind == "breve") { editor.Commit(); _ = editor.BuildShortReport("Traccia F5.10 breve"); return "report breve disponibile"; }
                editor.Commit();
                if (!editor.HasResults || editor.Busy) return "report completo: Attendere l’aggiornamento automatico e correggere gli eventuali dati non validi.";
                var options = ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet();
                _ = editor.BuildReport("Traccia F5.10", options);
                return "report completo disponibile";
            }
            catch (Exception error) { return "report " + kind + ": " + error.Message; }
        }

        private void Close()
        {
            host.Content = null; host.Close();
            editor.Dispose();
        }

        internal async Task DisposeAsync()
        {
            held?.Release(); held = null;
            if (started is not null) try { await started.WaitAsync(TimeSpan.FromMinutes(1)); } catch (Exception) { }
            Close();
        }
    }

    /// <summary>The visit of the F3 capture (F3EquivalenceCapture.Visit): every expander opened, every tab of every tab control selected and restored.</summary>
    static void Visit(FrameworkElement editor)
    {
        editor.UpdateLayout();
        var tabs = new HashSet<TabControl>();
        for (int pass = 0; pass < 8; pass++)
        {
            foreach (var e in Walk(editor).OfType<Expander>().ToArray()) e.IsExpanded = true;
            var found = Walk(editor).OfType<TabControl>().Where(t => tabs.Add(t)).ToArray(); if (found.Length == 0) break;
            foreach (var tab in found)
            {
                int selected = tab.SelectedIndex;
                for (int i = 0; i < tab.Items.Count; i++) { tab.SelectedIndex = i; editor.UpdateLayout(); }
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

    // ---------------------------------------------------------------- drawings

    /// <summary>
    /// For every F3 fixture of the module, opened and calculated in a window off screen: the display lists (lines, shapes, pens, brushes,
    /// texts) of the section of the control panel, of the stress map of every serviceability family with a calculated row, of the section of
    /// the shear tab, of the section σ/ε and the strain plane of the 3D domain (limit point and applied action), of the 2D domain and of
    /// the M–χ curve, at fixed sizes; for two fixtures also the eight contours of the characteristic stress map. Then the plain data of the
    /// 3D domain of every fixture (mesh, points, selection, options). The full lists go into <paramref name="output"/>; the returned lines
    /// (one per drawing: primitives and hash) are compared with disegni/elenco.txt.
    /// </summary>
    static async Task<(List<string> Drawings, List<string> Three)> Drawings(string root, string output)
    {
        Directory.CreateDirectory(output);
        var list = new List<string>(); var three = new List<string>();
        foreach (string id in ConcreteSectionScenarios.Fixtures(root))
        {
            var side = new HistoricSide(ConcreteSectionScenarios.FixtureData(root, id));
            try
            {
                await side.Layout();
                await side.Apply(new(ConcreteSectionStep.Wait));
                await side.Apply(new(ConcreteSectionStep.Calculate));
                await side.Apply(new(ConcreteSectionStep.Wait));
                await side.Draw(id, output, list, three, allContours: id is "default" or "sle-fessurazione-assi");
            }
            finally { await side.DisposeAsync(); }
        }
        return (list, three);
    }

    private static readonly MethodInfo RenderMethod = typeof(DrawingView).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(DrawingContext), typeof(Size)], null)
        ?? throw new MissingMethodException(nameof(DrawingView), "Render");

    /// <summary>The display list of a drawing view rendered at <paramref name="width"/> × <paramref name="height"/>, as its Png does.</summary>
    internal static string Record(string output, string name, DrawingView view, int width, int height)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open()) RenderMethod.Invoke(view, [dc, new Size(width, height)]);
        var lines = new List<string>();
        DisplayList(group, lines);
        string file = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')) + ".txt";
        File.WriteAllLines(Path.Combine(output, file), lines, new UTF8Encoding(false));
        return name + " · primitivi " + lines.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(lines);
    }

    static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    static string P(Point p) => N(p.X) + "," + N(p.Y);

    static string Brush(Brush? brush) => brush switch
    {
        null => "-",
        SolidColorBrush s => s.Color.ToString(CultureInfo.InvariantCulture) + (s.Opacity == 1 ? "" : "@" + N(s.Opacity)),
        ImageBrush => "immagine",
        _ => brush.GetType().Name
    };

    static string Pen(Pen? pen) => pen is null ? "-" : Brush(pen.Brush) + " " + N(pen.Thickness)
        + (pen.DashStyle is { Dashes.Count: > 0 } dash ? " tratto " + string.Join(";", dash.Dashes.Select(N)) + "+" + N(dash.Offset) : "");

    static string Geometry(System.Windows.Media.Geometry? geometry) => geometry switch
    {
        null => "-",
        LineGeometry l => "linea " + P(l.StartPoint) + " " + P(l.EndPoint),
        RectangleGeometry r => "rettangolo " + N(r.Rect.X) + "," + N(r.Rect.Y) + "," + N(r.Rect.Width) + "," + N(r.Rect.Height) + " " + N(r.RadiusX),
        EllipseGeometry e => "ellisse " + P(e.Center) + " " + N(e.RadiusX) + " " + N(e.RadiusY),
        _ => "percorso " + PathGeometry.CreateFromGeometry(geometry).ToString(CultureInfo.InvariantCulture)
    };

    static void DisplayList(Drawing drawing, List<string> lines)
    {
        switch (drawing)
        {
            case DrawingGroup group:
                lines.Add("gruppo " + N(group.Opacity) + " " + Geometry(group.ClipGeometry) + " " + (group.Transform is null ? "-" : group.Transform.Value.ToString(CultureInfo.InvariantCulture)));
                foreach (var child in group.Children) DisplayList(child, lines);
                lines.Add("fine");
                break;
            case GeometryDrawing shape:
                lines.Add("forma " + Geometry(shape.Geometry) + " riempimento " + Brush(shape.Brush) + " penna " + Pen(shape.Pen));
                break;
            case GlyphRunDrawing text:
                var run = text.GlyphRun;
                lines.Add("testo " + ConcreteSectionTrace.Quote(new string(run.Characters.ToArray())) + " " + P(run.BaselineOrigin) + " " + N(run.FontRenderingEmSize)
                    + " " + Brush(text.ForegroundBrush));
                break;
            case ImageDrawing image:
                // The stress raster: its pixels are compared through their hash.
                lines.Add("immagine " + N(image.Rect.X) + "," + N(image.Rect.Y) + "," + N(image.Rect.Width) + "," + N(image.Rect.Height) + " " + Pixels(image.ImageSource));
                break;
            default:
                lines.Add(drawing.GetType().Name);
                break;
        }
    }

    static string Pixels(ImageSource source)
    {
        if (source is not BitmapSource bitmap) return source.GetType().Name;
        int stride = bitmap.PixelWidth * ((bitmap.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[stride * bitmap.PixelHeight]; bitmap.CopyPixels(pixels, stride, 0);
        return bitmap.PixelWidth.ToString(CultureInfo.InvariantCulture) + "x" + bitmap.PixelHeight.ToString(CultureInfo.InvariantCulture) + " "
            + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels))[..16];
    }

    /// <summary>
    /// Keeps every window of the thread off screen and never activated while the traces run: the windows are created at −20000 (WH_CBT,
    /// HCBT_CREATEWND), every later move (DisplayAdaptation fits the dialogs into the monitor) is brought back off screen (WH_CALLWNDPROC,
    /// WM_WINDOWPOSCHANGING), and activation is refused (HCBT_ACTIVATE). The dialogs of the view keep working: they are driven by
    /// operations of the dispatcher inside their modal loop.
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

internal sealed partial class SheetEditor
{
    /// <summary>UiTests (F5.10 traces): the historic RC section view of this sheet (a new one after «Applica» of the actions).</summary>
    internal ConcreteWorkspace ConcreteForChecks => concrete ?? throw new InvalidOperationException("Foglio senza la vista storica della sezione c.a.");
}
