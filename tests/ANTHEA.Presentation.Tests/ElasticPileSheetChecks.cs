using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;
using Anthea.Presentation.Services;
using Anthea.Reports;
using Anthea.Calculations;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The tab of the elastic response of the horizontal pile and micropile with the reinforcement (F5.8, <see cref="ElasticPileViewModel"/> and
/// <see cref="PileReinforcementViewModel"/> inside <see cref="HorizontalPileSheetViewModel"/>) against its historic view, without WPF:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/ElasticPileTrace.cs gives the trace of the historic view frozen in tests/fixtures/f5-elastico/storica,
/// line by line (the dialogs through their ViewModels, the c.a. verifier through the request to the host and the writes of its dialog);</item>
/// <item>the sheet seen by the window (ISheetViewModel): the result, «Calcola» and the report of the tab shown;</item>
/// <item>with the engine held by a barrier: only the current revision is published, closing during the calculation, the tab of the capacity
/// does not cancel the elastic response; read-only refusals; Reload; the request of the c.a. verifier to the host.</item>
/// </list>
/// The F3 fixtures on the elastic tab are compared with the F3 reference by HorizontalPileSheetChecks (result, status and report too, with the
/// F4 transitions applied as ui/f3-equivalence applies them). One "UI" thread (SingleThreadContext), as on the dispatcher.
/// </summary>
internal static class ElasticPileSheetChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    /// <summary>The elastic responses of the sheets opened without a barrier of their own, drained before the thread of the checks ends.</summary>
    private static readonly HeldElasticAnalyses Shared = new();

    private static HorizontalPileSheetViewModel Open(string module, JsonObject data, HeldElasticAnalyses? engine = null, PresentationServices? services = null,
        bool readOnly = false, RecordingHost? host = null) =>
        (HorizontalPileSheetViewModel)new HorizontalPileSheetModule(module, new NoFigures(), (engine ?? Shared).Create).CreateSheet(new SheetContext((JsonObject)data.DeepClone(),
            readOnly, host ?? new RecordingHost(), services ?? TraceServices(), ModuleServices.Get(module), new CalculationLimiter(1)));

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "palo-elastico");
            Directory.CreateDirectory(traces);
            foreach (var scenario in ElasticPileScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                ElasticPileTrace.Write(Path.Combine(traces, scenario.Name + ".txt"), lines);
                string stored = Path.Combine(ElasticPileScenarios.Reference(root), scenario.Name + ".txt");
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                string? difference = ElasticPileTrace.FirstDifference(ElasticPileTrace.Read(stored), lines);
                expect.That(difference is null, "Risposta elastica MVVM diversa dalla vista storica, scenario " + scenario.Name + ", " + difference);
            }
            await ActiveTabAsync(root, expect);
            await CurrentRevisionAsync(root, expect);
            await ClosingAsync(root, expect);
            await ReadOnlyAsync(root, expect);
            await ReloadAsync(root, expect);
            await VerifierAsync(root, expect);
            Derivations(expect);
            await Shared.DrainAsync();
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, ElasticPileScenario scenario)
    {
        var lines = new List<string>();
        var ids = new ElasticPileIds();
        var engine = new HeldElasticAnalyses();
        string stored = Path.Combine(ElasticPileScenarios.Reference(root), scenario.Name + ".txt");
        var side = new Side(scenario.Module, engine, HorizontalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture),
            File.Exists(stored) ? ElasticPileTrace.Read(stored) : []);
        try
        {
            lines.AddRange(side.State().Lines("apertura", ids));
            foreach (var step in scenario.Steps)
            {
                await side.ApplyAsync(step, lines.Count(l => l == "== " + step) + 1);
                lines.AddRange(side.State().Lines(step.ToString(), ids));
            }
        }
        // A step the ViewModel cannot apply ends the trace: the comparison then shows the first different line before it.
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { lines.Add("errore " + error.GetType().Name + ": " + error.Message); }
        finally { engine.Release(); side.Sheet.Dispose(); await engine.DrainAsync(); }
        return lines;
    }

    /// <summary>The ViewModel driven as its view would drive it: a control the view disables or hides does nothing.</summary>
    private sealed class Side
    {
        private readonly string module;
        private readonly HeldElasticAnalyses engine;
        private readonly IReadOnlyList<string> historic;
        private int modified;
        private string? dialog;

        internal Side(string module, HeldElasticAnalyses engine, JsonObject data, IReadOnlyList<string> historic)
        {
            this.module = module; this.engine = engine; this.historic = historic;
            Attach(Open(module, data, engine));
        }

        /// <summary>
        /// The sheet of the c.a. verifier as its host leaves it. Until F5.10 the host is the historic ConcreteWorkspace (WPF), which prepares
        /// and commits the sheet before «Applica» and the closing: the check replays the sheet it left, frozen in the data of the historic
        /// trace after the step (<paramref name="occurrence"/>-th step with that name), and the ViewModel writes it back as the historic dialog.
        /// </summary>
        private void HostEdits(PileConcreteVerifier verifier, ElasticPileStep step, int occurrence)
        {
            int block = -1;
            for (int i = 0, seen = 0; i < historic.Count; i++)
                if (historic[i] == "== " + step && ++seen == occurrence) { block = i; break; }
            if (block < 0 || !historic[block + 1].StartsWith("dati ", StringComparison.Ordinal)) throw new InvalidOperationException("Passo del verificatore assente dalla traccia storica: " + step);
            var left = JsonNode.Parse(historic[block + 1][5..])!["elastico"]!["tratti"]![verifier.Index]!["foglio_cls"]!.AsObject();
            verifier.Sheet.Clear();
            foreach (var (key, value) in left) verifier.Sheet[key] = value?.DeepClone();
        }

        internal HorizontalPileSheetViewModel Sheet { get; private set; } = null!;

        private ISheetViewModel Window => Sheet;

        private ElasticPileViewModel Elastic => Sheet.Elastic;

        private PileReinforcementViewModel Reinforcement => Sheet.Elastic.Reinforcement;

        private void Attach(HorizontalPileSheetViewModel sheet)
        {
            Sheet = sheet;
            modified = 0;
            sheet.Modified += (_, _) => modified++;
        }

        private static bool Usable(FieldViewModel field) => field.IsEnabled && field.IsVisible && field.CanChange;

        private static void Edit(FormViewModel form, string key, string? text)
        {
            var field = form[key];
            if (!Usable(field)) return;
            switch (field.Kind)
            {
                case FieldKind.Text when text is not null: if (!field.IsReadOnly) field.EditText = text; break;
                case FieldKind.Choice when text is not null: field.SelectedChoice = text; break;
                case FieldKind.Bool when text is null: field.IsChecked = !field.IsChecked; break;
                default: throw new ArgumentException("Passo non adatto al campo " + key);
            }
        }

        private ElasticSegmentViewModel? Segment(int index) => index >= 0 && index < Reinforcement.Segments.Count && Reinforcement.IsBuilt ? Reinforcement.Segments[index] : null;

        private static bool Enabled(IEnumerable<ElasticCommand> commands, string label) => commands.FirstOrDefault(c => c.Label == label)?.IsEnabled == true;

        internal async Task ApplyAsync(ElasticPileStep step, int occurrence)
        {
            dialog = null;
            var segment = Segment(step.Index);
            switch (step.Kind)
            {
                case ElasticPileStep.Commit: Window.Commit(); break;
                case ElasticPileStep.Calculate: Window.Commit(); await Expect.Within(Window.CalculateAsync()); break;
                case ElasticPileStep.Start: Window.Commit(); _ = Window.CalculateAsync(); break;
                case ElasticPileStep.Wait: await Expect.Within(Elastic.PendingCalculation); break;
                case ElasticPileStep.ReadOnly: Sheet.IsReadOnly = true; break;
                case ElasticPileStep.Reopen:
                    var data = Sheet.Data;
                    Sheet.Dispose();
                    Attach(Open(module, data, engine));
                    break;
                case ElasticPileStep.Hold: engine.Hold(); break;
                case ElasticPileStep.Entered: await engine.EnteredAsync(); break;
                case ElasticPileStep.Release: engine.Release(); break;
                case ElasticPileStep.Analysis: Sheet.SelectedAnalysis = step.Index; break;
                case ElasticPileStep.CapacityStep:
                    var capacity = step.Capacity!;
                    if (capacity.Kind == HorizontalPileStep.AddSurvey) Sheet.AddSurvey();
                    else if (capacity.Kind == HorizontalPileStep.Field)
                    {
                        var field = Sheet.Forms.Select(f => f.Form).Where(f => f.Contains(capacity.Key!)).Select(f => f[capacity.Key!]).Single();
                        if (Sheet.CanEdit && field.IsEnabled && field.IsVisible && !field.IsReadOnly) field.EditText = capacity.Text!;
                    }
                    else throw new ArgumentException("Passo della capacità non previsto: " + capacity.Kind);
                    break;
                case ElasticPileStep.Survey: if (Elastic.HasHeader && Sheet.CanEdit) Elastic.SurveyChoice = step.Index; break;
                case ElasticPileStep.Option: if (Elastic.Options is { } options) Edit(options, step.Key!, step.Text); break;
                case ElasticPileStep.Toggle: if (Elastic.Toggles.FirstOrDefault(t => t.Key == step.Key) is { } toggle) toggle.IsChecked = !toggle.IsChecked; break;
                case ElasticPileStep.EditInputs: Elastic.EditInputs(); break;
                case ElasticPileStep.Migration: Elastic.ResolveMigration(step.Text == "legacy"); break;
                case ElasticPileStep.Depth: Elastic.SelectDepth(double.Parse(step.Text!, CultureInfo.InvariantCulture)); break;
                case ElasticPileStep.SegmentEnd: if (segment is not null && Sheet.CanEdit) segment.End = step.Text!; break;
                case ElasticPileStep.SegmentLinked: segment?.ToggleLinked(); break;
                case ElasticPileStep.SegmentField: if (segment?.Fields.FirstOrDefault(f => f.Key == step.Key) is { } box) box.Text = step.Text!; break;
                case ElasticPileStep.SegmentRing: segment?.ToggleRing(); break;
                case ElasticPileStep.SegmentKind: if (segment is not null) segment.Kind = step.Text!; break;
                case ElasticPileStep.MainTransverse: if (Reinforcement.IsBuilt) Reinforcement.MainTransverse = step.Text!; break;
                case ElasticPileStep.Detail:
                    if (!Reinforcement.IsBuilt) break;
                    // The user opens the group of the field first.
                    if (Reinforcement.Details!.Contains(step.Key!)) { Reinforcement.DetailsExpanded = true; Edit(Reinforcement.Details, step.Key!, step.Text); }
                    else if (Reinforcement.Seismic!.Contains(step.Key!)) { Reinforcement.SeismicExpanded = true; Edit(Reinforcement.Seismic, step.Key!, step.Text); }
                    else { Reinforcement.ExtraExpanded = true; Edit(Reinforcement.Extra!, step.Key!, step.Text); }
                    break;
                case ElasticPileStep.Minimum: if (Reinforcement.IsBuilt) Reinforcement.Minimum = !Reinforcement.Minimum; break;
                case ElasticPileStep.Approve:
                    if (!Reinforcement.IsBuilt) break;
                    if (step.Text == "sisma") { Reinforcement.SeismicExpanded = true; Reinforcement.Approve(true); }
                    else Reinforcement.Approve(false);
                    break;
                case ElasticPileStep.Merge: if (segment is not null && Enabled(segment.Commands, "Unisci ←")) Reinforcement.Merge(step.Index); break;
                case ElasticPileStep.Verify: if (segment is not null && Enabled(segment.Commands, "Verifiche →")) Reinforcement.Verify(step.Index); break;
                case ElasticPileStep.Bill: if (segment is not null && Enabled(segment.Commands, "Distinta ferri")) Reinforcement.ShowBill(step.Index); break;
                case ElasticPileStep.SummarySegment: Reinforcement.ChooseSegment(step.Index); break;
                case ElasticPileStep.SummaryMode: Reinforcement.ChooseMode(step.Text); break;
                case ElasticPileStep.Split:
                    if (segment is null || !Enabled(segment.Commands, "Dividi…")) break;
                    dialog = Reinforcement.Split(step.Index, step.Text!) ?? "";
                    break;
                case ElasticPileStep.Propose:
                    if (!Reinforcement.IsBuilt || !Enabled(Reinforcement.Commands, "Proponi suddivisione…")) break;
                    if (Reinforcement.Propose() is not { } proposal) break;
                    dialog = PileReinforcementViewModel.ProposalText(proposal);
                    Reinforcement.ApplyProposal(proposal);
                    break;
                case ElasticPileStep.Concrete:
                    if (segment is null || !Enabled(segment.Commands, "Apri verificatore c.a.")) break;
                    if (Reinforcement.OpenConcreteVerifier(step.Index) is not { } verifier) break;
                    HostEdits(verifier, step, occurrence);
                    if (step.Text == "applica") verifier.Apply();
                    dialog = verifier.Note;
                    verifier.Close();
                    break;
                case ElasticPileStep.Design:
                    if (segment is null || !Enabled(segment.Commands, "Dimensiona…")) break;
                    if (Reinforcement.Design(step.Index) is not { } design) break;
                    string[] catalogues = step.Text!.Split('|');
                    design.Counts = catalogues[0]; design.Diameters = catalogues[1]; design.Links = catalogues[2]; design.Spacings = catalogues[3];
                    await Expect.Within(design.FindAsync());
                    if (design.CanApply) design.Apply();
                    dialog = design.Status;
                    design.Close();
                    break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        internal ElasticPileTraceState State()
        {
            var e = Elastic;
            bool edit = Sheet.CanEdit;
            var state = new ElasticPileTraceState
            {
                Data = Sheet.Data, Modified = modified, Editable = edit, Analysis = Sheet.SelectedAnalysis, Exportable = Window.HasResults && !Window.IsBusy,
                Response = e.Response, Result = e.Result, Status = e.Status, Progress = ElasticPileTrace.Progress(e.ProgressVisible, e.ProgressIndeterminate, e.ProgressValue),
                Stiffness = e.Stiffness, Migration = e.Migration,
                MigrationCommands = e.Migration is null ? [] : [new("Usa dati comuni e conserva legacy", edit), new("Importa legacy nei dati comuni", edit), new("Mostra confronto completo", edit)],
                Header = e.HasHeader, SurveyChoices = e.SurveyChoices, SurveyChoice = e.SurveyChoice, SurveyEnabled = edit,
                HeaderCommands = e.HasHeader ? [new("Modifica dati e strati", edit), new("Info", true)] : [],
                Options = e.Options is { } options ? FormState(options) : [],
                Toggles = e.Toggles.Select(t => new ElasticToggleState(t.Key, t.Label, t.IsChecked, edit)).ToList(),
                Drawing = e.DrawingSource, Selected = e.SelectedPoint, Selection = e.Selection, SectionPreview = Section(e.Section), SectionDetail = e.SectionDetail,
                Summary = e.Extremes, Table = e.Points.Select(Row).ToList(), Csv = e.CanExportCsv, ReinforcementSummary = e.ReinforcementSummary,
                SheetMessage = e.SheetMessage, SheetPieces = e.SheetPieces, Sheet = e.SheetResult, Reinforcement = Reinforcement.IsBuilt ? ReinforcementState(Reinforcement, edit) : null,
                Dialog = dialog
            };
            dialog = null;
            return state;
        }
    }

    private static string[] Row(ElasticPilePointRow row) =>
        ElasticPileTrace.Row(typeof(ElasticPilePointRow).GetProperties().Select(p => p.GetValue(row)));

    private static string Section(ElasticPileSectionView view) =>
        ElasticPileTrace.Section(view.Outline, view.Bars.Select(b => (b.X, b.Y, b.Area, b.Diametro)), view.LinkRadius, view.LinkDiameter, view.ShowAxes, view.Caption, view.LinkCaption);

    private static string Tone(ElasticTone tone) => tone switch
    {
        ElasticTone.Muted => "attenuato",
        ElasticTone.Failure => "rosso",
        ElasticTone.Pending => "ambra",
        ElasticTone.Satisfied => "verde",
        ElasticTone.Excluded => "grigio",
        _ => "blu"
    };

    private static ElasticReinforcementState ReinforcementState(PileReinforcementViewModel r, bool edit)
    {
        var state = new ElasticReinforcementState
        {
            Phase = r.Phase, Progress = ElasticPileTrace.Progress(r.ProgressVisible, r.ProgressIndeterminate, r.ProgressValue), Message = r.Message,
            Commands = r.Commands.Select(c => new ElasticCommandState(c.Label, c.IsEnabled)).ToList(),
            MainTransverse = r.MainTransverse, MainTransverseChoices = r.TransverseKinds, MainTransverseEnabled = edit,
            Segments = r.Segments.Select(s => new ElasticSegmentState
            {
                Header = s.Header, End = s.End, EndEditable = edit, EndToolTip = s.EndToolTip, Linked = s.Linked, LinkedEnabled = s.CanChangeLinked,
                Fields = s.Fields.Select(f => new ElasticSegmentFieldState(f.Key, f.Title, f.Text, f.IsReadOnly, f.ToolTip)).ToList(),
                Ring = s.Ring, RingEnabled = s.CanChangeRing, RingToolTip = s.RingToolTip, Kind = s.Kind, Kinds = s.Kinds, KindEnabled = s.CanChangeKind,
                Commands = s.Commands.Select(c => new ElasticCommandState(c.Label, c.IsEnabled)).ToList(), Bill = s.Bill, BillExpanded = s.BillExpanded
            }).ToList(),
            DetailsExpanded = r.DetailsExpanded, Details = FormState(r.Details!), SeismicExpanded = r.SeismicExpanded, Seismic = FormState(r.Seismic!),
            SeismicCommands = r.SeismicCommands.Select(c => new ElasticCommandState(c.Label, c.IsEnabled)).ToList(),
            ExtraExpanded = r.ExtraExpanded, Extra = FormState(r.Extra!), Minimum = r.Minimum, MinimumEnabled = edit
        };
        state.Summary.Add(ElasticPileTrace.SummaryText("blu", "Sezione e verifiche"));
        if (r.Summary is { } summary)
        {
            if (summary.Header is { } header) state.Summary.Add(ElasticPileTrace.SummaryText(Tone(header.Tone), header.Text));
            state.Summary.Add(ElasticPileTrace.SummaryChoice(summary.Segments, summary.Segments[summary.Selected], r.SummaryEnabled));
            state.Summary.Add(ElasticPileTrace.SummaryChoice(ElasticPilePresentation.SummaryModes, summary.Mode, r.SummaryEnabled));
            if (summary.Segment is not { } segment) state.Summary.Add(ElasticPileTrace.SummaryText("blu", ElasticPilePresentation.SegmentPending));
            else
            {
                state.Summary.Add(ElasticPileTrace.SummarySection(Section(segment.Section)));
                state.Summary.Add(ElasticPileTrace.SummaryText("blu", segment.Details));
                state.Summary.Add(ElasticPileTrace.SummaryVerdict(Tone(segment.Verdict.Tone), segment.Verdict.State));
                foreach (var group in segment.Verdict.Groups) state.Summary.Add(ElasticPileTrace.SummaryGroup(group.Header, group.Expanded, Tone(group.Tone), group.Text));
                if (segment.Verdict.Satisfied is { } note) state.Summary.Add(ElasticPileTrace.SummaryText("verde", note));
                foreach (var line in segment.Lines) state.Summary.Add(ElasticPileTrace.SummaryText(Tone(line.Tone), line.Text));
                state.Summary.Add(ElasticPileTrace.SummaryText("rosso", segment.Error));
            }
            state.Summary.Add(ElasticPileTrace.SummaryCommand("Apri verificatore c.a.", r.SummaryEnabled));
            state.Summary.Add(ElasticPileTrace.SummaryText("attenuato", ElasticPilePresentation.SummaryNote));
        }
        return state;
    }

    /// <summary>The fields as the trace of the historic view reads them (the label tells whether a row is enabled; a check box is its own row).</summary>
    private static List<VerticalPileFieldState> FormState(FormViewModel form) => form.Fields.Select(f =>
    {
        bool enabled = f.IsEnabled && !(f.Layout == FieldLayout.EditorOnly && !f.CanChange);
        return f.Kind switch
        {
            FieldKind.Text => new VerticalPileFieldState(f.Key, "campo", f.Label, f.Text, null, enabled, f.IsDimmed, f.IsVisible),
            FieldKind.Choice => new VerticalPileFieldState(f.Key, "scelta", f.Label, f.SelectedChoice ?? "", f.Choices, enabled, f.IsDimmed, f.IsVisible),
            _ => new VerticalPileFieldState(f.Key, "opzione", f.Label, VerticalPileTrace.YesNo(f.IsChecked), null, enabled, f.IsDimmed, f.IsVisible),
        };
    }).ToList();

    // ---------------------------------------------------------------- the sheet seen by the window

    /// <summary>On the elastic tab the window sees the elastic result, «Calcola» calculates it and the report is the elastic one; on the capacity, the capacity.</summary>
    private static async Task ActiveTabAsync(string root, Expect expect)
    {
        using var sheet = Open(HorizontalPileScenarios.Pile, HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Pile, "single-soil-elastic"));
        ISheetViewModel window = sheet;
        expect.That(sheet.SelectedAnalysis == 1 && sheet.ActiveResult is null && window.Result is null && !window.HasResults, "Scheda elastica senza risultato all'apertura");
        await Expect.Within(window.CalculateAsync());
        expect.That(window.Result is { } elastic && ReferenceEquals(elastic, sheet.Elastic.Result) && elastic.S("tipo_risultato") == "palo_elastico" && window.HasResults,
            "«Calcola» sulla scheda elastica: risultato elastico per la finestra");
        expect.That(sheet.Result is null, "«Calcola» sulla scheda elastica non calcola la capacità");
        var report = sheet.Report.BuildReport("Prova", new HashSet<string>());
        expect.That(report.Length > 0 && DocxText.Extract(report, new TextNormalizer([]), new GuidMap()).Contains("Risposta", StringComparison.Ordinal),
            "Report elastico della scheda elastica");
        sheet.SelectedAnalysis = 0;
        expect.That(window.Result is null || ReferenceEquals(window.Result, sheet.Result), "Scheda della capacità: il risultato della capacità per la finestra");
        await Expect.Within(window.CalculateAsync());
        expect.That(window.Result is not null && ReferenceEquals(window.Result, sheet.Result) && window.Result.S("tipo_risultato") != "palo_elastico", "«Calcola» sulla capacità");
        expect.Throws<InvalidOperationException>(() => Open(HorizontalPileScenarios.Pile, HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Pile, "default-elastico")).Report.BuildReport("x", new HashSet<string>()),
            "Report senza risultato: «Premere Calcola prima di esportare.»");
    }

    /// <summary>With the engine held: a change during a calculation supersedes it, only the current revision is published.</summary>
    private static async Task CurrentRevisionAsync(string root, Expect expect)
    {
        var engine = new HeldElasticAnalyses();
        using var sheet = Open(HorizontalPileScenarios.Pile, HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Pile, "single-soil-elastic"), engine);
        engine.Hold();
        await engine.EnteredAsync();
        var first = sheet.Elastic.PendingCalculation;
        sheet.Elastic.Options!["passo"].EditText = "0,25";
        var second = sheet.Elastic.PendingCalculation;
        expect.That(!ReferenceEquals(first, second) && sheet.Elastic.Result is null && sheet.Elastic.Status == ElasticPilePresentation.StatusChanged, "Modifica durante il calcolo: in coda");
        engine.Release();
        await Expect.Within(Task.WhenAll(first, second));
        expect.That(sheet.Elastic.Result is { } result && result["input"]!["passo"]!.ToString() == "0,25", "Pubblicato solo il calcolo dei dati correnti");
        expect.That(engine.Calls == 2, "Due calcoli entrati nel motore: " + engine.Calls);
        engine.Hold();
        sheet.Elastic.Options!["passo"].EditText = "0,5";
        await engine.EnteredAsync();
        sheet.SelectedAnalysis = 0;
        engine.Release();
        await Expect.Within(sheet.Elastic.PendingCalculation);
        expect.That(sheet.Elastic.Result is not null, "La scheda della capacità non annulla la risposta elastica (come la vista storica)");
        await engine.DrainAsync();
    }

    /// <summary>Closing during the calculation: nothing is published, the engine is released.</summary>
    private static async Task ClosingAsync(string root, Expect expect)
    {
        var engine = new HeldElasticAnalyses();
        var sheet = Open(HorizontalPileScenarios.Pile, HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Pile, "single-soil-elastic"), engine);
        engine.Hold();
        await engine.EnteredAsync();
        var pending = sheet.Elastic.PendingCalculation;
        sheet.Dispose();
        engine.Release();
        await Expect.Within(pending);
        expect.That(sheet.Elastic.Result is null && sheet.Elastic.Response is null, "Chiusura durante il calcolo: nulla pubblicato");
        await engine.DrainAsync();
    }

    /// <summary>Read-only: every edit of the tab is refused; the depth, the tabs and «Calcola» stay.</summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        using var sheet = Open(HorizontalPileScenarios.Pile, HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Pile, "elastico-tratti-anello-spirale"), readOnly: true);
        await Expect.Within(sheet.Elastic.PendingCalculation);
        string before = sheet.Data.ToJsonString();
        var e = sheet.Elastic; var r = e.Reinforcement;
        e.SurveyChoice = 0; e.Options!["passo"].EditText = "1"; e.Toggles[0].IsChecked = !e.Toggles[0].IsChecked; e.EditInputs();
        r.Segments[1].End = "7"; r.Segments[1].ToggleLinked(); r.Segments[1].ToggleRing(); r.Segments[1].Kind = "Spirale"; r.Segments[1].Fields[0].Text = "30";
        r.MainTransverse = "Spirale"; r.Minimum = !r.Minimum; r.Approve(false); r.Merge(1); r.Split(0, "2");
        expect.That(r.Propose() is null && r.Design(0) is null && r.OpenConcreteVerifier(0) is null, "Dialoghi rifiutati in sola lettura");
        expect.That(sheet.Data.ToJsonString() == before && sheet.SelectedAnalysis == 1, "Sola lettura: nessuna scrittura");
        e.SelectDepth(3);
        expect.That(e.SelectedPoint is not null, "Sola lettura: la quota si seleziona");
        await Expect.Within(((ISheetViewModel)sheet).CalculateAsync());
        expect.That(e.Result is not null, "Sola lettura: «Calcola» ammesso");
    }

    /// <summary>Reload: the tab is built again on the new data and calculates them.</summary>
    private static async Task ReloadAsync(string root, Expect expect)
    {
        using var sheet = Open(HorizontalPileScenarios.Pile, HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Pile, "single-soil-elastic"));
        await Expect.Within(sheet.Elastic.PendingCalculation);
        var old = sheet.Elastic;
        var data = HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Pile, "elastico-tratti-anello-spirale");
        sheet.Reload(data);
        expect.That(!ReferenceEquals(old, sheet.Elastic) && sheet.Elastic.Result is null && sheet.Elastic.Reinforcement.Segments.Count == 3, "Reload: scheda elastica ricostruita");
        await Expect.Within(sheet.Elastic.PendingCalculation);
        expect.That(sheet.Elastic.Result is not null && sheet.Elastic.SurveyChoice == 1, "Reload: calcolo dei dati nuovi");
    }

    /// <summary>The c.a. verifier of a segment: requested to the host with the sheet of the segment; its dialog writes back as the historic one.</summary>
    private static async Task VerifierAsync(string root, Expect expect)
    {
        var host = new RecordingHost();
        using var sheet = Open(HorizontalPileScenarios.Pile, HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Pile, "single-soil-elastic"), host: host);
        await Expect.Within(sheet.Elastic.PendingCalculation);
        var verifier = sheet.Elastic.Reinforcement.OpenConcreteVerifier(0)!;
        expect.That(host.Copies.Count == 1 && host.Copies[0].Module == "str_palo" && ReferenceEquals(host.Copies[0].Data, verifier.Sheet) && host.Copies[0].Name == "Verificatore c.a. · T1",
            "Verificatore c.a. chiesto all'ospite (OpenModuleCopy)");
        verifier.Close();
        expect.That(sheet.Data["elastico"]!["tratti"]![0]!["foglio_cls"] is JsonObject, "Chiusura del verificatore: il foglio resta nel tratto");
        using var micro = Open(HorizontalPileScenarios.Micro, HorizontalPileScenarios.FixtureData(root, HorizontalPileScenarios.Micro, "elastico-chs-stratificato"), host: host);
        expect.That(micro.Elastic.Reinforcement.OpenConcreteVerifier(0) is null && micro.Elastic.Reinforcement.Message.Contains("CHS", StringComparison.Ordinal) && host.Copies.Count == 1,
            "Verificatore c.a. rifiutato per il CHS");
    }

    // ---------------------------------------------------------------- derivations

    private static void Derivations(Expect expect)
    {
        var view = new JsonObject { ["molle"] = true };
        expect.That(ElasticPilePresentation.Shown(view, "molle") && ElasticPilePresentation.Shown(view, "y") && !ElasticPilePresentation.Shown(new JsonObject(), "molle"),
            "Viste del disegno: tutto tranne le molle");
        expect.That(ElasticPilePresentation.SurveyChoices(2).SequenceEqual(["Stratigrafia 1", "Stratigrafia 2"]), "Scelte della stratigrafia");
        expect.That(ElasticPilePresentation.Unavailable("x") == "Calcolo non disponibile: x" && ElasticPilePresentation.SheetUnavailable("x") == "Distinta non disponibile: x", "Testi dell'errore");
        var root = new JsonObject { ["generali"] = new JsonObject { ["lunghezza"] = "10", ["tratto_libero"] = 1.5 },
            ["elastico"] = new JsonObject { ["tratti"] = new JsonArray(new JsonObject { ["id"] = "T1", ["fine"] = "4" }, new JsonObject { ["id"] = "T2", ["fine"] = null }) } };
        expect.That(ElasticPilePresentation.SegmentHeaders(root).SequenceEqual(["T1 · 0 → 4 m · L=4 m", "T2 · 4 → 11,5 m · L=7,5 m"]), "Intestazioni dei tratti");
        expect.That(ElasticPilePresentation.BillFor(null, "Calcolo non disponibile: x", "T1") == "Distinta non disponibile · Calcolo non disponibile: x"
            && ElasticPilePresentation.BillFor(null, "Dati modificati", "T1") == ElasticPilePresentation.BillPending, "Distinta senza risultato");
        expect.That(ElasticPilePresentation.SheetMessage(null) == ElasticPilePresentation.BillPending && ElasticPilePresentation.SheetPieces(null) == 0, "Tavola vuota");
        var empty = new JsonObject { ["armature"] = new JsonObject { ["tratti"] = new JsonArray(new JsonObject { ["id"] = "T1", ["errore"] = "manca" }), ["distinta"] = new JsonArray() } };
        expect.That(ElasticPilePresentation.SheetMessage(empty) == "Distinta non disponibile\nT1: manca", "Tavola senza pezzi: il motivo per tratto");
        var row = new JsonObject { ["collegato"] = false };
        expect.That(ElasticPilePresentation.SegmentFieldToolTip(row, "longitudinal_bar_count", "Numero barre") == "Numero barre · campo non assegnato: valore dalla sezione principale"
            && ElasticPilePresentation.SegmentFieldToolTip(new JsonObject { ["collegato"] = true }, "x", "y") == ElasticPilePresentation.SharedValue, "Note dei campi dei tratti");
        expect.That(ElasticPileFields.ExtraNote("XC2").StartsWith("Esposizione condivisa: XC2. Dettagli Pilastro", StringComparison.Ordinal), "Nota del copriferro");
    }

    // ---------------------------------------------------------------- services

    private static PresentationServices TraceServices() => new(new ScriptedMessages(), new AlwaysYes(), new ScriptedFileDialogs(), new ScriptedDialogs(),
        new RecordingClipboard(), new RecordingLinks(), new RecordingWiki());

    private sealed class AlwaysYes : IConfirmationService
    {
        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmDelete(string title, string question) => true;
    }

    /// <summary>The window side of the checks: the requests of the c.a. verifier are recorded.</summary>
    private sealed class RecordingHost : ISheetHost
    {
        internal List<(string Module, JsonObject Data, string Name)> Copies { get; } = [];
        public void OpenModuleCopy(string module, JsonObject data, string name) => Copies.Add((module, data, name));
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException("Report non previsto.");
    }

    /// <summary>The figures of the report of the capacity, not drawn (the elastic report has none).</summary>
    private sealed class NoFigures : IHorizontalPileFigureRenderer
    {
        public IReadOnlyList<ReportOrizzontale.Figure> Render(JsonObject result) => [];
    }
}
