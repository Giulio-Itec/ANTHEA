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
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Modules.VerticalPile;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using CommunityToolkit.Mvvm.Input;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.Common;
using X.Desktop.Views.VerticalPile;

namespace X.Desktop;

/// <summary>
/// --check-palo-verticale-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.6, proposal F5 section 7.3): the XAML view of the vertical pile and
/// micropile (VerticalPileSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the bench opens it with
/// <c>--module-impl geo_palo_verticale=mvvm --module-impl geo_micropalo_verticale=mvvm</c>, in windows outside the screen (never activated,
/// no mouse). Parts:
/// <list type="number">
/// <item>Traces through the view: the 18 scenarios of tests/Shared/VerticalPileTrace.cs driven through the controls of the view (fields,
/// choices and check boxes of the FieldForm, tabs, buttons, cells of the stratigraphies, curves, views and tables) with the held engine of
/// the traces, and read back from the controls (the charts from the Plot of the view), equal line by line to the traces of the historic
/// view frozen in tests/fixtures/f5-palo-verticale/storica.</item>
/// <item>For each of the 13 F3 fixtures of the two modules: the visit (every tab, three appearances, «Estendi»/«Riduci») writes nothing and
/// raises no Modified; data and result after the calculation are those of the frozen trace of the historic view (scenario palo-/micropalo-&lt;id&gt;);
/// the exports are those the historic view wrote on the same data: JSON of the result (J.Options), Word report (date normalized) and PDF side
/// by side of its recipe (ReportWord with ModuleFigureRenderer.VerticalFigures on the charts of the result), «Salva PNG…» of the capacity chart
/// (the image of the historic chart, recorded as SHA-256; V8 accepted).</item>
/// <item>V3 (registro F5-01): the micropile with a result changes injection through the choice of the view without failing.</item>
/// <item>Screenshots of the view in Chiara, Scuro and Molto scuro (folder schermate).</item>
/// <item>Help: the «?» labels shown are those of the historic view, «?» and F1 open their section through the IWikiNavigator, F1 elsewhere the
/// guide of the module, «Modello e dati comuni…» the theory page of the module (CalculationGuide, the page of
/// MainWindow.ShowContextualWikiTheory).</item>
/// <item>Automation names: those of the historic view kept, and the names of the layer cells that ui/smoke-display uses.</item>
/// <item>Read-only revision: the controls that accept the user are those of the historic view after InspectRevision.</item>
/// <item>Shared dialogs (T5): SoilTransferDialogViewModel against the historic window SoilTransferDialog on the same data (profiles, status,
/// description, preview, destinations, guidance, project sheets, send, replacement); «Invia / carica terreno…» routes the choice to the window.</item>
/// <item>Bench and Release: with --module-impl and in the Release the two modules open the MVVM view.</item>
/// </list>
/// The historic view is archived (f5-pass-geo, Q11: supporto/SUPERATI/ui-storica/palo-verticale): what the checks compared with it is compared
/// with the values it gave, frozen in its traces or recorded here (<see cref="HistoricHelpLabels"/>, <see cref="HistoricAutomationNames"/>,
/// <see cref="HistoricReadOnly"/>, <see cref="HistoricCapacityPng"/>, <see cref="HistoricNoResult"/>). The shared soil dialog is still compared
/// with the window SoilTransferDialog, which the other historic modules use.
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static class VerticalPileViewChecks
{
    private const string Pile = VerticalPileScenarios.Pile, Micro = VerticalPileScenarios.Micro;

    // ---------------------------------------------------------------- values of the historic view (archived with the Release switch)
    // Recorded by this check on the historic view before its archiving: the capture run on db7ef68d with the values logged
    // (supporto/artefatti/ci/20261023-124229-geo-cattura-3 of the worktree aw-f5-pass-geo), equal to the checks of every run up to the one of
    // the release 1.13.0 (supporto/artefatti/ci/20261023-013037-full-fine-ca-1.13.0 of the main checkout).

    /// <summary>The message of SheetEditor.ExportResult of a vertical pile sheet without a result.</summary>
    private const string HistoricNoResult = "Premere Calcola prima di esportare.";

    /// <summary>The «?» labels shown by the historic view (fixture default), in ordinal order.</summary>
    private static readonly Dictionary<string, string[]> HistoricHelpLabels = new(StringComparer.Ordinal)
    {
        [Pile] = ["Wiki: Azione assiale — Compressione", "Wiki: Azione assiale — Trazione", "Wiki: Considera sottospinta", "Wiki: Correlazione", "Wiki: Correlazione", "Wiki: Diametro palo", "Wiki: Lunghezza palo", "Wiki: Metodo", "Wiki: Peso proprio — Favorevole", "Wiki: Peso proprio — Sfavorevole", "Wiki: Presenza falda", "Wiki: Profondità falda", "Wiki: Sicurezza di base", "Wiki: Sicurezza laterale — Compressione", "Wiki: Sicurezza laterale — Trazione", "Wiki: Tipo battuto", "Wiki: Tipo di palo", "Wiki: Verticali indagate"],
        [Micro] = ["Wiki: Considera punta", "Wiki: Diametro perforazione", "Wiki: Inclinazione dalla verticale", "Wiki: Iniezione", "Wiki: Inizio aderenza lungo asse", "Wiki: Lunghezza palo", "Wiki: Peso al metro del micropalo", "Wiki: Pressione di iniezione", "Wiki: Profilo CHS", "Wiki: Punta rispetto alla laterale"],
    };

    /// <summary>The AutomationProperties.Name of the visible elements of the historic view, by module and fixture.</summary>
    private static readonly Dictionary<(string Module, string Id), string[]> HistoricAutomationNames = new()
    {
        [(Pile, "default")] = ["Addensamento", "Azione assiale — Compressione", "Azione assiale — Trazione", "Coeff. k [-]", "Coeff. α [-]", "Coeff. μ [-]", "Considera sottospinta", "Correlazione", "Cu [kPa]", "Diametro palo", "Fattore Nc [-]", "Fattore Nq [-]", "Lunghezza palo", "Metodo", "Peso proprio — Favorevole", "Peso proprio — Sfavorevole", "Peso specifico CLS / palo", "Presenza falda", "Profondità falda", "Sicurezza di base", "Sicurezza laterale — Compressione", "Sicurezza laterale — Trazione", "Spessore S [m]", "Tipo battuto", "Tipo di palo", "Tipologia", "Verticali indagate", "Wiki: Azione assiale — Compressione", "Wiki: Azione assiale — Trazione", "Wiki: Considera sottospinta", "Wiki: Correlazione", "Wiki: Diametro palo", "Wiki: Lunghezza palo", "Wiki: Metodo", "Wiki: Peso proprio — Favorevole", "Wiki: Peso proprio — Sfavorevole", "Wiki: Presenza falda", "Wiki: Profondità falda", "Wiki: Sicurezza di base", "Wiki: Sicurezza laterale — Compressione", "Wiki: Sicurezza laterale — Trazione", "Wiki: Tipo battuto", "Wiki: Tipo di palo", "Wiki: Verticali indagate", "c′ [kPa]", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]"],
        [(Pile, "battuto-falda-sondaggi")] = ["Addensamento", "Azione assiale — Compressione", "Azione assiale — Trazione", "Coeff. k [-]", "Coeff. α [-]", "Coeff. μ [-]", "Considera sottospinta", "Correlazione", "Cu [kPa]", "Diametro palo", "Fattore Nc [-]", "Fattore Nq [-]", "Interasse X", "Interasse Y", "Lunghezza palo", "Metodo", "Pali in X", "Pali in Y", "Peso proprio — Favorevole", "Peso proprio — Sfavorevole", "Peso specifico CLS / palo", "Presenza falda", "Profondità falda", "Sicurezza di base", "Sicurezza laterale — Compressione", "Sicurezza laterale — Trazione", "Spessore S [m]", "Tipo battuto", "Tipo di palo", "Tipologia", "Verticali indagate", "Wiki: Azione assiale — Compressione", "Wiki: Azione assiale — Trazione", "Wiki: Considera sottospinta", "Wiki: Correlazione", "Wiki: Diametro palo", "Wiki: Interasse X", "Wiki: Interasse Y", "Wiki: Lunghezza palo", "Wiki: Metodo", "Wiki: Pali in X", "Wiki: Pali in Y", "Wiki: Peso proprio — Favorevole", "Wiki: Peso proprio — Sfavorevole", "Wiki: Presenza falda", "Wiki: Profondità falda", "Wiki: Sicurezza di base", "Wiki: Sicurezza laterale — Compressione", "Wiki: Sicurezza laterale — Trazione", "Wiki: Tipo battuto", "Wiki: Tipo di palo", "Wiki: Verticali indagate", "c′ [kPa]", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]"],
        [(Micro, "default")] = ["Aderenza τ [kPa]", "Azione assiale — Compressione", "Azione assiale — Trazione", "Considera punta", "Correlazione", "Diametro perforazione", "Inclinazione dalla verticale", "Iniezione", "Inizio aderenza lungo asse", "Lunghezza palo", "Metodo", "Peso al metro del micropalo", "Peso proprio — Favorevole", "Peso proprio — Sfavorevole", "Pressione di iniezione", "Profilo CHS", "Punta rispetto alla laterale", "Sicurezza di base", "Sicurezza laterale — Compressione", "Sicurezza laterale — Trazione", "Spessore verticale [m]", "Terreno", "Verticali indagate", "Wiki: Considera punta", "Wiki: Diametro perforazione", "Wiki: Inclinazione dalla verticale", "Wiki: Iniezione", "Wiki: Inizio aderenza lungo asse", "Wiki: Lunghezza palo", "Wiki: Peso al metro del micropalo", "Wiki: Pressione di iniezione", "Wiki: Profilo CHS", "Wiki: Punta rispetto alla laterale", "α adottato"],
        [(Micro, "regressione-micropalo_igu_0_nessuna-riduzione")] = ["Aderenza τ [kPa]", "Azione assiale — Compressione", "Azione assiale — Trazione", "Considera punta", "Correlazione", "Diametro perforazione", "Inclinazione dalla verticale", "Iniezione", "Inizio aderenza lungo asse", "Lunghezza palo", "Metodo", "Peso al metro del micropalo", "Peso proprio — Favorevole", "Peso proprio — Sfavorevole", "Pressione di iniezione", "Profilo CHS", "Punta rispetto alla laterale", "Sicurezza di base", "Sicurezza laterale — Compressione", "Sicurezza laterale — Trazione", "Spessore verticale [m]", "Terreno", "Verticali indagate", "Wiki: Considera punta", "Wiki: Diametro perforazione", "Wiki: Inclinazione dalla verticale", "Wiki: Iniezione", "Wiki: Inizio aderenza lungo asse", "Wiki: Lunghezza palo", "Wiki: Peso al metro del micropalo", "Wiki: Pressione di iniezione", "Wiki: Profilo CHS", "Wiki: Punta rispetto alla laterale", "α adottato"],
    };

    /// <summary>The visible input controls of the historic view after InspectRevision and their state (<see cref="Controls"/>), by module.</summary>
    private static readonly Dictionary<string, Dictionary<string, string>> HistoricReadOnly = new(StringComparer.Ordinal)
    {
        [Pile] = new(StringComparer.Ordinal) { ["pulsante +"] = "bloccato", ["pulsante Aggiungi strato"] = "bloccato", ["pulsante Copia da…"] = "bloccato", ["pulsante Copia in…"] = "bloccato", ["pulsante Estendi"] = "libero", ["pulsante Invia / carica terreno…"] = "bloccato", ["pulsante Modello e dati comuni…"] = "libero", ["pulsante Nessuna"] = "libero", ["pulsante Reset"] = "bloccato", ["pulsante Tutte"] = "libero", ["pulsante [−]"] = "bloccato", ["pulsante −"] = "bloccato", ["scelta Addensamento"] = "bloccato", ["scelta Metodo"] = "bloccato", ["scelta Tipo battuto"] = "bloccato", ["scelta Tipo di palo"] = "bloccato", ["scelta Tipologia"] = "bloccato", ["scelta Verticali indagate"] = "bloccato", ["spunta Considera sottospinta"] = "bloccato", ["spunta Dren. C · progetto: 3.211,0"] = "libero", ["spunta Dren. T · progetto: 719,9"] = "libero", ["spunta Ed compressione: 814,0"] = "libero", ["spunta Ed trazione: 0,0"] = "libero", ["spunta Laterale"] = "bloccato", ["spunta Non dren. C · progetto: 3.211,0"] = "libero", ["spunta Non dren. T · progetto: 719,9"] = "libero", ["spunta Presenza falda"] = "bloccato", ["testo Azione assiale — Compressione"] = "bloccato", ["testo Azione assiale — Trazione"] = "bloccato", ["testo Coeff. k [-]"] = "bloccato", ["testo Coeff. α [-]"] = "bloccato", ["testo Coeff. μ [-]"] = "bloccato", ["testo Correlazione"] = "bloccato", ["testo Cu [kPa]"] = "bloccato", ["testo Diametro palo"] = "bloccato", ["testo Fattore Nc [-]"] = "bloccato", ["testo Fattore Nq [-]"] = "bloccato", ["testo Lunghezza palo"] = "bloccato", ["testo Peso proprio — Favorevole"] = "bloccato", ["testo Peso proprio — Sfavorevole"] = "bloccato", ["testo Peso specifico CLS / palo"] = "bloccato", ["testo Profondità falda"] = "bloccato", ["testo Sicurezza di base"] = "bloccato", ["testo Sicurezza laterale — Compressione"] = "bloccato", ["testo Sicurezza laterale — Trazione"] = "bloccato", ["testo Spessore S [m]"] = "bloccato", ["testo c′ [kPa]"] = "bloccato", ["testo γ [kN/m³]"] = "bloccato", ["testo γsat [kN/m³]"] = "bloccato", ["testo φ′ [°]"] = "bloccato" },
        [Micro] = new(StringComparer.Ordinal) { ["pulsante +"] = "bloccato", ["pulsante Aggiungi strato"] = "bloccato", ["pulsante Copia da…"] = "bloccato", ["pulsante Copia in…"] = "bloccato", ["pulsante Estendi"] = "libero", ["pulsante Modello e dati comuni…"] = "libero", ["pulsante Nessuna"] = "libero", ["pulsante Reset"] = "bloccato", ["pulsante Tutte"] = "libero", ["pulsante [−]"] = "bloccato", ["pulsante −"] = "bloccato", ["scelta Iniezione"] = "bloccato", ["scelta Metodo"] = "bloccato", ["scelta Profilo CHS"] = "bloccato", ["scelta Terreno"] = "bloccato", ["scelta Verticali indagate"] = "bloccato", ["spunta C · progetto: 2.074,2"] = "libero", ["spunta Considera punta"] = "bloccato", ["spunta Ed compressione: 217,9"] = "libero", ["spunta Ed trazione: 86,2"] = "libero", ["spunta Laterale"] = "bloccato", ["spunta T · progetto: 1.692,0"] = "libero", ["testo Aderenza τ [kPa]"] = "bloccato", ["testo Azione assiale — Compressione"] = "bloccato", ["testo Azione assiale — Trazione"] = "bloccato", ["testo Correlazione"] = "bloccato", ["testo Diametro perforazione"] = "bloccato", ["testo Inclinazione dalla verticale"] = "bloccato", ["testo Inizio aderenza lungo asse"] = "bloccato", ["testo Lunghezza palo"] = "bloccato", ["testo Peso al metro del micropalo"] = "bloccato", ["testo Peso proprio — Favorevole"] = "bloccato", ["testo Peso proprio — Sfavorevole"] = "bloccato", ["testo Pressione di iniezione"] = "bloccato", ["testo Punta rispetto alla laterale"] = "bloccato", ["testo Sicurezza di base"] = "bloccato", ["testo Sicurezza laterale — Compressione"] = "bloccato", ["testo Sicurezza laterale — Trazione"] = "bloccato", ["testo Spessore verticale [m]"] = "bloccato", ["testo α adottato"] = "bloccato" },
    };

    /// <summary>SHA-256 of the PNG of «Salva PNG…» of the historic capacity chart (Plot.Png, 1200×750 at 96 DPI), by fixture with a result.</summary>
    private static readonly Dictionary<string, string> HistoricCapacityPng = new(StringComparer.Ordinal)
    {
        ["geo_palo_verticale--battuto-falda-sondaggi"] = "2DEB1CCAE8D15BCC4F4712872B8FB88AF1187159C282E2EC2CCD6564937B5CA0",
        ["geo_palo_verticale--copertura-insufficiente"] = "5B417E3547B4CC3DE90AAD806B483E281FB1DDD4C0BEFB9BEA5AAF0F3B002C7B",
        ["geo_palo_verticale--elica-coesivo-avvisi"] = "228E9064E7E3827C4579D28490CC04D3CE75ACDB906A57234D9FEA71A28081D6",
        ["geo_palo_verticale--regressione-palo_storico_0"] = "76CAEA84E52394804FB49536C6CF75E87C74B5DEDA22A0AD5A13B8FF94FE197B",
        ["geo_micropalo_verticale--igu-copertura-avvisi"] = "AF58E9ACEA64DF1A8ECDE6A0C297605C6BD189C4885D9F08F6EF89284A18ABA0",
        ["geo_micropalo_verticale--irs-punta-inclinato"] = "C68C7FCC72D563007BF311C76ADCBCDB7801E7ED2CFE699D54B00EB84E0C1F05",
        ["geo_micropalo_verticale--regressione-micropalo_igu_0_nessuna-riduzione"] = "66B9F1B7AFE0E676D37B161DFF90794E85BDF0F464CEE3DD9755719EF5847DFC",
    };

    private static int checks;
    private static readonly List<string> Log = [];

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        checks++;
        Log.Add("OK " + message);
    }

    internal static async Task Run(string root, string output)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della prova deve essere vuota.");
        Directory.CreateDirectory(output);
        checks = 0; Log.Clear();
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        try
        {
            await Traces(root, output);
            await FixturesAndExports(root, output);
            await Injection(root);
            await Screenshots(root, Path.Combine(output, "schermate"));
            await Help(root);
            await AutomationNames(root);
            await ReadOnly(root);
            await SharedDialogs(root, output);
            Bench(root);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML del palo e del micropalo verticale (MvvmSheetEditor, fuori schermo)", new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- services and hosting

    /// <summary>The bench choices of the check: the two modules with the MVVM view.</summary>
    private static readonly string[] BenchArguments = [ModuleHostBench.Argument, Pile + "=mvvm", ModuleHostBench.Argument, Micro + "=mvvm"];

    /// <summary>Services of the sheets in the check: errors rethrown, every question answered yes, scripted dialogs and files, recorded Wiki.</summary>
    private sealed class Services : IConfirmationService, IDialogService, IFileDialogService, IWikiNavigator
    {
        internal List<string?> Wiki { get; } = [];
        internal List<string> Questions { get; } = [];
        internal string? SavePath { get; set; }
        internal int? CopyDestination { get; set; }
        internal Func<SoilTransferDialogViewModel, SoilTransferChoice?>? SoilTransfer { get; set; }

        internal PresentationServices Create()
        {
            var bench = ModuleHostBench.Services();
            return new PresentationServices(bench.Messages, this, this, this, bench.Clipboard, bench.Links, this);
        }

        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista: dati condivisi.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista: spostamento.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista: chiusura.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista: salvataggio.");
        public bool ConfirmDelete(string title, string question) { Questions.Add(title + ": " + question); return true; }

        /// <summary>«Copia in…»/«Copia da…»: the stratigraphy of <see cref="CopyDestination"/>, through the commands of the dialog; the soil transfer dialog through <see cref="SoilTransfer"/>.</summary>
        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            if (dialog is SurveyCopyDialogViewModel copy && CopyDestination is int destination)
            {
                copy.Selected = copy.Choices.Single(c => c.Survey == destination);
                copy.ConfirmCommand.Execute(null);
                return Task.FromResult(dialog.Result);
            }
            if (dialog is SoilTransferDialogViewModel transfer && SoilTransfer is { } answer) return Task.FromResult((TResult?)(object?)answer(transfer));
            throw new InvalidOperationException("Finestra non prevista: " + dialog.Title);
        }

        public string? OpenFile(OpenFileRequest request) => throw new InvalidOperationException("Dialogo di apertura non previsto.");
        public string? SaveFile(SaveFileRequest request) => SavePath ?? throw new InvalidOperationException("Dialogo di salvataggio non previsto.");
        public string? ChooseFolder(string? title = null) => throw new InvalidOperationException("Cartella non prevista.");
        public void Show(string? uri = null) => Wiki.Add(uri);
    }

    private static SheetEditorFactory Editors(Services services) =>
        new(ModuleHostBench.Implementations(BenchArguments), DesktopComposition.CreateModules(), services.Create(), new CalculationLimiter(CalculationLimiter.DefaultMaximum));

    /// <summary>A sheet in a window outside the screen, 1400×1000 like the sheet area of the main window (the size of the historic traces).</summary>
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
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Palo verticale " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
        }

        internal FrameworkElement Root => Editor.View;

        internal VerticalPileSheetViewModel Sheet => (VerticalPileSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal VerticalPileSheetView View => Visual(Root).OfType<VerticalPileSheetView>().Single();

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

    private static string? BoundPath(FrameworkElement element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    private static bool Usable(UIElement element) => element.IsEnabled && element.IsVisible;

    /// <summary>A click of the user: the command of the button (and its Click handlers) when the button is usable.</summary>
    private static void Press(Button button)
    {
        if (!Usable(button)) return;
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
        if (button.Command is { } command && command.CanExecute(button.CommandParameter)) command.Execute(button.CommandParameter);
    }

    // ---------------------------------------------------------------- the view, read and driven through its controls

    private sealed class ViewSide(Hosted host, HeldModuleService? engine, Services services)
    {
        private Task latest = Task.CompletedTask, observedPending = Task.CompletedTask;
        private DependencyObject Root => host.Root;
        private VerticalPileSheetViewModel Sheet => host.Sheet;
        private VerticalPileSheetView View => host.View;

        internal void Attach() => latest = observedPending = Sheet.PendingCalculation;

        /// <summary>The last calculation requested: the automatic one of the last edit or the started one, whichever came last.</summary>
        private Task Latest()
        {
            if (!ReferenceEquals(Sheet.PendingCalculation, observedPending)) latest = observedPending = Sheet.PendingCalculation;
            return latest;
        }

        private Task Started(Task task) { Latest(); latest = task; return task; }

        private T Editor<T>(string key) where T : FrameworkElement => Visual(Root).OfType<T>().Single(e => e.DataContext is FieldViewModel f && f.Key == key && e.TemplatedParent is not ComboBox);

        internal TextBox Box(string key) => Editor<TextBox>(key);

        private Button Command(string content) => Visual(Root).OfType<Button>().Single(b => Equals(b.Content, content) && b.IsVisible);

        private DataGrid LayerGrid() => Visual(View.Surveys).OfType<DataGrid>().Single();

        /// <summary>The row of the layer table for the layer <paramref name="index"/>, brought into view.</summary>
        private DataGridRow LayerRow(int index)
        {
            var grid = LayerGrid();
            var item = grid.Items[index];
            grid.ScrollIntoView(item);
            host.Layout();
            return (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(item);
        }

        private Task Select(int survey) { View.Surveys.SelectedIndex = survey; host.Layout(); return Task.CompletedTask; }

        internal async Task Apply(VerticalPileStep step)
        {
            switch (step.Kind)
            {
                case VerticalPileStep.Commit: host.Editor.Commit(); break;
                case VerticalPileStep.Calculate: await Started(host.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(60)); break;
                case VerticalPileStep.Start: _ = Started(host.Editor.CalculateAsync()); break;
                case VerticalPileStep.Wait: await Latest().WaitAsync(TimeSpan.FromSeconds(60)); break;
                case VerticalPileStep.ReadOnly: host.Editor.InspectRevision(); break;
                case VerticalPileStep.Hold: engine!.Hold(); break;
                case VerticalPileStep.Entered: await engine!.EnteredAsync(); break;
                case VerticalPileStep.Release: engine!.Release(); break;
                case VerticalPileStep.Field:
                    if (Box(step.Key!) is var text && Usable(text) && !text.IsReadOnly) text.Text = step.Text;
                    break;
                case VerticalPileStep.Choice:
                    if (Editor<ComboBox>(step.Key!) is var choice && Usable(choice)) choice.SelectedItem = step.Text;
                    break;
                case VerticalPileStep.Option:
                    if (Editor<CheckBox>(step.Key!) is var option && Usable(option)) option.IsChecked = option.IsChecked != true;
                    break;
                case VerticalPileStep.Reset: Press(Command("Reset")); break;
                case VerticalPileStep.Survey: await Select(step.Index); break;
                case VerticalPileStep.AddSurvey: Press(Command("+")); break;
                case VerticalPileStep.RemoveSurvey:
                {
                    var tab = (TabItem)View.Surveys.ItemContainerGenerator.ContainerFromIndex(step.Index);
                    Press(Visual(tab).OfType<Button>().Single(b => Equals(b.Tag, "elimina-stratigrafia")));
                    break;
                }
                case VerticalPileStep.RemoveSelectedSurvey: Press(Command("−")); break;
                case VerticalPileStep.CopySurvey:
                    await Select(step.Index);
                    services.CopyDestination = step.Text == "nuova" ? -1 : int.Parse(step.Text!, CultureInfo.InvariantCulture);
                    Press(Command("Copia in…"));
                    services.CopyDestination = null;
                    break;
                case VerticalPileStep.Layer:
                {
                    await Select(step.Index);
                    var grid = LayerGrid();
                    if (grid.IsReadOnly || !Usable(grid)) break;
                    var row = LayerRow(step.Row);
                    string path = "[" + step.Key + "]";
                    // The cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged), through the control of the column.
                    if (step.Text is null)
                    {
                        var check = Visual(row).OfType<CheckBox>().Single(c => BoundPath(c, ToggleButton.IsCheckedProperty) == path);
                        if (Usable(check)) check.IsChecked = check.IsChecked != true;
                    }
                    else if (Visual(row).OfType<ComboBox>().SingleOrDefault(c => BoundPath(c, Selector.SelectedItemProperty) == path) is { } cell) { if (Usable(cell)) cell.SelectedItem = step.Text; }
                    else
                    {
                        var box = Visual(row).OfType<TextBox>().Single(t => BoundPath(t, TextBox.TextProperty) == path);
                        if (Usable(box) && !box.IsReadOnly) box.Text = step.Text;
                    }
                    break;
                }
                case VerticalPileStep.AddLayer:
                    await Select(step.Index);
                    Press(Visual(View.Surveys).OfType<Button>().Single(b => Equals(b.Content, "Aggiungi strato")));
                    break;
                case VerticalPileStep.RemoveLayer:
                    await Select(step.Index);
                    Press(Visual(LayerRow(step.Row)).OfType<Button>().Single(b => Equals(b.Content, "[−]")));
                    break;
                case VerticalPileStep.CapacityView: if (Usable(View.CapacityView)) View.CapacityView.SelectedIndex = step.Index; break;
                case VerticalPileStep.Curve:
                {
                    var box = Visual(View.CurveChoices).OfType<CheckBox>().SingleOrDefault(c => c.DataContext is VerticalPileCurveChoiceViewModel v && v.Key == step.Key);
                    if (box is not null && Usable(box)) box.IsChecked = box.IsChecked != true;
                    break;
                }
                case VerticalPileStep.AllCurves: Press(Command("Tutte")); break;
                case VerticalPileStep.NoCurves: Press(Command("Nessuna")); break;
                case VerticalPileStep.Table:
                {
                    // The user opens «Tabelle e dettagli», chooses the table, then the chart tab again (where the curves are read).
                    int tab = View.Outputs.SelectedIndex;
                    View.Outputs.SelectedIndex = 1; host.Layout();
                    if (Usable(View.TableChoice)) View.TableChoice.SelectedIndex = step.Index;
                    host.Layout();
                    View.Outputs.SelectedIndex = tab;
                    break;
                }
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            // Layout only: the state is read before the dispatcher runs anything else, as the frozen traces read it (a started calculation
            // as it starts, an automatic one still waiting for its 450 ms).
            host.Layout();
        }

        /// <summary>The state of the sheet read from the controls of the view.</summary>
        internal VerticalPileTraceState State()
        {
            host.Layout();
            var view = View;
            var state = new VerticalPileTraceState
            {
                Data = host.Editor.Data, Modified = host.Modified, Editable = !Box("diametro").IsReadOnly, Exportable = host.Editor.HasResults && !host.Editor.Busy,
                Result = host.Editor.Result, Status = view.StatusText.Text, Warnings = view.WarningsBox.Text,
                General = Form(Sheet.GeneralForm), Efficiency = Form(Sheet.EfficiencyForm), Normative = Form(Sheet.NormativeForm), EfficiencyText = view.EfficiencyText.Text,
                Verification = ((IEnumerable<string[]>?)(Sheet.IsMicro ? view.MicroVerification : view.PileVerification).ItemsSource)?.ToList() ?? [],
                SelectedSurvey = view.Surveys.SelectedIndex, ProfileSurvey = view.ProfileDrawing.SelectedIndex,
                Surveys = view.Surveys.Items.Cast<VerticalPileSurveyViewModel>().Select(s => s.Rows.Select(r => r.Values).ToList()).ToList(),
                CapacityViews = ((IEnumerable)view.CapacityView.ItemsSource).Cast<string>().ToArray(), CapacityView = view.CapacityView.SelectedIndex,
                Capacity = Chart(view.CapacityPlot), Reference = Chart(view.ReferencePlot), EndValues = view.EndValues.Text,
                Curves = Visual(view.CurveChoices).OfType<CheckBox>().Select(c => ((string)c.Content, c.IsChecked == true)).ToList(),
                Tables = ((IEnumerable?)view.TableChoice.ItemsSource)?.Cast<string>().ToList() ?? [], SelectedTable = view.TableChoice.SelectedIndex,
                Raw = view.RawResult.Text,
            };
            state.Shown = ShownTable();
            return state;
        }

        /// <summary>The fields of a form as its FieldForm shows them; a row never shown has no editor yet, its value is the one of its field.</summary>
        private List<VerticalPileFieldState> Form(FormViewModel form) => form.Fields.Select(f =>
        {
            var editor = Visual(Root).OfType<Control>().FirstOrDefault(e => ReferenceEquals(e.DataContext, f) && e is TextBox or ComboBox or CheckBox && e.TemplatedParent is not ComboBox);
            var row = Visual(Root).OfType<Grid>().First(g => g.Name == "Row" && ReferenceEquals(g.DataContext, f));
            bool enabled = row.IsEnabled, dimmed = row.Opacity < 1, visible = row.Visibility == Visibility.Visible;
            string label = editor is null ? f.Label : AutomationProperties.GetName(editor);
            return f.Kind switch
            {
                FieldKind.Text => new VerticalPileFieldState(f.Key, "campo", label, editor is TextBox t ? t.Text : f.Text, null, enabled, dimmed, visible),
                FieldKind.Choice => new VerticalPileFieldState(f.Key, "scelta", label, editor is ComboBox c ? c.SelectedItem as string ?? "" : f.SelectedChoice ?? "",
                    editor is ComboBox items ? ((IEnumerable)items.ItemsSource).Cast<string>().ToArray() : f.Choices, enabled, dimmed, visible),
                _ => new VerticalPileFieldState(f.Key, "opzione", label, VerticalPileTrace.YesNo(editor is CheckBox b ? b.IsChecked == true : f.IsChecked), null, enabled, dimmed, visible),
            };
        }).ToList();

        /// <summary>«Tabelle e dettagli»: the tab is shown for the reading (layout only, nothing else runs), then the chart tab again.</summary>
        private VerticalPileTableState? ShownTable()
        {
            var view = View;
            if (Sheet.ShownTable is null) return null;
            int tab = view.Outputs.SelectedIndex;
            view.Outputs.SelectedIndex = 1;
            host.Layout();
            try
            {
                var grids = view.TableHost.IsVisible ? Visual(view.TableHost).OfType<VerticalPileTableGrid>().Where(g => g.Block is not null).ToList() : [];
                if (grids.Count == 0) return null;
                static (string, IReadOnlyList<string>, IReadOnlyList<string[]>) Table(VerticalPileTableGrid grid, string title) =>
                    (title, grid.Columns.Select(c => ((TextBlock)c.Header).Text).ToArray(), ((IEnumerable)grid.ItemsSource).Cast<string[]>().ToArray());
                var texts = Visual(view.TableHost).OfType<TextBlock>().Where(t => ReferenceEquals(t.DataContext, Sheet.ShownTable)).ToList();
                var state = new VerticalPileTableState();
                if (Sheet.ShownTable?.Note is not null)
                {
                    state.Note = texts.Single(t => BoundPath(t, TextBlock.TextProperty) == "Note").Text;
                    state.Footnote = texts.Single(t => BoundPath(t, TextBlock.TextProperty) == "Footnote").Text;
                    foreach (var grid in grids)
                    {
                        var title = Visual((DependencyObject)VisualTreeHelper.GetParent(grid)).OfType<TextBlock>().First(t => BoundPath(t, TextBlock.TextProperty) == "Title");
                        state.Tables.Add(Table(grid, title.Text));
                    }
                }
                else state.Tables.AddRange(grids.Select(g => Table(g, "")));
                return state;
            }
            finally { view.Outputs.SelectedIndex = tab; host.Layout(); }
        }
    }

    /// <summary>What a Plot shows, as the historic trace reads it (ChartState of VerticalPileHistoricTrace).</summary>
    internal static VerticalPileChartState Chart(Plot plot) => new()
    {
        Title = plot.Title, XLabel = plot.XLabel, YLabel = plot.YLabel, Note = plot.Note, EmptyMessage = plot.EmptyMessage,
        CapacityDepth = plot.CapacityDepth, XMinimum = plot.XMinimum,
        Series = plot.Series.Select(s => new VerticalPileSeriesState(s.Name, s.Color.ToString(), s.Dashed, s.Highlighted,
            s.DashPattern is null ? null : ReferenceEquals(s.DashPattern, DashStyles.Dash) ? "Dash" : ReferenceEquals(s.DashPattern, DashStyles.Dot) ? "Dot" : "?", s.Points)).ToList(),
        Markers = plot.Markers.Select(m => new VerticalPileMarkerState(m.X, m.Y, m.Label, m.Color.ToString(), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius)).ToList(),
    };

    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in VerticalPileScenarios.All(root))
        {
            var trace = new List<string>();
            var services = new Services();
            var presentation = services.Create();
            var engine = new HeldModuleService(scenario.Module);
            var modules = DesktopComposition.CreateModules();
            Hosted Open(JsonObject data) => new(new MvvmSheetEditor(modules.SheetModule(scenario.Module), data, presentation, engine, new CalculationLimiter(CalculationLimiter.DefaultMaximum)));
            var host = Open(VerticalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture));
            try
            {
                var side = new ViewSide(host, engine, services);
                side.Attach();
                trace.AddRange(side.State().Lines("apertura"));
                foreach (var step in scenario.Steps)
                {
                    if (step.Kind == VerticalPileStep.Reopen)
                    {
                        var data = host.Editor.Data;
                        host.Dispose();
                        host = Open(data);
                        side = new ViewSide(host, engine, services);
                        side.Attach();
                    }
                    else await side.Apply(step);
                    trace.AddRange(side.State().Lines(step.ToString()));
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { trace.Add("errore " + error.GetType().Name + ": " + error.Message); Log.Add("   " + scenario.Name + ": " + error); }
            finally { engine.Release(); host.Dispose(); }
            VerticalPileTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(VerticalPileScenarios.Reference(root), scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? VerticalPileTrace.FirstDifference(VerticalPileTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == 18, $"Tracce della vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe" + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    // ---------------------------------------------------------------- 2. visit and exports

    private static async Task FixturesAndExports(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var services = new Services();
        var editors = Editors(services);
        var fixtures = new[] { Pile, Micro }.SelectMany(m => VerticalPileScenarios.Fixtures(root, m).Select(id => (Module: m, Id: id))).ToArray();
        Check(fixtures.Length == 13, "Fixture F3 dei pali verticali: " + fixtures.Length);
        foreach (var (module, id) in fixtures)
        {
            string name = module + "--" + id;
            var data = VerticalPileScenarios.FixtureData(root, module, id);
            using var mvvm = new Hosted(editors.Open(module, data));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: VerticalPileSheetViewModel }, name + ": editor MVVM con la vista XAML");
            await mvvm.Settle();
            string opened = mvvm.Editor.Data.ToJsonString(J.Options);
            // The visit: every tab of every tab control (as ui/f3-equivalence), «Estendi»/«Riduci» of the three cards, the three appearances.
            foreach (var tabs in Visual(mvvm.Root).OfType<TabControl>().ToArray())
            {
                int selected = tabs.SelectedIndex;
                for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; await mvvm.Settle(); }
                tabs.SelectedIndex = selected;
            }
            foreach (string card in new[] { VerticalPileSheetViewModel.SurveysCard, VerticalPileSheetViewModel.ProfileCard, VerticalPileSheetViewModel.ChartsCard })
            {
                mvvm.Sheet.ToggleCardCommand.Execute(card); await mvvm.Settle();
                mvvm.Sheet.ToggleCardCommand.Execute(card); await mvvm.Settle();
            }
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == opened && mvvm.Modified == 0, name + ": la visita (schede, «Estendi», tre aspetti) non scrive e non solleva Modified");
            mvvm.Editor.Commit(); await mvvm.Editor.CalculateAsync(); mvvm.Editor.Commit();
            await mvvm.Settle();
            // The frozen trace of the historic view on the path of the F3 capture: apertura, conferma, calcola, conferma, riapri, conferma, attendi.
            var historic = HistoricBlocks(root, module, id);
            Check(historic.Length >= 4 && historic[0][0] == "== apertura" && historic[2][0] == "== " + VerticalPileStep.Calculate && historic[3][0] == "== " + VerticalPileStep.Commit,
                name + ": traccia congelata della vista storica sul percorso della cattura F3");
            var result = mvvm.Editor.Result;
            bool exportable = Line(historic[3], "modificato").EndsWith(" esportabile sì", StringComparison.Ordinal);
            Check("dati " + VerticalPileTrace.Compact(mvvm.Editor.Data) == Line(historic[3], "dati")
                && "risultato " + (result is null ? "-" : VerticalPileTrace.Hash(result)) == Line(historic[3], "risultato") && mvvm.Editor.HasResults == exportable,
                name + ": dati e risultato dopo il calcolo uguali a quelli della vista storica (traccia congelata)");
            if (!mvvm.Editor.HasResults)
            {
                Check(Fails(() => mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json")), out string message) && message == HistoricNoResult,
                    name + ": senza risultato l'esportazione è rifiutata con il messaggio della vista storica («" + message + "»)");
                continue;
            }
            string title = "Fixture F3 · " + module + " · " + id;
            mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json"));
            // SheetEditor.ExportResult of the historic view wrote the result with J.Options.
            File.WriteAllBytes(Path.Combine(folder, name + "-attesa.json"), Encoding.UTF8.GetBytes(result!.ToJsonString(J.Options)));
            Check(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, name + "-attesa.json"))),
                name + ": JSON del risultato uguale byte per byte a quello della vista storica");
            var sections = ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();
            void Expected() => Anthea.Reports.ReportFiles.Write(Path.Combine(folder, name + "-attesa.docx"), HistoricReport(title, module, mvvm.Editor.Data, result, sections), title);
            mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
            Expected();
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.docx")), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della ricetta della vista storica, figure comprese (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            // The PDF carries the time of the report inside its compressed pages: two exports that straddle a change of the time differ only
            // there. The pair is written again (at most twice) before comparing once more; any other difference stays.
            int attempts = 1;
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))) && attempts < 3)
            {
                attempts++;
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, sections);
                Expected();
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))),
                name + ": PDF affiancato uguale a quello della ricetta della vista storica (date normalizzate" + (attempts > 1 ? $", {attempts} esportazioni" : "") + ")");
            // «Salva PNG…» of the capacity chart: the file chosen in the dialog receives the image of the chart, the same as the historic one.
            string png = Path.Combine(folder, name + "-capacita.png");
            services.SavePath = png;
            var menu = mvvm.View.CapacityHost.ContextMenu!.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Salva PNG…"));
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, menu));
            services.SavePath = null;
            Check(File.Exists(png) && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(png))) == HistoricCapacityPng[name],
                name + ": «Salva PNG…» scrive l'immagine del grafico della capacità della vista storica");
        }
    }

    /// <summary>
    /// The report of the historic view (SheetEditor.BuildReportDocument): ReportWord with the figures of ModuleFigureRenderer.VerticalFigures, the
    /// capacity chart of the first view with the visible curves and the reference chart of the first stratigraphy, as the report of a project
    /// section draws them (ModuleFigureRenderer).
    /// </summary>
    private static byte[] HistoricReport(string title, string module, JsonObject data, JsonObject result, HashSet<string> sections)
    {
        bool micro = module == Micro;
        var capacity = new Plot
        {
            Capacity = true, Title = VerticalPilePresentation.CapacityTitle(micro), YLabel = VerticalPilePresentation.CapacityAxis(micro),
            CapacityDepth = VerticalPilePresentation.AvailableDepth(result), EmptyMessage = VerticalPilePresentation.NoCurveSelected,
            Series = VerticalPileCharts.Capacity(data, result, micro).Where(s => s.Visible && VerticalPileCharts.CapacityIncludes(s.Key, micro, 0)).Select(s => PlotPresentation.Series(s.Series)).ToList()
        };
        var reference = new Plot { InvertY = false };
        PlotPresentation.Apply(reference, VerticalPileCharts.Reference(data, result, micro, 0));
        var images = ModuleFigureRenderer.VerticalFigures(data, result, micro, capacity, reference);
        return Anthea.Reports.DocxWriter.Write(ReportWord.CreateDocument(title, module, data, result, sections, images.Select(f => f.Image()).ToList(), true));
    }

    /// <summary>The frozen trace of the historic view for the F3 fixture <paramref name="id"/> (scenario palo-/micropalo-&lt;id&gt;), split at its steps.</summary>
    private static string[][] HistoricBlocks(string root, string module, string id)
    {
        var blocks = new List<List<string>>();
        string file = (module == Pile ? "palo-" : "micropalo-") + id + ".txt";
        foreach (string line in VerticalPileTrace.Read(Path.Combine(VerticalPileScenarios.Reference(root), file)))
        {
            if (line.StartsWith("== ", StringComparison.Ordinal)) blocks.Add([]);
            if (blocks.Count == 0) throw new InvalidOperationException("Traccia congelata senza intestazione: " + file);
            blocks[^1].Add(line);
        }
        return blocks.Select(b => b.ToArray()).ToArray();
    }

    /// <summary>The line of a step of a trace that starts with <paramref name="name"/>.</summary>
    private static string Line(string[] block, string name) => block.Single(l => l.StartsWith(name + " ", StringComparison.Ordinal));

    private static bool Fails(Action action, out string message)
    {
        try { action(); message = ""; return false; }
        catch (InvalidOperationException error) { message = error.Message; return true; }
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

    // ---------------------------------------------------------------- 3. V3: injection with a result

    /// <summary>F5-01: the historic view fails when the injection of the micropile changes with a result; the new view does not.</summary>
    private static async Task Injection(string root)
    {
        var services = new Services();
        using var host = new Hosted(Editors(services).Open(Micro, VerticalPileScenarios.FixtureData(root, Micro, "regressione-micropalo_igu_0_nessuna-riduzione")));
        await host.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
        await host.Settle();
        Check(host.Editor.HasResults, "F5-01: micropalo calcolato all'apertura");
        var side = new ViewSide(host, null, services);
        await side.Apply(new VerticalPileStep(VerticalPileStep.Choice, "tipo_iniezione", "IRS"));
        var layers = host.Editor.Data.Array("stratigrafie")[0]!.AsArray();
        Check(host.Modified == 1 && !host.Editor.HasResults && layers.All(l => l.S("alpha") == VerticalPilePreview.InitialAlpha(l.S("terreno"), "IRS"))
            && host.Editor.Data["generali"].S("tipo_iniezione") == "IRS",
            "F5-01: cambio dell'iniezione con un risultato attraverso la scelta della vista: α riscritti, risultati azzerati, Modified, nessun errore");
        await host.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
        await host.Settle();
        Check(host.Editor.HasResults && host.View.ReferencePlot.Series.Count > 0, "F5-01: ricalcolo automatico e abaco della nuova iniezione");
    }

    // ---------------------------------------------------------------- 4. screenshots

    private static async Task Screenshots(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        var cases = new[] { ("palo-default", Pile, VerticalPileScenarios.FixtureData(root, Pile, "default")), ("micropalo-default", Micro, VerticalPileScenarios.FixtureData(root, Micro, "default")) };
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            foreach (var (name, module, data) in cases)
            {
                using var host = new Hosted(editors.Open(module, data), mode != AppAppearance.Light);
                await host.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
                await host.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"vista-{name}-{suffix}.png"), host.Png());
                written++;
            }
        }
        // The wide layout (sheet area of 1700 px) and an extended card, in Chiara; the stratigraphies of the micropile extended.
        using (var micro = new Hosted(editors.Open(Micro, VerticalPileScenarios.FixtureData(root, Micro, "default"))))
        {
            await micro.Settle();
            micro.Sheet.ToggleCardCommand.Execute(VerticalPileSheetViewModel.SurveysCard);
            await micro.Settle();
            File.WriteAllBytes(Path.Combine(folder, "vista-micropalo-default-stratigrafie-estese.png"), micro.Png());
            written++;
        }
        // The wide layout (sheet area of 1700 px) and an extended card, in Chiara.
        Appearance.Set(AppAppearance.Light, false);
        using (var wide = new Hosted(editors.Open(Pile, VerticalPileScenarios.FixtureData(root, Pile, "default")), width: 1700, height: 1000))
        {
            await wide.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
            await wide.Settle();
            File.WriteAllBytes(Path.Combine(folder, "vista-palo-default-larga.png"), wide.Png());
            var header = Visual(wide.View.Surveys).OfType<DataGridColumnHeader>().First(h => h.Content is TextBlock);
            Log.Add("   intestazione delle tabelle degli strati (Token.CommandBar): " + header.Background);
            wide.Sheet.ToggleCardCommand.Execute(VerticalPileSheetViewModel.ChartsCard);
            await wide.Settle();
            File.WriteAllBytes(Path.Combine(folder, "vista-palo-default-grafici-estesi.png"), wide.Png());
            written += 2;
        }
        Check(written == 9, "Schermate della vista nei tre aspetti, larga ed estesa: " + written + " PNG in " + folder);
    }

    // ---------------------------------------------------------------- 5. help

    private static async Task Help(string root)
    {
        foreach (string module in new[] { Pile, Micro })
        {
            var services = new Services();
            var data = VerticalPileScenarios.FixtureData(root, module, "default");
            string guide = Anthea.Wiki.WikiCatalog.ForModule(module)?.Id ?? throw new InvalidOperationException("Guida assente nella Wiki: " + module);
            var topic = Anthea.Presentation.Help.HelpTopics.ForField("lunghezza", module) ?? throw new InvalidOperationException("Argomento della lunghezza assente: " + module);
            using var host = new Hosted(Editors(services).Open(module, data));
            await host.Settle();
            static string[] Labels(DependencyObject root) => Visual(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
                .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
            string[] before = HistoricHelpLabels[module], after = Labels(host.Root);
            Check(after.SequenceEqual(before), module + ": etichette «?» visibili uguali a quelle della vista storica (" + before.Length + "): " + string.Join(", ", after));
            var side = new ViewSide(host, null, services);
            var help = Visual(host.Root).OfType<Button>().Single(b => b.IsVisible && AutomationProperties.GetName(b) == "Wiki: " + host.Sheet.GeneralForm["lunghezza"].Label);
            services.Wiki.Clear();
            help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
            F1(side.Box("lunghezza"));
            F1(host.View.Surveys);
            F1(host.View.CapacityView);
            Press(Visual(host.Root).OfType<Button>().Single(b => Equals(b.Content, CalculationGuide.Caption)));
            // CalculationGuide is MainWindow.ShowContextualWikiTheory: the first theory page related to the guide of the module, otherwise the guide.
            var article = Anthea.Wiki.WikiCatalog.ForModule(module)!;
            string theory = article.Related.Select(Anthea.Wiki.WikiCatalog.Resolve).FirstOrDefault(a => a is not null && a.Type == "theory" && a.Modules.Contains(module))?.Id ?? article.Id;
            Check(services.Wiki.SequenceEqual([topic.Uri, topic.Uri, guide, guide, theory]),
                module + ": «?», F1 sul campo, F1 fuori dai campi, «Modello e dati comuni…» dal navigatore della Wiki: " + string.Join(", ", services.Wiki));
            host.Editor.InspectRevision();
            await host.Settle();
            services.Wiki.Clear();
            Check(help.IsEnabled, module + ": «?» utilizzabile in sola lettura");
            help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
            F1(side.Box("lunghezza"));
            Press(Visual(host.Root).OfType<Button>().Single(b => Equals(b.Content, CalculationGuide.Caption)));
            Check(services.Wiki.SequenceEqual([topic.Uri, topic.Uri, theory]), module + ": «?», F1 e «Modello e dati comuni…» anche in sola lettura");
            Check(host.Modified == 0, module + ": l'aiuto non modifica il foglio");
        }
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    // ---------------------------------------------------------------- 6. automation names

    private static async Task AutomationNames(string root)
    {
        static HashSet<string> Names(DependencyObject root) => Visual(root).OfType<UIElement>().Where(e => e.IsVisible)
            .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
        var editors = Editors(new Services());
        foreach (var (module, id) in new[] { (Pile, "default"), (Pile, "battuto-falda-sondaggi"), (Micro, "default"), (Micro, "regressione-micropalo_igu_0_nessuna-riduzione") })
        {
            var data = VerticalPileScenarios.FixtureData(root, module, id);
            using var host = new Hosted(editors.Open(module, data));
            await host.Settle();
            var before = HistoricAutomationNames[(module, id)].ToHashSet(StringComparer.Ordinal); var after = Names(host.Root);
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi nuovi ({module} {id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
            Check(missing.Length == 0, $"{module} {id}: nomi di automazione della vista storica conservati ({before.Count})" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
            // The names of the layer cells read by ui/smoke-display, and the grey γ under an empty γsat.
            string[] used = module == Micro ? ["Spessore verticale [m]", "α adottato", "Aderenza τ [kPa]"] : ["Spessore S [m]", "γsat [kN/m³]"];
            Check(used.All(after.Contains), $"{module} {id}: nomi delle celle usati da ui/smoke-display: " + string.Join(", ", used));
        }
        using (var host = new Hosted(editors.Open(Pile, VerticalPileScenarios.FixtureData(root, Pile, "default"))))
        {
            await host.Settle();
            Check(Visual(host.Root).OfType<TextBlock>().Any(t => Equals(t.Tag, "gamma-sat-automatico")), "Suggerimento di γsat automatico con il Tag della vista storica");
        }
    }

    // ---------------------------------------------------------------- 7. read-only

    /// <summary>The controls that accept the user after InspectRevision, in the new view and as in the historic view on the same data.</summary>
    private static async Task ReadOnly(string root)
    {
        foreach (var (module, id) in new[] { (Pile, "regressione-palo_storico_0"), (Micro, "regressione-micropalo_igu_0_nessuna-riduzione") })
        {
            var services = new Services();
            var data = VerticalPileScenarios.FixtureData(root, module, id);
            using var host = new Hosted(Editors(services).Open(module, data));
            await host.Sheet.PendingCalculation.WaitAsync(TimeSpan.FromSeconds(60));
            host.Editor.InspectRevision();
            await host.Settle();
            var before = HistoricReadOnly[module]; var after = Controls(host.Root);
            var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
            var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
            Log.Add($"   sola lettura ({module}), solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({module}), solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({module}), comuni: " + string.Join(", ", common.Select(k => k + " " + after[k])));
            Check(different.Length == 0 && common.Length >= 20, $"{module}: sola lettura, stessi controlli bloccati e liberi della vista storica ({common.Length} controlli comuni)"
                + (different.Length == 0 ? "" : ": " + string.Join("; ", different)));
            string data0 = host.Editor.Data.ToJsonString(J.Options);
            var side = new ViewSide(host, null, services);
            foreach (var step in new VerticalPileStep[] { new(VerticalPileStep.Field, "lunghezza", "9"), new(VerticalPileStep.AddSurvey), new(VerticalPileStep.AddLayer, Index: 0),
                new(VerticalPileStep.Layer, "spessore", "7", 0, 0), new(VerticalPileStep.Reset) })
                await side.Apply(step);
            Check(host.Editor.Data.ToJsonString(J.Options) == data0 && host.Modified == 0, module + ": sola lettura, digitazione, strati e comandi rifiutati");
            await side.Apply(new VerticalPileStep(VerticalPileStep.CapacityView, Index: 1));
            await side.Apply(new VerticalPileStep(VerticalPileStep.Table, Index: 1));
            Check(host.Editor.HasResults && host.Sheet.SelectedCapacityView == 1 && host.Sheet.SelectedTableIndex == 1, module + ": sola lettura, viste del grafico e tabelle disponibili");
            Check(!ContextMenuService.GetIsEnabled(host.View.CapacityHost), module + ": sola lettura, menu del grafico tolto come nella vista storica");
        }
    }

    /// <summary>The state of the visible input controls, keyed by kind and name (automation name, content or binding).</summary>
    private static Dictionary<string, string> Controls(DependencyObject root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in Visual(root).OfType<Control>().Where(e => e.IsVisible))
        {
            string? key = element switch
            {
                TextBox t when t.TemplatedParent is not Control && AutomationProperties.GetName(t) is { Length: > 0 } name => "testo " + name,
                ComboBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "scelta " + name,
                CheckBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "spunta " + name,
                CheckBox c when c.Content is string text => "spunta " + text,
                Button b when b.Content is string text && text != "?" && b.TemplatedParent is not Control => "pulsante " + text,
                _ => null
            };
            if (key is null) continue;
            string value = element switch
            {
                TextBox t => t.IsReadOnly || !t.IsEnabled ? "bloccato" : "libero",
                _ => element.IsEnabled ? "libero" : "bloccato"
            };
            // The same control repeated (rows, curves): the key keeps the first state, a different one is recorded as «misto».
            map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
        }
        return map;
    }

    // ---------------------------------------------------------------- 8. shared dialogs (T5)

    /// <summary>
    /// SoilTransferDialogViewModel against the historic window on the same data: the window is built and read without being shown (its
    /// private controls), the ViewModel is driven through its properties and commands; then «Invia / carica terreno…» of the pile.
    /// </summary>
    private static async Task SharedDialogs(string root, string output)
    {
        var sources = new List<SoilSource>();
        foreach (var (module, id) in new[] { (Pile, "battuto-falda-sondaggi"), (Pile, "default"), ("geo_stratigrafia", StratigraphyFixture(root)), ("geo_muri_sostegno", "default"), ("geo_palo_orizzontale", "default"), ("geo_fondazioni_superficiali", "default") })
        {
            string file = VerticalPileScenarios.FixturePath(root, module, id);
            if (File.Exists(file)) sources.Add(new SoilSource(module + " · " + id, module, VerticalPileScenarios.FixtureData(root, module, id), "id-" + module));
        }
        Check(sources.Count >= 3, "Fogli terreno di prova per il dialogo condiviso: " + sources.Count);
        int compared = 0;
        foreach (var source in sources)
        {
            foreach (var given in new[] { (IReadOnlyList<SoilSource>)[], sources.Where(s => s != source).ToArray() })
            {
                var historic = new SoilTransferDialog(null!, source.Module, source.Data, given.Select(s => (s.Label, s.Module, s.Data, s.Id)));
                var shared = new SoilTransferDialogViewModel(source.Module, source.Data, given, new Services(), new Services());
                string label = source.Label + (given.Count > 0 ? " con fonti" : "");
                Same(historic, shared, label + ": apertura");
                if (shared.Surveys.Count > 1)
                {
                    Field<ComboBox>(historic, "survey").SelectedIndex = 1; shared.SelectedSurveyIndex = 1;
                    Same(historic, shared, label + ": secondo profilo");
                }
                foreach (var destination in shared.Destinations)
                {
                    historic.Destination.SelectedValue = destination.Module; shared.SelectedDestination = destination.Module;
                    Same(historic, shared, label + ": destinazione " + destination.Module);
                }
                if (given.Count > 0)
                {
                    // The historic window is never shown: its button is clicked when enabled.
                    var import = Buttons(historic).Single(b => Equals(b.Content, "Importa dal foglio selezionato"));
                    if (import.IsEnabled) import.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, import));
                    shared.ImportSourceCommand.Execute(null);
                    Same(historic, shared, label + ": importato dal foglio del progetto");
                }
                foreach (string scenario in shared.Scenarios)
                {
                    Scenario(historic).SelectedItem = scenario; shared.Scenario = scenario;
                    foreach (var destination in shared.Destinations)
                    {
                        historic.Destination.SelectedValue = destination.Module; shared.SelectedDestination = destination.Module;
                        var profile = Profile(historic);
                        var copy = new SoilTransferDialogViewModel(source.Module, source.Data, given, new Services(), new Services());
                        // As the historic window: the second profile of the sheet first, then the import from the project.
                        if (shared.Surveys.Count > 1 && Field<ComboBox>(historic, "survey").SelectedIndex == 1) copy.SelectedSurveyIndex = 1;
                        if (given.Count > 0) copy.ImportSourceCommand.Execute(null);
                        copy.Scenario = scenario; copy.SelectedDestination = destination.Module;
                        string expected = Outcome(() =>
                        {
                            historic.DestinationModule = destination.Module;
                            return "copia " + destination.Module + " «" + historic.OutputName + "» " + Identifiers(VerticalPileTrace.Compact(SoilProfileTransfer.Create(destination.Module, profile!, scenario)));
                        }, profile is null);
                        // Without a profile the button is disabled in both.
                        if (copy.SendCommand.CanExecute(null)) copy.SendCommand.Execute(null);
                        string actual = !copy.SendCommand.CanExecute(null) ? "nessuno" : copy.Result is { } sent ? "copia " + sent.Module + " «" + sent.Name + "» " + Identifiers(VerticalPileTrace.Compact(sent.Data))
                            : copy.StatusIsError ? "errore " + copy.Status : "nessuno · " + copy.Status;
                        if (actual != expected) { Log.Add("   invio, storica: " + expected); Log.Add("   invio, ViewModel: " + actual); }
                        Check(actual == expected, label + $": «Invia a un nuovo foglio» ({scenario}, {destination.Module}) come la finestra storica"
                            + (actual == expected ? "" : ": storica " + expected[..Math.Min(300, expected.Length)] + " · ViewModel " + actual[..Math.Min(300, actual.Length)]));
                        compared++;
                    }
                    var replaced = Profile(historic);
                    var apply = new SoilTransferDialogViewModel(source.Module, source.Data, given, new Services(), new Services());
                    // As the historic window: the second profile of the sheet first, then the import from the project.
                    if (shared.Surveys.Count > 1 && Field<ComboBox>(historic, "survey").SelectedIndex == 1) apply.SelectedSurveyIndex = 1;
                    if (given.Count > 0) apply.ImportSourceCommand.Execute(null);
                    apply.Scenario = scenario;
                    if (apply.ApplyCommand.CanExecute(null)) apply.ApplyCommand.Execute(null);
                    string want = replaced is null ? "nessuno" : Outcome(() => "sostituzione " + Identifiers(VerticalPileTrace.Compact(SoilProfileTransfer.Apply(source.Module,
                        (JsonObject)Field<object>(historic, "original")!, replaced, Field<ComboBox>(historic, "survey").SelectedIndex, scenario))), false);
                    string got = apply.Result is { } done ? "sostituzione " + Identifiers(VerticalPileTrace.Compact(done.Data)) : want.StartsWith("errore", StringComparison.Ordinal) ? "errore " + apply.Status : "nessuno";
                    Check(got == want, label + $": «Sostituisci il terreno del foglio corrente» ({scenario}) come la finestra storica");
                }
                historic.Close();
            }
        }
        // «Salva terreno…»: the file of the historic window (the profile as JSON, UTF-8).
        string saved = Path.Combine(output, "terreno.anthea-terreno.json");
        var services = new Services { SavePath = saved };
        var save = new SoilTransferDialogViewModel(Pile, sources[0].Data, [], services, services);
        save.SaveCommand.Execute(null);
        Check(File.ReadAllText(saved, Encoding.UTF8) == save.Profile!.ToJsonString(J.Options) && save.Status == "Profilo salvato e riutilizzabile negli altri moduli compatibili.",
            "«Salva terreno…» scrive il profilo come la finestra storica");
        // «Invia / carica terreno…» of the pile: commit, dialog with the module, the data and the soil sheets of the project, copy to the window.
        var route = new Services();
        using var host = new Hosted(Editors(route).Open(Pile, VerticalPileScenarios.FixtureData(root, Pile, "battuto-falda-sondaggi")));
        await host.Settle();
        (string Module, JsonObject Data, string Name)? opened = null;
        host.Editor.ModuleCopyRequested += (module, data, name) => opened = (module, data, name);
        host.Editor.SoilSources = () => sources.Select(s => (s.Label, s.Module, s.Data, s.Id));
        SoilTransferDialogViewModel? shown = null;
        route.SoilTransfer = dialog => { shown = dialog; dialog.SelectedDestination = "geo_stratigrafia"; dialog.SendCommand.Execute(null); return dialog.Result; };
        Press(Visual(host.Root).OfType<Button>().Single(b => Equals(b.Content, "Invia / carica terreno…")));
        await host.Settle();
        Check(shown is not null && shown.SourceLabels.Count == sources.Count && opened is { Module: "geo_stratigrafia" } && opened.Value.Name.StartsWith("Terreno da ", StringComparison.Ordinal) && host.Modified == 0,
            "«Invia / carica terreno…»: dialogo condiviso con le fonti del progetto, copia inviata alla finestra (" + opened?.Name + ")");
        using var micro = new Hosted(Editors(new Services()).Open(Micro, VerticalPileScenarios.FixtureData(root, Micro, "default")));
        await micro.Settle();
        Check(!Visual(micro.Root).OfType<Button>().Any(b => b.IsVisible && Equals(b.Content, "Invia / carica terreno…")), "«Invia / carica terreno…» solo per il palo, come la vista storica");
        Log.Add($"   dialogo del terreno: {compared} invii confrontati");
    }

    /// <summary>The identifiers that a new stratigraphy draws at random (32 hexadecimal digits), the same placeholder on both sides.</summary>
    private static string Identifiers(string json) => Regex.Replace(json, "\"[0-9a-f]{32}\"", "\"#\"");

    /// <summary>The first F3 fixture of the stratigraphy, in ordinal order.</summary>
    private static string StratigraphyFixture(string root) => VerticalPileScenarios.Fixtures(root, "geo_stratigrafia")[0];

    private static string Outcome(Func<string> action, bool none)
    {
        if (none) return "nessuno";
        try { return action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or FormatException) { return "errore " + error.Message; }
    }

    /// <summary>The profile chosen in the historic window, null when there is none.</summary>
    private static JsonObject? Profile(SoilTransferDialog historic) => (JsonObject?)typeof(SoilTransferDialog).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(historic);

    private static T Field<T>(object owner, string name) where T : class => (T)(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
        ?? throw new MissingFieldException(owner.GetType().Name, name));

    private static IEnumerable<Button> Buttons(DependencyObject root)
    {
        var pending = new Stack<object>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is Button button) yield return button;
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element).Cast<object>()) pending.Push(child);
        }
    }

    private static ComboBox Scenario(SoilTransferDialog historic)
    {
        var pending = new Stack<object>(); pending.Push(historic);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is ComboBox { ItemsSource: IEnumerable<string> items } combo && items.SequenceEqual(["Medi", "Minimi"])) return combo;
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element).Cast<object>()) pending.Push(child);
        }
        throw new InvalidOperationException("Scelta dei parametri della finestra storica non trovata");
    }

    /// <summary>What the historic window and the ViewModel show: profiles, status, description, preview, destination, guidance, project sheets, buttons.</summary>
    private static void Same(SoilTransferDialog historic, SoilTransferDialogViewModel shared, string what)
    {
        var survey = Field<ComboBox>(historic, "survey");
        var status = Field<TextBlock>(historic, "status");
        var description = Field<TextBlock>(historic, "description");
        var preview = Field<ContentControl>(historic, "preview");
        var rows = preview.Content is DataGrid grid ? ((IEnumerable)grid.ItemsSource).Cast<string[]>().Select(r => string.Join("|", r)).ToArray() : [];
        var headers = preview.Content is DataGrid table ? table.Columns.Select(c => c.Header as string).ToArray() : [];
        var guidance = ((Panel)((FrameworkElement)historic.Destination.Parent).Parent).Children.OfType<UIElement>()
            .SkipWhile(e => !ReferenceEquals(e, historic.Destination.Parent)).Skip(1).OfType<TextBlock>().First();
        var sourceChoice = Buttons(historic).Select(b => b).ToArray();
        string Historic() => string.Join("\n", [
            "profili " + string.Join("|", ((IEnumerable)survey.ItemsSource).Cast<string>()) + " scelto " + survey.SelectedIndex,
            "stato " + status.Text + (Equals(status.Foreground, Appearance.Ink) ? "" : " (errore)"),
            "descrizione " + description.Text,
            "anteprima " + (preview.Content is null ? "-" : string.Join("|", headers) + " " + string.Join(" / ", rows)),
            "destinazione " + historic.Destination.SelectedValue + " voci " + string.Join("|", historic.Destination.Items.Cast<ComboBoxItem>().Select(i => i.Tag + "=" + i.Content)),
            "guida " + guidance.Text,
            "invio " + historic.SendButton.IsEnabled + " importa " + Buttons(historic).Single(b => Equals(b.Content, "Importa dal foglio selezionato")).IsEnabled]);
        string Shared() => string.Join("\n", [
            "profili " + string.Join("|", shared.Surveys) + " scelto " + shared.SelectedSurveyIndex,
            "stato " + shared.Status + (shared.StatusIsError ? " (errore)" : ""),
            "descrizione " + shared.Description,
            "anteprima " + (!shared.HasPreview ? "-" : string.Join("|", shared.PreviewIsStratigraphy ? SoilTransferPresentation.StratigraphyColumns : SoilTransferPresentation.ProfileColumns)
                + " " + string.Join(" / ", shared.PreviewRows.Select(r => string.Join("|", r)))),
            "destinazione " + shared.SelectedDestination + " voci " + string.Join("|", shared.Destinations.Select(d => d.Module + "=" + d.Label)),
            "guida " + shared.Guidance,
            "invio " + shared.SendCommand.CanExecute(null) + " importa " + shared.ImportSourceCommand.CanExecute(null)]);
        string a = Historic(), b = Shared();
        if (a != b) Log.Add("   finestra storica:\n" + a + "\n   ViewModel:\n" + b);
        Check(a == b, "Dialogo del terreno, " + what + ": come la finestra storica");
        _ = sourceChoice;
    }

    // ---------------------------------------------------------------- 9. bench and Release

    private static void Bench(string root)
    {
        var editors = Editors(new Services());
        foreach (string module in new[] { Pile, Micro })
        {
            using (var mvvm = editors.Open(module, VerticalPileScenarios.FixtureData(root, module, "default")))
                Check(mvvm is MvvmSheetEditor { Sheet: VerticalPileSheetViewModel sheet } && sheet.GeneralForm.WikiNavigator is not null, module + ": con --module-impl il banco apre la vista MVVM");
            using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(module, VerticalPileScenarios.FixtureData(root, module, "default")))
                Check(release is SheetEditor && ModuleImplementations.Release.For(module) == SheetImplementation.Historic, module + ": la Release resta storica fino alla prova dell'utente");
        }
    }
}
