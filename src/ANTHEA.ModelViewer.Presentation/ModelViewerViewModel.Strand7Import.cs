using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    Strand7ModelCatalog? strand7Catalog;
    readonly Dictionary<string, int> strand7CaseNumbers = new(StringComparer.Ordinal);
    [ObservableProperty] string strand7ModelPath = "";
    [ObservableProperty] string strand7ResultsPath = "";
    [ObservableProperty] string strand7ApiPath = "";
    [ObservableProperty] string strand7PreparationSummary = "Seleziona il modello .st7, gli eventuali risultati statici lineari .lsa e l’API R3 a 64 bit, poi controlla file e casi.";
    public bool IsStrand7Import => SelectedImportSource == 2;
    public bool IsLiveImport => IsMidasImport || IsStrand7Import;
    public bool HasStrand7Catalog => strand7Catalog != null;
    partial void OnStrand7ModelPathChanged(string value) => ResetStrand7Preparation();
    partial void OnStrand7ResultsPathChanged(string value) => ResetStrand7Preparation();
    partial void OnStrand7ApiPathChanged(string value) => ResetStrand7Preparation();
    void ResetStrand7Preparation()
    {
        Strand7PreparationSummary = "File da controllare. Nessun dato Straus/Strand7 è stato letto.";
        if (strand7Catalog == null) return;
        strand7Catalog = null; strand7CaseNumbers.Clear();
        if (IsStrand7Import) { ImportCases.Clear(); OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary)); }
        OnPropertyChanged(nameof(HasStrand7Catalog)); ImportCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand] void ChooseStrand7Model() { if (CanEditImport && services.ChooseStrand7File(Strand7ImportFile.Model) is {} path) Strand7ModelPath = path; }
    [RelayCommand] void ChooseStrand7Results() { if (CanEditImport && services.ChooseStrand7File(Strand7ImportFile.Results) is {} path) Strand7ResultsPath = path; }
    [RelayCommand] void ChooseStrand7Api() { if (CanEditImport && services.ChooseStrand7File(Strand7ImportFile.ApiLibrary) is {} path) Strand7ApiPath = path; }
    bool CanCheckStrand7() => CanEditImport && IsStrand7Import && !disposed;
    Strand7ImportRequest Strand7Request(ModelUpdateScope scope) => new(Strand7ModelPath, string.IsNullOrWhiteSpace(Strand7ResultsPath) ? null : Strand7ResultsPath, Strand7ApiPath,
        scope, ImportCases.Where(c => c.IsSelected && strand7CaseNumbers.ContainsKey(c.Name)).Select(c => strand7CaseNumbers[c.Name]).ToArray(), ImportForces, ImportDisplacements)
        { ExpectedSignature = strand7Catalog?.SourceSignature };

    /// <summary>File prerequisites first (no native code), then the catalog read by the Straus7 worker: counts, result cases and combinations.</summary>
    [RelayCommand(CanExecute = nameof(CanCheckStrand7), IncludeCancelCommand = true)]
    async Task CheckStrand7SetupAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        var token = linked.Token;
        var request = Strand7Request((ModelUpdateScope)SelectedUpdateScope);
        IsBusy = true; ImportFailure = null;
        ++importOperation; ImportProgressMessage="Controllo dei file Straus / Strand7…";ImportProgressIndeterminate=true;ImportPercent=0;
        try
        {
            var result = await Task.Run(() => Strand7ImportSetup.Check(request, token), token);
            if (disposed) return;
            Strand7PreparationSummary = result.Summary + (result.Issues.Count > 0 ? "\n" + string.Join("\n", result.Issues) : "");
            if (!result.FilesReady) return;
            var catalog = await services.InspectStrand7Async(request with { CaseNumbers = [] }, BeginImportProgress("Avvio del processo di lettura Straus7…"), token);
            token.ThrowIfCancellationRequested(); if (disposed) return;
            strand7Catalog = catalog; strand7CaseNumbers.Clear(); ImportCases.Clear();
            foreach (var item in catalog.Cases) { strand7CaseNumbers[item.Name] = item.Number; ImportCases.Add(new(item.Name, ImportCaseSelectionChanged)); }
            Strand7PreparationSummary = "Catalogo letto · " + catalog.Summary + (catalog.ApiVersion.Length > 0 ? " · API " + catalog.ApiVersion : "") +
                (catalog.Warnings.Count > 0 ? "\nAvvisi del convertitore GPC: " + catalog.Warnings.Count + " (dettagli nel rapporto di importazione)." : "");
            ImportProgressMessage = "File pronti. Scegli il contenuto da acquisire."; ImportPercent = 100; ImportProgressIndeterminate = false;
            OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary)); OnPropertyChanged(nameof(HasStrand7Catalog));
        }
        catch (OperationCanceledException) { if (!disposed) Strand7PreparationSummary = "Controllo annullato. Nessun dato Straus/Strand7 è stato letto."; }
        catch (Exception ex) when (ex is not NotSupportedException)
        { if (!disposed) { SetImportFailure(ex); Strand7PreparationSummary = "Controllo non completato. Correggi i file o la configurazione e riprova."; } }
        catch (NotSupportedException) { if (!disposed) Strand7PreparationSummary = "File pronti. La lettura Straus7 non è disponibile in questa finestra."; }
        finally { IsBusy = false; ImportCommand.NotifyCanExecuteChanged(); }
    }

    bool HasStrand7Selection => strand7Catalog != null && (!ImportIncludesResults || ImportCases.Any(c => c.IsSelected) && (ImportForces || ImportDisplacements));

    Task<ModelSnapshot> ReadStrand7ModelAsync(ModelUpdateScope scope, CancellationToken token)
    {
        if (strand7Catalog == null) throw new InvalidOperationException("Controlla prima file e casi di Straus7.");
        return services.ImportStrand7Async(Strand7Request(scope), BeginImportProgress("Avvio del processo di lettura Straus7…"), token);
    }
}
