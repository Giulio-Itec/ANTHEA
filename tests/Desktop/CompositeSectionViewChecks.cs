using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.CompositeSection;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.CompositeSection;

namespace X.Desktop;

/// <summary>
/// --check-sezione-mista-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.11, proposal F5 section 7.3): the XAML view of the composite bridge section
/// (CompositeSectionSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the bench opens it with <c>--module-impl str_mista_ponte=mvvm</c>, in
/// windows outside the screen (never activated, no mouse). The Release keeps the historic view until the user has tried the view (decisions of
/// 17/10). Parts:
/// <list type="number">
/// <item>Traces through the view: the 20 scenarios of tests/Shared/CompositeSectionTrace.cs driven through the controls of the view (pages, fields,
/// choices and check boxes of the FieldForms, the cards of the phases, the cells, rows and buttons of the table of the actions, situations, display,
/// tabs, switches, the menu «Verifiche ▾», the scale of the concrete, the local sketch, the curves, the detached window off screen) with the held
/// engine of the traces, and read back from the controls (texts, choices, switches, drawings and charts of the view; the fields and the panels from
/// the ViewModel, as the other views, MU19), equal line by line to the traces of the historic view frozen in tests/fixtures/f5-sezione-mista.</item>
/// <item>The drawing lists of the drawing, of the local sketch and of the curves of the view, and the pixels of the figures of the report, for every
/// F3 fixture, equal to those of the historic view frozen in tests/fixtures/f5-sezione-mista/disegni.</item>
/// <item>For each of the 9 F3 fixtures: the visit of the F3 capture writes what the historic visit writes, and the three appearances write nothing;
/// after the calculation data and result are those of the historic view; the exports are those of the historic view on the same data: the Word report
/// (dates normalized) and the PDF beside it, the CSV of the stresses, the CSV of the points and fibres of a curve.</item>
/// <item>Screenshots of the view in Chiara, Scuro and Molto scuro (folder schermate; 144 DPI on this machine).</item>
/// <item>Help: the «?» labels shown are those of the historic view; every «?» and every F1 on a field of every form reaches the IWikiNavigator of
/// the services (lesson 1 of 17/10), F1 elsewhere the guide of the module.</item>
/// <item>Automation names: those of the historic view kept.</item>
/// <item>Read-only revision: the controls that accept the user are those of the historic view after InspectRevision.</item>
/// <item>The detached view (SM3), «Espandi», «PNG» and «Info modello…» against the historic windows.</item>
/// <item>Bench and Release: with --module-impl the section opens the MVVM view; the Release keeps the historic view.</item>
/// </list>
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static partial class CompositeSectionViewChecks
{
    private const string Module = CompositeSectionScenarios.Module;

    private static int checks;
    private static readonly List<string> Log = [];

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks++;
        Log.Add("OK " + message);
    }

    /// <summary>The parts of the check, in order; <paramref name="parts"/> (the fourth argument) runs only some of them, for the work on the view.</summary>
    internal static async Task Run(string root, string output, IReadOnlyCollection<string>? parts = null)
    {
        bool Part(string name) => parts is null || parts.Contains(name);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della prova deve essere vuota.");
        Directory.CreateDirectory(output);
        checks = 0; Log.Clear();
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        try
        {
            Directory.CreateDirectory(Path.Combine(output, "schermate"));
            if (Part("tracce")) await Traces(root, output);
            if (Part("disegni")) await Drawings(root, output);
            if (Part("esportazioni")) await FixturesAndExports(root, output);
            if (Part("schermate")) await Screenshots(root, Path.Combine(output, "schermate"));
            if (Part("aiuto")) await Help(root);
            if (Part("nomi")) await AutomationNames(root);
            if (Part("sola-lettura")) await ReadOnly(root);
            if (Part("finestre")) await Windows(root, output);
            if (Part("banco")) Bench(root);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML della sezione composta da ponte (MvvmSheetEditor, fuori schermo)"
            + (parts is null ? "" : " · solo " + string.Join(",", parts)), new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- services and hosting

    /// <summary>The bench choice of the check: the composite section with the MVVM view.</summary>
    private static readonly string[] BenchArguments = [ModuleHostBench.Argument, Module + "=mvvm"];

    /// <summary>Services of the sheets in the check: errors rethrown, messages recorded, no question and no dialog expected, files from <see cref="SavePath"/>, recorded Wiki.</summary>
    private sealed class Services : IConfirmationService, IDialogService, IFileDialogService, IWikiNavigator, IMessageService
    {
        internal List<string?> Wiki { get; } = [];
        internal List<(string Title, string Message)> Messages { get; } = [];
        internal List<SaveFileRequest> Saves { get; } = [];
        internal string? SavePath { get; set; }

        internal PresentationServices Create()
        {
            var bench = ModuleHostBench.Services();
            return new PresentationServices(this, this, this, this, bench.Clipboard, bench.Links, this);
        }

        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista: dati condivisi.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista: spostamento.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista: chiusura.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista: salvataggio.");
        public bool ConfirmDelete(string title, string question) => throw new InvalidOperationException("Domanda non prevista: " + question);

        public void ReportFailure(string title, Exception error) => throw new InvalidOperationException("Errore di un comando: " + title, error);
        public void Inform(string title, string message) => Messages.Add((title, message));
        public void Warn(string title, string message) => Messages.Add((title, message));

        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog) => throw new InvalidOperationException("Finestra non prevista: " + dialog.Title);

        public string? OpenFile(OpenFileRequest request) => throw new InvalidOperationException("Dialogo di apertura non previsto.");
        public string? SaveFile(SaveFileRequest request) { Saves.Add(request); return SavePath ?? throw new InvalidOperationException("Dialogo di salvataggio non previsto."); }
        public string? ChooseFolder(string? title = null) => throw new InvalidOperationException("Cartella non prevista.");
        public void Show(string? uri = null) => Wiki.Add(uri);
    }

    private static SheetEditorFactory Editors(Services services) =>
        new(ModuleHostBench.Implementations(BenchArguments), DesktopComposition.CreateModules(), services.Create(), new CalculationLimiter(CalculationLimiter.DefaultMaximum));

    /// <summary>
    /// The continuations of the sheet (the delay of the automatic calculation, the end of the engine, the publication, the curves) at the idle
    /// priority of the dispatcher, as the historic traces run those of the historic view (CompositeSectionHistoricTrace.WithIdleContinuations):
    /// they advance only while the check waits, never while a step settles.
    /// </summary>
    private static T Idle<T>(Func<T> action) => CompositeSectionHistoricTrace.HistoricSide.WithIdleContinuations(action);

    /// <summary>A sheet in a window outside the screen, 1400 × 1000 as the historic traces lay out the view; its dialogs and detached window off screen too.</summary>
    private sealed class Hosted : IDisposable
    {
        private readonly Window window;
        internal ISheetEditor Editor { get; }
        internal int Modified;
        internal List<Window> Dialogs { get; } = [];

        internal Hosted(ISheetEditor editor, bool dark = false, double width = 1400, double height = 1000)
        {
            Editor = editor;
            editor.Modified += () => Modified++;
            window = new Window
            {
                Content = editor.View, Width = width, Height = height, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Sezione mista " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
            if (Editor is MvvmSheetEditor) Offscreen(View);
        }

        internal Window Window => window;

        internal FrameworkElement Root => Editor.View;

        internal CompositeSectionSheetViewModel Sheet => (CompositeSectionSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal CompositeSectionSheetView View => Walk(Root).OfType<CompositeSectionSheetView>().Single();

        internal BridgeWorkspace Historic => ((SheetEditor)Editor).bridge ?? throw new InvalidOperationException("Foglio storico senza la vista della sezione composta.");

        /// <summary>The dialogs and the detached window of the view: outside the screen, never activated; the dialogs without waiting.</summary>
        private void Offscreen(CompositeSectionSheetView view)
        {
            static void Away(Window w) { w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = -32000; w.Top = -32000; w.ShowActivated = false; w.ShowInTaskbar = false; }
            view.ShowWindow = w => { Away(w); Dialogs.Add(w); w.Show(); w.UpdateLayout(); return null; };
            view.ShowDetached = w => { Away(w); w.Show(); w.UpdateLayout(); };
        }

        internal void Layout() { window.UpdateLayout(); Root.UpdateLayout(); }

        internal async Task Settle()
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
            Layout();
        }

        internal byte[] Png(FrameworkElement? element = null)
        {
            Layout();
            element ??= (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
        }

        public void Dispose()
        {
            foreach (var dialog in Dialogs.ToArray()) dialog.Close();
            window.Content = null;
            window.Close();
            Editor.Dispose();
        }
    }

    /// <summary>The logical and the visual tree, as the visit of the F3 capture walks it; without <paramref name="popups"/> the lists of the combo boxes are left out.</summary>
    internal static IEnumerable<DependencyObject> Walk(DependencyObject root, bool popups = true)
    {
        var pending = new Stack<DependencyObject>(); var seen = new HashSet<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop(); if (!seen.Add(current)) continue;
            yield return current;
            if (!popups && current is Popup) continue;
            var children = LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>().ToList();
            if (current is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) children.Add(VisualTreeHelper.GetChild(current, i));
            foreach (var child in children.AsEnumerable().Reverse()) pending.Push(child);
        }
    }

    /// <summary>
    /// The content that WPF has not laid out yet (closed groups, the page of the inputs not shown, the tabs of the results not chosen) measured
    /// outside the visual tree with its templates applied, so that it can be read and driven as the historic view, which builds every control in
    /// code (as RetainingWallViewChecks.Realize). Nothing is written into the sheet.
    /// </summary>
    internal static void Realize(FrameworkElement root)
    {
        int known = -1;
        for (int pass = 0; pass < 20; pass++)
        {
            var elements = Walk(root, popups: false).OfType<FrameworkElement>().ToArray();
            foreach (var element in elements)
            {
                if (!ReferenceEquals(element, root) && element is not Window && VisualTreeHelper.GetParent(element) is null && !(element.IsMeasureValid && element.IsArrangeValid))
                {
                    element.Measure(new Size(760, 700));
                    element.Arrange(new Rect(0, 0, 760, 700));
                }
                element.ApplyTemplate();
            }
            if (elements.Length == known) return;
            known = elements.Length;
        }
    }

    /// <summary>Shown as the historic trace checks it: no element up to the view is collapsed or hidden (closed expanders do not count).</summary>
    private static bool Shown(DependencyObject element, DependencyObject root)
    {
        for (DependencyObject? node = element; node is not null && !ReferenceEquals(node, root); node = LogicalTreeHelper.GetParent(node) ?? VisualTreeHelper.GetParent(node))
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    /// <summary>A click as the user makes it (ButtonBase.OnClick: the Click event, then the command), only when the button is usable.</summary>
    private static void Press(ButtonBase? button, DependencyObject root)
    {
        if (button is null || !button.IsEnabled || !Shown(button, root)) return;
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    private static void Press(UIElement target, Key key)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    }

    private static Button? Caption(DependencyObject root, string caption) => Walk(root).OfType<Button>().FirstOrDefault(b => Equals(b.Content, caption));

    /// <summary>
    /// «Calcola curva» pressed: its command ended and its button enabled again (the command raises CanExecuteChanged in the continuation that
    /// follows its task, at the idle priority of the sheet).
    /// </summary>
    private static async Task CurveEnded(Hosted host)
    {
        var command = host.Sheet.CalculateCurveCommand;
        await (command.ExecutionTask ?? Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(120));
        for (int i = 0; i < 200 && (command.IsRunning || host.View.CurveButton.IsEnabled != command.CanExecute(null)); i++)
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    /// <summary>The visit of the F3 capture (F3EquivalenceCapture.Visit): every expander opened, every page of every tab control chosen, then the first again.</summary>
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
}
