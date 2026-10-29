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
    // GPCModel 5.28: the rebuild cache keeps, for the next combinations of the same prepared model, the static samples matched, validated and
    // rotated at each location. The report is never attached and only its values, roles and concomitant states are displayed: its states
    // carry no fingerprint of their static sources, the walk of every source of every state that took most of the time of a combination.
    sealed record PreparedModel(string Revision,GPC.Model.Models.Model Model,GPC.Model.Combinations.Combination Output,string[] Warnings,Dictionary<ResultLocation,Element> Owners)
    {public CombinationRebuildCache Cache {get;}=new(){SourceFingerprints=false};}
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
        // COMBO-B2: the same count and explanation as the catalogue, instead of the untranslated «CombinationExpansionLimit» of GPC.
        var expansion=ModelCombinations.AlternativeCount(definitions.ToDictionary(d=>d.Id,StringComparer.Ordinal),definitionId,new(StringComparer.Ordinal));
        if(expansion.Over)throw new InvalidDataException(ModelCombinations.ExpansionMessage(expansion.Count));
        var alternatives=Alternatives(CombinationExpansion.Expand(records,definitionId,ModelCombinations.MaximumAlternatives));
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
        var report=CombinationRebuild.Rebuild(model,output,alternatives,"anthea-static",model.AllElements,false,token,context.Cache);
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
        if(!GpcSnapshotModel.MidasTables(snapshot)&&GpcSnapshotModel.CanRebuild(snapshot))
        {
            // 1.15.0: sources without MIDAS tables (Straus7). The GPC model is rebuilt from the document copy itself.
            var basis=snapshot.Results.Where(r=>!r.IsEnvelope&&r.Combination==null&&r.Stage==null).ToArray();
            progress?.Report(new($"Preparazione iniziale · {basis.Length} casi base GPC dalla copia del documento. Saranno riutilizzati per le altre combinazioni…"));
            var (rebuilt,output,notes)=GpcSnapshotModel.Build(snapshot,revision,basis,token);
            var sampleOwners=new Dictionary<ResultLocation,Element>(ReferenceEqualityComparer.Instance);
            foreach(var owner in rebuilt.AllElements)foreach(var sample in owner.Results.SelectMany(r=>r.Results))sampleOwners.Add(sample,owner);
            return new(revision,rebuilt,output,notes,sampleOwners);
        }
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

    /// <summary>COMBO-B1 (29/10/2026): the same term twice in an envelope (±Q, ±E: Q × 1 and Q × −1) gives two alternatives with the same
    /// label, and GPC refuses an envelope whose labels repeat. The second and following ones take « #2», « #3» in the order of the terms;
    /// the labels of the envelopes without repetitions do not change.</summary>
    static CombinationAlternative[] Alternatives(IReadOnlyList<LinearAlternative> expanded)
    {
        var used=new HashSet<string>(StringComparer.Ordinal);
        return expanded.Select(a=>
        {
            string label=a.Label;
            for(int n=2;!used.Add(label);n++)label=a.Label+" #"+n.ToString(CultureInfo.InvariantCulture);
            return new CombinationAlternative(label,a.Factors);
        }).ToArray();
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
        var corners=new List<CornerResult>();var fields=new Dictionary<(string Family,string Component),List<ResultValue>>();
        var displacements=new List<ModelDisplacement>();var reactions=new List<ModelReaction>();var governing=new List<ModelCombinationGoverning>();
        var elements=snapshot.Elements.ToDictionary(e=>e.Id);
        // 1.16.0: displacements and reactions of a node are separate groups; members take the family of their document element (BEAM, TRUSS, CABLE).
        foreach(var group in samples.GroupBy(p=>(p.Owner,p.Sample is PointResultPlateForces plate?plate.SourceNodeId:0,
            p.Sample is StationResultBeamForces beam?(double?)beam.ParametricDistance:null,p.Sample is NodeResultForces)))
        {
            token.ThrowIfCancellationRequested();
            int id=int.Parse(group.Key.Owner.Source.OriginalId,CultureInfo.InvariantCulture);
            var first=group.First().Sample;
            string family=first is PointResultPlateForces?"PLATE":first is StationResultBeamForces?elements[id].Type:first is NodeResultForces?"REACTION":"NODE";
            // GPC's SourceNodeId is the node's internal Model ID after mapping. Resolve its source identity explicitly.
            int node=family=="PLATE"?sourceNodes[group.Key.Item2??throw new InvalidDataException("Risultato plate privo del nodo sorgente.")]:
                family is "NODE" or "REACTION"?id:group.Key.Item3==0?elements[id].Nodes[0]:group.Key.Item3==1?elements[id].Nodes[1]:0;
            string[] components=family switch {"PLATE"=>ModelSnapshot.Components,"NODE"=>["ux","uy","uz"],"REACTION"=>[..ModelReactions.Components],_=>ResultFields.Components(family,"LOCAL").ToArray()};
            string[] canonical=family switch {"PLATE"=>["Mxx","Myy","Mxy","Nxx","Nyy","Nxy","Qx","Qy"],"NODE"=>["Dx","Dy","Dz"],"REACTION"=>["Fx","Fy","Fz","Mx","My","Mz"],
                "BEAM"=>["N","V1","V2","T","M1","M2"],_=>["N"]};
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
            else if(family=="REACTION")reactions.Add(new(id,values[0],values[1],values[2],values[3],values[4],values[5]));
            else for(int c=0;c<components.Length;c++)
            {if(!fields.TryGetValue((family,components[c]),out var list))fields[(family,components[c])]=list=[];list.Add(new(id,node,values[c],group.Key.Item3));}
        }
        string name=definition.Name+" ["+definition.Source+"]"+(envelope?(maximum?" (max)":" (min)"):"");
        return new(name,envelope,corners.ToArray())
        {
            Fields=fields.OrderBy(p=>p.Key.Family=="BEAM"?0:1).Select(p=>new ResultField(p.Key.Family,"LOCAL",p.Key.Component,p.Value.ToArray())).ToArray(),
            Displacements=displacements.Count>0?displacements.ToArray():null,
            Reactions=reactions.Count>0?reactions.OrderBy(r=>r.Node).ToArray():null,
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
