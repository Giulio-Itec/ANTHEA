using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

public enum ModelUpdateScope { Geometry, ModelWithoutResults, Results, Everything }
public sealed record ModelUpdateInfo(ModelUpdateScope Scope,string InputSource,bool ResultsCompatibilityVerified,int DiscardedResultCases);

/// <summary>Composes imported document copies; never recalculates or remaps analysis results.</summary>
public static class ModelSnapshotUpdate
{
    public static ModelSnapshot Apply(ModelSnapshot? current,ModelSnapshot incoming,ModelUpdateScope scope,CancellationToken token=default)
    {
        if(!Enum.IsDefined(scope))throw new InvalidDataException("Modalità di aggiornamento non valida.");
        token.ThrowIfCancellationRequested();incoming.Validate();
        if(scope==ModelUpdateScope.Everything)
        {
            if(current?.CustomCombinations==null&&current?.ExcludedCombinationIds==null)return incoming;
            var copy=ModelCombinations.Copy(incoming);
            copy.CustomCombinations=(current?.CustomCombinations??[]).Concat(incoming.CustomCombinations??[]).GroupBy(d=>d.Id).Select(g=>g.Last()).ToArray();
            copy.ExcludedCombinationIds=(current?.ExcludedCombinationIds??[]).Concat(incoming.ExcludedCombinationIds??[]).Distinct().ToArray();
            copy.Results=copy.Results.Where(r=>r.Combination==null).ToArray();copy.Validate();return copy;
        }
        if(scope==ModelUpdateScope.Results)
        {
            if(current==null)throw new InvalidDataException("Apri prima il modello al quale associare i risultati.");
            current.Validate();
            if(incoming.Results.Length==0)throw new InvalidDataException("La copia scelta non contiene risultati. Il modello aperto è stato conservato.");
            EnsureCompatible(current,incoming,token);
        }
        var basis=scope==ModelUpdateScope.Results?current!:incoming;
        bool geometryOnly=scope==ModelUpdateScope.Geometry;
        var result=new ModelSnapshot
        {
            Version=basis.Version,Name=basis.Name,Source=basis.Source,
            Nodes=basis.Nodes.ToArray(),Elements=basis.Elements.ToArray(),Plates=basis.Plates.ToArray(),Sections=basis.Sections.ToArray(),
            LocalFrames=basis.LocalFrames?.ToArray(),NodeFrames=basis.NodeFrames?.ToArray(),
            Boundaries=geometryOnly?null:basis.Boundaries?.ToArray(),
            SourceTables=geometryOnly?null:basis.SourceTables?.ToDictionary(p=>p.Key,p=>p.Value),
            SourceCombinations=geometryOnly?null:basis.SourceCombinations,
            Results=scope==ModelUpdateScope.Results?incoming.Results.Where(r=>r.Combination==null).ToArray():[],
            CustomCombinations=(current?.CustomCombinations??incoming.CustomCombinations)?.ToArray(),
            ExcludedCombinationIds=(current?.ExcludedCombinationIds??incoming.ExcludedCombinationIds)?.ToArray(),
            ImportMessages=(basis.ImportMessages??[]).Where(m=>m.Code!="UPDATE-SCOPE").Append(new("UPDATE-SCOPE","info","Importazione",Description(scope))).ToArray(),
            UpdateInfo=new(scope,incoming.Source,scope==ModelUpdateScope.Results,scope==ModelUpdateScope.Results?0:current?.Results.Length??0)
        };
        token.ThrowIfCancellationRequested();result.Validate();return result;
    }

    public static string Description(ModelUpdateScope scope)=>scope switch
    {
        ModelUpdateScope.Geometry=>"Solo geometria: nodi, elementi, sezioni, spessori, offset e terne geometriche. Materiali, gruppi, vincoli, carichi e risultati esclusi. Le vecchie sollecitazioni non vengono conservate.",
        ModelUpdateScope.ModelWithoutResults=>"Geometria, attributi e carichi della copia scelta. Le vecchie sollecitazioni vengono rimosse; nessun risultato viene importato.",
        ModelUpdateScope.Results=>"Solo risultati: sostituisce l’intero insieme dei casi, incluse le traslazioni nodali. Geometria, attributi e tabelle devono coincidere con la copia aperta. Nessuna rimappatura per vicinanza o numero rinumerato.",
        _=>"Tutto: sostituisce la copia del modello con geometria, attributi, carichi e risultati acquisiti dalla sorgente scelta."
    };

    static void EnsureCompatible(ModelSnapshot current,ModelSnapshot incoming,CancellationToken token)
    {
        void Require(bool condition,string part)
        {token.ThrowIfCancellationRequested();if(!condition)throw new InvalidDataException("Solo risultati non applicabile: "+part+" non coincidono. Usa Tutto per aggiornare il modello e controllarne la revisione. La copia aperta è stata conservata.");}
        Require(current.Nodes.OrderBy(n=>n.Id).SequenceEqual(incoming.Nodes.OrderBy(n=>n.Id)),"nodi e coordinate");
        Require(current.Elements.Length==incoming.Elements.Length,"elementi");
        Require(current.Elements.OrderBy(e=>e.Id).Zip(incoming.Elements.OrderBy(e=>e.Id)).All(p=>p.First.Id==p.Second.Id && p.First.Type==p.Second.Type && p.First.Property==p.Second.Property && p.First.Angle==p.Second.Angle && p.First.Nodes.SequenceEqual(p.Second.Nodes)),"connettività, tipi e assegnazioni");
        Require(current.Plates.OrderBy(p=>p.Id).SequenceEqual(incoming.Plates.OrderBy(p=>p.Id)) && current.Sections.OrderBy(s=>s.Id).SequenceEqual(incoming.Sections.OrderBy(s=>s.Id)),"sezioni, spessori e offset");
        Require((current.LocalFrames??[]).OrderBy(f=>f.Element).SequenceEqual((incoming.LocalFrames??[]).OrderBy(f=>f.Element)) && (current.NodeFrames??[]).OrderBy(f=>f.Node).SequenceEqual((incoming.NodeFrames??[]).OrderBy(f=>f.Node)),"terne locali");
        static object[] Boundaries(ModelSnapshot model)=>(model.Boundaries??[]).OrderBy(b=>b.SourceRecord,StringComparer.Ordinal).ThenBy(b=>b.Kind,StringComparer.Ordinal).ThenBy(b=>b.Element).ThenBy(b=>string.Join(",",b.Nodes),StringComparer.Ordinal).ThenBy(b=>b.Dofs,StringComparer.Ordinal).Cast<object>().ToArray();
        Require(Equal(JsonSerializer.SerializeToElement(Boundaries(current)),JsonSerializer.SerializeToElement(Boundaries(incoming))),"vincoli, rilasci e collegamenti");
        Require(Equal(JsonSerializer.SerializeToElement(current.SourceCombinations),JsonSerializer.SerializeToElement(incoming.SourceCombinations)),"casi e combinazioni della sorgente");
        var a=current.SourceTables??[];var b=incoming.SourceTables??[];
        Require(a.Count==b.Count,"tabelle disponibili");
        foreach(var table in a)
            Require(b.TryGetValue(table.Key,out var value)&&Equal(table.Value,value),"attributi e carichi della tabella "+table.Key);
    }
    // Object ordering and equivalent JSON numeric spellings do not constitute a model change.
    static bool Equal(JsonElement a,JsonElement b)
    {
        if(a.ValueKind!=b.ValueKind)return false;
        return a.ValueKind switch
        {
            JsonValueKind.Object=>a.EnumerateObject().Count()==b.EnumerateObject().Count()&&a.EnumerateObject().All(p=>b.TryGetProperty(p.Name,out var value)&&Equal(p.Value,value)),
            JsonValueKind.Array=>a.GetArrayLength()==b.GetArrayLength()&&a.EnumerateArray().Zip(b.EnumerateArray()).All(p=>Equal(p.First,p.Second)),
            JsonValueKind.Number=>a.TryGetDecimal(out var x)&&b.TryGetDecimal(out var y)?x==y:a.GetDouble()==b.GetDouble(),
            _=>a.ToString()==b.ToString()
        };
    }
}
