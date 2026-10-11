using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static async Task ImportFailureChecks()
    {
        var model=Sample.Model();int commits=0;var services=new FakeServices();
        using var vm=new ModelViewerViewModel(model,[],false,services,_=>commits++,(_,_,_,_)=>{});
        foreach(var kind in Enum.GetValues<ModelSourceFailureKind>())
        {
            services.Failure=new ModelSourceException(kind);await vm.ImportCommand.ExecuteAsync(null);
            Check(vm.HasImportFailure&&!vm.IsBusy&&ReferenceEquals(vm.Snapshot,model)&&commits==0&&vm.ImportFailure!.Recovery.Length>20,
                "source failure "+kind+" explains recovery and retains the previous valid model without partial commit");
        }
        Check(ModelSourceException.FromResponse(404,"{\"error\":{\"message\":\"client does not exist\"}}").Kind==ModelSourceFailureKind.SessionMissing,
            "real MIDAS client-missing response maps to reconnect advice");
        Check(ModelSourceException.FromResponse(400,"{\"error\":{\"message\":\"There is no document opened\"}}").Kind==ModelSourceFailureKind.DocumentClosed,
            "closed-document response differs from expired session");
        Check(ModelSourceException.FromResponse(403,"secret-token-not-to-display").Kind==ModelSourceFailureKind.AccessDenied&&
            !ModelSourceException.FromResponse(500,"secret-token-not-to-display").Message.Contains("secret"),"untrusted error bodies and credentials never become user-facing messages");
        vm.DismissImportFailureCommand.Execute(null);Check(!vm.HasImportFailure,"error banner is dismissible without changing model");
        services.Failure=null;services.Next=Sample.Model();await vm.ImportCommand.ExecuteAsync(null);
        Check(!vm.HasImportFailure&&commits==1,"successful retry clears failure and commits exactly once");
    }
    static void ViewSettingsAndRevisionChecks()
    {
        using var vm=new ModelViewerViewModel(Sample.Model(),[],false,new FakeServices(),_=>{},(_,_,_,_)=>{});
        vm.ApplyBackgroundCommand.Execute("#FFFFFF");Check(vm.SceneInk=="#24354A","light background gets dark scene labels");
        vm.ApplyBackgroundCommand.Execute("invalid");Check(vm.SceneBackground=="#FFFFFF"&&vm.ViewSettingsMessage.Length>0,"invalid custom color retains the previous valid background");
        vm.ResetViewSettingsCommand.Execute(null);Check(vm.SceneBackground=="#131D2B"&&!vm.CameraInertia&&vm.RotationSensitivity==.5&&!vm.ShowIsolines,"reset restores controlled rotation and smooth contours");
        var before=Sample.Model();var after=Sample.Model();after.Nodes[0]=after.Nodes[0] with{X=2e-7};
        Check(ModelRevisionComparison.Compare(before,after).Elements.Length==0,"sub-micrometre coordinate noise does not flag a revision");
        after.Nodes[0]=after.Nodes[0] with{X=.01};after.Plates[0]=after.Plates[0] with{Thickness=.4};
        var revision=ModelRevisionComparison.Compare(before,after);
        Check(revision.Elements.Single(e=>e.Id==10) is {Geometry:true,Attributes:true}&&revision.Elements.Single(e=>e.Id==11) is {Geometry:false,Attributes:true},"revision distinguishes moved geometry and changed property definitions");
        after.Results[0].Values[0].Values[1]=13;
        var delta=ModelRevisionComparison.CompareResults(revision,"SLU 01","PLATE","LOCAL","Myy");
        Check(delta.Rows.Single(v=>v.Element==10&&v.Node==1) is {Delta:3,GeometryChanged:true}&&delta.UnmatchedCurrent==0,"requested field delta preserves sample identity and flags changed geometry");
        var remove=Sample.Model();remove.Elements=remove.Elements.Where(e=>e.Id!=12).ToArray();
        Check(ModelRevisionComparison.Compare(before,remove).Elements.Single() is {Id:12,Removed:true},"deleted element remains explicit in revision review");
        before.Boundaries=[new("RIGD/1","CONSTRAINT",[1,2],null,"DX"),new("RIGD/1","CONSTRAINT",[1,3],null,"DX")];
        after=Sample.Model();after.Boundaries=before.Boundaries.Reverse().ToArray();
        Check(ModelRevisionComparison.Compare(before,after).Elements.Length==0,"one rigid-link source record can represent many links and order does not create changes");
    }
}
