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
using Anthea.Presentation.Modules.ShallowFoundation;
using Anthea.Presentation.Services;
using Anthea.Testing.Comparison;
using Anthea.Testing.Normalization;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of the shallow foundations (F5.9b) against its historic view, without WPF. This is the evidence of equivalence
/// that survives the archiving of the historic view:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/ShallowFoundationTrace.cs (the path of the F3 capture for each fixture of the module, edits
/// of every kind, groups and collapsed inputs, surveys and layers, read-only) gives the trace of the historic view frozen in
/// tests/fixtures/f5-fondazioni/storica, line by line;</item>
/// <item>for each F3 fixture of the module the data after opening, visit (the groups opened), calculation, export and reopening, the
/// result, the status and the text and structure of the report are byte-identical to the F3 reference of the historic view;</item>
/// <item>module and keys of interface state; read-only from the context; Reload; closing; synchronous calculation through the
/// injected calculation service (Q8 a); the text of the results without the approved library.</item>
/// </list>
/// One "UI" thread (SingleThreadContext), as on the dispatcher.
/// </summary>
internal static class ShallowFoundationSheetChecks
{
    internal static int AuditLibraryVersion(string root, string output) => SingleThreadContext.Run(async _ =>
    {
        var culture = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            using var sheet = Open(ShallowFoundationScenarios.FixtureData(root, "confirmed-actions"));
            await sheet.CalculateAsync();
            var result = sheet.Result ?? throw new InvalidOperationException("Risultato dell'audit assente.");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "foundation-current.json"), result.ToJsonString(new() { WriteIndented = true }));
            var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "lib/Checker/manifest.json")))!;
            string version = manifest["assemblies"]!.AsArray().Single(a => a!["file"]!.ToString() == "GPCChecker.Geotechnics.dll")!["assemblyVersion"]!.ToString();
            var projected = result.DeepClone().AsObject(); projected["library_version"] = version;
            File.WriteAllText(Path.Combine(output, "foundation-version-only.json"), projected.ToJsonString(new() { WriteIndented = true }));
            var reference = PileGroupTrace.Read(Path.Combine(ShallowFoundationScenarios.Reference(root), "fixture-confirmed-actions.txt"));
            string expected = reference.First(l => l.StartsWith("risultato ") && l != "risultato -")["risultato ".Length..];
            string hash = PileGroupTrace.Hash(projected);
            File.WriteAllText(Path.Combine(output, "foundation-version-audit.txt"), $"Runtime: {result["library_version"]}; riferimento: {version}\nHash originale: {PileGroupTrace.Hash(result)}\nHash cambiando solo library_version: {hash}\nHash congelato: {expected}\n");
            if (hash != expected) throw new InvalidOperationException("La differenza non è limitata alla versione della libreria.");
            return 1;
        }
        finally { CultureInfo.CurrentCulture = culture; }
    });
    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    private static readonly ShallowFoundationSheetModule Module = new(() => true);

    private static ShallowFoundationSheetViewModel Open(JsonObject data, IModuleService? service = null, Host? host = null, bool readOnly = false,
        ShallowFoundationSheetModule? module = null, IConfirmationService? confirmations = null) =>
        (ShallowFoundationSheetViewModel)(module ?? Module).CreateSheet(new SheetContext((JsonObject)data.DeepClone(), readOnly, host ?? new Host(),
            new PresentationServices(new ScriptedMessages(), confirmations ?? new YesConfirmations(), new ScriptedFileDialogs(), new ScriptedDialogs(),
                new RecordingClipboard(), new RecordingLinks(), new RecordingWiki()),
            service ?? ModuleServices.Get(ShallowFoundationScenarios.Module), new CalculationLimiter(1)));

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "fondazioni");
            Directory.CreateDirectory(traces);
            foreach (var scenario in ShallowFoundationScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                PileGroupTrace.Write(Path.Combine(traces, scenario.Name + ".txt"), lines);
                string stored = Path.Combine(ShallowFoundationScenarios.Reference(root), scenario.Name + ".txt");
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                string? difference = PileGroupTrace.FirstDifference(PileGroupTrace.Read(stored), lines);
                expect.That(difference is null, "Fondazioni MVVM diverse dalla vista storica, scenario " + scenario.Name + ", " + difference);
            }
            var fixtures = ShallowFoundationScenarios.Fixtures(root);
            expect.That(fixtures.Count == 11, "Fixture F3 delle fondazioni: " + fixtures.Count);
            var transitions = Transition.Load(Path.Combine(root, "tests", "fixtures", "f3", "transitions-f4.json"))
                .Where(t => t.File.StartsWith("sheets/" + ShallowFoundationScenarios.Module + "/", StringComparison.Ordinal)).ToList();
            foreach (string id in fixtures) await F3Async(root, traces, id, transitions.Select(t => t.File).ToHashSet(StringComparer.Ordinal), expect);
            CompareTransitions(root, traces, transitions, expect);
            Contract(expect);
            StateKeys(root, expect);
            await ReadOnlyAsync(root, expect);
            await ReloadAsync(root, expect);
            Closing(root, expect);
            await InjectedAsync(root, expect);
            Library(root, expect);
            await RemoveSurveyAsync(root, expect);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, ShallowFoundationScenario scenario)
    {
        var lines = new List<string>();
        var side = new Side(root, scenario.Fixture);
        try
        {
            lines.AddRange(side.State().Lines("apertura", compareFrozenVersion: true));
            foreach (var step in scenario.Steps)
            {
                await side.ApplyAsync(step);
                lines.AddRange(side.State().Lines(step.ToString(), compareFrozenVersion: true));
            }
        }
        // A step the ViewModel cannot apply ends the trace: the comparison then shows the first different line before it.
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { lines.Add("errore " + error.GetType().Name + ": " + error.Message); }
        finally { side.Sheet.Dispose(); }
        return lines;
    }

    /// <summary>The ViewModel driven as its view would drive it, step by step: a control the user cannot use does nothing.</summary>
    private sealed class Side
    {
        private int modified;
        private Host host = new();

        internal Side(string root, string fixture) => Attach(Open(ShallowFoundationScenarios.FixtureData(root, fixture), host: host));

        internal ShallowFoundationSheetViewModel Sheet { get; private set; } = null!;

        private void Attach(ShallowFoundationSheetViewModel sheet)
        {
            Sheet = sheet;
            modified = 0;
            sheet.Modified += (_, _) => modified++;
        }

        private static bool Usable(FieldViewModel field) => field.IsVisible && field.IsEnabled && field.CanChange;

        internal async Task ApplyAsync(ShallowFoundationStep step)
        {
            var s = Sheet;
            switch (step.Kind)
            {
                case ShallowFoundationStep.Commit: s.Commit(); break;
                case ShallowFoundationStep.Calculate:
                    var calculation = s.CalculateAsync();
                    if (!calculation.IsCompleted) throw new InvalidOperationException("Calcolo non sincrono.");
                    await calculation;
                    break;
                case ShallowFoundationStep.ReadOnly: s.IsReadOnly = true; break;
                case ShallowFoundationStep.Reopen:
                    var data = s.Data;
                    s.Dispose();
                    host = new Host();
                    Attach(Open(data, host: host));
                    break;
                case ShallowFoundationStep.Field:
                {
                    var field = s.Field(step.Key!);
                    if (Usable(field) && !field.IsInputReadOnly) field.EditText = step.Text!;
                    break;
                }
                case ShallowFoundationStep.Choice:
                    if (s.Forms.Any(f => f.Contains(step.Key!)))
                    {
                        var field = s.Field(step.Key!);
                        if (Usable(field)) field.SelectedChoice = step.Text;
                    }
                    else if (s.Choice(step.Key!) is { IsVisible: true, CanChange: true } choice) choice.SelectedItem = step.Text;
                    break;
                case ShallowFoundationStep.Option:
                {
                    var field = s.Field(step.Key!);
                    if (Usable(field)) field.IsChecked = !field.IsChecked;
                    break;
                }
                case ShallowFoundationStep.Group: { var group = s.Groups.Single(g => g.Key == step.Key); group.IsExpanded = !group.IsExpanded; break; }
                case ShallowFoundationStep.Collect: s.ToggleInputs(); break;
                case ShallowFoundationStep.Survey: s.SelectedSurvey = step.Index; break;
                case ShallowFoundationStep.AddSurvey: s.AddSurvey(); break;
                case ShallowFoundationStep.RemoveSurvey: s.RemoveSurvey(); break;
                // The cells of a read-only table refuse the typing (the view binds them to CanEdit).
                case ShallowFoundationStep.Layer: if (s.CanEdit) s.Layers[step.Row][step.Key!] = step.Text; break;
                case ShallowFoundationStep.AddLayer: s.AddLayer(); break;
                case ShallowFoundationStep.RemoveLayer: s.RemoveLayer(step.Row); break;
                case ShallowFoundationStep.Mechanism: if (s.Graphics is not null) s.MechanismAxis = step.Index; break;
                case ShallowFoundationStep.Report: s.RequestReport(); break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        internal ShallowFoundationTraceState State()
        {
            var s = Sheet;
            var trace = new ShallowFoundationTraceState
            {
                Data = s.Data, Modified = modified, ReportRequests = host.Reports, Editable = s.CanEdit, Result = s.Result, State = s.State,
                InputsCollapsed = s.InputsCollapsed, CollectText = s.CollectText, Surveys = s.Surveys, SelectedSurvey = s.SelectedSurvey,
                Layers = s.Layers.Select(r => ShallowFoundationTraceState.WithoutColour(r.Values)).ToList(), HalfSpace = s.Field("strato_portante_semiinfinito").IsChecked,
                Headline = s.ResultsText, HasDetails = s.HasDetails, Summary = s.Summary.ToList(), Coefficients = s.Coefficients.ToList(),
                CoefficientColumns = s.HasDetails ? s.CoefficientColumns : [],
                AdditiveTitle = s.Additive.Count > 0 ? s.AdditiveTitle : "", Additive = s.Additive.ToList(), AdditiveColumns = s.Additive.Count > 0 ? s.AdditiveColumns : [],
                Notes = s.Notes.ToList(),
                InputDrawings = ReferenceEquals(s.InputDrawing?.Data, s.Data), SoilDrawing = ReferenceEquals(s.InputDrawing?.Data, s.Data),
                GraphicsMessage = s.GraphicsMessage, Graphics = s.Graphics, PressureText = s.PressureSummary, ContactText = s.ContactDescription,
                MechanismAxes = s.Graphics is null ? [] : s.MechanismAxes, MechanismAxis = s.MechanismAxis, Mechanism = s.Mechanism, MechanismText = s.MechanismDescription,
            };
            foreach (string key in ShallowFoundationScenarios.FieldKeys)
            {
                if (s.Forms.Any(f => f.Contains(key)))
                {
                    var f = s.Field(key);
                    trace.Fields.Add(f.Kind switch
                    {
                        FieldKind.Text => new ShallowFoundationFieldState(key, "campo", f.Label, f.Unit, f.Text, null, f.IsVisible),
                        FieldKind.Choice => new ShallowFoundationFieldState(key, "scelta", f.Label, f.Unit, f.SelectedChoice ?? "", f.Choices, f.IsVisible),
                        _ => new ShallowFoundationFieldState(key, "opzione", f.Label, f.Unit, PileGroupTrace.YesNo(f.IsChecked), null, f.IsVisible),
                    });
                }
                else
                {
                    var c = s.Choice(key);
                    trace.Fields.Add(new ShallowFoundationFieldState(key, "scelta", c.Label, "", c.SelectedItem ?? "", c.Items, c.IsVisible));
                }
            }
            foreach (var group in s.Groups) trace.Groups.Add((group.Key, group.IsExpanded));
            return trace;
        }
    }

    // ---------------------------------------------------------------- F3 reference

    /// <summary>
    /// The path of ui/f3-equivalence through the ViewModel (opened, visited with every group opened, calculated, result, report,
    /// exported, reopened), compared with the F3 reference of the historic view in the canonical form of the capture: byte for byte,
    /// except the files with F4 transitions (the version of the library in two old fixtures), kept for <see cref="CompareTransitions"/>.
    /// </summary>
    private static async Task F3Async(string root, string output, string id, IReadOnlySet<string> withTransitions, Expect expect)
    {
        string folder = Path.Combine(root, "tests", "fixtures", "f3", "expected", "sheets", ShallowFoundationScenarios.Module, id);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        string Canonical(JsonNode? node) => CanonicalJson.Serialize(node, new GuidMap(), normalizer).Replace("\r\n", "\n");
        string Expected(string name) => File.ReadAllText(Path.Combine(folder, name), Encoding.UTF8).Replace("\r\n", "\n");
        void Same(string name, string actual)
        {
            string file = "sheets/" + ShallowFoundationScenarios.Module + "/" + id + "/" + name;
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
        using var sheet = Open(ShallowFoundationScenarios.FixtureData(root, id));
        sheet.Commit(); Same("opened.json", Canonical(sheet.Data));
        // The visit of the capture opens every expander of the view: the groups whose state the sheet saves.
        foreach (var group in sheet.Groups) group.IsExpanded = true;
        sheet.Commit(); Same("visited.json", Canonical(sheet.Data));
        await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        Same("calculated-data.json", Canonical(sheet.Data)); Same("result.json", Canonical(ShallowFoundationVersionProjection.Result(sheet.Result)));
        var status = new JsonObject { ["hasResults"] = sheet.HasResults };
        try
        {
            byte[] report = sheet.Report.BuildReport("Fixture F3 · " + ShallowFoundationScenarios.Module + " · " + id, new HashSet<string>());
            Same("report.txt", ShallowFoundationVersionProjection.Text(DocxText.Extract(report, normalizer, new GuidMap()).Replace("\r\n", "\n")));
            Same("report-structure.json", Canonical(Structure(report)));
            status["report"] = "available";
        }
        catch (InvalidOperationException error) { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
        Same("exported-data.json", Canonical(sheet.Data)); Same("status.json", Canonical(status));
        using var reopened = Open(sheet.Data);
        reopened.Commit(); Same("reopened.json", Canonical(reopened.Data));
    }

    /// <summary>The files with F4 transitions compared as ui/f3-equivalence compares them, with exactly those transitions admitted.</summary>
    private static void CompareTransitions(string root, string output, IReadOnlyList<Transition> transitions, Expect expect)
    {
        var comparer = new BaselineComparer(Tolerances.Load(Path.Combine(root, "tests", "ANTHEA.Testing", "tolerances.json")));
        comparer.Admit(transitions);
        var report = comparer.Compare(Path.Combine(output, "f3-riferimento"), Path.Combine(output, "f3-transizioni"));
        File.WriteAllText(Path.Combine(output, "f3-confronto.json"), report.ToJsonString(J.Options));
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

    // ---------------------------------------------------------------- contract

    private static void Contract(Expect expect)
    {
        expect.That(Module.Info == ModuleDirectory.Get(ShallowFoundationScenarios.Module) && Module.Reports == new ModuleReportCapabilities(false)
            && Module.Help.GuideId == ModuleHelp.For(ShallowFoundationScenarios.Module).GuideId, "Modulo delle fondazioni");
        expect.That(Module.Persistence.UiStateKeys.SequenceEqual(["vista_fondazioni"]), "Chiavi di stato dichiarate");
        var registry = new ModuleRegistry([Module]);
        expect.That(registry.SheetModuleIds.SequenceEqual([ShallowFoundationScenarios.Module]) && ReferenceEquals(registry.SheetModule(ShallowFoundationScenarios.Module), Module),
            "Registro con le fondazioni");
    }

    /// <summary>Groups and collapsed inputs write only the key of interface state declared by the module, and keep the result.</summary>
    private static void StateKeys(string root, Expect expect)
    {
        using var sheet = Open(ShallowFoundationScenarios.FixtureData(root, "confirmed-actions"));
        expect.That(sheet.CalculateAsync().IsCompleted && sheet.HasResults, "Calcolo del foglio confermato");
        var before = (JsonObject)sheet.Data.DeepClone();
        var result = sheet.Result;
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        foreach (var group in sheet.Groups) group.IsExpanded = !group.IsExpanded;
        sheet.ToggleInputs();
        string[] changed = sheet.Data.Select(p => p.Key).Where(k => !JsonNode.DeepEquals(before[k], sheet.Data[k])).ToArray();
        expect.That(modified == 5 && changed.SequenceEqual(["vista_fondazioni"]) && Module.Persistence.UiStateKeys.Contains(changed[0])
            && ReferenceEquals(result, sheet.Result) && sheet.InputsCollapsed && sheet.CollectText == "Mostra input",
            "Gruppi e input raccolti: chiavi scritte (" + string.Join(", ", changed) + ") o risultato perso");
    }

    // ---------------------------------------------------------------- read-only

    /// <summary>A sheet opened read-only (a previous revision): every edit is refused, groups, report and calculation are allowed.</summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        var host = new Host();
        using var sheet = Open(ShallowFoundationScenarios.FixtureData(root, "vesic-circolare-due-sondaggi"), host: host, readOnly: true);
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        string before = sheet.Data.ToJsonString();
        expect.That(!sheet.CanEdit && sheet.Forms.All(f => f.IsReadOnly) && sheet.MethodChoices.All(c => !c.CanChange), "Sola lettura dal contesto");
        sheet.GeometryForm["Width"].EditText = "9";
        sheet.MethodChoices[0].SelectedIndex = 2;
        sheet.Compression.SelectedIndex = 0;
        sheet.WaterForm["presenza_falda"].IsChecked = false;
        sheet.ConcreteForm["classe_cls"].SelectedChoice = "C40/50";
        sheet.SelectedSurvey = 0;
        sheet.AddSurvey(); sheet.RemoveSurvey(); sheet.AddLayer(); sheet.RemoveLayer(0);
        sheet.Layers[0]["spessore"] = "7";
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && sheet.Layers[0].Values["spessore"]!.ToString() == "0,8",
            "Sola lettura: modifica scritta nel documento");
        expect.That(await Expect.Within(sheet.CalculateAsync()) && sheet.HasResults && sheet.MechanismAxis == 0, "Sola lettura: calcolo");
        sheet.MechanismAxis = 1;
        sheet.RequestReport();
        sheet.Groups[0].IsExpanded = false;
        expect.That(sheet.MechanismAxis == 0 && host.Reports == 1 && modified == 1 && sheet.HasResults && sheet.Data["vista_fondazioni"]?["metodo"]?.GetValue<bool>() == false,
            "Sola lettura: sezione del meccanismo, report o gruppi");
        sheet.IsReadOnly = false;
        sheet.GeometryForm["Width"].EditText = "3,2";
        expect.That(sheet.Data["input"]!["Width"]!.ToString() == "3,2" && !sheet.HasResults && modified == 2 && sheet.State == "Dati modificati · premere Calcola",
            "Ritorno alla modifica");
    }

    // ---------------------------------------------------------------- reload

    /// <summary>Data replaced from outside: built again as at opening (migration of version 1), without Modified and without a result.</summary>
    private static async Task ReloadAsync(string root, Expect expect)
    {
        using var sheet = Open(ShallowFoundationScenarios.FixtureData(root, "confirmed-actions"));
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        await Expect.Within(sheet.CalculateAsync());
        sheet.SelectedSurvey = 0;
        sheet.GeometryForm["Width"].EditText = "2,2";
        var legacy = ShallowFoundationScenarios.FixtureData(root, "legacy-falda-piano-campagna");
        sheet.Reload(legacy);
        expect.That(modified == 1 && !sheet.HasResults && sheet.State == "Dati da verificare · premere Calcola" && sheet.GraphicsMessage == "Calcolare per aggiornare questa vista."
            && sheet.GeometryForm["Depth"].Text == "0" && sheet.Layers.Count == 1 && sheet.WaterForm["presenza_falda"].IsChecked, "Reload: campi, strati o stato");
        expect.That(sheet.Data["versione_fondazioni"]!.GetValue<int>() == 2 && legacy["versione_fondazioni"]!.GetValue<int>() == 1, "Reload: migrazione o origine modificata");
        expect.That(await Expect.Within(sheet.CalculateAsync()) && sheet.HasResults, "Reload: calcolo");
        using var reopened = Open(legacy);
        await Expect.Within(reopened.CalculateAsync());
        expect.That(sheet.Data.ToJsonString() == reopened.Data.ToJsonString() && modified == 1, "Reload: dati diversi da una riapertura");
    }

    // ---------------------------------------------------------------- closing

    /// <summary>A closed sheet writes nothing and refuses the commands of the sheet.</summary>
    private static void Closing(string root, Expect expect)
    {
        var service = new CountingService();
        var sheet = Open(ShallowFoundationScenarios.FixtureData(root, "confirmed-actions"), service);
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        string data = sheet.Data.ToJsonString();
        sheet.Dispose();
        sheet.MethodChoices[0].SelectedIndex = 2;
        sheet.AddLayer(); sheet.AddSurvey(); sheet.ToggleInputs();
        sheet.Groups[0].IsExpanded = false;
        expect.That(sheet.Data.ToJsonString() == data && modified == 0 && service.Calls == 0, "Modifica dopo la chiusura");
        expect.Throws<ObjectDisposedException>(sheet.Commit, "Commit dopo la chiusura");
        expect.Throws<ObjectDisposedException>(() => sheet.CalculateAsync(), "Calcola dopo la chiusura");
    }

    // ---------------------------------------------------------------- injected calculation

    /// <summary>The calculation service of the context, on the UI thread, only with «Calcola»; an error is the status line.</summary>
    private static async Task InjectedAsync(string root, Expect expect)
    {
        var service = new CountingService();
        using var sheet = Open(ShallowFoundationScenarios.FixtureData(root, "confirmed-actions"), service);
        int ui = Environment.CurrentManagedThreadId;
        expect.That(service.Calls == 0 && !sheet.HasResults && sheet.AutomaticDelay is null && sheet.CalculationMode == SheetCalculationMode.Synchronous,
            "Calcolo all'apertura o ricalcolo automatico");
        var calculated = sheet.CalculateAsync();
        expect.That(calculated.IsCompleted && await calculated && service.Calls == 1 && service.Threads.All(t => t == ui) && sheet.HasResults && !sheet.IsBusy,
            "Calcola non sincrono o fuori dal servizio del contesto");
        sheet.GeometryForm["Width"].EditText = "2,4";
        expect.That(!sheet.HasResults && service.Calls == 1 && sheet.PendingCalculation.IsCompleted, "Modifica senza risultati azzerati o calcolo automatico");
        sheet.Commit();
        expect.That(service.Calls == 1 && !sheet.HasResults, "Commit che calcola");
        service.Fail = true;
        expect.That(await Expect.Within(sheet.CalculateAsync()) && service.Calls == 2, "Calcolo con errore non pubblicato");
        expect.That(!sheet.HasResults && sheet.CalculationState == CalculationState.Failed && sheet.State == "motore di prova" && sheet.GraphicsMessage == "Calcolare per aggiornare questa vista.",
            "Errore del calcolo nello stato");
        service.Fail = false;
        expect.That(await Expect.Within(sheet.CalculateAsync()) && sheet.HasResults && service.Calls == 3 && sheet.State.StartsWith("Calcolato · sondaggio 1", StringComparison.Ordinal),
            "Calcolo dopo l'errore");
    }

    /// <summary>Without the approved library the sheet can be prepared and saved: only the text of the results changes.</summary>
    private static void Library(string root, Expect expect)
    {
        using var sheet = Open(ShallowFoundationScenarios.FixtureData(root, "default"), module: new ShallowFoundationSheetModule(() => false));
        expect.That(sheet.ResultsText == "Calcolo in attesa del rilascio approvato GPC · puoi preparare e salvare il foglio.", "Testo senza la libreria approvata");
    }

    /// <summary>«− Sondaggio» asks the confirmation service; a «no» changes nothing.</summary>
    private static Task RemoveSurveyAsync(string root, Expect expect)
    {
        var confirmations = new YesConfirmations { Answer = false };
        using var sheet = Open(ShallowFoundationScenarios.FixtureData(root, "vesic-circolare-due-sondaggi"), confirmations: confirmations);
        string before = sheet.Data.ToJsonString();
        sheet.RemoveSurvey();
        expect.That(confirmations.Asked.SequenceEqual(["Stratigrafia: Eliminare il sondaggio selezionato e i suoi strati?"]) && sheet.Data.ToJsonString() == before
            && sheet.Surveys.Count == 2, "Domanda dell'eliminazione del sondaggio");
        return Task.CompletedTask;
    }

    /// <summary>The module service of the foundations, counted (calls and threads); <see cref="Fail"/> makes it throw.</summary>
    private sealed class CountingService : IModuleService
    {
        private readonly IModuleService inner = ModuleServices.Get(ShallowFoundationScenarios.Module);
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

    /// <summary>Confirmations of the traces: the removal of a survey is answered <see cref="Answer"/> (yes); any other question fails.</summary>
    private sealed class YesConfirmations : IConfirmationService
    {
        internal bool Answer { get; init; } = true;
        internal List<string> Asked { get; } = [];
        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista.");
        public bool ConfirmDelete(string title, string question) { Asked.Add(title + ": " + question); return Answer; }
    }

    private sealed class Host : ISheetHost
    {
        internal int Reports { get; private set; }
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException("Copia non prevista.");
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => Reports++;
    }
}
