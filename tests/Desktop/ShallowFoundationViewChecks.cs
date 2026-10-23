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
using Anthea.FoundationWorkspace;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.PileGroup;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Modules.ShallowFoundation;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.ShallowFoundation;

namespace X.Desktop;

/// <summary>
/// --check-fondazioni-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.9b, proposal F5 section 7.3): the XAML view of the shallow
/// foundations (ShallowFoundationSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the window opens it with the MVVM implementation,
/// in windows outside the screen (never activated, no mouse). Parts:
/// <list type="number">
/// <item>Traces through the view: the 15 scenarios of tests/Shared/ShallowFoundationTrace.cs driven through the controls of the view
/// (text boxes, choices and check boxes of the FieldForms, combo boxes of the index choices, expanders, buttons, cells of the layers,
/// section of the mechanism) and read back from the controls, equal line by line to the traces of the historic view frozen in
/// tests/fixtures/f5-fondazioni/storica.</item>
/// <item>For each of the 11 F3 fixtures of the module: the visit (tabs, expanders without state, three appearances) writes nothing and
/// raises no Modified; the visit of the F3 capture (every expander opened) writes vista_fondazioni as the historic view; after «Calcola»
/// the technical drawings have the same display lists as in the historic view; the exports (report Word, PDF of MigraDoc beside it,
/// JSON of the result) are the same files as those of the historic view (decision T8).</item>
/// <item>Screenshots of the new and the historic view in Chiara, Scuro and Molto scuro (folder schermate).</item>
/// <item>Help (FN9, as today): no «?» label, as in the historic view; F1 on the fields, in the table and elsewhere and «Guida» open the guide
/// of the module through the IWikiNavigator of the services, also in a read-only revision.</item>
/// <item>The controls that FoundationWorkspace.UiChecks finds in the historic view (the field Width, the button «raccogli-input», six
/// graphic tabs, the table of the layers) and its checks of the layout, on the new view; AutomationProperties.Name of the historic view
/// kept by the new one.</item>
/// <item>Read-only revision: the same controls refuse the edits as in the historic view; groups, «Raccogli input», tabs, report and
/// «Calcola» stay usable; the section of the mechanism is refused (FN4).</item>
/// <item>«Invia / carica terreno…» through ISoilTransferDialog (T5): commit, module, data and soil sheets passed to the dialog, copy and
/// replacement sent to the window; the interactive bench.</item>
/// </list>
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static class ShallowFoundationViewChecks
{
    private const string Module = ShallowFoundationScenarios.Module;

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
        // The culture of the captures of the historic view (it-IT), also in the operations that the dispatcher runs outside this flow.
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var wiki = new RecordingWiki();
        var removals = new List<string>();
        var editors = Editors(wiki, (title, question) => { removals.Add(title + ": " + question); return true; });
        try
        {
            await Traces(root, output, editors);
            Check(removals.Count > 0 && removals.All(q => q == "Stratigrafia: Eliminare il sondaggio selezionato e i suoi strati?"),
                "«− Sondaggio» chiede la conferma con il testo della vista storica (" + removals.Count + " domande, risposta sì come nelle tracce)");
            await FixturesAndExports(root, output, editors);
            await Screenshots(root, Path.Combine(output, "schermate"), editors);
            await Help(root, editors, wiki);
            await Identifiers(root, output, editors);
            await ReadOnly(root, editors);
            await SoilTransfer(root);
            InteractiveBench(root);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML delle fondazioni superficiali (MvvmSheetEditor, fuori schermo)", new UTF8Encoding(false));
    }

    /// <summary>The editors of the window with the MVVM foundations, a Wiki navigator that records what it opens and the answer to the removals.</summary>
    private static SheetEditorFactory Editors(IWikiNavigator wiki, Func<string, string, bool>? delete = null, ModuleRegistry? modules = null)
    {
        var bench = ModuleHostBench.Services();
        var services = new PresentationServices(bench.Messages, new ScriptedConfirmations { Delete = delete }, bench.Files, bench.Dialogs, bench.Clipboard, bench.Links, wiki);
        return new SheetEditorFactory(ModuleHostBench.Implementations([ModuleHostBench.Argument, Module + "=mvvm"]), modules ?? DesktopComposition.CreateModules(), services,
            new CalculationLimiter(CalculationLimiter.DefaultMaximum));
    }

    // ---------------------------------------------------------------- hosting

    /// <summary>A sheet in a window outside the screen, laid out at 1400×1050 like the sheet area of the main window.</summary>
    private sealed class Hosted : IDisposable
    {
        private readonly Window window;
        internal ISheetEditor Editor { get; }
        internal int Modified, Reports;

        internal Hosted(ISheetEditor editor, bool dark = false)
        {
            Editor = editor;
            editor.Modified += () => Modified++;
            editor.ReportRequested += () => Reports++;
            window = new Window
            {
                Content = editor.View, Width = 1400, Height = 1050, Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Fondazioni " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
            Realize(Root);
        }

        internal FrameworkElement Root => Editor.View;

        internal ShallowFoundationSheetViewModel Sheet => (ShallowFoundationSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal ShallowFoundationSheetView View => Walk(Root).OfType<ShallowFoundationSheetView>().Single();

        internal FoundationWorkspace Historic => ((SheetEditor)Editor).FoundationForChecks ?? throw new InvalidOperationException("Foglio storico senza la vista delle fondazioni.");

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
            Realize(Root);
        }

        /// <summary>Opens every expander, as the visit of the F3 capture (the groups write vista_fondazioni, as today).</summary>
        internal async Task ExpandAll()
        {
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (var expander in Walk(Root).OfType<Expander>().ToArray()) expander.IsExpanded = true;
                await Settle();
            }
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

    /// <summary>The logical and the visual tree, as the visit of the F3 capture walks it (F3EquivalenceCapture.Walk); without
    /// <paramref name="popups"/> the content of the popups (lists of the combo boxes) is left out.</summary>
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
    /// Every control of the historic view exists, built in code, also where the user does not see it; the XAML view builds the content
    /// that WPF has not laid out yet (closed expanders, collapsed inputs, tabs not shown, rows hidden by the rules of the sheet) only when it
    /// is shown. Here that content is measured outside the visual tree and the templates of its controls are applied, so that the controls
    /// can be read and driven as in the historic trace (a control the user cannot use still does nothing). Nothing is written into the sheet.
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

    private static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    /// <summary>A click as the user makes it (ButtonBase.OnClick: the Click event, then the command), only when the button is usable.</summary>
    private static void Click(ButtonBase button)
    {
        if (!Usable(button)) return;
        typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
    }

    private static string? BoundPath(DependencyObject element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    /// <summary>The data captured by a drawing of FoundationViews.cs (its JsonObject parameter), as the historic trace reads it.</summary>
    private static JsonObject? Captured(object? view) => view is null ? null : view.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(f => f.FieldType == typeof(JsonObject)).GetValue(view) as JsonObject;

    // ---------------------------------------------------------------- the view, read and driven through its controls

    /// <summary>The controls of the new view, found by their bindings and by the field of their FieldForm row.</summary>
    private sealed class ViewSide(Hosted host)
    {
        private IEnumerable<DependencyObject> All => Walk(host.Root);

        internal T Editor<T>(string key) where T : FrameworkElement
        {
            var found = All.OfType<T>().Where(e => e.DataContext is FieldViewModel f && f.Key == key && e.TemplatedParent is not Control).ToArray();
            return found.Length == 1 ? found[0] : throw new InvalidOperationException($"Controllo {typeof(T).Name} del campo {key}: {found.Length} trovati.");
        }

        internal bool Has<T>(string key) where T : FrameworkElement => All.OfType<T>().Any(e => e.DataContext is FieldViewModel f && f.Key == key && e.TemplatedParent is not Control);

        /// <summary>The row of a FieldForm field (Row of FieldForm.xaml): its visibility follows the rules of the sheet.</summary>
        internal Grid Row(string key) => All.OfType<Grid>().Single(g => g.Name == "Row" && g.DataContext is FieldViewModel f && f.Key == key);

        private T Named<T>(string name, string key) where T : FrameworkElement
        {
            var found = All.OfType<T>().Where(t => t.Name == name && t.DataContext is FieldViewModel f && f.Key == key).ToArray();
            return found.Length == 1 ? found[0] : throw new InvalidOperationException($"{name} del campo {key}: {found.Length} trovati.");
        }

        /// <summary>The combo box of an index choice (ChoiceRow) and its row.</summary>
        internal (ComboBox Choice, Grid Row)? IndexChoice(string key)
        {
            var choice = All.OfType<ComboBox>().SingleOrDefault(c => c.DataContext is ShallowFoundationChoiceViewModel v && v.Key == key);
            if (choice is null) return null;
            var row = All.OfType<Grid>().Single(g => g.Name == "ChoiceRow" && ReferenceEquals(g.DataContext, choice.DataContext));
            return (choice, row);
        }

        internal Expander Group(string key)
        {
            string path = "Groups[" + ShallowFoundationScenarios.GroupKeys.ToList().IndexOf(key).ToString(CultureInfo.InvariantCulture) + "].IsExpanded";
            return All.OfType<Expander>().Single(e => BoundPath(e, Expander.IsExpandedProperty) == path);
        }

        internal Button Command(string path) => All.OfType<Button>().Single(b => BoundPath(b, ButtonBase.CommandProperty) == path);

        internal Button Collect => All.OfType<Button>().Single(b => Equals(b.Tag, "raccogli-input"));

        /// <summary>The editor of a cell of the layers: the control of the row bound to the key.</summary>
        private FrameworkElement Cell(ShallowFoundationSheetView view, JsonRowViewModel row, string key) => Walk(view.Layers).OfType<FrameworkElement>()
            .Single(e => ReferenceEquals(e.DataContext, row) && (e is TextBox t && BoundPath(t, TextBox.TextProperty) == "[" + key + "]"
                || e is ComboBox c && BoundPath(c, Selector.SelectedItemProperty) == "[" + key + "]"));

        internal async Task Apply(ShallowFoundationStep step)
        {
            var view = host.View;
            switch (step.Kind)
            {
                case ShallowFoundationStep.Commit: host.Editor.Commit(); break;
                case ShallowFoundationStep.Calculate: Click(Command("CalculateCommand")); break;
                case ShallowFoundationStep.ReadOnly: host.Editor.InspectRevision(); break;
                case ShallowFoundationStep.Field:
                {
                    var box = Editor<TextBox>(step.Key!);
                    if (Usable(box) && Usable(Row(step.Key!)) && !box.IsReadOnly) box.Text = step.Text;
                    break;
                }
                case ShallowFoundationStep.Choice:
                    if (IndexChoice(step.Key!) is var (choice, choiceRow)) { if (Usable(choice) && Usable(choiceRow)) choice.SelectedItem = step.Text; }
                    else
                    {
                        var combo = Editor<ComboBox>(step.Key!);
                        if (Usable(combo) && Usable(Row(step.Key!))) combo.SelectedItem = step.Text;
                    }
                    break;
                case ShallowFoundationStep.Option:
                {
                    var box = Editor<CheckBox>(step.Key!);
                    if (Usable(box) && Usable(Row(step.Key!))) box.IsChecked = box.IsChecked != true;
                    break;
                }
                case ShallowFoundationStep.Group: { var group = Group(step.Key!); group.IsExpanded = !group.IsExpanded; break; }
                case ShallowFoundationStep.Collect: Click(Collect); break;
                case ShallowFoundationStep.Survey: if (Usable(view.Survey)) view.Survey.SelectedIndex = step.Index; break;
                case ShallowFoundationStep.AddSurvey: Click(Command("AddSurveyCommand")); break;
                case ShallowFoundationStep.RemoveSurvey: Click(Command("RemoveSurveyCommand")); break;
                case ShallowFoundationStep.Layer:
                {
                    var grid = view.Layers;
                    if (grid.IsReadOnly || !Usable(grid)) break;
                    // The cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged), as the historic table.
                    var cell = Cell(view, (JsonRowViewModel)grid.Items[step.Row], step.Key!);
                    if (cell is TextBox text) text.Text = step.Text;
                    else ((ComboBox)cell).SelectedItem = step.Text;
                    break;
                }
                case ShallowFoundationStep.AddLayer: Click(Command("AddLayerCommand")); break;
                case ShallowFoundationStep.RemoveLayer:
                {
                    var row = view.Layers.Items[step.Row];
                    if (Walk(view.Layers).OfType<Button>().SingleOrDefault(b => ReferenceEquals(b.DataContext, row) && Equals(b.Content, "[−]")) is { } remove) Click(remove);
                    break;
                }
                case ShallowFoundationStep.Mechanism:
                    // The section exists only with the graphics of a result, in the «Rottura» tab.
                    if (view.MechanismAxis.Parent is UIElement { Visibility: Visibility.Visible } && Usable(view.MechanismAxis)) view.MechanismAxis.SelectedIndex = step.Index;
                    break;
                case ShallowFoundationStep.Report: Click(Command("ReportCommand")); break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            await host.Settle();
        }

        internal ShallowFoundationTraceState State()
        {
            var view = host.View;
            var data = host.Editor.Data;
            var trace = new ShallowFoundationTraceState
            {
                Data = data, Modified = host.Modified, ReportRequests = host.Reports, Result = host.Editor.Result, State = view.Status.Text,
                Editable = !Editor<TextBox>("Width").IsReadOnly,
                InputsCollapsed = view.InputScroll.Visibility != Visibility.Visible, CollectText = (string)Collect.Content,
                Surveys = ((IEnumerable)view.Survey.ItemsSource).Cast<string>().ToArray(), SelectedSurvey = view.Survey.SelectedIndex,
                Layers = view.Layers.Items.Cast<JsonRowViewModel>().Select(r => ShallowFoundationTraceState.WithoutColour(r.Values)).ToList(),
                HalfSpace = Editor<CheckBox>("strato_portante_semiinfinito").IsChecked == true,
                Headline = view.Headline.Text, HasDetails = view.Details.Visibility == Visibility.Visible,
                Summary = view.Summary.Items.Cast<string>().ToList(),
                Coefficients = ((IEnumerable)view.Coefficients.ItemsSource).Cast<string[]>().ToList(),
                CoefficientColumns = view.Coefficients.Columns.Select(c => (string)c.Header).ToArray(),
                Notes = view.Notes.Items.Cast<string>().ToList(),
                InputDrawings = ReferenceEquals(view.InputSoil.Data, data) && ReferenceEquals(view.InputPlan.Data, data),
                SoilDrawing = view.SoilHost.Content is FoundationSoilView soil && ReferenceEquals(soil.Data, data),
            };
            if (view.Additive.Visibility == Visibility.Visible)
            {
                trace.AdditiveTitle = view.AdditiveTitle.Text;
                trace.Additive = ((IEnumerable)view.Additive.ItemsSource).Cast<string[]>().ToList();
                trace.AdditiveColumns = view.Additive.Columns.Select(c => (string)c.Header).ToArray();
            }
            foreach (string key in ShallowFoundationScenarios.FieldKeys) trace.Fields.Add(FieldState(key));
            foreach (string key in ShallowFoundationScenarios.GroupKeys) trace.Groups.Add((key, Group(key).IsExpanded));
            ReadGraphics(view, trace);
            return trace;
        }

        private ShallowFoundationFieldState FieldState(string key)
        {
            if (IndexChoice(key) is var (choice, choiceRow))
                return new(key, "scelta", choiceRow.Children.OfType<TextBlock>().Single(t => t.Name == "ChoiceLabel").Text, "", choice.SelectedItem as string ?? "",
                    ((IEnumerable)choice.ItemsSource).Cast<string>().ToArray(), choiceRow.Visibility == Visibility.Visible);
            bool visible = Row(key).Visibility == Visibility.Visible;
            string unit = Named<TextBlock>("UnitText", key).Text;
            if (Has<TextBox>(key)) return new(key, "campo", Named<TextBlock>("LabelText", key).Text, unit, Editor<TextBox>(key).Text, null, visible);
            if (Has<ComboBox>(key))
            {
                var combo = Editor<ComboBox>(key);
                return new(key, "scelta", Named<TextBlock>("LabelText", key).Text, unit, combo.SelectedItem as string ?? "", ((IEnumerable)combo.ItemsSource).Cast<string>().ToArray(), visible);
            }
            return new(key, "opzione", Named<TextBlock>("CheckLabel", key).Text, unit, PileGroupTrace.YesNo(Editor<CheckBox>(key).IsChecked == true), null, visible);
        }

        private void ReadGraphics(ShallowFoundationSheetView view, ShallowFoundationTraceState trace)
        {
            var messages = All.OfType<TextBlock>().Where(t => Equals(t.Tag, "graphics-message")).ToArray();
            if (messages.Length != 4) throw new InvalidOperationException("Messaggi delle schede dei risultati: " + messages.Length);
            if (messages.All(m => m.Visibility == Visibility.Visible))
            {
                var texts = messages.Select(m => m.Text).Distinct().ToArray();
                if (texts.Length != 1) throw new InvalidOperationException("Messaggi diversi nelle schede dei risultati.");
                trace.GraphicsMessage = texts[0];
                return;
            }
            if (messages.Any(m => m.Visibility == Visibility.Visible)) throw new InvalidOperationException("Schede dei risultati in parte senza grafici.");
            trace.Graphics = Captured(view.AreaHost.Content);
            if (!ReferenceEquals(Captured(view.PressureHost.Content), trace.Graphics)) throw new InvalidOperationException("Pressioni di un altro risultato.");
            trace.PressureText = view.PressureText.Text;
            trace.ContactText = view.ContactText.Text;
            trace.MechanismAxes = ((IEnumerable)view.MechanismAxis.ItemsSource).Cast<string>().ToArray();
            trace.MechanismAxis = view.MechanismAxis.SelectedIndex;
            trace.Mechanism = Captured(view.MechanismHost.Content);
            trace.MechanismText = view.MechanismText.Text;
        }
    }

    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in ShallowFoundationScenarios.All(root))
        {
            var trace = new List<string>();
            var host = new Hosted(editors.Open(Module, ShallowFoundationScenarios.FixtureData(root, scenario.Fixture)));
            try
            {
                await host.Settle();
                Check(host.Modified == 0, scenario.Name + ": apertura senza Modified");
                var side = new ViewSide(host);
                trace.AddRange(side.State().Lines("apertura", compareFrozenVersion: true));
                foreach (var step in scenario.Steps)
                {
                    if (step.Kind == ShallowFoundationStep.Reopen)
                    {
                        var data = host.Editor.Data;
                        host.Dispose();
                        host = new Hosted(editors.Open(Module, data));
                        await host.Settle();
                        side = new ViewSide(host);
                    }
                    else await side.Apply(step);
                    trace.AddRange(side.State().Lines(step.ToString(), compareFrozenVersion: true));
                }
            }
            finally { host.Dispose(); }
            PileGroupTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(ShallowFoundationScenarios.Reference(root), scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? PileGroupTrace.FirstDifference(PileGroupTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == 15, $"Tracce della vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe" + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    // ---------------------------------------------------------------- 2. visit, drawings and exports

    private static async Task FixturesAndExports(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        DrawingsFolder = Path.Combine(output, "disegni");
        Directory.CreateDirectory(DrawingsFolder);
        var normalizer = new TextNormalizer();
        var fixtures = ShallowFoundationScenarios.Fixtures(root);
        Check(fixtures.Count == 11, "Fixture F3 delle fondazioni: " + fixtures.Count);
        int drawings = 0, exported = 0;
        foreach (string id in fixtures)
        {
            var data = ShallowFoundationScenarios.FixtureData(root, id);
            using var mvvm = new Hosted(editors.Open(Module, data));
            using var historic = new Hosted(new SheetEditor(Module, data));
            Check(mvvm.Editor is MvvmSheetEditor && historic.Editor is SheetEditor, id + ": editor MVVM e storico");
            string opened = mvvm.Editor.Data.ToJsonString(J.Options);
            Check(opened == historic.Editor.Data.ToJsonString(J.Options), id + ": dati all'apertura uguali alla vista storica, nell'ordine in memoria");
            // The visit of the user: every tab, the expanders without saved state, the three appearances.
            var view = mvvm.View;
            for (int i = 0; i < view.GraphicsTabs.Items.Count; i++) { view.GraphicsTabs.SelectedIndex = i; await mvvm.Settle(); }
            view.GraphicsTabs.SelectedIndex = 0;
            foreach (var expander in Walk(mvvm.Root).OfType<Expander>().Where(e => BindingOperations.GetBinding(e, Expander.IsExpandedProperty) is null).ToArray()) expander.IsExpanded = true;
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == opened && mvvm.Modified == 0,
                id + ": la visita (schede, espansori senza stato, tre aspetti) non scrive e non solleva Modified");
            // The visit of the F3 capture opens the groups too: the same writes of vista_fondazioni as the historic view.
            await mvvm.ExpandAll(); await historic.ExpandAll();
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == historic.Editor.Data.ToJsonString(J.Options) && mvvm.Modified == historic.Modified,
                $"{id}: gruppi aperti come nella visita F3, stesse scritture di vista_fondazioni ({mvvm.Modified} Modified) della vista storica");
            Click(new ViewSide(mvvm).Command("CalculateCommand")); await mvvm.Settle();
            await historic.Editor.CalculateAsync(); await historic.Settle();
            foreach (var host in new[] { mvvm, historic }) host.Editor.Commit();
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == historic.Editor.Data.ToJsonString(J.Options) && mvvm.Editor.HasResults == historic.Editor.HasResults
                && (mvvm.Editor.Result is null ? historic.Editor.Result is null : JsonNode.DeepEquals(mvvm.Editor.Result, historic.Editor.Result)),
                id + ": dati, stato e risultato dopo «Calcola» uguali");
            drawings += SameDrawings(id, mvvm, historic);
            if (!historic.Editor.HasResults)
            {
                Check(Fails(() => mvvm.Editor.ExportResult(Path.Combine(folder, id + "-mvvm.json")), out string a)
                    && Fails(() => historic.Editor.ExportResult(Path.Combine(folder, id + "-storica.json")), out string b) && a == b,
                    id + ": senza risultato l'esportazione dei risultati è rifiutata con lo stesso messaggio («" + a + "»)");
                Check(Fails(() => mvvm.Editor.ExportReport(Path.Combine(folder, id + "-mvvm.docx"), "x", []), out string c)
                    && Fails(() => historic.Editor.ExportReport(Path.Combine(folder, id + "-storica.docx"), "x", []), out string d) && c == d,
                    id + ": senza risultato il report è rifiutato con lo stesso messaggio («" + c + "»)");
                continue;
            }
            string title = "Fixture F3 · " + Module + " · " + id;
            foreach (var (host, name) in new[] { (mvvm, "mvvm"), (historic, "storica") })
            {
                host.Editor.ExportResult(Path.Combine(folder, $"{id}-{name}.json"));
                host.Editor.ExportReport(Path.Combine(folder, $"{id}-{name}.docx"), title, []);
            }
            Check(File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.json")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, id + "-storica.json"))),
                id + ": JSON del risultato uguale byte per byte");
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, id + "-storica.docx")), normalizer);
            Check(part is null, id + ": report Word uguale in ogni parte (date del documento normalizzate)" + (part is null ? "" : ", diversa " + part));
            byte[] pdf = File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.pdf"));
            Check(SamePdf(pdf, File.ReadAllBytes(Path.Combine(folder, id + "-storica.pdf"))), id + ": PDF affiancato uguale a quello della vista storica (date e identificativi normalizzati)");
            Check(SamePdf(pdf, FoundationReport.Pdf(title, mvvm.Editor.Result!)), id + ": PDF scritto da FoundationReport.Pdf (MigraDoc), non ricavato dal DOCX (T8)");
            exported++;
        }
        Log.Add($"   disegni confrontati: {drawings}; fixture esportate: {exported}");
    }

    /// <summary>Where the display lists of two different drawings are written, to read the difference.</summary>
    private static string? DrawingsFolder;

    private static bool Fails(Action action, out string message)
    {
        try { action(); message = ""; return false; }
        catch (InvalidOperationException error) { message = error.Message; return true; }
    }

    /// <summary>
    /// The technical drawings of the two views (FoundationViews.cs, in the order of their tabs) drawn at the same sizes: the same types and the
    /// same display lists, primitive by primitive. The new view hosts the drawings unchanged, so equal lists mean equal data.
    /// </summary>
    private static int SameDrawings(string id, Hosted mvvm, Hosted historic)
    {
        var a = Walk(mvvm.Root).OfType<FoundationPlot>().ToArray();
        var b = Walk(historic.Root).OfType<FoundationPlot>().ToArray();
        Check(a.Select(d => d.GetType().Name).SequenceEqual(b.Select(d => d.GetType().Name)),
            $"{id}: disegni tecnici della vista nuova uguali per tipo e ordine a quelli della storica ({string.Join(", ", a.Select(d => d.GetType().Name))})");
        int compared = 0;
        foreach (var (width, height) in new[] { (640, 420), (900, 600) })
            for (int i = 0; i < a.Length; i++)
            {
                var (x, y) = (DisplayList(Copy(a[i]), width, height), DisplayList(Copy(b[i]), width, height));
                if (!x.SequenceEqual(y) && DrawingsFolder is { } folder)
                {
                    File.WriteAllLines(Path.Combine(folder, $"{id}-{i}-{width}-mvvm.txt"), x, new UTF8Encoding(false));
                    File.WriteAllLines(Path.Combine(folder, $"{id}-{i}-{width}-storica.txt"), y, new UTF8Encoding(false));
                }
                Check(x.SequenceEqual(y),$"{id}: {a[i].GetType().Name} [{i}] {width}×{height}: stesso elenco di disegno ({x.Count} primitivi)");
                compared++;
            }
        return compared;
    }

    /// <summary>
    /// A new drawing of the same type with the data the hosted one received (the parameters of its constructor, or Data), outside every
    /// tree: drawn at the same sizes, the two views give their drawings the same data exactly when the display lists are equal.
    /// </summary>
    private static FoundationPlot Copy(FoundationPlot hosted)
    {
        var type = hosted.GetType();
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var constructor = type.GetConstructors(Members).Single();
        var fields = type.GetFields(Members);
        var copy = (FoundationPlot)constructor.Invoke(constructor.GetParameters().Select(p => fields.Single(f => f.Name == "<" + p.Name + ">P").GetValue(hosted)).ToArray());
        if (type.GetProperty("Data", Members) is { } data) data.SetValue(copy, data.GetValue(hosted));
        return copy;
    }

    /// <summary>The display list of a drawing laid out at the given size: groups, shapes (geometry, fill, pen) and texts with their position.</summary>
    private static List<string> DisplayList(FrameworkElement drawing, double width, double height)
    {
        drawing.Width = width; drawing.Height = height;
        drawing.Measure(new Size(width, height)); drawing.Arrange(new Rect(0, 0, width, height));
        var lines = new List<string>();
        if (VisualTreeHelper.GetDrawing(drawing) is { } content) Write(content, lines);
        return lines;

        static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        static string P(Point p) => N(p.X) + "," + N(p.Y);
        static string B(Brush? brush) => brush switch { null => "-", SolidColorBrush s => s.Color.ToString(CultureInfo.InvariantCulture), _ => brush.GetType().Name };
        static string Pen(Pen? pen) => pen is null ? "-" : B(pen.Brush) + " " + N(pen.Thickness) + (pen.DashStyle is { Dashes.Count: > 0 } dash ? " tratto " + string.Join(";", dash.Dashes.Select(N)) : "");
        static string G(Geometry? geometry) => geometry is null ? "-" : PathGeometry.CreateFromGeometry(geometry).ToString(CultureInfo.InvariantCulture);
        static void Write(Drawing node, List<string> lines)
        {
            switch (node)
            {
                case DrawingGroup group:
                    lines.Add("gruppo " + N(group.Opacity) + " " + G(group.ClipGeometry));
                    foreach (var child in group.Children) Write(child, lines);
                    lines.Add("fine");
                    break;
                case GeometryDrawing shape: lines.Add("forma " + G(shape.Geometry) + " " + B(shape.Brush) + " " + Pen(shape.Pen)); break;
                case GlyphRunDrawing text:
                    lines.Add("testo " + new string(text.GlyphRun.Characters.ToArray()) + " " + P(text.GlyphRun.BaselineOrigin) + " " + N(text.GlyphRun.FontRenderingEmSize) + " " + B(text.ForegroundBrush));
                    break;
                default: lines.Add(node.GetType().Name); break;
            }
        }
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

    /// <summary>Two PDF files equal but for what changes at every rendering: dates of creation and modification (also in the XMP
    /// metadata), identifiers of the file and of the XMP document, random tags of the embedded font subsets (six capital letters before «+»).</summary>
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

    private static async Task Screenshots(string root, string folder, SheetEditorFactory editors)
    {
        Directory.CreateDirectory(folder);
        var cases = new[] { "confirmed-actions", "meyerhof-eccentrica-input-raccolti", "vesic-circolare-due-sondaggi" };
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            foreach (string id in cases)
                foreach (var (kind, open) in new (string, Func<ISheetEditor>)[] { ("vista", () => editors.Open(Module, ShallowFoundationScenarios.FixtureData(root, id))),
                    ("storica", () => new SheetEditor(Module, ShallowFoundationScenarios.FixtureData(root, id))) })
                {
                    using var host = new Hosted(open(), mode != AppAppearance.Light);
                    await host.Editor.CalculateAsync();
                    // The first fixture with every expander open; the others as saved (inputs collapsed, groups closed).
                    if (id == "confirmed-actions") await host.ExpandAll();
                    var tabs = Walk(host.Root).OfType<TabControl>().First();
                    tabs.SelectedIndex = id == "confirmed-actions" ? 0 : id == "vesic-circolare-due-sondaggi" ? 3 : 2;
                    await host.Settle();
                    File.WriteAllBytes(Path.Combine(folder, $"{kind}-{id}-{suffix}.png"), host.Png());
                    written++;
                }
        }
        Appearance.Set(AppAppearance.Light, false);
        Check(written == 18, "Schermate della vista nuova e della storica nei tre aspetti: " + written + " PNG in " + folder);
    }

    // ---------------------------------------------------------------- 4. help

    private static async Task Help(string root, SheetEditorFactory editors, RecordingWiki wiki)
    {
        var data = ShallowFoundationScenarios.FixtureData(root, "vesic-circolare-due-sondaggi");
        string guide = Anthea.Wiki.WikiCatalog.ForModule(Module)?.Id ?? throw new InvalidOperationException("Guida delle fondazioni assente nella Wiki");
        using var historic = new Hosted(new SheetEditor(Module, data));
        using var host = new Hosted(editors.Open(Module, data));
        await historic.ExpandAll(); await host.ExpandAll();
        static string[] Labels(DependencyObject root) => Walk(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
            .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
        string[] before = Labels(historic.Root), after = Labels(host.Root);
        Check(before.SequenceEqual(after) && after.Length == 0, "Etichette «?» visibili uguali alla vista storica (FN9): nessuna, la Wiki non ha argomenti per i campi delle fondazioni");
        var side = new ViewSide(host);
        var view = host.View;
        var cell = Walk(view.Layers).OfType<TextBox>().First(t => t.DataContext is JsonRowViewModel);
        wiki.Shown.Clear();
        F1(side.Editor<TextBox>("Width"));
        F1(side.Editor<TextBox>("profondita_falda"));
        F1(side.Editor<TextBox>("fck_mpa"));
        F1(cell);
        F1(view.GraphicsTabs);
        Click(side.Command("GuideCommand"));
        Check(wiki.Shown.SequenceEqual([guide, guide, guide, guide, guide, guide]),
            "F1 su un numero, sulla falda, sul calcestruzzo, in una cella degli strati e fuori dai campi, e «Guida»: la guida del modulo dal navigatore della Wiki ("
            + string.Join(", ", wiki.Shown) + ")");
        host.Editor.InspectRevision();
        await host.Settle();
        wiki.Shown.Clear();
        F1(side.Editor<TextBox>("Width"));
        F1(side.Editor<TextBox>("profondita_falda"));
        Click(side.Command("GuideCommand"));
        Check(wiki.Shown.SequenceEqual([guide, guide, guide]), "F1 e «Guida» anche in sola lettura");
        Check(host.Modified == historic.Modified, "L'aiuto non modifica il foglio");
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    // ---------------------------------------------------------------- 5. identifiers of FoundationWorkspace.UiChecks and automation names

    /// <summary>
    /// FoundationWorkspace.UiChecks (tests/FoundationWorkspace.Checks, method Ui) finds in the historic view the TextBox with Tag «Width», the
    /// button with Tag «raccogli-input», the first TabControl with six tabs and a DataGrid, and checks the layout; the same on the new view
    /// (the field Width is found by the key of its FieldForm row and keeps the automation name of the label). The suite itself is
    /// rewritten at the switch of the Release, after the test of the user.
    /// </summary>
    private static async Task Identifiers(string root, string output, SheetEditorFactory editors)
    {
        var data = ShallowFoundationScenarios.FixtureData(root, "confirmed-actions");
        using (var host = new Hosted(editors.Open(Module, data)))
        {
            await host.Settle();
            var side = new ViewSide(host);
            var width = side.Editor<TextBox>("Width");
            Check(AutomationProperties.GetName(width) == "Larghezza B [m]", "UiChecks: il campo Width esiste, con il nome di automazione della sua etichetta");
            Click(side.Command("CalculateCommand")); await host.Settle();
            var result = host.Editor.Result;
            Check(result is not null, "UiChecks: risultato presente con la libreria approvata");
            width.Text = "2,5"; await host.Settle();
            Check(host.Editor.Result is null, "UiChecks: una modifica invalida il risultato");
            width.Text = "2"; Click(side.Command("CalculateCommand")); await host.Settle();
            host.View.Details.IsExpanded = true; await host.Settle();
            Click(side.Command("CalculateCommand")); await host.Settle();
            Check(host.View.Details.Visibility == Visibility.Visible && !host.View.Details.IsExpanded && host.Modified == 2,
                "Dettagli della verifica chiusi a ogni calcolo, come l'espansore che la vista storica ricreava; stato della vista non salvato");
            var tabs = Visual(host.Root).OfType<TabControl>().First();
            Check(tabs.Items.Count == 6, "UiChecks: sei schede riservate ai grafici");
            for (int i = 0; i < tabs.Items.Count; i++)
            {
                tabs.SelectedIndex = i; await host.Settle();
                Check(Visual(host.Root).OfType<TextBox>().Any(t => ReferenceEquals(t, width) && t.IsVisible) && Visual(host.Root).OfType<DataGrid>().Any(g => g.IsVisible),
                    "UiChecks: dati e strati restano visibili con il grafico " + i);
            }
            tabs.SelectedIndex = 3; await host.Settle();
            var collect = Visual(host.Root).OfType<Button>().Single(b => Equals(b.Tag, "raccogli-input"));
            var previous = host.Editor.Result; double before = tabs.ActualWidth;
            Click(collect); await host.Settle();
            Check(tabs.ActualWidth > before + 200 && ReferenceEquals(previous, host.Editor.Result), $"UiChecks: «Raccogli input» amplia i grafici ({before:0} → {tabs.ActualWidth:0} px) e conserva il risultato");
            Check(host.Editor.Data["vista_fondazioni"]!["input_raccolti"]!.GetValue<bool>(), "UiChecks: stato dei pannelli salvato nel foglio");
            File.WriteAllBytes(Path.Combine(output, "input-raccolti.png"), host.Png());
            Click(collect); await host.Settle();
        }
        foreach (string id in new[] { "default", "confirmed-actions", "vesic-circolare-due-sondaggi", "hansen-non-drenato-caratteristico" })
        {
            var fixture = ShallowFoundationScenarios.FixtureData(root, id);
            using var historic = new Hosted(new SheetEditor(Module, fixture));
            using var host = new Hosted(editors.Open(Module, fixture));
            await historic.Editor.CalculateAsync(); Click(new ViewSide(host).Command("CalculateCommand"));
            await historic.ExpandAll(); await host.ExpandAll();
            static HashSet<string> Names(DependencyObject root) => Walk(root).OfType<UIElement>().Where(e => e.IsVisible)
                .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
            var before = Names(historic.Root); var after = Names(host.Root);
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Check(missing.Length == 0, $"{id}: nomi di automazione della vista storica conservati ({before.Count}: {string.Join(", ", before.Order(StringComparer.Ordinal))})"
                + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
        }
    }

    private static IEnumerable<DependencyObject> Visual(DependencyObject root)
    {
        var pending = new Stack<DependencyObject>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            for (int i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--) pending.Push(VisualTreeHelper.GetChild(node, i));
        }
    }

    // ---------------------------------------------------------------- 6. read-only

    /// <summary>Field keys of the labels of the historic InputForms and of the columns of the layers, to name the controls of both views alike.</summary>
    private static readonly IReadOnlyDictionary<string, string> LabelKeys = ShallowFoundationFields.Water
        .Concat(ShallowFoundationFields.Concrete(new JsonObject { ["classe_cls"] = "Personalizzato" }, []))
        .Select(f => (f.Label, f.Key)).Concat(ShallowFoundationFields.LayerColumns.Select(f => (f.Label, "strato:" + f.Key)))
        .Concat(ShallowFoundationFields.MethodOptions.Select(f => (f.Label, f.Key)))
        .Append((Anthea.Application.Presentation.ShallowFoundationPresentation.HalfSpaceLabel, "strato_portante_semiinfinito"))
        .ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);

    /// <summary>Which visible controls accept the user, in the historic and in the new view (kind, key and state), in ordinal order.</summary>
    private static string Controls(DependencyObject root, Func<FrameworkElement, string?> key) => string.Join(" ", Walk(root).OfType<FrameworkElement>().Where(e => e.IsVisible)
        .Select(e => e switch
        {
            TextBox t when t.TemplatedParent is not Control => "testo " + key(t) + (t.IsReadOnly || !t.IsEnabled ? " bloccato" : " libero"),
            ComboBox c => "scelta " + key(c) + (c.IsEnabled ? " libera" : " bloccata"),
            CheckBox c => "spunta " + key(c) + (c.IsEnabled ? " libera" : " bloccata"),
            Button b when b.TemplatedParent is not Control => "pulsante " + key(b) + (b.IsEnabled ? " attivo" : " spento"),
            DataGrid g => "tabella " + key(g) + (g.IsReadOnly ? " sola lettura" : " modificabile"),
            _ => null
        }).Where(s => s is not null).Order(StringComparer.Ordinal));

    private static string? Common(FrameworkElement e) => e switch
    {
        Button b => b.Content as string is "Raccogli input" or "Mostra input" ? "raccogli" : (b.Content as string)?.Replace(" ", "_", StringComparison.Ordinal),
        ComboBox c when c.ItemsSource is IEnumerable items && items.Cast<object>().Contains("Sezione X · larghezza B") => "meccanismo",
        _ when AutomationProperties.GetName(e) is { Length: > 0 } name && LabelKeys.TryGetValue(name, out var mapped) && e is not TextBox { DataContext: FieldViewModel } => mapped,
        _ => null
    };

    private static string? HistoricKey(FrameworkElement e) => Common(e) ?? e switch
    {
        DataGrid g => g.Columns.Count switch { 9 => "strati", 6 => "coefficienti", _ => "additive" },
        TextBox t when t.Tag is string tag => tag,
        TextBox t when t.Parent is Grid row && row.Children.OfType<TextBlock>().Any(l => l.Text == "Combinazione") => "nome_combinazione",
        ComboBox c when c.Tag is string tag => tag,
        CheckBox { Content: TextBlock } => "azioni_confermate",
        CheckBox { Content: string text } when LabelKeys.TryGetValue(text, out var key) => key,
        _ => e.Tag as string
    };

    private static string? NewKey(FrameworkElement e) => e switch
    {
        DataGrid g => g.Name switch { "Layers" => "strati", "Coefficients" => "coefficienti", _ => "additive" },
        _ when e.DataContext is FieldViewModel f => f.Key,
        _ => Common(e) ?? e.Tag as string
    };

    private static async Task ReadOnly(string root, SheetEditorFactory editors)
    {
        var data = ShallowFoundationScenarios.FixtureData(root, "vesic-circolare-due-sondaggi");
        using var historic = new Hosted(new SheetEditor(Module, data));
        using var host = new Hosted(editors.Open(Module, data));
        await historic.Editor.CalculateAsync(); Click(new ViewSide(host).Command("CalculateCommand"));
        await historic.ExpandAll(); await host.ExpandAll();
        // The «Rottura» tab, where the section of the mechanism is shown.
        foreach (var h in new[] { historic, host }) { Walk(h.Root).OfType<TabControl>().First().SelectedIndex = 5; await h.Settle(); }
        string editableBefore = Controls(historic.Root, HistoricKey), editableAfter = Controls(host.Root, NewKey);
        Log.Add("   modificabile, storica: " + editableBefore);
        Log.Add("   modificabile, nuova:   " + editableAfter);
        Check(editableBefore == editableAfter, "Foglio modificabile: stessi controlli liberi della vista storica");
        int modified = historic.Modified;
        Check(modified == host.Modified, "Stesse scritture della visita prima della revisione");
        historic.Editor.InspectRevision(); host.Editor.InspectRevision();
        await historic.Settle(); await host.Settle();
        string before = Controls(historic.Root, HistoricKey), after = Controls(host.Root, NewKey);
        Log.Add("   sola lettura, storica: " + before);
        Log.Add("   sola lettura, nuova:   " + after);
        Check(before == after, "Sola lettura: stessi controlli bloccati e liberi della vista storica");
        var side = new ViewSide(host);
        var view = host.View;
        string data0 = host.Editor.Data.ToJsonString(J.Options), shown = side.Editor<TextBox>("Width").Text;
        side.Editor<TextBox>("Width").Text = "9";
        side.Editor<CheckBox>("presenza_falda").IsChecked = !side.Editor<CheckBox>("presenza_falda").IsChecked;
        view.MechanismAxis.SelectedIndex = 1;
        await host.Settle();
        Check(host.Editor.Data.ToJsonString(J.Options) == data0 && host.Modified == modified && side.Editor<TextBox>("Width").Text == shown && view.MechanismAxis.SelectedIndex == 0,
            "Sola lettura: digitazione, spunte e sezione del meccanismo (FN4) rifiutate, la vista mostra di nuovo i dati");
        var group = side.Group("metodo");
        group.IsExpanded = false; Click(side.Collect);
        await host.Settle();
        Check(host.Modified == modified + 2 && host.Editor.HasResults && host.Editor.Data["vista_fondazioni"]!["metodo"]!.GetValue<bool>() == false
            && host.Editor.Data["vista_fondazioni"]!["input_raccolti"]!.GetValue<bool>(), "Sola lettura: gruppi e «Raccogli input» scrivono vista_fondazioni e conservano il risultato (FN3, T6)");
        Click(side.Collect); Click(side.Command("ReportCommand")); Click(side.Command("CalculateCommand"));
        await host.Settle();
        Check(host.Reports == 1 && host.Editor.HasResults, "Sola lettura: report e «Calcola» disponibili");
    }

    // ---------------------------------------------------------------- 7. soil transfer and interactive bench

    /// <summary>The historic dialog behind ISoilTransferDialog, scripted: what it receives and what it answers.</summary>
    private sealed class ScriptedTransfer : ISoilTransferDialog
    {
        internal readonly Queue<SoilTransferChoice?> Answers = new();
        internal readonly List<(string Module, string Data, int Sources)> Calls = [];
        public Task<SoilTransferChoice?> ShowAsync(string module, JsonObject data, IReadOnlyList<SoilSource> sources)
        {
            Calls.Add((module, data.ToJsonString(J.Options), sources.Count));
            return Task.FromResult(Answers.Dequeue());
        }
    }

    private static async Task SoilTransfer(string root)
    {
        var transfer = new ScriptedTransfer();
        // Replace only the module under test; keep every module that the Release opens in MVVM, including Spettri.
        var defaults = DesktopComposition.CreateModules();
        var editors = Editors(new RecordingWiki(), modules: new ModuleRegistry(defaults.SheetModuleIds.Select(id =>
            id == Module ? new ShallowFoundationSheetModule(transfer: transfer) : defaults.SheetModule(id))));
        var data = ShallowFoundationScenarios.FixtureData(root, "vesic-circolare-due-sondaggi");
        using var host = new Hosted(editors.Open(Module, data));
        await host.Settle();
        var copies = new List<(string Module, string Name)>(); var replacements = new List<JsonObject>();
        host.Editor.ModuleCopyRequested += (module, _, name) => copies.Add((module, name));
        host.Editor.SoilReplacementRequested += replacements.Add;
        var sourceData = new JsonObject();
        host.Editor.SoilSources = () => [("Stratigrafia di prova", "geo_stratigrafia", sourceData, "id-1")];
        var button = new ViewSide(host).Command("TransferSoilCommand");
        Check(Equals(button.Content, "Invia / carica terreno…") && button.IsEnabled, "«Invia / carica terreno…» disponibile nel foglio modificabile");
        var copy = new JsonObject { ["copia"] = true }; var replacement = new JsonObject { ["sostituito"] = true };
        transfer.Answers.Enqueue(new SoilTransferChoice(copy, "geo_palo_verticale", "Palo dal terreno"));
        transfer.Answers.Enqueue(new SoilTransferChoice(replacement));
        transfer.Answers.Enqueue(null);
        string before = host.Editor.Data.ToJsonString(J.Options);
        for (int i = 0; i < 3; i++) { Click(button); await host.Settle(); }
        Check(transfer.Calls.Count == 3 && transfer.Calls.All(c => c.Module == Module && c.Data == before && c.Sources == 1),
            "Il dialogo riceve modulo, dati confermati e fogli terreno del progetto, come SheetEditor.OpenSoilTransfer");
        Check(copies.SequenceEqual([("geo_palo_verticale", "Palo dal terreno")]) && replacements.Count == 1 && ReferenceEquals(replacements[0], replacement)
            && host.Modified == 0 && host.Editor.Data.ToJsonString(J.Options) == before,
            "Copia in un nuovo foglio e sostituzione del terreno inviate alla finestra; chiusura senza scelta senza effetti");
        host.Editor.InspectRevision(); await host.Settle();
        Check(!button.IsEnabled, "«Invia / carica terreno…» spento in sola lettura, come sotto RevisionInspection");
    }

    /// <summary>
    /// ANTHEA.exe --interactive --module-impl geo_fondazioni_superficiali=mvvm (UiTests, manual tests before the switch of the Release): the
    /// factory of the composition root with the bench choice opens the MVVM foundations with the services of the product, the others stay
    /// historic; the Release still opens the historic view.
    /// </summary>
    private static void InteractiveBench(string root)
    {
        var composition = new DesktopComposition(() => null);
        var factory = ModuleHostBench.SheetEditors([ModuleHostBench.Interactive, ModuleHostBench.Argument, Module + "=mvvm"], composition);
        using (var foundation = factory.Open(Module, ShallowFoundationScenarios.FixtureData(root, "default")))
            Check(foundation is MvvmSheetEditor { Sheet: ShallowFoundationSheetViewModel sheet } && sheet.ConcreteForm.WikiNavigator is WpfWikiNavigator
                && sheet.TransferSoilCommand.CanExecute(null) && !sheet.HasResults, "Banco interattivo: fondazioni MVVM con i servizi del prodotto (Wiki, trasferimento del terreno), senza calcolo all'apertura");
        using (var other = factory.Open("geo_palo_verticale", Anthea.Application.Modules.ModuleDirectory.CreateData("geo_palo_verticale")))
            Check(other is MvvmSheetEditor, "Banco interattivo: gli altri moduli come nella Release (il palo verticale con la vista MVVM dal 23/10)");
        Check(composition.SheetEditors.Open(Module, ShallowFoundationScenarios.FixtureData(root, "default")) is SheetEditor historic && Dispose(historic),
            "Banco interattivo: la radice di composizione della Release apre ancora la vista storica");
    }

    private static bool Dispose(IDisposable value) { value.Dispose(); return true; }
}
