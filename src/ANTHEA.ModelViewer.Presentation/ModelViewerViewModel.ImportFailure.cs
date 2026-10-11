using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record ModelImportFailure(string Title,string Recovery,string RetainedData);
public sealed partial class ModelViewerViewModel
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasImportFailure))] private ModelImportFailure? importFailure;
    public bool HasImportFailure => ImportFailure != null;
    void SetImportFailure(Exception exception)
    {
        ImportFailure = exception is ModelSourceException source
            ? new(source.Message,source.Recovery,Snapshot==null?"Nessun dato importato.":"L’ultima copia valida rimane visibile. Nessun aggiornamento parziale è stato applicato.")
            : new("Importazione non completata",exception.Message,Snapshot==null?"Nessun dato importato.":"L’ultima copia valida rimane visibile.");
    }
    [RelayCommand] void DismissImportFailure() => ImportFailure=null;
}
