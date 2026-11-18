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
    [ObservableProperty] string strand7PreparationSummary = "Seleziona il modello .st7, gli eventuali risultati .lsa (statici lineari) o .nla (non lineari per fasi) e l’API R3 a 64 bit, poi controlla file e casi.";
    public bool IsStrand7Import => SelectedImportSource == 2;
    public bool IsLiveImport => IsMidasImport || IsStrand7Import;
    public bool HasStrand7Catalog => strand7Catalog != null;
    partial void OnStrand7ModelPathChanged(string value) => ResetStrand7Preparation();
    partial void OnStrand7ResultsPathChanged(string value) => ResetStrand7Preparation();
    partial void OnStrand7ApiPathChanged(string value) => ResetStrand7Preparation();
    void ResetStrand7Preparation()
    {
        Strand7PreparationSummary = "File da controllare. Nessun dato Straus/Strand7 è stato letto."; Strand7ExcludedCases = "";
        if (strand7Catalog == null) return;
        strand7Catalog = null; strand7CaseNumbers.Clear(); Strand7Warnings = "";
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
        // R10: «Tutto» without a results file checks the model as «Geometria + attributi + carichi» (the import takes no results then).
        var scope = (ModelUpdateScope)SelectedUpdateScope;
        if (scope == ModelUpdateScope.Everything && string.IsNullOrWhiteSpace(Strand7ResultsPath)) scope = ModelUpdateScope.ModelWithoutResults;
        var request = Strand7Request(scope);
        Strand7Warnings = ""; Strand7ExcludedCases = "";
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
            // 17/11/2026 (GPC.Converter 2.24): a case of several freedom cases shows its freedom case («5: G1 · FC 2»); the name stays the key.
            foreach (var item in catalog.Cases) { strand7CaseNumbers[item.Name] = item.Number; ImportCases.Add(new(item.Name, ImportCaseSelectionChanged, item.Label)); }
            // R9 (16/11/2026): why there is no result to import, and the warnings of the converter here, before the import (until 1.19.1 only
            // their number, with a reference to an import report that did not exist yet).
            bool resultsFile = !string.IsNullOrWhiteSpace(Strand7ResultsPath);
            string? reason = catalog.NoResultsReason(resultsFile);
            Strand7PreparationSummary = "Catalogo letto · " + catalog.Summary + (catalog.ApiVersion.Length > 0 ? " · API " + catalog.ApiVersion : "") +
                FreedomCasesSummary(catalog, resultsFile) +
                // R2 of the review of 17/11/2026: on a copy with results «Tutto» stays disabled (ResultsLossWarning says why).
                (reason is null ? "" : "\nNessun caso di risultato da importare: " + reason + (HasCurrentResults
                    ? ". La copia attuale ha risultati: leggi l'avviso sotto «Contenuto da acquisire»."
                    : ". Con «Tutto» si importano geometria, attributi e carichi, senza risultati.")) +
                (reason is null && catalog.ExcludedResultCases.Count > 0 ? "\nCasi di risultato esclusi: " + catalog.ExcludedResultCases.Count + ", elencati qui sotto con il motivo." : "") +
                (catalog.Warnings.Count > 0 ? "\nAvvisi del convertitore GPC: " + catalog.Warnings.Count + ", elencati qui sotto e conservati nei messaggi di acquisizione della copia." : "");
            // Note N2 of the revision: a reason of the converter kept as it is already ends with a full stop.
            Strand7ExcludedCases = string.Join("\n", catalog.ExcludedResultCases.Select(c => "• " + c.Name + ": " + c.Reason + (c.Reason.EndsWith('.') ? "" : ".")));
            Strand7Warnings = string.Join("\n", catalog.Warnings.Select(w => "• " + w));
            ImportProgressMessage = "File pronti. Scegli il contenuto da acquisire."; ImportPercent = 100; ImportProgressIndeterminate = false;
            OnPropertyChanged(nameof(HasImportCases)); OnPropertyChanged(nameof(ImportCasesSummary)); OnPropertyChanged(nameof(HasStrand7Catalog));
        }
        catch (OperationCanceledException) { if (!disposed) Strand7PreparationSummary = "Controllo annullato. Nessun dato Straus/Strand7 è stato letto."; }
        catch (Exception ex) when (ex is not NotSupportedException)
        { if (!disposed) { SetImportFailure(ex); Strand7PreparationSummary = "Controllo non completato. Correggi i file o la configurazione e riprova."; } }
        catch (NotSupportedException) { if (!disposed) Strand7PreparationSummary = "File pronti. La lettura Straus7 non è disponibile in questa finestra."; }
        finally { IsBusy = false; ImportCommand.NotifyCanExecuteChanged(); }
    }

    /// <summary>R9 (16/11/2026): the warnings of the GPC converter of the catalogue, one per line, shown before the import; empty without them.</summary>
    [ObservableProperty] string strand7Warnings = "";

    /// <summary>17/11/2026 (GPC.Converter 2.24): the result cases of the file left out of the catalogue, one per line with the reason of the
    /// converter in Italian; empty when every case is selectable.</summary>
    [ObservableProperty] string strand7ExcludedCases = "";
    public bool HasStrand7ExcludedCases => Strand7ExcludedCases.Length > 0;
    partial void OnStrand7ExcludedCasesChanged(string value) => OnPropertyChanged(nameof(HasStrand7ExcludedCases));

    /// <summary>17/11/2026 (GPC.Converter 2.24): the freedom cases of a model with several of them, for the summary of the catalogue; empty with one.
    /// Revision P2: the freedom cases and the load cases solved under each one are a fact of the model (its linear static settings), so that
    /// line is always there; what the summary says of the result cases follows the catalogue: «· FC n» only when a case has a freedom case,
    /// reactions and equilibrium only for linear static results with selectable cases (the reactions are those of the solver, the check is
    /// the one of GPC), nothing of them for a staged nonlinear solution (.nla: no freedom case in the names, no equilibrium check) or without
    /// a results file, and the preserved restraints and springs only with the warning of the converter.</summary>
    static string FreedomCasesSummary(Strand7ModelCatalog catalog, bool resultsFile)
    {
        if (catalog.FreedomCases.Count == 0) return "";
        var text = new System.Text.StringBuilder("\nPiù casi di vincolo (freedom case) nel modello, con i casi di carico che l’analisi statica lineare del modello risolve sotto ciascuno: ")
            .Append(string.Join("; ", catalog.FreedomCases)).Append('.');
        if (!resultsFile) text.Append(" Senza file dei risultati i freedom case restano dati del modello.");
        else if (catalog.NonlinearStages)
            text.Append(" I risultati sono di un’analisi statica non lineare per fasi (.nla): i casi sono elencati per fase e incremento, senza freedom case, " +
                "il controllo di equilibrio di GPC non si fa per queste soluzioni e le impostazioni dell’analisi statica lineare qui sopra non le riguardano.");
        else if (catalog.Cases.Count > 0)
        {
            text.Append(catalog.Cases.Any(c => c.FreedomCaseLabel != null)
                ? " Ogni caso di risultato è un caso di carico sotto il suo freedom case («· FC n» nell’elenco dei casi)."
                : " I nomi dei casi del file non indicano un freedom case («n: nome», come in una soluzione con un solo freedom case).");
            text.Append(" Le reazioni di ogni caso sono quelle del solutore nel freedom case con cui è risolto; il controllo di equilibrio lo fa GPC con queste reazioni.");
        }
        if (catalog.Warnings.Any(w => w.StartsWith("Straus7FreedomCasesPreserved", StringComparison.Ordinal)))
            text.Append(" Vincoli e molle dei freedom case restano conservati dal convertitore come dati della sorgente, non come vincoli del modello GPC " +
                "(avviso Straus7FreedomCasesPreserved).");
        // Set AZ (GPC.Converter 2.27): without the warning the restraints and springs of every freedom case are in the copy with their freedom case.
        else text.Append(" Vincoli e molle di ogni freedom case entrano nella copia con il loro freedom case: il visore disegna quelli del freedom case del caso scelto.");
        return text.ToString();
    }

    // R10 (16/11/2026, requirement of the user: «il file deve essere importato anche senza casi di carico»): with «Tutto» and a catalogue without
    // result cases the import takes geometry, attributes and loads; with result cases at least one case and one type of result, as before.
    bool HasStrand7Selection => strand7Catalog != null && (!ImportIncludesResults || ImportsWithoutResults ||
        ImportCases.Any(c => c.IsSelected) && (ImportForces || ImportDisplacements));

    Task<ModelSnapshot> ReadStrand7ModelAsync(ModelUpdateScope scope, CancellationToken token)
    {
        if (strand7Catalog == null) throw new InvalidOperationException("Controlla prima file e casi di Straus7.");
        return services.ImportStrand7Async(Strand7Request(scope), BeginImportProgress("Avvio del processo di lettura Straus7…"), token);
    }
}
