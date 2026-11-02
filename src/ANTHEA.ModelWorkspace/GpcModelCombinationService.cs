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
    {
        public CombinationRebuildCache Cache {get;}=new(){SourceFingerprints=false,PlatePrincipals=true};
        /// <summary>2/11/2026: the static samples of the prepared model matched, checked and rotated once for the matrix of GPC.</summary>
        public CombinationMatrix? Matrix {get;set;}
    }
    public void ClearCache()=>prepared=null;

    /// <summary>
    /// Request of the user of 2/11/2026 («spacchettando delle parti comuni»): the combinations within the limit of the expansion are evaluated
    /// by the matrix of GPC (CombinationMatrix, GPCModel 5.38), which gives the values, governing roles and principal components of the samples
    /// of CombinationRebuild bit for bit without registering them; the time of a combination was almost all in the samples, their states and
    /// texts. False: the samples of CombinationRebuild, as before (the checks compare the two).
    /// </summary>
    public bool UseMatrix {get;set;}=true;
    const string Dataset="anthea-static";

    /// <summary>
    /// Refactoring of the combinations (29/10/2026): the preparation of GPC (geometry and static cases of the copy, about 1 s for 100 elements
    /// and 50 cases, 8 s for 500 × 100) made in advance, for instance while the user looks at the catalogue, so that the first combination
    /// does not wait for it. The same preparation and the same gate as <see cref="CalculateAsync"/>: a calculation started meanwhile waits for
    /// it and reuses it. Nothing is reported: a copy that cannot be prepared is explained by the calculation, as before.
    /// </summary>
    public async Task PrepareAsync(ModelSnapshot snapshot,CancellationToken token=default)
    {
        try
        {
            await calculationGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await Task.Run(()=>
                {
                    if(!GpcSnapshotModel.CanRebuild(snapshot))return;
                    string revision=ModelCombinations.PreparationRevision(snapshot);
                    if(prepared?.Revision==revision)return; // already prepared: the catalogue opened again
                    snapshot.Validate();prepared=Prepare(snapshot,revision,null,token);
                    if(UseMatrix)prepared.Matrix=CombinationMatrix.Prepare(prepared.Model,Dataset,prepared.Model.AllElements,token);
                },token).ConfigureAwait(false);
            }
            finally {calculationGate.Release();}
        }
        catch(Exception ex) when(ex is OperationCanceledException or InvalidDataException or ArgumentException or InvalidOperationException
            or NotSupportedException or FormatException or KeyNotFoundException or System.Text.Json.JsonException) {}
    }

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
        // 1/11/2026 (point 8): over the limit the extremes are found term by term on the tree of the definition (GPC CombinationTreeNode),
        // without listing the alternatives; below it the expansion as before, so the results below the limit do not change.
        var tree=expansion.Over?CombinationExpansion.Tree(records,definitionId):null;
        var alternatives=tree==null?Alternatives(CombinationExpansion.Expand(records,definitionId,ModelCombinations.MaximumAlternatives)):[];
        // COMBO-B3: terms that cancel each other (Q − Q) leave no static case: explained instead of GPC's «EmptyCombination».
        if(tree==null&&alternatives.All(a=>a.Factors.Count==0))throw new InvalidDataException(ModelCombinations.ZeroMessage);
        token.ThrowIfCancellationRequested();
        string revision=ModelCombinations.DefinitionRevision(snapshot,definitionId);
        string preparationRevision=ModelCombinations.PreparationRevision(snapshot);
        bool reused=prepared?.Revision==preparationRevision;
        var context=reused?prepared!:Prepare(snapshot,preparationRevision,progress,token);
        token.ThrowIfCancellationRequested();prepared=context;
        var model=context.Model;
        var output=context.Output;
        var warnings=context.Warnings.ToList();
        bool matrix=tree==null&&UseMatrix;
        // The static samples of every location matched once for all the following combinations (the work of the cache of the rebuild).
        if(matrix)context.Matrix??=CombinationMatrix.Prepare(model,Dataset,model.AllElements,token);
        double preparationMs=watch.Elapsed.TotalMilliseconds;
        progress?.Report(new(tree==null?$"Calcolo GPC · {alternatives.Length:N0} stati alternativi…":$"Calcolo GPC · {tree.AlternativeCount():N0} stati alternativi, estremi per termini…"));
        CombinedState[] states;IReadOnlyList<PlatePrincipalResult> platePrincipals;
        if(matrix)
        {
            var evaluated=context.Matrix!.Evaluate(output,alternatives,true,token);
            token.ThrowIfCancellationRequested();
            if(evaluated.IsRejected||evaluated.Results.Count==0)throw new InvalidDataException("Combinazione non costruita: "+Messages(evaluated.Diagnostics));
            warnings.AddRange(evaluated.Diagnostics.Select(d=>d.Code+": "+d.Message));
            states=evaluated.Results.SelectMany(l=>l.States.Select(s=>new CombinedState(l.Owner,l.Location,s.StateId,s.Roles,DisplayVector(l.Location,s.Values)))).ToArray();
            platePrincipals=evaluated.PlatePrincipals;
        }
        else
        {
            // Keep the prepared model immutable: derived states remain in the report and presentation snapshot.
            var report=tree==null?CombinationRebuild.Rebuild(model,output,alternatives,Dataset,model.AllElements,false,token,context.Cache)
                :CombinationRebuild.Rebuild(model,output,tree,Dataset,model.AllElements,false,token,context.Cache);
            token.ThrowIfCancellationRequested();
            if(report.IsRejected||report.Samples.Count==0)throw new InvalidDataException("Combinazione non costruita: "+Messages(report.Diagnostics));
            warnings.AddRange(report.Diagnostics.Select(d=>d.Code+": "+d.Message));
            bool governed=tree!=null||alternatives.Length>1;
            states=report.Samples.Select(s=>new CombinedState(context.Owners[s.State.DerivedFrom[0]],s,s.State.ConcomitantStateId,governed?Roles(s):[],DisplayVector(s))).ToArray();
            platePrincipals=report.PlatePrincipals;
        }
        double rebuildMs=watch.Elapsed.TotalMilliseconds-preparationMs;
        progress?.Report(new("Preparazione di contouring, diagrammi e governanti…"));
        bool envelope=tree!=null||alternatives.Length>1;
        var sourceNodes=model.NodesElements.Values.ToDictionary(n=>n.Id,n=>int.Parse(n.Source.OriginalId,CultureInfo.InvariantCulture));
        var labels=states.Select(s=>s.StateId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        // 1/11/2026: the principal components of the plates computed by GPC for every combined state (PlatePrincipals), by location.
        var principals=new Dictionary<(Element,int?),PlatePrincipalResult>();
        foreach(var p in platePrincipals)if(p.Location is PointResultPlateForces {SourceNodeId:not null} at)principals.TryAdd((p.Owner,at.SourceNodeId),p);
        var principalLabels=platePrincipals.SelectMany(p=>p.Components).SelectMany(e=>new[]{e.MinimumStateId,e.MaximumStateId})
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var results=(envelope?new[]{false,true}:new[]{false}).Select(max=>DisplayResult(snapshot,selected,required,revision,
            states,sourceNodes,labels,envelope,max,token,principals,principalLabels)).ToArray();
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

    /// <summary>A combined state for the display: its element and location, its state id and governing roles (empty for a linear combination)
    /// and its vector as displayed. From the samples of CombinationRebuild or from the states of CombinationMatrix: the same values.</summary>
    readonly record struct CombinedState(Element Owner,ResultLocation Location,string StateId,IReadOnlyList<string> Roles,double[] Vector);

    static ModelResultSet DisplayResult(ModelSnapshot snapshot,ModelCombinationDefinition definition,string[] required,string revision,
        CombinedState[] states,IReadOnlyDictionary<int,int> sourceNodes,string[] labels,bool envelope,bool maximum,CancellationToken token,
        IReadOnlyDictionary<(Element,int?),PlatePrincipalResult> principals,string[] principalLabels)
    {
        var corners=new List<CornerResult>();var fields=new Dictionary<(string Family,string Component),List<ResultValue>>();
        var displacements=new List<ModelDisplacement>();var reactions=new List<ModelReaction>();var governing=new List<ModelCombinationGoverning>();
        var principalFields=PrincipalComponents.ToDictionary(c=>c,_=>new List<ResultValue>());var principalGoverning=new List<ModelCombinationGoverning>();
        var principalIndex=principalLabels.Select((label,i)=>(label,i)).ToDictionary(p=>p.label,p=>p.i,StringComparer.Ordinal);
        var elements=snapshot.Elements.ToDictionary(e=>e.Id);
        var labelIndex=labels.Select((label,i)=>(label,i)).ToDictionary(p=>p.label,p=>p.i,StringComparer.Ordinal);
        // 1.16.0: displacements and reactions of a node are separate groups; members take the family of their document element (BEAM, TRUSS, CABLE).
        foreach(var group in states.GroupBy(p=>(p.Owner,p.Location is PointResultPlateForces plate?plate.SourceNodeId:0,
            p.Location is StationResultBeamForces beam?(double?)beam.ParametricDistance:null,p.Location is NodeResultForces)))
        {
            token.ThrowIfCancellationRequested();
            int id=int.Parse(group.Key.Owner.Source.OriginalId,CultureInfo.InvariantCulture);
            var firstState=group.First();var first=firstState.Location;
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
                var chosen=envelope?group.Where(p=>p.Roles.Contains(role)).Select(p=>(CombinedState?)p).SingleOrDefault():firstState;
                if(chosen==null)throw new InvalidDataException("GPC non ha restituito il governante completo per "+components[c]+".");
                var vector=chosen.Value.Vector;values[c]=vector[c];
                if(envelope)governing.Add(new(family,id,node,group.Key.Item3,components[c],"A"+((labelIndex.TryGetValue(chosen.Value.StateId,out int index)?index:-1)+1),vector));
            }
            if(family=="PLATE")
            {
                corners.Add(new(id,node,values));
                if(principals.TryGetValue((group.Key.Owner,group.Key.Item2),out var principal))
                    for(int c=0;c<PrincipalComponents.Count;c++)
                    {
                        var (value,state,vector)=PrincipalExtreme(principal,c,maximum);
                        principalFields[PrincipalComponents[c]].Add(new(id,node,value));
                        if(envelope)principalGoverning.Add(new("PLATE",id,node,null,PrincipalComponents[c],"P"+(principalIndex[state]+1).ToString(CultureInfo.InvariantCulture),vector));
                    }
            }
            else if(family=="NODE")
            {
                var vector=firstState.Vector;
                displacements.Add(new(id,values[0],values[1],values[2]){Rotation=envelope?null:new(vector[3],vector[4],vector[5])});
            }
            else if(family=="REACTION")reactions.Add(new(id,values[0],values[1],values[2],values[3],values[4],values[5]));
            else for(int c=0;c<components.Length;c++)
            {if(!fields.TryGetValue((family,components[c]),out var list))fields[(family,components[c])]=list=[];list.Add(new(id,node,values[c],group.Key.Item3));}
        }
        string name=definition.Name+" ["+definition.Source+"]"+(envelope?(maximum?" (max)":" (min)"):"");
        return new(name,envelope,corners.ToArray())
        {
            // 1/11/2026: the principal components of the plates after the local fields (the local ones are unchanged).
            Fields=[..fields.OrderBy(p=>p.Key.Family=="BEAM"?0:1).Select(p=>new ResultField(p.Key.Family,"LOCAL",p.Key.Component,p.Value.ToArray())),
                ..PrincipalComponents.Where(c=>principalFields[c].Count>0).Select(c=>new ResultField("PLATE","PRINCIPAL",c,principalFields[c].ToArray()))],
            Displacements=displacements.Count>0?displacements.ToArray():null,
            Reactions=reactions.Count>0?reactions.OrderBy(r=>r.Node).ToArray():null,
            Combination=new(definition.Id,revision,"GPCModel 5 / GPC.Converter 2.0.3",required,governing.ToArray(),labels)
            {PrincipalGoverning=principalGoverning.Count>0?principalGoverning.ToArray():null,PrincipalAlternatives=principalGoverning.Count>0?principalLabels:null}
        };
    }

    /// <summary>1/11/2026: the principal components of the plates as displayed (MIDAS convention, kN·m/m and kN/m), in the order of
    /// ResultFields.Components("PLATE", "PRINCIPAL").</summary>
    static IReadOnlyList<string> PrincipalComponents=>ResultFields.Components("PLATE","PRINCIPAL");

    /// <summary>A displayed principal component from those of GPC (positive section face, N and mm): the displayed moments are the opposite of
    /// GPC's, so Mmax = −Mmin(GPC) and Mmin = −Mmax(GPC), and the displayed maximum of a moment is the GPC minimum; Fmax and Fmin are Nmax and
    /// Nmin. With the id of the governing state and its local vector as displayed (as DisplayVector).</summary>
    static (double Value,string State,double[] Vector) PrincipalExtreme(PlatePrincipalResult principal,int component,bool maximum)
    {
        const double moment=-MidasGpcDisplay.KilonewtonMetresPerMetrePerNewtonMillimetrePerMillimetre;
        var (gpc,reverse,scale)=component switch {0=>(3,true,moment),1=>(2,true,moment),2=>(0,false,1d),_=>(1,false,1d)};
        var extreme=principal.Components[gpc];bool upper=maximum!=reverse;
        var v=upper?extreme.MaximumState:extreme.MinimumState;
        return ((upper?extreme.Maximum:extreme.Minimum)*scale,upper?extreme.MaximumStateId:extreme.MinimumStateId,
            MidasGpcDisplay.PlateDisplayVector(v));
    }

    // The released GPC contract records the governing roles in Coverage. Read it strictly, never infer a missing extreme.
    static string[] Roles(ResultLocation sample)
    {
        string text=sample.State.Coverage??"";int end=text.IndexOf(" over the ",StringComparison.Ordinal);
        if(!text.StartsWith("Governs ",StringComparison.Ordinal)||end<8)throw new InvalidDataException("Formato dei governanti GPC non riconosciuto.");
        return text[8..end].Split(", ",StringSplitOptions.None);
    }

}
