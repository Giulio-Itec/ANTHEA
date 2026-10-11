using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelReports;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void RevisionRenderingChecks(ModelViewerControl control,Viewport3DX viewport,string output)
    {
        var state=(ModelViewerViewModel)control.DataContext;var current=state.Snapshot!;string originalKey=current.Fingerprint();
        var previous=new ModelSnapshot {Name="Simulazione di collaudo · copia precedente",Source=current.Source,Nodes=current.Nodes.ToArray(),Elements=current.Elements.ToArray(),Plates=current.Plates.ToArray(),Sections=current.Sections.ToArray(),Results=current.Results.ToArray(),LocalFrames=current.LocalFrames,NodeFrames=current.NodeFrames,Boundaries=current.Boundaries,SourceTables=current.SourceTables};
        int newId=current.Elements.Last(e=>e.Type=="BEAM").Id,removedId=current.Elements.Max(e=>e.Id)+100;
        previous.Elements=[..previous.Elements.Where(e=>e.Id!=newId),new(removedId,"BEAM",current.Elements.First(e=>e.Type=="BEAM").Property,[current.Nodes.MinBy(n=>n.X)!.Id,current.Nodes.MaxBy(n=>n.Z)!.Id])];
        previous.LocalFrames=previous.LocalFrames?.Where(f=>f.Element!=newId).ToArray();previous.Boundaries=previous.Boundaries?.Where(b=>b.Element!=newId).ToArray();
        int moved=current.Nodes.MaxBy(n=>n.Z)!.Id;previous.Nodes=previous.Nodes.Select(n=>n.Id==moved?n with {X=n.X+.1}:n).ToArray();
        previous.Sections[0]=previous.Sections[0] with {Width=previous.Sections[0].Width*.9};
        previous.Results=previous.Results.Select(r=>r with {Values=r.Values.Where(v=>v.Element!=newId).ToArray(),Fields=r.Fields?.Select(f=>f with {Values=f.Values.Where(v=>v.Element!=newId).ToArray()}).ToArray()}).ToArray();
        var result=previous.Results.First(r=>r.Name=="G1");var values=result.Values.ToArray();
        if(values.Length>0){var v=values[0];var components=v.Values.ToArray();components[1]-=10;values[0]=v with {Values=components};previous.Results=previous.Results.Select(r=>r.Name=="G1"?r with {Values=values}:r).ToArray();}
        else previous.Results=previous.Results.Select(r=>r.Name=="G1"?r with {Fields=r.Fields!.Select(f=>f.Family=="PLATE"&&f.Axes=="LOCAL"&&f.Component=="Myy"?f with {Values=f.Values.Select((v,i)=>i==0?v with {Value=v.Value-10}:v).ToArray()}:f).ToArray()}:r).ToArray();
        previous.Validate();var watch=Stopwatch.StartNew();var review=ModelRevisionComparison.Compare(previous,current);double compareMs=watch.Elapsed.TotalMilliseconds;
        var overlay=ModelRevisionOverlay.Create(review,previous.Fingerprint(),originalKey);
        Call(state,"ApplyRevision",review,overlay);state.ShowResults=false;state.SelectedRepresentation=1;state.ShowMesh=false;state.ShowInspector=false;state.SelectedPanel=0;Wait(150);
        var expander=Children<Expander>(control).First(e=>e.Header?.ToString()=="Check della revisione");expander.IsExpanded=true;Wait(200);
        Check(state.ShowRevisionDetails && state.ActiveRevisionOverlay?.Removed.Length==1 && state.ActiveRevisionOverlay.Markers.Any(m=>m.Change=="Nuovo"),"real-model revision review exposes new objects and deleted geometry without changing the snapshot");
        bool ColorPresent(Color color)=>viewport.Items.OfType<LineGeometryModel3D>().Any(l=>l.Color==color && l.Geometry?.Positions?.Count>0 && !l.IsHitTestVisible);
        Check(ColorPresent(Color.FromRgb(71,214,141))&&ColorPresent(Color.FromRgb(255,193,76))&&ColorPresent(Color.FromRgb(202,144,255))&&ColorPresent(Color.FromRgb(255,110,117)),"new, geometry, attribute and deleted outlines render as four distinct non-pickable overlays");
        SetAppearance("VeryDark");Wait(350);Capture(control,viewport,Path.Combine(output,"spalla-check-revisione-simulato.png"));
        state.ShowRemovedRevisionElements=false;Check(!ColorPresent(Color.FromRgb(255,110,117)),"deleted-object outlines can be hidden independently");state.ShowRemovedRevisionElements=true;
        state.ShowRevisionHighlights=false;Check(!ColorPresent(Color.FromRgb(71,214,141)) && state.RevisionRows.Count>0,"all revision highlights can be disabled while keeping the change table");state.ShowRevisionHighlights=true;
        state.SelectedRevisionRow=state.RevisionRows.First(r=>r.Category=="Elemento"&&r.Change=="Nuovo");
        Check(state.SelectedElementIds.Contains(newId),"selecting a revision table row selects the corresponding current object");state.ClearSelectionCommand.Execute(null);state.ShowInspector=false;
        var renderer=(ModelSceneRenderer)control.GetType().GetField("renderer",Flags)!.GetValue(control)!;var camera=renderer.CameraState()!;
        state.SelectedRevisionRow=state.RevisionRows.First(r=>r.Category=="Elemento"&&r.Change=="Cancellato");var focused=renderer.CameraState()!;
        double expectedX=overlay.Removed[0].Points.Average(p=>p.X)-(current.Nodes.Min(n=>n.X)+current.Nodes.Max(n=>n.X))/2;
        Check(state.SelectedElementIds.Count==0 && Math.Abs(focused.Position[0]+focused.Look[0]-expectedX)<1e-4,"a deleted table row frames its previous location without selecting an unrelated current object");
        renderer.RestoreCamera(camera);
        state.SelectedResultType=0;state.SelectedCase="G1";state.SelectedFamily=0;state.SelectedAxes=0;state.SelectedComponent="Myy";
        var task=viewport.Dispatcher.InvokeAsync(()=>state.ComputeRevisionDeltaCommand.ExecuteAsync(null)).Task.Unwrap();while(!task.IsCompleted)Wait(20);task.GetAwaiter().GetResult();
        Check(state.RevisionDeltaRows.Count>0 && state.RevisionDeltaRows.All(r=>!r.GeometryChanged),"requested real-case comparison retains common stations and excludes moved geometry by default");
        state.IncludeChangedGeometryInDelta=true;
        Check(state.RevisionDeltaRows.Any(r=>Math.Abs(r.Delta-10)<1e-8),"the synthetic ten-unit result change is recovered from the real imported sample identities");
        state.SelectedPanel=1;state.ShowResults=true;Wait(250);Capture(control,viewport,Path.Combine(output,"spalla-delta-revisione-simulato.png"));
        state.RevisionTab=0;state.ShowResults=false;state.CloseRevisionDetailsCommand.Execute(null);Wait(100);
        state.ReportTitle="Collaudo · confronto revisioni simulato";state.AddReportFigureCommand.Execute(null);state.SelectedReportChapter!.Title="Differenze controllate per il collaudo";
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var export=WpfModelViewerServices.ExportReportToAsync(state.CaptureReport(),current,Path.Combine(output,"revisione.pdf"),null,timeout.Token);
        while(!export.IsCompleted)Wait(25);var report=export.GetAwaiter().GetResult();var recipe=ModelReportFiles.Read(report.RecipePath);
        Check(recipe.Blocks[0].View!.RevisionOverlay?.CurrentRevision==originalKey && File.ReadAllText(report.HtmlPath).Contains("Corrispondenza per ID"),"revision report preserves baseline provenance and states the explicit ID matching rule");
        state.ClearRevisionCommand.Execute(null);SetAppearance("Light");Wait(100);
        Check(state.ActiveRevisionOverlay==null && !state.HasRevision && current.Fingerprint()==originalKey,"clearing the review releases overlays and preserves the complete original model");
        File.WriteAllText(Path.Combine(output,"tempi-revisione.json"),JsonSerializer.Serialize(new {GeometryAndAttributesMs=compareMs,Rows=review.Rows.Length,Elements=review.Elements.Length,Simulated=true}));
    }
}
