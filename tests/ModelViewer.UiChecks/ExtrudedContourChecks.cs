using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;
using Mesh = HelixToolkit.SharpDX.MeshGeometry3D;

static partial class Program
{
    static void ExtrudedContourChecks(string output)
    {
        var sample = Sample.Model();
        sample.Results = [new("Campo sintetico", false, sample.Elements.Where(e => e.Type == "PLATE")
            .SelectMany(e => e.Nodes.Select(n => new CornerResult(e.Id, n, [1, e.Id == 10 ? 10 : 30, 3, 4, 5, 6, 7, 8]))).ToArray())];
        sample.LocalFrames = [new(10, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)), new(11, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1))];
        string original = sample.Fingerprint();
        using var control = new ModelViewerControl(sample, [], false, _ => { }, (_, _, _, _) => { });
        var host = new Window { Title = "ANTHEA · contouring su volumi", Content = control, Width = 1450, Height = 950, ShowInTaskbar = false, ShowActivated = false };
        host.Show(); Wait(500);
        try
        {
            var state = (ModelViewerViewModel)control.DataContext; var scene = Children<Viewport3DX>(control).Single();
            state.ShowInspector = false; state.ShowContext = false; state.SelectedPanel = 2;
            Wait(150);
            var button = Children<Button>(control).Single(b => b.Content as string == "Volumi e offset");
            button.Command.Execute(button.CommandParameter); Wait(200);
            Mesh Colored() => (Mesh)scene.Items.OfType<MeshGeometryModel3D>().Single(m => m.Material is ColorStripeMaterial).Geometry!;
            var mesh = Colored();
            Check(state.IsExtruded && state.ShowResults && state.SelectedPanel == 2, "View button selects extrusion without clearing results or changing tab");
            Check(Math.Abs(mesh.Positions!.Min(p => p.Z) + .85) < 1e-6 && Math.Abs(mesh.Positions!.Max(p => p.Z) + .55) < 1e-6,
                "negative offset -0.7 and thickness 0.3 place actual contour vertices at z=-0.85 and -0.55");
            Check(mesh.Normals!.Any(n => n.Z < -.99) && mesh.Normals!.Any(n => n.Z > .99) && mesh.Normals!.Any(n => Math.Abs(n.Z) < .01),
                "contour geometry includes upper, lower and side faces with outward normals");
            double[] SharedUV() => Enumerable.Range(0, Colored().Positions!.Count).Where(i => Math.Abs(Colored().Positions![i].X) < 1e-6).Select(i => (double)Colored().TextureCoordinates![i].X).ToArray();
            Check(SharedUV().Contains(0) && SharedUV().Contains(1), "extruded faces preserve unaveraged discontinuities");
            state.SelectedContourValues = 1; Wait(180);
            Check(SharedUV().Length > 0 && SharedUV().All(v => Math.Abs(v - .5) < 1e-6), "both faces and exposed rims share the averaged value 20 at common vertices");
            state.ShowIsolines = true; state.ShowMesh = true; Wait(120);
            Capture(control, scene, Path.Combine(output, "plate-estrusi-offset-negativo.png"));
            state.SelectElements([10]); state.IsolateCommand.Execute(null); Wait(120);
            mesh = Colored();
            Check(mesh.Positions!.Where((p, i) => Math.Abs(p.X) < 1e-6 && Math.Abs(mesh.Normals![i].X - 1) < 1e-6).Any(),
                "isolating one plate closes its newly exposed common side");
            state.SelectedPanel = 1;
            Wait(150);
            var toggle = Children<CheckBox>(control).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Mostra risultati sul modello");
            toggle.IsChecked = false; Wait(100);
            Check(state.IsExtruded && !scene.Items.OfType<MeshGeometryModel3D>().Any(m => m.Material is ColorStripeMaterial), "result checkbox hides colors while preserving volumes");
            toggle.IsChecked = true; Wait(100);
            Check(Colored().Positions!.Min(p => p.Z) < -.8, "restoring results keeps the extruded geometry");
            var renderer = control.GetType().GetField("renderer", Flags)!.GetValue(control)!;
            bool picked = false;
            for (int y = 1; y < 10 && !picked; y++) for (int x = 1; x < 10 && !picked; x++)
                foreach (var hit in scene.FindHits(new Point(scene.ActualWidth*x/10, scene.ActualHeight*y/10)))
                    if (Call(renderer, "ElementAt", hit) is int id && id == 10) { picked = true; break; }
            Check(picked, "picking an extruded result face resolves the original plate ID");
            Check(sample.Fingerprint() == original, "extrusion, averaging, isolation and layer toggles preserve imported data");
        }
        finally { host.Close(); }
        // Triangle in the XZ plane: winding gives local normal -Y. Positive offset must therefore move towards -Y.
        var triangle = Sample.Model(); triangle.Nodes = [new(1,0,0,0),new(2,2,0,0),new(3,0,0,2)];
        triangle.Elements = [new(10,"PLATE",1,[1,2,3])]; triangle.Plates = [new(1,"Triangolo",.4,.6)];
        triangle.Results = [new("Triangolo",false,[new(10,1,[1,10,3,4,5,6,7,8]),new(10,2,[1,20,3,4,5,6,7,8]),new(10,3,[1,30,3,4,5,6,7,8])])];
        using var triangleControl = new ModelViewerControl(triangle, [], false, _ => { }, (_, _, _, _) => { });
        var triangleHost = new Window { Content=triangleControl, Width=1450, Height=950, ShowInTaskbar=false, ShowActivated=false };
        triangleHost.Show(); Wait(350);
        try
        {
            var state=(ModelViewerViewModel)triangleControl.DataContext; state.SetRepresentationCommand.Execute("1"); state.ShowInspector=false;
            var scene=Children<Viewport3DX>(triangleControl).Single(); Wait(150);
            state.FitCommand.Execute(null); Wait(150);
            var mesh=(Mesh)scene.Items.OfType<MeshGeometryModel3D>().Single(m=>m.Material is ColorStripeMaterial).Geometry!;
            Check(Math.Abs(mesh.Positions!.Min(p=>p.Y)+.8)<1e-6 && Math.Abs(mesh.Positions!.Max(p=>p.Y)+.4)<1e-6 && mesh.Indices!.Count==24,
                "triangular plate extrudes along local -Y with positive offset 0.6, thickness 0.4 and eight triangles");
            Check(Enumerable.Range(0,mesh.Positions!.Count).All(i=>Math.Abs(mesh.TextureCoordinates![i].X-((mesh.Positions![i].X+1)+2*(mesh.Positions![i].Z+1))/4)<1e-6),
                "independent linear field u=(x+2z)/4 has identical nodal values on both faces and all rims");
            Capture(triangleControl,scene,Path.Combine(output,"plate-triangolare-offset-positivo.png"));
        }
        finally { triangleHost.Close(); }
    }

    static void CaptureExtrudedRealModel(ModelViewerControl control, Viewport3DX scene, string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;
        state.ShowResults=true; state.SelectedFamily=0; state.SelectedComponent="Myy"; state.SelectedContourValues=1;
        state.ShowMesh=false; state.ShowInspector=false; state.SetRepresentationCommand.Execute("1");
        state.FitCommand.Execute(null); Wait(500);
        Check(state.Status.StartsWith("Scena pronta") && scene.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Material is ColorStripeMaterial),
            "real MIDAS plate field renders on extruded geometry");
        Capture(control,scene,Path.Combine(output,"spalla-risultati-estrusi.png"));
        state.SelectedPanel=1; Wait(100); Capture(control,scene,Path.Combine(output,"spalla-risultati-estrusi-opzioni.png"));
    }
}
