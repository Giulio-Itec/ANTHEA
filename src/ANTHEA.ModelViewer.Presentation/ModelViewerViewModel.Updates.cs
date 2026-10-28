using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> UpdateScopeLabels {get;}=["Solo geometria","Geometria + attributi + carichi","Solo risultati","Tutto"];
    [ObservableProperty] int selectedUpdateScope=3;
    [ObservableProperty] bool showImportOptions;
    public bool CanChooseUpdateScope=>CanEditImport;
    public string UpdateScopeDescription=>SelectedUpdateScope==2 && Snapshot==null?"Apri prima il modello al quale associare i risultati.":ModelSnapshotUpdate.Description((ModelUpdateScope)SelectedUpdateScope);
    public string UpdateProvenance=>Snapshot?.UpdateInfo is {} info
        ? "Ultimo aggiornamento: "+UpdateScopeLabels[(int)info.Scope]+(info.ResultsCompatibilityVerified?" · compatibilità verificata":"")+(info.DiscardedResultCases>0?$" · {info.DiscardedResultCases} casi precedenti rimossi":"")+". Copia letta: "+info.InputSource
        : "La copia comprende i dati presenti nel pacchetto importato.";
    partial void OnSelectedUpdateScopeChanged(int value)
    {OnPropertyChanged(nameof(UpdateScopeDescription));OnPropertyChanged(nameof(ImportIncludesResults));if(strand7Catalog==null)ResetStrand7Preparation();ImportCommand.NotifyCanExecuteChanged();}
    [RelayCommand] void OpenImportOptions()
    {SelectedPanel=0;IsNavigationCollapsed=false;ShowCombinationManager=false;ShowImportOptions=true;}
    public bool CanCloseImportOptions=>!IsBusy;
    [RelayCommand(CanExecute=nameof(CanCloseImportOptions))] void CloseImportOptions() { if(!IsBusy)ShowImportOptions=false; }
}
