using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Application.Modules;
using Anthea.Presentation.Tests;

namespace X.Desktop;

/// <summary>
/// --check-pile-group-historic &lt;radice&gt; &lt;cartella&gt; and --check-pile-group-historic-capture &lt;radice&gt; &lt;cartella&gt;
/// (UiTests, F5.4, proposal F5 section 7.2): the scenarios of tests/Shared/PileGroupTrace.cs on the historic view
/// <see cref="HorizontalPileGroupWorkspace"/>, opened as the window opens it (<see cref="SheetEditor"/>), off screen. After the
/// opening and after every step the state seen by the user is written as text: data in memory order, Modified, result, texts,
/// tables, fields and drawing. The capture writes the traces in the folder; the check also compares them with the traces frozen
/// in tests/fixtures/f5-palificata/storica, which the ViewModel tests without WPF compare with PileGroupSheetViewModel. The check
/// stays in the ui stage until the historic view is archived (Q11). Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class PileGroupHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = PileGroupScenarios.All(root);
        string reference = PileGroupScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            var trace = await Trace(root, scenario);
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
                throw new InvalidOperationException("Vista storica della palificata diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica"
            + (check ? " uguali alle tracce congelate" : " catturate"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, PileGroupScenario scenario)
    {
        var lines = new List<string>();
        var data = PileGroupScenarios.FixtureData(root, scenario.Fixture);
        var side = new HistoricSide(data);
        try
        {
            lines.AddRange(side.State().Lines("apertura"));
            foreach (var step in scenario.Steps)
            {
                await side.Apply(step);
                lines.AddRange(side.State().Lines(step.ToString()));
            }
        }
        finally { side.Dispose(); }
        return lines;
    }

    /// <summary>The historic sheet driven through its controls, as the user does.</summary>
    private sealed class HistoricSide : IDisposable
    {
        private SheetEditor editor = null!;
        private HorizontalPileGroupWorkspace view = null!;
        private int modified;

        internal HistoricSide(JsonObject data) => Open(data);

        private void Open(JsonObject data)
        {
            editor = new SheetEditor(HorizontalPileGroupEditor.Module, data);
            view = editor.PileGroupForChecks ?? throw new InvalidOperationException("Foglio della palificata senza la vista storica.");
            modified = 0;
            editor.Modified += () => modified++;
            editor.Measure(new Size(1400, 1000)); editor.Arrange(new Rect(0, 0, 1400, 1000)); editor.UpdateLayout();
        }

        internal PileGroupTraceState State() => view.TraceForChecks(editor.Data, modified, editor.Result);

        internal async Task Apply(PileGroupStep step)
        {
            switch (step.Kind)
            {
                case PileGroupStep.Commit: editor.Commit(); break;
                case PileGroupStep.Calculate: await editor.CalculateAsync(); break;
                case PileGroupStep.Wait: await view.PendingForChecks; break;
                case PileGroupStep.ReadOnly: editor.InspectRevision(); break;
                case PileGroupStep.Reopen:
                    var data = editor.Data; editor.Dispose(); Open(data); break;
                case PileGroupStep.Field: view.TextBoxForChecks(step.Key!).Text = step.Text; break;
                case PileGroupStep.Option:
                    var box = view.CheckBoxForChecks(step.Key!);
                    box.IsChecked = box.IsChecked != true;
                    box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, box));
                    break;
                case PileGroupStep.Layout: view.LayoutBoxForChecks.SelectedItem = step.Text; break;
                case PileGroupStep.Cap: view.CapBoxForChecks.SelectedItem = step.Text; break;
                case PileGroupStep.Spacing: view.SpacingBoxForChecks.SelectedIndex = step.Index; break;
                case PileGroupStep.Pile: view.EditPileForChecks(step.Index, step.Key!, step.Text!); break;
                case PileGroupStep.AddPile: view.AddPileForChecks(); break;
                case PileGroupStep.RemovePile: view.RemovePileForChecks(step.Index); break;
                case PileGroupStep.CancelEdit: view.CancelEditForChecks(); break;
                case PileGroupStep.Select: view.SelectForChecks(step.Index); break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        public void Dispose() => editor.Dispose();
    }
}

internal sealed partial class SheetEditor
{
    /// <summary>UiTests: the historic view of the pile group (F5.4 traces).</summary>
    internal HorizontalPileGroupWorkspace? PileGroupForChecks => pileGroup;
}

internal sealed partial class HorizontalPileGroupWorkspace
{
    private static readonly string[] DirectionKeys = ["X", "Y", "X-", "Y-", "custom"];

    /// <summary>UiTests: the automatic recalculation started by the last change.</summary>
    internal Task PendingForChecks => scheduledCalculation;

    internal ComboBox LayoutBoxForChecks => Controls<ComboBox>().Single(c => Equals(c.Tag, "geometria"));
    internal ComboBox CapBoxForChecks => Controls<ComboBox>().Single(c => c.ItemsSource is string[] items && items.Contains("Contorno palificata"));
    internal ComboBox SpacingBoxForChecks => Controls<ComboBox>().Single(c => c.ItemsSource is string[] items && items.Contains("Personalizzata"));
    internal TextBox TextBoxForChecks(string key) => Controls<TextBox>().Single(t => Equals(t.Tag, key));
    internal CheckBox CheckBoxForChecks(string key) => Controls<CheckBox>().Single(c => Equals(c.Tag, key));

    /// <summary>A cell edited in the table: the binding writes the row at every keystroke.</summary>
    internal void EditPileForChecks(int index, string column, string text)
    {
        var row = piles[index];
        switch (column) { case "Id": row.Id = text; break; case "X": row.X = text; break; case "Y": row.Y = text; break; default: throw new ArgumentException(column); }
    }

    /// <summary>The new row of the table: the grid adds an empty <see cref="PileRow"/> to the collection.</summary>
    internal void AddPileForChecks() => piles.Add(new PileRow());

    internal void RemovePileForChecks(int index) => piles.RemoveAt(index);

    /// <summary>The handler of a cancelled cell edit (CellEditEnding), which the view posts to the dispatcher.</summary>
    internal void CancelEditForChecks() { CapturePiles(); Queue(); }

    internal void SelectForChecks(int index) => comparison.SelectedIndex = index;

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

    /// <summary>The state of the view for the trace of F5.4: what the controls show, read without changing them.</summary>
    internal PileGroupTraceState TraceForChecks(JsonObject sheetData, int modifiedCount, JsonObject? result)
    {
        var diameter = TextBoxForChecks("diametro");
        var trace = new PileGroupTraceState
        {
            Data = sheetData, Modified = modifiedCount, Result = result, Editable = !diameter.IsReadOnly,
            Title = title.Text, State = state.Text, Minimum = minValue.Text, Mean = meanValue.Text, Maximum = maxValue.Text, Reduction = reduction.Text,
            Geometry = geometry.Text, Source = source.Text, Notes = notes.Text,
            Comparison = (comparison.ItemsSource as IEnumerable<ComparisonRow>)?.Select(r => new[] { r.Direction, r.MethodName, r.Minimum, r.Mean, r.Maximum, r.Reduction, r.Status }).ToList() ?? [],
            SelectedComparison = comparison.SelectedIndex,
            Details = (details.ItemsSource as IEnumerable)?.Cast<object>().Select(DetailCells).ToList() ?? [],
            Piles = piles.Select(p => new[] { p.Id, p.X, p.Y }).ToList(),
            CoordinatesEditable = !coordinates.IsReadOnly, CanAddPiles = coordinates.CanUserAddRows, CoordinateHelp = coordinateHelp.Text,
            Layout = LayoutBoxForChecks.SelectedItem as string,
            Diameter = diameter.Text,
            Directions = DirectionKeys.Select(k => PileGroupTrace.DirectionLine(k, CheckBoxForChecks(k).IsChecked == true)).ToList(),
            CustomAngle = TextBoxForChecks("angolo_custom").Text, CustomAngleEnabled = TextBoxForChecks("angolo_custom").IsEnabled,
            CapShape = CapBoxForChecks.SelectedItem as string, CapEdge = TextBoxForChecks("bordo_d").Text,
            Extrapolations = CheckBoxForChecks("estrapolazioni").IsChecked == true, ProjectedRows = CheckBoxForChecks("file_proiettate").IsChecked == true,
            SpacingDirection = spacingDirection, SpacingIndex = SpacingBoxForChecks.SelectedIndex,
            ParallelSpacing = TextBoxForChecks("interasse_parallelo").Text, TransverseSpacing = TextBoxForChecks("interasse_trasversale").Text,
        };
        foreach (var child in generator.Children.Cast<UIElement>())
        {
            if (child is Grid row && row.Children.OfType<TextBox>().SingleOrDefault() is { } box)
                trace.Generator.Add(PileGroupTrace.FieldLine((string)box.Tag, LabelText(row), box.Text, box.IsEnabled));
            else if (child is CheckBox option) trace.Generator.Add(PileGroupTrace.OptionLine((string)option.Tag, ((TextBlock)option.Content).Text, option.IsChecked == true));
            else if (child is TextBlock note) trace.GeneratorNote = note.Text;
            else throw new InvalidOperationException("Elemento del generatore non previsto: " + child.GetType().Name);
        }
        var planType = typeof(PileGroupPlan);
        object? Field(string name) => planType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plan);
        trace.PlanShown = Field("data") is not null; trace.PlanSelected = (JsonNode?)Field("result"); trace.PlanCap = (JsonArray?)Field("cap");
        return trace;
    }

    private static string LabelText(Grid row) => ((DockPanel)row.Children[0]).Children.OfType<TextBlock>().Single().Text;

    private static object?[] DetailCells(object row)
    {
        object? Value(string name) => row.GetType().GetProperty(name)!.GetValue(row);
        return new[] { "Palo", "Fila", "Q", "T", "Beta", "Alfa", "Coefficiente", "Riduzione" }.Select(Value).ToArray();
    }
}
