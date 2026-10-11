using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    private GraphicalLoadData? graphicalLoads;
    public IReadOnlyList<string> LoadCases {get;private set;}=[];
    public IReadOnlyList<GraphicalLoad> ActiveLoads {get;private set;}=[];
    public IReadOnlyList<ModelDisplacement> Displacements {get;private set;}=[];
    public bool HasDisplacements=>Displacements.Count>0;
    public string DeformationMessage=>HasDisplacements?$"{Displacements.Count:N0} spostamenti nodali globali importati [m]. Deformata nodale; nessuna forma interna ricostruita.":"Spostamenti non acquisiti per questo caso. La deformata sarà disponibile con dati nodali importati.";
    public string LoadsMessage {get;private set;}="";
    [ObservableProperty] private bool showLoads;
    [ObservableProperty] private string? selectedLoadCase;
    [ObservableProperty] private double loadGlyphScale=1;
    [ObservableProperty] private bool showDeformation;
    [ObservableProperty] private double deformationScale=100;
    partial void OnShowLoadsChanged(bool value)=>RefreshScene();
    partial void OnSelectedLoadCaseChanged(string? value)=>RefreshScene();
    partial void OnLoadGlyphScaleChanged(double value)=>RefreshScene();
    partial void OnShowDeformationChanged(bool value)=>RefreshScene();
    partial void OnDeformationScaleChanged(double value)=>RefreshScene();
    void InitializeLoadGraphics()
    {
        graphicalLoads=MidasGraphicalLoads.Read(Snapshot!);
        LoadCases=graphicalLoads.Loads.Select(l=>l.Case).Concat(graphicalLoads.Unsupported.Keys).Distinct().Order().ToArray();
        SelectedLoadCase=LoadCases.FirstOrDefault();OnPropertyChanged(nameof(LoadCases));
    }
    void UpdateLoadGraphics()
    {
        ActiveLoads=ShowLoads?graphicalLoads?.Loads.Where(l=>l.Case==SelectedLoadCase).ToArray()??[]:[];
        int skipped=graphicalLoads?.Unsupported.GetValueOrDefault(SelectedLoadCase??"")??0;
        LoadsMessage=$"Carichi del caso {SelectedLoadCase??"—"} · simboli normalizzati per tipo"+(skipped>0?$"\n{skipped:N0} assegnazioni non rappresentate: consultabili nelle tabelle originali.":"");
        Displacements=Snapshot?.Results.FirstOrDefault(r=>r.Name==SelectedCase)?.Displacements??[];
        foreach(string p in new[]{nameof(ActiveLoads),nameof(LoadsMessage),nameof(Displacements),nameof(HasDisplacements),nameof(DeformationMessage)})OnPropertyChanged(p);
    }
}
