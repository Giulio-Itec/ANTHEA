using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static async Task MidasLiveImportChecks()
    {
        Reject(() => new MidasConnection("http://example.test/civil", "secret"), "live import rejects cleartext credentials");
        Reject(() => new MidasConnection("https://example.test/civil?key=secret", "secret"), "live import rejects keys in the service URL");
        Reject(() => new MidasConnection("https://name:secret@example.test/civil", "secret"), "live import rejects URL user information");
        var connection = new MidasConnection("https://example.test/civil", "test-private-key");
        Check(!connection.ToString().Contains("test-private-key"), "connection diagnostics redact the key");
        using var handler = new MidasProtocolFixture();
        using var http = new HttpClient(handler);
        using var source = new MidasModelSource(http);
        var catalog = await source.InspectAsync(connection, null, default);
        Check(catalog.Nodes == 4 && catalog.Elements == 2 && catalog.StaticCases.SequenceEqual(["Dead", "Live"]), "live catalog reads model counts and static case names without results");
        Check(handler.Requests.Count == 4 && handler.Requests.All(r => r.Method == "GET"), "checking a connection never requests or mutates analysis results");
        var request = new MidasImportRequest(connection, ModelUpdateScope.Everything, catalog.Signature, ["Dead"], true, true);
        var snapshot = await source.ReadAsync(request, null, default);
        Check(snapshot.Nodes[1].X == 2 && snapshot.Plates[0].Thickness == .2 && snapshot.Plates[0].Offset == -.1, "live acquisition preserves geometry units and plate offsets");
        Check(snapshot.Results.Single().Name == "Dead" && snapshot.Results[0].Values.Length == 3 && snapshot.Results[0].Displacements!.Length == 4, "live acquisition imports only selected cases with complete force and displacement coverage");
        Check(snapshot.Results[0].Displacements![1].X == .002 && snapshot.Results[0].Values[0].Values[0] == 2, "imported displacement and plate moment values use independent expected unit conversions");
        Check(snapshot.Results[0].Fields!.Single(f => f.Component == "My").Values[0].Value == 12, "beam moment conversion is N mm to kN m");
        Check(snapshot.SourceTables!.ContainsKey("CONS") && snapshot.SourceTables["GRUP"].EnumerateObject().Count() == 0, "live acquisition keeps attributes and explicitly empty source categories");
        Check(!JsonSerializer.Serialize(snapshot).Contains("test-private-key") && !JsonSerializer.Serialize(snapshot).Contains("example.test"), "snapshot and provenance never persist connection credentials or URL");
        Check(handler.Requests.Where(r => r.Method == "POST").All(r => r.Path == "/civil/post/TABLE" && !r.Body.Contains("EXPORT_PATH") && r.Body.Contains("Dead(ST)") && !r.Body.Contains("Live(ST)")), "only read-only result tables are posted with explicit selected static cases");
        Check(handler.Keys.All(k => k == "test-private-key"), "MAPI key is sent only in the expected request header");
        var normalized = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot));
        normalized.LocalFrames = [new(10, new(1,0,0), new(0,1,0), new(0,0,1))];
        normalized.NodeFrames = [new(1, new(1,0,0), new(0,1,0), new(0,0,1))];
        normalized.Boundaries = [new("CONS/1", "RESTRAIN", [1], null, "DX, DY, DZ")];
        var update = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot));
        Check(MidasNormalizationReuse.TryApply(normalized, update) && update.LocalFrames!.SequenceEqual(normalized.LocalFrames) && update.Boundaries!.Length == 1, "unchanged complete source preserves existing normalized axes and constraints");
        Check(!ReferenceEquals(update.Boundaries![0].Nodes, normalized.Boundaries![0].Nodes) && ModelSnapshotUpdate.Apply(normalized, update, ModelUpdateScope.Results).Results.Length == 1, "normalization reuse is independent and allows compatible results-only updates");
        var changedSource = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot));
        changedSource.SourceTables!["CONS"] = JsonSerializer.SerializeToElement(new { changed = true });
        Check(!MidasNormalizationReuse.TryApply(normalized, changedSource) && changedSource.LocalFrames == null, "a changed restraint prevents reuse of all stale normalization");
        var changedGeometry = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot));
        changedGeometry.Nodes[0] = changedGeometry.Nodes[0] with { X = 1 };
        Check(!MidasNormalizationReuse.TryApply(normalized, changedGeometry), "geometry mismatch prevents reuse even if source tables were retained");
        Check(!MidasNormalizationReuse.TryApply(null, snapshot) && !MidasNormalizationReuse.TryApply(normalized, normalized), "first imports and already normalized inputs are never rewritten");
        var incompleteBefore = ModelSnapshot.FromJson(JsonSerializer.Serialize(normalized));
        var incompleteAfter = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot));
        incompleteBefore.SourceTables!.Remove("FRLS"); incompleteAfter.SourceTables!.Remove("FRLS");
        Check(!MidasNormalizationReuse.TryApply(incompleteBefore, incompleteAfter), "missing normalization inputs block reuse even when both partial table sets match");
        var equivalent = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot));
        using (var equivalentMaterial = JsonDocument.Parse("{\"1\":{\"NAME\":\"Concrete\",\"test\":1.0}}")) equivalent.SourceTables!["MATL"] = equivalentMaterial.RootElement.Clone();
        using (var previousMaterial = JsonDocument.Parse("{\"1\":{\"test\":1,\"NAME\":\"Concrete\"}}")) normalized.SourceTables!["MATL"] = previousMaterial.RootElement.Clone();
        Check(MidasNormalizationReuse.TryApply(normalized, equivalent), "source equality ignores object order and equivalent decimal spellings");

        handler.Requests.Clear();
        var geometry = await source.ReadAsync(request with { Scope = ModelUpdateScope.Geometry, StaticCases = [], Forces = false, Displacements = false }, null, default);
        Check(geometry.Results.Length == 0 && handler.Requests.All(r => r.Method == "GET") && handler.Requests.All(r => !r.Path.EndsWith("/MATL")), "geometry acquisition avoids all result and material requests");
        await RejectLive(() => source.ReadAsync(request with { StaticCases = [] }, null, default), "results import requires a nonempty case selection");
        await RejectLive(() => source.ReadAsync(request with { ExpectedSignature = "stale" }, null, default), "changed source after catalog selection requires another inspection");

        foreach (string mode in new[] { "partial-plate", "partial-beam", "partial-displacements", "duplicate-plate", "wrong-case", "duplicate-displacements", "changed-attributes" })
        {
            using var badHandler = new MidasProtocolFixture { Mode = mode };
            using var badHttp = new HttpClient(badHandler);
            using var badSource = new MidasModelSource(badHttp);
            await RejectLive(() => badSource.ReadAsync(request, null, default), "live import rejects " + mode + " without publishing a snapshot");
        }
        foreach (var (mode, expected) in new[] { ("session", ModelSourceFailureKind.SessionMissing), ("document", ModelSourceFailureKind.DocumentClosed), ("denied", ModelSourceFailureKind.AccessDenied), ("redirect", ModelSourceFailureKind.AccessDenied), ("timeout", ModelSourceFailureKind.Timeout) })
        {
            using var badHandler = new MidasProtocolFixture { Mode = mode };
            using var badHttp = new HttpClient(badHandler);
            using var badSource = new MidasModelSource(badHttp, TimeSpan.FromMilliseconds(20));
            try { await badSource.InspectAsync(connection, null, default); throw new Exception("Accepted " + mode); }
            catch (ModelSourceException ex) { Check(ex.Kind == expected && !ex.ToString().Contains("test-private-key"), "live connection classifies " + mode + " with no raw response or key"); }
        }
        using (var badHandler = new MidasProtocolFixture { Mode = "timeout" })
        using (var badHttp = new HttpClient(badHandler))
        using (var badSource = new MidasModelSource(badHttp))
        using (var cancellation = new CancellationTokenSource(20))
        {
            try { await badSource.InspectAsync(connection, null, cancellation.Token); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(true, "user cancellation remains cancellation rather than a source timeout"); }
        }
        foreach (string mode in new[] { "invalid-json", "too-large", "unknown-table" })
        {
            using var badHandler = new MidasProtocolFixture { Mode = mode };
            using var badHttp = new HttpClient(badHandler);
            using var badSource = new MidasModelSource(badHttp);
            await RejectLive(() => badSource.InspectAsync(connection, null, default), "live connection rejects " + mode);
        }

        var services = new LiveFixtureServices(source);
        int committed = 0;
        using var vm = new ModelViewerViewModel(null, [], false, services, _ => committed++, (_, _, _, _) => { });
        vm.SelectedImportSource = 1; vm.MidasBaseUrl = "https://example.test/civil"; vm.MidasApiKey = "test-private-key";
        Check(!vm.ImportCommand.CanExecute(null) && vm.InspectMidasCommand.CanExecute(null), "MVVM requires inspection before live import");
        await vm.InspectMidasCommand.ExecuteAsync(null);
        Check(committed == 0 && vm.ImportCases.Count == 2 && vm.ImportCommand.CanExecute(null), "inspection fills the selectable case list without changing the document");
        vm.ClearImportCasesCommand.Execute(null);
        Check(!vm.ImportCommand.CanExecute(null), "empty result selection disables live import");
        vm.SelectedUpdateScope = 0;
        Check(vm.ImportCommand.CanExecute(null), "geometry-only import does not require result cases");
        vm.SelectedUpdateScope = 3; vm.ImportCases[0].IsSelected = true;
        await vm.ImportCommand.ExecuteAsync(null);
        Check(committed == 1 && vm.Snapshot!.Results.Single().Name == "Dead" && !vm.IsBusy, "MVVM commits one complete selected-case import");
        var previous = vm.Snapshot;
        services.Fail = true;
        await vm.ImportCommand.ExecuteAsync(null);
        Check(committed == 1 && ReferenceEquals(previous, vm.Snapshot) && vm.HasImportFailure && vm.ImportFailure!.Recovery.Contains("Riattiva"), "source refusal keeps the prior model and displays recovery instructions");
        vm.MidasApiKey = "new-private-key";
        Check(vm.ImportCases.Count == 0 && !vm.ImportCommand.CanExecute(null), "changing credentials invalidates the catalog and case selection");
        using var readOnly = new ModelViewerViewModel(snapshot, [], true, services, _ => committed++, (_, _, _, _) => { });
        readOnly.SelectedImportSource = 1; readOnly.MidasApiKey = "test-private-key";
        Check(!readOnly.InspectMidasCommand.CanExecute(null) && !readOnly.ImportCommand.CanExecute(null) && !readOnly.CanEditImport, "read-only revisions cannot connect or import");
        vm.Dispose(); Check(vm.MidasApiKey.Length == 0, "closing the viewer clears its session credential");
    }

    static async Task RejectLive(Func<Task> operation, string message)
    {
        try { await operation(); }
        catch (InvalidDataException) { Check(true, message); return; }
        throw new Exception("Accepted: " + message);
    }

    sealed class LiveFixtureServices(IMidasModelSource source) : IModelViewerServices
    {
        public bool Fail;
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token) => Task.FromResult<ModelSnapshot?>(null);
        public Task<MidasModelCatalog> InspectMidasAsync(MidasConnection connection, IProgress<ModelImportProgress> progress, CancellationToken token) => source.InspectAsync(connection, progress, token);
        public Task<ModelSnapshot> ImportMidasAsync(MidasImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token) => Fail ? Task.FromException<ModelSnapshot>(new ModelSourceException(ModelSourceFailureKind.SessionMissing)) : source.ReadAsync(request, progress, token);
        public void FitView() { }
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
    }

    sealed class MidasProtocolFixture : HttpMessageHandler
    {
        public string Mode = "";
        public List<(string Method, string Path, string Body)> Requests { get; } = [];
        public List<string> Keys { get; } = [];
        int materialReads;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(token);
            Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body));
            Keys.Add(request.Headers.GetValues("MAPI-Key").Single());
            if (Mode == "timeout") await Task.Delay(Timeout.Infinite, token);
            if (Mode == "session") return Reply("{\"error\":{\"message\":\"client does not exist test-private-key\"}}");
            if (Mode == "document") return Reply("{\"error\":{\"message\":\"no document open test-private-key\"}}");
            if (Mode == "denied") return Reply("test-private-key", HttpStatusCode.Unauthorized);
            if (Mode == "redirect") return Reply("", HttpStatusCode.Redirect);
            if (Mode == "invalid-json") return Reply("test-private-key");
            if (Mode == "too-large") { var reply = Reply("{}"); reply.Content.Headers.ContentLength = 32_000_001; return reply; }
            if (Mode == "unknown-table") return Reply("{\"message\":\"not supported\"}");
            if (request.Method == HttpMethod.Post) return Result(body);
            string name = request.RequestUri.Segments.Last();
            string data = name switch
            {
                "UNIT" => "{\"1\":{\"DIST\":\"MM\",\"FORCE\":\"N\"}}",
                "NODE" => "{\"1\":{\"X\":0,\"Y\":0,\"Z\":0},\"2\":{\"X\":2000,\"Y\":0,\"Z\":0},\"3\":{\"X\":0,\"Y\":3000,\"Z\":0},\"4\":{\"X\":2000,\"Y\":0,\"Z\":4000}}",
                "ELEM" => "{\"10\":{\"TYPE\":\"PLATE\",\"SECT\":1,\"NODE\":[1,2,3,0]},\"20\":{\"TYPE\":\"BEAM\",\"SECT\":2,\"NODE\":[2,4]}}",
                "THIK" => "{\"1\":{\"TYPE\":\"VALUE\",\"NAME\":\"Wall\",\"T_IN\":200,\"OFFSET\":2,\"O_VALUE\":-100}}",
                "SECT" => "{\"2\":{\"SECT_NAME\":\"Beam\",\"SECT_BEFORE\":{\"SHAPE\":\"SB\",\"OFFSET_PT\":\"CC\",\"SECT_I\":{\"vSIZE\":[500,300]}}}}",
                "STLD" => "{\"1\":{\"NAME\":\"Dead\"},\"2\":{\"NAME\":\"Live\"}}",
                "MATL" => Mode == "changed-attributes" && ++materialReads > 1 ? "{\"1\":{\"NAME\":\"Changed\"}}" : "{\"1\":{\"NAME\":\"Concrete\"}}",
                "CONS" => "{\"1\":{\"ITEMS\":[{\"CONSTRAINT\":\"111111\"}]}}",
                _ => ""
            };
            return Reply(data.Length == 0 ? "{\"message\":\"\"}" : "{\"" + name + "\":" + data + "}");
        }
        HttpResponseMessage Result(string body)
        {
            using var doc = JsonDocument.Parse(body);
            var arg = doc.RootElement.GetProperty("Argument"); string type = arg.GetProperty("TABLE_TYPE").GetString()!;
            string name = arg.GetProperty("LOAD_CASE_NAMES")[0].GetString()![..^4];
            if (Mode == "wrong-case") name = "Other";
            string[] head; List<string[]> rows;
            if (type == "PLATEFORCEUL")
            {
                head = ["Elem", "Load", "Node", "Mxx", "Myy", "Mxy", "Fxx", "Fyy", "Fxy", "Vxx", "Vyy"];
                rows = Enumerable.Range(1, Mode == "partial-plate" ? 2 : 3).Select(n => new[] { "10", name, n.ToString(), "2000", "3000", "4000", "5000", "6000", "7000", "8000", "9000" }).ToList();
                if (Mode == "duplicate-plate") rows.Add(rows[0]);
            }
            else if (type == "BEAMFORCE")
            {
                head = ["Elem", "Load", "Part", "Axial", "Shear-y", "Shear-z", "Torsion", "Moment-y", "Moment-z"];
                rows = [["20", name, "I[2]", "1000", "2000", "3000", "4000000", "12000000", "6000000"]];
                if (Mode != "partial-beam") rows.Add(["20", name, "J[4]", "2000", "3000", "4000", "5000000", "13000000", "7000000"]);
            }
            else
            {
                head = ["Node", "Load", "DX", "DY", "DZ"];
                rows = Enumerable.Range(1, Mode == "partial-displacements" ? 3 : 4).Select(n => new[] { n.ToString(), name, n.ToString(), "0", "0" }).ToList();
                if (Mode == "duplicate-displacements") rows.Add(rows[0]);
            }
            return Reply(JsonSerializer.Serialize(new { ANTHEAResults = new { FORCE = "N", DIST = "MM", HEAD = head, DATA = rows } }));
        }
        static HttpResponseMessage Reply(string json, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
