using System.Numerics;
using ANTHEA.ModelWorkspace;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    private HashSet<int>? activePlateBoundaries;
    private void UpdatePlateBoundaries(SceneOptions options)
    {
        var active=model!.Elements.Where(e=>e.Type=="PLATE" && !incompleteDeformations.Contains(e.Id) &&
            (options.Visible==null || options.Visible.Contains(e.Id)) && (!options.ShowResults || options.ShowContext || MatchesFamily(e,options))).Select(e=>e.Id).ToHashSet();
        if(activePlateBoundaries?.SetEquals(active)==true)return;
        activePlateBoundaries=active;
        plateSides.Clear();
        var edges = new Dictionary<(int, int, int), List<(int element, int edge, Vector3 normal)>>();
        foreach (var element in model!.Elements.Where(e => e.Type == "PLATE"))
        {
            plateSides[element.Id] = Enumerable.Repeat(true, element.Nodes.Length).ToArray();
            if(!active.Contains(element.Id))continue;
            var p = element.Nodes.Select(n => undeformedPoints[n]).ToArray(); var normal = Vector3.Normalize(Vector3.Cross(p[1] - p[0], p[2] - p[0]));
            for (int i = 0; i < p.Length; i++)
            {
                int a = element.Nodes[i], b = element.Nodes[(i + 1) % p.Length]; var key = (element.Property, Math.Min(a, b), Math.Max(a, b));
                if (!edges.TryGetValue(key, out var list)) edges[key] = list = new(); list.Add((element.Id, i, normal));
            }
        }
        // Remove only matching internal side faces of coplanar plates with the same physical property.
        // Different normals, properties and non-manifold junctions retain their full volume boundaries.
        foreach (var edge in edges.Values.Where(e => e.Count == 2))
            if (Vector3.Dot(edge[0].normal, edge[1].normal) > 0.999999f)
            { plateSides[edge[0].element][edge[0].edge] = false; plateSides[edge[1].element][edge[1].edge] = false; }
        // Suppress seam lines through thickness along straight, continuous external boundaries.
        plateOutlineCorners.Clear();
        var directions=new Dictionary<(int Property,int Node),List<Vector3>>();
        foreach(var element in model!.Elements.Where(e=>e.Type=="PLATE" && active.Contains(e.Id)))
            for(int i=0;i<element.Nodes.Length;i++)
            {
                if(!plateSides[element.Id][i])continue;
                int a=element.Nodes[i],b=element.Nodes[(i+1)%element.Nodes.Length];
                foreach(var pair in new[]{(a,b),(b,a)})
                {
                    var key=(element.Property,pair.Item1);
                    if(!directions.TryGetValue(key,out var list))directions[key]=list=[];
                    list.Add(Vector3.Normalize(undeformedPoints[pair.Item2]-undeformedPoints[pair.Item1]));
                }
            }
        foreach(var pair in directions)
            if(pair.Value.Count!=2 || Vector3.Dot(pair.Value[0],pair.Value[1])>-.999999f)plateOutlineCorners.Add(pair.Key);
    }
}
