using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.VerticalPile;
using Anthea.Presentation.Services;
using Anthea.Reports;
using X.Core;
using Anthea.Testing.Comparison;
using Anthea.Testing.Normalization;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of the horizontal pile and micropile (F5.7) against its historic view, without WPF. This is the evidence of equivalence
/// that survives the archiving of the historic view:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/HorizontalPileTrace.cs gives the trace of the historic view frozen in
/// tests/fixtures/f5-palo-orizzontale/storica, line by line;</item>
/// <item>for each F3 fixture of the two modules on the tab of the capacity the data after opening, visit, calculation, export and
/// reopening, the result, the status and the text and structure of the report equal the F3 reference byte for byte; for the fixtures on
/// the tab of the elastic response (F5.8: result, «Calcola» and report of the tab shown) every file equals it as ui/f3-equivalence compares
/// it, with the tolerances and exactly the F4 transitions of these modules admitted (all on single-soil-elastic);</item>
/// <item>the diagrams of every survey equal those frozen in tests/fixtures/f5-palo-orizzontale/diagrammi, which the historic view draws
/// (ui/check-palo-orizzontale-historic);</item>
/// <item>module and keys of interface state; derivations of ANTHEA.Application with values written by hand; with the engine held by a
/// barrier: only the current revision is published, the elastic tab cancels without clearing, closing during the calculation;
/// read-only, Reload; the details of the soil editor, the CSV export and the copy dialogs.</item>
/// </list>
/// One "UI" thread (SingleThreadContext): the background calculation publishes there, as on the dispatcher.
/// </summary>
internal static class HorizontalPileSheetChecks
{
    private const string Pile = HorizontalPileScenarios.Pile, Micro = HorizontalPileScenarios.Micro;

    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    private static readonly SameSizeFigures Figures = new();

    /// <summary>The elastic responses of the sheets (F5.8), drained before the thread of the checks ends.</summary>
    private static readonly HeldElasticAnalyses Elastic = new();

    private static HorizontalPileSheetViewModel Open(string module, JsonObject data, IModuleService? service = null, PresentationServices? services = null, bool readOnly = false) =>
        (HorizontalPileSheetViewModel)new HorizontalPileSheetModule(module, Figures, Elastic.Create).CreateSheet(new SheetContext((JsonObject)data.DeepClone(), readOnly, new Host(),
            services ?? Scripted.Services(confirmations: null), service ?? ModuleServices.Get(module), new CalculationLimiter(1)));

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "palo-orizzontale");
            Directory.CreateDirectory(traces);
            foreach (var scenario in HorizontalPileScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                HorizontalPileTrace.Write(Path.Combine(traces, scenario.Name + ".txt"), lines);
                string stored = Path.Combine(HorizontalPileScenarios.Reference(root), scenario.Name + ".txt");
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                // F5-02 (O6): the historic trace with the declared difference of the new view (the choice «Acciaio» shows the class of the data).
                var (expected, declared) = HorizontalPileTrace.WithDeclaredDifferences(HorizontalPileTrace.Read(stored));
                expect.That(declared == 0 || scenario.Name == HorizontalPileTrace.CustomSteelScenario, "F5-02 solo nello scenario dell'acciaio personalizzato: " + scenario.Name);
                string? difference = HorizontalPileTrace.FirstDifference(expected, lines);
                expect.That(difference is null, "Palo orizzontale MVVM diverso dalla vista storica, scenario " + scenario.Name + ", " + difference);
                if (scenario.Name == HorizontalPileTrace.CustomSteelScenario) expect.That(declared > 0, "F5-02: righe della scelta «Acciaio» con la classe dei dati: " + declared);
            }
            CustomSteel(root, expect);
            var fixtures = new[] { Pile, Micro }.SelectMany(m => HorizontalPileScenarios.Fixtures(root, m).Select(id => (Module: m, Id: id))).ToArray();
            expect.That(fixtures.Length == 21, "Fixture F3 dei pali orizzontali: " + fixtures.Length);
            string capture = Path.Combine(output, "palo-orizzontale-f3");
            foreach (var (module, id) in fixtures) await F3Async(root, module, id, capture, expect);
            CompareF3(root, capture, fixtures, expect);
            foreach (var (module, id) in fixtures) await DiagramsAsync(root, module, id, expect);
            Contract(expect);
            Derivations(expect);
            await CurrentRevisionAsync(root, expect);
            await ElasticTabAsync(root, expect);
            await ClosingAsync(root, expect);
            await ReadOnlyAsync(root, expect);
            await ReloadAsync(root, expect);
            await SoilDetailsAsync(root, expect);
            await CsvAsync(root, expect);
            await SurveyCommandsAsync(root, expect);
            await Elastic.DrainAsync();
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, HorizontalPileScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(scenario.Module);
        var side = new Side(scenario.Module, engine, HorizontalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture));
        try
        {
            lines.AddRange(side.State().Lines("apertura"));
            foreach (var step in scenario.Steps)
            {
                await side.ApplyAsync(step);
                lines.AddRange(side.State().Lines(step.ToString()));
            }
        }
        // A step the ViewModel cannot apply ends the trace: the comparison then shows the first different line before it.
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { lines.Add("errore " + error.GetType().Name + ": " + error.Message); }
        finally { engine.Release(); side.Sheet.Dispose(); }
        return lines;
    }

    /// <summary>The ViewModel driven as its view would drive it: a control the view disables does nothing (CanEdit, field states).</summary>
    private sealed class Side
    {
        private readonly string module;
        private readonly HeldModuleService engine;
        private int modified;
        private Task latest = Task.CompletedTask, observedPending = Task.CompletedTask;

        internal Side(string module, HeldModuleService engine, JsonObject data)
        {
            this.module = module; this.engine = engine;
            Attach(Open(module, data, engine, TraceServices()));
        }

        internal HorizontalPileSheetViewModel Sheet { get; private set; } = null!;

        private void Attach(HorizontalPileSheetViewModel sheet)
        {
            Sheet = sheet;
            modified = 0;
            sheet.Modified += (_, _) => modified++;
            latest = observedPending = sheet.PendingCalculation;
        }

        /// <summary>The last calculation requested: the automatic one of the last edit or the started one, whichever came last.</summary>
        private Task Latest()
        {
            if (!ReferenceEquals(Sheet.PendingCalculation, observedPending)) latest = observedPending = Sheet.PendingCalculation;
            return latest;
        }

        private Task Started(Task task) { Latest(); latest = task; return task; }

        private FieldViewModel Field(string key) => Sheet.Forms.Select(f => f.Form).Where(f => f.Contains(key)).Select(f => f[key]).Single();

        private bool Usable(FieldViewModel field) => Sheet.CanEdit && field.IsEnabled && field.IsVisible;

        private HorizontalSoilRowViewModel? Soil(int survey, int row) =>
            Sheet.Surveys[survey].Soils.TryGetValue(Sheet.Surveys[survey].Rows[row], out var soil) ? soil : null;

        internal async Task ApplyAsync(HorizontalPileStep step)
        {
            switch (step.Kind)
            {
                case HorizontalPileStep.Commit: Sheet.Commit(); break;
                case HorizontalPileStep.Calculate: Sheet.Commit(); await Expect.Within(Started(Sheet.CalculateAsync())); break;
                case HorizontalPileStep.Start: Sheet.Commit(); _ = Started(Sheet.CalculateAsync()); break;
                case HorizontalPileStep.Wait: await Expect.Within(Latest()); break;
                case HorizontalPileStep.ReadOnly: Sheet.IsReadOnly = true; break;
                case HorizontalPileStep.Reopen:
                    var data = Sheet.Data;
                    Sheet.Dispose();
                    Attach(Open(module, data, engine, TraceServices()));
                    break;
                case HorizontalPileStep.Hold: engine.Hold(); break;
                case HorizontalPileStep.Entered: await engine.EnteredAsync(); break;
                case HorizontalPileStep.Release: engine.Release(); break;
                case HorizontalPileStep.Field:
                {
                    var field = Field(step.Key!);
                    if (Usable(field) && !field.IsReadOnly) field.EditText = step.Text!;
                    break;
                }
                case HorizontalPileStep.Choice:
                {
                    var field = Field(step.Key!);
                    if (Usable(field)) field.SelectedChoice = step.Text;
                    break;
                }
                case HorizontalPileStep.Option:
                {
                    var field = Field(step.Key!);
                    if (Usable(field)) field.IsChecked = !field.IsChecked;
                    break;
                }
                case HorizontalPileStep.Analysis: Sheet.SelectedAnalysis = step.Index; break;
                case HorizontalPileStep.Survey: Sheet.SelectedSurveyIndex = step.Index; break;
                case HorizontalPileStep.AddSurvey: Sheet.AddSurvey(); break;
                case HorizontalPileStep.RemoveSurvey: Sheet.RemoveSurvey(step.Index); break;
                case HorizontalPileStep.RemoveSelectedSurvey: Sheet.RemoveSelectedSurvey(); break;
                case HorizontalPileStep.CopySurvey:
                    Sheet.SelectedSurveyIndex = step.Index;
                    Sheet.CopySurvey(step.Index, step.Text == "nuova" ? -1 : int.Parse(step.Text!, CultureInfo.InvariantCulture));
                    break;
                case HorizontalPileStep.CopyFrom:
                    Sheet.SelectedSurveyIndex = step.Index;
                    Sheet.CopySurvey(int.Parse(step.Text!, CultureInfo.InvariantCulture), step.Index);
                    break;
                case HorizontalPileStep.Layer:
                {
                    Sheet.SelectedSurveyIndex = step.Index;
                    if (!Sheet.CanEdit) break;
                    // The cells of the view write the row at every keystroke.
                    Sheet.Surveys[step.Index].Rows[step.Row][step.Key!] = step.Text;
                    break;
                }
                case HorizontalPileStep.AddLayer: Sheet.SelectedSurveyIndex = step.Index; Sheet.AddLayer(step.Index); break;
                case HorizontalPileStep.RemoveLayer:
                    Sheet.SelectedSurveyIndex = step.Index;
                    Sheet.RemoveLayer(step.Index, Sheet.Surveys[step.Index].Rows[step.Row]);
                    break;
                case HorizontalPileStep.Soil:
                {
                    Sheet.SelectedSurveyIndex = step.Index;
                    if (Soil(step.Index, step.Row) is not { } soil || !Sheet.CanEdit) break;
                    switch (step.Key)
                    {
                        case HorizontalPileStep.SoilCategory: soil.Category = step.Text!; break;
                        case HorizontalPileStep.SoilLaw: if (soil.Laws.ToList().IndexOf(step.Text!) is int index and >= 0) soil.LawIndex = index; break;
                        case HorizontalPileStep.SoilValue: if (soil.ValueEditable) soil.Value = step.Text!; break;
                        case HorizontalPileStep.SoilOverride: if (soil.OverrideShown) soil.Override = !soil.Override; break;
                        case HorizontalPileStep.SoilNh: if (soil.NhShown) soil.Nh = step.Text!; break;
                        default: throw new ArgumentException("Comando del terreno sconosciuto: " + step.Key);
                    }
                    break;
                }
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        internal HorizontalPileTraceState State()
        {
            var s = Sheet;
            var selected = s.SelectedSurveyIndex >= 0 && s.SelectedSurveyIndex < s.Surveys.Count ? s.Surveys[s.SelectedSurveyIndex] : null;
            var section = s.SectionDrawing;
            var state = new HorizontalPileTraceState
            {
                Data = s.Data, Modified = modified, Editable = s.CanEdit, Exportable = s.HasResults && !s.IsBusy, ResultsShown = s.CanShowResults,
                Analysis = s.SelectedAnalysis, Result = s.Result,
                Status = s.Status, Summary = s.Summary, Moment = s.MomentText, MomentToolTip = s.MomentToolTip, Model = s.ModelText, ModelHint = s.ModelHint,
                Warnings = s.WarningText,
                Forms = s.Forms.Select(f => new HorizontalPileFormState(f.Name, FormState(f.Form),
                    f.Form.Groups.Select(g => new HorizontalPileGroupState(g.Title, g.Fields.Select(x => x.Key).ToArray(), g.IsExpanded)).ToList())).ToList(),
                Verification = s.Verification.Rows.ToList(), Outcome = s.Verification.Outcome, OutcomeKind = OutcomeKind(s.Verification.Kind),
                SelectedSurvey = s.SelectedSurveyIndex, ProfileSurvey = s.SelectedSurveyIndex,
                Surveys = s.Surveys.Select(v => v.Rows.Select(r => r.Values).ToList()).ToList(),
                Soils = selected is null ? [] : selected.Rows.Select(r => selected.Soils.TryGetValue(r, out var soil) ? SoilState(soil) : null).ToList(),
                ConcreteSection = s.IsMicro ? null : HorizontalPileTrace.ConcreteSection(section.Outline, section.Bars.Select(b => (b.X, b.Y, b.Area, b.Diametro)),
                    section.LinkRadius, section.LinkDiameter, section.ShowAxes, section.Caption),
                Chs = s.IsMicro ? s.ChsProperties : null,
            };
            if (s.Result is not null)
                state.Results = s.Results.Select(r => new HorizontalPileSurveyResultState(r.Tables.Select(t => new HorizontalPileTableState(t.Title, t.Columns, t.Rows)).ToList(), r.Info)).ToList();
            return state;
        }
    }

    private static string OutcomeKind(HorizontalVerificationOutcome kind) => kind switch
    {
        HorizontalVerificationOutcome.Satisfied => "soddisfatta",
        HorizontalVerificationOutcome.NotSatisfied => "non-soddisfatta",
        _ => "attenuato"
    };

    private static HorizontalSoilState SoilState(HorizontalSoilRowViewModel soil) => new(soil.Categories, soil.Category, soil.Laws, soil.LawIndex, soil.Parameter,
        soil.Minimum, soil.Maximum, soil.ValueEditable ? soil.Value : soil.TabulatedValue, soil.ValueEditable, soil.OverrideShown, soil.Override,
        soil.NhShown ? soil.Nh : null, soil.Description);

    /// <summary>The services of the traces: every removal and replacement confirmed, nothing else asked.</summary>
    private static PresentationServices TraceServices() => new(new ScriptedMessages(), new AlwaysYes(), new ScriptedFileDialogs(), new ScriptedDialogs(),
        new RecordingClipboard(), new RecordingLinks(), new RecordingWiki());

    /// <summary>
    /// The fields as the trace of the historic view reads them: the first element of the row tells whether it is enabled. A row without a
    /// separate label (a check box of a form without symbol columns) is its editor, which the read-only revision disables, as FieldForm does.
    /// </summary>
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

    // ---------------------------------------------------------------- F3 reference

    /// <summary>
    /// The path of ui/f3-equivalence through the ViewModel, written in the layout of a capture (sheets/module/id/…). The visit selects every
    /// tab of the analyses and every stratigraphy and comes back, as F3EquivalenceCapture.Visit does on the controls.
    /// </summary>
    private static async Task F3Async(string root, string module, string id, string capture, Expect expect)
    {
        string folder = Path.Combine(capture, "sheets", module, id);
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        var guids = new GuidMap();
        void Save(string name, JsonNode? value) => File.WriteAllText(Path.Combine(folder, name), CanonicalJson.Serialize(value, guids, normalizer), new UTF8Encoding(false));
        using var sheet = Open(module, HorizontalPileScenarios.FixtureData(root, module, id));
        sheet.Commit(); Save("opened.json", sheet.Data);
        int analysis = sheet.SelectedAnalysis;
        for (int i = 0; i < 2; i++) sheet.SelectedAnalysis = i;
        sheet.SelectedAnalysis = analysis;
        int survey = sheet.SelectedSurveyIndex;
        for (int i = 0; i < sheet.Surveys.Count; i++) sheet.SelectedSurveyIndex = i;
        sheet.SelectedSurveyIndex = survey;
        sheet.Commit(); Save("visited.json", sheet.Data);
        // The window calculates and reads the tab shown (F5.8: the elastic response for the fixtures that open on it).
        ISheetViewModel window = sheet;
        sheet.Commit(); await Expect.Within(window.CalculateAsync()); sheet.Commit();
        Save("calculated-data.json", sheet.Data); Save("result.json", window.Result);
        var status = new JsonObject { ["hasResults"] = window.HasResults };
        try
        {
            var options = ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();
            byte[] report = sheet.Report.BuildReport("Fixture F3 · " + module + " · " + id, options);
            File.WriteAllText(Path.Combine(folder, "report.txt"), DocxText.Extract(report, normalizer, guids), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "report-structure.json"), CanonicalJson.Serialize(Structure(report), guids, normalizer), new UTF8Encoding(false));
            status["report"] = "available";
        }
        catch (Exception error) when (error is not IOException and not UnauthorizedAccessException)
        { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
        Save("exported-data.json", sheet.Data); Save("status.json", status);
        using var reopened = Open(module, sheet.Data);
        reopened.Commit(); Save("reopened.json", reopened.Data);
        expect.That(File.Exists(Path.Combine(folder, "opened.json")), "Cattura F3 del ViewModel non scritta: " + module + "/" + id);
    }

    /// <summary>
    /// The capture of the ViewModel against the F3 reference of the historic view: every file byte for byte (after the line endings) for the
    /// fixtures on the tab of the capacity, except the four with the transitions of AG-PO-01; for those on the tab of the elastic response (F5.8),
    /// and for those four, every file as ui/f3-equivalence compares it, with the tolerances (the measured times tempi_ms are volatile) and exactly
    /// the F4 transitions of the two modules admitted.
    /// </summary>
    private static void CompareF3(string root, string capture, IReadOnlyList<(string Module, string Id)> fixtures, Expect expect)
    {
        string expected = Path.Combine(root, "tests", "fixtures", "f3", "expected");
        var transitions = Transition.Load(Path.Combine(root, "tests", "fixtures", "f3", "transitions-f4.json"))
            .Where(t => fixtures.Any(f => t.File.StartsWith("sheets/" + f.Module + "/", StringComparison.Ordinal))).ToList();
        string reference = Path.Combine(capture, "..", "palo-orizzontale-f3-elastico-riferimento");
        string candidate = Path.Combine(capture, "..", "palo-orizzontale-f3-elastico-cattura");
        int elastic = 0, compared = 0, capacity = 0;
        foreach (var (module, id) in fixtures)
        {
            string relative = "sheets/" + module + "/" + id;
            bool opensElastic = HorizontalPileScenarios.OpensElastic(HorizontalPileScenarios.FixtureData(root, module, id));
            if (opensElastic) elastic++;
            var names = Directory.EnumerateFiles(Path.Combine(expected, relative)).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();
            var produced = Directory.EnumerateFiles(Path.Combine(capture, relative)).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();
            expect.That(names.SequenceEqual(produced), module + "/" + id + ": file della cattura diversi dal riferimento F3 (" + string.Join(", ", produced) + ")");
            foreach (string? name in names)
            {
                string file = relative + "/" + name;
                string a = File.ReadAllText(Path.Combine(expected, file), Encoding.UTF8).Replace("\r\n", "\n");
                string b = File.ReadAllText(Path.Combine(capture, file), Encoding.UTF8).Replace("\r\n", "\n");
                compared++;
                // AG-PO-01 (24/10/2026): a file of the capacity with F4 transitions (only those of AG-PO-01, the analytic parabola of the
                // concrete) is compared with the elastic ones, as ui/f3-equivalence compares it; the others byte for byte.
                var listed = transitions.Where(t => t.File == file).ToList();
                if (!opensElastic && listed.Count == 0)
                {
                    expect.That(a == b, file + " diverso dal riferimento F3 della vista storica");
                    continue;
                }
                if (!opensElastic)
                {
                    expect.That(listed.All(t => t.Entry == "AG-PO-01"), file + ": transizione F4 su un file della capacità diversa da AG-PO-01");
                    capacity++;
                }
                foreach (var (folder, text) in new[] { (reference, a), (candidate, b) })
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(folder, file))!);
                    File.WriteAllText(Path.Combine(folder, file), text, new UTF8Encoding(false));
                }
            }
        }
        var comparer = new BaselineComparer(Tolerances.Load(Path.Combine(root, "tests", "ANTHEA.Testing", "tolerances.json")));
        comparer.Admit(transitions);
        var report = comparer.Compare(reference, candidate);
        File.WriteAllText(Path.Combine(capture, "..", "palo-orizzontale-f3-elastico-confronto.json"), report.ToJsonString(J.Options));
        expect.That(comparer.NotAdmitted == 0, "Fixture F3 della risposta elastica: " + comparer.NotAdmitted + " differenze non ammesse o transizioni F4 assenti");
        // PARATIE-19 (22/10/2026): the shear depth of the circular piles with a continuous ring (S2 of the paratie review) changes VRd and
        // what depends on it in three elastic fixtures; the other transitions stay those of the elastic response of single-soil-elastic.
        // AG-PO-01 and AG-PO-02 (24/10/2026): the analytic parabola of the concrete (Model 5.21.0.0, Concrete 0.20.0.0) changes My and Hu of
        // the two fixtures of the capacity with an RC section (report.txt and result.json of broms-coesivo-impedita and single-soil-layer) and
        // MRd and ratios of the reinforcement checks of the elastic fixtures; 17 earlier transitions keep their value before, with the new
        // value after and the entry extended with « / AG-PO-02».
        var shear = transitions.Where(t => t.Entry.StartsWith("PARATIE-19", StringComparison.Ordinal)).ToList();
        var parabola = transitions.Where(t => t.Entry is "AG-PO-01" or "AG-PO-02").ToList();
        var others = transitions.Except(shear).Except(parabola).ToList();
        expect.That(elastic == 11 && capacity == 4 && shear.Count == 3941 && others.Count == 1117
            && others.All(t => t.File.Contains("/single-soil-elastic/", StringComparison.Ordinal))
            && parabola.Count(t => t.Entry == "AG-PO-01") == 480 && parabola.Count(t => t.Entry == "AG-PO-02") == 7110
            && transitions.Count(t => t.Entry.EndsWith(" / AG-PO-02", StringComparison.Ordinal)) == 17,
            "Transizioni F4 dei pali orizzontali: " + transitions.Count + " (attese le 1117 della risposta elastica di single-soil-elastic, le 3941 di PARATIE-19, "
            + "le 480 di AG-PO-01 su 4 file della capacità e le 7110 di AG-PO-02; 17 voci estese con AG-PO-02)");
        expect.That(compared > 0, "Nessun file F3 confrontato");
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

    // ---------------------------------------------------------------- diagrams

    /// <summary>The diagrams of every survey of the result after «Calcola» equal those that the historic view draws, frozen by the ui stage.</summary>
    private static async Task DiagramsAsync(string root, string module, string id, Expect expect)
    {
        var data = HorizontalPileScenarios.FixtureData(root, module, id);
        string file = Path.Combine(HorizontalPileScenarios.Diagrams(root), module + "--" + id + ".json");
        if (HorizontalPileScenarios.OpensElastic(data)) { expect.That(!File.Exists(file), "Diagrammi congelati per una fixture della risposta elastica: " + id); return; }
        using var sheet = Open(module, data);
        sheet.Commit(); await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        if (sheet.Result is null) { expect.That(!File.Exists(file), "Diagrammi congelati senza risultato: " + id); return; }
        var actual = HorizontalPileTrace.Diagrams(Enumerable.Range(0, sheet.Result.Array("sondaggi").Count)
            .SelectMany(i => new[] { true, false }.Select(soil => sheet.Diagram(i, soil))));
        expect.That(File.Exists(file) && File.ReadAllText(file).Replace("\r\n", "\n") == actual.Replace("\r\n", "\n"),
            "Diagrammi del ViewModel diversi da quelli della vista storica: " + module + "--" + id);
    }

    /// <summary>
    /// F5-02 (O6, declared difference of the new view): a value of the steel that makes it custom shows «Personalizzato» in the choice «Acciaio»,
    /// as the data say (the historic view kept the previous class); the note of a class of the catalogue is the one of the values of the catalogue.
    /// </summary>
    private static void CustomSteel(string root, Expect expect)
    {
        using var sheet = Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "single-soil-layer"));
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var form = sheet.SectionForm;
        var section = sheet.Data["sezione"]!.AsObject();
        form["classe_acciaio"].SelectedChoice = "B450A";
        expect.That(section.S("classe_acciaio") == "B450A" && form["classe_acciaio"].SelectedChoice == "B450A"
            && form["classe_acciaio"].Note == MaterialInputCatalog.SteelDifference(section, "NTC 2018") && modified == 2,
            "F5-02: classe dell'acciaio dal catalogo, nota sui valori del catalogo (" + (form["classe_acciaio"].Note ?? "nessuna") + ")");
        form["steel_fu_mpa"].EditText = "560";
        expect.That(section.S("classe_acciaio") == "Personalizzato" && form["classe_acciaio"].SelectedChoice == "Personalizzato" && modified == 4,
            "F5-02: un valore dell'acciaio personalizzato mostra «Personalizzato» nella scelta «Acciaio»");
        form["steel_diagramma"].SelectedChoice = "Incrudente";
        expect.That(section.S("classe_acciaio") == "Personalizzato" && form["classe_acciaio"].SelectedChoice == "Personalizzato" && modified == 6,
            "F5-02: anche il legame dell'acciaio");
    }

    // ---------------------------------------------------------------- contract and derivations

    private static void Contract(Expect expect)
    {
        foreach (string module in new[] { Pile, Micro })
        {
            var sheetModule = new HorizontalPileSheetModule(module, Figures);
            expect.That(sheetModule.Info == ModuleDirectory.Get(module) && sheetModule.Reports == new ModuleReportCapabilities(false)
                && sheetModule.Help.GuideId == ModuleHelp.For(module).GuideId, "Modulo " + module);
            expect.That(sheetModule.Persistence.UiStateKeys.SequenceEqual(["vista_orizzontale"]), "Chiavi di stato dichiarate: " + module);
        }
        expect.Throws<ArgumentException>(() => _ = new HorizontalPileSheetModule("geo_palo_verticale", Figures), "Modulo non dei pali orizzontali accettato");
        var registry = new ModuleRegistry([new HorizontalPileSheetModule(Micro, Figures), new HorizontalPileSheetModule(Pile, Figures)]);
        expect.That(registry.SheetModuleIds.SequenceEqual(ModuleDirectory.All.Select(m => m.Id).Where(m => m is Pile or Micro)), "Registro con i due moduli nell'ordine della directory");
        expect.That(HorizontalPileSheetViewModel.RecalculationDelay == TimeSpan.FromMilliseconds(450), "Ritardo del ricalcolo automatico");
        using var sheet = Open(Pile, ModuleDirectory.CreateData(Pile));
        expect.That(sheet.CalculationMode == SheetCalculationMode.Background && sheet.AutomaticDelay == TimeSpan.FromMilliseconds(450) && !sheet.IsMicro
            && sheet.SelectedAnalysis == 0 && sheet.Forms.Count == 7 && sheet.SectionToolTips.Count == 3, "Foglio del palo sullo sfondo, 450 ms, sette form");
        using var micro = Open(Micro, ModuleDirectory.CreateData(Micro));
        expect.That(micro.IsMicro && micro.SectionForm.Contains("profilo_chs") && micro.SectionToolTips.Count == 0
            && micro.SectionForm.Groups.Select(g => g.Title).SequenceEqual(["Tubolare CHS", "Acciaio", "Proprietà della sezione"]), "Foglio del micropalo con il CHS");
        expect.Throws<ArgumentException>(() => _ = new HorizontalPileSheetViewModel(Pile, new SheetContext(new JsonObject(), false, new Host(), Scripted.Services(),
            ModuleServices.Get(Micro), new CalculationLimiter(1)), Figures), "Servizio di un altro modulo accettato");
    }

    /// <summary>The derivations moved into ANTHEA.Application, with values written by hand.</summary>
    private static void Derivations(Expect expect)
    {
        var none = HorizontalPilePresentation.Verification(null);
        expect.That(none.Rows.Count == 4 && none.Rows.All(r => r[1] == "—") && none.Outcome == "Da calcolare" && none.Kind == HorizontalVerificationOutcome.Muted, "Verifica senza risultato");
        var satisfied = HorizontalPilePresentation.Verification(new JsonObject { ["azione_kn"] = 100, ["capacita_kn"] = 1234.56, ["resistenza_caratteristica_manuale_kn"] = 800, ["resistenza_progetto_manuale_kn"] = 615.4 });
        expect.That(satisfied.Rows[1][1] == "1.234,6" && satisfied.Rows[3][1] == "615,4" && satisfied.Outcome == "Verifica soddisfatta" && satisfied.Kind == HorizontalVerificationOutcome.Satisfied, "Verifica soddisfatta");
        var failed = HorizontalPilePresentation.Verification(new JsonObject { ["azione_kn"] = 700, ["resistenza_progetto_manuale_kn"] = 615.4 });
        expect.That(failed.Outcome == "Verifica non soddisfatta" && failed.Kind == HorizontalVerificationOutcome.NotSatisfied && failed.Rows[1][1] == "—", "Verifica non soddisfatta");
        var missing = HorizontalPilePresentation.Verification(new JsonObject { ["azione_kn"] = 700 });
        expect.That(missing.Outcome == "Verifica non eseguita: Rd non determinata" && missing.Kind == HorizontalVerificationOutcome.Muted, "Verifica senza Rd");
        expect.That(HorizontalPilePresentation.LayerName(0) == "A" && HorizontalPilePresentation.LayerName(25) == "Z" && HorizontalPilePresentation.LayerName(27) == "B", "Nome dello strato");
        expect.That(HorizontalPilePresentation.Summary(new JsonObject { ["meccanismo"] = "Lungo", ["sondaggio_governante"] = 2, ["momento_resistente_knm"] = 1088.11, ["rapporto_meccanico"] = 0.16672, ["sperimentale"] = true })
            == "Lungo · stratigrafia 2\nMy = 1.088,1 kNm\nHEd / Hu = 16,7%\nVerifica normativa incompleta.\nMODELLO SPERIMENTALE", "Sintesi");
        var ca = HorizontalPilePresentation.SectionMoment(new JsonObject { ["momento_knm"] = 1353.08, ["n_kn"] = 800, ["lati_contorno"] = 32, ["residuo_n_kn"] = 0.000123, ["tolleranza_n_kn"] = 0.01, ["modello"] = "Checker" }, false);
        expect.That(ca.Text == "MRd = 1.353,1 kNm · N = 800,0 kN\nChecker · 32 lati · residuo N = 0,000123 kN (tol. 0,01)\nNormativa del risultato non disponibile" && ca.ToolTip == "Checker", "Momento della sezione c.a.");
        var chs = HorizontalPilePresentation.SectionMoment(new JsonObject { ["momento_knm"] = 77.267, ["n_kn"] = 150, ["classe"] = 1 }, true);
        expect.That(chs.Text == "My(N) = 77,3 kNm · N = 150,0 kN\nCHS classe 1 · solo acciaio · interazione N–M lineare" && chs.ToolTip is null, "Momento del CHS");
        var analysis = new HorizontalPileAnalysis(new JsonObject(), null, "spessore_chs_mm: inserire un numero finito maggiore di 0.");
        var manual = new JsonObject { ["generali"] = new JsonObject { ["origine_momento"] = "Manuale", ["momento_resistente"] = "45" } };
        expect.That(HorizontalPilePresentation.CalculatedMoment(analysis, manual, true).Text == "My inserito manualmente = 45,0 kNm"
            && HorizontalPilePresentation.CalculatedMoment(analysis, new JsonObject { ["generali"] = new JsonObject() }, true).Text == "Momento non disponibile: spessore_chs_mm: inserire un numero finito maggiore di 0."
            && HorizontalPilePresentation.ResultMoment(new JsonObject { ["momento_resistente_knm"] = 2500 }, false).Text == "My adottato manualmente = 2.500,0 kNm", "Momento manuale e non disponibile");
        var section = new JsonObject { ["second_inner_enabled"] = true, ["longitudinal_bar_count"] = "16", ["longitudinal_bar_diameter_mm"] = "24", ["second_inner_gap"] = "40" };
        expect.That(HorizontalPilePresentation.FillInnerRing(section).SequenceEqual(["second_inner_count", "second_inner_diameter"])
            && section.S("second_inner_count") == "16" && section.S("second_inner_diameter") == "24" && section.S("second_inner_gap") == "40"
            && HorizontalPilePresentation.FillInnerRing(new JsonObject()).Count == 0, "Secondo anello riempito dalla prima corona");
        var steel = new JsonObject { ["classe_acciaio"] = "B450C" };
        expect.That(HorizontalPilePresentation.ApplySectionMaterial(steel, "steel_fu_mpa").Count == 0 && steel.S("classe_acciaio") == "Personalizzato"
            && steel.S("materiale_acciaio_nome") == "Acciaio personalizzato", "Acciaio personalizzato");
        var concrete = new JsonObject { ["classe_cls"] = "C30/37" };
        var shown = HorizontalPilePresentation.ApplySectionMaterial(concrete, "classe_cls");
        expect.That(shown.Contains("fck_mpa") && concrete.D("fck_mpa") == 30 && !shown.Contains("nome"), "Calcestruzzo dal catalogo");
        var states = HorizontalPilePresentation.FieldStates(new JsonObject { ["generali"] = new JsonObject { ["presenza_falda"] = false, ["origine_momento"] = "Manuale" },
            ["verifica"] = new JsonObject { ["efficienza_metodo"] = "Reese & Van Impe (foglio)" } });
        expect.That(states.Contains(("profondita_falda", false, true)) && states.Contains(("eccentricita", true, false)) && states.Contains(("momento_resistente", true, true))
            && states.Contains(("efficienza_eta", false, true)) && states.Contains(("interasse_destro", true, true)) && states.Count == 9, "Campi attivi per opzione");
        expect.That(HorizontalPilePresentation.ChsFieldStates(new JsonObject { ["modo_chs"] = "Manuale" }).SequenceEqual([("profilo_chs", false), ("diametro_chs_mm", true), ("spessore_chs_mm", true)]), "Campi del CHS");
        var figures = HorizontalPilePresentation.Figures(new JsonObject { ["sondaggi"] = new JsonArray(new JsonObject(), new JsonObject()) });
        expect.That(figures.Count == 4 && figures[0] == new HorizontalPileFigurePlan(1, true, "Tensioni e resistenze del terreno", 1000, 660)
            && figures[3] == new HorizontalPileFigurePlan(2, false, "Equilibrio e sollecitazioni alla capacità ultima", 1000, 660) && figures[0].AspectRatio == 1000d / 660, "Piano delle figure");
        var hinge = new JsonObject { ["meccanismo"] = "Lungo", ["cerniere_m"] = new JsonArray(0, 2.5), ["diagrammi"] = new JsonArray(
            new JsonObject { ["z"] = 2, ["lato"] = "nodo" }, new JsonObject { ["z"] = 2.5, ["lato"] = "prima" }, new JsonObject { ["z"] = 2.5, ["lato"] = "dopo" }, new JsonObject { ["z"] = 3, ["lato"] = "nodo" }) };
        expect.That(HorizontalPileDiagrams.ReactionLimit(hinge) == 2.5 && HorizontalPileDiagrams.PhysicalDiagram(hinge).Length == 2
            && HorizontalPileDiagrams.ReactionLimit(new JsonObject { ["meccanismo"] = "Corto" }) is null, "Ramo fisico della reazione sopra la cerniera interna");
        expect.That(HorizontalPilePresentation.NewLayer().ToJsonString() == "{\"tipologia\":\"Granulare\",\"spessore\":\"0\",\"peso_specifico\":\"0\",\"peso_specifico_saturo\":\"0\",\"angolo_attrito\":\"0\",\"coesione_non_drenata\":\"0\",\"coesione_efficace\":\"0\"}", "Strato nuovo");
    }

    // ---------------------------------------------------------------- calculation with a barrier

    /// <summary>Only the current revision is published: a calculation superseded by an edit, even when it completes, is never shown.</summary>
    private static async Task CurrentRevisionAsync(string root, Expect expect)
    {
        var engine = new HeldModuleService(Pile);
        using var sheet = Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "single-soil-layer"), engine);
        try
        {
            engine.Hold();
            var first = sheet.PendingCalculation;
            await engine.EnteredAsync();
            expect.That(sheet.IsBusy && sheet.Status == HorizontalPilePresentation.StatusRunning, "Calcolo in corso");
            sheet.GeneralForm["lunghezza"].EditText = "9";
            var second = sheet.PendingCalculation;
            engine.Release();
            expect.That(!await Expect.Within(first), "Calcolo superato pubblicato");
            expect.That(await Expect.Within(second) && sheet.Result!["input"]!["generali"].S("lunghezza") == "9", "Ultima revisione pubblicata");
            expect.That(engine.Calls == 2, "Motore chiamato una volta per revisione: " + engine.Calls);
        }
        finally { engine.Release(); }
    }

    /// <summary>The tab of the elastic response cancels the calculation of the capacity without clearing its result; back, it calculates.</summary>
    private static async Task ElasticTabAsync(string root, Expect expect)
    {
        var engine = new HeldModuleService(Pile);
        using var sheet = Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "single-soil-layer"), engine);
        try
        {
            expect.That(await Expect.Within(sheet.PendingCalculation), "Calcolo all'apertura");
            var result = sheet.Result;
            engine.Hold();
            var running = sheet.CalculateAsync();
            await engine.EnteredAsync();
            int modified = 0;
            sheet.Modified += (_, _) => modified++;
            sheet.SelectedAnalysis = 1;
            expect.That(sheet.Data.S("vista_orizzontale") == "elastico" && modified == 1 && ReferenceEquals(sheet.Result, result)
                && sheet.CalculationState == CalculationState.Cancelled, "Scheda elastica: calcolo annullato, risultato conservato, Modified");
            engine.Release();
            await Expect.Within(running);
            expect.That(ReferenceEquals(sheet.Result, result) && sheet.CalculationState == CalculationState.Cancelled, "Calcolo annullato mai pubblicato");
            sheet.SelectedAnalysis = 0;
            expect.That(sheet.Data.S("vista_orizzontale") == "capacita" && modified == 2 && ReferenceEquals(sheet.Result, result) && sheet.PendingCalculation.IsCompleted, "Ritorno con un risultato: nessun ricalcolo");
        }
        finally { engine.Release(); }
    }

    /// <summary>Closing during a calculation: nothing is published, nothing fails, the document is not touched.</summary>
    private static async Task ClosingAsync(string root, Expect expect)
    {
        var engine = new HeldModuleService(Micro);
        var data = HorizontalPileScenarios.FixtureData(root, Micro, "single-soil-layer");
        var sheet = Open(Micro, data, engine);
        try
        {
            engine.Hold();
            var pending = sheet.PendingCalculation;
            await engine.EnteredAsync();
            sheet.Dispose();
            engine.Release();
            expect.That(!await Expect.Within(pending) && sheet.Result is null && !sheet.CanEdit, "Chiusura durante il calcolo");
            sheet.SelectedAnalysis = 1;
            expect.That(sheet.SelectedAnalysis == 0 && !sheet.Data.ContainsKey("vista_orizzontale"), "Foglio chiuso: la scheda non cambia");
            expect.That(data.ToJsonString() == HorizontalPileScenarios.FixtureData(root, Micro, "single-soil-layer").ToJsonString(), "Dati della fixture intatti");
        }
        finally { engine.Release(); sheet.Dispose(); }
    }

    /// <summary>Read-only: forms, table, soil editors and stratigraphies refuse; selection, tab (in memory, without Modified: rule T6) and calculation stay.</summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        using var sheet = Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "broms-coesivo-impedita"), readOnly: true);
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults, "Calcolo in sola lettura");
        string before = sheet.Data.ToJsonString();
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        expect.That(sheet.Forms.All(f => f.Form.IsReadOnly) && !sheet.CanEdit, "Form in sola lettura");
        sheet.Surveys[0].Rows[0]["spessore"] = "9";
        expect.That(sheet.Surveys[0].Rows[0].Values.S("spessore") == "4", "Cella ripristinata");
        sheet.AddSurvey(); sheet.RemoveSurvey(1); sheet.AddLayer(0); sheet.RemoveLayer(0, sheet.Surveys[0].Rows[0]);
        expect.That(!sheet.CopySurvey(0, -1), "Copia rifiutata");
        var soil = sheet.Surveys[0].Soils[sheet.Surveys[0].Rows[0]];
        soil.Category = soil.Categories[^1]; soil.LawIndex = soil.Laws.Count - 1; soil.Value = "1"; soil.Source = "x"; soil.AdoptMean(); soil.KeepValue();
        sheet.SelectedSurveyIndex = 1;
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && sheet.SelectedSurveyIndex == 1, "Modifiche rifiutate, selezione ammessa");
        sheet.SelectedAnalysis = 1;
        // Regola T6 (F5.15, F5-17): la scheda resta nella copia in memoria del foglio, senza Modified.
        expect.That(modified == 0 && sheet.Data.S("vista_orizzontale") == "elastico", "Scheda delle analisi in sola lettura: in memoria, senza Modified");
        sheet.IsReadOnly = false;
        expect.That(sheet.Forms.All(f => !f.Form.IsReadOnly), "Form di nuovo modificabili");
    }

    /// <summary>Reload: the sheet is rebuilt on the new data as at opening (preparations included), without Modified, and recalculates.</summary>
    private static async Task ReloadAsync(string root, Expect expect)
    {
        using var sheet = Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "single-soil-layer"));
        await Expect.Within(sheet.PendingCalculation);
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var other = HorizontalPileScenarios.FixtureData(root, Pile, "broms-coesivo-impedita");
        sheet.Reload(other);
        expect.That(modified == 0 && sheet.Result is null && sheet.Surveys.Count == 2 && sheet.Status == HorizontalPilePresentation.StatusPending
            && sheet.Summary == HorizontalPilePresentation.SummaryInitial && sheet.Data["elastico"] is JsonObject && sheet.GeneralForm.Get("vincolo") == "Impedita", "Reload come all'apertura");
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults && sheet.Status == HorizontalPilePresentation.StatusCompleted, "Ricalcolo dopo il Reload");
    }

    // ---------------------------------------------------------------- dialogs and commands outside the traces

    /// <summary>The details of the soil editor (a modal dialog of the historic view, outside the traces): source, reason, mean, value kept.</summary>
    private static async Task SoilDetailsAsync(string root, Expect expect)
    {
        using var sheet = Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "single-soil-layer"));
        await Expect.Within(sheet.PendingCalculation);
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var row = sheet.Surveys[0].Rows[0];
        var soil = sheet.Surveys[0].Soils[row];
        var layer = sheet.Data.Array("stratigrafie")[0]![0]!;
        soil.Source = "Prova in sito";
        expect.That(layer["reazione_orizzontale"].S("fonte") == "Prova in sito" && modified == 2 && !sheet.HasResults, "Fonte del valore: due Modified e ricalcolo");
        soil.OverrideReason = "ignorato";
        expect.That(layer["reazione_orizzontale"]!["motivo_override"] is null, "Motivazione senza override ignorata");
        expect.That(soil.CanAdoptMean, "Media adottabile per la correlazione della tabella");
        soil.Value = "9";
        expect.That(layer["reazione_orizzontale"].S("origine_scelta") == "Valore modificato dall'utente", "Valore modificato registrato");
        soil.KeepValue();
        expect.That(layer["reazione_orizzontale"].S("origine_scelta") == "Valore mantenuto esplicitamente dopo cambio fonte" && soil.DetailsText == soil.Description, "Valore mantenuto");
        soil.AdoptMean();
        expect.That(soil.DetailsText == soil.Description && soil.Value == layer["reazione_orizzontale"].S("A"), "Nuova media adottata");
    }

    /// <summary>«Esporta CSV»: the file chosen in the dialog, nothing when cancelled, the error shown when writing fails.</summary>
    private static async Task CsvAsync(string root, Expect expect)
    {
        var messages = new ScriptedMessages();
        string folder = Path.Combine(Path.GetTempPath(), "anthea-f57-csv-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "palo.csv");
        var files = new ScriptedFileDialogs().Then(null).Then(path).Then(Path.Combine(folder, "manca", "palo.csv"));
        using var sheet = Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "single-soil-layer"), null, Scripted.Services(messages, files: files));
        sheet.ExportCsv();
        expect.That(files.Requests.Count == 0, "Esporta CSV senza risultato: nessun dialogo");
        await Expect.Within(sheet.PendingCalculation);
        sheet.ExportCsv();
        expect.That(!File.Exists(path) && files.Requests[0] is SaveFileRequest { FileName: "Palo_orizzontale.csv" } request
            && FileFilter.Join(request.Filters) == "Tabelle CSV|*.csv", "Esporta CSV annullato");
        sheet.ExportCsv();
        expect.That(File.ReadAllText(path, Encoding.UTF8) == HorizontalPileEditor.Csv(sheet.Result!), "CSV dei risultati");
        sheet.ExportCsv();
        expect.That(messages.Shown.Count == 1 && messages.Shown[0].Title == "Esportazione non riuscita", "Errore di scrittura mostrato");
        Directory.Delete(folder, true);
    }

    /// <summary>The questions and the dialog of «Copia in…»/«Copia da…» and of the removal of a stratigraphy.</summary>
    private static async Task SurveyCommandsAsync(string root, Expect expect)
    {
        var messages = new ScriptedMessages();
        var confirmations = new ScriptedConfirmations().Then(false).Then(true);
        var dialogs = new ScriptedDialogs().Then(d => ((SurveyCopyDialogViewModel)d).Dismiss()).Then(d => { var v = (SurveyCopyDialogViewModel)d; v.Selected = v.Choices[^1]; v.Confirm(); })
            .Then(d => { var v = (SurveyCopyDialogViewModel)d; v.Selected = v.Choices[0]; v.Confirm(); });
        using var sheet = Open(Pile, HorizontalPileScenarios.FixtureData(root, Pile, "broms-coesivo-impedita"), null, Scripted.Services(messages, confirmations, dialogs: dialogs));
        string before = sheet.Data.ToJsonString();
        sheet.SelectedSurveyIndex = 1;
        await sheet.CopySurveyIntoAsync();
        var shown = (SurveyCopyDialogViewModel)dialogs.Shown[0];
        expect.That(sheet.Data.ToJsonString() == before && shown.Title == "Copia stratigrafia in…" && shown.Question == "Copia la stratigrafia 2 in:"
            && shown.Choices.Select(c => c.Label).SequenceEqual(["Stratigrafia 1", "Nuova stratigrafia"]), "Dialogo «Copia in…» annullato");
        await sheet.CopySurveyIntoAsync();
        var surveys = sheet.Data.Array("stratigrafie");
        expect.That(surveys.Count == 3 && JsonNode.DeepEquals(surveys[2], surveys[1]) && sheet.SelectedSurveyIndex == 2 && sheet.Data["elastico"]!["stratigrafia"]!.GetValue<int>() == -1,
            "Copia in una stratigrafia nuova (scelta dell'elastico azzerata)");
        await sheet.CopySurveyFromAsync();
        expect.That(confirmations.Asked.Last() == "Copia stratigrafia: Sostituire gli strati della stratigrafia 3?" && !JsonNode.DeepEquals(surveys[2], surveys[0]),
            "«Copia da…» con sostituzione rifiutata");
        sheet.RemoveSurvey(2);
        expect.That(confirmations.Asked.Last() == "Stratigrafia: Eliminare la stratigrafia 3 e tutti i suoi strati?" && surveys.Count == 2 && sheet.SelectedSurveyIndex == 1, "Stratigrafia eliminata");
        using var single = Open(Micro, HorizontalPileScenarios.FixtureData(root, Micro, "single-soil-layer"), null, Scripted.Services(messages));
        single.RemoveSelectedSurvey();
        await single.CopySurveyFromAsync();
        expect.That(single.Data.Array("stratigrafie").Count == 1 && messages.Shown.Select(m => m.Message).SequenceEqual(
            ["Mantenere almeno una stratigrafia. È possibile eliminare tutti i suoi strati.", "Non ci sono altre stratigrafie da cui copiare."]), "Ultima stratigrafia e copia senza altre stratigrafie");
    }

    // ---------------------------------------------------------------- services of the checks

    /// <summary>Figures of the report as images of the size of the WPF ones: only the size enters the report (extent of the images).</summary>
    private sealed class SameSizeFigures : IHorizontalPileFigureRenderer
    {
        public IReadOnlyList<ReportOrizzontale.Figure> Render(JsonObject result) =>
            HorizontalPilePresentation.Figures(result).Select(f => new ReportOrizzontale.Figure(f.Survey, f.Caption, Png(f.Width, f.Height), f.AspectRatio)).ToArray();

        /// <summary>The signature and the header of a PNG of the given size (the report reads width and height at bytes 16–23).</summary>
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

    /// <summary>The questions of the traces: every removal and replacement confirmed, as the historic trace does (confirm: false).</summary>
    private sealed class AlwaysYes : IConfirmationService
    {
        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmDelete(string title, string question) => true;
    }

    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException("Copia non prevista.");
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException("Report non previsto.");
    }
}
