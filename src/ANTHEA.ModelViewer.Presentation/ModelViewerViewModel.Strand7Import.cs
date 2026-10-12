using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    [ObservableProperty] string strand7ModelPath = "";
    [ObservableProperty] string strand7ResultsPath = "";
    [ObservableProperty] string strand7ApiPath = "";
    [ObservableProperty] string strand7PreparationSummary = "Seleziona il modello .st7, gli eventuali risultati .lsa e l’API R3 a 64 bit. La lettura nativa è predisposta ma non ancora attiva in questa build.";
    public bool IsStrand7Import => SelectedImportSource == 2;
    partial void OnStrand7ModelPathChanged(string value) => ResetStrand7Preparation();
    partial void OnStrand7ResultsPathChanged(string value) => ResetStrand7Preparation();
    partial void OnStrand7ApiPathChanged(string value) => ResetStrand7Preparation();
    void ResetStrand7Preparation() => Strand7PreparationSummary = "File da controllare. Nessun dato Straus/Strand7 è stato letto.";

    [RelayCommand] void ChooseStrand7Model() { if (CanEditImport && services.ChooseStrand7File(Strand7ImportFile.Model) is {} path) Strand7ModelPath = path; }
    [RelayCommand] void ChooseStrand7Results() { if (CanEditImport && services.ChooseStrand7File(Strand7ImportFile.Results) is {} path) Strand7ResultsPath = path; }
    [RelayCommand] void ChooseStrand7Api() { if (CanEditImport && services.ChooseStrand7File(Strand7ImportFile.ApiLibrary) is {} path) Strand7ApiPath = path; }
    bool CanCheckStrand7() => CanEditImport && IsStrand7Import && !disposed;
    [RelayCommand(CanExecute = nameof(CanCheckStrand7), IncludeCancelCommand = true)]
    async Task CheckStrand7SetupAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        var token = linked.Token;
        var request = new Strand7ImportRequest(Strand7ModelPath, Strand7ResultsPath, Strand7ApiPath, (ModelUpdateScope)SelectedUpdateScope, [], ImportForces, ImportDisplacements);
        IsBusy = true;
        ++importOperation; ImportProgressMessage="Controllo dei file Straus / Strand7…";ImportProgressIndeterminate=true;ImportPercent=0;
        try
        {
            var result = await Task.Run(() => Strand7ImportSetup.Check(request, token), token);
            if (!disposed) Strand7PreparationSummary = result.Summary + (result.Issues.Count > 0 ? "\n" + string.Join("\n", result.Issues) : "");
        }
        catch (OperationCanceledException) { if (!disposed) Strand7PreparationSummary = "Controllo dei file annullato."; }
        catch (Exception) { if (!disposed) Strand7PreparationSummary = "Impossibile controllare i file. Verifica i percorsi e i permessi di lettura."; }
        finally { IsBusy = false; }
    }
}
