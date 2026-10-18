using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Tests;
using X.Core;
using X.Desktop.Views.Common;
using X.Desktop.Views.HorizontalPile;

namespace X.Desktop;

/// <summary>The tab of the capacity (F5.7): the traces and the drawings through the view.</summary>
internal static partial class HorizontalPileViewChecks
{
    /// <summary>
    /// The tab of the capacity read and driven through the controls of the view, as the historic trace does with HorizontalWorkspace: a control the
    /// user cannot use (disabled, hidden, read-only) does nothing. The controls are searched in the page of the capacity, also while the tab of the
    /// elastic response is shown (the page keeps its controls, as the historic tab). The content of the expanders is laid out once at the opening
    /// (each closed expander opened and closed again, its state unchanged), so that the fields of the closed groups can be read and typed as in
    /// the historic InputForm, which builds every editor.
    /// </summary>
    private sealed class CapacitySide(Hosted host, HeldModuleService? engine, Services services)
    {
        private Task latest = Task.CompletedTask, observedPending = Task.CompletedTask;
        private HorizontalPileSheetViewModel Sheet => host.Sheet;
        private HorizontalPileSheetView View => host.View;
        private DependencyObject Page => View.CapacityPage;

        internal void Attach()
        {
            latest = observedPending = Sheet.PendingCalculation;
            RealizeExpanders();
        }

        /// <summary>
        /// Lays out the content of the closed expanders of the page once (open, layout, closed again). A sheet opened on the tab of the elastic
        /// response shows the page of the capacity for this layout only, without its binding to the sheet (the choice of the tab is not written):
        /// the historic view builds its controls in any case.
        /// </summary>
        internal void RealizeExpanders()
        {
            var tabs = View.Analyses;
            BindingBase? binding = null;
            if (tabs.SelectedIndex != 0)
            {
                binding = BindingOperations.GetBindingBase(tabs, Selector.SelectedIndexProperty);
                BindingOperations.ClearBinding(tabs, Selector.SelectedIndexProperty);
                tabs.SelectedIndex = 0;
            }
            host.Layout();
            for (int pass = 0; pass < 3; pass++)
            {
                var closed = Visual(Page).OfType<Expander>().Where(e => !e.IsExpanded).ToList();
                if (closed.Count == 0) break;
                foreach (var expander in closed) expander.IsExpanded = true;
                host.Layout();
                foreach (var expander in closed) expander.IsExpanded = false;
                host.Layout();
            }
            if (binding is null) return;
            BindingOperations.SetBinding(tabs, Selector.SelectedIndexProperty, binding);
            host.Layout();
        }

        /// <summary>The last calculation requested: the automatic one of the last edit or the started one, whichever came last.</summary>
        private Task Latest()
        {
            if (!ReferenceEquals(Sheet.PendingCalculation, observedPending)) latest = observedPending = Sheet.PendingCalculation;
            return latest;
        }

        private Task Started(Task task) { Latest(); latest = task; return task; }

        /// <summary>The editor of a field of a form (text box, choice or check box of its FieldForm), with its row.</summary>
        private (Control Editor, Grid Row)? Editor(string key)
        {
            var field = Sheet.Forms.Select(f => f.Form).Where(f => f.Contains(key)).Select(f => f[key]).Single();
            var controls = Controls();
            return controls.Editors.TryGetValue(field, out var editor) ? (editor, controls.Rows[field]) : null;
        }

        /// <summary>Usable as InputForm in the historic trace: enabled and its row not hidden.</summary>
        private static bool UsableField((Control Editor, Grid Row) field) => field.Editor.IsEnabled && field.Row.Visibility == Visibility.Visible;

        private static bool Shown(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

        private Button StripButton(string content) => Visual(View.Surveys).OfType<Button>().Single(b => Equals(b.Content, content));

        private DataGrid LayerGrid() => Visual(View.Surveys).OfType<DataGrid>().Single();

        private DataGridRow LayerRow(int index)
        {
            var grid = LayerGrid();
            var item = grid.Items[index];
            grid.ScrollIntoView(item);
            host.Layout();
            return (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(item);
        }

        private void Select(int survey) { View.Surveys.SelectedIndex = survey; host.Layout(); }

        /// <summary>The soil editor of a row of the selected stratigraphy, null when the row has none.</summary>
        private static FrameworkElement? SoilPanel(DataGridRow row) => Visual(row).OfType<FrameworkElement>().FirstOrDefault(e => Equals(e.Tag, "editor-terreno"));

        private static T Part<T>(FrameworkElement soil, string tag) where T : FrameworkElement => Visual(soil).OfType<T>().Single(e => Equals(e.Tag, tag));

        internal async Task Apply(HorizontalPileStep step)
        {
            switch (step.Kind)
            {
                case HorizontalPileStep.Commit: host.Editor.Commit(); break;
                case HorizontalPileStep.Calculate: await Started(host.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(60)); break;
                case HorizontalPileStep.Start: _ = Started(host.Editor.CalculateAsync()); break;
                case HorizontalPileStep.Wait: await Latest().WaitAsync(TimeSpan.FromSeconds(60)); break;
                case HorizontalPileStep.ReadOnly: host.Editor.InspectRevision(); break;
                case HorizontalPileStep.Hold: engine!.Hold(); break;
                case HorizontalPileStep.Entered: await engine!.EnteredAsync(); break;
                case HorizontalPileStep.Release: engine!.Release(); break;
                case HorizontalPileStep.Field:
                    if (Editor(step.Key!) is { Editor: TextBox text } field && UsableField(field) && !text.IsReadOnly) text.Text = step.Text;
                    break;
                case HorizontalPileStep.Choice:
                    if (Editor(step.Key!) is { Editor: ComboBox choice } chosen && UsableField(chosen)) choice.SelectedItem = step.Text;
                    break;
                case HorizontalPileStep.Option:
                    if (Editor(step.Key!) is { Editor: CheckBox option } clicked && UsableField(clicked)) option.IsChecked = option.IsChecked != true;
                    break;
                case HorizontalPileStep.Analysis: View.Analyses.SelectedIndex = step.Index; break;
                case HorizontalPileStep.Survey: Select(step.Index); break;
                case HorizontalPileStep.AddSurvey: Press(StripButton("+ Stratigrafia")); break;
                case HorizontalPileStep.RemoveSurvey:
                {
                    var tab = (TabItem)View.Surveys.ItemContainerGenerator.ContainerFromIndex(step.Index);
                    Press(Visual(tab).OfType<Button>().Single(b => Equals(b.Tag, "elimina-stratigrafia")));
                    break;
                }
                case HorizontalPileStep.RemoveSelectedSurvey: Press(StripButton("− Stratigrafia")); break;
                case HorizontalPileStep.CopySurvey:
                case HorizontalPileStep.CopyFrom:
                    Select(step.Index);
                    services.CopyDestination = step.Text == "nuova" ? -1 : int.Parse(step.Text!, CultureInfo.InvariantCulture);
                    Press(StripButton(step.Kind == HorizontalPileStep.CopySurvey ? "Copia in…" : "Copia da…"));
                    services.CopyDestination = null;
                    break;
                case HorizontalPileStep.Layer:
                {
                    Select(step.Index);
                    var grid = LayerGrid();
                    if (grid.IsReadOnly || !Usable(grid)) break;
                    var row = LayerRow(step.Row);
                    string path = "[" + step.Key + "]";
                    // The cells write the row at every keystroke (UpdateSourceTrigger=PropertyChanged), through the control of the column.
                    if (Visual(row).OfType<ComboBox>().SingleOrDefault(c => BoundPath(c, Selector.SelectedItemProperty) == path) is { } cell) { if (Usable(cell)) cell.SelectedItem = step.Text; }
                    else
                    {
                        var box = Visual(row).OfType<TextBox>().Single(t => BoundPath(t, TextBox.TextProperty) == path);
                        if (Usable(box) && !box.IsReadOnly) box.Text = step.Text;
                    }
                    break;
                }
                case HorizontalPileStep.AddLayer:
                    Select(step.Index);
                    Press(Visual(View.Surveys).OfType<Button>().Single(b => Equals(b.Content, "Aggiungi strato")));
                    break;
                case HorizontalPileStep.RemoveLayer:
                    Select(step.Index);
                    Press(Visual(LayerRow(step.Row)).OfType<Button>().Single(b => Equals(b.Content, "[−]")));
                    break;
                case HorizontalPileStep.Soil:
                {
                    Select(step.Index);
                    if (SoilPanel(LayerRow(step.Row)) is not { } soil) break;
                    switch (step.Key)
                    {
                        case HorizontalPileStep.SoilCategory:
                            if (Part<ComboBox>(soil, "terreno-categoria") is var category && Shown(category)) category.SelectedItem = step.Text;
                            break;
                        case HorizontalPileStep.SoilLaw:
                            if (Part<ComboBox>(soil, "terreno-legge") is var law && Shown(law) && ((IEnumerable)law.ItemsSource).Cast<string>().ToList().IndexOf(step.Text!) is int index and >= 0)
                                law.SelectedIndex = index;
                            break;
                        case HorizontalPileStep.SoilValue:
                            if (Part<TextBox>(soil, "terreno-valore") is var value && Shown(value) && !value.IsReadOnly) value.Text = step.Text;
                            break;
                        case HorizontalPileStep.SoilOverride:
                            if (Part<CheckBox>(soil, "terreno-override") is var box && Shown(box)) box.IsChecked = box.IsChecked != true;
                            break;
                        case HorizontalPileStep.SoilNh:
                            if (Part<TextBox>(soil, "terreno-nh") is var nh && Shown(nh) && !nh.IsReadOnly) nh.Text = step.Text;
                            break;
                        default: throw new ArgumentException("Comando del terreno sconosciuto: " + step.Key);
                    }
                    break;
                }
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            // Layout only: the state is read before the dispatcher runs anything else, as the frozen traces read it.
            host.Layout();
        }

        /// <summary>The state of the tab of the capacity read from the controls of the view.</summary>
        internal HorizontalPileTraceState State()
        {
            host.Layout();
            var view = View;
            var controls = Controls();
            var diameter = (TextBox)controls.Editors[Sheet.GeneralForm["diametro"]];
            var state = new HorizontalPileTraceState
            {
                Data = host.Editor.Data, Modified = host.Modified, Editable = !diameter.IsReadOnly, Exportable = Sheet.HasResults && !Sheet.IsBusy,
                ResultsShown = view.DetailsButton.IsEnabled && view.CsvButton.IsEnabled, Analysis = view.Analyses.SelectedIndex, Result = Sheet.Result,
                Status = view.StatusText.Text, Summary = view.SummaryText.Text, Moment = view.MomentText.Text, MomentToolTip = view.MomentText.ToolTip as string,
                Model = view.ModelText.Text, ModelHint = view.ModelText.ToolTip as string, Warnings = view.WarningsText.Text,
                Forms = Sheet.Forms.Select(f => new HorizontalPileFormState(f.Name, Fields(f.Form, controls), Groups(f.Form, controls))).ToList(),
                Verification = ((IEnumerable<string[]>?)view.VerificationGrid.ItemsSource)?.ToList() ?? [],
                Outcome = view.OutcomeText.Text, OutcomeKind = OutcomeKind(view.OutcomeText),
                SelectedSurvey = view.Surveys.SelectedIndex, ProfileSurvey = view.ProfileDrawing.SelectedIndex,
                Surveys = view.Surveys.Items.Cast<HorizontalPileSurveyViewModel>().Select(s => s.Rows.Select(r => r.Values).ToList()).ToList(),
                ConcreteSection = Sheet.IsMicro ? null : HorizontalPileTrace.ConcreteSection(view.ConcreteDrawing.Outline, view.ConcreteDrawing.Bars.Select(b => (b.X, b.Y, b.Area, b.Diametro)),
                    view.ConcreteDrawing.CircularLinkRadius, view.ConcreteDrawing.LinkDiameter, view.ConcreteDrawing.ShowAxes, view.ConcreteDrawing.Caption),
                Chs = Sheet.IsMicro ? view.ChsDrawing.Properties : null,
            };
            state.Soils = Soils();
            if (Sheet.Result is not null) state.Results = Results();
            return state;
        }

        private FormControls Controls() => FormControlsOf(Page);

        /// <summary>The groups of a form as its FieldForm shows them: the title of the expander, the keys of its rows, open or closed.</summary>
        private static List<HorizontalPileGroupState> Groups(FormViewModel form, FormControls controls) =>
            controls.Groups.Where(e => form.Groups.Contains((FieldGroupViewModel)e.DataContext))
            .Select(e => new HorizontalPileGroupState(((TextBlock)e.Header).Text,
                Visual(e).OfType<Grid>().Where(g => g.Name == "Row").Select(g => ((FieldViewModel)g.DataContext).Key).ToArray(), e.IsExpanded)).ToList();

        private static string OutcomeKind(TextBlock outcome)
        {
            string colour = ((SolidColorBrush)outcome.Foreground).Color.ToString(CultureInfo.InvariantCulture);
            string Token(string key) => ((SolidColorBrush)outcome.FindResource(key)).Color.ToString(CultureInfo.InvariantCulture);
            return colour == Token("Token.Status.Ok") ? "soddisfatta" : colour == Token("Token.Status.Fail") ? "non-soddisfatta" : colour == Token("Token.Muted") ? "attenuato" : "colore " + colour;
        }

        /// <summary>The soil editors of the rows of the selected stratigraphy, as the cells of the table show them; null for a row without one.</summary>
        private List<HorizontalSoilState?> Soils()
        {
            var view = View;
            if (view.Surveys.SelectedIndex < 0) return [];
            var grid = Visual(view.Surveys).OfType<DataGrid>().SingleOrDefault();
            if (grid is null) return [];
            var states = new List<HorizontalSoilState?>();
            foreach (var item in grid.Items)
            {
                // A row without its soil editor (the sheet creates it for the rows of the selected stratigraphy on the tab of the capacity).
                if (grid.ItemContainerGenerator.ContainerFromItem(item) is not DataGridRow row || SoilPanel(row) is not { DataContext: HorizontalSoilRowViewModel } soil) { states.Add(null); continue; }
                var category = Part<ComboBox>(soil, "terreno-categoria"); var law = Part<ComboBox>(soil, "terreno-legge");
                var value = Part<TextBox>(soil, "terreno-valore"); var over = Part<CheckBox>(soil, "terreno-override"); var nh = Part<TextBox>(soil, "terreno-nh");
                bool editable = value.Visibility == Visibility.Visible;
                states.Add(new HorizontalSoilState(((IEnumerable)category.ItemsSource).Cast<string>().ToArray(), category.SelectedItem as string ?? "",
                    ((IEnumerable)law.ItemsSource).Cast<string>().ToArray(), law.SelectedIndex,
                    Part<TextBlock>(soil, "terreno-parametro").Text, Part<TextBlock>(soil, "terreno-minimo").Text, Part<TextBlock>(soil, "terreno-massimo").Text,
                    editable ? value.Text : Part<TextBlock>(soil, "terreno-tabellato").Text, editable, over.Visibility == Visibility.Visible, over.IsChecked == true,
                    nh.Visibility == Visibility.Visible ? nh.Text : null, soil.ToolTip as string ?? ""));
            }
            return states;
        }

        /// <summary>
        /// What «Diagrammi e dettagli» shows for every survey: the dialog of the view built on the sheet (not shown), its tabs «Sondaggio n» with the
        /// tables given to their template (title of the tab, columns, rows) and the line under the title.
        /// </summary>
        private List<HorizontalPileSurveyResultState> Results()
        {
            var dialog = new HorizontalPileResultsView(Sheet);
            var states = new List<HorizontalPileSurveyResultState>();
            foreach (var tab in dialog.Tabs.Items.Cast<TabItem>().Where(t => ((string)t.Header).StartsWith("Sondaggio ", StringComparison.Ordinal)))
            {
                var dock = (DockPanel)tab.Content;
                var info = (HorizontalPileResultSurvey)dock.Children.OfType<ContentControl>().Single().Content;
                var tables = dock.Children.OfType<TabControl>().Single().Items.Cast<TabItem>().Where(t => t.Content is ContentControl { Content: HorizontalPileResultTable })
                    .Select(t => (HorizontalPileResultTable)((ContentControl)t.Content).Content)
                    .Select(t => new HorizontalPileTableState(t.Title, t.Columns, t.Rows)).ToList();
                states.Add(new HorizontalPileSurveyResultState(tables, info.Info));
            }
            return states;
        }
    }

    // ---------------------------------------------------------------- FieldForm readers (capacity and elastic response)

    /// <summary>The editors, the rows and the group expanders of FieldForms.</summary>
    private sealed class FormControls
    {
        internal Dictionary<FieldViewModel, Control> Editors { get; } = [];
        internal Dictionary<FieldViewModel, Grid> Rows { get; } = [];
        internal List<Expander> Groups { get; } = [];
    }

    /// <summary>The editors, the rows and the group expanders of the FieldForms under <paramref name="root"/>, found in one walk.</summary>
    private static FormControls FormControlsOf(DependencyObject root)
    {
        var found = new FormControls();
        foreach (var node in Visual(root))
            switch (node)
            {
                case Grid { Name: "Row", DataContext: FieldViewModel field } row: found.Rows.TryAdd(field, row); break;
                case Control { DataContext: FieldViewModel field } editor when editor is TextBox or ComboBox or CheckBox && editor.TemplatedParent is not ComboBox:
                    found.Editors.TryAdd(field, editor); break;
                case Expander { DataContext: FieldGroupViewModel } group: found.Groups.Add(group); break;
            }
        return found;
    }

    /// <summary>The fields of a form as its FieldForm shows them (the line of the F5.6 and F5.7 traces).</summary>
    private static List<VerticalPileFieldState> Fields(FormViewModel form, FormControls controls) => form.Fields.Select(f =>
    {
        var row = controls.Rows.GetValueOrDefault(f) ?? throw new InvalidOperationException("Riga della vista non disposta: " + f.Key);
        bool enabled = row.IsEnabled, dimmed = row.Opacity < 1, visible = row.Visibility == Visibility.Visible;
        // A check box without the symbol columns is the only element of its row (InputForm: the label of the row is the editor itself).
        if (f.Layout == FieldLayout.EditorOnly) enabled &= controls.Editors.GetValueOrDefault(f)?.IsEnabled ?? f.CanChange;
        // A row hidden since the opening has no editor yet (WPF lays out only what is shown): its value is the one of its field.
        if (controls.Editors.GetValueOrDefault(f) is not { } editor)
            return f.Kind switch
            {
                FieldKind.Text => new VerticalPileFieldState(f.Key, "campo", f.Label, f.Text, null, enabled, dimmed, visible),
                FieldKind.Choice => new VerticalPileFieldState(f.Key, "scelta", f.Label, f.SelectedChoice ?? "", f.Choices, enabled, dimmed, visible),
                _ => new VerticalPileFieldState(f.Key, "opzione", f.Label, VerticalPileTrace.YesNo(f.IsChecked), null, enabled, dimmed, visible),
            };
        string label = AutomationProperties.GetName(editor);
        return editor switch
        {
            TextBox t => new VerticalPileFieldState(f.Key, "campo", label, t.Text, null, enabled, dimmed, visible),
            ComboBox c => new VerticalPileFieldState(f.Key, "scelta", label, c.SelectedItem as string ?? "", ((IEnumerable)c.ItemsSource).Cast<string>().ToArray(), enabled, dimmed, visible),
            _ => new VerticalPileFieldState(f.Key, "opzione", label, VerticalPileTrace.YesNo(((CheckBox)editor).IsChecked == true), null, enabled, dimmed, visible),
        };
    }).ToList();

    // ---------------------------------------------------------------- 1. traces and drawings of the capacity

    private static async Task CapacityTraces(string root, string output)
    {
        string folder = Path.Combine(output, "tracce-capacita");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0, declared = 0;
        foreach (var scenario in HorizontalPileScenarios.All(root))
        {
            var trace = new List<string>();
            var services = new Services();
            var presentation = services.Create();
            var engine = new HeldModuleService(scenario.Module);
            var modules = Modules();
            Hosted Open(JsonObject data) => new(new MvvmSheetEditor(modules.SheetModule(scenario.Module), data, presentation, engine, new CalculationLimiter(CalculationLimiter.DefaultMaximum)));
            var host = Open(HorizontalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture));
            try
            {
                var side = new CapacitySide(host, engine, services);
                side.Attach();
                trace.AddRange(side.State().Lines("apertura"));
                foreach (var step in scenario.Steps)
                {
                    if (step.Kind == HorizontalPileStep.Reopen)
                    {
                        var data = host.Editor.Data;
                        host.Dispose();
                        host = Open(data);
                        side = new CapacitySide(host, engine, services);
                        side.Attach();
                    }
                    else await side.Apply(step);
                    trace.AddRange(side.State().Lines(step.ToString()));
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { trace.Add("errore " + error.GetType().Name + ": " + error.Message); Log.Add("   " + scenario.Name + ": " + error); }
            finally { engine.Release(); host.Dispose(); }
            HorizontalPileTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(HorizontalPileScenarios.Reference(root), scenario.Name + ".txt");
            if (!File.Exists(stored)) { differences.Add(scenario.Name + ": traccia di riferimento assente"); continue; }
            // F5-02 (O6): the historic trace with the declared difference of the new view.
            var (expected, changed) = HorizontalPileTrace.WithDeclaredDifferences(HorizontalPileTrace.Read(stored));
            if (changed > 0 && scenario.Name != HorizontalPileTrace.CustomSteelScenario) differences.Add(scenario.Name + ": F5-02 fuori dallo scenario dell'acciaio personalizzato");
            declared += changed;
            string? difference = HorizontalPileTrace.FirstDifference(expected, trace);
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == HorizontalPileScenarios.All(root).Count && declared > 0,
            $"Tracce della capacità attraverso la vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe, {declared} righe F5-02"
            + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    /// <summary>
    /// The display lists of the diagrams of «Diagrammi e dettagli» (the dialog of the view, opened with its button) and of the profile of the view
    /// for every F3 fixture of the two modules on the capacity with a result, at the sizes of the historic list: equal to disegni/elenco.txt.
    /// </summary>
    private static async Task CapacityDrawings(string root, string output)
    {
        string folder = Path.Combine(output, "disegni-capacita");
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        var list = new List<string>();
        foreach (string module in new[] { Pile, Micro })
            foreach (string id in HorizontalPileScenarios.Fixtures(root, module))
            {
                var data = HorizontalPileScenarios.FixtureData(root, module, id);
                if (HorizontalPileScenarios.OpensElastic(data)) continue;
                using var host = new Hosted(editors.Open(module, data));
                host.Editor.Commit(); await host.Editor.CalculateAsync(); host.Editor.Commit();
                await host.Settle();
                if (host.Sheet.Result is not { } result) continue;
                Press(host.View.DetailsButton);
                await host.Settle();
                var dialog = (HorizontalPileResultsView)host.Dialogs.Single().Content;
                int surveys = result.Array("sondaggi").Count;
                var surveyTabs = dialog.Tabs.Items.Cast<TabItem>().Where(t => ((string)t.Header).StartsWith("Sondaggio ", StringComparison.Ordinal)).ToList();
                Check(surveyTabs.Count == surveys, $"{module}--{id}: «Diagrammi e dettagli» con una scheda per sondaggio ({surveys})");
                for (int i = 0; i < surveys; i++)
                {
                    var diagrams = ((DockPanel)surveyTabs[i].Content).Children.OfType<TabControl>().Single().Items.Cast<TabItem>()
                        .Where(t => t.Content is ScrollViewer { Content: HorizontalDiagrams }).Select(t => (HorizontalDiagrams)((ScrollViewer)t.Content).Content).ToList();
                    foreach (var (soil, drawing) in new[] { (true, diagrams[0]), (false, diagrams[1]) })
                        foreach (var (w, h) in new[] { (1000, 660), (1280, 820) })
                            list.Add(HorizontalPileHistoricTrace.Record(folder, $"{module}--{id} sondaggio {i + 1} {(soil ? "terreno" : "palo")} {w}x{h}", drawing, w, h));
                    // The profile of the view with the stratigraphy chosen through its tab.
                    host.View.Surveys.SelectedIndex = i;
                    await host.Settle();
                    list.Add(HorizontalPileHistoricTrace.Record(folder, $"{module}--{id} profilo {i + 1} 420x640", host.View.ProfileDrawing, 420, 640));
                }
                host.CloseDialogs();
            }
        HorizontalPileTrace.Write(Path.Combine(folder, "elenco.txt"), list);
        string stored = Path.Combine(HorizontalPileHistoricTrace.DrawingsReference(root), "elenco.txt");
        string? difference = HorizontalPileTrace.FirstDifference(HorizontalPileTrace.Read(stored), list);
        Check(difference is null, $"Disegni della scheda della capacità attraverso la vista uguali a quelli congelati della vista storica: {list.Count}" + (difference is null ? "" : " — " + difference));
    }
}
