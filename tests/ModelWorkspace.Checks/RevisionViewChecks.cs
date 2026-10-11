using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelReports;
using System.Text.Json;

static partial class Program
{
    static async Task RevisionViewChecks()
    {
        var previous=Sample.Model();previous.Elements=[..previous.Elements,new(14,"BEAM",2,[5,6])];
        var current=ModelSnapshot.FromJson(JsonSerializer.Serialize(previous));
        current.Nodes[1]=current.Nodes[1] with {X=2.25};current.Sections[0]=current.Sections[0] with {Width=.35};
        current.Elements=[..current.Elements.Where(e=>e.Id!=11),new(13,"BEAM",2,[3,6])];
        var corners=current.Results[0].Values.Where(v=>v.Element!=11).ToArray();corners[0]=corners[0] with {Values=[1,30,3,4,5,6,7,8]};current.Results=[current.Results[0] with {Values=corners}];current.Validate();
        string oldKey=previous.Fingerprint(),newKey=current.Fingerprint();
        var services=new RevisionServices {Previous=previous,Next=current};int imports=0;
        using var vm=new ModelViewerViewModel(current,[],false,services,_=>imports++,(_,_,_,_)=>{});
        await vm.ComparePreviousCommand.ExecuteAsync(null);
        Check(vm.RevisionReview!=null && imports==0 && ReferenceEquals(vm.Snapshot,current),"loading a previous copy compares revisions without replacing the current model or writing the project");
        Check(vm.ActiveRevisionOverlay!.Markers.Any(m=>m.Element==13&&m.Change=="Nuovo") && vm.ActiveRevisionOverlay.Markers.Any(m=>m.Element==10&&m.Change=="Geometria") && vm.ActiveRevisionOverlay.Markers.Any(m=>m.Element==12&&m.Change=="Attributi") && vm.ActiveRevisionOverlay.Removed.Single().Element==11,"revision overlays distinguish added, geometry, attribute and deleted elements");
        Check(vm.RevisionDeltaRows.Count==0 && vm.ShowRevisionDetails && !vm.IsRevisionBusy,"revision comparison does not eagerly compute force deltas");
        vm.RevisionFilter=4;Check(vm.RevisionRows.Any(r=>r.Category=="Elemento"&&r.Id=="11") && vm.RevisionRows.All(r=>r.Change=="Cancellato"),"deleted objects have an explicit filtered list");
        vm.SelectedCase="SLU 01";vm.SelectedFamily=0;vm.SelectedAxes=0;vm.SelectedComponent="Myy";await vm.ComputeRevisionDeltaCommand.ExecuteAsync(null);
        Check(vm.RevisionDeltaRows.Count==0 && vm.RevisionDeltaSummary.Contains("esclusi"),"force comparison excludes changed geometry by default");
        vm.IncludeChangedGeometryInDelta=true;
        Check(vm.RevisionDeltaRows.Single(r=>r.Element==10&&r.Node==1).Delta==20,"requested force delta preserves the independent 30 minus 10 equals 20 result");
        vm.ForceUnit="N";Check(vm.RevisionDeltaRows.Single(r=>r.Node==1).Delta==20000,"delta presentation follows shared force units without repeating the comparison");
        vm.ExportRevisionDeltaCommand.Execute(null);Check(services.Csv.Contains("20000") && services.Csv.Contains("Geometria modificata"),"CSV includes numeric differences and geometry-change flags");
        vm.SelectedComponent="Mxx";Check(vm.RevisionDeltaRows.Count==0,"changing the force component clears stale delta rows");
        vm.ShowRemovedRevisionElements=false;var preset=vm.CaptureView("Revisione");ModelViewPresetStore.Validate([preset]);
        vm.ShowRevisionHighlights=false;Check(vm.ActiveRevisionOverlay==null,"revision highlights can be disabled independently of the review data");
        vm.ApplyView(preset);Check(vm.ActiveRevisionOverlay is {ShowRemoved:false} && vm.RevisionReview!=null,"saved revision annotations restore their visibility and preserve the loaded comparison");
        using var restored=new ModelViewerViewModel(current,[],true,new FakeServices(),_=>{},(_,_,_,_)=>{});restored.ApplyView(JsonSerializer.Deserialize<ModelViewPreset>(JsonSerializer.Serialize(preset))!);
        Check(restored.ActiveRevisionOverlay?.PreviousRevision==oldKey && restored.RevisionReview==null && !restored.CanComputeRevisionDelta,"portable annotations retain baseline provenance without pretending to contain prior result arrays");
        vm.AddReportFigureCommand.Execute(null);var report=vm.CaptureReport();ModelReportFiles.ValidateReferences(report,current);
        var different=ModelSnapshot.FromJson(JsonSerializer.Serialize(current));different.Nodes[0]=different.Nodes[0] with {X=.1};
        Reject(()=>ModelReportFiles.ValidateReferences(report,different),"revision figures reject a changed current snapshot until the comparison is recomputed");
        Reject(()=>ModelRevisionOverlay.Validate(preset.RevisionOverlay! with {Removed=[new(999,[new(double.NaN,0,0),new(1,0,0)])]}),"non-finite deleted geometry cannot enter a saved view");
        using var importing=new ModelViewerViewModel(previous,[],false,services,_=>imports++,(_,_,_,_)=>{});
        await importing.ImportCommand.ExecuteAsync(null);
        Check(importing.RevisionReview?.Previous==previous && importing.RevisionReview.Current==current && imports==1,"successful model replacement automatically attaches the exact previous/current comparison");
        Check(previous.Fingerprint()==oldKey && current.Fingerprint()==newKey,"revision review, delta, annotations and reports preserve both source snapshots");
        var pending=new TaskCompletionSource<ModelSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.Reader=token=>pending.Task.WaitAsync(token);
        var work=vm.ComparePreviousCommand.ExecuteAsync(null);
        Check(vm.IsRevisionBusy && !vm.ImportCommand.CanExecute(null),"comparison work prevents a concurrent model replacement");
        vm.ComparePreviousCancelCommand.Execute(null);await work;
        Check(!vm.IsRevisionBusy && vm.ActiveRevisionOverlay?.CurrentRevision==newKey && vm.RevisionMessage.Contains("annullato"),"cancelling acquisition keeps the last successful review and the current model");
    }
    sealed class RevisionServices:IModelViewerServices
    {
        public ModelSnapshot? Previous,Next;public string Csv="";public Func<CancellationToken,Task<ModelSnapshot?>>? Reader;
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token)=>Task.FromResult(Next);
        public Task<ModelSnapshot?> OpenPreviousModelAsync(CancellationToken token)=>Reader?.Invoke(token)??Task.FromResult(Previous);
        public ModelCameraState? CaptureCamera()=>new([8,8,8],[-8,-8,-8],[0,0,1],false,10,42,1.5);
        public void FitView(){} public void SaveImage(){} public void ExportTable(string name,string csv)=>Csv=csv;
    }
}
