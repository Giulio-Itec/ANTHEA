using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.VerticalPile;
using Anthea.Presentation.Services;
using Anthea.Testing.Comparison;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of the vertical pile and micropile (F5.6) against its historic view, without WPF. This is the evidence of equivalence
/// that survives the archiving of the historic view:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/VerticalPileTrace.cs gives the trace of the historic view frozen in
/// tests/fixtures/f5-palo-verticale/storica, line by line;</item>
/// <item>for each F3 fixture of the two modules the data after opening, visit, calculation, export and reopening, the result, the status
/// and the text and structure of the report equal the F3 reference (byte for byte, or through the F4 transitions listed for the file);</item>
/// <item>the series of the capacity chart in every view and the reference chart of every stratigraphy equal those frozen by f3-figures;</item>
/// <item>module and keys of interface state; derivations of ANTHEA.Application with values written by hand; with the engine held by a
/// barrier: only the current revision is published, closing during the calculation, read-only, Reload; dialogs and confirmations.</item>
/// </list>
/// One "UI" thread (SingleThreadContext): the background calculation publishes there, as on the dispatcher.
/// </summary>
internal static class VerticalPileSheetChecks
{
    private const string Pile = VerticalPileScenarios.Pile, Micro = VerticalPileScenarios.Micro;

    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    private static readonly SameSizeFigures Figures = new();

    private static VerticalPileSheetViewModel Open(string module, JsonObject data, IModuleService? service = null, PresentationServices? services = null) =>
        (VerticalPileSheetViewModel)new VerticalPileSheetModule(module, Figures).CreateSheet(new SheetContext((JsonObject)data.DeepClone(), false, new Host(),
            services ?? Scripted.Services(confirmations: null), service ?? ModuleServices.Get(module), new CalculationLimiter(1)));

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3, f3-figures and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "palo-verticale");
            Directory.CreateDirectory(traces);
            foreach (var scenario in VerticalPileScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                VerticalPileTrace.Write(Path.Combine(traces, scenario.Name + ".txt"), lines);
                string stored = Path.Combine(VerticalPileScenarios.Reference(root), scenario.Name + ".txt");
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                string? difference = VerticalPileTrace.FirstDifference(VerticalPileTrace.Read(stored), lines);
                expect.That(difference is null, "Palo verticale MVVM diverso dalla vista storica, scenario " + scenario.Name + ", " + difference);
            }
            var fixtures = new[] { Pile, Micro }.SelectMany(m => VerticalPileScenarios.Fixtures(root, m).Select(id => (Module: m, Id: id))).ToArray();
            expect.That(fixtures.Length == 13, "Fixture F3 dei pali verticali: " + fixtures.Length);
            string capture = Path.Combine(output, "palo-verticale-f3");
            foreach (var (module, id) in fixtures) await F3Async(root, module, id, capture, expect);
            CompareF3(root, capture, fixtures, expect);
            foreach (var (module, id) in fixtures) await FiguresAsync(root, module, id, expect);
            Contract(expect);
            Derivations(expect);
            await StateKeysAsync(root, expect);
            await CurrentRevisionAsync(root, expect);
            await ClosingAsync(root, expect);
            await ReadOnlyAsync(root, expect);
            await ReloadAsync(root, expect);
            await InjectionAsync(root, expect);
            await SurveyCommandsAsync(root, expect);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, VerticalPileScenario scenario)
    {
        var lines = new List<string>();
        var engine = new HeldModuleService(scenario.Module);
        var side = new Side(scenario.Module, engine, VerticalPileScenarios.FixtureData(root, scenario.Module, scenario.Fixture));
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

        internal VerticalPileSheetViewModel Sheet { get; private set; } = null!;

        private void Attach(VerticalPileSheetViewModel sheet)
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

        private FieldViewModel Field(string key) => new[] { Sheet.GeneralForm, Sheet.EfficiencyForm, Sheet.NormativeForm }
            .Where(f => f.Contains(key)).Select(f => f[key]).Single();

        private bool Usable(FieldViewModel field) => Sheet.CanEdit && field.IsEnabled && field.IsVisible;

        internal async Task ApplyAsync(VerticalPileStep step)
        {
            switch (step.Kind)
            {
                case VerticalPileStep.Commit: Sheet.Commit(); break;
                case VerticalPileStep.Calculate: Sheet.Commit(); await Expect.Within(Started(Sheet.CalculateAsync())); break;
                case VerticalPileStep.Start: Sheet.Commit(); _ = Started(Sheet.CalculateAsync()); break;
                case VerticalPileStep.Wait: await Expect.Within(Latest()); break;
                case VerticalPileStep.ReadOnly: Sheet.IsReadOnly = true; break;
                case VerticalPileStep.Reopen:
                    var data = Sheet.Data;
                    Sheet.Dispose();
                    Attach(Open(module, data, engine, TraceServices()));
                    break;
                case VerticalPileStep.Hold: engine.Hold(); break;
                case VerticalPileStep.Entered: await engine.EnteredAsync(); break;
                case VerticalPileStep.Release: engine.Release(); break;
                case VerticalPileStep.Field:
                {
                    var field = Field(step.Key!);
                    if (Usable(field) && !field.IsReadOnly) field.EditText = step.Text!;
                    break;
                }
                case VerticalPileStep.Choice:
                {
                    var field = Field(step.Key!);
                    if (Usable(field)) field.SelectedChoice = step.Text;
                    break;
                }
                case VerticalPileStep.Option:
                {
                    var field = Field(step.Key!);
                    if (Usable(field)) field.IsChecked = !field.IsChecked;
                    break;
                }
                case VerticalPileStep.Reset: Sheet.ResetNormative(); break;
                case VerticalPileStep.Survey: Sheet.SelectedSurveyIndex = step.Index; break;
                case VerticalPileStep.AddSurvey: Sheet.AddSurvey(); break;
                case VerticalPileStep.RemoveSurvey: Sheet.RemoveSurvey(step.Index); break;
                case VerticalPileStep.RemoveSelectedSurvey: Sheet.RemoveSelectedSurvey(); break;
                case VerticalPileStep.CopySurvey:
                    Sheet.SelectedSurveyIndex = step.Index;
                    Sheet.CopySurvey(step.Index, step.Text == "nuova" ? -1 : int.Parse(step.Text!, CultureInfo.InvariantCulture));
                    break;
                case VerticalPileStep.Layer:
                {
                    Sheet.SelectedSurveyIndex = step.Index;
                    if (!Sheet.CanEdit) break;
                    var row = Sheet.Surveys[step.Index].Rows[step.Row];
                    // The cells of the view write the row at every keystroke.
                    if (step.Text is null) row[step.Key!] = !(row[step.Key!] is true);
                    else row[step.Key!] = step.Text;
                    break;
                }
                case VerticalPileStep.AddLayer: Sheet.SelectedSurveyIndex = step.Index; Sheet.AddLayer(step.Index); break;
                case VerticalPileStep.RemoveLayer:
                    Sheet.SelectedSurveyIndex = step.Index;
                    Sheet.RemoveLayer(step.Index, Sheet.Surveys[step.Index].Rows[step.Row]);
                    break;
                case VerticalPileStep.CapacityView: Sheet.SelectedCapacityView = step.Index; break;
                case VerticalPileStep.Curve:
                    if (Sheet.CurveChoices.FirstOrDefault(c => c.Key == step.Key) is { } choice) choice.IsChecked = !choice.IsChecked;
                    break;
                case VerticalPileStep.AllCurves: Sheet.ShowAllCurves(); break;
                case VerticalPileStep.NoCurves: Sheet.HideAllCurves(); break;
                case VerticalPileStep.Table: Sheet.SelectedTableIndex = step.Index; break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        internal VerticalPileTraceState State()
        {
            var s = Sheet;
            return new VerticalPileTraceState
            {
                Data = s.Data, Modified = modified, Editable = s.CanEdit, Exportable = s.HasResults && !s.IsBusy, Result = s.Result,
                Status = s.Status, Warnings = s.WarningText,
                General = FormState(s.GeneralForm), Efficiency = FormState(s.EfficiencyForm), Normative = FormState(s.NormativeForm), EfficiencyText = s.EfficiencyText,
                Verification = s.Verification.ToList(),
                SelectedSurvey = s.SelectedSurveyIndex, ProfileSurvey = s.SelectedSurveyIndex,
                Surveys = s.Surveys.Select(v => v.Rows.Select(r => r.Values).ToList()).ToList(),
                CapacityViews = s.CapacityViews, CapacityView = s.SelectedCapacityView,
                Capacity = CapacityState(s.CapacityChart), Reference = ReferenceState(s.ReferenceChart), EndValues = s.EndValues,
                Curves = s.CurveChoices.Select(c => (c.Label, c.IsChecked)).ToList(),
                Tables = s.TableTitles, SelectedTable = s.SelectedTableIndex, Shown = TableState(s.ShownTable), Raw = s.RawResult,
            };
        }
    }

    /// <summary>The services of the traces: every removal and replacement confirmed, nothing else asked.</summary>
    private static PresentationServices TraceServices() => new(new ScriptedMessages(), new AlwaysYes(), new ScriptedFileDialogs(), new ScriptedDialogs(),
        new RecordingClipboard(), new RecordingLinks(), new RecordingWiki());

    private static List<VerticalPileFieldState> FormState(FormViewModel form) => form.Fields.Select(f => f.Kind switch
    {
        FieldKind.Text => new VerticalPileFieldState(f.Key, "campo", f.Label, f.Text, null, f.IsEnabled, f.IsDimmed, f.IsVisible),
        FieldKind.Choice => new VerticalPileFieldState(f.Key, "scelta", f.Label, f.SelectedChoice ?? "", f.Choices, f.IsEnabled, f.IsDimmed, f.IsVisible),
        _ => new VerticalPileFieldState(f.Key, "opzione", f.Label, VerticalPileTrace.YesNo(f.IsChecked), null, f.IsEnabled, f.IsDimmed, f.IsVisible),
    }).ToList();

    private static VerticalPileSeriesState Series(PlotSeriesData s) => new(s.Name, VerticalPileTrace.Colour(s.Color), s.Dashed, s.Highlighted, s.DashPattern, s.Points);

    private static VerticalPileChartState CapacityState(VerticalPileCapacityChart c) => new()
    {
        Title = c.Title, XLabel = c.XLabel, YLabel = c.YLabel, Note = "", EmptyMessage = c.EmptyMessage, CapacityDepth = c.CapacityDepth, XMinimum = null,
        Series = c.Series.Select(Series).ToList(),
    };

    /// <summary>The reference chart as the Plot of the view shows it: no message of its own («Premere Calcola», never shown with curves), no depth.</summary>
    private static VerticalPileChartState ReferenceState(PlotData p) => new()
    {
        Title = p.Title, XLabel = p.XLabel, YLabel = p.YLabel, Note = p.Note, EmptyMessage = "Premere Calcola", CapacityDepth = 0, XMinimum = p.XMinimum,
        Series = p.Series.Select(Series).ToList(),
        Markers = p.Markers.Select(m => new VerticalPileMarkerState(m.X, m.Y, m.Label, VerticalPileTrace.Colour(m.Color), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius)).ToList(),
    };

    private static VerticalPileTableState? TableState(VerticalPileTableView? view) => view is null ? null : new VerticalPileTableState
    {
        Note = view.Note, Footnote = view.Footnote,
        Tables = view.Tables.Select(t => (t.Title ?? "", t.Columns, t.Rows)).ToList(),
    };

    // ---------------------------------------------------------------- F3 reference

    /// <summary>The path of ui/f3-equivalence through the ViewModel, written in the layout of a capture (sheets/module/id/…).</summary>
    private static async Task F3Async(string root, string module, string id, string capture, Expect expect)
    {
        string folder = Path.Combine(capture, "sheets", module, id);
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        var guids = new GuidMap();
        void Save(string name, JsonNode? value) => File.WriteAllText(Path.Combine(folder, name), CanonicalJson.Serialize(value, guids, normalizer), new UTF8Encoding(false));
        using var sheet = Open(module, VerticalPileScenarios.FixtureData(root, module, id));
        sheet.Commit(); Save("opened.json", sheet.Data);
        // The visit of the tabs and of the expanders writes nothing (traces of the historic view).
        sheet.Commit(); Save("visited.json", sheet.Data);
        sheet.Commit(); await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        Save("calculated-data.json", sheet.Data); Save("result.json", sheet.Result);
        var status = new JsonObject { ["hasResults"] = sheet.HasResults };
        try
        {
            var options = ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();
            byte[] report = sheet.Report.BuildReport("Fixture F3 · " + module + " · " + id, options);
            File.WriteAllText(Path.Combine(folder, "report.txt"), DocxText.Extract(report, normalizer, guids), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "report-structure.json"), CanonicalJson.Serialize(Structure(report), guids, normalizer), new UTF8Encoding(false));
            status["report"] = "available";
        }
        catch (InvalidOperationException error) { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
        Save("exported-data.json", sheet.Data); Save("status.json", status);
        using var reopened = Open(module, sheet.Data);
        reopened.Commit(); Save("reopened.json", reopened.Data);
        expect.That(File.Exists(Path.Combine(folder, "opened.json")), "Cattura F3 del ViewModel non scritta: " + module + "/" + id);
    }

    /// <summary>
    /// The capture of the ViewModel against the F3 reference of the historic view: every file byte for byte (after the line endings), except
    /// the files with F4 transitions, compared as ui/f3-equivalence compares them, with exactly those transitions admitted.
    /// </summary>
    private static void CompareF3(string root, string capture, IReadOnlyList<(string Module, string Id)> fixtures, Expect expect)
    {
        string expected = Path.Combine(root, "tests", "fixtures", "f3", "expected");
        var transitions = Transition.Load(Path.Combine(root, "tests", "fixtures", "f3", "transitions-f4.json"))
            .Where(t => fixtures.Any(f => t.File.StartsWith("sheets/" + f.Module + "/" + f.Id + "/", StringComparison.Ordinal))).ToList();
        var withTransitions = transitions.Select(t => t.File).ToHashSet(StringComparer.Ordinal);
        string reference = Path.Combine(capture, "..", "palo-verticale-f3-riferimento");
        string candidate = Path.Combine(capture, "..", "palo-verticale-f3-transizioni");
        foreach (var (module, id) in fixtures)
        {
            string relative = "sheets/" + module + "/" + id;
            var names = Directory.EnumerateFiles(Path.Combine(expected, relative)).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();
            var produced = Directory.EnumerateFiles(Path.Combine(capture, relative)).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();
            expect.That(names.SequenceEqual(produced), module + "/" + id + ": file della cattura diversi dal riferimento F3 (" + string.Join(", ", produced) + ")");
            foreach (string? name in names)
            {
                string file = relative + "/" + name;
                string a = File.ReadAllText(Path.Combine(expected, file), Encoding.UTF8).Replace("\r\n", "\n");
                string b = File.ReadAllText(Path.Combine(capture, file), Encoding.UTF8).Replace("\r\n", "\n");
                if (!withTransitions.Contains(file)) { expect.That(a == b, file + " diverso dal riferimento F3 della vista storica"); continue; }
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
        File.WriteAllText(Path.Combine(capture, "..", "palo-verticale-f3-confronto.json"), report.ToJsonString(J.Options));
        expect.That(transitions.Count == 4 && comparer.NotAdmitted == 0, "File con transizioni F4: " + comparer.NotAdmitted + " differenze non ammesse o transizioni assenti");
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

    // ---------------------------------------------------------------- f3-figures

    /// <summary>
    /// The series of the charts as ui/f3-figures freezes them (F3FigureChecks): after commit, «Calcola» and commit, every curve, the
    /// capacity chart in every view and the reference chart of every stratigraphy, with the properties of the Plot of the view.
    /// </summary>
    private static async Task FiguresAsync(string root, string module, string id, Expect expect)
    {
        using var sheet = Open(module, VerticalPileScenarios.FixtureData(root, module, id));
        sheet.Commit(); await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        var states = new Dictionary<string, object> { ["hasResults"] = sheet.HasResults };
        states["allSeries"] = sheet.Curves.Select(c => new { c.Key, Series = SeriesFigure(c.Series) }).ToArray();
        for (int i = 0; i < sheet.CapacityViews.Count; i++)
        {
            sheet.SelectedCapacityView = i;
            var c = sheet.CapacityChart;
            states["capacity-" + i] = PlotFigure(c.Title, c.XLabel, c.YLabel, "", true, true, c.CapacityDepth, null, c.EmptyMessage, c.Series, []);
        }
        for (int i = 0; i < sheet.Surveys.Count; i++)
        {
            sheet.SelectedSurveyIndex = i;
            var r = sheet.ReferenceChart;
            states["reference-" + i] = PlotFigure(r.Title, r.XLabel, r.YLabel, r.Note, false, false, 0, r.XMinimum, "Premere Calcola", r.Series, r.Markers);
        }
        string name = module + "--" + id + ".json";
        string actual = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
        string expected = File.ReadAllText(Path.Combine(root, "tests", "fixtures", "f3-figures", name));
        expect.That(expected.Replace("\r\n", "\n") == actual.Replace("\r\n", "\n"), "Serie dei grafici del ViewModel diverse da f3-figures: " + name);
    }

    // The shapes of F3FigureChecks.SeriesState and PlotState, with the brushes and the dash styles of the Plot of the view.
    private static object SeriesFigure(PlotSeriesData s) => new { s.Name, s.Points, Color = VerticalPileTrace.Colour(s.Color), s.Dashed, s.Highlighted,
        Dashes = s.DashPattern switch { "Dash" => new double[] { 2, 2 }, "Dot" => new double[] { 0, 2 }, _ => null },
        DashOffset = s.DashPattern switch { "Dash" => (double?)1, "Dot" => 0, _ => null } };

    private static object PlotFigure(string title, string xLabel, string yLabel, string note, bool invertY, bool capacity, double capacityDepth, double? xMinimum,
        string emptyMessage, IEnumerable<PlotSeriesData> series, IEnumerable<PlotMarkerData> markers) => new
    {
        Title = title, XLabel = xLabel, YLabel = yLabel, Note = note, InvertY = invertY, Capacity = capacity, CapacityDepth = capacityDepth,
        XMinimum = xMinimum, XMaximum = (double?)null, XTickStep = (double?)null, EmptyMessage = emptyMessage, AxisNumberFormat = (string?)null,
        ScaleX = 1.0, ScaleY = 1.0, Series = series.Select(SeriesFigure).ToArray(),
        Markers = markers.Select(m => new { m.X, m.Y, m.Label, Color = VerticalPileTrace.Colour(m.Color), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius }).ToArray()
    };

    // ---------------------------------------------------------------- contract and derivations

    private static void Contract(Expect expect)
    {
        foreach (string module in new[] { Pile, Micro })
        {
            var sheetModule = new VerticalPileSheetModule(module, Figures);
            expect.That(sheetModule.Info == ModuleDirectory.Get(module) && sheetModule.Reports == new ModuleReportCapabilities(false)
                && sheetModule.Help.GuideId == ModuleHelp.For(module).GuideId, "Modulo " + module);
            expect.That(sheetModule.Persistence.UiStateKeys.SequenceEqual(["visibilita_grafici"]), "Chiavi di stato dichiarate: " + module);
        }
        expect.Throws<ArgumentException>(() => _ = new VerticalPileSheetModule("geo_palo_orizzontale", Figures), "Modulo non dei pali verticali accettato");
        var registry = new ModuleRegistry([new VerticalPileSheetModule(Micro, Figures), new VerticalPileSheetModule(Pile, Figures)]);
        expect.That(registry.SheetModuleIds.SequenceEqual(ModuleDirectory.All.Select(m => m.Id).Where(m => m is Pile or Micro)), "Registro con i due moduli nell'ordine della directory");
        expect.That(VerticalPileSheetViewModel.RecalculationDelay == TimeSpan.FromMilliseconds(450), "Ritardo del ricalcolo automatico");
        using var sheet = Open(Pile, ModuleDirectory.CreateData(Pile));
        expect.That(sheet.CalculationMode == SheetCalculationMode.Background && sheet.AutomaticDelay == TimeSpan.FromMilliseconds(450) && !sheet.IsMicro,
            "Calcolo sullo sfondo con il ritardo della vista storica");
    }

    /// <summary>The derivations moved into ANTHEA.Application (VerticalPilePresentation), with values written by hand.</summary>
    private static void Derivations(Expect expect)
    {
        static JsonArray Curve(double depth, double value) => new(new JsonArray(0, 0), new JsonArray(depth, value));
        var result = new JsonObject
        {
            ["copertura_completa"] = true, ["profondita_massima"] = 12.3,
            ["curve"] = new JsonObject { ["compressione"] = new JsonObject { ["progetto"] = Curve(12.3, 1000) }, ["trazione"] = new JsonObject { ["progetto"] = Curve(12.3, 400) } },
            ["azioni"] = new JsonObject { ["compressione"] = Curve(12.3, 500), ["trazione"] = new JsonArray() },
        };
        var rows = VerticalPilePresentation.Verification(result, true);
        expect.That(rows.Count == 2 && rows[0].SequenceEqual(["Compressione", "500,0", "1.000,0", "50,0%", "Verifica soddisfatta"])
            && rows[1].SequenceEqual(["Trazione", "—", "400,0", "—", "Azione non inserita"]), "Verifica del micropalo");
        result["azioni"]!["compressione"] = Curve(12.3, 1200);
        expect.That(VerticalPilePresentation.Verification(result, true)[0][4] == "Verifica non soddisfatta", "Verifica non soddisfatta");
        expect.That(VerticalPilePresentation.Verification(null, false).Count == 4 && VerticalPilePresentation.Verification(null, false).All(r => r[4] == "Da verificare" && r[2] == "—"),
            "Verifica del palo senza risultato");
        result["copertura_completa"] = false;
        expect.That(VerticalPilePresentation.Verification(result, true).All(r => r[4] == "Verifica incompleta: stratigrafia insufficiente"), "Verifica con copertura incompleta");
        expect.That(VerticalPilePresentation.CapacitySummaryNote(result, false) == "Compressione e trazione · quota disponibile z = 12,3 m · copertura incompleta"
            && VerticalPilePresentation.Warnings("A", result).StartsWith("ATTENZIONE: stratigrafia insufficiente", StringComparison.Ordinal)
            && VerticalPilePresentation.EndValues(new JsonObject { ["generali"] = new JsonObject { ["lunghezza"] = "12.3" } }, result) == "Valori a L non disponibili",
            "Testi con copertura incompleta");
        result["copertura_completa"] = true;
        expect.That(VerticalPilePresentation.Warnings("A", result) == "A"
            && VerticalPilePresentation.EndValues(new JsonObject { ["generali"] = new JsonObject { ["lunghezza"] = "12.3" } }, result) == "Valori a L=12.3 m [kN]"
            && VerticalPilePresentation.CurveChoice("Dren. C · progetto", [[0, 0], [1234.56, 12.3]], result) == "Dren. C · progetto: 1.234,6"
            && VerticalPilePresentation.CurveChoice("Ed trazione", [], result) == "Ed trazione: —", "Testi delle curve");
        expect.That(VerticalPilePresentation.DetailCell("12,34") == "12,3" && VerticalPilePresentation.DetailCell("—") == "—" && VerticalPilePresentation.DetailCell("Granulare") == "Granulare",
            "Celle delle tabelle");
        expect.That(VerticalPilePresentation.EfficiencyFieldVisible("numero_pali_x", "Feld") && !VerticalPilePresentation.EfficiencyFieldVisible("interasse_x", "Feld")
            && VerticalPilePresentation.EfficiencyFieldVisible("interasse_y", "Converse-Labarre") && VerticalPilePresentation.EfficiencyFieldVisible("eta_trazione", "Definita dall'utente")
            && !VerticalPilePresentation.EfficiencyFieldVisible("eta_trazione", "Nessuna riduzione"), "Campi dell'efficienza visibili per metodo");
        var general = new JsonObject { ["tipo_palo"] = "Battuto", ["presenza_falda"] = false, ["considera_punta"] = true };
        expect.That(VerticalPilePresentation.GeneralFieldStates(general, false).SequenceEqual([("sottotipo_palo_battuto", true), ("profondita_falda", false), ("considera_sottospinta", false)])
            && VerticalPilePresentation.GeneralFieldStates(general, true).SequenceEqual([("percentuale_punta", true)])
            && VerticalPilePresentation.NormativeFieldStates(general, true).SequenceEqual([("sicurezza_base", true)]) && VerticalPilePresentation.NormativeFieldStates(general, false).Count == 0,
            "Campi attivi per opzione");
        expect.That(VerticalPilePresentation.CapacityEmptyMessage(new JsonObject { ["generali"] = new JsonObject { ["lunghezza"] = "" } }) == "Lunghezza: inserire un numero valido"
            && VerticalPilePresentation.CapacityEmptyMessage(new JsonObject { ["generali"] = new JsonObject { ["lunghezza"] = "8" } }) == "Completare i dati · calcolo automatico",
            "Messaggio del grafico senza curve");
        var data = new JsonObject { ["stratigrafie"] = new JsonArray(new JsonArray(), new JsonArray()) };
        var pile = VerticalPilePresentation.Figures(data, false, "C", "R");
        var micro = VerticalPilePresentation.Figures(data, true, "C", "R");
        expect.That(pile.Select(f => f.Caption).SequenceEqual(["C", "R", "Profilo del sondaggio 1", "Profilo del sondaggio 2", "Profilo stratigrafico"])
            && pile.Select(f => f.Height).SequenceEqual([750, 750, 900, 900, 750]) && pile.Select(f => f.ReportIndex).SequenceEqual([null, null, 0, 1, null]),
            "Figure del report del palo");
        expect.That(micro.Select(f => f.Caption).SequenceEqual(["C", "Abachi Bustamante–Doix del sondaggio 1", "Abachi Bustamante–Doix del sondaggio 2", "Profilo del sondaggio 1", "Profilo del sondaggio 2", "Profilo stratigrafico"])
            && micro.Where(f => f.Kind == VerticalPileFigureKind.Profile).All(f => f.Width == 720 && f.Height == 600)
            && micro.Where(f => f.Kind == VerticalPileFigureKind.Abacus).All(f => f.Category == "grafico_nq" && f.ReportIndex is null), "Figure del report del micropalo");
    }

    // ---------------------------------------------------------------- keys of interface state

    /// <summary>Curves, views, tables and stratigraphies chosen: only «visibilita_grafici» is written, with Modified and without recalculation.</summary>
    private static async Task StateKeysAsync(string root, Expect expect)
    {
        var service = new CountingService(Pile);
        using var sheet = Open(Pile, VerticalPileScenarios.FixtureData(root, Pile, "battuto-falda-sondaggi"), service);
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults, "Calcolo all'apertura");
        var before = (JsonObject)sheet.Data.DeepClone();
        int modified = 0, calls = service.Calls;
        sheet.Modified += (_, _) => modified++;
        sheet.SelectedCapacityView = 2; sheet.SelectedSurveyIndex = 1; sheet.SelectedTableIndex = 3; sheet.SelectedCapacityView = 0;
        expect.That(JsonNode.DeepEquals(before, sheet.Data) && modified == 0, "Viste, stratigrafie e tabelle scritte nel documento");
        sheet.CurveChoices[0].IsChecked = false;
        sheet.HideAllCurves();
        sheet.ShowAllCurves();
        string[] changed = before.Select(p => p.Key).Concat(sheet.Data.Select(p => p.Key)).Distinct().Where(k => !JsonNode.DeepEquals(before[k], sheet.Data[k])).ToArray();
        expect.That(changed.SequenceEqual(VerticalPileSheetViewModel.UiStateKeys) && modified == 3 && sheet.HasResults && service.Calls == calls, "Curve: chiavi scritte diverse da quelle dichiarate (" + string.Join(", ", changed) + ")");
        sheet.ShowAllCurves();
        expect.That(modified == 3, "Curve già tutte visibili: Modified senza modifiche");
    }

    // ---------------------------------------------------------------- barriers

    /// <summary>A calculation superseded while it runs is never published; only the calculation of the current revision is.</summary>
    private static async Task CurrentRevisionAsync(string root, Expect expect)
    {
        var engine = new HeldModuleService(Pile);
        try
        {
            using var sheet = Open(Pile, VerticalPileScenarios.FixtureData(root, Pile, "regressione-palo_storico_0"), engine);
            expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.Result!["lunghezza_palo"]!.GetValue<double>() == 12.3, "Calcolo all'apertura");
            engine.Hold();
            sheet.GeneralForm["lunghezza"].EditText = "13";
            var first = sheet.PendingCalculation;
            await engine.EnteredAsync();
            expect.That(sheet.IsBusy && sheet.Status == VerticalPilePresentation.StatusRunning && !sheet.HasResults, "Calcolo sullo sfondo in corso");
            sheet.GeneralForm["lunghezza"].EditText = "14";
            var second = sheet.PendingCalculation;
            expect.That(!sheet.IsBusy && sheet.CalculationState == CalculationState.Pending && sheet.Status == VerticalPilePresentation.StatusPending, "Revisione nuova in attesa");
            engine.Release();
            expect.That(!await Expect.Within(first) && await Expect.Within(second), "Pubblicato il calcolo di una revisione superata");
            expect.That(sheet.Result!["lunghezza_palo"]!.GetValue<double>() == 14 && sheet.Status == VerticalPilePresentation.StatusCompleted, "Risultato della revisione corrente");
            engine.Hold();
            sheet.GeneralForm["diametro"].EditText = "1,05";
            await engine.EnteredAsync();
            var explicitCalculation = sheet.CalculateAsync();
            expect.That(sheet.Status == VerticalPilePresentation.StatusRunning && !explicitCalculation.IsCompleted, "«Calcola» durante un calcolo: stato mostrato");
            engine.Release();
            expect.That(await Expect.Within(explicitCalculation) && sheet.HasResults && sheet.Result!["diametro_nq"]!.GetValue<double>() == 1.05, "«Calcola» durante un calcolo");
        }
        finally { engine.Release(); }
    }

    /// <summary>Closed during a calculation: nothing is published, nothing is written, the edits are ignored.</summary>
    private static async Task ClosingAsync(string root, Expect expect)
    {
        var engine = new HeldModuleService(Micro);
        try
        {
            var sheet = Open(Micro, VerticalPileScenarios.FixtureData(root, Micro, "irs-punta-inclinato"), engine);
            expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults, "Calcolo all'apertura");
            engine.Hold();
            sheet.GeneralForm["lunghezza"].EditText = "15";
            var pending = sheet.PendingCalculation;
            await engine.EnteredAsync();
            string data = sheet.Data.ToJsonString();
            sheet.Dispose();
            engine.Release();
            expect.That(!await Expect.Within(pending) && !sheet.HasResults && sheet.Data.ToJsonString() == data, "Chiusura durante il calcolo: risultato pubblicato o dati scritti");
            sheet.GeneralForm["lunghezza"].EditText = "99";
            sheet.Surveys[0].Rows[0]["spessore"] = "7";
            sheet.AddSurvey(); sheet.AddLayer(0); sheet.ShowAllCurves();
            expect.That(sheet.Data.ToJsonString() == data, "Modifica dopo la chiusura scritta nel documento");
            expect.Throws<ObjectDisposedException>(sheet.Commit, "Commit dopo la chiusura");
            expect.Throws<ObjectDisposedException>(() => _ = sheet.CalculateAsync(), "Calcolo dopo la chiusura");
        }
        finally { engine.Release(); }
    }

    /// <summary>A previous revision: edits refused (forms on detached copies, cells, commands), curves and calculation allowed.</summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        using var sheet = Open(Pile, VerticalPileScenarios.FixtureData(root, Pile, "battuto-falda-sondaggi"));
        expect.That(await Expect.Within(sheet.PendingCalculation), "Calcolo all'apertura");
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        sheet.IsReadOnly = true;
        string before = sheet.Data.ToJsonString();
        sheet.GeneralForm["diametro"].EditText = "2";
        sheet.GeneralForm["tipo_palo"].SelectedChoice = "Trivellato";
        sheet.EfficiencyForm["metodo"].SelectedChoice = "Feld";
        sheet.NormativeForm["sicurezza_base"].EditText = "1,9";
        sheet.ResetNormative();
        sheet.Surveys[0].Rows[0]["spessore"] = "9";
        sheet.AddLayer(0); sheet.RemoveLayer(0, sheet.Surveys[0].Rows[0]); sheet.AddSurvey(); sheet.RemoveSurvey(1); sheet.CopySurvey(0, -1);
        await sheet.CopySurveyIntoAsync();
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && sheet.HasResults && !sheet.CanEdit, "Sola lettura: modifica scritta nel documento");
        expect.That(sheet.Surveys[0].Rows[0].Values.S("spessore") == "4", "Sola lettura: cella rifiutata non ripristinata");
        sheet.CurveChoices[0].IsChecked = false;
        // Regola T6 (F5.15, F5-17): le curve mostrate restano nella copia in memoria del foglio, senza Modified.
        expect.That(modified == 0 && sheet.Data["visibilita_grafici"] is JsonObject, "Sola lettura: curve mostrate senza Modified");
        expect.That(await Expect.Within(sheet.CalculateAsync()) && sheet.HasResults, "Sola lettura: calcolo");
        sheet.IsReadOnly = false;
        expect.That(sheet.CanEdit && sheet.GeneralForm["diametro"].Text == "0.6", "Ritorno alla modifica: testi");
        sheet.GeneralForm["diametro"].EditText = "0,7";
        expect.That(sheet.Data["generali"].S("diametro") == "0,7" && modified == 1 && !sheet.HasResults, "Ritorno alla modifica: il campo non scrive");
    }

    /// <summary>Data replaced from outside: built again as at opening (lazy migrations), no Modified, recalculated after the delay.</summary>
    private static async Task ReloadAsync(string root, Expect expect)
    {
        using var sheet = Open(Pile, VerticalPileScenarios.FixtureData(root, Pile, "regressione-palo_storico_0"));
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults, "Calcolo all'apertura");
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var legacy = VerticalPileScenarios.FixtureData(root, Pile, "battuto-falda-sondaggi");
        sheet.Reload(legacy);
        expect.That(modified == 0 && !sheet.HasResults && sheet.Status == VerticalPilePresentation.StatusPending && sheet.Surveys.Count == 2
            && sheet.GeneralForm["tipo_palo"].SelectedChoice == "Battuto" && sheet.TableTitles.Count == 0 && sheet.CurveChoices.Count == 0
            && sheet.CapacityChart.Title == VerticalPileCapacityChart.InitialTitle, "Reload: campi, stratigrafie o stato");
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults && sheet.TableTitles.Count > 0, "Reload: ricalcolo automatico");
        sheet.Commit();
        using var reopened = Open(Pile, legacy);
        reopened.Commit();
        expect.That(sheet.Data.ToJsonString() == reopened.Data.ToJsonString() && modified == 0, "Reload: dati diversi da una riapertura");
        var old = VerticalPileScenarios.FixtureData(root, Pile, "legacy-nq-gamma");
        sheet.Reload(old);
        using var migrated = Open(Pile, old);
        expect.That(old["efficienza"] is null && sheet.Data["efficienza"] is JsonObject && sheet.Data.ToJsonString() == migrated.Data.ToJsonString(),
            "Reload: migrazione pigra dell'archivio diversa da un'apertura");
    }

    /// <summary>
    /// The injection of the micropile changed with a result: the historic view fails (VerticalPileCharts.Reference with the result of the
    /// other injection, F5.6 document); the ViewModel writes the initial α and recalculates.
    /// </summary>
    private static async Task InjectionAsync(string root, Expect expect)
    {
        using var sheet = Open(Micro, VerticalPileScenarios.FixtureData(root, Micro, "regressione-micropalo_igu_0_nessuna-riduzione"));
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults, "Calcolo all'apertura");
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        sheet.GeneralForm["tipo_iniezione"].SelectedChoice = "IRS";
        var layers = sheet.Data.Array("stratigrafie")[0]!.AsArray();
        expect.That(modified == 1 && !sheet.HasResults && layers.All(l => l.S("alpha") == VerticalPilePreview.InitialAlpha(l.S("terreno"), "IRS")),
            "Cambio dell'iniezione con un risultato");
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults, "Ricalcolo dopo il cambio dell'iniezione");
    }

    /// <summary>Confirmations and the dialog of «Copia in…»/«Copia da…»: an answer no keeps the data; a cancelled dialog copies nothing.</summary>
    private static async Task SurveyCommandsAsync(string root, Expect expect)
    {
        var confirmations = new ScriptedConfirmations().Then(false).Then(false);
        var dialogs = new ScriptedDialogs()
            .Then(d => ((SurveyCopyDialogViewModel)d).Dismiss())
            .Then(d => { var copy = (SurveyCopyDialogViewModel)d; copy.Selected = copy.Choices[^1]; copy.Confirm(); })
            .Then(d => { var copy = (SurveyCopyDialogViewModel)d; copy.Selected = copy.Choices[0]; copy.Confirm(); });
        var messages = new ScriptedMessages();
        using var sheet = Open(Pile, VerticalPileScenarios.FixtureData(root, Pile, "battuto-falda-sondaggi"), null, Scripted.Services(messages, confirmations, null, dialogs));
        string before = sheet.Data.ToJsonString();
        sheet.RemoveSurvey(0);
        expect.That(sheet.Data.ToJsonString() == before && confirmations.Asked.SequenceEqual(["Stratigrafia: Eliminare la stratigrafia 1 e tutti i suoi strati?"]), "Eliminazione rifiutata");
        sheet.SelectedSurveyIndex = 1;
        await sheet.CopySurveyIntoAsync();
        var shown = (SurveyCopyDialogViewModel)dialogs.Shown[0];
        expect.That(sheet.Data.ToJsonString() == before && shown.Title == "Copia stratigrafia in…" && shown.Question == "Copia gli strati della stratigrafia 2 in:"
            && shown.Choices.Select(c => c.Label).SequenceEqual(["Stratigrafia 1", "Nuova stratigrafia"]), "Dialogo «Copia in…» annullato");
        await sheet.CopySurveyIntoAsync();
        var surveys = sheet.Data.Array("stratigrafie");
        expect.That(surveys.Count == 3 && JsonNode.DeepEquals(surveys[2], surveys[1]) && sheet.SelectedSurveyIndex == 2, "Copia in una stratigrafia nuova");
        await sheet.CopySurveyFromAsync();
        expect.That(confirmations.Asked.Last() == "Copia stratigrafia: Sostituire tutti gli strati della stratigrafia 3 con una copia della stratigrafia 1?"
            && !JsonNode.DeepEquals(surveys[2], surveys[0]), "«Copia da…» con sostituzione rifiutata");
        var none = ModuleDirectory.CreateData(Pile);
        none["stratigrafie"] = new JsonArray();
        using var empty = Open(Pile, none, null, Scripted.Services(messages));
        await empty.CopySurveyIntoAsync();
        expect.That(empty.SelectedSurveyIndex == -1 && messages.Shown.Last() == ("", "Selezionare una stratigrafia.", false), "Copia senza stratigrafie");
    }

    // ---------------------------------------------------------------- services of the checks

    /// <summary>Figures of the report as images of the size of the WPF ones: only the size enters the report (extent of the images).</summary>
    private sealed class SameSizeFigures : IVerticalPileFigureRenderer
    {
        public IReadOnlyList<ReportFigure> Render(VerticalPileFigureRequest request) =>
            request.Figures.Select(f => new ReportFigure(f.Caption, Png(f.Width, f.Height), f.Category, f.ReportIndex)).ToArray();

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

    /// <summary>The module service, counted.</summary>
    private sealed class CountingService(string module) : IModuleService
    {
        private readonly IModuleService inner = ModuleServices.Get(module);
        private int calls;
        internal int Calls => Volatile.Read(ref calls);
        public string ModuleId => inner.ModuleId;
        public JsonObject Prepare(JsonObject source) => inner.Prepare(source);
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Interlocked.Increment(ref calls);
            return inner.Calculate(source, cancellation);
        }
    }

    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException("Copia non prevista.");
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException("Report non previsto.");
    }
}
