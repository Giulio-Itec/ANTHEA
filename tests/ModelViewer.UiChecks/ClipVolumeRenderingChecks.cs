using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelReports;
using HelixToolkit.Wpf.SharpDX;
using Mesh=HelixToolkit.SharpDX.MeshGeometry3D;

static partial class Program
{
    static void ClipVolumeRenderingChecks(ModelViewerControl control,Viewport3DX viewport,string output)
    {
        var planes=new SceneClipSet([new(Vector3.UnitX,.5f),new(-Vector3.UnitX,-1.5f),new(Vector3.UnitY,.25f),new(-Vector3.UnitY,-1.25f),new(Vector3.UnitZ,-1),new(-Vector3.UnitZ,-1)]);
        var triangle=new SurfaceBuilder();triangle.Triangle(new(0,0,0),new(2,0,0),new(0,2,0),0,1,0,7);
        var clipped=planes.Apply(triangle);
        Check(clipped.Positions.Count>0 && clipped.Positions.All(planes.Contains) && clipped.ElementIds.All(id=>id==7),"six-plane triangle clipping preserves the imported element identity and all geometric bounds");
        Check(Enumerable.Range(0,clipped.Positions.Count).All(i=>Math.Abs(clipped.UV[i].X-clipped.Positions[i].X/2)<1e-6),"clipped scalar textures follow the independent field u=x/2 without recomputation");
        var a=new Vector3(-1,.5f,0);var b=new Vector3(3,.5f,0);
        Check(planes.Segment(ref a,ref b) && a==new Vector3(.5f,.5f,0) && b==new Vector3(1.5f,.5f,0),"line clipped by the box keeps its exact crossing segment");
        var outsideA=new Vector3(-1,3,0);var outsideB=new Vector3(3,3,0);
        Check(!planes.Segment(ref outsideA,ref outsideB),"line outside another half-space is completely excluded");
        var polygon=planes.Polygon([new(0,0,0),new(2,0,0),new(2,2,0),new(0,2,0)]);
        Check(polygon.Length==4 && polygon.All(planes.Contains),"rectangle selection follows the clipped polygon");
        var state=(ModelViewerViewModel)control.DataContext;string before=state.Snapshot!.Fingerprint();
        var renderer=(ModelSceneRenderer)control.GetType().GetField("renderer",Flags)!.GetValue(control)!;
        state.SelectedResultType=0;state.SelectedFamily=0;state.SelectedComponent="Myy";state.SelectedCase=state.Cases.Contains("G1")?"G1":state.Cases.First();
        state.ShowResults=true;state.SelectedRepresentation=1;state.ShowInspector=false;state.SelectedPanel=2;state.ShowMesh=false;
        state.ResetClipBoxCommand.Execute(null);state.ClipBoxMaxX=(state.Snapshot.Nodes.Min(n=>n.X)+state.Snapshot.Nodes.Max(n=>n.X))/2;Wait(150);
        var tools=Children<ModelViewToolsView>(control).Single();var expander=Children<Expander>(tools).First(e=>e.Header?.ToString()?.Contains("Taglio e volume")==true);
        expander.IsExpanded=true;Wait(100);expander.BringIntoView();Wait(100);
        Check(state.ActiveClipPlanes.Count==6 && viewport.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Geometry?.Positions?.Count>0),"real model renders extruded contours inside the clipping volume");
        Check(viewport.Items.OfType<MeshGeometryModel3D>().SelectMany(m=>m.Geometry?.Positions??[]).All(renderer.InClip),"every rendered surface vertex lies inside all active clipping planes");
        bool Outline()=>viewport.Items.OfType<LineGeometryModel3D>().Any(l=>l.Color==System.Windows.Media.Color.FromArgb(170,104,203,220) && l.Geometry?.Positions?.Count==24 && !l.IsHitTestVisible);
        Check(Outline(),"volume boundary is a twelve-edge non-pickable outline");
        var bound=state.ActiveClipBox!;SetAppearance("VeryDark");Wait(100);Capture(control,viewport,Path.Combine(output,"spalla-volume-taglio.png"));
        state.ShowClipBox=false;Wait(100);Check(!Outline() && state.ActiveClipPlanes.Count==6,"hiding the box outline leaves the clipping active");
        if(state.Snapshot.Results.Any(r=>r.Displacements?.Length>0))
        {
            state.SelectedResultType=1;state.SelectedCase=state.Cases.Contains("SismaX")?"SismaX":state.Cases.First();state.ShowDeformation=true;
            Check(!state.CanAnimate && state.CanDeform,"static deformations remain available inside the cut while animation is disabled");
        }
        state.ReportTitle="Spalla · volume di esplorazione";state.AddReportFigureCommand.Execute(null);state.SelectedReportChapter!.Title="Vista ritagliata del modello";
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var task=WpfModelViewerServices.ExportReportToAsync(state.CaptureReport(),state.Snapshot,Path.Combine(output,"volume-taglio.pdf"),null,timeout.Token);
        while(!task.IsCompleted)Wait(25);var report=task.GetAwaiter().GetResult();
        var recipe=ModelReportFiles.Read(report.RecipePath);
        Check(report.Figures==1 && recipe.Blocks[0].View is {Clip:true,ClipShape:1} && recipe.Blocks[0].View!.ClipBox==bound with {Outline=false},"report regenerates and persists the actual six-plane volume");
        Check(File.ReadAllText(report.HtmlPath).Contains("anche fuori dal taglio"),"report states that the scalar scale is preserved across clipping");
        state.ClipEnabled=false;state.ShowDeformation=false;state.SelectedResultType=0;SetAppearance("Light");Wait(100);
        Check(!Outline() && state.ActiveClipPlanes.Count==0,"disabling clipping restores the complete model and removes its outline");
        Check(state.Snapshot.Fingerprint()==before,"volume rendering and report preserve every source value");
    }
}
