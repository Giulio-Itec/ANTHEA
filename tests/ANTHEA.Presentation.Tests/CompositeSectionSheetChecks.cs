using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Bridge;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.CompositeSection;
using Anthea.Presentation.Services;
using Anthea.Testing.Comparison;
using Anthea.Testing.Normalization;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of the composite bridge section (F5.11) against its historic view, without WPF. This is the evidence of equivalence that
/// survives the archiving of the historic view:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/CompositeSectionTrace.cs (the path of the F3 capture for each fixture of the module, the definition of
/// the section, the accessories, the phases on their cards and in the table of the actions, the views of the results, the history, the
/// curves of the section, the detached view, the report, a read-only revision, the background calculation with a held engine) gives the
/// trace of the historic view frozen in tests/fixtures/f5-sezione-mista/storica, line by line;</item>
/// <item>for each F3 fixture of the module the data after opening, visit, calculation, export and reopening, the result, the status and
/// the text and structure of the report are byte-identical to the F3 reference of the historic view, with the F4 transitions of the two
/// old fixtures admitted exactly as ui/f3-equivalence admits them;</item>
/// <item>module and keys of interface state; the background calculation with barriers (thread, busy until the engine returns, only the
/// current revision published, closing during a calculation); read-only from the context; Reload; the exports.</item>
/// </list>
/// One "UI" thread (SingleThreadContext), as on the dispatcher. The adapter of the traces drives the ViewModel as the view would, and reads
/// it as the user sees it; where the historic view showed something only because of WPF (the protection of a read-only revision reaches a
/// control only once it is in the tree), the adapter models it and says so (<see cref="Side"/>).
/// </summary>
internal static class CompositeSectionSheetChecks
{
    internal static int Run(string root, string output, bool traces = true) => SingleThreadContext.Run(_ => RunAsync(root, output, traces));

    private static readonly CompositeSectionSheetModule Module = new(new PlanFigures());

    private static CompositeSectionSheetViewModel Open(JsonObject data, IModuleService? service = null, bool readOnly = false, PresentationServices? services = null) =>
        (CompositeSectionSheetViewModel)Module.CreateSheet(new SheetContext((JsonObject)data.DeepClone(), readOnly, new Host(), services ?? Scripted.Services(),
            service ?? ModuleServices.Get(CompositeSectionScenarios.Module), new CalculationLimiter(CalculationLimiter.DefaultMaximum)));

    private static async Task<int> RunAsync(string root, string output, bool traces)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string folder = Path.Combine(output, "sezione-mista");
            Directory.CreateDirectory(folder);
            if (traces)
            {
                foreach (var scenario in CompositeSectionScenarios.All(root))
                {
                    var lines = await TraceAsync(root, scenario);
                    CompositeSectionTrace.Write(Path.Combine(folder, scenario.Name + ".txt"), lines);
                    string stored = Path.Combine(CompositeSectionScenarios.Reference(root), scenario.Name + ".txt");
                    expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                    string? difference = CompositeSectionTrace.FirstDifference(CompositeSectionTrace.Read(stored), lines);
                    expect.That(difference is null, "Sezione mista MVVM diversa dalla vista storica, scenario " + scenario.Name + ", " + difference);
                }
                var fixtures = CompositeSectionScenarios.Fixtures(root);
                expect.That(fixtures.Count == 9, "Fixture F3 della sezione mista: " + fixtures.Count);
                var transitions = Transition.Load(Path.Combine(root, "tests", "fixtures", "f3", "transitions-f4.json"))
                    .Where(t => t.File.StartsWith("sheets/" + CompositeSectionScenarios.Module + "/", StringComparison.Ordinal)).ToList();
                foreach (string id in fixtures) await F3Async(root, folder, id, transitions.Select(t => t.File).ToHashSet(StringComparer.Ordinal), expect);
                CompareTransitions(root, folder, transitions, expect);
            }
            Contract(expect);
            await FormsAsync(root, expect);
            await StateKeysAsync(root, expect);
            await BackgroundAsync(root, expect);
            await ReadOnlyAsync(root, expect);
            await ReloadAsync(root, expect);
            await ExportsAsync(root, folder, expect);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, CompositeSectionScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(CompositeSectionScenarios.Module);
        var side = new Side(CompositeSectionScenarios.FixtureData(root, scenario.Fixture), engine);
        try
        {
            var previous = side.State();
            lines.AddRange(CompositeSectionTrace.Lines("apertura", null, previous));
            foreach (var step in scenario.Steps)
            {
                await side.ApplyAsync(step);
                var state = side.State();
                lines.AddRange(CompositeSectionTrace.Lines(step.ToString(), previous, state));
                previous = state;
            }
        }
        // A step the ViewModel cannot apply ends the trace: the comparison then shows the first different line before it.
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException or IndexOutOfRangeException)
        { lines.Add("errore " + error.GetType().Name + ": " + error.Message + " " + error.StackTrace?.Split('\n').FirstOrDefault()?.Trim()); }
        finally { engine.Release(); side.Dispose(); }
        return lines;
    }

    /// <summary>
    /// The ViewModel driven as its view would drive it, step by step: a control the user cannot use (not on the page shown, hidden, disabled,
    /// or protected by the read-only revision) does nothing; then read as the user sees it.
    /// <para>The historic view protected a read-only revision with RevisionInspection: at once the controls in its logical tree (the page
    /// shown, with its collapsed groups), then, at every layout, the controls materialized in the visual tree (shown at least once: the
    /// groups expanded, the tab selected). A control not yet reached looked editable in the trace but could not be used before being shown,
    /// and so protected. The ViewModel refuses every edit at once (<see cref="SheetViewModelBase.IsReadOnly"/>); this adapter models only what
    /// the trace of the historic view shows: which forms and controls the protection had reached (<see cref="protectedParts"/>).</para>
    /// </summary>
    private sealed class Side : IDisposable
    {
        private readonly HeldModuleService engine;
        private int modified;
        private string message = "";
        private bool readOnly;
        // The parts of the view reached by the protection of the revision (forms, cards, controls), and the groups of the inputs expanded.
        private readonly HashSet<object> protectedParts = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<string> expanded = new(StringComparer.Ordinal);

        internal Side(JsonObject data, HeldModuleService engine) { this.engine = engine; Attach(data); }

        internal CompositeSectionSheetViewModel Sheet { get; private set; } = null!;

        private void Attach(JsonObject data)
        {
            Sheet = Open(data, engine);
            modified = 0;
            Sheet.Modified += (_, _) => modified++;
            readOnly = false; protectedParts.Clear();
            // The groups of the inputs as the historic view builds them: only «Geometria» expanded on the control panel.
            expanded.Clear(); expanded.Add("geometria");
        }

        public void Dispose() => Sheet.Dispose();

        // ---------------------------------------------------------------- the parts of the view

        private const string Toolbar = "barra", Detail = "prospetto", Table = "tabella", Latest = "ultima", PhaseCommands = "comandi-fasi", CurveControls = "curve";

        /// <summary>The group of a form of the control panel and what else makes it visible (null: always on its page).</summary>
        private static readonly Dictionary<string, (int Page, string? Group)> FormPlaces = new(StringComparer.Ordinal)
        {
            ["normativa"] = (0, null), ["coefficienti"] = (0, "coefficienti"), ["materiali"] = (0, "materiali"), ["soletta"] = (0, "geometria"),
            ["predalle"] = (0, "geometria"), ["carpenteria"] = (0, "geometria"), ["sezione"] = (0, "geometria"), ["piastra"] = (0, "geometria"),
            ["armatura-sup"] = (0, "armature"), ["armatura-inf"] = (0, "armature"), ["trasversale"] = (0, "pioli"), ["fatica"] = (0, "pioli"),
            ["pioli"] = (0, "pioli"), ["irrigidimenti"] = (0, "irrigidimenti"), ["appoggio"] = (0, "appoggio"), ["cassoncino"] = (0, "cassoncino"),
            ["metodo"] = (1, null), ["discretizzazione"] = (1, "discretizzazione"), ["limiti"] = (1, null)
        };

        /// <summary>The containers of a form that the data hide (the plate of the H, the groups shown with the studs, the box, the mesh).</summary>
        private bool ContainerShown(string name) => name switch
        {
            "piastra" => Sheet.PlateShown, "trasversale" => Sheet.TransverseShown, "fatica" => Sheet.FatigueShown, "cassoncino" => Sheet.BoxShown,
            "discretizzazione" => Sheet.MeshShown, _ => true
        };

        private bool MainShown => Sheet.Page != 2;
        private bool InputPageShown(int page) => MainShown && Sheet.InputPage == page;

        /// <summary>A form of the inputs can be reached by the user (its page and its containers shown; a collapsed group does not hide it from the trace).</summary>
        private bool FormReachable(string name) => FormPlaces.TryGetValue(name, out var place) && InputPageShown(place.Page) && ContainerShown(name);

        /// <summary>A form of the inputs is shown: its page, its containers and its groups.</summary>
        private bool FormDisplayed(string name)
        {
            if (!FormReachable(name)) return false;
            var place = FormPlaces[name];
            bool group = place.Group is null || expanded.Contains(place.Group);
            return name is "trasversale" or "fatica" ? group && expanded.Contains(name) : group;
        }

        private bool ToolbarReachable => Sheet.IsDetached || MainShown;

        /// <summary>The parts of the view in its logical tree: what the protection reaches when the revision becomes read-only.</summary>
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

        /// <summary>The parts of the view shown now (materialized in the visual tree): what the protection reaches at every layout.</summary>
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

        /// <summary>After every step: the layout of the historic view, where the protection reached what is shown.</summary>
        private void Layout()
        {
            if (readOnly) foreach (var part in DisplayedParts()) protectedParts.Add(part);
        }

        // ---------------------------------------------------------------- steps

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

        /// <summary>The form can be reached by the user and is not protected.</summary>
        private bool FormUsable(string name, int phase, FormViewModel form)
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

        internal async Task ApplyAsync(CompositeSectionStep step)
        {
            message = "";
            var s = Sheet;
            switch (step.Kind)
            {
                case CompositeSectionStep.Commit: s.Commit(); break;
                case CompositeSectionStep.Calculate: s.Commit(); await Expect.Within(s.CalculateAsync()); break;
                case CompositeSectionStep.Start: s.Commit(); _ = s.CalculateAsync(); break;
                case CompositeSectionStep.Wait: await Expect.Within(s.PendingWork); break;
                case CompositeSectionStep.ReadOnly:
                    s.IsReadOnly = true; readOnly = true;
                    foreach (var part in LogicalParts()) protectedParts.Add(part);
                    break;
                case CompositeSectionStep.Reopen:
                    var data = s.Data; s.Dispose(); Attach(data); break;
                case CompositeSectionStep.Hold: engine.Hold(); break;
                case CompositeSectionStep.Entered: await engine.EnteredAsync(); break;
                case CompositeSectionStep.Release: engine.Release(); break;
                case CompositeSectionStep.Visit: Visit(); break;
                case CompositeSectionStep.Report:
                    try
                    {
                        var options = step.Text is null ? ModuleReportService.DefaultSections(CompositeSectionScenarios.Module) : step.Text.Split(',').ToHashSet();
                        s.Report.BuildReport("Traccia F5.11", options);
                        message = "report disponibile";
                    }
                    catch (Exception error) when (error is InvalidOperationException or ArgumentException) { message = "report non disponibile: " + error.Message; }
                    break;
                case CompositeSectionStep.CurveCalculate:
                    if (!MainShown && !s.CurveBusy && !Protected(CurveControls)) await Expect.Within(s.CalculateResponseAsync());
                    break;
                case CompositeSectionStep.Page: s.Page = step.Index; break;
                case CompositeSectionStep.Field:
                {
                    var form = FormFor(step.Form!, step.Index);
                    if (form is not null && form.Contains(step.Key!) && form[step.Key!] is { Kind: FieldKind.Text } field && field.IsVisible && field.IsEnabled
                        && FormUsable(step.Form!, step.Index, form)) form.Set(step.Key!, step.Text!);
                    break;
                }
                case CompositeSectionStep.Choice:
                {
                    var form = FormFor(step.Form!, step.Index);
                    if (form is not null && form.Contains(step.Key!) && form[step.Key!] is { Kind: FieldKind.Choice } field && field.IsVisible && field.IsEnabled
                        && FormUsable(step.Form!, step.Index, form)) field.SelectedChoice = step.Text;
                    break;
                }
                case CompositeSectionStep.Option:
                {
                    var form = FormFor(step.Form!, step.Index);
                    if (form is not null && form.Contains(step.Key!) && form[step.Key!] is { Kind: FieldKind.Bool } field && field.IsVisible && field.IsEnabled
                        && FormUsable(step.Form!, step.Index, form)) field.IsChecked = !field.IsChecked;
                    break;
                }
                case CompositeSectionStep.AddPhase: if (InputPageShown(1) && !Protected(PhaseCommands)) s.AddLoadCommand.Execute(null); break;
                case CompositeSectionStep.AddShrinkage: if (InputPageShown(1) && !Protected(PhaseCommands)) s.AddShrinkageCommand.Execute(null); break;
                case CompositeSectionStep.MovePhase:
                case CompositeSectionStep.DeletePhase:
                {
                    if (step.Index < 0 || step.Index >= s.Phases.Count || !InputPageShown(1)) break;
                    var card = s.Phases[step.Index];
                    if (Protected(card)) break;
                    var command = step.Kind == CompositeSectionStep.DeletePhase ? card.DeleteCommand : step.Row < 0 ? card.MoveUpCommand : card.MoveDownCommand;
                    if (command.CanExecute(null)) command.Execute(null);
                    break;
                }
                case CompositeSectionStep.Cell:
                {
                    if (!TableUsable || Protected(Table) || step.Row < 0 || step.Row >= s.ActionRows.Count) break;
                    if (step.Key == "T" && !s.TorqueColumnShown || s.InactiveKinds(step.Key!).Contains(s.ActionRows[step.Row].Values.S("tipo"))) break;
                    s.EditCell(step.Row, step.Key!, step.Text);
                    break;
                }
                case CompositeSectionStep.SelectRow: if (TableUsable && step.Row < s.ActionRows.Count) s.SelectedActionIndex = step.Row; break;
                case CompositeSectionStep.TableLoad: TableCommand(s.AddLoadCommand); break;
                case CompositeSectionStep.TableShrinkage: TableCommand(s.AddShrinkageCommand); break;
                case CompositeSectionStep.TableUp: TableCommand(s.MoveSelectedUpCommand); break;
                case CompositeSectionStep.TableDown: TableCommand(s.MoveSelectedDownCommand); break;
                case CompositeSectionStep.TableDelete: TableCommand(s.DeleteSelectedCommand); break;
                case CompositeSectionStep.Situation:
                    // With the view detached the situation is chosen in the window of the view.
                    if ((s.IsDetached || MainShown && s.StageControlsShown) && step.Index < s.Situations.Count) s.SelectedSituation = step.Index;
                    break;
                case CompositeSectionStep.Latest: if (MainShown && s.StageControlsShown && !Protected(Latest)) s.LatestSituationCommand.Execute(null); break;
                case CompositeSectionStep.Display: if (s.DisplayShown && ToolbarReachable) s.DisplayIndex = step.Index; break;
                case CompositeSectionStep.Results: if (MainShown && s.AnalysisPanelsShown) s.ResultsTab = step.Index; break;
                case CompositeSectionStep.Switch: Switch(step.Key!); break;
                case CompositeSectionStep.Scale: if (s.ScaleShown && ToolbarReachable && !Protected(Toolbar)) s.CommitScale(step.Text!); break;
                case CompositeSectionStep.DetailSection: if (MainShown && s.DetailShown && !Protected(Detail)) s.DetailSection = step.Index; break;
                case CompositeSectionStep.PropertyTab: if (MainShown && !s.AnalysisPanelsShown && step.Index < s.PropertyTabs.Count) s.SelectedPropertyTab = step.Index; break;
                case CompositeSectionStep.EffectiveDetails: if (MainShown && s.AnalysisPanelsShown) s.EffectiveDetailsCommand.Execute(null); break;
                case CompositeSectionStep.CurvePhase:
                    if (!MainShown && s.CurvePhaseShown && !Protected(CurveControls) && step.Index < s.CurvePhases.Count) s.CurvePhaseIndex = step.Index;
                    break;
                case CompositeSectionStep.CurveFiber: if (!MainShown && !Protected(CurveControls) && step.Index < s.FiberChoices.Count) s.FiberIndex = step.Index; break;
                case CompositeSectionStep.CurvePoint: if (!MainShown && !Protected(CurveControls)) s.PointValue = step.Index; break;
                case CompositeSectionStep.Detach: if (ToolbarReachable) s.DetachCommand.Execute(null); break;
                case CompositeSectionStep.Reattach: s.Reattach(); break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
            Layout();
        }

        /// <summary>The table of the actions can be reached: on the page of the phases, in the panel of the results.</summary>
        private bool TableUsable => InputPageShown(1) && Sheet.AnalysisPanelsShown;

        /// <summary>A button under the table (of the tab «Sollecitazioni», reached also when another tab is chosen).</summary>
        private void TableCommand(CommunityToolkit.Mvvm.Input.IRelayCommand command)
        {
            if (MainShown && Sheet.AnalysisPanelsShown && !Protected(Table) && command.CanExecute(null)) command.Execute(null);
        }

        private void Switch(string key)
        {
            var s = Sheet;
            bool toolbar = ToolbarReachable && !Protected(Toolbar);
            switch (key)
            {
                case "quote": if (toolbar && s.LabelsShown) s.GeometryLabels = !s.GeometryLabels; break;
                case "info-armature": if (toolbar && s.LabelsShown) s.RebarLabels = !s.RebarLabels; break;
                case "asse-neutro": if (ToolbarReachable && s.NeutralAxisShown) s.NeutralAxis = !s.NeutralAxis; break;
                case "scala-auto": if (toolbar && s.ScaleShown) s.ScaleAuto = !s.ScaleAuto; break;
                case "contorno-sezione": if (toolbar) s.ContourSection = !s.ContourSection; break;
                case "contorno-tensioni": if (toolbar) s.ContourDiagram = !s.ContourDiagram; break;
                case "limiti-tensioni": if (toolbar) s.StressLimits = !s.StressLimits; break;
                case "prospetto": if (MainShown && s.DetailShown) s.DetailExpanded = !s.DetailExpanded; break;
                default: throw new ArgumentException("Interruttore sconosciuto: " + key);
            }
        }

        /// <summary>
        /// The visit of the F3 capture (F3EquivalenceCapture.Visit) on the view of the sheet: the expanders in the tree opened (the local sketch
        /// and the groups of the page of inputs shown), then every page of every tab control in the tree chosen and the first one again, in
        /// the order of the tree (pages, properties of the section on the control panel, results); the tab controls that the pages show
        /// meanwhile are not in the tree when the visit looks for them.
        /// </summary>
        private void Visit()
        {
            var s = Sheet;
            if (MainShown)
            {
                s.DetailExpanded = true;
                foreach (var (name, place) in FormPlaces) if (place.Page == s.InputPage && place.Group is not null) expanded.Add(place.Group);
                if (s.InputPage == 0) { expanded.Add("trasversale"); expanded.Add("fatica"); }
            }
            bool properties = MainShown && !s.AnalysisPanelsShown, results = MainShown;
            int selected = s.Page;
            for (int i = 0; i < 3; i++) s.Page = i;
            s.Page = selected;
            if (properties)
            {
                selected = s.SelectedPropertyTab;
                for (int i = 0; i < s.PropertyTabs.Count; i++) s.SelectedPropertyTab = i;
                s.SelectedPropertyTab = selected;
            }
            if (results)
            {
                selected = s.ResultsTab;
                for (int i = 0; i < 5; i++) s.ResultsTab = i;
                s.ResultsTab = selected;
            }
        }

        // ---------------------------------------------------------------- state

        internal CompositeSectionTraceState State()
        {
            var s = Sheet;
            var t = new CompositeSectionTraceState();
            static string Q(string? text) => CompositeSectionTrace.Quote(text);
            static string N(int value) => CompositeSectionTrace.Number(value);
            static string D(double value) => CompositeSectionTrace.Number(value);
            static string YN(bool value) => CompositeSectionTrace.YesNo(value);
            static string Check(bool value, bool visible, bool enabled) => YN(value) + (visible ? "" : "(nascosto)") + (enabled ? "" : "(spento)");
            bool toolbar = !Protected(Toolbar);
            t.Data(s.Data);
            t.Add("modificato", N(modified));
            t.Add("messaggio", message);
            t.Add("calcolo", "occupato " + YN(s.IsBusy) + " · risultato " + (s.Result is { } result ? CompositeSectionTrace.Hash(result) : "-")
                + " · mostrato " + (s.DisplayedCalculation is null ? "-" : ReferenceEquals(s.DisplayedCalculation, s.Calculation) ? "corrente" : "da aggiornare")
                + " · esportabile " + YN(s.Calculation is not null && !s.IsBusy));
            t.Add("stato", Q(s.Status));
            t.Add("avviso", s.ResultNotice != "" ? Q(s.ResultNotice) : "-");
            t.Add("avvisi", Q(s.WarningText));
            t.Add("panoramica", Q(s.Overview));
            t.Add("riepilogo", Q(s.SummaryCards.Text));
            t.Add("materiali", Q(s.MaterialText));
            t.Add("altezza", Q(s.SteelHeightText));
            t.Add("soletta", Q(s.SlabLayoutText));
            t.Add("metodo", Q(s.MethodNotice));
            t.Add("convenzione", Q(s.LoadConvention));
            t.Add("fasi", Q(s.PhaseCountText));
            t.Add("convergenza", Q(s.ConvergenceText));
            t.Add("pagina", N(s.Page) + " · titolo " + Q(s.InputTitle) + " · " + Q(s.InputSubtitle) + " · pannello " + (s.AnalysisPanelsShown ? "riepilogo" : "proprietà")
                + " · tabelle " + YN(s.AnalysisPanelsShown) + " · staccata " + YN(s.IsDetached));
            t.Add("situazioni", Q(string.Join("|", s.Situations)) + " · scelta " + N(s.SelectedSituation) + " · visibile " + YN(s.StageControlsShown) + " · attiva sì");
            t.Add("vista", N(s.DisplayIndex) + " · visibile " + YN(s.DisplayShown) + " · risultati " + N(s.ResultsTab));
            t.Add("interruttori", "quote " + Check(s.GeometryLabels, s.LabelsShown, toolbar) + " · armature " + Check(s.RebarLabels, s.LabelsShown, toolbar)
                + " · asse neutro " + Check(s.NeutralAxis, s.NeutralAxisShown, true) + " " + Q(s.NeutralAxisToolTip) + " · contorno sezione " + YN(s.ContourSection)
                + " · contorno tensioni " + YN(s.ContourDiagram) + " · limiti " + YN(s.StressLimits) + " · menu " + YN(toolbar));
            t.Add("scala", Q(s.ScaleText) + " · auto " + Check(s.ScaleAuto, true, toolbar) + " · visibile " + YN(s.ScaleShown) + " · errore " + YN(s.ScaleError)
                + " · " + Q(s.ScaleToolTip) + " · modificabile " + YN(toolbar));
            t.Add("prospetto", "visibile " + YN(s.DetailShown) + " · aperto " + YN(s.DetailExpanded) + " · sezione " + N(s.DetailSection) + " " + YN(!Protected(Detail))
                + " · schizzo appoggio " + YN(s.DetailAtSupport));
            t.Add("contenitori", "piastra " + YN(s.PlateShown) + " · nota piastra " + YN(s.PlateShown) + " · trasversale " + YN(s.TransverseShown)
                + " · fatica " + YN(s.FatigueShown) + " · cassoncino " + YN(s.BoxShown) + " · discretizzazione " + YN(s.MeshShown)
                + " · torsione risultati " + YN(s.TorsionResultsShown));
            foreach (var (name, _) in CompositeSectionScenarios.Forms) t.Form(name, Fields(s.Forms[name]));
            var phases = s.Data.Array("fasi");
            t.Add("schede", N(s.Phases.Count));
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
            bool table = Protected(Table);
            t.Add("azioni", "righe " + N(s.ActionRows.Count) + " · selezione " + N(s.SelectedActionIndex) + " · ΔT " + YN(s.TorqueColumnShown) + " · bloccata " + YN(table)
                + " · stessi oggetti " + YN(s.ActionRows.Select(r => r.Values).SequenceEqual(phases.OfType<JsonObject>())));
            for (int i = 0; i < s.ActionRows.Count; i++)
            {
                string kind = s.ActionRows[i].Values.S("tipo");
                t.Add("azioni." + i, "spente " + string.Join(",", CompositeSectionFields.Actions.Where(c => s.InactiveKinds(c.Key).Contains(kind)).Select(c => c.Key)));
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
            t.Add("proprieta", Q(string.Join("|", s.PropertyTabs.Select(p => p.Header))) + " · scelta " + N(s.SelectedPropertyTab) + " · nota " + Q(s.PropertyNotice));
            t.Panel("proprieta.acciaio", s.SteelPropertyTables.SelectMany(PropertyBlocks).ToList());
            t.Form(CompositeSectionScenarios.SlabPropertiesForm, Fields(s.SlabPropertiesForm));
            t.Panel("proprieta.soletta", s.SlabPropertyError is { } slabError ? [new(CompositeSectionBlock.TextKind, slabError)] : s.SlabPropertyTables.SelectMany(PropertyBlocks).ToList());
            for (int i = 2; i < s.PropertyTabs.Count; i++) t.Panel("proprieta.fase." + (i - 2), PhaseBlocks(s.PropertyTabs[i]));
            var shown = s.DisplayedCalculation;
            var d = s.Drawing;
            t.Add("disegno", "modo " + N(d.Mode) + " · da aggiornare " + YN(d.Stale) + " · quote " + YN(d.GeometryLabels) + " · armature " + YN(d.RebarLabels)
                + " · asse neutro " + YN(d.NeutralAxis) + " · contorni " + YN(d.ContourSection) + YN(d.ContourDiagram) + " · limiti " + YN(d.StressLimits)
                + " · CLS × " + D(d.ConcreteAmplification) + " · appoggio " + YN(d.DetailAtSupport));
            t.Add("disegno.geometria", (d.Geometry is null ? "-" : ReferenceEquals(d.Geometry, shown?.Geometry) ? "risultato" : "anteprima") + " " + CompositeSectionTrace.HashObject(d.Geometry));
            t.Add("disegno.ingresso", d.Input is null ? "-" : (ReferenceEquals(d.Input, shown?.Input) ? "risultato " : "anteprima ") + CompositeSectionTrace.Hash(d.Input));
            t.Add("disegno.fase", d.Stage is null ? "-" : N(shown?.Stages.IndexOf(d.Stage) ?? -1) + " " + Q(d.Stage.Name));
            t.Add("disegno.carichi", string.Join(" | ", d.LoadPoints.Select(p => N(p.Index) + " " + Q(p.Name) + " " + D(p.Y) + " " + D(p.Force))));
            bool curves = !Protected(CurveControls);
            t.Form(CompositeSectionScenarios.CurveForm, Fields(s.CurveForm));
            t.Add("curva", "fasi " + Q(string.Join("|", s.CurvePhases)) + " · scelta " + N(s.CurvePhaseIndex) + " · visibile " + YN(s.CurvePhaseShown)
                + " · calcola " + YN(!s.CurveBusy && curves) + " · esporta " + YN(s.CurveExportEnabled)
                + " · curva " + (s.ResponseCalculation is null ? "-" : N(s.ResponseCalculation.Points.Count) + " punti"));
            t.Add("curva.avviso", Q(s.CurveNotice));
            t.Add("curva.punto", Q(s.PointInfo) + " · cursore " + D(s.PointValue) + "/" + D(s.PointMaximum));
            t.Add("curva.fibre", Q(string.Join("|", s.FiberChoices.Select(f => f.ToString()))) + " · scelta " + N(s.FiberIndex));
            t.Add("curva.grafico", Chart(s.SectionChart));
            t.Add("curva.fibra", Chart(s.FiberChart));
            t.Panel("curva.tabella", Blocks(s.CurveTable));
            return t;

            static string Chart(CompositeSectionChart chart) => Q(chart.XLabel) + " " + Q(chart.YLabel) + " · punti " + N(chart.Points.Count) + " "
                + CompositeSectionTrace.HashObject(chart.Points.Select(p => new[] { p.X, p.Y })) + " · scelto " + N(chart.Selected);
        }

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

    // ---------------------------------------------------------------- F3 reference

    /// <summary>
    /// The path of ui/f3-equivalence through the ViewModel (opened, visited, calculated, result, report, exported, reopened), compared with
    /// the F3 reference of the historic view in the canonical form of the capture: byte for byte, except the files with F4 transitions
    /// (two old fixtures, and result and report of storico-non-lineare-curve since AG-SM-01), kept for <see cref="CompareTransitions"/>.
    /// </summary>
    private static async Task F3Async(string root, string output, string id, IReadOnlySet<string> withTransitions, Expect expect)
    {
        string folder = Path.Combine(root, "tests", "fixtures", "f3", "expected", "sheets", CompositeSectionScenarios.Module, id);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        var guids = new GuidMap();
        var data = CompositeSectionScenarios.FixtureData(root, id);
        CanonicalJson.Register(data, guids);
        string Canonical(JsonNode? node) { CanonicalJson.Register(node, guids); return CanonicalJson.Serialize(node, guids, normalizer).Replace("\r\n", "\n"); }
        string Expected(string name) => File.ReadAllText(Path.Combine(folder, name), Encoding.UTF8).Replace("\r\n", "\n");
        void Same(string name, string actual)
        {
            string file = "sheets/" + CompositeSectionScenarios.Module + "/" + id + "/" + name;
            if (withTransitions.Contains(file))
            {
                foreach (var (side, text) in new[] { ("f3-riferimento", Expected(name)), ("f3-transizioni", actual) })
                {
                    string path = Path.Combine(output, side, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, text, new UTF8Encoding(false));
                }
                return;
            }
            // The text of a difference is kept beside the traces of the run, to read it against the reference.
            if (actual != Expected(name)) File.WriteAllText(Path.Combine(output, "f3-" + id + "-" + name), actual, new UTF8Encoding(false));
            expect.That(actual == Expected(name), id + ": " + name + " diverso dal riferimento F3 della vista storica");
        }
        var engine = new HeldModuleService(CompositeSectionScenarios.Module);
        var side = new Side(data, engine);
        try
        {
            var sheet = side.Sheet;
            sheet.Commit(); Same("opened.json", Canonical(sheet.Data));
            await side.ApplyAsync(new(CompositeSectionStep.Visit));
            sheet.Commit(); Same("visited.json", Canonical(sheet.Data));
            sheet.Commit(); await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
            Same("calculated-data.json", Canonical(sheet.Data)); Same("result.json", Canonical(sheet.Result));
            var status = new JsonObject { ["hasResults"] = sheet.HasResults };
            try
            {
                byte[] report = sheet.Report.BuildReport("Fixture F3 · " + CompositeSectionScenarios.Module + " · " + id, ModuleReportService.DefaultSections(CompositeSectionScenarios.Module));
                Same("report.txt", DocxText.Extract(report, normalizer, guids).Replace("\r\n", "\n"));
                Same("report-structure.json", Canonical(Structure(report)));
                status["report"] = "available";
            }
            catch (Exception error) when (error is not IOException && error is not UnauthorizedAccessException)
            { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
            Same("exported-data.json", Canonical(sheet.Data)); Same("status.json", Canonical(status));
            using var reopened = Open(sheet.Data);
            reopened.Commit(); Same("reopened.json", Canonical(reopened.Data));
        }
        finally { engine.Release(); side.Dispose(); }
    }

    /// <summary>The files with F4 transitions compared as ui/f3-equivalence compares them, with exactly those transitions admitted.</summary>
    private static void CompareTransitions(string root, string output, IReadOnlyList<Transition> transitions, Expect expect)
    {
        var comparer = new BaselineComparer(Tolerances.Load(Path.Combine(root, "tests", "ANTHEA.Testing", "tolerances.json")));
        comparer.Admit(transitions);
        var report = comparer.Compare(Path.Combine(output, "f3-riferimento"), Path.Combine(output, "f3-transizioni"));
        File.WriteAllText(Path.Combine(output, "f3-confronto.json"), report.ToJsonString(J.Options));
        // AG-SM-01 (24/10/2026, accepted by the user): the analytic concrete law of Model 5.21.0.0 in the nonlinear fibre history of
        // CompositeBridge 1.13.0.1 changes result and report of storico-non-lineare-curve: 2680 new transitions; the other 12 are BUG-PON-2.
        int analytic = transitions.Count(t => t.Entry == "AG-SM-01");
        expect.That(analytic == 2680 && transitions.Count - analytic == 12 && comparer.NotAdmitted == 0, "File con transizioni F4: " + transitions.Count + " transizioni, "
            + comparer.NotAdmitted + " differenze non ammesse o transizioni assenti");
    }

    /// <summary>Tables, rows, cells, images and styles of the report, as F3EquivalenceCapture.CaptureReport counts them.</summary>
    private static JsonObject Structure(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var document = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        return new JsonObject
        {
            ["tables"] = document.Descendants(w + "tbl").Count(),
            ["rows"] = document.Descendants(w + "tr").Count(),
            ["cells"] = document.Descendants(w + "tc").Count(),
            ["images"] = new JsonArray(document.Descendants(wp + "extent").Select(e => (JsonNode)new JsonObject {
                ["cx"] = (string?)e.Attribute("cx"), ["cy"] = (string?)e.Attribute("cy") }).ToArray()),
            ["styles"] = new JsonArray(document.Descendants(w + "pStyle").Select(e => JsonValue.Create((string?)e.Attribute(w + "val"))).ToArray())
        };
    }

    // ---------------------------------------------------------------- contract

    private static void Contract(Expect expect)
    {
        expect.That(Module.Info == ModuleDirectory.Get(CompositeSectionScenarios.Module) && Module.Reports == new ModuleReportCapabilities(false)
            && Module.Help.GuideId == ModuleHelp.For(CompositeSectionScenarios.Module).GuideId, "Modulo della sezione mista");
        expect.That(Module.Persistence.UiStateKeys.SequenceEqual(["ui_mista", "curve_sezione"]) && ReferenceEquals(CompositeSectionSheetViewModel.UiStateKeys, Module.Persistence.UiStateKeys),
            "Chiavi di stato dell'interfaccia della sezione mista");
        var registry = new ModuleRegistry([Module]);
        expect.That(registry.SheetModuleIds.SequenceEqual([CompositeSectionScenarios.Module]) && ReferenceEquals(registry.SheetModule(CompositeSectionScenarios.Module), Module),
            "Registro con la sezione mista");
        expect.Throws<ArgumentNullException>(() => new CompositeSectionSheetModule(null!), "Modulo senza disegni del report");
        expect.That(CompositeSectionSheetViewModel.RecalculationDelay == TimeSpan.FromMilliseconds(500), "Ritardo del ricalcolo automatico");
    }

    /// <summary>
    /// Every form of the sheet (definition, accessories, options, cards and homogenizations of the phases, slab properties, curves) opens
    /// the Wiki through the navigator of the services, with the module of the sheet; the cards rebuilt on the page of the phases too.
    /// </summary>
    private static async Task FormsAsync(string root, Expect expect)
    {
        var wiki = new RecordingWiki();
        using var sheet = Open(CompositeSectionScenarios.FixtureData(root, "anima-inclinata-ec4-sle"),
            services: new PresentationServices(new ScriptedMessages(), new ScriptedConfirmations(), new ScriptedFileDialogs(), new ScriptedDialogs(),
                new RecordingClipboard(), new RecordingLinks(), wiki));
        await Expect.Within(sheet.PendingWork);
        IEnumerable<FormViewModel> All() => sheet.Forms.Values.Concat(sheet.Phases.SelectMany(c => c.Homogenization is null ? new[] { c.Form } : [c.Form, c.Homogenization]))
            .Append(sheet.SlabPropertiesForm!).Append(sheet.CurveForm!);
        bool Wired() => All().All(f => ReferenceEquals(f.WikiNavigator, wiki) && f.InheritedWikiModule == CompositeSectionScenarios.Module && f.CommitOnFocusLoss && !f.IsReadOnly);
        expect.That(All().Count() == 19 + 5 + 3 + 2 && Wired(), "Form senza il navigatore della Wiki dei servizi o senza il modulo");
        sheet.Page = 0; sheet.Page = 1;
        expect.That(Wired(), "Schede delle fasi ricostruite senza il navigatore della Wiki");
        int opened = 0;
        foreach (var field in All().SelectMany(f => f.Fields).Where(f => f.HasHelp)) { if (field.OpenHelp()) opened++; }
        expect.That(opened > 0 && wiki.Shown.Count == opened, "«?» dei campi senza il navigatore dei servizi: " + opened + " / " + wiki.Shown.Count);
    }

    /// <summary>
    /// What the view state writes: displays, tabs, situations, switches and splitters go into <c>ui_mista</c> with Modified and keep the
    /// result; the path of the curves into <c>curve_sezione</c>; nothing else of the data changes.
    /// </summary>
    private static async Task StateKeysAsync(string root, Expect expect)
    {
        using var sheet = Open(CompositeSectionScenarios.FixtureData(root, "default"));
        await Expect.Within(sheet.PendingWork);
        sheet.Page = 1;
        var result = sheet.Calculation;
        string Others() { var copy = (JsonObject)sheet.Data.DeepClone(); foreach (string key in CompositeSectionSheetViewModel.UiStateKeys) copy.Remove(key); return copy.ToJsonString(); }
        string before = Others();
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        sheet.DisplayIndex = 1; sheet.ResultsTab = 3; sheet.SelectedSituation = 0; sheet.NeutralAxis = !sheet.NeutralAxis; sheet.ContourSection = true;
        sheet.ScaleAuto = false; sheet.CommitScale("3"); sheet.SetSplit("righe", .4); sheet.Page = 0; sheet.GeometryLabels = !sheet.GeometryLabels;
        sheet.SlabPropertiesForm!.Set("psi", "1,1"); sheet.Page = 2; sheet.CurveForm!.Set("punti", "12");
        expect.That(Others() == before && modified == 13 && ReferenceEquals(result, sheet.Calculation) && sheet.Calculation is not null,
            "Stato della vista: altri dati scritti, Modified mancante o risultato perso (" + modified + ")");
        expect.That(sheet.ViewSettings.D("split_righe") == .4 && sheet.ViewSettings.D("amplificazione_cls") == 3 && sheet.ResponseData.S("punti") == "12"
            && sheet.ViewSettings["soletta_proprieta"]!.S("psi") == "1,1", "Stato della vista salvato in ui_mista e curve_sezione");
    }

    // ---------------------------------------------------------------- background calculation

    /// <summary>
    /// The calculation on a background thread through the service of the context: busy while the engine runs, also for a request
    /// overtaken by an edit; only the current revision is published; closing during a calculation publishes nothing.
    /// </summary>
    private static async Task BackgroundAsync(string root, Expect expect)
    {
        var engine = new HeldModuleService(CompositeSectionScenarios.Module);
        var counting = new ThreadService(engine);
        var sheet = Open(CompositeSectionScenarios.FixtureData(root, "default"), counting);
        int ui = Environment.CurrentManagedThreadId;
        expect.That(sheet.CalculationMode == SheetCalculationMode.Background && sheet.AutomaticDelay == CompositeSectionSheetViewModel.RecalculationDelay,
            "Calcolo sullo sfondo con il ritardo di 500 ms");
        await Expect.Within(sheet.PendingWork);
        expect.That(sheet.Calculation is not null && counting.Threads.All(t => t != ui) && !sheet.IsBusy && sheet.Status == BridgeInputPresentation.StatusUpdated(3),
            "Calcolo all'apertura fuori dal thread dell'interfaccia");
        engine.Hold();
        var first = sheet.CalculateAsync();
        await engine.EnteredAsync();
        expect.That(sheet.IsBusy && sheet.Status == BridgeInputPresentation.StatusRunning && sheet.CalculationState == CalculationState.Running && !sheet.CanExport,
            "Occupato durante il calcolo");
        sheet.Page = 1;
        sheet.Phases[1].Form.Set("Mx", "2050");
        expect.That(sheet.IsBusy && sheet.Calculation is null && sheet.ResultsAreStale && sheet.Status == BridgeInputPresentation.StatusPending,
            "Calcolo superato: occupato finché il motore non torna, risultati da aggiornare");
        engine.Release();
        expect.That(!await Expect.Within(first), "Calcolo superato pubblicato");
        await Expect.Within(sheet.PendingWork);
        expect.That(sheet.Calculation is not null && sheet.Calculation.Input.Array("fasi")[1]!.S("Mx") == "2050" && !sheet.IsBusy && !sheet.ResultsAreStale,
            "Revisione corrente pubblicata");
        // F5.15, SM5 point 3: a situation chosen during a calculation is shown and saved when the engine returns, and the publication keeps it
        // (the historic view showed the previous situation again).
        string? SavedSituation() => sheet.Data["ui_mista"]?["fase"]?.ToJsonString();
        engine.Hold();
        var chosen = sheet.CalculateAsync();
        await engine.EnteredAsync();
        sheet.SelectedSituation = 0;
        expect.That(sheet.IsBusy && SavedSituation() != "0", "Situazione scelta durante il calcolo: vista non ancora salvata");
        engine.Release();
        expect.That(await Expect.Within(chosen) && sheet.SelectedSituation == 0 && SavedSituation() == "0" && !sheet.IsBusy,
            "Situazione scelta durante il calcolo: mostrata, salvata e conservata alla fine (F5.15, SM5)");
        engine.Hold();
        var last = sheet.CalculateAsync();
        await engine.EnteredAsync();
        sheet.Dispose();
        engine.Release();
        expect.That(!await Expect.Within(last) && sheet.IsDisposed, "Calcolo pubblicato dopo la chiusura");
        expect.Throws<ObjectDisposedException>(() => sheet.CalculateAsync(), "Calcola dopo la chiusura");
        // Closing during the delay of the automatic recalculation: nothing is calculated or published.
        var delayed = new HeldModuleService(CompositeSectionScenarios.Module);
        var closing = Open(CompositeSectionScenarios.FixtureData(root, "default"), delayed);
        await Expect.Within(closing.PendingWork);
        int calls = delayed.Calls;
        closing.Page = 1;
        closing.Phases[1].Form.Set("Mx", "2075");
        var waiting = closing.PendingCalculation;
        closing.Dispose();
        expect.That(!await Expect.Within(waiting) && delayed.Calls == calls && closing.Calculation is null, "Chiusura durante il ritardo: calcolo eseguito o pubblicato");
    }

    // ---------------------------------------------------------------- read-only

    /// <summary>
    /// A sheet opened read-only (a previous revision): every edit is refused (forms, cards, table, switches, scale, sketch, curves); pages,
    /// situations, displays, neutral axis, tabs, details, the detached view, calculation and report stay available.
    /// </summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        using var sheet = Open(CompositeSectionScenarios.FixtureData(root, "irrigidimenti-appoggio-piastra2"), readOnly: true);
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        await Expect.Within(sheet.PendingWork);
        expect.That(!sheet.CanEdit && sheet.Forms.Values.All(f => f.IsReadOnly) && sheet.Phases.All(c => c.Form.IsReadOnly && c.Homogenization?.IsReadOnly != false)
            && sheet.SlabPropertiesForm!.IsReadOnly && sheet.CurveForm!.IsReadOnly && sheet.ActionsReadOnly, "Sola lettura dal contesto");
        string before = sheet.Data.ToJsonString();
        sheet.Forms["soletta"]["b_cls"].EditText = "2000";
        sheet.Forms["sezione"]["sezione"].SelectedChoice = "Cassoncino";
        sheet.Forms["predalle"]["predalle"].IsChecked = true;
        sheet.EditCell(0, "Mx", "100"); sheet.AddPhase(false); sheet.AddPhase(true);
        foreach (var command in new[] { sheet.AddLoadCommand, sheet.AddShrinkageCommand, sheet.MoveSelectedUpCommand, sheet.MoveSelectedDownCommand, sheet.DeleteSelectedCommand,
            sheet.LatestSituationCommand, sheet.Phases[0].MoveUpCommand, sheet.Phases[1].DeleteCommand, sheet.CancelCurveCommand })
            expect.That(!command.CanExecute(null), "Sola lettura: comando eseguibile");
        expect.That(!sheet.CalculateCurveCommand.CanExecute(null), "Sola lettura: «Calcola curva» eseguibile");
        sheet.GeometryLabels = !sheet.GeometryLabels; sheet.RebarLabels = !sheet.RebarLabels; sheet.ContourSection = !sheet.ContourSection;
        sheet.ScaleAuto = !sheet.ScaleAuto; sheet.CommitScale("7"); sheet.DetailSection = 1 - sheet.DetailSection; sheet.ShowLatestSituation();
        await sheet.CalculateResponseAsync();
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && sheet.ResponseCalculation is null, "Sola lettura: modifica scritta nel documento");
        sheet.Page = 1; sheet.DisplayIndex = 1; sheet.SelectedSituation = 0; sheet.NeutralAxis = !sheet.NeutralAxis; sheet.ResultsTab = 4; sheet.ShowEffectiveDetails();
        sheet.SelectedActionIndex = 1; sheet.Detach(); sheet.Reattach();
        await Expect.Within(sheet.CalculateAsync());
        sheet.Report.BuildReport("Sola lettura", ModuleReportService.DefaultSections(CompositeSectionScenarios.Module));
        expect.That(sheet.Page == 1 && sheet.DisplayIndex == 1 && sheet.ResultsTab == 2 && sheet.SelectedActionIndex == 1 && sheet.Calculation is not null && modified > 0
            && sheet.ViewSettings["report_sezioni"] is JsonArray, "Sola lettura: viste, calcolo o report rifiutati");
        sheet.IsReadOnly = false;
        expect.That(sheet.Forms.Values.All(f => !f.IsReadOnly) && sheet.Phases.All(c => !c.Form.IsReadOnly) && sheet.AddLoadCommand.CanExecute(null), "Ritorno alla modifica");
        sheet.Forms["soletta"].Set("b_cls", "2100");
        expect.That(sheet.Data.S("b_cls") == "2100" && sheet.Calculation is null && sheet.Status == BridgeInputPresentation.StatusPending, "Modifica dopo la sola lettura");
    }

    // ---------------------------------------------------------------- reload

    /// <summary>Data replaced from outside: built again as at opening, without Modified and without a result; then as a reopening.</summary>
    private static async Task ReloadAsync(string root, Expect expect)
    {
        using var sheet = Open(CompositeSectionScenarios.FixtureData(root, "default"));
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        await Expect.Within(sheet.PendingWork);
        sheet.Page = 1;
        sheet.Phases[0].Form.Set("Mx", "1300");
        var other = CompositeSectionScenarios.FixtureData(root, "cassoncino-torsione-pioli");
        string source = other.ToJsonString();
        sheet.Reload(other);
        expect.That(modified == 2 && sheet.Calculation is null && sheet.DisplayedCalculation is null && sheet.Status == BridgeInputPresentation.StatusPreparing
            && sheet.BoxShown && other.ToJsonString() == source, "Reload: sezione, stato o origine modificata");
        await Expect.Within(sheet.PendingWork);
        expect.That(sheet.Calculation is not null && !sheet.IsBusy, "Reload: calcolo automatico dei nuovi dati");
        using var reopened = Open(other);
        await Expect.Within(reopened.PendingWork);
        expect.That(sheet.Data.ToJsonString() == reopened.Data.ToJsonString() && sheet.Calculation is not null && reopened.Calculation is not null
            && sheet.Result!.ToJsonString() == reopened.Result!.ToJsonString() && modified == 2 && sheet.Page == reopened.Page
            && sheet.Phases.Count == reopened.Phases.Count && sheet.ActionRows.Count == reopened.ActionRows.Count, "Reload: dati o risultato diversi da una riapertura");
    }

    // ---------------------------------------------------------------- exports

    /// <summary>«Esporta CSV…» of the stresses and «Esporta punti e fibre CSV…» of the curves, refused without a result, through the services of the context.</summary>
    private static async Task ExportsAsync(string root, string output, Expect expect)
    {
        string folder = Path.Combine(output, "sezione-mista-esportazioni");
        Directory.CreateDirectory(folder);
        string stresses = Path.Combine(folder, "tensioni.csv"), points = Path.Combine(folder, "curva.csv");
        var files = new ScriptedFileDialogs().Then(stresses).Then(points);
        using var sheet = Open(CompositeSectionScenarios.FixtureData(root, "storico-non-lineare-curve"), services: Scripted.Services(files: files));
        sheet.ExportCsv();
        expect.That(sheet.Status == BridgeInputPresentation.WaitBeforeExport && files.Requests.Count == 0, "CSV senza risultato non rifiutato");
        await Expect.Within(sheet.PendingWork);
        sheet.ExportCsv();
        byte[] bom = Encoding.UTF8.GetPreamble();
        expect.That(File.ReadAllBytes(stresses).SequenceEqual(bom.Concat(Encoding.UTF8.GetBytes(BridgeResultPresentation.Csv(sheet.Calculation!))))
            && ((SaveFileRequest)files.Requests[0]).FileName == BridgeResultPresentation.CsvFileName, "CSV delle tensioni");
        sheet.ExportResponse();
        expect.That(files.Requests.Count == 1, "CSV della curva senza curva");
        await sheet.CalculateResponseAsync();
        expect.That(sheet.ResponseCalculation is not null && sheet.CurveExportEnabled, "Curva calcolata");
        sheet.ExportResponse();
        expect.That(File.ReadAllBytes(points).SequenceEqual(bom.Concat(Encoding.UTF8.GetBytes(BridgeEditor.ResponseCsv(sheet.ResponseCalculation!))))
            && ((SaveFileRequest)files.Requests[1]).FileName == BridgeResponsePresentation.CsvFileName, "CSV della curva");
    }

    // ---------------------------------------------------------------- services of the checks

    /// <summary>Figures of the report as BridgeReportPresentation.Figures plans them: only the size enters the report (extent of the images).</summary>
    private sealed class PlanFigures : ICompositeSectionFigureRenderer
    {
        public IReadOnlyList<ReportFigure> Render(BridgeResultView result, JsonObject viewSettings, IReadOnlySet<string> options) =>
            BridgeReportPresentation.Figures(result, viewSettings, options).Select(f => new ReportFigure(f.Caption, Png(f.Width, f.Height), f.Category)).ToArray();

        /// <summary>The signature and the header of a PNG of the given size.</summary>
        private static byte[] Png(int width, int height)
        {
            var png = new byte[33];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(png, 0);
            BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), width);
            BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20, 4), height);
            png[24] = 8; png[25] = 6;
            return png;
        }
    }

    /// <summary>The module service with the threads of its calls.</summary>
    private sealed class ThreadService(IModuleService inner) : IModuleService
    {
        internal System.Collections.Concurrent.ConcurrentQueue<int> Threads { get; } = new();
        public string ModuleId => inner.ModuleId;
        public JsonObject Prepare(JsonObject source) => inner.Prepare(source);
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Threads.Enqueue(Environment.CurrentManagedThreadId);
            return inner.Calculate(source, cancellation);
        }
    }

    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException("Copia non prevista: " + module);
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() { }
    }
}
