using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.PileGroup;
using Anthea.Testing.Normalization;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of the pile group (F5.4 pilot) against its historic view, without WPF. This is the evidence of equivalence
/// that survives the archiving of the historic view:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/PileGroupTrace.cs (the path of the F3 capture for each fixture of the module, edits of
/// every kind, read-only) gives the trace of the historic view frozen in tests/fixtures/f5-palificata/storica, line by line:
/// data in memory order, Modified, result, texts, tables, fields and drawing after every step;</item>
/// <item>for each F3 fixture of the module the data after opening, visit, calculation, export and reopening, the result, the
/// status and the text and structure of the report are byte-identical to the F3 reference of the historic view;</item>
/// <item>module and keys of interface state; read-only; Reload; closing during the delay; synchronous calculation through the
/// injected calculation service (Q8 a).</item>
/// </list>
/// One "UI" thread (SingleThreadContext): the automatic recalculation after 180 ms runs there, as on the dispatcher.
/// </summary>
internal static class PileGroupSheetChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    private static readonly PileGroupSheetModule Module = new();

    private static PileGroupSheetViewModel Open(JsonObject data, IModuleService? service = null) =>
        (PileGroupSheetViewModel)Module.CreateSheet(new SheetContext((JsonObject)data.DeepClone(), false, new Host(), Scripted.Services(),
            service ?? ModuleServices.Get(PileGroupScenarios.Module), new CalculationLimiter(1)));

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "palificata");
            Directory.CreateDirectory(traces);
            foreach (var scenario in PileGroupScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                PileGroupTrace.Write(Path.Combine(traces, scenario.Name + ".txt"), lines);
                string stored = Path.Combine(PileGroupScenarios.Reference(root), scenario.Name + ".txt");
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                string? difference = PileGroupTrace.FirstDifference(PileGroupTrace.Read(stored), lines);
                expect.That(difference is null, "Palificata MVVM diversa dalla vista storica, scenario " + scenario.Name + ", " + difference);
            }
            var fixtures = PileGroupScenarios.Fixtures(root);
            expect.That(fixtures.Count == 8, "Fixture F3 della palificata: " + fixtures.Count);
            foreach (string id in fixtures) await F3Async(root, id, expect);
            Contract(expect);
            StateKeys(root, expect);
            await ReadOnlyAsync(root, expect);
            await ReloadAsync(root, expect);
            await ClosingAsync(root, expect);
            await InjectedAsync(root, expect);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, PileGroupScenario scenario)
    {
        var lines = new List<string>();
        var side = new Side(Open(PileGroupScenarios.FixtureData(root, scenario.Fixture)));
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
        finally { side.Sheet.Dispose(); }
        return lines;
    }

    /// <summary>The ViewModel driven as its view would drive it, step by step.</summary>
    private sealed class Side
    {
        private int modified;

        internal Side(PileGroupSheetViewModel sheet) => Attach(sheet);

        internal PileGroupSheetViewModel Sheet { get; private set; } = null!;

        private void Attach(PileGroupSheetViewModel sheet)
        {
            Sheet = sheet;
            modified = 0;
            sheet.Modified += (_, _) => modified++;
        }

        private FieldViewModel Field(string key) => Sheet.Field(key);

        internal async Task ApplyAsync(PileGroupStep step)
        {
            switch (step.Kind)
            {
                case PileGroupStep.Commit: Sheet.Commit(); break;
                case PileGroupStep.Calculate: await Expect.Within(Sheet.CalculateAsync()); break;
                case PileGroupStep.Wait: await Expect.Within(Sheet.PendingCalculation); break;
                case PileGroupStep.ReadOnly: Sheet.IsReadOnly = true; break;
                case PileGroupStep.Reopen:
                    var data = Sheet.Data;
                    Sheet.Dispose();
                    Attach(Open(data));
                    break;
                case PileGroupStep.Field: Field(step.Key!).EditText = step.Text!; break;
                case PileGroupStep.Option:
                    var option = Field(step.Key!);
                    option.IsChecked = !option.IsChecked;
                    break;
                case PileGroupStep.Layout: Sheet.SelectedLayout = step.Text; break;
                case PileGroupStep.Cap: Sheet.SelectedCapShape = step.Text; break;
                case PileGroupStep.Spacing: Sheet.SpacingDirectionIndex = step.Index; break;
                case PileGroupStep.Pile:
                    var row = Sheet.Piles[step.Index];
                    if (step.Key == "Id") row.Id = step.Text!;
                    else if (step.Key == "X") row.X = step.Text!;
                    else if (step.Key == "Y") row.Y = step.Text!;
                    else throw new ArgumentException("Colonna sconosciuta: " + step.Key);
                    break;
                case PileGroupStep.AddPile: Sheet.Piles.Add(new PileRowViewModel()); break;
                case PileGroupStep.RemovePile: Sheet.Piles.RemoveAt(step.Index); break;
                case PileGroupStep.CancelEdit: Sheet.CoordinateEditCancelled(); break;
                // As the grid of the view: a row that is not there cannot be selected (no result: an empty comparison).
                case PileGroupStep.Select: if (step.Index < Sheet.Comparison.Count) Sheet.SelectedComparison = Sheet.Comparison[step.Index]; break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        internal PileGroupTraceState State()
        {
            var s = Sheet;
            var trace = new PileGroupTraceState
            {
                Data = s.Data, Modified = modified, Result = s.Result, Editable = s.CanEdit,
                Title = s.Title, State = s.State, Minimum = s.Minimum, Mean = s.Mean, Maximum = s.Maximum, Reduction = s.Reduction,
                Geometry = s.Geometry, Source = s.Source, Notes = s.Notes,
                Comparison = s.Comparison.Select(r => new[] { r.Direction, r.MethodName, r.Minimum, r.Mean, r.Maximum, r.Reduction, r.Status }).ToList(),
                SelectedComparison = s.SelectedComparison is null ? -1 : s.Comparison.ToList().IndexOf(s.SelectedComparison),
                Details = s.Details.Select(d => new object?[] { d.Pile, d.Row, d.Parallel, d.Transverse, d.Beta, d.Alpha, d.Factor, d.Reduction }).ToList(),
                Piles = s.Piles.Select(p => new[] { p.Id, p.X, p.Y }).ToList(),
                CoordinatesEditable = s.CanEditCoordinates, CanAddPiles = s.CanEditCoordinates, CoordinateHelp = s.CoordinateHelp,
                Layout = s.SelectedLayout, GeneratorNote = s.GeneratorNote,
                Diameter = s.DiameterForm["diametro"].Text,
                Directions = new[] { "X", "Y", "X-", "Y-", "custom" }.Select(k => PileGroupTrace.DirectionLine(k, s.Field(k).IsChecked)).ToList(),
                CustomAngle = s.CustomDirectionForm["angolo_custom"].Text, CustomAngleEnabled = s.CustomDirectionForm["angolo_custom"].IsEnabled,
                CapShape = s.SelectedCapShape, CapEdge = s.CapForm["bordo_d"].Text,
                Extrapolations = s.ExtrapolationsForm["estrapolazioni"].IsChecked, ProjectedRows = s.ProjectedRowsForm["file_proiettate"].IsChecked,
                SpacingDirection = s.SpacingDirection, SpacingIndex = s.SpacingDirectionIndex,
                ParallelSpacing = s.SpacingForm["interasse_parallelo"].Text, TransverseSpacing = s.SpacingForm["interasse_trasversale"].Text,
                PlanShown = s.Plan is not null, PlanSelected = s.Plan?.Selected, PlanCap = s.Plan?.Cap,
            };
            foreach (var field in (s.GeneratorForm?.Fields ?? []).Concat(s.RotationForm?.Fields ?? []))
                trace.Generator.Add(field.Kind == FieldKind.Bool ? PileGroupTrace.OptionLine(field.Key, field.Label, field.IsChecked)
                    : PileGroupTrace.FieldLine(field.Key, field.Label, field.Text, field.IsEnabled));
            return trace;
        }
    }

    // ---------------------------------------------------------------- F3 reference

    /// <summary>
    /// The path of ui/f3-equivalence through the ViewModel (opened, visited, calculated, result, report, exported, reopened), compared
    /// with the F3 reference of the historic view in the canonical form of the capture.
    /// </summary>
    private static async Task F3Async(string root, string id, Expect expect)
    {
        string folder = Path.Combine(root, "tests", "fixtures", "f3", "expected", "sheets", PileGroupScenarios.Module, id);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        string Canonical(JsonNode? node) => CanonicalJson.Serialize(node, new GuidMap(), normalizer).Replace("\r\n", "\n");
        string Expected(string name) => File.ReadAllText(Path.Combine(folder, name), Encoding.UTF8).Replace("\r\n", "\n");
        void Same(string name, string actual) => expect.That(actual == Expected(name), id + ": " + name + " diverso dal riferimento F3 della vista storica");
        using var sheet = Open(PileGroupScenarios.FixtureData(root, id));
        sheet.Commit(); Same("opened.json", Canonical(sheet.Data));
        sheet.Commit(); Same("visited.json", Canonical(sheet.Data));
        await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        Same("calculated-data.json", Canonical(sheet.Data)); Same("result.json", Canonical(sheet.Result));
        var status = new JsonObject { ["hasResults"] = sheet.HasResults };
        try
        {
            byte[] report = sheet.Report.BuildReport("Fixture F3 · " + PileGroupScenarios.Module + " · " + id, new HashSet<string>());
            Same("report.txt", DocxText.Extract(report, normalizer, new GuidMap()).Replace("\r\n", "\n"));
            Same("report-structure.json", Canonical(Structure(report)));
            status["report"] = "available";
        }
        catch (InvalidOperationException error) { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
        Same("exported-data.json", Canonical(sheet.Data)); Same("status.json", Canonical(status));
        using var reopened = Open(sheet.Data);
        reopened.Commit(); Same("reopened.json", Canonical(reopened.Data));
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
        expect.That(Module.Info == ModuleDirectory.Get(PileGroupScenarios.Module) && Module.Reports == new ModuleReportCapabilities(false)
            && Module.Help.GuideId == ModuleHelp.For(PileGroupScenarios.Module).GuideId, "Modulo della palificata");
        expect.That(Module.Persistence.UiStateKeys.SequenceEqual(["metodo", "direzione_selezionata", "angolo"]), "Chiavi di stato dichiarate");
        var registry = new ModuleRegistry([Module]);
        expect.That(registry.SheetModuleIds.SequenceEqual([PileGroupScenarios.Module]) && ReferenceEquals(registry.SheetModule(PileGroupScenarios.Module), Module),
            "Registro con la palificata");
        expect.That(PileGroupSheetViewModel.RecalculationDelay == TimeSpan.FromMilliseconds(180), "Ritardo del ricalcolo automatico");
    }

    /// <summary>Selecting every result of the comparison writes only the keys of interface state declared by the module.</summary>
    private static void StateKeys(string root, Expect expect)
    {
        using var sheet = Open(PileGroupScenarios.FixtureData(root, "quinconce-direzioni"));
        sheet.Commit();
        var before = (JsonObject)sheet.Data.DeepClone();
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var rows = sheet.Comparison.ToArray();
        foreach (var row in rows) sheet.SelectedComparison = row;
        string[] changed = before.Select(p => p.Key).Where(k => !JsonNode.DeepEquals(before[k], sheet.Data[k])).ToArray();
        expect.That(rows.Length == 24 && modified == rows.Length && sheet.Data.Count == before.Count && sheet.HasResults
            && changed.Length > 0 && changed.All(Module.Persistence.UiStateKeys.Contains),
            "Selezione nel confronto: chiavi scritte diverse da quelle dichiarate (" + string.Join(", ", changed) + ")");
    }

    // ---------------------------------------------------------------- read-only

    /// <summary>
    /// A previous revision: the user's changes are refused (read-only forms, D4; layout, cap, spacing direction, cells, the
    /// cancelled edit), the selection in the comparison writes the selected result and raises Modified, the calculation stays.
    /// </summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        using var sheet = Open(PileGroupScenarios.FixtureData(root, "legacy-pile-group"));
        sheet.Commit();
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        sheet.DiameterForm["diametro"].EditText = "1,0";
        string typed = sheet.DiameterForm["diametro"].Text;
        await Expect.Within(sheet.CalculateAsync());
        sheet.IsReadOnly = true;
        string before = sheet.Data.ToJsonString();
        var forms = sheet.Forms.ToArray();
        expect.That(!sheet.CanEdit && !sheet.CanEditCoordinates && sheet.DiameterForm["diametro"].Text == typed && modified == 1 && sheet.GeneratorForm is null
            && sheet.RotationForm is null && forms.Length == 8
            && forms.All(f => f.IsReadOnly && f.Fields.All(field => field.IsInputReadOnly && !field.CanChange)), "Sola lettura: stato, form o testi mostrati");
        sheet.DiameterForm["diametro"].EditText = "2";
        sheet.ExtrapolationsForm["estrapolazioni"].IsChecked = true;
        sheet.CustomDirectionForm["custom"].IsChecked = true;
        sheet.SpacingForm["interasse_parallelo"].EditText = "3";
        sheet.SelectedLayout = "Rettangolare";
        sheet.SelectedCapShape = "Rettangolare";
        sheet.SpacingDirectionIndex = 2;
        sheet.Piles[0].X = "99";
        sheet.CoordinateEditCancelled();
        expect.That(sheet.Data.ToJsonString() == before && modified == 1 && sheet.HasResults, "Sola lettura: modifica scritta nel documento");
        expect.That(sheet.SelectedLayout == "Generica" && sheet.SelectedCapShape == "Contorno palificata" && sheet.SpacingDirectionIndex == 0
            && sheet.Piles[0].X == "-3", "Sola lettura: scelta o cella accettata");
        sheet.SelectedComparison = sheet.Comparison[7];
        // Regola T6 (F5.15, F5-17): la selezione resta nella copia in memoria del foglio, senza Modified.
        expect.That(modified == 1 && sheet.Data["metodo"]!.ToString() == "1" && sheet.Data.S("direzione_selezionata") == "Y"
            && sheet.Result!["selezionato"]!["Method"]!.ToString() == "1" && sheet.Title.StartsWith("AASHTO", StringComparison.Ordinal),
            "Sola lettura: selezione nel confronto");
        expect.That(await Expect.Within(sheet.CalculateAsync()) && sheet.HasResults && sheet.SelectedComparison == sheet.Comparison[7], "Sola lettura: calcolo");
        sheet.IsReadOnly = false;
        expect.That(sheet.CanEdit && sheet.CanEditCoordinates && sheet.DiameterForm["diametro"].Text == typed && forms.All(f => !f.IsReadOnly)
            && ReferenceEquals(forms[0], sheet.DiameterForm), "Ritorno alla modifica: stato, form o testi mostrati");
        sheet.DiameterForm["diametro"].EditText = "1,5";
        expect.That(sheet.Data["diametro"]!.GetValue<double>() == 1.5 && modified == 2 && !sheet.HasResults, "Ritorno alla modifica: il campo non scrive");
    }

    // ---------------------------------------------------------------- reload

    /// <summary>Data replaced from outside: built again as at opening (migration of the archive), no Modified, recalculated after the delay.</summary>
    private static async Task ReloadAsync(string root, Expect expect)
    {
        using var sheet = Open(PileGroupScenarios.FixtureData(root, "default"));
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        sheet.DiameterForm["diametro"].EditText = "3";
        var legacy = PileGroupScenarios.FixtureData(root, "legacy-pile-group");
        sheet.Reload(legacy);
        expect.That(modified == 1 && !sheet.HasResults && sheet.State == "Aggiornamento automatico…" && sheet.SelectedLayout == "Generica"
            && sheet.DiameterForm["diametro"].Text == "1" && sheet.Piles.Count == 9 && sheet.CanEditCoordinates, "Reload: campi, tabella o stato");
        expect.That(sheet.Data["versione"]!.GetValue<int>() == 2 && legacy["versione"]!.GetValue<int>() == 1, "Reload: migrazione dell'archivio o origine modificata");
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults, "Reload: ricalcolo automatico");
        sheet.Commit();
        using var reopened = Open(legacy);
        reopened.Commit();
        expect.That(sheet.Data.ToJsonString() == reopened.Data.ToJsonString() && modified == 1, "Reload: dati diversi da una riapertura");
    }

    // ---------------------------------------------------------------- closing

    /// <summary>Closed during the delay of the automatic recalculation: nothing runs, nothing is written, the edits are ignored.</summary>
    private static async Task ClosingAsync(string root, Expect expect)
    {
        var service = new CountingService();
        var sheet = Open(PileGroupScenarios.FixtureData(root, "default"), service);
        int calls = service.Calls;
        sheet.DiameterForm["diametro"].EditText = "1,1";
        string data = sheet.Data.ToJsonString();
        var pending = sheet.PendingCalculation;
        expect.That(!pending.IsCompleted && sheet.CalculationState == CalculationState.Pending, "Ricalcolo automatico non in attesa");
        sheet.Dispose();
        expect.That(!await Expect.Within(pending) && service.Calls == calls && sheet.Data.ToJsonString() == data && !sheet.HasResults,
            "Chiusura durante il ritardo: calcolo eseguito o dati scritti");
        sheet.SelectedLayout = "Quinconce";
        sheet.Piles[0].X = "7";
        expect.That(sheet.Data.ToJsonString() == data && service.Calls == calls, "Modifica dopo la chiusura");
        expect.Throws<ObjectDisposedException>(sheet.Commit, "Commit dopo la chiusura");
    }

    // ---------------------------------------------------------------- injected calculation

    /// <summary>The calculation service of the context, on the UI thread: at opening, «Calcola», the delay, Commit, errors.</summary>
    private static async Task InjectedAsync(string root, Expect expect)
    {
        var service = new CountingService();
        using var sheet = Open(PileGroupScenarios.FixtureData(root, "default"), service);
        int ui = Environment.CurrentManagedThreadId;
        expect.That(service.Calls == 1 && service.Threads.All(t => t == ui) && sheet.HasResults && !sheet.IsBusy,
            "Calcolo all'apertura non sincrono o fuori dal servizio del contesto");
        var calculated = sheet.CalculateAsync();
        expect.That(calculated.IsCompleted && await calculated && service.Calls == 1, "Calcola non sincrono o cache della sessione non usata");
        sheet.DiameterForm["diametro"].EditText = "1,2";
        expect.That(!sheet.HasResults && sheet.State == "Aggiornamento automatico…" && service.Calls == 1, "Modifica senza risultati azzerati o calcolo immediato");
        expect.That(await Expect.Within(sheet.PendingCalculation) && service.Calls == 2 && service.Threads.All(t => t == ui) && sheet.HasResults,
            "Ricalcolo automatico fuori dal thread dell'interfaccia");
        sheet.DiameterForm["diametro"].EditText = "1,3";
        var pending = sheet.PendingCalculation;
        sheet.Commit();
        expect.That(service.Calls == 3 && sheet.HasResults && !await Expect.Within(pending), "Commit con un ricalcolo in attesa");
        service.Fail = true;
        sheet.DiameterForm["diametro"].EditText = "1,4";
        sheet.Commit();
        expect.That(!sheet.HasResults && sheet.CalculationState == CalculationState.Failed && sheet.State == "Dati da completare: motore di prova"
            && sheet.Comparison.Count == 0 && sheet.Plan is { Selected: null }, "Errore del calcolo");
        service.Fail = false;
        sheet.Commit();
        expect.That(sheet.HasResults && service.Calls == 5, "Commit senza risultati non ricalcola");
    }

    /// <summary>The module service of the pile group, counted (calls and threads); <see cref="Fail"/> makes it throw.</summary>
    private sealed class CountingService : IModuleService
    {
        private readonly IModuleService inner = ModuleServices.Get(PileGroupScenarios.Module);
        internal int Calls { get; private set; }
        internal List<int> Threads { get; } = [];
        internal bool Fail { get; set; }
        public string ModuleId => inner.ModuleId;
        public JsonObject Prepare(JsonObject source) => inner.Prepare(source);
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
        {
            Calls++;
            Threads.Add(Environment.CurrentManagedThreadId);
            if (Fail) throw new ArgumentException("motore di prova");
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
