using ANTHEA.ModelWorkspace;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>Display-only, equal-weight corner smoothing. Never transforms axes or changes source results.</summary>
internal sealed class PlateContourAverager
{
    private sealed record Patch(ModelLocalFrame Frame, List<(int Element, int Node)> Corners);
    private readonly ModelElement[] plates;
    private readonly Patch[] patches;
    private readonly HashSet<int> framedElements;

    internal PlateContourAverager(ModelSnapshot snapshot, IReadOnlyDictionary<int, ElementDisplay> materials)
    {
        plates = snapshot.Elements.Where(e => e.Type == "PLATE").ToArray();
        var frames = (snapshot.LocalFrames ?? []).ToDictionary(f => f.Element);
        framedElements = frames.Keys.ToHashSet();
        var groups = new Dictionary<(int Node, int Property, string Material), List<Patch>>();
        foreach (var element in plates)
        {
            // Imported frames are authoritative. Guessing axes from the mesh could mix different components.
            if (!frames.TryGetValue(element.Id, out var frame)) continue;
            foreach (int node in element.Nodes)
            {
                var key = (node, element.Property, materials[element.Id].Key);
                if (!groups.TryGetValue(key, out var candidates)) groups[key] = candidates = [];
                var patch = candidates.FirstOrDefault(p => Aligned(p.Frame, frame));
                if (patch == null) candidates.Add(patch = new(frame, []));
                patch.Corners.Add((element.Id, node));
            }
        }
        patches = groups.Values.SelectMany(g => g).Where(p => p.Corners.Count > 1).ToArray();
    }

    internal AveragedContour Apply(IReadOnlyList<ResultValue> source)
    {
        var positions = source.Select((v, i) => (Key: (v.Element, v.Node), Index: i)).ToDictionary(v => v.Key, v => v.Index);
        // A plate missing any corner is rendered as missing data; it must not influence its neighbours.
        var complete = plates.Where(e => e.Nodes.All(n => positions.ContainsKey((e.Id, n)))).Select(e => e.Id).ToHashSet();
        var values = source.ToArray();
        var averagedNodes = new HashSet<int>();
        foreach (var patch in patches)
        {
            var indices = patch.Corners.Where(c => complete.Contains(c.Element)).Select(c => positions[c]).ToArray();
            if (indices.Length < 2) continue;
            double magnitude = indices.Max(i => Math.Abs(source[i].Value));
            double sum = 0, correction = 0;
            // Scaled compensated sum avoids overflow even for finite, near-limit values of opposite signs.
            if (magnitude > 0)
                foreach (int index in indices)
                {
                    double term = source[index].Value / magnitude - correction, next = sum + term;
                    correction = (next - sum) - term; sum = next;
                }
            double mean = Math.Clamp(sum / indices.Length, -1, 1) * magnitude;
            foreach (int index in indices) values[index] = source[index] with { Value = mean };
            averagedNodes.Add(patch.Corners[0].Node);
        }
        return new(values, averagedNodes.Count, complete.Count(e => !framedElements.Contains(e)));
    }

    private static bool Aligned(ModelLocalFrame a, ModelLocalFrame b) =>
        Close(a.X, b.X) && Close(a.Y, b.Y) && Close(a.Z, b.Z);
    private static bool Close(ModelDirection a, ModelDirection b) =>
        Math.Abs(a.X - b.X) <= 1e-6 && Math.Abs(a.Y - b.Y) <= 1e-6 && Math.Abs(a.Z - b.Z) <= 1e-6;
}

internal sealed record AveragedContour(IReadOnlyList<ResultValue> Values, int VertexCount, int UnframedElements);
