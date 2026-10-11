using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> ClipShapes {get;}=["Piano", "Volume X / Y / Z"];
    [ObservableProperty] private int clipShape;
    [ObservableProperty] private double clipBoxMinX=-1;
    [ObservableProperty] private double clipBoxMaxX=1;
    [ObservableProperty] private double clipBoxMinY=-1;
    [ObservableProperty] private double clipBoxMaxY=1;
    [ObservableProperty] private double clipBoxMinZ=-1;
    [ObservableProperty] private double clipBoxMaxZ=1;
    [ObservableProperty] private bool showClipBox=true;
    public bool IsBoxClip=>ClipShape==1;
    public bool IsPlaneClip=>!IsBoxClip;
    public IReadOnlyList<GraphicPlane> ActiveClipPlanes {get;private set;}=[];
    public ModelClipBox? ActiveClipBox {get;private set;}
    ModelClipBox CaptureClipBox()=>new(ClipBoxMinX,ClipBoxMaxX,ClipBoxMinY,ClipBoxMaxY,ClipBoxMinZ,ClipBoxMaxZ,ShowClipBox);
    partial void OnClipShapeChanged(int value){OnPropertyChanged(nameof(IsBoxClip));OnPropertyChanged(nameof(IsPlaneClip));RefreshScene();}
    partial void OnClipBoxMinXChanged(double value)=>RefreshScene();
    partial void OnClipBoxMaxXChanged(double value)=>RefreshScene();
    partial void OnClipBoxMinYChanged(double value)=>RefreshScene();
    partial void OnClipBoxMaxYChanged(double value)=>RefreshScene();
    partial void OnClipBoxMinZChanged(double value)=>RefreshScene();
    partial void OnClipBoxMaxZChanged(double value)=>RefreshScene();
    partial void OnShowClipBoxChanged(bool value)=>RefreshScene();
    void ApplyClipBox(ModelClipBox box)
    {
        ClipBoxMinX=box.MinX;ClipBoxMaxX=box.MaxX;ClipBoxMinY=box.MinY;ClipBoxMaxY=box.MaxY;ClipBoxMinZ=box.MinZ;ClipBoxMaxZ=box.MaxZ;ShowClipBox=box.Outline;
    }
    [RelayCommand] void ResetClipBox()=>SetClipBoxBounds(false,true);
    [RelayCommand] void ClipBoxFromSelection()=>SetClipBoxBounds(true,true);
    void SetClipBoxBounds(bool selected,bool activate)
    {
        if(Snapshot==null)return;
        var elements=Snapshot.Elements.Where(e=>!selected || SelectedElementIds.Contains(e.Id)).ToArray();
        var ids=elements.SelectMany(e=>e.Nodes).Concat(selected?SelectedNodeIds:[]).ToHashSet();
        var nodes=Snapshot.Nodes.Where(n=>ids.Contains(n.Id)).ToArray();
        if(nodes.Length==0){Status="Seleziona nodi o elementi per impostare il volume.";return;}
        // Conservative display bounds, including the supported solid offsets and translated end points.
        var displacement=activate&&ShowDeformation&&CanDeform?Displacements.ToDictionary(d=>d.Node):[];
        var complete=elements.Where(e=>e.Nodes.All(displacement.ContainsKey)).SelectMany(e=>e.Nodes).ToHashSet();
        var points=nodes.Select(n=>complete.Contains(n.Id)&&displacement.TryGetValue(n.Id,out var d)?n with {X=n.X+d.X*EffectiveDeformationScale*DeformationPhase,Y=n.Y+d.Y*EffectiveDeformationScale*DeformationPhase,Z=n.Z+d.Z*EffectiveDeformationScale*DeformationPhase}:n).ToArray();
        double span=Math.Max(points.Max(n=>n.X)-points.Min(n=>n.X),Math.Max(points.Max(n=>n.Y)-points.Min(n=>n.Y),points.Max(n=>n.Z)-points.Min(n=>n.Z)));
        double solid=IsExtruded?elements.Select(e=>e.Type=="PLATE"?Snapshot.Plates.Where(p=>p.Id==e.Property).Select(p=>Math.Abs(p.Offset)+p.Thickness/2).Single():Snapshot.Sections.Where(s=>s.Id==e.Property).Select(s=>Math.Sqrt(s.Width*s.Width+s.Height*s.Height)/2).Single()).DefaultIfEmpty(0).Max():0;
        double margin=Math.Max(.01,span*.02)+solid;
        bool before=updating;updating=true;
        try
        {
            ApplyClipBox(new(points.Min(n=>n.X)-margin,points.Max(n=>n.X)+margin,points.Min(n=>n.Y)-margin,points.Max(n=>n.Y)+margin,points.Min(n=>n.Z)-margin,points.Max(n=>n.Z)+margin,ShowClipBox));
            if(activate){ClipShape=1;ClipEnabled=true;}
        }
        finally{updating=before;}
        if(activate)RefreshScene();
    }
    void UpdateClipVolume()
    {
        ActiveClipPlanes=ActiveClipPlane is {} plane?[plane]:[];ActiveClipBox=null;
        if(ClipEnabled&&IsBoxClip)
        {
            var box=CaptureClipBox();
            if(box.IsValid)
            {
                ActiveClipBox=box;
                ActiveClipPlanes=[new(1,0,0,box.MinX),new(-1,0,0,-box.MaxX),new(0,1,0,box.MinY),new(0,-1,0,-box.MaxY),new(0,0,1,box.MinZ),new(0,0,-1,-box.MaxZ)];
                ClipDescription=$"Volume {box.MaxX-box.MinX:G4} × {box.MaxY-box.MinY:G4} × {box.MaxZ-box.MinZ:G4} m. Ritaglio grafico, senza modificare il modello.";
            }
            else ClipDescription="Taglio non applicato: indica valori finiti e un minimo inferiore al massimo per ogni asse.";
        }
        if(ActiveClipPlanes.Count>0)ClipDescription+=" La scala conserva i campioni degli elementi, anche fuori dal taglio.";
        OnPropertyChanged(nameof(ActiveClipPlanes));OnPropertyChanged(nameof(ActiveClipBox));
    }
}
