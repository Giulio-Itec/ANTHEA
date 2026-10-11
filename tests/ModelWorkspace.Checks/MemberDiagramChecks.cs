using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static void MemberDiagramChecks()
    {
        var model=Sample.Model();
        model.Elements=[..model.Elements,new(13,"TRUSS",2,[3,6])];
        model.Results=[model.Results[0] with {Fields=[..ResultFields.Components("BEAM","LOCAL").Select(c=>new ResultField("BEAM","LOCAL",c,[new(12,1,-10,0),new(12,0,20,.5),new(12,4,0,1)])),new("TRUSS","LOCAL","Fx",[new(13,0,-30,.5)])]}];
        model.Validate();string fingerprint=model.Fingerprint();
        var series=MemberDiagramSeries.Read(model,12,"SLU 01","kN","m");
        Check(series.Length==6 && series.Single(s=>s.Component=="My").Unit=="kN·m","member diagram exposes six imported local components with correct units");
        var my=series.Single(s=>s.Component=="My");
        Check(my.At(.25)==(5.0,false) && my.At(.75)==(10.0,false) && my.At(.5)==(20.0,true),"independent -10,20,0 plot gives 5 and 10 by graphical interpolation and identifies its imported peak");
        var partial=new MemberDiagramSeries("Fx","kN",[new(.25,-4,0),new(.75,8,0)]);
        Check(partial.At(0).Value==null && partial.At(1).Value==null && partial.At(.5).Value==2,"plot never extrapolates beyond available stations");
        var ambiguous=new MemberDiagramSeries("Fx","kN",[new(0,1,1),new(.5,2,0),new(.5,3,0),new(1,4,2)]);
        Check(ambiguous.HasAmbiguousStations && ambiguous.At(.5).Value==null && ambiguous.At(.25).Value==null && ambiguous.At(0).Value==1,"conflicting station values remain ambiguous without invented averages or connections");
        var axial=MemberDiagramSeries.Read(model,13,"SLU 01","kN","m").Single();
        Check(axial.At(.5)==(-30.0,true) && axial.At(0).Value==null,"single truss sample remains punctual, with no full-span value invented");
        Check(MemberDiagramSeries.Read(model,12,"SLU 01","N","mm").Single(s=>s.Component=="My").Maximum==20_000_000,"moment diagram converts kN m to N mm without changing source samples");
        using var vm=new ModelViewerViewModel(model,[],false,new FakeServices(),_=>{},(_,_,_,_)=>{});
        vm.SelectElement(12);vm.OpenMemberDiagramCommand.Execute(null);vm.SelectedComponent="My";
        Check(vm.ShowMemberDiagram && vm.MemberDiagramElementId==12 && vm.MemberDiagramSeries.Count==1 && vm.SelectedPanel==1,"selected member opens its diagram from the shared result settings");
        vm.SelectedBeamLocation=3;
        Check(vm.ActiveValues.Count==1 && vm.MemberDiagramSeries.Single().Samples.Length==3,"detail plot retains all imported stations independently of the 3D station filter");
        vm.MemberDiagramStation=.25;
        Check(vm.MemberDiagramReadings.Single().Value=="5" && vm.MemberDiagramReadings.Single().Origin=="Interpolazione grafica","cursor reading explicitly distinguishes graphical interpolation");
        vm.MemberDiagramAllComponents=true;
        Check(vm.MemberDiagramSeries.Count==6 && vm.MemberDiagramColumns==3,"all six component diagrams share one station cursor");
        vm.ShowTable=true;Check(!vm.ShowMemberDiagram,"table replaces the diagram drawer without stacking duplicate lower panels");
        vm.ShowMemberDiagram=true;Check(!vm.ShowTable,"diagram replaces the table drawer");
        vm.HideCommand.Execute(null);
        Check(vm.MemberDiagramElementId==null && !vm.HasMemberDiagram,"hiding the plotted member clears the plot instead of leaving stale values");
        vm.MemberDiagramStation=double.NaN;Check(vm.MemberDiagramStation==.5,"invalid cursor input restores a finite station");
        Check(model.Fingerprint()==fingerprint,"member plots, units and cursor preserve all engineering data");
    }
}
