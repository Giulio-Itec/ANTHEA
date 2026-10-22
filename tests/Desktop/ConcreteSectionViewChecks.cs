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
/// outside the screen (never activated, no mouse). The Release keeps the historic view (decisions of 17/10 and 22/10). Parts:
/// <list type="number">
/// <item>«banco»: with the option the bench opens the MVVM view, the Release keeps the historic one.</item>
/// <item>«disegni»: the drawings of the sheet fed by the plain data of the ViewModel through <see cref="ConcreteSectionDrawings"/> (the section of
/// the control panel of the view, the stress maps of the three families with every contour of the Rara for two fixtures, the section of the shear,
/// the section σ/ε and the strain plane of the 3D panel at the limit point and at the action, the 2D domain, the M–χ curve), for every F3 fixture,
/// equal to the display lists of the historic view frozen in tests/fixtures/f5-sezione/disegni/elenco.txt.</item>
/// <item>«esportazioni»: for each of the 11 F3 fixtures, on the same data, the MVVM view and the historic view: the data after the opening, after
/// the visit of the F3 capture (and the three appearances that write nothing), after the calculation, the result; then the exports: the Word report
/// with every part (figures included, dates normalized) and the PDF beside it, the short report, the JSON of the results, the report of the M–χ
/// curve with its figure.</item>
/// <item>«schermate»: the tab 01 of the view in Chiara, Scuro and Molto scuro (folder schermate; 144 DPI on this machine), and the historic one.</item>
/// <item>«aiuto»: every F1 on a field of every form of the tab 01 reaches the IWikiNavigator of the services (lesson 1 of 17/10), F1 elsewhere the
/// guide of the module.</item>
/// <item>«sola-lettura»: in the tab 01 the controls that accept the user after InspectRevision are those of the historic view.</item>
/// </list>
/// The traces through the controls of the view (tests/Shared/ConcreteSectionTrace.cs) come with the tabs 02–08 of the view (section 20 of
/// docs/refactoring/f5-10-sezione.md). Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static class ConcreteSectionViewChecks
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
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { System.Globalization.CultureInfo.CurrentCulture = culture; System.Globalization.CultureInfo.CurrentUICulture = culture; });
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.CurrentUICulture = culture;
        try
        {
            if (Part("banco")) Bench(root);
            if (Part("disegni")) await Drawings(root, Path.Combine(output, "disegni"));
            if (Part("esportazioni")) await Exports(root, Path.Combine(output, "esportazioni"));
            if (Part("schermate")) await Screenshots(root, Path.Combine(output, "schermate"));
            if (Part("aiuto")) await Help(root);
            if (Part("sola-lettura")) await ReadOnly(root);
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

        internal ConcreteWorkspace Historic => ((SheetEditor)Editor).ConcreteForChecks;

        internal void Layout() { window.UpdateLayout(); Root.UpdateLayout(); }

        /// <summary>Nothing left to do: the calculation, the queue and the work of the panels of the sheet (SettleForChecks for the historic view).</summary>
        internal async Task Settle()
        {
            Layout();
            if (Editor is SheetEditor) { await Historic.SettleForChecks(); Layout(); return; }
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
            Check(release is SheetEditor && ModuleImplementations.Release.For(Module) == SheetImplementation.Historic, "La Release resta storica fino al passaggio con l'utente");
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
            list.Add(ConcreteSectionHistoricTrace.Record(output, id + " sezione 900x700", host.View.Preview, 900, 700));
            foreach (var panel in sheet.StressPanels)
            {
                await Show(ConcreteSheetViewModel.StressTab); sheet.StressFamily = panel.Index; await host.Settle();
                if (panel.Map.Stress is null) continue;
                var map = new ConcreteSectionViewport(); ConcreteSectionDrawings.Apply(map, panel.Map);
                list.Add(ConcreteSectionHistoricTrace.Record(output, id + " " + panel.Key + " 900x700", map, 900, 700));
                if (id is not ("default" or "sle-fessurazione-assi") || panel.Key != "SLE") continue;
                foreach (string other in ConcreteSectionViewport.Contours)
                {
                    map.Contour = other;
                    list.Add(ConcreteSectionHistoricTrace.Record(output, id + " " + panel.Key + " " + other + " 900x700", map, 900, 700));
                }
            }
            await Show(ConcreteSheetViewModel.ShearTab);
            var shear = new ConcreteSectionViewport(); ConcreteSectionDrawings.Apply(shear, sheet.Shear.Drawing);
            list.Add(ConcreteSectionHistoricTrace.Record(output, id + " taglio 700x600", shear, 700, 600));
            await Show(1);
            var inspection = sheet.Domain3D.Inspection; sheet.Domain3D.InspectionTab = 1; await host.Settle();
            foreach (int state in new[] { 1, 0 })
            {
                inspection.State = state; await host.Settle();
                string label = state == 1 ? "limite" : "azione";
                var view = new ConcreteSectionViewport(); ConcreteSectionDrawings.Apply(view, inspection.Map);
                list.Add(ConcreteSectionHistoricTrace.Record(output, id + " ispezione 3D " + label + " 800x650", view, 800, 650));
                list.Add(ConcreteSectionHistoricTrace.Record(output, id + " piano 3D " + label + " 800x650", new ConcreteStrainPlaneView { Diagram = inspection.Plane }, 800, 650));
            }
            await Show(2);
            var plot = ConcreteSectionDrawings.DomainPlot(); ConcreteSectionDrawings.Apply(plot, sheet.Domain2D.Chart!);
            list.Add(ConcreteSectionHistoricTrace.Record(output, id + " dominio 2D 900x700", plot, 900, 700));
            await Show(6);
            await sheet.Curvature.CalculateCommand.ExecuteAsync(null); await host.Settle();
            var curve = PlotPresentation.CurvaturePlot();
            if (sheet.Curvature.Result is { } result) PlotPresentation.ApplyCurvature(curve, result);
            list.Add(ConcreteSectionHistoricTrace.Record(output, id + " momento-curvatura 900x650", curve, 900, 650));
        }
        ConcreteSectionTrace.Write(Path.Combine(output, "elenco.txt"), list);
        string? difference = ConcreteSectionTrace.FirstDifference(ConcreteSectionTrace.Read(Path.Combine(ConcreteSectionScenarios.Drawings(root), "elenco.txt")), list);
        Check(difference is null, $"Disegni della sezione c.a. dai dati del ViewModel uguali agli elenchi congelati della vista storica ({list.Count} disegni)"
            + (difference is null ? "" : ": " + difference));
    }

    // ---------------------------------------------------------------- 3. F3 fixtures and exports

    private static async Task Exports(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var editors = Editors(new Services());
        var sections = ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet();
        int reports = 0;
        foreach (string id in ConcreteSectionScenarios.Fixtures(root))
        {
            string name = Module + "--" + id;
            var data = ConcreteSectionScenarios.FixtureData(root, id);
            using var mvvm = new Hosted(editors.Open(Module, data));
            using var historic = new Hosted(new SheetEditor(Module, data));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: ConcreteSheetViewModel }, name + ": editor MVVM con la vista XAML");
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Check(Same(mvvm, historic), name + ": dati all'apertura, dopo il calcolo automatico, uguali a quelli della vista storica");
            CompositeSectionViewChecks.Visit(mvvm.Root); CompositeSectionViewChecks.Visit(historic.Root);
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Check(Same(mvvm, historic), $"{name}: la visita della cattura F3 scrive come la vista storica (Modified {mvvm.Modified} / {historic.Modified})");
            string visited = mvvm.Editor.Data.ToJsonString(J.Options); int modified = mvvm.Modified;
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            mvvm.Editor.Commit();
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == visited && mvvm.Modified == modified, name + ": i tre aspetti non scrivono e non sollevano Modified");
            await mvvm.Editor.CalculateAsync(); await historic.Editor.CalculateAsync();
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Dump(folder, name, mvvm, historic);
            Check(Same(mvvm, historic) && mvvm.Editor.HasResults == historic.Editor.HasResults
                && (historic.Editor.Result is null || mvvm.Editor.Result!.ToJsonString(J.Options) == historic.Editor.Result.ToJsonString(J.Options)),
                name + ": dati e risultato dopo il calcolo uguali a quelli della vista storica");
            if (!historic.Editor.HasResults)
            {
                Check(Refused(() => mvvm.Editor.BuildReport("Report", sections)) == Refused(() => historic.Editor.BuildReport("Report", sections))
                    && Refused(() => mvvm.Editor.BuildShortReport("Breve")) == Refused(() => historic.Editor.BuildShortReport("Breve")), name + ": report rifiutati come nella vista storica");
                continue;
            }
            // Word report with the figures and the PDF beside it, short report, JSON of the results: the bytes of the historic view.
            string title = "Fixture F3 · " + Module + " · " + id;
            int mvvmBefore = mvvm.Modified, historicBefore = historic.Modified;
            mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
            historic.Editor.ExportReport(Path.Combine(folder, name + "-storica.docx"), title, sections);
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.docx")), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della vista storica, figure comprese (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))), name + ": PDF affiancato uguale");
            Check(Same(mvvm, historic) && mvvm.Modified - mvvmBefore == historic.Modified - historicBefore, name + ": il report salva le sezioni scelte come la vista storica, con gli stessi Modified");
            string? brief = DifferentPart(mvvm.Editor.BuildShortReport("Breve " + id), historic.Editor.BuildShortReport("Breve " + id), normalizer);
            Check(brief is null, name + ": report breve uguale a quello della vista storica" + (brief is null ? "" : ", diversa " + brief));
            mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json")); historic.Editor.ExportResult(Path.Combine(folder, name + "-storica.json"));
            Check(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, name + "-storica.json"))), name + ": risultati JSON uguali");
            // The M–χ curve on both views, then its report with the figure.
            await mvvm.Sheet.Curvature.CalculateCommand.ExecuteAsync(null); await historic.Historic.CalculateCurvatureForReportCheck();
            await mvvm.Settle(); await historic.Settle();
            if (mvvm.Sheet.Curvature.Result is not null)
            {
                string? curve = DifferentPart(mvvm.Editor.BuildReport(title, ["curvatura", "grafici"]), historic.Editor.BuildReport(title, ["curvatura", "grafici"]), normalizer);
                Check(curve is null, name + ": report della curva M–χ con la figura uguale" + (curve is null ? "" : ", diversa " + curve));
            }
            reports++;
        }
        Log.Add($"   esportazioni: {reports} report");
        Check(reports >= 9, $"Report confrontati con la vista storica: {reports}");

        static bool Same(Hosted a, Hosted b) => a.Editor.Data.ToJsonString(J.Options) == b.Editor.Data.ToJsonString(J.Options);
        static string Refused(Action action) { try { action(); return "disponibile"; } catch (InvalidOperationException error) { return error.Message; } }
    }

    /// <summary>The data and the results of the two views beside the exports when they differ, to read the difference.</summary>
    private static void Dump(string folder, string name, Hosted mvvm, Hosted historic)
    {
        string a = mvvm.Editor.Data.ToJsonString(J.Options), b = historic.Editor.Data.ToJsonString(J.Options);
        string? ra = mvvm.Editor.Result?.ToJsonString(J.Options), rb = historic.Editor.Result?.ToJsonString(J.Options);
        if (a == b && ra == rb) return;
        File.WriteAllText(Path.Combine(folder, name + "-dati-mvvm.json"), a); File.WriteAllText(Path.Combine(folder, name + "-dati-storica.json"), b);
        File.WriteAllText(Path.Combine(folder, name + "-risultato-mvvm.json"), ra ?? "null"); File.WriteAllText(Path.Combine(folder, name + "-risultato-storica.json"), rb ?? "null");
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
        using (var historic = new Hosted(new SheetEditor(Module, ConcreteSectionScenarios.FixtureData(root, "default")), false, 1600, 1000))
        {
            await historic.Settle();
            File.WriteAllBytes(Path.Combine(folder, "storica-pannello-chiara.png"), historic.Png());
            written++;
        }
        Check(written == 4, "Schermate del pannello di controllo nei tre aspetti e della vista storica: " + written + " PNG in " + folder + " (144 DPI su questa macchina)");
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

    /// <summary>The main tabs bound to their ViewModels so far (section 20 of docs/refactoring/f5-10-sezione.md): 01, 02 and 03.</summary>
    private static readonly int[] BoundTabs = [0, 1, 2];

    /// <summary>The controls of the tab 01 that accept the user after InspectRevision, in the new view and in the historic view on the same data.</summary>
    private static async Task ReadOnly(string root)
    {
        var data = ConcreteSectionScenarios.FixtureData(root, "default");
        using var host = new Hosted(Editors(new Services()).Open(Module, data));
        using var historic = new Hosted(new SheetEditor(Module, data));
        await host.Settle(); await historic.Settle();
        host.Editor.InspectRevision(); historic.Editor.InspectRevision();
        await host.Settle(); await historic.Settle();
        var maps = new List<Dictionary<string, string>>();
        foreach (var hosted in new[] { historic, host })
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var tabs = CompositeSectionViewChecks.Walk(hosted.Root).OfType<TabControl>().First();
            foreach (int index in BoundTabs)
            {
                tabs.SelectedIndex = index; await hosted.Settle();
                for (int pass = 0; pass < 3; pass++) { foreach (var e in CompositeSectionViewChecks.Walk(hosted.Root).OfType<Expander>().ToArray()) e.IsExpanded = true; await hosted.Settle(); }
                foreach (var element in CompositeSectionViewChecks.Walk((DependencyObject)tabs.SelectedContent).OfType<ButtonBase>().Where(e => e.IsVisible && e.Content is string { Length: > 0 }))
                {
                    string key = index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + element.GetType().Name + ":" + element.Content;
                    string value = element.IsEnabled ? "libero" : "bloccato";
                    map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
                }
            }
            maps.Add(map);
        }
        var (before, after) = (maps[0], maps[1]);
        var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
        var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
        Log.Add("   sola lettura, solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
        Log.Add("   sola lettura, solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
        Check(different.Length == 0 && common.Length >= 30, $"Sola lettura nelle schede legate: stessi pulsanti e caselle bloccati e liberi della vista storica ({common.Length} comuni)"
            + (different.Length == 0 ? "" : ": " + string.Join("; ", different)));
        Check(host.Editor.HasResults, "Sola lettura: il risultato resta");
    }
}
