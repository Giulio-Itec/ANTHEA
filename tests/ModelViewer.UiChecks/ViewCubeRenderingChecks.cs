using System.IO;
using System.Numerics;
using System.Windows;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void ViewCubeRenderingChecks(ModelViewerControl control,Viewport3DX viewport,string output,bool captureBefore)
    {
        var state=(ModelViewerViewModel)control.DataContext;state.ShowInspector=false;state.SelectedPanel=2;state.ShowViewCube=true;
        SetAppearance("VeryDark");Wait(200);
        File.WriteAllText(Path.Combine(output,"dimensione-cubo.txt"),$"Valore impostato: {viewport.ViewCubeSize}\nValore predefinito della libreria: {Viewport3DX.ViewCubeSizeProperty.DefaultMetadata.DefaultValue}\n");
        Capture(control,viewport,Path.Combine(output,captureBefore?"cubo-prima.png":"cubo-corretto.png"));
        if(captureBefore)return;
        Check(viewport.ViewCubeSize is >.5 and <2,"view cube size uses the library scale factor rather than a pixel width");
        Check(viewport.ViewCubeTexture!=null,"cube uses the module face labels and readable neutral colors");
        var cube=(ViewBoxModel3D)viewport.Template.FindName("PART_ViewCube",viewport);
        var samples=new Dictionary<int,(Point Point,Vector3 Normal)>();
        for(int x=0;x<viewport.ActualWidth;x+=3)for(int y=0;y<Math.Min(150,viewport.ActualHeight);y+=3)
        {
            var point=new Vector2(x,y);var hits=new List<HitTestResult>();
            if(!cube.HitTest(new HitTestContext(viewport.RenderContext,viewport.UnProject(point),point),ref hits))continue;
            var normal=hits[0].NormalAtHit;int components=new[]{normal.X,normal.Y,normal.Z}.Count(v=>Math.Abs(v)>.01);
            if(components>0&&!samples.ContainsKey(components))samples[components]=(new Point(x,y),normal);
        }
        Check(samples.ContainsKey(1)&&samples.ContainsKey(2)&&samples.ContainsKey(3),"visible cube exposes real hit targets for faces, 45-degree edges and isometric corners");
        var renderer=(ModelSceneRenderer)control.GetType().GetField("renderer",Flags)!.GetValue(control)!;var original=renderer.CameraState()!;
        foreach(var sample in samples.OrderBy(p=>p.Key))
        {
            var hit=(bool)Call(viewport,"ViewBoxHitTest",sample.Value.Point,null!)!;Wait(900);
            var camera=renderer.CameraState()!;var look=Vector3.Normalize(new((float)camera.Look[0],(float)camera.Look[1],(float)camera.Look[2]));
            File.AppendAllText(Path.Combine(output,"clic-cubo.txt"),$"{sample.Key}: hit={hit}, punto={sample.Value.Point}, atteso={sample.Value.Normal}, camera={look}, prodotto={Vector3.Dot(look,Vector3.Normalize(sample.Value.Normal))}\n");
            Check(hit&&Vector3.Dot(look,Vector3.Normalize(sample.Value.Normal))>.999,"cube click changes the camera to the picked direction with "+sample.Key+" nonzero components");
            renderer.RestoreCamera(original);Wait(250);
        }
        var pointOnCube=samples[2].Point;
        Check((bool)Call(control,"IsViewCubeHit",pointOnCube)! && !(bool)Call(control,"IsViewCubeHit",new Point(5,viewport.ActualHeight-5))!,"selection tools can distinguish cube clicks from model clicks");
        state.ShowViewCube=false;Wait(80);
        Check(!cube.IsRendering && !(bool)Call(control,"IsViewCubeHit",pointOnCube)!,"disabled cube leaves no active click target");
        state.ShowViewCube=true;
        var window=Window.GetWindow(control);double oldWidth=window.Width,oldHeight=window.Height;window.Width=1100;window.Height=650;state.ShowTable=true;Wait(180);
        double cx=viewport.ActualWidth*(1+viewport.ViewCubeHorizontalPosition)/2,cy=viewport.ActualHeight*(1-viewport.ViewCubeVerticalPosition)/2;
        Check(cx>=50&&viewport.ActualWidth-cx>=50&&cy>=50&&viewport.ActualHeight-cy>=50,"cube stays inside a short viewport with the table open");
        Check(viewport.ActualHeight>=150,"lower details adapt their height to retain a usable model viewport");
        Capture(control,viewport,Path.Combine(output,"cubo-vista-ridotta.png"),100);
        state.ShowTable=false;window.Width=oldWidth;window.Height=oldHeight;SetAppearance("Light");
    }
}
