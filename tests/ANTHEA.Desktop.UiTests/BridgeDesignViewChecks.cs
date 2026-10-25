using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.BridgeDesign;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.BridgeDesign;

namespace X.Desktop;

/// <summary>
/// --check-bridge-design-view &lt;radice&gt; &lt;cartella&gt; [parti] (UiTests, F5.12, proposal F5 section 7.3): the XAML view of Bridge Design
/// (BridgeDesignSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the Release opens it since the switch of 23/10/2026, in windows outside the
/// screen (never activated, no mouse). The historic view and the historic window of the prices are archived (supporto/SUPERATI/ui-storica/bridge-design):
/// what the parts compared with them opened beside the new view is compared with the values they gave, recorded before the archiving in
/// tests/fixtures/f5-bridge-design/vista-storica.json, or with their recipe (the report). Parts (a fourth argument, comma separated, runs only some
/// of them):
/// <list type="number">
/// <item>tracce: the 19 scenarios of tests/Shared/BridgeDesignTrace.cs driven through the controls of the view (pages and tabs, the text and the
/// slider of the parameters, choices, options, the buttons of the typologies and of the panels, the alternatives, the price editor shown from its
/// DataTemplate, the locks, the limits, the ranking, the cloud, the variable, the filter, Pareto) with the held engine of the traces, read back from
/// the controls (status, metrics, configuration, pages and tabs, drawing, the boxes and the texts of the search, the progress, the buttons, the
/// preview, the charts) and the panels from the ViewModel (as the other views, MU19); equal line by line to the traces of the historic view frozen
/// in tests/fixtures/f5-bridge-design/storica.</item>
/// <item>disegni: the drawing lists of the drawing (elevation and section), of the moment diagram, of the icons of the typologies, of the charts and
/// of the preview of the search, and the pixels of the figures of the report, for every F3 fixture, equal to disegni/elenco.txt.</item>
/// <item>prezzario: the price editor of the dialog service (its DataTemplate) against the historic window: size, texts, tabs and columns; the files
/// through the file dialog service; «Annulla» and «Applica prezzario»; in a read-only revision the editor without «Applica prezzario» (F5.15, BD4).</item>
/// <item>stampa: the page of «Stampa scheda» equal to the historic page (texts and pixels); the window of the printer only with a result.</item>
/// <item>schermate: the two pages in Chiara, Scuro and Molto scuro, the price editor, the compact layout (144 DPI here).</item>
/// <item>aiuto: F1 opens the guide of the module through the navigator of the services, as SheetEditor (no «?» on the fields, as the historic view).</item>
/// <item>nomi: the automation names of the historic view kept.</item>
/// <item>sola-lettura: the controls that accept the user after InspectRevision are those of the historic view, but «Suggerisci campate dalla
/// lunghezza» (F5.15, BD10 point 5).</item>
/// <item>banco: the Release opens the MVVM view, «historic» is refused by the bench; «occupato» until the engine returns (BD3).</item>
/// <item>esportazioni: for each F3 fixture the visit of the F3 capture writes what the historic visit writes, the three appearances write nothing,
/// data and result after the calculation are those of the historic view, the exported JSON carries the data (BD7), the Word report and the PDF,
/// the CSV of the quantities and of the dimensions are those of the historic view.</item>
/// <item>immagini: for each F3 fixture with a result «Immagine PNG» writes the PNG of the drawing of the historic view (last: see <see cref="Run"/>).</item>
/// </list>
/// Written on 22/10 without running it (daytime rule): the first run is in the evening. Writes dettaglio.txt (line by line, each part between
/// «== parte» lines) and esito.txt («PASS n controlli …»).
/// </summary>
internal static partial class BridgeDesignViewChecks
{
    private const string Module = BridgeDesignScenarios.Module;

    private static int checks;
    private static readonly DetailLog Log = new();

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks++;
        Log.Add("OK " + message);
    }

    /// <summary>
    /// dettaglio.txt written line by line while the check runs (22/10 evening). A process ended from outside, as the behaviour monitoring of the
    /// antivirus ends ANTHEA.exe during the exports (question S6), runs no finally: the parts done, their checks and the step in progress stay on
    /// disk instead of nothing.
    /// </summary>
    private sealed class DetailLog
    {
        private StreamWriter? file;

        internal void Open(string path) =>
            file = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };

        internal void Add(string line) => file?.WriteLine(line);

        internal void Close() { file?.Dispose(); file = null; }
    }

    /// <summary>The parts of the check, in the order they run (see <see cref="Run"/>).</summary>
    internal static readonly string[] PartNames = ["tracce", "disegni", "prezzario", "stampa", "schermate", "aiuto", "nomi", "sola-lettura", "banco", "esportazioni", "immagini"];

    /// <summary>
    /// The parts of the check, in order; <paramref name="parts"/> (the fourth argument) runs only some of them, for the work on the view. The exports
    /// run last and the PNG of the drawing («immagini») after them (22/10 evening): on this machine the behaviour monitoring of Trend Micro ends the
    /// unsigned ANTHEA.exe of the runner, without errors and with exit code 0, while DocumentFile.Write renames the temporary file of an exported
    /// image (rule NON_POLICY_JUST_ADC, question S6 of f5-ripresa.md). Every other part reports first, and dettaglio.txt shows the step in progress.
    /// </summary>
    internal static async Task Run(string root, string output, IReadOnlyCollection<string>? parts = null)
    {
        if (parts?.Except(PartNames).ToArray() is { Length: > 0 } unknown) throw new ArgumentException("Parti sconosciute: " + string.Join(", ", unknown) + "; parti: " + string.Join(",", PartNames));
        bool Part(string name) => parts is null || parts.Contains(name);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della prova deve essere vuota.");
        Directory.CreateDirectory(output);
        checks = 0;
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        Func<Task>[] steps =
        [
            () => Traces(root, output), () => Drawings(root, output), () => PriceEditor(root, output), () => PrintPage(root, output),
            () => Screenshots(root, Path.Combine(output, "schermate")), () => Help(root), () => AutomationNames(root), () => ReadOnly(root),
            () => Bench(root, output), () => FixturesAndExports(root, output), () => Images(root, output)
        ];
        Log.Open(Path.Combine(output, "dettaglio.txt"));
        try
        {
            Directory.CreateDirectory(Path.Combine(output, "schermate"));
            for (int i = 0; i < PartNames.Length; i++)
            {
                if (!Part(PartNames[i])) continue;
                int before = checks;
                Log.Add("== parte " + PartNames[i]);
                await steps[i]();
                Log.Add($"== parte {PartNames[i]}: PASS, {checks - before} controlli");
            }
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            Log.Close();
        }
        if (System.Windows.Application.Current.Windows.Count != 0) throw new InvalidOperationException("Finestre della prova rimaste aperte: " + System.Windows.Application.Current.Windows.Count);
        checks++;
        File.AppendAllText(Path.Combine(output, "dettaglio.txt"), "OK Finestre della prova chiuse" + Environment.NewLine, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML di Bridge Design (MvvmSheetEditor, fuori schermo)"
            + (parts is null ? "" : " · solo " + string.Join(",", PartNames.Where(Part))), new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- services and hosting

    /// <summary>The bench choice of the check: Bridge Design with the MVVM view.</summary>
    private static readonly string[] BenchArguments = [ModuleHostBench.Argument, Module + "=mvvm"];

    /// <summary>
    /// Services of the sheets in the check: errors rethrown, messages recorded, no question expected, files from <see cref="SavePath"/> and
    /// <see cref="OpenPath"/>, recorded Wiki; the price editor shown in a window off screen from its DataTemplate (as WpfDialogService shows it, but
    /// never activated and without a modal loop), driven by <see cref="PriceScript"/>, then closed.
    /// </summary>
    private sealed class Services : IConfirmationService, IDialogService, IFileDialogService, IWikiNavigator, IMessageService
    {
        internal List<string?> Wiki { get; } = [];
        internal List<(string Title, string Message)> Messages { get; } = [];
        internal List<SaveFileRequest> Saves { get; } = [];
        internal List<OpenFileRequest> Opens { get; } = [];
        internal string? SavePath { get; set; }
        internal string? OpenPath { get; set; }

        /// <summary>What the user does in the window of the price editor while it is open; null: no editor expected.</summary>
        internal Action<PriceCatalogueViewModel, Window>? PriceScript { get; set; }

        /// <summary>The windows of the price editor shown.</summary>
        internal int PriceWindows { get; private set; }

        /// <summary>
        /// A failure inside <see cref="PriceScript"/>: the asynchronous command of the sheet would keep it in its task, so it is kept here and
        /// thrown again by <see cref="ThrowScriptError"/> after the click.
        /// </summary>
        private Exception? scriptError;

        internal void ThrowScriptError()
        {
            if (scriptError is not { } error) return;
            scriptError = null;
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

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

        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            if (dialog is not PriceCatalogueViewModel editor || PriceScript is not { } script) throw new InvalidOperationException("Finestra non prevista: " + dialog.Title);
            // As WpfDialogService: the window around the dialog, its view from the DataTemplate of the application, closed when the dialog asks.
            var window = new Window
            {
                Title = dialog.Title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false, ShowActivated = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000
            };
            Appearance.Watch(window);
            void Close(object? sender, EventArgs e) => window.Close();
            dialog.CloseRequested += Close;
            PriceWindows++;
            try
            {
                window.Show(); window.UpdateLayout();
                script(editor, window);
            }
            catch (Exception error) { scriptError = error; }
            finally
            {
                dialog.CloseRequested -= Close;
                if (window.IsVisible) window.Close();
            }
            return Task.FromResult(dialog.Result);
        }

        public string? OpenFile(OpenFileRequest request) { Opens.Add(request); return OpenPath ?? throw new InvalidOperationException("Dialogo di apertura non previsto."); }
        public string? SaveFile(SaveFileRequest request) { Saves.Add(request); return SavePath ?? throw new InvalidOperationException("Dialogo di salvataggio non previsto."); }
        public string? ChooseFolder(string? title = null) => throw new InvalidOperationException("Cartella non prevista.");
        public void Show(string? uri = null) => Wiki.Add(uri);
    }

    private static SheetEditorFactory Editors(Services services) =>
        new(ModuleHostBench.Implementations(BenchArguments), DesktopComposition.CreateModules(), services.Create(), new CalculationLimiter(CalculationLimiter.DefaultMaximum));

    /// <summary>
    /// The continuations of the sheet (the delay of the automatic calculation, the end of the engine, the publication, the search and its progress)
    /// at the idle priority of the dispatcher, as the historic traces ran those of the historic view (ViewTraceTools.WithIdleContinuations):
    /// they advance only while the check waits, never while a step settles.
    /// </summary>
    private static T Idle<T>(Func<T> action) => ViewTraceTools.WithIdleContinuations(action);

    /// <summary>A sheet in a window outside the screen, 1400 × 1000 as the historic traces lay out the view.</summary>
    private sealed class Hosted : IDisposable
    {
        private readonly Window window;
        internal ISheetEditor Editor { get; }
        internal int Modified;

        internal Hosted(ISheetEditor editor, bool dark = false, double width = 1400, double height = 1000)
        {
            Editor = editor;
            editor.Modified += () => Modified++;
            window = new Window
            {
                Content = editor.View, Width = width, Height = height, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Bridge Design " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
            if (Editor is MvvmSheetEditor) Offscreen(View);
        }

        internal Window Window => window;

        internal FrameworkElement Root => Editor.View;

        internal BridgeDesignSheetViewModel Sheet => (BridgeDesignSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        private BridgeDesignSheetView? view;

        /// <summary>The view of the DataTemplate (the same for the life of the editor).</summary>
        internal BridgeDesignSheetView View => view ??= Walk(Root).OfType<BridgeDesignSheetView>().Single();

        /// <summary>The window of the printer never opens in the check: it answers no, unless a part says otherwise.</summary>
        private static void Offscreen(BridgeDesignSheetView view) => view.ShowPrintDialog = _ => false;

        /// <summary>The calculation of the opening (or the last one requested).</summary>
        internal Task Pending => Sheet.WaitForCalculationAsync();

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

    /// <summary>The user can reach and use the control: shown in the window (on the page and in the tabs chosen) and enabled.</summary>
    private static bool Usable(UIElement? element) => element is { IsVisible: true, IsEnabled: true };

    /// <summary>A click as the user makes it (ButtonBase.OnClick: the Click event, then the command; for a check box the toggle first), only when usable.</summary>
    private static void Press(ButtonBase? button)
    {
        if (!Usable(button)) return;
        try { typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw(); }
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    /// <summary>The first button shown with this caption under <paramref name="root"/>.</summary>
    private static Button? Caption(DependencyObject root, string caption) => Walk(root).OfType<Button>().FirstOrDefault(b => b.IsVisible && Equals(b.Content, caption));

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

    /// <summary>The data of a sheet with the dates written by the sheet normalized (environmental factors, price history), as the traces read them.</summary>
    private static string Normalized(JsonNode? node) => node is null ? "null" : BridgeDesignTrace.Normalize(node.ToJsonString(J.Options));

    // ---------------------------------------------------------------- 10. bench and Release, BD3, BD7

    private static async Task Bench(string root, string output)
    {
        var services = new Services();
        var editors = Editors(services);
        using (var mvvm = editors.Open(Module, BridgeDesignScenarios.FixtureData(root, "default")))
            Check(mvvm is MvvmSheetEditor { Sheet: BridgeDesignSheetViewModel }, "Il banco della prova apre la vista MVVM di Bridge Design");
        using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(Module, BridgeDesignScenarios.FixtureData(root, "default")))
            Check(release is MvvmSheetEditor { Sheet: BridgeDesignSheetViewModel } && ModuleImplementations.Release.For(Module) == SheetImplementation.Mvvm,
                "La Release apre Bridge Design con la vista MVVM (passaggio del 23/10/2026)");
        bool historicRefused = false;
        try { ModuleHostBench.Implementations([ModuleHostBench.Argument, Module + "=historic"]); }
        catch (ArgumentException error) when (error.Message.Contains("archiviata", StringComparison.Ordinal)) { historicRefused = true; }
        Check(historicRefused, "Banco: la vista storica di Bridge Design è archiviata, «historic» rifiutato");
        // BD3: «occupato» from the start of the engine to its return, also for the window (the historic view never gave it to SheetEditor).
        var engine = new HeldModuleService(Module);
        engine.Hold();
        var held = Idle(() => new Hosted(new MvvmSheetEditor(DesktopComposition.CreateModules().SheetModule(Module), BridgeDesignScenarios.FixtureData(root, "default"),
            services.Create(), engine, new CalculationLimiter(CalculationLimiter.DefaultMaximum))));
        try
        {
            await engine.EnteredAsync().WaitAsync(TimeSpan.FromSeconds(60));
            await held.Settle();
            Check(held.Editor.Busy && !held.Editor.HasResults, "BD3: la finestra vede il foglio occupato mentre il motore lavora");
            string json = Path.Combine(output, "risultato-occupato.json");
            bool refused = false;
            try { held.Editor.ExportResult(json); } catch (InvalidOperationException) { refused = true; }
            Check(refused && !File.Exists(json), "BD3: esportazione JSON rifiutata mentre il foglio è occupato");
            engine.Release();
            await Idle(() => held.Sheet.WaitForCalculationAsync()).WaitAsync(TimeSpan.FromSeconds(120));
            for (int i = 0; i < 50 && held.Editor.Busy; i++) await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await held.Settle();
            Check(!held.Editor.Busy && held.Editor.HasResults, "BD3: occupato fino al ritorno del motore, poi il risultato");
            // BD7: the JSON of the result carries the data of the sheet, as SheetEditor writes it.
            held.Editor.ExportResult(json);
            var exported = JsonNode.Parse(File.ReadAllText(json))!.AsObject();
            Check(exported["dati"] is JsonObject data && Normalized(data) == Normalized(held.Editor.Data) && exported.Count == held.Editor.Result!.Count + 1,
                "BD7: il JSON esportato contiene il risultato e i dati del foglio, come SheetEditor");
        }
        finally { engine.Release(); held.Dispose(); }
    }
}
