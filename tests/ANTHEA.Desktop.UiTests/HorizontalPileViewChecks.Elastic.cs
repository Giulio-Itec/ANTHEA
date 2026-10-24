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
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;
using Anthea.Presentation.Tests;
using CommunityToolkit.Mvvm.Input;
using X.Core;
using X.Desktop.Views.HorizontalPile;

namespace X.Desktop;

/// <summary>The tab of the elastic response with the reinforcement of the pile (F5.8): the traces, the drawings and the checks through the view.</summary>
internal static partial class HorizontalPileViewChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo ElasticDrawingResult = typeof(ElasticPileDrawing).GetField("result", Private) ?? throw new MissingFieldException(nameof(ElasticPileDrawing), "result");
    private static readonly FieldInfo ElasticDrawingSelected = typeof(ElasticPileDrawing).GetField("selected", Private) ?? throw new MissingFieldException(nameof(ElasticPileDrawing), "selected");
    private static readonly FieldInfo ElasticDrawingClicked = typeof(ElasticPileDrawing).GetField(nameof(ElasticPileDrawing.DepthClicked), Private)
        ?? throw new MissingFieldException(nameof(ElasticPileDrawing), nameof(ElasticPileDrawing.DepthClicked));
    private static readonly FieldInfo ReinforcementSheetResult = typeof(PileReinforcementDrawing).GetField("result", Private) ?? throw new MissingFieldException(nameof(PileReinforcementDrawing), "result");

    /// <summary>The time a calculation of the checks may take.</summary>
    private static readonly TimeSpan Calculation = TimeSpan.FromSeconds(120);

    /// <summary>The editors of a sheet of the elastic traces: the bench with the two MVVM modules, the elastic response behind <paramref name="engine"/>.</summary>
    private static SheetEditorFactory ElasticEditors(Services services, HeldElasticAnalyses engine) =>
        new(ModuleHostBench.Implementations(BenchArguments), Modules(engine), services.Create(), new CalculationLimiter(CalculationLimiter.DefaultMaximum));

    /// <summary>The view of the elastic response of a hosted sheet (the content of the second tab, also while the first tab is shown).</summary>
    private static ElasticPileView ElasticViewOf(Hosted host) =>
        Visual(host.View.ElasticHost).OfType<ElasticPileView>().SingleOrDefault() ?? throw new InvalidOperationException("Vista della risposta elastica non creata");

    /// <summary>Lays out the tab of the elastic response also while the tab of the capacity is shown (the content of the hidden tab is out of the window).</summary>
    private static void LayoutElastic(Hosted host)
    {
        host.Layout();
        var content = host.View.ElasticHost;
        if (PresentationSource.FromVisual(content) is not null) return;
        double width = content.ActualWidth > 0 ? content.ActualWidth : host.View.Analyses.ActualWidth;
        content.Measure(new Size(width, double.PositiveInfinity));
        content.Arrange(new Rect(0, 0, width, content.DesiredSize.Height));
        content.UpdateLayout();
    }

    /// <summary>Enabled, and it and its parents up to the view not hidden (also outside the window, as the historic Usable).</summary>
    private static bool Shown(FrameworkElement element)
    {
        if (!element.IsEnabled) return false;
        for (DependencyObject? node = element; node is not null and not ElasticPileView; node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node))
            if (node is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    /// <summary>As the historic trace reads a control (ElasticHistoricControls.Usable): enabled and not hidden itself, also inside a closed group.</summary>
    private static bool Free(FrameworkElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    private static ElasticCommandState ElasticCommand(Button button) => new(Convert.ToString(button.Content, CultureInfo.InvariantCulture) ?? "", Free(button));

    /// <summary>The name of the tone of a text of the tab, as the historic trace names its colours: the tone of the ViewModel when the text has one, else its colour.</summary>
    private static string ToneOf(TextBlock text)
    {
        if (text.Tag is ElasticTone tone)
            return tone switch
            {
                ElasticTone.Muted => "attenuato",
                ElasticTone.Failure => "rosso",
                ElasticTone.Pending => "ambra",
                ElasticTone.Satisfied => "verde",
                ElasticTone.Excluded => "grigio",
                _ => "blu"
            };
        Color colour = ((SolidColorBrush)text.Foreground).Color;
        bool Is(string token) => ((SolidColorBrush)text.FindResource(token)).Color == colour;
        return Is("Token.Ink") ? "blu" : Is("Token.Muted") ? "attenuato" : "colore " + colour.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The tab of the elastic response read and driven through the controls of the view, as the historic trace does with ElasticPileWorkspace and
    /// PileReinforcementEditor: a control the user cannot use (disabled, hidden, read-only) does nothing; the dialogs of the view are shown outside
    /// the screen and driven through their buttons; the c.a. verifier is the dialog of the services (E1) shown as WpfDialogService shows it, with the
    /// historic ConcreteWorkspace of its view. The steps on the tab of the capacity go through <see cref="CapacitySide"/>.
    /// </summary>
    private sealed class ElasticSide
    {
        private readonly Hosted host;
        private readonly HeldElasticAnalyses engine;
        private readonly CapacitySide capacity;
        private string? dialog;
        private Action<PileConcreteVerifier>? verifier;

        internal ElasticSide(Hosted host, Services services, HeldElasticAnalyses engine)
        {
            this.host = host; this.engine = engine;
            capacity = new CapacitySide(host, null, services);
            capacity.Attach();
            services.Dialog = shown =>
            {
                if (shown is not PileConcreteVerifier concrete || verifier is not { } next) throw new InvalidOperationException("Finestra non prevista: " + shown);
                next(concrete);
                return Task.FromResult<object?>(concrete.Result);
            };
            LayoutElastic(host);
            View.ShowWindow = host.ShowOffScreen;
            View.Reinforcement.ShowWindow = host.ShowOffScreen;
        }

        private ElasticPileView View => ElasticViewOf(host);

        private PileReinforcementView Reinforcement => View.Reinforcement;

        private ElasticPileViewModel Elastic => host.Sheet.Elastic;

        private void Layout() => LayoutElastic(host);

        /// <summary>The card of the segment <paramref name="index"/> (null when the editor has no such card).</summary>
        private StackPanel? Card(int index)
        {
            if (index < 0 || index >= Reinforcement.Segments.Items.Count || Reinforcement.Visibility != Visibility.Visible) return null;
            var container = (DependencyObject)Reinforcement.Segments.ItemContainerGenerator.ContainerFromIndex(index);
            return container is null ? null : (StackPanel)Visual(container).OfType<Border>().First(b => Equals(b.Tag, "tratto")).Child;
        }

        private static T Tagged<T>(DependencyObject card, string tag) where T : FrameworkElement => Visual(card).OfType<T>().First(e => Equals(e.Tag, tag));

        private static Button? CardButton(StackPanel? card, string label) =>
            card is null ? null : Tagged<WrapPanel>(card, "tratto-comandi").Children.OfType<Button>().FirstOrDefault(b => Equals(b.Content, label) && b.Visibility == Visibility.Visible);

        private static Button Named(DependencyObject root, string label) => ElasticHistoricControls.Logical<Button>(root).First(b => Equals(b.Content, label));

        /// <summary>A field of a form of the tab typed, chosen or clicked through its FieldForm.</summary>
        private void Edit(FrameworkElement scope, FormViewModel form, string key, string? text)
        {
            var field = form[key];
            var controls = FormControlsOf(scope);
            if (!controls.Editors.TryGetValue(field, out var editor) || !controls.Rows.TryGetValue(field, out var row)) throw new InvalidOperationException("Campo della vista non disposto: " + key);
            if (!editor.IsEnabled || row.Visibility != Visibility.Visible || !Shown(scope)) return;
            switch (editor)
            {
                case TextBox box when text is not null: if (!box.IsReadOnly) box.Text = text; break;
                case ComboBox choice when text is not null: choice.SelectedItem = text; break;
                case CheckBox option when text is null: option.IsChecked = option.IsChecked != true; break;
                default: throw new ArgumentException("Passo non adatto al campo " + key);
            }
        }

        private static void Toggle(CheckBox box) { if (Shown(box)) box.IsChecked = box.IsChecked != true; }

        /// <summary>The group of the reinforcement that holds the field <paramref name="key"/>, with its form.</summary>
        private (Expander Group, FormViewModel Form) Group(string key)
        {
            var r = Elastic.Reinforcement;
            return r.Details!.Contains(key) ? (Reinforcement.DetailsGroup, r.Details) : r.Seismic!.Contains(key) ? (Reinforcement.SeismicGroup, r.Seismic) : (Reinforcement.ExtraGroup, r.Extra!);
        }

        /// <summary>The user opens a closed group first (the view protects the controls it shows).</summary>
        private void Open(Expander group) { if (!group.IsExpanded) { group.IsExpanded = true; Layout(); } }

        /// <summary>The window of the view shown by the last command, with its view; null when the command showed nothing.</summary>
        private T? Dialog<T>() where T : DependencyObject => host.Dialogs.Count == 0 ? null : Visual(host.Dialogs[^1]).OfType<T>().SingleOrDefault();

        internal async Task Apply(ElasticPileStep step)
        {
            dialog = null;
            Layout();
            var card = Card(step.Index);
            switch (step.Kind)
            {
                case ElasticPileStep.Commit: host.Editor.Commit(); break;
                case ElasticPileStep.Calculate: await host.Editor.CalculateAsync().WaitAsync(Calculation); break;
                case ElasticPileStep.Start: _ = host.Editor.CalculateAsync(); break;
                case ElasticPileStep.Wait:
                    await Elastic.PendingCalculation.WaitAsync(Calculation);
                    // On the tab of the elastic response the window sees the elastic result and «esportabile» depends on the calculation of the
                    // capacity too (IsBusy): it starts 450 ms after an edit, the response 300 ms after, so the outcome is a race. In the frozen traces it
                    // had always ended (on this tab every «attendi» with an elastic result reads «esportabile sì»): the check lets it end. Nothing of
                    // the capacity is in the lines of the elastic trace.
                    if (host.View.Analyses.SelectedIndex == 1) await host.Sheet.PendingCalculation.WaitAsync(Calculation);
                    break;
                case ElasticPileStep.ReadOnly: host.Editor.InspectRevision(); break;
                case ElasticPileStep.Hold: engine.Hold(); break;
                case ElasticPileStep.Entered: await engine.EnteredAsync(); break;
                case ElasticPileStep.Release: engine.Release(); break;
                case ElasticPileStep.Analysis: host.View.Analyses.SelectedIndex = step.Index; break;
                case ElasticPileStep.CapacityStep:
                    // A stratigraphy added on the tab of the capacity while the elastic response is shown: the button of the hidden tab, as the
                    // historic trace clicks it (enabled and not hidden itself).
                    if (step.Capacity!.Kind == HorizontalPileStep.AddSurvey)
                    {
                        var add = Visual(host.View.Surveys).OfType<Button>().Single(b => Equals(b.Content, "+ Stratigrafia"));
                        if (Free(add) && add.Command is { } command && command.CanExecute(add.CommandParameter)) command.Execute(add.CommandParameter);
                    }
                    else await capacity.Apply(step.Capacity);
                    break;
                case ElasticPileStep.Survey: if (Shown(View.SurveyChoice)) View.SurveyChoice.SelectedIndex = step.Index; break;
                case ElasticPileStep.Option: if (Elastic.Options is { } options) Edit(View.Options, options, step.Key!, step.Text); break;
                case ElasticPileStep.Toggle:
                    if (Visual(View.Toggles).OfType<CheckBox>().FirstOrDefault(b => Equals(b.Tag, step.Key)) is { } box) Toggle(box);
                    break;
                case ElasticPileStep.EditInputs: if (View.Header.Visibility == Visibility.Visible) Press(Named(View.Header, "Modifica dati e strati")); break;
                case ElasticPileStep.Migration:
                    if (View.MigrationPanel.Visibility == Visibility.Visible)
                        Press(Named(View.MigrationPanel, step.Text == "legacy" ? "Importa legacy nei dati comuni" : "Usa dati comuni e conserva legacy"));
                    break;
                case ElasticPileStep.Depth:
                    // A click on the drawing at that depth: what OnMouseDown gives to the view (DepthClicked), with a response drawn.
                    if (ElasticDrawingResult.GetValue(View.Drawing) is not null)
                        ((Action<double>?)ElasticDrawingClicked.GetValue(View.Drawing))?.Invoke(double.Parse(step.Text!, CultureInfo.InvariantCulture));
                    break;
                case ElasticPileStep.SegmentEnd:
                    if (card is not null && Tagged<TextBox>(card, "tratto-fine") is var end && Shown(end) && !end.IsReadOnly) end.Text = step.Text;
                    break;
                case ElasticPileStep.SegmentLinked: if (card is not null) Toggle(Tagged<CheckBox>(card, "tratto-principale")); break;
                case ElasticPileStep.SegmentField:
                    if (card is not null && Visual(card).OfType<TextBox>().FirstOrDefault(b => Equals(b.Tag, "tratto-campo") && b.DataContext is ElasticSegmentFieldViewModel f && f.Key == step.Key) is { } field
                        && Shown(field) && !field.IsReadOnly) field.Text = step.Text;
                    break;
                case ElasticPileStep.SegmentRing: if (card is not null) Toggle(Tagged<CheckBox>(card, "tratto-anello")); break;
                case ElasticPileStep.SegmentKind:
                    if (card is not null && Tagged<ComboBox>(card, "tratto-tipo") is var kind && Shown(kind)) kind.SelectedItem = step.Text;
                    break;
                case ElasticPileStep.MainTransverse: if (Reinforcement.Visibility == Visibility.Visible && Shown(Reinforcement.MainTransverse)) Reinforcement.MainTransverse.SelectedItem = step.Text; break;
                case ElasticPileStep.Detail:
                    if (Reinforcement.Visibility != Visibility.Visible) break;
                    var (group, form) = Group(step.Key!);
                    Open(group);
                    Edit(group, form, step.Key!, step.Text);
                    break;
                case ElasticPileStep.Minimum: if (Reinforcement.Visibility == Visibility.Visible) Toggle(Reinforcement.Minimum); break;
                case ElasticPileStep.Approve:
                    if (Reinforcement.Visibility != Visibility.Visible) break;
                    if (step.Text == "sisma") { Open(Reinforcement.SeismicGroup); Press(Named(Reinforcement.SeismicGroup, "Approva tutto · sisma")); }
                    else Press(Named(Reinforcement.Editor, "Approva tutto · ipotesi"));
                    break;
                case ElasticPileStep.Merge: if (CardButton(card, "Unisci ←") is { } merge) Press(merge); break;
                case ElasticPileStep.Verify: if (CardButton(card, "Verifiche →") is { } verify) Press(verify); break;
                case ElasticPileStep.Bill: if (CardButton(card, "Distinta ferri") is { } bill) Press(bill); break;
                case ElasticPileStep.SummarySegment: if (Shown(Reinforcement.SummarySegment)) Reinforcement.SummarySegment.SelectedIndex = step.Index; break;
                case ElasticPileStep.SummaryMode: if (Shown(Reinforcement.SummaryMode)) Reinforcement.SummaryMode.SelectedItem = step.Text; break;
                case ElasticPileStep.Split:
                {
                    if (CardButton(card, "Dividi…") is not { } split || !Shown(split)) break;
                    host.CloseDialogs();
                    Press(split);
                    if (Dialog<ElasticSplitView>() is not { } view) break;
                    view.Depth.Text = step.Text;
                    Press(view.SplitButton);
                    dialog = view.Depth.ToolTip as string ?? "";
                    host.CloseDialogs();
                    break;
                }
                case ElasticPileStep.Propose:
                {
                    if (Reinforcement.Visibility != Visibility.Visible || Named(Reinforcement.Editor, "Proponi suddivisione…") is not { } propose || !Shown(propose)) break;
                    host.CloseDialogs();
                    Press(propose);
                    if (Dialog<ElasticProposalView>() is not { } view) break;
                    dialog = view.ProposalText.Text;
                    Press(view.ApplyButton);
                    host.CloseDialogs();
                    break;
                }
                case ElasticPileStep.Concrete:
                {
                    if (CardButton(card, "Apri verificatore c.a.") is not { } open || !Shown(open)) break;
                    string? note = null;
                    verifier = concrete =>
                    {
                        // As WpfDialogService: a window on the ViewModel of the dialog, its view from the DataTemplate of the desktop, shown outside the
                        // screen and laid out without running the dispatcher (as the historic trace shows its dialog: no calculation starts meanwhile).
                        var window = new Window
                        {
                            Title = concrete.Title, Content = concrete, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false, ShowActivated = false,
                            WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000
                        };
                        Appearance.Watch(window);
                        window.Show();
                        window.UpdateLayout();
                        var view = Visual(window).OfType<PileConcreteVerifierView>().Single();
                        if (step.Text == "applica") Press(view.ApplyButton);
                        note = view.NoteText.Text;
                        window.Close();
                    };
                    Press(open);
                    if (open.Command is IAsyncRelayCommand command && command.ExecutionTask is { } running) await running.WaitAsync(Calculation);
                    verifier = null;
                    dialog = note;
                    break;
                }
                case ElasticPileStep.Design:
                {
                    if (CardButton(card, "Dimensiona…") is not { } design || !Shown(design)) break;
                    host.CloseDialogs();
                    Press(design);
                    if (Dialog<PileSegmentDesignView>() is not { } view) break;
                    string[] catalogues = step.Text!.Split('|');
                    view.Counts.Text = catalogues[0]; view.Diameters.Text = catalogues[1]; view.Links.Text = catalogues[2]; view.Spacings.Text = catalogues[3];
                    Press(view.FindButton);
                    if (view.FindButton.Command is IAsyncRelayCommand find && find.ExecutionTask is { } search) await search.WaitAsync(Calculation);
                    if (view.ApplyButton.IsEnabled) Press(view.ApplyButton);
                    dialog = view.StatusText.Text;
                    host.CloseDialogs();
                    break;
                }
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            // Layout only: the state is read before the dispatcher runs anything else, as the frozen traces read it.
            Layout();
        }

        /// <summary>
        /// Lays out the closed groups of the reinforcement whose form is not laid out yet (opened and closed again, as the capacity side does with its
        /// expanders): the historic InputForm builds every editor, the FieldForm only those shown.
        /// </summary>
        private void RealizeGroups()
        {
            var r = Elastic.Reinforcement;
            if (Reinforcement.Visibility != Visibility.Visible || !r.IsBuilt) return;
            foreach (var (group, form) in new[] { (Reinforcement.DetailsGroup, r.Details), (Reinforcement.SeismicGroup, r.Seismic), (Reinforcement.ExtraGroup, r.Extra) })
            {
                if (form is null || group.IsExpanded) continue;
                var rows = FormControlsOf(group).Rows;
                if (form.Fields.All(rows.ContainsKey)) continue;
                group.IsExpanded = true; Layout();
                group.IsExpanded = false; Layout();
            }
        }

        /// <summary>
        /// The state of the tab of the elastic response read from the controls of the view. While the tab of the capacity is shown, the tab of the
        /// elastic response is shown for the reading only (the choice of the tab is not written: the binding of the tabs is removed around it),
        /// as the historic trace reads the controls of its hidden tab.
        /// </summary>
        internal ElasticPileTraceState State()
        {
            var tabs = host.View.Analyses;
            int analysis = tabs.SelectedIndex;
            if (analysis == 1) return Read(analysis);
            var binding = BindingOperations.GetBindingBase(tabs, Selector.SelectedIndexProperty);
            BindingOperations.ClearBinding(tabs, Selector.SelectedIndexProperty);
            tabs.SelectedIndex = 1;
            try { return Read(analysis); }
            finally
            {
                tabs.SelectedIndex = analysis;
                if (binding is not null) BindingOperations.SetBinding(tabs, Selector.SelectedIndexProperty, binding);
                host.Layout();
            }
        }

        private ElasticPileTraceState Read(int analysis)
        {
            Layout();
            RealizeGroups();
            var v = View;
            var general = FormControlsOf(host.View.CapacityPage).Editors[host.Sheet.GeneralForm["diametro"]];
            var state = new ElasticPileTraceState
            {
                Data = host.Editor.Data, Modified = host.Modified, Editable = !((TextBox)general).IsReadOnly, Analysis = analysis,
                Exportable = host.Editor.Result is not null && !host.Editor.Busy, Response = Elastic.Response, Result = Elastic.Result,
                Status = v.StatusText.Text, Progress = ElasticHistoricControls.Progress(v.Progress), Stiffness = v.StiffnessText.Text,
                Drawing = (JsonObject?)ElasticDrawingResult.GetValue(v.Drawing), Selected = (JsonNode?)ElasticDrawingSelected.GetValue(v.Drawing),
                Selection = v.SelectionText.Text, SectionPreview = ElasticHistoricControls.Section(v.SectionDrawing), SectionDetail = v.SectionDetailText.Text,
                Summary = v.ExtremesText.Text,
                Table = v.PointsTable.ItemsSource is IEnumerable rows ? rows.Cast<object>().Select(r => ElasticPileTrace.Row(r.GetType().GetProperties().Select(p => p.GetValue(r)))).ToList() : [],
                Csv = v.CsvButton.IsEnabled, ReinforcementSummary = v.ReinforcementSummaryText.Text, SheetMessage = v.SheetDrawing.EmptyMessage,
                SheetPieces = v.SheetDrawing.Pieces.Count, Sheet = (JsonObject?)ReinforcementSheetResult.GetValue(v.SheetDrawing),
                Reinforcement = Reinforcement.Visibility == Visibility.Visible ? ReinforcementState() : null, Dialog = dialog
            };
            dialog = null;
            if (v.MigrationPanel.Visibility == Visibility.Visible)
            {
                state.Migration = v.MigrationText.Text;
                state.MigrationCommands = ElasticHistoricControls.Logical<Button>(v.MigrationPanel).Select(ElasticCommand).ToList();
            }
            if (v.Header.Visibility == Visibility.Visible)
            {
                state.Header = true;
                state.SurveyChoices = ((IEnumerable)v.SurveyChoice.ItemsSource).Cast<string>().ToArray(); state.SurveyChoice = v.SurveyChoice.SelectedIndex;
                state.SurveyEnabled = Free(v.SurveyChoice);
                state.HeaderCommands = ElasticHistoricControls.Logical<Button>(v.Header).Select(ElasticCommand).ToList();
                state.Options = Elastic.Options is { } options ? Fields(options, FormControlsOf(v.Options)) : [];
                state.Toggles = Visual(v.Toggles).OfType<CheckBox>().Select(b => new ElasticToggleState((string)b.Tag, (string)b.Content, b.IsChecked == true, Free(b))).ToList();
            }
            return state;
        }

        private ElasticReinforcementState ReinforcementState()
        {
            var r = Reinforcement;
            var model = Elastic.Reinforcement;
            var state = new ElasticReinforcementState
            {
                Phase = r.PhaseText.Text, Progress = ElasticHistoricControls.Progress(r.Progress), Message = r.MessageText.Text,
                Commands = r.Editor.Children.OfType<WrapPanel>().SelectMany(w => w.Children.OfType<Button>()).Select(ElasticCommand).ToList(),
                MainTransverse = r.MainTransverse.SelectedItem as string ?? "", MainTransverseChoices = ((IEnumerable)r.MainTransverse.ItemsSource).Cast<string>().ToArray(),
                MainTransverseEnabled = Free(r.MainTransverse),
                DetailsExpanded = r.DetailsGroup.IsExpanded, Details = Fields(model.Details!, FormControlsOf(r.DetailsGroup)),
                SeismicExpanded = r.SeismicGroup.IsExpanded, Seismic = Fields(model.Seismic!, FormControlsOf(r.SeismicGroup)),
                SeismicCommands = ElasticHistoricControls.Logical<Button>(r.SeismicGroup).Select(ElasticCommand).ToList(),
                ExtraExpanded = r.ExtraGroup.IsExpanded, Extra = Fields(model.Extra!, FormControlsOf(r.ExtraGroup)),
                Minimum = r.Minimum.IsChecked == true, MinimumEnabled = Free(r.Minimum)
            };
            for (int i = 0; i < r.Segments.Items.Count; i++)
            {
                var card = Card(i) ?? throw new InvalidOperationException("Scheda del tratto non disposta: " + i);
                var end = Tagged<TextBox>(card, "tratto-fine"); var linked = Tagged<CheckBox>(card, "tratto-principale");
                var ring = Tagged<CheckBox>(card, "tratto-anello"); var kind = Tagged<ComboBox>(card, "tratto-tipo");
                var bill = Visual(card).OfType<Expander>().Single();
                state.Segments.Add(new ElasticSegmentState
                {
                    Header = ((TextBlock)card.Children[0]).Text, End = end.Text, EndEditable = !end.IsReadOnly && Free(end), EndToolTip = end.ToolTip as string,
                    Linked = linked.IsChecked == true, LinkedEnabled = Free(linked), Ring = ring.IsChecked == true, RingEnabled = Free(ring), RingToolTip = ring.ToolTip as string,
                    Kind = kind.SelectedItem as string ?? "", Kinds = ((IEnumerable)kind.ItemsSource).Cast<string>().ToArray(), KindEnabled = Free(kind),
                    Fields = Visual(card).OfType<TextBox>().Where(b => Equals(b.Tag, "tratto-campo")).Select(b => new ElasticSegmentFieldState(
                        ((ElasticSegmentFieldViewModel)b.DataContext).Key, ((TextBlock)((Panel)b.Parent).Children[0]).Text, b.Text, b.IsReadOnly || !Free(b), b.ToolTip as string)).ToList(),
                    Commands = Tagged<WrapPanel>(card, "tratto-comandi").Children.OfType<Button>().Where(b => b.Visibility == Visibility.Visible).Select(ElasticCommand).ToList(),
                    Bill = ((TextBlock)bill.Content).Text, BillExpanded = bill.IsExpanded
                });
            }
            state.Summary = SummaryLines();
            return state;
        }

        /// <summary>The panel «Sezione e verifiche», one line per element shown, in its order.</summary>
        private List<string> SummaryLines()
        {
            var r = Reinforcement;
            var panel = (StackPanel)r.SummaryPanel.Child;
            var title = (TextBlock)panel.Children[0];
            var lines = new List<string> { ElasticPileTrace.SummaryText(ToneOf(title), title.Text) };
            if (r.SummaryPanel.Visibility != Visibility.Visible) return lines;
            static bool Visible(UIElement e) => e.Visibility == Visibility.Visible;
            static IEnumerable<string> Items(ItemsControl c) => ((IEnumerable)c.ItemsSource).Cast<string>();
            foreach (var element in panel.Children.OfType<FrameworkElement>().Skip(1).Where(Visible))
                switch (element)
                {
                    case Button button: lines.Add(ElasticPileTrace.SummaryCommand((string)button.Content, Free(button))); break;
                    case ComboBox choice: lines.Add(ElasticPileTrace.SummaryChoice(Items(choice), choice.SelectedItem as string, Free(choice))); break;
                    case TextBlock text: lines.Add(ElasticPileTrace.SummaryText(ToneOf(text), text.Text)); break;
                    case ContentControl header when header.Name == "SummaryHeader":
                        var shown = Visual(header).OfType<TextBlock>().Single();
                        lines.Add(ElasticPileTrace.SummaryText(ToneOf(shown), shown.Text));
                        break;
                    case StackPanel checks when checks.Name == "SegmentChecks":
                        foreach (var part in checks.Children.OfType<FrameworkElement>().Where(Visible))
                            switch (part)
                            {
                                case ContentControl section when section.Name == "SummarySectionHost":
                                    lines.Add(ElasticPileTrace.SummarySection(ElasticHistoricControls.Section(r.SummarySection))); break;
                                case Border verdict when verdict.Name == "Verdict":
                                    var state = (TextBlock)verdict.Child;
                                    lines.Add(ElasticPileTrace.SummaryVerdict(ToneOf(state), state.Text)); break;
                                case ItemsControl groups when groups.Name == "VerdictGroups":
                                    foreach (var group in Visual(groups).OfType<Expander>())
                                    {
                                        var text = (TextBlock)group.Content;
                                        lines.Add(ElasticPileTrace.SummaryGroup((string)group.Header, group.IsExpanded, ToneOf(text), text.Text));
                                    }
                                    break;
                                case ItemsControl texts when texts.Name == "Lines":
                                    foreach (var text in Visual(texts).OfType<TextBlock>()) lines.Add(ElasticPileTrace.SummaryText(ToneOf(text), text.Text));
                                    break;
                                case TextBlock text: lines.Add(ElasticPileTrace.SummaryText(ToneOf(text), text.Text)); break;
                                default: lines.Add("elemento " + part.GetType().Name); break;
                            }
                        break;
                    default: lines.Add("elemento " + element.GetType().Name); break;
                }
            return lines;
        }
    }

    // ---------------------------------------------------------------- 10. traces of the elastic response

    /// <summary>
    /// The 20 scenarios of tests/Shared/ElasticPileTrace.cs driven through the controls of the view (and of its dialogs) with the held engine of the
    /// traces, read back from the controls: equal line by line to the traces of the historic view frozen in tests/fixtures/f5-elastico/storica.
    /// </summary>
    private static async Task ElasticTraces(string root, string output)
    {
        string folder = Path.Combine(output, "tracce-elastiche");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in ElasticPileScenarios.All(root))
        {
            var trace = new List<string>();
            var ids = new ElasticPileIds();
            var services = new Services();
            var engine = new HeldElasticAnalyses();
            var editors = ElasticEditors(services, engine);
            var host = new Hosted(editors.Open(scenario.Module, HorizontalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture)));
            try
            {
                var side = new ElasticSide(host, services, engine);
                trace.AddRange(side.State().Lines("apertura", ids));
                foreach (var step in scenario.Steps)
                {
                    if (step.Kind == ElasticPileStep.Reopen)
                    {
                        var data = host.Editor.Data;
                        host.Dispose();
                        host = new Hosted(editors.Open(scenario.Module, data));
                        side = new ElasticSide(host, services, engine);
                    }
                    else await side.Apply(step);
                    trace.AddRange(side.State().Lines(step.ToString(), ids));
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or TimeoutException)
            {
                trace.Add("errore " + error.GetType().Name + ": " + error.Message); Log.Add("   " + scenario.Name + ": " + error);
            }
            finally { engine.Release(); host.Dispose(); await engine.DrainAsync(); }
            ElasticPileTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(ElasticPileScenarios.Reference(root), scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? ElasticPileTrace.FirstDifference(ElasticPileTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == 20, $"Tracce della risposta elastica attraverso la vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe"
            + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    /// <summary>
    /// The drawings that the view shows, for every F3 fixture of the two modules on the elastic response with a response after «Calcola»: the drawing of
    /// the response with the options of the fixture, the same with a depth chosen by a click at a third of the length, and the sheet of the
    /// reinforcement, at the sizes of the historic list: equal to the lines with the same name of disegni/elenco.txt.
    /// </summary>
    private static async Task ElasticDrawings(string root, string output)
    {
        string folder = Path.Combine(output, "disegni-elastici");
        Directory.CreateDirectory(folder);
        var stored = ElasticPileTrace.Read(Path.Combine(ElasticPileScenarios.DrawingsReference(root), "elenco.txt"))
            .ToDictionary(l => l[..l.IndexOf(" · primitivi ", StringComparison.Ordinal)], StringComparer.Ordinal);
        var list = new List<string>();
        var differences = new List<string>();
        foreach (string module in new[] { Pile, Micro })
            foreach (string id in ElasticPileScenarios.Fixtures(root, module))
            {
                var services = new Services();
                using var host = new Hosted(Editors(services).Open(module, HorizontalPileScenarios.FixtureData(root, module, id)));
                await host.Sheet.Elastic.PendingCalculation.WaitAsync(Calculation);
                host.Editor.Commit(); await host.Editor.CalculateAsync().WaitAsync(Calculation); host.Editor.Commit();
                await host.Settle();
                var elastic = host.Sheet.Elastic;
                if (elastic.Response is not { } response) continue;
                var view = ElasticViewOf(host);
                string name = module + "--" + id;
                void Record(string label, FrameworkElement drawing, int w, int h)
                {
                    string line = HorizontalPileHistoricTrace.Record(folder, name + " " + label, drawing, w, h);
                    list.Add(line);
                    string key = line[..line.IndexOf(" · primitivi ", StringComparison.Ordinal)];
                    if (!stored.TryGetValue(key, out var expected)) differences.Add(key + ": assente nell'elenco congelato");
                    else if (expected != line) differences.Add(key + ": " + line + " invece di " + expected);
                }
                Record("disegno opzioni 1750x650", view.Drawing, 1750, 650);
                ((Action<double>?)ElasticDrawingClicked.GetValue(view.Drawing))?.Invoke(response["input"].D("lunghezza") / 3);
                await host.Settle();
                Record("disegno quota 1750x650", view.Drawing, 1750, 650);
                Record("tavola", view.SheetDrawing, (int)Math.Ceiling(view.SheetDrawing.SheetSize.Width), (int)Math.Ceiling(view.SheetDrawing.SheetSize.Height));
            }
        ElasticPileTrace.Write(Path.Combine(folder, "elenco.txt"), list);
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        // Three drawings of the view for every sheet of the frozen list (the historic list has a sheet per fixture with a response).
        int expected = stored.Keys.Count(k => k.EndsWith(" tavola", StringComparison.Ordinal)) * 3;
        Check(differences.Count == 0 && list.Count == expected, $"Disegni della scheda elastica attraverso la vista uguali a quelli congelati della vista storica: {list.Count}"
            + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    // ---------------------------------------------------------------- 11. checks of the elastic response

    /// <summary>Opens the three groups of the reinforcement of the view.</summary>
    private static void OpenGroups(DependencyObject root)
    {
        foreach (var group in Visual(root).OfType<Expander>().Where(e => e.Header is string header
            && (header == ElasticPileFields.DetailsHeader || header == ElasticPileFields.SeismicHeader || header == ElasticPileFields.ExtraHeader)).ToArray())
            group.IsExpanded = true;
    }

    /// <summary>
    /// Help, automation names, read-only, the c.a. verifier (E1) and the exports of the tab of the elastic response (f5-pass-orizzontale: the historic tab is
    /// archived, the comparisons with it are done).
    /// </summary>
    private static async Task ElasticChecks(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni-elastiche");
        Directory.CreateDirectory(folder);
        foreach (var (module, id) in new[] { (Pile, "elastico-tratti-anello-spirale"), (Micro, "elastico-chs-stratificato") })
        {
            var data = HorizontalPileScenarios.FixtureData(root, module, id);
            string guide = Anthea.Wiki.WikiCatalog.ForModule(module)?.Id ?? throw new InvalidOperationException("Guida assente nella Wiki: " + module);

            // Help: the «?» shown are those of the historic tab; every «?» and every F1 on a field of the four forms reaches the navigator.
            {
                var services = new Services();
                using var host = new Hosted(Editors(services).Open(module, data));
                await host.Sheet.Elastic.PendingCalculation.WaitAsync(Calculation);
                await host.Settle();
                var view = ElasticViewOf(host);
                // The groups opened by the user once the editors are built after the result.
                OpenGroups(view);
                await host.Settle();
                static string[] Labels(DependencyObject scope) => Visual(scope).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?")
                    .Select(b => AutomationProperties.GetName(b)).Order(StringComparer.Ordinal).ToArray();
                string[] after = Labels(view);
                Check(after.Length > 0 && after.All(l => l.Length > 0), module + ": etichette «?» della scheda elastica (" + after.Length + "): " + string.Join(", ", after));
                foreach (bool readOnly in new[] { false, true })
                {
                    if (readOnly) { host.Editor.InspectRevision(); await host.Settle(); OpenGroups(view); await host.Settle(); }
                    string label = module + (readOnly ? " (sola lettura)" : "");
                    services.Wiki.Clear();
                    var helps = Visual(view).OfType<Button>().Where(b => b.IsVisible && b.Content as string == "?").ToArray();
                    foreach (var help in helps) help.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, help));
                    Check(helps.Length > 0 && services.Wiki.Count == helps.Length && services.Wiki.All(u => u is not null),
                        label + $": ogni «?» della scheda elastica ({helps.Length}) arriva al navigatore della Wiki");
                    services.Wiki.Clear();
                    var fields = Visual(view).OfType<Control>().Where(e => e is TextBox or ComboBox or CheckBox && e.DataContext is FieldViewModel && e.TemplatedParent is not ComboBox && e.IsVisible).ToArray();
                    var expected = fields.Select(e => ((FieldViewModel)e.DataContext).HelpUri ?? guide).ToList();
                    foreach (var field in fields) F1(field);
                    var forms = fields.Select(e => ((FieldViewModel)e.DataContext)).Select(f => new[] { host.Sheet.Elastic.Options, host.Sheet.Elastic.Reinforcement.Details,
                        host.Sheet.Elastic.Reinforcement.Seismic, host.Sheet.Elastic.Reinforcement.Extra }.Count(form => form is not null && form.Fields.Contains(f))).ToArray();
                    Check(fields.Length > 10 && forms.All(n => n == 1) && services.Wiki.SequenceEqual(expected),
                        label + $": F1 su ogni campo dei quattro form della scheda elastica ({fields.Length}) arriva al navigatore della Wiki");
                    services.Wiki.Clear();
                    F1(view.Drawing);
                    Check(services.Wiki.SequenceEqual([guide]), label + ": F1 fuori dai campi della scheda elastica apre la guida del modulo: " + string.Join(", ", services.Wiki));
                }
            }

            // Automation names: those of the historic tab kept; the controls that ElasticPile.UiChecks finds by Tag, content or header.
            {
                using var host = new Hosted(Editors(new Services()).Open(module, data));
                await host.Sheet.Elastic.PendingCalculation.WaitAsync(Calculation);
                await host.Settle();
                var view = ElasticViewOf(host);
                static HashSet<string> Names(DependencyObject scope) => Visual(scope).OfType<UIElement>().Where(e => e.IsVisible)
                    .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
                var after = Names(view);
                Log.Add($"   nomi di automazione della scheda elastica ({module}, {after.Count}): " + string.Join(", ", after.Order(StringComparer.Ordinal)));
                int segments = host.Sheet.Elastic.Reinforcement.Segments.Count;
                string[] keys = ["strati", "falda", "kh", "mesh", "molle", "carichi", "k", "y", "theta", "q", "N", "V", "M", "tratti"];
                var boxes = Visual(view).OfType<CheckBox>().ToArray();
                Check(keys.All(k => boxes.Count(b => b.Tag?.ToString() == k) == 1), module + ": le caselle del disegno con il Tag della chiave lette da ElasticPile.UiChecks");
                Check(Visual(view).OfType<Button>().Count(b => b.Content?.ToString() == "Apri verificatore c.a.") == segments + 1
                    && Visual(view).OfType<ComboBox>().Count(c => c.Items.Contains("Interferro")) == 1
                    && Visual(view).OfType<Expander>().Count(e => e.Header?.ToString()?.StartsWith("Distinta ferri", StringComparison.Ordinal) == true && e.Content is TextBlock) == segments
                    && boxes.Count(c => c.Content?.ToString()?.Contains("anello interno") == true) == segments
                    && Visual(view).OfType<Button>().Count(b => b.Content?.ToString() == "Distinta ferri") == segments,
                    module + $": verificatori ({segments} tratti + riepilogo), scelta «Interferro», distinte, anelli e «Distinta ferri» come li cerca ElasticPile.UiChecks");
            }

            // Read-only: the controls of the tab that accept the user after InspectRevision, against the historic tab; the edits are refused.
            {
                var services = new Services();
                var engine = new HeldElasticAnalyses();
                using var host = new Hosted(ElasticEditors(services, engine).Open(module, data));
                await host.Sheet.Elastic.PendingCalculation.WaitAsync(Calculation);
                host.Editor.InspectRevision();
                var view = ElasticViewOf(host);
                OpenGroups(view);
                await host.Settle();
                var after = Controls(view);
                Log.Add($"   sola lettura della scheda elastica ({module}): " + string.Join(", ", after.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + " " + p.Value)));
                string before0 = host.Editor.Data.ToJsonString(J.Options);
                var side = new ElasticSide(host, services, engine);
                foreach (var step in new ElasticPileStep[] { new(ElasticPileStep.Option, "passo", "1"), new(ElasticPileStep.Toggle, "y"), new(ElasticPileStep.Survey, Index: 0),
                    new(ElasticPileStep.SegmentEnd, Text: "3", Index: 0), new(ElasticPileStep.SegmentLinked, Index: 0), new(ElasticPileStep.SegmentKind, Text: "Spirale", Index: 0),
                    new(ElasticPileStep.MainTransverse, Text: "Spirale"), new(ElasticPileStep.Detail, "z_d", "0,7"), new(ElasticPileStep.Minimum),
                    new(ElasticPileStep.Approve, Text: "ipotesi"), new(ElasticPileStep.Split, Text: "2", Index: 0), new(ElasticPileStep.Propose), new(ElasticPileStep.Concrete, Text: "applica", Index: 0),
                    new(ElasticPileStep.Design, Text: "16|24|10|150", Index: 0) })
                    await side.Apply(step);
                Check(host.Editor.Data.ToJsonString(J.Options) == before0 && host.Modified == 0 && host.Dialogs.Count == 0,
                    module + ": sola lettura della scheda elastica, digitazione, caselle, tratti, ipotesi e dialoghi rifiutati");
                engine.Release(); await engine.DrainAsync();
            }
        }

        // The c.a. verifier (E1): the dialog of the services with the view of its DataTemplate and the RC section of the Release (ConcreteSheetViewModel
        // and its view, since the switch of 23/10/2026; until then the historic ConcreteWorkspace) on the sheet of the segment; «Applica» writes the
        // reinforcement, the closing keeps the sheet in the segment, nothing goes to the window (no copy of a module).
        {
            var services = new Services();
            using var host = new Hosted(Editors(services).Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "single-soil-elastic")));
            await host.Sheet.Elastic.PendingCalculation.WaitAsync(Calculation);
            await host.Settle();
            var view = ElasticViewOf(host);
            var shown = new List<(PileConcreteVerifier Verifier, PileConcreteVerifierView View, bool Workspace, bool KeptWhileOpen)>();
            services.Dialog = async dialog =>
            {
                var verifier = (PileConcreteVerifier)dialog;
                var window = new Window { Title = verifier.Title, Content = verifier, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false, ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -32000, Top = -32000 };
                window.Show();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                var content = Visual(window).OfType<PileConcreteVerifierView>().Single();
                shown.Add((verifier, content, content.Editor is { Sheet: Anthea.Presentation.Modules.ConcreteSection.ConcreteSheetViewModel } editor
                    && Visual(window).OfType<X.Desktop.Views.ConcreteSection.ConcreteSectionSheetView>().Any(v => ReferenceEquals(v.DataContext, editor.Sheet)),
                    host.Editor.Data["elastico"]!["tratti"]![verifier.Index]!["foglio_cls"] is null));
                Press(content.ApplyButton);
                window.Close();
                return verifier.Result;
            };
            var card = Visual(view.Reinforcement.Segments).OfType<Button>().First(b => Equals(b.Content, "Apri verificatore c.a."));
            Press(card);
            if (card.Command is IAsyncRelayCommand command && command.ExecutionTask is { } running) await running.WaitAsync(Calculation);
            await host.Settle();
            var (verifierShown, verifierView, hosted, kept) = shown.Single();
            Check(verifierShown.Title == "Verificatore c.a. · T1" && verifierView.Width == 1264 && verifierView.Height == 861 && hosted && kept,
                "Verificatore c.a. (E1): dialogo dei servizi con la vista del suo modello (1264 × 861) e la sezione c.a. della Release (ViewModel e vista) sul foglio del tratto");
            Check(host.Copies.Count == 0 && host.Editor.Data["elastico"]!["tratti"]![0]!["foglio_cls"] is JsonObject sheet && sheet.ToJsonString() == verifierShown.Sheet.ToJsonString()
                && verifierView.NoteText.Text == ElasticPileFields.ConcreteApplied && host.Modified > 0,
                "Verificatore c.a. (E1): «Applica armatura…» scrive il palo, la chiusura conserva il foglio nel tratto, nessuna copia alla finestra");
            host.Editor.InspectRevision();
            await host.Settle();
            Check(!Visual(view).OfType<Button>().Where(b => Equals(b.Content, "Apri verificatore c.a.")).Any(b => b.IsEnabled) && shown.Count == 1,
                "Verificatore c.a. (E1): spento in sola lettura, come la vista storica");
        }

        // «Esporta CSV» of «Valori per quota» and «Esporta PNG…» of the sheet of the reinforcement: the bytes of the historic buttons.
        {
            var services = new Services();
            var data = HorizontalPileScenarios.FixtureData(root, Pile, "elastico-tratti-anello-spirale");
            using var host = new Hosted(Editors(services).Open(Pile, data));
            await host.Sheet.Elastic.PendingCalculation.WaitAsync(Calculation);
            host.Editor.Commit(); await host.Editor.CalculateAsync().WaitAsync(Calculation); host.Editor.Commit();
            await host.Settle();
            var view = ElasticViewOf(host);
            view.ShowWindow = host.ShowOffScreen;
            string csv = Path.Combine(folder, "risposta-elastica.csv");
            services.SavePath = csv;
            // The user opens «Valori per quota · N–V–M concomitanti» (closed at the opening, as the historic group) and presses its button.
            Visual(view).OfType<Expander>().Single(e => Equals(e.Header, "Valori per quota · N–V–M concomitanti")).IsExpanded = true;
            await host.Settle();
            Press(view.CsvButton);
            // The file of the historic button (ElasticPileEditor.Csv) on the result shown.
            var shown = host.Sheet.Elastic.Result ?? throw new InvalidOperationException("Risposta elastica senza risultato");
            Check(File.Exists(csv) && File.ReadAllBytes(csv).SequenceEqual(Encoding.UTF8.GetBytes(ElasticPileEditor.Csv(shown))),
                "«Esporta CSV» della scheda elastica scrive il file del bottone storico sul risultato mostrato");
            string png = Path.Combine(folder, "armature-palo.png");
            services.SavePath = png;
            Press(Visual(view).OfType<Button>().Single(b => Equals(b.Content, "Apri tavola armature…")));
            var sheetView = Visual(host.Dialogs.Single()).OfType<ElasticSheetView>().Single();
            Press(Visual(sheetView).OfType<Button>().Single(b => Equals(b.Content, "Esporta PNG…")));
            host.CloseDialogs();
            services.SavePath = null;
            var reference = new PileReinforcementDrawing { HorizontalAlignment = HorizontalAlignment.Left };
            reference.Set(shown);
            byte[] expectedPng = reference.Png((int)Math.Ceiling(reference.SheetSize.Width * 2), (int)Math.Ceiling(reference.SheetSize.Height * 2));
            if (!File.Exists(png) || !File.ReadAllBytes(png).SequenceEqual(expectedPng)) File.WriteAllBytes(Path.Combine(folder, "armature-palo-atteso.png"), expectedPng);
            Check(File.Exists(png) && File.ReadAllBytes(png).SequenceEqual(expectedPng), "«Esporta PNG…» della tavola delle armature scrive l'immagine del bottone storico (risultato mostrato)");
            Check(host.Modified == 0, "Le esportazioni della scheda elastica non modificano il foglio");
        }
    }
}
