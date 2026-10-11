using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelReports;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void MeasurementRenderingChecks(ModelViewerControl control,Viewport3DX viewport,string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;string before=state.Snapshot!.Fingerprint();
        var renderer=(ModelSceneRenderer)control.GetType().GetField("renderer",Flags)!.GetValue(control)!;
        state.SelectedMode=0;state.SelectedRepresentation=1;state.ShowInspector=false;state.SelectedPanel=2;state.ShowMesh=false;
        double xmin=state.Snapshot.Nodes.Min(n=>n.X),xmax=state.Snapshot.Nodes.Max(n=>n.X),ymin=state.Snapshot.Nodes.Min(n=>n.Y),ymax=state.Snapshot.Nodes.Max(n=>n.Y),zmin=state.Snapshot.Nodes.Min(n=>n.Z);
        int Corner(double x,double y)=>state.Snapshot.Nodes.MinBy(n=>Math.Pow(n.X-x,2)+Math.Pow(n.Y-y,2)+Math.Pow(n.Z-zmin,2))!.Id;
        int[] nodes=[Corner(xmin,ymin),Corner(xmax,ymin),Corner(xmax,ymax)];
        if(nodes.Distinct().Count()<3)nodes=state.Snapshot.Elements.First(e=>e.Type=="PLATE").Nodes.Take(3).ToArray();
        state.StartMeasurementCommand.Execute(null);state.AddMeasurementNode(nodes[0]);
        bool HasMeasureLines()=>viewport.Items.OfType<LineGeometryModel3D>().Any(l=>l.Color==Color.FromRgb(103,225,227) && !l.IsHitTestVisible);
        Check(HasMeasureLines() && state.PendingMeasurementNodes.Count==1,"the first picked node has a non-pickable measurement marker");
        state.AddMeasurementNode(nodes[1]);state.CancelMeasurementCommand.Execute(null);
        state.SelectedMeasurementKind=1;state.MeasurementNodeIds=string.Join(" ",nodes);state.AddMeasurementIdsCommand.Execute(null);Wait(150);
        var tools=Children<ModelViewToolsView>(control).Single();var expander=Children<Expander>(tools).Single(e=>e.Header?.ToString()=="Misure fra nodi");expander.IsExpanded=true;Wait(100);expander.BringIntoView();Wait(100);
        Check(state.MeasurementReadings.Count==2 && state.MeasurementReadings.All(m=>m.Available) && HasMeasureLines(),"distance and angle commands display both their readings and geometric annotations");
        Check(viewport.Items.OfType<BillboardTextModel3D>().Any(b=>b.Geometry is HelixToolkit.SharpDX.BillboardText3D text && text.TextInfo.Any(t=>t.Text.Contains("Geometria originale"))),"measurement labels explicitly identify the original geometry basis");
        var camera=renderer.CameraState()!;
        state.CameraPresetCommand.Execute("top");Wait(350);renderer.Fit();Wait(100);
        var nodePoint=renderer.Project(renderer.PointForNode(nodes[0]));
        Check(Call(control,"NodeNear",nodePoint) is int found && (renderer.Project(renderer.PointForNode(found))-nodePoint).Length<1,"node acquisition uses the actual visible projection rather than screen-independent coordinates");
        state.ClipEnabled=true;state.ClipAxis=0;state.ClipPercent=100;Wait(100);
        Check(Call(control,"NodeNear",nodePoint)==null,"node acquisition excludes points discarded by graphical clipping");
        state.ClipEnabled=false;renderer.RestoreCamera(camera);Wait(100);
        SetAppearance("VeryDark");Wait(100);Capture(control,viewport,Path.Combine(output,"spalla-misure-molto-scuro.png"));
        File.WriteAllText(Path.Combine(output,"label-layout.json"),System.Text.Json.JsonSerializer.Serialize(new {Width=viewport.ActualWidth,Height=viewport.ActualHeight,Bounds=renderer.LabelBounds,Labels=viewport.Items.OfType<BillboardTextModel3D>().SelectMany(b=>(b.Geometry as HelixToolkit.SharpDX.BillboardText3D)?.TextInfo??[]).Select(t=>new {t.Text,t.ActualWidth,t.ActualHeight,t.Scale,Origin=renderer.Project(t.Origin).ToString(),Offset=t.Offset.ToString()})}));
        state.ShowMeasurements=false;Check(!HasMeasureLines() && state.MeasurementReadings.Count==2,"measurement visibility removes overlays without deleting their definitions");state.ShowMeasurements=true;
        state.SelectElement(state.Snapshot.Elements[0].Id);state.StartMeasurementCommand.Execute(null);state.AddMeasurementNode(nodes[0]);
        control.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(control),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
        Check(!state.IsMeasuring && state.PendingMeasurementNodes.Count==0 && state.SelectedElementIds.Count==0,"Escape stops measurement picking, removes pending markers and deselects the model");
        state.ReportTitle="Spalla · misure geometriche";state.AddReportFigureCommand.Execute(null);state.SelectedReportChapter!.Title="Geometria originale";
        if(state.Snapshot.Results.Any(r=>r.Displacements?.Length>0))
        {
            state.SelectedCase=state.Cases.Contains("SismaX")?"SismaX":state.Snapshot.Results.First(r=>r.Displacements?.Length>0).Name;
            state.MeasurementBasis=1;state.ShowDeformation=true;state.SelectedDeformationScale=2;state.DeformationScale=1000;
            string reading=state.MeasurementReadings[0].Value;state.DeformationScale=20000;state.DeformationPhase=.25;Wait(100);
            Check(state.MeasurementReadings[0].Value==reading && state.MeasurementAnnotations.All(m=>m.Caption.Contains("deformata ×1")),"deformed measurements retain real values when the graphical amplification and phase change");
            state.DeformationPhase=1;state.AddReportFigureCommand.Execute(null);state.SelectedReportChapter!.Title="Misure sulla deformata reale";
        }
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var task=WpfModelViewerServices.ExportReportToAsync(state.CaptureReport(),state.Snapshot,Path.Combine(output,"misure.pdf"),null,timeout.Token);
        while(!task.IsCompleted)Wait(25);var report=task.GetAwaiter().GetResult();
        var recipe=ModelReportFiles.Read(report.RecipePath);
        Check(recipe.Blocks.Where(b=>b.Kind==ReportBlockKind.Figure).All(b=>b.View!.Measurements?.Items.Length==2),"report recipes retain the two measurement definitions on every figure");
        Check(File.ReadAllText(report.HtmlPath).Contains("amplificazione grafica"),"report states the physical measurement basis independently of amplified rendering");
        var broken=recipe with {Blocks=[recipe.Blocks[0] with {View=recipe.Blocks[0].View! with {Measurements=new(true,0,"m",[new(0,[nodes[0],int.MaxValue])])}}]};
        try{ModelReportFiles.ValidateReferences(broken,state.Snapshot);throw new Exception("Missing measurement node accepted");}catch(InvalidDataException){Check(true,"report rejects a deleted measurement node rather than substituting geometry");}
        state.MeasurementBasis=0;state.ShowDeformation=false;state.ClearMeasurementsCommand.Execute(null);SetAppearance("Light");Wait(100);
        Check(!HasMeasureLines() && state.MeasurementReadings.Count==0 && state.Snapshot.Fingerprint()==before,"clearing annotations preserves every imported source value");
    }
}
