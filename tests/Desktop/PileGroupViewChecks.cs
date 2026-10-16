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
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.PileGroup;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.PileGroup;

namespace X.Desktop;

/// <summary>
/// --check-pile-group-view &lt;radice&gt; &lt;cartella&gt; (UiTests, F5.4, proposal F5 section 7.3): the XAML view of the horizontal pile
/// group (PileGroupSheetView) hosted by <see cref="MvvmSheetEditor"/>, as the window opens it with the MVVM implementation, in
/// windows outside the screen (never activated, no mouse). Parts:
/// <list type="number">
/// <item>Traces through the view: the scenarios of tests/Shared/PileGroupTrace.cs driven through the controls of the view (text
/// boxes, check boxes and choices of the FieldForm, combo boxes, comparison table, cancelled cell edit) and read back from the
/// controls, equal line by line to the traces of the historic view frozen in tests/fixtures/f5-palificata/storica.</item>
/// <item>For each F3 fixture of the module: the visit (layout, expanders) writes nothing and raises no Modified; the exports
/// (Word + PDF report and JSON of the result) are the same files as those of the historic view.</item>
/// <item>The UI part of HorizontalPileGroup.Checks (before F5.4 on the historic view by reflection): PNG 1400×1050, result
/// calculated, the selection of the comparison through SelectedComparison drives the result shown.</item>
/// <item>Screenshots of the new and the historic view in Chiara, Scuro and Molto scuro (folder schermate).</item>
/// <item>Help: the «?» labels shown are those of the historic view and open their section, F1 on a field opens its section
/// (or the guide of the module), F1 elsewhere the guide of the module, all through the IWikiNavigator of the services.</item>
/// <item>AutomationProperties.Name of the historic view kept by the new one.</item>
/// <item>Read-only revision: the same controls refuse the edits as in the historic view; the comparison and the «?» stay usable.</item>
/// </list>
/// Writes dettaglio.txt and esito.txt («PASS n controlli …»).
/// </summary>
internal static class PileGroupViewChecks
{
    private const string Module = PileGroupScenarios.Module;

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
        // The culture of the captures of the historic view (it-IT without the overrides of the user, who may have changed the decimal
        // separator), also in the operations that the dispatcher runs outside this flow, such as the posted handler of a cancelled cell
        // edit: WPF keeps for the later operations of the thread the culture set inside an operation, not the one of an async flow.
        var culture = CultureInfo.GetCultureInfo("it-IT");
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = culture; });
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var wiki = new RecordingWiki();
        var editors = Editors(wiki);
        try
        {
            await Traces(root, output, editors);
            await FixturesAndExports(root, output, editors);
            await HorizontalPileGroupUi(output, editors);
            await Screenshots(root, Path.Combine(output, "schermate"), editors);
            await Help(root, editors, wiki);
            await AutomationNames(root, editors);
            await ReadOnly(root, editors);
        }
        finally
        {
            Appearance.Set(AppAppearance.Light, false);
            File.WriteAllLines(Path.Combine(output, "dettaglio.txt"), Log, new UTF8Encoding(false));
        }
        Check(Application.Current.Windows.Count == 0, "Finestre della prova chiuse");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli della vista XAML della palificata (MvvmSheetEditor, fuori schermo)", new UTF8Encoding(false));
    }

    /// <summary>The editors of the window with the MVVM pile group and a Wiki navigator that records what it opens.</summary>
    private static SheetEditorFactory Editors(IWikiNavigator wiki)
    {
        var bench = ModuleHostBench.Services();
        var services = new PresentationServices(bench.Messages, bench.Confirmations, bench.Files, bench.Dialogs, bench.Clipboard, bench.Links, wiki);
        return new SheetEditorFactory(ModuleHostBench.Implementations([ModuleHostBench.Argument, Module + "=mvvm"]), DesktopComposition.CreateModules(), services,
            new CalculationLimiter(CalculationLimiter.DefaultMaximum));
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
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Title = "Palificata " + editor.GetType().Name
            };
            Appearance.Watch(window);
            Appearance.SetDark(window, dark);
            window.Show();
            Layout();
        }

        internal FrameworkElement Root => Editor.View;

        internal PileGroupSheetViewModel Sheet => (PileGroupSheetViewModel)((MvvmSheetEditor)Editor).Sheet;

        internal PileGroupSheetView View => Visual(Root).OfType<PileGroupSheetView>().Single();

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

    private static string? BoundPath(FrameworkElement element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    // ---------------------------------------------------------------- the view, read and driven through its controls

    /// <summary>The controls of the new view, found by their bindings and by the field of their FieldForm row.</summary>
    private sealed class ViewSide(Hosted host)
    {
        private DependencyObject Root => host.Root;
        private PileGroupSheetViewModel Sheet => host.Sheet;

        internal TextBox Box(string key) => Visual(Root).OfType<TextBox>().Single(t => t.DataContext is FieldViewModel f && f.Key == key);
        internal CheckBox Option(string key) => Visual(Root).OfType<CheckBox>().Single(c => c.DataContext is FieldViewModel f && f.Key == key);
        internal ComboBox Choice(string itemsPath) => Visual(Root).OfType<ComboBox>().Single(c => BoundPath(c, ItemsControl.ItemsSourceProperty) == itemsPath);
        internal TextBlock Text(string path) => Visual(Root).OfType<TextBlock>().Single(t => ReferenceEquals(t.DataContext, Sheet) && BoundPath(t, TextBlock.TextProperty) == path);
        private TextBlock Named(string name, string key) => Visual(Root).OfType<TextBlock>().Single(t => t.Name == name && t.DataContext is FieldViewModel f && f.Key == key);

        internal async Task Apply(PileGroupStep step)
        {
            var view = host.View;
            switch (step.Kind)
            {
                case PileGroupStep.Commit: host.Editor.Commit(); break;
                case PileGroupStep.Calculate: await host.Editor.CalculateAsync(); break;
                case PileGroupStep.Wait: await Sheet.PendingCalculation; break;
                case PileGroupStep.ReadOnly: host.Editor.InspectRevision(); break;
                case PileGroupStep.Field: Box(step.Key!).Text = step.Text; break;
                case PileGroupStep.Option: var option = Option(step.Key!); option.IsChecked = option.IsChecked != true; break;
                case PileGroupStep.Layout: Choice("Layouts").SelectedItem = step.Text; break;
                case PileGroupStep.Cap: Choice("CapShapes").SelectedItem = step.Text; break;
                case PileGroupStep.Spacing: Choice("SpacingDirections").SelectedIndex = step.Index; break;
                // The cells write the row at every keystroke through the binding of their column (UpdateSourceTrigger=PropertyChanged),
                // as in the historic trace, which also wrote the rows of the table.
                case PileGroupStep.Pile: Write((PileRowViewModel)view.Coordinates.Items[step.Index], ColumnPath(view.Coordinates, step.Key!), step.Text!); break;
                // The new row of the table: the grid adds an empty row to its collection.
                case PileGroupStep.AddPile: Sheet.Piles.Add(new PileRowViewModel()); break;
                case PileGroupStep.RemovePile: Sheet.Piles.RemoveAt(step.Index); break;
                case PileGroupStep.CancelEdit: await CancelCellEdit(view.Coordinates); break;
                case PileGroupStep.Select: view.ComparisonGrid.SelectedIndex = step.Index; break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            await host.Settle();
        }

        private static string ColumnPath(DataGrid grid, string key) => key switch { "Id" => "Id", "X" => "X", "Y" => "Y", _ => throw new ArgumentException("Colonna sconosciuta: " + key) } is var path
            && grid.Columns.OfType<DataGridTextColumn>().Any(c => ((Binding)c.Binding).Path.Path == path) ? path : throw new InvalidOperationException("Colonna assente: " + key);

        private static void Write(object row, string property, string text) => row.GetType().GetProperty(property)!.SetValue(row, text);

        /// <summary>Esc in a coordinate cell: the real edit of the grid is started and cancelled, then the dispatcher runs the posted handler.</summary>
        private async Task CancelCellEdit(DataGrid grid)
        {
            var item = grid.Items[0];
            grid.ScrollIntoView(item);
            host.Layout();
            grid.CurrentCell = new DataGridCellInfo(item, grid.Columns[1]);
            if (!grid.BeginEdit()) throw new InvalidOperationException("Modifica della cella non avviata");
            grid.CancelEdit(DataGridEditingUnit.Cell);
            grid.CancelEdit(DataGridEditingUnit.Row);
            await grid.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        }

        internal PileGroupTraceState State()
        {
            host.Layout();
            var view = host.View;
            var diameter = Box("diametro");
            var trace = new PileGroupTraceState
            {
                Data = host.Editor.Data, Modified = host.Modified, Result = host.Editor.Result, Editable = !diameter.IsReadOnly,
                Title = Text("Title").Text, State = Text("State").Text, Minimum = Text("Minimum").Text, Mean = Text("Mean").Text, Maximum = Text("Maximum").Text,
                Reduction = Text("Reduction").Text, Geometry = Text("Geometry").Text, Source = Text("Source").Text, Notes = Text("Notes").Text,
                Comparison = Rows(view.ComparisonGrid).Select(r => r.Select(c => (string)c!).ToArray()).ToList(),
                SelectedComparison = view.ComparisonGrid.SelectedIndex,
                Details = Rows(view.DetailsGrid).ToList(),
                Piles = Rows(view.Coordinates).Select(r => r.Select(c => (string)c!).ToArray()).ToList(),
                CoordinatesEditable = !view.Coordinates.IsReadOnly, CanAddPiles = view.Coordinates.CanUserAddRows, CoordinateHelp = Text("CoordinateHelp").Text,
                Layout = Choice("Layouts").SelectedItem as string,
                Diameter = diameter.Text,
                Directions = new[] { "X", "Y", "X-", "Y-", "custom" }.Select(k => PileGroupTrace.DirectionLine(k, Option(k).IsChecked == true)).ToList(),
                CustomAngle = Box("angolo_custom").Text, CustomAngleEnabled = Box("angolo_custom").IsEnabled,
                CapShape = Choice("CapShapes").SelectedItem as string, CapEdge = Box("bordo_d").Text,
                Extrapolations = Option("estrapolazioni").IsChecked == true, ProjectedRows = Option("file_proiettate").IsChecked == true,
                SpacingDirection = Sheet.SpacingDirection, SpacingIndex = Choice("SpacingDirections").SelectedIndex,
                ParallelSpacing = Box("interasse_parallelo").Text, TransverseSpacing = Box("interasse_trasversale").Text,
            };
            var note = Text("GeneratorNote");
            trace.GeneratorNote = note.IsVisible ? note.Text : "";
            // The fields of the layout, then the rotation, in the order of their FieldForm rows.
            foreach (var form in new[] { Sheet.GeneratorForm, Sheet.RotationForm }.OfType<FormViewModel>())
                foreach (var field in form.Fields)
                    trace.Generator.Add(field.Kind == FieldKind.Bool ? PileGroupTrace.OptionLine(field.Key, Named("CheckLabel", field.Key).Text, Option(field.Key).IsChecked == true)
                        : PileGroupTrace.FieldLine(field.Key, Named("LabelText", field.Key).Text, Box(field.Key).Text, Box(field.Key).IsEnabled));
            object? Plan(string name) => typeof(PileGroupPlan).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view.PlanDrawing);
            trace.PlanShown = Plan("data") is not null; trace.PlanSelected = (JsonNode?)Plan("result"); trace.PlanCap = (JsonArray?)Plan("cap");
            return trace;
        }

        /// <summary>The cells of the rows of a table, read through the bindings of its columns (not through the ViewModel's own lists).</summary>
        private static IEnumerable<object?[]> Rows(DataGrid grid)
        {
            var paths = grid.Columns.OfType<DataGridTextColumn>().Select(c => ((Binding)c.Binding).Path.Path).ToArray();
            foreach (var item in grid.Items.Cast<object>().Where(i => i != CollectionView.NewItemPlaceholder))
                yield return paths.Select(p => item.GetType().GetProperty(p)!.GetValue(item)).ToArray();
        }
    }

    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in PileGroupScenarios.All(root))
        {
            var trace = new List<string>();
            var host = new Hosted(editors.Open(Module, PileGroupScenarios.FixtureData(root, scenario.Fixture)));
            try
            {
                await host.Expand();
                Check(host.Modified == 0, scenario.Name + ": apertura e visita senza Modified");
                var side = new ViewSide(host);
                trace.AddRange(side.State().Lines("apertura"));
                foreach (var step in scenario.Steps)
                {
                    if (step.Kind == PileGroupStep.Reopen)
                    {
                        var data = host.Editor.Data;
                        host.Dispose();
                        host = new Hosted(editors.Open(Module, data));
                        await host.Expand();
                        side = new ViewSide(host);
                    }
                    else await side.Apply(step);
                    trace.AddRange(side.State().Lines(step.ToString()));
                }
            }
            finally { host.Dispose(); }
            PileGroupTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(PileGroupScenarios.Reference(root), scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? PileGroupTrace.FirstDifference(PileGroupTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0, $"Tracce della vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe" + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    // ---------------------------------------------------------------- 2. visit and exports

    private static async Task FixturesAndExports(string root, string output, SheetEditorFactory editors)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        foreach (string id in PileGroupScenarios.Fixtures(root))
        {
            var data = PileGroupScenarios.FixtureData(root, id);
            using var mvvm = new Hosted(editors.Open(Module, data));
            using var historic = new Hosted(new SheetEditor(Module, data));
            Check(mvvm.Editor is MvvmSheetEditor && historic.Editor is SheetEditor, id + ": editor MVVM e storico");
            string opened = mvvm.Editor.Data.ToJsonString(J.Options);
            Check(opened == historic.Editor.Data.ToJsonString(J.Options), id + ": dati all'apertura uguali alla vista storica, nell'ordine in memoria");
            await mvvm.Expand();
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == opened && mvvm.Modified == 0, id + ": la visita (espansori, tre aspetti) non scrive e non solleva Modified");
            await historic.Expand();
            foreach (var host in new[] { mvvm, historic }) { await host.Editor.CalculateAsync(); host.Editor.Commit(); }
            Check(mvvm.Editor.Data.ToJsonString(J.Options) == historic.Editor.Data.ToJsonString(J.Options) && mvvm.Editor.HasResults == historic.Editor.HasResults,
                id + ": dati e stato dopo il calcolo uguali");
            if (!historic.Editor.HasResults)
            {
                Check(Fails(() => mvvm.Editor.ExportResult(Path.Combine(folder, id + "-mvvm.json")), out string a)
                    && Fails(() => historic.Editor.ExportResult(Path.Combine(folder, id + "-storica.json")), out string b) && a == b,
                    id + ": senza risultato l'esportazione è rifiutata con lo stesso messaggio («" + a + "»)");
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
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, id + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, id + "-storica.pdf"))),
                id + ": PDF affiancato uguale (date del documento normalizzate)");
        }
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

    // ---------------------------------------------------------------- 3. the UI part of HorizontalPileGroup.Checks

    /// <summary>
    /// The UI part of supporto/test/HorizontalPileGroup.Checks before F5.4 (reflection on the historic view), on the new view hosted
    /// by MvvmSheetEditor: method 4, offscreen PNG 1400×1050, result calculated, row 6 of the comparison selected through
    /// SelectedComparison drives the method and the direction of the result shown (Davisson along +Y).
    /// </summary>
    private static async Task HorizontalPileGroupUi(string output, SheetEditorFactory editors)
    {
        var data = HorizontalPileGroup.Defaults(); data["metodo"] = 4;
        using var editor = editors.Open(Module, data);
        var view = editor.View;
        view.Measure(new Size(1400, 1050)); view.Arrange(new Rect(0, 0, 1400, 1050)); view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1400, 1050, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(Path.Combine(output, "ui-4.png"))) encoder.Save(file);
        Check(editor.Result is not null, "HorizontalPileGroup.Checks: UI calculated");
        var sheet = (PileGroupSheetViewModel)((MvvmSheetEditor)editor).Sheet;
        Check(sheet.Result!["selezionato"].D("Method") == 4 && sheet.SelectedComparison is { Method: 4 }, "HorizontalPileGroup.Checks: metodo scelto selezionato all'apertura");
        sheet.SelectedComparison = sheet.Comparison[6];
        var selected = editor.Result!;
        Check(selected["selezionato"].S("DirectionId") == "Y" && selected["selezionato"].D("Method") == 0, "HorizontalPileGroup.Checks: selection drives displayed method and direction");
        var grid = Visual(view).OfType<PileGroupSheetView>().Single().ComparisonGrid;
        view.UpdateLayout();
        Check(grid.SelectedIndex == 6 && sheet.Title.StartsWith(HorizontalPileGroupEditor.MethodNames[0], StringComparison.Ordinal), "HorizontalPileGroup.Checks: riga e titolo della selezione nella vista");
        await Task.Yield();
    }

    // ---------------------------------------------------------------- 4. screenshots

    private static async Task Screenshots(string root, string folder, SheetEditorFactory editors)
    {
        Directory.CreateDirectory(folder);
        var cases = new[] { ("default", PileGroupScenarios.FixtureData(root, "default")), ("quinconce-direzioni", PileGroupScenarios.FixtureData(root, "quinconce-direzioni")) };
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            foreach (var (name, data) in cases)
                foreach (var (kind, open) in new (string, Func<ISheetEditor>)[] { ("vista", () => editors.Open(Module, data)), ("storica", () => new SheetEditor(Module, data)) })
                {
                    using var host = new Hosted(open(), mode != AppAppearance.Light);
                    await host.Expand();
                    await host.Settle();
                    File.WriteAllBytes(Path.Combine(folder, $"{kind}-{name}-{suffix}.png"), host.Png());
                    written++;
                }
        }
        Appearance.Set(AppAppearance.Light, false);
        Check(written == 12, "Schermate della vista nuova e della storica nei tre aspetti: " + written + " PNG in " + folder);
    }

    // ---------------------------------------------------------------- 5. help

    private static async Task Help(string root, SheetEditorFactory editors, RecordingWiki wiki)
    {
        var data = PileGroupScenarios.FixtureData(root, "default");
        string guide = Anthea.Wiki.WikiCatalog.ForModule(Module)?.Id ?? throw new InvalidOperationException("Guida della palificata assente nella Wiki");
        var topic = Anthea.Presentation.Help.HelpTopics.ForField("diametro", Module) ?? throw new InvalidOperationException("Argomento del diametro assente");
        using var historic = new Hosted(new SheetEditor(Module, data));
        using var host = new Hosted(editors.Open(Module, data));
        await historic.Expand(); await host.Expand();
        static string[] Labels(DependencyObject root) => Visual(root).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
            .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
        string[] before = Labels(historic.Root), after = Labels(host.Root);
        Check(before.SequenceEqual(after) && after.SequenceEqual(["Wiki: Diametro comune D [m]"]), "Etichette «?» visibili uguali alla vista storica: " + string.Join(", ", after));
        var help = Visual(host.Root).OfType<Button>().Single(b => b.IsVisible && AutomationProperties.GetName(b) == "Wiki: Diametro comune D [m]");
        Check(help.ToolTip as string == topic.Title + "\nApri questa sezione della Wiki", "«?» del diametro: suggerimento dell'argomento");
        wiki.Shown.Clear();
        help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
        var side = new ViewSide(host);
        F1(side.Box("diametro"));
        F1(side.Box("bordo_d"));
        F1(host.View.ComparisonGrid);
        F1(host.View.Coordinates);
        Check(wiki.Shown.SequenceEqual([topic.Uri, topic.Uri, guide, guide, guide]),
            "«?» e F1 dal navigatore della Wiki: argomento del campo, F1 sul campo con argomento, sul campo senza (guida), fuori dai campi (guida): " + string.Join(", ", wiki.Shown));
        host.Editor.InspectRevision();
        await host.Settle();
        wiki.Shown.Clear();
        Check(help.IsEnabled, "«?» utilizzabile in sola lettura");
        help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
        F1(side.Box("diametro"));
        Check(wiki.Shown.SequenceEqual([topic.Uri, topic.Uri]), "«?» e F1 anche in sola lettura");
        Check(host.Modified == 0 && historic.Modified == 0, "L'aiuto non modifica il foglio");
    }

    private static void F1(UIElement target)
    {
        var source = PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("Elemento senza finestra: " + target);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
    }

    // ---------------------------------------------------------------- 6. automation names

    private static async Task AutomationNames(string root, SheetEditorFactory editors)
    {
        foreach (string id in new[] { "default", "quinconce-direzioni", "legacy-pile-group" })
        {
            var data = PileGroupScenarios.FixtureData(root, id);
            using var historic = new Hosted(new SheetEditor(Module, data));
            using var host = new Hosted(editors.Open(Module, data));
            await historic.Expand(); await host.Expand();
            static HashSet<string> Names(DependencyObject root) => Visual(root).OfType<UIElement>().Where(e => e.IsVisible)
                .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
            var before = Names(historic.Root); var after = Names(host.Root);
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Check(missing.Length == 0, $"{id}: nomi di automazione della vista storica conservati ({before.Count}: {string.Join(", ", before.Order(StringComparer.Ordinal))})"
                + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
        }
    }

    // ---------------------------------------------------------------- 7. read-only

    /// <summary>Which controls accept the user after InspectRevision, in the historic and in the new view (types and keys).</summary>
    private static async Task ReadOnly(string root, SheetEditorFactory editors)
    {
        var data = PileGroupScenarios.FixtureData(root, "legacy-pile-group");
        using var historic = new Hosted(new SheetEditor(Module, data));
        using var host = new Hosted(editors.Open(Module, data));
        await historic.Expand(); await host.Expand();
        var side = new ViewSide(host);
        var diameterBackground = side.Box("diametro").Background;
        historic.Editor.InspectRevision(); host.Editor.InspectRevision();
        await historic.Settle(); await host.Settle();
        static string State(DependencyObject root, Func<FrameworkElement, string?> key) => string.Join(" ", Visual(root).OfType<FrameworkElement>().Where(e => e.IsVisible)
            .Select(e => e switch
            {
                TextBox t when t.TemplatedParent is not Control => "testo " + key(t) + (t.IsReadOnly || !t.IsEnabled ? " bloccato" : " libero"),
                ComboBox c => "scelta " + key(c) + (c.IsEnabled ? " libera" : " bloccata"),
                CheckBox c => "spunta " + key(c) + (c.IsEnabled ? " libera" : " bloccata"),
                DataGrid g => "tabella " + key(g) + (g.IsReadOnly && !g.CanUserAddRows && !g.CanUserDeleteRows ? " sola lettura" : " modificabile") + (g.IsEnabled ? " selezionabile" : ""),
                _ => null
            }).Where(s => s is not null).Order(StringComparer.Ordinal));
        static string? HistoricKey(FrameworkElement e) => e switch
        {
            DataGrid g => g.Columns.Count == 3 ? "coordinate" : g.Columns.Count == 7 ? "confronto" : "dettagli",
            ComboBox c => c.ItemsSource is IEnumerable items && items.Cast<object>().Contains("Personalizzata") ? "interassi" : c.ItemsSource is IEnumerable caps && caps.Cast<object>().Contains("Contorno palificata") ? "basamento" : "disposizione",
            _ => e.Tag as string
        };
        static string? NewKey(FrameworkElement e) => e switch
        {
            DataGrid g => g.Name switch { "Coordinates" => "coordinate", "ComparisonGrid" => "confronto", _ => "dettagli" },
            ComboBox c => BoundPath(c, ItemsControl.ItemsSourceProperty) switch { "SpacingDirections" => "interassi", "CapShapes" => "basamento", _ => "disposizione" },
            _ => e.DataContext is FieldViewModel f ? f.Key : null
        };
        string before = State(historic.Root, HistoricKey), after = State(host.Root, NewKey);
        Log.Add("   sola lettura, storica: " + before);
        Log.Add("   sola lettura, nuova:   " + after);
        Check(before == after, "Sola lettura: stessi controlli bloccati e liberi della vista storica");
        Check(ReferenceEquals(side.Box("diametro").Background, diameterBackground), "Sola lettura: fondo dei campi invariato (non è un campo calcolato)");
        string data0 = host.Editor.Data.ToJsonString(J.Options), shown = side.Box("diametro").Text;
        side.Box("diametro").Text = "9";
        side.Option("X-").IsChecked = true;
        await host.Settle();
        Check(host.Editor.Data.ToJsonString(J.Options) == data0 && host.Modified == 0 && side.Box("diametro").Text == shown && side.Option("X-").IsChecked != true,
            "Sola lettura: digitazione e spunte rifiutate, la vista mostra di nuovo i dati");
        host.View.ComparisonGrid.SelectedIndex = 3;
        await host.Settle();
        Check(host.Modified == 1 && host.Sheet.SelectedComparison == host.Sheet.Comparison[3] && host.Editor.Data["metodo"]!.ToString() == host.Sheet.Comparison[3].Method.ToString(CultureInfo.InvariantCulture),
            "Sola lettura: la selezione nel confronto resta disponibile");
    }
}
