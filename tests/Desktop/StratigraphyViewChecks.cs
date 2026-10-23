using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Modules.Stratigraphy;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.Stratigraphy;

namespace X.Desktop;

/// <summary>
/// --check-stratigrafia-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.6b, proposal F5 section 7.3): the XAML view of the stratigraphy
/// (StratigraphySheetView with StratigraphyEditorView) hosted by <see cref="MvvmSheetEditor"/>, as the bench opens it with
/// <c>--module-impl geo_stratigrafia=mvvm</c>, in windows outside the screen (never activated, no mouse). The Release keeps the historic
/// view until the user has tried the new one (decisions of 17/10). Parts:
/// <list type="number">
/// <item>Traces through the view: the 15 scenarios of tests/Shared/StratigraphyTrace.cs driven through the controls of the view (fields and
/// choices of the FieldForm, list of the layers, buttons, the two check boxes clicked as the user clicks them) and through the controls of the
/// shared soil transfer dialog shown by its DataTemplate (decision T5), read back from the controls, equal line by line to the traces of the
/// historic view frozen in tests/fixtures/f5-stratigrafia/storica.</item>
/// <item>For each of the 5 F3 fixtures: the visit (every layer of the list, three appearances) writes nothing and raises no Modified; the
/// commit and the calculation (none) give the data of the frozen trace of the historic view (scenario fixture-&lt;id&gt;); the exports are those
/// the historic view wrote: «Esporta risultati» refused with its message, Word report of StratigraphyReport (dates normalized) and PDF beside
/// it.</item>
/// <item>Screenshots of the view in Chiara, Scuro and Molto scuro, a narrow window, the import dialog and the soil dialog (folder schermate).</item>
/// <item>Help: the «?» labels shown are those of the historic view (none); F1 on a field of every form and outside the fields reaches the
/// IWikiNavigator of the services at every press (the guide of the module), also in read-only; F1 in the preview of a draft does nothing,
/// as in the historic window.</item>
/// <item>Automation names: those of the historic view kept, and the texts and names that Stratigraphy.UiChecks reads.</item>
/// <item>Read-only revision: the controls that accept the user are those of the historic view after InspectRevision.</item>
/// <item>The AI import with a scripted source of the drafts (never the network; a placeholder key that is never written): the import button
/// enabled by the module, the dialog of the view (file, model, key in the PasswordBox, extraction, drafts, preview with the editor of the
/// view, review, application, errors, interruption) and the key cleared at the closing.</item>
/// <item>The shared soil dialog: its view (SoilTransferTemplates) against the historic window SoilTransferDialog on the stratigraphy
/// fixtures, with and without other sheets of the project; the composition root gives the stratigraphy the shared dialog.</item>
/// <item>Bench and Release: with --module-impl the module opens the MVVM view; the Release keeps the historic view.</item>
/// </list>
/// The historic view is not opened any more (f5-pass-geo, Q11): what the checks compared with it is compared with the values it gave, frozen
/// in its traces or recorded here (<see cref="HistoricAutomationNames"/>, <see cref="HistoricReadOnly"/>, <see cref="HistoricNoResult"/>; no
/// «?» label). The shared soil dialog is still compared with the window SoilTransferDialog, which the other historic modules use.
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static class StratigraphyViewChecks
{
    private const string Module = StratigraphyScenarios.Module;

    // ---------------------------------------------------------------- values of the historic view (archived with the Release switch)
    // Recorded by this check on the historic view before its archiving: the capture run on db7ef68d with the values logged
    // (supporto/artefatti/ci/20261023-124229-geo-cattura-3 of the worktree aw-f5-pass-geo), equal to the checks of every run up to the one of
    // the release 1.13.0 (supporto/artefatti/ci/20261023-013037-full-fine-ca-1.13.0 of the main checkout).

    /// <summary>The message of SheetEditor.ExportResult of the stratigraphy, which has no result.</summary>
    private const string HistoricNoResult = "Premere Calcola prima di esportare.";

    /// <summary>The AutomationProperties.Name of the visible elements of the historic view, by fixture.</summary>
    private static readonly Dictionary<string, string[]> HistoricAutomationNames = new(StringComparer.Ordinal)
    {
        ["minimi-entrambi-indefinito"] = ["Angolo di resistenza efficace φ′ [°]", "Angolo di resistenza efficace φ′ [°] · minimo", "Coesione efficace c′ [kPa]", "Coesione efficace c′ [kPa] · minimo", "Colore del profilo [#RRGGBB]", "Comportamento per il trasferimento ai moduli di verifica", "Denominazione", "Descrizione / ubicazione", "Descrizione litologica", "Falda", "Famiglia prevalente · retino automatico", "Fonte dei medi / pagine", "Fonte dei minimi / pagine", "Nome della stratigrafia", "Note (specificare eventuali qualificazioni diverse per parametro)", "Note sui minimi", "Parametri da usare nelle verifiche", "Peso dell’unità di volume saturo γsat [kN/m³]", "Peso dell’unità di volume saturo γsat [kN/m³] · minimo", "Peso naturale γ [kN/m³]", "Peso naturale γ [kN/m³] · minimo", "Profondità falda [m] · positiva verso il basso", "Qualificazione dei medi", "Qualificazione dei minimi", "Quota altimetrica piano campagna [m] · facoltativa", "Resistenza non drenata cu [kPa]", "Resistenza non drenata cu [kPa] · minimo", "Spessore [m]"],
        ["profilo-medi-falda"] = ["Angolo di resistenza efficace φ′ [°]", "Coesione efficace c′ [kPa]", "Colore del profilo [#RRGGBB]", "Comportamento per il trasferimento ai moduli di verifica", "Denominazione", "Descrizione / ubicazione", "Descrizione litologica", "Falda", "Famiglia prevalente · retino automatico", "Fonte dei medi / pagine", "Nome della stratigrafia", "Note (specificare eventuali qualificazioni diverse per parametro)", "Parametri da usare nelle verifiche", "Peso dell’unità di volume saturo γsat [kN/m³]", "Peso naturale γ [kN/m³]", "Profondità falda [m] · positiva verso il basso", "Qualificazione dei medi", "Quota altimetrica piano campagna [m] · facoltativa", "Resistenza non drenata cu [kPa]", "Spessore [m]"],
        ["testi-digitati"] = ["Angolo di resistenza efficace φ′ [°]", "Angolo di resistenza efficace φ′ [°] · minimo", "Coesione efficace c′ [kPa]", "Coesione efficace c′ [kPa] · minimo", "Colore del profilo [#RRGGBB]", "Comportamento per il trasferimento ai moduli di verifica", "Denominazione", "Descrizione / ubicazione", "Descrizione litologica", "Falda", "Famiglia prevalente · retino automatico", "Fonte dei medi / pagine", "Fonte dei minimi / pagine", "Nome della stratigrafia", "Note (specificare eventuali qualificazioni diverse per parametro)", "Note sui minimi", "Parametri da usare nelle verifiche", "Peso dell’unità di volume saturo γsat [kN/m³]", "Peso dell’unità di volume saturo γsat [kN/m³] · minimo", "Peso naturale γ [kN/m³]", "Peso naturale γ [kN/m³] · minimo", "Profondità falda [m] · positiva verso il basso", "Qualificazione dei medi", "Qualificazione dei minimi", "Quota altimetrica piano campagna [m] · facoltativa", "Resistenza non drenata cu [kPa]", "Resistenza non drenata cu [kPa] · minimo", "Spessore [m]"],
    };

    /// <summary>The visible input controls of the historic view after InspectRevision and their state (<see cref="Controls"/>), by fixture and selected layer.</summary>
    private static readonly Dictionary<(string Id, int Layer), Dictionary<string, string>> HistoricReadOnly = new()
    {
        [("minimi-entrambi-indefinito", 0)] = new(StringComparer.Ordinal) { ["elenco degli strati"] = "libero", ["pulsante + Strato"] = "bloccato", ["pulsante Duplica"] = "bloccato", ["pulsante Elimina"] = "bloccato", ["pulsante Importa relazione PDF con AI…"] = "bloccato", ["pulsante Invia / importa terreno da ANTHEA…"] = "bloccato", ["pulsante ↑"] = "bloccato", ["pulsante ↓"] = "bloccato", ["scelta Comportamento per il trasferimento ai moduli di verifica"] = "bloccato", ["scelta Falda"] = "bloccato", ["scelta Famiglia prevalente · retino automatico"] = "bloccato", ["scelta Parametri da usare nelle verifiche"] = "bloccato", ["scelta Qualificazione dei medi"] = "bloccato", ["scelta Qualificazione dei minimi"] = "bloccato", ["spunta Gestisci anche i valori minimi (opzionali)"] = "bloccato", ["spunta Ultimo strato esteso indefinitamente"] = "bloccato", ["testo Angolo di resistenza efficace φ′ [°]"] = "bloccato", ["testo Angolo di resistenza efficace φ′ [°] · minimo"] = "bloccato", ["testo Coesione efficace c′ [kPa]"] = "bloccato", ["testo Coesione efficace c′ [kPa] · minimo"] = "bloccato", ["testo Colore del profilo [#RRGGBB]"] = "bloccato", ["testo Denominazione"] = "bloccato", ["testo Descrizione / ubicazione"] = "bloccato", ["testo Descrizione litologica"] = "bloccato", ["testo Fonte dei medi / pagine"] = "bloccato", ["testo Fonte dei minimi / pagine"] = "bloccato", ["testo Nome della stratigrafia"] = "bloccato", ["testo Note (specificare eventuali qualificazioni diverse per parametro)"] = "bloccato", ["testo Note sui minimi"] = "bloccato", ["testo Peso dell’unità di volume saturo γsat [kN/m³]"] = "bloccato", ["testo Peso dell’unità di volume saturo γsat [kN/m³] · minimo"] = "bloccato", ["testo Peso naturale γ [kN/m³]"] = "bloccato", ["testo Peso naturale γ [kN/m³] · minimo"] = "bloccato", ["testo Profondità falda [m] · positiva verso il basso"] = "bloccato", ["testo Quota altimetrica piano campagna [m] · facoltativa"] = "bloccato", ["testo Resistenza non drenata cu [kPa]"] = "bloccato", ["testo Resistenza non drenata cu [kPa] · minimo"] = "bloccato", ["testo Spessore [m]"] = "bloccato" },
        [("minimi-entrambi-indefinito", 2)] = new(StringComparer.Ordinal) { ["elenco degli strati"] = "libero", ["pulsante + Strato"] = "bloccato", ["pulsante Duplica"] = "bloccato", ["pulsante Elimina"] = "bloccato", ["pulsante Importa relazione PDF con AI…"] = "bloccato", ["pulsante Invia / importa terreno da ANTHEA…"] = "bloccato", ["pulsante ↑"] = "bloccato", ["pulsante ↓"] = "bloccato", ["scelta Comportamento per il trasferimento ai moduli di verifica"] = "bloccato", ["scelta Falda"] = "bloccato", ["scelta Famiglia prevalente · retino automatico"] = "bloccato", ["scelta Parametri da usare nelle verifiche"] = "bloccato", ["scelta Qualificazione dei medi"] = "bloccato", ["spunta Gestisci anche i valori minimi (opzionali)"] = "bloccato", ["spunta Ultimo strato esteso indefinitamente"] = "bloccato", ["testo Angolo di resistenza efficace φ′ [°]"] = "bloccato", ["testo Coesione efficace c′ [kPa]"] = "bloccato", ["testo Colore del profilo [#RRGGBB]"] = "bloccato", ["testo Denominazione"] = "bloccato", ["testo Descrizione / ubicazione"] = "bloccato", ["testo Descrizione litologica"] = "bloccato", ["testo Fonte dei medi / pagine"] = "bloccato", ["testo Nome della stratigrafia"] = "bloccato", ["testo Note (specificare eventuali qualificazioni diverse per parametro)"] = "bloccato", ["testo Peso dell’unità di volume saturo γsat [kN/m³]"] = "bloccato", ["testo Peso naturale γ [kN/m³]"] = "bloccato", ["testo Profondità falda [m] · positiva verso il basso"] = "bloccato", ["testo Quota altimetrica piano campagna [m] · facoltativa"] = "bloccato", ["testo Resistenza non drenata cu [kPa]"] = "bloccato" },
        [("profilo-medi-falda", 0)] = new(StringComparer.Ordinal) { ["elenco degli strati"] = "libero", ["pulsante + Strato"] = "bloccato", ["pulsante Duplica"] = "bloccato", ["pulsante Elimina"] = "bloccato", ["pulsante Importa relazione PDF con AI…"] = "bloccato", ["pulsante Invia / importa terreno da ANTHEA…"] = "bloccato", ["pulsante ↑"] = "bloccato", ["pulsante ↓"] = "bloccato", ["scelta Comportamento per il trasferimento ai moduli di verifica"] = "bloccato", ["scelta Falda"] = "bloccato", ["scelta Famiglia prevalente · retino automatico"] = "bloccato", ["scelta Parametri da usare nelle verifiche"] = "bloccato", ["scelta Qualificazione dei medi"] = "bloccato", ["spunta Gestisci anche i valori minimi (opzionali)"] = "bloccato", ["spunta Ultimo strato esteso indefinitamente"] = "bloccato", ["testo Angolo di resistenza efficace φ′ [°]"] = "bloccato", ["testo Coesione efficace c′ [kPa]"] = "bloccato", ["testo Colore del profilo [#RRGGBB]"] = "bloccato", ["testo Denominazione"] = "bloccato", ["testo Descrizione / ubicazione"] = "bloccato", ["testo Descrizione litologica"] = "bloccato", ["testo Fonte dei medi / pagine"] = "bloccato", ["testo Nome della stratigrafia"] = "bloccato", ["testo Note (specificare eventuali qualificazioni diverse per parametro)"] = "bloccato", ["testo Peso dell’unità di volume saturo γsat [kN/m³]"] = "bloccato", ["testo Peso naturale γ [kN/m³]"] = "bloccato", ["testo Profondità falda [m] · positiva verso il basso"] = "bloccato", ["testo Quota altimetrica piano campagna [m] · facoltativa"] = "bloccato", ["testo Resistenza non drenata cu [kPa]"] = "bloccato", ["testo Spessore [m]"] = "bloccato" },
        [("profilo-medi-falda", 2)] = new(StringComparer.Ordinal) { ["elenco degli strati"] = "libero", ["pulsante + Strato"] = "bloccato", ["pulsante Duplica"] = "bloccato", ["pulsante Elimina"] = "bloccato", ["pulsante Importa relazione PDF con AI…"] = "bloccato", ["pulsante Invia / importa terreno da ANTHEA…"] = "bloccato", ["pulsante ↑"] = "bloccato", ["pulsante ↓"] = "bloccato", ["scelta Comportamento per il trasferimento ai moduli di verifica"] = "bloccato", ["scelta Falda"] = "bloccato", ["scelta Famiglia prevalente · retino automatico"] = "bloccato", ["scelta Parametri da usare nelle verifiche"] = "bloccato", ["scelta Qualificazione dei medi"] = "bloccato", ["spunta Gestisci anche i valori minimi (opzionali)"] = "bloccato", ["spunta Ultimo strato esteso indefinitamente"] = "bloccato", ["testo Angolo di resistenza efficace φ′ [°]"] = "bloccato", ["testo Coesione efficace c′ [kPa]"] = "bloccato", ["testo Colore del profilo [#RRGGBB]"] = "bloccato", ["testo Denominazione"] = "bloccato", ["testo Descrizione / ubicazione"] = "bloccato", ["testo Descrizione litologica"] = "bloccato", ["testo Fonte dei medi / pagine"] = "bloccato", ["testo Nome della stratigrafia"] = "bloccato", ["testo Note (specificare eventuali qualificazioni diverse per parametro)"] = "bloccato", ["testo Peso dell’unità di volume saturo γsat [kN/m³]"] = "bloccato", ["testo Peso naturale γ [kN/m³]"] = "bloccato", ["testo Profondità falda [m] · positiva verso il basso"] = "bloccato", ["testo Quota altimetrica piano campagna [m] · facoltativa"] = "bloccato", ["testo Resistenza non drenata cu [kPa]"] = "bloccato", ["testo Spessore [m]"] = "bloccato" },
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
            await Screenshots(root, Path.Combine(output, "schermate"));
            await Help(root);
            await AutomationNames(root);
            await ReadOnly(root);
            await Import(root, output);
            await SharedDialog(root);
            Bench(root);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML della stratigrafia (MvvmSheetEditor, fuori schermo)", new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- services and hosting

    /// <summary>The bench choice of the check: the stratigraphy with the MVVM view.</summary>
    private static readonly string[] BenchArguments = [ModuleHostBench.Argument, Module + "=mvvm"];

    /// <summary>
    /// Services of the sheets in the check: the messages and the questions recorded in order as the lines of the traces (questions answered
    /// as scripted, the replacement of the soil answered yes as the historic trace answers it), the failure of a command kept for the step,
    /// the dialogs shown off screen through their DataTemplate and driven by <see cref="Dialog"/>, the open dialog scripted, the Wiki recorded.
    /// </summary>
    private sealed class Services : IMessageService, IConfirmationService, IDialogService, IFileDialogService, IWikiNavigator
    {
        private readonly List<string> messages = [];
        internal Queue<bool> Answers { get; } = new();
        internal string? Failure { get; set; }
        internal List<string?> Wiki { get; } = [];
        internal List<object> Shown { get; } = [];
        internal Queue<string?> OpenPaths { get; } = new();
        internal Func<object, Window, Task>? Dialog { get; set; }

        internal PresentationServices Create()
        {
            var bench = ModuleHostBench.Services();
            return new PresentationServices(this, this, this, this, bench.Clipboard, bench.Links, this);
        }

        internal List<string> Take() { var list = messages.ToList(); messages.Clear(); return list; }

        public void ReportFailure(string title, Exception error) => Failure = StratigraphyTrace.Error(error);
        public void Inform(string title, string message) => messages.Add(StratigraphyTrace.Message(title, message));
        public void Warn(string title, string message) => messages.Add(StratigraphyTrace.Message(title, message));

        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista: dati condivisi.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista: spostamento.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista: chiusura.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista: salvataggio.");

        public bool ConfirmDelete(string title, string question)
        {
            bool yes = Answers.Count > 0 && Answers.Dequeue();
            messages.Add(StratigraphyTrace.Question(title, question, yes));
            return yes;
        }

        public bool ConfirmReplace(string title, string question)
        {
            messages.Add(StratigraphyTrace.Question(title, question, true));
            return true;
        }

        /// <summary>The dialog in a window outside the screen with the view of its DataTemplate, driven by <see cref="Dialog"/>; closed when it asks.</summary>
        public async Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            Shown.Add(dialog);
            var drive = Dialog ?? throw new InvalidOperationException("Finestra non prevista: " + dialog.Title);
            var window = new Window
            {
                Title = dialog.Title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize
            };
            Appearance.Watch(window);
            bool closed = false;
            void Close(object? sender, EventArgs e) { closed = true; window.Close(); }
            dialog.CloseRequested += Close;
            window.Show();
            window.UpdateLayout();
            try { await drive(dialog, window); }
            finally
            {
                dialog.CloseRequested -= Close;
                if (!closed) window.Close();
            }
            return closed ? dialog.Result : default;
        }

        public string? OpenFile(OpenFileRequest request) => OpenPaths.Count > 0 ? OpenPaths.Dequeue() : throw new InvalidOperationException("Dialogo di apertura non previsto.");
        public string? SaveFile(SaveFileRequest request) => throw new InvalidOperationException("Dialogo di salvataggio non previsto.");
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
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Stratigrafia " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
        }

        internal FrameworkElement Root => Editor.View;

        internal StratigraphySheetViewModel Sheet => (StratigraphySheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal StratigraphySheetView SheetView => Visual(Root).OfType<StratigraphySheetView>().Single();

        internal StratigraphyEditorView View => Visual(Root).OfType<StratigraphyEditorView>().Single();

        internal void Layout() { window.UpdateLayout(); Root.UpdateLayout(); }

        internal async Task Settle()
        {
            Layout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Layout();
        }

        internal byte[] Png() { Layout(); return StratigraphyViewChecks.Png((FrameworkElement)window.Content); }

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

    private static byte[] Png(FrameworkElement element)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }

    /// <summary>The image of a dialog window shown by its DataTemplate (its root visual).</summary>
    private static byte[] DialogPng(Window window)
    {
        window.UpdateLayout();
        return Png((FrameworkElement)VisualTreeHelper.GetChild(window, 0));
    }

    /// <summary>The text box of a field of the FieldForm in a part of a view, by its label.</summary>
    private static TextBox FieldBox(DependencyObject part, string label) =>
        Visual(part).OfType<TextBox>().Single(b => b.TemplatedParent is not ComboBox && b.DataContext is FieldViewModel && Name(b) == label);

    /// <summary>Usable as the historic trace reads it: enabled and visible.</summary>
    private static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    private static readonly MethodInfo OnClick = typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(ButtonBase), "OnClick");

    /// <summary>
    /// A click of the user on a usable button or check box, without the mouse: ButtonBase.OnClick (a check box changes, then Click and the
    /// command, ToggleButton.OnClick).
    /// </summary>
    private static void Click(ButtonBase button)
    {
        if (!Usable(button)) return;
        try { OnClick.Invoke(button, null); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    private static object Private(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
        ?? throw new MissingFieldException(owner.GetType().Name, name);

    // ---------------------------------------------------------------- labels and keys (as the historic trace reads them)

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

    private const string ImportLabel = "Importa relazione PDF con AI…", TransferLabel = "Invia / importa terreno da ANTHEA…";
    private static readonly string[] ActionLabels = ["+ Strato", "Duplica", "Elimina", "↑", "↓"];

    private static string Name(DependencyObject control) => AutomationProperties.GetName(control);

    /// <summary>The input controls of a part of the view in the visual order: text boxes and choices of the FieldForm, check boxes.</summary>
    private static IEnumerable<Control> Inputs(DependencyObject root, IReadOnlyDictionary<string, string> keys) => Visual(root).OfType<Control>().Where(c =>
        c is TextBox or ComboBox && c.TemplatedParent is not ComboBox && keys.ContainsKey(Name(c)) || c is CheckBox { Content: string text } && keys.ContainsKey(text));

    private static StratigraphyControl ReadControl(Control control, IReadOnlyDictionary<string, string> keys)
    {
        string label = control is CheckBox check ? (string)check.Content : Name(control);
        string key = keys.TryGetValue(label, out var found) ? found : "?" + label;
        return control switch
        {
            TextBox box => new("testo", key, label, box.Text, Usable(box) && !box.IsReadOnly),
            ComboBox combo => new("scelta", key, label, combo.SelectedItem as string ?? "-", Usable(combo), combo.Items.Cast<object>().Select(i => i.ToString()!).ToArray()),
            CheckBox box => new("opzione", key, label, StratigraphyTrace.YesNo(box.IsChecked == true), Usable(box)),
            _ => throw new InvalidOperationException("Controllo inatteso: " + control.GetType().Name)
        };
    }

    // ---------------------------------------------------------------- the view, read and driven through its controls

    /// <summary>The MVVM sheet in a window off screen, driven through the controls of its view as the user does.</summary>
    private sealed class ViewSide : IDisposable
    {
        private readonly SoilSource[] sources;
        private readonly Services services;
        private readonly SheetEditorFactory editors;
        private readonly List<string> transfer = [];
        private Hosted host = null!;
        private int sourcesGiven;
        private (string Module, JsonObject Data, string Name)? copy;
        private JsonObject? replacement;

        internal ViewSide(JsonObject data, SoilSource[] sources)
        {
            this.sources = sources;
            services = new Services();
            editors = Editors(services);
            Open(data);
        }

        private void Open(JsonObject data)
        {
            var editor = editors.Open(Module, data);
            editor.SoilSources = () => { sourcesGiven = sources.Length; return sources.Select(s => (s.Label, s.Module, s.Data, s.Id)); };
            editor.ModuleCopyRequested += (module, output, name) => copy = (module, output, name);
            editor.SoilReplacementRequested += output => replacement = output;
            host = new Hosted(editor);
        }

        internal bool IsMvvm => host.Editor is MvvmSheetEditor { Sheet: StratigraphySheetViewModel };

        internal StratigraphyTraceState State(string? error)
        {
            host.Layout();
            var view = host.View;
            var state = new StratigraphyTraceState { Data = host.Editor.Data, Modified = host.Modified, Error = error };
            state.Messages.AddRange(services.Take());
            var import = view.ImportButton;
            if (import.Visibility == Visibility.Visible)
            {
                state.ImportVisible = true; state.ImportLabel = (string)import.Content; state.ImportEnabled = import.IsEnabled; state.ImportToolTip = import.ToolTip as string ?? "";
            }
            state.General = Inputs(view.GeneralForm, GeneralKeys).Select(c => ReadControl(c, GeneralKeys)).ToList();
            state.Actions = ActionLabels.Select(label => Visual(view.Actions).OfType<Button>().Single(b => Equals(b.Content, label))).Select(b => ((string)b.Content, Usable(b))).ToList();
            var list = view.LayerList;
            state.Layers = Enumerable.Range(0, list.Items.Count).Select(i => list.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item
                ? Visual(item).OfType<TextBlock>().First().Text : throw new InvalidOperationException("Riga dell'elenco degli strati non generata: " + i)).ToList();
            state.Selected = list.SelectedIndex;
            if (view.LayerFields.Visibility == Visibility.Visible)
            {
                state.Layer.AddRange(Inputs(view.LayerFields, LayerKeys).Select(c => ReadControl(c, LayerKeys)));
                if (view.MinimaNote.Visibility == Visibility.Visible) state.MinimaNote = view.MinimaNote.Text;
            }
            state.Status = view.StatusText.Text;
            state.Profile = view.Profile.Data is not null; state.ProfileSelection = view.Profile.Selection;
            var button = host.SheetView.TransferButton;
            state.TransferLabel = (string)button.Content; state.TransferUsable = button.IsEnabled;
            state.Transfer.AddRange(transfer);
            return state;
        }

        private TextBox? Box(DependencyObject part, string key, IReadOnlyDictionary<string, string> keys) =>
            Inputs(part, keys).OfType<TextBox>().SingleOrDefault(b => keys[Name(b)] == key);

        private ComboBox? Combo(DependencyObject part, string key, IReadOnlyDictionary<string, string> keys) =>
            Inputs(part, keys).OfType<ComboBox>().SingleOrDefault(b => keys[Name(b)] == key);

        internal async Task<string?> Apply(StratigraphyStep step)
        {
            services.Failure = null; services.Answers.Clear(); transfer.Clear();
            var view = host.View;
            static void Type(TextBox? box, string text) { if (box is not null && Usable(box) && !box.IsReadOnly) box.Text = text; }
            static void Choose(ComboBox? combo, string text) { if (combo is not null && Usable(combo)) combo.SelectedItem = text; }
            try
            {
                switch (step.Kind)
                {
                    case StratigraphyStep.Commit: host.Editor.Commit(); break;
                    case StratigraphyStep.Calculate: await host.Editor.CalculateAsync(); break;
                    case StratigraphyStep.ReadOnly: host.Editor.InspectRevision(); break;
                    case StratigraphyStep.Reopen:
                        var data = host.Editor.Data;
                        host.Dispose();
                        Open(data);
                        break;
                    case StratigraphyStep.Field: Type(Box(view.GeneralForm, step.Key!, GeneralKeys), step.Text!); break;
                    case StratigraphyStep.Choice: Choose(Combo(view.GeneralForm, step.Key!, GeneralKeys), step.Text!); break;
                    case StratigraphyStep.Select: view.LayerList.SelectedIndex = step.Index; break;
                    case StratigraphyStep.LayerField: Type(Box(view.LayerFields, step.Key!, LayerKeys), step.Text!); break;
                    case StratigraphyStep.LayerChoice: Choose(Combo(view.LayerFields, step.Key!, LayerKeys), step.Text!); break;
                    case StratigraphyStep.MinimumField: Type(Box(view.LayerFields, "minimum." + step.Key, LayerKeys), step.Text!); break;
                    case StratigraphyStep.MinimumChoice: Choose(Combo(view.LayerFields, "minimum." + step.Key, LayerKeys), step.Text!); break;
                    case StratigraphyStep.Infinite: Click(view.InfiniteOption); break;
                    case StratigraphyStep.Minima:
                        if (step.Text is not null) services.Answers.Enqueue(step.Text == "sì");
                        Click(view.MinimaOption);
                        break;
                    case StratigraphyStep.Add: Click(Action(view, "+ Strato")); break;
                    case StratigraphyStep.Duplicate: Click(Action(view, "Duplica")); break;
                    case StratigraphyStep.Remove: Click(Action(view, "Elimina")); break;
                    case StratigraphyStep.Up: Click(Action(view, "↑")); break;
                    case StratigraphyStep.Down: Click(Action(view, "↓")); break;
                    case StratigraphyStep.Import: Click(view.ImportButton); break;
                    case StratigraphyStep.Send or StratigraphyStep.Replace: await Transfer(step); break;
                    default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
                }
                return services.Failure;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return StratigraphyTrace.Error(error); }
            finally { host.Layout(); }
        }

        private static Button Action(StratigraphyEditorView view, string label) => Visual(view.Actions).OfType<Button>().Single(b => Equals(b.Content, label));

        /// <summary>
        /// «Invia / importa terreno da ANTHEA…»: the shared dialog shown by its DataTemplate, driven through its controls as the historic trace
        /// drives the window: «Importa dal foglio selezionato» (profile «progetto»), the parameters, then «Invia a un nuovo foglio» with the
        /// destination or «Sostituisci il terreno del foglio corrente» (the question answered yes); an error stays in the status and the dialog
        /// is closed without a choice.
        /// </summary>
        private async Task Transfer(StratigraphyStep step)
        {
            var button = host.SheetView.TransferButton;
            if (!Usable(button)) return;
            copy = null; replacement = null; sourcesGiven = -1;
            bool opened = false, sheetData = false, closed = false;
            string module = "", status = "";
            services.Dialog = (shown, window) =>
            {
                var dialog = (SoilTransferDialogViewModel)shown;
                opened = true;
                dialog.CloseRequested += (_, _) => closed = true;
                module = (string)Private(dialog, "module");
                sheetData = JsonNode.DeepEquals((JsonObject)Private(dialog, "original"), host.Editor.Data);
                if (step.Kind == StratigraphyStep.Replace && step.Key == "progetto") Click(DialogButton(window, "Importa dal foglio selezionato"));
                window.UpdateLayout();
                DialogCombo(window, "Scenario").SelectedItem = step.Text;
                if (step.Kind == StratigraphyStep.Send)
                {
                    DialogCombo(window, "Destination").SelectedValue = step.Key;
                    window.UpdateLayout();
                    Click(DialogButton(window, "Invia a un nuovo foglio"));
                }
                else Click(DialogButton(window, "Sostituisci il terreno del foglio corrente"));
                if (!closed) { window.UpdateLayout(); status = DialogText(window, "Status").Text; }
                return Task.CompletedTask;
            };
            try
            {
                Click(button);
                if (button.Command is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand { ExecutionTask: { } running }) await running.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally { services.Dialog = null; }
            if (!opened)
            {
                // The commit of an invalid document fails before the dialog (the error of the step, S4); otherwise the dialog must open.
                if (services.Failure is null) throw new InvalidOperationException("Finestra del trasferimento del terreno non aperta.");
                return;
            }
            transfer.AddRange(StratigraphyTrace.Transfer(module, sheetData, sourcesGiven, copy?.Module, copy?.Name, copy?.Data ?? replacement, replacement is not null, status));
        }

        public void Dispose() => host.Dispose();
    }

    /// <summary>A button of a dialog shown by its DataTemplate, by its text.</summary>
    private static Button DialogButton(DependencyObject window, string text) => Visual(window).OfType<Button>().Single(b => Equals(b.Content, text));

    /// <summary>A choice of a dialog by the name of the template.</summary>
    private static ComboBox DialogCombo(DependencyObject window, string name) => Visual(window).OfType<ComboBox>().Single(c => c.Name == name);

    /// <summary>A text of a dialog by the name of the template.</summary>
    private static TextBlock DialogText(DependencyObject window, string name) => Visual(window).OfType<TextBlock>().Single(t => t.Name == name);

    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        bool mvvm = true;
        foreach (var scenario in StratigraphyScenarios.All(root))
        {
            var trace = new List<string>();
            var data = StratigraphyScenarios.FixtureData(root, scenario.Fixture);
            SoilSource[] sources = scenario.Source is null ? []
                : [new(scenario.SourceLabel, Module, StratigraphyScenarios.FixtureData(root, scenario.Source), StratigraphyScenarios.SourceId)];
            var guids = new StratigraphyGuids([data, .. sources.Select(s => s.Data)]);
            var side = new ViewSide(data, sources);
            try
            {
                mvvm &= side.IsMvvm;
                trace.AddRange(side.State(null).Lines("apertura", guids));
                foreach (var step in scenario.Steps)
                {
                    string? error = await side.Apply(step);
                    trace.AddRange(side.State(error).Lines(step.ToString(), guids));
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { trace.Add("errore della prova " + error.GetType().Name + ": " + error.Message); Log.Add("   " + scenario.Name + ": " + error); }
            finally { side.Dispose(); }
            StratigraphyTrace.Write(Path.Combine(folder, scenario.File), trace);
            string stored = Path.Combine(StratigraphyScenarios.Reference(root), scenario.File);
            string? difference = File.Exists(stored) ? StratigraphyTrace.FirstDifference(StratigraphyTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.File + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(mvvm, "Tracce sul foglio MVVM con la vista XAML (banco --module-impl)");
        Check(differences.Count == 0 && scenarios == 15, $"Tracce della vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe" + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    // ---------------------------------------------------------------- 2. visit and exports

    private static async Task FixturesAndExports(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var editors = Editors(new Services());
        var fixtures = StratigraphyScenarios.Fixtures(root);
        Check(fixtures.Count == 5, "Fixture F3 della stratigrafia: " + fixtures.Count);
        foreach (string id in fixtures)
        {
            var data = StratigraphyScenarios.FixtureData(root, id);
            using var mvvm = new Hosted(editors.Open(Module, data));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: StratigraphySheetViewModel }, id + ": editor MVVM con la vista XAML");
            await mvvm.Settle();
            string opened = mvvm.Editor.Data.ToJsonString(J.Options);
            // The visit: every layer of the list, the first again, the three appearances.
            var list = mvvm.View.LayerList;
            for (int i = 0; i < list.Items.Count; i++) { list.SelectedIndex = i; await mvvm.Settle(); }
            list.SelectedIndex = 0; await mvvm.Settle();
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == opened && mvvm.Modified == 0, id + ": la visita (strati, tre aspetti) non scrive e non solleva Modified");
            mvvm.Editor.Commit(); await mvvm.Editor.CalculateAsync(); mvvm.Editor.Commit();
            // The frozen trace of the historic view on the path of the F3 capture: apertura, conferma, calcola, conferma, riapri, conferma.
            var historic = HistoricBlocks(root, id);
            Check(historic.Length >= 4 && historic[0][0] == "== apertura" && historic[3][0] == "== conferma",
                id + ": traccia congelata della vista storica sul percorso della cattura F3");
            Check("dati " + StratigraphyTrace.Compact(mvvm.Editor.Data) == historic[3].Single(l => l.StartsWith("dati ", StringComparison.Ordinal))
                && !mvvm.Editor.HasResults && mvvm.Editor.Result is null, id + ": dati dopo «Calcola» uguali a quelli della vista storica (traccia congelata), nessun risultato");
            Check(Fails(() => mvvm.Editor.ExportResult(Path.Combine(folder, id + "-mvvm.json")), out string message) && message == HistoricNoResult,
                id + ": «Esporta risultati» rifiutato con il messaggio della vista storica («" + message + "»)");
            string title = "Fixture F3 · " + Module + " · " + id;
            var sections = ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();
            // SheetEditor.ExportReport of the historic view: commit, then the Word of StratigraphyReport and the PDF beside it (ReportFiles).
            void Expected() => Anthea.Reports.ReportFiles.Write(Path.Combine(folder, id + "-attesa.docx"),
                Anthea.Reports.DocxWriter.Write(Anthea.Reports.StratigraphyReport.Create(title, (JsonObject)mvvm.Editor.Data.DeepClone())), title);
            mvvm.Editor.ExportReport(Path.Combine(folder, id + "-mvvm.docx"), title, sections);
            Expected();
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, id + "-attesa.docx")), normalizer);
            Check(part is null, id + ": report Word uguale in ogni parte a quello della vista storica (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            // The PDF carries the time of the report inside its compressed pages: a pair that straddles a change of the time is written again.
            int attempts = 1;
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, id + "-attesa.pdf"))) && attempts < 3)
            {
                attempts++;
                mvvm.Editor.ExportReport(Path.Combine(folder, id + "-mvvm.docx"), title, sections);
                Expected();
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, id + "-attesa.pdf"))),
                id + ": PDF affiancato uguale a quello della vista storica (date normalizzate" + (attempts > 1 ? $", {attempts} esportazioni" : "") + ")");
            Check(mvvm.Modified == 0, id + ": conferma, «Calcola» ed esportazioni senza Modified");
        }
    }

    /// <summary>The frozen trace of the historic view for the F3 fixture <paramref name="id"/> (scenario fixture-&lt;id&gt;), split at its steps.</summary>
    private static string[][] HistoricBlocks(string root, string id)
    {
        var blocks = new List<List<string>>();
        string file = "fixture-" + id + ".txt";
        foreach (string line in StratigraphyTrace.Read(Path.Combine(StratigraphyScenarios.Reference(root), file)))
        {
            if (line.StartsWith("== ", StringComparison.Ordinal)) blocks.Add([]);
            if (blocks.Count == 0) throw new InvalidOperationException("Traccia congelata senza intestazione: " + file);
            blocks[^1].Add(line);
        }
        return blocks.Select(b => b.ToArray()).ToArray();
    }

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

    // ---------------------------------------------------------------- 3. screenshots

    private static async Task Screenshots(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            foreach (string id in new[] { "minimi-entrambi-indefinito", "profilo-medi-falda" })
            {
                using var host = new Hosted(editors.Open(Module, StratigraphyScenarios.FixtureData(root, id)), mode != AppAppearance.Light);
                await host.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"vista-{id}-{suffix}.png"), host.Png());
                written++;
            }
        }
        Appearance.Set(AppAppearance.Light, false);
        // The last layer, of indefinite extent, selected; a narrow sheet area (the minimum width of the two columns).
        using (var host = new Hosted(editors.Open(Module, StratigraphyScenarios.FixtureData(root, "minimi-entrambi-indefinito"))))
        {
            await host.Settle();
            host.View.LayerList.SelectedIndex = 2;
            await host.Settle();
            File.WriteAllBytes(Path.Combine(folder, "vista-minimi-entrambi-indefinito-ultimo-strato.png"), host.Png());
            written++;
        }
        using (var narrow = new Hosted(editors.Open(Module, StratigraphyScenarios.FixtureData(root, "profilo-medi-falda")), width: 700, height: 900))
        {
            await narrow.Settle();
            File.WriteAllBytes(Path.Combine(folder, "vista-profilo-medi-falda-stretta.png"), narrow.Png());
            // WPF layout rounding at 125% maps 730 DIP to 729.6 DIP (half a physical pixel).
            double halfPixel = .5 / VisualTreeHelper.GetDpi(narrow.View).DpiScaleX;
            Check(narrow.View.Columns.Width >= 730 && narrow.View.Columns.ActualWidth >= 730 - halfPixel - 1e-7
                && narrow.View.Viewport.ExtentWidth > narrow.View.Viewport.ViewportWidth,
                "Area stretta: minimo 730 DIP; width=" + narrow.View.Columns.Width.ToString("R", CultureInfo.InvariantCulture) + "; actual=" + narrow.View.Columns.ActualWidth.ToString("R", CultureInfo.InvariantCulture) + "; extent=" + narrow.View.Viewport.ExtentWidth.ToString("R", CultureInfo.InvariantCulture) + "; viewport=" + narrow.View.Viewport.ViewportWidth.ToString("R", CultureInfo.InvariantCulture) + "; dpi=" + VisualTreeHelper.GetDpi(narrow.View).DpiScaleX.ToString("R", CultureInfo.InvariantCulture));
            written++;
        }
        using (var wide = new Hosted(editors.Open(Module, StratigraphyScenarios.FixtureData(root, "profilo-medi-falda")), width: 1700, height: 1000))
        {
            await wide.Settle();
            double expected = wide.View.Viewport.ActualWidth - 32;
            Check(Math.Abs(wide.View.Columns.ActualWidth - expected) < 1 && wide.View.Viewport.ExtentWidth <= wide.View.Viewport.ViewportWidth + 1,
                "Area larga: le due colonne occupano la larghezza meno i margini, senza scorrimento (" + wide.View.Columns.ActualWidth.ToString("0", CultureInfo.InvariantCulture) + ")");
        }
        Check(written == 8, "Schermate della vista nei tre aspetti, con l'ultimo strato e stretta: " + written + " PNG in " + folder);
        Shots = folder;
    }

    /// <summary>The folder of the screenshots, for the dialogs (import and soil).</summary>
    private static string? Shots;

    // ---------------------------------------------------------------- 4. help

    private static async Task Help(string root)
    {
        var services = new Services();
        var data = StratigraphyScenarios.FixtureData(root, "minimi-entrambi-indefinito");
        string guide = Anthea.Wiki.WikiCatalog.ForModule(Module)?.Id ?? throw new InvalidOperationException("Guida assente nella Wiki: " + Module);
        using var host = new Hosted(Editors(services).Open(Module, data));
        await host.Settle();
        static string[] Labels(DependencyObject root) => Visual(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
            .Select(b => Name(b)).Order(StringComparer.Ordinal).ToArray();
        string[] after = Labels(host.Root);
        Check(after.Length == 0, "Etichette «?» visibili uguali a quelle della vista storica: nessuna");
        var view = host.View;
        // F1 on the first editor of every form of the layer with minima, then outside the fields: every press reaches the navigator of the services.
        var targets = new List<UIElement>();
        foreach (var part in new FrameworkElement[] { view.GeneralForm, view.IdentityForm, view.DescriptionForm, view.ThicknessForm, view.MeansForm, view.MinimaForm, view.NotesForm })
            targets.Add(Visual(part).OfType<Control>().First(c => c is TextBox or ComboBox && c.TemplatedParent is not ComboBox && c.DataContext is FieldViewModel));
        targets.Add(view.LayerList); targets.Add(view.InfiniteOption); targets.Add(host.SheetView.TransferButton); targets.Add(view.StatusText);
        services.Wiki.Clear();
        foreach (var target in targets) F1(target);
        Check(services.Wiki.Count == targets.Count && services.Wiki.All(u => u == guide),
            $"F1 su un campo di ognuno dei 7 form e fuori dai campi: {services.Wiki.Count} pressioni su {targets.Count} arrivano al navigatore della Wiki dei servizi, con la guida del modulo");
        var forms = host.Sheet.Editor.Forms.ToArray();
        Check(forms.Length == 7 && forms.All(f => f.WikiNavigator is not null && f.InheritedWikiModule == Module), "Form della vista con il navigatore della Wiki e il modulo ereditato dalla vista");
        host.Editor.InspectRevision();
        await host.Settle();
        services.Wiki.Clear();
        foreach (var target in targets) F1(target);
        Check(services.Wiki.Count == targets.Count && services.Wiki.All(u => u == guide), "F1 anche in sola lettura: " + services.Wiki.Count + " pressioni su " + targets.Count);
        Check(host.Modified == 0, "L'aiuto non modifica il foglio");
    }

    // ---------------------------------------------------------------- 5. automation names

    private static async Task AutomationNames(string root)
    {
        static HashSet<string> Names(DependencyObject root) => Visual(root).OfType<UIElement>().Where(e => e.IsVisible)
            .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
        var editors = Editors(new Services());
        foreach (string id in new[] { "minimi-entrambi-indefinito", "profilo-medi-falda", "testi-digitati" })
        {
            var data = StratigraphyScenarios.FixtureData(root, id);
            using var host = new Hosted(editors.Open(Module, data));
            await host.Settle();
            var before = HistoricAutomationNames[id].ToHashSet(StringComparer.Ordinal); var after = Names(host.Root);
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
            Check(missing.Length == 0, $"{id}: nomi di automazione della vista storica conservati ({before.Count})" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
        }
        // What tests/ModelViewer.UiChecks/StratigraphyRenderingChecks.cs (Stratigraphy.UiChecks) reads of the sheet.
        var sample = StratigraphyScenarios.FixtureData(root, "minimi-entrambi-indefinito");
        using var sheet = new Hosted(editors.Open(Module, sample));
        sheet.View.LayerList.SelectedIndex = 2;
        await sheet.Settle();
        var nodes = Visual(sheet.Root).ToList();
        Check(nodes.OfType<TextBlock>().Any(t => t.Text == "Stratigrafia del terreno") && nodes.OfType<TextBlock>().Any(t => t.Text == "Valori minimi · opzionali")
            && nodes.OfType<ListBox>().Single().Items.Count == 3 && nodes.OfType<ComboBox>().Any(c => c.SelectedItem as string == "Entrambi")
            && nodes.OfType<CheckBox>().Single(c => c.Content as string == "Ultimo strato esteso indefinitamente").IsChecked == true
            && nodes.OfType<TextBox>().Count(b => Name(b) == "Nome della stratigrafia") == 1,
            "Testi, elenco, scelta, casella e nome letti da Stratigraphy.UiChecks presenti nella vista");
    }

    // ---------------------------------------------------------------- 6. read-only

    /// <summary>The controls that accept the user after InspectRevision, in the new view and as in the historic view on the same data.</summary>
    private static async Task ReadOnly(string root)
    {
        foreach (string id in new[] { "minimi-entrambi-indefinito", "profilo-medi-falda" })
        {
            var data = StratigraphyScenarios.FixtureData(root, id);
            using var host = new Hosted(Editors(new Services()).Open(Module, data));
            await host.Settle();
            host.Editor.InspectRevision();
            // The first layer, then another one selected in the revision (the selection stays allowed; the new fields are protected).
            foreach (int layer in new[] { 0, 2 })
            {
                host.View.LayerList.SelectedIndex = layer;
                await host.Settle();
                var before = HistoricReadOnly[(id, layer)]; var after = Controls(host.Root);
                var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
                var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
                Log.Add($"   sola lettura ({id}, strato {layer}), solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
                Log.Add($"   sola lettura ({id}, strato {layer}), solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
                Log.Add($"   sola lettura ({id}, strato {layer}), comuni: " + string.Join(", ", common.Select(k => k + " " + after[k])));
                Check(different.Length == 0 && common.Length >= 20 && host.View.LayerList.SelectedIndex == layer,
                    $"{id}, strato {layer}: sola lettura, stessi controlli bloccati e liberi della vista storica ({common.Length} controlli comuni)" + (different.Length == 0 ? "" : ": " + string.Join("; ", different)));
            }
            // The historic view kept the data it opened (recorded in the capture run): the new one too.
            Check(host.Modified == 0 && StratigraphyTrace.Compact(host.Editor.Data) == StratigraphyTrace.Compact(data), id + ": sola lettura senza scritture");
        }
    }

    /// <summary>The state of the visible input controls, keyed by kind and name (automation name or content).</summary>
    private static Dictionary<string, string> Controls(DependencyObject root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in Visual(root).OfType<Control>().Where(e => e.IsVisible))
        {
            string? key = element switch
            {
                TextBox t when t.TemplatedParent is not Control && Name(t) is { Length: > 0 } name => "testo " + name,
                ComboBox c when Name(c) is { Length: > 0 } name => "scelta " + name,
                CheckBox c when c.Content is string text => "spunta " + text,
                Button b when b.Content is string text && text != "?" && b.TemplatedParent is not Control => "pulsante " + text,
                ListBox => "elenco degli strati",
                _ => null
            };
            if (key is null) continue;
            string value = element switch
            {
                TextBox t => t.IsReadOnly || !t.IsEnabled ? "bloccato" : "libero",
                _ => element.IsEnabled ? "libero" : "bloccato"
            };
            map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
        }
        return map;
    }

    // ---------------------------------------------------------------- 7. AI import

    /// <summary>
    /// The import enabled by the module (only here: the Release keeps it disabled, S5) with a scripted source of the drafts: never the network;
    /// the proposed key is a placeholder, compared and never written. The dialog of the view is driven through its controls.
    /// </summary>
    private static async Task Import(string root, string output)
    {
        const string model = "modello-di-prova", key = "chiave-di-prova-non-valida", typed = "altra-chiave-di-prova-non-valida";
        string pdf = Path.Combine(output, "relazione-di-prova.pdf");
        File.WriteAllText(pdf, "%PDF-1.4\nsynthetic transport fixture");
        var source = new ScriptedDrafts();
        var services = new Services();
        var module = new StratigraphySheetModule(new(source, () => new StratigraphyImportDefaults(model, key), ImportEnabled: true));
        Check(Application.Current.TryFindResource(new DataTemplateKey(typeof(StratigraphyImportDialogViewModel))) is DataTemplate
            && Application.Current.TryFindResource(new DataTemplateKey(typeof(SoilTransferDialogViewModel))) is DataTemplate,
            "DataTemplate del dialogo dell'importazione e del dialogo condiviso del terreno nelle risorse dell'applicazione (WpfDialogService)");
        using var host = new Hosted(new MvvmSheetEditor(module, StratigraphyScenarios.FixtureData(root, "profilo-medi-falda"), services.Create(), ModuleServices.Get(Module),
            new CalculationLimiter(CalculationLimiter.DefaultMaximum)));
        await host.Settle();
        var button = host.View.ImportButton;
        Check(button.IsVisible && button.IsEnabled && button.ToolTip is null, "Importazione attivata dal modulo: pulsante attivo senza il suggerimento della Release");
        // Success: file, extraction, preview, review, a change of the draft, application.
        var draft = Draft("S1 · bozza", "Controllare le unità");
        source.Next = _ => Task.FromResult<IReadOnlyList<JsonObject>>([draft, Draft("S2", "")]);
        services.OpenPaths.Enqueue(pdf);
        StratigraphyImportDialogView? dialogView = null;
        services.Dialog = async (shown, window) =>
        {
            var dialog = (StratigraphyImportDialogViewModel)shown;
            var view = dialogView = Visual(window).OfType<StratigraphyImportDialogView>().Single();
            Check(window.Title == StratigraphyImport.Title && view.MessageText.Text == StratigraphyImport.Ready && view.ModelBox.Text == model && view.KeyBox.Password == key
                && !DialogButton(window, "Sostituisci la stratigrafia con il profilo revisionato").IsEnabled && !DialogButton(window, "Interrompi").IsEnabled,
                "Finestra dell'importazione all'apertura: titolo, messaggio, modello e chiave proposti nei controlli, applicazione e interruzione spente");
            Click(DialogButton(window, "Scegli PDF…"));
            view.KeyBox.Password = typed;
            Click(DialogButton(window, "Invia PDF a OpenAI e prepara bozze"));
            await Settle(window);
            Check(view.FileBox.Text == pdf && source.Calls.Single() == (pdf, typed, model) && dialog.ApiKey == typed,
                "Estrazione con il file scelto, il modello e la chiave digitata nel PasswordBox (sorgente scritta dalla prova, mai la rete)");
            var editor = Visual(view.PreviewHost).OfType<StratigraphyEditorView>().Single();
            Check(view.ProfileChoice.Items.Cast<string>().SequenceEqual(["1 · S1 · bozza", "2 · S2"]) && view.ProfileChoice.SelectedIndex == 0
                && view.MessageText.Text == "Bozza AI da controllare · Controllare le unità" && editor.ImportButton.Visibility != Visibility.Visible
                && editor.LayerList.Items.Count == StratigraphyDocument.Layers(draft).Count && !DialogButton(window, "Sostituisci la stratigrafia con il profilo revisionato").IsEnabled,
                "Bozze estratte: profili, messaggio, anteprima con l'editor della vista senza il pulsante dell'importazione");
            var forms = dialog.Preview!.Forms.ToArray();
            Check(forms.All(f => f.WikiNavigator is not null), "Anteprima: ogni form con il navigatore della Wiki dei servizi");
            services.Wiki.Clear();
            F1(FieldBox(editor.GeneralForm, "Nome della stratigrafia"));
            Check(services.Wiki.Count == 0, "F1 nell'anteprima: nessuna pagina, come la finestra storica (fuori dal foglio)");
            Click(view.ReviewedOption);
            Check(dialog.Reviewed && DialogButton(window, "Sostituisci la stratigrafia con il profilo revisionato").IsEnabled, "Bozza revisionata applicabile");
            FieldBox(editor.GeneralForm, "Nome della stratigrafia").Text = "S1 corretta";
            window.UpdateLayout();
            Check(!dialog.Reviewed && view.ReviewedOption.IsChecked == false && !DialogButton(window, "Sostituisci la stratigrafia con il profilo revisionato").IsEnabled,
                "Una modifica della bozza toglie la revisione");
            if (Shots is not null) File.WriteAllBytes(Path.Combine(Shots, "dialogo-importazione-chiara.png"), DialogPng(window));
            Click(view.ReviewedOption);
            Click(DialogButton(window, "Sostituisci la stratigrafia con il profilo revisionato"));
        };
        Click(button);
        await host.Sheet.ImportCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(30));
        await host.Settle();
        services.Dialog = null;
        Check(host.Modified == 1 && host.Editor.Data.S("name") == "S1 corretta" && host.Editor.Data["import_source"]!["reviewed"]!.GetValue<bool>()
            && FieldBox(host.View.GeneralForm, "Nome della stratigrafia").Text == "S1 corretta",
            "Bozza applicata: dati del foglio sostituiti, Modified, editor della vista ricostruito");
        Check(dialogView is not null && dialogView.KeyBox.Password.Length == 0, "Chiave cancellata dal PasswordBox alla chiusura del dialogo");
        // Errors of the extraction and the interruption; the dialog closed without a result.
        var gate = new TaskCompletionSource<IReadOnlyList<JsonObject>>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.Dialog = async (shown, window) =>
        {
            var dialog = (StratigraphyImportDialogViewModel)shown;
            var view = Visual(window).OfType<StratigraphyImportDialogView>().Single();
            source.Next = _ => Task.FromException<IReadOnlyList<JsonObject>>(new System.Net.Http.HttpRequestException("rete"));
            Click(DialogButton(window, "Invia PDF a OpenAI e prepara bozze"));
            await Settle(window);
            Check(view.MessageText.Text == StratigraphyImport.ConnectionFailed && view.ProfileChoice.Items.Count == 0, "Errore di connessione nel messaggio del dialogo");
            source.Next = token => { token.Register(() => gate.TrySetCanceled(token)); return gate.Task; };
            Click(DialogButton(window, "Invia PDF a OpenAI e prepara bozze"));
            window.UpdateLayout();
            Check(dialog.IsRunning && !view.ModelBox.IsEnabled && !view.KeyBox.IsEnabled && DialogButton(window, "Interrompi").IsEnabled
                && !DialogButton(window, "Invia PDF a OpenAI e prepara bozze").IsEnabled && !DialogButton(window, "Scegli PDF…").IsEnabled
                && view.MessageText.Text == StratigraphyImport.Reading, "Estrazione in corso: file, modello e chiave bloccati, interruzione attiva");
            Click(DialogButton(window, "Interrompi"));
            for (int i = 0; i < 100 && dialog.IsRunning; i++) await Settle(window);
            Check(!dialog.IsRunning && view.MessageText.Text == StratigraphyImport.Interrupted && view.ModelBox.IsEnabled && view.KeyBox.IsEnabled,
                "Estrazione interrotta: messaggio, controlli di nuovo attivi");
            dialog.Dismiss();
        };
        Click(button);
        await host.Sheet.ImportCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(30));
        await host.Settle();
        services.Dialog = null;
        Check(host.Modified == 1 && host.Editor.Data.S("name") == "S1 corretta" && source.Calls.Count == 3, "Dialogo chiuso senza applicare: dati del foglio invariati");
        // The Release: the bench of the composition root, import disabled with the historic tooltip.
        using var release = new Hosted(Editors(new Services()).Open(Module, StratigraphyScenarios.FixtureData(root, "profilo-medi-falda")));
        await release.Settle();
        Check(!release.View.ImportButton.IsEnabled && release.View.ImportButton.ToolTip as string == StratigraphyImport.Disabled
            && ToolTipService.GetShowOnDisabled(release.View.ImportButton), "Release: importazione spenta con il suggerimento della vista storica (S5)");
    }

    private static async Task Settle(Window window)
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        window.UpdateLayout();
    }

    /// <summary>A draft as the importer gives it: a valid stratigraphy named <paramref name="name"/> with the provenance of the import.</summary>
    private static JsonObject Draft(string name, string warnings)
    {
        var draft = StratigraphyDocument.Create();
        draft["name"] = name;
        draft["import_source"] = new JsonObject { ["file"] = "relazione.pdf", ["sha256"] = "", ["model"] = "modello-di-prova", ["date"] = "", ["warnings"] = warnings, ["reviewed"] = false };
        return draft;
    }

    /// <summary>The source of the drafts, scripted: never the network or a real AI service.</summary>
    private sealed class ScriptedDrafts : IStratigraphyDraftSource
    {
        internal Func<CancellationToken, Task<IReadOnlyList<JsonObject>>> Next { get; set; } = _ => throw new InvalidOperationException("Estrazione non prevista.");
        internal List<(string Path, string Key, string Model)> Calls { get; } = [];

        public Task<IReadOnlyList<JsonObject>> ExtractAsync(string path, string apiKey, string model, CancellationToken cancellation)
        {
            Calls.Add((path, apiKey, model));
            return Next(cancellation);
        }
    }

    // ---------------------------------------------------------------- 8. the shared soil dialog (T5)

    /// <summary>
    /// The view of the shared dialog (SoilTransferTemplates, through the DataTemplate) against the historic window SoilTransferDialog on the
    /// stratigraphy fixtures, with and without the other stratigraphies of the project: profiles, status, description, preview, destinations,
    /// guidance and buttons at opening, for every destination, after the import from the project and for both parameter sets. The window is
    /// built and read without being shown (its private controls), the shared dialog is driven through the controls of its view. Sending and
    /// replacing are proved by the traces through the view (part 1).
    /// </summary>
    private static async Task SharedDialog(string root)
    {
        var sources = StratigraphyScenarios.Fixtures(root).Select(id => new SoilSource(Module + " · " + id, Module, StratigraphyScenarios.FixtureData(root, id), "id-" + id)).ToArray();
        int compared = 0;
        foreach (var source in sources)
        {
            foreach (var given in new[] { (IReadOnlyList<SoilSource>)[], sources.Where(s => s != source).ToArray() })
            {
                var historic = new SoilTransferDialog(null!, source.Module, source.Data, given.Select(s => (s.Label, s.Module, s.Data, s.Id)));
                var services = new Services();
                services.Dialog = async (shown, window) =>
                {
                    var dialog = (SoilTransferDialogViewModel)shown;
                    string label = source.Label + (given.Count > 0 ? " con fonti" : "");
                    Same(historic, window, label + ": apertura"); compared++;
                    foreach (var destination in dialog.Destinations)
                    {
                        historic.Destination.SelectedValue = destination.Module; DialogCombo(window, "Destination").SelectedValue = destination.Module;
                        Same(historic, window, label + ": destinazione " + destination.Module); compared++;
                    }
                    if (given.Count > 0)
                    {
                        var import = HistoricButtons(historic).Single(b => Equals(b.Content, "Importa dal foglio selezionato"));
                        if (import.IsEnabled) import.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, import));
                        Click(DialogButton(window, "Importa dal foglio selezionato"));
                        Same(historic, window, label + ": importato dal foglio del progetto"); compared++;
                    }
                    foreach (string scenario in dialog.Scenarios)
                    {
                        HistoricScenario(historic).SelectedItem = scenario; DialogCombo(window, "Scenario").SelectedItem = scenario;
                        Same(historic, window, label + ": parametri " + scenario); compared++;
                    }
                    if (Shots is not null && source == sources[0] && given.Count > 0)
                        File.WriteAllBytes(Path.Combine(Shots, "dialogo-terreno-chiara.png"), DialogPng(window));
                    await Task.CompletedTask;
                };
                var shared = new SoilTransferDialogService(services.Create());
                var choice = await shared.ShowAsync(source.Module, source.Data, given);
                Check(choice is null && services.Shown.Single() is SoilTransferDialogViewModel, source.Label + ": dialogo condiviso mostrato dal servizio dei dialoghi e chiuso senza scelta");
                if (Shots is not null && source == sources[0] && given.Count > 0)
                {
                    // The historic window in the same state, outside the screen and never activated, for the comparison by eye.
                    historic.WindowStartupLocation = WindowStartupLocation.Manual; historic.Left = -32000; historic.Top = -32000;
                    historic.ShowActivated = false; historic.ShowInTaskbar = false;
                    historic.Show(); historic.UpdateLayout();
                    File.WriteAllBytes(Path.Combine(Shots, "dialogo-terreno-storico-chiara.png"), Png((FrameworkElement)historic.Content));
                }
                historic.Close();
            }
        }
        Log.Add($"   dialogo del terreno: {compared} stati confrontati");
        // The composition root gives the stratigraphy the shared dialog: «Invia / importa terreno da ANTHEA…» of the view shows it.
        var route = new Services();
        using var host = new Hosted(Editors(route).Open(Module, StratigraphyScenarios.FixtureData(root, "profilo-medi-falda")));
        await host.Settle();
        (string Module, JsonObject Data, string Name)? opened = null;
        host.Editor.ModuleCopyRequested += (module, data, name) => opened = (module, data, name);
        host.Editor.SoilSources = () => sources.Select(s => (s.Label, s.Module, s.Data, s.Id));
        route.Dialog = (shown, window) =>
        {
            DialogCombo(window, "Destination").SelectedValue = "geo_palo_verticale";
            Click(DialogButton(window, "Invia a un nuovo foglio"));
            return Task.CompletedTask;
        };
        Click(host.SheetView.TransferButton);
        await host.Settle();
        Check(route.Shown.SingleOrDefault() is SoilTransferDialogViewModel { SourceLabels.Count: 5 } && opened is { Module: "geo_palo_verticale" } && host.Modified == 0,
            "Radice di composizione: «Invia / importa terreno da ANTHEA…» mostra il dialogo condiviso (T5) con le fonti del progetto e invia la copia alla finestra (" + opened?.Name + ")");
    }

    private static IEnumerable<Button> HistoricButtons(DependencyObject root)
    {
        var pending = new Stack<object>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is Button button) yield return button;
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element).Cast<object>()) pending.Push(child);
        }
    }

    private static ComboBox HistoricScenario(SoilTransferDialog historic)
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

    private static T Field<T>(object owner, string name) where T : class => (T)Private(owner, name);

    /// <summary>What the historic window and the view of the shared dialog show: profiles, status, description, preview, destination, guidance, buttons.</summary>
    private static void Same(SoilTransferDialog historic, Window window, string what)
    {
        window.UpdateLayout();
        var survey = Field<ComboBox>(historic, "survey");
        var status = Field<TextBlock>(historic, "status");
        var description = Field<TextBlock>(historic, "description");
        var preview = Field<ContentControl>(historic, "preview");
        var rows = preview.Content is DataGrid grid ? ((IEnumerable)grid.ItemsSource).Cast<string[]>().Select(r => string.Join("|", r)).ToArray() : [];
        var headers = preview.Content is DataGrid table ? table.Columns.Select(c => c.Header as string).ToArray() : [];
        var guidance = ((Panel)((FrameworkElement)historic.Destination.Parent).Parent).Children.OfType<UIElement>()
            .SkipWhile(e => !ReferenceEquals(e, historic.Destination.Parent)).Skip(1).OfType<TextBlock>().First();
        string a = string.Join("\n", [
            "profili " + string.Join("|", ((IEnumerable)survey.ItemsSource).Cast<string>()) + " scelto " + survey.SelectedIndex,
            "stato " + status.Text + (Equals(status.Foreground, Appearance.Ink) ? "" : " (errore)"),
            "descrizione " + description.Text,
            "anteprima " + (preview.Content is null ? "-" : string.Join("|", headers) + " " + string.Join(" / ", rows)),
            "destinazione " + historic.Destination.SelectedValue + " voci " + string.Join("|", historic.Destination.Items.Cast<ComboBoxItem>().Select(i => i.Tag + "=" + i.Content)),
            "guida " + guidance.Text,
            "invio " + historic.SendButton.IsEnabled + " importa " + HistoricButtons(historic).Single(b => Equals(b.Content, "Importa dal foglio selezionato")).IsEnabled]);
        var surveyChoice = DialogCombo(window, "Survey");
        var statusText = DialogText(window, "Status");
        var grids = Visual(window).OfType<DataGrid>().Where(g => g.IsVisible).ToArray();
        var destination = DialogCombo(window, "Destination");
        var failure = window.TryFindResource("Token.Status.Fail");
        string b = string.Join("\n", [
            "profili " + string.Join("|", surveyChoice.Items.Cast<string>()) + " scelto " + surveyChoice.SelectedIndex,
            "stato " + statusText.Text + (ReferenceEquals(statusText.Foreground, failure) ? " (errore)" : ""),
            "descrizione " + DialogText(window, "Description").Text,
            "anteprima " + (grids.Length == 0 ? "-" : string.Join("|", grids.Single().Columns.Select(c => c.Header as string)) + " "
                + string.Join(" / ", ((IEnumerable)grids.Single().ItemsSource).Cast<string[]>().Select(r => string.Join("|", r)))),
            "destinazione " + destination.SelectedValue + " voci " + string.Join("|", destination.Items.Cast<SoilTransferDestination>().Select(d => d.Module + "=" + d.Label)),
            "guida " + DialogText(window, "Guidance").Text,
            "invio " + DialogButton(window, "Invia a un nuovo foglio").IsEnabled + " importa " + DialogButton(window, "Importa dal foglio selezionato").IsEnabled]);
        if (a != b) Log.Add("   finestra storica:\n" + a + "\n   vista del dialogo condiviso:\n" + b);
        Check(a == b, "Dialogo del terreno, " + what + ": la vista del dialogo condiviso come la finestra storica");
    }

    // ---------------------------------------------------------------- 9. bench and Release

    private static void Bench(string root)
    {
        var data = StratigraphyScenarios.FixtureData(root, "profilo-medi-falda");
        using (var mvvm = Editors(new Services()).Open(Module, data))
            Check(mvvm is MvvmSheetEditor { Sheet: StratigraphySheetViewModel sheet } && sheet.Editor.Forms.All(f => f.WikiNavigator is not null),
                Module + ": con --module-impl il banco apre la vista MVVM, ogni form con il navigatore della Wiki");
        using (var release = ModuleHostBench.SheetEditors([], DesktopComposition.CreateModules()).Open(Module, data))
            Check(release is SheetEditor && ModuleImplementations.Release.For(Module) == SheetImplementation.Historic, Module + ": la Release resta storica fino alla prova dell'utente");
    }
}
