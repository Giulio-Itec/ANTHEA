using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Modules;
using Anthea.Application.Stratigraphy;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.Shared;
using Anthea.Presentation.Modules.Stratigraphy;
using Anthea.Presentation.Services;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of the stratigraphy (F5.6b) against its historic view, without WPF. This is the evidence of equivalence that survives
/// the archiving of the historic view:
/// <list type="bullet">
/// <item>every scenario of tests/Shared/StratigraphyTrace.cs gives the trace of the historic view frozen in tests/fixtures/f5-stratigrafia/storica,
/// line by line: data in memory order, Modified, messages and questions, errors, import button, fields, actions, layers, status, profile
/// and the transfer of the soil. The soil transfer dialog is the shared ViewModel <see cref="SoilTransferDialogViewModel"/> (F5.6, decision
/// T5), driven as the historic trace drives the historic window: the trace proves what the sheet passes to it, what the dialog does on its
/// two paths and what the sheet does with its answer;</item>
/// <item>for each F3 fixture of the module the data after opening, visit, calculation, export and reopening, the result, the status and
/// the text and structure of the report are byte-identical to the F3 reference of the historic view;</item>
/// <item>module and registry, persisted keys, read-only, Reload, closing, the AI import with a scripted source of the drafts (success,
/// error, cancellation, review, application; never the network) and the routing of the soil transfer.</item>
/// </list>
/// One "UI" thread (SingleThreadContext), as the dispatcher.
/// </summary>
internal static class StratigraphySheetChecks
{
    internal static int Run(string root, string output) => SingleThreadContext.Run(_ => RunAsync(root, output));

    /// <summary>Texts of the buttons of the historic view (view texts, not of the ViewModel).</summary>
    private const string ImportLabel = "Importa relazione PDF con AI…", TransferLabel = "Invia / importa terreno da ANTHEA…";
    private static readonly string[] ActionLabels = ["+ Strato", "Duplica", "Elimina", "↑", "↓"];

    private static StratigraphySheetOptions Options(ISoilTransferDialog? transfer = null, IStratigraphyDraftSource? drafts = null, bool importEnabled = false) =>
        new(drafts ?? new ScriptedDrafts(), () => new StratigraphyImportDefaults("modello-di-prova", "chiave-di-prova"), transfer ?? new RefusedTransfer(), importEnabled);

    private static StratigraphySheetViewModel Open(JsonObject data, PresentationServices? services = null, ISheetHost? host = null, StratigraphySheetOptions? options = null,
        bool readOnly = false)
    {
        var context = new SheetContext((JsonObject)data.DeepClone(), readOnly, host ?? new Host(), services ?? Scripted.Services(), ModuleServices.Get(StratigraphyScenarios.Module),
            new CalculationLimiter(1));
        return (StratigraphySheetViewModel)new StratigraphySheetModule(options ?? Options()).CreateSheet(context);
    }

    private static async Task<int> RunAsync(string root, string output)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        // The culture of the captures of the historic view (F3 and traces).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            string traces = Path.Combine(output, "stratigrafia");
            Directory.CreateDirectory(traces);
            foreach (var scenario in StratigraphyScenarios.All(root))
            {
                var lines = await TraceAsync(root, scenario);
                StratigraphyTrace.Write(Path.Combine(traces, scenario.File), lines);
                string stored = Path.Combine(StratigraphyScenarios.Reference(root), scenario.File);
                expect.That(File.Exists(stored), "Traccia della vista storica assente: " + stored);
                string? difference = StratigraphyTrace.FirstDifference(StratigraphyTrace.Read(stored), lines);
                expect.That(difference is null, "Stratigrafia MVVM diversa dalla vista storica, scenario " + scenario.File + ", " + difference);
            }
            var fixtures = StratigraphyScenarios.Fixtures(root);
            expect.That(fixtures.Count == 5, "Fixture F3 della stratigrafia");
            foreach (string id in fixtures) await F3Async(root, traces, id, expect);
            Contract(expect);
            StateKeys(root, expect);
            await ReadOnlyAsync(root, expect);
            Reload(root, expect);
            Closing(root, expect);
            await ImportAsync(root, output, expect);
            await TransferAsync(root, expect);
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task<List<string>> TraceAsync(string root, StratigraphyScenario scenario)
    {
        var lines = new List<string>();
        var data = StratigraphyScenarios.FixtureData(root, scenario.Fixture);
        SoilSource[] sources = scenario.Source is null ? []
            : [new(scenario.SourceLabel, StratigraphyScenarios.Module, StratigraphyScenarios.FixtureData(root, scenario.Source), StratigraphyScenarios.SourceId)];
        var guids = new StratigraphyGuids([data, .. sources.Select(s => s.Data)]);
        var side = new Side(data, sources);
        try
        {
            lines.AddRange(side.State(null).Lines("apertura", guids));
            foreach (var step in scenario.Steps)
            {
                string? error = await side.ApplyAsync(step);
                lines.AddRange(side.State(error).Lines(step.ToString(), guids));
            }
        }
        // A step the ViewModel cannot apply ends the trace: the comparison then shows the first different line before it.
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
        { lines.Add("errore della prova " + error.GetType().Name + ": " + error.Message); }
        finally { side.Sheet.Dispose(); }
        return lines;
    }

    /// <summary>
    /// The ViewModel driven as its view would drive it, step by step. The side is also the services of the sheet: messages and questions
    /// are recorded in order (the questions answered as the scenario says), the window records the copies and the replacements of the soil,
    /// and the soil transfer dialog is the shared <see cref="SoilTransferDialogViewModel"/>, driven as the historic trace drives the window.
    /// </summary>
    private sealed class Side : IMessageService, IConfirmationService, ISheetHost, ISoilTransferDialog
    {
        private readonly IReadOnlyList<SoilSource> sources;
        private readonly PresentationServices services;
        private readonly StratigraphySheetOptions options;
        private readonly List<string> messages = [], transfer = [];
        private readonly Queue<bool> answers = new();
        private StratigraphyStep? step;
        private int modified, sourcesGiven;
        private string? failure;
        private (string Module, JsonObject Data, string Name)? copy;
        private JsonObject? replacement;
        private (string Module, bool SheetData, int Sources, string Status)? dialog;

        internal Side(JsonObject data, IReadOnlyList<SoilSource> sources)
        {
            this.sources = sources;
            services = new PresentationServices(this, this, new ScriptedFileDialogs(), new ScriptedDialogs(), new RecordingClipboard(), new RecordingLinks(), new RecordingWiki());
            options = Options(this);
            Attach(Open(data, services, this, options));
        }

        internal StratigraphySheetViewModel Sheet { get; private set; } = null!;

        private void Attach(StratigraphySheetViewModel sheet)
        {
            Sheet = sheet;
            modified = 0;
            sheet.Modified += (_, _) => modified++;
        }

        private static FormViewModel? With(string key, params FormViewModel?[] forms) => forms.FirstOrDefault(f => f is not null && f.Contains(key));

        private static void Type(FormViewModel? form, string key, string text) { if (form is not null && form.Contains(key)) form[key].EditText = text; }

        private static void Choose(FormViewModel? form, string key, string text) { if (form is not null && form.Contains(key)) form[key].SelectedChoice = text; }

        internal async Task<string?> ApplyAsync(StratigraphyStep current)
        {
            step = current; answers.Clear(); transfer.Clear(); failure = null; copy = null; replacement = null; dialog = null;
            var editor = Sheet.Editor;
            try
            {
                switch (current.Kind)
                {
                    case StratigraphyStep.Commit: Sheet.Commit(); break;
                    // «Calcola» of the window: MvvmSheetEditor.CalculateAsync commits the sheet, then calculates (nothing for the stratigraphy).
                    case StratigraphyStep.Calculate: Sheet.Commit(); await Expect.Within(Sheet.CalculateAsync()); break;
                    case StratigraphyStep.ReadOnly: Sheet.IsReadOnly = true; break;
                    case StratigraphyStep.Reopen:
                        var data = Sheet.Data;
                        Sheet.Dispose();
                        Attach(Open(data, services, this, options));
                        break;
                    case StratigraphyStep.Field: Type(editor.GeneralForm, current.Key!, current.Text!); break;
                    case StratigraphyStep.Choice: Choose(editor.GeneralForm, current.Key!, current.Text!); break;
                    case StratigraphyStep.Select: editor.SelectedLayerIndex = current.Index; break;
                    case StratigraphyStep.LayerField: Type(With(current.Key!, editor.LayerForm, editor.ThicknessForm, editor.MeansForm, editor.NotesForm), current.Key!, current.Text!); break;
                    case StratigraphyStep.LayerChoice: Choose(With(current.Key!, editor.LayerForm, editor.MeansForm), current.Key!, current.Text!); break;
                    case StratigraphyStep.MinimumField: Type(editor.MinimaForm, current.Key!, current.Text!); break;
                    case StratigraphyStep.MinimumChoice: Choose(editor.MinimaForm, current.Key!, current.Text!); break;
                    case StratigraphyStep.Infinite: editor.ToggleInfinite(); break;
                    case StratigraphyStep.Minima:
                        if (current.Text is not null) answers.Enqueue(current.Text == "sì");
                        editor.ToggleMinimum();
                        break;
                    case StratigraphyStep.Add: editor.AddLayer(); break;
                    case StratigraphyStep.Duplicate: editor.DuplicateLayer(); break;
                    case StratigraphyStep.Remove: editor.RemoveLayer(); break;
                    case StratigraphyStep.Up: editor.MoveLayerUp(); break;
                    case StratigraphyStep.Down: editor.MoveLayerDown(); break;
                    case StratigraphyStep.Import: if (Sheet.ImportCommand.CanExecute(null)) await Expect.Within(Sheet.ImportCommand.ExecuteAsync(null)); break;
                    case StratigraphyStep.Send or StratigraphyStep.Replace:
                        if (!Sheet.TransferSoilCommand.CanExecute(null)) break;
                        await Expect.Within(Sheet.TransferSoilCommand.ExecuteAsync(null));
                        if (dialog is { } shown)
                            transfer.AddRange(StratigraphyTrace.Transfer(shown.Module, shown.SheetData, shown.Sources, copy?.Module, copy?.Name, copy?.Data ?? replacement,
                                replacement is not null, shown.Status));
                        break;
                    default: throw new ArgumentException("Passo sconosciuto: " + current.Kind);
                }
                return failure;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return StratigraphyTrace.Error(error); }
        }

        internal StratigraphyTraceState State(string? error)
        {
            var sheet = Sheet;
            var editor = sheet.Editor;
            var state = new StratigraphyTraceState
            {
                Data = sheet.Data, Modified = modified, Error = error,
                ImportVisible = editor.ImportVisible, ImportLabel = ImportLabel, ImportEnabled = sheet.CanImport, ImportToolTip = sheet.ImportToolTip,
                General = Controls(editor.GeneralForm, ""),
                Actions = ActionLabels.Select(label => (label, editor.CanEdit)).ToList(),
                Layers = editor.Layers.Select(row => row.Values.S("name")).ToList(), Selected = editor.SelectedLayerIndex,
                Status = editor.Status, Profile = editor.Profile is not null, ProfileSelection = editor.ProfileSelection,
                TransferLabel = TransferLabel, TransferUsable = sheet.TransferSoilCommand.CanExecute(null),
            };
            state.Messages.AddRange(messages); messages.Clear();
            if (editor.LayerForm is { } identity)
            {
                state.Layer.AddRange(Controls(identity, ""));
                state.Layer.Add(new("opzione", "infinite", StratigraphyFields.InfiniteLabel, StratigraphyTrace.YesNo(editor.IsInfinite), editor.CanToggleInfinite));
                if (editor.ThicknessForm is { } thickness) state.Layer.AddRange(Controls(thickness, ""));
                state.Layer.Add(new("opzione", "minimum", StratigraphyFields.MinimaLabel, StratigraphyTrace.YesNo(editor.HasMinimum), editor.CanEdit));
                state.Layer.AddRange(Controls(editor.MeansForm!, ""));
                if (editor.MinimaForm is { } minima) state.Layer.AddRange(Controls(minima, "minimum."));
                else state.MinimaNote = StratigraphyPresentation.MinimaNote;
                state.Layer.AddRange(Controls(editor.NotesForm!, ""));
            }
            state.Transfer.AddRange(transfer);
            return state;
        }

        private static List<StratigraphyControl> Controls(FormViewModel form, string prefix) => form.Fields.Select(field => field.Kind == FieldKind.Choice
            ? new StratigraphyControl("scelta", prefix + field.Key, field.Label, field.SelectedChoice ?? "-", field.CanChange && field.IsEnabled, field.Choices)
            : new StratigraphyControl("testo", prefix + field.Key, field.Label, field.Text, !field.IsInputReadOnly && field.IsEnabled)).ToList();

        // ------------------------------------------------ messages and questions

        public void ReportFailure(string title, Exception error) => failure = StratigraphyTrace.Error(error);
        public void Inform(string title, string message) => messages.Add(StratigraphyTrace.Message(title, message));
        public void Warn(string title, string message) => messages.Add(StratigraphyTrace.Message(title, message));

        public bool ConfirmDelete(string title, string question)
        {
            bool yes = answers.Count > 0 && answers.Dequeue();
            messages.Add(StratigraphyTrace.Question(title, question, yes));
            return yes;
        }

        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => throw new InvalidOperationException("Domanda non prevista: dati condivisi.");
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => throw new InvalidOperationException("Domanda non prevista: spostamento.");
        public bool ConfirmClose(Func<bool> saveOrDiscard) => throw new InvalidOperationException("Domanda non prevista: chiusura.");
        public SaveChangesAnswer AskSaveChanges() => throw new InvalidOperationException("Domanda non prevista: salvataggio.");

        // ------------------------------------------------ window

        public void OpenModuleCopy(string module, JsonObject data, string name) => copy = (module, data, name);
        public void ReplaceSoil(JsonObject data) => replacement = data;
        public IReadOnlyList<SoilSource> SoilSources() { sourcesGiven = sources.Count; return sources; }
        public void RequestReport() => throw new InvalidOperationException("Report non previsto.");

        // ------------------------------------------------ the shared soil transfer dialog (F5.6, T5)

        /// <summary>
        /// The shared dialog <see cref="SoilTransferDialogViewModel"/> (the ViewModel of the historic SoilTransferDialog, F5.6), driven as the
        /// historic trace drives the window (tests/Desktop/StratigraphyHistoricTrace.cs): at opening the profile of the sheet (or the error in
        /// its status); «Importa dal foglio selezionato» (first sheet of the project, when there is one); the scenario of the step; then «Invia
        /// a un nuovo foglio» (destination of the step) or «Sostituisci il terreno del foglio corrente» (the question answered yes). The two
        /// buttons work only with a profile; an error stays in the status and the dialog is closed without a choice.
        /// </summary>
        public Task<SoilTransferChoice?> ShowAsync(string module, JsonObject data, IReadOnlyList<SoilSource> given)
        {
            var current = step ?? throw new InvalidOperationException("Dialogo del terreno fuori da un passo.");
            var shared = new SoilTransferDialogViewModel(module, data, given, new ScriptedFileDialogs(), this);
            bool closed = false;
            shared.CloseRequested += (_, _) => closed = true;
            static void Press(CommunityToolkit.Mvvm.Input.IRelayCommand command) { if (command.CanExecute(null)) command.Execute(null); }
            if (current.Kind == StratigraphyStep.Replace && current.Key == "progetto") Press(shared.ImportSourceCommand);
            shared.Scenario = current.Text!;
            if (current.Kind == StratigraphyStep.Send) { shared.SelectedDestination = current.Key!; Press(shared.SendCommand); }
            else Press(shared.ApplyCommand);
            var choice = closed ? shared.Result : null;
            dialog = (module, JsonNode.DeepEquals(data, Sheet.Data), sourcesGiven, closed ? "" : shared.Status);
            return Task.FromResult(choice);
        }

        /// <summary>The question of «Sostituisci il terreno del foglio corrente», answered yes as the historic trace answers it.</summary>
        public bool ConfirmReplace(string title, string question)
        {
            messages.Add(StratigraphyTrace.Question(title, question, true));
            return true;
        }
    }

    // ---------------------------------------------------------------- F3 reference

    /// <summary>
    /// The path of ui/f3-equivalence through the ViewModel (opened, visited, calculated, result, report, exported, reopened), compared
    /// with the F3 reference of the historic view in the canonical form of the capture. The stratigraphy has no F4 transition.
    /// </summary>
    private static async Task F3Async(string root, string output, string id, Expect expect)
    {
        string module = StratigraphyScenarios.Module;
        string folder = Path.Combine(root, "tests", "fixtures", "f3", "expected", "sheets", module, id);
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        string Canonical(JsonNode? node) => CanonicalJson.Serialize(node, new GuidMap(), normalizer).Replace("\r\n", "\n");
        string Expected(string name) => File.ReadAllText(Path.Combine(folder, name), Encoding.UTF8).Replace("\r\n", "\n");
        void Same(string name, string actual) => expect.That(actual == Expected(name), module + "/" + id + ": " + name + " diverso dal riferimento F3 della vista storica");
        using var sheet = Open(StratigraphyScenarios.FixtureData(root, id));
        sheet.Commit(); Same("opened.json", Canonical(sheet.Data));
        sheet.Commit(); Same("visited.json", Canonical(sheet.Data));
        await Expect.Within(sheet.CalculateAsync()); sheet.Commit();
        Same("calculated-data.json", Canonical(sheet.Data)); Same("result.json", Canonical(sheet.Result));
        var status = new JsonObject { ["hasResults"] = sheet.HasResults };
        byte[] report = sheet.Report.BuildReport("Fixture F3 · " + module + " · " + id, new HashSet<string>());
        status["report"] = "available";
        string text = DocxText.Extract(report, normalizer, new GuidMap()).Replace("\r\n", "\n");
        File.WriteAllText(Path.Combine(output, module + "--" + id + ".report.txt"), text, new UTF8Encoding(false));
        Same("report.txt", text);
        Same("report-structure.json", Canonical(Structure(report)));
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
        var module = new StratigraphySheetModule(Options());
        expect.That(module.Info == ModuleDirectory.Get(StratigraphyScenarios.Module) && module.Reports == new ModuleReportCapabilities(false)
            && module.Help.GuideId == ModuleHelp.For(StratigraphyScenarios.Module).GuideId && module.Help.GuideId is not null
            && module.Persistence.UiStateKeys.Count == 0, "Modulo della stratigrafia");
        expect.Throws<ArgumentNullException>(() => _ = new StratigraphySheetModule(null!), "Modulo senza opzioni");
        var registry = new ModuleRegistry([module]);
        expect.That(registry.SheetModuleIds.SequenceEqual([StratigraphyScenarios.Module]) && ReferenceEquals(registry.SheetModule(StratigraphyScenarios.Module), module),
            "Registro con la stratigrafia");
        using var sheet = Open(StratigraphyDocument.Create());
        expect.That(sheet.ModuleId == StratigraphyScenarios.Module && sheet.CalculationMode == SheetCalculationMode.None && sheet.AutomaticDelay is null
            && !sheet.CanImport && sheet.ImportToolTip == StratigraphyImport.Disabled && sheet.Editor.ImportVisible && !sheet.ImportCommand.CanExecute(null),
            "Foglio: senza calcolo, importazione spenta come nella Release");
        expect.That(ReferenceEquals(((ISheetViewModel)sheet).PreviewData, sheet.Data), "Anteprima diversa dai dati");
    }

    /// <summary>
    /// The keys written by the sheet: only keys of the document (the editor never adds interface state); a behaviour chosen in a layer without
    /// it is added at the end of the layer, as the view does.
    /// </summary>
    private static void StateKeys(string root, Expect expect)
    {
        var fixture = StratigraphyScenarios.FixtureData(root, "testi-digitati");
        using var sheet = Open(fixture);
        var editor = sheet.Editor;
        editor.GeneralForm["name"].EditText = "Nuovo nome"; editor.GeneralForm["scenario"].SelectedChoice = "Entrambi";
        editor.LayerForm!["behavior"].SelectedChoice = "Granulare"; editor.MeansForm!["gamma"].EditText = "19";
        editor.SelectedLayerIndex = 1; editor.MinimaForm!["notes"].EditText = "Nota";
        sheet.Commit();
        var layer = StratigraphyDocument.Layers(sheet.Data)[0]!.AsObject();
        expect.That(sheet.Data.Select(p => p.Key).SequenceEqual(fixture.Select(p => p.Key)) && layer.Select(p => p.Key).Last() == "behavior"
            && layer.Count == StratigraphyDocument.Layers(fixture)[0]!.AsObject().Count + 1, "Chiavi scritte dalla scheda");
    }

    // ---------------------------------------------------------------- read-only

    /// <summary>A previous revision: every edit is refused, the selection of the layers stays; back to editable, the edits are accepted.</summary>
    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        var host = new Host();
        using var sheet = Open(StratigraphyScenarios.FixtureData(root, "minimi-entrambi-indefinito"), host: host, readOnly: true);
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var editor = sheet.Editor;
        string before = sheet.Data.ToJsonString();
        editor.GeneralForm["name"].EditText = "Altro"; editor.SelectedLayerIndex = 2; editor.LayerForm!["name"].EditText = "Altro";
        editor.ToggleInfinite(); editor.ToggleMinimum(); editor.AddLayer(); editor.DuplicateLayer(); editor.RemoveLayer(); editor.MoveLayerUp(); editor.MoveLayerDown();
        await Expect.Within(sheet.TransferSoilCommand.ExecuteAsync(null));
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && editor.SelectedLayerIndex == 2 && !editor.CanEdit && !editor.CanToggleInfinite
            && editor.Forms.All(f => f.IsReadOnly) && !sheet.TransferSoilCommand.CanExecute(null) && host.Requests.Count == 0, "Modifica accettata in sola lettura");
        sheet.Commit();
        expect.That(sheet.Data.ToJsonString() == before, "Conferma in sola lettura");
        sheet.IsReadOnly = false;
        editor.LayerForm!["name"].EditText = "Calcare";
        expect.That(modified == 1 && StratigraphyDocument.Layers(sheet.Data)[2]!["name"]!.ToString() == "Calcare" && editor.Forms.All(f => !f.IsReadOnly)
            && editor.CanToggleInfinite && sheet.TransferSoilCommand.CanExecute(null), "Ritorno alla modifica");
    }

    // ---------------------------------------------------------------- reload

    /// <summary>Data replaced from outside: the editor again as at opening (first layer), no Modified, new revision; edits go on.</summary>
    private static void Reload(string root, Expect expect)
    {
        using var sheet = Open(StratigraphyScenarios.FixtureData(root, "profilo-medi-falda"));
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        sheet.Editor.SelectedLayerIndex = 2;
        sheet.Editor.LayerForm!["name"].EditText = "Limo";
        long revision = sheet.Revision;
        var other = StratigraphyScenarios.FixtureData(root, "minimi-entrambi-indefinito");
        sheet.Reload(other);
        using var reopened = Open(other);
        expect.That(modified == 1 && sheet.Revision > revision && sheet.Data.ToJsonString() == other.ToJsonString() && sheet.Editor.SelectedLayerIndex == 0
            && sheet.Editor.Layers.Count == 3 && sheet.Editor.Status == reopened.Editor.Status && ReferenceEquals(sheet.Editor.Data, sheet.Data)
            && sheet.Editor.GeneralForm["name"].Text == "Profilo P2 · medi e minimi", "Reload diverso da una riapertura");
        sheet.Editor.GeneralForm["name"].EditText = "P2";
        expect.That(modified == 2 && sheet.Data.S("name") == "P2", "Modifica dopo Reload");
    }

    // ---------------------------------------------------------------- closing

    /// <summary>Closed: edits are ignored, nothing is written, Commit fails as for every sheet, the commands are disabled.</summary>
    private static void Closing(string root, Expect expect)
    {
        var sheet = Open(StratigraphyScenarios.FixtureData(root, "profilo-medi-falda"));
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var editor = sheet.Editor;
        string data = sheet.Data.ToJsonString();
        sheet.Dispose();
        editor.GeneralForm["name"].EditText = "Altro"; editor.AddLayer(); editor.ToggleMinimum();
        expect.That(sheet.Data.ToJsonString() == data && modified == 0 && editor.IsReadOnly && !sheet.TransferSoilCommand.CanExecute(null), "Modifica dopo la chiusura");
        expect.Throws<ObjectDisposedException>(sheet.Commit, "Commit dopo la chiusura");
    }

    // ---------------------------------------------------------------- AI import

    /// <summary>
    /// The import with a scripted source of the drafts (never the network): disabled in the Release; the file dialog; success with the first
    /// draft shown and its review; a change of the draft clears the review; a draft that is not valid stays; the applied draft replaces the
    /// data of the sheet (Modified once, editor again); errors of the extraction in the message; cancellation by «Interrompi» and by the
    /// closing of the dialog; the key cleared at the end.
    /// </summary>
    private static async Task ImportAsync(string root, string output, Expect expect)
    {
        string pdf = Path.Combine(output, "relazione-f56b.pdf");
        File.WriteAllText(pdf, "%PDF-1.4\nsynthetic transport fixture");
        var source = new ScriptedDrafts();
        StratigraphyImportDialogViewModel? shown = null;
        var files = new ScriptedFileDialogs();
        var dialogs = new ScriptedDialogs();
        var messages = new ScriptedMessages();
        var services = Scripted.Services(messages: messages, files: files, dialogs: dialogs);
        using (var sheet = Open(StratigraphyScenarios.FixtureData(root, "profilo-medi-falda"), services, options: Options(drafts: source, importEnabled: true)))
        {
            int modified = 0;
            sheet.Modified += (_, _) => modified++;
            expect.That(sheet.CanImport && sheet.ImportToolTip == "" && sheet.ImportCommand.CanExecute(null), "Importazione attivata dal modulo");
            // Success: file, extraction, review, application.
            var draft = Draft("S1 · bozza", "Controllare le unità");
            source.Next = _ => Task.FromResult<IReadOnlyList<JsonObject>>([draft, Draft("S2", "")]);
            files.Then(pdf);
            dialogs.Then(d =>
            {
                var import = shown = (StratigraphyImportDialogViewModel)d;
                expect.That(import.Title == StratigraphyImport.Title && import.Message == StratigraphyImport.Ready && import.Model == "modello-di-prova"
                    && import.ApiKey == "chiave-di-prova" && !import.CanApply && !import.ApplyCommand.CanExecute(null), "Finestra dell'importazione all'apertura");
                import.BrowseCommand.Execute(null);
                var extraction = import.ExtractCommand.ExecuteAsync(null);
                expect.That(extraction.IsCompleted && import.File == pdf && import.Profiles.SequenceEqual(["1 · S1 · bozza", "2 · S2"]) && import.SelectedProfileIndex == 0
                    && import.Message == "Bozza AI da controllare · Controllare le unità" && import.Preview is { ImportVisible: false } && !import.CanApply,
                    "Bozze estratte");
                expect.That(source.Calls.Single() == (pdf, "chiave-di-prova", "modello-di-prova"), "Richiesta dell'estrazione");
                import.Reviewed = true;
                expect.That(import.CanApply && import.ApplyCommand.CanExecute(null), "Bozza revisionata applicabile");
                import.Preview!.GeneralForm["name"].EditText = "S1 corretta";
                expect.That(!import.Reviewed && !import.CanApply, "Modifica della bozza senza azzerare la revisione");
                import.Reviewed = true;
                import.ApplyCommand.Execute(null);
            });
            await Expect.Within(sheet.ImportCommand.ExecuteAsync(null));
            expect.That(modified == 1 && sheet.Data.S("name") == "S1 corretta" && sheet.Data["import_source"]!["reviewed"]!.GetValue<bool>()
                && draft["import_source"]!["reviewed"]!.GetValue<bool>() == false && sheet.Editor.Data == sheet.Data && sheet.Editor.SelectedLayerIndex == 0
                && shown!.IsDisposed && shown.ApiKey == "", "Bozza applicata");
            // Errors of the extraction, a draft that is not valid, cancellation by «Interrompi»; the dialog is closed without a result.
            var gate = new TaskCompletionSource<IReadOnlyList<JsonObject>>(TaskCreationOptions.RunContinuationsAsynchronously);
            dialogs.Then(d =>
            {
                var import = (StratigraphyImportDialogViewModel)d;
                source.Next = _ => Task.FromException<IReadOnlyList<JsonObject>>(new HttpRequestException("rete"));
                expect.That(import.ExtractCommand.ExecuteAsync(null).IsCompleted && import.Message == StratigraphyImport.ConnectionFailed && import.Profiles.Count == 0,
                    "Errore di connessione");
                source.Next = _ => Task.FromException<IReadOnlyList<JsonObject>>(new ArgumentException("Inserire chiave API e modello OpenAI compatibile con PDF e output strutturato."));
                _ = import.ExtractCommand.ExecuteAsync(null);
                expect.That(import.Message == "Inserire chiave API e modello OpenAI compatibile con PDF e output strutturato.", "Errore dei dati dell'importazione");
                var broken = Draft("S3", "");
                StratigraphyDocument.Layers(broken)[0]!["thickness"] = null;
                source.Next = _ => Task.FromResult<IReadOnlyList<JsonObject>>([broken]);
                _ = import.ExtractCommand.ExecuteAsync(null);
                import.Reviewed = true; import.ApplyCommand.Execute(null);
                expect.That(import.Message == "Correggere la bozza prima di applicarla: Strato 1: lo spessore deve essere positivo." && import.Result is null,
                    "Bozza non valida applicata");
                source.Next = token => { token.Register(() => gate.TrySetCanceled(token)); return gate.Task; };
                var running = import.ExtractCommand.ExecuteAsync(null);
                expect.That(!running.IsCompleted && import.IsRunning && !import.BrowseCommand.CanExecute(null) && !import.ExtractCommand.CanExecute(null)
                    && import.InterruptCommand.CanExecute(null) && !import.CanApply && import.Message == StratigraphyImport.Reading, "Estrazione in corso");
                import.Model = "altro";
                expect.That(import.Model == "modello-di-prova", "Modello cambiato durante l'estrazione");
                import.InterruptCommand.Execute(null);
                shown = import;
                // The dialog stays open until the interruption has been shown: the closing comes from the next step of the check.
            });
            await Expect.Within(sheet.ImportCommand.ExecuteAsync(null));
            await SettleAsync(() => !shown!.IsRunning);
            expect.That(shown!.Message == StratigraphyImport.Interrupted && !shown.IsRunning && shown.IsDisposed && modified == 1 && sheet.Data.S("name") == "S1 corretta"
                && messages.Failures.Count == 0, "Interruzione dell'estrazione");
            // The closing of the dialog during an extraction cancels it.
            var held = new TaskCompletionSource<IReadOnlyList<JsonObject>>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken seen = default;
            dialogs.Then(d =>
            {
                var import = (StratigraphyImportDialogViewModel)d;
                source.Next = token => { seen = token; token.Register(() => held.TrySetCanceled(token)); return held.Task; };
                _ = import.ExtractCommand.ExecuteAsync(null);
                shown = import;
                import.Dismiss();
            });
            await Expect.Within(sheet.ImportCommand.ExecuteAsync(null));
            expect.That(seen.IsCancellationRequested && held.Task.IsCanceled, "Estrazione non annullata alla chiusura");
            await SettleAsync(() => !shown!.IsRunning);
            expect.That(seen.IsCancellationRequested && shown!.IsDisposed && modified == 1, "Chiusura durante l'estrazione");
        }
        // The Release: disabled, the command does nothing.
        using (var sheet = Open(StratigraphyScenarios.FixtureData(root, "profilo-medi-falda"), Scripted.Services()))
        {
            await Expect.Within(sheet.ImportCommand.ExecuteAsync(null));
            expect.That(!sheet.CanImport && sheet.ImportToolTip == StratigraphyImport.Disabled, "Importazione della Release");
        }
    }

    /// <summary>
    /// Lets the continuations queued on the UI thread run (the end of an extraction cancelled by the dialog) until <paramref name="done"/>,
    /// within the deadline of the checks: nothing is left running when the suite ends.
    /// </summary>
    private static async Task SettleAsync(Func<bool> done)
    {
        var deadline = DateTime.UtcNow + Expect.Deadline;
        while (!done())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Estrazione dell'importazione non conclusa.");
            await Task.Yield();
        }
    }

    /// <summary>A draft as the importer gives it: a valid stratigraphy named <paramref name="name"/> with the provenance of the import.</summary>
    private static JsonObject Draft(string name, string warnings)
    {
        var draft = StratigraphyDocument.Create();
        draft["name"] = name;
        draft["import_source"] = new JsonObject { ["file"] = "relazione.pdf", ["sha256"] = "", ["model"] = "modello-di-prova", ["date"] = "", ["warnings"] = warnings, ["reviewed"] = false };
        return draft;
    }

    // ---------------------------------------------------------------- soil transfer

    /// <summary>
    /// The routing of the soil transfer: the sheet commits first (an invalid document is shown as the error of the command and the dialog does
    /// not open), passes its module, its data and the sheets of the project, then a copy goes to OpenModuleCopy, a replacement to ReplaceSoil,
    /// and nothing when the dialog closes without a choice.
    /// </summary>
    private static async Task TransferAsync(string root, Expect expect)
    {
        var host = new Host { Sources = [new("Sondaggio S1", StratigraphyScenarios.Module, StratigraphyScenarios.FixtureData(root, "profilo-medi-falda"), "id1")] };
        var transfer = new ScriptedTransfer();
        var messages = new ScriptedMessages();
        using var sheet = Open(StratigraphyScenarios.FixtureData(root, "testi-digitati"), Scripted.Services(messages: messages), host, Options(transfer));
        var copy = new JsonObject { ["copia"] = true };
        transfer.Next = new SoilTransferChoice(copy, "geo_palo_verticale", "Terreno da Stratigrafia digitata · Medi");
        await Expect.Within(sheet.TransferSoilCommand.ExecuteAsync(null));
        expect.That(transfer.Calls.Single() is var call && call.Module == StratigraphyScenarios.Module && ReferenceEquals(call.Data, sheet.Data) && call.Sources.Single().Id == "id1"
            && host.Requests.Single() == "copia geo_palo_verticale «Terreno da Stratigrafia digitata · Medi»" && ReferenceEquals(host.Copied, copy), "Invio a un nuovo foglio");
        var replacement = new JsonObject { ["sostituito"] = true };
        transfer.Next = new SoilTransferChoice(replacement);
        await Expect.Within(sheet.TransferSoilCommand.ExecuteAsync(null));
        expect.That(host.Requests.Last() == "sostituzione" && ReferenceEquals(host.Replaced, replacement), "Sostituzione del terreno");
        transfer.Next = null;
        await Expect.Within(sheet.TransferSoilCommand.ExecuteAsync(null));
        expect.That(host.Requests.Count == 2 && transfer.Calls.Count == 3, "Dialogo chiuso senza scelta");
        sheet.Editor.GeneralForm["name"].EditText = "";
        await Expect.Within(sheet.TransferSoilCommand.ExecuteAsync(null));
        expect.That(transfer.Calls.Count == 3 && messages.Failures.Single() is var failure && failure.Title == "Operazione non completata"
            && failure.Error is ArgumentException && failure.Error.Message == "Assegnare un nome alla stratigrafia.", "Conferma non valida prima del dialogo");
    }

    // ---------------------------------------------------------------- doubles

    /// <summary>The source of the drafts, scripted: never the network or a real AI service. <see cref="Next"/> answers every request.</summary>
    private sealed class ScriptedDrafts : IStratigraphyDraftSource
    {
        internal Func<CancellationToken, Task<IReadOnlyList<JsonObject>>> Next { get; set; } = _ => throw new InvalidOperationException("Estrazione non prevista.");
        internal List<(string Path, string Key, string Model)> Calls { get; } = [];

        public Task<IReadOnlyList<JsonObject>> ExtractAsync(string path, string apiKey, string model, CancellationToken cancellation)
        {
            Calls.Add((path, apiKey, model));
            return Next(cancellation);
        }
    }

    /// <summary>The soil transfer dialog, scripted: records the calls and answers <see cref="Next"/>.</summary>
    private sealed class ScriptedTransfer : ISoilTransferDialog
    {
        internal SoilTransferChoice? Next { get; set; }
        internal List<(string Module, JsonObject Data, IReadOnlyList<SoilSource> Sources)> Calls { get; } = [];

        public Task<SoilTransferChoice?> ShowAsync(string module, JsonObject data, IReadOnlyList<SoilSource> sources)
        {
            Calls.Add((module, data, sources));
            return Task.FromResult(Next);
        }
    }

    /// <summary>A soil transfer dialog that must not open.</summary>
    private sealed class RefusedTransfer : ISoilTransferDialog
    {
        public Task<SoilTransferChoice?> ShowAsync(string module, JsonObject data, IReadOnlyList<SoilSource> sources) =>
            throw new InvalidOperationException("Dialogo del terreno non previsto.");
    }

    private sealed class Host : ISheetHost
    {
        internal IReadOnlyList<SoilSource> Sources { get; init; } = [];
        internal List<string> Requests { get; } = [];
        internal JsonObject? Copied { get; private set; }
        internal JsonObject? Replaced { get; private set; }

        public void OpenModuleCopy(string module, JsonObject data, string name) { Requests.Add("copia " + module + " «" + name + "»"); Copied = data; }
        public void ReplaceSoil(JsonObject data) { Requests.Add("sostituzione"); Replaced = data; }
        public IReadOnlyList<SoilSource> SoilSources() => Sources;
        public void RequestReport() => throw new InvalidOperationException("Report non previsto.");
    }
}
