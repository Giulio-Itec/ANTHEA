using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static void MeasurementChecks()
    {
        var sample=Sample.Model();sample.Nodes[1]=new(2,3,4,0);sample.Nodes[2]=new(3,3,4,2);
        sample.Results=[sample.Results[0] with {Displacements=[new(1,0,0,0),new(2,0,0,12),new(3,0,0,12)]},new("Inviluppo",true,[]) {Displacements=[new(1,0,0,0),new(2,1,1,1)]}];
        var distance=new ModelMeasurement(0,[1,2]);var angle=new ModelMeasurement(1,[1,2,3]);
        var original=ModelMeasurements.Read(sample,distance,0,null);var deformed=ModelMeasurements.Read(sample,distance,1,"SLU 01");
        Check(original.Value==5 && original.Delta==new ModelDirection(3,4,0),"GPC measurement adapter retains the independent 3-4-5 distance and global components");
        Check(deformed.Value==13 && deformed.Delta==new ModelDirection(3,4,12),"real deformed distance follows the independent 5-12-13 triangle");
        Check(Math.Abs(ModelMeasurements.Read(sample,angle,0,null).Value!.Value-90)<1e-10,"three-node measurement uses the middle node as the right-angle vertex");
        Check(!ModelMeasurements.Read(sample,distance,1,"Inviluppo").Available && !ModelMeasurements.Read(sample,distance,1,"missing").Available,"envelope and missing cases do not manufacture a physical deformed measurement");
        Check(!ModelMeasurements.Read(sample,new(0,[1,4]),1,"SLU 01").Available && !ModelMeasurements.Read(sample,new(0,[1,900]),0,null).Available,"missing translations and missing nodes remain explicit unavailable readings");
        var duplicate=Sample.Model();duplicate.Nodes[1]=duplicate.Nodes[0] with {Id=2};
        Check(ModelMeasurements.Read(duplicate,distance,0,null).Value==0 && !ModelMeasurements.Read(duplicate,angle,0,null).Available,"coincident coordinates give zero distance and an undefined angle");
        Reject(()=>ModelMeasurements.Validate(new(true,0,"m",[new(1,[1,2,2])])),"repeated-node angle rejected");
        Reject(()=>ModelMeasurements.Validate(new(true,0,"m",Enumerable.Repeat(distance,21).ToArray())),"measurement count bound protects imported recipes");
        string before=sample.Fingerprint();using var vm=new ModelViewerViewModel(sample,[],false,new FakeServices(),_=>{},(_,_,_,_)=>{});
        vm.StartMeasurementCommand.Execute(null);vm.AddMeasurementNode(1);vm.AddMeasurementNode(1);
        Check(vm.PendingMeasurementNodes.SequenceEqual([1]) && vm.MeasurementMessage.Contains("distinto"),"node picking rejects accidental repeated clicks without losing the first point");
        vm.AddMeasurementNode(2);Check(vm.Measurements.Count==1 && vm.PendingMeasurementNodes.Count==0 && vm.IsMeasuring,"a completed pair is retained while the tool is ready for another pair");
        vm.MeasurementBasis=1;vm.MeasurementUnit="mm";vm.DeformationScale=9999;vm.SelectedDeformationScale=2;vm.DeformationPhase=.1;vm.ShowDeformation=true;
        Check(vm.MeasurementReadings.Single().Value=="13000 mm","measurement units use the real displacement independently of display scale and animation phase");
        var preset=JsonSerializer.Deserialize<ModelViewPreset>(JsonSerializer.Serialize(vm.CaptureView("Misure")))!;ModelViewPresetStore.Validate([preset]);
        vm.ClearMeasurementsCommand.Execute(null);vm.ApplyView(preset);
        Check(vm.Measurements.Single().Nodes.SequenceEqual([1,2]) && vm.MeasurementBasis==1 && vm.MeasurementUnit=="mm" && !vm.IsMeasuring,"saved views restore measurement definitions and units without resuming a picking operation");
        vm.ShowMeasurements=false;Check(vm.MeasurementAnnotations.Count==0 && vm.MeasurementReadings.Count==1,"measurement overlays can be hidden while readings remain available");
        vm.CompareEnabled=true;vm.CompareCase="Inviluppo";
        Check(vm.Comparison!.MeasurementReadings.Single().Available==false,"comparison evaluates each measurement against its own case, preserving unavailable envelope semantics");
        vm.CancelMeasurementCommand.Execute(null);vm.SelectedMeasurementKind=1;vm.MeasurementNodeIds="1 2 3";vm.AddMeasurementIdsCommand.Execute(null);
        Check(vm.Measurements.Count==2 && vm.Measurements.Last().Kind==1,"typed node identifiers add an angle through the same viewmodel command");
        Check(sample.Fingerprint()==before,"measurements and saved views never alter imported coordinates or results");
    }
}
