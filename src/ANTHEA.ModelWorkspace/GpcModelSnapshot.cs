using System.Globalization;
using GPC.Converter;
using GPC.Geometry;
using GPC.Model.Attributes;
using GPC.Model.Elements;
using GPC.Model.Results.Locations;
using GPC.Model.Results.State;
using GPC.Model.Sections;
using GPC.Model.Structure.Assignments;

namespace ANTHEA.ModelWorkspace;

/// <summary>Identity, names and thickness of a GPC element property as the source declares it.</summary>
public sealed record GpcPropertyIdentity(int Id, string Name, double? ThicknessMillimetres = null, string? Shape = null);

/// <summary>Choices of a source for <see cref="GpcModelSnapshot.Create"/>. Defaults are solver independent.</summary>
public sealed class GpcSnapshotOptions
{
    public string Name { get; init; } = "Modello importato";
    public string Source { get; init; } = "";
    /// <summary>Added to the source number of every beam, truss or cable, when beams and plates are numbered separately by the solver.</summary>
    public int BeamIdOffset { get; init; }
    /// <summary>GPC load case name to document case name (also used in the combination terms).</summary>
    public Func<string, string> CaseName { get; init; } = name => name;
    public Func<BeamElement, GpcPropertyIdentity?>? BeamProperty { get; init; }
    public Func<AreaElement, GpcPropertyIdentity?>? PlateProperty { get; init; }
    /// <summary>Results dataset to copy; null for a model without results.</summary>
    public string? DatasetId { get; init; }
    public bool Forces { get; init; } = true;
    public bool Displacements { get; init; } = true;
    /// <summary>Source label of the combination definitions (document identities "label/id").</summary>
    public string CombinationSource { get; init; } = "GPC";
    public IReadOnlyList<ModelImportMessage> Messages { get; init; } = [];
    /// <summary>1.16.0: SHA-256 (64 hex digits) of the results source, required by stage results (<see cref="ModelStageResult.SourceRevision"/>).</summary>
    public string? SourceRevision { get; init; }
    /// <summary>1.16.0: GPC stage name (state Phase) to document phase label.</summary>
    public Func<string, string> StagePhase { get; init; } = phase => phase;
    /// <summary>1.16.0: equilibrium of a static case checked by the source library, by GPC load case name; null when not checked.</summary>
    public Func<string, ModelEquilibrium?>? Equilibrium { get; init; }
}

/// <summary>Presentation snapshot of a GPC model imported by a converter, with the results of one dataset. Solver independent.
/// Units: GPC N, mm and rad become m, kN/m, kN·m/m (plates), kN, kN·m (members). Signs: the display convention of the MIDAS copy
/// (<see cref="MidasGpcDisplay.DisplayVector"/>), so that the same physical state reads the same in ANTHEA whatever the source:
/// plate Mxx, Myy, Mxy are minus the GPC moments (GPC: positive with tension on the +z face), member My is minus GPC M1.
/// No structural quantity is computed: values are copied, converted and sign-mapped.</summary>
public static class GpcModelSnapshot
{
    const double MillimetresPerMetre = 1000;
    // 1.16.0: reactions from GPC N and N·mm to kN and kN·m; reported values below this ratio of the largest reaction are solver round-off.
    const double NewtonsPerKilonewton = 1000, NewtonMillimetresPerKilonewtonMetre = 1e6, RoundOffRatio = 1e-6;
    public const int VariantPropertyOffset = 1000000;
    static readonly IReadOnlyList<string> Dofs = ["DX", "DY", "DZ", "RX", "RY", "RZ"];

    public static ModelSnapshot Create(GPC.Model.Models.Model model, GpcSnapshotOptions options, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(options);
        var messages = options.Messages.ToList();
        static int? Number(Element e) => e.Source?.OriginalId is { } id && int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : null;
        var gpcNodes = model.NodesElements.Values.OrderBy(n => n.Id).ToArray();
        bool sourceNodeIds = gpcNodes.All(n => Number(n).HasValue) && gpcNodes.Select(Number).Distinct().Count() == gpcNodes.Length;
        var nodeIds = gpcNodes.ToDictionary(n => n.Id, n => sourceNodeIds ? Number(n)!.Value : n.Id);
        var nodes = gpcNodes.Select(n => new ModelNode(nodeIds[n.Id], n.Position.X / MillimetresPerMetre, n.Position.Y / MillimetresPerMetre, n.Position.Z / MillimetresPerMetre)).ToArray();
        token.ThrowIfCancellationRequested();

        var elements = new List<ModelElement>(); var frames = new List<ModelLocalFrame>(); var owners = new Dictionary<Element, ModelElement>(ReferenceEqualityComparer.Instance);
        var sections = new Dictionary<int, SectionProperty>(); var plates = new Dictionary<(int, double, double), PlateProperty>();
        var defaultProperties = new Dictionary<object, int>(ReferenceEqualityComparer.Instance); var skipped = new Dictionary<string, int>(StringComparer.Ordinal);
        int DefaultProperty(object? property) => property == null ? 0 : defaultProperties.TryGetValue(property, out int id) ? id : defaultProperties[property] = defaultProperties.Count + 1;
        static ModelDirection Direction(Vector3d v) => new(v.X, v.Y, v.Z);
        var used = new HashSet<int>();
        int ElementId(Element e, int offset)
        {
            int id = (Number(e) ?? e.Id) + offset;
            if (!used.Add(id)) throw new InvalidDataException("Numerazione degli elementi della sorgente ambigua: " + id + ".");
            return id;
        }
        foreach (var beam in model.BeamElements.Values.OrderBy(b => b.Id))
        {
            token.ThrowIfCancellationRequested();
            string? type = beam.Assignments.Formulation switch
            {
                BeamFormulation.Unknown or BeamFormulation.StraightTwoNode => "BEAM", BeamFormulation.Truss => "TRUSS",
                BeamFormulation.Cable => "CABLE", BeamFormulation.TensionOnly => "TENSTR", _ => null
            };
            if (type == null || beam.NodeI == null || beam.NodeJ == null) { skipped[beam.Assignments.Formulation.ToString()] = skipped.GetValueOrDefault(beam.Assignments.Formulation.ToString()) + 1; continue; }
            var identity = options.BeamProperty?.Invoke(beam) ?? new GpcPropertyIdentity(DefaultProperty(beam.BeamProperty), beam.BeamProperty?.Name ?? "Senza proprietà");
            var element = new ModelElement(ElementId(beam, options.BeamIdOffset), type, identity.Id, [nodeIds[beam.NodeI.Id], nodeIds[beam.NodeJ.Id]]);
            elements.Add(element); owners.Add(beam, element);
            var axes = beam.Assignments.SectionAxes ?? beam.CoordinateSystem;
            if (axes != null) frames.Add(new(element.Id, Direction(axes.V3), Direction(axes.V1), Direction(axes.V2)));
            var shape = beam.BeamProperty switch { GPC.Model.Sections.Steel.SteelSection steel => steel.SectionShape, GPC.Model.Sections.Concrete.ReinforcedConcreteSection concrete => concrete.SectionShape, _ => null };
            var offset = beam.Assignments.SectionCentroidOffset;
            bool centred = (offset == null || Math.Abs(offset.X) < 1e-9 && Math.Abs(offset.Y) < 1e-9) && beam.Assignments.OffsetI == null && beam.Assignments.OffsetJ == null;
            var section = shape is SectionRectangular rectangle
                ? new SectionProperty(identity.Id, identity.Name, "SB", rectangle.Width / MillimetresPerMetre, rectangle.Height / MillimetresPerMetre, centred)
                : new SectionProperty(identity.Id, identity.Name, identity.Shape ?? (shape == null ? "" : ShapeLabel(shape)), 0, 0, false);
            if (!sections.TryGetValue(identity.Id, out var known)) sections.Add(identity.Id, section);
            else if (known.Shape == "SB" && known.Centered && !section.Centered) sections[identity.Id] = known with { Centered = false };
        }
        int dropped = 0, variantCount = 0; var droppedProperties = new SortedSet<int>();
        foreach (var shell in model.AreaElements.Values.OrderBy(s => s.Id))
        {
            token.ThrowIfCancellationRequested();
            var identity = options.PlateProperty?.Invoke(shell) ?? new GpcPropertyIdentity(DefaultProperty(shell.PlateProperty), shell.PlateProperty?.Name ?? "Senza proprietà");
            double? thickness = identity.ThicknessMillimetres ?? shell.Assignments.PhysicalThickness;
            if (shell.Nodes.Count is not (3 or 4) || thickness is not > 0) { dropped++; droppedProperties.Add(identity.Id); continue; }
            double offset = shell.Assignments.Offset ?? 0;
            var key = (identity.Id, thickness.Value, offset);
            if (!plates.TryGetValue(key, out var plate))
            {
                // A property used with another thickness or offset (Straus7 plate offsets are element attributes) gets its own document property.
                bool first = !plates.Keys.Any(k => k.Item1 == identity.Id);
                int id = first ? identity.Id : VariantPropertyOffset + ++variantCount;
                plate = new PlateProperty(id, first ? identity.Name : identity.Name + " · offset " + (offset / MillimetresPerMetre).ToString("G4", CultureInfo.InvariantCulture) + " m",
                    thickness.Value / MillimetresPerMetre, offset / MillimetresPerMetre);
                plates.Add(key, plate);
            }
            var element = new ModelElement(ElementId(shell, 0), "PLATE", plate.Id, shell.Nodes.Select(n => nodeIds[n.Id]).ToArray());
            elements.Add(element); owners.Add(shell, element);
            if (shell.CoordinateSystem is { } axes) frames.Add(new(element.Id, Direction(axes.V1), Direction(axes.V2), Direction(axes.V3)));
        }
        if (skipped.Count > 0)
            messages.Add(new("GPC-FORMULATION-SKIPPED", "Warning", "Aste", "Aste di formulazione non rappresentabile nella vista: " + string.Join(", ", skipped.Select(p => p.Key + " × " + p.Value)) + "."));
        if (dropped > 0)
            messages.Add(new("GPC-PLATE-THICKNESS-MISSING", "Warning", "Piastre", $"{dropped:N0} piastre escluse dalla vista: le proprietà {string.Join(", ", droppedProperties)} non hanno uno spessore fisico unico. Restano nel file sorgente."));

        var snapshot = new ModelSnapshot
        {
            Name = options.Name, Source = options.Source, Nodes = nodes, Elements = elements.ToArray(),
            Plates = plates.Values.OrderBy(p => p.Id).ToArray(), Sections = sections.Values.OrderBy(s => s.Id).ToArray(),
            LocalFrames = frames.ToArray(), NodeFrames = NodeFrames(gpcNodes, nodeIds), Boundaries = Boundaries(model, nodeIds, owners).ToArray()
        };
        token.ThrowIfCancellationRequested();
        snapshot.SourceCombinations = Combinations(model, options);
        if (options.DatasetId != null)
        {
            var supports = snapshot.Boundaries.Where(b => b.Kind == "RESTRAIN").SelectMany(b => b.Nodes).ToHashSet();
            snapshot.Results = Results(model, options, nodeIds, owners, supports, token);
            // Stage results need the version 2 of the format (older versions would read them as static cases).
            if (snapshot.Results.Any(r => r.Stage != null)) snapshot.Version = 2;
        }
        snapshot.ImportMessages = messages.ToArray();
        return snapshot;
    }

    static string ShapeLabel(ISectionShape shape)
    {
        string name = shape.GetType().Name;
        return name.StartsWith("Section", StringComparison.Ordinal) ? name["Section".Length..] : name;
    }

    // Orientation declared by the source for the node, otherwise that of its supports and springs; global elsewhere, never inferred from members.
    static ModelNodeFrame[] NodeFrames(NodeElement[] nodes, IReadOnlyDictionary<int, int> ids) => nodes.Select(n =>
    {
        var axes = n.CoordinateSystem ?? n.Assignments.Restrains.Select(r => r.Restrain?.CoordinateSystem).FirstOrDefault(c => c != null)
            ?? n.Assignments.GroundSprings.Select(s => s.CoordinateSystem).FirstOrDefault(c => c != null) ?? CoordinateSystem.Global;
        return new ModelNodeFrame(ids[n.Id], new(axes.V1.X, axes.V1.Y, axes.V1.Z), new(axes.V2.X, axes.V2.Y, axes.V2.Z), new(axes.V3.X, axes.V3.Y, axes.V3.Z));
    }).ToArray();

    static IEnumerable<ModelBoundary> Boundaries(GPC.Model.Models.Model model, IReadOnlyDictionary<int, int> ids, IReadOnlyDictionary<Element, ModelElement> owners)
    {
        static string Flags(IEnumerable<bool> flags) => string.Join(", ", flags.Select((active, i) => active ? Dofs[i] : null).OfType<string>());
        foreach (var node in model.NodesElements.Values.OrderBy(n => n.Id))
        {
            int id = ids[node.Id];
            foreach (var restraint in node.Assignments.Restrains.Where(r => r.Case == null && r.Phase == null && r.Restrain != null))
            {
                var fixedDofs = restraint.Restrain.Restrains.Where(d => d.IsRestrained && (int)d.Dof < Dofs.Count).Select(d => (int)d.Dof).ToHashSet();
                if (fixedDofs.Count > 0) yield return new(restraint.SourceRecord ?? "Vincolo " + id, "RESTRAIN", [id], null, Flags(Enumerable.Range(0, Dofs.Count).Select(fixedDofs.Contains)));
            }
            foreach (var spring in node.Assignments.GroundSprings)
                yield return new(spring.SourceRecord ?? "Molla " + id, "RESTRAIN", [id], null, "Molla elastica · " + Flags(Enumerable.Range(0, Dofs.Count).Select(i => spring[i, i] != 0)));
            foreach (var link in node.Assignments.Links)
            {
                if (!ids.TryGetValue(link.OtherNodeId, out int other)) continue;
                if (link.KinematicCoefficients != null)
                    yield return new(link.SourceRecord ?? "Collegamento " + id, "CONSTRAINT", [id, other], null, "Collegamento rigido · " + link.RightHandSide?.Length.ToString(CultureInfo.InvariantCulture) + " equazioni");
                else if (link.Spring != null) yield return new(link.SourceRecord ?? "Collegamento " + id, "LINK", [id, other], null, "Matrice elastica");
            }
        }
        foreach (var beam in model.BeamElements.Values.OrderBy(b => b.Id))
        {
            if (!owners.TryGetValue(beam, out var element)) continue;
            foreach (var release in beam.Attributes.Values.OfType<BeamReleasesAttribute>())
                foreach (var (node, connections, end) in new[] { (element.Nodes[0], release.I, "I"), (element.Nodes[1], release.J, "J") })
                    if (connections.Any(c => c.Kind != BeamConnectionKind.Continuous))
                        yield return new((release.SourceRecord ?? "Rilascio " + element.Id) + "/" + end, "RELEASE", [node], element.Id, string.Join(", ", connections.Select((c, i) => Dofs[i] + ": " + c.Kind)));
        }
    }

    static ModelSourceCombinations? Combinations(GPC.Model.Models.Model model, GpcSnapshotOptions options)
    {
        var cases = model.LoadCases.Values.Where(c => c is not GPC.Model.Stages.StageResultCase).Select(c => options.CaseName(c.Name)).ToArray();
        var records = CombinationExpansion.Definitions(model);
        if (cases.Length == 0 && records.Count == 0) return null;
        string Id(string id) => options.CombinationSource + "/" + id;
        // Terms of static cases are tagged "ST", the label ANTHEA uses for static cases of every source; nested terms keep their reference.
        var definitions = records.Select(d => new ModelCombinationDefinition(Id(d.Id), d.Name, (ModelCombinationKind)d.Kind, options.CombinationSource, d.Status ?? "",
            d.Terms.Select(t => t.IsCombination ? new ModelCombinationTerm(Id(t.Name), true, t.Factor, t.Analysis ?? "CB")
                : new ModelCombinationTerm(options.CaseName(t.Name), false, t.Factor, t.IsStaticCase ? "ST" : t.Analysis ?? "")).ToArray())).ToArray();
        return new(cases, definitions);
    }

    static ModelResultSet[] Results(GPC.Model.Models.Model model, GpcSnapshotOptions options, IReadOnlyDictionary<int, int> nodeIds, IReadOnlyDictionary<Element, ModelElement> owners,
        IReadOnlySet<int> supports, CancellationToken token)
    {
        var corners = new Dictionary<string, List<CornerResult>>(StringComparer.Ordinal);
        var fields = new Dictionary<(string Case, string Family, string Component), List<ResultValue>>();
        var displacements = new Dictionary<string, List<ModelDisplacement>>(StringComparer.Ordinal);
        var reactions = new Dictionary<string, List<ModelReaction>>(StringComparer.Ordinal);
        var order = new List<string>(); var stages = new Dictionary<string, ModelStageResult>(StringComparer.Ordinal); var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        bool Mine(ResultLocation s) => s.State?.DatasetId == options.DatasetId && s.Case != null;
        string Case(ResultLocation s)
        {
            string name;
            // 1.16.0: a state of a stage output case (staged nonlinear analysis) is a stage result: SourceCase · phase · step, never a static case.
            if (s.Case is GPC.Model.Stages.StageResultCase stage)
            {
                if (options.SourceRevision is not { Length: 64 } revision) throw new InvalidDataException("Risultati per fase senza l’impronta della sorgente.");
                var context = new ModelStageResult(stage.SourceCaseName, options.StagePhase(s.State.Phase ?? ""), s.State.Step ?? "", options.Displacements ? "Accumulative" : null, revision);
                name = context.Label; stages.TryAdd(name, context);
            }
            else { name = options.CaseName(s.Case.Name); sources.TryAdd(name, s.Case.Name); }
            if (!order.Contains(name)) order.Add(name);
            return name;
        }
        string[] beamComponents = ResultFields.Components("BEAM", "LOCAL").ToArray();
        foreach (var (owner, element) in owners)
        {
            token.ThrowIfCancellationRequested();
            if (!options.Forces) break;
            foreach (var sample in owner.Results.SelectMany(r => r.Results).Where(Mine))
            {
                if (sample is PointResultPlateForces plate && element.Type == "PLATE" && plate.PointKind == ShellResultPointKind.ElementNodeExtrapolated && plate.SourceNodeId is int node)
                {
                    string name = Case(sample);
                    (corners.TryGetValue(name, out var list) ? list : corners[name] = []).Add(new(element.Id, nodeIds[node], MidasGpcDisplay.DisplayVector(sample)));
                }
                else if (sample is StationResultBeamForces station && element.Type != "PLATE")
                {
                    string name = Case(sample); var vector = MidasGpcDisplay.DisplayVector(sample); double xi = station.ParametricDistance;
                    int at = xi == 0 ? element.Nodes[0] : xi == 1 ? element.Nodes[1] : 0;
                    var components = ResultFields.Components(element.Type, "LOCAL");
                    for (int c = 0; c < components.Count; c++)
                    {
                        var key = (name, element.Type, components[c]);
                        (fields.TryGetValue(key, out var list) ? list : fields[key] = []).Add(new(element.Id, at, vector[Array.IndexOf(beamComponents, components[c])], xi));
                    }
                }
            }
        }
        if (options.Displacements)
            foreach (var node in model.NodesElements.Values)
            {
                token.ThrowIfCancellationRequested();
                foreach (var sample in node.Results.SelectMany(r => r.Results).Where(Mine).OfType<NodeResultDisplacement>())
                {
                    var (d, r) = sample.ResultDisplacement.GetGlobalDisplacementsTuple(); string name = Case(sample);
                    (displacements.TryGetValue(name, out var list) ? list : displacements[name] = []).Add(new(nodeIds[node.Id], d.X / MillimetresPerMetre, d.Y / MillimetresPerMetre, d.Z / MillimetresPerMetre) { Rotation = new(r.X, r.Y, r.Z) });
                }
            }
        // 1.16.0: support reactions of the nodes with a restraint or a spring to ground, and of the nodes with a reported value that is not round-off
        // (above 1e-6 of the largest reaction of the dataset: supports the copy does not draw, e.g. restraints of other freedom cases), the same nodes in
        // every state: the force of the support on the node, rotated to global axes, kN and kN·m.
        var reported = model.NodesElements.Values.Select(n => (Node: n, Samples: n.Results.SelectMany(r => r.Results).Where(Mine).OfType<NodeResultForces>()
            .Where(s => s.Kind == NodalForceKind.SupportReaction).ToArray())).Where(p => p.Samples.Length > 0).ToArray();
        static double Force(NodeResultForces s) => Math.Max(Math.Abs(s.Fx), Math.Max(Math.Abs(s.Fy), Math.Abs(s.Fz)));
        static double Moment(NodeResultForces s) => Math.Max(Math.Abs(s.Mx), Math.Max(Math.Abs(s.My), Math.Abs(s.Mz)));
        double forceScale = reported.SelectMany(p => p.Samples).Select(Force).DefaultIfEmpty().Max(), momentScale = reported.SelectMany(p => p.Samples).Select(Moment).DefaultIfEmpty().Max();
        foreach (var (node, samples) in reported)
        {
            token.ThrowIfCancellationRequested();
            int id = nodeIds[node.Id];
            if (!supports.Contains(id) && samples.All(s => Force(s) <= RoundOffRatio * forceScale && Moment(s) <= RoundOffRatio * momentScale)) continue;
            foreach (var sample in samples)
            {
                var axes = sample.ResultBeamForces.CoordinateSystem;
                Vector3d f = axes.V1 * sample.Fx + axes.V2 * sample.Fy + axes.V3 * sample.Fz, m = axes.V1 * sample.Mx + axes.V2 * sample.My + axes.V3 * sample.Mz;
                string name = Case(sample);
                (reactions.TryGetValue(name, out var list) ? list : reactions[name] = []).Add(new(id, f.X / NewtonsPerKilonewton, f.Y / NewtonsPerKilonewton, f.Z / NewtonsPerKilonewton,
                    m.X / NewtonMillimetresPerKilonewtonMetre, m.Y / NewtonMillimetresPerKilonewtonMetre, m.Z / NewtonMillimetresPerKilonewtonMetre));
            }
        }
        return order.Select(name => new ModelResultSet(name, false, corners.GetValueOrDefault(name)?.ToArray() ?? [])
        {
            Fields = fields.Where(p => p.Key.Case == name).Select(p => new ResultField(p.Key.Family, "LOCAL", p.Key.Component, p.Value.ToArray())).ToArray() is { Length: > 0 } f ? f : null,
            Displacements = displacements.GetValueOrDefault(name)?.OrderBy(v => v.Node).ToArray(),
            Reactions = reactions.GetValueOrDefault(name)?.OrderBy(v => v.Node).ToArray(),
            Stage = stages.GetValueOrDefault(name),
            Equilibrium = sources.TryGetValue(name, out var source) ? options.Equilibrium?.Invoke(source) : null
        }).ToArray();
    }
}
