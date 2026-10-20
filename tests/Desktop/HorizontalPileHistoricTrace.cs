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
using X.Core;

namespace X.Desktop;

/// <summary>
/// --check-palo-orizzontale-historic &lt;radice&gt; &lt;cartella&gt; and --check-palo-orizzontale-historic-capture &lt;radice&gt; &lt;cartella&gt;
/// (UiTests, F5.7, proposal F5 section 7.2): the scenarios of tests/Shared/HorizontalPileTrace.cs on the historic view of the
/// horizontal pile and micropile (<see cref="HorizontalWorkspace"/> inside <see cref="SheetEditor"/>), opened as the window opens it,
/// off screen, with the calculation service of the trace (<see cref="HeldModuleService"/>, which can hold the background calculation).
/// Every step is done as the user does it, through the controls: a control that the user cannot use (disabled, hidden, or protected by
/// the read-only revision) does nothing. After the opening and after every step the state seen by the user on the tab of the capacity is
/// written as text. Then the drawings: for every F3 fixture of the two modules with a result, the diagrams of «Diagrammi e dettagli»
/// (the figures of the report) and the profile with the forces, as display lists (lines, shapes, pens, brushes, texts) at two sizes.
/// The capture writes the traces and the lists in the folder; the check also compares them with those frozen in
/// tests/fixtures/f5-palo-orizzontale (the traces line by line, the lists through their hashes in disegni/elenco.txt), which the
/// ViewModel tests without WPF compare with HorizontalPileSheetViewModel. The check stays in the ui stage until the historic view is
/// archived (Q11). Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class HorizontalPileHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = HorizontalPileScenarios.All(root);
        string reference = HorizontalPileScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            var trace = await Trace(root, scenario);
            HorizontalPileTrace.Write(Path.Combine(output, scenario.Name + ".txt"), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? HorizontalPileTrace.FirstDifference(HorizontalPileTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
        }
        var drawings = await Drawings(root, Path.Combine(output, "disegni"), Path.Combine(output, "diagrammi"));
        HorizontalPileTrace.Write(Path.Combine(output, "disegni", "elenco.txt"), drawings);
        string drawingList = Path.Combine(DrawingsReference(root), "elenco.txt");
        if (check)
        {
            var names = scenarios.Select(s => s.Name + ".txt").ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt").Select(n => Path.GetFileName(n)).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            string? drawingDifference = File.Exists(drawingList) ? HorizontalPileTrace.FirstDifference(HorizontalPileTrace.Read(drawingList), drawings) : "elenco dei disegni di riferimento assente";
            if (drawingDifference is not null) differences.Add("disegni: " + drawingDifference);
            string frozen = HorizontalPileScenarios.Diagrams(root), captured = Path.Combine(output, "diagrammi");
            var diagramNames = Directory.EnumerateFiles(captured, "*.json").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();
            var storedNames = Directory.Exists(frozen) ? Directory.EnumerateFiles(frozen, "*.json").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray() : [];
            if (!diagramNames.SequenceEqual(storedNames)) differences.Add("diagrammi: file diversi dal riferimento (" + string.Join(", ", diagramNames) + ")");
            foreach (string name in diagramNames.Intersect(storedNames))
                if (File.ReadAllText(Path.Combine(frozen, name)).Replace("\r\n", "\n") != File.ReadAllText(Path.Combine(captured, name)).Replace("\r\n", "\n"))
                    differences.Add("diagrammi: " + name + " diverso dal riferimento");
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Vista storica del palo orizzontale diversa dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe della vista storica, {drawings.Count} disegni"
            + (check ? " uguali a quelli congelati" : " catturati"), new UTF8Encoding(false));
    }

    /// <summary>Folder of the drawings of the historic view, frozen before the migration.</summary>
    internal static string DrawingsReference(string root) => Path.Combine(root, "tests", "fixtures", "f5-palo-orizzontale", "disegni");

    static async Task<List<string>> Trace(string root, HorizontalPileScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(scenario.Module);
        var side = new HistoricSide(scenario.Module, HorizontalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture), engine);
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

        private HorizontalWorkspace Sheet => editor.HorizontalForChecks;

        private void Open(JsonObject data)
        {
            editor = new SheetEditor(module, data, engine);
            modified = 0;
            editor.Modified += () => modified++;
            Layout();
        }

        private void Layout() { editor.Measure(new Size(1400, 1000)); editor.Arrange(new Rect(0, 0, 1400, 1000)); editor.UpdateLayout(); }

        internal HorizontalPileTraceState State() => Sheet.TraceForChecks(modified);

        internal async Task Apply(HorizontalPileStep step)
        {
            switch (step.Kind)
            {
                case HorizontalPileStep.Commit: editor.Commit(); break;
                case HorizontalPileStep.Calculate: await editor.CalculateAsync(); break;
                case HorizontalPileStep.Start: _ = editor.CalculateAsync(); break;
                case HorizontalPileStep.Wait: await Sheet.PendingForChecks; break;
                case HorizontalPileStep.ReadOnly: editor.InspectRevision(); break;
                case HorizontalPileStep.Reopen:
                    var data = editor.Data; editor.Dispose(); Open(data); break;
                case HorizontalPileStep.Hold: engine.Hold(); break;
                case HorizontalPileStep.Entered: await engine.EnteredAsync(); break;
                case HorizontalPileStep.Release: engine.Release(); break;
                default: Sheet.ApplyForChecks(step, Layout); break;
            }
            Layout();
        }

        public void Dispose() => editor.Dispose();
    }

    // ---------------------------------------------------------------- drawings

    /// <summary>
    /// The display lists of the diagrams of every survey (soil and pile, at the size of the report and at a larger one) and of the
    /// profile with the forces, for every F3 fixture of the two modules that has a result after «Calcola». The full lists go into
    /// <paramref name="output"/>; the returned lines (one per drawing: primitives and hash) are compared with disegni/elenco.txt.
    /// </summary>
    static async Task<List<string>> Drawings(string root, string output, string diagrams)
    {
        Directory.CreateDirectory(output); Directory.CreateDirectory(diagrams);
        var list = new List<string>();
        foreach (string module in new[] { HorizontalPileScenarios.Pile, HorizontalPileScenarios.Micro })
            foreach (string id in HorizontalPileScenarios.Fixtures(root, module))
            {
                var data = HorizontalPileScenarios.FixtureData(root, module, id);
                if (HorizontalPileScenarios.OpensElastic(data)) continue;
                using var editor = new SheetEditor(module, data);
                editor.Commit(); await editor.CalculateAsync(); editor.Commit();
                var sheet = editor.HorizontalForChecks;
                if (sheet.Result is not { } result) continue;
                int surveys = result.Array("sondaggi").Count;
                // The plain data that HorizontalDiagrams draws (the display lists below prove the drawing), which the ViewModel exposes.
                File.WriteAllText(Path.Combine(diagrams, module + "--" + id + ".json"), HorizontalPileTrace.Diagrams(Enumerable.Range(0, surveys)
                    .SelectMany(i => new[] { true, false }.Select(soil => Anthea.Application.Presentation.HorizontalPileDiagrams.Build(result, i, soil)))), new UTF8Encoding(false));
                for (int i = 0; i < surveys; i++)
                {
                    foreach (bool soil in new[] { true, false })
                        foreach (var (w, h) in new[] { (1000, 660), (1280, 820) })
                            list.Add(Record(output, $"{module}--{id} sondaggio {i + 1} {(soil ? "terreno" : "palo")} {w}x{h}",
                                new HorizontalDiagrams(result, i, soil), w, h));
                    list.Add(Record(output, $"{module}--{id} profilo {i + 1} 420x640",
                        new StratigraphyDrawing { Data = sheet.Data, ShowAll = false, HorizontalForces = true, HorizontalResult = result, SelectedIndex = i }, 420, 640));
                }
            }
        return list;
    }

    internal static string Record(string output, string name, FrameworkElement drawing, int width, int height)
    {
        drawing.Width = width; drawing.Height = height;
        // Frozen display lists describe exact DIP sizes, independent of the host's pixel rounding.
        // At 125%, inherited rounding would turn 1750 × 650 into 1750.4 × 649.6.
        object rounding = drawing.ReadLocalValue(FrameworkElement.UseLayoutRoundingProperty);
        var lines = new List<string>();
        try
        {
            drawing.UseLayoutRounding = false;
            drawing.Measure(new Size(width, height)); drawing.Arrange(new Rect(0, 0, width, height)); drawing.UpdateLayout();
            if (VisualTreeHelper.GetDrawing(drawing) is { } content) DisplayList(content, lines);
        }
        finally
        {
            if (rounding == DependencyProperty.UnsetValue) drawing.ClearValue(FrameworkElement.UseLayoutRoundingProperty);
            else drawing.SetValue(FrameworkElement.UseLayoutRoundingProperty, rounding);
        }
        string file = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')) + ".txt";
        File.WriteAllLines(Path.Combine(output, file), lines, new UTF8Encoding(false));
        return name + " · primitivi " + lines.Count.ToString(CultureInfo.InvariantCulture) + " " + HorizontalPileTrace.Hash((object)lines);
    }

    static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    static string P(Point p) => N(p.X) + "," + N(p.Y);

    static string Brush(Brush? brush) => brush switch
    {
        null => "-",
        SolidColorBrush s => s.Color.ToString(CultureInfo.InvariantCulture) + (s.Opacity == 1 ? "" : "@" + N(s.Opacity)),
        _ => brush.GetType().Name
    };

    static string Pen(Pen? pen) => pen is null ? "-" : Brush(pen.Brush) + " " + N(pen.Thickness)
        + (pen.DashStyle is { Dashes.Count: > 0 } dash ? " tratto " + string.Join(";", dash.Dashes.Select(N)) + "+" + N(dash.Offset) : "");

    static string Geometry(System.Windows.Media.Geometry? geometry) => geometry switch
    {
        null => "-",
        LineGeometry l => "linea " + P(l.StartPoint) + " " + P(l.EndPoint),
        RectangleGeometry r => "rettangolo " + N(r.Rect.X) + "," + N(r.Rect.Y) + "," + N(r.Rect.Width) + "," + N(r.Rect.Height) + " " + N(r.RadiusX),
        EllipseGeometry e => "ellisse " + P(e.Center) + " " + N(e.RadiusX) + " " + N(e.RadiusY),
        _ => "percorso " + PathGeometry.CreateFromGeometry(geometry).ToString(CultureInfo.InvariantCulture)
    };

    static void DisplayList(Drawing drawing, List<string> lines)
    {
        switch (drawing)
        {
            case DrawingGroup group:
                lines.Add("gruppo " + N(group.Opacity) + " " + Geometry(group.ClipGeometry) + " " + (group.Transform is null ? "-" : group.Transform.Value.ToString(CultureInfo.InvariantCulture)));
                foreach (var child in group.Children) DisplayList(child, lines);
                lines.Add("fine");
                break;
            case GeometryDrawing shape:
                lines.Add("forma " + Geometry(shape.Geometry) + " riempimento " + Brush(shape.Brush) + " penna " + Pen(shape.Pen));
                break;
            case GlyphRunDrawing text:
                var run = text.GlyphRun;
                lines.Add("testo " + HorizontalPileTrace.Quote(new string(run.Characters.ToArray())) + " " + P(run.BaselineOrigin) + " " + N(run.FontRenderingEmSize)
                    + " " + Brush(text.ForegroundBrush));
                break;
            default:
                lines.Add(drawing.GetType().Name);
                break;
        }
    }
}

internal sealed partial class SheetEditor
{
    /// <summary>UiTests (F5.7 traces): the historic view of the horizontal pile and micropile.</summary>
    internal HorizontalWorkspace HorizontalForChecks => horizontal ?? throw new InvalidOperationException("Foglio non orizzontale.");
}

internal sealed partial class HorizontalWorkspace
{
    private static readonly FieldInfo InputFormRows = typeof(InputForm).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "rows");

    /// <summary>UiTests: the last calculation of the capacity requested (the automatic one or the one of «Calcola»).</summary>
    internal Task PendingForChecks => scheduledCalculation;

    private static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    private static void Click(Button button) { if (Usable(button)) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); }

    private IEnumerable<(string Name, InputForm Form)> Forms() => new[] { ("generali", general), ("modello", model), ("coefficienti", factors), ("efficienza", efficiency),
        ("momento", moment), ("sezione", sectionFields), ("ej", ejForm) };

    private FrameworkElement? FormEditor(string key) => Forms().Select(f => f.Form.Editors.GetValueOrDefault(key)).SingleOrDefault(e => e is not null);

    private Button SurveyButton(string content) =>
        ((Panel)((ContentControl)surveys.Template.FindName("SurveyActions", surveys)).Content).Children.OfType<Button>().Single(b => Equals(b.Content, content));

    private Button HeaderButton(int survey) => ((Panel)((TabItem)surveys.Items[survey]).Header).Children.OfType<Button>().Single(b => Equals(b.Content, "[−]"));

    /// <summary>The soil editors realized for the rows of the selected stratigraphy (after the layout), by row.</summary>
    private Dictionary<JsonRow, HorizontalSoilEditor> SoilEditors()
    {
        var found = new Dictionary<JsonRow, HorizontalSoilEditor>();
        if (surveys.SelectedIndex < 0 || surveys.SelectedIndex >= grids.Count) return found;
        foreach (var editor in Ui.Descendants<HorizontalSoilEditor>(grids[surveys.SelectedIndex]))
            if (editor.DataContext is JsonRow row && editor.Content is Panel) found[row] = editor;
        return found;
    }

    /// <summary>UiTests (F5.7 traces): a step of the user on the controls of the horizontal pile view; a control the user cannot use does nothing.</summary>
    internal void ApplyForChecks(HorizontalPileStep step, Action layout)
    {
        void Select(int survey) { surveys.SelectedIndex = survey; layout(); }
        switch (step.Kind)
        {
            case HorizontalPileStep.Field:
                if (FormEditor(step.Key!) is TextBox text && Usable(text) && !text.IsReadOnly) text.Text = step.Text;
                break;
            case HorizontalPileStep.Choice:
                if (FormEditor(step.Key!) is ComboBox choice && Usable(choice)) choice.SelectedItem = step.Text;
                break;
            case HorizontalPileStep.Option:
                if (FormEditor(step.Key!) is CheckBox option && Usable(option)) option.IsChecked = option.IsChecked != true;
                break;
            case HorizontalPileStep.Analysis: analyses.SelectedIndex = step.Index; break;
            case HorizontalPileStep.Survey: Select(step.Index); break;
            case HorizontalPileStep.AddSurvey: Click(SurveyButton("+ Stratigrafia")); break;
            case HorizontalPileStep.RemoveSurvey:
                // The [−] of the tab asks for a confirmation (message box): the trace answers yes.
                if (Usable(HeaderButton(step.Index))) DeleteSurvey(Data.Array("stratigrafie")[step.Index]!.AsArray(), confirm: false);
                break;
            case HorizontalPileStep.RemoveSelectedSurvey:
                if (Usable(SurveyButton("− Stratigrafia")) && surveys.SelectedIndex >= 0) DeleteSurvey(Data.Array("stratigrafie")[surveys.SelectedIndex]!.AsArray(), confirm: false);
                break;
            case HorizontalPileStep.CopySurvey:
                // «Copia in…» copies the selected stratigraphy; the dialog chooses the destination, the question is answered yes.
                Select(step.Index);
                if (Usable(SurveyButton("Copia in…"))) CopySurvey(step.Index, step.Text == "nuova" ? -1 : int.Parse(step.Text!, CultureInfo.InvariantCulture), confirm: false);
                break;
            case HorizontalPileStep.CopyFrom:
                // «Copia da…» replaces the selected stratigraphy with the one chosen in the dialog, the question is answered yes.
                Select(step.Index);
                if (Usable(SurveyButton("Copia da…"))) CopySurvey(int.Parse(step.Text!, CultureInfo.InvariantCulture), step.Index, confirm: false);
                break;
            case HorizontalPileStep.Layer:
            {
                Select(step.Index);
                var grid = grids[step.Index];
                if (grid.IsReadOnly || !Usable(grid)) break;
                // The bindings of the cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged).
                grid.Rows[step.Row][step.Key!] = step.Text;
                break;
            }
            case HorizontalPileStep.AddLayer:
                Select(step.Index);
                Click(Find<Button>((DependencyObject)((TabItem)surveys.Items[step.Index]).Content, b => Equals(b.Content, "Aggiungi strato")));
                break;
            case HorizontalPileStep.RemoveLayer:
            {
                Select(step.Index);
                var grid = grids[step.Index];
                var row = grid.Rows[step.Row];
                Click(Ui.Descendants<Button>(grid).Single(b => ReferenceEquals(b.DataContext, row) && Equals(b.Content, "[−]")));
                break;
            }
            case HorizontalPileStep.Soil:
            {
                Select(step.Index);
                var row = grids[step.Index].Rows[step.Row];
                if (!SoilEditors().TryGetValue(row, out var editor)) break;
                var parts = ((Panel)editor.Content).Children.Cast<UIElement>().ToList();
                switch (step.Key)
                {
                    case HorizontalPileStep.SoilCategory: if (parts[0] is ComboBox category && Usable(category)) category.SelectedItem = step.Text; break;
                    case HorizontalPileStep.SoilLaw:
                        if (parts[1] is ComboBox law && Usable(law) && ((string[])law.ItemsSource).ToList().IndexOf(step.Text!) is int index and >= 0) law.SelectedIndex = index;
                        break;
                    case HorizontalPileStep.SoilValue: if (parts[5] is TextBox value && Usable(value) && !value.IsReadOnly) value.Text = step.Text; break;
                    case HorizontalPileStep.SoilOverride:
                        if (parts.OfType<CheckBox>().FirstOrDefault() is { } box && Usable(box))
                        { box.IsChecked = box.IsChecked != true; box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, box)); }
                        break;
                    case HorizontalPileStep.SoilNh:
                        if (parts.Count > 8 && parts[8] is TextBox nh && Usable(nh) && !nh.IsReadOnly) nh.Text = step.Text;
                        break;
                    default: throw new ArgumentException("Comando del terreno sconosciuto: " + step.Key);
                }
                break;
            }
            default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
        }
    }

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

    /// <summary>UiTests (F5.7 traces): the state of the tab of the capacity, read from its controls without changing them.</summary>
    internal HorizontalPileTraceState TraceForChecks(int modifiedCount)
    {
        var outcome = ((StackPanel)verification.Content).Children.OfType<TextBlock>().Single();
        var soils = SoilEditors();
        var state = new HorizontalPileTraceState
        {
            Data = Data, Modified = modifiedCount, Editable = !((TextBox)general.Editors["diametro"]).IsReadOnly, Exportable = Result is not null && !Busy,
            ResultsShown = details.IsEnabled && csv.IsEnabled, Analysis = analyses.SelectedIndex, Result = Result,
            Status = status.Text, Summary = summary.Text, Moment = momentValue.Text, MomentToolTip = momentValue.ToolTip as string,
            Model = modelLabel.Text, ModelHint = modelLabel.ToolTip as string, Warnings = warnings.Text,
            Forms = Forms().Select(f => new HorizontalPileFormState(f.Name, FormState(f.Form), GroupState(f.Form))).ToList(),
            Verification = ((((StackPanel)verification.Content).Children[0] as DataGrid)?.ItemsSource as IEnumerable<string[]>)?.ToList() ?? [],
            Outcome = outcome.Text, OutcomeKind = OutcomeKind(outcome.Foreground),
            SelectedSurvey = surveys.SelectedIndex, ProfileSurvey = profile.SelectedIndex,
            Surveys = grids.Select(g => g.Rows.Select(r => r.Values).ToList()).ToList(),
            Soils = surveys.SelectedIndex >= 0 && surveys.SelectedIndex < grids.Count ? grids[surveys.SelectedIndex].Rows.Select(r => soils.TryGetValue(r, out var e) ? SoilState(e) : null).ToList() : [],
            ConcreteSection = MicroHorizontal ? null : HorizontalPileTrace.ConcreteSection(sectionDrawing.Outline, sectionDrawing.Bars.Select(b => (b.X, b.Y, b.Area, b.Diametro)),
                sectionDrawing.CircularLinkRadius, sectionDrawing.LinkDiameter, sectionDrawing.ShowAxes, sectionDrawing.Caption),
            Chs = MicroHorizontal ? chsDrawing.Properties : null,
        };
        if (Result is not null) state.Results = ResultState();
        return state;
    }

    private static string OutcomeKind(Brush brush)
    {
        string colour = ((SolidColorBrush)brush).Color.ToString(CultureInfo.InvariantCulture);
        return colour == ((SolidColorBrush)Ui.Brush("#16703C")).Color.ToString(CultureInfo.InvariantCulture) ? "soddisfatta"
            : colour == ((SolidColorBrush)Ui.Brush("#B42318")).Color.ToString(CultureInfo.InvariantCulture) ? "non-soddisfatta"
            : colour == ((SolidColorBrush)Ui.Muted).Color.ToString(CultureInfo.InvariantCulture) ? "attenuato" : "colore " + colour;
    }

    private static List<VerticalPileFieldState> FormState(InputForm form)
    {
        var rows = (Dictionary<string, List<FrameworkElement>>)InputFormRows.GetValue(form)!;
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

    /// <summary>The groups of a form (GroupFields): the title, the keys in the order of the group and whether it is open.</summary>
    private static List<HorizontalPileGroupState> GroupState(InputForm form)
    {
        var rows = (Dictionary<string, List<FrameworkElement>>)InputFormRows.GetValue(form)!;
        if (form.Content is not StackPanel panel) return [];
        return panel.Children.OfType<Expander>().Select(expander =>
        {
            var keys = rows.Where(r => ReferenceEquals(r.Value[0].Parent, expander.Content)).OrderBy(r => Grid.GetRow(r.Value[0])).Select(r => r.Key).ToList();
            return new HorizontalPileGroupState(((TextBlock)expander.Header).Text, keys, expander.IsExpanded);
        }).ToList();
    }

    private static HorizontalSoilState SoilState(HorizontalSoilEditor editor)
    {
        var parts = ((Panel)editor.Content).Children.Cast<UIElement>().ToList();
        var category = (ComboBox)parts[0]; var law = (ComboBox)parts[1];
        bool editable = parts[5] is TextBox;
        var over = parts.OfType<CheckBox>().FirstOrDefault();
        return new HorizontalSoilState(((IEnumerable<string>)category.ItemsSource).ToArray(), category.SelectedItem as string ?? "", (string[])law.ItemsSource, law.SelectedIndex,
            ((TextBlock)parts[2]).Text, ((TextBlock)parts[3]).Text, ((TextBlock)parts[4]).Text,
            editable ? ((TextBox)parts[5]).Text : ((TextBlock)parts[5]).Text, editable, over is not null, over?.IsChecked == true,
            parts.Count > 8 && parts[8] is TextBox nh ? nh.Text : null, editor.ToolTip as string ?? "");
    }

    /// <summary>What «Diagrammi e dettagli» shows for every survey: the three tables (title of the tab, columns, rows) and the line under them.</summary>
    private List<HorizontalPileSurveyResultState> ResultState()
    {
        var states = new List<HorizontalPileSurveyResultState>();
        foreach (var tab in ResultsTabs().Items.Cast<TabItem>().Where(t => ((string)t.Header).StartsWith("Sondaggio ", StringComparison.Ordinal)))
        {
            var dock = (DockPanel)tab.Content;
            var info = dock.Children.OfType<TextBlock>().Single();
            var panel = dock.Children.OfType<TabControl>().Single();
            var tables = panel.Items.Cast<TabItem>().Where(t => t.Content is DataGrid).Select(t =>
            {
                var grid = (DataGrid)t.Content;
                return new HorizontalPileTableState((string)t.Header, grid.Columns.Select(c => Convert.ToString(c.Header, CultureInfo.InvariantCulture) ?? "").ToArray(),
                    ((IEnumerable<string[]>)grid.ItemsSource).ToArray());
            }).ToList();
            states.Add(new HorizontalPileSurveyResultState(tables, info.Text));
        }
        return states;
    }
}
