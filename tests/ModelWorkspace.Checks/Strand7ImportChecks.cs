using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static bool Near(double a, double b, double tolerance = 1e-9) => Math.Abs(a - b) <= tolerance * Math.Max(1, Math.Abs(b));

    /// <summary>1.15.0: Straus7 in the viewer without the native API: bridge from a synthetic GPC model, GPC combinations, worker protocol
    /// with a substitute worker (this executable), source and view model.</summary>
    static async Task Strand7ImportChecks(string output)
    {
        string directory = Path.Combine(output, "straus7"); Directory.CreateDirectory(directory);
        // ---- Bridge GPC model + results → snapshot ----
        var snapshot = Strand7Sample.Snapshot(); snapshot.Validate();
        File.WriteAllText(Path.Combine(directory, "sintetico-straus7.antheamodel"), JsonSerializer.Serialize(snapshot));
        Check(snapshot.Nodes.Length == 12 && snapshot.Nodes.Single(n => n.Id == 6) is { X: 2, Y: 0, Z: 3 }, "Straus7 bridge: API node numbers, millimetres to metres");
        Check(snapshot.Elements.Select(e => (e.Id, e.Type, e.Property)).SequenceEqual([(1000001, "BEAM", 7), (1000002, "BEAM", 7), (1000003, "TRUSS", 8), (1, "PLATE", 3), (2, "PLATE", 4), (3, "PLATE", 1000001)]),
            "Straus7 bridge: plates keep their numbers, members 1 000 000 + number, Straus7 property numbers");
        Check(snapshot.Sections.Single(s => s.Id == 7) is { Name: "Pila 400x300", Shape: "SB", Centered: true } pila && Near(pila.Width, .3) && Near(pila.Height, .4) &&
              snapshot.Sections.Single(s => s.Id == 8) is { Name: "Tirante CHS", Shape: "CHS", Width: 0 }, "Straus7 bridge: solid rectangle for the view, other sections by name and GPC shape");
        Check(snapshot.Plates.Single(p => p.Id == 3) is { Name: "Soletta 250", Offset: 0 } && Near(snapshot.Plates.Single(p => p.Id == 3).Thickness, .25) &&
              snapshot.Plates.Single(p => p.Id == 4) is { Name: "Proprietà 4" } && Near(snapshot.Plates.Single(p => p.Id == 4).Thickness, .3) &&
              snapshot.Plates.Single(p => p.Id == 1000001).Name == "Soletta 250 · offset 0.05 m" && Near(snapshot.Plates.Single(p => p.Id == 1000001).Offset, .05),
            "Straus7 bridge: thickness from GPC or from the Straus7 property record, element offsets as their own property");
        var frames = snapshot.LocalFrames!.ToDictionary(f => f.Element);
        Check(frames.Count == 6 && frames[1000001].X == new ModelDirection(0, 0, 1) && frames[1000002].Y == new ModelDirection(0, 1, 0) && frames[1000003].X == new ModelDirection(0, 0, -1) &&
              frames[1].Z == new ModelDirection(0, 0, 1), "Straus7 bridge: GPC member frames (x = V3) and plate frames");
        var b = snapshot.Boundaries!;
        Check(b.Any(x => x is { Kind: "RESTRAIN", Dofs: "DX, DY, DZ, RX, RY, RZ" } && x.Nodes[0] == 1) && b.Any(x => x is { Kind: "RESTRAIN", Dofs: "DX, DY, DZ" } && x.Nodes[0] == 4) &&
              b.Any(x => x is { Kind: "RESTRAIN", Dofs: "Molla elastica · DZ" } && x.Nodes[0] == 2) && b.Any(x => x.Kind == "CONSTRAINT" && x.Nodes.SequenceEqual([5, 11])) &&
              b.Any(x => x is { Kind: "LINK", Dofs: "Matrice elastica" } && x.Nodes.SequenceEqual([6, 12])) && b.Single(x => x.Kind == "RELEASE") is { Element: 1000002 } release && release.Nodes[0] == 6 && release.Dofs.Contains("RY: Released"),
            "Straus7 bridge: supports, node spring, rigid and elastic links, end release with the existing symbols");
        Check(snapshot.Results.Select(r => r.Name).SequenceEqual(["1: Dead", "2: Live"]) && snapshot.Results.All(r => r.Values.Length == 12 && r.Displacements!.Length == 12),
            "Straus7 bridge: one result set per Straus7 case, the four corners of every plate and every node");
        var dead = snapshot.Results[0]; var corner = dead.Values.Single(v => v.Element == 1 && v.Node == 1);
        Check(corner.Values.SequenceEqual([-1.0, .5, -.05, 10, 20, 1, 2, 3]), "Straus7 bridge: plate moments ×1e-3 with the MIDAS display sign (minus GPC), forces ×1 in kN/m");
        var my = ResultFields.Read(dead, "BEAM", "LOCAL", "My").Single(v => v.Element == 1000002 && v.Station == .5);
        var fx = ResultFields.Read(dead, "BEAM", "LOCAL", "Fx").Single(v => v.Element == 1000002 && v.Station == 0);
        Check(Near(my.Value, -1) && my.Node == 0 && Near(fx.Value, -5) && fx.Node == 5 && ResultFields.Read(dead, "TRUSS", "LOCAL", "Fx").Count == 2 &&
              Near(ResultFields.Read(dead, "BEAM", "LOCAL", "Mx").First().Value, .3), "Straus7 bridge: member stations in kN and kN·m, My minus GPC M1, truss axial force");
        var displacement = dead.Displacements!.Single(d => d.Node == 3);
        Check(Near(displacement.X, .001) && Near(displacement.Z, -.003) && Near(displacement.Rotation!.X, 1e-4) && Near(displacement.Rotation.Z, -2e-4), "Straus7 bridge: global displacements in metres and rotations in radians");
        var definitions = ModelCombinations.Read(snapshot);
        Check(snapshot.SourceCombinations!.StaticCases.SequenceEqual(["1: Dead", "2: Live"]) && definitions.Single() is { Id: "STRAUS7/LSA/1", Name: "ULS", Source: "STRAUS7", Editable: false, Kind: ModelCombinationKind.Linear } lsa &&
              lsa.Terms.Select(t => (t.Reference, t.Factor, t.Analysis)).SequenceEqual([("1: Dead", 1.35, "ST"), ("2: Live", 1.5, "ST")]) && ModelCombinations.StaticCaseNames(snapshot).SequenceEqual(["1: Dead", "2: Live"]),
            "Straus7 bridge: LSA combination as a read-only source definition of the static cases");
        Check(snapshot.ImportMessages!.Select(m => m.Code).ToHashSet().IsSupersetOf(["STRAUS7-NUMBERING", "STRAUS7-CONVENTIONS", "STRAUS7-RESULTS"]), "Straus7 bridge: numbering, units, signs and result scope declared in the import report");
        var reopened = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot));
        Check(reopened.Fingerprint() == snapshot.Fingerprint() && reopened.SourceCombinations!.Definitions.Length == 1, "Straus7 copy survives the package round trip with its source combinations");
        var legacy = Sample.Model(); string before = legacy.Fingerprint();
        Check(!JsonSerializer.Serialize(legacy).Contains("SourceCombinations") && Sample.Model().Fingerprint() == before, "the new optional field is omitted when null: existing fingerprints do not change");
        Check(Strand7Snapshot.CaseName("Case 12: Vento X") == "12: Vento X" && Strand7Snapshot.CaseName("Peso") == "Peso", "Straus7 case names as the result cases of Straus7");
        var geometry = Strand7Sample.Snapshot(false);
        Check(geometry.Results.Length == 0 && geometry.SourceCombinations != null && geometry.Elements.Length == 6, "Straus7 bridge without a dataset: geometry, attributes and definitions only");
        var tampered = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot)); tampered.SourceCombinations = tampered.SourceCombinations! with { Definitions = [definitions[0] with { Editable = true }] };
        Reject(tampered.Validate, "editable or foreign source combinations are rejected");

        // ---- GPC combination engine on the copy ----
        var availability = ModelCombinations.Inspect(snapshot).Single();
        Check(availability.CanCalculate && availability.State == "Da calcolare", "Straus7 LSA combination can be calculated from the imported static cases");
        var service = new GpcModelCombinationService();
        var calculated = await service.CalculateAsync(snapshot, "STRAUS7/LSA/1");
        var uls = calculated.Results.Single(); var combined = uls.Values.Single(v => v.Element == 1 && v.Node == 1).Values;
        Check(uls.Name == "ULS [STRAUS7]" && Near(combined[0], 1.35 * -1 + 1.5 * -2) && Near(combined[3], 1.35 * 10 + 1.5 * 20) && Near(combined[6], 1.35 * 2 + 1.5 * 4),
            "GPC rebuilds the LSA combination at the plate corners: 1.35 Dead + 1.5 Live");
        Check(Near(ResultFields.Read(uls, "BEAM", "LOCAL", "My").Single(v => v.Element == 1000002 && v.Station == .5).Value, 1.35 * -1 + 1.5 * -2) &&
              Near(uls.Displacements!.Single(d => d.Node == 3).X, 1.35 * .001 + 1.5 * .002), "GPC rebuilds member stations and displacements of the Straus7 combination");
        var attached = ModelCombinations.Attach(snapshot, "STRAUS7/LSA/1", calculated);
        Check(attached.Results.Length == 3 && ModelCombinations.Inspect(attached).Single().State == "Calcolata", "the rebuilt Straus7 combination is attached to the copy");
        var envelope = ModelCombinations.Save(snapshot, new("ANTHEA/inviluppo", "Inviluppo", ModelCombinationKind.Envelope, "ANTHEA", "", [new("1: Dead", false, 1), new("2: Live", false, 1)], true));
        var enveloped = await service.CalculateAsync(envelope, "ANTHEA/inviluppo");
        Check(enveloped.Results.Length == 2 && Near(enveloped.Results.Single(r => r.Name.EndsWith("(min)")).Values.Single(v => v.Element == 1 && v.Node == 1).Values[0], -2) &&
              Near(enveloped.Results.Single(r => r.Name.EndsWith("(max)")).Values.Single(v => v.Element == 1 && v.Node == 1).Values[0], -1), "ANTHEA envelopes of Straus7 cases are rebuilt by GPC");

        // ---- Partial updates keep the source definitions coherent ----
        var onlyGeometry = ModelSnapshotUpdate.Apply(null, snapshot, ModelUpdateScope.Geometry);
        Check(onlyGeometry.SourceCombinations == null && onlyGeometry.Results.Length == 0, "geometry-only update drops the source definitions with the attributes");
        var resultsOnly = ModelSnapshotUpdate.Apply(Strand7Sample.Snapshot(false), snapshot, ModelUpdateScope.Results);
        Check(resultsOnly.Results.Length == 2 && resultsOnly.SourceCombinations!.Definitions.Length == 1, "results-only update of a compatible Straus7 copy");
        var renamed = ModelSnapshot.FromJson(JsonSerializer.Serialize(snapshot)); renamed.SourceCombinations = renamed.SourceCombinations! with { StaticCases = ["1: Dead", "2: Live", "3: Vento"] };
        Reject(() => ModelSnapshotUpdate.Apply(renamed, snapshot, ModelUpdateScope.Results), "results-only update refuses different source cases");

        // ---- Protocol ----
        Check(Strand7WorkerProtocol.TryProgress("PROGRESSO 2 6 Lettura dei risultati", out var step) && step is { Completed: 2, Total: 6, Message: "Lettura dei risultati" } &&
              !Strand7WorkerProtocol.TryProgress("PROGRESSO 7 6 x", out _) && Strand7WorkerProtocol.ErrorText("ERRORE Licenza assente") == "Licenza assente" &&
              Strand7WorkerProtocol.Progress(1, 3, "a\nb") == "PROGRESSO 1 3 a b", "worker protocol lines: progress, errors, one line per message");
        string atomic = Path.Combine(directory, "risultato.json"); File.Delete(atomic);
        Strand7WorkerProtocol.WriteResult(atomic, new { A = 1 });
        Check(File.Exists(atomic) && !File.Exists(atomic + Strand7WorkerProtocol.PartialSuffix), "worker result is written completely before its final name appears");
        try { Strand7WorkerProtocol.WriteResult(atomic, new { A = 2 }); throw new Exception("Result overwritten"); } catch (IOException) { Check(true, "an existing worker result is never replaced"); }
        string badRequest = Path.Combine(directory, "richiesta-errata.json");
        File.WriteAllText(badRequest, Strand7WorkerProtocol.Serialize(new(Strand7WorkerProtocol.Version, Strand7WorkerOperation.Read, "a.st7", null, null, ModelUpdateScope.Everything, [1, 1], true, true, null, "x.json")));
        Reject(() => Strand7WorkerProtocol.ReadRequest(badRequest), "worker refuses a request with repeated case numbers");
        File.WriteAllText(badRequest, "{\"Protocol\":99}");
        Reject(() => Strand7WorkerProtocol.ReadRequest(badRequest), "worker refuses another protocol version");

        // ---- Source with a substitute worker (this executable) ----
        string model = Path.Combine(directory, "fixture.st7"), results = Path.Combine(directory, "fixture.lsa"), api = Path.Combine(directory, "St7api.dll");
        File.WriteAllText(model, "substitute model"); File.WriteAllText(results, "substitute results");
        // Any native x64 DLL passes the file check, which reads the PE header only; the substitute worker never loads it.
        File.Copy(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll"), api, true);
        string echo = Path.Combine(directory, "richiesta-ricevuta.json");
        Strand7ModelSource Source(string mode) => SubstituteWorker(mode, echo);
        var request = new Strand7ImportRequest(model, results, api, ModelUpdateScope.Everything, [], true, true);
        var steps = new List<ModelImportProgress>(); var progress = new SynchronousProgress(steps.Add);
        var catalog = await Source("catalog").InspectAsync(request, progress, CancellationToken.None);
        Check(catalog is { Nodes: 12, Beams: 3, Plates: 3, Combinations: 1, SourceSignature: "SYNTHETICGEOMETRY/SYNTHETICRESULTS" } && catalog.Cases.Select(c => (c.Number, c.Name)).SequenceEqual([(1, "1: Dead"), (2, "2: Live")]) &&
              steps.Count == 2 && steps[1].Total == 2, "Straus7 source: catalog and progress from the worker process");
        Check(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(echo)).GetProperty("Operation").GetString() == "Inspect", "Straus7 source: the worker receives an Inspect request");
        var read = await Source("snapshot").ReadAsync(request with { CaseNumbers = [1, 2], ExpectedSignature = catalog.SourceSignature }, null, CancellationToken.None);
        var received = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(echo));
        Check(read.Fingerprint() == snapshot.Fingerprint() && received.GetProperty("CaseNumbers").EnumerateArray().Select(n => n.GetInt32()).SequenceEqual([1, 2]) &&
              received.GetProperty("ExpectedSignature").GetString() == "SYNTHETICGEOMETRY/SYNTHETICRESULTS" && received.GetProperty("Scope").GetString() == "Everything",
            "Straus7 source: read with the selected result cases and the catalog signature returns the validated copy");
        async Task Refused(string mode, string text, string message)
        {
            try { await Source(mode).ReadAsync(request with { CaseNumbers = [1], ExpectedSignature = "S" }, null, CancellationToken.None); throw new Exception("Accepted: " + message); }
            catch (InvalidDataException ex) when (ex.Message.Contains(text) && ex.Message.Contains("Nessun dato è stato importato")) { Check(true, message); }
        }
        await Refused("fail", "Licenza API di prova non disponibile", "Straus7 source: worker errors reach the user in Italian with the exit meaning");
        await Refused("silent", "risultato completo", "Straus7 source: a worker ending without its result publishes nothing");
        await Refused("invalid", "non valida", "Straus7 source: an invalid copy from the worker is refused");
        using (var cancellation = new CancellationTokenSource())
        {
            var started = new TaskCompletionSource();
            var pending = Source("hang").ReadAsync(request with { CaseNumbers = [1], ExpectedSignature = "S" }, new SynchronousProgress(_ => started.TrySetResult()), cancellation.Token);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(60)); cancellation.Cancel();
            try { await pending; throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(true, "Straus7 source: cancelling ends the worker process and publishes nothing"); }
        }
        // With the apphost the substitute has its own executable; under "dotnet" other processes share the path and are not counted.
        if (!Path.GetFileNameWithoutExtension(SubstituteWorkerPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var orphan = Process.GetProcesses().Where(p => { try { return p.Id != Environment.ProcessId && p.MainModule?.FileName == SubstituteWorkerPath; } catch (Exception) { return false; } }).ToArray();
            Check(orphan.Length == 0, "no substitute worker survives the cancellation");
        }
        try { await new Strand7ModelSource(Path.Combine(directory, "assente.exe")).InspectAsync(request, null, CancellationToken.None); throw new Exception("Missing worker accepted"); }
        catch (InvalidDataException ex) when (ex.Message.Contains("non trovato")) { Check(true, "Straus7 source: a missing worker is reported, not hidden"); }
        try { await Source("catalog").ReadAsync(request with { CaseNumbers = [] }, null, CancellationToken.None); throw new Exception("Empty selection accepted"); }
        catch (InvalidDataException) { Check(true, "Straus7 source: a result import needs at least one case"); }

        // ---- View model ----
        var services = new Strand7Services(model, results, api);
        int commits = 0;
        using var vm = new ModelViewerViewModel(null, [], false, services, _ => commits++, (_, _, _, _) => { });
        vm.SelectedImportSource = 2;
        vm.ChooseStrand7ModelCommand.Execute(null); vm.ChooseStrand7ResultsCommand.Execute(null); vm.ChooseStrand7ApiCommand.Execute(null);
        Check(vm.IsStrand7Import && vm.IsLiveImport && vm.ImportActionLabel == "Importa da Straus7" && !vm.ImportSourceLabels[2].Contains("predisposizione") &&
              !vm.ImportActionLabel.Contains("attivare") && !vm.LiveImportLimitations.Contains("predispos") && !vm.ImportCommand.CanExecute(null), "Straus7 source is active, without preparation labels, and waits for the catalog");
        await vm.CheckStrand7SetupCommand.ExecuteAsync(null);
        Check(vm.HasStrand7Catalog && vm.ImportCases.Select(c => c.Name).SequenceEqual(["1: Dead", "2: Live"]) && vm.Strand7PreparationSummary.StartsWith("Catalogo letto", StringComparison.Ordinal) &&
              vm.ImportCommand.CanExecute(null) && commits == 0, "Straus7 check reads the catalog through the source and offers its cases");
        vm.ClearImportCasesCommand.Execute(null); Check(!vm.ImportCommand.CanExecute(null), "Straus7 result import needs a selected case");
        vm.ImportCases[1].IsSelected = true;
        await vm.ImportCommand.ExecuteAsync(null);
        Check(commits == 1 && vm.Snapshot?.Fingerprint() is { } imported && services.Read!.CaseNumbers.SequenceEqual([2]) && services.Read.ExpectedSignature == "SYNTHETICGEOMETRY/SYNTHETICRESULTS" &&
              vm.Cases.Contains("1: Dead") && vm.Status.Contains("completato"), "Straus7 import commits the copy read with the selected case numbers and the catalog signature");
        string current = vm.Snapshot!.Fingerprint();
        services.Hang = true; var hanging = vm.ImportCommand.ExecuteAsync(null);
        await services.Started.Task.WaitAsync(TimeSpan.FromSeconds(30)); vm.ImportCancelCommand.Execute(null); await hanging;
        Check(commits == 1 && vm.Snapshot.Fingerprint() == current && vm.Status == "Importazione annullata" && !vm.IsBusy, "cancelled Straus7 import keeps the displayed copy");
        services.Hang = false; services.Fail = true; await vm.ImportCommand.ExecuteAsync(null);
        Check(commits == 1 && vm.Snapshot.Fingerprint() == current && vm.HasImportFailure && vm.ImportFailure!.Recovery.Contains("processo di prova"), "failed Straus7 import keeps the displayed copy and explains the failure");
        vm.Strand7ModelPath = model + " "; Check(!vm.HasStrand7Catalog && vm.ImportCases.Count == 0 && !vm.ImportCommand.CanExecute(null), "changing a Straus7 file discards the catalog and its cases");
        vm.Strand7ModelPath = model; await vm.CheckStrand7SetupCommand.ExecuteAsync(null); vm.SelectedImportSource = 0;
        Check(vm.ImportCases.Count == 0 && !vm.HasStrand7Catalog && vm.ImportCommand.CanExecute(null), "switching source discards the Straus7 cases");
    }

    static string SubstituteWorkerPath => Environment.ProcessPath!;
    static Strand7ModelSource SubstituteWorker(string mode, string echo)
    {
        // Run as "dotnet ModelWorkspace.Checks.dll" or as the apphost: the same executable in substitute-worker mode.
        bool host = Path.GetFileNameWithoutExtension(SubstituteWorkerPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        string[] arguments = [.. host ? new[] { typeof(Program).Assembly.Location } : [], "--fake-strand7-worker", mode, echo];
        return new Strand7ModelSource(SubstituteWorkerPath, arguments);
    }

    /// <summary>Substitute worker: the protocol of ANTHEA.Strand7Worker with fixed answers, no native API.</summary>
    static int FakeStrand7Worker(string[] args)
    {
        string mode = args[1], echo = args[2];
        var request = Strand7WorkerProtocol.ReadRequest(args[3]);
        File.Copy(args[3], echo, true);
        Console.WriteLine(Strand7WorkerProtocol.Progress(1, 2, "Apertura del modello di prova…"));
        switch (mode)
        {
            case "catalog":
                Console.WriteLine(Strand7WorkerProtocol.Progress(2, 2, "Catalogo pronto."));
                Strand7WorkerProtocol.WriteResult(request.OutputPath, new Strand7ModelCatalog(12, 6, [new(1, "1: Dead"), new(2, "2: Live")], "SYNTHETICGEOMETRY/SYNTHETICRESULTS")
                    { Beams = 3, Plates = 3, Combinations = 1, LoadCases = ["1: Dead", "2: Live"], ApiVersion = "prova" });
                break;
            case "snapshot": Strand7WorkerProtocol.WriteResult(request.OutputPath, Strand7Sample.Snapshot()); break;
            case "invalid": var bad = Strand7Sample.Snapshot(); bad.Elements[0].Nodes[0] = 999; Strand7WorkerProtocol.WriteResult(request.OutputPath, bad); break;
            case "fail": Console.WriteLine(Strand7WorkerProtocol.Error("Licenza API di prova non disponibile.")); return Strand7WorkerProtocol.NativeUnavailable;
            case "silent": return Strand7WorkerProtocol.Success;
            case "hang": Thread.Sleep(Timeout.Infinite); break;
        }
        Console.WriteLine(Strand7WorkerProtocol.DoneLine);
        return Strand7WorkerProtocol.Success;
    }

    sealed class SynchronousProgress(Action<ModelImportProgress> report) : IProgress<ModelImportProgress> { public void Report(ModelImportProgress value) => report(value); }

    sealed class Strand7Services(string model, string results, string api) : IModelViewerServices
    {
        public Strand7ImportRequest? Read; public bool Hang, Fail; public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? ChooseStrand7File(Strand7ImportFile kind) => kind switch { Strand7ImportFile.Model => model, Strand7ImportFile.Results => results, _ => api };
        public Task<Strand7ModelCatalog> InspectStrand7Async(Strand7ImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token) =>
            Task.FromResult(new Strand7ModelCatalog(12, 6, [new(1, "1: Dead"), new(2, "2: Live")], "SYNTHETICGEOMETRY/SYNTHETICRESULTS") { Beams = 3, Plates = 3, Combinations = 1, LoadCases = ["1: Dead", "2: Live"] });
        public async Task<ModelSnapshot> ImportStrand7Async(Strand7ImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token)
        {
            Read = request;
            if (Hang) { Started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); }
            if (Fail) throw new InvalidDataException("Errore del processo di prova.");
            return Strand7Sample.Snapshot();
        }
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token) => Task.FromResult<ModelSnapshot?>(null);
        public void FitView() { }
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
    }
}
