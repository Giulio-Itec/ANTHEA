using System.Text.Json;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static void ClipVolumeChecks()
    {
        var model=Sample.Model();string before=model.Fingerprint();
        using var vm=new ModelViewerViewModel(model,[],false,new FakeServices(),_=>{},(_,_,_,_)=>{});
        vm.SelectedRepresentation=1;vm.ResetClipBoxCommand.Execute(null);
        Check(vm.ClipEnabled && vm.IsBoxClip && vm.ActiveClipPlanes.Count==6 && vm.ActiveClipPlane==null,"box clipping creates six explicit graphical half-spaces independently of the single-plane state");
        Check(vm.ClipBoxMinX<0 && vm.ClipBoxMaxX>4 && vm.ClipBoxMinZ<-.85,"full box includes imported solid offsets with a conservative margin");
        vm.ClipBoxMinX=.5;vm.ClipBoxMaxX=1.5;vm.ClipBoxMinY=.25;vm.ClipBoxMaxY=1.25;vm.ClipBoxMinZ=-1;vm.ClipBoxMaxZ=1;
        var preset=vm.CaptureView("Volume");ModelViewPresetStore.Validate([preset]);
        Check(preset.ClipBox==new ModelClipBox(.5,1.5,.25,1.25,-1,1,true),"volume limits persist in global metres with outline state");
        vm.ClipEnabled=false;vm.ClipBoxMinX=-9;vm.ApplyView(preset);
        Check(vm.ActiveClipBox==preset.ClipBox && vm.ActiveClipPlanes.Count==6,"saved view restores the exact box and enabled clipping");
        vm.ShowClipBox=false;Check(vm.ActiveClipBox is {Outline:false} && vm.ActiveClipPlanes.Count==6,"box outline can be hidden without disabling the cut");
        vm.ClipBoxMinX=2;
        Check(vm.ActiveClipPlanes.Count==0 && vm.ClipDescription.Contains("non applicato"),"reversed bounds disable clipping with an explicit message, without an inverted volume");
        Reject(()=>ModelViewPresetStore.Validate([vm.CaptureView("Invalid")]),"invalid active volume cannot be persisted in a view or report");
        vm.ClipShape=0;vm.ClipAxis=0;vm.ClipPercent=50;
        Check(vm.ActiveClipPlanes.Count==1 && vm.ActiveClipPlane?.Distance==2,"single-plane behavior remains available after an invalid box");
        ModelViewPresetStore.Validate([vm.CaptureView("Piano")]);
        vm.SelectElement(10);vm.ClipBoxFromSelectionCommand.Execute(null);
        Check(vm.IsBoxClip && vm.ClipBoxMaxX<4 && vm.ClipBoxMinZ<-.85,"selection bounds include the selected plate thickness and offset");
        vm.CompareEnabled=true;
        Check(vm.Comparison!.ActiveClipBox==vm.ActiveClipBox,"comparison receives the same clipping volume");
        vm.CompareEnabled=false;vm.ClipEnabled=false;
        Check(vm.ActiveClipPlanes.Count==0 && vm.ActiveClipBox==null,"turning clipping off clears every half-space and outline");
        Check(model.Fingerprint()==before,"clipping, presets and comparison leave geometry and forces unchanged");
    }
}
