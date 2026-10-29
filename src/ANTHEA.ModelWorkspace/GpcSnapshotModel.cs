using System.Globalization;
using GPC.Converter;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Core.Coordinates;
using GPC.Model.Models;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Core.Units;
using GPC.Model.Results.State;
using GPC.Model.Structure.Assignments;

namespace ANTHEA.ModelWorkspace;

/// <summary>GPC model and static results of a document copy without MIDAS tables (Straus7 and other GPC sources), for the GPC
/// combination engine. The model is rebuilt only from the copy: node positions, the local frames of beams and plates and the static
/// cases. Values return to GPC units and signs, the inverse of <see cref="MidasGpcDisplay.DisplayVector"/>; no value is computed.</summary>
internal static class GpcSnapshotModel
{
    const double MillimetresPerMetre = 1000, NewtonsPerKilonewton = 1000, NewtonMillimetresPerKilonewtonMetre = 1e6;
    const string Program = "ANTHEA";

    internal static bool MidasTables(ModelSnapshot snapshot) => snapshot.SourceTables != null && new[] { "UNIT", "NODE", "ELEM", "STLD" }.All(snapshot.SourceTables.ContainsKey);
    /// <summary>MIDAS copies use their source tables; other sources need their declared cases and the local frames of the members.</summary>
    public static bool CanRebuild(ModelSnapshot snapshot) => MidasTables(snapshot) || snapshot.SourceTables == null && snapshot.SourceCombinations != null && snapshot.LocalFrames != null;

    internal static (GPC.Model.Models.Model Model, GPC.Model.Combinations.Combination Output, string[] Warnings) Build(ModelSnapshot snapshot, string revision,
        ModelResultSet[] cases, CancellationToken token)
    {
        static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);
        static Vector3d V(ModelDirection d) => new(d.X, d.Y, d.Z);
        var warnings = new List<string>();
        var points = snapshot.Nodes.ToDictionary(n => n.Id, n => new Point3d(n.X * MillimetresPerMetre, n.Y * MillimetresPerMetre, n.Z * MillimetresPerMetre));
        var frames = (snapshot.LocalFrames ?? []).ToDictionary(f => f.Element);
        var batch = new ImportBatch { Program = Program, SolverVersion = "copia del documento", ModelRevision = revision, AnalysisId = revision, SourceHash = revision };
        foreach (var node in snapshot.Nodes) batch.Nodes.Add(new NodeRecord { Id = Id(node.Id), GlobalPosition = points[node.Id], Record = "Nodo " + Id(node.Id) });
        var beamAxes = new Dictionary<int, CoordinateSystem>(); var plateAxes = new Dictionary<int, CoordinateSystem>();
        int withoutFrame = 0, axial = 0;
        foreach (var element in snapshot.Elements)
        {
            token.ThrowIfCancellationRequested();
            if (!frames.TryGetValue(element.Id, out var frame)) { withoutFrame++; continue; }
            if (element.Type is "BEAM" or "TRUSS" or "CABLE")
            {
                // Snapshot frame: x along the member (GPC V3), y and z the section axes V1 and V2. 1.16.0: trusses and cables (axial force only) too.
                var axes = new CoordinateSystem(points[element.Nodes[0]], V(frame.Y), V(frame.Z), V(frame.X)); beamAxes.Add(element.Id, axes);
                batch.Beams.Add(new BeamRecord { Id = Id(element.Id), I = Id(element.Nodes[0]), J = Id(element.Nodes[1]), CoordinateSystem = axes,
                    Formulation = element.Type == "TRUSS" ? BeamFormulation.Truss : element.Type == "CABLE" ? BeamFormulation.Cable : BeamFormulation.StraightTwoNode,
                    Record = "Asta " + Id(element.Id) });
            }
            else if (element.Type == "PLATE")
            {
                var corners = element.Nodes.Select(n => points[n]).ToArray();
                var origin = new Point3d(corners.Average(p => p.X), corners.Average(p => p.Y), corners.Average(p => p.Z));
                var axes = new CoordinateSystem(origin, V(frame.X), V(frame.Y), V(frame.Z)); plateAxes.Add(element.Id, axes);
                batch.Shells.Add(new ShellRecord { Id = Id(element.Id), Nodes = element.Nodes.Select(Id).ToArray(), CoordinateSystem = axes, Record = "Piastra " + Id(element.Id) });
            }
            else axial++;
        }
        if (withoutFrame > 0) warnings.Add($"{withoutFrame} elementi senza terna locale nella copia: esclusi dalla ricostruzione delle combinazioni.");
        if (axial > 0) warnings.Add($"{axial} aste a sola trazione o sola compressione: risposta non lineare, sforzi normali non ricostruiti nelle combinazioni.");
        foreach (var result in cases) batch.LoadCases.Add(new LoadCaseRecord { Name = result.Name, Record = "Caso " + result.Name });
        // Factor-free output declared before the results, as for the MIDAS copy; Rebuild receives the expanded factors.
        batch.Combinations.Add(new() { Id = "ANTHEA/runtime", Name = "ANTHEA runtime", Kind = CombinationKind.Envelope, Source = "ANTHEA", Status = "", Terms = [] });
        var mapped = ModelMapper.Map(batch, token);
        token.ThrowIfCancellationRequested();
        if (mapped.Model == null) throw new InvalidDataException("Modello GPC non disponibile: " + Messages(mapped.Diagnostics));
        var model = mapped.Model;
        var source = model.AnalysisSource;
        var results = new ResultImportBatch { Source = new AnalysisSource { Program = source.Program, SolverVersion = source.SolverVersion, ModelRevision = source.ModelRevision, AnalysisId = source.AnalysisId },
            DatasetId = "anthea-static", SourceHash = revision, ExpectedInputFingerprint = model.AnalysisFingerprint(), Units = new ResultUnits(1, 1, 1), ShellDenominatorLengthToMm = 1,
            ReaderVersion = "ANTHEA.ModelSnapshot/1", Semantics = AnalysisSemantics.LinearStatic,
            ResolvedConvention = "Document copy: plate Fxx..Vyy kN/m = N/mm, Mxx..Mxy kN·m/m with the sign of the MIDAS display (minus GPC); member kN and kN·m, My minus GPC M1; global displacements m and rad." };
        static ResultState State(string name, int count) => new() { Semantics = AnalysisSemantics.LinearStatic, ConcomitantStateId = "ANTHEA " + name, IsCombined = false, IsCumulative = true,
            Components = Enumerable.Repeat(ComponentAvailability.Available, count).ToArray(), Coverage = "Values of the ANTHEA document copy." };
        var elements = snapshot.Elements.ToDictionary(e => e.Id);
        string[] beamComponents = ResultFields.Components("BEAM", "LOCAL").ToArray();
        bool translationsOnly = false; int stationless = 0;
        foreach (var result in cases)
        {
            token.ThrowIfCancellationRequested();
            foreach (var corner in result.Values)
            {
                if (!plateAxes.TryGetValue(corner.Element, out var axes)) continue;
                var v = corner.Values; Vector3d d = points[corner.Node] - axes.Origin;
                results.Shells.Add(new ShellForceRecord { ElementId = Id(corner.Element), Case = result.Name, State = State(result.Name, 8), Axes = axes,
                    Values = [v[3], v[4], v[5], v[6], v[7], -v[0] * NewtonsPerKilonewton, -v[1] * NewtonsPerKilonewton, -v[2] * NewtonsPerKilonewton],
                    Location = new Point2d(Axes.Dot(d, axes.V1), Axes.Dot(d, axes.V2)), CoordinateKind = GPC.Model.Results.Locations.ResultCoordinateKind.LocalPhysical,
                    PointKind = GPC.Model.Results.Locations.ShellResultPointKind.ElementNodeExtrapolated, SourceNodeId = Id(corner.Node),
                    Record = "Piastra " + Id(corner.Element) + "/" + result.Name + "/nodo " + Id(corner.Node) });
            }
            var fields = beamComponents.Select(c => ResultFields.Read(result, "BEAM", "LOCAL", c).Where(v => beamAxes.ContainsKey(v.Element)).ToDictionary(v => (v.Element, v.Station), v => v.Value)).ToArray();
            foreach (var key in fields.SelectMany(f => f.Keys).Distinct())
            {
                if (fields.Any(f => !f.ContainsKey(key))) throw new InvalidDataException("Componenti beam incomplete nel caso " + result.Name + ".");
                if (key.Station is not double xi) { stationless++; continue; }
                var element = elements[key.Element]; Point3d i = points[element.Nodes[0]], j = points[element.Nodes[1]];
                Vector3d span = j - i; var axes = beamAxes[key.Element]; var at = i + span * xi;
                double[] v = fields.Select(f => f[key]).ToArray();
                results.Beams.Add(new BeamForceRecord { ElementId = Id(key.Element), Case = result.Name, State = State(result.Name, 6), Axes = new CoordinateSystem(at, axes.V1, axes.V2, axes.V3),
                    Values = [v[0] * NewtonsPerKilonewton, v[1] * NewtonsPerKilonewton, v[2] * NewtonsPerKilonewton, v[3] * NewtonMillimetresPerKilonewtonMetre, -v[4] * NewtonMillimetresPerKilonewtonMetre, v[5] * NewtonMillimetresPerKilonewtonMetre],
                    Station = xi, PhysicalDistance = Axes.Length(span) * xi, Body = ActionBody.PositiveSectionFace, StationDomain = "NodeToNode",
                    Record = "Asta " + Id(key.Element) + "/" + result.Name + "/" + xi.ToString("R", CultureInfo.InvariantCulture) });
            }
            // 1.16.0: trusses and cables carry the axial force only; shears, torque and moments are zero by formulation (pin-ended axial members).
            foreach (var type in new[] { "TRUSS", "CABLE" })
                foreach (var v in ResultFields.Read(result, type, "LOCAL", "Fx").Where(v => beamAxes.ContainsKey(v.Element)))
                {
                    if (v.Station is not double xi) { stationless++; continue; }
                    var element = elements[v.Element]; Point3d i = points[element.Nodes[0]], j = points[element.Nodes[1]];
                    Vector3d span = j - i; var axes = beamAxes[v.Element]; var at = i + span * xi;
                    results.Beams.Add(new BeamForceRecord { ElementId = Id(v.Element), Case = result.Name, State = State(result.Name, 6), Axes = new CoordinateSystem(at, axes.V1, axes.V2, axes.V3),
                        Values = [v.Value * NewtonsPerKilonewton, 0, 0, 0, 0, 0], Station = xi, PhysicalDistance = Axes.Length(span) * xi, Body = ActionBody.PositiveSectionFace,
                        StationDomain = "NodeToNode", Record = "Asta assiale " + Id(v.Element) + "/" + result.Name + "/" + xi.ToString("R", CultureInfo.InvariantCulture) });
                }
            // 1.16.0: support reactions (force of the support on the node, global axes), combined like the other nodal results.
            foreach (var reaction in result.Reactions ?? [])
            {
                var p = points[reaction.Node];
                results.NodeForces.Add(new NodeForceRecord { ElementId = Id(reaction.Node), Case = result.Name, State = State(result.Name, 6),
                    Axes = new CoordinateSystem(p, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1)),
                    Values = [reaction.Fx * NewtonsPerKilonewton, reaction.Fy * NewtonsPerKilonewton, reaction.Fz * NewtonsPerKilonewton,
                        reaction.Mx * NewtonMillimetresPerKilonewtonMetre, reaction.My * NewtonMillimetresPerKilonewtonMetre, reaction.Mz * NewtonMillimetresPerKilonewtonMetre],
                    Kind = NodalForceKind.SupportReaction, Body = ActionBody.OnNode, Record = "Reazione " + Id(reaction.Node) + "/" + result.Name });
            }
            foreach (var displacement in result.Displacements ?? [])
            {
                // The complete six-component state is required: zero rotations are never manufactured.
                if (displacement.Rotation is not { } r) { translationsOnly = true; continue; }
                var p = points[displacement.Node];
                results.NodeDisplacements.Add(new NodeDisplacementRecord { ElementId = Id(displacement.Node), Case = result.Name, State = State(result.Name, 6),
                    Axes = new CoordinateSystem(p, new Vector3d(1, 0, 0), new Vector3d(0, 1, 0), new Vector3d(0, 0, 1)),
                    Values = [displacement.X * MillimetresPerMetre, displacement.Y * MillimetresPerMetre, displacement.Z * MillimetresPerMetre, r.X, r.Y, r.Z],
                    Record = "Nodo " + Id(displacement.Node) + "/" + result.Name });
            }
        }
        if (stationless > 0) warnings.Add($"{stationless} valori di asta senza stazione: esclusi dalla ricostruzione.");
        if (translationsOnly) warnings.Add("Spostamenti: la copia contiene nodi con sole traslazioni; le deformate delle combinazioni richiedono anche le rotazioni. Nessuna rotazione è stata inventata.");
        if (results.Shells.Count + results.Beams.Count + results.NodeDisplacements.Count + results.NodeForces.Count == 0) throw new InvalidDataException("Nessuna sollecitazione plate o beam disponibile nei casi richiesti.");
        var imported = ResultMapper.Import(model, results, token);
        token.ThrowIfCancellationRequested();
        if (imported.Status == ImportStatus.Rejected) throw new InvalidDataException("Casi base non compatibili: " + Messages(imported.Diagnostics));
        warnings.AddRange(mapped.Diagnostics.Concat(imported.Diagnostics).Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Code + ": " + d.Message));
        return (model, model.Combinations.Values.Single(), warnings.ToArray());
    }

    static string Messages(IEnumerable<ModelDiagnostic> diagnostics) => string.Join("; ", diagnostics.Where(d => d.Severity != DiagnosticSeverity.Information).Take(5).Select(d => d.Code + ": " + d.Message));
}
