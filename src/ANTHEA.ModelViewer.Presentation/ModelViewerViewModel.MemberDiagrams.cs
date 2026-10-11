using System.Globalization;
using System.Text;
using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    [ObservableProperty] private bool showMemberDiagram;
    [ObservableProperty] private int? memberDiagramElementId;
    [ObservableProperty] private bool memberDiagramAllComponents;
    [ObservableProperty] private bool memberDiagramInvert;
    [ObservableProperty] private bool memberDiagramFill=true;
    [ObservableProperty] private bool memberDiagramCursorLocked;
    [ObservableProperty] private double memberDiagramStation=.5;
    [ObservableProperty] private IReadOnlyList<MemberDiagramChoice> memberDiagramChoices=[];
    [ObservableProperty] private IReadOnlyList<MemberDiagramSeries> memberDiagramSeries=[];
    [ObservableProperty] private IReadOnlyList<MemberDiagramReading> memberDiagramReadings=[];
    [ObservableProperty] private string memberDiagramTitle="Diagrammi dell’asta";
    [ObservableProperty] private string memberDiagramMessage="Seleziona un’asta nel modello.";
    public bool HasMemberDiagram => MemberDiagramSeries.Count>0;
    public string MemberDiagramPosition => $"x/L = {MemberDiagramStation:0.0000} · x = {MemberDiagramStation*memberDiagramLength:0.####} m da I";
    public int MemberDiagramColumns => MemberDiagramAllComponents ? 3 : 1;
    public double MemberDiagramHeight => MemberDiagramAllComponents ? 460 : 330;
    public double MemberDiagramPlotHeight => MemberDiagramAllComponents ? 160 : 185;
    private double memberDiagramLength;
    private bool updatingMemberDiagram;
    private IReadOnlyList<MemberDiagramSeries> allMemberSeries=[];
    partial void OnShowMemberDiagramChanged(bool value)
    {
        if(value){ShowTable=false;ShowRevisionDetails=false;PauseAnimation();UpdateMemberDiagram();}
    }
    partial void OnMemberDiagramElementIdChanged(int? value)
    {
        if(updatingMemberDiagram)return;
        if(value is int id && Snapshot?.Elements.FirstOrDefault(e=>e.Id==id) is {} element)
        {
            bool before=updating;updating=true;
            SelectedResultType=0;SelectedFamily=Array.IndexOf(FamilyKeys,element.Type);SelectedAxes=0;
            SelectElement(id);updating=before;ShowInspector=false;RefreshScene();
        }
        UpdateMemberDiagram();
    }
    partial void OnMemberDiagramAllComponentsChanged(bool value){OnPropertyChanged(nameof(MemberDiagramColumns));OnPropertyChanged(nameof(MemberDiagramHeight));OnPropertyChanged(nameof(MemberDiagramPlotHeight));UpdateMemberDiagram();}
    partial void OnMemberDiagramStationChanged(double value)
    {
        if(!double.IsFinite(value) || value is <0 or >1){MemberDiagramStation=double.IsFinite(value)?Math.Clamp(value,0,1):.5;return;}
        UpdateMemberReadings();
    }
    [RelayCommand] void OpenMemberDiagram()
    {
        if(Snapshot==null)return;
        SelectedResultType=0;
        var line=Snapshot.Elements.FirstOrDefault(e=>SelectedElementIds.Contains(e.Id)&&ElementFamilies.IsLine(e.Type));
        SelectedFamily=line==null?Math.Max(1,SelectedFamily):Array.IndexOf(FamilyKeys,line.Type);SelectedAxes=0;
        SelectedPanel=1;IsNavigationCollapsed=false;ShowMemberDiagram=true;
    }
    [RelayCommand] void CloseMemberDiagram()=>ShowMemberDiagram=false;
    void UpdateMemberDiagram()
    {
        if(!ShowMemberDiagram || updatingMemberDiagram || Snapshot==null)return;
        updatingMemberDiagram=true;
        try
        {
            var candidates=Snapshot.Elements.Where(e=>ElementFamilies.IsLine(e.Type) && (VisibleElementIds==null || VisibleElementIds.Contains(e.Id))).ToArray();
            var choices=candidates.Select(e=>new MemberDiagramChoice(e.Id,e.Type,e.Property)).ToArray();
            if(!MemberDiagramChoices.SequenceEqual(choices))MemberDiagramChoices=choices;
            var selected=candidates.Where(e=>SelectedElementIds.Contains(e.Id)).ToArray();
            if(selected.Length==1)MemberDiagramElementId=selected[0].Id;
            else if(!candidates.Any(e=>e.Id==MemberDiagramElementId))MemberDiagramElementId=null;
            var element=candidates.FirstOrDefault(e=>e.Id==MemberDiagramElementId);
            allMemberSeries=element==null?[]:global::ANTHEA.ModelViewer.Presentation.MemberDiagramSeries.Read(Snapshot,element.Id,SelectedCase,ForceUnit,LengthUnit);
            // Components and units are the same controls used by the 3D view; the plot always states local axes.
            MemberDiagramSeries=MemberDiagramAllComponents?allMemberSeries:allMemberSeries.Where(s=>s.Component==SelectedComponent).ToArray();
            var nodes=element==null?[]:Snapshot.Nodes.Where(n=>element.Nodes.Contains(n.Id)).ToArray();
            memberDiagramLength=nodes.Length==2?Math.Sqrt(Math.Pow(nodes[1].X-nodes[0].X,2)+Math.Pow(nodes[1].Y-nodes[0].Y,2)+Math.Pow(nodes[1].Z-nodes[0].Z,2)):0;
            MemberDiagramTitle=element==null?"Diagrammi dell’asta":$"{element.Type} {element.Id} · I {element.Nodes[0]} → J {element.Nodes[1]} · L {memberDiagramLength:0.####} m · {SelectedCase} · assi locali";
            MemberDiagramMessage=element==null?"Seleziona un’asta nel modello oppure nell’elenco."
                :allMemberSeries.Count==0?"Sollecitazioni locali non importate per questa asta e questo caso."
                :!HasMemberDiagram?"Scegli una componente dell’asta nella scheda Risultati oppure attiva «Tutte le componenti»."
                :"Punti = campioni importati. Segmenti = interpolazione grafica; nessuna estensione oltre le stazioni disponibili."
                    +(MemberDiagramSeries.Any(s=>s.HasAmbiguousStations)?" Stazioni con valori multipli: sola rappresentazione dei punti, senza interpolazione.":"")
                    +(Snapshot.Results.First(r=>r.Name==SelectedCase).IsEnvelope?" Inviluppo: estremi delle componenti non simultanei.":"");
            OnPropertyChanged(nameof(HasMemberDiagram));UpdateMemberReadings();
        }
        finally{updatingMemberDiagram=false;}
    }
    void UpdateMemberReadings()
    {
        MemberDiagramReadings=MemberDiagramSeries.Select(s=>
        {
            var reading=s.At(MemberDiagramStation);
            return new MemberDiagramReading(s.Component,s.Unit,reading.Value?.ToString("G7",CultureInfo.CurrentCulture)??"—",reading.Value==null?"Nessun campione univoco":reading.Imported?"Importato":"Interpolazione grafica");
        }).ToArray();
        OnPropertyChanged(nameof(MemberDiagramPosition));
    }
    [RelayCommand] void ExportMemberDiagram()
    {
        if(MemberDiagramElementId is not int id || allMemberSeries.Count==0)return;
        static string Csv(string value)=>"\""+value.Replace("\"","\"\"")+"\"";
        var text=new StringBuilder("Elemento;Caso;Assi;Componente;Unita;x/L;Nodo;Valore\r\n");
        foreach(var series in allMemberSeries)foreach(var sample in series.Samples)
            text.AppendLine(string.Join(";",id,Csv(SelectedCase??""),"LOCAL",series.Component,Csv(series.Unit),sample.Station.ToString("R",CultureInfo.InvariantCulture),sample.Node==0?"":sample.Node.ToString(),sample.Value.ToString("R",CultureInfo.InvariantCulture)));
        try{services.ExportTable($"asta-{id}-campioni",text.ToString());Status="Campioni importati dell’asta esportati.";}
        catch(Exception ex){Status="Esportazione non completata: "+ex.Message;}
    }
}
