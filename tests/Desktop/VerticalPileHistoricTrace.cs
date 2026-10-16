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
using Anthea.Presentation.Tests;

namespace X.Desktop;

/// <summary>
/// --check-palo-verticale-historic &lt;radice&gt; &lt;cartella&gt; and --check-palo-verticale-historic-capture &lt;radice&gt; &lt;cartella&gt;
/// (UiTests, F5.6, proposal F5 section 7.2): the scenarios of tests/Shared/VerticalPileTrace.cs on the historic view of the vertical
/// pile and micropile (the generic branch of <see cref="SheetEditor"/> and GeoEditor), opened as the window opens it, off screen,
/// with the calculation service of the trace (<see cref="HeldModuleService"/>, which can hold the background calculation). Every
/// step is done as the user does it, through the controls: a control that the user cannot use (disabled, or protected by the
/// read-only revision) does nothing. After the opening and after every step the state seen by the user is written as text: data in
/// memory order, Modified, result, status, warnings, forms, verification, stratigraphies, charts, tables and JSON. The capture writes
/// the traces in the folder; the check also compares them with the traces frozen in tests/fixtures/f5-palo-verticale/storica, which
/// the ViewModel tests without WPF compare with VerticalPileSheetViewModel. The check stays in the ui stage until the historic view
/// is archived (Q11). Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class VerticalPileHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = VerticalPileScenarios.All(root);
        string reference = VerticalPileScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            var trace = await Trace(root, scenario);
            VerticalPileTrace.Write(Path.Combine(output, scenario.Name + ".txt"), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? VerticalPileTrace.FirstDifference(VerticalPileTrace.Read(stored), trace) : "traccia di riferimento assente";
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
                throw new InvalidOperationException("Vista storica del palo verticale diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica"
            + (check ? " uguali alle tracce congelate" : " catturate"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, VerticalPileScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(scenario.Module);
        var side = new HistoricSide(scenario.Module, VerticalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture), engine);
        try
        {
            lines.AddRange(side.State().Lines("apertura"));
            foreach (var step in scenario.Steps)
            {
                await side.Apply(step);
                lines.AddRange(side.State().Lines(step.ToString()));
            }
        }
        finally { engine.Release(); side.Dispose(); }
        return lines;
    }

    /// <summary>The historic sheet driven through its controls, as the user does.</summary>
    private sealed class HistoricSide : IDisposable
    {
        private readonly string module;
        private readonly HeldModuleService engine;
        private SheetEditor editor = null!;
        private int modified;

        internal HistoricSide(string module, JsonObject data, HeldModuleService engine)
        {
            this.module = module; this.engine = engine;
            Open(data);
        }

        private void Open(JsonObject data)
        {
            editor = new SheetEditor(module, data, engine);
            modified = 0;
            editor.Modified += () => modified++;
            Layout();
        }

        private void Layout() { editor.Measure(new Size(1400, 1000)); editor.Arrange(new Rect(0, 0, 1400, 1000)); editor.UpdateLayout(); }

        internal VerticalPileTraceState State() => editor.VerticalTraceForChecks(modified);

        internal async Task Apply(VerticalPileStep step)
        {
            switch (step.Kind)
            {
                case VerticalPileStep.Commit: editor.Commit(); break;
                case VerticalPileStep.Calculate: await editor.CalculateAsync(); break;
                case VerticalPileStep.Start: _ = editor.CalculateAsync(); break;
                case VerticalPileStep.Wait: await editor.VerticalPendingForChecks; break;
                case VerticalPileStep.ReadOnly: editor.InspectRevision(); break;
                case VerticalPileStep.Reopen:
                    var data = editor.Data; editor.Dispose(); Open(data); break;
                case VerticalPileStep.Hold: engine.Hold(); break;
                case VerticalPileStep.Entered: await engine.EnteredAsync(); break;
                case VerticalPileStep.Release: engine.Release(); break;
                default: editor.ApplyVerticalForChecks(step, Layout); break;
            }
            Layout();
        }

        public void Dispose() => editor.Dispose();
    }
}

internal sealed partial class SheetEditor
{
    private static readonly FieldInfo FormRows = typeof(InputForm).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "rows");

    /// <summary>UiTests: the last calculation requested (the automatic one or the one of «Calcola»).</summary>
    internal Task VerticalPendingForChecks => scheduledCalculation;

    /// <summary>UiTests (F5.6 traces): a step of the user on the controls of the vertical pile view; a control the user cannot use does nothing.</summary>
    internal void ApplyVerticalForChecks(VerticalPileStep step, Action layout)
    {
        static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;
        static void Click(Button button) { if (Usable(button)) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); }
        void Select(int survey) { sondages.SelectedIndex = survey; layout(); }
        switch (step.Kind)
        {
            case VerticalPileStep.Field:
                if (FormEditor(step.Key!) is TextBox text && Usable(text) && !text.IsReadOnly) text.Text = step.Text;
                break;
            case VerticalPileStep.Choice:
                if (FormEditor(step.Key!) is ComboBox choice && Usable(choice)) choice.SelectedItem = step.Text;
                break;
            case VerticalPileStep.Option:
                if (FormEditor(step.Key!) is CheckBox option && Usable(option)) option.IsChecked = option.IsChecked != true;
                break;
            case VerticalPileStep.Reset: Click(CardButton(2, "Reset")); break;
            case VerticalPileStep.Survey: Select(step.Index); break;
            case VerticalPileStep.AddSurvey: Click(CardButton(4, "+")); break;
            case VerticalPileStep.RemoveSurvey:
                // The [−] of the tab asks for a confirmation (message box): the trace answers yes.
                if (Usable(HeaderButton(step.Index))) DeleteSurvey(Data.Array("stratigrafie")[step.Index]!.AsArray(), confirm: false);
                break;
            case VerticalPileStep.RemoveSelectedSurvey:
                if (Usable(CardButton(4, "−")) && sondages.SelectedIndex >= 0) DeleteSurvey(Data.Array("stratigrafie")[sondages.SelectedIndex]!.AsArray(), confirm: false);
                break;
            case VerticalPileStep.CopySurvey:
                // «Copia in…» copies the selected stratigraphy; the dialog chooses the destination, the question is answered yes.
                Select(step.Index);
                if (Usable(CardButton(4, "Copia in…"))) CopySurvey(step.Index, step.Text == "nuova" ? -1 : int.Parse(step.Text!, CultureInfo.InvariantCulture), confirm: false);
                break;
            case VerticalPileStep.Layer:
            {
                Select(step.Index);
                var grid = layerGrids[step.Index];
                if (grid.IsReadOnly || !Usable(grid)) break;
                var row = grid.Rows[step.Row];
                // The bindings of the cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged).
                if (step.Text is null) row[step.Key!] = !(row[step.Key!] is true);
                else row[step.Key!] = step.Text;
                break;
            }
            case VerticalPileStep.AddLayer:
                Select(step.Index);
                Click(Find<Button>((DependencyObject)((TabItem)sondages.Items[step.Index]).Content, b => Equals(b.Content, "Aggiungi strato")));
                break;
            case VerticalPileStep.RemoveLayer:
            {
                Select(step.Index);
                var grid = layerGrids[step.Index];
                var row = grid.Rows[step.Row];
                Click(Ui.Descendants<Button>(grid).Single(b => ReferenceEquals(b.DataContext, row) && Equals(b.Content, "[−]")));
                break;
            }
            case VerticalPileStep.CapacityView: if (Usable(capacityView)) capacityView.SelectedIndex = step.Index; break;
            case VerticalPileStep.Curve:
            {
                var shown = allSeries.Where(p => CapacityIncludes(p.Key)).Select(p => p.Key).ToList();
                int index = shown.IndexOf(step.Key!);
                if (index >= 0 && curveChoices.Children[index] is CheckBox box && Usable(box)) box.IsChecked = box.IsChecked != true;
                break;
            }
            case VerticalPileStep.AllCurves: Click(((Panel)endValues.Parent).Children.OfType<Button>().Single(b => Equals(b.Content, "Tutte"))); break;
            case VerticalPileStep.NoCurves: Click(((Panel)endValues.Parent).Children.OfType<Button>().Single(b => Equals(b.Content, "Nessuna"))); break;
            case VerticalPileStep.Table: if (Usable(tableSelect)) tableSelect.SelectedIndex = step.Index; break;
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
    }

    private FrameworkElement? FormEditor(string key) => new[] { generalForm, efficiencyForm, normativeForm }
        .Select(f => f.Editors.GetValueOrDefault(key)).SingleOrDefault(e => e is not null);

    private Button CardButton(int card, string content) => Find<Button>(cards[card], b => Equals(b.Content, content));

    private Button HeaderButton(int survey) => Find<Button>((DependencyObject)((TabItem)sondages.Items[survey]).Header, b => Equals(b.Content, "[−]"));

    private static T Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        var pending = new Stack<object>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is T found && match(found)) return found;
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element).Cast<object>().Reverse()) pending.Push(child);
        }
        throw new InvalidOperationException("Controllo della vista storica non trovato: " + typeof(T).Name);
    }

    /// <summary>UiTests (F5.6 traces): the state of the vertical pile view, read from its controls without changing them.</summary>
    internal VerticalPileTraceState VerticalTraceForChecks(int modifiedCount) => new()
    {
        Data = Data, Modified = modifiedCount, Editable = !((TextBox)generalForm.Editors["diametro"]).IsReadOnly, Exportable = HasResults && !Busy,
        Result = Result, Status = status.Text, Warnings = warnings.Text,
        General = FormState(generalForm), Efficiency = FormState(efficiencyForm), Normative = FormState(normativeForm), EfficiencyText = effLabel.Text,
        Verification = ((verification.Content as DataGrid)?.ItemsSource as IEnumerable<string[]>)?.ToList() ?? [],
        SelectedSurvey = sondages.SelectedIndex, ProfileSurvey = stratigraphy.SelectedIndex,
        Surveys = layerGrids.Select(g => g.Rows.Select(r => r.Values).ToList()).ToList(),
        CapacityViews = (string[])capacityView.ItemsSource, CapacityView = capacityView.SelectedIndex,
        Capacity = ChartState(plot), Reference = ChartState(reference), EndValues = endValues.Text,
        Curves = curveChoices.Children.OfType<CheckBox>().Select(c => ((string)c.Content, c.IsChecked == true)).ToList(),
        Tables = tableSelect.Items.Cast<string>().ToList(), SelectedTable = tableSelect.SelectedIndex, Shown = ShownTableState(), Raw = raw.Text,
    };

    private static List<VerticalPileFieldState> FormState(InputForm form)
    {
        var rows = (Dictionary<string, List<FrameworkElement>>)FormRows.GetValue(form)!;
        return form.Editors.Select(pair =>
        {
            // The first element of the row is its label: InputForm.Enable and ShowField change every element of the row, while the
            // protection of the revision disables only the editors.
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

    private static VerticalPileChartState ChartState(Plot plot) => new()
    {
        Title = plot.Title, XLabel = plot.XLabel, YLabel = plot.YLabel, Note = plot.Note, EmptyMessage = plot.EmptyMessage,
        CapacityDepth = plot.CapacityDepth, XMinimum = plot.XMinimum,
        Series = plot.Series.Select(s => new VerticalPileSeriesState(s.Name, s.Color.ToString(), s.Dashed, s.Highlighted,
            s.DashPattern is null ? null : ReferenceEquals(s.DashPattern, DashStyles.Dash) ? "Dash" : ReferenceEquals(s.DashPattern, DashStyles.Dot) ? "Dot" : "?", s.Points)).ToList(),
        Markers = plot.Markers.Select(m => new VerticalPileMarkerState(m.X, m.Y, m.Label, m.Color.ToString(), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius)).ToList(),
    };

    private VerticalPileTableState? ShownTableState()
    {
        static (string, IReadOnlyList<string>, IReadOnlyList<string[]>) Table(string title, DataGrid grid) =>
            (title, grid.Columns.Select(c => c.Header is TextBlock header ? header.Text : Convert.ToString(c.Header, CultureInfo.InvariantCulture) ?? "").ToArray(),
                ((IEnumerable)grid.ItemsSource).Cast<string[]>().ToArray());
        switch (tableHost.Content)
        {
            case null: return null;
            case DataGrid grid: return new VerticalPileTableState { Tables = [Table("", grid)] };
            case ScrollViewer { Content: StackPanel panel }:
            {
                var state = new VerticalPileTableState();
                var children = panel.Children.Cast<UIElement>().ToList();
                state.Note = ((TextBlock)children[0]).Text;
                int i = 1;
                for (; i + 1 < children.Count && children[i + 1] is DataGrid grid; i += 2) state.Tables.Add(Table(((TextBlock)children[i]).Text, grid));
                if (i < children.Count) state.Footnote = ((TextBlock)children[i]).Text;
                return state;
            }
            default: throw new InvalidOperationException("Contenuto della tabella non previsto: " + tableHost.Content.GetType().Name);
        }
    }
}
