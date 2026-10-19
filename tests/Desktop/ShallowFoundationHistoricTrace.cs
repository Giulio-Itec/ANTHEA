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
using Anthea.Presentation.Tests;

namespace X.Desktop;

/// <summary>
/// --check-fondazioni-historic &lt;radice&gt; &lt;cartella&gt; and --check-fondazioni-historic-capture &lt;radice&gt; &lt;cartella&gt;
/// (UiTests, F5.9b, proposal F5 section 7.2): the scenarios of tests/Shared/ShallowFoundationTrace.cs on the historic view
/// <see cref="FoundationWorkspace"/>, opened as the window opens it (<see cref="SheetEditor"/>), off screen. Every step is done as the
/// user does it, through the controls: a control that the user cannot use (hidden by the rules of the sheet, disabled, or protected by
/// the read-only revision) does nothing. After the opening and after every step the state seen by the user is written as text: data
/// in memory order, Modified, result, status, fields, groups, surveys and layers, results and graphic tabs. The capture writes the
/// traces in the folder; the check also compares them with the traces frozen in tests/fixtures/f5-fondazioni/storica, which the
/// ViewModel tests without WPF compare with ShallowFoundationSheetViewModel. The check stays in the ui stage until the historic view is
/// archived (Q11). Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class ShallowFoundationHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = ShallowFoundationScenarios.All(root);
        string reference = ShallowFoundationScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            var trace = await Trace(root, scenario, compareFrozenVersion: check);
            PileGroupTrace.Write(Path.Combine(output, scenario.Name + ".txt"), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? PileGroupTrace.FirstDifference(PileGroupTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
        }
        if (check)
        {
            var names = scenarios.Select(s => s.Name + ".txt").ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt").Select(n => Path.GetFileName(n)).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Vista storica delle fondazioni diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica"
            + (check ? " uguali alle tracce congelate" : " catturate"), new UTF8Encoding(false));
        await Task.CompletedTask;
    }

    static Task<List<string>> Trace(string root, ShallowFoundationScenario scenario, bool compareFrozenVersion)
    {
        var lines = new List<string>();
        var side = new HistoricSide(ShallowFoundationScenarios.FixtureData(root, scenario.Fixture));
        try
        {
            lines.AddRange(side.State().Lines("apertura", compareFrozenVersion));
            foreach (var step in scenario.Steps)
            {
                side.Apply(step);
                lines.AddRange(side.State().Lines(step.ToString(), compareFrozenVersion));
            }
        }
        finally { side.Dispose(); }
        return Task.FromResult(lines);
    }

    /// <summary>The historic sheet driven through its controls, as the user does.</summary>
    private sealed class HistoricSide : IDisposable
    {
        private SheetEditor editor = null!;
        private FoundationWorkspace view = null!;
        private int modified, reports;

        internal HistoricSide(JsonObject data) => Open(data);

        private void Open(JsonObject data)
        {
            editor = new SheetEditor(ShallowFoundationScenarios.Module, data);
            view = editor.FoundationForChecks ?? throw new InvalidOperationException("Foglio delle fondazioni senza la vista storica.");
            modified = 0; reports = 0;
            editor.Modified += () => modified++;
            editor.ReportRequested += () => reports++;
            Layout();
        }

        private void Layout() { editor.Measure(new Size(1400, 1000)); editor.Arrange(new Rect(0, 0, 1400, 1000)); editor.UpdateLayout(); }

        internal ShallowFoundationTraceState State() => view.TraceForChecks(editor.Data, modified, reports, editor.Result);

        internal void Apply(ShallowFoundationStep step)
        {
            switch (step.Kind)
            {
                case ShallowFoundationStep.Commit: editor.Commit(); break;
                // SheetEditor.CalculateAsync runs FoundationWorkspace.TryCalculate on this thread and is completed when it returns.
                case ShallowFoundationStep.Calculate: _ = editor.CalculateAsync(); break;
                case ShallowFoundationStep.ReadOnly: editor.InspectRevision(); break;
                case ShallowFoundationStep.Reopen:
                    var data = editor.Data; editor.Dispose(); Open(data); break;
                default: view.ApplyForChecks(step); break;
            }
            Layout();
        }

        public void Dispose() => editor.Dispose();
    }
}

internal sealed partial class SheetEditor
{
    /// <summary>UiTests: the historic view of the shallow foundations (F5.9b traces).</summary>
    internal FoundationWorkspace? FoundationForChecks => foundation;
}

internal sealed partial class FoundationWorkspace
{
    private static readonly FieldInfo InputFormRows = typeof(InputForm).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "rows");
    private static readonly IReadOnlyDictionary<string, string> GroupTitles = new Dictionary<string, string>
    { ["metodo"] = "Metodo e verifica", ["Geometria"] = "Geometria", ["Azioni"] = "Azioni", ["stratigrafia"] = "Stratigrafia" };

    private static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    private static void Click(ButtonBase button) { if (Usable(button)) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); }

    private List<T> Controls<T>() where T : DependencyObject
    {
        var found = new List<T>(); var pending = new Stack<object>(); pending.Push(this);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is T match) found.Add(match);
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element).Cast<object>().Reverse()) pending.Push(child);
        }
        return found;
    }

    private InputForm Form(string key) => Controls<InputForm>().Single(f => f.Editors.ContainsKey(key));

    private TextBox NameBox => Controls<TextBox>().Single(t => t.Parent is Grid row && row.Children.OfType<TextBlock>().Any(l => l.Text == "Combinazione"));

    private Expander Group(string key) => Controls<Expander>().Single(e => Equals(e.Header, GroupTitles[key]));

    private Button Collect => Controls<Button>().Single(b => Equals(b.Tag, "raccogli-input"));

    private Button Named(string text) => Controls<Button>().Single(b => Equals(b.Content, text));

    private CheckBox HalfSpace => Controls<CheckBox>().Single(c => c.Content is string text && text.StartsWith("Strato portante omogeneo", StringComparison.Ordinal));

    /// <summary>The editor of a field and the element whose visibility the rules of the sheet change.</summary>
    private (FrameworkElement Editor, FrameworkElement Row) Editor(string key)
    {
        if (fields.TryGetValue(key, out var row))
            return (row is CheckBox ? row : ((Grid)row).Children.OfType<FrameworkElement>().Single(e => e is TextBox or ComboBox), row);
        if (key == "azioni_confermate") return (fields["confirmation"], fields["confirmation"]);
        if (key == "nome_combinazione") { var name = NameBox; return (name, (FrameworkElement)name.Parent); }
        if (key == "strato_portante_semiinfinito") return (HalfSpace, HalfSpace);
        var editor = Form(key).Editors[key];
        return (editor, editor);
    }

    /// <summary>UiTests (F5.9b traces): a step of the user on the controls of the view; a control the user cannot use does nothing.</summary>
    internal void ApplyForChecks(ShallowFoundationStep step)
    {
        switch (step.Kind)
        {
            case ShallowFoundationStep.Field:
            {
                var (editor, row) = Editor(step.Key!);
                if (editor is TextBox text && Usable(text) && Usable(row) && !text.IsReadOnly) text.Text = step.Text;
                break;
            }
            case ShallowFoundationStep.Choice:
            {
                var (editor, row) = Editor(step.Key!);
                if (editor is ComboBox choice && Usable(choice) && Usable(row)) choice.SelectedItem = step.Text;
                break;
            }
            case ShallowFoundationStep.Option:
            {
                var (editor, row) = Editor(step.Key!);
                if (editor is CheckBox box && Usable(box) && Usable(row))
                {
                    box.IsChecked = box.IsChecked != true;
                    box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, box));
                }
                break;
            }
            case ShallowFoundationStep.Group: { var group = Group(step.Key!); group.IsExpanded = !group.IsExpanded; break; }
            case ShallowFoundationStep.Collect: Click(Collect); break;
            case ShallowFoundationStep.Survey: if (Usable(survey)) survey.SelectedIndex = step.Index; break;
            case ShallowFoundationStep.AddSurvey: Click(Named("+ Sondaggio")); break;
            case ShallowFoundationStep.RemoveSurvey:
                // The button asks for a confirmation (message box) when there is more than one survey: the trace answers yes.
                if (Usable(Named("− Sondaggio"))) DeleteSurvey(confirm: false);
                break;
            case ShallowFoundationStep.Layer:
            {
                var grid = soilGrid!;
                if (grid.IsReadOnly || !Usable(grid)) break;
                // The bindings of the cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged).
                grid.Rows[step.Row][step.Key!] = step.Text;
                break;
            }
            case ShallowFoundationStep.AddLayer: Click(Named("Aggiungi strato")); break;
            case ShallowFoundationStep.RemoveLayer:
            {
                var row = soilGrid!.Rows[step.Row];
                if (Ui.Descendants<Button>(soilGrid).SingleOrDefault(b => ReferenceEquals(b.DataContext, row) && Equals(b.Content, "[−]")) is { } remove) Click(remove);
                break;
            }
            case ShallowFoundationStep.Mechanism:
                if (graphicsHosts[4].Content is DockPanel panel && panel.Children.OfType<ComboBox>().SingleOrDefault() is { } axis && Usable(axis)) axis.SelectedIndex = step.Index;
                break;
            case ShallowFoundationStep.Report: Click(Named("Report Word e PDF")); break;
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
    }

    /// <summary>The state of the view for the trace of F5.9b: what the controls show, read without changing them.</summary>
    internal ShallowFoundationTraceState TraceForChecks(JsonObject sheetData, int modifiedCount, int reportCount, JsonObject? result)
    {
        var trace = new ShallowFoundationTraceState
        {
            Data = sheetData, Modified = modifiedCount, ReportRequests = reportCount, Result = result, State = state.Text,
            Editable = !((TextBox)Editor("Width").Editor).IsReadOnly,
        };
        foreach (string key in ShallowFoundationScenarios.FieldKeys) trace.Fields.Add(FieldState(key));
        foreach (string key in ShallowFoundationScenarios.GroupKeys) trace.Groups.Add((key, Group(key).IsExpanded));
        trace.InputsCollapsed = LogicalTreeHelper.GetParent(LogicalTreeHelper.GetParent(Group("metodo"))) is ScrollViewer { Visibility: not Visibility.Visible };
        trace.CollectText = (string)Collect.Content;
        trace.Surveys = ((IEnumerable)survey.ItemsSource).Cast<string>().ToArray();
        trace.SelectedSurvey = survey.SelectedIndex;
        trace.Layers = soilGrid!.Rows.Select(r => ShallowFoundationTraceState.WithoutColour(r.Values)).ToList();
        trace.HalfSpace = HalfSpace.IsChecked == true;
        ReadResults(trace);
        trace.InputDrawings = ReferenceEquals(diagram.Data, data) && ReferenceEquals(plan.Data, data);
        trace.SoilDrawing = graphicsHosts[0].Content is FoundationSoilView soil && ReferenceEquals(soil.Data, data);
        ReadGraphics(trace);
        return trace;
    }

    private ShallowFoundationFieldState FieldState(string key)
    {
        var (editor, row) = Editor(key);
        bool visible = row.Visibility == Visibility.Visible;
        string unit = "";
        if (!fields.ContainsKey(key) && key is not ("azioni_confermate" or "nome_combinazione")
            && ((IDictionary)InputFormRows.GetValue(Form(key))!)[key] is List<FrameworkElement> { Count: 3 } elements)
            unit = ((TextBlock)elements[2]).Text;
        string label = editor switch
        {
            CheckBox { Content: string text } => text,
            CheckBox { Content: TextBlock block } when key == "azioni_confermate" => block.Text,
            _ when row is Grid grid && grid.Children.OfType<TextBlock>().FirstOrDefault() is { } text => text.Text,
            _ => AutomationProperties.GetName(editor)
        };
        return editor switch
        {
            TextBox text => new(key, "campo", label, unit, text.Text, null, visible),
            ComboBox choice => new(key, "scelta", label, unit, choice.SelectedItem as string ?? "", ((IEnumerable)choice.ItemsSource).Cast<string>().ToArray(), visible),
            CheckBox box => new(key, "opzione", label, unit, PileGroupTrace.YesNo(box.IsChecked == true), null, visible),
            _ => throw new InvalidOperationException("Campo non previsto: " + key)
        };
    }

    private void ReadResults(ShallowFoundationTraceState trace)
    {
        var children = results.Children.Cast<UIElement>().ToArray();
        trace.Headline = ((TextBlock)children[0]).Text;
        if (children.Length == 1) return;
        trace.HasDetails = true;
        var details = (StackPanel)((ScrollViewer)((Expander)children[1]).Content).Content;
        int tables = 0;
        string? pendingTitle = null;
        foreach (var element in details.Children.Cast<UIElement>())
        {
            if (element is DataGrid grid)
            {
                var rows = ((IEnumerable)grid.ItemsSource).Cast<string[]>().ToList();
                var columns = grid.Columns.Select(c => (string)c.Header).ToArray();
                if (tables++ == 0) { trace.Coefficients = rows; trace.CoefficientColumns = columns; }
                else { trace.AdditiveTitle = pendingTitle ?? ""; trace.Additive = rows; trace.AdditiveColumns = columns; pendingTitle = null; }
            }
            else if (element is TextBlock text)
            {
                if (tables == 0) trace.Summary.Add(text.Text);
                else if (tables == 1 && text.Text == "Hansen non drenato · correzioni additive") pendingTitle = text.Text;
                else trace.Notes.Add(text.Text);
            }
            else throw new InvalidOperationException("Elemento dei dettagli non previsto: " + element.GetType().Name);
        }
        if (pendingTitle is not null) throw new InvalidOperationException("Titolo delle correzioni additive senza tabella.");
    }

    private void ReadGraphics(ShallowFoundationTraceState trace)
    {
        static JsonObject? Captured(object view) => view.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(f => f.FieldType == typeof(JsonObject)).GetValue(view) as JsonObject;
        var shown = graphicsHosts.Skip(1).Select(h => h.Content).ToArray();
        if (shown.All(c => c is TextBlock))
        {
            var messages = shown.Cast<TextBlock>().Select(t => t.Text).Distinct().ToArray();
            if (messages.Length != 1) throw new InvalidOperationException("Messaggi diversi nelle schede dei risultati.");
            trace.GraphicsMessage = messages[0];
            return;
        }
        trace.Graphics = Captured((FoundationPlanView)shown[0]!);
        var pressure = (DockPanel)shown[1]!;
        trace.PressureText = pressure.Children.OfType<TextBlock>().Single().Text;
        if (!ReferenceEquals(Captured(pressure.Children.OfType<FoundationPlanView>().Single()), trace.Graphics)) throw new InvalidOperationException("Pressioni di un altro risultato.");
        var contact = (DockPanel)shown[2]!;
        trace.ContactText = contact.Children.OfType<TextBlock>().Single().Text;
        var mechanism = (DockPanel)shown[3]!;
        var axis = mechanism.Children.OfType<ComboBox>().Single();
        trace.MechanismAxes = ((IEnumerable)axis.ItemsSource).Cast<string>().ToArray();
        trace.MechanismAxis = axis.SelectedIndex;
        trace.Mechanism = Captured((FoundationMechanismView)mechanism.Children.OfType<ContentControl>().Single().Content);
        trace.MechanismText = mechanism.Children.OfType<TextBlock>().Single().Text;
    }
}
