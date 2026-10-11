using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record MeasurementReading(ModelMeasurement Definition,string Title,string Value,string Details,bool Available);
public sealed record MeasurementAnnotation(int[] Nodes,string Caption);

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> MeasurementKinds {get;}=["Distanza · due nodi","Angolo · tre nodi"];
    public IReadOnlyList<string> MeasurementBases {get;}=["Geometria originale","Deformata reale · ×1"];
    public IReadOnlyList<string> MeasurementUnits {get;}=["m","cm","mm"];
    public ObservableCollection<ModelMeasurement> Measurements {get;}=[];
    public IReadOnlyList<MeasurementReading> MeasurementReadings {get;private set;}=[];
    public IReadOnlyList<MeasurementAnnotation> MeasurementAnnotations=>ShowMeasurements?MeasurementReadings.Where(r=>r.Available).Select(r=>new MeasurementAnnotation(r.Definition.Nodes,(r.Definition.Kind==0?"L ":"Angolo ")+r.Value+" | N "+string.Join("-",r.Definition.Nodes)+"\n"+MeasurementBasisLabel)).ToArray():[];
    public IReadOnlyList<int> PendingMeasurementNodes=>pendingMeasurementNodes;
    public string MeasurementBasisLabel=>MeasurementBasis==1?$"{SelectedCase} · deformata ×1":"Geometria originale";
    public string MeasurementPrompt=>!IsMeasuring?"Scegli il tipo, poi indica i nodi nel modello o inserisci gli ID.":
        SelectedMeasurementKind==0?$"Distanza: scegli il nodo {pendingMeasurementNodes.Count+1}/2. Esc termina.":$"Angolo: scegli il nodo {pendingMeasurementNodes.Count+1}/3. Il secondo è il vertice. Esc termina.";
    public string MeasurementNodesLabel=>pendingMeasurementNodes.Count==0?"":"Nodi scelti: "+string.Join(" → ",pendingMeasurementNodes);
    [ObservableProperty] private int selectedMeasurementKind;
    [ObservableProperty] private int measurementBasis;
    [ObservableProperty] private string measurementUnit="m";
    [ObservableProperty] private string measurementNodeIds="";
    [ObservableProperty] private string measurementMessage="";
    [ObservableProperty] private bool showMeasurements=true;
    [ObservableProperty] private bool isMeasuring;
    readonly List<int> pendingMeasurementNodes=[];
    partial void OnSelectedMeasurementKindChanged(int value){pendingMeasurementNodes.Clear();RefreshScene();}
    partial void OnMeasurementBasisChanged(int value)=>RefreshScene();
    partial void OnMeasurementUnitChanged(string value)=>RefreshScene();
    partial void OnShowMeasurementsChanged(bool value)=>RefreshScene();
    [RelayCommand] void StartMeasurement(){PauseAnimation();IsMeasuring=true;ShowMeasurements=true;MeasurementMessage="";pendingMeasurementNodes.Clear();RefreshScene();}
    [RelayCommand] void CancelMeasurement(){IsMeasuring=false;pendingMeasurementNodes.Clear();RefreshScene();}
    [RelayCommand] void ClearMeasurements(){Measurements.Clear();pendingMeasurementNodes.Clear();MeasurementMessage="";RefreshScene();}
    [RelayCommand] void RemoveMeasurement(ModelMeasurement? item){if(item!=null)Measurements.Remove(item);RefreshScene();}
    [RelayCommand] void AddMeasurementIds()
    {
        var ids=MeasurementNodeIds.Split([' ',',',';'],StringSplitOptions.RemoveEmptyEntries);
        if(ids.Length!=(SelectedMeasurementKind==0?2:3) || ids.Any(s=>!int.TryParse(s,out int n)||n<=0))
        {MeasurementMessage=SelectedMeasurementKind==0?"Inserisci due ID nodali, separati da uno spazio.":"Inserisci tre ID nodali: estremo, vertice, estremo.";return;}
        TryAddMeasurement(ids.Select(int.Parse).ToArray());
    }
    public void AddMeasurementNode(int node)
    {
        if(!IsMeasuring || Snapshot?.Nodes.Any(n=>n.Id==node)!=true)return;
        if(pendingMeasurementNodes.Contains(node)){MeasurementMessage="Scegli un nodo distinto.";return;}
        pendingMeasurementNodes.Add(node);MeasurementMessage="";
        if(pendingMeasurementNodes.Count==(SelectedMeasurementKind==0?2:3))
        {TryAddMeasurement(pendingMeasurementNodes.ToArray());pendingMeasurementNodes.Clear();}
        RefreshScene();
    }
    void TryAddMeasurement(int[] nodes)
    {
        if(Snapshot==null)return;
        if(Measurements.Count>=20){MeasurementMessage="Sono già presenti venti misure. Rimuovine una prima di aggiungerne altre.";return;}
        if(nodes.Distinct().Count()!=nodes.Length || nodes.Any(id=>!Snapshot.Nodes.Any(n=>n.Id==id)))
        {MeasurementMessage="Usa nodi distinti presenti nel modello.";return;}
        Measurements.Add(new(SelectedMeasurementKind,nodes));ShowMeasurements=true;MeasurementNodeIds="";MeasurementMessage="Misura aggiunta.";RefreshScene();
    }
    ModelMeasurementSettings CaptureMeasurements()=>new(ShowMeasurements,MeasurementBasis,MeasurementUnit,Measurements.Select(m=>m with {Nodes=m.Nodes.ToArray()}).ToArray());
    void ApplyMeasurements(ModelMeasurementSettings? settings)
    {
        Measurements.Clear();pendingMeasurementNodes.Clear();IsMeasuring=false;
        ShowMeasurements=settings?.Visible??true;MeasurementBasis=settings?.Basis??0;MeasurementUnit=settings?.Unit??"m";
        foreach(var measurement in settings?.Items??[])Measurements.Add(measurement with {Nodes=measurement.Nodes.ToArray()});
    }
    void UpdateMeasurements()
    {
        double factor=MeasurementUnit=="mm"?1000:MeasurementUnit=="cm"?100:1;
        MeasurementReadings=Measurements.Select(m=>
        {
            var result=ModelMeasurements.Read(Snapshot!,m,MeasurementBasis,SelectedCase);
            string title=(m.Kind==0?"N ":"Angolo N ")+string.Join(" → ",m.Nodes);
            string value=!result.Available?"Non disponibile":m.Kind==0?$"{result.Value*factor:G7} {MeasurementUnit}":$"{result.Value:G7}°";
            string details=result.Delta is {} d?$"ΔX {d.X*factor:G7} · ΔY {d.Y*factor:G7} · ΔZ {d.Z*factor:G7} {MeasurementUnit}":result.Available?"Vertice: nodo "+m.Nodes[1]:result.Message;
            return new MeasurementReading(m,title,value,details,result.Available);
        }).ToArray();
        foreach(string name in new[]{nameof(MeasurementReadings),nameof(MeasurementAnnotations),nameof(PendingMeasurementNodes),nameof(MeasurementPrompt),nameof(MeasurementNodesLabel),nameof(MeasurementBasisLabel)})OnPropertyChanged(name);
    }
}
