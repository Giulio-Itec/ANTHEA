using System.Text.Json;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    // Hand-authored independent fixture: units kN/m; Q differs in sign by component.
    static ModelSnapshot CombinationModel()
    {
        var model=new ModelSnapshot
        {
            Name="Prova combinazioni",Source="MIDAS Civil NX",
            Nodes=[new(1,0,0,0),new(2,2,0,0),new(3,2,2,0),new(4,0,2,0)],
            Elements=[new(10,"PLATE",1,[1,2,3,4]),new(20,"BEAM",1,[1,2])],
            Plates=[new(1,"Soletta",.2,0)],Sections=[new(1,"Trave","SB",.3,.5,true)],
            SourceTables=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>("""
            {"UNIT":{"1":{"FORCE":"KN","DIST":"M"}},
             "NODE":{"1":{"X":0,"Y":0,"Z":0},"2":{"X":2,"Y":0,"Z":0},"3":{"X":2,"Y":2,"Z":0},"4":{"X":0,"Y":2,"Z":0}},
             "ELEM":{"10":{"TYPE":"PLATE","MATL":1,"SECT":1,"NODE":[1,2,3,4],"ANGLE":0,"STYPE":1},"20":{"TYPE":"BEAM","MATL":1,"SECT":1,"NODE":[1,2,0,0],"ANGLE":0,"STYPE":0}},
             "MATL":{"1":{"TYPE":"CONC","NAME":"C30/37","PARAM":[{"P_TYPE":1,"STANDARD":"EN04(RC)","DB":"C30/37","ELAST":32836000}]}},
             "THIK":{"1":{"NAME":"Soletta","TYPE":"VALUE","T_IN":0.2,"O_VALUE":0}},
             "SECT":{"1":{"SECTTYPE":"DBUSER","SECT_NAME":"Trave","SECT_BEFORE":{"OFFSET_PT":"CC","OFFSET_CENTER":0,"SHAPE":"SB","DATATYPE":2,"SECT_I":{"vSIZE":[0.5,0.3,0,0]}}}},
             "STLD":{"1":{"NAME":"G","TYPE":"D"},"2":{"NAME":"Q","TYPE":"L"}}}
            """)
        };
        string[] beamComponents=["Fx","Fy","Fz","Mx","My","Mz"];
        ModelResultSet Result(string name,double[] p,double[] b)=>new(name,false,model.Nodes.Select(n=>new CornerResult(10,n.Id,p.ToArray())).ToArray())
        {Fields=beamComponents.Select((c,i)=>new ResultField("BEAM","LOCAL",c,[new(20,1,b[i],0),new(20,2,b[i],1)])).ToArray()};
        model.Results=[Result("G",[10,20,30,40,50,60,70,80],[10,20,30,40,50,60]),
            Result("Q",[-4,8,-12,16,-20,24,-28,32],[-4,8,-12,16,-20,24])];
        return model;
    }

    static async Task CombinationChecks(string output)
    {
        var model=CombinationModel();string original=model.Fingerprint();
        var linear=new ModelCombinationDefinition("ANTHEA/sum","Somma",ModelCombinationKind.Linear,"ANTHEA","",[new("G",false,2),new("Q",false,-.5)],true);
        model=ModelCombinations.Save(model,linear);
        Check(model.CustomCombinations!.Length==1&&original==CombinationModel().Fingerprint(),"combination editor preserves the input document");
        var service=new GpcModelCombinationService();
        var sum=await service.CalculateAsync(model,linear.Id);
        var sparse=ModelSnapshot.FromJson(JsonSerializer.Serialize(model));
        sparse.Nodes=sparse.Nodes.Select(n=>n with {Id=n.Id+1000}).ToArray();
        sparse.Elements=sparse.Elements.Select(e=>e with {Nodes=e.Nodes.Select(n=>n+1000).ToArray()}).ToArray();
        sparse.Results=sparse.Results.Select(r=>r with {Values=r.Values.Select(v=>v with {Node=v.Node+1000}).ToArray(),Fields=r.Fields!.Select(f=>f with {Values=f.Values.Select(v=>v with {Node=v.Node+1000}).ToArray()}).ToArray()}).ToArray();
        var sourceNodes=System.Text.Json.Nodes.JsonNode.Parse(sparse.SourceTables!["NODE"].GetRawText())!.AsObject();
        sparse.SourceTables["NODE"]=JsonSerializer.SerializeToElement(sourceNodes.ToDictionary(n=>(int.Parse(n.Key)+1000).ToString(),n=>n.Value));
        var sourceElements=System.Text.Json.Nodes.JsonNode.Parse(sparse.SourceTables["ELEM"].GetRawText())!;
        foreach(var e in sourceElements.AsObject())
        {var ids=e.Value!["NODE"]!.AsArray();for(int i=0;i<ids.Count;i++)if(ids[i]!.GetValue<int>()>0)ids[i]=ids[i]!.GetValue<int>()+1000;}
        sparse.SourceTables["ELEM"]=JsonSerializer.SerializeToElement(sourceElements);
        var sparseResult=await service.CalculateAsync(sparse,linear.Id);
        ModelCombinations.Attach(sparse,linear.Id,sparseResult).Validate();
        Check(sparseResult.Results[0].Values.All(v=>v.Node>1000),"GPC internal node IDs are mapped back to non-contiguous source identities");
        sum=await service.CalculateAsync(model,linear.Id);
        var expected=new double[]{22,36,66,72,110,108,154,144};
        Check(sum.Results.Length==1&&!sum.Results[0].IsEnvelope&&sum.Results[0].Values.All(v=>v.Values.Zip(expected).All(p=>Math.Abs(p.First-p.Second)<1e-8)),"GPC linear combination: eight independent plate expectations, signs and units");
        Check(sum.Results[0].Fields!.Select(f=>f.Values[0].Value).Zip(expected.Take(6)).All(p=>Math.Abs(p.First-p.Second)<1e-8),"GPC linear combination: all six beam components, including My sign");
        var env=new ModelCombinationDefinition("ANTHEA/env","Inviluppo",ModelCombinationKind.Envelope,"ANTHEA","",[new("G",false,1),new("Q",false,1)],true);
        model=ModelCombinations.Save(model,env);
        var envelope=await service.CalculateAsync(model,env.Id);
        Check(envelope.PreparationReused,"changing only combination definitions reuses prepared GPC geometry and all static cases");
        double[][] limits=[[-4,8,-12,16,-20,24,-28,32],[10,20,30,40,50,60,70,80]];
        Check(envelope.Results.Length==2&&envelope.Results.Select((r,i)=>r.Values.All(v=>v.Values.SequenceEqual(limits[i]))).All(b=>b),"GPC envelope preserves minima/maxima in display axes, including inverted signs");
        Check(envelope.Results.All(r=>r.Combination!.Governing.Length==44)&&envelope.Results[0].Combination!.Governing.First(g=>g.Family=="PLATE").ConcomitantValues.SequenceEqual(limits[0]),"envelope retains the complete concomitant vector for each governing component");
        var attached=ModelCombinations.Attach(model,env.Id,envelope);
        string json=JsonSerializer.Serialize(attached);var loaded=ModelSnapshot.FromJson(json);
        Check(loaded.Fingerprint()==attached.Fingerprint()&&ModelCombinations.Inspect(loaded).Single(d=>d.Definition.Id==env.Id).State=="Calcolata","definitions and calculated fields survive save/reopen with governants");
        Reject(()=>ModelCombinations.Save(model,linear with {Terms=[new(linear.Id,true,1)]}),"combination cycles rejected");
        Reject(()=>ModelCombinations.Save(model,linear with {Terms=[new("Missing",false,1)]}),"unknown case rejected");
        Reject(()=>ModelCombinations.Save(model,linear with {Terms=[new("G",false,double.NaN)]}),"non-finite coefficient rejected");
        var nested=ModelCombinations.Save(model,linear with {Terms=[new(env.Id,true,-2)]});
        var nestedResult=await service.CalculateAsync(nested,linear.Id);
        Check(nestedResult.Results[0].Values[0].Values[0]==-20&&nestedResult.Results[1].Values[0].Values[0]==8,"nested envelope with negative factor reverses extrema");
        Reject(()=>ModelCombinations.Remove(nested,env.Id),"referenced custom definition cannot be deleted");
        Check(ModelCombinations.Save(attached,linear).Results.All(r=>r.Combination==null),"editing definitions invalidates derived fields");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try {await service.CalculateAsync(model,env.Id,token:cancelled.Token);throw new Exception("Cancellation ignored");}
        catch(OperationCanceledException){Check(true,"cancelled calculation never publishes partial fields");}
        await File.WriteAllTextAsync(Path.Combine(output,"combination-fixture.antheamodel"),json);
        var crossed=ModelCombinations.Save(model,env with {Terms=[new("G",false,1),new("Q",false,4)]});
        var crossedResult=await service.CalculateAsync(crossed,env.Id);
        Check(crossedResult.Results[1].Values[0].Values.SequenceEqual(new double[]{10,32,30,64,50,96,70,128}),"envelope can change governing alternative within one plate vector");
        var gov=crossedResult.Results[1].Combination!.Governing.Where(g=>g.Family=="PLATE"&&g.Node==1).ToArray();
        Check(gov[0].Alternative!=gov[1].Alternative&&gov[1].ConcomitantValues[0]==-16,"component governants retain actual concomitant Mxx, not the unrelated maximum");
        var displaced=ModelSnapshot.FromJson(JsonSerializer.Serialize(model));
        displaced.Results=displaced.Results.Select(r=>r with {Displacements=displaced.Nodes.Select(n=>r.Name=="G"
            ?new ModelDisplacement(n.Id,.001,-.002,.003){Rotation=new(.001,-.002,.003)}
            :new ModelDisplacement(n.Id,-.002,.008,-.012){Rotation=new(.002,.004,-.008)}).ToArray()}).ToArray();
        var nodal=await service.CalculateAsync(displaced,linear.Id);
        Check(nodal.Results[0].Displacements?.Length==4&&Math.Abs(nodal.Results[0].Displacements![0].X-.003)<1e-12&&
            Math.Abs(nodal.Results[0].Displacements![0].Rotation!.X-.001)<1e-12,"complete nodal translations and rotations are combined by GPC in metres/radians");
        var imported=CombinationModel();
        imported.SourceTables!["LCOM-GEN"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{{"1",new {NAME="Importata",iTYPE=0,ACTIVE="ACTIVE",vCOMB=new[]{new {ANAL="ST",LCNAME="G",FACTOR=1}}}}});
        var origin=ModelCombinations.Read(imported).Single();
        var edited=ModelCombinations.Save(imported,origin with {Editable=true,Terms=[new("G",false,2)]});
        Check(ModelCombinations.Read(edited).Single().Terms[0].Factor==2&&ModelCombinations.Read(imported).Single().Terms[0].Factor==1,"editing an imported combination creates a local override without changing source evidence");
        var removed=ModelCombinations.Remove(imported,origin.Id);
        Check(ModelCombinations.Read(removed).Length==0&&ModelCombinations.Read(ModelCombinations.RestoreImported(removed)).Length==1,"imported definitions can be deleted and restored locally");
        foreach(var scope in Enum.GetValues<ModelUpdateScope>())
        {
            var updated=ModelSnapshotUpdate.Apply(model,CombinationModel(),scope);
            Check(updated.CustomCombinations?.Length==2&&updated.Results.All(r=>r.Combination==null),"model update preserves custom definitions and invalidates derived cache: "+scope);
        }
        var stale=CombinationModel();stale.Nodes[0]=stale.Nodes[0] with {Z=.01};
        stale=ModelCombinations.Save(stale,linear);
        try {await service.CalculateAsync(stale,linear.Id);throw new Exception("Stale geometry accepted");}
        catch(InvalidDataException){Check(true,"GPC adapter rejects geometry inconsistent with source tables");}
        var incomplete=ModelSnapshot.FromJson(JsonSerializer.Serialize(model));incomplete.Results=[incomplete.Results[0]];
        Check(!ModelCombinations.Inspect(incomplete).Single(d=>d.Definition.Id==linear.Id).CanCalculate,"missing base results disable calculation without assuming zero");
        await CombinationPresentationChecks(imported);
        foreach(var (table,jsonData) in new[]{
            ("LCOM-GEN","{\"1\":{\"NAME\":\"Errata\",\"iTYPE\":0,\"vCOMB\":[{\"ANAL\":\"ST\",\"LCNAME\":\"G\"}]}}"),
            ("STLD","{\"1\":{\"TYPE\":\"D\"}}"),
            ("STLD","{\"1\":{\"NAME\":\"\"}}")})
        {
            var malformed=CombinationModel();malformed.SourceTables![table]=JsonSerializer.Deserialize<JsonElement>(jsonData);
            Reject(()=>malformed.Validate(),"invalid combination/case metadata rejected before publication: "+table);
            int publications=0;
            using var protectedView=new ModelViewerViewModel(imported,[],false,new FakeServices{Next=malformed},_=>publications++,(_,_,_,_)=>{});
            await protectedView.ImportCommand.ExecuteAsync(null);
            Check(publications==0&&ReferenceEquals(protectedView.Snapshot,imported)&&protectedView.HasImportFailure,
                "failed metadata import preserves both project document and view: "+table);
        }
    }

    static async Task CombinationPresentationChecks(ModelSnapshot imported)
    {
        int changes=0;
        using var vm=new ModelViewerViewModel(imported,[],false,new FakeServices(),_=>changes++,(_,_,_,_)=>{});
        vm.ShowInspector=false;vm.SelectedRepresentation=1;
        vm.OpenCombinationManagerCommand.Execute(null);
        vm.NewCombinationCommand.Execute(null);
        Check(vm.IsCombinationDirty&&vm.CanEditCombination&&vm.CombinationTerms.Count==1,"new combination opens an editable draft");
        vm.CombinationName="Somma UI";vm.CombinationTerms[0].Factor="2";
        vm.AddCombinationTermCommand.Execute(null);vm.CombinationTerms[1].Source=vm.CombinationSources.Single(s=>s.Id=="Q");vm.CombinationTerms[1].Factor="-0.5";
        vm.SaveCombinationCommand.Execute(null);
        Check(changes==1&&!vm.IsCombinationDirty&&vm.CalculateCombinationCommand.CanExecute(null)&&vm.SelectedRepresentation==1&&!vm.ShowInspector,"saving from MVVM preserves viewport representation and panel state");
        await vm.CalculateCombinationCommand.ExecuteAsync(null);
        Check(changes==2&&vm.ResultKind.Contains("GPC")&&vm.Snapshot!.Results.Last().Values[0].Values[0]==22&&!vm.IsBusy,"MVVM calculation atomically publishes GPC results into the viewer");
        await vm.CalculateCombinationCommand.ExecuteAsync(null);
        Check(changes==2&&vm.CombinationMessage.Contains("già calcolati"),"calculated result reselected without rebuilding or rewriting document");
        vm.CombinationTerms[0].Factor="NaN";vm.SaveCombinationCommand.Execute(null);
        Check(changes==2&&vm.IsCombinationDirty&&vm.CombinationMessage.Contains("non valido"),"invalid coefficients remain editable and cannot modify the model");
        vm.DiscardCombinationEditsCommand.Execute(null);vm.DeleteCombinationCommand.Execute(null);
        Check(changes==3&&vm.Combinations.Count==1,"custom combination can be removed from the interface");
        vm.SelectedCombination=vm.Combinations[0];vm.DeleteCombinationCommand.Execute(null);
        Check(vm.Combinations.Count==0&&vm.RestoreImportedCombinationsCommand.CanExecute(null),"imported combination can be deleted from the interface");
        vm.RestoreImportedCombinationsCommand.Execute(null);Check(vm.Combinations.Count==1,"restore imported definitions from retained source tables");
        vm.ShowCombinationGrid=true;
        Check(vm.CombinationGridRows?.Count==1&&vm.CombinationGridHeaders.Values.Contains("↓ G"),"combination matrix exposes coefficients from saved definitions");
        using var readOnly=new ModelViewerViewModel(imported,[],true,new FakeServices(),_=>throw new Exception("readonly mutated"),(_,_,_,_)=>{});
        readOnly.OpenCombinationManagerCommand.Execute(null);
        Check(!readOnly.NewCombinationCommand.CanExecute(null)&&!readOnly.DeleteCombinationCommand.CanExecute(null)&&!readOnly.CalculateCombinationCommand.CanExecute(null),"historical revision permits catalogue inspection and blocks combination mutations");
        using var editImported=new ModelViewerViewModel(imported,[],false,new FakeServices(),_=>{},(_,_,_,_)=>{});
        editImported.CombinationName="Importata modificata";editImported.SaveCombinationCommand.Execute(null);
        Check(editImported.Snapshot!.CustomCombinations?.Single().Status=="ACTIVE","editing imported definition preserves its source activity status");
    }

    static async Task<int> CombinationReal(string output,string[] args)
    {
        int index=Array.IndexOf(args,"--combination-real");
        var snapshot=ModelSnapshotFiles.Read(args[index+1]);
        string id=args.Length>index+2?args[index+2]:"LCOM-CONC/SLU_Q1_1";
        Console.WriteLine($"Combination {id}; {snapshot.Results.Length} base cases");
        var progress=new Progress<ModelCombinationProgress>(p=>Console.WriteLine(DateTime.Now.ToString("HH:mm:ss")+" "+p.Message));
        var service=new GpcModelCombinationService();
        var result=await service.CalculateAsync(snapshot,id,progress);
        await File.WriteAllTextAsync(Path.Combine(output,"combination-real.json"),JsonSerializer.Serialize(result));
        using(var stream=File.Create(Path.Combine(output,"spalla-combinazioni.antheamodel")))await JsonSerializer.SerializeAsync(stream,ModelCombinations.Attach(snapshot,id,result));
        var warm=await service.CalculateAsync(snapshot,id,progress);
        await File.WriteAllTextAsync(Path.Combine(output,"timing.json"),JsonSerializer.Serialize(new {Cold=new{result.Milliseconds,result.PreparationMilliseconds,result.RebuildMilliseconds,result.DisplayMilliseconds},Warm=new{warm.Milliseconds,warm.PreparationMilliseconds,warm.RebuildMilliseconds,warm.DisplayMilliseconds,warm.PreparationReused}}));
        Console.WriteLine($"PASS {result.Milliseconds:F0} ms; {result.Results.Length} fields; {string.Join("; ",result.Warnings)}");return 0;
    }
}
