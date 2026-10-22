using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Modules.PileGroup;
using Anthea.Presentation.Services;
using Anthea.Testing.Comparison;
using Anthea.Testing.Normalization;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The RC section (F5.10 S1) beyond the traces: the module and its sheet without a project and without a window (requirement of section 5),
/// read-only revision, closing during a calculation, the fixed order of the errors of parallel steps (C6), the Wiki of every field, the
/// requests of the non modal windows, Reload; with --sezione-tracce also the path of the F3 capture (data, result, report and short report,
/// status, exported data, reopened: S9) against the F3 reference and the sections sent by the walls.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static ConcreteSheetViewModel OpenSheet(JsonObject data, ISheetHost? host = null, PresentationServices? services = null, bool readOnly = false,
        ConcreteAnalysisSession? session = null) =>
        new(new SheetContext((JsonObject)data.DeepClone(), readOnly, host ?? new Host(), services ?? Scripted.Services(),
            ModuleServices.Get(ConcreteSectionSheetModule.Id), new CalculationLimiter(1)), session ?? new ConcreteAnalysisSession(), Figures);

    /// <summary>Waits until the sheet has nothing left to do (calculation, queue, quick resistances).</summary>
    private static async Task IdleAsync(ConcreteSheetViewModel sheet)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew(); int quiet = 0;
        while (quiet < 4)
        {
            await Task.Yield(); await Task.Delay(20); await Task.Yield();
            quiet = !sheet.IsBusy && !sheet.IsCalculationQueued && !sheet.Inputs.IsQuickResistanceRunning ? quiet + 1 : 0;
            if (watch.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("La sezione c.a. non si ferma: " + sheet.Status);
        }
    }

    private static void Contract(Expect expect)
    {
        var module = new ConcreteSectionSheetModule(Figures);
        expect.Throws<ArgumentNullException>(() => new ConcreteSectionSheetModule(null!), "Modulo senza disegni del report");
        expect.That(module.Info == ModuleDirectory.Get("str_palo") && module.Reports.ShortReport
            && module.Persistence.UiStateKeys.SequenceEqual(["tab", "layout", "quote_dimensioni", "quote_copriferro", "quote_interferro"]), "Modulo della sezione c.a.");
        var registry = new ModuleRegistry([new PileGroupSheetModule(), module]);
        expect.That(registry.SheetModuleIds.SequenceEqual(ModuleDirectory.All.Select(m => m.Id).Where(m => m is "geo_efficienza_orizzontale" or "str_palo")),
            "Registro con la sezione c.a. nell'ordine della directory");
        expect.Throws<ArgumentException>(() => new ConcreteSheetViewModel(new SheetContext(new JsonObject(), false, new Host(), Scripted.Services(),
            ModuleServices.Get("geo_palo_verticale"), new CalculationLimiter(1)), Figures), "Servizio di un altro modulo rifiutato");
    }

    // Section 5 (C4): the sheet works without a project and without a window; Commit leaves the complete data in the object of the context.
    private static async Task WithoutWindowAsync(string root, Expect expect)
    {
        var host = new StrictHost();
        var data = (JsonObject)ConcreteSectionScenarios.FixtureData(root, "default").DeepClone();
        var context = new SheetContext(data, false, host, Scripted.Services(), ModuleServices.Get(ConcreteSectionSheetModule.Id), new CalculationLimiter(1));
        using var sheet = (ConcreteSheetViewModel)new ConcreteSectionSheetModule(Figures).CreateSheet(context);
        expect.That(ReferenceEquals(sheet.Data, data) && sheet.CalculationMode == SheetCalculationMode.None && sheet.AutomaticDelay is null && sheet.IsCalculationQueued,
            "Foglio sui dati del contesto, modalità None, calcolo dell'apertura in coda");
        await IdleAsync(sheet);
        expect.That(sheet.HasResults && sheet.Result?["domini"] is JsonObject && host.Calls == 0, "Calcolo dell'apertura senza finestra e senza richieste all'ospite");
        bool published = await sheet.CalculateAsync();
        expect.That(!published || sheet.HasResults, "«Calcola» con le verifiche correnti");
        sheet.Commit();
        var stored = data["combinazioni"]!["SLU"]![0]!;
        expect.That(stored["visible"] is not null && stored["azioni"] is JsonArray && data["workspace_ca"]!["taglio"]!["azioni"] is JsonArray && host.Calls == 0,
            "Commit: righe delle azioni e del taglio nei dati del contesto");
        // S9: the report and the short report without a project and without a window, no request to the host.
        expect.That(sheet.Report.BuildReport("Report", ReportSections()).Length > 0 && ((IModuleShortReport)sheet.Report).BuildShortReport("Breve").Length > 0 && host.Calls == 0,
            "Report e report breve senza finestra e senza richieste all'ospite");
    }

    private static async Task ReadOnlyAsync(string root, Expect expect)
    {
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "circolare-cava"), readOnly: true);
        await IdleAsync(sheet);
        string before = sheet.Data.ToJsonString(); int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var inputs = sheet.Inputs;
        expect.That(inputs.AllForms.All(f => f.IsReadOnly) && !inputs.AddCableCommand.CanExecute(null) && !inputs.RestoreBarsCommand.CanExecute(null)
            && !inputs.NewConcreteMaterialCommand.CanExecute(null) && !inputs.ToggleHoleCommand.CanExecute(null) && inputs.Layers.All(l => !l.ToggleCommand.CanExecute(null))
            && inputs.OpenRheologyCommand.CanExecute(null) && inputs.Tendons.IsReadOnly && inputs.Bars.IsReadOnly, "Sola lettura: form e comandi protetti, Reologia disponibile");
        inputs.Geometry["width_mm"].EditText = "777"; inputs.Geometry["shape"].SelectedChoice = "Rettangolare"; inputs.SetDimension("quote_dimensioni", false);
        inputs.ShowAxes = false;
        expect.That(sheet.Data.ToJsonString() == before && modified == 0 && inputs.Drawing.Axes, "Sola lettura: nessuna modifica dei dati");
        sheet.SelectedTab = 1;
        expect.That(sheet.Data["workspace_ca"]!["tab"]!.ToString() == "1" && modified == 1, "Sola lettura: la scheda scrive ancora tab con Modified (storico, C5)");
        expect.That(sheet.HasResults && await sheet.CalculateAsync() == sheet.HasResults, "Sola lettura: «Calcola» ammesso");
        // The grids of the bars and of the cables follow the revision (F5.10, traces through the view: they stayed editable).
        var tables = new List<string>();
        inputs.Bars.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ConcreteTableViewModel.IsReadOnly)) tables.Add("barre"); };
        inputs.Tendons.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ConcreteTableViewModel.IsReadOnly)) tables.Add("trefoli"); };
        sheet.IsReadOnly = false;
        expect.That(inputs.AllForms.All(f => !f.IsReadOnly) && inputs.AddCableCommand.CanExecute(null), "Revisione modificabile di nuovo");
        expect.That(tables.SequenceEqual(["barre", "trefoli"]) && !inputs.Bars.IsReadOnly && !inputs.Tendons.IsReadOnly,
            "Tabelle delle barre e dei cavi: IsReadOnly notificato al cambio della revisione · " + string.Join(",", tables));
        sheet.IsReadOnly = true;
        expect.That(tables.Count == 4 && inputs.Bars.IsReadOnly && inputs.Tendons.IsReadOnly, "Tabelle delle barre e dei cavi di nuovo in sola lettura, notificate");
    }

    // Closing while a batch waits in the held engine: nothing is published, no error.
    private static async Task ClosingAsync(string root, Expect expect)
    {
        var session = new ConcreteAnalysisSession();
        var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), session: session);
        await IdleAsync(sheet);
        var gate = (SemaphoreSlim)typeof(ConcreteAnalysisSession).GetField("batches", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
        await gate.WaitAsync();
        try
        {
            sheet.Inputs.Geometry["width_mm"].EditText = "650";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!sheet.IsBusy) { await Task.Yield(); await Task.Delay(10); if (watch.Elapsed > Expect.Deadline) throw new TimeoutException("Nessun calcolo avviato."); }
            string status = sheet.Status;
            sheet.Dispose();
            expect.That(sheet.IsDisposed && !sheet.HasResults && sheet.Result is null, "Chiusura durante il calcolo: nessun risultato");
            gate.Release(); gate = null!;
            await Task.Delay(100); await Task.Yield();
            expect.That(sheet.Status == status && !sheet.HasResults, "Calcolo tardivo mai pubblicato dopo la chiusura");
        }
        finally { gate?.Release(); }
        expect.Throws<ObjectDisposedException>(() => sheet.Commit(), "Commit dopo la chiusura rifiutato");
    }

    // C6: two parallel steps that stop together give their errors in the fixed order of the checks, at every run.
    private static async Task ErrorOrderAsync(string root, Expect expect)
    {
        // The data of the default fixture as the sheet prepares them, with directions of the 2D domain that the engine refuses.
        JsonObject data;
        using (var prepared = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"))) data = (JsonObject)prepared.Data.DeepClone();
        data["workspace_ca"]!["dominio2d"]!["angoli"] = "abc";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int run = 0; run < 3; run++)
        {
            using var sheet = OpenSheet(data);
            await IdleAsync(sheet);
            seen.Add(sheet.Status);
            expect.That(sheet.Calculation.Errors.Keys.SequenceEqual(["2D:SLU", "2D:SLV"]), "Errori dei due domini 2D nell'ordine fisso: " + string.Join(", ", sheet.Calculation.Errors.Keys));
        }
        expect.That(seen.Count == 1 && seen.Single().StartsWith("Aggiornamento parziale · 2D:SLU: ", StringComparison.Ordinal) && seen.Single().Contains(" · 2D:SLV: ", StringComparison.Ordinal),
            "Stato degli errori parziali sempre uguale (C6): " + string.Join(" | ", seen));
    }

    // Lesson of 17/10: every form has the navigator of the services; F1 on every field reaches it (the view gives the module of the sheet).
    private static async Task HelpAsync(string root, Expect expect)
    {
        var wiki = new RecordingWiki();
        var services = new PresentationServices(new ScriptedMessages(), new ScriptedConfirmations(), new ScriptedFileDialogs(), new ScriptedDialogs(), new RecordingClipboard(),
            new RecordingLinks(), wiki);
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), services: services);
        await IdleAsync(sheet);
        var forms = sheet.Inputs.AllForms.Append(sheet.Inputs.CreateMaterialDialog("Calcestruzzo").Form).Append(sheet.Inputs.CreateWizardDialog().Form)
            .Concat([sheet.Domain3D.Form, sheet.Domain2D.Form, sheet.Domain3D.CreateScaleDialog().Form]).Concat(sheet.StressPanels.Select(p => p.Form))
            .Concat([sheet.Shear.Form, sheet.Shear.Torsion, sheet.Shear.Stirrups.Values, sheet.Shear.Stirrups.Scheme]).Concat(sheet.Detailing.AllForms)
            .Concat([sheet.Curvature.Form, sheet.Design.Form, sheet.Design.Limits, sheet.Design.Shown]).ToList();
        expect.That(forms.All(f => ReferenceEquals(f.WikiNavigator, wiki)), "Ogni form con il navigatore della Wiki dei servizi");
        int presses = 0;
        foreach (var form in forms)
        {
            form.InheritedWikiModule = ConcreteSectionSheetModule.Id;
            foreach (var field in form.Fields) { presses++; expect.That(field.OpenContextHelp(), "F1 senza navigatore: " + field.Key); }
        }
        expect.That(wiki.Shown.Count == presses && wiki.Shown.All(u => !string.IsNullOrEmpty(u)), "Ogni F1 arriva al navigatore registrato");
    }

    // «Reologia del calcestruzzo…» and «Proprietà / report…»: requests to the view; the properties write φ and n with Modified.
    private static async Task WindowsAsync(string root, Expect expect)
    {
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"));
        await IdleAsync(sheet);
        double? strength = null; ConcreteSectionPropertiesViewModel? properties = null; int modified = 0;
        sheet.RheologyRequested += (_, value) => strength = value;
        sheet.Inputs.OpenPropertiesCommand.Execute(null);
        expect.That(properties is null && sheet.Data["workspace_ca"]!["proprieta_sezione"] is null, "Senza vista nessuna finestra delle proprietà");
        sheet.PropertiesRequested += (_, value) => properties = value;
        sheet.Modified += (_, _) => modified++;
        sheet.Inputs.OpenRheologyCommand.Execute(null);
        expect.That(strength == Math.Abs(sheet.Input.D("fck_mpa")), "Reologia chiesta con fck");
        sheet.Inputs.OpenPropertiesCommand.Execute(null);
        expect.That(properties is not null, "Finestra delle proprietà chiesta alla vista");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!properties!.IsReady && !properties.Text.StartsWith("Proprietà non disponibili", StringComparison.Ordinal))
        { await Task.Yield(); await Task.Delay(20); if (watch.Elapsed > Expect.Deadline) throw new TimeoutException("Proprietà della sezione non calcolate."); }
        var options = sheet.Data["workspace_ca"]!["proprieta_sezione"]!;
        expect.That(properties.IsReady && properties.Text.Length > 0 && options["metodo"]!.ToString() == "Da φ" && options["n"] is not null && modified == 1
            && properties.CopyCommand.CanExecute(null), "Proprietà: testo, φ e n nel foglio, Modified");
        properties.Dispose();
    }

    // Reload: the data replaced from outside are opened again as a new sheet would open them.
    private static async Task ReloadAsync(string root, Expect expect)
    {
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"));
        await IdleAsync(sheet);
        var other = ConcreteSectionScenarios.FixtureData(root, "circolare-cava");
        long revision = sheet.Revision; var inputs = sheet.Inputs;
        sheet.Reload(other);
        using var fresh = OpenSheet(other);
        expect.That(sheet.Data.ToJsonString() == fresh.Data.ToJsonString() && !ReferenceEquals(inputs, sheet.Inputs) && sheet.Revision > revision && !sheet.HasResults
            && sheet.IsCalculationQueued && sheet.SelectedTab == fresh.SelectedTab, "Reload: dati aperti di nuovo, risultati azzerati, calcolo in coda");
        await IdleAsync(sheet);
        expect.That(sheet.HasResults, "Reload: risultati dei dati nuovi");
    }

    // ---------------------------------------------------------------- the files of the data of the F3 capture (section 4.0)

    /// <summary>
    /// The path of the F3 capture through the ViewModel, as F3EquivalenceCapture makes it (opened, visited, calculated-data, result; the report and
    /// the short report with their text and structure, status; exported-data and reopened after the report), against the F3 reference of the
    /// historic view: byte for byte in the canonical form of the capture, except the files with F4 transitions (the four fixtures before F5.10),
    /// compared as ui/f3-equivalence compares them, with exactly those transitions admitted. Since S9 the report is the ViewModel's (the figures
    /// planned as the desktop draws them: their captions enter the text, their images only the structure).
    /// </summary>
    private static async Task DataFilesAsync(string root, string output, Expect expect)
    {
        string expected = Path.Combine(root, "tests", "fixtures", "f3", "expected", "sheets", ConcreteSectionSheetModule.Id);
        string capture = Path.Combine(output, "sezione-ca-f3");
        var normalizer = new TextNormalizer([(root, "<RADICE>")]);
        var transitions = Transition.Load(Path.Combine(root, "tests", "fixtures", "f3", "transitions-f4.json"))
            .Where(t => t.File.StartsWith("sheets/" + ConcreteSectionSheetModule.Id + "/", StringComparison.Ordinal)).ToList();
        var withTransitions = transitions.Select(t => t.File).ToHashSet(StringComparer.Ordinal);
        string[] names = ["opened.json", "visited.json", "calculated-data.json", "result.json", "report.txt", "report-structure.json", "short.txt", "short-structure.json",
            "status.json", "exported-data.json", "reopened.json"];
        var differences = new List<string>();
        int files = 0, admitted = 0;
        foreach (string id in ConcreteSectionScenarios.Fixtures(root))
        {
            string folder = Path.Combine(capture, id); Directory.CreateDirectory(folder);
            var guids = new GuidMap(); var data = ConcreteSectionScenarios.FixtureData(root, id);
            CanonicalJson.Register(data, guids);
            void Write(string name, string text) => File.WriteAllText(Path.Combine(folder, name), text, new UTF8Encoding(false));
            void Save(string name, JsonNode? value)
            {
                CanonicalJson.Register(value, guids);
                Write(name + ".json", CanonicalJson.Serialize(value, guids, normalizer));
            }
            void Capture(byte[] docx, string name)
            {
                Write(name + ".txt", DocxText.Extract(docx, normalizer, guids));
                Write(name + "-structure.json", CanonicalJson.Serialize(ReportStructure(docx), guids, normalizer));
            }
            using (var sheet = OpenSheet(data))
            {
                sheet.Commit(); Save("opened", sheet.Data);
                Visit(sheet); sheet.Commit(); Save("visited", sheet.Data);
                await sheet.CalculateAsync(); sheet.Commit(); Save("calculated-data", sheet.Data);
                Save("result", sheet.Result);
                var status = new JsonObject { ["hasResults"] = sheet.HasResults };
                try
                {
                    Capture(sheet.Report.BuildReport("Fixture F3 · " + ConcreteSectionSheetModule.Id + " · " + id, ReportSections()), "report");
                    status["report"] = "available";
                    Capture(((IModuleShortReport)sheet.Report).BuildShortReport("Fixture F3 breve · " + id), "short");
                }
                catch (Exception error) when (error is not IOException && error is not UnauthorizedAccessException)
                { status["report"] = "unavailable"; status["reason"] = error.GetType().Name + ": " + error.Message; }
                Save("exported-data", sheet.Data); Save("status", status);
                using var reopened = OpenSheet(sheet.Data);
                reopened.Commit(); Save("reopened", reopened.Data);
            }
            foreach (string name in names)
            {
                string referencePath = Path.Combine(expected, id, name), producedPath = Path.Combine(folder, name);
                if (!File.Exists(referencePath)) { if (File.Exists(producedPath)) differences.Add(id + "/" + name + " (non nel riferimento)"); continue; }
                files++;
                string reference = File.ReadAllText(referencePath, Encoding.UTF8).Replace("\r\n", "\n");
                string produced = File.Exists(producedPath) ? File.ReadAllText(producedPath, Encoding.UTF8).Replace("\r\n", "\n") : "";
                string file = "sheets/" + ConcreteSectionSheetModule.Id + "/" + id + "/" + name;
                if (withTransitions.Contains(file))
                {
                    // Kept for the comparison with the F4 transitions, as ui/f3-equivalence compares them.
                    admitted++;
                    foreach (var (side, text) in new[] { ("f3-riferimento", reference), ("f3-transizioni", produced) })
                    {
                        string path = Path.Combine(capture, side, file);
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllText(path, text, new UTF8Encoding(false));
                    }
                }
                else if (reference != produced) differences.Add(id + "/" + name);
            }
        }
        expect.That(files == 121 && differences.Count == 0, "File F3 della sezione c.a. diversi dal riferimento (" + files.ToString(CultureInfo.InvariantCulture) + " file): "
            + string.Join(", ", differences));
        var comparer = new BaselineComparer(Tolerances.Load(Path.Combine(root, "tests", "ANTHEA.Testing", "tolerances.json")));
        comparer.Admit(transitions);
        var report = comparer.Compare(Path.Combine(capture, "f3-riferimento"), Path.Combine(capture, "f3-transizioni"));
        File.WriteAllText(Path.Combine(capture, "f3-confronto.json"), report.ToJsonString(J.Options));
        expect.That(admitted == withTransitions.Count && comparer.NotAdmitted == 0, "File F3 della sezione c.a. con transizioni F4: " + admitted.ToString(CultureInfo.InvariantCulture)
            + " file, " + transitions.Count.ToString(CultureInfo.InvariantCulture) + " transizioni, " + comparer.NotAdmitted.ToString(CultureInfo.InvariantCulture)
            + " differenze non ammesse o transizioni assenti");
    }

    /// <summary>The visit of the F3 capture on the ViewModel: every expander opened, every main tab selected and the first one restored.</summary>
    private static void Visit(ConcreteSheetViewModel sheet)
    {
        foreach (var group in sheet.Inputs.Groups) group.IsExpanded = true;
        sheet.Inputs.TendonGroup.IsExpanded = true;
        foreach (var form in sheet.Inputs.AllForms) foreach (var group in form.Groups) group.IsExpanded = true;
        int selected = sheet.SelectedTab;
        for (int i = 0; i < ConcreteSheetViewModel.TabTitles.Length; i++) sheet.SelectedTab = i;
        sheet.SelectedTab = selected;
    }

    // ---------------------------------------------------------------- the sections of the walls (section 5)

    /// <summary>
    /// Section 5 of f5-10-sezione.md: the sections that the walls send to the module c.a. («Apri sezione in c.a.» of the scenario
    /// «risultati-sezione» of F5.9), produced again by the ViewModel of the walls, equal to those of the frozen trace of the walls (names and
    /// hashes) and to the files of tests/fixtures/f5-sezione/muri, on which the scenarios «muri/…» compare the ViewModel of the section with the
    /// historic view. The files produced are written in the output, for a new capture.
    /// </summary>
    private static async Task WallSectionsAsync(string root, string output, Expect expect)
    {
        var sent = await RetainingWallSheetChecks.SentSectionsAsync(root);
        string line = File.ReadAllLines(Path.Combine(RetainingWallScenarios.Reference(root), "risultati-sezione.txt"), Encoding.UTF8).Last(l => l.Contains("sezioni: ", StringComparison.Ordinal));
        string frozen = line[(line.IndexOf("sezioni: ", StringComparison.Ordinal) + "sezioni: ".Length)..];
        string produced = string.Join(" | ", sent.Select(s => RetainingWallTrace.Quote(s.Name) + " " + s.Hash));
        expect.That(sent.Count == 2 && produced == frozen, "Sezioni inviate dai muri diverse dalla traccia congelata di F5.9: " + produced + " / " + frozen);
        string folder = Path.Combine(output, "sezione-ca-muri"); Directory.CreateDirectory(folder);
        var stored = ConcreteSectionScenarios.WallFixtures(root);
        foreach (var (name, data, _) in sent)
        {
            string id = ConcreteSectionScenarios.WallPrefix + WallFileName(name);
            var file = new JsonObject { ["module"] = ConcreteSectionSheetModule.Id, ["id"] = id, ["origin"] = "sezione inviata dal muro «" + name + "» (F5.9, scenario risultati-sezione)",
                ["data"] = data.DeepClone() };
            File.WriteAllText(Path.Combine(folder, WallFileName(name) + ".json"), file.ToJsonString(J.Options), new UTF8Encoding(false));
            expect.That(stored.Contains(id), "Sezione del muro non conservata in tests/fixtures/f5-sezione/muri: " + id);
            expect.That(ConcreteSectionTrace.StableHash(ConcreteSectionScenarios.FixtureData(root, id)) == ConcreteSectionTrace.StableHash(data),
                "Sezione del muro conservata diversa da quella inviata dal ViewModel dei muri: " + id);
        }
        expect.That(stored.Count == sent.Count, "Sezioni dei muri conservate: " + stored.Count);
    }

    /// <summary>The name of the file of a section of a wall: «Muro · Fusto · z/l 1,62 m» → «fusto-z-l-1-62-m».</summary>
    private static string WallFileName(string name)
    {
        var text = new StringBuilder();
        foreach (char c in name.Replace("Muro · ", "", StringComparison.Ordinal).ToLowerInvariant()) text.Append(char.IsLetterOrDigit(c) ? c : '-');
        return string.Join("-", text.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>A host that records every request: the sheet must work without any.</summary>
    private sealed class StrictHost : ISheetHost
    {
        internal int Calls { get; private set; }
        public void OpenModuleCopy(string module, JsonObject data, string name) => Calls++;
        public void ReplaceSoil(JsonObject data) => Calls++;
        public IReadOnlyList<SoilSource> SoilSources() { Calls++; return []; }
        public void RequestReport() => Calls++;
    }
}
