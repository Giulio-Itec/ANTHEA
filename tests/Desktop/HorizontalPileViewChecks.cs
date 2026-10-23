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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.Common;
using X.Desktop.Views.HorizontalPile;

namespace X.Desktop;

/// <summary>
/// --check-palo-orizzontale-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.7 and F5.8, proposal F5 section 7.3): the XAML view of the horizontal
/// pile and micropile (HorizontalPileSheetView, the tab of the capacity, and the view of the elastic response with the reinforcement) hosted by
/// <see cref="MvvmSheetEditor"/>, as the bench opens it with <c>--module-impl geo_palo_orizzontale=mvvm --module-impl geo_micropalo_orizzontale=mvvm</c>,
/// in windows outside the screen (never activated, no mouse). Since 23/10/2026 the Release opens this view and the historic one is archived
/// (supporto/SUPERATI/ui-storica/palo-orizzontale and palo-elastico): the comparisons with the historic view are replaced by the frozen traces
/// and drawings and by independent expectations. Parts:
/// <list type="number">
/// <item>Traces through the view: the 27 scenarios of tests/Shared/HorizontalPileTrace.cs driven through the controls of the view (fields,
/// choices and check boxes of the FieldForm, tabs, buttons, cells of the stratigraphies, the soil editors) with the held engine of the traces,
/// and read back from the controls, equal line by line to the traces of the historic view frozen in tests/fixtures/f5-palo-orizzontale/storica
/// (with the declared difference F5-02, O6); the display lists of the diagrams of «Diagrammi e dettagli» and of the profile drawn by the view,
/// equal to those frozen.</item>
/// <item>For each of the 21 F3 fixtures of the two modules: the visit (every tab but the analyses, «Estendi»/«Riduci», three appearances) writes
/// nothing and raises no Modified; after «Calcola» the exports of the result shown: JSON equal to the result, Word report and PDF beside it,
/// «Esporta CSV» with the tables of the historic button (HorizontalPileEditor.Csv); without a result the export is refused with its message.</item>
/// <item>F5-02 (O6): the choice «Acciaio» of the view after a custom value of the steel, and the note of a class of the catalogue.</item>
/// <item>Screenshots of the view in Chiara, Scuro and Molto scuro (folder schermate; 144 DPI on this machine).</item>
/// <item>Help: the «?» labels shown (logged); every «?» and every F1 on a field of every form reaches the IWikiNavigator
/// of the services (lesson 1 of 17/10), F1 elsewhere the guide of the module, «Modello e dati comuni…» the theory page (CalculationGuide).</item>
/// <item>Automation names (logged), those that ui/smoke-horizontal and ElasticPile.UiChecks read among them.</item>
/// <item>Read-only revision: the controls after InspectRevision (logged); typing, layers, soil and commands refused.</item>
/// <item>The shared soil transfer dialog (T5, O7): «Invia / carica terreno…» of the pile and of the micropile, a copy and a replacement sent to
/// the window.</item>
/// <item>Bench and Release: the two modules open the MVVM view, in the bench and in the Release.</item>
/// <item>Traces of the elastic response through the view (F5.8): the 20 scenarios of tests/Shared/ElasticPileTrace.cs driven through the controls of
/// the view and of its dialogs (split, proposal, design shown outside the screen; the c.a. verifier as the dialog of the services with the historic
/// ConcreteWorkspace of its view, E1), equal line by line to the traces of the historic view frozen in tests/fixtures/f5-elastico/storica; the
/// drawings the view shows (the response with the options of the fixture, with a depth chosen by a click, the sheet of the reinforcement) equal to
/// those frozen.</item>
/// <item>The tab of the elastic response: every «?» and F1 of its four forms to the navigator, the automation names (logged) and the
/// controls that ElasticPile.UiChecks finds, read-only refusals, the c.a. verifier with its return (E1), «Esporta CSV» and «Esporta
/// PNG…» of the sheet with the bytes of the historic buttons on the result shown.</item>
/// </list>
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static partial class HorizontalPileViewChecks
{
    private const string Pile = HorizontalPileScenarios.Pile, Micro = HorizontalPileScenarios.Micro;

    private static int checks;
    private static readonly List<string> Log = [];

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks++;
        Log.Add("OK " + message);
    }

    internal static async Task Run(string root, string output, string? only = null)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della prova deve essere vuota.");
        Directory.CreateDirectory(output);
        checks = 0; Log.Clear();
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        bool Part(string name) => only is null || only.Split(',').Contains(name);
        try
        {
            if (Part("tracce")) await CapacityTraces(root, output);
            if (Part("disegni")) await CapacityDrawings(root, output);
            if (Part("esportazioni")) await FixturesAndExports(root, output);
            if (Part("acciaio")) await CustomSteel(root);
            if (Part("schermate")) await Screenshots(root, Path.Combine(output, "schermate"));
            if (Part("schermate-elastica")) await ElasticScreenshots(root, Path.Combine(output, "schermate"));
            if (Part("aiuto")) await Help(root);
            if (Part("nomi")) await AutomationNames(root);
            if (Part("sola-lettura")) await ReadOnly(root);
            if (Part("terreno")) await SoilDialog(root);
            if (Part("banco")) Bench(root);
            if (Part("tracce-elastiche")) await ElasticTraces(root, output);
            if (Part("disegni-elastici")) await ElasticDrawings(root, output);
            if (Part("elastica")) await ElasticChecks(root, output);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            await Elastic.DrainAsync();
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML del palo e del micropalo orizzontale (MvvmSheetEditor, fuori schermo)"
            + (only is null ? "" : " · solo " + only), new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- services and hosting

    /// <summary>The bench choices of the check: the two modules with the MVVM view.</summary>
    private static readonly string[] BenchArguments = [ModuleHostBench.Argument, Pile + "=mvvm", ModuleHostBench.Argument, Micro + "=mvvm"];

    /// <summary>The calculations of the elastic response of every sheet of the check, drained before the end (as Presentation.Tests).</summary>
    private static readonly HeldElasticAnalyses Elastic = new();

    /// <summary>
    /// Services of the sheets in the check: errors rethrown, messages recorded, every question answered yes, scripted dialogs and files,
    /// recorded Wiki and links.
    /// </summary>
    private sealed class Services : IConfirmationService, IDialogService, IFileDialogService, IWikiNavigator, IMessageService, IExternalLinkService
    {
        internal List<string?> Wiki { get; } = [];
        internal List<Uri> Links { get; } = [];
        internal List<(string Title, string Message)> Messages { get; } = [];
        internal List<string> Questions { get; } = [];
        internal string? SavePath { get; set; }
        internal int? CopyDestination { get; set; }
        internal int Soils { get; private set; }
        internal Func<SoilTransferDialogViewModel, SoilTransferChoice?>? SoilTransfer { get; set; }
        internal Func<object, Task<object?>>? Dialog { get; set; }

        internal PresentationServices Create()
        {
            var bench = ModuleHostBench.Services();
            return new PresentationServices(this, this, this, this, bench.Clipboard, this, this);
        }

        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista: dati condivisi.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista: spostamento.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista: chiusura.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista: salvataggio.");
        public bool ConfirmDelete(string title, string question) { Questions.Add(title + ": " + question); return true; }

        public void ReportFailure(string title, Exception error) => throw new InvalidOperationException("Errore di un comando: " + title, error);
        public void Inform(string title, string message) => Messages.Add((title, message));
        public void Warn(string title, string message) => Messages.Add((title, message));

        /// <summary>«Copia in…»/«Copia da…»: the stratigraphy of <see cref="CopyDestination"/>; the soil transfer through <see cref="SoilTransfer"/>; others through <see cref="Dialog"/>.</summary>
        public async Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            if (dialog is Anthea.Presentation.Modules.VerticalPile.SurveyCopyDialogViewModel copy && CopyDestination is int destination)
            {
                copy.Selected = copy.Choices.Single(c => c.Survey == destination);
                copy.ConfirmCommand.Execute(null);
                return dialog.Result;
            }
            if (dialog is SoilTransferDialogViewModel transfer)
            {
                Soils++;
                return SoilTransfer is { } answer ? (TResult?)(object?)answer(transfer) : default;
            }
            if (Dialog is { } shown) return (TResult?)await shown(dialog);
            throw new InvalidOperationException("Finestra non prevista: " + dialog.Title);
        }

        public string? OpenFile(OpenFileRequest request) => throw new InvalidOperationException("Dialogo di apertura non previsto.");
        public string? SaveFile(SaveFileRequest request) => SavePath ?? throw new InvalidOperationException("Dialogo di salvataggio non previsto.");
        public string? ChooseFolder(string? title = null) => throw new InvalidOperationException("Cartella non prevista.");
        public void Show(string? uri = null) => Wiki.Add(uri);
        public void Open(Uri target) => Links.Add(target);
    }

    /// <summary>The modules of the check: the composition root of the desktop, the elastic response of each sheet behind the barrier of the traces.</summary>
    private static ModuleRegistry Modules(HeldElasticAnalyses? elastic = null)
    {
        var analyses = elastic ?? Elastic;
        var horizontal = new[] { Pile, Micro }.Select(m => (ISheetModule)new HorizontalPileSheetModule(m, new HorizontalPileFigureRenderer(), analyses.Create)).ToArray();
        return new ModuleRegistry(DesktopComposition.CreateModules().SheetModuleIds.Select(id => id is Pile or Micro ? horizontal.Single(h => h.Info.Id == id)
            : DesktopComposition.CreateModules().SheetModule(id)));
    }

    private static SheetEditorFactory Editors(Services services) =>
        new(ModuleHostBench.Implementations(BenchArguments), Modules(), services.Create(), new CalculationLimiter(CalculationLimiter.DefaultMaximum));

    /// <summary>A sheet in a window outside the screen, 1400×1000 like the sheet area of the main window (the size of the historic traces).</summary>
    private sealed class Hosted : IDisposable
    {
        private readonly Window window;
        internal ISheetEditor Editor { get; }
        internal int Modified;
        internal List<(string Module, JsonObject Data, string Name)> Copies { get; } = [];
        internal List<JsonObject> Replacements { get; } = [];

        internal Hosted(ISheetEditor editor, bool dark = false, double width = 1400, double height = 1000)
        {
            Editor = editor;
            editor.Modified += () => Modified++;
            editor.ModuleCopyRequested += (module, data, name) => Copies.Add((module, data, name));
            editor.SoilReplacementRequested += data => Replacements.Add(data);
            window = new Window
            {
                Content = editor.View, Width = width, Height = height, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Palo orizzontale " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
            if (Editor is MvvmSheetEditor) View.ShowWindow = ShowOffScreen;
        }

        internal FrameworkElement Root => Editor.View;

        internal HorizontalPileSheetViewModel Sheet => (HorizontalPileSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal HorizontalPileSheetView View => Visual(Root).OfType<HorizontalPileSheetView>().Single();

        /// <summary>The windows of the view shown by the last command (dialogs of the view), still open.</summary>
        internal List<Window> Dialogs { get; } = [];

        internal bool? ShowOffScreen(Window dialog)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.Manual; dialog.Left = -32000; dialog.Top = -32000; dialog.ShowActivated = false; dialog.ShowInTaskbar = false;
            dialog.Show(); dialog.UpdateLayout();
            Dialogs.Add(dialog);
            return null;
        }

        internal void CloseDialogs()
        {
            foreach (var dialog in Dialogs) if (dialog.IsVisible) dialog.Close();
            Dialogs.Clear();
        }

        internal void Layout() { window.UpdateLayout(); Root.UpdateLayout(); }

        internal async Task Settle()
        {
            Layout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
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
            CloseDialogs();
            window.Content = null;
            window.Close();
            Editor.Dispose();
        }
    }

    internal static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--) pending.Push(VisualTreeHelper.GetChild(node, i));
        }
    }

    private static string? BoundPath(DependencyObject element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    private static bool Usable(UIElement element) => element.IsEnabled && element.IsVisible;

    /// <summary>A click of the user: the Click handlers of the button, then its command, when the button is usable.</summary>
    private static void Press(Button button)
    {
        if (!Usable(button)) return;
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        if (button.Command is { } command && command.CanExecute(button.CommandParameter)) command.Execute(button.CommandParameter);
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    // ---------------------------------------------------------------- screenshots

    /// <summary>The tab of the elastic response (F5.8) in the three appearances, the whole page (a window as high as the page), pile and micropile.</summary>
    private static async Task ElasticScreenshots(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        var cases = new[] { ("palo", Pile, "elastico-tratti-anello-spirale"), ("micropalo", Micro, "elastico-chs-stratificato") };
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            foreach (var (name, module, id) in cases)
            {
                using var host = new Hosted(editors.Open(module, HorizontalPileScenarios.FixtureData(root, module, id)), mode != AppAppearance.Light);
                await host.Sheet.Elastic.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(120));
                await host.Settle();
                // The whole page of the tab (the scroll viewer shows a part of it), on the background of the view.
                var page = Visual(host.Root).OfType<ElasticPileView>().Single().Page;
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(page.ActualWidth), (int)Math.Ceiling(page.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                var background = new DrawingVisual();
                using (var context = background.RenderOpen()) context.DrawRectangle((Brush)page.FindResource("Token.Surface"), null, new Rect(0, 0, page.ActualWidth, page.ActualHeight));
                bitmap.Render(background); bitmap.Render(page);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(Path.Combine(folder, $"elastica-{name}-{suffix}.png"))) encoder.Save(file);
                written++;
            }
        }
        Appearance.Set(AppAppearance.Light, false);
        Check(written == 6, "Schermate della scheda elastica nei tre aspetti: " + written + " PNG in " + folder);
    }

    private static async Task Screenshots(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        var cases = new[] { ("palo", Pile, "broms-coesivo-impedita"), ("micropalo", Micro, "stratificato-chs-catalogo") };
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            foreach (var (name, module, id) in cases)
            {
                using var host = new Hosted(editors.Open(module, HorizontalPileScenarios.FixtureData(root, module, id)), mode != AppAppearance.Light);
                await host.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
                await host.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"capacita-{name}-{suffix}.png"), host.Png());
                written++;
            }
        }
        Appearance.Set(AppAppearance.Light, false);
        using (var wide = new Hosted(editors.Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "broms-coesivo-impedita")), width: 1700, height: 1000))
        {
            await wide.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
            await wide.Settle();
            File.WriteAllBytes(Path.Combine(folder, "capacita-palo-larga.png"), wide.Png());
            foreach (string card in new[] { HorizontalPileSheetViewModel.SurveysCard, HorizontalPileSheetViewModel.ProfileCard, HorizontalPileSheetViewModel.SectionCard })
            {
                wide.Sheet.ToggleCardCommand.Execute(card); await wide.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"capacita-palo-estesa-{card}.png"), wide.Png());
                wide.Sheet.ToggleCardCommand.Execute(card); await wide.Settle();
                written++;
            }
            Press(wide.View.DetailsButton);
            await wide.Settle();
            var dialog = wide.Dialogs.Single();
            File.WriteAllBytes(Path.Combine(folder, "capacita-palo-diagrammi.png"), Ui.Snapshot(dialog));
            wide.CloseDialogs();
            written += 2;
        }
        Check(written == 11, "Schermate della scheda della capacità nei tre aspetti, larga, estesa e dei diagrammi: " + written + " PNG in " + folder);
    }
}
