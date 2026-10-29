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
            // 1.16.0: stages, masses, temperatures, reactions (GPC_STRAUS7_STAGES: "versioned" = tests/fixtures/model-viewer-straus7/native-stages-r31.zip,
            // or a folder with stages.st7, stages.LSA, stages-stage1.LSA and stages.NLA).
            string? stagesFolder = null;
            if (Environment.GetEnvironmentVariable("GPC_STRAUS7_STAGES") is { Length: > 0 } stages)
            {
                stagesFolder = Fresh();
                if (stages == "versioned") ZipFile.ExtractToDirectory(Path.Combine(RepositoryRoot(), "tests", "fixtures", "model-viewer-straus7", "native-stages-r31.zip"), stagesFolder);
                else foreach (var file in new[] { "stages.st7", "stages.LSA", "stages-stage1.LSA", "stages.NLA" }) CopyTo(stagesFolder, Path.Combine(stages, file));
            }
            // Real MIDAS exports (GPC_MIDAS_TEXT_SAMPLES) in the same opt-in run: ANTHEA against the GPC file adapter.
            int midas = MidasTextRealChecks(directory);
            if (sources.Count == 0 && stagesFolder == null)
            {
                File.WriteAllText(Path.Combine(output, "esito.txt"), midas > 0 ? $"PASS {checks} controlli · {midas} file MIDAS reali, nessun modello Straus7"
                    : "PASS 0 controlli nativi: variabili GPC_STRAUS7_ELEMENTS, GPC_STRAUS7_PLATES, GPC_STRAUS7_BRIDGHE, GPC_MIDAS_TEXT_SAMPLES non impostate");
                return 0;
            }
            string api = Strand7ImportSetup.FindInstalledApi();
            Check(api.Length > 0, "Straus7 R3 x64 API installed: " + api);
            string worker = WorkerExecutable();
            var summaries = new List<object>();
            foreach (var (label, model, results) in sources) summaries.Add(await NativeModel(label, model, results, worker, api, directory));
            if (stagesFolder != null) summaries.Add(await NativeStages(stagesFolder, worker, api, directory));
            File.WriteAllText(Path.Combine(directory, "riepilogo.json"), JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli nativi Straus7 · {sources.Count + (stagesFolder == null ? 0 : 1)} modelli" + (midas > 0 ? $" · {midas} file MIDAS reali" : ""));
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
        int corners = 0, stations = 0, equilibrium = 0, axial = 0, combinedReactions = 0, comparedReactions = 0; double worst = 0; string? combination = null;
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
                // 1.16.0: trusses and cables (axial force) and support reactions of the combination, against the sum of the imported terms.
                foreach (var type in new[] { "TRUSS", "CABLE" })
                {
                    var terms = definition.Terms.Select(t => (t.Factor, Values: ResultFields.Read(byName[t.Reference], type, "LOCAL", "Fx").ToDictionary(v => (v.Element, v.Station), v => v.Value))).ToArray();
                    var combined = ResultFields.Read(calculated, type, "LOCAL", "Fx");
                    if (terms.All(t => t.Values.Count == 0)) continue;
                    Check(combined.Count == terms[0].Values.Count && combined.All(v => Math.Abs(v.Value - terms.Sum(t => t.Factor * t.Values[(v.Element, v.Station)])) <= 1e-9 * Math.Max(1, Math.Abs(v.Value))),
                        $"{label}: GPC rebuilds the axial force of {combined.Select(v => v.Element).Distinct().Count()} {type} members in {definition.Name} as the sum of its terms");
                    axial += combined.Count;
                }
                if (calculated.Reactions is { Length: > 0 } rebuilt)
                {
                    Check(rebuilt.All(r => ModelReactions.Components.Select((_, k) => Math.Abs(r.Values[k] - definition.Terms.Sum(t => t.Factor * byName[t.Reference].Reactions!.Single(x => x.Node == r.Node).Values[k]))).Max()
                        <= 1e-9 * Math.Max(1, r.Values.Max(Math.Abs))), $"{label}: GPC rebuilds the {rebuilt.Length} support reactions of {definition.Name} as the sum of its terms");
                    combinedReactions = rebuilt.Length;
                }
            }
            // 1.16.0: reactions of the copy against the GPC samples, the equilibrium of every case as checked by the worker.
            int compared = 0;
            double scale = m.NodesElements.Values.SelectMany(n => n.Results.SelectMany(r => r.Results)).OfType<NodeResultForces>().Where(r => r.Kind == NodalForceKind.SupportReaction)
                .Select(r => Math.Max(Math.Abs(r.Fx), Math.Max(Math.Abs(r.Fy), Math.Abs(r.Fz)))).DefaultIfEmpty().Max();
            foreach (var node in m.NodesElements.Values)
                foreach (var sample in node.Results.SelectMany(r => r.Results).OfType<NodeResultForces>().Where(r => r.Kind == NodalForceKind.SupportReaction))
                {
                    var copy = byName[Strand7Snapshot.CaseName(sample.Case.Name)].Reactions?.SingleOrDefault(r => r.Node == int.Parse(node.Source.OriginalId));
                    if (copy == null) { Check(Math.Max(Math.Abs(sample.Fx), Math.Max(Math.Abs(sample.Fy), Math.Abs(sample.Fz))) <= 1e-6 * scale, $"{label}: outside the supports only round-off reactions are left out"); continue; }
                    worst = Math.Max(worst, Math.Abs(copy.Fz - sample.Fz * 1e-3) + Math.Abs(copy.Mx - sample.Mx * 1e-6)); compared++;
                }
            Check(compared > 0 && worst <= 1e-9, $"{label}: {compared} reactions of the copy are the GPC values in kN and kN·m");
            Check(snapshot.Results.All(r => r.Equilibrium?.Balanced == true), $"{label}: the worker checked the equilibrium of every case (GPC Equilibrium.Check)");
            comparedReactions = compared;
        }
        return new { Label = label, Model = Path.GetFileName(model), Results = results == null ? null : Path.GetFileName(results), catalog.SourceSignature, catalog.ApiVersion,
            Nodes = snapshot.Nodes.Length, Members = members, Plates = plates, Cases = snapshot.Results.Select(r => r.Name).ToArray(),
            SourceCombinations = snapshot.SourceCombinations?.Definitions.Length ?? 0, Boundaries = snapshot.Boundaries?.GroupBy(x => x.Kind).ToDictionary(g => g.Key, g => g.Count()),
            ComparedCorners = corners, ComparedStations = stations, MaximumDifference = worst, BalancedCases = equilibrium, RebuiltCombination = combination,
            ComparedReactions = comparedReactions, RebuiltAxialValues = axial, RebuiltReactions = combinedReactions,
            MassesTemperaturesStages = new[] { snapshot.Masses?.Length ?? 0, snapshot.Temperatures?.Length ?? 0, snapshot.ConstructionStages?.Length ?? 0 },
            InspectSeconds = inspect, ReadSeconds = read, Messages = snapshot.ImportMessages?.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.Count()) };
    }

    /// <summary>1.16.0: the stages fixture of GPC Model (generate_native_stages_fixture.py) through the real worker: stages, masses and temperatures as data,
    /// reactions and equilibrium of the unstaged solution (hand check of the weight 34.8466 kN in the provenance), trusses in the LSA combination,
    /// results of the active stage 1 without the inactive elements, staged NLA states as stage results.</summary>
    static async Task<object> NativeStages(string folder, string worker, string api, string directory)
    {
        var source = new Strand7ModelSource(worker); string model = Path.Combine(folder, "stages.st7");
        async Task<(Strand7ModelCatalog Catalog, ModelSnapshot Copy)> Read(string results)
        {
            var request = new Strand7ImportRequest(model, Path.Combine(folder, results), api, ModelUpdateScope.Everything, [], true, true);
            var catalog = await source.InspectAsync(request, null, CancellationToken.None);
            var copy = await source.ReadAsync(request with { CaseNumbers = catalog.Cases.Select(c => c.Number).ToArray(), ExpectedSignature = catalog.SourceSignature }, null, CancellationToken.None);
            copy.Validate(); File.WriteAllText(Path.Combine(directory, "stages " + results + ".antheamodel"), JsonSerializer.Serialize(copy));
            return (catalog, copy);
        }
        // Unstaged linear static solution of a model with stages.
        var (catalog, lsa) = await Read("stages.LSA");
        Check(catalog.Phases.Select(p => p.Name).SequenceEqual(["1: Deck only", "2: Complete"]) && !catalog.NonlinearStages && catalog.Cases.Count == 3 && catalog.Cases.All(c => c.StageNumber == null),
            "stages fixture: catalog with the two stages and the three unstaged linear static cases");
        Check(lsa.ConstructionStages is [{ Label: "1: Deck only", Type: "Unmorphed" } first, { Label: "2: Complete" } second] && first.Elements.SequenceEqual([1, 1000001]) &&
              second.Elements.SequenceEqual([1, 1000001, 1000002, 1000003]), "stages fixture: stages and their active elements as data of the copy");
        Check(lsa.Masses!.Length == 5 && lsa.Masses.Single(x => x.Kind == "NODE").Values.SequenceEqual([.5, .5, .5]) && Near(lsa.Masses.Single(x => x.Kind == "BEAM-NS").Values[0], .1) &&
              Near(lsa.Masses.Single(x => x.Kind == "PLATE-NS").Values[0], .2) && Near(lsa.Masses.Single(x => x.Kind == "NODE-ROTATION").Values[2], 30e-6) &&
              lsa.Temperatures!.Length == 6 && lsa.Temperatures.Single(x => x.Kind == "REFERENCE").Values[0] == 10 && Near(lsa.Temperatures.Single(x => x.Kind == "BEAM-GRADIENT").Values[0], 10),
            "stages fixture: masses (t, t/m, t/m², t·m²) and temperatures (°C, °C/m) of the source as data of the copy");
        var dead = lsa.Results.Single(r => r.Name == "1: Dead");
        Check(lsa.Results.All(r => r.Equilibrium?.Balanced == true) && Near(dead.Equilibrium!.AppliedForce[2], -34.8466, 1e-5) && Near(dead.Equilibrium.ReactionForce[2], 34.8466, 1e-5),
            "stages fixture: the GPC check balances loads, gravity on structural, node and non-structural masses against the reactions (34.8466 kN, hand check)");
        Check(Near(dead.Reactions!.Sum(r => r.Fz), 34.8466, 1e-5) && dead.Reactions!.Select(r => r.Node).SequenceEqual([1, 3, 4, 5, 8]), "stages fixture: reactions of the five supports");
        var uls = (await new GpcModelCombinationService().CalculateAsync(lsa, "STRAUS7/LSA/1")).Results.Single();
        var live = lsa.Results.Single(r => r.Name == "2: Live");
        double Axial(ModelResultSet r, double? station) => ResultFields.Read(r, "TRUSS", "LOCAL", "Fx").Single(v => v.Element == 1000003 && v.Station == station).Value;
        Check(ResultFields.Read(uls, "TRUSS", "LOCAL", "Fx").Count > 0 && ResultFields.Read(uls, "TRUSS", "LOCAL", "Fx").All(v => Near(v.Value, 1.35 * Axial(dead, v.Station) + 1.5 * Axial(live, v.Station), 1e-9)),
            "stages fixture: the truss enters the rebuilt ULS = 1.35 Dead + 1.5 Live");
        // Linear static solution with the active stage 1.
        var (stageCatalog, stage1) = await Read("stages-stage1.LSA");
        Check(stageCatalog.StageResultsAvailable && stageCatalog.Cases.All(c => c.StageNumber == 1) && stageCatalog.Warnings.Any(w => w.Contains("fase attiva 1: Deck only")),
            "active stage: the catalog names the stage of the linear static cases");
        Check(stage1.Results.All(r => r.Stage == null && r.Equilibrium?.Balanced == null) && stage1.Results.All(r => ResultFields.Read(r, "BEAM", "LOCAL", "Fx").All(v => v.Element == 1000001) &&
              ResultFields.Read(r, "TRUSS", "LOCAL", "Fx").Count == 0) && stage1.ImportMessages!.Any(m => m.Code == "Straus7ActiveStageResults"),
            "active stage: static cases of that configuration, without the inactive beam and truss, equilibrium not evaluated");
        var stageUls = (await new GpcModelCombinationService().CalculateAsync(stage1, "STRAUS7/LSA/1")).Results.Single();
        Check(ResultFields.Read(stageUls, "BEAM", "LOCAL", "My").Count > 0, "active stage: the LSA combination of the stage is rebuilt from its cases");
        // Staged nonlinear solution.
        var (nlaCatalog, nla) = await Read("stages.NLA");
        Check(nlaCatalog.NonlinearStages && nlaCatalog.Cases.Select(c => c.Name).SequenceEqual(["NLA · 1: Deck only · 1: Dead", "NLA · 2: Complete · 2: Live half", "NLA · 2: Complete · 3: Live full"]),
            "staged NLA: the catalog lists one state per stage and increment");
        Check(nla.Version == 2 && nla.Results.Length == 3 && nla.Results.All(r => r.Stage is { SourceCase: "NLA", DisplacementReference: "Accumulative" }) &&
              nla.Results[0].Name == "NLA [CS · 1: Deck only · 1: Dead · Accumulative]" && ResultFields.Read(nla.Results[0], "TRUSS", "LOCAL", "Fx").Count == 0 &&
              ResultFields.Read(nla.Results[1], "TRUSS", "LOCAL", "Fx").Count > 0, "staged NLA: stage results of the copy, the truss only from stage 2");
        Check(Near(nla.Results[1].Reactions!.Single(r => r.Node == 4).Fz, 8.272, 1e-3) && ModelCombinations.Inspect(nla).All(a => !a.CanCalculate),
            "staged NLA: reactions of the state (truss support 8.272 kN, unmorphed stage) and no combination of stage results");
        return new { Label = "native-stages-r31", Phases = catalog.Phases.Count, lsa.Masses.Length, Temperatures = lsa.Temperatures.Length, Reactions = dead.Reactions.Length,
            AppliedZ = dead.Equilibrium.AppliedForce[2], StageCases = stage1.Results.Length, NonlinearStates = nla.Results.Select(r => r.Name).ToArray() };
    }
}
