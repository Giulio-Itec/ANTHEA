using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ANTHEA.ModelViewer.Wpf;

public partial class ModelViewerControl
{
    private readonly List<Point> selectionPolygon=[];
    private Point? dragStart;
    private readonly System.Diagnostics.Stopwatch hoverWatch=System.Diagnostics.Stopwatch.StartNew();
    private ModelCameraState? previousCamera;
    private readonly System.Windows.Threading.DispatcherTimer labelRefresh=new() {Interval=TimeSpan.FromMilliseconds(250)};
    private readonly System.ComponentModel.DependencyPropertyDescriptor cameraProperty=
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Viewport3DX.CameraProperty,typeof(Viewport3DX));
    private void AttachInteraction()
    {
        Viewport.PreviewMouseDown+=InputDown;Viewport.PreviewMouseMove+=InputMove;Viewport.PreviewMouseUp+=InputUp;Viewport.MouseLeave+=InputLeave;
        labelRefresh.Tick+=RefreshLabels;
        Viewport.CameraChanged+=CameraChanged;Viewport.PreviewMouseWheel+=RememberWheelCamera;
        cameraProperty.AddValueChanged(Viewport,CameraReplaced);
        viewModel.FocusSelectionRequested+=FocusSelection;viewModel.PreviousCameraRequested+=PreviousCamera;viewModel.HoverChanged+=HoverChanged;
    }
    private void DetachInteraction()
    {
        Viewport.PreviewMouseDown-=InputDown;Viewport.PreviewMouseMove-=InputMove;Viewport.PreviewMouseUp-=InputUp;Viewport.MouseLeave-=InputLeave;
        labelRefresh.Stop();labelRefresh.Tick-=RefreshLabels;
        Viewport.CameraChanged-=CameraChanged;Viewport.PreviewMouseWheel-=RememberWheelCamera;
        cameraProperty.RemoveValueChanged(Viewport,CameraReplaced);
        viewModel.FocusSelectionRequested-=FocusSelection;viewModel.PreviousCameraRequested-=PreviousCamera;viewModel.HoverChanged-=HoverChanged;
    }
    private void RefreshLabels(object? sender,EventArgs e)
    {
        labelRefresh.Stop();
        if(disposed || renderer==null)return;
        if(viewModel.ShowNodeIds||viewModel.ShowElementIds||viewModel.ShowPropertyIds||viewModel.ShowExtrema||viewModel.PinnedProbes.Count>0)
        { renderer.Render(Options(viewModel));if(comparisonRenderer!=null && viewModel.Comparison is {} state)comparisonRenderer.Render(Options(state)); }
    }
    private void RememberWheelCamera(object sender,MouseWheelEventArgs e)=>previousCamera=renderer?.CameraState();
    private void CameraReplaced(object? sender,EventArgs e)
    {
        SyncComparisonCamera();labelRefresh.Stop();labelRefresh.Start();
    }
    private void CameraChanged(object? sender,RoutedEventArgs e)
    {
        SyncComparisonCamera();
        labelRefresh.Stop();labelRefresh.Start();
    }
    private void FocusSelection(object? sender,EventArgs e) { previousCamera=renderer?.CameraState();renderer?.Focus(viewModel.SelectedElementIds,viewModel.SelectedNodeIds); }
    private void PreviousCamera(object? sender,EventArgs e)
    {
        if(previousCamera==null)return;var current=renderer?.CameraState();renderer?.RestoreCamera(previousCamera);previousCamera=current;
    }
    private void HoverChanged(object? sender,EventArgs e)=>renderer?.Hover(viewModel.HoverProbe?.Element);
    private void InputLeave(object sender,MouseEventArgs e) { if(dragStart==null)viewModel.HoverProbe=null; }
    private void InputDown(object sender,MouseButtonEventArgs e)
    {
        if(e.ChangedButton==MouseButton.Right||e.ChangedButton==MouseButton.Middle) { previousCamera=renderer?.CameraState();return; }
        if(e.ChangedButton!=MouseButton.Left)return;
        var point=e.GetPosition(Viewport);Viewport.Focus();
        if(viewModel.SelectionTool==1) { dragStart=point;Viewport.CaptureMouse();e.Handled=true; }
        else if(viewModel.SelectionTool==2)
        {
            if(e.ClickCount==2) { if(selectionPolygon.Count>=3)FinishSelection(selectionPolygon,true); CancelSelectionDrawing(); }
            else selectionPolygon.Add(point);
            DrawSelection(point);e.Handled=true;
        }
        else if(viewModel.SelectionTarget==1&&renderer!=null&&viewModel.Snapshot!=null)
        {
            var nodes=viewModel.Snapshot.Elements.Where(x=>viewModel.VisibleElementIds==null||viewModel.VisibleElementIds.Contains(x.Id)).SelectMany(x=>x.Nodes).Distinct();
            var hit=nodes.Where(n=>renderer.InClip(renderer.PointForNode(n))&&renderer.InFront(renderer.PointForNode(n)))
                .Select(n=>(Id:n,Distance:(renderer.Project(renderer.PointForNode(n))-point).Length)).Where(n=>n.Distance<12).OrderBy(n=>n.Distance).FirstOrDefault();
            if(hit.Id>0)viewModel.SelectNodes([hit.Id],Keyboard.Modifiers.HasFlag(ModifierKeys.Control),Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled=true;
        }
    }
    private void InputMove(object sender,MouseEventArgs e)
    {
        var p=e.GetPosition(Viewport);
        if(dragStart!=null||selectionPolygon.Count>0) { DrawSelection(p);return; }
        if(e.LeftButton==MouseButtonState.Pressed||e.RightButton==MouseButtonState.Pressed||hoverWatch.ElapsedMilliseconds<80)return;
        hoverWatch.Restart();
        if(renderer==null||viewModel.Snapshot==null)return;
        var hit=Viewport.FindHits(p).FirstOrDefault(h=>renderer.ElementAt(h) is int id&&viewModel.Snapshot.Elements.Any(el=>el.Id==id&&viewModel.AcceptsElement(el)));
        if(renderer.ElementAt(hit) is not int elementId) { viewModel.HoverProbe=null;return; }
        var element=viewModel.Snapshot.Elements.First(el=>el.Id==elementId);
        var values=viewModel.ContourValues.Where(v=>v.Element==elementId).ToArray();
        if(values.Length>0)
        {
            Vector3 At(ResultValue v)=>v.Node>0?renderer.PointForNode(v.Node):Vector3.Lerp(renderer.PointForNode(element.Nodes[0]),renderer.PointForNode(element.Nodes[^1]),(float)(v.Station??0));
            var nearest=values.MinBy(v=>(renderer.Project(At(v))-p).LengthSquared)!;
            viewModel.HoverProbe=viewModel.ProbeFor(nearest,"Campione vicino · ");
        }
        else
        {
            var n=viewModel.Snapshot.Nodes.First(n=>n.Id==element.Nodes[0]);
            viewModel.HoverProbe=new(elementId,n.Id,null,$"E {element.Id} · {element.Type}\nProprietà {element.Property}\nNodi {string.Join(", ",element.Nodes)}",n.X,n.Y,n.Z);
        }
    }
    private void InputUp(object sender,MouseButtonEventArgs e)
    {
        if(e.ChangedButton!=MouseButton.Left||dragStart is not {} start)return;
        var end=e.GetPosition(Viewport);
        if((end-start).Length>4)FinishSelection([start,new(end.X,start.Y),end,new(start.X,end.Y)],end.X<start.X);
        CancelSelectionDrawing();e.Handled=true;
    }
    private void CancelSelectionDrawing() { dragStart=null;selectionPolygon.Clear();SelectionCanvas.Children.Clear();Viewport.ReleaseMouseCapture(); }
    private void DrawSelection(Point last)
    {
        SelectionCanvas.Children.Clear();IEnumerable<Point> polygon=selectionPolygon.Append(last);
        if(dragStart is {} a)polygon=[a,new(last.X,a.Y),last,new(a.X,last.Y)];
        SelectionCanvas.Children.Add(new Polygon { Points=new PointCollection(polygon),Stroke=Brushes.DeepSkyBlue,StrokeThickness=1.5,Fill=new SolidColorBrush(Color.FromArgb(35,50,170,230)),StrokeDashArray=new DoubleCollection([4,3]) });
    }
    private void FinishSelection(IReadOnlyList<Point> points,bool crossing)
    {
        if(renderer==null||viewModel.Snapshot==null)return;
        var region=points.Select(p=>new ScreenPoint(p.X,p.Y)).ToArray();var elements=viewModel.Snapshot.Elements.Where(e=>viewModel.VisibleElementIds==null||viewModel.VisibleElementIds.Contains(e.Id));
        bool VisiblePoint(int n)=>renderer.InClip(renderer.PointForNode(n))&&renderer.InFront(renderer.PointForNode(n));
        ScreenPoint Project(int n) { var p=renderer.Project(renderer.PointForNode(n));return new(p.X,p.Y); }
        bool add=Keyboard.Modifiers.HasFlag(ModifierKeys.Control),subtract=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if(viewModel.SelectionTarget==1)viewModel.SelectNodes(elements.SelectMany(e=>e.Nodes).Distinct().Where(n=>VisiblePoint(n)&&ScreenSelection.Contains(region,Project(n))),add,subtract);
        else viewModel.SelectElements(elements.Where(e=>viewModel.AcceptsElement(e)&&ScreenSelection.Matches(region,renderer.SelectionShape(e).Select(p=>{var s=renderer.Project(p);return new ScreenPoint(s.X,s.Y);}).ToArray(),crossing)).Select(e=>e.Id),add,subtract);
    }
}
