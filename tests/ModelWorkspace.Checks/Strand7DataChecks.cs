using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using GPC.Converter;
using GPC.Converter.Straus7;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Core.Units;
using GPC.Model.Models;
using GPC.Model.Results.Locations;
using GPC.Model.Results.State;
using GPC.Model.Stages;
using GPC.Model.Structure.Assignments;

static partial class Program
{
    /// <summary>1.16.0: Straus7 stages, trusses and cables in the combinations, support reactions with the equilibrium check, masses and temperatures,
    /// without the native API (synthetic GPC models, every expected value an input of the check). The native counterpart is Strand7NativeChecks.</summary>
    static async Task Strand7DataChecks(string output)
    {
        string directory = Path.Combine(output, "straus7-dati"); Directory.CreateDirectory(directory);
        // ---- Reactions in the copy ----
        var snapshot = Strand7Sample.Snapshot(); snapshot.Validate();
        var dead = snapshot.Results.Single(r => r.Name == "1: Dead");
        Check(dead.Reactions!.Select(r => r.Node).SequenceEqual([1, 4]) && Near(dead.Reactions![0].Fz, .1) && Near(dead.Reactions[1].Fz, -.1) && Near(dead.Reactions[1].Mx, .1) &&
              snapshot.Results.Single(r => r.Name == "2: Live").Reactions![0].Fz == .2, "Straus7 bridge: support reactions in kN and kN·m, global axes, the force of the support on the node");

        // ---- Trusses and reactions in the GPC combinations ----
        var service = new GpcModelCombinationService();
        var calculated = await service.CalculateAsync(snapshot, "STRAUS7/LSA/1");
        var uls = calculated.Results.Single();
        var truss = ResultFields.Read(uls, "TRUSS", "LOCAL", "Fx");
        Check(truss.Count == 2 && truss.All(v => v.Element == 1000003 && Near(v.Value, 1.35 * -5 + 1.5 * -10)) && truss.Select(v => v.Station).Order().SequenceEqual(new double?[] { 0, 1 }),
            "GPC rebuilds the axial force of the trusses in the LSA combination: 1.35 Dead + 1.5 Live at both ends");
        Check(Near(ResultFields.Read(uls, "BEAM", "LOCAL", "My").Single(v => v.Element == 1000002 && v.Station == .5).Value, 1.35 * -1 + 1.5 * -2), "beam stations of the combination unchanged by the trusses");
        Check(uls.Reactions is { Length: 2 } reactions && reactions[0].Node == 1 && Near(reactions[0].Fz, 1.35 * .1 + 1.5 * .2) && Near(reactions[1].Mx, 1.35 * .1 + 1.5 * .2),
            "GPC rebuilds the support reactions of the combination");
        var attached = ModelCombinations.Attach(snapshot, "STRAUS7/LSA/1", calculated); attached.Validate();
        Check(ModelSnapshot.FromJson(JsonSerializer.Serialize(attached)).Results.Single(r => r.Combination != null).Reactions!.Length == 2, "the combination with trusses and reactions is attached and reopened");
        var envelope = ModelCombinations.Save(snapshot, new("ANTHEA/inviluppo", "Inviluppo", ModelCombinationKind.Envelope, "ANTHEA", "", [new("1: Dead", false, 1), new("2: Live", false, 1)], true));
        var enveloped = await service.CalculateAsync(envelope, "ANTHEA/inviluppo");
        var min = enveloped.Results.Single(r => r.Name.EndsWith("(min)")); var max = enveloped.Results.Single(r => r.Name.EndsWith("(max)"));
        Check(ResultFields.Read(min, "TRUSS", "LOCAL", "Fx").All(v => Near(v.Value, -10)) && ResultFields.Read(max, "TRUSS", "LOCAL", "Fx").All(v => Near(v.Value, -5)) &&
              min.Combination!.Governing.Count(g => g.Family == "TRUSS" && g.Component == "Fx") == 2 && Near(min.Reactions!.Single(r => r.Node == 1).Fz, .1) &&
              Near(max.Reactions!.Single(r => r.Node == 1).Fz, .2) && max.Combination!.Governing.Any(g => g.Family == "REACTION" && g.Node == 4 && g.Component == "Mx"),
            "envelope of the Straus7 cases: trusses and reactions with their governing alternatives");
        ModelCombinations.Attach(envelope, "ANTHEA/inviluppo", enveloped).Validate();
        Check(true, "an envelope with truss and reaction governing alternatives passes the document validation");

        // ---- Equilibrium (GPC) ----
        var model = Strand7Sample.Model();
        var balance = Straus7Equilibrium.Check(model, Strand7Sample.Dead, Strand7Sample.Dataset);
        Check(balance.Balanced == true && balance.Reactions == 2 && balance.ReactionForce.All(v => Math.Abs(v) < 1e-9), "GPC equilibrium: a self-balanced pair of reactions without loads balances");
        var check = Strand7Snapshot.Equilibrium(balance);
        Check(check.Balanced == true && check.Note.StartsWith("Carichi applicati e reazioni vincolari in equilibrio") && check.ReactionForce.Length == 3, "the GPC check as data of the copy (kN, kN·m)");
        var balanced = GpcModelSnapshot.Create(model, Strand7Snapshot.Options(model, "equilibrio", "Straus7 · sintetico", Strand7Sample.Dataset, true, true, Strand7Snapshot.Conventions(true),
            null, name => name == Strand7Sample.Dead ? check : null));
        balanced.Validate();
        Check(balanced.Results.Single(r => r.Name == "1: Dead").Equilibrium?.Balanced == true && balanced.Results.Single(r => r.Name == "2: Live").Equilibrium == null,
            "the equilibrium check goes with its static case");
        var tampered = ModelSnapshot.FromJson(JsonSerializer.Serialize(balanced));
        tampered.Results[0] = tampered.Results[0] with { Equilibrium = check with { AppliedForce = [1] } };
        Reject(tampered.Validate, "an incomplete equilibrium resultant is rejected");
        tampered = ModelSnapshot.FromJson(JsonSerializer.Serialize(balanced));
        tampered.Results[0] = tampered.Results[0] with { Reactions = [new(999, 0, 0, 0, 0, 0, 0)] };
        Reject(tampered.Validate, "a reaction of an absent node is rejected");

        // ---- Masses, temperatures and stages as data ----
        var data = new Straus7ModelData();
        data.Stages.Add(new() { Number = 1, Name = "Impalcato", ModelName = "Stage 1: Impalcato", Type = Straus7StageType.Unmorphed, EnabledGroupIds = [2], EnabledGroups = [@"Group 2: Model\Deck"],
            ActiveBeams = [1, 2, 99], ActivePlates = [1], Record = "Stage/1" });
        data.Stages.Add(new() { Number = 2, Name = "Completa", ModelName = "Stage 2: Completa", Type = Straus7StageType.Morphed, EnabledGroupIds = [2, 3], EnabledGroups = ["a", "b"],
            ActiveBeams = [1, 2, 3], ActivePlates = [1, 2, 3], Record = "Stage/2" });
        data.Masses.Add(new() { Kind = Straus7MassKind.NodeTranslational, Number = 3, Values = [.5, .5, .25], Record = "Node/3/translational mass" });
        data.Masses.Add(new() { Kind = Straus7MassKind.NodeRotational, Number = 3, Values = [1e6, 2e6, 3e6], Axes = [1, 0, 0, 0, 1, 0, 0, 0, 1], Record = "Node/3/rotational mass" });
        data.Masses.Add(new() { Kind = Straus7MassKind.NodeNonStructural, Number = 2, Case = Strand7Sample.Dead, Values = [.2], DynamicFactor = 1, Offset = [0, 0, 100], Record = "Node/2/ns" });
        data.Masses.Add(new() { Kind = Straus7MassKind.BeamNonStructural, Number = 1, Case = Strand7Sample.Dead, Values = [1e-4, 3e-4], Stations = [0, 1], DynamicFactor = 1, Offset = [0, 0, 0], Record = "Beam/1/ns" });
        data.Masses.Add(new() { Kind = Straus7MassKind.PlateNonStructural, Number = 1, Case = Strand7Sample.Dead, Values = [2e-7], DynamicFactor = .8, Offset = [0, 0, 0], Record = "Plate/1/ns" });
        data.Masses.Add(new() { Kind = Straus7MassKind.NodeTranslational, Number = 999, Values = [1, 1, 1], Record = "Node/999/translational mass" });
        data.Temperatures.Add(new() { Kind = Straus7TemperatureKind.ReferenceTemperature, Case = Strand7Sample.Live, Values = [10], Record = "LoadCase/2/reference temperature" });
        data.Temperatures.Add(new() { Kind = Straus7TemperatureKind.NodeTemperature, Number = 1, Case = Strand7Sample.Live, Values = [40], Type = "Reference", Record = "Node/1/temperature" });
        data.Temperatures.Add(new() { Kind = Straus7TemperatureKind.BeamGradient, Number = 2, Case = Strand7Sample.Live, Values = [.01, -.005], Record = "Beam/2/gradient" });
        data.Temperatures.Add(new() { Kind = Straus7TemperatureKind.PlateGradient, Number = 3, Case = Strand7Sample.Live, Values = [.02], Record = "Plate/3/gradient" });
        model = Strand7Sample.Model();
        using (var stream = new MemoryStream())
        {
            new DataContractJsonSerializer(typeof(Straus7ModelData)).WriteObject(stream, data);
            model.PreservedSourceData.Add(new PreservedAssignment { Kind = Straus7ModelData.Kind, SourceRecord = Straus7ModelData.Record, RawData = Encoding.UTF8.GetString(stream.ToArray()) });
        }
        var withData = GpcModelSnapshot.Create(model, Strand7Snapshot.Options(model, "dati", "Straus7 · sintetico", null, true, true, Strand7Snapshot.Conventions(false)));
        var note = Strand7Snapshot.AddModelData(model, withData); withData.Validate();
        File.WriteAllText(Path.Combine(directory, "masse-temperature-fasi.antheamodel"), JsonSerializer.Serialize(withData));
        ModelMass Mass(string kind) => withData.Masses!.Single(m => m.Kind == kind);
        Check(withData.Masses!.Length == 5 && Mass("NODE") is { Entity: 3, Case: null } t && t.Values.SequenceEqual([.5, .5, .25]) &&
              Mass("NODE-ROTATION").Values.SequenceEqual([1, 2, 3]) && Mass("NODE-ROTATION").Axes!.Z.Z == 1 &&
              Mass("NODE-NS") is { Entity: 2, Case: "1: Dead" } n && n.Offset!.Z == .1 && n.DynamicFactor == 1 &&
              Mass("BEAM-NS") is { Entity: 1000001 } b && Near(b.Values[0], .1) && Near(b.Values[1], .3) && b.Stations!.SequenceEqual([0d, 1]) &&
              Mass("PLATE-NS") is { Entity: 1 } p && Near(p.Values[0], .2), "masses as data of the copy: t, t·m², t/m, t/m², offsets in m, beams with the Straus7 offset of the numbers");
        ModelTemperature Temperature(string kind) => withData.Temperatures!.Single(x => x.Kind == kind);
        Check(withData.Temperatures!.Length == 4 && Temperature("REFERENCE") is { Entity: 0, Case: "2: Live" } && Temperature("NODE") is { Entity: 1, Type: "Reference" } &&
              Temperature("BEAM-GRADIENT") is { Entity: 1000002 } g && Near(g.Values[0], 10) && Near(g.Values[1], -5) && Near(Temperature("PLATE-GRADIENT").Values[0], 20),
            "temperatures as data of the copy: °C and gradients in °C/m");
        Check(withData.ConstructionStages!.Length == 2 && withData.ConstructionStages[0] is { Label: "1: Impalcato", Type: "Unmorphed" } s1 && s1.Elements.SequenceEqual([1, 1000001, 1000002]) &&
              withData.ConstructionStages[1].Elements.Length == 6, "stages as data of the copy: label, type, groups and the elements of the copy active in each");
        Check(note is { Severity: "Warning" } && note.Message.Contains("1 dati di nodi o elementi assenti"), "data of nodes or elements outside the copy are left out and counted");
        Check(ModelSnapshot.FromJson(JsonSerializer.Serialize(withData)).Fingerprint() == withData.Fingerprint(), "masses, temperatures and stages survive the package round trip");
        tampered = ModelSnapshot.FromJson(JsonSerializer.Serialize(withData)); tampered.Masses = [new("BEAM-NS", 1, "1: Dead", [1])];
        Reject(tampered.Validate, "a beam mass on a plate is rejected");
        tampered = ModelSnapshot.FromJson(JsonSerializer.Serialize(withData)); tampered.Temperatures = [new("NODE", 999, "1: Dead", [1])];
        Reject(tampered.Validate, "a temperature of an absent node is rejected");
        tampered = ModelSnapshot.FromJson(JsonSerializer.Serialize(withData)); tampered.ConstructionStages = [.. tampered.ConstructionStages!, tampered.ConstructionStages![0]];
        Reject(tampered.Validate, "duplicate stages are rejected");
        var plain = JsonSerializer.Serialize(Strand7Sample.Snapshot(false));
        Check(!plain.Contains("\"Masses\"") && !plain.Contains("\"Temperatures\"") && !plain.Contains("\"ConstructionStages\"") && !plain.Contains("\"Reactions\"") && !plain.Contains("\"Equilibrium\""),
            "a copy without the new data writes none of the new fields (fingerprints of the existing documents unchanged)");

        // ---- Stage results of a staged nonlinear solution ----
        var staged = StagedSample(out string nonlinearDataset);
        var stagedCopy = GpcModelSnapshot.Create(staged, Strand7Snapshot.Options(staged, "fasi", "Straus7 · sintetico NLA", nonlinearDataset, true, true, Strand7Snapshot.Conventions(true, true), new string('A', 64)));
        stagedCopy.Validate();
        var state = stagedCopy.Results.Single();
        Check(stagedCopy.Version == 2 && state.Name == "NLA [CS · 1: Getto · 1: Peso · Accumulative]" && state.Stage is { SourceCase: "NLA", Phase: "1: Getto", Step: "1: Peso", DisplacementReference: "Accumulative" } &&
              ResultFields.Read(state, "BEAM", "LOCAL", "Fx").Count == 2 && state.Displacements!.Length == 2 && state.Reactions!.Single().Node == 1,
            "staged nonlinear states are stage results of the copy: NLA · phase · increment, format 2, with forces, displacements and reactions");
        Check(stagedCopy.SourceCombinations!.StaticCases.SequenceEqual(["1: Peso"]) && !ModelCombinations.StaticCaseNames(stagedCopy).Contains(state.Name), "the stage output case and its states are never static cases of the combinations");
        Reject(() => GpcModelSnapshot.Create(staged, Strand7Snapshot.Options(staged, "fasi", "x", nonlinearDataset, true, true, [])), "stage results without the SHA-256 of their source are refused");

        // ---- Results files .lsa and .nla ----
        string st7 = Path.Combine(directory, "m.st7"), nla = Path.Combine(directory, "m.nla"), txt = Path.Combine(directory, "m.txt"), api = Path.Combine(directory, "St7api.dll");
        foreach (var file in new[] { st7, nla, txt, api }) File.WriteAllText(file, "x");
        var setup = Strand7ImportSetup.Check(new(st7, nla, api, ModelUpdateScope.Everything, [], true, true));
        Check(!setup.Issues.Any(i => i.StartsWith("Risultati")), "a staged nonlinear results file (.nla) is accepted next to .lsa");
        setup = Strand7ImportSetup.Check(new(st7, txt, api, ModelUpdateScope.Everything, [], true, true));
        Check(setup.Issues.Any(i => i.StartsWith("Risultati: seleziona un file .lsa o .nla")), "another results file is refused");

        // ---- Tables of the viewer ----
        withData.Results = Strand7Sample.Snapshot().Results.Select(r => r with { Equilibrium = r.Name == "1: Dead" ? check : null }).ToArray(); withData.Validate();

        // ---- Partial updates: masses, temperatures and stages are attributes of the model ----
        var modelOnly = ModelSnapshotUpdate.Apply(null, withData, ModelUpdateScope.ModelWithoutResults);
        Check(modelOnly.Masses!.Length == 5 && modelOnly.Temperatures!.Length == 4 && modelOnly.ConstructionStages!.Length == 2 && modelOnly.Results.Length == 0,
            "an update of the model without results keeps masses, temperatures and stages");
        var geometryOnly = ModelSnapshotUpdate.Apply(null, withData, ModelUpdateScope.Geometry);
        Check(geometryOnly.Masses == null && geometryOnly.Temperatures == null && geometryOnly.ConstructionStages == null, "a geometry-only update leaves them out with the other attributes");
        var resultsUpdate = ModelSnapshotUpdate.Apply(modelOnly, withData, ModelUpdateScope.Results);
        Check(resultsUpdate.Masses!.Length == 5 && resultsUpdate.Results.Single(r => r.Name == "1: Dead").Reactions!.Length == 2, "a results-only update keeps the data and brings the reactions");
        var differentMasses = ModelSnapshot.FromJson(JsonSerializer.Serialize(withData)); differentMasses.Masses = [differentMasses.Masses![0]];
        Reject(() => ModelSnapshotUpdate.Apply(modelOnly, differentMasses, ModelUpdateScope.Results), "a results-only update refuses a copy with other masses");
        var stagedModel = ModelSnapshotUpdate.Apply(null, stagedCopy, ModelUpdateScope.ModelWithoutResults); stagedModel.Version = 1; stagedModel.Validate();
        var stagedUpdate = ModelSnapshotUpdate.Apply(stagedModel, stagedCopy, ModelUpdateScope.Results);
        Check(stagedUpdate.Version == 2 && stagedUpdate.Results.Single().Stage != null, "results for stage update a copy of format 1 to the format 2");
        using var vm = new ModelViewerViewModel(withData, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        vm.ShowAllCommand.Execute(null); vm.ShowTable = true; vm.TableOnlyVisible = false; vm.SelectedCase = "1: Dead";
        vm.SelectedTableKind = 7;
        Check(vm.TableKinds.Count == 11 && vm.TableRows!.Count == 2 && (double)vm.TableRows.Cast<System.Data.DataRowView>().Single(r => (int)r["Nodo"] == 4)["Mx [kN·m]"] == .1 &&
              vm.TableSummary.Contains("in equilibrio"), "reactions table of the current case with the GPC equilibrium check");
        vm.SelectedTableKind = 8;
        Check(vm.TableRows!.Count == 5 && vm.TableRows.Cast<System.Data.DataRowView>().Any(r => (string)r["Tipo"] == "Massa non strutturale · asta" && (string)r["Unità"] == "t/m"), "masses table");
        vm.SelectedTableKind = 9;
        Check(vm.TableRows!.Count == 4 && vm.TableSummary.Contains("nessun carico"), "temperatures table");
        vm.SelectedTableKind = 10;
        Check(vm.TableRows!.Count == 2 && (string)vm.TableRows[0]["Fase"] == "1: Impalcato" && (int)vm.TableRows[0]["Elementi attivi"] == 3, "stages table");
        Check(vm.TableCsv().Split('\n')[0].Contains("\"Fase\""), "the new tables export as CSV");
    }

    /// <summary>A two-node beam with one stage and one staged nonlinear state ("Stage 1: Getto", increment "1: Peso") on the stage output case.</summary>
    static GPC.Model.Models.Model StagedSample(out string dataset)
    {
        dataset = "synthetic-nla";
        var batch = new ImportBatch { Program = "Straus7", SolverVersion = "3.1.5", ModelRevision = "staged", AnalysisId = "NLA synthetic", SourceHash = "SYNTHETICSTAGED" };
        batch.Nodes.Add(new NodeRecord { Id = "1", GlobalPosition = new Point3d(0, 0, 0), Record = "Node/1" });
        batch.Nodes.Add(new NodeRecord { Id = "2", GlobalPosition = new Point3d(4000, 0, 0), Record = "Node/2" });
        var axes = new CoordinateSystem(new Point3d(0, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1), new Vector3d(1, 0, 0));
        batch.Beams.Add(new BeamRecord { Id = "1", I = "1", J = "2", CoordinateSystem = axes, Formulation = BeamFormulation.StraightTwoNode, Record = "Beam/1" });
        batch.NodeRestrains.Add(new NodeRestrainRecord { NodeId = "1", FixedDofs = [true, true, true, true, true, true], CoordinateSystem = CoordinateSystem.Global, Record = "Restraint/1" });
        batch.LoadCases.Add(new LoadCaseRecord { Name = "Case 1: Peso", Record = "LoadCase/1" });
        batch.LoadCases.Add(new LoadCaseRecord { Name = Straus7StageResults.NonlinearCase, StageSourceCaseName = Straus7StageResults.NonlinearSourceCase, Record = "St7GetNumStages" });
        batch.Stages.Add(new Stage(1, "Stage 1: Getto") { Definition = new StageDefinition(1, 0) });
        var data = new Straus7ModelData(); data.Stages.Add(new() { Number = 1, Name = "Getto", ModelName = "Stage 1: Getto", EnabledGroupIds = [1], EnabledGroups = ["Group 1: Model"], ActiveBeams = [1], ActivePlates = [] });
        using (var stream = new MemoryStream())
        {
            new DataContractJsonSerializer(typeof(Straus7ModelData)).WriteObject(stream, data);
            batch.Uninterpreted.Add(new PreservedAssignment { Kind = Straus7ModelData.Kind, SourceRecord = Straus7ModelData.Record, RawData = Encoding.UTF8.GetString(stream.ToArray()) });
        }
        var model = ModelMapper.Map(batch).Model ?? throw new InvalidDataException("Synthetic staged model rejected.");
        static ResultState State(int count) => new() { Semantics = AnalysisSemantics.NonlinearStatic, Phase = "Stage 1: Getto", Step = "1: Peso", ConcomitantStateId = "synthetic NLA 1",
            IsCombined = false, IsCumulative = true, Components = Enumerable.Repeat(ComponentAvailability.Available, count).ToArray() };
        var results = new ResultImportBatch { Source = model.AnalysisSource, DatasetId = dataset, ExpectedInputFingerprint = model.AnalysisFingerprint(), SourceHash = "SYNTHETICNLA",
            ReaderVersion = "synthetic", Units = new ResultUnits(1, 1, 1), ShellDenominatorLengthToMm = 1, ResolvedConvention = "GPC components", IsSynthetic = true, Semantics = AnalysisSemantics.NonlinearStatic };
        var beam = model.BeamElements.Values.Single();
        foreach (double xi in new[] { 0d, 1 })
            results.Beams.Add(new BeamForceRecord { ElementId = "1", Case = Straus7StageResults.NonlinearCase, State = State(6), Axes = new CoordinateSystem(beam.StartPoint + (beam.EndPoint - beam.StartPoint) * xi, axes.V1, axes.V2, axes.V3),
                Values = [-1000, 0, 500, 0, 2e6 * (1 - xi), 0], Station = xi, PhysicalDistance = beam.Length * xi, Body = ActionBody.PositiveSectionFace, StationDomain = "NodeToNode", Record = "beam/" + xi });
        foreach (var node in model.NodesElements.Values)
        {
            var frame = new CoordinateSystem(node.Position, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1));
            results.NodeDisplacements.Add(new NodeDisplacementRecord { ElementId = node.Source.OriginalId, Case = Straus7StageResults.NonlinearCase, State = State(6), Axes = frame,
                Values = [0, 0, node.Source.OriginalId == "1" ? 0 : -5, 0, 0, 0], Record = "node/" + node.Source.OriginalId });
            if (node.Source.OriginalId == "1")
                results.NodeForces.Add(new NodeForceRecord { ElementId = "1", Case = Straus7StageResults.NonlinearCase, State = State(6), Axes = frame, Kind = NodalForceKind.SupportReaction,
                    Body = ActionBody.OnNode, Values = [0, 0, 500, 0, 2e6, 0], Record = "reaction/1" });
        }
        var imported = ResultMapper.Import(model, results);
        if (imported.Status != ImportStatus.Completed) throw new InvalidDataException("Synthetic staged results rejected: " + string.Join("; ", imported.Diagnostics.Select(d => d.Code + " " + d.Message)));
        return model;
    }
}
