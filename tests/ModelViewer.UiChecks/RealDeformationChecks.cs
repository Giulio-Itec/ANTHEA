using System.Diagnostics;
using System.IO;
using System.Text.Json;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void RealDeformationChecks(ModelViewerControl control, Viewport3DX viewport, string output)
    {
        var state = (ModelViewerViewModel)control.DataContext;
        var renderer = (ModelSceneRenderer)control.GetType().GetField("renderer", Flags)!.GetValue(control)!;
        Check(state.Snapshot!.Nodes.Length == 3755 && state.Snapshot.Elements.Length == 3616 && state.Cases.Count == 49, "real displacement preview contains the complete abutment and all 49 static cases");
        Check(state.Snapshot.Results.All(r => r.Displacements?.Length == 3755), "each real static case has an imported translation for every node");
        string fingerprint = state.Snapshot.Fingerprint();
        state.SelectedResultType=1; state.SelectedPanel=1; state.ShowInspector=false; state.ShowDeformation=true; state.SelectedRepresentation=1;
        state.SelectedComponent="|u|"; state.ShowMesh=false; state.SelectedQuality=1;
        var changes = new List<object>();
        state.SelectedCase="G2";
        foreach (var name in new[] { "G1", "Q1.1+", "SismaX", "SismaY", "Q1_PARAGHIAIA" })
        {
            var watch = Stopwatch.StartNew(); state.SelectedCase=name;
            double prepared = watch.Elapsed.TotalMilliseconds;
            bool rendered = false; EventHandler frame=(_,_)=>rendered=true; viewport.OnRendered+=frame; viewport.InvalidateRender();
            while(!rendered && watch.ElapsedMilliseconds<5000)Wait(10);
            viewport.OnRendered-=frame;
            Check(rendered && state.CanDeform && state.ActiveValues.Select(v=>v.Element).Distinct().Count()==3616, "all element families display real nodal results for " + name);
            changes.Add(new { Case=name,PreparationMs=prepared,VisibleMs=watch.Elapsed.TotalMilliseconds });
        }
        state.SelectedCase="SismaX"; state.ShowExtrema=true;state.CameraPresetCommand.Execute("iso-ne"); Wait(300); renderer.Fit(); Wait(200);
        SetAppearance("Dark"); Wait(100);
        Capture(control,viewport,Path.Combine(output,"spalla-deformata-scuro.png"));
        SetAppearance("VeryDark"); Wait(100);
        Capture(control,viewport,Path.Combine(output,"spalla-deformata-molto-scuro.png"));
        state.ShowExtrema=false;
        var setup=Stopwatch.StartNew();state.ToggleAnimationCommand.Execute(null);setup.Stop();
        Check(state.IsAnimationPlaying && renderer.AnimationVertexCount>100000,"real extruded abutment animates with reusable geometry buffers");
        int count=renderer.AnimationVertexCount; var frames=new List<double>();
        for(int i=0;i<60;i++)
        {
            var frame=Stopwatch.StartNew();state.AdvanceAnimation(TimeSpan.FromSeconds(1.0/30));frame.Stop();frames.Add(frame.Elapsed.TotalMilliseconds);
        }
        int presented=0,changedFrames=0;double previousPhase=state.DeformationPhase;
        EventHandler framePresented=(_,_)=>{presented++;if(Math.Abs(previousPhase-state.DeformationPhase)>1e-9){changedFrames++;previousPhase=state.DeformationPhase;}};
        viewport.OnRendered+=framePresented;var actual=Stopwatch.StartNew();Wait(2000);actual.Stop();viewport.OnRendered-=framePresented;
        Check(presented>10 && state.IsAnimationPlaying,"real Direct3D host presents the animation while the UI remains responsive");
        state.PauseAnimation();state.ResetDeformationFrameCommand.Execute(null);
        state.ShowTable=true;state.SelectedTableKind=5;Wait(150);
        Check(state.TableRows!.Count==state.ActiveValues.Select(v=>v.Node).Distinct().Count(),"real displacement table lists each imported node once without duplicating shared vertices");
        state.ShowTable=false;state.ShowExtrema=true;
        Check(state.Snapshot.Fingerprint()==fingerprint,"real animation and theme switching preserve all 183995 imported displacement vectors");
        File.WriteAllText(Path.Combine(output,"tempi-deformate.json"),JsonSerializer.Serialize(new {
            Model=state.Snapshot.Name,Nodes=3755,Elements=3616,Cases=49,Changes=changes,
            Animation=new {Vertices=count,SetupMs=setup.Elapsed.TotalMilliseconds,FrameCpuMedianMs=frames.Order().ElementAt(30),FrameCpuP95Ms=frames.Order().ElementAt(57),PresentedFrames=presented,DistinctAnimatedFrames=changedFrames,ElapsedSeconds=actual.Elapsed.TotalSeconds,ObservedAnimationFps=changedFrames/actual.Elapsed.TotalSeconds}
        },new JsonSerializerOptions {WriteIndented=true}));
        SetAppearance("Light");
    }
}
