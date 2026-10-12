using System.Collections.ObjectModel;
using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ImportCaseOption(string name, Action changed) : ObservableObject
{
    public string Name { get; } = name;
    [ObservableProperty] bool isSelected = true;
    partial void OnIsSelectedChanged(bool value) => changed();
}

public sealed partial class ModelViewerViewModel
{
    MidasModelCatalog? liveCatalog;
    int importOperation;
    public IReadOnlyList<string> ImportSourceLabels { get; } = ["File o copia già acquisita", "MIDAS Civil NX · modello aperto", "Straus / Strand7 R3 · predisposizione"];
    [ObservableProperty] int selectedImportSource;
    [ObservableProperty] string midasBaseUrl = "https://moa-engineers.midasit.com/civil";
    // Session only; never included in view presets, reports or the project document.
    [ObservableProperty] string midasApiKey = "";
    [ObservableProperty] string connectionSummary = "Inserisci URL e chiave, poi controlla la connessione.";
    [ObservableProperty] bool importForces = true;
    [ObservableProperty] bool importDisplacements = true;
    [ObservableProperty] string importProgressMessage = "";
    [ObservableProperty] double importPercent;
    [ObservableProperty] bool importProgressIndeterminate;
    public ObservableCollection<ImportCaseOption> ImportCases { get; } = [];
    public bool IsMidasImport => SelectedImportSource == 1;
    public bool IsFileImport => SelectedImportSource == 0;
    public bool HasImportCases => ImportCases.Count > 0;
    public bool ImportIncludesResults => SelectedUpdateScope is 2 or 3;
    public bool CanEditImport => !IsBusy && !IsReadOnly;
    public string ImportActionLabel => IsMidasImport ? "Importa dal modello aperto" : IsStrand7Import ? "Import Straus · da attivare" : "Scegli copia e importa…";
    public string ImportCasesSummary => $"{ImportCases.Count(c => c.IsSelected)} di {ImportCases.Count} casi selezionati";
    public string LiveImportLimitations => "Casi statici: sollecitazioni locali BEAM/PLATE e spostamenti globali. Le combinazioni sono acquisite come definizioni. Terne e simboli di vincoli/link richiedono ancora il convertitore GPC distribuito.";

    partial void OnSelectedImportSourceChanged(int value)
    {
        OnPropertyChanged(nameof(IsMidasImport)); OnPropertyChanged(nameof(IsFileImport)); OnPropertyChanged(nameof(ImportActionLabel));
        OnPropertyChanged(nameof(IsStrand7Import)); CheckStrand7SetupCommand.NotifyCanExecuteChanged();
        if(IsStrand7Import && string.IsNullOrWhiteSpace(Strand7ApiPath))Strand7ApiPath=Strand7ImportSetup.FindInstalledApi();
        ImportCommand.NotifyCanExecuteChanged(); InspectMidasCommand.NotifyCanExecuteChanged();
    }
    partial void OnMidasBaseUrlChanged(string value) => InvalidateConnection();
    partial void OnMidasApiKeyChanged(string value) => InvalidateConnection();
    partial void OnImportForcesChanged(bool value) => ImportCommand.NotifyCanExecuteChanged();
    partial void OnImportDisplacementsChanged(bool value) => ImportCommand.NotifyCanExecuteChanged();
    void InvalidateConnection()
    {
        liveCatalog = null; ImportCases.Clear(); ConnectionSummary = "Connessione da controllare.";
        OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary));
        ImportCommand.NotifyCanExecuteChanged(); InspectMidasCommand.NotifyCanExecuteChanged();
    }
    void ImportCaseSelectionChanged() { OnPropertyChanged(nameof(ImportCasesSummary)); ImportCommand.NotifyCanExecuteChanged(); }
    bool HasLiveImportSelection => IsFileImport || IsMidasImport && liveCatalog != null && (!ImportIncludesResults ||
        ImportCases.Any(c => c.IsSelected) && (ImportForces || ImportDisplacements));
    bool CanInspectMidas() => IsMidasImport && CanEditImport && !disposed && !IsVerificationRunning && !IsReportRunning && !IsRevisionBusy &&
        !string.IsNullOrWhiteSpace(MidasBaseUrl) && !string.IsNullOrWhiteSpace(MidasApiKey);

    [RelayCommand(CanExecute = nameof(CanInspectMidas), IncludeCancelCommand = true)]
    async Task InspectMidasAsync(CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        var token = cancellation.Token;
        IsBusy = true; ImportFailure = null; liveCatalog = null; ImportCases.Clear();
        OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary));
        try
        {
            var connection = new MidasConnection(MidasBaseUrl, MidasApiKey);
            var catalog = await services.InspectMidasAsync(connection, BeginImportProgress(), token);
            token.ThrowIfCancellationRequested(); if (disposed) return;
            liveCatalog = catalog;
            foreach (string name in catalog.StaticCases) ImportCases.Add(new(name, ImportCaseSelectionChanged));
            ConnectionSummary = catalog.Summary + " · " + string.Join(", ", catalog.ElementFamilies);
            ImportProgressMessage = "Connessione pronta. Scegli il contenuto da acquisire."; ImportPercent = 100; ImportProgressIndeterminate = false;
            OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary));
        }
        catch (OperationCanceledException) { if (!disposed) ConnectionSummary = "Controllo annullato."; }
        catch (Exception ex) { if (!disposed) { SetImportFailure(ex); ConnectionSummary = "Connessione non disponibile. Correggi la sessione e riprova."; } }
        finally { IsBusy = false; }
    }

    [RelayCommand] void SelectAllImportCases() { if (CanEditImport) foreach (var item in ImportCases) item.IsSelected = true; }
    [RelayCommand] void ClearImportCases() { if (CanEditImport) foreach (var item in ImportCases) item.IsSelected = false; }
    IProgress<ModelImportProgress> BeginImportProgress()
    {
        int operation = ++importOperation;
        ImportPercent = 0; ImportProgressIndeterminate = true; ImportProgressMessage = "Connessione a MIDAS…";
        return new Progress<ModelImportProgress>(p =>
        {
            if (disposed || !IsBusy || operation != importOperation) return;
            ImportProgressMessage = p.Message; ImportPercent = p.Percent; ImportProgressIndeterminate = p.Total == 0;
        });
    }
    Task<ModelSnapshot> ReadLiveModelAsync(ModelUpdateScope scope, CancellationToken token)
    {
        var request = new MidasImportRequest(new(MidasBaseUrl, MidasApiKey), scope,
            liveCatalog?.Signature ?? throw new InvalidOperationException("Controlla prima la connessione."),
            ImportCases.Where(c => c.IsSelected).Select(c => c.Name).ToArray(), ImportForces, ImportDisplacements);
        return services.ImportMidasAsync(request, BeginImportProgress(), token);
    }
}
