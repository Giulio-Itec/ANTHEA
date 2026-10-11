using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static void ExplorationChecks(string output)
    {
        ScreenPoint[] square=[new(0,0),new(10,0),new(10,10),new(0,10)];
        Check(ScreenSelection.Contains(square,new(0,5))&&!ScreenSelection.Contains(square,new(11,5)), "selection includes its boundary and excludes outside points");
        Check(ScreenSelection.Matches(square,[new(-5,5),new(15,5)],true)&&!ScreenSelection.Matches(square,[new(-5,5),new(15,5)],false),"crossing selection catches an intersecting line with both endpoints outside");
        Check(ScreenSelection.Matches(square,[new(-5,-5),new(15,-5),new(15,15),new(-5,15)],true),"crossing selection catches a face enclosing the entire rectangle");
        Check(!ScreenSelection.Matches(square,[new(double.NaN,5)],true)&&!ScreenSelection.Matches(square,[],false),"invalid projected shapes cannot be selected");
        var model=AveragingSample();var services=new ViewServices(Path.Combine(output,"saved-views"));
        string fingerprint=model.Fingerprint();
        using var vm=new ModelViewerViewModel(model,[],false,services,_=>{},(_,_,_,_)=>{});
        vm.SelectionIds="10-11 999";vm.SelectByIdsCommand.Execute(null);
        Check(vm.SelectedElementIds.SetEquals([10,11]),"ID selection ignores absent IDs and expands explicit ranges");
        vm.SelectionIds="11-10";vm.SelectByIdsCommand.Execute(null);
        Check(vm.SelectedElementIds.SetEquals([10,11]),"invalid ID range preserves selection");
        vm.SelectElements([11],subtract:true);
        Check(vm.SelectedElementIds.SetEquals([10])&&vm.SelectedTreeItem?.Table=="ELEM","subtracting selection restores the sole element inspector");
        vm.SelectionTarget=2;vm.SelectElements([10,12]);
        Check(vm.SelectedElementIds.SetEquals([12]),"line-family selection excludes plates");
        vm.SelectionTarget=1;vm.SelectNodes([1,2]);
        Check(vm.SelectedNodeIds.SetEquals([1,2])&&vm.SelectionCount.Contains("nodi"),"nodal selection has independent IDs and user feedback");
        vm.ClearSelectionCommand.Execute(null);
        Check(vm.SelectedNodeIds.Count==0&&vm.SelectedElementIds.Count==0,"clear selection removes both nodes and elements");
        vm.SelectionTarget=0;vm.SelectElements([10,11,12]);vm.HideCommand.Execute(null);
        Check(vm.VisibleElementIds?.Count==0,"hiding all elements creates an explicit empty scene");
        vm.PreviousVisibilityCommand.Execute(null);
        Check(vm.VisibleElementIds==null,"previous visibility distinguishes an empty scene from all elements");
        vm.SelectedContourValues=1;vm.ResultSelectionMinimum="-91";vm.ResultSelectionMaximum="-89";vm.SelectByResultRangeCommand.Execute(null);
        Check(vm.SelectedElementIds.SetEquals([10,11]),"range selection follows the averaged field at shared vertices");
        vm.SelectedContourValues=0;vm.SelectByResultRangeCommand.Execute(null);
        Check(vm.SelectedElementIds.Count==0,"range selection follows the restored original discontinuity");
        vm.ClipEnabled=true;vm.ClipAxis=0;vm.ClipPercent=50;
        Check(vm.ActiveClipPlane==new GraphicPlane(1,0,0,3),"axis clipping uses the model coordinate range");
        vm.ClipAxis=3;vm.ClipNodes="1 2 3";
        Check(vm.ActiveClipPlane is {Z:1,Distance:0},"three non-collinear imported nodes define the graphic plane");
        vm.ClipNodes="1 1 2";
        Check(vm.ActiveClipPlane==null,"invalid three-node plane is rejected without hiding geometry");
        vm.CompareEnabled=true;vm.CompareCase=vm.SelectedCase;vm.CompareComponent="Myy";vm.CompareAveraging=1;vm.SelectedContourValues=0;
        Check(vm.Comparison!.IsAveragedContour&&vm.Comparison.ActiveValues.SequenceEqual(vm.ActiveValues)&&vm.Comparison.ContourValues.Min(v=>v.Value)==-90,"comparison has independent averaging over the same original data");
        Check(vm.Comparison.ColorMinimum==-200&&vm.ColorMinimum==-200&&vm.Comparison.ColorMaximum==vm.ColorMaximum,"common comparison scale includes extrema from both rendered fields");
        vm.CompareComponent="Fxx";
        Check(!vm.CanShareComparisonScale&&vm.Comparison.ColorMinimum!=vm.ColorMinimum,"incompatible engineering units retain separate scales");
        vm.CompareEnabled=false;vm.ClipEnabled=false;vm.SelectedContourValues=1;vm.SelectedColorScale=3;
        vm.ColorScaleLower="-50";vm.ColorScaleUpper="50";vm.ApplyColorScaleCommand.Execute(null);
        vm.SelectElement(10);vm.IsolateCommand.Execute(null);vm.ClipEnabled=true;vm.ClipAxis=0;vm.ClipPercent=30;
        vm.ViewName="Parete";vm.SaveViewCommand.Execute(null);
        Check(vm.SavedViews.Count==1&&File.Exists(Path.Combine(output,"saved-views",fingerprint+".json")),"view preferences persist separately under the exact revision");
        vm.ShowAllCommand.Execute(null);vm.SelectedContourValues=0;vm.SelectedColorScale=0;vm.ClipEnabled=false;vm.SelectionTarget=1;
        vm.RecallViewCommand.Execute(null);
        Check(vm.VisibleElementIds!.SetEquals([10])&&vm.SelectedElementIds.SetEquals([10])&&vm.IsAveragedContour&&vm.ColorMinimum==-50&&vm.ColorMaximum==50&&vm.ClipEnabled&&vm.ClipPercent==30&&services.Restored!=null,"view recall restores selection, visibility, field, manual scale, clipping and camera");
        using(var reopened=new ModelViewerViewModel(model,[],false,services,_=>{},(_,_,_,_)=>{}))
            Check(reopened.SavedViews.Count==1,"saved views survive a new viewmodel");
        Check(model.Fingerprint()==fingerprint,"exploration and persistence leave the engineering snapshot unchanged");
        var store=new ModelViewPresetStore(Path.Combine(output,"reject-views"));var preset=vm.SavedViews[0];
        Reject(()=>store.Write("../escape",[preset]),"view store refuses path traversal");
        Reject(()=>store.Write(fingerprint,[preset with {Camera=new([0,0,1],[0,0,-1],[0,0,1],false,1,45)}]),"camera cannot restore parallel look/up axes");
        Reject(()=>store.Write(fingerprint,[preset with {Opacity=double.NaN}]),"corrupted view opacity is rejected");
        Reject(()=>store.Write(fingerprint,[preset with {Component=null!}]),"corrupted view text is rejected");
        vm.DeleteViewCommand.Execute(null);
        Check(services.Store.Read(fingerprint).Length==0,"deleting a saved view persists its removal");

        var loadModel=Sample.Model();
        loadModel.SourceTables=new()
        {
            ["UNIT"]=JsonSerializer.SerializeToElement(new { _=0 }),
            ["CNLD"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["1"]=new { ITEMS=new object[]{new { LCNAME="N",FX=1000,FY=0,FZ=-2000,MX=3000,MY=0,MZ=0 }}}})
        };
        loadModel.SourceTables["UNIT"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["1"]=new {FORCE="N",DIST="MM"}});
        var loads=MidasGraphicalLoads.Read(loadModel);
        Check(loads.Loads.Length==2&&loads.Loads.Single(l=>!l.Moment).Vector==new ModelDirection(1,0,-2)&&loads.Loads.Single(l=>l.Moment).Vector.X==.003,"glyph adapter converts explicit global nodal forces and moments independently");
        loadModel.SourceTables["CNLD"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["1"]=new {ITEMS=new object[]{"invalid"}}});
        Check(MidasGraphicalLoads.Read(loadModel).Unsupported.Values.Sum()==1,"malformed optional load records are reported, not rendered");
        var undeformed=Sample.Model();string old=undeformed.Fingerprint();
        var roundtrip=ModelSnapshot.FromJson(JsonSerializer.Serialize(undeformed));
        Check(roundtrip.Fingerprint()==old&&!JsonSerializer.Serialize(undeformed).Contains("Displacements"),"absent displacements preserve legacy snapshots");
        undeformed.Results[0]=undeformed.Results[0] with {Displacements=[new(1,0,0,.01)]};undeformed.Validate();
        using var deformed=new ModelViewerViewModel(undeformed,[],false,new FakeServices(),_=>{},(_,_,_,_)=>{});
        Check(deformed.HasDisplacements&&deformed.Displacements[0].Z==.01,"deformation is available only from imported displacements");
        undeformed.Results[0]=undeformed.Results[0] with {Displacements=[new(999,0,0,.01)]};
        Reject(undeformed.Validate,"displacements cannot target absent nodes");
    }
    sealed class ViewServices(string directory):IModelViewerServices
    {
        public ModelViewPresetStore Store {get;}=new(directory);
        public ModelCameraState? Restored;
        public Task<ModelSnapshot?> ImportAsync(CancellationToken cancellationToken)=>Task.FromResult<ModelSnapshot?>(null);
        public void FitView(){} public void SaveImage(){} public void ExportTable(string name,string csv){}
        public ModelCameraState CaptureCamera()=>new([0,0,10],[0,0,-10],[0,1,0],false,10,45);
        public void RestoreCamera(ModelCameraState camera)=>Restored=camera;
        public IReadOnlyList<ModelViewPreset> LoadViews(string revision)=>Store.Read(revision);
        public void SaveViews(string revision,IReadOnlyList<ModelViewPreset> views)=>Store.Write(revision,views);
    }
}
