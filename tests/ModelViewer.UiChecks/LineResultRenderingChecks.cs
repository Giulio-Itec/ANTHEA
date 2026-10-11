using System.IO;
using System.Windows;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;
using Mesh = HelixToolkit.SharpDX.MeshGeometry3D;

static partial class Program
{
    static void LineResultRenderingChecks(string output)
    {
        string[] families=["BEAM","TRUSS","TENSTR","COMPTR","CABLE","BEAM"];
        var model=new ModelSnapshot {Name="Aste · prova indipendente",Source="Fixture sintetica",
            Nodes=families.SelectMany((_,i)=>new[]{new ModelNode(i*2+1,0,i*.8,0),new ModelNode(i*2+2,4,i*.8,0)}).ToArray(),
            Elements=families.Select((family,i)=>new ModelElement(100+i,family,i==5?2:1,[i*2+1,i*2+2])).ToArray(),
            Sections=[new(1,"Rettangolo","SB",.2,.3,true),new(2,"Profilo non disponibile","UNKNOWN",0,0,false)],
            LocalFrames=Enumerable.Range(0,6).Select(i=>new ModelLocalFrame(100+i,new(1,0,0),new(0,1,0),new(0,0,1))).ToArray()};
        model.Results=[new("Combinazione sintetica",false,[]) {Fields=families.Distinct().SelectMany(f=>ResultFields.Components(f,"LOCAL").Select(component=>new ResultField(f,"LOCAL",component,
            model.Elements.Where(e=>e.Type==f).SelectMany(e=>new ResultValue[]{new(e.Id,e.Nodes[0],-10,0),new(e.Id,0,20,.5),new(e.Id,e.Nodes[1],0,1)}).ToArray()))).ToArray()}];
        string fingerprint=model.Fingerprint();
        using var control=new ModelViewerControl(model,[],false,_=>{},(_,_,_,_)=>{});
        var host=new Window {Title="ANTHEA · diagrammi e contouring delle aste",Content=control,Width=1500,Height=1000,ShowInTaskbar=false,ShowActivated=false};
        host.Show(); Wait(450);
        try
        {
            var state=(ModelViewerViewModel)control.DataContext;var scene=Children<Viewport3DX>(control).Single();
            state.SelectedFamily=1;state.SelectedComponent="My";state.ShowInspector=false;state.ShowContext=false;
            state.ShowBeamDiagram=true;state.ShowBeamDiagramValues=true;state.SelectedPanel=1;
            Mesh Colored()=>(Mesh)scene.Items.OfType<MeshGeometryModel3D>().Single(m=>m.Material is ColorStripeMaterial).Geometry!;
            bool HasDiagram()=>scene.Items.OfType<LineGeometryModel3D>().Any(l=>l.Color==System.Windows.Media.Color.FromRgb(242,194,109)&&l.Geometry?.Positions?.Count>0);
            state.SelectedRepresentation=1;Wait(180);
            var mesh=Colored();
            Check(mesh.Positions!.Min(p=>p.Z)<-.149 && mesh.Positions!.Max(p=>p.Z)>.149 && HasDiagram(), "extruded beam shows its real rectangular section and an independent force diagram");
            Check(Enumerable.Range(0,mesh.Positions!.Count).All(i=>{
                double station=(mesh.Positions![i].X+2)/4;
                double expected=station<=.5?station*2:1-(station-.5)*4/3;
                return Math.Abs(mesh.TextureCoordinates![i].X-expected)<1e-6;
            }),"solid beam UV follows the independent piecewise field -10,20,0 at stations 0,0.5,1");
            Check(scene.Items.OfType<LineGeometryModel3D>().Any(l=>l.Thickness==4&&l.Geometry?.Positions?.Count>0),"unsupported section keeps a colored analytical axis without an invented solid");
            Capture(control,scene,Path.Combine(output,"aste-diagrammi-contouring-estruso.png"));
            state.SelectedRepresentation=0;Wait(150);
            Check(!scene.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Material is ColorStripeMaterial)&&HasDiagram()&&scene.Items.OfType<LineGeometryModel3D>().Any(l=>l.Thickness==4&&l.Geometry?.Positions?.Count>0),
                "wireframe representation combines colored axes and force diagrams");
            Capture(control,scene,Path.Combine(output,"aste-diagrammi-contouring-wireframe.png"));
            state.SelectedRepresentation=1;state.SelectedSurfaceMode=2;Wait(120);
            Check(!scene.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Material is ColorStripeMaterial)&&HasDiagram(),"explicit wireframe style also overrides solid beam representation while retaining results");
            state.SelectedSurfaceMode=0;state.ShowLineContour=false;Wait(120);
            Check(HasDiagram()&&!scene.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Material is ColorStripeMaterial)&&!scene.Items.OfType<LineGeometryModel3D>().Any(l=>l.Thickness==4&&l.Geometry?.Positions?.Count>0),"diagram-only works on neutral solids with no residual contour colors");
            state.ShowBeamDiagram=false;state.ShowLineContour=true;Wait(120);
            Check(!HasDiagram()&&Colored().Positions!.Count>0,"contour-only removes the diagram independently");
            state.ShowBeamDiagram=true;state.SelectedBeamLocation=3;Wait(120);
            Check(!scene.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Material is ColorStripeMaterial)&&HasDiagram(),"a midpoint-only sample draws a cross-section marker and ordinate, never a whole-span result");
            state.SelectedBeamLocation=0;state.SelectedBeamDiagramAxis=1;state.SelectedComponent="My";Wait(120);
            var diagram=scene.Items.OfType<LineGeometryModel3D>().Single(l=>l.Color==System.Windows.Media.Color.FromRgb(242,194,109));
            Check(diagram.Geometry!.Positions!.All(p=>Math.Abs(p.Z)<1e-6),"explicit local Y diagram stays in the XY plane");
            state.SelectedBeamDiagramAxis=0;state.SelectedComponent="Mz";Wait(120);
            Check(state.BeamDiagramAlongY,"automatic Mz diagram uses local Y");
            for(int family=2;family<=5;family++)
            {
                state.SelectedFamily=family;Wait(100);
                Check(state.SelectedComponent=="Fx"&&Colored().Positions!.Count>0&&HasDiagram(),"axial family supports solid contour and diagram: "+state.Family);
            }
            state.SelectedFamily=1;state.SelectedComponent="My";state.ShowResults=false;Wait(100);
            Check(!HasDiagram()&&!scene.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Material is ColorStripeMaterial),"global result switch hides both representations without hiding geometry");
            Check(model.Fingerprint()==fingerprint,"all line display choices preserve source stations and values");
        }
        finally {host.Close();}
    }

    static void CaptureRealBeamResults(ModelViewerControl control,Viewport3DX scene,string output)
    {
        var state=(ModelViewerViewModel)control.DataContext; state.ShowResults=true;state.SelectedFamily=1;state.SelectedComponent="My";
        state.ShowBeamDiagram=true;state.ShowLineContour=true;state.ShowBeamDiagramValues=false;state.BeamDiagramScale=.5;
        state.SelectedRepresentation=1;state.SelectedPanel=1;state.ShowContext=false;Wait(180);
        state.FitCommand.Execute(null);Wait(200);
        Check(state.HasResults&&state.Status.StartsWith("Scena pronta"),"real imported beam stations render diagrams and solid contour");
        Capture(control,scene,Path.Combine(output,"spalla-aste-diagrammi-contouring-estruso.png"));
        state.SelectedRepresentation=0;Wait(150);Capture(control,scene,Path.Combine(output,"spalla-aste-diagrammi-contouring-wireframe.png"));
    }
}
