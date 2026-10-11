using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Maths;
using Color=System.Windows.Media.Color;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed record ExplorationOptions(GraphicPlane? Clip=null,bool Outlines=true,double Opacity=1,bool ReversePalette=false,int Bands=0,
    bool AvoidLabels=true,IReadOnlyList<ModelProbe>? Probes=null,bool BeamDiagram=false,double DiagramScale=1,
    IReadOnlyList<GraphicalLoad>? Loads=null,double LoadScale=1,IReadOnlyList<ModelDisplacement>? Displacements=null,double DeformationScale=1,
    bool DiagramAlongY=false,bool DiagramValues=false,bool ShowUndeformed=true,double UndeformedOpacity=.35,bool UndeformedMesh=false,
    IReadOnlyList<GraphicPlane>? ClipPlanes=null,ModelClipBox? ClipBox=null,
    IReadOnlyList<MeasurementAnnotation>? Measurements=null,IReadOnlyList<int>? MeasurementNodes=null);

internal sealed partial class ModelSceneRenderer
{
    private ModelDirection modelOrigin = new(0,0,0);
    private SceneClipSet? clipping;
    private LineGeometryModel3D? hoverLine;
    private readonly List<Rect> labelBounds=[];
    internal IReadOnlyList<Rect> LabelBounds=>labelBounds;
    internal Vector3 PointForNode(int id)=>points[id];
    internal IReadOnlyList<Vector3> SelectionShape(ModelElement element)
    {
        var polygon=ElementPoints(element);
        if(polygon.Any(p=>!InFront(p)))return [];
        return clipping?.Polygon(polygon)??polygon;
    }
    internal Point Project(Vector3 p)
    {
        if(viewport.Camera is not HelixToolkit.Wpf.SharpDX.ProjectionCamera camera)return new(double.NaN,double.NaN);
        // The render context still contains the preceding frame immediately after a camera/layout change.
        // Use the current camera so labels, selection and report layout share the same projection.
        var matrix=camera.CameraInternal.CreateViewMatrix()*camera.CameraInternal.CreateProjectionMatrix((float)(viewport.ActualWidth/Math.Max(1,viewport.ActualHeight)));
        var projected=Vector4.Transform(new Vector4(p,1),matrix);
        if(Math.Abs(projected.W)<1e-12)return new(double.NaN,double.NaN);
        var dpi=VisualTreeHelper.GetDpi(viewport);
        return new((projected.X/projected.W+1)*viewport.ActualWidth/2-.5/dpi.DpiScaleX,
            (1-projected.Y/projected.W)*viewport.ActualHeight/2-.5/dpi.DpiScaleY);
    }
    internal bool InClip(Vector3 p)=>clipping==null||clipping.Contains(p);
    internal bool InFront(Vector3 p)=>viewport.Camera is not HelixToolkit.Wpf.SharpDX.ProjectionCamera c || Vector3D.DotProduct(new Vector3D(p.X-c.Position.X,p.Y-c.Position.Y,p.Z-c.Position.Z),c.LookDirection)>0;
    internal bool LabelFits(Vector3 p)
    {
        if(!InClip(p)||!InFront(p)) return false;
        if(lastOptions?.Exploration?.AvoidLabels==false) return true;
        var screen=Project(p); if(!double.IsFinite(screen.X)||!double.IsFinite(screen.Y)||screen.X<0||screen.Y<0||screen.X>viewport.ActualWidth||screen.Y>viewport.ActualHeight) return false;
        var bounds=new Rect(screen.X-50,screen.Y-18,100,36);
        if(labelBounds.Any(r=>r.IntersectsWith(bounds)))return false;
        labelBounds.Add(bounds);return true;
    }
    bool ProbePlacement(ModelProbe probe,Vector3 p,out Vector2 offset)
    {
        offset=new(12,24);
        if(!InClip(p)||!InFront(p))return false;
        if(lastOptions?.Exploration?.AvoidLabels==false)return true;
        var screen=Project(p);if(!double.IsFinite(screen.X)||!double.IsFinite(screen.Y))return false;
        var lines=probe.Caption.Split('\n');
        double width=lines.Max(l=>l.Length)*8+24,height=lines.Length*22+16;
        if(width>viewport.ActualWidth-16||height>viewport.ActualHeight-16)return false;
        foreach(int row in new[]{0,1,-1,2,-2,3,-3})
        {
            double x=Math.Clamp(screen.X+12,width/2+8,viewport.ActualWidth-width/2-8);
            double y=Math.Clamp(screen.Y-24+row*(height+10),height/2+8,viewport.ActualHeight-height/2-8);
            var bounds=new Rect(x-width/2,y-height/2,width,height);
            if(labelBounds.Any(r=>r.IntersectsWith(bounds)))continue;
            labelBounds.Add(bounds);offset=new((float)(x-screen.X),(float)(screen.Y-y));return true;
        }
        return false;
    }
    Vector3 LabelLeaderEnd(Vector3 p,Vector2 offset)
    {
        if(viewport.Camera is not HelixToolkit.Wpf.SharpDX.ProjectionCamera camera)return p;
        var look=camera.LookDirection;look.Normalize();var right=Vector3D.CrossProduct(look,camera.UpDirection);right.Normalize();
        var up=Vector3D.CrossProduct(right,look);up.Normalize();
        var matrix=camera.CameraInternal.CreateProjectionMatrix((float)(viewport.ActualWidth/Math.Max(1,viewport.ActualHeight)));
        double depth=camera is HelixToolkit.Wpf.SharpDX.OrthographicCamera?1:Vector3D.DotProduct(new Vector3D(p.X-camera.Position.X,p.Y-camera.Position.Y,p.Z-camera.Position.Z),look);
        double x=offset.X*2*depth/(matrix.M11*Math.Max(1,viewport.ActualWidth)),y=offset.Y*2*depth/(matrix.M22*Math.Max(1,viewport.ActualHeight));
        return p+new Vector3((float)(right.X*x+up.X*y),(float)(right.Y*x+up.Y*y),(float)(right.Z*x+up.Z*y));
    }
    internal void Hover(int? id)
    {
        if(hoverLine!=null) { viewport.Items.Remove(hoverLine); hoverLine=null; }
        var e=model?.Elements.FirstOrDefault(e=>e.Id==id); if(e==null)return;
        var edges=new EdgeBuilder(); var p=ElementPoints(e);
        ElementOutline(edges,e,p,lastOptions?.Extruded==true);
        hoverLine=AddLines(edges,Color.FromRgb(100,220,255),3);
    }
    internal void Focus(IReadOnlySet<int> ids,IReadOnlySet<int>? nodeIds=null)
    {
        var p=model?.Elements.Where(e=>ids.Contains(e.Id)).SelectMany(DisplayPoints).Concat((nodeIds??new HashSet<int>()).Select(n=>points[n])).ToArray();
        if(p==null||p.Length==0)return;
        FocusPoints(p);
    }
    private void FocusPoints(IReadOnlyList<Vector3> p)
    {
        if(p.Count==0)return;
        var min=p.Aggregate(Vector3.Min);var max=p.Aggregate(Vector3.Max);var center=(min+max)/2;
        if(viewport.Camera is not HelixToolkit.Wpf.SharpDX.ProjectionCamera camera||CameraState() is not {} state)return;
        // Fit against the actual projection: tall split views have a narrower horizontal field of view.
        // Screen-sized labels must never contribute to the geometric bounds.
        double radius=Math.Max(.1,(max-min).Length()*.55),aspect=Math.Max(.05,viewport.ActualWidth/Math.Max(1,viewport.ActualHeight));
        var direction=camera.LookDirection;direction.Normalize();
        double distance;
        if(state.Orthographic)distance=Math.Max(radius*2,camera.LookDirection.Length);
        else
        {
            var matrix=camera.CameraInternal.CreateProjectionMatrix((float)aspect);
            double cotangent=Math.Max(Math.Abs(matrix.M11),Math.Abs(matrix.M22));
            distance=radius*Math.Sqrt(1+cotangent*cotangent);
        }
        var look=direction*distance;
        RestoreCamera(state with {Position=[center.X-look.X,center.Y-look.Y,center.Z-look.Z],
            Look=[look.X,look.Y,look.Z],Width=2*radius*Math.Max(1,aspect)});
    }
    internal ModelCameraState? CameraState()
    {
        if(viewport.Camera is not HelixToolkit.Wpf.SharpDX.ProjectionCamera c)return null;
        return new([c.Position.X,c.Position.Y,c.Position.Z],[c.LookDirection.X,c.LookDirection.Y,c.LookDirection.Z],[c.UpDirection.X,c.UpDirection.Y,c.UpDirection.Z],c is HelixToolkit.Wpf.SharpDX.OrthographicCamera,
            c is HelixToolkit.Wpf.SharpDX.OrthographicCamera o?o.Width:extent,c is HelixToolkit.Wpf.SharpDX.PerspectiveCamera p?p.FieldOfView:42);
    }
    internal void RestoreCamera(ModelCameraState state)
    {
        cameraInitialized=true;
        HelixToolkit.Wpf.SharpDX.ProjectionCamera c=state.Orthographic?new HelixToolkit.Wpf.SharpDX.OrthographicCamera { Width=state.Width }:new HelixToolkit.Wpf.SharpDX.PerspectiveCamera { FieldOfView=state.FieldOfView };
        c.Position=new(state.Position[0],state.Position[1],state.Position[2]);c.LookDirection=new(state.Look[0],state.Look[1],state.Look[2]);c.UpDirection=new(state.Up[0],state.Up[1],state.Up[2]);
        c.NearPlaneDistance=.01;c.FarPlaneDistance=Math.Max(10000,extent*10);viewport.Orthographic=state.Orthographic;viewport.Camera=c;
    }
    void DrawExploration(SceneOptions options)
    {
        if(options.Exploration is not {} settings)return;
        if(settings.Outlines && !options.Extruded)
        {
            var outline=new EdgeBuilder();var seen=new HashSet<(int,int)>();
            foreach(var e in model!.Elements.Where(e=>e.Type=="PLATE"&&(options.Visible==null||options.Visible.Contains(e.Id))))
                for(int i=0;i<e.Nodes.Length;i++)
                {
                    int a=e.Nodes[i],b=e.Nodes[(i+1)%e.Nodes.Length];
                    if(plateSides[e.Id][i]&&seen.Add((Math.Min(a,b),Math.Max(a,b)))) outline.Line(points[a],points[b]);
                }
            AddLines(outline,Color.FromRgb(116,147,170),1.2);
        }
        if(options.Selection.Count==0 && options.SelectedNodes.Count>0)
        {
            var dots=new EdgeBuilder();float size=(float)extent*.003f;
            foreach(int id in options.SelectedNodes.Where(points.ContainsKey))
            { var p=points[id];dots.Line(p-Vector3.UnitX*size,p+Vector3.UnitX*size);dots.Line(p-Vector3.UnitY*size,p+Vector3.UnitY*size);dots.Line(p-Vector3.UnitZ*size,p+Vector3.UnitZ*size); }
            AddLines(dots,Color.FromRgb(255,207,96),3);
        }
        if(settings.Probes is {Count:>0})
        {
            var text=new BillboardText3D();var leaders=new EdgeBuilder();
            foreach(var probe in settings.Probes)
            {
                if(options.Visible!=null&&!options.Visible.Contains(probe.Element))continue;
                var p=ProbePoint(probe);
                if(!ProbePlacement(probe,p,out var offset))continue;
                leaders.Line(p,LabelLeaderEnd(p,offset));
                text.TextInfo.Add(new TextInfo(probe.Caption,p) { Foreground=new Color4(1,.95f,.72f,1),Background=new Color4(.04f,.07f,.1f,.94f),Scale=(float)(.58*options.RasterScale),Offset=offset*(float)options.RasterScale });
            }
            AddLines(leaders,Color.FromRgb(230,211,147),.7);
            viewport.Items.Add(new BillboardTextModel3D { Geometry=text,FixedSize=true,IsHitTestVisible=false,DepthBias=-10000 });
        }
        if(settings.BeamDiagram&&options.ShowResults&&options.Family!="PLATE")DrawBeamDiagram(options,settings);
        DrawLoadGraphics(options,settings);
        if(!preparingAnimation)DrawMeasurements(options,settings);
    }
}
