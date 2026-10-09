using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using X.Core;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>
/// --check-contrast &lt;cartella&gt; [--riferimento &lt;cartella&gt;] [--viste &lt;regex&gt;] (UiTests configuration only).
/// Builds every view, dialog and popup of the interface in the three appearances, entirely off screen, and measures the
/// contrast of every visible text twice: from the pixels of the capture (prevailing background against the text pixel
/// farthest from it) and from the properties (effective Foreground against the first non-transparent Background up the
/// visual tree). In the dark appearances it also looks for large light areas. Output: one PNG per view and appearance
/// (&lt;vista&gt;-&lt;modalità&gt;.png), annotated copies in difetti/, report.json and riepilogo.md.
/// No calibration, no theme change: the views are built exactly as in the application (mode chosen before
/// navigating, colours applied by the Loaded handler of Appearance), never re-themed before the capture.
/// </summary>
internal sealed class ContrastAudit
{
    internal static readonly AppAppearance[] Modes = [AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark];
    internal const double CaptureWidth = 1600, CaptureHeight = 1000;
    internal static ContrastAudit? Current;
    private static bool handlersRegistered;
    private static long lastBeat;
    private static string step = "";

    /// <summary>Buttons that open a window or a popup, with the time to wait for it (seconds). Only these are clicked:
    /// any other command could write files, start long searches or open the native dialogs of the system.</summary>
    internal static readonly (string Label, double Seconds)[] DialogTriggers =
    [
        ("Info modello…", 8), ("Stacca vista", 8), ("Verifiche ▾", 4), ("Scala…", 4), ("Espandi", 4), ("Proprietà / report…", 8),
        ("Proprietà…", 8), ("Nuovo materiale CLS", 8), ("Nuovo materiale acciaio", 8), ("Nuovo materiale trefoli", 8), ("Copia in…", 4),
        ("ⓘ Tabelle", 4), ("Diagrammi e dettagli", 8), ("Apri tavola armature…", 8), ("Info", 4), ("NTC §7.2.5", 6),
        ("Info sezioni", 4), ("Apri verificatore c.a.", 60), ("Dimensiona…", 8), ("Dividi…", 4), ("Proponi suddivisione…", 8),
        ("Tavola armature e distinta ferri…", 10), ("Distinta ferri…", 10), ("Valori di calcolo…", 90), ("Invia / carica terreno…", 8),
        ("Apri sezione in c.a.", 60)
    ];

    /// <summary>Every window of X.Desktop (Ui.Dialog, new Window, Window subclasses): title prefix, where it opens, reason when not reached.</summary>
    private static readonly (string Prefix, string Origin, string Reason)[] KnownWindows =
    [
        ("Contenuti del report Word", "MainWindow.ExportReport", "richiede un modulo con risultati (pali verticali, sezione c.a., sezione da ponte)"),
        ("Confronto · ", "ProjectSharing.ShowCoherence", "non raggiunta"),
        ("Collegamento dei sondaggi", "ProjectSharing.ShowCoherence › Mostra collegamento stratigrafie…", "richiede almeno due fogli con terreni condivisi nella stessa sezione"),
        ("Report · ", "ProjectReport.ExportSectionReport (controllo prima del report)", "nessuna sezione con differenze o avvisi: il comando apre subito la finestra di sistema Salva"),
        ("Generazione del report", "ProjectReport.ExportSectionReport (avanzamento)", "si apre solo dopo la finestra di sistema Salva e la scrittura del documento Word"),
        ("Nuova revisione · ", "ProjectWorkspace.CreateProjectRevision", "non raggiunta"),
        ("Revisioni · ", "ProjectWorkspace.ShowProjectRevisions", "non raggiunta"),
        ("Dati condivisi della sezione", "WpfDesktopServices.ConfirmSharedUpdate", "non raggiunta"),
        ("Conferma spostamento", "WpfDesktopServices.ConfirmProjectMove", "non raggiunta"),
        ("Apri collegamento Wiki", "Ui.Ask da WikiView", "non raggiunta"),
        ("Modello di calcolo e dati condivisi", "CalculationHelpView.Show (fuori dalla finestra principale)", "si apre solo da una vista ospitata in un'altra finestra (verificatore c.a. del palo elastico)"),
        ("Sezione composta · informazioni sul modello", "BridgeInformation", "non raggiunta"),
        ("ANTHEA · Sezione da ponte · sollecitazioni e tensioni", "BridgeDetachedView.DetachView", "non raggiunta"),
        ("Nuovo materiale · ", "ConcreteMaterialEditor.CreateMaterialDialog", "non raggiunta"),
        ("Scala del dominio", "ConcreteRefinements (Scala…)", "non raggiunta"),
        ("Proprietà / report della sezione", "ConcreteSectionProperties", "non raggiunta"),
        ("Info · risposta elastica del palo", "ElasticPileWorkspace.ShowInfo", "non raggiunta"),
        ("Armature del palo · elevazione e distinta", "ElasticPileWorkspace.ShowReinforcement", "richiede il calcolo elastico concluso"),
        ("Copia stratigrafia in…", "GeoEditor / HorizontalPresentation", "non raggiunta"),
        ("Parametro dello strato · origine e applicabilità", "HorizontalSoilEditor.ShowDetails", "non raggiunta"),
        ("Palo orizzontale · risultati", "HorizontalWorkspace.ShowResults", "richiede i risultati del palo orizzontale"),
        ("NTC 2018 · dettagli dei pali", "PileReinforcementEditor.CreateNtcWindow", "non raggiunta"),
        ("Perimetro delle verifiche", "PileReinforcementEditor (Info sezioni)", "non raggiunta"),
        ("Verificatore c.a. · ", "PileReinforcementEditor.OpenConcrete", "non raggiunta"),
        ("Dividi il tratto", "PileReinforcementEditor.Split", "non raggiunta"),
        ("Proposta di suddivisione", "PileReinforcementEditor.Propose", "richiede i risultati elastici"),
        ("Dimensionamento del tratto · proposta da applicare", "PileReinforcementEditor.Design", "richiede i risultati elastici"),
        ("Armature del muro · sezione e distinta ferri", "RetainingWallWorkspace.ShowBarSchedule", "non raggiunta"),
        ("Muro · valori utilizzati nel calcolo", "RetainingWallWorkspace.CalculationParametersWindow", "non raggiunta"),
        ("Tabelle e dettagli", "SheetEditor.ShowDetails", "nessun chiamante: il metodo non è collegato a comandi dell'interfaccia"),
        ("Confronto completo", "ElasticPileWorkspace (Mostra confronto completo)", "solo con archivi elastici precedenti da migrare"),
        ("Terreno · invio e riutilizzo", "SoilTransferDialog", "non raggiunta"),
        ("ANTHEA — ", "MainWindow.OpenModuleCopy (Apri sezione in c.a. dal muro, fuori dai progetti)", "non raggiunta"),
    ];

    internal readonly string OutputDirectory;
    private readonly string? referenceDirectory;
    private readonly Regex? filter;
    internal AppAppearance Mode { get; private set; }
    internal string Context = "";
    internal MainWindow? Main;
    internal int InFlight, Processed;
    internal readonly Dictionary<string, Func<Window, Task>> DialogActions = new();
    internal readonly string TestArchive;
    internal string Root => sources.Root;
    private readonly HashSet<string> names = new();
    private readonly HashSet<Window> seenWindows = new();
    private readonly JsonArray captures = new(), defects = new(), infos = new(), excludedLight = new(), notes = new(), violations = new(), windows = new(), uncovered = new();
    internal readonly JsonArray SystemDialogs = new();
    private readonly Dictionary<string, (int Count, long Pixels, string Reason)> exclusions = new();
    private readonly HashSet<string> capturedTitles = new();
    private readonly Dictionary<string, HashSet<string>> triggersFound = new();
    private readonly SourceIndex sources;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private int counter;
    private double dpi = 96;

    private ContrastAudit(string output, string? reference, string? views)
    {
        OutputDirectory = output; referenceDirectory = reference is null ? null : Path.GetFullPath(reference);
        filter = views is null ? null : new Regex(views, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        sources = new SourceIndex(SourceIndex.RepositoryRoot());
        TestArchive = Path.Combine(sources.Root, "supporto", "artefatti", "prova-f1.4.programma");
    }

    internal static async Task Run(string[] args)
    {
        if (args.Length == 2 && args[1] == "--diagnosi") { await Diagnose(Path.GetFullPath(args[0])); return; }
        if (args.Length == 2 && args[1] == "--diagnosi-menu") { await DiagnoseMenu(Path.GetFullPath(args[0])); return; }
        string output = Path.GetFullPath(args[0]); string? reference = null, views = null;
        for (int i = 1; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length) throw new ArgumentException("Valore mancante per " + args[i]);
            if (args[i] == "--riferimento") reference = args[i + 1];
            else if (args[i] == "--viste") views = args[i + 1];
            else throw new ArgumentException("Opzione sconosciuta: " + args[i]);
        }
        Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output, "difetti"));
        foreach (var old in Directory.GetFiles(output, "*.png").Concat(Directory.GetFiles(Path.Combine(output, "difetti"), "*.png")).ToList()) File.Delete(old);
        var audit = new ContrastAudit(output, reference, views);
        // The c.a. checker of the elastic pile is a ConcreteWorkspace in a dialog: from there "Modello e dati comuni…" opens its own window.
        audit.DialogActions["Verificatore c.a. · "] = async window =>
        {
            if (FindButton(window, "Modello e dati comuni…") is { } theory) await audit.Trigger("Modello e dati comuni", () => Click(theory), 6);
        };
        Current = audit; RegisterHandlers(); StartWatchdog(output);
        var started = DateTime.Now;
        using (OffscreenWindows.Install(audit))
        {
            try
            {
                foreach (var mode in Modes)
                {
                    audit.BeginMode(mode);
                    var window = await audit.OpenMain(mode);
                    try { await window.AuditContrastViews(audit); }
                    finally { await audit.CloseMain(window); }
                    var start = mode == AppAppearance.Light ? AppAppearance.VeryDark : AppAppearance.Light;
                    if (audit.Want("cambio"))
                    {
                        var switching = await audit.OpenMain(start);
                        try { await switching.AuditAppearanceSwitch(audit, start, mode); }
                        finally { await audit.CloseMain(switching); }
                    }
                }
            }
            finally { Current = null; Appearance.Set(AppAppearance.Light, false); }
        }
        Interlocked.Exchange(ref lastBeat, -1);
        audit.Write(started, args);
    }

    /// <summary>
    /// --check-contrast &lt;cartella&gt; --diagnosi: checks the off-screen mechanism (moves of a hidden window, then of the shown
    /// window, cloaking, activation) and how Appearance themes a control added to a window already loaded, with and
    /// without its own Loaded handler. Output: diagnosi.txt.
    /// </summary>
    private static async Task Diagnose(string output)
    {
        Directory.CreateDirectory(output);
        var log = new List<string>();
        var audit = new ContrastAudit(output, null, null);
        using (OffscreenWindows.Install(audit))
        {
            var window = new Window { Width = 400, Height = 300, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = 100, Top = 100 };
            bool loaded = false; window.Loaded += (_, _) => loaded = true;
            var handle = new WindowInteropHelper(window).EnsureHandle();
            log.Add("handle creato: " + OffscreenWindows.Report(handle));
            OffscreenWindows.MoveForTest(handle, 100, 100);
            log.Add("dopo SetWindowPos(100,100): " + OffscreenWindows.Report(handle));
            window.Left = 200; window.Top = 200; await Task.Delay(50);
            var (hidden, _) = OffscreenWindows.Where(handle);
            log.Add("dopo Left/Top=200: " + OffscreenWindows.Report(handle));
            // Shown only if the hidden window could not be brought on screen.
            if (hidden.Right <= -10000 && hidden.Bottom <= -10000)
            {
                window.Show(); await Task.Delay(200);
                log.Add("dopo Show: " + OffscreenWindows.Report(handle) + " Loaded=" + loaded + " attiva=" + OffscreenWindows.ForegroundIsOurs());
                OffscreenWindows.MoveForTest(handle, 100, 100); await Task.Delay(50);
                log.Add("mostrata, dopo SetWindowPos(100,100): " + OffscreenWindows.Report(handle));
            }
            else log.Add("Show non eseguito: la finestra nascosta non resta fuori schermo");
            log.AddRange(OffscreenWindows.Log);
            // Theme of a button added to a loaded window (as the module views are).
            Appearance.Set(AppAppearance.Dark, false);
            var host = new StackPanel(); window.Content = host; await Task.Delay(100);
            string Describe(Button b) => $"{((SolidColorBrush)b.Background).Color} origine={DependencyPropertyHelper.GetValueSource(b, Control.BackgroundProperty).BaseValueSource} corrente={DependencyPropertyHelper.GetValueSource(b, Control.BackgroundProperty).IsCurrent} caricato={b.IsLoaded}";
            var button = Ui.Button("Prova", () => { });
            button.Loaded += (_, _) => log.Add("Loaded del pulsante: " + Describe(button));
            host.Children.Add(button);
            log.Add("aggiunto: " + Describe(button));
            for (int i = 0; i < 5; i++) { await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(30); }
            log.Add("dopo l'attesa: " + Describe(button));
            var inspected = Ui.Button("Ispezione", () => { }, inspection: true); host.Children.Add(inspected);
            var plain = Ui.Button("Senza gestore Loaded", () => { });
            plain.SizeChanged += (_, e) => log.Add($"SizeChanged del pulsante senza gestore Loaded: {e.NewSize} caricato={plain.IsLoaded} {Describe(plain)}");
            host.Children.Add(plain);
            var handled = Ui.Button("Con gestore Loaded", () => { }, inspection: true); handled.Loaded += (_, _) => { }; host.Children.Add(handled);
            for (int i = 0; i < 5; i++) { await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); await Task.Delay(30); }
            log.Add("pulsante con ispezione, senza gestore Loaded: " + Describe(inspected));
            log.Add("pulsante senza gestore Loaded: " + Describe(plain));
            log.Add("pulsante con ispezione e gestore Loaded: " + Describe(handled));
            Appearance.Set(AppAppearance.Light, false);
            window.Close();
        }
        File.WriteAllLines(Path.Combine(output, "diagnosi.txt"), log);
    }

    /// <summary>
    /// --check-contrast &lt;cartella&gt; --diagnosi-menu: the menu bar of the main window off screen, after the paths a user
    /// follows (start in a dark appearance, open and close a menu, choose the appearance from the Aspetto menu). Clicks go
    /// through the automation peers (MenuItem.OnClick, as a real click). The preference file is restored at the end.
    /// Output: diagnosi-menu.txt and one PNG of the menu bar per scenario.
    /// </summary>
    private static async Task DiagnoseMenu(string output)
    {
        Directory.CreateDirectory(output);
        var log = new List<string>();
        var audit = new ContrastAudit(output, null, null);
        string preference = Appearance.PreferencePath;
        byte[]? saved = File.Exists(preference) ? File.ReadAllBytes(preference) : null;
        static string B(Brush? b) => b is SolidColorBrush s ? s.Color.ToString() : b?.GetType().Name ?? "null";
        static MenuItem Top(Window w, string header) => Ui.Descendants<Menu>(w).First().Items.OfType<MenuItem>().First(i => (i.Header as string)?.Replace("_", "") == header);
        async Task Open(MenuItem item) { ((IExpandCollapseProvider)new MenuItemAutomationPeer(item).GetPattern(PatternInterface.ExpandCollapse)).Expand(); await audit.Settle(120); }
        async Task Choose(MenuItem parent, string header)
        {
            await Open(parent);
            var option = parent.Items.OfType<MenuItem>().First(i => (i.Header as string) == header);
            ((IInvokeProvider)new MenuItemAutomationPeer(option).GetPattern(PatternInterface.Invoke)).Invoke();
            await audit.Settle(200);
        }
        async Task Scenario(string name, AppAppearance start, Func<MainWindow, Task> act)
        {
            audit.Mode = start;
            var window = await audit.OpenMain(start);
            try
            {
                await act(window); await audit.Settle(150);
                var menu = Ui.Descendants<Menu>(window).First();
                log.Add($"[{name}] avvio={start} corrente={Appearance.Current} menu: sfondo={B(menu.Background)} testo={B(menu.Foreground)} modalità menu={menu.IsKeyboardFocusWithin}");
                foreach (var item in menu.Items.OfType<MenuItem>())
                {
                    var row = item.Template?.FindName("Row", item) as Border;
                    var texts = Ui.Descendants<TextBlock>(item).Where(t => t.IsVisible).Select(t => $"'{t.Text}' {B(t.Foreground)}");
                    log.Add($"  {item.Header}: scuro={Appearance.GetDark(item)} modello={(row is null ? "nativo" : "scuro")} testo={B(item.Foreground)} " +
                        $"({DependencyPropertyHelper.GetValueSource(item, Control.ForegroundProperty).BaseValueSource}) sfondo={B(item.Background)} riga={B(row?.Background)} " +
                        $"evidenziato={item.IsHighlighted} aperto={item.IsSubmenuOpen} opacità={item.Opacity} testi=[{string.Join("; ", texts)}]");
                }
                menu.UpdateLayout();
                SavePng(Render(menu, (int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight)), Path.Combine(output, name + ".png"));
            }
            catch (Exception ex) { log.Add($"[{name}] errore: {ex}"); }
            finally { await audit.CloseMain(window); }
        }
        using (OffscreenWindows.Install(audit))
        {
            try
            {
                await Scenario("avvio-scuro", AppAppearance.Dark, _ => Task.CompletedTask);
                await Scenario("avvio-molto-scuro", AppAppearance.VeryDark, _ => Task.CompletedTask);
                await Scenario("avvio-scuro-file-aperto-e-chiuso", AppAppearance.Dark, async w => { var file = Top(w, "File"); await Open(file); file.IsSubmenuOpen = false; });
                await Scenario("chiaro-poi-scuro-dal-menu", AppAppearance.Light, w => Choose(Top(w, "Aspetto"), "Scuro"));
                await Scenario("chiaro-poi-molto-scuro-dal-menu", AppAppearance.Light, w => Choose(Top(w, "Aspetto"), "Molto scuro"));
                await Scenario("scuro-poi-chiaro-poi-scuro-dal-menu", AppAppearance.Dark, async w => { await Choose(Top(w, "Aspetto"), "Chiaro"); await Choose(Top(w, "Aspetto"), "Scuro"); });
            }
            finally
            {
                Appearance.Set(AppAppearance.Light, false);
                if (saved is null) { if (File.Exists(preference)) File.Delete(preference); } else File.WriteAllBytes(preference, saved);
            }
        }
        File.WriteAllLines(Path.Combine(output, "diagnosi-menu.txt"), log);
    }

    private static void RegisterHandlers()
    {
        if (handlersRegistered) return;
        handlersRegistered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) => Current?.OnWindowLoaded((Window)sender)));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((sender, _) => Current?.OnContextMenuOpened((ContextMenu)sender)));
        // Nothing is under the pointer, but no automatic tooltip may open during the audit.
        EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTipService.ToolTipOpeningEvent, new ToolTipEventHandler((_, e) => { if (Current is not null) e.Handled = true; }));
    }

    private static void StartWatchdog(string output)
    {
        Beat("avvio");
        new Thread(() =>
        {
            while (true)
            {
                Thread.Sleep(5000);
                long beat = Interlocked.Read(ref lastBeat);
                if (beat == -1) return;
                long idle = Environment.TickCount64 - beat;
                if (idle < 300_000) continue;
                try { File.WriteAllText(Path.Combine(output, "errore.txt"), $"Verifica bloccata da {idle / 1000} s durante: {Volatile.Read(ref step)}"); } catch { }
                Process.GetCurrentProcess().Kill();
            }
        }) { IsBackground = true, Name = "Contrasto · sorveglianza" }.Start();
    }

    internal static void Beat(string current) { Volatile.Write(ref step, current); Interlocked.Exchange(ref lastBeat, Environment.TickCount64); }

    internal bool Want(string view) => filter is null || filter.IsMatch(view);

    private void BeginMode(AppAppearance mode) { Mode = mode; names.Clear(); Appearance.Set(mode, false); }

    /// <summary>Main window off screen: native handle without showing it (Loaded and the theme handler work), fixed
    /// 1600×1000 client area, no frame; shown off screen and without activation only if the handle alone gives no Loaded.</summary>
    internal async Task<MainWindow> OpenMain(AppAppearance mode)
    {
        Appearance.Set(mode, false);
        var window = new MainWindow(TestServices.Create);
        Main = window; seenWindows.Add(window);
        window.PrepareContrastAudit();
        window.WindowStyle = WindowStyle.None; window.ResizeMode = ResizeMode.NoResize; window.ShowInTaskbar = false; window.ShowActivated = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = OffscreenWindows.Offscreen; window.Top = OffscreenWindows.Offscreen;
        window.Width = CaptureWidth; window.Height = CaptureHeight;
        var handle = new WindowInteropHelper(window).EnsureHandle();
        dpi = OffscreenWindows.ForceSize(handle, CaptureWidth, CaptureHeight);
        // The handle alone gives no Loaded: the window is shown, at (-20000, -20000), cloaked and not activated.
        window.Show(); await Settle(100);
        if (!window.IsLoaded) Note("finestra principale", "Loaded non generato");
        CheckPosition(window, "finestra principale");
        return window;
    }

    internal async Task CloseMain(MainWindow window)
    {
        try { window.FinishSmoke(); window.Close(); } catch (Exception ex) { Note("chiusura", ex.Message); }
        Main = null; await Settle();
    }

    internal async Task Step(string view, Func<Task> body)
    {
        if (!Want(view)) return;
        string previous = Context; Context = view; Beat(view);
        try { await body(); }
        catch (Exception ex) { Note(view, "Errore: " + ex); }
        finally { Context = previous; }
    }

    internal async Task Settle(int delay = 30)
    {
        Beat(Context);
        var dispatcher = Dispatcher.CurrentDispatcher;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (delay > 0) await Task.Delay(delay);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    internal async Task<bool> WaitUntil(Func<bool> condition, double seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.UtcNow > until) return false;
            await Task.Delay(40); Beat(Context);
        }
        return true;
    }

    /// <summary>Runs a command that may open windows or popups and waits until the hook has captured and closed them.</summary>
    internal async Task<bool> Trigger(string what, Action action, double seconds = 3)
    {
        int before = Processed, busy = InFlight;
        Beat(Context + " · " + what);
        try { action(); }
        catch (Exception ex) { Note(Context, what + ": " + ex.Message); }
        await WaitUntil(() => Processed > before && InFlight <= busy, seconds);
        await WaitUntil(() => InFlight <= busy, 90);
        await Settle();
        return Processed > before;
    }

    internal void Note(string view, string message) => notes.Add(new JsonObject { ["vista"] = view, ["modalita"] = Mode.ToString(), ["nota"] = message });

    internal void Uncovered(string window, string reason)
    {
        if (uncovered.OfType<JsonObject>().Any(u => u.S("finestra") == window)) return;
        uncovered.Add(new JsonObject { ["finestra"] = window, ["motivo"] = reason });
    }

    internal void RecordTriggers(string module, IEnumerable<string> labels)
    {
        if (!triggersFound.TryGetValue(module, out var set)) triggersFound[module] = set = [];
        set.UnionWith(labels);
    }

    // ---------------------------------------------------------------- windows and popups

    private void OnWindowLoaded(Window window)
    {
        if (ReferenceEquals(window, Main) || !seenWindows.Add(window)) return;
        InFlight++;
        window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            try { await ProcessWindow(window); }
            catch (Exception ex) { Note(Context, "Finestra " + window.Title + ": " + ex.Message); try { window.Close(); } catch { } }
            finally { InFlight--; Processed++; }
        }));
    }

    private void CheckPosition(Window window, string name)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var (rect, visible) = OffscreenWindows.Where(handle);
        bool offscreen = rect.Right <= -10000 || rect.Bottom <= -10000, cloaked = OffscreenWindows.IsCloaked(handle), activated = OffscreenWindows.ForegroundIsOurs();
        windows.Add(new JsonObject { ["finestra"] = name, ["modalita"] = Mode.ToString(), ["sinistra"] = rect.Left, ["alto"] = rect.Top, ["larghezza"] = rect.Right - rect.Left,
            ["altezza"] = rect.Bottom - rect.Top, ["visibile_win32"] = visible, ["occultata_dwm"] = cloaked, ["fuori_schermo"] = offscreen, ["attiva"] = activated });
        if (!offscreen || visible && !cloaked || activated)
            violations.Add(new JsonObject { ["finestra"] = name, ["modalita"] = Mode.ToString(), ["posizione"] = $"{rect.Left},{rect.Top}", ["occultata"] = cloaked, ["attivata"] = activated });
    }

    private async Task ProcessWindow(Window window)
    {
        string title = window.Title ?? "finestra";
        capturedTitles.Add(title);
        string previous = Context, name = Context + "--" + Slug(title, 48);
        try
        {
            await Settle(150);
            CheckPosition(window, title);
            if (window is MainWindow copy) await copy.AuditWaitEditor(this, false);
            foreach (var workspace in Ui.Descendants<ConcreteWorkspace>(window)) await WaitUntil(() => !workspace.Busy, 60);
            await Settle(60);
            var root = WindowRoot(window);
            await ExploreTabs(window, name, 0, 2, [], new LeafBudget(14), async (leaf, _) => { await Settle(); await SnapStable(leaf, root, "finestra"); }, _ => Settle(60));
            var collapsed = Ui.Descendants<Expander>(window).Where(x => !x.IsExpanded && x.IsVisible).ToList();
            if (collapsed.Count > 0)
            {
                foreach (var expander in collapsed) expander.IsExpanded = true;
                await Settle(80); await SnapStable(name + "-espansa", root, "finestra");
            }
            Context = name;
            foreach (var (prefix, action) in DialogActions.ToList())
                if (title.StartsWith(prefix, StringComparison.Ordinal)) await action(window);
        }
        finally
        {
            Context = previous;
            if (window.IsLoaded) window.Close();
        }
    }

    private void OnContextMenuOpened(ContextMenu menu)
    {
        InFlight++;
        menu.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            try
            {
                string first = menu.Items.OfType<MenuItem>().Select(i => i.Header?.ToString()).FirstOrDefault(h => !string.IsNullOrWhiteSpace(h)) ?? "voci";
                if (PopupRootOf(menu) is { } root) await SnapPopup(Context + "--menu-" + Slug(first, 24), root);
            }
            catch (Exception ex) { Note(Context, "Menu contestuale: " + ex.Message); }
            finally { menu.IsOpen = false; InFlight--; Processed++; }
        }));
    }

    internal async Task SnapSubmenu(MenuItem item, string name)
    {
        item.IsSubmenuOpen = true; await Settle();
        if (item.Template?.FindName("PART_Popup", item) is Popup { Child: Visual child } && PopupRootOf(child) is { } root) await SnapPopup(name, root);
        else Note(name, "sottomenu non aperto");
        item.IsSubmenuOpen = false; await Settle();
    }

    internal async Task SnapComboBox(ComboBox combo, string name)
    {
        combo.IsDropDownOpen = true; await Settle();
        if (combo.Template?.FindName("PART_Popup", combo) is Popup { Child: Visual child } && PopupRootOf(child) is { } root) await SnapPopup(name, root);
        else Note(name, "elenco a discesa non aperto");
        combo.IsDropDownOpen = false; await Settle();
    }

    internal async Task SnapToolTip(FrameworkElement target, string name)
    {
        var tip = target.ToolTip as ToolTip ?? new ToolTip { Content = target.ToolTip };
        tip.PlacementTarget = target; tip.Placement = PlacementMode.Bottom; tip.IsOpen = true;
        await Settle();
        if (PopupRootOf(tip) is { } root) await SnapPopup(name, root); else Note(name, "suggerimento non aperto");
        tip.IsOpen = false; await Settle();
    }

    /// <summary>Popups open with the system animation (fade or slide).</summary>
    private Task SnapPopup(string name, FrameworkElement root) => SnapStable(name, root, "popup", first: 150);

    /// <summary>Captured once two renders 150 ms apart are identical (popup animations, updates that follow a calculation
    /// or a tab change), so that the captures of the light appearance can be compared pixel by pixel between runs.</summary>
    internal async Task SnapStable(string view, FrameworkElement root, string kind, int first = 0)
    {
        if (names.Contains(Name(view))) return;
        byte[]? previous = null;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (attempt > 0 || first > 0) await Settle(attempt == 0 ? first : 150);
            root.UpdateLayout();
            int width = (int)Math.Ceiling(root.ActualWidth), height = (int)Math.Ceiling(root.ActualHeight);
            if (width < 2 || height < 2) continue;
            var pixels = new byte[width * height * 4]; Render(root, width, height).CopyPixels(pixels, width * 4, 0);
            if (previous is not null && previous.AsSpan().SequenceEqual(pixels)) break;
            previous = pixels;
            if (attempt == 15) Note(view, "cattura non stabile dopo 16 tentativi");
        }
        Snap(view, root, kind);
    }

    private string Name(string view) => string.Join("--", view.Split("--").Select(part => Slug(part, 90))) + "-" + Mode;

    internal static FrameworkElement? PopupRootOf(Visual visual) => PresentationSource.FromVisual(visual)?.RootVisual as FrameworkElement;

    internal static FrameworkElement WindowRoot(Window window) =>
        VisualTreeHelper.GetChildrenCount(window) > 0 && VisualTreeHelper.GetChild(window, 0) is FrameworkElement root ? root : window;

    // ---------------------------------------------------------------- tabs, pages and commands

    internal sealed class LeafBudget(int limit)
    {
        private int used;
        internal bool Take() => used++ < limit;
        internal bool Exhausted => used > limit;
    }

    internal readonly Dictionary<TabControl, int> TabDefaults = new();

    /// <summary>Depth-first visit of the tabs: every tab of every visible TabControl, the nested ones inside the selected
    /// tab; sibling TabControls are varied one at a time. The original selection is restored afterwards.</summary>
    internal async Task ExploreTabs(FrameworkElement scope, string name, int depth, int maxDepth, List<(TabControl Tabs, int Index)> path, LeafBudget budget,
        Func<string, List<(TabControl Tabs, int Index)>, Task> leaf, Func<int, Task> changed)
    {
        var controls = OutermostTabControls(scope).Where(t => t.IsVisible && t.Items.Count > 0).ToList();
        if (controls.Count == 0 || depth >= maxDepth) { if (budget.Take()) await leaf(name, path); return; }
        for (int c = 0; c < controls.Count; c++)
        {
            var tabs = controls[c]; int original = Math.Max(0, tabs.SelectedIndex);
            TabDefaults.TryAdd(tabs, original);
            for (int i = 0; i < tabs.Items.Count; i++)
            {
                if (c > 0 && i == original) continue;
                if (tabs.Items[i] is TabItem { Visibility: not Visibility.Visible } or TabItem { IsEnabled: false }) continue;
                if (tabs.SelectedIndex != i) { tabs.SelectedIndex = i; await changed(depth); }
                string segment = (controls.Count > 1 ? $"c{c + 1}" : "") + $"t{i + 1}-" + Slug(Header(tabs.Items[i]), 16);
                path.Add((tabs, i));
                var host = tabs.Template?.FindName("PART_SelectedContentHost", tabs) as FrameworkElement ?? tabs;
                await ExploreTabs(host, name + "-" + segment, depth + 1, maxDepth, path, budget, leaf, changed);
                path.RemoveAt(path.Count - 1);
            }
            if (tabs.SelectedIndex != original) { tabs.SelectedIndex = original; await changed(depth); }
        }
    }

    /// <summary>Back to the default tabs, then the recorded route of a command.</summary>
    internal async Task<bool> Reselect(List<(TabControl Tabs, int Index)> route)
    {
        foreach (var (tabs, index) in TabDefaults)
            if (tabs.IsLoaded && tabs.SelectedIndex != index) { tabs.SelectedIndex = index; await Settle(); }
        foreach (var (tabs, index) in route)
        {
            if (!tabs.IsLoaded || !tabs.IsVisible) return false;
            if (tabs.SelectedIndex != index) { tabs.SelectedIndex = index; await Settle(60); }
        }
        return true;
    }

    private static List<TabControl> OutermostTabControls(DependencyObject scope)
    {
        var found = new List<TabControl>();
        void Visit(DependencyObject node)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is UIElement { Visibility: not Visibility.Visible }) continue;
                if (child is TabControl tabs) { found.Add(tabs); continue; }
                Visit(child);
            }
        }
        Visit(scope); return found;
    }

    private static string Header(object item)
    {
        object? header = item is HeaderedContentControl h ? h.Header : item;
        return header switch
        {
            string s => s,
            TextBlock t => t.Text,
            DependencyObject d => Ui.Descendants<TextBlock>(d).Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "scheda",
            _ => header?.ToString() ?? "scheda"
        };
    }

    /// <summary>Further pages of the largest scrollable area of the view (at most three), then back to the top.</summary>
    internal async Task SnapPages(FrameworkElement scope, string name, FrameworkElement root)
    {
        var viewer = Ui.Descendants<ScrollViewer>(scope).Where(s => s.IsVisible && s.ScrollableHeight > 40 && s.ViewportHeight > 150 && s.ActualHeight > 150)
            .OrderByDescending(s => s.ActualWidth * s.ActualHeight).FirstOrDefault();
        if (viewer is null) return;
        double original = viewer.VerticalOffset;
        for (int page = 2; page <= 4; page++)
        {
            double target = Math.Min(viewer.ScrollableHeight, (page - 1) * viewer.ViewportHeight * 0.9);
            if (target <= viewer.VerticalOffset + 1) break;
            viewer.ScrollToVerticalOffset(target); await Settle(60);
            await SnapStable(name + "-p" + page, root, "vista");
            if (viewer.VerticalOffset >= viewer.ScrollableHeight - 1) break;
        }
        viewer.ScrollToVerticalOffset(original); await Settle();
    }

    internal static string ButtonLabel(ButtonBase button) => button.Content switch
    {
        string s => s.Trim(),
        TextBlock t => t.Text.Trim(),
        DependencyObject d => Ui.Descendants<TextBlock>(d).Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))?.Trim() ?? "",
        _ => button.Content?.ToString()?.Trim() ?? ""
    };

    internal static Button? FindButton(DependencyObject scope, string label, bool enabledOnly = true) =>
        Ui.Descendants<Button>(scope).FirstOrDefault(b => b.IsVisible && (!enabledOnly || b.IsEnabled) && ButtonLabel(b) == label);

    internal static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    internal static FrameworkElement? FirstWithToolTip(DependencyObject scope) =>
        Ui.Descendants<FrameworkElement>(scope).FirstOrDefault(e => e.IsVisible && e.ToolTip is string { Length: > 0 });

    internal static FrameworkElement? FirstWithContextMenu(DependencyObject scope) =>
        Ui.Descendants<FrameworkElement>(scope).FirstOrDefault(e => e.IsVisible && e.ContextMenu is not null);

    // ---------------------------------------------------------------- capture and measure

    internal void Snap(string view, FrameworkElement root, string kind)
    {
        string name = Name(view);
        if (!names.Add(name)) return;
        Beat(name);
        root.UpdateLayout();
        int width = (int)Math.Ceiling(root.ActualWidth), height = (int)Math.Ceiling(root.ActualHeight);
        if (width < 2 || height < 2) { Note(view, "cattura vuota"); return; }
        var bitmap = Render(root, width, height);
        SavePng(bitmap, Path.Combine(OutputDirectory, name + ".png"));
        var pixels = new byte[width * height * 4]; bitmap.CopyPixels(pixels, width * 4, 0);
        var marks = Analyze(view, name, root, pixels, width, height, out int texts);
        if (marks.Count > 0) Annotate(bitmap, marks, Path.Combine(OutputDirectory, "difetti", name + ".png"));
        captures.Add(new JsonObject { ["vista"] = view, ["modalita"] = Mode.ToString(), ["tipo"] = kind, ["file"] = name + ".png", ["larghezza"] = width,
            ["altezza"] = height, ["testi"] = texts, ["segnalazioni"] = marks.Count(m => !m.Info), ["informazioni"] = marks.Count(m => m.Info) });
    }

    private static RenderTargetBitmap Render(FrameworkElement root, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        if (VisualTreeHelper.GetOffset(root) == default && root.RenderTransform.Value.IsIdentity) bitmap.Render(root);
        else
        {
            var visual = new DrawingVisual(); var area = new Rect(0, 0, width, height);
            using (var dc = visual.RenderOpen())
                dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = area, ViewportUnits = BrushMappingMode.Absolute, Viewport = area }, null, area);
            bitmap.Render(visual);
        }
        bitmap.Freeze(); return bitmap;
    }

    private static void SavePng(BitmapSource bitmap, string file)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); encoder.Save(stream);
    }

    private static void Annotate(BitmapSource bitmap, List<(Rect Rect, bool Info)> marks, string file)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            var red = new Pen(Brushes.Red, 1.5); var orange = new Pen(Brushes.Orange, 1);
            foreach (var (rect, info) in marks.OrderBy(m => !m.Info)) { var r = rect; r.Inflate(2, 2); dc.DrawRectangle(null, info ? orange : red, r); }
        }
        var output = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32); output.Render(visual); SavePng(output, file);
    }

    private sealed record Probe(FrameworkElement Owner, Visual Ink, string Text, double Size, int Weight, Brush? Foreground, double Margin);
    private sealed record Provider(DependencyObject Element, Brush Brush, DependencyProperty Property);

    private List<(Rect Rect, bool Info)> Analyze(string view, string capture, FrameworkElement root, byte[] pixels, int width, int height, out int texts)
    {
        var marks = new List<(Rect Rect, bool Info)>();
        var probes = new List<Probe>(); var excluded = new List<(FrameworkElement Element, string Key, string Reason)>(); var providers = new List<Provider>();
        void Visit(DependencyObject node)
        {
            if (node is UIElement { Visibility: not Visibility.Visible }) return;
            switch (node)
            {
                case TextBlock tb when !string.IsNullOrWhiteSpace(tb.Text):
                    probes.Add(new(tb, tb, tb.Text, tb.FontSize, tb.FontWeight.ToOpenTypeWeight(), tb.Foreground, 1)); break;
                case TextBox box when !string.IsNullOrWhiteSpace(box.Text) && Find(box, d => d.GetType().Name == "TextBoxView") is Visual textView:
                    probes.Add(new(box, textView, box.Text, box.FontSize, box.FontWeight.ToOpenTypeWeight(), box.Foreground, 0)); break;
                case Control formula when formula.GetType().Name == "FormulaControl":
                    double scale = formula.GetType().GetProperty("Scale")?.GetValue(formula) is double s ? s : formula.FontSize;
                    probes.Add(new(formula, formula, "formula: " + (formula.GetType().GetProperty("Formula")?.GetValue(formula) ?? ""), scale, 400, formula.Foreground, 0)); break;
            }
            if (Exclusion(node) is { } reason && node is FrameworkElement excludedElement) excluded.Add((excludedElement, reason.Key, reason.Reason));
            switch (node)
            {
                case Border { Background: { } b } border: providers.Add(new(border, b, Border.BackgroundProperty)); break;
                case Panel { Background: { } b } panel: providers.Add(new(panel, b, Panel.BackgroundProperty)); break;
                case TextBlock { Background: { } b } block: providers.Add(new(block, b, TextBlock.BackgroundProperty)); break;
                case System.Windows.Shapes.Shape { Fill: { } b } shape: providers.Add(new(shape, b, System.Windows.Shapes.Shape.FillProperty)); break;
            }
            if (node is Viewport3D) return;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Visit(VisualTreeHelper.GetChild(node, i));
        }
        Visit(root);
        texts = 0;
        foreach (var probe in probes)
        {
            var rect = VisibleRect(probe.Ink, probe.Owner, root, probe.Margin, width, height);
            if (rect is null) continue;
            texts++;
            var pixel = PixelContrast(pixels, width, height, rect.Value);
            var property = PropertyContrast(probe);
            bool large = probe.Size >= 24 || probe.Size >= 18.5 && probe.Weight >= 600;
            double threshold = large ? 3 : 4.5;
            bool disabled = !probe.Owner.IsEnabled;
            double? ratio = pixel?.Ratio ?? property?.Ratio;
            string? type = null;
            if (disabled) { if (ratio < 2.5) type = "info-disabilitato"; }
            else if (ratio < threshold) type = "contrasto-testo";
            else if (property?.Ratio < threshold) type = "avviso-proprieta";
            if (type is null) continue;
            var defect = TextDefect(type, view, capture, root, probe, rect.Value, pixel, property, threshold, large, disabled);
            if (type == "contrasto-testo") defects.Add(defect); else infos.Add(defect);
            marks.Add((rect.Value, type != "contrasto-testo"));
        }
        if (Mode != AppAppearance.Light) LightAreas(view, capture, root, pixels, width, height, excluded, providers, marks);
        return marks;
    }

    private static DependencyObject? Find(DependencyObject root, Func<DependencyObject, bool> match)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (match(child)) return child;
            if (Find(child, match) is { } found) return found;
        }
        return null;
    }

    private static readonly Dictionary<Type, bool> customRender = new();
    private static bool CustomRender(Type type)
    {
        if (customRender.TryGetValue(type, out bool known)) return known;
        var method = type.GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, [typeof(DrawingContext)], null);
        string? assembly = method?.DeclaringType?.Assembly.GetName().Name;
        bool custom = assembly is not null && assembly is not ("PresentationFramework" or "PresentationCore" or "WindowsBase") && !assembly.StartsWith("PresentationFramework.", StringComparison.Ordinal);
        customRender[type] = custom; return custom;
    }

    /// <summary>Elements whose light areas are intended content (documented in the report).</summary>
    private static (string Key, string Reason)? Exclusion(DependencyObject node) => node switch
    {
        Image => ("Image", "immagine bitmap (logo, icone dei moduli e figure): contenuto grafico voluto"),
        Viewport3D => ("Viewport3D", "vista 3D del dominio: disegno tecnico"),
        MediaElement => ("MediaElement", "contenuto multimediale"),
        System.Windows.Shapes.Shape { TemplatedParent: null } shape => ("Shape:" + shape.GetType().Name, "forma vettoriale costruita nel codice: parte di un disegno o di un'icona"),
        Control c when c.GetType().Name == "FormulaControl" => ("FormulaControl", "formula WpfMath: disegno del testo matematico"),
        FrameworkElement e when CustomRender(e.GetType()) => ("OnRender:" + e.GetType().Name, "disegno, grafico o icona disegnati in OnRender (" + e.GetType().Name + ")"),
        _ => null
    };

    private static Rect? VisibleRect(Visual ink, UIElement owner, FrameworkElement root, double margin, int width, int height)
    {
        if (!owner.IsVisible) return null;
        Rect local = ink is TextBlock ? VisualTreeHelper.GetContentBounds(ink) : VisualTreeHelper.GetDescendantBounds(ink);
        if (local.IsEmpty || local.Width <= 0 || local.Height <= 0) return null;
        Rect rect;
        try { rect = ink.TransformToAncestor(root).TransformBounds(local); } catch (InvalidOperationException) { return null; }
        var full = rect;
        double opacity = 1;
        for (DependencyObject? a = ink; a is not null && !ReferenceEquals(a, root); a = VisualTreeHelper.GetParent(a))
        {
            if (a is UIElement element) { if (element.Visibility != Visibility.Visible) return null; opacity *= element.Opacity; }
            if (a is Visual visual && VisualTreeHelper.GetClip(visual) is Geometry clip)
            {
                rect.Intersect(visual.TransformToAncestor(root).TransformBounds(clip.Bounds));
                if (rect.IsEmpty) return null;
            }
        }
        if (opacity < 0.05) return null;
        rect.Intersect(new Rect(0, 0, width, height));
        // A text cut by the edge of a scrolling area or of the capture shows only a sliver of its glyphs: not measurable.
        if (rect.IsEmpty || rect.Height < Math.Min(full.Height, 12) * 0.6 || rect.Width < Math.Min(full.Width, 12) * 0.6) return null;
        rect.Inflate(margin, margin);
        rect.Intersect(new Rect(0, 0, width, height));
        return rect.IsEmpty || rect.Width < 2 || rect.Height < 2 ? null : rect;
    }

    private static Rect? ElementRect(DependencyObject element, FrameworkElement root, int width, int height)
    {
        if (element is not UIElement ui || element is not Visual) return null;
        var visual = (Visual)element;
        Rect rect;
        try { rect = visual.TransformToAncestor(root).TransformBounds(new Rect(ui.RenderSize)); } catch (InvalidOperationException) { return null; }
        for (DependencyObject? a = visual; a is not null && !ReferenceEquals(a, root); a = VisualTreeHelper.GetParent(a))
        {
            if (a is UIElement { Visibility: not Visibility.Visible }) return null;
            if (a is Visual v && VisualTreeHelper.GetClip(v) is Geometry clip)
            {
                rect.Intersect(v.TransformToAncestor(root).TransformBounds(clip.Bounds));
                if (rect.IsEmpty) return null;
            }
        }
        rect.Intersect(new Rect(0, 0, width, height));
        return rect.IsEmpty ? null : rect;
    }

    private sealed record PixelResult(double Ratio, Color Background, Color Text);
    private sealed record PropertyResult(double Ratio, Color Text, Color Background, DependencyObject? BackgroundElement, DependencyProperty? BackgroundProperty, bool Partial);

    private static PixelResult? PixelContrast(byte[] pixels, int width, int height, Rect rect)
    {
        int x0 = Math.Max(0, (int)Math.Floor(rect.X)), y0 = Math.Max(0, (int)Math.Floor(rect.Y));
        int x1 = Math.Min(width, (int)Math.Ceiling(rect.Right)), y1 = Math.Min(height, (int)Math.Ceiling(rect.Bottom));
        if (x1 - x0 < 2 || y1 - y0 < 2) return null;
        var bins = new Dictionary<int, (int N, long R, long G, long B)>();
        int total = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                int i = (y * width + x) * 4; int a = pixels[i + 3]; if (a < 200) continue;
                int r = pixels[i + 2] * 255 / a, g = pixels[i + 1] * 255 / a, b = pixels[i] * 255 / a;
                int key = (r >> 3) << 10 | (g >> 3) << 5 | (b >> 3);
                bins.TryGetValue(key, out var bin); bins[key] = (bin.N + 1, bin.R + r, bin.G + g, bin.B + b); total++;
            }
        if (total < 6) return null;
        var mode = bins.Values.MaxBy(v => v.N);
        var background = Color.FromRgb((byte)(mode.R / mode.N), (byte)(mode.G / mode.N), (byte)(mode.B / mode.N));
        double best = -1; var text = background;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                int i = (y * width + x) * 4; int a = pixels[i + 3]; if (a < 200) continue;
                int r = pixels[i + 2] * 255 / a, g = pixels[i + 1] * 255 / a, b = pixels[i] * 255 / a;
                double d = Math.Pow(r - background.R, 2) + Math.Pow(g - background.G, 2) + Math.Pow(b - background.B, 2);
                if (d > best) { best = d; text = Color.FromRgb((byte)r, (byte)g, (byte)b); }
            }
        return new(Contrast(background, text), background, text);
    }

    private static PropertyResult? PropertyContrast(Probe probe)
    {
        if (BrushColor(probe.Foreground) is not Color foreground) return null;
        var layers = new List<(Color Color, DependencyObject Element, DependencyProperty Property)>();
        for (DependencyObject? a = probe.Ink; a is not null; a = VisualTreeHelper.GetParent(a))
        {
            Brush? brush = null; DependencyProperty? property = null;
            if (a is Border b) { brush = b.Background; property = Border.BackgroundProperty; }
            else if (a is Panel p) { brush = p.Background; property = Panel.BackgroundProperty; }
            else if (a is TextBlock t) { brush = t.Background; property = TextBlock.BackgroundProperty; }
            if (brush is null) continue;
            if (BrushColor(brush) is not Color color) break;
            if (color.A == 0) continue;
            layers.Add((color, a, property!));
            if (color.A >= 250) break;
        }
        if (layers.Count == 0) return null;
        bool partial = layers[^1].Color.A < 250;
        var background = partial ? Over(layers[^1].Color, Colors.White) : Opaque(layers[^1].Color);
        for (int i = layers.Count - 2; i >= 0; i--) background = Over(layers[i].Color, background);
        var text = foreground.A < 255 ? Over(foreground, background) : foreground;
        return new(Contrast(text, background), text, background, layers[0].Element, layers[0].Property, partial);
    }

    private static Color? BrushColor(Brush? brush)
    {
        switch (brush)
        {
            case SolidColorBrush solid: { var c = solid.Color; c.A = (byte)Math.Round(c.A * Math.Clamp(solid.Opacity, 0, 1)); return c; }
            case GradientBrush { GradientStops.Count: > 0 } gradient:
            {
                double a = 0, r = 0, g = 0, b = 0; int n = gradient.GradientStops.Count;
                foreach (var stop in gradient.GradientStops) { a += stop.Color.A; r += stop.Color.R; g += stop.Color.G; b += stop.Color.B; }
                return Color.FromArgb((byte)(a / n * Math.Clamp(gradient.Opacity, 0, 1)), (byte)(r / n), (byte)(g / n), (byte)(b / n));
            }
            default: return null;
        }
    }

    private static Color Opaque(Color c) { c.A = 255; return c; }
    private static Color Over(Color top, Color bottom)
    {
        double a = top.A / 255.0;
        return Color.FromRgb((byte)Math.Round(top.R * a + bottom.R * (1 - a)), (byte)Math.Round(top.G * a + bottom.G * (1 - a)), (byte)Math.Round(top.B * a + bottom.B * (1 - a)));
    }

    private static readonly double[] Linear = Enumerable.Range(0, 256).Select(v => { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }).ToArray();
    internal static double Luminance(Color c) => 0.2126 * Linear[c.R] + 0.7152 * Linear[c.G] + 0.0722 * Linear[c.B];
    internal static double Contrast(Color a, Color b)
    {
        double l1 = Luminance(a), l2 = Luminance(b);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private void LightAreas(string view, string capture, FrameworkElement root, byte[] pixels, int width, int height,
        List<(FrameworkElement Element, string Key, string Reason)> excluded, List<Provider> providers, List<(Rect Rect, bool Info)> marks)
    {
        var light = new bool[width * height];
        for (int i = 0, p = 0; i < light.Length; i++, p += 4)
        {
            int a = pixels[p + 3]; if (a < 200) continue;
            double l = 0.2126 * Linear[pixels[p + 2] * 255 / a] + 0.7152 * Linear[pixels[p + 1] * 255 / a] + 0.0722 * Linear[pixels[p] * 255 / a];
            light[i] = l > 0.8;
        }
        var mask = new bool[width * height];
        foreach (var (element, key, reason) in excluded)
        {
            if (ElementRect(element, root, width, height) is not Rect r) continue;
            int x0 = (int)Math.Floor(r.X), y0 = (int)Math.Floor(r.Y), x1 = Math.Min(width, (int)Math.Ceiling(r.Right)), y1 = Math.Min(height, (int)Math.Ceiling(r.Bottom));
            long count = 0;
            for (int y = Math.Max(0, y0); y < y1; y++) for (int x = Math.Max(0, x0); x < x1; x++) { int i = y * width + x; if (light[i] && !mask[i]) count++; mask[i] = true; }
            exclusions.TryGetValue(key, out var total); exclusions[key] = (total.Count + 1, total.Pixels + count, reason);
            if (count > 2000) excludedLight.Add(new JsonObject { ["vista"] = view, ["modalita"] = Mode.ToString(), ["elemento"] = element.GetType().Name, ["motivo"] = reason, ["pixel_chiari"] = count, ["rettangolo"] = RectJson(r) });
        }
        var seen = new bool[width * height]; var stack = new Stack<int>();
        for (int start = 0; start < light.Length; start++)
        {
            if (!light[start] || mask[start] || seen[start]) continue;
            int area = 0, minX = width, minY = height, maxX = 0, maxY = 0; long sumX = 0, sumY = 0; var members = new List<int>();
            seen[start] = true; stack.Push(start);
            while (stack.Count > 0)
            {
                int i = stack.Pop(); int x = i % width, y = i / width; area++; sumX += x; sumY += y;
                if (members.Count < 400000) members.Add(i);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                if (x > 0) Push(i - 1); if (x < width - 1) Push(i + 1); if (y > 0) Push(i - width); if (y < height - 1) Push(i + width);
            }
            void Push(int j) { if (light[j] && !mask[j] && !seen[j]) { seen[j] = true; stack.Push(j); } }
            int boxW = maxX - minX + 1, boxH = maxY - minY + 1;
            if (area <= 2000 || boxW < 4 || boxH < 4) continue;
            double cx = (double)sumX / area, cy = (double)sumY / area;
            int sample = members.MinBy(i => Math.Pow(i % width - cx, 2) + Math.Pow(i / width - cy, 2));
            var point = new Point(sample % width + 0.5, sample / width + 0.5);
            Provider? source = null;
            for (int k = providers.Count - 1; k >= 0; k--)
            {
                var candidate = providers[k];
                if (BrushColor(candidate.Brush) is not Color c || c.A < 128 || Luminance(c) <= 0.8) continue;
                if (ElementRect(candidate.Element, root, width, height) is Rect r && r.Contains(point)) { source = candidate; break; }
            }
            if (source is not null && Exclusion(source.Element) is not null) continue;
            int p0 = sample * 4;
            var seenColor = Color.FromRgb(pixels[p0 + 2], pixels[p0 + 1], pixels[p0]);
            var rect = new Rect(minX, minY, boxW, boxH);
            var defect = new JsonObject
            {
                ["id"] = $"D{++counter:0000}", ["tipo"] = "area-chiara", ["vista"] = view, ["modalita"] = Mode.ToString(), ["cattura"] = capture + ".png",
                ["area_px"] = area, ["luminanza"] = Math.Round(Luminance(seenColor), 3), ["colore_visto"] = Hex(seenColor), ["rettangolo"] = RectJson(rect),
                ["punto"] = $"{(int)point.X},{(int)point.Y}"
            };
            if (source is not null)
            {
                var (owner, property) = TemplateOwner(source.Element, source.Property, source.Brush);
                defect["elemento"] = Describe(owner, root);
                defect["sfondo"] = BrushInfo(owner, property, "background");
                defect["sorgenti"] = SourceLines((JsonObject)defect["sfondo"]!, owner, root);
                defect["creazione"] = new JsonArray(sources.FindText(defect["elemento"]?["testo"]?.ToString(), AntheaTypes(owner, root)).Select(l => (JsonNode?)l).ToArray());
                defect["chiave"] = $"area|{owner.GetType().Name}|{defect["elemento"]!["tipi_anthea"]}|{defect["sfondo"]!["colore_originale"]}|{defect["sfondo"]!["origine_valore"]}";
            }
            else { defect["elemento"] = new JsonObject { ["tipo"] = "non identificato" }; defect["chiave"] = $"area|?|{view}|{Hex(seenColor)}"; }
            defects.Add(defect); marks.Add((rect, false));
        }
    }

    private JsonObject TextDefect(string type, string view, string capture, FrameworkElement root, Probe probe, Rect rect, PixelResult? pixel, PropertyResult? property,
        double threshold, bool large, bool disabled)
    {
        var element = Describe(probe.Owner, root);
        var (fgOwner, fgProperty) = ForegroundOwner(probe.Owner);
        var foreground = BrushInfo(fgOwner, fgProperty, "foreground");
        JsonObject? background = null;
        if (property?.BackgroundElement is { } bgElement)
        {
            var bgBrush = (Brush?)bgElement.GetValue(property.BackgroundProperty!);
            var (owner, prop) = TemplateOwner(bgElement, property.BackgroundProperty!, bgBrush);
            background = BrushInfo(owner, prop, "background");
        }
        var defect = new JsonObject
        {
            ["id"] = $"D{++counter:0000}", ["tipo"] = type, ["vista"] = view, ["modalita"] = Mode.ToString(), ["cattura"] = capture + ".png",
            ["elemento"] = element, ["rapporto_pixel"] = pixel is null ? null : Math.Round(pixel.Ratio, 2), ["rapporto_proprieta"] = property is null ? null : Math.Round(property.Ratio, 2),
            ["soglia"] = threshold, ["testo_grande"] = large, ["disabilitato"] = disabled, ["dimensione_px"] = Math.Round(probe.Size, 1), ["peso"] = probe.Weight,
            ["testo_visto"] = pixel is null ? null : Hex(pixel.Text), ["sfondo_visto"] = pixel is null ? null : Hex(pixel.Background),
            ["testo_proprieta"] = property is null ? null : Hex(property.Text), ["sfondo_proprieta"] = property is null ? null : Hex(property.Background) + (property.Partial ? " (parziale)" : ""),
            ["primo_piano"] = foreground, ["sfondo"] = background, ["rettangolo"] = RectJson(rect)
        };
        var lines = new JsonArray();
        bool fgThemed = foreground["tematizzato"]?.GetValue<bool>() == true, bgThemed = background?["tematizzato"]?.GetValue<bool>() == true;
        // In the dark appearances the brush not taken from the palette is the most likely cause; when both are, both are listed.
        if (background is not null && (!bgThemed || fgThemed || Mode == AppAppearance.Light)) foreach (var line in SourceLines(background, probe.Owner, root)) lines.Add(line?.DeepClone());
        if (!fgThemed || bgThemed || background is null) foreach (var line in SourceLines(foreground, probe.Owner, root)) if (lines.Count < 8) lines.Add(line?.DeepClone());
        defect["sorgenti"] = lines;
        defect["creazione"] = new JsonArray(sources.FindText(probe.Text, AntheaTypes(probe.Owner, root)).Select(l => (JsonNode?)l).ToArray());
        // Same source: same element type in the same ANTHEA component, with the same colour assignments (owner, original colour, value source).
        defect["chiave"] = $"{type}|{element["tipo"]}|{element["tipi_anthea"]}|{foreground["proprietario"]} {foreground["colore_originale"]} {foreground["origine_valore"]}|" +
            $"{background?["proprietario"]} {background?["colore_originale"]} {background?["origine_valore"]}";
        return defect;
    }

    private static string Normalize(string text) { var t = Regex.Replace(text.Trim(), @"\s+", " "); return t.Length > 60 ? t[..60] : t; }

    private static (DependencyObject Owner, DependencyProperty Property) ForegroundOwner(DependencyObject element)
    {
        var property = TextElement.ForegroundProperty; DependencyObject? last = element; int guard = 0;
        for (DependencyObject? a = element; a is not null && guard++ < 300; a = LogicalTreeHelper.GetParent(a) ?? (a is Visual ? VisualTreeHelper.GetParent(a) : null))
        {
            last = a;
            var source = DependencyPropertyHelper.GetValueSource(a, property);
            if (source.BaseValueSource is not (BaseValueSource.Inherited or BaseValueSource.Default)) return (a, property);
        }
        return (last ?? element, property);
    }

    /// <summary>A template part bound to the colour of its control reports the control, where the colour is assigned.</summary>
    private static (DependencyObject Owner, DependencyProperty Property) TemplateOwner(DependencyObject element, DependencyProperty property, Brush? brush)
    {
        if (element is FrameworkElement { TemplatedParent: Control control } && brush is not null)
        {
            if (ReferenceEquals(control.Background, brush)) return (control, Control.BackgroundProperty);
            if (ReferenceEquals(control.Foreground, brush)) return (control, Control.ForegroundProperty);
            if (ReferenceEquals(control.BorderBrush, brush)) return (control, Control.BorderBrushProperty);
        }
        return (element, property);
    }

    private static readonly object? Origins = typeof(Appearance).GetField("origins", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
    private static (Color Color, string Role)? PaletteOrigin(SolidColorBrush brush)
    {
        if (Origins is null) return null;
        var args = new object?[] { brush, null };
        if (Origins.GetType().GetMethod("TryGetValue")?.Invoke(Origins, args) is not true || args[1] is not { } source) return null;
        var type = source.GetType();
        return ((Color)type.GetProperty("Color", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(source)!,
            (string)type.GetProperty("Role", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(source)!);
    }

    private static JsonObject BrushInfo(DependencyObject owner, DependencyProperty property, string role)
    {
        var brush = owner.GetValue(property) as Brush;
        var source = DependencyPropertyHelper.GetValueSource(owner, property);
        var info = new JsonObject
        {
            ["proprietario"] = Label(owner), ["proprieta"] = property.Name, ["origine_valore"] = source.BaseValueSource.ToString() + (source.IsExpression ? " (espressione)" : ""),
            ["ruolo_atteso"] = role, ["modello"] = (owner as FrameworkElement)?.TemplatedParent?.GetType().Name
        };
        switch (brush)
        {
            case SolidColorBrush solid:
                var origin = PaletteOrigin(solid);
                info["colore"] = Hex(solid.Color); info["colore_originale"] = Hex(origin?.Color ?? solid.Color);
                info["tematizzato"] = origin is not null; info["ruolo_palette"] = origin?.Role;
                info["congelato"] = solid.IsFrozen;
                break;
            case GradientBrush gradient:
                info["colore"] = "gradiente " + string.Join(" ", gradient.GradientStops.Select(s => Hex(s.Color))); info["colore_originale"] = info["colore"]!.GetValue<string>(); info["tematizzato"] = false; break;
            default:
                info["colore"] = brush?.GetType().Name ?? "nessuno"; info["colore_originale"] = info["colore"]!.GetValue<string>(); info["tematizzato"] = false; break;
        }
        return info;
    }

    private JsonArray SourceLines(JsonObject brush, DependencyObject element, FrameworkElement root)
    {
        var related = AntheaTypes(element, root);
        string hex = brush["colore_originale"]?.GetValue<string>() ?? "";
        string role = brush["ruolo_palette"]?.GetValue<string>() ?? brush["ruolo_atteso"]?.GetValue<string>() ?? "";
        var result = new JsonArray();
        if (!hex.StartsWith('#')) return result;
        bool themed = brush["tematizzato"]?.GetValue<bool>() == true;
        string ownerType = brush.S("proprietario").Split('#')[0];
        foreach (var line in sources.Find(hex, role, brush["proprieta"]?.GetValue<string>() ?? "", related, themed, elementType: ownerType)) result.Add(line);
        if (result.Count == 0)
            result.Add($"nessuna occorrenza di {hex} nei sorgenti di X.Desktop e X.Materiali" + (brush["modello"]?.ToString() is { Length: > 0 } template
                ? $": colore del modello WPF predefinito di {template} ({brush.S("proprietario")}, {brush.S("origine_valore")})" : $" ({brush.S("proprietario")}, {brush.S("origine_valore")})"));
        return result;
    }

    private static List<string> AntheaTypes(DependencyObject element, FrameworkElement root)
    {
        var types = new List<string>();
        for (DependencyObject? a = element; a is not null; a = VisualTreeHelper.GetParent(a))
        {
            if (a is FrameworkElement { TemplatedParent: { } parent } && IsAnthea(parent.GetType())) Add(parent.GetType().Name);
            if (IsAnthea(a.GetType())) Add(a.GetType().Name);
            if (ReferenceEquals(a, root)) break;
        }
        types.Reverse(); return types;
        void Add(string name) { if (types.Count == 0 || types[^1] != name) types.Add(name); }
    }

    private static bool IsAnthea(Type type) => type.Assembly.GetName().Name is "ANTHEA" or "Materiali";

    private static string Label(DependencyObject element) => element.GetType().Name + (element is FrameworkElement { Name.Length: > 0 } f ? "#" + f.Name : "");

    private static JsonObject Describe(DependencyObject element, FrameworkElement root)
    {
        var chain = new List<string>();
        for (DependencyObject? a = element; a is not null; a = VisualTreeHelper.GetParent(a)) { chain.Add(Label(a)); if (ReferenceEquals(a, root)) break; }
        chain.Reverse();
        string ancestors = chain.Count > 16 ? string.Join(" > ", chain.Take(3)) + " > … > " + string.Join(" > ", chain.TakeLast(12)) : string.Join(" > ", chain);
        string? text = element switch
        {
            TextBlock t => t.Text, TextBox t => t.Text, HeaderedContentControl { Header: string h } => h, ContentControl { Content: string s } => s,
            Window w => w.Title, _ => null
        };
        return new JsonObject
        {
            ["tipo"] = element.GetType().Name, ["nome"] = (element as FrameworkElement)?.Name is { Length: > 0 } n ? n : null,
            ["automazione"] = AutomationProperties.GetName(element) is { Length: > 0 } automation ? automation : null,
            ["testo"] = text is null ? null : (text.Length > 140 ? text[..140] + "…" : text),
            ["tipi_anthea"] = string.Join(" > ", AntheaTypes(element, root)), ["antenati"] = ancestors,
            ["caricato"] = (element as FrameworkElement)?.IsLoaded
        };
    }

    private static JsonArray RectJson(Rect r) => new((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));

    internal static string Hex(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    internal static string Slug(string text, int max)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (char ch in normalized)
        {
            if (char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            char c = char.ToLowerInvariant(ch);
            builder.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' ? c : '-');
        }
        var slug = Regex.Replace(builder.ToString(), "-+", "-").Trim('-');
        if (slug.Length > max) slug = slug[..max].Trim('-');
        return slug.Length == 0 ? "x" : slug;
    }

    // ---------------------------------------------------------------- report

    private void Write(DateTime started, string[] args)
    {
        foreach (var (prefix, origin, reason) in KnownWindows)
            if (!capturedTitles.Any(t => t.StartsWith(prefix, StringComparison.Ordinal)))
                Uncovered(prefix.TrimEnd(' ', '·') + " (" + origin + ")", reason);
        Uncovered("MessageBox e finestre di sistema (Apri, Salva, Stampa)", "finestre native di Windows, non disegnate da ANTHEA: il tema dell'applicazione non le riguarda");
        foreach (var (module, labels) in triggersFound)
            if (labels.Count == 0) notes.Add(new JsonObject { ["vista"] = module, ["modalita"] = "tutte", ["nota"] = "nessun comando di apertura finestre trovato" });
        var all = defects.OfType<JsonObject>().ToList();
        var groups = all.GroupBy(d => d.S("chiave")).Select(g =>
        {
            var first = g.First();
            return new JsonObject
            {
                ["chiave"] = g.Key, ["tipo"] = first.S("tipo"), ["occorrenze"] = g.Count(),
                ["modalita"] = new JsonArray(g.Select(d => d.S("modalita")).Distinct().Order().Select(m => (JsonNode?)m).ToArray()),
                ["viste"] = new JsonArray(g.Select(d => d.S("vista")).Distinct().Order().Select(v => (JsonNode?)v).ToArray()),
                ["rapporto_min"] = g.Select(d => d["rapporto_pixel"]?.GetValue<double>() ?? d["rapporto_proprieta"]?.GetValue<double>() ?? 0).DefaultIfEmpty(0).Min(),
                ["testi"] = new JsonArray(g.Select(d => d["elemento"]?["testo"]?.ToString()).OfType<string>().Select(Normalize).Distinct().Take(12).Select(t => (JsonNode?)t).ToArray()),
                ["elemento"] = first["elemento"]?.DeepClone(), ["primo_piano"] = first["primo_piano"]?.DeepClone(), ["sfondo"] = first["sfondo"]?.DeepClone(),
                ["testo_visto"] = first["testo_visto"]?.DeepClone(), ["sfondo_visto"] = first["sfondo_visto"]?.DeepClone() ?? first["colore_visto"]?.DeepClone(),
                ["sorgenti"] = first["sorgenti"]?.DeepClone(), ["creazione"] = g.Select(d => d["creazione"]).FirstOrDefault(c => c is JsonArray { Count: > 0 })?.DeepClone(),
                ["esempio"] = first.S("id"), ["cattura"] = first.S("cattura")
            };
        }).OrderBy(g => g["modalita"]!.AsArray().All(m => m!.GetValue<string>() == "Light") ? 1 : 0).ThenByDescending(g => g["occorrenze"]!.GetValue<int>()).ToList();
        int index = 0; foreach (var g in groups) g["id"] = $"G{++index:000}";
        var causes = all.GroupBy(d => $"{d.S("tipo")} · testo {d["primo_piano"]?["proprietario"]} {d["primo_piano"]?["colore_originale"]} {d["primo_piano"]?["origine_valore"]} · sfondo {d["sfondo"]?["proprietario"]} {d["sfondo"]?["colore_originale"]} {d["sfondo"]?["origine_valore"]}")
            .Select(g => new JsonObject
            {
                ["causa"] = g.Key, ["occorrenze"] = g.Count(), ["gruppi"] = g.Select(d => d.S("chiave")).Distinct().Count(),
                ["modalita"] = string.Join(", ", g.Select(d => d.S("modalita")).Distinct().Order()), ["esempio"] = g.First().S("id"), ["sorgenti"] = g.First()["sorgenti"]?.DeepClone()
            }).OrderByDescending(c => c["occorrenze"]!.GetValue<int>()).ToList();
        var exclusionList = new JsonArray(exclusions.OrderByDescending(e => e.Value.Pixels).Select(e => (JsonNode?)new JsonObject
            { ["elemento"] = e.Key, ["motivo"] = e.Value.Reason, ["occorrenze"] = e.Value.Count, ["pixel_chiari_esclusi"] = e.Value.Pixels }).ToArray());
        var invariance = CompareReference();
        var report = new JsonObject
        {
            ["strumento"] = "supporto/test/Desktop/ContrastAudit.cs", ["comando"] = "dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-contrast " + string.Join(" ", args),
            ["avvio"] = started.ToString("s"), ["durata_s"] = Math.Round(clock.Elapsed.TotalSeconds),
            ["ambiente"] = new JsonObject { ["dpi_finestra"] = dpi, ["area_cattura_dip"] = $"{CaptureWidth}x{CaptureHeight}", ["sistema"] = Environment.OSVersion.VersionString },
            ["regole"] = new JsonArray(
                "Testo: rapporto WCAG 2.x < 4,5 (< 3 per testo grande: ≥ 24 px, oppure ≥ 18,5 px con peso ≥ 600, il grassetto SemiBold dell'applicazione).",
                "Misura dai pixel: nel rettangolo del testo (limiti del disegno del TextBlock o delle righe del TextBox, ritagliati dalle aree di scorrimento) lo sfondo è il colore prevalente (istogramma a 5 bit per canale), il testo è il pixel più lontano dallo sfondo; la segnalazione usa questa misura, la misura dalle proprietà la sostituisce solo se mancano i pixel.",
                "Misura dalle proprietà: Foreground effettivo contro il primo Background non trasparente (Border, Panel, TextBlock) risalendo l'albero visuale, con composizione dei livelli semitrasparenti; 'avviso-proprieta' quando solo questa misura è sotto soglia.",
                "Controlli disabilitati: solo 'info-disabilitato' sotto 2,5. Testi tagliati dal bordo di un'area di scorrimento o della cattura: misurati solo se ne resta visibile almeno il 60% dell'altezza e della larghezza (fino a 12 px).",
                "Aree chiare (solo Scuro e Molto scuro): componenti connesse di pixel con luminanza relativa > 0,8, area > 2000 px e lati ≥ 4 px (le linee sottili sono escluse), fuori dalle esclusioni documentate; la sorgente è l'elemento più in alto con sfondo chiaro sotto il punto centrale.",
                "Le viste sono costruite dopo aver scelto la modalità, come all'avvio con la preferenza salvata; le viste 'cambio-*' verificano il cambio di modalità con la vista aperta. Nessuna ApplyTree prima della cattura.",
                "Sorgenti: colore originale del pennello (prima della trasformazione della palette, se tematizzato) cercato nei sorgenti di X.Desktop, con priorità ai file dei tipi ANTHEA antenati dell'elemento."),
            ["soglie"] = new JsonObject { ["normale"] = 4.5, ["grande"] = 3.0, ["disabilitato_info"] = 2.5, ["luminanza_area"] = 0.8, ["area_min_px"] = 2000 },
            ["conteggi"] = new JsonObject
            {
                ["catture"] = captures.Count, ["difetti"] = all.Count, ["gruppi"] = groups.Count,
                ["difetti_per_modalita"] = new JsonObject(Modes.Select(m => KeyValuePair.Create(m.ToString(), (JsonNode?)all.Count(d => d.S("modalita") == m.ToString())))),
                ["informazioni"] = infos.Count
            },
            ["gruppi"] = new JsonArray(groups.Select(g => (JsonNode?)g).ToArray()),
            ["cause"] = new JsonArray(causes.Select(c => (JsonNode?)c).ToArray()),
            ["difetti"] = defects.DeepClone(), ["informazioni"] = infos.DeepClone(), ["esclusioni"] = exclusionList, ["aree_chiare_escluse"] = excludedLight.DeepClone(),
            ["catture"] = captures.DeepClone(), ["finestre"] = windows.DeepClone(), ["violazioni_fuori_schermo"] = violations.DeepClone(),
            ["finestre_di_sistema"] = SystemDialogs.DeepClone(), ["non_coperti"] = uncovered.DeepClone(), ["note"] = notes.DeepClone(),
            ["comandi_trovati"] = new JsonObject(triggersFound.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)new JsonArray(p.Value.Order().Select(v => (JsonNode?)v).ToArray())))),
            ["invarianza_chiaro"] = invariance
        };
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(Path.Combine(OutputDirectory, "report.json"), report.ToJsonString(options), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(OutputDirectory, "riepilogo.md"), Summary(report, groups, causes), new UTF8Encoding(false));
    }

    /// <summary>Captures whose content changes at every run by design: compared and reported apart, never as a difference.</summary>
    private static readonly (string Part, string Reason)[] VariableCaptures =
    [
        ("collegamento-dei-sondaggi", "anteprima JSON degli strati collegati, con gli identificativi generati a ogni collegamento (ProjectSharedData.LinkSoils)")
    ];

    private JsonObject? CompareReference()
    {
        if (referenceDirectory is null) return null;
        var differences = new JsonArray(); var variable = new JsonArray(); int compared = 0, equal = 0;
        var current = Directory.EnumerateFiles(OutputDirectory, "*-Light.png").Select(Path.GetFileName).OfType<string>().ToHashSet();
        var reference = Directory.Exists(referenceDirectory) ? Directory.EnumerateFiles(referenceDirectory, "*-Light.png").Select(Path.GetFileName).OfType<string>().ToHashSet() : [];
        foreach (var name in current.Intersect(reference).Order())
        {
            compared++;
            var (a, aw, ah) = Decode(Path.Combine(OutputDirectory, name)); var (b, bw, bh) = Decode(Path.Combine(referenceDirectory, name));
            if (aw != bw || ah != bh) { differences.Add(new JsonObject { ["cattura"] = name, ["dimensioni"] = $"{aw}x{ah} invece di {bw}x{bh}" }); continue; }
            int count = 0, minX = aw, minY = ah, maxX = -1, maxY = -1;
            for (int i = 0; i < a.Length; i += 4)
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3])
                { count++; int p = i / 4, x = p % aw, y = p / aw; minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            if (count == 0) equal++;
            else if (VariableCaptures.FirstOrDefault(v => name.Contains(v.Part, StringComparison.Ordinal)) is { Part: not null } known)
                variable.Add(new JsonObject { ["cattura"] = name, ["pixel_diversi"] = count, ["motivo"] = known.Reason });
            else differences.Add(new JsonObject { ["cattura"] = name, ["pixel_diversi"] = count, ["rettangolo"] = new JsonArray(minX, minY, maxX - minX + 1, maxY - minY + 1) });
        }
        return new JsonObject
        {
            ["riferimento"] = referenceDirectory, ["confrontate"] = compared, ["identiche"] = equal, ["diverse"] = differences, ["variabili_per_costruzione"] = variable,
            ["mancanti"] = new JsonArray(reference.Except(current).Order().Select(n => (JsonNode?)n).ToArray()),
            ["nuove"] = new JsonArray(current.Except(reference).Order().Select(n => (JsonNode?)n).ToArray())
        };
    }

    private static (byte[] Pixels, int Width, int Height) Decode(string file)
    {
        using var stream = File.OpenRead(file);
        var frame = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        BitmapSource source = frame.Format == PixelFormats.Pbgra32 ? frame : new FormatConvertedBitmap(frame, PixelFormats.Pbgra32, null, 0);
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4]; source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        return (pixels, source.PixelWidth, source.PixelHeight);
    }

    private static string Summary(JsonObject report, List<JsonObject> groups, List<JsonObject> causes)
    {
        var md = new StringBuilder();
        string Cell(string? s) => (s ?? "").Replace("|", "\\|").Replace("\n", " ");
        var counts = report["conteggi"]!;
        md.AppendLine("# Contrasto delle modalità di aspetto · run");
        md.AppendLine();
        md.AppendLine($"Comando: `{report.S("comando")}`  ");
        md.AppendLine($"Avvio {report.S("avvio")}, durata {report["durata_s"]} s, DPI della finestra {report["ambiente"]!["dpi_finestra"]}, area {report["ambiente"]!["area_cattura_dip"]} DIP.");
        md.AppendLine();
        md.AppendLine($"Catture: {counts["catture"]}. Difetti: {counts["difetti"]} in {counts["gruppi"]} gruppi " +
            $"(Chiaro {counts["difetti_per_modalita"]!["Light"]}, Scuro {counts["difetti_per_modalita"]!["Dark"]}, Molto scuro {counts["difetti_per_modalita"]!["VeryDark"]}). Informazioni: {counts["informazioni"]}.");
        md.AppendLine();
        md.AppendLine("## Regole");
        foreach (var rule in report["regole"]!.AsArray()) md.AppendLine("- " + rule);
        md.AppendLine();
        void Table(string title, IEnumerable<JsonObject> rows)
        {
            md.AppendLine("## " + title); md.AppendLine();
            md.AppendLine("| Gruppo | Tipo | Modalità | N | Rapporto min | Elemento | Testo | Primo piano | Sfondo | Viste | Sorgente |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var g in rows)
            {
                var e = g["elemento"]; var views = g["viste"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
                string fg = g["primo_piano"] is JsonObject f ? $"{f.S("colore_originale")}→{f.S("colore")} {(f["tematizzato"]?.GetValue<bool>() == true ? "palette" : "fisso")} · {f.S("proprietario")} · {f.S("origine_valore")}" : "";
                string bg = g["sfondo"] is JsonObject b ? $"{b.S("colore_originale")}→{b.S("colore")} {(b["tematizzato"]?.GetValue<bool>() == true ? "palette" : "fisso")} · {b.S("proprietario")} · {b.S("origine_valore")}" : "";
                md.AppendLine($"| {g.S("id")} | {g.S("tipo")} | {string.Join(", ", g["modalita"]!.AsArray().Select(m => m!.GetValue<string>()))} | {g["occorrenze"]} | {g["rapporto_min"]} | " +
                    $"{Cell(e?["tipo"]?.ToString())} · {Cell(e?["tipi_anthea"]?.ToString())} | {Cell(string.Join(" / ", (g["testi"]?.AsArray() ?? []).Take(3).Select(x => x?.ToString())))} | {Cell(fg)} | {Cell(bg)} | " +
                    $"{Cell(string.Join(", ", views.Take(6)) + (views.Count > 6 ? $" (+{views.Count - 6})" : ""))} | {Cell(g["sorgenti"]?.AsArray().FirstOrDefault()?.ToString())} |");
            }
            md.AppendLine();
        }
        Table("Gruppi nelle modalità scure", groups.Where(g => g["modalita"]!.AsArray().Any(m => m!.GetValue<string>() != "Light")));
        Table("Gruppi solo in modalità chiara (preesistenti: la modalità chiara non si modifica)", groups.Where(g => g["modalita"]!.AsArray().All(m => m!.GetValue<string>() == "Light")));
        md.AppendLine("## Cause più frequenti"); md.AppendLine();
        md.AppendLine("| Occorrenze | Gruppi | Modalità | Causa (tipo · primo piano · sfondo) |"); md.AppendLine("|---|---|---|---|");
        foreach (var c in causes.Take(40)) md.AppendLine($"| {c["occorrenze"]} | {c["gruppi"]} | {c.S("modalita")} | {Cell(c.S("causa"))} |");
        md.AppendLine();
        md.AppendLine("## Esclusioni delle aree chiare"); md.AppendLine();
        md.AppendLine("| Elemento | Motivo | Occorrenze | Pixel chiari esclusi |"); md.AppendLine("|---|---|---|---|");
        foreach (var e in report["esclusioni"]!.AsArray().OfType<JsonObject>()) md.AppendLine($"| {Cell(e.S("elemento"))} | {Cell(e.S("motivo"))} | {e["occorrenze"]} | {e["pixel_chiari_esclusi"]} |");
        md.AppendLine();
        md.AppendLine("## Finestre non coperte"); md.AppendLine();
        foreach (var u in report["non_coperti"]!.AsArray().OfType<JsonObject>()) md.AppendLine($"- {u.S("finestra")}: {u.S("motivo")}");
        md.AppendLine();
        md.AppendLine("## Controllo fuori schermo"); md.AppendLine();
        var violations = report["violazioni_fuori_schermo"]!.AsArray();
        md.AppendLine(violations.Count == 0 ? $"Nessuna finestra sullo schermo né attivata ({report["finestre"]!.AsArray().Count} finestre controllate)." : $"{violations.Count} finestre fuori regola: vedere report.json.");
        var system = report["finestre_di_sistema"]!.AsArray();
        if (system.Count > 0) md.AppendLine($"Finestre di sistema chiuse automaticamente: {system.Count}.");
        if (report["invarianza_chiaro"] is JsonObject inv)
        {
            md.AppendLine(); md.AppendLine("## Invarianza della modalità chiara"); md.AppendLine();
            md.AppendLine($"Riferimento {inv.S("riferimento")}: {inv["confrontate"]} catture confrontate, {inv["identiche"]} identiche, {inv["diverse"]!.AsArray().Count} diverse, " +
                $"{inv["variabili_per_costruzione"]!.AsArray().Count} variabili per costruzione, {inv["mancanti"]!.AsArray().Count} mancanti, {inv["nuove"]!.AsArray().Count} nuove.");
            foreach (var d in inv["diverse"]!.AsArray().OfType<JsonObject>()) md.AppendLine($"- {d.S("cattura")}: {d["pixel_diversi"] ?? d["dimensioni"]}");
            foreach (var d in inv["variabili_per_costruzione"]!.AsArray().OfType<JsonObject>()) md.AppendLine($"- {d.S("cattura")}: {d["pixel_diversi"]} pixel, variabile per costruzione ({d.S("motivo")})");
        }
        var notes = report["note"]!.AsArray();
        if (notes.Count > 0) { md.AppendLine(); md.AppendLine("## Note"); md.AppendLine(); foreach (var n in notes.OfType<JsonObject>()) md.AppendLine($"- {n.S("vista")} ({n.S("modalita")}): {Cell(n.S("nota").Split('\n')[0])}"); }
        return md.ToString();
    }
}

/// <summary>Source lines of X.Desktop that assign a colour, ranked by the ANTHEA types around the element.</summary>
internal sealed class SourceIndex
{
    internal readonly string Root;
    private readonly List<(string File, int Line, string Text)> lines = [];
    private readonly Dictionary<string, HashSet<string>> typeFiles = new();
    private static readonly Dictionary<Color, string[]> NamedColors = typeof(Colors).GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => p.Name != "Transparent").GroupBy(p => (Color)p.GetValue(null)!).ToDictionary(g => g.Key, g => g.Select(p => p.Name).ToArray());

    internal SourceIndex(string root)
    {
        Root = root;
        // The two projects that build the interface: X.Desktop and the material view of X.Materiali.
        foreach (var file in new[] { "X.Desktop", "X.Materiali" }.Where(p => Directory.Exists(Path.Combine(root, p)))
            .SelectMany(p => Directory.EnumerateFiles(Path.Combine(root, p), "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) &&
                !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var text = File.ReadAllLines(file);
            for (int i = 0; i < text.Length; i++) lines.Add((relative, i + 1, text[i]));
            foreach (Match m in Regex.Matches(string.Join("\n", text), @"\b(?:class|record|struct)\s+(\w+)"))
            {
                if (!typeFiles.TryGetValue(m.Groups[1].Value, out var set)) typeFiles[m.Groups[1].Value] = set = [];
                set.Add(relative);
            }
        }
    }

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "X.Desktop", "X.Desktop.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException("Radice del repository non trovata da " + AppContext.BaseDirectory);
    }

    private readonly Dictionary<string, List<((string File, int Line, string Text) Line, int Hits)>> matches = new();
    private readonly Dictionary<string, List<string>> results = new();

    /// <summary>Lines that create an element with this text (string literal), nearest component first.</summary>
    internal IEnumerable<string> FindText(string? text, IReadOnlyList<string> relatedTypes, int max = 2)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var literal = text.Trim().Split('\n')[0].Trim();
        if (literal.Length < 3) return [];
        if (literal.Length > 40) literal = literal[..40];
        string quoted = "\"" + literal;
        var nearest = relatedTypes.Count > 0 ? typeFiles.GetValueOrDefault(relatedTypes[^1]) ?? [] : [];
        return lines.Where(l => l.Text.Contains(quoted, StringComparison.Ordinal))
            .OrderByDescending(l => nearest.Contains(l.File)).ThenBy(l => l.File).ThenBy(l => l.Line)
            .Take(max).Select(l => $"{l.File}:{l.Line}: {Trim(l.Text)}").ToList();
    }

    internal IEnumerable<string> Find(string hex, string role, string property, IEnumerable<string> relatedTypes, bool themed, int max = 4, string? elementType = null)
    {
        if (hex.Length < 7) return [];
        string rgb = hex.Length == 9 ? hex[3..] : hex[1..];
        var types = relatedTypes.ToList();
        string key = string.Join("|", rgb, role, property, themed, elementType, string.Join(">", types));
        if (results.TryGetValue(key, out var cached)) return cached;
        string matchKey = rgb + "|" + themed;
        if (!matches.TryGetValue(matchKey, out var candidates))
        {
            var color = (Color)ColorConverter.ConvertFromString("#" + rgb);
            var patterns = new List<string> { "#" + rgb, "#FF" + rgb };
            if (NamedColors.TryGetValue(color, out var named)) foreach (var n in named) { patterns.Add("Brushes." + n); patterns.Add("Colors." + n); patterns.Add("\"" + n + "\""); }
            foreach (var field in typeof(Ui).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (field.GetValue(null) is SolidColorBrush b && b.Color == color) patterns.Add("Ui." + field.Name);
            // A brush outside the palette cannot come from Appearance.* or from the palette resources.
            if (themed)
            {
                patterns.Add("." + rgb + "\"");
                foreach (var name in new[] { "Paper", "Surface", "Ink", "Accent" }) if (PaletteColor(name) == color) patterns.Add("Appearance." + name);
            }
            candidates = lines.Where(l => themed || !l.Text.Contains("Appearance.", StringComparison.Ordinal) && !l.Text.Contains("DynamicResource", StringComparison.Ordinal))
                .Select(l => (l, patterns.Count(p => l.Text.Contains(p, StringComparison.OrdinalIgnoreCase)))).Where(x => x.Item2 > 0).ToList();
            matches[matchKey] = candidates;
        }
        // The nearest ANTHEA component weighs most; MainWindow, ancestor of everything, little.
        var nearest = types.Count > 0 ? typeFiles.GetValueOrDefault(types[^1]) ?? [] : [];
        var related = types.Where(t => t != "MainWindow").SelectMany(t => typeFiles.GetValueOrDefault(t) ?? []).ToHashSet();
        string hint = property switch { "Foreground" => "Foreground", "Background" => "Background", "BorderBrush" => "Border", "Fill" => "Fill", _ => property };
        string[] creation = elementType switch
        {
            "Button" => ["new Button", "Ui.Button("], "TextBlock" => ["new TextBlock", "Ui.Text("], "Border" => ["new Border", "Ui.Paper("],
            null => [], _ => ["new " + elementType]
        };
        var found = candidates.Select(c => (c.Line, Score: Score(c.Line, c.Hits))).OrderByDescending(x => x.Score).ThenBy(x => x.Line.File).ThenBy(x => x.Line.Line)
            .Take(max).Select(x => $"{x.Line.File}:{x.Line.Line}: {Trim(x.Line.Text)}").ToList();
        results[key] = found;
        return found;
        int Score((string File, int Line, string Text) line, int hits)
        {
            int score = 1 + hits;
            if (nearest.Contains(line.File)) score += 10; else if (related.Contains(line.File)) score += 5;
            if (line.Text.Contains(hint, StringComparison.Ordinal) || role == "foreground" && line.Text.Contains("color:", StringComparison.Ordinal)) score += 3;
            if (creation.Any(c => line.Text.Contains(c, StringComparison.Ordinal))) score += 6;
            if (line.File.EndsWith("AppearanceResources.cs", StringComparison.Ordinal)) score -= 2;
            return score;
        }
    }

    private static Color PaletteColor(string name) => name switch
    {
        "Paper" => Colors.White, "Surface" => (Color)ColorConverter.ConvertFromString("#F3F5F8"), "Ink" => (Color)ColorConverter.ConvertFromString("#0B2A4A"),
        _ => (Color)ColorConverter.ConvertFromString("#0B5CAD")
    };

    private static string Trim(string text) { var t = text.Trim(); return t.Length > 220 ? t[..220] + "…" : t; }
}

/// <summary>
/// Keeps every window of this thread invisible. A WH_CBT hook sees each new top-level WPF or system window before
/// it exists on screen: it starts at (-20000, -20000), leaves the taskbar and Alt+Tab, and its activation is refused.
/// A WH_CALLWNDPROC hook sees the messages sent to it before WPF does: it cloaks the window in DWM before it is
/// shown (never painted on a monitor, wherever it is) and adds an HwndSource hook to WPF windows that keeps them at
/// (-20000, -20000) against later moves (DisplayAdaptation fits windows to the monitor) and fixes the size of the
/// main window. System dialog boxes (message boxes, file dialogs) are cancelled at once and reported: the audit
/// never opens them on purpose.
/// </summary>
internal sealed class OffscreenWindows : IDisposable
{
    internal const int Offscreen = -20000;
    private const int WH_CALLWNDPROC = 4, WH_CBT = 5, HCBT_CREATEWND = 3, HCBT_ACTIVATE = 5, GWL_EXSTYLE = -20;
    private const int WM_SHOWWINDOW = 0x0018, WM_WINDOWPOSCHANGING = 0x0046, WM_GETMINMAXINFO = 0x0024, WM_NCDESTROY = 0x0082, WM_STYLECHANGING = 0x007C;
    private const uint WM_COMMAND = 0x0111;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
    private const int WS_CHILD = 0x40000000, WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000;
    private const int DWMWA_CLOAK = 13, DWMWA_CLOAKED = 14;
    private static readonly IntPtr HWND_MESSAGE = new(-3);
    private static OffscreenWindows? active;
    internal static readonly List<string> Log = [];

    [StructLayout(LayoutKind.Sequential)] private struct CREATESTRUCT { public IntPtr lpCreateParams, hInstance, hMenu, hwndParent; public int cy, cx, y, x, style; public IntPtr lpszName, lpszClass; public int dwExStyle; }
    [StructLayout(LayoutKind.Sequential)] private struct CWPSTRUCT { public IntPtr lParam, wParam; public int message; public IntPtr hwnd; }
    [StructLayout(LayoutKind.Sequential)] private struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct STYLESTRUCT { public int styleOld, styleNew; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    private readonly ContrastAudit audit;
    private readonly HookProc cbt, sent;
    private readonly HwndSourceHook wpf;
    private readonly IntPtr cbtHook, sentHook;
    private readonly HashSet<IntPtr> managed = [], system = [], attached = [], cloaked = [];
    private readonly Dictionary<IntPtr, (int Cx, int Cy)> forced = new();

    private OffscreenWindows(ContrastAudit audit)
    {
        this.audit = audit; cbt = Cbt; sent = Sent; wpf = WpfHook;
        uint thread = GetCurrentThreadId();
        cbtHook = SetWindowsHookEx(WH_CBT, cbt, IntPtr.Zero, thread);
        sentHook = SetWindowsHookEx(WH_CALLWNDPROC, sent, IntPtr.Zero, thread);
        if (cbtHook == IntPtr.Zero || sentHook == IntPtr.Zero) throw new InvalidOperationException("Agganci delle finestre non installati: " + Marshal.GetLastWin32Error());
    }

    internal static IDisposable Install(ContrastAudit audit) => active = new OffscreenWindows(audit);

    public void Dispose()
    {
        UnhookWindowsHookEx(sentHook); UnhookWindowsHookEx(cbtHook);
        foreach (var handle in attached.ToList()) HwndSource.FromHwnd(handle)?.RemoveHook(wpf);
        active = null;
    }

    private static string ClassName(IntPtr hwnd) { var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity); return name.ToString(); }

    private static int HiddenStyle(int style) => (style & ~WS_EX_APPWINDOW) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

    private IntPtr Cbt(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code == HCBT_CREATEWND)
            {
                var pointer = Marshal.ReadIntPtr(lParam);
                var cs = Marshal.PtrToStructure<CREATESTRUCT>(pointer);
                if ((cs.style & WS_CHILD) == 0 && cs.hwndParent != HWND_MESSAGE)
                {
                    string name = ClassName(wParam);
                    bool dialog = name == "#32770";
                    if (dialog || name.StartsWith("HwndWrapper[", StringComparison.Ordinal))
                    {
                        cs.x = Offscreen; cs.y = Offscreen; Marshal.StructureToPtr(cs, pointer, false);
                        managed.Add(wParam); if (dialog) system.Add(wParam);
                        SetWindowLongPtr(wParam, GWL_EXSTYLE, (IntPtr)HiddenStyle((int)GetWindowLongPtr(wParam, GWL_EXSTYLE)));
                    }
                }
            }
            else if (code == HCBT_ACTIVATE && managed.Contains(wParam))
            {
                if (system.Contains(wParam))
                {
                    audit.SystemDialogs.Add(new JsonObject { ["vista"] = audit.Context, ["modalita"] = audit.Mode.ToString(), ["classe"] = ClassName(wParam) });
                    PostMessage(wParam, WM_COMMAND, (IntPtr)2, IntPtr.Zero); PostMessage(wParam, WM_COMMAND, (IntPtr)7, IntPtr.Zero);
                }
                return (IntPtr)1;
            }
        }
        catch (Exception ex) { if (Log.Count < 400) Log.Add("CBT: " + ex.GetType().Name + " " + ex.Message); }
        return CallNextHookEx(cbtHook, code, wParam, lParam);
    }

    /// <summary>Messages sent to the windows of this thread, before their window procedure.</summary>
    private IntPtr Sent(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0)
            {
                var m = Marshal.PtrToStructure<CWPSTRUCT>(lParam);
                if (managed.Contains(m.hwnd))
                {
                    if (!system.Contains(m.hwnd) && !attached.Contains(m.hwnd) && HwndSource.FromHwnd(m.hwnd) is { } source)
                    { source.AddHook(wpf); attached.Add(m.hwnd); }
                    bool showing = m.message == WM_SHOWWINDOW && m.wParam != IntPtr.Zero ||
                        m.message == WM_WINDOWPOSCHANGING && (Marshal.PtrToStructure<WINDOWPOS>(m.lParam).flags & SWP_SHOWWINDOW) != 0;
                    if (showing && !cloaked.Contains(m.hwnd))
                    {
                        int value = 1, result = DwmSetWindowAttribute(m.hwnd, DWMWA_CLOAK, ref value, 4);
                        if (result == 0) cloaked.Add(m.hwnd);
                        else if (Log.Count < 400) Log.Add($"occultamento non riuscito 0x{result:X8} per {ClassName(m.hwnd)}");
                    }
                    if (m.message == WM_NCDESTROY) { managed.Remove(m.hwnd); system.Remove(m.hwnd); attached.Remove(m.hwnd); cloaked.Remove(m.hwnd); forced.Remove(m.hwnd); }
                }
            }
        }
        catch (Exception ex) { if (Log.Count < 400) Log.Add("Messaggi: " + ex.GetType().Name + " " + ex.Message); }
        return CallNextHookEx(sentHook, code, wParam, lParam);
    }

    /// <summary>Inside WPF's window procedure, where the structures of the messages can be changed.</summary>
    private IntPtr WpfHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case WM_WINDOWPOSCHANGING:
            {
                var position = Marshal.PtrToStructure<WINDOWPOS>(lParam); bool changed = false;
                if ((position.flags & SWP_NOMOVE) == 0 && (position.x != Offscreen || position.y != Offscreen)) { position.x = Offscreen; position.y = Offscreen; changed = true; }
                if (forced.TryGetValue(hwnd, out var size) && (position.flags & SWP_NOSIZE) == 0) { position.cx = size.Cx; position.cy = size.Cy; changed = true; }
                if (changed) Marshal.StructureToPtr(position, lParam, false);
                break;
            }
            case WM_STYLECHANGING when (int)wParam == GWL_EXSTYLE:
            {
                var style = Marshal.PtrToStructure<STYLESTRUCT>(lParam); style.styleNew = HiddenStyle(style.styleNew); Marshal.StructureToPtr(style, lParam, false);
                break;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>Fixed client size (no frame) for the main window, independent of the monitor work area; returns the DPI.</summary>
    internal static double ForceSize(IntPtr hwnd, double width, double height)
    {
        uint dpi = GetDpiForWindow(hwnd); if (dpi == 0) dpi = 96;
        int cx = (int)Math.Round(width * dpi / 96), cy = (int)Math.Round(height * dpi / 96);
        if (active is not null)
        {
            active.forced[hwnd] = (cx, cy);
            if (active.attached.Add(hwnd)) HwndSource.FromHwnd(hwnd)?.AddHook(active.wpf);
        }
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, cx, cy, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
        return dpi;
    }

    internal static string Report(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var rect); DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, 4);
        return $"rett=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom}) visibile={IsWindowVisible(hwnd)} occultata={cloaked} stileEx=0x{(long)GetWindowLongPtr(hwnd, GWL_EXSTYLE):X} " +
            $"gestita={active?.managed.Contains(hwnd)} aggancio_wpf={active?.attached.Contains(hwnd)}";
    }

    internal static void MoveForTest(IntPtr hwnd, int x, int y) => SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

    internal static bool IsCloaked(IntPtr hwnd) => DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0;

    internal static (RECT Rect, bool Visible) Where(IntPtr hwnd) { GetWindowRect(hwnd, out var rect); return (rect, IsWindowVisible(hwnd)); }

    internal static bool ForegroundIsOurs()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        GetWindowThreadProcessId(foreground, out uint process);
        return process == (uint)Environment.ProcessId;
    }
}


public sealed partial class MainWindow
{
    internal void PrepareContrastAudit() => wiki = new WikiView((_, _) => { }, new WikiProgress(persist: false), returnToWork: () => { });

    private FrameworkElement AuditRoot => ContrastAudit.WindowRoot(this);

    /// <summary>Before replacing the document: no open sheet to commit, nothing to save (ConfirmDiscard would ask with a MessageBox).</summary>
    private void AuditReset() { editor?.Dispose(); editor = null; currentSheet = null; sheetContent.Content = null; dirty = false; }

    internal async Task AuditWaitEditor(ContrastAudit a, bool calculate)
    {
        if (editor is null) return;
        var active = editor;
        if (calculate) { try { await active.CalculateAsync(); } catch (Exception ex) { a.Note(a.Context, "Calcolo: " + ex.Message); } }
        try { await active.WaitForAutomatic(); } catch (Exception ex) { a.Note(a.Context, ex.Message); }
        if (!await a.WaitUntil(() => editor is null || !editor.Busy, 120)) a.Note(a.Context, "calcolo ancora in corso dopo 120 s");
        await a.Settle(60);
    }

    /// <summary>Every view of the main window, in the order a user meets them; windows and popups are captured by the hooks of ContrastAudit.</summary>
    internal async Task AuditContrastViews(ContrastAudit a)
    {
        await a.Step("home", async () =>
        {
            ShowHome(); await a.Settle();
            await a.SnapStable("home", AuditRoot, "vista");
            if (ContrastAudit.FirstWithToolTip(dashboard) is { } tipped) await a.SnapToolTip(tipped, "home--suggerimento");
            if (Ui.Descendants<Menu>(this).FirstOrDefault() is { } menu)
                foreach (var item in menu.Items.OfType<MenuItem>()) await a.SnapSubmenu(item, "menu-" + ContrastAudit.Slug(item.Header?.ToString()?.Replace("_", "") ?? "voce", 20));
        });
        await a.Step("moduli", async () =>
        {
            ShowModules(); await a.Settle(); await a.SnapStable("moduli", AuditRoot, "vista");
            await a.SnapPages(dashboardBody, "moduli", AuditRoot);
            ShowModules("Strutture"); await a.Settle(); await a.SnapStable("moduli-strutture", AuditRoot, "vista");
        });
        await a.Step("wiki", () => AuditWiki(a));
        await a.Step("progetti", () => AuditProjects(a));
        foreach (var module in ModuleCatalog.All) await a.Step("modulo-" + module.Id, () => AuditModule(a, module.Id));
    }

    private async Task AuditWiki(ContrastAudit a)
    {
        ShowWiki(); await a.Settle(150); await a.SnapStable("wiki-indice", AuditRoot, "vista");
        ShowWiki("/wiki/manuale"); await a.Settle(150); await a.SnapStable("wiki-manuale", AuditRoot, "vista");
        if (WikiCatalog.ForModule("geo_palo_verticale") is { } guide) { ShowWiki(guide.Id); await a.Settle(200); await a.SnapStable("wiki-guida-palo", AuditRoot, "vista"); await a.SnapPages(wiki!, "wiki-guida-palo", AuditRoot); }
        var theory = WikiCatalog.Resolve("beam") ?? WikiCatalog.Articles.First(x => x.Type == "theory" && x.Status != "historical");
        ShowWiki(theory.Id); await a.Settle(200); await a.SnapStable("wiki-teoria", AuditRoot, "vista"); await a.SnapPages(wiki!, "wiki-teoria", AuditRoot);
        a.Context = "wiki-teoria";
        if (Ui.Descendants<Button>(wiki!).FirstOrDefault(b => b.IsVisible && ContrastAudit.ButtonLabel(b) == "⋯") is { } dots)
            await a.Trigger("azioni della sezione", () => ContrastAudit.Click(dots));
        if (ContrastAudit.FindButton(wiki!, "Apri collegamento") is { } ask) await a.Trigger("Apri collegamento", () => ContrastAudit.Click(ask));
        if (Ui.Descendants<ComboBox>(wiki!).FirstOrDefault(c => c.IsVisible) is { } selector) await a.SnapComboBox(selector, "wiki--elenco-aspetto");
        if (Ui.Descendants<TextBox>(wiki!).FirstOrDefault(t => AutomationProperties.GetName(t) == "Ricerca nella Wiki") is { } search)
        {
            search.Text = "palo"; await Task.Delay(500); await a.Settle(150); await a.SnapStable("wiki-ricerca", AuditRoot, "vista");
            search.Text = ""; await Task.Delay(500); await a.Settle();
        }
    }

    private TreeViewItem? AuditTreeItem(JsonObject node)
    {
        IEnumerable<TreeViewItem> All(ItemsControl parent) => parent.Items.OfType<TreeViewItem>().SelectMany(i => new[] { i }.Concat(All(i)));
        return All(tree).FirstOrDefault(i => ReferenceEquals(i.Tag, node));
    }

    private static void AuditFixRevisionDates(JsonNode? node)
    {
        // Revision dates come from DateTime.Now: fixed, so that the reference captures stay reproducible.
        if (node is JsonObject o)
        {
            if (o.ContainsKey("numero") && o.ContainsKey("data")) o["data"] = "2026-01-15T10:30:00.0000000+01:00";
            foreach (var (_, child) in o.ToList()) AuditFixRevisionDates(child);
        }
        else if (node is JsonArray array) foreach (var child in array) AuditFixRevisionDates(child);
    }

    private async Task AuditProjects(ContrastAudit a)
    {
        AuditReset(); NewProjects(); await a.Settle(); await a.SnapStable("progetti-vuoto", AuditRoot, "vista");
        if (!File.Exists(a.TestArchive)) { a.Uncovered("Progetti con archivio di prova", "archivio di prova assente: " + a.TestArchive); return; }
        AuditReset(); LoadFile(a.TestArchive); await a.Settle();
        var project = document.Array("progetti").OfType<JsonObject>().First();
        var sections = project.Array("strutture").OfType<JsonObject>().ToArray();
        var first = sections[0]; var second = sections.Length > 1 ? sections[1] : sections[0];
        string firstId = first.S("id");
        ShowProjectOverview(project); await a.Settle(); await a.SnapStable("progetti-progetto", AuditRoot, "vista");
        ShowProjectOverview(first); await a.Settle(); await a.SnapStable("progetti-sezione", AuditRoot, "vista");
        foreach (var expander in Ui.Descendants<Expander>(projectContent).Where(x => !x.IsExpanded)) expander.IsExpanded = true;
        await a.Settle(); await a.SnapStable("progetti-sezione-espansa", AuditRoot, "vista");
        if (projectCatalogCards.Count > 0)
        {
            var card = projectCatalogCards[0].Card;
            card.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            await a.Settle(); await a.SnapStable("progetti-catalogo-hover", AuditRoot, "vista");
            card.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent }); await a.Settle();
        }
        a.Context = "progetti-sezione";
        if (AuditTreeItem(first) is { } item)
        {
            if (item.ContextMenu is { } menu) await a.Trigger("menu dell'albero", () => { menu.PlacementTarget = item; menu.IsOpen = true; });
            if (Ui.Descendants<TextBlock>(item).FirstOrDefault(t => t.IsVisible && t.ToolTip is string) is { } label) await a.SnapToolTip(label, "progetti--suggerimento-albero");
            BeginProjectRename(item); await a.Settle(); await a.SnapStable("progetti-rinomina", AuditRoot, "vista");
            finishProjectRename?.Invoke(false); await a.Settle();
        }
        ShowProjectOverview(first); await a.Settle();
        await a.Trigger("Confronto e avvisi", () => ShowCoherence(first));
        // A common value that differs between two sheets of the section: conflicts in the overview, in the comparison and before the report.
        a.Context = "progetti-conflitto";
        if (first.Array("fogli").OfType<JsonObject>().Where(s => s.S("modulo_id") == "mat_calcestruzzo").Skip(1).FirstOrDefault()?["dati"] is JsonObject concrete)
        {
            concrete["classe"] = "C35/45";
            ShowProjectOverview(first); await a.Settle(); await a.SnapStable("progetti-conflitto", AuditRoot, "vista");
            await a.Trigger("Confronto e avvisi", () => ShowCoherence(first));
        }
        var reportSection = new[] { first, second, project }.FirstOrDefault(s => ProjectSharedData.SubtreeSheets(s).Any() &&
            (ProjectSharedData.Differences(s).Count > 0 || SectionReportWarnings(s).Length > 0));
        if (reportSection is not null) await a.Trigger("Genera report", () => ExportSectionReport(reportSection));
        a.Context = "progetti-sezione";
        var confirmations = new WpfConfirmationService(this);
        await a.Trigger("Dati condivisi", () => confirmations.ConfirmSharedUpdate(new SharedUpdatePrompt(["Materiali"], first.S("nome"),
            [new SharedUpdateTarget("CLS seconda verifica", ["CLS · fck [MPa]: 30 → 35", "Classe di esposizione: XC1 → XC3"])])));
        string[] changes = [];
        if (first.Array("fogli").OfType<JsonObject>().FirstOrDefault(s => s.S("modulo_id") == "mat_calcestruzzo") is { } moving && !ReferenceEquals(first, second))
        {
            Scripted.ProjectMove = c => { changes = c; return false; };
            try { PreviewProjectMove(moving, second); } finally { Scripted.ProjectMove = null; }
        }
        if (changes.Length == 0) changes = ["CLS di riferimento · Classe calcestruzzo: C30/37 → C25/30"];
        await a.Trigger("Conferma spostamento", () => confirmations.ConfirmProjectMove(second.S("nome"), changes));

        a.Context = "progetti-foglio";
        var sheet = first.Array("fogli").OfType<JsonObject>().FirstOrDefault(s => s.S("modulo_id") == "str_palo") ?? first.Array("fogli").OfType<JsonObject>().First();
        ShowSheet(sheet); await AuditWaitEditor(a, true); await a.SnapStable("progetti-foglio", AuditRoot, "vista");
        SetProjectSheetTreeVisible(true); await a.Settle(); await a.SnapStable("progetti-foglio-struttura", AuditRoot, "vista");
        if (first.Array("fogli").OfType<JsonObject>().FirstOrDefault(s => s.S("modulo_id") == "mat_calcestruzzo") is { } material)
        { ShowSheet(material); await AuditWaitEditor(a, false); await a.SnapStable("progetti-foglio-materiale", AuditRoot, "vista"); }
        SetProjectSheetTreeVisible(false); await a.Settle();

        a.Context = "progetti-revisioni";
        ShowProjectOverview(first); await a.Settle();
        CompleteProjectRevision(first, "Revisione di prova"); AuditFixRevisionDates(document);
        first = ProjectRevisions.Find(document, firstId)!;
        ShowProjectOverview(first); await a.Settle(); await a.SnapStable("progetti-revisioni", AuditRoot, "vista");
        var scope = first;
        await a.Trigger("Riepilogo modifiche", () => ShowProjectRevisions(scope));
        await a.Trigger("Nuova revisione", () => CreateProjectRevision(scope));
        if (first.Array("revisioni").OfType<JsonObject>().FirstOrDefault() is { } archived)
        {
            SwitchProjectRevision(firstId, (int)archived.D("numero")); await a.Settle(); await a.SnapStable("progetti-revisione-archiviata", AuditRoot, "vista");
            if (ProjectRevisions.Find(document, firstId) is { } snapshot && snapshot.Array("fogli").OfType<JsonObject>().FirstOrDefault() is { } archivedSheet)
            { ShowSheet(archivedSheet); await AuditWaitEditor(a, false); await a.SnapStable("progetti-revisione-archiviata-foglio", AuditRoot, "vista"); }
            SwitchProjectRevision(firstId, null); await a.Settle();
        }

        a.Context = "progetti-terreni";
        project = document.Array("progetti").OfType<JsonObject>().First();
        var soils = ProjectDocuments.AddSection(project, "Terreni");
        AddSheetTo("geo_palo_verticale", soils, false); AddSheetTo("geo_palo_verticale", soils, false); await a.Settle();
        a.DialogActions["Confronto · Terreni"] = async window =>
        {
            if (Ui.Descendants<Expander>(window).FirstOrDefault(x => x.Header as string == "Collegamento stratigrafie") is not { } expander) return;
            expander.IsExpanded = true; await a.Settle(80);
            if (ContrastAudit.FindButton(window, "Mostra collegamento stratigrafie…") is { } show) await a.Trigger("Collegamento dei sondaggi", () => ContrastAudit.Click(show));
        };
        try { ShowProjectOverview(soils); await a.Settle(); await a.SnapStable("progetti-sezione-terreni", AuditRoot, "vista"); await a.Trigger("Confronto terreni", () => ShowCoherence(soils)); }
        finally { a.DialogActions.Remove("Confronto · Terreni"); }
        dirty = false;
    }

    /// <summary>Complete input for the modules whose defaults stop at "dati da completare", so that the results appear:
    /// cases and examples already in the repository (supporto/test/casi_confronto.json, supporto/esempi), or the smoke recipe.</summary>
    private static JsonObject? AuditCompleteData(ContrastAudit a, string module)
    {
        JsonObject? Case(string name) => File.Exists(Path.Combine(a.Root, "supporto", "test", "casi_confronto.json"))
            ? SmokeTestData.LoadCases().OfType<JsonObject>().FirstOrDefault(c => c.S("nome") == name)?["input"]?.DeepClone().AsObject() : null;
        JsonObject? Example(params string[] parts) { var file = Path.Combine([a.Root, "supporto", "esempi", .. parts]); return File.Exists(file) ? Archivio.Leggi(file)["dati"]?.DeepClone().AsObject() : null; }
        JsonObject Layered(JsonObject data) { data.Array("stratigrafie")[0]!.AsArray().Add(PaloOrizzontale.Layer()); return data; }
        return module switch
        {
            "geo_palo_verticale" => Case("palo_storico_0"),
            "geo_micropalo_verticale" => Case("micropalo_IRS_45_Feld"),
            PaloOrizzontale.Module => Layered(PaloOrizzontale.Defaults()),
            MicropaloOrizzontale.Module => Layered(MicropaloOrizzontale.Defaults()),
            RetainingWall.Module => Example("muri-sostegno", "mensola.anthea"),
            BridgeConcept.Module => Example("bridge-design.anthea"),
            _ => null
        };
    }

    /// <summary>The module as opened from the catalogue; when its defaults are incomplete, the main view only, then every tab,
    /// popup and window on complete data ("-dati").</summary>
    private async Task AuditModule(ContrastAudit a, string module)
    {
        JsonObject? complete = null;
        try { complete = AuditCompleteData(a, module); } catch (Exception ex) { a.Note("modulo-" + module, "dati completi non disponibili: " + ex.Message); }
        if (complete is not null)
        {
            ModuleCatalog.ValidateData(module, complete);
            await AuditModuleState(a, module, null, "modulo-" + module, full: false);
        }
        await AuditModuleState(a, module, complete, "modulo-" + module + (complete is null ? "" : "-dati"), full: true);
    }

    private async Task AuditModuleState(ContrastAudit a, string module, JsonObject? data, string name, bool full)
    {
        AuditReset();
        if (data is null) NewCalculation(module);
        else { ExitRevisionPreview(); document = Archivio.Documento(module); document["dati"] = data.DeepClone(); path = null; ShowSheet(document); RefreshTree(); UpdateTitle(); }
        await AuditWaitEditor(a, true);
        a.Context = name;
        if (!full) { await a.SnapStable(name, AuditRoot, "vista"); await a.SnapPages(sheetContent, name, AuditRoot); return; }
        a.TabDefaults.Clear();
        var routes = new Dictionary<string, List<(TabControl Tabs, int Index)>>();
        var routesEnabled = new HashSet<string>();
        bool horizontal = module is PaloOrizzontale.Module or MicropaloOrizzontale.Module;
        await a.ExploreTabs(sheetContent, name, 0, 3, [], new ContrastAudit.LeafBudget(70), async (leaf, route) =>
        {
            await AuditWaitEditor(a, false);
            await a.SnapStable(leaf, AuditRoot, "vista");
            await a.SnapPages(sheetContent, leaf, AuditRoot);
            // Route of the first tab where the command is enabled, otherwise where it is first seen.
            foreach (var (label, _) in ContrastAudit.DialogTriggers)
            {
                if (routesEnabled.Contains(label) || ContrastAudit.FindButton(sheetContent, label, enabledOnly: false) is not { } seen) continue;
                if (seen.IsEnabled) { routes[label] = route.ToList(); routesEnabled.Add(label); }
                else routes.TryAdd(label, route.ToList());
            }
        }, depth => AuditWaitEditor(a, horizontal && depth == 0));
        await a.Reselect([]);
        await AuditWaitEditor(a, false);
        if (Ui.Descendants<ComboBox>(sheetContent).FirstOrDefault(c => c.IsVisible && c.IsEnabled) is { } combo) await a.SnapComboBox(combo, name + "--elenco");
        if (ContrastAudit.FirstWithToolTip(sheetContent) is { } tipped) await a.SnapToolTip(tipped, name + "--suggerimento");
        if (ContrastAudit.FirstWithContextMenu(sheetContent) is { } owner && owner.ContextMenu is { } context)
            await a.Trigger("menu contestuale", () => { context.PlacementTarget = owner; context.IsOpen = true; });
        if (wikiTopics.IsVisible) await a.Trigger("Approfondimenti", ShowModuleWikiTopics);
        if (module is "geo_palo_verticale" or "geo_micropalo_verticale" or "str_palo" or BridgeSection.Module && editor is { HasResults: true, Busy: false })
            await a.Trigger("Report Word", ExportReport);
        foreach (var (label, seconds) in ContrastAudit.DialogTriggers)
        {
            if (!routes.TryGetValue(label, out var route)) continue;
            if (!await a.Reselect(route)) { a.Note(name, "scheda non più disponibile per " + label); continue; }
            await AuditWaitEditor(a, false);
            if (ContrastAudit.FindButton(sheetContent, label, enabledOnly: false) is not { } button) { a.Note(name, "comando non più visibile: " + label); continue; }
            // A command that needs a selected row (Apri sezione in c.a. of the wall): the rows are selected one at a time, as a user would.
            if (!button.IsEnabled) await AuditEnableBySelection(a, button);
            if (!button.IsEnabled) { a.Note(name, "comando non abilitato: " + label); continue; }
            if (!await a.Trigger(label, () => ContrastAudit.Click(button), seconds)) a.Note(name, "nessuna finestra da " + label);
            dirty = false;
        }
        await a.Reselect([]);
        a.RecordTriggers(name, routes.Keys);
    }

    private async Task AuditEnableBySelection(ContrastAudit a, Button button)
    {
        foreach (var grid in Ui.Descendants<DataGrid>(sheetContent).Where(g => g.IsVisible && g.Items.Count > 0).ToList())
            for (int i = 0; i < Math.Min(40, grid.Items.Count) && !button.IsEnabled; i++) { grid.SelectedIndex = i; await a.Settle(20); }
    }

    /// <summary>Appearance changed with the view open (Appearance.Set re-themes the open windows): home, a project section, the c.a. section.</summary>
    internal async Task AuditAppearanceSwitch(ContrastAudit a, AppAppearance start, AppAppearance mode)
    {
        async Task Switch(string name, Func<Task> show)
        {
            await a.Step(name, async () =>
            {
                Appearance.Set(start, false); await show(); await a.Settle(80);
                Appearance.Set(mode, false); await a.Settle(80);
                await a.SnapStable(name, AuditRoot, "vista");
            });
        }
        await Switch("cambio-home", () => { ShowHome(); return Task.CompletedTask; });
        await Switch("cambio-progetti-sezione", async () =>
        {
            AuditReset();
            if (File.Exists(a.TestArchive)) { LoadFile(a.TestArchive); ShowProjectOverview(document.Array("progetti")[0]!["strutture"]![0]!.AsObject()); }
            else ShowProjects();
            await a.Settle();
        });
        await Switch("cambio-modulo-str_palo", async () => { AuditReset(); NewCalculation("str_palo"); await AuditWaitEditor(a, true); });
        Appearance.Set(mode, false);
    }
}
