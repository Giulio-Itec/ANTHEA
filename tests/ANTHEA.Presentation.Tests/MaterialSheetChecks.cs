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
using Anthea.Presentation.Modules.Materials;
using Anthea.Testing.Normalization;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModels of the materials (F5.5) against their historic views, without WPF. This is the evidence of equivalence that
/// survives the archiving of the historic views:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/MaterialTrace.cs (the path of the F3 capture for each fixture of the two modules, edits of
/// every kind, read-only) gives the trace of the historic views frozen in tests/fixtures/f5-materiali/storica, line by line: data
/// and preview in memory order, Modified, inputs, texts, tables and the diagram of the steel after every step;</item>
/// <item>for each F3 fixture of the two modules the data after opening, visit, calculation, export and reopening, the result, the
/// status and the text and structure of the report are byte-identical to the F3 reference of the historic views;</item>
/// <item>modules, persisted keys, read-only, Reload, closing, the calculation service of the context, the preview of the contract
/// and the mode without published calculation of the two sheets.</item>
/// </list>
/// One "UI" thread (SingleThreadContext), as the dispatcher.
/// </summary>
internal static class MaterialSheetChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    private static readonly ConcreteMaterialSheetModule ConcreteModule = new();
    private static readonly RebarMaterialSheetModule SteelModule = new();

    private static ISheetViewModel Open(string module, JsonObject data, IModuleService? service = null, bool readOnly = false)
    {
        var context = new SheetContext((JsonObject)data.DeepClone(), readOnly, new Host(), Scripted.Services(), service ?? ModuleServices.Get(module), new CalculationLimiter(1));
        return module == MaterialScenarios.Concrete ? ConcreteModule.CreateSheet(context) : SteelModule.CreateSheet(context);
    }

    private static ConcreteMaterialSheetViewModel Concrete(JsonObject data, IModuleService? service = null) => (ConcreteMaterialSheetViewModel)Open(MaterialScenarios.Concrete, data, service);
    private static RebarMaterialSheetViewModel Steel(JsonObject data, IModuleService? service = null) => (RebarMaterialSheetViewModel)Open(MaterialScenarios.Steel, data, service);

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic views (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "materiali");
            Directory.CreateDirectory(traces);
            foreach (var scenario in MaterialScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                MaterialTrace.Write(Path.Combine(traces, scenario.File), lines);
                string stored = Path.Combine(MaterialScenarios.Reference(root), scenario.File);
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                string? difference = MaterialTrace.FirstDifference(MaterialTrace.Read(stored), lines);
                expect.That(difference is null, "Materiale MVVM diverso dalla vista storica, scenario " + scenario.File + ", " + difference);
            }
            expect.That(MaterialScenarios.Fixtures(root, MaterialScenarios.Concrete).Count == 8 && MaterialScenarios.Fixtures(root, MaterialScenarios.Steel).Count == 7,
                "Fixture F3 dei materiali");
            foreach (string module in new[] { MaterialScenarios.Concrete, MaterialScenarios.Steel })
                foreach (string id in MaterialScenarios.Fixtures(root, module)) await F3Async(root, traces, module, id, expect);
            Contract(expect);
            StateKeys(root, expect);
            await ReadOnlyAsync(root, expect);
            Reload(root, expect);
            Closing(root, expect);
            await InjectedAsync(root, expect);
            Preview(root, expect);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, MaterialScenario scenario)
    {
        var lines = new List<string>();
        var side = new Side(scenario.Module, Open(scenario.Module, MaterialScenarios.FixtureData(root, scenario.Module, scenario.Fixture)));
        try
        {
            lines.AddRange(side.Lines("apertura"));
            foreach (var step in scenario.Steps)
            {
                await side.ApplyAsync(step);
                lines.AddRange(side.Lines(step.ToString()));
            }
        }
        // A step the ViewModel cannot apply ends the trace: the comparison then shows the first different line before it.
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException) { lines.Add("errore " + error.GetType().Name + ": " + error.Message); }
        finally { side.Sheet.Dispose(); }
        return lines;
    }

    /// <summary>The ViewModel driven as its view would drive it, step by step.</summary>
    private sealed class Side
    {
        private readonly string module;
        private int modified;

        internal Side(string module, ISheetViewModel sheet) { this.module = module; Attach(sheet); }

        internal ISheetViewModel Sheet { get; private set; } = null!;

        private ConcreteMaterialSheetViewModel C => (ConcreteMaterialSheetViewModel)Sheet;
        private RebarMaterialSheetViewModel S => (RebarMaterialSheetViewModel)Sheet;

        private void Attach(ISheetViewModel sheet)
        {
            Sheet = sheet;
            modified = 0;
            sheet.Modified += (_, _) => modified++;
        }

        private FieldViewModel SteelField(string key) => new[] { S.IdentityForm, S.PropertiesForm, S.CoefficientForm }.Where(f => f.Contains(key)).Select(f => f[key]).Single();

        internal async Task ApplyAsync(MaterialStep step)
        {
            bool concrete = module == MaterialScenarios.Concrete;
            switch (step.Kind)
            {
                case MaterialStep.Commit: Sheet.Commit(); break;
                // «Calcola» of the window: MvvmSheetEditor.CalculateAsync commits the sheet, then calculates (the historic material
                // sheets only commit).
                case MaterialStep.Calculate: Sheet.Commit(); await Expect.Within(Sheet.CalculateAsync()); break;
                case MaterialStep.ReadOnly: Sheet.IsReadOnly = true; break;
                case MaterialStep.Reopen:
                    var data = Sheet.Data;
                    Sheet.Dispose();
                    Attach(Open(module, data));
                    break;
                case MaterialStep.Class: C.SelectedClass = step.Text; break;
                case MaterialStep.Number: C.Inputs[step.Key!].EditText = step.Text!; break;
                case MaterialStep.Choice when concrete && step.Key == ConcreteMaterialSheetViewModel.DeviationValueKey: C.SelectedDeviationValue = step.Text; break;
                case MaterialStep.Choice when concrete: C.Inputs[step.Key!].SelectedChoice = step.Text; break;
                case MaterialStep.Choice: S.PropertiesForm[step.Key!].SelectedChoice = step.Text; break;
                case MaterialStep.Option:
                    var option = C.Inputs[step.Key!];
                    option.IsChecked = !option.IsChecked;
                    break;
                case MaterialStep.Exposure: C.SelectedExposure = step.Text; break;
                case MaterialStep.Steel: S.SelectedType = step.Text; break;
                case MaterialStep.Field: SteelField(step.Key!).EditText = step.Text!; break;
                case MaterialStep.Reference: S.Reference = step.Text!; break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        internal IEnumerable<string> Lines(string step) => module == MaterialScenarios.Concrete ? ConcreteState().Lines(step) : SteelState().Lines(step);

        private ConcreteTraceState ConcreteState()
        {
            var s = C;
            return new()
            {
                Data = s.Data, Preview = s.PreviewData, Modified = modified, Editable = s.CanEdit,
                ClassName = s.SelectedClass, Classes = s.Classes.Count,
                Numbers = Materiali.MaterialSheetInput.NumberKeys.Select(k => (k, s.Inputs[k].Text)).ToList(),
                Choices = Materiali.MaterialSheetInput.ChoiceKeys.Select(k => k == ConcreteMaterialSheetViewModel.DeviationValueKey
                    ? (k, s.SelectedDeviationValue, s.CanEdit && s.CanChooseDeviationValue) : (k, s.Inputs[k].SelectedChoice, s.CanEdit)).ToList(),
                DeviationValues = s.DeviationValues.ToList(),
                Options = Materiali.MaterialSheetInput.FlagKeys.Select(k => (k, s.Inputs[k].IsChecked)).ToList(),
                Exposure = s.SelectedExposure, Exposures = s.Exposures.Count, Active = s.ActiveExposures.ToList(),
                ExposureDetails = s.ExposureDetails, ExposureExamples = s.ExposureExamples, Aggressiveness = s.Aggressiveness,
                Combined = s.CombinedExposure, CombinedVisible = s.IsCombinedExposureVisible,
                Strength = s.Strength, Tension = s.Tension, Modulus = s.Modulus, Status = s.Status,
                Properties = s.Properties.Select(r => new[] { r.Nome, r.Simbolo, r.Valore, r.Unita }).ToList(),
                BondValue = s.BondValue, BondDetails = s.BondDetails, MinimumClass = s.MinimumConcreteClass, MinimumNote = s.MinimumConcreteNote,
                CoverHeadline = s.CoverHeadline, CoverSteps = s.CoverSteps, CoverNotes = s.CoverNotes, CoverError = s.CoverError,
                NtcVisible = s.IsNtcCover, Ec2Visible = !s.IsNtcCover,
                DeviationNote = s.DeviationNote, DeviationToolTip = s.DeviationToolTip,
                Composition = s.Composition.Select(p => (p.Key, p.Value)).ToList(),
                CompositionSource = s.CompositionSource, AirNote = s.CompositionAirNote, ChlorideNote = s.CompositionChlorideNote,
                Requirements = s.CompositionRequirements, Consistency = s.ConsistencyInfo,
            };
        }

        private SteelTraceState SteelState()
        {
            var s = S;
            var diagram = s.PropertiesForm["steel_diagramma"];
            return new()
            {
                Data = s.Data, Preview = s.PreviewData, Modified = modified, Editable = s.CanEdit,
                Type = s.SelectedType, Types = s.Types.ToList(),
                Fields = new[] { "materiale_acciaio_nome", "fyk_mpa", "steel_fu_mpa", "steel_modulus_mpa", "steel_eps_u", "gamma_s" }
                    .Select(k => (k, SteelField(k).Text, !s.CanEdit || (k == "materiale_acciaio_nome" ? s.NameLocked : s.Locked.GetValueOrDefault(k)))).ToList(),
                Diagram = diagram.SelectedChoice, Diagrams = diagram.Choices.ToList(), DiagramEnabled = s.CanEdit && s.DiagramEnabled,
                Reference = s.Reference, Description = s.Description, Source = s.Source, Results = s.Results, Status = s.Status,
                StatusCoherent = s.StatusCoherent, Series = s.HasDiagram ? [(s.DiagramName, s.Diagram)] : [],
            };
        }
    }

    // ---------------------------------------------------------------- F3 reference

    /// <summary>
    /// The path of ui/f3-equivalence through the ViewModel (opened, visited, calculated, result, report, exported, reopened), compared
    /// with the F3 reference of the historic view in the canonical form of the capture.
    /// </summary>
    private static async Task F3Async(string root, string output, string module, string id, Expect expect)
    {
        string folder = Path.Combine(root, "tests", "fixtures", "f3", "expected", "sheets", module, id);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        string Canonical(JsonNode? node) => CanonicalJson.Serialize(node, new GuidMap(), normalizer).Replace("\r\n", "\n");
        // The reference with the F4 transitions admitted one by one (D2): the U5 texts of the report of the first two concrete fixtures.
        string Expected(string name) => WithTransitions(root, "sheets/" + module + "/" + id + "/" + name,
            File.ReadAllText(Path.Combine(folder, name), Encoding.UTF8).Replace("\r\n", "\n"));
        void Same(string name, string actual) => expect.That(actual == Expected(name), module + "/" + id + ": " + name + " diverso dal riferimento F3 della vista storica");
        using var sheet = Open(module, MaterialScenarios.FixtureData(root, module, id));
        sheet.Commit(); Same("opened.json", Canonical(sheet.Data));
        sheet.Commit(); Same("visited.json", Canonical(sheet.Data));
        await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        Same("calculated-data.json", Canonical(sheet.Data)); Same("result.json", Canonical(sheet.Result));
        var status = new JsonObject { ["hasResults"] = sheet.HasResults };
        byte[]? report = null;
        try
        {
            report = sheet.Report.BuildReport("Fixture F3 · " + module + " · " + id, new HashSet<string>());
            status["report"] = "available";
        }
        catch (InvalidOperationException error) { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
        if (report is not null)
        {
            string text = DocxText.Extract(report, normalizer, new GuidMap()).Replace("\r\n", "\n");
            File.WriteAllText(Path.Combine(output, module + "--" + id + ".report.txt"), text, new UTF8Encoding(false));
            Same("report.txt", text);
            Same("report-structure.json", Canonical(Structure(report)));
        }
        Same("exported-data.json", Canonical(sheet.Data)); Same("status.json", Canonical(status));
        using var reopened = Open(module, sheet.Data);
        reopened.Commit(); Same("reopened.json", Canonical(reopened.Data));
    }

    /// <summary>
    /// The frozen text of <paramref name="file"/> (relative to the expected folder) with its transitions of transitions-f4.json applied,
    /// with the rule of the F3 comparison: every listed line must hold the value «a» of the reference and becomes «b»; a transition
    /// that does not occur, or one that is not a line of text, fails.
    /// </summary>
    private static string WithTransitions(string root, string file, string expected)
    {
        var entries = Transitions(root).Where(t => t["file"]?.GetValue<string>() == file).ToArray();
        if (entries.Length == 0) return expected;
        var lines = expected.Split('\n');
        foreach (var entry in entries)
        {
            string path = entry["percorso"]!.GetValue<string>();
            if (entry["classe"]?.GetValue<string>() != "testo" || !path.StartsWith("riga ", StringComparison.Ordinal))
                throw new InvalidOperationException("Transizione F4 non testuale per " + file + ": " + path);
            int line = int.Parse(path["riga ".Length..], CultureInfo.InvariantCulture) - 1;
            if (line >= lines.Length || lines[line] != entry["a"]!.GetValue<string>())
                throw new InvalidOperationException("Transizione F4 che non si presenta nel riferimento: " + file + " " + path);
            lines[line] = entry["b"]!.GetValue<string>();
        }
        return string.Join('\n', lines);
    }

    private static JsonObject[]? transitions;

    private static JsonObject[] Transitions(string root) => transitions ??= JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "f3", "transitions-f4.json")))!["voci"]!
        .AsArray().OfType<JsonObject>().ToArray();

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
        foreach (ISheetModule module in new ISheetModule[] { ConcreteModule, SteelModule })
            expect.That(module.Info == ModuleDirectory.Get(module.Info.Id) && module.Reports == new ModuleReportCapabilities(false)
                && module.Help.GuideId == ModuleHelp.For(module.Info.Id).GuideId && module.Persistence.UiStateKeys.Count == 0, "Modulo " + module.Info.Id);
        expect.That(ConcreteModule.Info.Id == MaterialScenarios.Concrete && SteelModule.Info.Id == MaterialScenarios.Steel, "Identità dei moduli dei materiali");
        var registry = new ModuleRegistry([SteelModule, ConcreteModule]);
        expect.That(registry.SheetModuleIds.SequenceEqual([MaterialScenarios.Concrete, MaterialScenarios.Steel])
            && ReferenceEquals(registry.SheetModule(MaterialScenarios.Concrete), ConcreteModule) && ReferenceEquals(registry.SheetModule(MaterialScenarios.Steel), SteelModule),
            "Registro con i materiali, nell'ordine di ModuleDirectory");
    }

    /// <summary>
    /// The keys written by the sheets: the concrete writes at Commit exactly the keys of its state, after every kind of edit; the
    /// steel writes only into «input» (the keys of the steel) and «riferimento».
    /// </summary>
    private static void StateKeys(string root, Expect expect)
    {
        string[] concreteKeys = ["versione_materiali", "classe", "esposizione_principale", "esposizioni_precedenti", "numeri", "scelte", "opzioni", "esposizioni"];
        using (var sheet = Concrete(MaterialScenarios.FixtureData(root, MaterialScenarios.Concrete, "legacy-tre-esposizioni")))
        {
            sheet.SelectedClass = "C50/60"; sheet.Inputs["diameter"].EditText = "22"; sheet.Inputs["life"].SelectedChoice = "100 anni";
            sheet.Inputs["rough"].IsChecked = false; sheet.SelectedDeviationValue = "7 mm"; sheet.SelectedExposure = "XS1";
            sheet.Commit();
            expect.That(sheet.Data.Select(p => p.Key).SequenceEqual(concreteKeys) && sheet.Data["numeri"]!.AsObject().Select(p => p.Key).SequenceEqual(Materiali.MaterialSheetInput.NumberKeys)
                && sheet.Data["scelte"]!.AsObject().Select(p => p.Key).SequenceEqual(Materiali.MaterialSheetInput.ChoiceKeys)
                && sheet.Data["opzioni"]!.AsObject().Select(p => p.Key).SequenceEqual(Materiali.MaterialSheetInput.FlagKeys)
                && sheet.Data.S("classe") == "C50/60" && sheet.Data["scelte"].S("deviationValue") == "7 mm" && sheet.Data.S("esposizione_principale") == "XS1",
                "Calcestruzzo: chiavi scritte alla conferma");
        }
        using (var sheet = Steel(MaterialScenarios.FixtureData(root, MaterialScenarios.Steel, "default")))
        {
            var before = (JsonObject)sheet.Data.DeepClone();
            sheet.SelectedType = "Personalizzato"; sheet.PropertiesForm["fyk_mpa"].EditText = "470"; sheet.CoefficientForm["gamma_s"].EditText = "1,2";
            sheet.PropertiesForm["steel_diagramma"].SelectedChoice = "Incrudente"; sheet.Reference = "Prova";
            expect.That(sheet.Data.Select(p => p.Key).SequenceEqual(before.Select(p => p.Key))
                && sheet.Data["input"]!.AsObject().Select(p => p.Key).SequenceEqual(before["input"]!.AsObject().Select(p => p.Key))
                && sheet.Data.S("riferimento") == "Prova" && sheet.Data["input"].S("fyk_mpa") == "470", "Acciaio: chiavi scritte");
        }
    }

    // ---------------------------------------------------------------- read-only

    /// <summary>A previous revision: every edit is refused, Modified is never raised; Commit still writes the state, as today.</summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        using (var sheet = Concrete(MaterialScenarios.FixtureData(root, MaterialScenarios.Concrete, "legacy-tre-esposizioni")))
        {
            int modified = 0;
            sheet.Modified += (_, _) => modified++;
            sheet.Inputs["diameter"].EditText = "30";
            expect.That(modified == 1 && sheet.PreviewData["numeri"].S("diameter") == "30" && sheet.Data["numeri"].S("diameter") == "25", "Calcestruzzo: modifica prima della sola lettura");
            var form = sheet.Inputs;
            expect.That(!form.IsReadOnly && sheet.CanEditDeviationValue && form.WikiNavigator is RecordingWiki,
                "Calcestruzzo: campi modificabili, con la Wiki dei servizi");
            sheet.IsReadOnly = true;
            // T2: the same form, read-only (no detached copy); the deviation value is locked with it.
            expect.That(ReferenceEquals(sheet.Inputs, form) && form.IsReadOnly && !form["diameter"].CanChange && form["diameter"].IsInputReadOnly && !sheet.CanEditDeviationValue,
                "Calcestruzzo: sola lettura con FormViewModel.IsReadOnly sullo stesso form (T2)");
            string preview = sheet.PreviewData.ToJsonString();
            sheet.SelectedClass = "C20/25"; sheet.Inputs["diameter"].EditText = "12"; sheet.Inputs["coverMethod"].SelectedChoice = "NTC + Circ. 2019";
            sheet.Inputs["rough"].IsChecked = false; sheet.Inputs["deviationControl"].SelectedChoice = "Ordinario"; sheet.SelectedDeviationValue = "5 mm";
            sheet.SelectedExposure = "XA3";
            expect.That(sheet.PreviewData.ToJsonString() == preview && modified == 1 && !sheet.CanEdit && sheet.SelectedClass == "C32/40"
                && sheet.SelectedExposure == "XD2" && sheet.Inputs["diameter"].Text == "30" && sheet.Inputs["diameter"].EditText == "30"
                && sheet.Inputs["rough"].IsChecked && sheet.Inputs["coverMethod"].SelectedChoice != "NTC + Circ. 2019", "Calcestruzzo: modifica accettata in sola lettura");
            await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
            expect.That(sheet.Data.ToJsonString() == preview && !sheet.HasResults && modified == 1, "Calcestruzzo: conferma in sola lettura");
            sheet.IsReadOnly = false;
            expect.That(ReferenceEquals(sheet.Inputs, form) && !form.IsReadOnly && sheet.Inputs["diameter"].Text == "30" && sheet.PreviewData["numeri"].S("diameter") == "30",
                "Calcestruzzo: testi mostrati al ritorno alla modifica");
            sheet.Inputs["diameter"].EditText = "14";
            expect.That(sheet.PreviewData["numeri"].S("diameter") == "14" && modified == 2, "Calcestruzzo: ritorno alla modifica");
        }
        using (var sheet = Steel(MaterialScenarios.FixtureData(root, MaterialScenarios.Steel, "personalizzato-incrudente"), null))
        {
            int modified = 0;
            sheet.Modified += (_, _) => modified++;
            var forms = new[] { sheet.IdentityForm, sheet.PropertiesForm, sheet.CoefficientForm };
            expect.That(forms.All(f => !f.IsReadOnly && f.WikiNavigator is RecordingWiki && f.WikiModule is null),
                "Acciaio: campi modificabili, con la Wiki dei servizi e il modulo della vista");
            sheet.IsReadOnly = true;
            // T2: the same forms, read-only (no detached copies).
            expect.That(forms.SequenceEqual([sheet.IdentityForm, sheet.PropertiesForm, sheet.CoefficientForm]) && forms.All(f => f.IsReadOnly),
                "Acciaio: sola lettura con FormViewModel.IsReadOnly sugli stessi form (T2)");
            string before = sheet.Data.ToJsonString();
            sheet.SelectedType = "B450C"; sheet.PropertiesForm["fyk_mpa"].EditText = "999"; sheet.PropertiesForm["steel_diagramma"].SelectedChoice = "Elastoplastico";
            sheet.IdentityForm["materiale_acciaio_nome"].EditText = "Altro"; sheet.CoefficientForm["gamma_s"].EditText = "1"; sheet.Reference = "Altro";
            expect.That(sheet.Data.ToJsonString() == before && modified == 0 && sheet.SelectedType == "Personalizzato" && sheet.Reference.StartsWith("Certificato", StringComparison.Ordinal)
                && sheet.PropertiesForm["fyk_mpa"].Text == sheet.Data["input"].S("fyk_mpa") && sheet.IdentityForm["materiale_acciaio_nome"].Text == sheet.Data["input"].S("materiale_acciaio_nome"),
                "Acciaio: modifica accettata in sola lettura");
            sheet.IsReadOnly = false;
            expect.That(forms.All(f => !f.IsReadOnly) && forms.SequenceEqual([sheet.IdentityForm, sheet.PropertiesForm, sheet.CoefficientForm]), "Acciaio: stessi form al ritorno alla modifica");
            sheet.PropertiesForm["fyk_mpa"].EditText = "440";
            expect.That(sheet.Data["input"].S("fyk_mpa") == "440" && modified == 1, "Acciaio: ritorno alla modifica");
        }
        // M6: the fields fixed by the catalogue are a state of the view; the diagram is enabled on its form (the Enable of the historic
        // view), without refusing a write.
        using (var sheet = Steel(MaterialScenarios.FixtureData(root, MaterialScenarios.Steel, "default"), null))
        {
            var diagram = sheet.PropertiesForm["steel_diagramma"];
            expect.That(!sheet.DiagramEnabled && !diagram.IsEnabled && diagram.CanChange && sheet.NameLocked && sheet.Locked.Values.All(v => v),
                "Acciaio da catalogo: diagramma spento sul form, campi fissati come stato della vista");
            sheet.SelectedType = "FeB44k";
            expect.That(sheet.DiagramEnabled && diagram.IsEnabled && !sheet.Locked["steel_eps_u"] && sheet.Locked["fyk_mpa"], "Acciaio storico: diagramma acceso, εu libero");
            sheet.SelectedType = "B450C";
            expect.That(!diagram.IsEnabled && diagram.SelectedChoice == "Elastoplastico", "Acciaio da catalogo: diagramma spento di nuovo");
            diagram.SelectedChoice = "Incrudente";
            expect.That(sheet.Data["input"].S("steel_diagramma") == "Incrudente", "Acciaio: la scelta del diagramma spento non è rifiutata dal ViewModel (M6)");
        }
    }

    // ---------------------------------------------------------------- reload

    /// <summary>Data replaced from outside: read again as at opening (normalized state, steel fields), no Modified, new revision.</summary>
    private static void Reload(string root, Expect expect)
    {
        using (var sheet = Concrete(MaterialScenarios.FixtureData(root, MaterialScenarios.Concrete, "default")))
        {
            int modified = 0;
            sheet.Modified += (_, _) => modified++;
            sheet.Inputs["diameter"].EditText = "18";
            long revision = sheet.Revision;
            var legacy = MaterialScenarios.FixtureData(root, MaterialScenarios.Concrete, "legacy-tre-esposizioni");
            sheet.Reload(legacy);
            using var reopened = Concrete(legacy);
            expect.That(modified == 1 && sheet.Revision > revision && sheet.PreviewData.ToJsonString() == reopened.PreviewData.ToJsonString()
                && sheet.Data.ToJsonString() == legacy.ToJsonString() && sheet.SelectedExposure == "XD2" && sheet.CombinedExposure == reopened.CombinedExposure
                && sheet.CoverHeadline == reopened.CoverHeadline && sheet.Inputs["diameter"].Text == "25", "Calcestruzzo: Reload diverso da una riapertura");
            sheet.Inputs["diameter"].EditText = "26";
            expect.That(modified == 2 && sheet.PreviewData["numeri"].S("diameter") == "26", "Calcestruzzo: modifica dopo Reload");
        }
        using (var sheet = Steel(MaterialScenarios.FixtureData(root, MaterialScenarios.Steel, "default")))
        {
            int modified = 0;
            sheet.Modified += (_, _) => modified++;
            sheet.SelectedType = "B450A";
            var other = MaterialScenarios.FixtureData(root, MaterialScenarios.Steel, "storico-feb38k-eps");
            sheet.Reload(other);
            using var reopened = Steel(other);
            expect.That(modified == 1 && sheet.SelectedType == "FeB38k" && sheet.Data.ToJsonString() == other.ToJsonString() && sheet.Status == reopened.Status
                && sheet.PropertiesForm["steel_eps_u"].Text == "40" && sheet.Reference == reopened.Reference && sheet.Diagram.Count == reopened.Diagram.Count,
                "Acciaio: Reload diverso da una riapertura");
            sheet.PropertiesForm["steel_eps_u"].EditText = "41";
            expect.That(modified == 2 && sheet.Data["input"].S("steel_eps_u") == "41", "Acciaio: modifica dopo Reload");
        }
    }

    // ---------------------------------------------------------------- closing

    /// <summary>Closed: edits are ignored, nothing is written, Commit and Calcola fail as for every sheet.</summary>
    private static void Closing(string root, Expect expect)
    {
        var concrete = Concrete(MaterialScenarios.FixtureData(root, MaterialScenarios.Concrete, "default"));
        concrete.Inputs["diameter"].EditText = "20";
        string preview = concrete.PreviewData.ToJsonString();
        concrete.Dispose();
        concrete.SelectedClass = "C20/25"; concrete.SelectedExposure = "XA1";
        expect.That(concrete.PreviewData.ToJsonString() == preview && concrete.Data.ToJsonString() == "{\"versione_materiali\":1}", "Calcestruzzo: modifica dopo la chiusura");
        expect.Throws<ObjectDisposedException>(concrete.Commit, "Calcestruzzo: Commit dopo la chiusura");
        var service = new CountingService(MaterialScenarios.Steel);
        var steel = Steel(MaterialScenarios.FixtureData(root, MaterialScenarios.Steel, "default"), service);
        int calls = service.Calls;
        string data = steel.Data.ToJsonString();
        steel.Dispose();
        steel.SelectedType = "B450A"; steel.Reference = "x";
        expect.That(steel.Data.ToJsonString() == data && service.Calls == calls, "Acciaio: modifica dopo la chiusura");
        expect.Throws<ObjectDisposedException>(() => steel.CalculateAsync(), "Acciaio: Calcola dopo la chiusura");
    }

    // ---------------------------------------------------------------- injected calculation

    /// <summary>
    /// The calculation service of the context: the steel calls it on the UI thread at opening and after every change, for the values
    /// shown; «Calcola» publishes nothing (the historic sheets have no result). The concrete never calls it.
    /// </summary>
    private static async Task InjectedAsync(string root, Expect expect)
    {
        var service = new CountingService(MaterialScenarios.Steel);
        using (var sheet = Steel(MaterialScenarios.FixtureData(root, MaterialScenarios.Steel, "default"), service))
        {
            int ui = Environment.CurrentManagedThreadId;
            expect.That(service.Calls == 1 && service.Threads.All(t => t == ui) && sheet.StatusCoherent && sheet.HasDiagram, "Acciaio: calcolo all'apertura fuori dal servizio del contesto");
            var calculated = sheet.CalculateAsync();
            expect.That(calculated.IsCompleted && !await calculated && service.Calls == 1 && !sheet.HasResults && sheet.Result is null && !sheet.IsBusy,
                "Acciaio: Calcola pubblica un risultato o chiama il servizio");
            sheet.SelectedType = "FeB22k";
            expect.That(service.Calls == 2 && service.Threads.All(t => t == ui) && sheet.Status.StartsWith("⚠", StringComparison.Ordinal) && !sheet.HasDiagram,
                "Acciaio: scelta del tipo senza ricalcolo dei valori");
            service.Fail = true;
            sheet.PropertiesForm["steel_eps_u"].EditText = "30";
            expect.That(service.Calls == 3 && sheet.Status == "⚠ motore di prova" && sheet.Results == "fyd     —\nεyd     —\nfu / fyk     —" && !sheet.HasDiagram,
                "Acciaio: errore del servizio");
            service.Fail = false;
            sheet.Reference = "Certificato";
            expect.That(service.Calls == 3, "Acciaio: il riferimento ricalcola");
        }
        var unused = new CountingService(MaterialScenarios.Concrete);
        using (var sheet = Concrete(MaterialScenarios.FixtureData(root, MaterialScenarios.Concrete, "ec2-riduzioni-tolleranza"), unused))
        {
            sheet.Inputs["diameter"].EditText = "24"; sheet.Commit();
            expect.That(!await Expect.Within(sheet.CalculateAsync()) && unused.Calls == 0 && !sheet.HasResults && sheet.CalculationState == CalculationState.Idle,
                "Calcestruzzo: il calcolo del modulo è chiamato o pubblicato");
        }
    }

    // ---------------------------------------------------------------- preview

    /// <summary>The preview of the contract (ISheetViewModel.PreviewData): the data for the steel, the state not committed for the concrete.</summary>
    private static void Preview(string root, Expect expect)
    {
        using (var steel = Steel(MaterialScenarios.FixtureData(root, MaterialScenarios.Steel, "b450a")))
            expect.That(ReferenceEquals(((ISheetViewModel)steel).PreviewData, steel.Data), "Anteprima dell'acciaio diversa dai dati");
        using var concrete = Concrete(MaterialScenarios.FixtureData(root, MaterialScenarios.Concrete, "default"));
        ISheetViewModel sheet = concrete;
        var first = sheet.PreviewData;
        expect.That(!ReferenceEquals(first, sheet.PreviewData) && JsonNode.DeepEquals(first, sheet.PreviewData) && !JsonNode.DeepEquals(first, sheet.Data),
            "Anteprima del calcestruzzo: oggetto nuovo, uguale a sé stessa, diversa dai dati non confermati");
        concrete.Commit();
        expect.That(JsonNode.DeepEquals(sheet.PreviewData, sheet.Data), "Anteprima del calcestruzzo diversa dai dati confermati");
    }

    /// <summary>The module service, counted (calls and threads); <see cref="Fail"/> makes it throw as an invalid input does.</summary>
    private sealed class CountingService(string module) : IModuleService
    {
        private readonly IModuleService inner = ModuleServices.Get(module);
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
