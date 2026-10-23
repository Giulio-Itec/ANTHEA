using System.Globalization;
using System.IO;
using System.IO.Compression;
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
using Anthea.Application.Reporting;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.Materials;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.Materials;

namespace X.Desktop;

/// <summary>
/// --check-materials-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.5, proposal F5 section 7.3): the XAML views of the material sheets
/// (ConcreteMaterialSheetView, RebarMaterialSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the window opens them with the MVVM
/// implementation, in windows outside the screen (never activated, no mouse). Parts:
/// <list type="number">
/// <item>Traces through the views: the 21 scenarios of tests/Shared/MaterialTrace.cs driven through the controls of the views (text
/// boxes, choices and check boxes of the fields, class, exposure, deviation value, type and reference of the steel) and read back from the
/// controls, equal line by line to the traces of the historic views frozen in tests/fixtures/f5-materiali/storica, data and preview
/// (the edits of the concrete held until Commit) included. What the historic views computed without showing it is read from the
/// ViewModel: the cards of fck, fctm and Ecm (built and never added) and the active exposures (decision M10).</item>
/// <item>For each F3 fixture of the two modules: the visit (layout, expanders, three appearances) writes nothing and raises no Modified;
/// data and preview at the opening, data and Modified after commit and «Calcola» equal to those of the frozen trace of the historic view
/// (scenario fixture-&lt;id&gt;); the exports are the files the historic view wrote: Word and PDF of MaterialReportChapter.Write on the
/// committed data (decision T8), JSON refused with the message of the historic view; the chapter of the material (MaterialReportContent)
/// is MaterialReportChapter.Create on the committed data, as in the historic view, and the report of the sheet (Report.BuildReport) is the
/// exported Word.</item>
/// <item>The chapter of the materials in the report of a project, after edits through the view that give the data the historic view
/// gave after the same edits (<see cref="HistoricProjectEdits"/>).</item>
/// <item>Screenshots of the views in Chiara, Scuro and Molto scuro (folder schermate).</item>
/// <item>Help: the «?» labels shown are those of the historic views and open their section; F1 on a field opens its section, or the guide
/// of the module (decision D11); F1 elsewhere opens the guide of the module; all through the IWikiNavigator of the services, also in
/// read-only.</item>
/// <item>AutomationProperties.Name of the historic views kept by the new ones.</item>
/// <item>Read-only revision: the same controls refuse the edits as in the historic views; typing and choices are refused.</item>
/// <item>The interactive bench opens the two MVVM sheets with the services of the product; the Release stays historic.</item>
/// </list>
/// The historic views are not opened any more (f5-pass-geo, before their archiving, Q11): what the checks compared with them is compared
/// with the values they gave, frozen in their traces or recorded here (<see cref="HistoricHelpLabels"/>, <see cref="HistoricAutomationNames"/>,
/// <see cref="HistoricReadOnly"/>, <see cref="HistoricProjectEdits"/>, <see cref="HistoricNoResult"/>).
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static class MaterialSheetViewChecks
{
    private const string Concrete = MaterialScenarios.Concrete, Steel = MaterialScenarios.Steel;
    private static readonly string[] CompositionKeys = ["chloride", "ratio", "cement", "air", "dmax"];
    private static readonly string[] SteelTextKeys = ["materiale_acciaio_nome", "fyk_mpa", "steel_fu_mpa", "steel_modulus_mpa", "steel_eps_u", "gamma_s"];

    // ---------------------------------------------------------------- values of the historic views (archived with the Release switch)
    // Recorded by this check on the historic views before their archiving: the same in every run up to the one of the release 1.13.0
    // (supporto/artefatti/ci/20261023-013037-full-fine-ca-1.13.0 of the main checkout, dettaglio.txt) and in the capture run on db7ef68d
    // with the values logged (supporto/artefatti/ci/20261023-120256-geo-cattura-mat of the worktree aw-f5-pass-geo).

    /// <summary>The message of SheetEditor.ExportResult of a material sheet, which never has a result.</summary>
    private const string HistoricNoResult = "Premere Calcola prima di esportare.";

    private static readonly string[] SteelHelpLabels = ["Wiki: Coefficiente parziale γs", "Wiki: Deformazione ultima εu", "Wiki: Diagramma", "Wiki: Modulo elastico Es",
        "Wiki: Nome", "Wiki: Rottura fu", "Wiki: Snervamento fyk"];

    /// <summary>The «?» labels shown by the historic views (expanders open), by module and fixture.</summary>
    private static readonly Dictionary<(string Module, string Id), string[]> HistoricHelpLabels = new()
    {
        [(Concrete, "default")] = ["Wiki: Controllo di esecuzione", "Wiki: Criterio copriferro", "Wiki: Diametro della barra da verificare · φ",
            "Wiki: Dimensione massima aggregato · Dmax", "Wiki: Tipo di elemento", "Wiki: Vita utile di progetto"],
        [(Concrete, "ec2-riduzioni-tolleranza")] = ["Wiki: Controllo di esecuzione", "Wiki: Criterio copriferro", "Wiki: Diametro della barra da verificare · φ",
            "Wiki: Dimensione massima aggregato · Dmax", "Wiki: Vita utile di progetto"],
        [(Steel, "default")] = SteelHelpLabels,
        [(Steel, "storico-feb38k-eps")] = SteelHelpLabels,
    };

    private static readonly string[] ConcreteAutomationNames = ["Classe di esposizione",
        "Schema del copriferro: cnom, diametro delle staffe, semidiametro delle barre longitudinali e distanza d′", "Wiki: Controllo di esecuzione",
        "Wiki: Criterio copriferro", "Wiki: Diametro della barra da verificare · φ", "Wiki: Dimensione massima aggregato · Dmax", "Wiki: Vita utile di progetto"];

    private static readonly string[] SteelAutomationNames = ["Certificato o riferimento", "Coefficiente parziale γs", "Deformazione ultima εu", "Diagramma",
        "Modulo elastico Es", "Nome", "Rottura fu", "Snervamento fyk", "Tipo di acciaio", "Wiki: Coefficiente parziale γs", "Wiki: Deformazione ultima εu",
        "Wiki: Diagramma", "Wiki: Modulo elastico Es", "Wiki: Nome", "Wiki: Rottura fu", "Wiki: Snervamento fyk"];

    /// <summary>The AutomationProperties.Name of the historic views (visible elements, expanders open), by module and fixture.</summary>
    private static readonly Dictionary<(string Module, string Id), string[]> HistoricAutomationNames = new()
    {
        [(Concrete, "default")] = [.. ConcreteAutomationNames, "Wiki: Tipo di elemento"],
        [(Concrete, "ec2-riduzioni-tolleranza")] = ConcreteAutomationNames,
        [(Concrete, "legacy-tre-esposizioni")] = ConcreteAutomationNames,
        [(Steel, "default")] = SteelAutomationNames,
        [(Steel, "storico-feb38k-eps")] = SteelAutomationNames,
    };

    private const string SteelReadOnly = "scelta steel_diagramma bloccata scelta tipo bloccata testo fyk_mpa bloccato testo gamma_s bloccato "
        + "testo materiale_acciaio_nome bloccato testo riferimento bloccato testo steel_eps_u bloccato testo steel_fu_mpa bloccato testo steel_modulus_mpa bloccato";

    private static string ValueBoxes(int count) => string.Join(" ", Enumerable.Repeat("testo valore bloccato", count));

    /// <summary>
    /// The controls of the historic views that accept the user before and after InspectRevision (<see cref="State"/>, with the keys of the
    /// accessors of their traces), by module and fixture.
    /// </summary>
    private static readonly Dictionary<(string Module, string Id), (string Before, string After)> HistoricReadOnly = new()
    {
        [(Concrete, "default")] = (
            "scelta abrasion libera scelta bondCondition libera scelta cement libera scelta cementClass libera scelta cementEarly libera scelta classe libera "
            + "scelta consistency libera scelta coverMethod libera scelta deviationControl libera scelta deviationValue bloccata scelta esposizione libera "
            + "scelta ground libera scelta life libera scelta ntcElement libera spunta ntcQuality libera spunta rough libera tabella valore sola lettura "
            + "testo aggregate libero testo bondAlpha libero testo bondGamma libero testo cementName libero testo diameter libero " + ValueBoxes(6),
            "scelta abrasion bloccata scelta bondCondition bloccata scelta cement bloccata scelta cementClass bloccata scelta cementEarly bloccata "
            + "scelta classe bloccata scelta consistency bloccata scelta coverMethod bloccata scelta deviationControl bloccata scelta deviationValue bloccata "
            + "scelta esposizione bloccata scelta ground bloccata scelta life bloccata scelta ntcElement bloccata spunta ntcQuality bloccata spunta rough bloccata "
            + "tabella valore sola lettura testo aggregate bloccato testo bondAlpha bloccato testo bondGamma bloccato testo cementName bloccato "
            + "testo diameter bloccato " + ValueBoxes(6)),
        [(Concrete, "ec2-riduzioni-tolleranza")] = (
            "scelta abrasion libera scelta bondCondition libera scelta cement libera scelta cementClass libera scelta cementEarly libera scelta classe libera "
            + "scelta consistency libera scelta coverMethod libera scelta deviationControl libera scelta deviationValue libera scelta esposizione libera "
            + "scelta ground libera scelta life libera spunta highStrength libera spunta quality libera spunta rough libera spunta slab libera "
            + "tabella valore sola lettura testo aggregate libero testo bondAlpha libero testo bondGamma libero testo cementName libero testo diameter libero "
            + ValueBoxes(5),
            "scelta abrasion bloccata scelta bondCondition bloccata scelta cement bloccata scelta cementClass bloccata scelta cementEarly bloccata "
            + "scelta classe bloccata scelta consistency bloccata scelta coverMethod bloccata scelta deviationControl bloccata scelta deviationValue bloccata "
            + "scelta esposizione bloccata scelta ground bloccata scelta life bloccata spunta highStrength bloccata spunta quality bloccata spunta rough bloccata "
            + "spunta slab bloccata tabella valore sola lettura testo aggregate bloccato testo bondAlpha bloccato testo bondGamma bloccato "
            + "testo cementName bloccato testo diameter bloccato " + ValueBoxes(5)),
        [(Steel, "default")] = (
            "scelta steel_diagramma bloccata scelta tipo libera testo fyk_mpa bloccato testo gamma_s libero testo materiale_acciaio_nome bloccato "
            + "testo riferimento libero testo steel_eps_u bloccato testo steel_fu_mpa bloccato testo steel_modulus_mpa bloccato",
            SteelReadOnly),
        [(Steel, "personalizzato-incrudente")] = (
            "scelta steel_diagramma libera scelta tipo libera testo fyk_mpa libero testo gamma_s libero testo materiale_acciaio_nome libero "
            + "testo riferimento libero testo steel_eps_u libero testo steel_fu_mpa libero testo steel_modulus_mpa libero",
            SteelReadOnly),
    };

    /// <summary>
    /// The edits of the chapter in a project report, with the data (MaterialTrace.Compact) and the Modified events of the historic view after
    /// the same edits through its controls and the commit.
    /// </summary>
    private static readonly (string Module, string Id, MaterialStep[] Steps, string Data, int Modified)[] HistoricProjectEdits =
    [
        (Concrete, "default", [new(MaterialStep.Class, Text: "C40/50"), new(MaterialStep.Exposure, Text: "XS2"), new(MaterialStep.Number, "diameter", "20"),
            new(MaterialStep.Choice, "coverMethod", "EC2 2004")],
            """{"versione_materiali":1,"classe":"C40/50","esposizione_principale":"XS2","numeri":{"diameter":"20","aggregate":"20","cementName":"","bondAlpha":"1,0","bondGamma":"1,5"},"scelte":{"ntcElement":"Trave / pilastro","coverMethod":"EC2 2004","life":"50 anni","deviationControl":"Ordinario","deviationValue":"10 mm","ground":"Casseratura","abrasion":"Nessuno","consistency":"Da definire","cement":"CEM I","cementClass":"32,5","cementEarly":"N","bondCondition":"Altre · η₁ = 0,7"},"opzioni":{"highStrength":false,"slab":false,"quality":false,"rough":false,"ntcQuality":false},"esposizioni":["XS2"]}""", 4),
        (Steel, "default", [new(MaterialStep.Steel, Text: "Personalizzato"), new(MaterialStep.Field, "fyk_mpa", "455"), new(MaterialStep.Reference, Text: "Certificato 7")],
            """{"versione_acciaio":1,"input":{"classe_acciaio":"Personalizzato","materiale_acciaio_nome":"Acciaio personalizzato","steel_modulus_mpa":200000,"fyk_mpa":"455","steel_fu_mpa":540,"steel_eps_u":75,"steel_diagramma":"Elastoplastico","gamma_s":"1.15"},"riferimento":"Certificato 7"}""", 3),
    ];

    /// <summary>The Wiki of the services of the check: it records the sections opened, in order.</summary>
    private sealed class RecordingWiki : IWikiNavigator
    {
        internal List<string?> Shown { get; } = [];
        public void Show(string? uri = null) => Shown.Add(uri);
    }

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
        // The culture of the captures of the historic views (it-IT without the overrides of the user), also in the operations of the dispatcher.
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var wiki = new RecordingWiki();
        var editors = Editors(wiki);
        try
        {
            await Traces(root, output, editors);
            await FixturesAndExports(root, output, editors);
            await ProjectReport(root, output, editors);
            await Screenshots(root, Path.Combine(output, "schermate"), editors);
            await Help(root, editors, wiki);
            await AutomationNames(root, editors);
            await ReadOnly(root, editors);
            InteractiveBench(root);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli delle viste XAML dei materiali (MvvmSheetEditor, fuori schermo)", new UTF8Encoding(false));
    }

    /// <summary>The editors of the window with the two MVVM material sheets and a Wiki navigator that records what it opens.</summary>
    private static SheetEditorFactory Editors(IWikiNavigator wiki)
    {
        var bench = ModuleHostBench.Services();
        var services = new PresentationServices(bench.Messages, bench.Confirmations, bench.Files, bench.Dialogs, bench.Clipboard, bench.Links, wiki);
        return new SheetEditorFactory(ModuleHostBench.Implementations([ModuleHostBench.Argument, Concrete + "=mvvm", ModuleHostBench.Argument, Steel + "=mvvm"]),
            DesktopComposition.CreateModules(), services, new CalculationLimiter(CalculationLimiter.DefaultMaximum));
    }

    // ---------------------------------------------------------------- hosting

    /// <summary>A sheet in a window outside the screen, laid out at 1400×1050 like the sheet area of the main window.</summary>
    private sealed class Hosted : IDisposable
    {
        private readonly Window window;
        internal ISheetEditor Editor { get; }
        internal int Modified;

        internal Hosted(ISheetEditor editor, bool dark = false)
        {
            Editor = editor;
            editor.Modified += () => Modified++;
            window = new Window
            {
                Content = editor.View, Width = 1400, Height = 1050, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Materiali " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
        }

        internal FrameworkElement Root => Editor.View;

        internal ConcreteMaterialSheetViewModel ConcreteSheet => (ConcreteMaterialSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal RebarMaterialSheetViewModel SteelSheet => (RebarMaterialSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal void Layout()
        {
            window.UpdateLayout();
            Root.UpdateLayout();
        }

        internal async Task Settle()
        {
            Layout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            Layout();
        }

        /// <summary>Opens every expander, as the visit of the F3 capture.</summary>
        internal async Task Expand()
        {
            foreach (var expander in Visual(Root).OfType<Expander>().ToArray()) expander.IsExpanded = true;
            await Settle();
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

    /// <summary>
    /// Every element of the view, also those never laid out (the options of a criterion not shown, the content of a closed expander): the
    /// logical and the visual tree, after applying the templates, so that the controls of collapsed parts exist as in the historic views.
    /// </summary>
    internal static List<DependencyObject> All(DependencyObject root)
    {
        int count = -1;
        List<DependencyObject> found = [];
        for (int pass = 0; pass < 10 && found.Count != count; pass++)
        {
            count = found.Count;
            found = Walk(root);
            foreach (var element in found.OfType<FrameworkElement>()) element.ApplyTemplate();
        }
        return found;

        static List<DependencyObject> Walk(DependencyObject root)
        {
            var seen = new HashSet<DependencyObject>(); var list = new List<DependencyObject>(); var pending = new Stack<DependencyObject>(); pending.Push(root);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (!seen.Add(node)) continue;
                list.Add(node);
                foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) pending.Push(child);
                if (node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D)
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) pending.Push(VisualTreeHelper.GetChild(node, i));
            }
            return list;
        }
    }

    private static string? BoundPath(FrameworkElement element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    private static object Token(string name) => Application.Current.FindResource("Token." + name);

    // ---------------------------------------------------------------- the views, read and driven through their controls

    /// <summary>The controls of the new views, found by the field of their row or by their bindings.</summary>
    private sealed class ViewSide(Hosted host, string module)
    {
        private List<DependencyObject> Elements => All(host.Root);
        private object Sheet => ((MvvmSheetEditor)host.Editor).Sheet;

        private T Field<T>(string key) where T : FrameworkElement => Elements.OfType<T>().Single(e => e.DataContext is FieldViewModel f && f.Key == key && (e is not TextBox t || t.TemplatedParent is not Control));
        internal TextBox Box(string key) => Field<TextBox>(key);
        internal ComboBox Choice(string key) => Field<ComboBox>(key);
        internal CheckBox Option(string key) => Field<CheckBox>(key);
        internal ComboBox Bound(string itemsPath) => Elements.OfType<ComboBox>().Single(c => BoundPath(c, ItemsControl.ItemsSourceProperty) == itemsPath);
        internal TextBlock Text(string path) => Elements.OfType<TextBlock>().Single(t => ReferenceEquals(t.DataContext, Sheet) && BoundPath(t, TextBlock.TextProperty) == path);
        internal TextBox Value(string path) => Elements.OfType<TextBox>().Single(t => ReferenceEquals(t.DataContext, Sheet) && BoundPath(t, TextBox.TextProperty) == path);
        internal T Named<T>(string name) where T : FrameworkElement => Elements.OfType<T>().Single(e => e.Name == name);

        internal async Task Apply(MaterialStep step)
        {
            bool concrete = module == Concrete;
            switch (step.Kind)
            {
                case MaterialStep.Commit: host.Editor.Commit(); break;
                case MaterialStep.Calculate: await host.Editor.CalculateAsync(); break;
                case MaterialStep.ReadOnly: host.Editor.InspectRevision(); break;
                case MaterialStep.Class: Bound("Classes").SelectedItem = step.Text; break;
                case MaterialStep.Number: Box(step.Key!).Text = step.Text; break;
                case MaterialStep.Choice when concrete && step.Key == ConcreteMaterialSheetViewModel.DeviationValueKey: Bound("DeviationValues").SelectedItem = step.Text; break;
                case MaterialStep.Choice: Choice(step.Key!).SelectedItem = step.Text; break;
                case MaterialStep.Option: var option = Option(step.Key!); option.IsChecked = option.IsChecked != true; break;
                case MaterialStep.Exposure: Bound("Exposures").SelectedItem = step.Text; break;
                case MaterialStep.Steel: Bound("Types").SelectedItem = step.Text; break;
                case MaterialStep.Field: Box(step.Key!).Text = step.Text; break;
                case MaterialStep.Reference: Named<TextBox>("Reference").Text = step.Text; break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            await host.Settle();
        }

        internal IEnumerable<string> Lines(string step) => module == Concrete ? ConcreteState().Lines(step) : SteelState().Lines(step);

        private ConcreteTraceState ConcreteState()
        {
            host.Layout();
            var s = host.ConcreteSheet;
            var classes = Bound("Classes"); var exposures = Bound("Exposures"); var deviation = Bound("DeviationValues");
            var combined = Text("CombinedExposure"); var headline = Text("CoverHeadline"); var note = Text("DeviationNote");
            return new()
            {
                Data = host.Editor.Data, Preview = host.Editor.PreviewData, Modified = host.Modified, Editable = classes.IsEnabled,
                ClassName = classes.SelectedItem as string, Classes = classes.Items.Count,
                Numbers = Materiali.MaterialSheetInput.NumberKeys.Select(k => (k, Box(k).Text)).ToList(),
                Choices = Materiali.MaterialSheetInput.ChoiceKeys.Select(k => k == ConcreteMaterialSheetViewModel.DeviationValueKey
                    ? (k, deviation.SelectedItem?.ToString(), deviation.IsEnabled) : (k, Choice(k).SelectedItem?.ToString(), Choice(k).IsEnabled)).ToList(),
                DeviationValues = deviation.Items.Cast<object>().Select(i => i.ToString()!).ToList(),
                Options = Materiali.MaterialSheetInput.FlagKeys.Select(k => (k, Option(k).IsChecked == true)).ToList(),
                Exposure = exposures.SelectedItem?.ToString(), Exposures = exposures.Items.Count,
                // Not shown, as in the historic view (hidden check boxes, decision M10): from the ViewModel.
                Active = s.ActiveExposures.ToList(),
                ExposureDetails = Text("ExposureDetails").Text, ExposureExamples = Text("ExposureExamples").Text, Aggressiveness = Text("Aggressiveness").Text,
                Combined = combined.Text, CombinedVisible = combined.Visibility == Visibility.Visible,
                // The cards of fck, fctm and Ecm were built by the historic view and never added to it: from the ViewModel.
                Strength = s.Strength, Tension = s.Tension, Modulus = s.Modulus, Status = Text("Status").Text,
                Properties = Rows(Named<DataGrid>("PropertiesTable")).Select(r => r.Select(c => (string)c!).ToArray()).ToList(),
                BondValue = Text("BondValue").Text, BondDetails = Text("BondDetails").Text,
                MinimumClass = Value("MinimumConcreteClass").Text, MinimumNote = Text("MinimumConcreteNote").Text,
                CoverHeadline = headline.Text, CoverSteps = Text("CoverSteps").Text, CoverNotes = Text("CoverNotes").Text,
                CoverError = ReferenceEquals(headline.Foreground, Token("Warning.Foreground")),
                NtcVisible = Named<StackPanel>("NtcOptions").Visibility == Visibility.Visible, Ec2Visible = Named<Expander>("Ec2Options").Visibility == Visibility.Visible,
                DeviationNote = note.Text, DeviationToolTip = note.ToolTip as string ?? "",
                Composition = CompositionKeys.Select(k => (k, Value("Composition[" + k + "]").Text)).ToList(),
                CompositionSource = Text("CompositionSource").Text, AirNote = Text("CompositionAirNote").Text, ChlorideNote = Text("CompositionChlorideNote").Text,
                Requirements = Text("CompositionRequirements").Text, Consistency = Text("ConsistencyInfo").Text,
            };
        }

        private SteelTraceState SteelState()
        {
            host.Layout();
            var type = Bound("Types"); var diagram = Choice("steel_diagramma"); var status = Named<TextBlock>("StatusText");
            var plot = Elements.OfType<Plot>().Single();
            return new()
            {
                Data = host.Editor.Data, Preview = host.Editor.PreviewData, Modified = host.Modified, Editable = type.IsEnabled,
                Type = type.SelectedItem as string, Types = type.Items.Cast<object>().Select(i => i.ToString()!).ToList(),
                Fields = SteelTextKeys.Select(k => (k, Box(k).Text, Box(k).IsReadOnly)).ToList(),
                Diagram = diagram.SelectedItem as string, Diagrams = diagram.Items.Cast<object>().Select(i => i.ToString()!).ToList(), DiagramEnabled = diagram.IsEnabled,
                Reference = Named<TextBox>("Reference").Text,
                Description = Text("Description").Text, Source = Text("Source").Text, Results = Text("Results").Text, Status = status.Text,
                StatusCoherent = ReferenceEquals(status.Foreground, Token("Status.Coherent")),
                Series = plot.Series.Select(s => (s.Name, (IReadOnlyList<double[]>)s.Points)).ToList(),
            };
        }
    }

    /// <summary>The cells of the rows of a table, read through the bindings of its columns.</summary>
    private static IEnumerable<object?[]> Rows(DataGrid grid)
    {
        var paths = grid.Columns.OfType<DataGridTextColumn>().Select(c => ((Binding)c.Binding).Path.Path).ToArray();
        foreach (var item in grid.Items.Cast<object>().Where(i => i != CollectionView.NewItemPlaceholder))
            yield return paths.Select(p => item.GetType().GetProperty(p)!.GetValue(item)).ToArray();
    }

    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in MaterialScenarios.All(root))
        {
            var trace = new List<string>();
            var host = new Hosted(editors.Open(scenario.Module, MaterialScenarios.FixtureData(root, scenario.Module, scenario.Fixture)));
            try
            {
                string preview = host.Editor.PreviewData.ToJsonString(J.Options), data = host.Editor.Data.ToJsonString(J.Options);
                await host.Expand();
                Check(host.Modified == 0 && host.Editor.PreviewData.ToJsonString(J.Options) == preview && host.Editor.Data.ToJsonString(J.Options) == data,
                    scenario.File + ": apertura e visita senza Modified e senza scritture");
                var side = new ViewSide(host, scenario.Module);
                trace.AddRange(side.Lines("apertura"));
                foreach (var step in scenario.Steps)
                {
                    if (step.Kind == MaterialStep.Reopen)
                    {
                        var reopened = host.Editor.Data;
                        host.Dispose();
                        host = new Hosted(editors.Open(scenario.Module, reopened));
                        await host.Expand();
                        side = new ViewSide(host, scenario.Module);
                    }
                    else await side.Apply(step);
                    trace.AddRange(side.Lines(step.ToString()));
                }
            }
            finally { host.Dispose(); }
            MaterialTrace.Write(Path.Combine(folder, scenario.File), trace);
            string stored = Path.Combine(MaterialScenarios.Reference(root), scenario.File);
            string? difference = File.Exists(stored) ? MaterialTrace.FirstDifference(MaterialTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.File + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == 21, $"Tracce delle viste XAML uguali a quelle delle viste storiche: {scenarios} scenari, {lines} righe" + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    // ---------------------------------------------------------------- 2. visit and exports

    private static async Task FixturesAndExports(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        int fixtures = 0;
        foreach (string module in new[] { Concrete, Steel })
            foreach (string id in MaterialScenarios.Fixtures(root, module))
            {
                string name = module + "--" + id;
                // The frozen trace of the historic view on the path of the F3 capture: apertura, conferma, calcola, conferma, riapri, conferma.
                var historic = HistoricBlocks(root, module, id);
                Check(historic.Length == 6 && historic[0][0] == "== apertura" && historic[2][0] == "== " + MaterialStep.Calculate && historic[3][0] == "== " + MaterialStep.Commit,
                    name + ": traccia congelata della vista storica (fixture-" + id + ") sul percorso della cattura F3");
                var data = MaterialScenarios.FixtureData(root, module, id);
                using var mvvm = new Hosted(editors.Open(module, data));
                Check(mvvm.Editor is MvvmSheetEditor, name + ": editor MVVM");
                string opened = mvvm.Editor.Data.ToJsonString(J.Options), preview = mvvm.Editor.PreviewData.ToJsonString(J.Options);
                Check("dati " + MaterialTrace.Compact(mvvm.Editor.Data) == Line(historic[0], "dati") && "anteprima " + MaterialTrace.Compact(mvvm.Editor.PreviewData) == Line(historic[0], "anteprima"),
                    name + ": dati e anteprima all'apertura uguali a quelli della vista storica (traccia congelata), nell'ordine in memoria");
                await mvvm.Expand();
                foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
                Check(mvvm.Editor.Data.ToJsonString(J.Options) == opened && mvvm.Editor.PreviewData.ToJsonString(J.Options) == preview && mvvm.Modified == 0,
                    name + ": la visita (espansori, tre aspetti) non scrive e non solleva Modified");
                mvvm.Editor.Commit(); await mvvm.Editor.CalculateAsync(); mvvm.Editor.Commit();
                string committed = "dati " + MaterialTrace.Compact(mvvm.Editor.Data);
                Check(committed == Line(historic[3], "dati") && Modified(historic[3]) == mvvm.Modified && !mvvm.Editor.HasResults && mvvm.Editor.Result is null,
                    name + ": dati, risultato e Modified dopo conferma e «Calcola» uguali a quelli della vista storica (traccia congelata)");
                Check(Fails(() => mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json")), out string message) && message == HistoricNoResult,
                    name + ": esportazione JSON rifiutata con il messaggio della vista storica («" + message + "»)");
                // The chapter and the report of the historic view: SheetEditor committed, then MaterialReportChapter.Create and .Write on its data.
                var expected = Materiali.MaterialReportChapter.Create(module, (JsonObject)mvvm.Editor.Data.DeepClone()); var actual = mvvm.Editor.MaterialReportContent();
                Check(expected.Error == actual.Error && JsonNode.DeepEquals(System.Text.Json.JsonSerializer.SerializeToNode(expected.Derived), System.Text.Json.JsonSerializer.SerializeToNode(actual.Derived))
                    && expected.Document is null && actual.Document is null,
                    name + ": capitolo del materiale (MaterialReportContent) uguale a quello della vista storica" + (actual.Error is null ? "" : ", con l'errore «" + actual.Error + "»"));
                string title = "Fixture F3 · " + module + " · " + id;
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, []);
                Materiali.MaterialReportChapter.Write(Path.Combine(folder, name + "-attesa.docx"), module, title, (JsonObject)mvvm.Editor.Data.DeepClone());
                byte[] word = File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx"));
                string? part = DifferentPart(word, File.ReadAllBytes(Path.Combine(folder, name + "-attesa.docx")), normalizer);
                Check(part is null, name + ": report Word uguale in ogni parte a quello della vista storica (date del documento normalizzate)" + (part is null ? "" : ", diversa " + part));
                Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))),
                    name + ": PDF affiancato uguale a quello della vista storica, scritto come oggi da MaterialReportChapter.Write (T8; date del documento normalizzate)");
                part = DifferentPart(mvvm.Editor.BuildReport(title, []), word, normalizer);
                Check(part is null, name + ": il report del foglio (Report.BuildReport) è il Word esportato" + (part is null ? "" : ", diversa " + part));
                Check("dati " + MaterialTrace.Compact(mvvm.Editor.Data) == committed && Modified(historic[3]) == mvvm.Modified,
                    name + ": dati e Modified dopo le esportazioni uguali a quelli della vista storica");
                fixtures++;
            }
        Check(fixtures == 15, "Fixture F3 dei materiali visitate ed esportate: " + fixtures);
    }

    /// <summary>The frozen trace of the historic view for the F3 fixture <paramref name="id"/> (scenario fixture-&lt;id&gt;), split at its steps.</summary>
    private static string[][] HistoricBlocks(string root, string module, string id)
    {
        var blocks = new List<List<string>>();
        string file = new MaterialScenario(module, "fixture-" + id, id, []).File;
        foreach (string line in MaterialTrace.Read(Path.Combine(MaterialScenarios.Reference(root), file)))
        {
            if (line.StartsWith("== ", StringComparison.Ordinal)) blocks.Add([]);
            if (blocks.Count == 0) throw new InvalidOperationException("Traccia congelata senza intestazione: " + file);
            blocks[^1].Add(line);
        }
        return blocks.Select(b => b.ToArray()).ToArray();
    }

    /// <summary>The line of a step of a trace that starts with <paramref name="name"/>, as the trace states write it.</summary>
    private static string Line(string[] block, string name) => block.Single(l => l.StartsWith(name + " ", StringComparison.Ordinal));

    /// <summary>The Modified events of a step of a trace («modificato n · modificabile …»).</summary>
    private static int Modified(string[] block) => int.Parse(Line(block, "modificato").Split(' ')[1], CultureInfo.InvariantCulture);

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

    /// <summary>Two PDF files equal but for what changes at every rendering: dates, identifiers and random tags of the font subsets.</summary>
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

    // ---------------------------------------------------------------- 3. the chapter of the materials in a project report

    /// <summary>
    /// Edits through the view that give the data the historic view gave after the same edits (<see cref="HistoricProjectEdits"/>), then the
    /// report of a project section with the committed sheet: the chapter of the material (ModuleReportService, as MainWindow writes the report
    /// of a section) is the same document as with the data of the historic view.
    /// </summary>
    private static async Task ProjectReport(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "progetto");
        Directory.CreateDirectory(folder);
        var builder = new ModuleReportService(new ModuleFigureRenderer());
        var normalizer = new TextNormalizer();
        foreach (var (module, id, steps, historicData, historicModified) in HistoricProjectEdits)
        {
            var data = MaterialScenarios.FixtureData(root, module, id);
            using var mvvm = new Hosted(editors.Open(module, data));
            await mvvm.Expand();
            var side = new ViewSide(mvvm, module);
            foreach (var step in steps) await side.Apply(step);
            mvvm.Editor.Commit();
            Check(MaterialTrace.Compact(mvvm.Editor.Data) == historicData && mvvm.Modified == historicModified,
                module + ": le modifiche attraverso la vista danno i dati e i Modified della vista storica dopo le stesse modifiche");
            var archive = ProjectDocuments.CreateArchive();
            var section = ProjectDocuments.AddSection(ProjectDocuments.AddProject(archive, "Progetto materiali"), "Materiali");
            var sheet = ProjectDocuments.AddSheet(section, module, module == Concrete ? "Calcestruzzo" : "Acciaio per armature");
            var reports = new List<byte[]>();
            foreach (var (sheetData, kind) in new[] { (mvvm.Editor.Data.DeepClone(), "mvvm"), (JsonNode.Parse(historicData)!, "attesa") })
            {
                sheet["dati"] = sheetData;
                string path = Path.Combine(folder, $"{module}-{kind}.docx");
                await ProjectReportService.WriteSnapshotAsync(ProjectReportService.Snapshot(section), path, builder);
                reports.Add(File.ReadAllBytes(path));
            }
            string? part = DifferentPart(reports[0], reports[1], normalizer);
            Check(part is null, module + ": capitolo del materiale nel report del progetto uguale a quello con i dati della vista storica" + (part is null ? "" : ", diversa " + part));
        }
    }

    // ---------------------------------------------------------------- 4. screenshots

    private static async Task Screenshots(string root, string folder, SheetEditorFactory editors)
    {
        Directory.CreateDirectory(folder);
        var cases = new[] { (Concrete, "default"), (Concrete, "ec2-riduzioni-tolleranza"), (Steel, "default"), (Steel, "storico-feb38k-eps") };
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            foreach (var (module, id) in cases)
            {
                var data = MaterialScenarios.FixtureData(root, module, id);
                string name = (module == Concrete ? "calcestruzzo-" : "acciaio-") + id;
                using var host = new Hosted(editors.Open(module, data), mode != AppAppearance.Light);
                await host.Expand();
                await host.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"vista-{name}-{suffix}.png"), host.Png());
                written++;
            }
        }
        Appearance.Set(AppAppearance.Light, false);
        Check(written == 12, "Schermate delle viste nei tre aspetti: " + written + " PNG in " + folder);
    }

    // ---------------------------------------------------------------- 5. help

    private static async Task Help(string root, SheetEditorFactory editors, RecordingWiki wiki)
    {
        foreach (var (module, id) in new[] { (Concrete, "default"), (Concrete, "ec2-riduzioni-tolleranza"), (Steel, "default"), (Steel, "storico-feb38k-eps") })
        {
            var data = MaterialScenarios.FixtureData(root, module, id);
            using var host = new Hosted(editors.Open(module, data));
            await host.Expand();
            static string[] Labels(DependencyObject root) => Visual(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
                .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
            string[] after = Labels(host.Root);
            Check(HistoricHelpLabels[(module, id)].SequenceEqual(after), $"{module}/{id}: etichette «?» visibili uguali a quelle della vista storica: " + string.Join(", ", after));
        }
        await FieldHelp(root, editors, wiki, Concrete, "diameter", "cementName", "Exposures");
        await FieldHelp(root, editors, wiki, Steel, "fyk_mpa", "gamma_s", "Types");
    }

    /// <summary>
    /// «?» of <paramref name="key"/>, F1 on it, F1 on <paramref name="other"/> (its topic or the guide, decision D11) and F1 on the choice
    /// <paramref name="outside"/>, which is not a field (the guide, from the host); then «?» and F1 again in read-only.
    /// </summary>
    private static async Task FieldHelp(string root, SheetEditorFactory editors, RecordingWiki wiki, string module, string key, string other, string outside)
    {
        string guide = Anthea.Wiki.WikiCatalog.ForModule(module)?.Id ?? throw new InvalidOperationException("Guida del modulo assente nella Wiki: " + module);
        var topic = Anthea.Presentation.Help.HelpTopics.ForField(key, module) ?? throw new InvalidOperationException("Argomento assente: " + key);
        string otherUri = Anthea.Presentation.Help.HelpTopics.ForField(other, module)?.Uri ?? guide;
        using var host = new Hosted(editors.Open(module, MaterialScenarios.FixtureData(root, module, "default")));
        await host.Expand();
        var side = new ViewSide(host, module);
        var field = (FieldViewModel)side.Box(key).DataContext;
        var help = Visual(host.Root).OfType<Button>().Single(b => b.IsVisible && AutomationProperties.GetName(b) == "Wiki: " + field.Label);
        Check(help.ToolTip as string == topic.Title + "\nApri questa sezione della Wiki", $"{module}: «?» di {key}, suggerimento dell'argomento");
        wiki.Shown.Clear();
        help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
        F1(side.Box(key));
        F1(side.Box(other));
        F1(side.Bound(outside));
        Check(wiki.Shown.SequenceEqual([topic.Uri, topic.Uri, otherUri, guide]),
            $"{module}: «?» e F1 dal navigatore della Wiki: argomento di {key}, F1 su {key} (argomento), su {other} ({(otherUri == guide ? "guida" : "argomento")}), fuori dai campi (guida): "
            + string.Join(", ", wiki.Shown));
        host.Editor.InspectRevision();
        await host.Settle();
        wiki.Shown.Clear();
        Check(help.IsEnabled, module + ": «?» utilizzabile in sola lettura");
        help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
        F1(side.Box(key));
        Check(wiki.Shown.SequenceEqual([topic.Uri, topic.Uri]), module + ": «?» e F1 anche in sola lettura");
        Check(host.Modified == 0, module + ": l'aiuto non modifica il foglio");
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    // ---------------------------------------------------------------- 6. automation names

    private static async Task AutomationNames(string root, SheetEditorFactory editors)
    {
        foreach (var (module, id) in new[] { (Concrete, "default"), (Concrete, "ec2-riduzioni-tolleranza"), (Concrete, "legacy-tre-esposizioni"), (Steel, "default"), (Steel, "storico-feb38k-eps") })
        {
            var data = MaterialScenarios.FixtureData(root, module, id);
            using var host = new Hosted(editors.Open(module, data));
            await host.Expand();
            static HashSet<string> Names(DependencyObject root) => Visual(root).OfType<UIElement>().Where(e => e.IsVisible)
                .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
            var before = HistoricAutomationNames[(module, id)].ToHashSet(StringComparer.Ordinal); var after = Names(host.Root);
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Check(missing.Length == 0, $"{module}/{id}: nomi di automazione della vista storica conservati ({before.Count}: {string.Join(", ", before.Order(StringComparer.Ordinal))})"
                + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
            Log.Add($"   nomi nuovi ({module}/{id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
        }
    }

    // ---------------------------------------------------------------- 7. read-only

    /// <summary>Which controls accept the user before and after InspectRevision, in the new views and as in the historic views (kinds and keys).</summary>
    private static async Task ReadOnly(string root, SheetEditorFactory editors)
    {
        foreach (var (module, id) in new[] { (Concrete, "default"), (Concrete, "ec2-riduzioni-tolleranza"), (Steel, "default"), (Steel, "personalizzato-incrudente") })
        {
            var data = MaterialScenarios.FixtureData(root, module, id);
            using var host = new Hosted(editors.Open(module, data));
            await host.Expand();
            var side = new ViewSide(host, module);
            var (before0, before) = HistoricReadOnly[(module, id)];
            string after0 = State(host.Root, NewKey);
            Check(before0 == after0, $"{module}/{id}: prima della sola lettura stessi controlli liberi e bloccati della vista storica");
            host.Editor.InspectRevision();
            await host.Settle();
            string after = State(host.Root, NewKey);
            Log.Add($"   sola lettura {module}/{id}, storica: " + before);
            Log.Add($"   sola lettura {module}/{id}, nuova:   " + after);
            Check(before == after, $"{module}/{id}: sola lettura, stessi controlli bloccati e liberi della vista storica");
            string preview = host.Editor.PreviewData.ToJsonString(J.Options), stored = host.Editor.Data.ToJsonString(J.Options);
            if (module == Concrete)
            {
                string shown = side.Box("diameter").Text;
                side.Box("diameter").Text = "9"; side.Option("rough").IsChecked = side.Option("rough").IsChecked != true;
                side.Choice("life").SelectedItem = "100 anni"; side.Bound("Classes").SelectedItem = "C20/25"; side.Bound("Exposures").SelectedItem = "XA3";
                await host.Settle();
                Check(host.Editor.PreviewData.ToJsonString(J.Options) == preview && host.Editor.Data.ToJsonString(J.Options) == stored && host.Modified == 0
                    && side.Box("diameter").Text == shown, module + ": digitazione e scelte rifiutate, la vista mostra di nuovo i dati");
            }
            else
            {
                string shown = side.Box("gamma_s").Text;
                side.Box("gamma_s").Text = "1"; side.Named<TextBox>("Reference").Text = "Altro"; side.Bound("Types").SelectedItem = "B450A";
                await host.Settle();
                Check(host.Editor.Data.ToJsonString(J.Options) == stored && host.Modified == 0 && side.Box("gamma_s").Text == shown,
                    module + ": digitazione e scelte rifiutate, la vista mostra di nuovo i dati");
            }
        }
    }

    private static string State(DependencyObject root, Func<FrameworkElement, string?> key) => string.Join(" ", Visual(root).OfType<FrameworkElement>().Where(e => e.IsVisible)
        .Select(e => e switch
        {
            TextBox t when t.TemplatedParent is not Control => "testo " + key(t) + (t.IsReadOnly || !t.IsEnabled ? " bloccato" : " libero"),
            ComboBox c => "scelta " + key(c) + (c.IsEnabled ? " libera" : " bloccata"),
            CheckBox c => "spunta " + key(c) + (c.IsEnabled ? " libera" : " bloccata"),
            DataGrid g => "tabella " + key(g) + (g.IsReadOnly ? " sola lettura" : " modificabile"),
            _ => null
        }).Where(s => s is not null).Order(StringComparer.Ordinal));

    private static string? NewKey(FrameworkElement e)
    {
        if (e is TextBox or ComboBox or CheckBox && e.DataContext is FieldViewModel field) return field.Key;
        if (e is ComboBox choice)
            return BoundPath(choice, ItemsControl.ItemsSourceProperty) switch
            {
                "Classes" => "classe", "Exposures" => "esposizione", "DeviationValues" => ConcreteMaterialSheetViewModel.DeviationValueKey, "Types" => "tipo", _ => "altro"
            };
        if (e is TextBox { Name: "Reference" }) return "riferimento";
        return e is TextBox or DataGrid ? "valore" : "altro";
    }

    // ---------------------------------------------------------------- 8. interactive bench

    /// <summary>
    /// ANTHEA.exe --interactive --module-impl mat_calcestruzzo=mvvm --module-impl mat_acciaio_armatura=mvvm (UiTests, manual tests before the
    /// switch): the factory of the composition root with the bench choices opens the MVVM material sheets with the services of the product.
    /// </summary>
    private static void InteractiveBench(string root)
    {
        var composition = new DesktopComposition(() => null);
        var factory = ModuleHostBench.SheetEditors([ModuleHostBench.Interactive, ModuleHostBench.Argument, Concrete + "=mvvm", ModuleHostBench.Argument, Steel + "=mvvm"], composition);
        using (var concrete = factory.Open(Concrete, MaterialScenarios.FixtureData(root, Concrete, "default")))
            Check(concrete is MvvmSheetEditor { Sheet: ConcreteMaterialSheetViewModel sheet } && sheet.Inputs.WikiNavigator is WpfWikiNavigator,
                "Banco interattivo: calcestruzzo MVVM con i servizi del prodotto (Wiki della finestra)");
        using (var steel = factory.Open(Steel, MaterialScenarios.FixtureData(root, Steel, "default")))
            Check(steel is MvvmSheetEditor { Sheet: RebarMaterialSheetViewModel sheet } && sheet.PropertiesForm.WikiNavigator is WpfWikiNavigator && sheet.StatusCoherent,
                "Banco interattivo: acciaio MVVM con i servizi del prodotto");
        foreach (string module in new[] { Concrete, Steel })
            using (var release = composition.SheetEditors.Open(module, MaterialScenarios.FixtureData(root, module, "default")))
                Check(release is SheetEditor, "Banco interattivo: la Release apre ancora la vista storica di " + module);
    }
}
