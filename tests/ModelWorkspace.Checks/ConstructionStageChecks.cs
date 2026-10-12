using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static async Task ConstructionStageChecks(string output)
    {
        var connection = new MidasConnection("https://example.test/civil", "stage-test-key");
        using var handler = new StageProtocolFixture(); using var http = new HttpClient(handler); using var source = new MidasModelSource(http);
        var catalog = await source.InspectAsync(connection, null, default);
        Check(catalog.Phases.Select(p => p.Name).SequenceEqual(["Getto", "Completamento"]) && catalog.Phases[0].DurationDays == 0 &&
            catalog.StageCases.SequenceEqual(["Summation"]), "stage catalog uses GPC order, duration and scoped cases without requesting results");
        var selection = new MidasStageReadRequest([new("Getto", "001(last)"), new("Completamento", "001(first)"), new("Completamento", "002(last)")], ["Summation"], "Accumulative");
        var request = new MidasImportRequest(connection, ModelUpdateScope.Everything, catalog.Signature, [], true, true) { Stages = selection };
        var snapshot = await source.ReadAsync(request, null, default);
        Check(snapshot.Results.Length == 3 && snapshot.Results.All(r => r.Stage != null) && snapshot.Results.Select(r => r.Name).Distinct().Count() == 3,
            "same source case remains three separate phase/step states");
        var first = snapshot.Results.Single(r => r.Stage!.Phase == "Getto");
        Check(first.Values.Length == 4 && first.Values.All(v => v.Values.SequenceEqual(new double[] { 2, -3, 4, 5, -6, 7, 8, -9 })),
            "stage plate forces retain all eight independent expected values, signs and display units");
        Check(first.Fields!.Select(f => f.Values[0].Value).SequenceEqual(new double[] { 1, -2, 3, 4, -12, 6 }),
            "stage beam adapter reuses combination sign conventions and N mm to kN m conversion");
        Check(first.Displacements!.Length == 2 && first.Displacements[0].X == .001 && first.Displacements[0].Rotation!.Z == .03,
            "stage displacements preserve metres and radians; inactive or absent nodes are not invented");
        Check(snapshot.Results.Single(r => r.Stage!.Step == "002(last)").Values[0].Values[0] == 6, "step filters cannot overwrite the first state with the last step");
        Check(handler.Bodies.All(b => b.Contains("Summation(CS)") && b.Contains("STAGE_STEP") && b.Contains("OPT_CS")) &&
            handler.Bodies.Where(b => b.Contains("DISPLACEMENTG")).All(b => b.Contains("DISP_OPT") && b.Contains("Accumulative")),
            "every stage table request carries explicit phase, step, source case and displacement reference");
        string json = JsonSerializer.Serialize(snapshot); var roundtrip = ModelSnapshot.FromJson(json);
        Check(snapshot.Version == 2 && roundtrip.Fingerprint() == snapshot.Fingerprint() && roundtrip.Results[0].Stage == snapshot.Results[0].Stage,
            "stage context and result values survive save/reopen without fingerprint changes");
        Check(!json.Contains("stage-test-key") && !json.Contains("example.test"), "stage provenance never saves live credentials");
        Check(ModelCombinations.StaticCaseNames(snapshot).SequenceEqual(["G", "Q"]), "stage output states never enter the static combination editor");
        Reject(() => ModelCombinations.Save(snapshot, new("ANTHEA/stage", "Not static", ModelCombinationKind.Linear, "ANTHEA", "", [new(first.Name, false, 1)], true)),
            "a construction state cannot be mislabeled as a linear static base case");
        var oldFormat = ModelSnapshot.FromJson(json); oldFormat.Version = 1;
        Reject(oldFormat.Validate, "stage states cannot be stored in the static-only legacy format");
        var futureFormat = ModelSnapshot.FromJson(json); futureFormat.Version = 3;
        Reject(futureFormat.Validate, "future snapshot formats are rejected explicitly");
        var corrupted = ModelSnapshot.FromJson(json); corrupted.Results = [first with { Stage = first.Stage! with { Version = 2 } }];
        Reject(corrupted.Validate, "future stage result metadata is rejected");
        corrupted = ModelSnapshot.FromJson(json); corrupted.Results = [first with { Stage = null, Name = "ordinary" }];
        // Legacy static packages remain valid and omit every new optional identity field.
        var legacy = CombinationModel(); string legacyJson = JsonSerializer.Serialize(legacy);
        Check(!legacyJson.Contains("\"Stage\"") && ModelSnapshot.FromJson(legacyJson).Fingerprint() == legacy.Fingerprint(), "legacy static snapshots omit stage metadata entirely");
        var update = ModelSnapshotUpdate.Apply(snapshot, roundtrip, ModelUpdateScope.Results);
        Check(update.Results.Length == 3 && update.Results.All(r => r.Stage != null), "results-only refresh keeps explicit phase/step context");
        roundtrip.SourceTables!["STAG"] = JsonSerializer.SerializeToElement(new { changed = true });
        Reject(() => ModelSnapshotUpdate.Apply(snapshot, roundtrip, ModelUpdateScope.Results), "a changed construction schedule blocks results-only updates");
        foreach (string reference in new[] { "Current", "Real" })
        {
            var result = await source.ReadAsync(request with { Stages = selection with { DisplacementReference = reference } }, null, default);
            Check(result.Results.All(r => r.Stage!.DisplacementReference == reference) && result.Results[0].Displacements!.Length == 2,
                "source displacement reference stays explicit: " + reference);
        }
        var forces = await source.ReadAsync(request with { Displacements = false }, null, default);
        Check(forces.Results.All(r => r.Stage!.DisplacementReference == null && r.Displacements == null), "force-only stage import does not pretend to have a displacement reference");
        foreach (string mode in new[] { "wrong-phase", "wrong-step", "wrong-case", "missing-state", "duplicate", "missing-stage-column", "changed-stage" })
        {
            using var badHandler = new StageProtocolFixture { Mode = mode }; using var badHttp = new HttpClient(badHandler); using var badSource = new MidasModelSource(badHttp);
            await RejectLive(() => badSource.ReadAsync(request, null, default), "stage acquisition rejects " + mode + " atomically");
        }
        using var vm = new ModelViewerViewModel(snapshot, [], false, new LiveFixtureServices(source), _ => { }, (_, _, _, _) => { });
        vm.SelectedResultScope = 2; vm.SelectedStagePhase = "Completamento"; vm.SelectedStageStep = "002(last)";
        Check(vm.FilteredCases.Count == 1 && vm.SelectedCase == snapshot.Results.Single(r => r.Stage!.Step == "002(last)").Name && vm.IsStageResult,
            "MVVM phase/step filters select exactly one field without altering the result archive");
        Check(vm.StageResultDescription.Contains("Accumulative") && vm.StageResultDescription.Contains("non è simulata"), "viewer explains displacement reference and full geometry context");
        vm.SelectedResultScope = 1; Check(vm.FilteredCases.Count == 0 && vm.SelectedCase == null, "static scope hides stage results and does not fall back to another phase");
        vm.SelectedCase = first.Name; Check(vm.FilteredCases.Contains(first.Name), "restoring a saved stage view synchronizes browsing filters");
        vm.SelectedImportSource = 1; vm.MidasBaseUrl = "https://example.test/civil"; vm.MidasApiKey = "stage-test-key";
        await vm.InspectMidasCommand.ExecuteAsync(null); vm.ClearImportCasesCommand.Execute(null);
        vm.ImportConstructionStages = true;
        Check(vm.HasImportStages && !vm.ImportCommand.CanExecute(null), "stage import requires an explicit phase and step");
        vm.ImportStages[0].IsSelected = true; vm.ImportStages[0].StepsText = "001(last)";
        Check(vm.ImportCommand.CanExecute(null), "explicit stage-only selection enables acquisition without static cases");
        vm.ImportStages[0].StepsText = "001(last);001(last)";
        Check(!vm.ImportCommand.CanExecute(null), "duplicate stage step labels disable acquisition");
        vm.MidasApiKey = "changed-key";
        Check(!vm.HasImportStages && !vm.ImportConstructionStages, "changing the connection invalidates stage choices as well as static cases");
        await File.WriteAllTextAsync(Path.Combine(output, "stages-synthetic.antheamodel"), json);
    }

    sealed class StageProtocolFixture : HttpMessageHandler
    {
        public string Mode = "";
        public List<string> Bodies { get; } = [];
        int stageReads;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string json;
            if (request.Method == HttpMethod.Get)
            {
                string name = request.RequestUri!.Segments.Last(); var tables = CombinationModel().SourceTables!;
                tables["STAG"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> {
                    ["10"] = new { NAME = "Completamento", DURATION = 10, bSV_RSLT = true },
                    ["2"] = new { NAME = Mode == "changed-stage" && ++stageReads > 1 ? "Alterato" : "Getto", DURATION = 0, bSV_RSLT = true } });
                json = tables.TryGetValue(name, out var table) ? JsonSerializer.Serialize(new Dictionary<string, JsonElement> { [name] = table }) : "{\"message\":\"\"}";
            }
            else
            {
                string body = await request.Content!.ReadAsStringAsync(token); Bodies.Add(body);
                using var document = JsonDocument.Parse(body); var arg = document.RootElement.GetProperty("Argument");
                var stageStep = arg.GetProperty("STAGE_STEP")[0].GetString()!.Split(':'); string phase = stageStep[0], step = stageStep[1];
                int factor = phase == "Getto" ? 1 : step == "001(first)" ? 2 : 3;
                if (Mode == "wrong-phase") phase = "Other"; if (Mode == "wrong-step") step = "003";
                string sourceCase = Mode == "wrong-case" ? "Unknown" : "Summation";
                string type = arg.GetProperty("TABLE_TYPE").GetString()!;
                string[] head; var rows = new List<string[]>();
                string[] Row(string id, string? location, params double[] values) => new[] { (rows.Count + 1).ToString(), id, sourceCase, phase, step }
                    .Concat(location == null ? [] : new[] { location }).Concat(values.Select(v => (v * factor).ToString("R", System.Globalization.CultureInfo.InvariantCulture))).ToArray();
                if (type == "PLATEFORCEUL")
                {
                    head = ["Index", "Elem", "Load", "Stage", "Step", "Node", "Mxx", "Myy", "Mxy", "Fxx", "Fyy", "Fxy", "Vxx", "Vyy"];
                    for (int n = 1; n <= 4; n++) rows.Add(Row("10", n.ToString(), 2000, -3000, 4000, 5, -6, 7, 8, -9));
                }
                else if (type == "BEAMFORCE")
                {
                    head = ["Index", "Elem", "Load", "Stage", "Step", "Part", "Axial", "Shear-y", "Shear-z", "Torsion", "Moment-y", "Moment-z"];
                    rows.Add(Row("20", "I[1]", 1000, -2000, 3000, 4000000, -12000000, 6000000)); rows.Add(Row("20", "J[2]", 1000, -2000, 3000, 4000000, -12000000, 6000000));
                }
                else
                {
                    head = ["Index", "Node", "Load", "Stage", "Step", "DX", "DY", "DZ", "RX", "RY", "RZ"];
                    for (int n = 1; n <= (factor == 1 ? 2 : 4); n++) rows.Add(Row(n.ToString(), null, n, -n, 0, .01, .02, .03));
                }
                if (Mode == "duplicate") rows.Add(rows[0]);
                if (Mode == "missing-state" && factor == 3) rows.Clear();
                if (Mode == "missing-stage-column") { head = head.Where(h => h != "Stage").ToArray(); rows = rows.Select(r => r.Where((_, i) => i != 3).ToArray()).ToList(); }
                json = JsonSerializer.Serialize(new { ANTHEAStage = new { FORCE = "N", DIST = "MM", HEAD = head, DATA = rows } });
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
