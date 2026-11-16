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
    public IReadOnlyList<string> ImportSourceLabels { get; } = ["File · .antheamodel, MIDAS .mct/.mgt o copia acquisita", "MIDAS Civil NX · modello aperto", "Straus7 / Strand7 R3 · file .st7, .lsa e .nla"];
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
    public string ImportActionLabel => IsMidasImport ? "Importa dal modello aperto" : IsStrand7Import ? "Importa da Straus7" : "Scegli il file e importa…";
    public string ImportCasesSummary => $"{ImportCases.Count(c => c.IsSelected)} di {ImportCases.Count} casi selezionati";
    public string LiveImportLimitations => IsStrand7Import
        ? "Statico lineare, casi primari: piastre ai nodi senza media fra elementi, aste alle stazioni di Straus7, spostamenti e rotazioni globali. Reazioni vincolari con il controllo di equilibrio. Le combinazioni LSA sono definizioni: ANTHEA le costruisce con GPC dai casi importati, truss e cavi compresi. Fasi, masse e temperature come dati del modello; dal file .nla i risultati per fase. Brick e link non mappati restano nel file con un avviso."
        : "Casi statici: sollecitazioni locali BEAM/PLATE e spostamenti globali. Stage: scegli fasi, passi e riferimento della deformata; nessuna somma automatica fra stati. Le combinazioni sono acquisite come definizioni. Terne e simboli di vincoli/link richiedono ancora il convertitore GPC distribuito.";

    partial void OnSelectedImportSourceChanged(int value)
    {
        OnPropertyChanged(nameof(IsMidasImport)); OnPropertyChanged(nameof(IsFileImport)); OnPropertyChanged(nameof(ImportActionLabel));
        OnPropertyChanged(nameof(IsStrand7Import)); OnPropertyChanged(nameof(IsLiveImport)); OnPropertyChanged(nameof(LiveImportLimitations)); CheckStrand7SetupCommand.NotifyCanExecuteChanged();
        // The selectable cases belong to the catalog of one source.
        if (strand7Catalog != null || liveCatalog != null)
        {
            strand7Catalog = null; strand7CaseNumbers.Clear(); liveCatalog = null; ImportCases.Clear(); ResetImportStages();
            ConnectionSummary = "Connessione da controllare."; ResetStrand7Preparation();
            OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary)); OnPropertyChanged(nameof(HasStrand7Catalog));
        }
        if(IsStrand7Import && string.IsNullOrWhiteSpace(Strand7ApiPath))Strand7ApiPath=Strand7ImportSetup.FindInstalledApi();
        ImportCommand.NotifyCanExecuteChanged(); InspectMidasCommand.NotifyCanExecuteChanged();
    }
    partial void OnMidasBaseUrlChanged(string value) => InvalidateConnection();
    partial void OnMidasApiKeyChanged(string value) => InvalidateConnection();
    partial void OnImportForcesChanged(bool value) => ImportCommand.NotifyCanExecuteChanged();
    partial void OnImportDisplacementsChanged(bool value) => ImportCommand.NotifyCanExecuteChanged();
    void InvalidateConnection()
    {
        liveCatalog = null; ImportCases.Clear(); ResetImportStages(); ConnectionSummary = "Connessione da controllare.";
        OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary));
        ImportCommand.NotifyCanExecuteChanged(); InspectMidasCommand.NotifyCanExecuteChanged();
    }
    void ImportCaseSelectionChanged() { OnPropertyChanged(nameof(ImportCasesSummary)); ImportCommand.NotifyCanExecuteChanged(); }
    bool HasLiveImportSelection => IsFileImport || IsStrand7Import && HasStrand7Selection || IsMidasImport && liveCatalog != null && (!ImportIncludesResults || ImportsWithoutResults ||
        (ImportCases.Any(c => c.IsSelected) || HasValidStageSelection) && (!ImportConstructionStages || HasValidStageSelection) && (ImportForces || ImportDisplacements));

    /// <summary>
    /// Rilievo R10 (16/11/2026, requirement of the user: «il file deve essere importato anche senza casi di carico»): «Tutto» from a source whose
    /// catalogue offers no result case (a Straus7 file whose result cases the converter does not match, or without a results file; a MIDAS
    /// model without static or stage cases) imports geometry, attributes and loads, without results, instead of staying disabled.
    /// </summary>
    public bool ImportsWithoutResults => SelectedUpdateScope == (int)ModelUpdateScope.Everything &&
        (IsStrand7Import && strand7Catalog is { Cases.Count: 0 } || IsMidasImport && liveCatalog is { StaticCases.Count: 0, StageCases.Count: 0 });

    /// <summary>The scope the import asks the source for: «Geometria + attributi + carichi» for <see cref="ImportsWithoutResults"/>.</summary>
    ModelUpdateScope EffectiveImportScope => ImportsWithoutResults ? ModelUpdateScope.ModelWithoutResults : (ModelUpdateScope)SelectedUpdateScope;

    /// <summary>Why <see cref="ImportsWithoutResults"/>, for the message at the end of the import.</summary>
    string NoResultsReason => IsStrand7Import ? strand7Catalog?.NoResultsReason(!string.IsNullOrWhiteSpace(Strand7ResultsPath)) ?? "" : "il modello aperto in MIDAS non ha casi statici né casi stage";
    bool CanInspectMidas() => IsMidasImport && CanEditImport && !disposed && !IsVerificationRunning && !IsReportRunning && !IsRevisionBusy &&
        !string.IsNullOrWhiteSpace(MidasBaseUrl) && !string.IsNullOrWhiteSpace(MidasApiKey);

    [RelayCommand(CanExecute = nameof(CanInspectMidas), IncludeCancelCommand = true)]
    async Task InspectMidasAsync(CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        var token = cancellation.Token;
        IsBusy = true; ImportFailure = null; liveCatalog = null; ImportCases.Clear(); ResetImportStages();
        OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary));
        try
        {
            var connection = new MidasConnection(MidasBaseUrl, MidasApiKey);
            var catalog = await services.InspectMidasAsync(connection, BeginImportProgress(), token);
            token.ThrowIfCancellationRequested(); if (disposed) return;
            liveCatalog = catalog; PopulateImportStages(catalog);
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
    IProgress<ModelImportProgress> BeginImportProgress(string initial = "Connessione a MIDAS…")
    {
        int operation = ++importOperation;
        ImportPercent = 0; ImportProgressIndeterminate = true; ImportProgressMessage = initial;
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
            ImportCases.Where(c => c.IsSelected).Select(c => c.Name).ToArray(), ImportForces, ImportDisplacements) { Stages = StageImportRequest() };
        return services.ImportMidasAsync(request, BeginImportProgress(), token);
    }
}
