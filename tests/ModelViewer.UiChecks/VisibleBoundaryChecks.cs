using System.IO;
using System.Numerics;
using System.Windows;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void VisibleBoundaryRealChecks(ModelViewerControl control,Viewport3DX viewport,string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;var renderer=(ModelSceneRenderer)control.GetType().GetField("renderer",Flags)!.GetValue(control)!;
        state.SelectedResultType=0;state.SelectedFamily=0;state.SelectedComponent="Myy";state.SelectedCase=state.Cases.Contains("G1")?"G1":state.Cases.First();
        state.ShowResults=true;state.SelectedRepresentation=1;state.ShowMesh=false;state.ShowInspector=false;
        var full=renderer.Render(ModelViewerControl.Options(state));
        var original=viewport.Items.OfType<MeshGeometryModel3D>().SelectMany(m=>m.Geometry!.Positions!).ToArray();
        state.SelectElements(state.Snapshot!.Elements.Select(e=>e.Id));state.IsolateCommand.Execute(null);state.ClearSelectionCommand.Execute(null);
        var isolated=renderer.Render(ModelViewerControl.Options(state));
        Check(full.Triangles==isolated.Triangles && original.SequenceEqual(viewport.Items.OfType<MeshGeometryModel3D>().SelectMany(m=>m.Geometry!.Positions!)),"real extruded contours remain bit-identical when isolation includes every element");
        var group=state.Snapshot.Elements.Where(e=>e.Type=="PLATE").GroupBy(e=>e.Property).MaxBy(g=>g.Count())!;
        state.SelectElements(group.Select(e=>e.Id));state.IsolateCommand.Execute(null);state.ClearSelectionCommand.Execute(null);renderer.Fit();Wait(350);
        var subset=renderer.Render(ModelViewerControl.Options(state));
        Check(subset.Triangles<full.Triangles && subset.Triangles>0 && state.VisibleElementIds!.Count==group.Count(),"real property isolation produces a smaller closed model without changing the active scalar field");
        SetAppearance("VeryDark");Wait(150);Capture(control,viewport,Path.Combine(output,"spalla-proprieta-isolata.png"));
        File.WriteAllText(Path.Combine(output,"triangoli-reali.txt"),$"Completo: {full.Triangles}\nIsolamento completo: {isolated.Triangles}\nProprieta {group.Key}: {group.Count()} plate, {subset.Triangles} triangoli\n");
        SetAppearance("Light");
    }
    static void VisibleBoundaryChecks(string output,bool captureBefore)
    {
        var sample=Sample.Model();sample.Elements=sample.Elements.Where(e=>e.Type=="PLATE").ToArray();
        using var control=new ModelViewerControl(sample,[],false,_=>{},(_,_,_,_)=>{});
        var window=new Window {Title="ANTHEA · contorni della selezione",Content=control,Width=1300,Height=900,ShowActivated=false,ShowInTaskbar=false};window.Show();Wait(300);
        try
        {
            var state=(ModelViewerViewModel)control.DataContext;var viewport=Children<Viewport3DX>(control).Single();
            var renderer=(ModelSceneRenderer)control.GetType().GetField("renderer",Flags)!.GetValue(control)!;
            state.SelectedMode=1;state.ShowMesh=false;state.ShowInspector=false;state.SelectedPanel=2;
            var options=ModelViewerControl.Options(state);
            var full=renderer.Render(options);
            var baseline=viewport.Items.OfType<MeshGeometryModel3D>().SelectMany(m=>m.Geometry!.Positions!).ToArray();
            var subset=renderer.Render(options with {Visible=sample.Elements.Select(e=>e.Id).ToHashSet()});Wait(100);
            Capture(control,viewport,Path.Combine(output,"due-plate-isolati.png"));
            File.WriteAllText(Path.Combine(output,"triangoli.txt"),$"Modello completo: {full.Triangles}\nInsieme esplicito: {subset.Triangles}\n");
            if(captureBefore)return;
            Check(full.Triangles==20 && subset.Triangles==20,"two adjacent rectangular prisms retain 20 external triangles after explicit isolation, without internal side walls");
            Check(baseline.SequenceEqual(viewport.Items.OfType<MeshGeometryModel3D>().SelectMany(m=>m.Geometry!.Positions!)),"isolating the whole visible model preserves its exact solid geometry");
            var only=renderer.Render(options with {Visible=new HashSet<int>{sample.Elements[0].Id}});
            Check(only.Triangles==12,"hiding the neighbor restores the two exposed triangles and closes the remaining rectangular prism");
            var edge=sample.Elements[0].Nodes.Intersect(sample.Elements[1].Nodes).ToArray();var a=renderer.PointForNode(edge[0]);var b=renderer.PointForNode(edge[1]);
            var skin=options with {Extruded=false,Visible=new HashSet<int>{sample.Elements[0].Id}};renderer.Render(skin);
            var outline=viewport.Items.OfType<LineGeometryModel3D>().Where(l=>l.Color==System.Windows.Media.Color.FromRgb(116,147,170)).SelectMany(l=>l.Geometry?.Positions??[]).ToArray();
            Check(Enumerable.Range(0,outline.Length/2).Any(i=>outline[i*2]==a&&outline[i*2+1]==b||outline[i*2]==b&&outline[i*2+1]==a),"analytical outlines include the newly exposed edge when its neighbor is hidden");
            var bothAgain=renderer.Render(options);
            Check(bothAgain.Triangles==20 && baseline.SequenceEqual(viewport.Items.OfType<MeshGeometryModel3D>().SelectMany(m=>m.Geometry!.Positions!)),"restoring visibility removes the temporary side wall and returns to the original geometry");
            renderer.Render(options with {Exploration=options.Exploration! with {Displacements=[new(sample.Elements[0].Nodes[0],0,0,.1)],DeformationScale=1}});
            Check(viewport.Items.OfType<MeshGeometryModel3D>().All(m=>m.Geometry!.Positions!.Count==0),"elements with incomplete displacements remain outlines rather than partially fabricated solids");
        }
        finally{window.Close();}
    }
}
