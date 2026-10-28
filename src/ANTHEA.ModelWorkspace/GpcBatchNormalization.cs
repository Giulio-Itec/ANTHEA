using System.Globalization;
using GPC.Converter;
using GPC.Geometry;
using GPC.Model.Attributes;
using GPC.Model.Core.Diagnostics;

namespace ANTHEA.ModelWorkspace;

/// <summary>Display data of a GPC import batch: local frames of beams and plates, node frames, restraint, spring, link and release
/// symbols and the GPC diagnostics as import messages. The frames and assignments are those resolved by the GPC converter; nothing is
/// recomputed. Shared by the MIDAS text importer and tools/ModelViewer.Prepare (laboratory copy of the Civil NX API).</summary>
public static class GpcBatchNormalization
{
    static readonly string[] Dofs = ["DX", "DY", "DZ", "RX", "RY", "RZ"];

    /// <summary>Replaces LocalFrames, NodeFrames (only when the snapshot holds a SKEW table), Boundaries and ImportMessages.
    /// Records of elements or nodes that the snapshot does not contain are skipped. IDs of the batch are the snapshot IDs.</summary>
    public static void Apply(ModelSnapshot snapshot, ImportBatch batch, IEnumerable<ModelDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(batch); ArgumentNullException.ThrowIfNull(diagnostics);
        var elements = snapshot.Elements.Select(e => e.Id).ToHashSet(); var nodes = snapshot.Nodes.Select(n => n.Id).ToHashSet();
        static int Id(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        static ModelDirection Direction(Vector3d v) => new(v.X, v.Y, v.Z);
        // Plates: x, y, normal. Beams: the snapshot x is the member axis (GPC V3), y and z the section axes V1 and V2.
        snapshot.LocalFrames = batch.Shells.Where(s => s.CoordinateSystem != null && elements.Contains(Id(s.Id)))
            .Select(s => new ModelLocalFrame(Id(s.Id), Direction(s.CoordinateSystem.V1), Direction(s.CoordinateSystem.V2), Direction(s.CoordinateSystem.V3)))
            .Concat(batch.Beams.Where(b => b.CoordinateSystem != null && elements.Contains(Id(b.Id)))
                .Select(b => new ModelLocalFrame(Id(b.Id), Direction(b.CoordinateSystem.V3), Direction(b.CoordinateSystem.V1), Direction(b.CoordinateSystem.V2)))).ToArray();
        // An unresolved nodal orientation is not replaced by a global triad. Null NodeFrames means the source table was not acquired.
        if (snapshot.SourceTables?.TryGetValue("SKEW", out var skew) == true)
        {
            var skewed = skew.ValueKind == System.Text.Json.JsonValueKind.Object ? skew.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal) : [];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            snapshot.NodeFrames = batch.Nodes.Where(n => !(skewed.Contains(n.Id) && n.CoordinateSystem == null) && nodes.Contains(Id(n.Id)) && seen.Add(n.Id)).Select(n =>
            {
                var axes = n.CoordinateSystem ?? CoordinateSystem.Global;
                return new ModelNodeFrame(Id(n.Id), Direction(axes.V1), Direction(axes.V2), Direction(axes.V3));
            }).ToArray();
        }
        static string Flags(IEnumerable<bool> flags) => string.Join(", ", flags.Select((active, i) => active ? Dofs[i] : null).OfType<string>());
        var boundaries = batch.NodeRestrains.Where(r => nodes.Contains(Id(r.NodeId)))
            .Select(r => new ModelBoundary(r.Record, "RESTRAIN", [Id(r.NodeId)], null, Flags(r.FixedDofs))).ToList();
        // Linear springs to the ground share the support symbol; the DOFs with a stiffness are listed, values stay in the source.
        boundaries.AddRange(batch.NodeSprings.Where(s => s.Spring != null && nodes.Contains(Id(s.NodeId)))
            .Select(s => new ModelBoundary(s.Record, "RESTRAIN", [Id(s.NodeId)], null, "Molla elastica · " + Flags(Enumerable.Range(0, 6).Select(i => s.Spring[i, i] != 0)))));
        boundaries.AddRange(batch.NodeLinks.Where(r => nodes.Contains(Id(r.I)) && nodes.Contains(Id(r.J))).Select(r => new ModelBoundary(r.Record, r.RigidDofs == null ? "LINK" : "CONSTRAINT",
            [Id(r.I), Id(r.J)], null, r.RigidDofs == null ? "Matrice elastica" : Flags(r.RigidDofs))));
        var beams = batch.Beams.GroupBy(b => b.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var r in batch.BeamReleases)
        {
            if (!beams.TryGetValue(r.BeamId, out var beam) || !elements.Contains(Id(r.BeamId))) continue;
            foreach (var (node, connections, end) in new[] { (beam.I, r.Release.I, "I"), (beam.J, r.Release.J, "J") })
                if (connections.Any(c => c.Kind != BeamConnectionKind.Continuous))
                    boundaries.Add(new(r.Record + "/" + end, "RELEASE", [Id(node)], Id(r.BeamId), string.Join(", ", connections.Select((c, i) => Dofs[i] + ": " + c.Kind))));
        }
        snapshot.Boundaries = boundaries.ToArray();
        snapshot.ImportMessages = diagnostics.Select(d => new ModelImportMessage(d.Code, d.Severity.ToString(), d.Record, d.Message)).ToArray();
    }
}
