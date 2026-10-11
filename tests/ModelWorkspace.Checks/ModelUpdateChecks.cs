using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static async Task ModelUpdateChecks()
    {
        var current=Sample.Model();current.SourceTables=new(){["MATL"]=JsonSerializer.SerializeToElement(new { x=new {NAME="C30",E=30000.0} })};
        current.Boundaries=[new("CONS/1","RESTRAIN",[1],null,"DX, DY, DZ")];
        var incoming=ModelSnapshot.FromJson(JsonSerializer.Serialize(current));incoming.Source="Nuova copia";
        incoming.Results=[new("Nuovo caso",false,[]) {Displacements=[new(1,.001,0,0)]}];
        string before=current.Fingerprint(),read=incoming.Fingerprint();
        var geometry=ModelSnapshotUpdate.Apply(current,incoming,ModelUpdateScope.Geometry);
        Check(geometry.Results.Length==0 && geometry.SourceTables==null && geometry.Boundaries==null && geometry.Plates[0].Offset==-.7 && geometry.Sections[0].Width==.25,"geometry-only keeps physical section and offset geometry while removing attributes, loads and previous results");
        Check(geometry.UpdateInfo is {Scope:ModelUpdateScope.Geometry,DiscardedResultCases:1},"partial update explicitly records its scope and removed prior cases");
        var model=ModelSnapshotUpdate.Apply(current,incoming,ModelUpdateScope.ModelWithoutResults);
        Check(model.Results.Length==0 && model.Boundaries!.Length==1 && model.SourceTables!.ContainsKey("MATL"),"model update retains imported attributes and boundaries without old or incoming results");
        var results=ModelSnapshotUpdate.Apply(current,incoming,ModelUpdateScope.Results);
        Check(results.Results.Single().Name=="Nuovo caso" && results.Results[0].Displacements![0].X==.001 && results.Name==current.Name && results.Source==current.Source && results.UpdateInfo is {ResultsCompatibilityVerified:true,InputSource:"Nuova copia"},"results-only replaces all cases including translations while preserving the model identity and recording result provenance");
        Check(ReferenceEquals(ModelSnapshotUpdate.Apply(current,incoming,ModelUpdateScope.Everything),incoming),"full import retains the exact incoming snapshot and fingerprint contract");
        Check(current.Fingerprint()==before && incoming.Fingerprint()==read,"partial composition does not mutate either source copy");
        var reordered=ModelSnapshot.FromJson(JsonSerializer.Serialize(incoming));Array.Reverse(reordered.Nodes);Array.Reverse(reordered.Elements);reordered.SourceTables!["MATL"]=JsonSerializer.Deserialize<JsonElement>("{\"x\":{\"E\":30000,\"NAME\":\"C30\"}}");
        Check(ModelSnapshotUpdate.Apply(current,reordered,ModelUpdateScope.Results).Results.Length==1,"equivalent table object ordering and numeric spellings remain compatible");
        var changed=ModelSnapshot.FromJson(JsonSerializer.Serialize(incoming));changed.Nodes[0]=changed.Nodes[0] with {X=1e-10};
        Reject(()=>ModelSnapshotUpdate.Apply(current,changed,ModelUpdateScope.Results),"even a small coordinate difference prevents silently attaching results to another geometry");
        changed=ModelSnapshot.FromJson(JsonSerializer.Serialize(incoming));changed.SourceTables!["MATL"]=JsonSerializer.SerializeToElement(new {x=new {NAME="C30",E=31000.0}});
        Reject(()=>ModelSnapshotUpdate.Apply(current,changed,ModelUpdateScope.Results),"material changes reject a results-only update");
        changed=ModelSnapshot.FromJson(JsonSerializer.Serialize(incoming));changed.SourceTables=null;
        Reject(()=>ModelSnapshotUpdate.Apply(current,changed,ModelUpdateScope.Results),"missing attribute evidence cannot prove results compatibility");
        changed=ModelSnapshot.FromJson(JsonSerializer.Serialize(incoming));changed.Boundaries=[new("CONS/1","RESTRAIN",[1],null,"DX")];
        Reject(()=>ModelSnapshotUpdate.Apply(current,changed,ModelUpdateScope.Results),"a changed support prevents stale result association");
        Reject(()=>ModelSnapshotUpdate.Apply(null,incoming,ModelUpdateScope.Results),"results-only needs an existing model");
        Reject(()=>ModelSnapshotUpdate.Apply(current,model,ModelUpdateScope.Results),"empty result source cannot erase the current result set silently");
        using(var canceled=new CancellationTokenSource())
        {canceled.Cancel();try{ModelSnapshotUpdate.Apply(current,incoming,ModelUpdateScope.Geometry,canceled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"partial import observes cancellation before composing a copy");}}
        int imports=0;var service=new FakeServices {Next=incoming};
        using var vm=new ModelViewerViewModel(current,[],false,service,_=>imports++,(_,_,_,_)=>{});
        vm.SelectedUpdateScope=2;vm.SelectedRepresentation=1;vm.SelectedColorMode=1;vm.SelectedColorCategoryKey="PLATE";vm.ChooseCategoryColorCommand.Execute("#BB9FE4");
        await vm.ImportCommand.ExecuteAsync(null);
        Check(imports==1 && vm.Snapshot!.Results.Single().Name=="Nuovo caso" && vm.SelectedCase=="Nuovo caso" && vm.SelectedRepresentation==1 && vm.ElementColors[10].Color=="#BB9FE4","results-only import preserves graphical settings and selects an available new case");
        Check(vm.RevisionReview!=null && vm.RevisionReview.Elements.Length==0 && !vm.HasImportFailure,"successful partial import is integrated into revision review");
        service.Next=changed;string valid=vm.Snapshot!.Fingerprint();await vm.ImportCommand.ExecuteAsync(null);
        Check(imports==1 && vm.Snapshot.Fingerprint()==valid && vm.HasImportFailure && vm.ImportFailure!.Recovery.Contains("Solo risultati"),"incompatible import produces an actionable banner and retains the last valid model");
        service.Next=incoming;vm.SelectedUpdateScope=0;await vm.ImportCommand.ExecuteAsync(null);
        Check(imports==2 && vm.Cases.Count==0 && !vm.HasResults && vm.Snapshot!.Results.Length==0 && vm.UpdateProvenance.Contains("rimossi"),"geometry-only import removes old result availability from selectors, scene and provenance");
        using var empty=new ModelViewerViewModel(null,[],false,service,_=>{},(_,_,_,_)=>{});empty.SelectedUpdateScope=2;
        Check(!empty.ImportCommand.CanExecute(null),"results-only command is unavailable before a model is opened");
        var waiting=new FakeServices{Pending=new(TaskCreationOptions.RunContinuationsAsynchronously)};
        using var delayed=new ModelViewerViewModel(current,[],false,waiting,_=>imports++,(_,_,_,_)=>{});
        var pending=delayed.ImportCommand.ExecuteAsync(null);delayed.ImportCancelCommand.Execute(null);waiting.Pending.SetResult(incoming);await pending;
        Check(imports==2 && ReferenceEquals(delayed.Snapshot,current) && !delayed.HasImportFailure && delayed.Status.Contains("annullata"),"user cancellation discards even a late completed read without a false timeout banner");
    }
}
