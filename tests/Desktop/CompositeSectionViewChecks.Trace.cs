using System.Collections;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.CompositeSection;
using Anthea.Presentation.Tests;
using X.Core;
using X.Desktop.Services;
using X.Desktop.Views.CompositeSection;

namespace X.Desktop;

/// <summary>The traces and the drawings through the XAML view (F5.11).</summary>
internal static partial class CompositeSectionViewChecks
{
    // ---------------------------------------------------------------- 1. traces

    private static async Task Traces(string root, string output)
    {
        string folder = Path.Combine(output, "tracce");
        Directory.CreateDirectory(folder);
        var differences = new List<string>();
        int scenarios = 0, lines = 0;
        foreach (var scenario in CompositeSectionScenarios.All(root))
        {
            var trace = new List<string>();
            var engine = new HeldModuleService(Module);
            var side = new ViewSide(CompositeSectionScenarios.FixtureData(root, scenario.Fixture), engine);
            try
            {
                // The state of the opening is read at once, before the dispatcher runs anything.
                var previous = side.State();
                trace.AddRange(CompositeSectionTrace.Lines("apertura", null, previous));
                foreach (var step in scenario.Steps)
                {
                    await side.Apply(step);
                    var state = side.State();
                    trace.AddRange(CompositeSectionTrace.Lines(step.ToString(), previous, state));
                    previous = state;
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or TimeoutException or KeyNotFoundException or NullReferenceException)
            { trace.Add("errore " + error.GetType().Name + ": " + error.Message); Log.Add("   " + scenario.Name + ": " + error); }
            finally { engine.Release(); side.Dispose(); }
            CompositeSectionTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), trace);
            string stored = Path.Combine(CompositeSectionScenarios.Reference(root), scenario.Name + ".txt");
            string? difference = File.Exists(stored) ? CompositeSectionTrace.FirstDifference(CompositeSectionTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.Name + ": " + difference);
            scenarios++; lines += trace.Count;
        }
        if (differences.Count > 0) File.WriteAllLines(Path.Combine(folder, "differenze.txt"), differences, new UTF8Encoding(false));
        Check(differences.Count == 0 && scenarios == 20, $"Tracce della vista XAML uguali a quelle della vista storica: {scenarios} scenari, {lines} righe"
            + (differences.Count == 0 ? "" : " — " + differences[0]));
    }

    /// <summary>
    /// The sheet with the XAML view, driven through its controls as the user does it: a control the user cannot use (not on the page shown, hidden,
    /// disabled) does nothing; read back from the controls of the view (texts, pages, choices, switches, the drawing, the local sketch and the
    /// charts), the forms, the cards and the panels from the ViewModel (MU19). The historic view protected a read-only revision with
    /// RevisionInspection, reaching first the controls of its logical tree, then those shown at every layout: as the adapter without WPF of
    /// Presentation.Tests, the side models which parts the protection of the historic view had reached (<see cref="protectedParts"/>); the XAML
    /// view refuses every edit at once, so the two agree on what the user can do.
    /// </summary>
    private sealed class ViewSide : IDisposable
    {
        private readonly HeldModuleService engine;
        private readonly Services services = new();
        private Hosted host = null!;
        private string message = "";
        private bool readOnly;
        private readonly HashSet<object> protectedParts = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<string> expanded = new(StringComparer.Ordinal);

        internal ViewSide(JsonObject data, HeldModuleService engine) { this.engine = engine; Attach(data); }

        internal Hosted Host => host;
        private CompositeSectionSheetViewModel Sheet => host.Sheet;
        private CompositeSectionSheetView View => host.View;
        private FrameworkElement Root => host.Root;

        private void Attach(JsonObject data)
        {
            var presentation = services.Create();
            var module = DesktopComposition.CreateModules().SheetModule(Module);
            host = Idle(() => new Hosted(new MvvmSheetEditor(module, data, presentation, engine, new CalculationLimiter(CalculationLimiter.DefaultMaximum))));
            readOnly = false; protectedParts.Clear();
            // The groups of the inputs as the historic view builds them: only «Geometria» expanded on the control panel.
            expanded.Clear(); expanded.Add("geometria");
        }

        public void Dispose() => host.Dispose();

        // ---------------------------------------------------------------- the parts of the view (the model of the historic protection)

        private const string Toolbar = "barra", Detail = "prospetto", Table = "tabella", Latest = "ultima", PhaseCommands = "comandi-fasi", CurveControls = "curve";

        private static readonly Dictionary<string, (int Page, string? Group)> FormPlaces = new(StringComparer.Ordinal)
        {
            ["normativa"] = (0, null), ["coefficienti"] = (0, "coefficienti"), ["materiali"] = (0, "materiali"), ["soletta"] = (0, "geometria"),
            ["predalle"] = (0, "geometria"), ["carpenteria"] = (0, "geometria"), ["sezione"] = (0, "geometria"), ["piastra"] = (0, "geometria"),
            ["armatura-sup"] = (0, "armature"), ["armatura-inf"] = (0, "armature"), ["trasversale"] = (0, "pioli"), ["fatica"] = (0, "pioli"),
            ["pioli"] = (0, "pioli"), ["irrigidimenti"] = (0, "irrigidimenti"), ["appoggio"] = (0, "appoggio"), ["cassoncino"] = (0, "cassoncino"),
            ["metodo"] = (1, null), ["discretizzazione"] = (1, "discretizzazione"), ["limiti"] = (1, null)
        };

        private bool ContainerShown(string name) => name switch
        {
            "piastra" => Sheet.PlateShown, "trasversale" => Sheet.TransverseShown, "fatica" => Sheet.FatigueShown, "cassoncino" => Sheet.BoxShown,
            "discretizzazione" => Sheet.MeshShown, _ => true
        };

        private bool MainShown => Sheet.Page != 2;
        private bool InputPageShown(int page) => MainShown && Sheet.InputPage == page;
        private bool FormReachable(string name) => FormPlaces.TryGetValue(name, out var place) && InputPageShown(place.Page) && ContainerShown(name);

        private bool FormDisplayed(string name)
        {
            if (!FormReachable(name)) return false;
            var place = FormPlaces[name];
            bool group = place.Group is null || expanded.Contains(place.Group);
            return name is "trasversale" or "fatica" ? group && expanded.Contains(name) : group;
        }

        private bool ToolbarReachable => Sheet.IsDetached || MainShown;

        private IEnumerable<object> LogicalParts()
        {
            if (MainShown)
            {
                if (!Sheet.IsDetached) yield return Toolbar;
                yield return Detail; yield return Table; yield return Latest;
                if (Sheet.InputPage == 0) foreach (string name in FormPlaces.Keys.Where(n => FormPlaces[n].Page == 0)) yield return Sheet.Forms[name];
                if (Sheet.InputPage == 1)
                {
                    foreach (string name in FormPlaces.Keys.Where(n => FormPlaces[n].Page == 1)) yield return Sheet.Forms[name];
                    foreach (var part in Cards()) yield return part;
                    yield return PhaseCommands;
                }
                if (!Sheet.AnalysisPanelsShown && Sheet.SlabPropertiesForm is { } slab) yield return slab;
            }
            else
            {
                if (Sheet.CurveForm is { } curve) yield return curve;
                yield return CurveControls;
            }
        }

        private IEnumerable<object> DisplayedParts()
        {
            if (MainShown)
            {
                if (!Sheet.IsDetached) yield return Toolbar;
                if (Sheet.DetailShown && Sheet.DetailExpanded) yield return Detail;
                if (Sheet.AnalysisPanelsShown && Sheet.ResultsTab == 0) yield return Table;
                yield return Latest;
                foreach (string name in FormPlaces.Keys.Where(FormDisplayed)) yield return Sheet.Forms[name];
                if (Sheet.InputPage == 1) { foreach (var part in Cards()) yield return part; yield return PhaseCommands; }
                if (!Sheet.AnalysisPanelsShown && Sheet.SelectedPropertyTab == 1 && Sheet.SlabPropertiesForm is { } slab) yield return slab;
            }
            else
            {
                if (Sheet.CurveForm is { } curve) yield return curve;
                yield return CurveControls;
            }
        }

        private IEnumerable<object> Cards()
        {
            foreach (var card in Sheet.Phases)
            {
                yield return card; yield return card.Form;
                if (card.Homogenization is { } h) yield return h;
            }
        }

        private bool Protected(object part) => protectedParts.Contains(part);

        private void Protect()
        {
            if (readOnly) foreach (var part in DisplayedParts()) protectedParts.Add(part);
        }

        // ---------------------------------------------------------------- the controls

        private IEnumerable<DependencyObject> All => Walk(Root, popups: false);

        /// <summary>The form named by the trace.</summary>
        private FormViewModel? FormFor(string name, int phase)
        {
            var s = Sheet;
            switch (name)
            {
                case CompositeSectionScenarios.PhaseForm: return phase >= 0 && phase < s.Phases.Count ? s.Phases[phase].Form : null;
                case CompositeSectionScenarios.HomogenizationForm:
                    var phases = s.Data.Array("fasi");
                    return phase >= 0 && phase < phases.Count ? s.Phases.FirstOrDefault(c => ReferenceEquals(c.Phase, phases[phase]))?.Homogenization : null;
                case CompositeSectionScenarios.SlabPropertiesForm: return s.SlabPropertiesForm;
                case CompositeSectionScenarios.CurveForm: return s.CurveForm;
                default: return s.Forms[name];
            }
        }

        private bool FormUsable(string name, FormViewModel form)
        {
            if (Protected(form)) return false;
            return name switch
            {
                CompositeSectionScenarios.PhaseForm or CompositeSectionScenarios.HomogenizationForm => InputPageShown(1),
                CompositeSectionScenarios.SlabPropertiesForm => MainShown && !Sheet.AnalysisPanelsShown,
                CompositeSectionScenarios.CurveForm => !MainShown,
                _ => FormReachable(name)
            };
        }

        /// <summary>The editor of a field in its FieldForm (built where WPF has not laid it out yet, as the historic InputForm builds every editor).</summary>
        private T? Editor<T>(FieldViewModel field) where T : FrameworkElement
        {
            T? Find() => All.OfType<T>().FirstOrDefault(e => ReferenceEquals(e.DataContext, field) && e.TemplatedParent is not ComboBox);
            if (Find() is { } editor) return editor;
            for (int pass = 0; pass < 3; pass++)
            {
                Realize(Root); host.Layout();
                if (Find() is { } built) return built;
            }
            return null;
        }

        /// <summary>The container of the card of a phase, laid out.</summary>
        private FrameworkElement? Card(int index)
        {
            var cards = View.PhaseCards;
            if (index < 0 || index >= cards.Items.Count) return null;
            if (cards.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container) { Realize(Root); host.Layout(); }
            return cards.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
        }

        private static bool Toggle(ToggleButton box)
        {
            if (!box.IsEnabled) return false;
            box.IsChecked = box.IsChecked != true;
            return true;
        }

        // ---------------------------------------------------------------- steps

        internal async Task Apply(CompositeSectionStep step)
        {
            message = "";
            var wait = Idle(() => Start(step));
            if (wait is not null) await wait.WaitAsync(TimeSpan.FromSeconds(120));
            // «Calcola curva» is enabled again when its command has ended (its continuation runs at the idle priority of the sheet), as the
            // historic button at the end of the curve.
            if (step.Kind == CompositeSectionStep.CurveCalculate) await Commands();
            await host.Settle();
            Protect();
        }

        private Task? Start(CompositeSectionStep step)
        {
            var s = Sheet;
            var view = View;
            switch (step.Kind)
            {
                case CompositeSectionStep.Commit: host.Editor.Commit(); return null;
                // «Calcola» of the window: the editor commits the sheet, then calculates at once.
                case CompositeSectionStep.Calculate: return host.Editor.CalculateAsync();
                case CompositeSectionStep.Start: _ = host.Editor.CalculateAsync(); return null;
                case CompositeSectionStep.Wait: return s.PendingWork;
                case CompositeSectionStep.ReadOnly:
                    host.Editor.InspectRevision(); readOnly = true;
                    foreach (var part in LogicalParts()) protectedParts.Add(part);
                    return null;
                case CompositeSectionStep.Reopen:
                {
                    var data = host.Editor.Data; host.Dispose(); Attach(data); return null;
                }
                case CompositeSectionStep.Hold: engine.Hold(); return null;
                case CompositeSectionStep.Entered: return engine.EnteredAsync();
                case CompositeSectionStep.Release: engine.Release(); return null;
                case CompositeSectionStep.Visit:
                    if (MainShown)
                    {
                        foreach (var (_, place) in FormPlaces) if (place.Page == s.InputPage && place.Group is not null) expanded.Add(place.Group);
                        if (s.InputPage == 0) { expanded.Add("trasversale"); expanded.Add("fatica"); }
                    }
                    CompositeSectionViewChecks.Visit(Root);
                    return null;
                case CompositeSectionStep.Report:
                    try
                    {
                        var options = step.Text is null ? ReportBridge.DefaultSections() : step.Text.Split(',').ToHashSet();
                        host.Editor.BuildReport("Traccia F5.11", options);
                        message = "report disponibile";
                    }
                    catch (Exception error) when (error is InvalidOperationException or ArgumentException) { message = "report non disponibile: " + error.Message; }
                    return null;
                case CompositeSectionStep.CurveCalculate:
                    if (MainShown || s.CurveBusy || Protected(CurveControls) || !view.CurveButton.IsEnabled) return null;
                    Press(view.CurveButton, Root);
                    return s.CalculateCurveCommand.ExecutionTask ?? Task.CompletedTask;
                case CompositeSectionStep.Page: view.Pages.SelectedIndex = step.Index; return null;
                case CompositeSectionStep.Field:
                {
                    var form = FormFor(step.Form!, step.Index);
                    if (form is null || !form.Contains(step.Key!) || form[step.Key!] is not { Kind: FieldKind.Text } field || !field.IsVisible || !field.IsEnabled || !FormUsable(step.Form!, form)) return null;
                    if (Editor<TextBox>(field) is { } text && text.IsEnabled && !text.IsReadOnly)
                    {
                        // The user focuses the field (the raw text replaces the formatted one), types and leaves, as FieldForm reports the focus.
                        field.IsFocused = true; text.Text = step.Text; field.IsFocused = false;
                    }
                    return null;
                }
                case CompositeSectionStep.Choice:
                {
                    var form = FormFor(step.Form!, step.Index);
                    if (form is null || !form.Contains(step.Key!) || form[step.Key!] is not { Kind: FieldKind.Choice } field || !field.IsVisible || !field.IsEnabled || !FormUsable(step.Form!, form)) return null;
                    if (Editor<ComboBox>(field) is { } choice && choice.IsEnabled) choice.SelectedItem = step.Text;
                    return null;
                }
                case CompositeSectionStep.Option:
                {
                    var form = FormFor(step.Form!, step.Index);
                    if (form is null || !form.Contains(step.Key!) || form[step.Key!] is not { Kind: FieldKind.Bool } field || !field.IsVisible || !field.IsEnabled || !FormUsable(step.Form!, form)) return null;
                    if (Editor<CheckBox>(field) is { } box) Toggle(box);
                    return null;
                }
                case CompositeSectionStep.AddPhase: if (InputPageShown(1) && !Protected(PhaseCommands)) Press(view.AddPhaseButton, Root); return null;
                case CompositeSectionStep.AddShrinkage: if (InputPageShown(1) && !Protected(PhaseCommands)) Press(view.AddShrinkageButton, Root); return null;
                case CompositeSectionStep.MovePhase:
                case CompositeSectionStep.DeletePhase:
                {
                    if (step.Index < 0 || step.Index >= s.Phases.Count || !InputPageShown(1) || Protected(s.Phases[step.Index])) return null;
                    string content = step.Kind == CompositeSectionStep.DeletePhase ? "Elimina" : step.Row < 0 ? "↑" : "↓";
                    if (Card(step.Index) is { } card) Press(Caption(card, content), Root);
                    return null;
                }
                case CompositeSectionStep.Cell:
                {
                    var grid = view.ActionsGrid;
                    if (!TableUsable || Protected(Table) || grid.IsReadOnly || step.Row < 0 || step.Row >= grid.Items.Count) return null;
                    var row = (JsonRowViewModel)grid.Items[step.Row];
                    if (grid.Column(step.Key!).Visibility != Visibility.Visible || s.IsCellInactive(row.Values.S("tipo"), step.Key!)) return null;
                    // The bindings of the cells write the row at every change (UpdateSourceTrigger=PropertyChanged).
                    if (step.Text is null) row[step.Key!] = !(row[step.Key!] is true);
                    else row[step.Key!] = step.Text;
                    return null;
                }
                case CompositeSectionStep.SelectRow: if (TableUsable && step.Row < view.ActionsGrid.Items.Count) view.ActionsGrid.SelectedIndex = step.Row; return null;
                case CompositeSectionStep.TableLoad: TableButton("+ Carico"); return null;
                case CompositeSectionStep.TableShrinkage: TableButton("+ Ritiro"); return null;
                case CompositeSectionStep.TableUp: TableButton("↑"); return null;
                case CompositeSectionStep.TableDown: TableButton("↓"); return null;
                case CompositeSectionStep.TableDelete: TableButton("Elimina"); return null;
                case CompositeSectionStep.Situation:
                {
                    // With the view detached the situation is chosen in the window of the view.
                    if (view.DetachedView is { } detached) { if (step.Index < detached.SituationChoice.Items.Count) detached.SituationChoice.SelectedIndex = step.Index; }
                    else if (MainShown && Shown(view.StageChoice, Root) && step.Index < view.StageChoice.Items.Count) view.StageChoice.SelectedIndex = step.Index;
                    return null;
                }
                case CompositeSectionStep.Latest: if (MainShown && s.StageControlsShown && !Protected(Latest)) Press(view.LatestButton, Root); return null;
                case CompositeSectionStep.Display: if (ToolbarReachable && view.DisplayChoice.Visibility == Visibility.Visible) view.DisplayChoice.SelectedIndex = step.Index; return null;
                case CompositeSectionStep.Results: if (MainShown && s.AnalysisPanelsShown) view.Results.SelectedIndex = step.Index; return null;
                case CompositeSectionStep.Switch: Switch(step.Key!); return null;
                case CompositeSectionStep.Scale:
                    if (s.ScaleShown && ToolbarReachable && !Protected(Toolbar) && !view.ScaleEditor.IsReadOnly)
                    {
                        view.ScaleEditor.Text = step.Text; Press(view.ScaleEditor, System.Windows.Input.Key.Enter);
                    }
                    return null;
                case CompositeSectionStep.DetailSection: if (MainShown && s.DetailShown && !Protected(Detail) && view.DetailSectionChoice.IsEnabled) view.DetailSectionChoice.SelectedIndex = step.Index; return null;
                case CompositeSectionStep.PropertyTab: if (MainShown && !s.AnalysisPanelsShown && step.Index < view.PropertyTabs.Items.Count) view.PropertyTabs.SelectedIndex = step.Index; return null;
                case CompositeSectionStep.EffectiveDetails: if (MainShown && s.AnalysisPanelsShown) Press(Caption(view.SummaryPanel, "Dettagli sezione efficace ↓"), Root); return null;
                case CompositeSectionStep.CurvePhase:
                    if (!MainShown && s.CurvePhaseShown && !Protected(CurveControls) && view.CurvePhaseChoice.IsEnabled && step.Index < view.CurvePhaseChoice.Items.Count) view.CurvePhaseChoice.SelectedIndex = step.Index;
                    return null;
                case CompositeSectionStep.CurveFiber:
                    if (!MainShown && !Protected(CurveControls) && view.FiberChoice.IsEnabled && step.Index < view.FiberChoice.Items.Count) view.FiberChoice.SelectedIndex = step.Index;
                    return null;
                case CompositeSectionStep.CurvePoint: if (!MainShown && !Protected(CurveControls) && view.PointSlider.IsEnabled) view.PointSlider.Value = step.Index; return null;
                case CompositeSectionStep.Detach: if (ToolbarReachable) Press(view.DetachButton, Root); return null;
                case CompositeSectionStep.Reattach: view.DetachedWindow?.Close(); return null;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        /// <summary>Lets the asynchronous commands of the sheet end (their continuations wait at the idle priority, as those of the sheet).</summary>
        private async Task Commands()
        {
            // The command re-enables its button (CanExecuteChanged) in the continuation that follows its task.
            for (int i = 0; i < 200 && (Sheet.CalculateCurveCommand.IsRunning || View.CurveButton.IsEnabled != Sheet.CalculateCurveCommand.CanExecute(null)); i++)
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        /// <summary>The table of the actions can be reached: on the page of the phases, in the panel of the results.</summary>
        private bool TableUsable => InputPageShown(1) && Sheet.AnalysisPanelsShown;

        /// <summary>A button under the table (of the tab «Sollecitazioni», reached also when another tab is chosen, as in the historic view).</summary>
        private void TableButton(string content)
        {
            if (!MainShown || !Sheet.AnalysisPanelsShown || Protected(Table)) return;
            var tab = (TabItem)View.Results.Items[0];
            Press(Caption((DependencyObject)tab.Content, content), Root);
        }

        private void Switch(string key)
        {
            var s = Sheet; var view = View;
            bool toolbar = ToolbarReachable && !Protected(Toolbar);
            switch (key)
            {
                case "quote": if (toolbar && s.LabelsShown) Toggle(view.GeometryLabelsSwitch); break;
                case "info-armature": if (toolbar && s.LabelsShown) Toggle(view.RebarLabelsSwitch); break;
                case "asse-neutro": if (ToolbarReachable && s.NeutralAxisShown) Toggle(view.NeutralAxisSwitch); break;
                case "scala-auto": if (toolbar && s.ScaleShown) Toggle(view.ScaleAutoSwitch); break;
                case "contorno-sezione": Menu(0); break;
                case "contorno-tensioni": Menu(1); break;
                case "limiti-tensioni": Menu(2); break;
                case "prospetto": if (MainShown && s.DetailShown) view.DetailExpander.IsExpanded = !view.DetailExpander.IsExpanded; break;
                default: throw new ArgumentException("Interruttore sconosciuto: " + key);
            }
            // The switches of «Verifiche ▾»: the menu opens from its button.
            void Menu(int index)
            {
                if (!toolbar || !view.ChecksMenuButton.IsEnabled) return;
                var item = (MenuItem)view.ChecksMenuButton.ContextMenu.Items[index];
                item.IsChecked = !item.IsChecked;
            }
        }

        // ---------------------------------------------------------------- state

        internal CompositeSectionTraceState State()
        {
            var s = Sheet; var view = View;
            var t = new CompositeSectionTraceState();
            static string Q(string? text) => CompositeSectionTrace.Quote(text);
            static string N(int value) => CompositeSectionTrace.Number(value);
            static string D(double value) => CompositeSectionTrace.Number(value);
            static string YN(bool value) => CompositeSectionTrace.YesNo(value);
            static bool Visible(UIElement element) => element.Visibility == Visibility.Visible;
            static string Check(ToggleButton box, bool enabled) => YN(box.IsChecked == true) + (Visible(box) ? "" : "(nascosto)") + (enabled ? "" : "(spento)");
            bool toolbar = !Protected(Toolbar);
            t.Data(s.Data);
            t.Add("modificato", N(host.Modified));
            t.Add("messaggio", message);
            t.Add("calcolo", "occupato " + YN(host.Editor.Busy) + " · risultato " + (host.Editor.Result is { } result ? CompositeSectionTrace.Hash(result) : "-")
                + " · mostrato " + (s.DisplayedCalculation is null ? "-" : ReferenceEquals(s.DisplayedCalculation, s.Calculation) ? "corrente" : "da aggiornare")
                + " · esportabile " + YN(s.Calculation is not null && !s.IsBusy));
            t.Add("stato", Q(view.StatusText.Text));
            t.Add("avviso", Visible(view.NoticeText) ? Q(view.NoticeText.Text) : "-");
            t.Add("avvisi", Q(view.WarningsText.Text));
            t.Add("panoramica", Q(view.OverviewText.Text));
            t.Add("riepilogo", Q(s.SummaryCards.Text));
            t.Add("materiali", Q(s.MaterialText));
            t.Add("altezza", Q(s.SteelHeightText));
            t.Add("soletta", Q(s.SlabLayoutText));
            t.Add("metodo", Q(s.MethodNotice));
            t.Add("convenzione", Q(s.LoadConvention));
            t.Add("fasi", Q(s.PhaseCountText));
            t.Add("convergenza", Q(view.ConvergenceText.Text));
            t.Add("pagina", N(view.Pages.SelectedIndex) + " · titolo " + Q(view.InputTitle.Text) + " · " + Q(s.InputSubtitle) + " · pannello " + (Visible(view.SummaryPanel) ? "riepilogo" : "proprietà")
                + " · tabelle " + YN(Visible(view.ResultsPanel)) + " · staccata " + YN(view.DetachedWindow is not null));
            var stage = view.DetachedView?.SituationChoice ?? view.StageChoice;
            t.Add("situazioni", Q(string.Join("|", view.StageChoice.Items.Cast<object>())) + " · scelta " + N(stage.SelectedIndex) + " · visibile " + YN(Visible(view.StageControls)) + " · attiva sì");
            t.Add("vista", N(view.DisplayChoice.SelectedIndex) + " · visibile " + YN(Visible(view.DisplayChoice)) + " · risultati " + N(view.Results.SelectedIndex));
            t.Add("interruttori", "quote " + Check(view.GeometryLabelsSwitch, toolbar) + " · armature " + Check(view.RebarLabelsSwitch, toolbar)
                + " · asse neutro " + Check(view.NeutralAxisSwitch, true) + " " + Q(view.NeutralAxisSwitch.ToolTip as string) + " · contorno sezione " + YN(Item(0))
                + " · contorno tensioni " + YN(Item(1)) + " · limiti " + YN(Item(2)) + " · menu " + YN(toolbar));
            t.Add("scala", Q(view.ScaleEditor.Text) + " · auto " + Check(view.ScaleAutoSwitch, toolbar) + " · visibile " + YN(Visible(view.ScaleControls)) + " · errore " + YN(s.ScaleError)
                + " · " + Q(view.ScaleAutoSwitch.ToolTip as string) + " · modificabile " + YN(toolbar));
            t.Add("prospetto", "visibile " + YN(Visible(view.DetailExpander)) + " · aperto " + YN(view.DetailExpander.IsExpanded) + " · sezione " + N(view.DetailSectionChoice.SelectedIndex)
                + " " + YN(!Protected(Detail)) + " · schizzo appoggio " + YN(view.DetailSketch.AtSupport));
            t.Add("contenitori", "piastra " + YN(FormHostShown("piastra")) + " · nota piastra " + YN(s.PlateShown) + " · trasversale " + YN(GroupShown("trasversale"))
                + " · fatica " + YN(GroupShown("fatica")) + " · cassoncino " + YN(GroupShown("cassoncino")) + " · discretizzazione " + YN(GroupShown("discretizzazione"))
                + " · torsione risultati " + YN(Visible(view.TorsionGroup)));
            foreach (var (name, _) in CompositeSectionScenarios.Forms) t.Form(name, Fields(s.Forms[name]));
            var phases = s.Data.Array("fasi");
            t.Add("schede", N(view.PhaseCards.Items.Count));
            for (int i = 0; i < s.Phases.Count; i++)
            {
                var card = s.Phases[i];
                string off = Protected(card) ? "(spento)" : "";
                t.Add("scheda." + i, Q(card.Header) + " · note " + Q(string.Join("|", card.Notes)) + " · comandi ↑" + off + " ↓" + off + " Elimina" + off);
                t.Form(CompositeSectionScenarios.PhaseForm + "." + i, Fields(card.Form));
                var homogenization = i < phases.Count ? s.Phases.FirstOrDefault(c => ReferenceEquals(c.Phase, phases[i])) : null;
                t.Form(CompositeSectionScenarios.HomogenizationForm + "." + i, Fields(homogenization?.Homogenization));
                if (homogenization?.Homogenization is not null) t.Add("scheda." + i + ".omogeneizzazione", Q(homogenization.HomogenizationInfo));
            }
            string phaseCommands = Protected(PhaseCommands) ? "(spento)" : "";
            t.Add("comandi-fasi", "+ Aggiungi fase" + phaseCommands + " + Ritiro" + phaseCommands);
            var grid = view.ActionsGrid;
            bool table = Protected(Table);
            t.Add("azioni", "righe " + N(grid.Items.Count) + " · selezione " + N(grid.SelectedIndex) + " · ΔT " + YN(grid.Column("T").Visibility == Visibility.Visible) + " · bloccata " + YN(table)
                + " · stessi oggetti " + YN(grid.Items.Cast<JsonRowViewModel>().Select(r => r.Values).SequenceEqual(phases.OfType<JsonObject>())));
            for (int i = 0; i < grid.Items.Count; i++)
            {
                string kind = ((JsonRowViewModel)grid.Items[i]).Values.S("tipo");
                t.Add("azioni." + i, "spente " + string.Join(",", CompositeSectionFields.Actions.Where(c => s.IsCellInactive(kind, c.Key)).Select(c => c.Key)));
            }
            string tableCommands = table ? "(spento)" : "";
            t.Add("comandi-azioni", string.Join(" ", new[] { "+ Carico", "+ Ritiro", "↑", "↓", "Elimina" }.Select(b => b + tableCommands)));
            t.Panel("pannello.tensioni", Blocks(s.StressPanel));
            t.Panel("pannello.verifiche", Blocks(s.VerificationPanel));
            t.Panel("pannello.omogeneizzazione", Blocks(s.HomogenizationPanel));
            t.Panel("pannello.equilibrio", Blocks(s.EquilibriumPanel));
            t.Panel("pannello.classe4", Blocks(s.Class4Panel));
            t.Panel("pannello.geometria", Blocks(s.GeometryPanel));
            t.Panel("pannello.taglio", Blocks(s.ShearPanel));
            t.Panel("pannello.pioli", Blocks(s.StudsPanel));
            t.Panel("pannello.torsione", Blocks(s.TorsionPanel));
            t.Add("proprieta", Q(string.Join("|", view.PropertyTabs.Items.Cast<CompositeSectionPropertyTab>().Select(p => p.Header))) + " · scelta " + N(view.PropertyTabs.SelectedIndex)
                + " · nota " + Q(s.PropertyNotice));
            t.Panel("proprieta.acciaio", s.SteelPropertyTables.SelectMany(PropertyBlocks).ToList());
            t.Form(CompositeSectionScenarios.SlabPropertiesForm, Fields(s.SlabPropertiesForm));
            t.Panel("proprieta.soletta", s.SlabPropertyError is { } slabError ? [new(CompositeSectionBlock.TextKind, slabError)] : s.SlabPropertyTables.SelectMany(PropertyBlocks).ToList());
            for (int i = 2; i < s.PropertyTabs.Count; i++) t.Panel("proprieta.fase." + (i - 2), PhaseBlocks(s.PropertyTabs[i]));
            // The drawing of the view.
            var shown = s.DisplayedCalculation;
            var d = view.Drawing;
            t.Add("disegno", "modo " + N(d.Mode) + " · da aggiornare " + YN(d.IsStale) + " · quote " + YN(d.ShowGeometryLabels) + " · armature " + YN(d.ShowRebarLabels)
                + " · asse neutro " + YN(d.ShowNeutralAxis) + " · contorni " + YN(d.ContourSection) + YN(d.ContourDiagram) + " · limiti " + YN(d.ShowStressLimits)
                + " · CLS × " + D(d.ConcreteAmplification) + " · appoggio " + YN(d.DetailAtSupport));
            t.Add("disegno.geometria", (d.Geometry is null ? "-" : ReferenceEquals(d.Geometry, shown?.Geometry) ? "risultato" : "anteprima") + " " + CompositeSectionTrace.HashObject(d.Geometry));
            t.Add("disegno.ingresso", d.Input is null ? "-" : (ReferenceEquals(d.Input, shown?.Input) ? "risultato " : "anteprima ") + CompositeSectionTrace.Hash(d.Input));
            t.Add("disegno.fase", d.Stage is null ? "-" : N(shown?.Stages.IndexOf(d.Stage) ?? -1) + " " + Q(d.Stage.Name));
            t.Add("disegno.carichi", string.Join(" | ", d.LoadPoints.Select(p => N(p.Index) + " " + Q(p.Name) + " " + D(p.Y) + " " + D(p.Force))));
            // The page of the curves.
            bool curves = !Protected(CurveControls);
            t.Form(CompositeSectionScenarios.CurveForm, Fields(s.CurveForm));
            t.Add("curva", "fasi " + Q(string.Join("|", view.CurvePhaseChoice.Items.Cast<object>())) + " · scelta " + N(view.CurvePhaseChoice.SelectedIndex)
                + " · visibile " + YN(Visible(view.CurvePhaseChoice)) + " · calcola " + YN(!s.CurveBusy && curves) + " · esporta " + YN(view.CurveExportButton.IsEnabled)
                + " · curva " + (s.ResponseCalculation is null ? "-" : N(s.ResponseCalculation.Points.Count) + " punti"));
            t.Add("curva.avviso", Q(view.CurveNoticeText.Text));
            t.Add("curva.punto", Q(s.PointInfo) + " · cursore " + D(view.PointSlider.Value) + "/" + D(view.PointSlider.Maximum));
            t.Add("curva.fibre", Q(string.Join("|", view.FiberChoice.Items.Cast<object>())) + " · scelta " + N(view.FiberChoice.SelectedIndex));
            t.Add("curva.grafico", Chart(view.SectionPlot));
            t.Add("curva.fibra", Chart(view.FiberPlot));
            t.Panel("curva.tabella", Blocks(s.CurveTable));
            return t;

            bool Item(int index) => ((MenuItem)view.ChecksMenuButton.ContextMenu.Items[index]).IsChecked;
            static string Chart(BridgeResponsePlot plot) => Q(plot.XLabel) + " " + Q(plot.YLabel) + " · punti " + N(plot.Points.Length) + " "
                + CompositeSectionTrace.HashObject(plot.Points.Select(p => new[] { p.X, p.Y })) + " · scelto " + N(plot.Selected);
        }

        /// <summary>The host of a form of the view is shown (its own visibility: the second plate of the H).</summary>
        private bool FormHostShown(string name) => Logical<ContentControl>(View.ControlPanelInputs).Single(c => ReferenceEquals(c.Content, Sheet.Forms[name])).Visibility == Visibility.Visible;

        /// <summary>A group of the inputs (an expander with its tag) is shown (its own visibility; the page of the inputs may be out of the tree).</summary>
        private bool GroupShown(string tag) => Logical<Expander>(View.ControlPanelInputs).Concat(Logical<Expander>(View.PhaseInputs)).Single(e => Equals(e.Tag, tag)).Visibility == Visibility.Visible;

        private static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject => CompositeSectionSheetView.Logical<T>(root);

        /// <summary>The fields of a form as the historic InputForm showed them: the row (enabled, faded, shown) and the editor (protected or disabled).</summary>
        private List<CompositeSectionFieldState>? Fields(FormViewModel? form)
        {
            if (form is null) return null;
            bool locked = Protected(form);
            return form.Fields.Select(f => f.Kind switch
            {
                FieldKind.Text => new CompositeSectionFieldState(f.Key, "campo", f.Label, form.Get(f.Key), null, f.IsEnabled, f.IsDimmed, f.IsVisible, f.IsEnabled && !locked),
                FieldKind.Choice => new CompositeSectionFieldState(f.Key, "scelta", f.Label, form.Get(f.Key), f.Choices, f.IsEnabled, f.IsDimmed, f.IsVisible, f.IsEnabled && !locked),
                // The check box is the first element of its row: the protection disables the row too.
                _ => new CompositeSectionFieldState(f.Key, "opzione", f.Label, CompositeSectionTrace.YesNo(f.IsChecked), null, f.IsEnabled && !locked, f.IsDimmed, f.IsVisible, f.IsEnabled && !locked)
            }).ToList();
        }
    }

    // ---------------------------------------------------------------- blocks

    /// <summary>A panel of results as the historic view rendered it: texts, tables and groups in reading order; no panel, no blocks.</summary>
    private static List<CompositeSectionBlock> Blocks(BridgePanel? panel) => panel is null ? [] : panel.Blocks.Select(Block).ToList();

    private static CompositeSectionBlock Block(BridgeBlock block) => block.Kind switch
    {
        BridgeBlockKind.Table => new(CompositeSectionBlock.TableKind, "", block.Columns, block.Rows),
        BridgeBlockKind.Group => new(CompositeSectionBlock.GroupKind, block.Text, Children: block.Children!.Select(Block).ToList()),
        _ => new(CompositeSectionBlock.TextKind, block.Text)
    };

    /// <summary>A table of the properties (PropertyTable of the historic view): a group with the table, or the title, the note and the table.</summary>
    private static IEnumerable<CompositeSectionBlock> PropertyBlocks(PresentationTable table)
    {
        var body = new CompositeSectionBlock(CompositeSectionBlock.TableKind, "", table.Columns, table.Rows);
        if (table.Collapsible) { yield return new(CompositeSectionBlock.GroupKind, table.Title, Children: [body]); yield break; }
        yield return new(CompositeSectionBlock.TextKind, table.Title);
        if (table.Note is not null) yield return new(CompositeSectionBlock.TextKind, table.Note);
        yield return body;
    }

    /// <summary>The tab of a phase in the properties: title, description, homogenization and table, or the error.</summary>
    private static List<CompositeSectionBlock> PhaseBlocks(CompositeSectionPropertyTab tab)
    {
        if (tab.Properties is not { } p) return [new(CompositeSectionBlock.TextKind, tab.Error ?? "")];
        var blocks = new List<CompositeSectionBlock> { new(CompositeSectionBlock.TextKind, p.Title), new(CompositeSectionBlock.TextKind, p.Description) };
        if (p.Homogenization is not null) blocks.Add(new(CompositeSectionBlock.TextKind, p.Homogenization));
        blocks.AddRange(PropertyBlocks(p.Properties));
        return blocks;
    }

    // ---------------------------------------------------------------- 2. drawings

    /// <summary>
    /// For every F3 fixture of the module, opened and calculated off screen with the XAML view: the drawing lists of the drawing of the view as the
    /// sheet shows it, of the geometry (control panel), of the total stresses and of the contributions of the first and of the last situation, of the
    /// local sketch (when the sheet shows it), of the curves of the section (computed for the fixture that opens on that page), and the pixels of every
    /// figure of the report: equal to those of the historic view frozen in tests/fixtures/f5-sezione-mista/disegni/elenco.txt.
    /// </summary>
    private static async Task Drawings(string root, string output)
    {
        string folder = Path.Combine(output, "disegni");
        Directory.CreateDirectory(folder);
        var list = new List<string>();
        var services = new Services();
        var module = DesktopComposition.CreateModules().SheetModule(Module);
        foreach (string id in CompositeSectionScenarios.Fixtures(root))
        {
            var engine = new HeldModuleService(Module);
            using var host = Idle(() => new Hosted(new MvvmSheetEditor(module, CompositeSectionScenarios.FixtureData(root, id), services.Create(), engine, new CalculationLimiter(CalculationLimiter.DefaultMaximum))));
            await host.Settle();
            await host.Sheet.PendingWork.WaitAsync(TimeSpan.FromSeconds(120)); await host.Settle();
            await Idle(() => host.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(120)); await host.Settle();
            var view = host.View; var sheet = host.Sheet;
            list.Add(CompositeSectionHistoricTrace.Record(folder, id + " vista 1000x640", view.Drawing, 1000, 640));
            if (view.DetailExpander.Visibility == Visibility.Visible)
                list.Add(CompositeSectionHistoricTrace.Record(folder, id + " prospetto 800x165", view.DetailSketch, 800, 165));
            if (sheet.Page == 2 && view.CurveButton.IsEnabled)
            {
                Idle(() => { Press(view.CurveButton, host.Root); return 0; });
                await CurveEnded(host); await host.Settle();
                list.Add(CompositeSectionHistoricTrace.Record(folder, id + " curva 700x420", view.SectionPlot, 700, 420));
                list.Add(CompositeSectionHistoricTrace.Record(folder, id + " fibra 700x420", view.FiberPlot, 700, 420));
            }
            if (sheet.Calculation is not { } result) continue;
            Idle(() => view.Pages.SelectedIndex = 0); await host.Settle();
            list.Add(CompositeSectionHistoricTrace.Record(folder, id + " geometria 1000x640", view.Drawing, 1000, 640));
            Idle(() => view.Pages.SelectedIndex = 1); await host.Settle();
            foreach (int stage in new[] { 0, result.Stages.Count - 1 }.Distinct())
            {
                Idle(() => view.StageChoice.SelectedIndex = stage);
                foreach (int display in new[] { 0, 1 })
                {
                    Idle(() => view.DisplayChoice.SelectedIndex = display); await host.Settle();
                    list.Add(CompositeSectionHistoricTrace.Record(folder, $"{id} situazione {stage + 1} vista {display} 1000x640", view.Drawing, 1000, 640));
                }
            }
            foreach (var figure in ModuleFigureRenderer.BridgeFigures(result, sheet.ViewSettings, ReportBridge.DefaultSections()))
                list.Add($"{id} figura {figure.Category} {CompositeSectionTrace.Quote(figure.Caption)} · {CompositeSectionHistoricTrace.Pixels(figure.Png)}");
            engine.Release();
        }
        CompositeSectionTrace.Write(Path.Combine(folder, "elenco.txt"), list);
        string stored = Path.Combine(CompositeSectionScenarios.Drawings(root), "elenco.txt");
        string? difference = File.Exists(stored) ? CompositeSectionTrace.FirstDifference(CompositeSectionTrace.Read(stored), list) : "riferimento dei disegni assente";
        Check(difference is null, $"Disegni della vista XAML (sezione, prospetto, curve) e figure del report uguali a quelli della vista storica: {list.Count} righe" + (difference is null ? "" : " — " + difference));
    }
}
