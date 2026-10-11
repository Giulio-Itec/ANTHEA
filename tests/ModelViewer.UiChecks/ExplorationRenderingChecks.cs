using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void ExplorationRenderingChecks(string output)
    {
        var triangle=new SurfaceBuilder();
        triangle.Triangle(new(-1,0,0),new(1,0,0),new(0,1,0),-1,1,0,42);
        var plane=new SceneClip(Vector3.UnitX,0);var cut=plane.Apply(triangle);
        Check(cut.Positions.Count==3&&cut.ElementIds.All(id=>id==42)&&cut.Positions.All(p=>p.X>=0),"clipping keeps the half triangle and original picking ID");
        Check(cut.Positions.Zip(cut.UV).All(p=>Math.Abs(p.First.X-p.Second.X)<1e-6),"clipping interpolates the scalar texture coordinates at new vertices");
        var a=new Vector3(-2,0,0);var b=new Vector3(3,0,0);
        Check(plane.Segment(ref a,ref b)&&a==Vector3.Zero&&b.X==3,"clipped segment has the independently expected endpoint");
        var sample=Sample.Model();
        sample.Nodes=sample.Nodes.Select(n=>n with {X=n.X+1_000_000_000.25,Y=n.Y+1_000_000_000.25}).ToArray();
        sample.LocalFrames=[new(10,new(1,0,0),new(0,1,0),new(0,0,1)),new(11,new(1,0,0),new(0,1,0),new(0,0,1))];
        sample.Results=[new("A",false,sample.Elements.Where(e=>e.Type=="PLATE").SelectMany(e=>e.Nodes.Select(n=>new CornerResult(e.Id,n,[1,e.Id==10?10:30,3,4,5,6,7,8]))).ToArray())
            {Displacements=sample.Nodes.Select(n=>new ModelDisplacement(n.Id,0,0,.1)).ToArray()}];
        sample.SourceTables=new()
        {
            ["UNIT"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["1"]=new {FORCE="KN",DIST="M"}}),
            ["CNLD"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["1"]=new {ITEMS=new object[]{new {LCNAME="Peso",FX=0,FY=0,FZ=-10}}}})
        };
        string fingerprint=sample.Fingerprint();
        using var control=new ModelViewerControl(sample,[],false,_=>{},(_,_,_,_)=>{});
        var host=new Window {Title="ANTHEA · collaudo esplorazione",Content=control,Width=1500,Height=950,ShowInTaskbar=false,ShowActivated=false};
        host.Show();Wait(350);
        try
        {
            var state=(ModelViewerViewModel)control.DataContext;state.SelectedPanel=2;state.SelectedMode=0;state.ShowInspector=false;state.SelectedPanel=2;
            var viewport=Children<Viewport3DX>(control).Single();
            var renderer=(ModelSceneRenderer)control.GetType().GetField("renderer",Flags)!.GetValue(control)!;
            state.CameraPresetCommand.Execute("top");Wait(300);
            state.ClipEnabled=true;state.ClipAxis=0;state.ClipPercent=60;Wait(100);
            Check(viewport.Items.OfType<MeshGeometryModel3D>().SelectMany(m=>m.Geometry!.Positions!).All(p=>p.X>=.39999),"GPU clipping preserves local precision with billion-metre model coordinates");
            Check(renderer.SelectionShape(sample.Elements[0]).Count==0&&renderer.SelectionShape(sample.Elements[1]).All(p=>p.X>=.39999),"screen selection ignores hidden halves of clipped elements");
            state.SelectionTool=1;
            Call(control,"FinishSelection",new List<Point>{new(0,0),new(viewport.ActualWidth,0),new(viewport.ActualWidth,viewport.ActualHeight),new(0,viewport.ActualHeight)},true);
            Check(state.SelectedElementIds.SetEquals([11]),"rectangle selection operates on the visible clipped face only");
            state.ClipEnabled=false;state.SelectNodes([5]);
            Check(viewport.Items.OfType<LineGeometryModel3D>().Any(l=>l.Color==Color.FromRgb(255,207,96)),"selected nodes have their own visible glyph");
            state.ClearSelectionCommand.Execute(null);state.SelectedPanel=1;state.SelectedContourValues=1;
            state.HoverProbe=state.ProbeFor(state.ContourValues.First(),"Campione vicino · ");state.PinProbeCommand.Execute(null);
            Check(state.PinnedProbes.Count==1&&viewport.Items.OfType<BillboardTextModel3D>().Any(),"pinned samples render through the viewmodel command");
            state.ClearProbesCommand.Execute(null);state.ShowExtrema=true;Wait(300);
            var labels=renderer.LabelBounds.ToArray();
            Check(labels.Length==2&&!labels[0].IntersectsWith(labels[1]),"extreme callouts reserve non-overlapping screen rectangles");
            state.ShowExtrema=false;state.ContourBands=1;
            var stripe=(ColorStripeMaterial)viewport.Items.OfType<MeshGeometryModel3D>().First(m=>m.Material is ColorStripeMaterial).Material!;
            var colors=stripe.ColorStripeX!.ToArray();
            Check(colors.Distinct().Count()==8,"discrete contour material uses eight distinct bands");
            state.ReversePalette=true;
            stripe=(ColorStripeMaterial)viewport.Items.OfType<MeshGeometryModel3D>().First(m=>m.Material is ColorStripeMaterial).Material!;
            Check(stripe.ColorStripeX!.First()==colors.Last(),"reversing the palette reverses the displayed band colors");
            var opaque=viewport.RenderBitmap()!;
            state.SurfaceOpacity=.4;Wait(120);
            var transparent=viewport.RenderBitmap()!;
            byte[] Pixels(BitmapSource bitmap) {var format=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);var bytes=new byte[format.PixelWidth*format.PixelHeight*4];format.CopyPixels(bytes,format.PixelWidth*4,0);return bytes;}
            var firstPixels=Pixels(opaque);var secondPixels=Pixels(transparent);
            Check(firstPixels.Zip(secondPixels).Count(pair=>Math.Abs(pair.First-pair.Second)>5)>500,"opacity visibly blends the contour with its background");
            Check(viewport.Items.OfType<MeshGeometryModel3D>().All(m=>m.IsTransparent),"surface opacity activates transparent rendering");
            state.SurfaceOpacity=1;state.ContourBands=0;state.ReversePalette=false;
            state.CompareEnabled=true;state.CompareCase="A";state.CompareComponent="Myy";state.CompareAveraging=0;Wait(250);
            var comparison=Children<Viewport3DX>(control).Single(v=>v!=viewport);
            Wait(500);
            File.WriteAllText(Path.Combine(output,"pose-sintetica.json"),JsonSerializer.Serialize(new {
                Source=renderer.CameraState(),ComparisonPosition=comparison.Camera?.Position.ToString(),ComparisonLook=comparison.Camera?.LookDirection.ToString(),
                SourcePosition=viewport.Camera?.Position.ToString(),SourceLook=viewport.Camera?.LookDirection.ToString(),
                SameObject=ReferenceEquals(viewport.Camera,comparison.Camera),Legends=Children<ModelContourLegend>(control).Count()}));
            Check(!ReferenceEquals(viewport.Camera,comparison.Camera)&&viewport.Camera!.Position==comparison.Camera!.Position&&viewport.Camera.LookDirection==comparison.Camera.LookDirection&&Children<ModelContourLegend>(control).Count()==2,"side-by-side fields synchronize pose using independent camera clocks and two legends");
            Check(Math.Abs(viewport.ActualWidth-comparison.ActualWidth)<2,"comparison reserves equal drawing areas beside both legends");
            state.CameraPresetCommand.Execute("iso-ne");Wait(400);
            Check(viewport.Camera!.LookDirection==comparison.Camera!.LookDirection&&viewport.Camera.Position==comparison.Camera.Position,"animated orientation changes synchronize the comparison camera");
            Check(state.ColorMinimum==10&&state.Comparison!.ColorMaximum==30,"common legend remains consistent across original and averaged fields");
            string path=Path.Combine(output,"confronto-export-1600.png");
            double beforeWidth=viewport.RenderHost!.ActualWidth,beforeHeight=viewport.RenderHost.ActualHeight;
            state.ExportWidth=1600;ViewerImageExporter.SaveScene(viewport,comparison,state,path);
            var bitmap=new BitmapImage(new Uri(path));
            Check(bitmap.PixelWidth==1600&&new FileInfo(path).Length>15000,"high-resolution export captures both GPU views with headings and legends");
            Check(viewport.BackgroundColor==(Color)ColorConverter.ConvertFromString(state.SceneBackground),"print export restores the onscreen background");
            Check(System.Windows.Data.BindingOperations.IsDataBound(viewport,Viewport3DX.BackgroundColorProperty),"print export preserves the background binding for later view settings");
            Check(Math.Abs(viewport.RenderHost.ActualWidth-beforeWidth)<2&&Math.Abs(viewport.RenderHost.ActualHeight-beforeHeight)<2,"high-resolution export restores physical viewport dimensions at the active Windows DPI");
            state.CompareEnabled=false;state.ShowDeformation=true;state.DeformationScale=2;
            var deformed=viewport.Items.OfType<LineGeometryModel3D>().Single(l=>l.Color==Color.FromRgb(247,120,190));
            Check(deformed.Geometry!.Positions!.All(p=>Math.Abs(p.Z-.2)<1e-6),"deformed wireframe uses imported displacement times the chosen display factor");
            state.ShowLoads=true;
            Check(viewport.Items.OfType<LineGeometryModel3D>().Any(l=>l.Color==Color.FromRgb(91,210,147)&&l.Geometry!.Positions!.Count>0),"explicit imported nodal load produces a directional glyph");
            state.ShowLoads=false;state.ShowDeformation=false;state.SelectedPanel=2;Wait(150);
            Capture(control,viewport,Path.Combine(output,"esplorazione-sintetica.png"));
            Check(sample.Fingerprint()==fingerprint,"all GPU exploration options preserve the imported engineering snapshot");
        }
        finally {host.Close();}
    }

    static void CaptureExplorationOnRealModel(ModelViewerControl control,Viewport3DX viewport,string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;
        state.ShowInspector=false;state.IsNavigationCollapsed=false;state.SelectedPanel=1;
        state.ShowAllCommand.Execute(null);state.ClearSelectionCommand.Execute(null);
        state.SelectedFamily=0;state.SelectedAxes=0;state.SelectedComponent="Myy";state.SelectedContourValues=1;state.SelectedColorScale=0;
        state.ShowMesh=false;state.ShowExtrema=true;state.ShowContext=true;
        state.DisableOverlaysCommand.Execute(null);state.CameraPresetCommand.Execute("iso-ne");Wait(400);
        Capture(control,viewport,Path.Combine(output,"vista-risultati.png"));
        state.CompareEnabled=true;state.CompareCase=state.SelectedCase;state.CompareComponent="Mxx";state.CompareAveraging=1;Wait(750);
        state.ExportWidth=2400;
        var comparison=Children<Viewport3DX>(control).Single(v=>v!=viewport);
        viewport.SaveScreen(Path.Combine(output,"confronto-nativo.png"));
        var renderer=(ModelSceneRenderer)control.GetType().GetField("renderer",Flags)!.GetValue(control)!;
        var projected=state.Snapshot!.Nodes.Select(n=>renderer.Project(renderer.PointForNode(n.Id))).ToArray();
        var local=state.Snapshot.Nodes.Select(n=>renderer.PointForNode(n.Id)).ToArray();
        File.WriteAllText(Path.Combine(output,"camera.json"),JsonSerializer.Serialize(new {Camera=renderer.CameraState(),Width=viewport.ActualWidth,Height=viewport.ActualHeight,
            Minimum=new[]{local.Min(p=>p.X),local.Min(p=>p.Y),local.Min(p=>p.Z)},Maximum=new[]{local.Max(p=>p.X),local.Max(p=>p.Y),local.Max(p=>p.Z)},
            Screen=new[]{projected.Min(p=>p.X),projected.Min(p=>p.Y),projected.Max(p=>p.X),projected.Max(p=>p.Y)}},new JsonSerializerOptions {WriteIndented=true}));
        Check(projected.All(p=>p.X>=0&&p.Y>=0&&p.X<=viewport.ActualWidth&&p.Y<=viewport.ActualHeight),"comparison framing keeps the whole analytical model inside the native viewport");
        ViewerImageExporter.SaveScene(viewport,comparison,state,Path.Combine(output,"confronto-spalla.png"));
        Check(state.Status.StartsWith("Scena pronta")&&state.Comparison!.HasResults,"real-model comparison renders two available components");
        bool previousProjection=state.IsOrthographic;
        state.IsOrthographic=true;renderer.Focus(state.Snapshot.Elements.Select(e=>e.Id).ToHashSet());Wait(300);
        var orthographicPoints=state.Snapshot.Nodes.Select(n=>renderer.Project(renderer.PointForNode(n.Id))).ToArray();
        File.WriteAllText(Path.Combine(output,"orthographic.json"),JsonSerializer.Serialize(new {Camera=renderer.CameraState(),
            OtherType=comparison.Camera?.GetType().Name,OtherWidth=(comparison.Camera as OrthographicCamera)?.Width,
            ActualType=viewport.Camera?.GetType().Name,ViewportWidth=viewport.ActualWidth,ViewportHeight=viewport.ActualHeight,
            Screen=new[]{orthographicPoints.Min(p=>p.X),orthographicPoints.Min(p=>p.Y),orthographicPoints.Max(p=>p.X),orthographicPoints.Max(p=>p.Y)}},new JsonSerializerOptions {WriteIndented=true}));
        Check(orthographicPoints.All(p=>p.X>=0&&p.Y>=0&&p.X<=viewport.ActualWidth&&p.Y<=viewport.ActualHeight)&&
            viewport.Camera is OrthographicCamera firstCamera&&comparison.Camera is OrthographicCamera secondCamera&&firstCamera.Width==secondCamera.Width,
            "orthographic split views preserve both framing and synchronized width");
        state.IsOrthographic=previousProjection;renderer.Focus(state.Snapshot.Elements.Select(e=>e.Id).ToHashSet());Wait(150);
        state.CompareEnabled=false;state.ShowExtrema=false;state.SelectedPanel=2;state.ClipAxis=0;state.ClipPercent=38;state.ClipEnabled=true;Wait(180);
        Capture(control,viewport,Path.Combine(output,"vista-taglio.png"));
        state.ClipEnabled=false;state.SelectedMode=0;state.SelectedPanel=0;state.ShowLoads=true;
        state.SelectedLoadCase=state.LoadCases.FirstOrDefault(c=>c=="G2_SOVR")??state.LoadCases.FirstOrDefault();Wait(180);
        Check(state.ActiveLoads.Count>0&&state.Status.StartsWith("Scena pronta"),"real imported load assignments are available in the model tab");
        Capture(control,viewport,Path.Combine(output,"vista-carichi.png"));
        state.ShowLoads=false;
    }
}
