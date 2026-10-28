using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using ANTHEA.ModelWorkspace;
using GPC.Converter;
using GPC.Converter.Straus7;
using GPC.Model.Elements;
using GPC.Model.Loads;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Results.State;

static partial class Program
{
    /// <summary>Opt-in native checks (installed and licensed Straus7 R3 x64), as the native tests of GPC Model:
    /// GPC_STRAUS7_ELEMENTS ("versioned" = tests/fixtures/model-viewer-straus7/native-elements-r31.zip, or a folder with elements.st7 and elements.LSA),
    /// GPC_STRAUS7_PLATES (a copy of the manufacturer's Plates.st7), GPC_STRAUS7_BRIDGHE (a folder of BridGHe *.st7 with their .LSA).
    /// Every source is copied into a new temporary folder; the real worker reads the copy, and this test process reads it again with the GPC
    /// converter (tests only) to compare the snapshot with the GPC samples, to check the equilibrium of loads and reactions and to compare a
    /// GPC-rebuilt combination with the sum of its terms.</summary>
    static async Task<int> Strand7NativeChecks(string output)
    {
        try
        {
            string directory = Path.Combine(output, "straus7-native"); Directory.CreateDirectory(directory);
            static string Fresh() { var folder = Path.Combine(Path.GetTempPath(), "anthea-straus7-native-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); return folder; }
            static string CopyTo(string folder, string file) { var target = Path.Combine(folder, Path.GetFileName(file)); File.Copy(file, target); return target; }
            var sources = new List<(string Label, string Model, string? Results)>();
            if (Environment.GetEnvironmentVariable("GPC_STRAUS7_ELEMENTS") is { Length: > 0 } elements)
            {
                var folder = Fresh();
                if (elements == "versioned") ZipFile.ExtractToDirectory(Path.Combine(RepositoryRoot(), "tests", "fixtures", "model-viewer-straus7", "native-elements-r31.zip"), folder);
                else { CopyTo(folder, Path.Combine(elements, "elements.st7")); CopyTo(folder, Path.Combine(elements, "elements.LSA")); }
                sources.Add(("native-elements-r31", Path.Combine(folder, "elements.st7"), Path.Combine(folder, "elements.LSA")));
            }
            if (Environment.GetEnvironmentVariable("GPC_STRAUS7_PLATES") is { Length: > 0 } plates)
                sources.Add(("Plates", CopyTo(Fresh(), plates), null));
            if (Environment.GetEnvironmentVariable("GPC_STRAUS7_BRIDGHE") is { Length: > 0 } bridghe)
                foreach (var file in Directory.GetFiles(bridghe, "*.st7").OrderBy(f => f, StringComparer.Ordinal))
                    if (File.Exists(Path.ChangeExtension(file, ".LSA")))
                    { var folder = Fresh(); sources.Add(("BridGHe " + Path.GetFileNameWithoutExtension(file), CopyTo(folder, file), CopyTo(folder, Path.ChangeExtension(file, ".LSA")))); }
            if (sources.Count == 0)
            {
                File.WriteAllText(Path.Combine(output, "esito.txt"), "PASS 0 controlli nativi Straus7: variabili GPC_STRAUS7_ELEMENTS, GPC_STRAUS7_PLATES, GPC_STRAUS7_BRIDGHE non impostate");
                return 0;
            }
            string api = Strand7ImportSetup.FindInstalledApi();
            Check(api.Length > 0, "Straus7 R3 x64 API installed: " + api);
            string worker = WorkerExecutable();
            var summaries = new List<object>();
            foreach (var (label, model, results) in sources) summaries.Add(await NativeModel(label, model, results, worker, api, directory));
            File.WriteAllText(Path.Combine(directory, "riepilogo.json"), JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli nativi Straus7 · {sources.Count} modelli");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); File.WriteAllText(Path.Combine(output, "errore.txt"), ex.ToString()); return 1; }
    }

    static string WorkerExecutable()
    {
        string configuration = AppContext.BaseDirectory.Contains(Path.DirectorySeparatorChar + "Debug" + Path.DirectorySeparatorChar) ? "Debug" : "Release";
        var candidates = new[] { Path.Combine(AppContext.BaseDirectory, Strand7WorkerProtocol.WorkerFileName),
            Path.Combine(RepositoryRoot(), "src", "ANTHEA.Strand7Worker", "bin", configuration, "net8.0", Strand7WorkerProtocol.WorkerFileName) };
        return candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Worker Straus7 non compilato.", candidates[1]);
    }

    static async Task<object> NativeModel(string label, string model, string? results, string worker, string api, string directory)
    {
        var source = new Strand7ModelSource(worker);
        var request = new Strand7ImportRequest(model, results, api, results == null ? ModelUpdateScope.ModelWithoutResults : ModelUpdateScope.Everything, [], true, true);
        var watch = Stopwatch.StartNew();
        var catalog = await source.InspectAsync(request, null, CancellationToken.None);
        double inspect = watch.Elapsed.TotalSeconds;
        var cases = catalog.Cases.Where(c => c.StageNumber == null).Select(c => c.Number).ToArray();
        Check(results == null || cases.Length > 0, label + ": catalog of the linear static result cases from the worker");
        watch.Restart();
        var snapshot = await source.ReadAsync(request with { CaseNumbers = cases, ExpectedSignature = catalog.SourceSignature }, null, CancellationToken.None);
        double read = watch.Elapsed.TotalSeconds;
        snapshot.Validate();
        int plates = snapshot.Elements.Count(e => e.Type == "PLATE"), members = snapshot.Elements.Length - plates;
        Check(snapshot.Nodes.Length == catalog.Nodes && members <= catalog.Beams && (plates == catalog.Plates || snapshot.ImportMessages!.Any(m => m.Code == "GPC-PLATE-THICKNESS-MISSING")),
            label + $": worker copy with {snapshot.Nodes.Length} nodes, {members} members, {plates} plates");
        string json = JsonSerializer.Serialize(snapshot);
        if (json.Length < 40_000_000) File.WriteAllText(Path.Combine(directory, label + ".antheamodel"), json);

        // Independent reading in this test process: the GPC converter on the same copy.
        var converter = new Straus7ApiConverter(api);
        var geometry = converter.Import(new Straus7ImportRequest { ModelPath = model, ModelRevision = "check " + label, AnalysisId = "check" });
        Check(geometry.Model != null, label + ": GPC import in the test process");
        var m = geometry.Model!;
        Check(m.NodesElements.Count == snapshot.Nodes.Length && m.AreaElements.Count == catalog.Plates && m.BeamElements.Count == catalog.Beams, label + ": same model as the GPC converter");
        int corners = 0, stations = 0, equilibrium = 0; double worst = 0; string? combination = null;
        if (results != null)
        {
            var listing = converter.ReadResults(new Straus7ResultsRequest { ModelPath = model, ResultPath = results });
            var map = Straus7LinearStaticResults.CaseMap(m, listing.Cases);
            var names = listing.Cases.Where(c => c.Stage == 0 && map.ContainsKey(c.Number)).Select(c => map[c.Number]).ToArray();
            var native = converter.ReadResults(m, new ResultFilter { StaticCases = names }.Resolve(m), model, results, false, 5, default, true);
            Check(native.Status == ImportStatus.Completed, label + ": GPC results read in the test process");
            var imported = Straus7LinearStaticResults.Import(m, native, "check");
            Check(imported.Status is ImportStatus.Completed or ImportStatus.Partial, label + ": GPC normalization in the test process");
            var byName = snapshot.Results.ToDictionary(r => r.Name);
            var cornerValues = snapshot.Results.ToDictionary(r => r.Name, r => r.Values.ToDictionary(v => (v.Element, v.Node)));
            var sourceNodes = m.NodesElements.Values.ToDictionary(n => n.Id, n => int.Parse(n.Source.OriginalId));
            foreach (var plate in m.AreaElements.Values)
                foreach (var sample in plate.Results.SelectMany(r => r.Results).OfType<PointResultPlateForces>().Where(s => s.PointKind == ShellResultPointKind.ElementNodeExtrapolated))
                {
                    if (!cornerValues[Strand7Snapshot.CaseName(sample.Case.Name)].TryGetValue((int.Parse(plate.Source.OriginalId), sourceNodes[sample.SourceNodeId!.Value]), out var value)) continue;
                    worst = Math.Max(worst, Math.Abs(value.Values[0] + sample.Forces.Mxx * 1e-3) + Math.Abs(value.Values[3] - sample.Forces.Fxx) + Math.Abs(value.Values[1] + sample.Forces.Myy * 1e-3));
                    corners++;
                }
            var moments = snapshot.Results.ToDictionary(r => r.Name, r => ResultFields.Read(r, "BEAM", "LOCAL", "My").ToDictionary(v => (v.Element, v.Station)));
            foreach (var beam in m.BeamElements.Values.Where(x => x.Assignments.Formulation == GPC.Model.Structure.Assignments.BeamFormulation.StraightTwoNode))
                foreach (var sample in beam.Results.SelectMany(r => r.Results).OfType<StationResultBeamForces>())
                {
                    if (!moments[Strand7Snapshot.CaseName(sample.Case.Name)].TryGetValue((Strand7Snapshot.BeamIdOffset + int.Parse(beam.Source.OriginalId), (double?)sample.ParametricDistance), out var value)) continue;
                    worst = Math.Max(worst, Math.Abs(value.Value + sample.ResultBeamForces.M1 * 1e-6)); stations++;
                }
            Check(corners > 0 || m.AreaElements.Count == 0, label + $": {corners} plate corners compared with the GPC samples");
            Check(worst <= 1e-9, label + $": snapshot values are the GPC values with the declared units and signs (scarto {worst:E2})");
            foreach (var c in native.Cases)
            {
                var name = map[c.Number];
                var contributions = new List<EquilibriumContribution>();
                foreach (var node in m.NodesElements.Values) contributions.AddRange(node.Loads.Values.OfType<PointLoad>().Where(l => l.LoadCase.Name == name).Select(l => Equilibrium.PointLoad(l, "node")));
                foreach (var beam in m.BeamElements.Values)
                    contributions.AddRange(beam.Assignments.Loads.Where(l => (l.Concentrated?.LoadCase ?? l.StartIntensity.LoadCase).Name == name).Select(l => Equilibrium.BeamLoad(beam, l)));
                contributions.AddRange(m.ModelLoads.Values.OfType<ModelGravityLoad>().Where(g => g.LoadCase.Name == name).Select(g => Equilibrium.Gravity(m, g, "gravity")));
                double applied = contributions.Sum(x => x.Force.Z);
                double reactions = m.NodesElements.Values.SelectMany(n => n.Results.SelectMany(r => r.Results)).OfType<NodeResultForces>()
                    .Where(r => r.Case?.Name == name && r.Kind == NodalForceKind.SupportReaction).Sum(r => r.Fz);
                Console.WriteLine($"  {label} {name}: applied Z {applied:F3} N, reactions Z {reactions:F3} N");
                Check(Math.Abs(applied + reactions) <= 1e-6 * Math.Max(1, Math.Abs(applied)), $"{label} {Strand7Snapshot.CaseName(name)}: imported loads balance the solver reactions ({applied:F1} N)");
                equilibrium++;
            }
            var definition = snapshot.SourceCombinations?.Definitions.FirstOrDefault(d => d.Kind == ModelCombinationKind.Linear && d.Terms.All(t => byName.ContainsKey(t.Reference)));
            if (definition != null)
            {
                var calculated = (await new GpcModelCombinationService().CalculateAsync(snapshot, definition.Id)).Results.Single();
                double value, expected; string unit;
                if (calculated.Values.Length > 0)
                {
                    var target = calculated.Values.MaxBy(v => Math.Abs(v.Values[0]))!; value = target.Values[0]; unit = "kN·m/m, piastra " + target.Element;
                    expected = definition.Terms.Sum(t => t.Factor * cornerValues[t.Reference][(target.Element, target.Node)].Values[0]);
                }
                else
                {
                    // Members only (BridGHe): the largest My of the rebuilt combination against the sum of the imported stations.
                    var target = ResultFields.Read(calculated, "BEAM", "LOCAL", "My").MaxBy(v => Math.Abs(v.Value))!; value = target.Value; unit = "kN·m, asta " + target.Element + " x/L " + target.Station;
                    expected = definition.Terms.Sum(t => t.Factor * moments[t.Reference][(target.Element, target.Station)].Value);
                }
                Check(Math.Abs(value - expected) <= 1e-9 * Math.Max(1, Math.Abs(expected)), $"{label}: GPC rebuilds the LSA combination {definition.Name} as the sum of its terms ({expected:G6} {unit})");
                combination = definition.Name;
            }
        }
        return new { Label = label, Model = Path.GetFileName(model), Results = results == null ? null : Path.GetFileName(results), catalog.SourceSignature, catalog.ApiVersion,
            Nodes = snapshot.Nodes.Length, Members = members, Plates = plates, Cases = snapshot.Results.Select(r => r.Name).ToArray(),
            SourceCombinations = snapshot.SourceCombinations?.Definitions.Length ?? 0, Boundaries = snapshot.Boundaries?.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Count()),
            ComparedCorners = corners, ComparedStations = stations, MaximumDifference = worst, BalancedCases = equilibrium, RebuiltCombination = combination,
            InspectSeconds = inspect, ReadSeconds = read, Messages = snapshot.ImportMessages?.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.Count()) };
    }
}
