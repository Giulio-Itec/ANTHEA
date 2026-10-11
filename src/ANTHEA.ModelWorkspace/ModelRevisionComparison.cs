using System.Diagnostics;
using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

public sealed record ModelRevisionRow(string Category, string Id, string Change, string Detail, int[] Elements);
public sealed record ModelElementRevision(int Id, bool Added, bool Removed, bool Geometry, bool Attributes);
public sealed record ModelRevisionReview(ModelSnapshot Previous, ModelSnapshot Current,
    ModelElementRevision[] Elements, ModelRevisionRow[] Rows, double Milliseconds);
public sealed record ModelResultDeltaRow(int Element, int Node, double? Station, double Previous, double Current, double Delta, bool GeometryChanged);
public sealed record ModelResultDelta(ModelResultDeltaRow[] Rows, int UnmatchedPrevious, int UnmatchedCurrent, double Milliseconds);

/// <summary>Document revision comparison. Matches explicit IDs; never infers structural equivalence.</summary>
public static class ModelRevisionComparison
{
    public static ModelRevisionReview Compare(ModelSnapshot previous, ModelSnapshot current, double coordinateTolerance = 1e-6, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(coordinateTolerance) || coordinateTolerance < 0) throw new ArgumentException("Tolleranza non valida.");
        var watch = Stopwatch.StartNew();
        var before = previous.Elements.ToDictionary(e => e.Id); var after = current.Elements.ToDictionary(e => e.Id);
        var oldNodes = previous.Nodes.ToDictionary(n => n.Id); var newNodes = current.Nodes.ToDictionary(n => n.Id);
        var moved = oldNodes.Keys.Intersect(newNodes.Keys).Where(id =>
            Math.Abs(oldNodes[id].X-newNodes[id].X)>coordinateTolerance || Math.Abs(oldNodes[id].Y-newNodes[id].Y)>coordinateTolerance || Math.Abs(oldNodes[id].Z-newNodes[id].Z)>coordinateTolerance).ToHashSet();
        var rows = new List<ModelRevisionRow>(); var elementRows = new List<ModelRevisionRow>(); var changedAttributes = new HashSet<int>();
        var adjacency = current.Elements.SelectMany(e => e.Nodes.Select(n => (Node:n, Element:e.Id))).ToLookup(x=>x.Node,x=>x.Element);
        var oldProperties = previous.Plates.Select(p=>(Key:("PLATE",p.Id), Value:JsonSerializer.SerializeToElement(p)))
            .Concat(previous.Sections.Select(p=>(Key:("LINE",p.Id), Value:JsonSerializer.SerializeToElement(p)))).ToDictionary(p=>p.Key,p=>p.Value);
        var newProperties = current.Plates.Select(p=>(Key:("PLATE",p.Id), Value:JsonSerializer.SerializeToElement(p)))
            .Concat(current.Sections.Select(p=>(Key:("LINE",p.Id), Value:JsonSerializer.SerializeToElement(p)))).ToDictionary(p=>p.Key,p=>p.Value);
        var oldFrames = (previous.LocalFrames ?? []).ToDictionary(f => f.Element);
        var newFrames = (current.LocalFrames ?? []).ToDictionary(f => f.Element);
        foreach (int id in oldNodes.Keys.Union(newNodes.Keys))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? change = !oldNodes.ContainsKey(id) ? "Nuovo" : !newNodes.ContainsKey(id) ? "Cancellato" : moved.Contains(id) ? "Modificato" : null;
            if (change != null) rows.Add(new("Nodo", id.ToString(), change, "Coordinate / presenza", adjacency[id].ToArray()));
        }
        foreach (var pair in before.Where(p => after.ContainsKey(p.Key)))
        {
            var element = after[pair.Key];
            var property = (element.Type == "PLATE" ? "PLATE" : "LINE", element.Property);
            bool definition = !Equal(oldProperties.GetValueOrDefault(property),newProperties.GetValueOrDefault(property));
            if (element.Property != pair.Value.Property || definition || !Equal(Material(previous,pair.Key), Material(current,pair.Key)))
                changedAttributes.Add(pair.Key);
        }
        foreach (string table in (previous.SourceTables?.Keys.AsEnumerable() ?? []).Union(current.SourceTables?.Keys.AsEnumerable() ?? []).Where(t=>t is not ("NODE" or "ELEM")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool had = previous.SourceTables?.TryGetValue(table, out _) == true, has = current.SourceTables?.TryGetValue(table, out _) == true;
            if (!had || !has) { rows.Add(new(table, "—", "Disponibilità", has ? "Tabella acquisita solo nella revisione corrente" : "Tabella non acquisita nella revisione corrente; eliminazione non deducibile", [])); continue; }
            var oldRecords = previous.SourceTables![table].EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value);
            var newRecords = current.SourceTables![table].EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value);
            foreach (string id in oldRecords.Keys.Union(newRecords.Keys))
            {
                bool old = oldRecords.TryGetValue(id,out var a), now = newRecords.TryGetValue(id,out var b);
                if (old && now && Equal(a,b)) continue;
                int[] elements = Affected(current,table,id,now?b:a).Concat(Affected(previous,table,id,old?a:b)).Where(after.ContainsKey).Distinct().ToArray();
                changedAttributes.UnionWith(elements);
                rows.Add(new(table,id,!old?"Nuovo":!now?"Cancellato":"Modificato","Definizione / assegnazione",elements));
            }
        }
        var oldNodeFrames = (previous.NodeFrames ?? []).ToDictionary(f=>f.Node);
        var newNodeFrames = (current.NodeFrames ?? []).ToDictionary(f=>f.Node);
        foreach(int node in oldNodeFrames.Keys.Union(newNodeFrames.Keys))
            if(!Equal(JsonSerializer.SerializeToElement(oldNodeFrames.GetValueOrDefault(node)),JsonSerializer.SerializeToElement(newNodeFrames.GetValueOrDefault(node))))
            { changedAttributes.UnionWith(adjacency[node]); rows.Add(new("Terna nodale",node.ToString(),"Modificato","Orientamento / disponibilità",adjacency[node].ToArray())); }
        static Dictionary<string,ModelBoundary[]> BoundaryGroups(ModelSnapshot snapshot) => (snapshot.Boundaries ?? [])
            .GroupBy(b=>b.SourceRecord).ToDictionary(g=>g.Key,g=>g.OrderBy(b=>b.Kind).ThenBy(b=>b.Element).ThenBy(b=>string.Join(",",b.Nodes)).ThenBy(b=>b.Dofs).ToArray());
        var oldBoundaries = BoundaryGroups(previous);
        var newBoundaries = BoundaryGroups(current);
        foreach(string key in oldBoundaries.Keys.Union(newBoundaries.Keys))
        {
            var a = oldBoundaries.GetValueOrDefault(key); var b = newBoundaries.GetValueOrDefault(key);
            if(Equal(JsonSerializer.SerializeToElement(a),JsonSerializer.SerializeToElement(b)))continue;
            var affected = (a??[]).Concat(b??[]).SelectMany(boundary=>boundary.Nodes).Distinct().SelectMany(n=>adjacency[n]).Distinct().ToArray();
            changedAttributes.UnionWith(affected);
            rows.Add(new("Condizione al contorno",key,a==null?"Nuovo":b==null?"Cancellato":"Modificato","Assegnazione / disponibilità",affected));
        }
        var changes = new List<ModelElementRevision>();
        foreach (int id in before.Keys.Union(after.Keys))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool old = before.TryGetValue(id,out var a), now = after.TryGetValue(id,out var b);
            bool geometry = old && now && (a!.Type != b!.Type || !a.Nodes.SequenceEqual(b.Nodes) || Math.Abs(a.Angle-b.Angle)>1e-8 || b.Nodes.Any(moved.Contains) ||
                !Equal(JsonSerializer.SerializeToElement(oldFrames.GetValueOrDefault(id)),JsonSerializer.SerializeToElement(newFrames.GetValueOrDefault(id))));
            bool attributes = old && now && changedAttributes.Contains(id);
            if (old && now && !geometry && !attributes) continue;
            changes.Add(new(id,!old,!now,geometry,attributes));
            elementRows.Add(new("Elemento",id.ToString(),!old?"Nuovo":!now?"Cancellato":"Modificato",
                geometry && attributes?"Geometria e attributi":geometry?"Geometria":attributes?"Attributi":"Presenza",now?[id]:[]));
        }
        return new(previous,current,changes.ToArray(),elementRows.Concat(rows).ToArray(),watch.Elapsed.TotalMilliseconds);
    }
    static JsonElement Material(ModelSnapshot model,int id) =>
        model.SourceTables?.TryGetValue("ELEM",out var table)==true && table.TryGetProperty(id.ToString(),out var record) && record.TryGetProperty("MATL",out var material) ? material : default;
    static IEnumerable<int> Affected(ModelSnapshot model,string table,string key,JsonElement record)
    {
        if (!int.TryParse(key,out int id)) return [];
        var adjacency = model.Elements.Where(e=>e.Nodes.Contains(id)).Select(e=>e.Id);
        return table switch
        {
            "SECT" => model.Elements.Where(e=>ElementFamilies.IsLine(e.Type)&&e.Property==id).Select(e=>e.Id),
            "THIK" => model.Elements.Where(e=>e.Type=="PLATE"&&e.Property==id).Select(e=>e.Id),
            "MATL" => model.Elements.Where(e=>Material(model,e.Id).ValueKind==JsonValueKind.Number && Material(model,e.Id).GetInt32()==id).Select(e=>e.Id),
            "GRUP" when record.TryGetProperty("E_LIST",out var ids) => ids.EnumerateArray().Select(x=>x.GetInt32()).ToArray(),
            "CONS" or "SKEW" or "CNLD" or "NBOF" or "NSPR" => adjacency,
            "FRLS" or "PRLS" or "OFFS" or "BMLD" or "PRES" => model.Elements.Where(e=>e.Id==id).Select(e=>e.Id),
            "ELNK" when record.TryGetProperty("NODE",out var nodes) => model.Elements.Where(e=>e.Nodes.Intersect(nodes.EnumerateArray().Select(n=>n.GetInt32())).Any()).Select(e=>e.Id),
            _ => []
        };
    }
    static bool Equal(JsonElement a,JsonElement b)
    {
        if(a.ValueKind!=b.ValueKind)return false;
        return a.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => true,
            JsonValueKind.Number => Math.Abs(a.GetDouble()-b.GetDouble()) <= 1e-9*Math.Max(1,Math.Max(Math.Abs(a.GetDouble()),Math.Abs(b.GetDouble()))),
            JsonValueKind.Object => a.EnumerateObject().Count()==b.EnumerateObject().Count() && a.EnumerateObject().All(p=>b.TryGetProperty(p.Name,out var v)&&Equal(p.Value,v)),
            JsonValueKind.Array => a.GetArrayLength()==b.GetArrayLength() && a.EnumerateArray().Zip(b.EnumerateArray()).All(p=>Equal(p.First,p.Second)),
            _ => a.ToString()==b.ToString()
        };
    }
    public static ModelResultDelta CompareResults(ModelRevisionReview review,string caseName,string family,string axes,string component,CancellationToken cancellationToken=default)
    {
        var watch=Stopwatch.StartNew();
        var before=ResultFields.Read(review.Previous.Results.SingleOrDefault(r=>r.Name==caseName),family,axes,component).ToDictionary(v=>(v.Element,v.Node,v.Station));
        var after=ResultFields.Read(review.Current.Results.SingleOrDefault(r=>r.Name==caseName),family,axes,component).ToDictionary(v=>(v.Element,v.Node,v.Station));
        if(before.Count==0 || after.Count==0)throw new InvalidOperationException("Il campo richiesto non è disponibile in entrambe le revisioni.");
        var geometry=review.Elements.Where(e=>e.Geometry).Select(e=>e.Id).ToHashSet();
        var rows=new List<ModelResultDeltaRow>();
        foreach(var pair in after)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(before.TryGetValue(pair.Key,out var old))rows.Add(new(pair.Key.Element,pair.Key.Node,pair.Key.Station,old.Value,pair.Value.Value,pair.Value.Value-old.Value,geometry.Contains(pair.Key.Element)));
        }
        return new(rows.ToArray(),before.Count-rows.Count,after.Count-rows.Count,watch.Elapsed.TotalMilliseconds);
    }
}
