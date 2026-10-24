using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.ConcreteSection;

namespace X.Desktop;

/// <summary>
/// --check-sezione-view &lt;radice&gt; &lt;cartella&gt; [parti] (UiTests, F5.10, view after S9, proposal F5 section 7.3): the XAML view of the RC section
/// (ConcreteSectionSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the bench opens it with the option module-impl str_palo=mvvm, in windows
/// outside the screen (never activated, no mouse). The Release opens this view since the switch of 23/10/2026 (the historic view is archived in
/// supporto/SUPERATI/ui-storica/sezione-ca). The parts never open the historic view: what they compared with it opened beside the new one is compared with the values it gave, recorded
/// by this check before the switch in tests/fixtures/f5-sezione/vista-storica.json (prompt of the switches of 23/10, as for the pile group). Parts:
/// <list type="number">
/// <item>«banco»: with the option the bench opens the MVVM view, the Release too (switch of 23/10/2026), the historic view is refused (archived).</item>
/// <item>«disegni»: the drawings of the view, fed by the plain data of the ViewModel through <see cref="ConcreteSectionDrawings"/> (the section of
/// the control panel, the stress maps of the three families with every contour of the Rara for two fixtures, the section of the shear, the section
/// σ/ε and the strain plane of the 3D panel at the limit point and at the action, the 2D domain, the M–χ curve), for every F3 fixture, equal to the
/// display lists of the historic view frozen in tests/fixtures/f5-sezione/disegni/elenco.txt.</item>
/// <item>«esportazioni»: for each of the 11 F3 fixtures, the MVVM view against the recorded values of the historic view: the data after the opening, after
/// the visit of the F3 capture (and the three appearances that write nothing), after the calculation, the result; then the exports: the Word report
/// with every part (figures included, dates normalized) and the PDF beside it, the short report, the JSON of the results, the report of the M–χ
/// curve with its figure.</item>
/// <item>«schermate»: the tab 01 of the view in Chiara, Scuro and Molto scuro (folder schermate; 144 DPI on this machine), and the eight tabs.</item>
/// <item>«aiuto»: every F1 on a field of every form of the eight tabs reaches the IWikiNavigator of the services (lesson 1 of 17/10), F1 elsewhere
/// the guide of the module.</item>
/// <item>«sola-lettura»: in the eight tabs the buttons and check boxes that accept the user after InspectRevision are those of the historic view.</item>
/// <item>«tracce»: the 57 frozen scenarios of tests/Shared/ConcreteSectionTrace.cs driven through the controls of the view, found by their automation names,
/// with the dialogs shown and driven through their controls, equal to the frozen traces (ConcreteSectionViewChecks.Trace.cs).</item>
/// <item>«nomi»: the automation names of the historic view kept, every control the user reaches in the eight tabs named
/// (ConcreteSectionViewChecks.Windows.cs).</item>
/// <item>«finestre»: «Scala…» of the 3D and 2D domains and «Proprietà / report…» driven through their controls beside the historic windows.</item>
/// </list>
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static partial class ConcreteSectionViewChecks
{
    private const string Module = ConcreteSectionScenarios.Module;

    private static int checks;
    private static readonly List<string> Log = [];

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks++;
        Log.Add("OK " + message);
    }

    /// <summary>The parts of the check, in order; <paramref name="parts"/> (the fourth argument) runs only some of them.</summary>
    internal static async Task Run(string root, string output, IReadOnlyCollection<string>? parts = null)
    {
        bool Part(string name) => parts is null || parts.Contains(name);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della prova deve essere vuota.");
        Directory.CreateDirectory(output);
        checks = 0; Log.Clear();
        var culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        // The drawings first, in the culture state of --check-sezione-historic that froze disegni/elenco.txt: it-IT only in this async flow, the
        // culture of the user (here with the decimal point) in the operations that the dispatcher runs outside it. The opening of a fixture shown
        // on the 2D domain (circolare-cava) draws the title of the chart in the calculation queued by Loaded, outside the flow: «θ = 30.00°».
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture = culture;
        try
        {
            if (Part("banco")) Bench(root);
            if (Part("disegni")) await Drawings(root, Path.Combine(output, "disegni"));
            // The traces in the same culture state: the frozen traces were written by the same capture (θ = 30.00°, question V-a).
            if (Part("tracce")) await Traces(root, Path.Combine(output, "tracce"));
            // Then it-IT also for the later operations of the thread (lesson of F5.4): the exports compare the two views in the same culture.
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { System.Globalization.CultureInfo.CurrentCulture = culture; System.Globalization.CultureInfo.CurrentUICulture = culture; });
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture = culture;
            if (Part("esportazioni")) await Exports(root, Path.Combine(output, "esportazioni"));
            if (Part("schermate")) await Screenshots(root, Path.Combine(output, "schermate"));
            if (Part("aiuto")) await Help(root);
            if (Part("sola-lettura")) await ReadOnly(root);
            if (Part("nomi")) await AutomationNames(root);
            if (Part("finestre")) await Windows(root);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML della sezione c.a. (MvvmSheetEditor, fuori schermo)"
            + (parts is null ? "" : " · solo " + string.Join(",", parts)), new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- services and hosting

    /// <summary>The bench choice of the check: the RC section with the MVVM view.</summary>
    private static readonly string[] BenchArguments = [ModuleHostBench.Argument, Module + "=mvvm"];

    /// <summary>Services of the sheets in the check: errors rethrown, messages recorded, no question and no dialog expected, recorded Wiki.</summary>
    private sealed class Services : IConfirmationService, IDialogService, IFileDialogService, IWikiNavigator, IMessageService
    {
        internal List<string?> Wiki { get; } = [];
        internal List<(string Title, string Message)> Messages { get; } = [];

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
        public string? SaveFile(SaveFileRequest request) => throw new InvalidOperationException("Dialogo di salvataggio non previsto.");
        public string? ChooseFolder(string? title = null) => throw new InvalidOperationException("Cartella non prevista.");
        public void Show(string? uri = null) => Wiki.Add(uri);
    }

    private static SheetEditorFactory Editors(Services services) =>
        new(ModuleHostBench.Implementations(BenchArguments), DesktopComposition.CreateModules(), services.Create(), new CalculationLimiter(CalculationLimiter.DefaultMaximum));

    /// <summary>A sheet in a window outside the screen, 1400 × 1000 as the historic traces lay out the view; the dialogs of the view off screen too.</summary>
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
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Sezione c.a. " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
            if (Editor is MvvmSheetEditor)
            {
                static void Away(Window w) { w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = -32000; w.Top = -32000; w.ShowActivated = false; w.ShowInTaskbar = false; }
                View.ShowWindow = w => { Away(w); Dialogs.Add(w); w.Show(); w.UpdateLayout(); return null; };
                View.ShowModeless = w => { Away(w); Dialogs.Add(w); w.Show(); w.UpdateLayout(); };
            }
        }

        internal FrameworkElement Root => Editor.View;

        internal ConcreteSheetViewModel Sheet => (ConcreteSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal ConcreteSectionSheetView View => CompositeSectionViewChecks.Walk(Root).OfType<ConcreteSectionSheetView>().Single();

        internal void Layout() { window.UpdateLayout(); Root.UpdateLayout(); }

        /// <summary>Nothing left to do: the calculation, the queue and the work of the panels of the sheet.</summary>
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
                if (watch.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("La sezione c.a. MVVM non si ferma: " + s.Status);
            }
            Layout();
        }

        internal byte[] Png()
        {
            Layout();
            var element = (FrameworkElement)window.Content;
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

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    // ---------------------------------------------------------------- 1. bench and Release

    private static void Bench(string root)
    {
        var editors = Editors(new Services());
        using (var mvvm = editors.Open(Module, ConcreteSectionScenarios.FixtureData(root, "default")))
            Check(mvvm is MvvmSheetEditor { Sheet: ConcreteSheetViewModel sheet } && sheet.Inputs.AllForms.All(f => f.WikiNavigator is not null),
                "Con l'opzione del banco la sezione c.a. si apre nella vista MVVM, con il navigatore della Wiki in ogni form");
        using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(Module, ConcreteSectionScenarios.FixtureData(root, "default")))
            Check(release is MvvmSheetEditor { Sheet: ConcreteSheetViewModel } && ModuleImplementations.Release.For(Module) == SheetImplementation.Mvvm,
                "La Release apre la sezione c.a. con la vista MVVM (passaggio del 23/10/2026)");
        bool historicRefused = false;
        try { ModuleHostBench.Implementations([ModuleHostBench.Argument, Module + "=historic"]); }
        catch (ArgumentException error) when (error.Message.Contains("archiviata", StringComparison.Ordinal)) { historicRefused = true; }
        Check(historicRefused, "Banco: la vista storica della sezione c.a. è archiviata, «historic» rifiutato");
    }

    // ---------------------------------------------------------------- 2. drawings

    /// <summary>
    /// The drawings of every F3 fixture as the historic DrawForChecks draws them (same tabs, families, states and sizes), fed by the data of the
    /// ViewModel: the section of the control panel from the view, the others through <see cref="ConcreteSectionDrawings"/> (as the figures of the
    /// report and the tabs of the view), compared line by line with tests/fixtures/f5-sezione/disegni/elenco.txt.
    /// </summary>
    private static async Task Drawings(string root, string output)
    {
        Directory.CreateDirectory(output);
        var list = new List<string>();
        var editors = Editors(new Services());
        foreach (string id in ConcreteSectionScenarios.Fixtures(root))
        {
            using var host = new Hosted(editors.Open(Module, ConcreteSectionScenarios.FixtureData(root, id)));
            var sheet = host.Sheet;
            await host.Settle(); await sheet.CalculateAsync(); await host.Settle();
            async Task Show(int tab) { sheet.SelectedTab = tab; await host.Settle(); }
            await Show(0);
            list.Add(ConcreteSectionDrawingRecord.Record(output, id + " sezione 900x700", host.View.Preview, 900, 700));
            foreach (var panel in sheet.StressPanels)
            {
                await Show(ConcreteSheetViewModel.StressTab); sheet.StressFamily = panel.Index; await host.Settle();
                if (panel.Map.Stress is null) continue;
                // The map of the family in the view (tab 04).
                var map = CompositeSectionViewChecks.Walk(host.Root).OfType<ConcreteStressPanelView>().Single(v => ReferenceEquals(v.DataContext, panel)).Map;
                list.Add(ConcreteSectionDrawingRecord.Record(output, id + " " + panel.Key + " 900x700", map, 900, 700));
                if (id is not ("default" or "sle-fessurazione-assi") || panel.Key != "SLE") continue;
                string shown = map.Contour;
                foreach (string other in ConcreteSectionViewport.Contours)
                {
                    map.Contour = other;
                    list.Add(ConcreteSectionDrawingRecord.Record(output, id + " " + panel.Key + " " + other + " 900x700", map, 900, 700));
                }
                map.Contour = shown;
            }
            await Show(ConcreteSheetViewModel.ShearTab);
            list.Add(ConcreteSectionDrawingRecord.Record(output, id + " taglio 700x600", host.View.ShearView.Drawing, 700, 600));
            await Show(1);
            var inspection = sheet.Domain3D.Inspection; sheet.Domain3D.InspectionTab = 1; await host.Settle();
            foreach (int state in new[] { 1, 0 })
            {
                inspection.State = state; await host.Settle();
                string label = state == 1 ? "limite" : "azione";
                list.Add(ConcreteSectionDrawingRecord.Record(output, id + " ispezione 3D " + label + " 800x650", host.View.Domain3DView.Map, 800, 650));
                list.Add(ConcreteSectionDrawingRecord.Record(output, id + " piano 3D " + label + " 800x650", host.View.Domain3DView.Plane, 800, 650));
            }
            await Show(2);
            list.Add(ConcreteSectionDrawingRecord.Record(output, id + " dominio 2D 900x700", (Plot)host.View.Domain2DView.Drawing!, 900, 700));
            await Show(6);
            await sheet.Curvature.CalculateCommand.ExecuteAsync(null); await host.Settle();
            list.Add(ConcreteSectionDrawingRecord.Record(output, id + " momento-curvatura 900x650", host.View.CurvatureView.Chart, 900, 650));
        }
        ConcreteSectionTrace.Write(Path.Combine(output, "elenco.txt"), list);
        string? difference = ConcreteSectionTrace.FirstDifference(ConcreteSectionTrace.Read(Path.Combine(ConcreteSectionScenarios.Drawings(root), "elenco.txt")), list);
        Check(difference is null, $"Disegni della sezione c.a. dai dati del ViewModel uguali agli elenchi congelati della vista storica ({list.Count} disegni)"
            + (difference is null ? "" : ": " + difference));
    }

    // ---------------------------------------------------------------- 3. F3 fixtures and exports

    /// <summary>
    /// For the 11 F3 fixtures, on the MVVM view: the data after the opening, after the visit of the F3 capture (and the three appearances that write
    /// nothing), after the calculation, the result, the sections saved by the report, the exported results, equal to those of the historic view on the
    /// same data, recorded by this check before the archiving (tests/fixtures/f5-sezione/vista-storica.json: the texts compared, as the check made
    /// them, numbered identifiers and outcomes of the parallel steps in the fixed order, C6); the Word report with every part (figures included,
    /// dates normalized), the short report and the report of the M–χ curve equal part by part to the historic ones (recorded); the PDF beside the Word
    /// report equal to the one that ReportFiles writes from that Word report, as SheetEditor.ExportReport wrote it.
    /// </summary>
    private static async Task Exports(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var editors = Editors(new Services());
        var sections = ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet();
        var recorded = Recorded(root)["esportazioni"]!.AsObject();
        var fixtures = ConcreteSectionScenarios.Fixtures(root);
        Check(fixtures.Order(StringComparer.Ordinal).SequenceEqual(recorded.Select(p => p.Key).Order(StringComparer.Ordinal)),
            "Fixture F3 della sezione c.a.: " + fixtures.Count + ", tutte con i valori registrati della vista storica");
        int reports = 0;
        foreach (string id in fixtures)
        {
            string name = Module + "--" + id;
            var historic = recorded[id]!.AsObject();
            var data = ConcreteSectionScenarios.FixtureData(root, id);
            using var mvvm = new Hosted(editors.Open(Module, data));
            var sides = new Sides(ConcreteSectionScenarios.FixtureData(root, id), mvvm, historic["sequenza"]!.AsArray());
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: ConcreteSheetViewModel }, name + ": editor MVVM con la vista XAML");
            await mvvm.Settle();
            mvvm.Editor.Commit();
            sides.Dump(folder, name + "-apertura");
            Check(sides.SameData, name + ": dati all'apertura, dopo il calcolo automatico, uguali a quelli della vista storica (registrati)");
            CompositeSectionViewChecks.Visit(mvvm.Root);
            await mvvm.Settle();
            mvvm.Editor.Commit();
            sides.Dump(folder, name + "-visita");
            Check(sides.SameData, $"{name}: la visita della cattura F3 scrive come la vista storica (Modified {mvvm.Modified}, registrati)");
            string visited = mvvm.Editor.Data.ToJsonString(J.Options); int modified = mvvm.Modified;
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            mvvm.Editor.Commit();
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == visited && mvvm.Modified == modified, name + ": i tre aspetti non scrivono e non sollevano Modified");
            await mvvm.Editor.CalculateAsync();
            await mvvm.Settle();
            mvvm.Editor.Commit();
            sides.Dump(folder, name);
            Check(sides.SameData && mvvm.Editor.HasResults == historic["risultati"]!.GetValue<bool>() && sides.SameResult,
                name + ": dati e risultato dopo il calcolo uguali a quelli della vista storica (registrati; esiti dei passi paralleli nell'ordine fisso, C6)");
            if (!historic["risultati"]!.GetValue<bool>())
            {
                Check(Refused(() => mvvm.Editor.BuildReport("Report", sections)) == historic["rifiuto_report"]!.GetValue<string>()
                    && Refused(() => mvvm.Editor.BuildShortReport("Breve")) == historic["rifiuto_breve"]!.GetValue<string>(), name + ": report rifiutati come nella vista storica (registrati)");
                Check(sides.Complete, name + ": confronti con la vista storica tutti eseguiti");
                continue;
            }
            // Word report with the figures and the PDF beside it, short report, JSON of the results: the bytes of the historic view.
            string title = "Fixture F3 · " + Module + " · " + id;
            int mvvmBefore = mvvm.Modified;
            mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
            string? part = DifferentParts(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), historic["report"]!.AsObject(), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della vista storica, figure comprese (date normalizzate, registrato)" + (part is null ? "" : ", diversa " + part));
            // The PDF of the editor beside the Word report: the one that ReportFiles writes from that Word report, as SheetEditor.ExportReport wrote it. The
            // cover carries the time to the minute: two writings astride a minute differ, so both are written again.
            int attempts = 1;
            void Expected() => Anthea.Reports.ReportFiles.Write(Path.Combine(folder, name + "-attesa.docx"), File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), title);
            Expected();
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))) && attempts < 3)
            {
                attempts++;
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
                Expected();
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))),
                name + ": PDF affiancato uguale a quello di ReportFiles dal report Word" + (attempts > 1 ? $" ({attempts} esportazioni)" : ""));
            Check(sides.SameData && (mvvm.Modified - mvvmBefore) / attempts == historic["report_modified"]!.GetValue<int>(),
                name + ": il report salva le sezioni scelte come la vista storica, con gli stessi Modified (registrati)");
            // F5.13 g3 (G10): the bytes of the short report beside the other exports, to read a difference.
            byte[] briefReport = mvvm.Editor.BuildShortReport("Breve " + id);
            File.WriteAllBytes(Path.Combine(folder, name + "-breve.docx"), briefReport);
            string? brief = DifferentParts(briefReport, historic["breve"]!.AsObject(), normalizer);
            Check(brief is null, name + ": report breve uguale a quello della vista storica (registrato)" + (brief is null ? "" : ", diversa " + brief));
            mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json"));
            byte[] mvvmJson = File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.json"));
            // As the check made it beside the historic view: the bytes when they were equal, otherwise the texts (identifiers numbered, C6).
            Check(historic["json_uguali"]!.GetValue<bool>() ? ShaBytes(mvvmJson) == historic["json"]!.GetValue<string>() : sides.SameExport(JsonNode.Parse(mvvmJson)!),
                name + ": risultati JSON uguali a quelli della vista storica (registrati; esiti dei passi paralleli nell'ordine fisso, C6)");
            // The M–χ curve, then its report with the figure.
            await mvvm.Sheet.Curvature.CalculateCommand.ExecuteAsync(null);
            await mvvm.Settle();
            Check((mvvm.Sheet.Curvature.Result is not null) == historic["curva"]!.GetValue<bool>(), name + ": curva M–χ calcolata come nella vista storica");
            if (mvvm.Sheet.Curvature.Result is not null)
            {
                string? curve = DifferentParts(mvvm.Editor.BuildReport(title, ["curvatura", "grafici"]), historic["report_curva"]!.AsObject(), normalizer);
                Check(curve is null, name + ": report della curva M–χ con la figura uguale a quello della vista storica (registrato)" + (curve is null ? "" : ", diversa " + curve));
            }
            Check(sides.Complete, name + ": confronti con la vista storica tutti eseguiti");
            reports++;
        }
        Log.Add($"   esportazioni: {reports} report");
        Check(reports >= 9, $"Report confrontati con la vista storica: {reports}");

        static string Refused(Action action) { try { action(); return "disponibile"; } catch (InvalidOperationException error) { return error.Message; } }
    }

    /// <summary>
    /// The results as text with the outcomes of the parallel steps in the fixed order of the checks (decision C6): the historic view writes «domini»,
    /// «tensioni» and «errori_calcolo» in the order in which the threads of the batch end, which changes from one run to the next (circolare-cava,
    /// 22/10: 3D:SLV before 3D:SLU in one run, after it in the next); the ViewModel always in the order of <see cref="ConcreteCalculation.AllCalculations"/>.
    /// Every other key, and every value, stays as written.
    /// </summary>
    private static string FixedOrder(JsonNode result)
    {
        var copy = result.DeepClone();
        if (copy is JsonObject root)
            foreach (string key in new[] { "domini", "tensioni", "errori_calcolo" })
                if (root[key] is JsonObject parts)
                {
                    var ordered = parts.Select((p, i) => (p.Key, Index: i)).OrderBy(p => ConcreteCalculation.AllCalculations.IndexOf(p.Key) is var at and >= 0 ? at : int.MaxValue)
                        .ThenBy(p => p.Index).Select(p => p.Key).ToArray();
                    var values = ordered.Select(k => (k, parts[k])).ToArray();
                    parts.Clear();
                    foreach (var (k, value) in values) parts[k] = value;
                }
        return copy.ToJsonString(J.Options);
    }

    /// <summary>
    /// The data, the result and the exported results of the MVVM view on a fixture, as text to compare with those of the historic view recorded in the
    /// same order (<paramref name="recorded"/>, SHA-256 of each text): the identifiers that the view creates (rows without «id» in the fixture get
    /// Guid.NewGuid at the opening) are numbered in the order in which they appear, as in the traces (<see cref="ConcreteGuids"/>, always data first),
    /// the outcomes of the parallel steps in the fixed order (<see cref="FixedOrder"/>, C6). The texts are taken in the order in which the check took
    /// them beside the historic view, so that the numbering and the recorded values match.
    /// </summary>
    private sealed class Sides(JsonObject fixture, Hosted mvvm, JsonArray recorded)
    {
        private readonly ConcreteGuids mvvmGuids = new(fixture);
        private int next;

        private string Historic() => next < recorded.Count ? recorded[next++]!.GetValue<string>()
            : throw new InvalidOperationException("Valori registrati della vista storica esauriti al confronto " + (next + 1));

        private (string Mvvm, string Historic) Data() => (mvvmGuids.Normalize(mvvm.Editor.Data.ToJsonString(J.Options)), Historic());

        // The data first in every comparison: the numbering of the identifiers follows the data.
        private (string Mvvm, string Historic) Result()
        {
            Data();
            return (mvvm.Editor.Result is { } r ? mvvmGuids.Normalize(FixedOrder(r)) : "null", Historic());
        }

        internal bool SameData => Data() is var (a, b) && Sha(a) == b;

        internal bool SameResult => Result() is var (a, b) && Sha(a) == b;

        internal bool SameExport(JsonNode a)
        {
            Data();
            return Sha(mvvmGuids.Normalize(FixedOrder(a))) == Historic();
        }

        /// <summary>Every recorded value compared.</summary>
        internal bool Complete => next == recorded.Count;

        /// <summary>The data and the results of the view beside the exports when they differ from the recorded ones, to read the difference.</summary>
        internal void Dump(string folder, string name)
        {
            var (a, b) = Data(); var (ra, rb) = Result();
            if (Sha(a) == b && Sha(ra) == rb) return;
            File.WriteAllText(Path.Combine(folder, name + "-dati-mvvm.json"), a);
            File.WriteAllText(Path.Combine(folder, name + "-risultato-mvvm.json"), ra);
        }
    }

    /// <summary>
    /// The values of the historic view recorded by this check before its archiving (passaggio della Release della sezione c.a.):
    /// tests/fixtures/f5-sezione/vista-storica.json.
    /// </summary>
    private static JsonObject Recorded(string root) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "f5-sezione", "vista-storica.json")))!.AsObject();

    /// <summary>SHA-256 of the UTF-8 text, as the values of <see cref="Recorded"/>.</summary>
    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string ShaBytes(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    /// F5.13 g3 (G10): the line «ANTHEA · gg/mm/aaaa · esito» of the short report (ReportConcreteShort) carries the day of the run without the time, which
    /// NormalizeReportTimestamps leaves (it replaces only «ANTHEA · gg/mm/aaaa hh:mm»): the impronte of tests/fixtures/f5-sezione/vista-storica.json have the
    /// day of the recording, 23/10/2026, in that line. The day of the run (± 1 day, as TextNormalizer) is written back as the day of the recording, so that
    /// the short report is compared on every day; any other date of the line is kept.
    /// </summary>
    private static string RecordingDay(string xml) => ShortReportDay.Replace(xml, m =>
    {
        var day = new DateOnly(int.Parse(m.Groups["y"].Value), int.Parse(m.Groups["m"].Value), int.Parse(m.Groups["d"].Value));
        return Math.Abs(day.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber) <= 1 ? m.Groups["prefix"].Value + "23/10/2026" : m.Value;
    });

    private static readonly Regex ShortReportDay = new(@"(?<prefix><w:t(?:\s+xml:space=""preserve"")?>ANTHEA · )(?<d>\d{2})/(?<m>\d{2})/(?<y>\d{4})(?= · )", RegexOptions.Compiled);

    /// <summary>
    /// First part of a Word document that differs from the parts recorded for the historic one (SHA-256 of docProps/core.xml and word/document.xml
    /// with the dates normalized as in F3, of every other part byte for byte); null when equal.
    /// </summary>
    private static string? DifferentParts(byte[] docx, JsonObject recorded, TextNormalizer normalizer)
    {
        using var zip = new ZipArchive(new MemoryStream(docx));
        var names = zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(recorded.Select(p => p.Key).Order(StringComparer.Ordinal))) return "elenco delle parti";
        foreach (string name in names)
        {
            using var stream = zip.GetEntry(name)!.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy); byte[] bytes = copy.ToArray();
            string sha = name == "docProps/core.xml" ? Sha(normalizer.Normalize(Encoding.UTF8.GetString(bytes)))
                : name == "word/document.xml" ? Sha(RecordingDay(normalizer.NormalizeReportTimestamps(Encoding.UTF8.GetString(bytes)))) : ShaBytes(bytes);
            if (sha != recorded[name]!.GetValue<string>()) return name;
        }
        return null;
    }

    /// <summary>First part of two Word documents that differs, with the dates of core.xml and of the text normalized as in F3; null when equal.</summary>
    private static string? DifferentPart(byte[] a, byte[] b, TextNormalizer normalizer)
    {
        using var first = new ZipArchive(new MemoryStream(a)); using var second = new ZipArchive(new MemoryStream(b));
        var names = first.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(second.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal))) return "elenco delle parti";
        foreach (string name in names)
        {
            byte[] x = Read(first.GetEntry(name)!), y = Read(second.GetEntry(name)!);
            bool equal = name is "docProps/core.xml" or "word/document.xml"
                ? Normalize(name, x, normalizer) == Normalize(name, y, normalizer) : x.SequenceEqual(y);
            if (!equal) return name;
        }
        return null;
        static byte[] Read(ZipArchiveEntry entry) { using var stream = entry.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray(); }
        static string Normalize(string name, byte[] bytes, TextNormalizer normalizer) => name == "docProps/core.xml"
            ? normalizer.Normalize(Encoding.UTF8.GetString(bytes)) : normalizer.NormalizeReportTimestamps(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>Two PDF files equal but for what changes at every rendering (dates, identifiers, tags of the font subsets).</summary>
    private static bool SamePdf(byte[] a, byte[] b)
    {
        static string Text(byte[] bytes)
        {
            string text = Encoding.Latin1.GetString(bytes);
            text = Regex.Replace(text, @"\(D:[0-9+\-Z']+\)", "(D:)");
            text = Regex.Replace(text, @"/ID\s*\[[^\]]*\]", "/ID[]");
            text = Regex.Replace(text, @"(?<=/(?:FontName|BaseFont)/)[A-Z]{6}\+", "SUBSET+");
            text = Regex.Replace(text, @"(<xmp:\w*Date>)[^<]*(<)", "$1$2");
            return Regex.Replace(text, @"uuid:[0-9a-fA-F-]+", "uuid:");
        }
        return a.SequenceEqual(b) || Text(a) == Text(b);
    }

    // ---------------------------------------------------------------- 4. screenshots

    private static async Task Screenshots(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            using var host = new Hosted(editors.Open(Module, ConcreteSectionScenarios.FixtureData(root, "default")), mode != AppAppearance.Light, 1600, 1000);
            await host.Settle();
            foreach (var e in CompositeSectionViewChecks.Walk(host.Root).OfType<Expander>().ToArray()) e.IsExpanded = true;
            await host.Settle();
            File.WriteAllBytes(Path.Combine(folder, $"vista-pannello-{suffix}.png"), host.Png());
            written++;
        }
        Appearance.Set(AppAppearance.Light, false);
        // Every tab of the view, in Chiara, after the calculation and the M–χ curve: schede/NN-vista.png (the historic view is not opened any more: its
        // screenshots stay in the artifacts of the runs before the switch).
        string tabs = Path.Combine(folder, "schede");
        Directory.CreateDirectory(tabs);
        var data = ConcreteSectionScenarios.FixtureData(root, "default");
        using (var mvvm = new Hosted(editors.Open(Module, data), false, 1600, 1000))
        {
            await mvvm.Settle();
            await mvvm.Editor.CalculateAsync();
            await mvvm.Sheet.Curvature.CalculateCommand.ExecuteAsync(null);
            await mvvm.Settle();
            foreach (int tab in BoundTabs)
            {
                mvvm.Sheet.SelectedTab = tab;
                await mvvm.Settle();
                string number = (tab + 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
                File.WriteAllBytes(Path.Combine(tabs, number + "-vista.png"), mvvm.Png());
                written++;
            }
        }
        Check(written == 3 + BoundTabs.Length, "Schermate del pannello di controllo nei tre aspetti e delle otto schede: "
            + written + " PNG in " + folder + " (144 DPI su questa macchina)");
    }

    // ---------------------------------------------------------------- 5. help

    private static async Task Help(string root)
    {
        var services = new Services();
        using var host = new Hosted(Editors(services).Open(Module, ConcreteSectionScenarios.FixtureData(root, "default")));
        await host.Settle();
        services.Wiki.Clear();
        int presses = 0; var forms = new HashSet<FormViewModel>();
        // The tabs bound so far: 01 «Pannello di controllo», 02 and 03, the domains (section 20 of the note).
        foreach (int tab in BoundTabs)
        {
            host.Sheet.SelectedTab = tab; await host.Settle();
            for (int pass = 0; pass < 3; pass++) { foreach (var e in CompositeSectionViewChecks.Walk(host.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await host.Settle(); }
            var editors = CompositeSectionViewChecks.Walk(host.Root).OfType<Control>()
                .Where(e => e.IsVisible && e.DataContext is FieldViewModel { HelpUri: not null } && e is TextBox or ComboBox or CheckBox && e.TemplatedParent is not ComboBox).ToArray();
            foreach (var editor in editors) { F1(editor); presses++; forms.Add(((FieldViewModel)editor.DataContext).Form); }
        }
        Check(presses > 40 && services.Wiki.Count == presses && services.Wiki.All(u => u is not null),
            $"F1 su {presses} campi di {forms.Count} form delle schede legate: ogni pressione arriva al navigatore della Wiki dei servizi ({services.Wiki.Count}/{presses})");
        string guide = Anthea.Wiki.WikiCatalog.ForModule(Module)?.Id ?? throw new InvalidOperationException("Guida assente nella Wiki: " + Module);
        services.Wiki.Clear();
        F1(CompositeSectionViewChecks.Walk(host.Root).OfType<TabControl>().First());
        Check(services.Wiki.SequenceEqual([guide]), "F1 fuori dai campi: la guida del modulo dal navigatore (" + guide + ")");
    }

    // ---------------------------------------------------------------- 6. read-only

    /// <summary>The main tabs of the view, every one bound to its ViewModel (section 20 of docs/refactoring/f5-10-sezione.md).</summary>
    private static readonly int[] BoundTabs = [0, 1, 2, 3, 4, 5, 6, 7];

    /// <summary>
    /// The controls of the eight tabs that accept the user after InspectRevision, in the new view, against those of the historic view on the same data,
    /// recorded before the switch.
    /// </summary>
    private static async Task ReadOnly(string root)
    {
        var data = ConcreteSectionScenarios.FixtureData(root, "default");
        using var host = new Hosted(Editors(new Services()).Open(Module, data));
        await host.Settle();
        host.Editor.InspectRevision();
        await host.Settle();
        var after = new Dictionary<string, string>(StringComparer.Ordinal);
        var tabs = CompositeSectionViewChecks.Walk(host.Root).OfType<TabControl>().First();
        foreach (int index in BoundTabs)
        {
            tabs.SelectedIndex = index; await host.Settle();
            for (int pass = 0; pass < 3; pass++) { foreach (var e in CompositeSectionViewChecks.Walk(host.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await host.Settle(); }
            foreach (var element in CompositeSectionViewChecks.Walk((DependencyObject)tabs.SelectedContent).OfType<ButtonBase>().Where(e => e.IsVisible && e.Content is string { Length: > 0 }))
            {
                string key = index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + element.GetType().Name + ":" + element.Content;
                string value = element.IsEnabled ? "libero" : "bloccato";
                after[key] = after.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
            }
        }
        var before = Recorded(root)["sola_lettura"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>(), StringComparer.Ordinal);
        var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
        var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
        Log.Add("   sola lettura, solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
        Log.Add("   sola lettura, solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
        Check(different.Length == 0 && common.Length >= 30, $"Sola lettura nelle schede legate: stessi pulsanti e caselle bloccati e liberi della vista storica, registrati ({common.Length} comuni)"
            + (different.Length == 0 ? "" : ": " + string.Join("; ", different)));
        Check(host.Editor.HasResults, "Sola lettura: il risultato resta");
    }
}
