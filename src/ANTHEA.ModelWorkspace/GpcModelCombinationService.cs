using static ANTHEA.ModelWorkspace.MidasGpcDisplay;
using System.Diagnostics;
using System.Globalization;
using GPC.Converter;
using GPC.Converter.CivilNx;
using GPC.Model.Analysis;
using GPC.Model.Elements;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Processing;
using GPC.Model.Core.Diagnostics;

namespace ANTHEA.ModelWorkspace;

/// <summary>Maps the retained MIDAS snapshot to the released GPC converter and returns display fields.
/// Dependency expansion, superposition, frame transforms and governing states are computed by GPC.</summary>
public sealed partial class GpcModelCombinationService
{
    readonly SemaphoreSlim calculationGate=new(1,1);
    PreparedModel? prepared;
    sealed record PreparedModel(string Revision,GPC.Model.Models.Model Model,GPC.Model.Combinations.Combination Output,string[] Warnings,Dictionary<ResultLocation,Element> Owners);
    public void ClearCache()=>prepared=null;

    public async Task<ModelCombinationCalculation> CalculateAsync(ModelSnapshot snapshot,string definitionId,
        IProgress<ModelCombinationProgress>? progress=null,CancellationToken token=default)
    {
        await calculationGate.WaitAsync(token).ConfigureAwait(false);
        try {return await Task.Run(()=>Calculate(snapshot,definitionId,progress,token),token).ConfigureAwait(false);}
        finally {calculationGate.Release();}
    }

    ModelCombinationCalculation Calculate(ModelSnapshot snapshot,string definitionId,IProgress<ModelCombinationProgress>? progress,CancellationToken token)
    {
        var watch=Stopwatch.StartNew();snapshot.Validate();token.ThrowIfCancellationRequested();
        var definitions=ModelCombinations.Read(snapshot);
        var selected=definitions.SingleOrDefault(d=>d.Id==definitionId)??throw new InvalidDataException("Combinazione non trovata.");
        var records=definitions.Select(ModelCombinations.ToGpc).ToArray();
        var required=CombinationExpansion.StaticCases(records,definitionId).ToArray();
        if(required.Length==0)throw new InvalidDataException("La combinazione non contiene casi statici.");
        var bases=required.Select(c=>snapshot.Results.SingleOrDefault(r=>r.Name==c&&!r.IsEnvelope&&r.Combination==null&&r.Stage==null)
            ??throw new InvalidDataException("Risultati del caso base mancanti: "+c)).ToArray();
        progress?.Report(new("Risoluzione delle dipendenze della combinazione…"));
        // Bound expansion before allocating result states. This limit is explicit rather than a silent truncation.
        var alternatives=CombinationExpansion.Expand(records,definitionId,4096)
            .Select(a=>new CombinationAlternative(a.Label,a.Factors)).ToArray();
        token.ThrowIfCancellationRequested();
        string revision=ModelCombinations.InputRevision(snapshot);
        string preparationRevision=ModelCombinations.PreparationRevision(snapshot);
        bool reused=prepared?.Revision==preparationRevision;
        var context=reused?prepared!:Prepare(snapshot,preparationRevision,progress,token);
        token.ThrowIfCancellationRequested();prepared=context;
        var model=context.Model;
        var output=context.Output;
        var warnings=context.Warnings.ToList();
        double preparationMs=watch.Elapsed.TotalMilliseconds;
        progress?.Report(new($"Calcolo GPC · {alternatives.Length:N0} stati alternativi…"));
        // Keep the prepared model immutable: derived states remain in the report and presentation snapshot.
        var report=CombinationRebuild.Rebuild(model,output,alternatives,"anthea-static",model.AllElements,false,token);
        token.ThrowIfCancellationRequested();
        if(report.IsRejected||report.Samples.Count==0)throw new InvalidDataException("Combinazione non costruita: "+Messages(report.Diagnostics));
        warnings.AddRange(report.Diagnostics.Select(d=>d.Code+": "+d.Message));
        double rebuildMs=watch.Elapsed.TotalMilliseconds-preparationMs;
        progress?.Report(new("Preparazione di contouring, diagrammi e governanti…"));
        bool envelope=alternatives.Length>1;
        var samples=report.Samples.Select(s=>(Owner:context.Owners[s.State.DerivedFrom[0]],Sample:s)).ToArray();
        var sourceNodes=model.NodesElements.Values.ToDictionary(n=>n.Id,n=>int.Parse(n.Source.OriginalId,CultureInfo.InvariantCulture));
        var labels=samples.Select(s=>s.Sample.State.ConcomitantStateId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var results=(envelope?new[]{false,true}:new[]{false}).Select(max=>DisplayResult(snapshot,selected,required,revision,
            samples,sourceNodes,labels,envelope,max,token)).ToArray();
        if(results.All(r=>r.Values.Length==0&&(r.Fields?.Length??0)==0&&(r.Displacements?.Length??0)==0))
            throw new InvalidDataException("Nessun campo completo disponibile per la visualizzazione.");
        return new(results,warnings.Distinct().ToArray(),watch.Elapsed.TotalMilliseconds,preparationMs,rebuildMs,
            watch.Elapsed.TotalMilliseconds-preparationMs-rebuildMs,reused);
    }

    static PreparedModel Prepare(ModelSnapshot snapshot,string revision,IProgress<ModelCombinationProgress>? progress,CancellationToken token)
    {
        var source=Source(snapshot);
        progress?.Report(new("Preparazione iniziale del modello GPC…"));
        var import=CivilNxGeometryReader.Import(source,new AnalysisSource {Program=CivilNxModelProfile.Program,
            SolverVersion="copia conservata da ANTHEA",ModelRevision=revision,AnalysisId=revision},new CombinationProfile(),token);
        token.ThrowIfCancellationRequested();
        if(import.Model==null||import.Status==ImportStatus.Rejected)throw new InvalidDataException("Modello GPC non disponibile: "+Messages(import.Diagnostics));
        var model=import.Model;ValidateGeometry(snapshot,model);
        var warnings=import.Diagnostics.Where(d=>d.Severity!=DiagnosticSeverity.Information).Select(d=>d.Code+": "+d.Message).ToList();
        var cases=snapshot.Results.Where(r=>!r.IsEnvelope&&r.Combination==null&&r.Stage==null&&model.LoadCases.ContainsKey(r.Name)).ToArray();
        progress?.Report(new($"Preparazione iniziale · {cases.Length} casi base GPC. Saranno riutilizzati per le altre combinazioni…"));
        var tables=ResultTables(snapshot,cases,warnings,token);
        var imported=CivilNxResults.Import(model,source,tables,"anthea-static",cancellationToken:token);
        token.ThrowIfCancellationRequested();
        if(imported.Status==ImportStatus.Rejected)throw new InvalidDataException("Casi base non compatibili: "+Messages(imported.Diagnostics));
        warnings.AddRange(imported.Diagnostics.Where(d=>d.Severity!=DiagnosticSeverity.Information).Select(d=>d.Code+": "+d.Message));
        var owners=new Dictionary<ResultLocation,Element>(ReferenceEqualityComparer.Instance);
        foreach(var owner in model.AllElements)foreach(var sample in owner.Results.SelectMany(r=>r.Results))owners.Add(sample,owner);
        return new(revision,model,model.Combinations.Values.Single(),warnings.ToArray(),owners);
    }

    static string Messages(IEnumerable<ModelDiagnostic> diagnostics)=>string.Join("; ",diagnostics
        .Where(d=>d.Severity!=DiagnosticSeverity.Information).Take(5).Select(d=>d.Code+": "+d.Message));

    sealed class CombinationProfile:ICivilNxGeometryProfile
    {
        readonly CivilNxModelProfile source=new();
        public string Id=>source.Id;
        public IReadOnlyDictionary<string,CapabilityStatus> Capabilities=>source.Capabilities;
        public ImportBatch Read(CivilNxSnapshot snapshot,AnalysisSource identity,CancellationToken token)
        {
            var batch=source.Read(snapshot,identity,token);batch.Combinations.Clear();
            // A factor-free output is declared BEFORE analysis. Rebuild receives the actual expanded factors
            // and returns detached states, so edits never change the prepared model or its analysis fingerprint.
            // The named user definition and its input revision belong to the ANTHEA document, not this scratch model.
            batch.Combinations.Add(new(){Id="ANTHEA/runtime",Name="ANTHEA runtime",Kind=CombinationKind.Envelope,Source="ANTHEA",Status="",Terms=[]});
            return batch;
        }
    }

    static ModelResultSet DisplayResult(ModelSnapshot snapshot,ModelCombinationDefinition definition,string[] required,string revision,
        (Element Owner,ResultLocation Sample)[] samples,IReadOnlyDictionary<int,int> sourceNodes,string[] labels,bool envelope,bool maximum,CancellationToken token)
    {
        var corners=new List<CornerResult>();var fields=new Dictionary<string,List<ResultValue>>();
        var displacements=new List<ModelDisplacement>();var governing=new List<ModelCombinationGoverning>();
        foreach(var group in samples.GroupBy(p=>(p.Owner,p.Sample is PointResultPlateForces plate?plate.SourceNodeId:0,
            p.Sample is StationResultBeamForces beam?(double?)beam.ParametricDistance:null)))
        {
            token.ThrowIfCancellationRequested();
            int id=int.Parse(group.Key.Owner.Source.OriginalId,CultureInfo.InvariantCulture);
            var first=group.First().Sample;
            string family=first is PointResultPlateForces?"PLATE":first is StationResultBeamForces?"BEAM":"NODE";
            // GPC's SourceNodeId is the node's internal Model ID after mapping. Resolve its source identity explicitly.
            int node=family=="PLATE"?sourceNodes[group.Key.Item2??throw new InvalidDataException("Risultato plate privo del nodo sorgente.")]:
                family=="NODE"?id:group.Key.Item3==0?snapshot.Elements.Single(e=>e.Id==id).Nodes[0]:group.Key.Item3==1?snapshot.Elements.Single(e=>e.Id==id).Nodes[1]:0;
            string[] components=family=="PLATE"?ModelSnapshot.Components:family=="BEAM"?ResultFields.Components("BEAM","LOCAL").ToArray():["ux","uy","uz"];
            string[] canonical=family=="PLATE"?["Mxx","Myy","Mxy","Nxx","Nyy","Nxy","Qx","Qy"]:family=="BEAM"?["N","V1","V2","T","M1","M2"]:["Dx","Dy","Dz"];
            var values=new double[components.Length];
            for(int c=0;c<components.Length;c++)
            {
                bool reverse=family=="PLATE"&&c<3||family=="BEAM"&&c==4;
                string role=((maximum!=reverse)?"max ":"min ")+canonical[c];
                var chosen=envelope?group.Select(p=>p.Sample).SingleOrDefault(s=>Roles(s).Contains(role)):first;
                if(chosen==null)throw new InvalidDataException("GPC non ha restituito il governante completo per "+components[c]+".");
                var vector=DisplayVector(chosen);values[c]=vector[c];
                if(envelope)governing.Add(new(family,id,node,group.Key.Item3,components[c],"A"+(Array.IndexOf(labels,chosen.State.ConcomitantStateId)+1),vector));
            }
            if(family=="PLATE")corners.Add(new(id,node,values));
            else if(family=="NODE")
            {
                var vector=DisplayVector(first);
                displacements.Add(new(id,values[0],values[1],values[2]){Rotation=envelope?null:new(vector[3],vector[4],vector[5])});
            }
            else for(int c=0;c<components.Length;c++)
            {if(!fields.TryGetValue(components[c],out var list))fields[components[c]]=list=[];list.Add(new(id,node,values[c],group.Key.Item3));}
        }
        string name=definition.Name+" ["+definition.Source+"]"+(envelope?(maximum?" (max)":" (min)"):"");
        return new(name,envelope,corners.ToArray())
        {
            Fields=fields.Select(p=>new ResultField("BEAM","LOCAL",p.Key,p.Value.ToArray())).ToArray(),
            Displacements=displacements.Count>0?displacements.ToArray():null,
            Combination=new(definition.Id,revision,"GPCModel 5 / GPC.Converter 2.0.3",required,governing.ToArray(),labels)
        };
    }

    // The released GPC contract records the governing roles in Coverage. Read it strictly, never infer a missing extreme.
    static string[] Roles(ResultLocation sample)
    {
        string text=sample.State.Coverage??"";int end=text.IndexOf(" over the ",StringComparison.Ordinal);
        if(!text.StartsWith("Governs ",StringComparison.Ordinal)||end<8)throw new InvalidDataException("Formato dei governanti GPC non riconosciuto.");
        return text[8..end].Split(", ",StringSplitOptions.None);
    }

}
