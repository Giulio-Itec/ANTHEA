using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Anthea.Application.Modules;
using Anthea.Presentation.Tests;
using X.Core;

namespace X.Desktop;

/// <summary>
/// --check-palo-elastico-historic &lt;radice&gt; &lt;cartella&gt; and --check-palo-elastico-historic-capture &lt;radice&gt; &lt;cartella&gt;
/// (UiTests, F5.8, proposal F5 section 7.2): the scenarios of tests/Shared/ElasticPileTrace.cs on the historic view of the elastic
/// response of the horizontal pile and micropile (<see cref="ElasticPileWorkspace"/> with <see cref="PileReinforcementEditor"/>, the
/// second tab of <see cref="HorizontalWorkspace"/> inside <see cref="SheetEditor"/>), opened as the window opens it, off screen, with the
/// calculation of the elastic response behind the barrier of the trace (<see cref="HeldElasticAnalyses"/>). Every step is done as the
/// user does it, through the controls; the dialogs (split, proposal, c.a. verifier, design) are the windows of the view, shown off
/// screen and driven through their buttons. After the opening and after every step the state seen by the user on the tab is written as
/// text. Then the drawings: for every F3 fixture of the two modules on the elastic response, the drawing of the response
/// (<see cref="ElasticPileDrawing"/>, with the options of the fixture, every option and a selected depth) and the reinforcement sheet
/// (<see cref="PileReinforcementDrawing"/>), as display lists. The capture writes the traces and the lists in the folder; the check also
/// compares them with those frozen in tests/fixtures/f5-elastico, which the ViewModel tests without WPF compare with the ViewModel. Writes
/// esito.txt («PASS n scenari …»).
/// </summary>
internal static class ElasticPileHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = ElasticPileScenarios.All(root);
        string reference = ElasticPileScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            Console.WriteLine("Elastico " + scenario.Name);
            var trace = await Trace(root, scenario);
            ElasticPileTrace.Write(Path.Combine(output, scenario.Name + ".txt"), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? ElasticPileTrace.FirstDifference(ElasticPileTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
        }
        var drawings = await Drawings(root, Path.Combine(output, "disegni"));
        ElasticPileTrace.Write(Path.Combine(output, "disegni", "elenco.txt"), drawings);
        if (check)
        {
            var names = scenarios.Select(s => s.Name + ".txt").ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt").Select(n => Path.GetFileName(n)).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            string list = Path.Combine(ElasticPileScenarios.DrawingsReference(root), "elenco.txt");
            string? drawingDifference = File.Exists(list) ? ElasticPileTrace.FirstDifference(ElasticPileTrace.Read(list), drawings) : "elenco dei disegni di riferimento assente";
            if (drawingDifference is not null) differences.Add("disegni: " + drawingDifference);
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Vista storica della risposta elastica diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica, {drawings.Count} disegni"
            + (check ? " uguali a quelli congelati" : " catturati"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, ElasticPileScenario scenario)
    {
        var lines = new List<string>();
        var ids = new ElasticPileIds();
        var engine = new HeldElasticAnalyses();
        var side = new HistoricSide(scenario.Module, HorizontalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture), engine);
        try
        {
            lines.AddRange(side.State().Lines("apertura", ids));
            foreach (var step in scenario.Steps)
            {
                await side.Apply(step);
                lines.AddRange(side.State().Lines(step.ToString(), ids));
            }
        }
        finally { engine.Release(); side.Dispose(); }
        return lines;
    }

    /// <summary>The historic sheet driven through its controls, as the user does.</summary>
    private sealed class HistoricSide : IDisposable
    {
        private readonly string module;
        private readonly HeldElasticAnalyses engine;
        private SheetEditor editor = null!;
        private int modified;

        internal HistoricSide(string module, JsonObject data, HeldElasticAnalyses engine)
        {
            this.module = module; this.engine = engine;
            Open(data);
        }

        private HorizontalWorkspace Sheet => editor.HorizontalForChecks;

        private ElasticPileWorkspace Elastic => Sheet.ElasticForChecks;

        private void Open(JsonObject data)
        {
            editor = new SheetEditor(module, data, null, engine.Create);
            modified = 0;
            editor.Modified += () => modified++;
            Layout();
        }

        private void Layout() { editor.Measure(new Size(1400, 1000)); editor.Arrange(new Rect(0, 0, 1400, 1000)); editor.UpdateLayout(); }

        internal ElasticPileTraceState State()
        {
            var state = Elastic.TraceForChecks(Layout, !Sheet.EditableForChecks);
            state.Data = editor.Data; state.Modified = modified; state.Editable = Sheet.EditableForChecks; state.Analysis = Sheet.AnalysisForChecks;
            state.Exportable = editor.Result is not null && !editor.Busy;
            return state;
        }

        internal async Task Apply(ElasticPileStep step)
        {
            switch (step.Kind)
            {
                case ElasticPileStep.Commit: editor.Commit(); break;
                case ElasticPileStep.Calculate: await editor.CalculateAsync(); break;
                case ElasticPileStep.Start: _ = editor.CalculateAsync(); break;
                case ElasticPileStep.Wait: await Elastic.PendingForChecks; break;
                case ElasticPileStep.ReadOnly: editor.InspectRevision(); break;
                case ElasticPileStep.Reopen:
                    var data = editor.Data; editor.Dispose(); Open(data); break;
                case ElasticPileStep.Hold: engine.Hold(); break;
                case ElasticPileStep.Entered: await engine.EnteredAsync(); break;
                case ElasticPileStep.Release: engine.Release(); break;
                case ElasticPileStep.Analysis: Sheet.ApplyForChecks(new HorizontalPileStep(HorizontalPileStep.Analysis, Index: step.Index), Layout); break;
                case ElasticPileStep.CapacityStep: Sheet.ApplyForChecks(step.Capacity!, Layout); break;
                default: await Elastic.ApplyForChecks(step, Layout); break;
            }
            Layout();
        }

        public void Dispose() => editor.Dispose();
    }

    // ---------------------------------------------------------------- drawings

    /// <summary>
    /// The display lists of the drawing of the response (fixture options, every option on, a selected depth, at the size of the view and at a
    /// smaller one) and of the reinforcement sheet (at its size, and empty), for every F3 fixture of the two modules on the elastic response
    /// with a response after «Calcola». The full lists go into <paramref name="output"/>; the returned lines (one per drawing: primitives and
    /// hash) are compared with disegni/elenco.txt.
    /// </summary>
    static async Task<List<string>> Drawings(string root, string output)
    {
        Directory.CreateDirectory(output);
        var list = new List<string>();
        foreach (string module in new[] { ElasticPileScenarios.Pile, ElasticPileScenarios.Micro })
            foreach (string id in ElasticPileScenarios.Fixtures(root, module))
            {
                var data = HorizontalPileScenarios.FixtureData(root, module, id);
                using var editor = new SheetEditor(module, data);
                editor.Commit(); await editor.CalculateAsync(); editor.Commit();
                var elastic = editor.HorizontalForChecks.ElasticForChecks;
                if (elastic.Response is not { } response) continue;
                var result = elastic.Result;
                string name = module + "--" + id;
                var view = (JsonObject)(editor.Data["elastico"]?["visualizzazione"] ?? new JsonObject()).DeepClone();
                var all = new JsonObject();
                foreach (string key in new[] { "strati", "falda", "kh", "mesh", "molle", "carichi", "sisma", "k", "y", "theta", "q", "N", "V", "M", "tratti" }) all[key] = true;
                foreach (var (label, shown, options) in new[] { ("opzioni", result ?? response, view), ("risposta", response, view), ("tutto", result ?? response, all) })
                    foreach (var (w, h) in new[] { (1750, 650), (1200, 800) })
                    {
                        var drawing = new ElasticPileDrawing { Options = (JsonObject)options.DeepClone() };
                        drawing.Set(shown);
                        list.Add(HorizontalPileHistoricTrace.Record(output, $"{name} disegno {label} {w}x{h}", drawing, w, h));
                    }
                var selected = new ElasticPileDrawing { Options = (JsonObject)view.DeepClone() };
                selected.Set(result ?? response);
                selected.SelectDepth(response["input"].D("lunghezza") / 3);
                list.Add(HorizontalPileHistoricTrace.Record(output, $"{name} disegno quota 1750x650", selected, 1750, 650));
                var sheet = new PileReinforcementDrawing { HorizontalAlignment = HorizontalAlignment.Left };
                sheet.Set(result);
                list.Add(HorizontalPileHistoricTrace.Record(output, $"{name} tavola", sheet, (int)Math.Ceiling(sheet.SheetSize.Width), (int)Math.Ceiling(sheet.SheetSize.Height)));
            }
        var empty = new PileReinforcementDrawing();
        empty.Set(null, "Distinta non disponibile: prova");
        list.Add(HorizontalPileHistoricTrace.Record(output, "tavola vuota", empty, 1140, 320));
        return list;
    }
}

internal sealed partial class HorizontalWorkspace
{
    /// <summary>UiTests (F5.8 traces): the elastic response, the second tab.</summary>
    internal ElasticPileWorkspace ElasticForChecks => elastic;

    /// <summary>UiTests (F5.8 traces): the tab of the analyses shown.</summary>
    internal int AnalysisForChecks => analyses.SelectedIndex;

    /// <summary>UiTests (F5.8 traces): false when the revision is read-only (the fields of the sheet are protected).</summary>
    internal bool EditableForChecks => !((TextBox)general.Editors["diametro"]).IsReadOnly;
}

/// <summary>Readers of the controls of the historic view shared by the elastic response and the reinforcement editor (UiTests).</summary>
internal static class ElasticHistoricControls
{
    private static readonly FieldInfo InputFormRows = typeof(InputForm).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "rows");

    internal static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    internal static void Click(ButtonBase button) { if (Usable(button)) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); }

    /// <summary>A check box clicked by the user: the state changes, then Click.</summary>
    internal static void Toggle(CheckBox box)
    {
        if (!Usable(box)) return;
        box.IsChecked = box.IsChecked != true;
        box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, box));
    }

    internal static ElasticCommandState Command(Button button) => new(Convert.ToString(button.Content, CultureInfo.InvariantCulture) ?? "", Usable(button));

    internal static IEnumerable<T> Children<T>(Panel panel) => panel.Children.OfType<T>();

    /// <summary>The descendants in the logical tree (also inside collapsed expanders and dialogs not laid out).</summary>
    internal static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T found) yield return found;
            foreach (var inner in Logical<T>(child)) yield return inner;
        }
    }

    internal static Button Button(DependencyObject root, string label) => Logical<Button>(root).First(b => Equals(b.Content, label));

    /// <summary>The fields of an InputForm as the user sees them (the line of the F5.6 and F5.7 traces).</summary>
    internal static List<VerticalPileFieldState> Fields(InputForm form)
    {
        var rows = (Dictionary<string, List<FrameworkElement>>)InputFormRows.GetValue(form)!;
        return form.Editors.Select(pair =>
        {
            var label = rows[pair.Key][0];
            string name = AutomationProperties.GetName(pair.Value);
            bool enabled = label.IsEnabled, dimmed = label.Opacity < 1, visible = label.Visibility == Visibility.Visible;
            return pair.Value switch
            {
                TextBox => new VerticalPileFieldState(pair.Key, "campo", name, form.Get(pair.Key), null, enabled, dimmed, visible),
                ComboBox choice => new VerticalPileFieldState(pair.Key, "scelta", name, form.Get(pair.Key), ((IEnumerable<string>)choice.ItemsSource).ToArray(), enabled, dimmed, visible),
                CheckBox option => new VerticalPileFieldState(pair.Key, "opzione", name, VerticalPileTrace.YesNo(option.IsChecked == true), null, enabled, dimmed, visible),
                _ => throw new InvalidOperationException("Editor non previsto: " + pair.Value.GetType().Name)
            };
        }).ToList();
    }

    /// <summary>A field of a form typed, chosen or clicked by the user; false when the form has no such field.</summary>
    internal static bool Edit(InputForm form, string key, string? text)
    {
        if (!form.Editors.TryGetValue(key, out var editor)) return false;
        switch (editor)
        {
            case TextBox box when text is not null: if (Usable(box) && !box.IsReadOnly) box.Text = text; break;
            case ComboBox choice when text is not null: if (Usable(choice)) choice.SelectedItem = text; break;
            case CheckBox option when text is null: Toggle(option); break;
            default: throw new ArgumentException("Passo non adatto al campo " + key);
        }
        return true;
    }

    internal static string Tone(Brush? brush) => brush is SolidColorBrush solid ? ElasticPileTrace.Tone("#" + solid.Color.ToString(CultureInfo.InvariantCulture)[3..]) : "-";

    internal static string Section(SectionDrawing drawing) => ElasticPileTrace.Section(drawing.Outline, drawing.Bars.Select(b => (b.X, b.Y, b.Area, b.Diametro)),
        drawing.CircularLinkRadius, drawing.LinkDiameter, drawing.ShowAxes, drawing.Caption, drawing.LinkCaption);

    internal static string Progress(ProgressBar bar) => ElasticPileTrace.Progress(bar.Visibility == Visibility.Visible, bar.IsIndeterminate, bar.Value);

    /// <summary>A dialog of the view shown off screen, without activation, as the trace drives it.</summary>
    internal static void ShowOffScreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000; window.ShowActivated = false; window.ShowInTaskbar = false;
        window.Show(); window.UpdateLayout();
    }
}

internal sealed partial class ElasticPileWorkspace
{
    private static readonly FieldInfo DrawingResult = typeof(ElasticPileDrawing).GetField("result", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo DrawingSelected = typeof(ElasticPileDrawing).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo SheetResult = typeof(PileReinforcementDrawing).GetField("result", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>UiTests (F5.8 traces): the last calculation of the elastic response requested.</summary>
    internal Task PendingForChecks => scheduledCalculation;

    /// <summary>UiTests (F5.8 traces): the state of the tab, read from its controls without changing them.</summary>
    internal ElasticPileTraceState TraceForChecks(Action layout, bool readOnly)
    {
        var state = new ElasticPileTraceState
        {
            Response = Response, Result = Result, Status = status.Text, Progress = ElasticHistoricControls.Progress(calculationProgress), Stiffness = section.Text,
            Drawing = (JsonObject?)DrawingResult.GetValue(drawing), Selected = (JsonNode?)DrawingSelected.GetValue(drawing),
            Selection = selection.Text, SectionPreview = ElasticHistoricControls.Section(sectionPreview), SectionDetail = sectionDetail.Text, Summary = summary.Text,
            Table = table.ItemsSource is IEnumerable rows ? rows.Cast<object>().Select(r => ElasticPileTrace.Row(r.GetType().GetProperties().Select(p => p.GetValue(r)))).ToList() : [],
            Csv = csv.IsEnabled, ReinforcementSummary = reinforcementSummary.Text, SheetMessage = reinforcementDrawing.EmptyMessage,
            SheetPieces = reinforcementDrawing.Pieces.Count, Sheet = (JsonObject?)SheetResult.GetValue(reinforcementDrawing),
            Reinforcement = reinforcementEditor.TraceForChecks(layout, readOnly), Dialog = reinforcementEditor.TakeDialogForChecks()
        };
        if (migration.Content is Border paper && paper.Child is StackPanel panel)
        {
            state.Migration = ((TextBlock)panel.Children[1]).Text;
            state.MigrationCommands = ElasticHistoricControls.Logical<Button>(panel).Select(ElasticHistoricControls.Command).ToList();
        }
        if (header.Content is StackPanel controls)
        {
            var bar = (Panel)controls.Children[0];
            var choice = bar.Children.OfType<ComboBox>().Single();
            state.Header = true;
            state.SurveyChoices = ((IEnumerable<string>)choice.ItemsSource).ToArray(); state.SurveyChoice = choice.SelectedIndex; state.SurveyEnabled = ElasticHistoricControls.Usable(choice);
            state.HeaderCommands = bar.Children.OfType<Button>().Select(ElasticHistoricControls.Command).ToList();
            state.Options = ElasticHistoricControls.Fields(options!);
            state.Toggles = controls.Children.OfType<WrapPanel>().Single(w => w.Children.OfType<CheckBox>().Any()).Children.OfType<CheckBox>()
                .Select(b => new ElasticToggleState((string)b.Tag, (string)b.Content, b.IsChecked == true, ElasticHistoricControls.Usable(b))).ToList();
        }
        return state;
    }

    /// <summary>UiTests (F5.8 traces): a step of the user on the controls of the tab; a control the user cannot use does nothing.</summary>
    internal async Task ApplyForChecks(ElasticPileStep step, Action layout)
    {
        var controls = header.Content as StackPanel;
        switch (step.Kind)
        {
            case ElasticPileStep.Survey:
                if (controls is not null && ((Panel)controls.Children[0]).Children.OfType<ComboBox>().Single() is { } choice && ElasticHistoricControls.Usable(choice)) choice.SelectedIndex = step.Index;
                break;
            case ElasticPileStep.Option:
                if (options is not null) ElasticHistoricControls.Edit(options, step.Key!, step.Text);
                break;
            case ElasticPileStep.Toggle:
                if (controls?.Children.OfType<WrapPanel>().Single(w => w.Children.OfType<CheckBox>().Any()).Children.OfType<CheckBox>().Single(b => (string)b.Tag == step.Key) is { } box) ElasticHistoricControls.Toggle(box);
                break;
            case ElasticPileStep.EditInputs:
                if (controls is not null) ElasticHistoricControls.Click(ElasticHistoricControls.Button(controls, "Modifica dati e strati"));
                break;
            case ElasticPileStep.Migration:
                if (migration.Content is Border paper)
                    ElasticHistoricControls.Click(ElasticHistoricControls.Button(paper, step.Text == "legacy" ? "Importa legacy nei dati comuni" : "Usa dati comuni e conserva legacy"));
                break;
            case ElasticPileStep.Depth:
                // A click on the drawing at that depth (OnMouseDown gives SelectDepth of the depth under the pointer).
                if (DrawingResult.GetValue(drawing) is not null) drawing.SelectDepth(double.Parse(step.Text!, CultureInfo.InvariantCulture));
                break;
            default: await reinforcementEditor.ApplyForChecks(step, layout); break;
        }
    }
}

internal sealed partial class PileReinforcementEditor
{
    private static readonly string[] SegmentTitles = ["Numero barre", "φ barre [mm]", "φ staffe [mm]", "Passo staffe [mm]", "Barre anello interno", "φ anello interno [mm]", "Distanza libera [mm]"];
    private static readonly string[] SegmentKeys = [.. ElasticPileEditor.ReinforcementKeys, "second_inner_count", "second_inner_diameter", "second_inner_gap"];

    private StackPanel? Stack => (Content as Grid)?.Children.OfType<StackPanel>().FirstOrDefault();

    /// <summary>The cards of the segments: the stack of each.</summary>
    private List<StackPanel> Cards() => Stack is { } stack ? stack.Children.OfType<Border>().Select(b => (StackPanel)b.Child).ToList() : [];

    /// <summary>The fields of a card, by title: the title and the editor.</summary>
    private static List<(string Title, FrameworkElement Editor)> CardFields(StackPanel card) =>
        ((Panel)card.Children[1]).Children.OfType<StackPanel>().Select(f => (((TextBlock)f.Children[0]).Text, (FrameworkElement)f.Children[1])).ToList();

    private static Panel CardCommands(StackPanel card) => (Panel)card.Children[2];

    private Expander? Group(string header) => Stack?.Children.OfType<Expander>().FirstOrDefault(e => Equals(e.Header, header));

    private InputForm? Form(string header) => Group(header)?.Content switch
    {
        InputForm form => form,
        StackPanel panel => panel.Children.OfType<InputForm>().FirstOrDefault(),
        _ => null
    };

    private const string DetailsHeader = "Ipotesi di verifica e vincoli per la proposta", SeismicHeader = "Sisma · testa del palo · NTC §7.2.5", ExtraHeader = "Sovrapposizioni, interferro e copriferro";

    /// <summary>
    /// UiTests (F5.8 traces): the editor as the user sees it; null while the comparison with an old archive is shown (nothing built). In a
    /// read-only revision the view protects the controls it shows: the fields of a closed group are read as the user finds them when he
    /// opens it (the group is opened and closed again around the reading; its state is the one before).
    /// </summary>
    internal ElasticReinforcementState? TraceForChecks(Action layout, bool readOnly)
    {
        if (Stack is not { } stack) return null;
        var closed = readOnly ? new[] { DetailsHeader, SeismicHeader, ExtraHeader }.Select(Group).OfType<Expander>().Where(g => !g.IsExpanded).ToList() : [];
        foreach (var group in closed) group.IsExpanded = true;
        if (closed.Count > 0) layout();
        try { return Trace(stack, closed); }
        finally { foreach (var group in closed) group.IsExpanded = false; if (closed.Count > 0) layout(); }
    }

    private ElasticReinforcementState Trace(StackPanel stack, List<Expander> opened)
    {
        var state = new ElasticReinforcementState { Phase = phase.Text, Progress = ElasticHistoricControls.Progress(progress), Message = message.Text };
        var bars = stack.Children.OfType<WrapPanel>().ToList();
        state.Commands = bars.SelectMany(b => b.Children.OfType<Button>()).Select(ElasticHistoricControls.Command).ToList();
        var transverse = bars.SelectMany(b => b.Children.OfType<ComboBox>()).Single();
        state.MainTransverse = transverse.SelectedItem as string ?? ""; state.MainTransverseChoices = ((IEnumerable<string>)transverse.ItemsSource).ToArray();
        state.MainTransverseEnabled = ElasticHistoricControls.Usable(transverse);
        foreach (var card in Cards())
        {
            var segment = new ElasticSegmentState { Header = ((TextBlock)card.Children[0]).Text };
            foreach (var (title, editor) in CardFields(card))
                switch (title)
                {
                    case "Fine tratto / barre [m]": var end = (TextBox)editor; segment.End = end.Text; segment.EndEditable = !end.IsReadOnly && ElasticHistoricControls.Usable(end); segment.EndToolTip = end.ToolTip as string; break;
                    case "Origine armatura": var linked = (CheckBox)editor; segment.Linked = linked.IsChecked == true; segment.LinkedEnabled = ElasticHistoricControls.Usable(linked); break;
                    case "Anello interno": var ring = (CheckBox)editor; segment.Ring = ring.IsChecked == true; segment.RingEnabled = ElasticHistoricControls.Usable(ring); segment.RingToolTip = ring.ToolTip as string; break;
                    case "Tipo trasversale":
                        var kind = (ComboBox)editor; segment.Kind = kind.SelectedItem as string ?? ""; segment.Kinds = ((IEnumerable<string>)kind.ItemsSource).ToArray();
                        segment.KindEnabled = ElasticHistoricControls.Usable(kind); break;
                    default:
                        var box = (TextBox)editor;
                        segment.Fields.Add(new(SegmentKeys[Array.IndexOf(SegmentTitles, title)], title, box.Text, box.IsReadOnly || !ElasticHistoricControls.Usable(box), box.ToolTip as string));
                        break;
                }
            segment.Commands = CardCommands(card).Children.OfType<Button>().Select(ElasticHistoricControls.Command).ToList();
            var bill = card.Children.OfType<Expander>().Single();
            segment.Bill = ((TextBlock)bill.Content).Text; segment.BillExpanded = bill.IsExpanded;
            state.Segments.Add(segment);
        }
        if (Group(DetailsHeader) is { } details) { state.DetailsExpanded = details.IsExpanded && !opened.Contains(details); state.Details = ElasticHistoricControls.Fields((InputForm)details.Content); }
        if (Group(SeismicHeader) is { } seismic)
        {
            state.SeismicExpanded = seismic.IsExpanded && !opened.Contains(seismic);
            var panel = (StackPanel)seismic.Content;
            state.Seismic = ElasticHistoricControls.Fields(panel.Children.OfType<InputForm>().Single());
            state.SeismicCommands = panel.Children.OfType<WrapPanel>().SelectMany(b => b.Children.OfType<Button>()).Select(ElasticHistoricControls.Command).ToList();
        }
        if (Group(ExtraHeader) is { } extra) { state.ExtraExpanded = extra.IsExpanded && !opened.Contains(extra); state.Extra = ElasticHistoricControls.Fields(((StackPanel)extra.Content).Children.OfType<InputForm>().Single()); }
        var minimum = stack.Children.OfType<CheckBox>().Single();
        state.Minimum = minimum.IsChecked == true; state.MinimumEnabled = ElasticHistoricControls.Usable(minimum);
        state.Summary = SummaryLines();
        return state;
    }

    /// <summary>The panel «Sezione e verifiche», one line per element in its order.</summary>
    private List<string> SummaryLines()
    {
        var lines = new List<string>();
        if (summary.Content is not Border paper || paper.Child is not StackPanel content) return lines;
        foreach (var element in content.Children)
            lines.Add(element switch
            {
                TextBlock text => ElasticPileTrace.SummaryText(ElasticHistoricControls.Tone(text.Foreground), text.Text),
                ComboBox choice => ElasticPileTrace.SummaryChoice(((IEnumerable<string>)choice.ItemsSource), choice.SelectedItem as string, ElasticHistoricControls.Usable(choice)),
                SectionDrawing section => ElasticPileTrace.SummarySection(ElasticHistoricControls.Section(section)),
                Border verdict when verdict.Child is TextBlock text => ElasticPileTrace.SummaryVerdict(ElasticHistoricControls.Tone(text.Foreground), text.Text),
                Expander group when group.Content is TextBlock text => ElasticPileTrace.SummaryGroup((string)group.Header, group.IsExpanded, ElasticHistoricControls.Tone(text.Foreground), text.Text),
                Button button => ElasticPileTrace.SummaryCommand((string)button.Content, ElasticHistoricControls.Usable(button)),
                _ => "elemento " + element.GetType().Name
            });
        return lines;
    }

    private ComboBox? SummaryChoice(int index) => summary.Content is Border { Child: StackPanel content } ? content.Children.OfType<ComboBox>().ElementAtOrDefault(index) : null;

    private string? DialogForChecks;

    /// <summary>UiTests (F5.8 traces): what the dialog of the last step showed when the step left it (once), null without a dialog.</summary>
    internal string? TakeDialogForChecks() { var text = DialogForChecks; DialogForChecks = null; return text; }

    /// <summary>UiTests (F5.8 traces): a step of the user on the editor of the reinforcement; a control the user cannot use does nothing.</summary>
    internal async Task ApplyForChecks(ElasticPileStep step, Action layout)
    {
        DialogForChecks = null;
        if (Stack is not { } stack) return;
        var cards = Cards();
        StackPanel? card = step.Index >= 0 && step.Index < cards.Count ? cards[step.Index] : null;
        FrameworkElement? Field(string title) => card is null ? null : CardFields(card).FirstOrDefault(f => f.Title == title).Editor;
        Button? CardButton(string label) => card is null ? null : CardCommands(card).Children.OfType<Button>().FirstOrDefault(b => Equals(b.Content, label));
        switch (step.Kind)
        {
            case ElasticPileStep.SegmentEnd:
                if (Field("Fine tratto / barre [m]") is TextBox end && ElasticHistoricControls.Usable(end) && !end.IsReadOnly) end.Text = step.Text;
                break;
            case ElasticPileStep.SegmentLinked:
                if (Field("Origine armatura") is CheckBox linked) ElasticHistoricControls.Toggle(linked);
                break;
            case ElasticPileStep.SegmentField:
                if (Field(SegmentTitles[Array.IndexOf(SegmentKeys, step.Key)]) is TextBox box && ElasticHistoricControls.Usable(box) && !box.IsReadOnly) box.Text = step.Text;
                break;
            case ElasticPileStep.SegmentRing:
                if (Field("Anello interno") is CheckBox ring) ElasticHistoricControls.Toggle(ring);
                break;
            case ElasticPileStep.SegmentKind:
                if (Field("Tipo trasversale") is ComboBox kind && ElasticHistoricControls.Usable(kind)) kind.SelectedItem = step.Text;
                break;
            case ElasticPileStep.MainTransverse:
                if (stack.Children.OfType<WrapPanel>().SelectMany(b => b.Children.OfType<ComboBox>()).Single() is { } transverse && ElasticHistoricControls.Usable(transverse)) transverse.SelectedItem = step.Text;
                break;
            case ElasticPileStep.Detail:
                // The fields are inside expanders: the user opens the expander first (the view protects the controls it shows).
                foreach (string header in new[] { DetailsHeader, SeismicHeader, ExtraHeader })
                    if (Form(header) is { } form && form.Editors.ContainsKey(step.Key!))
                    {
                        var group = Group(header)!;
                        if (!group.IsExpanded) { group.IsExpanded = true; layout(); }
                        ElasticHistoricControls.Edit(Form(header)!, step.Key!, step.Text);
                        break;
                    }
                break;
            case ElasticPileStep.Minimum:
                ElasticHistoricControls.Toggle(stack.Children.OfType<CheckBox>().Single());
                break;
            case ElasticPileStep.Approve:
                if (step.Text == "sisma")
                {
                    var group = Group(SeismicHeader)!;
                    if (!group.IsExpanded) { group.IsExpanded = true; layout(); }
                    ElasticHistoricControls.Click(ElasticHistoricControls.Button(Group(SeismicHeader)!, "Approva tutto · sisma"));
                }
                else ElasticHistoricControls.Click(ElasticHistoricControls.Button(stack, "Approva tutto · ipotesi"));
                break;
            case ElasticPileStep.Merge: if (CardButton("Unisci ←") is { } merge) ElasticHistoricControls.Click(merge); break;
            case ElasticPileStep.Verify: if (CardButton("Verifiche →") is { } verify) ElasticHistoricControls.Click(verify); break;
            case ElasticPileStep.Bill: if (CardButton("Distinta ferri") is { } bill) ElasticHistoricControls.Click(bill); break;
            case ElasticPileStep.SummarySegment:
                if (SummaryChoice(0) is { } segments && ElasticHistoricControls.Usable(segments)) segments.SelectedIndex = step.Index;
                break;
            case ElasticPileStep.SummaryMode:
                if (SummaryChoice(1) is { } mode && ElasticHistoricControls.Usable(mode)) mode.SelectedItem = step.Text;
                break;
            case ElasticPileStep.Split:
                if (CardButton("Dividi…") is not { } split || !ElasticHistoricControls.Usable(split)) break;
                var splitWindow = CreateSplitWindow(step.Index);
                ElasticHistoricControls.ShowOffScreen(splitWindow);
                ElasticHistoricControls.Logical<TextBox>(splitWindow).Single().Text = step.Text;
                var cut = ElasticHistoricControls.Logical<TextBox>(splitWindow).Single();
                ElasticHistoricControls.Click(ElasticHistoricControls.Button(splitWindow, "Dividi"));
                DialogForChecks = cut.ToolTip as string ?? "";
                if (splitWindow.IsVisible) splitWindow.Close();
                break;
            case ElasticPileStep.Propose:
                if (!ElasticHistoricControls.Usable(ElasticHistoricControls.Button(stack, "Proponi suddivisione…"))) break;
                if (CreateProposalWindow() is not { } proposal) break;
                ElasticHistoricControls.ShowOffScreen(proposal);
                DialogForChecks = ElasticHistoricControls.Logical<TextBlock>(proposal).Last().Text;
                ElasticHistoricControls.Click(ElasticHistoricControls.Button(proposal, "Applica e archivia i tratti precedenti"));
                if (proposal.IsVisible) proposal.Close();
                break;
            case ElasticPileStep.Concrete:
                if (CardButton("Apri verificatore c.a.") is not { } open || !ElasticHistoricControls.Usable(open)) break;
                if (CreateConcreteWindow(step.Index) is not { } verifier) break;
                ElasticHistoricControls.ShowOffScreen(verifier);
                var applyButton = ElasticHistoricControls.Logical<Button>(verifier).First(b => Convert.ToString(b.Content, CultureInfo.InvariantCulture)?.StartsWith("Applica armatura", StringComparison.Ordinal) == true);
                if (step.Text == "applica") ElasticHistoricControls.Click(applyButton);
                DialogForChecks = ((TextBlock)((Panel)applyButton.Parent).Children[0]).Text;
                verifier.Close();
                break;
            case ElasticPileStep.Design:
                if (CardButton("Dimensiona…") is not { } design || !ElasticHistoricControls.Usable(design)) break;
                if (CreateSegmentDesignWindow(step.Index) is not { } window) break;
                ElasticHistoricControls.ShowOffScreen(window);
                var boxes = ElasticHistoricControls.Logical<TextBox>(window).ToArray();
                string[] catalogues = step.Text!.Split('|');
                for (int i = 0; i < 4; i++) boxes[i].Text = catalogues[i];
                ElasticHistoricControls.Click(ElasticHistoricControls.Button(window, "Trova proposta"));
                await segmentDesignTask.WaitAsync(TimeSpan.FromSeconds(120));
                var apply = ElasticHistoricControls.Logical<Button>(window).First(b => Convert.ToString(b.Content, CultureInfo.InvariantCulture)?.StartsWith("Applica ", StringComparison.Ordinal) == true);
                if (apply.IsEnabled) ElasticHistoricControls.Click(apply);
                DialogForChecks = ElasticHistoricControls.Logical<TextBlock>(window).Last().Text;
                window.Close();
                break;
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
    }
}
