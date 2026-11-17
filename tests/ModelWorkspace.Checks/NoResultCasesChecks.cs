using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

/// <summary>
/// Rilievi R9 e R10 del 16/11/2026 (registro MOD-RIL-R9, MOD-RIL-R10): a Straus7 catalogue without selectable result cases says why and
/// shows the warnings of the converter; «Tutto» imports geometry, attributes and loads without results (MIDAS too).
/// </summary>
static partial class Program
{
    sealed class NoResultServices : IModelViewerServices
    {
        public Strand7ModelCatalog Catalog = new(12, 6, [], "SYNTHETICGEOMETRY/SYNTHETICRESULTS");
        public MidasModelCatalog Midas = new(4, 2, [], ["PLATE", "BEAM"], "MIDAS-SIGNATURE");
        public Strand7ImportRequest? Read; public MidasImportRequest? MidasRead;
        public string? ChooseStrand7File(Strand7ImportFile kind) => kind switch { Strand7ImportFile.Model => strand7Model, Strand7ImportFile.Results => strand7Results, _ => strand7Api };
        public string strand7Model = "", strand7Results = "", strand7Api = "";
        public Task<Strand7ModelCatalog> InspectStrand7Async(Strand7ImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token) => Task.FromResult(Catalog);
        // As the worker: results only when they are asked (R2 of the review of 17/11/2026: a copy imported without results has none).
        public Task<ModelSnapshot> ImportStrand7Async(Strand7ImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token)
        { Read = request; return Task.FromResult(Strand7Sample.Snapshot(request.Scope is ModelUpdateScope.Everything or ModelUpdateScope.Results)); }
        public Task<MidasModelCatalog> InspectMidasAsync(MidasConnection connection, IProgress<ModelImportProgress> progress, CancellationToken token) => Task.FromResult(Midas);
        public Task<ModelSnapshot> ImportMidasAsync(MidasImportRequest request, IProgress<ModelImportProgress> progress, CancellationToken token) { MidasRead = request; var s = CombinationModel(); s.Results = []; return Task.FromResult(s); }
        public Task<ModelSnapshot?> ImportAsync(CancellationToken token) => Task.FromResult<ModelSnapshot?>(null);
        public void FitView() { }
        public void SaveImage() { }
        public void ExportTable(string name, string csv) { }
    }

    static async Task NoResultCasesChecks(string output)
    {
        // The Straus7 file of the user (15:15, 16/11): 15 load cases, 7 LSA combinations, two freedom cases, every result case a load case ×
        // freedom case pair the converter does not match: no selectable case.
        string folder = Path.Combine(output, "rilievi-r9-r10"); Directory.CreateDirectory(folder);
        string File(string name) { string path = Path.Combine(folder, name); System.IO.File.WriteAllBytes(path, [1, 2, 3]); return path; }
        string api = Path.Combine(folder, "St7api.dll");
        // A native x64 library in place of St7api.dll, as Strand7ImportChecks: the prerequisites of the files pass, nothing native is loaded.
        System.IO.File.Copy(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll"), api, true);
        var services = new NoResultServices { strand7Model = File("modello.st7"), strand7Results = File("modello.lsa"), strand7Api = api };
        string[] unmapped = ["1: [1: G1] [1: Freedom Case 1]", "5: [5: G1] [2: Freedom Case 1]"];
        services.Catalog = new Strand7ModelCatalog(2563, 5238, [], "SYNTHETICGEOMETRY/SYNTHETICRESULTS")
        {
            Beams = 96, Plates = 5142, Combinations = 7, LoadCases = ["1: G1", "2: G2", "3: Q", "4: Q"],
            Warnings = ["Straus7FreedomCasesPreserved: 429 record di vincoli e 4 molle non mappati.", "Casi di risultato senza un caso di carico corrispondente, non selezionabili: " + string.Join(", ", unmapped) + "."],
            UnmappedResultCases = unmapped
        };
        int commits = 0;
        using var vm = new ModelViewerViewModel(null, [], false, services, _ => commits++, (_, _, _, _) => { });
        vm.SelectedImportSource = 2;
        vm.ChooseStrand7ModelCommand.Execute(null); vm.ChooseStrand7ResultsCommand.Execute(null); vm.ChooseStrand7ApiCommand.Execute(null);
        await vm.CheckStrand7SetupCommand.ExecuteAsync(null);
        Check(vm.HasStrand7Catalog && vm.ImportCases.Count == 0, "R9: the catalogue of the model without selectable result cases is read");
        string? reason = services.Catalog.NoResultsReason(true);
        Check(reason is not null && reason.Contains("nessuno dei 2 casi di risultato", StringComparison.Ordinal) && reason.Contains("freedom case", StringComparison.Ordinal) &&
              reason.Contains(unmapped[0], StringComparison.Ordinal), "R9: without selectable cases the catalogue says why: several freedom cases, the result cases left out");
        Check(new Strand7ModelCatalog(1, 1, [], "S").NoResultsReason(false)!.Contains("nessun file dei risultati", StringComparison.Ordinal) &&
              new Strand7ModelCatalog(1, 1, [], "S").NoResultsReason(true) == "il modello non ha casi di carico" &&
              new Strand7ModelCatalog(1, 1, [new(1, "1: G")], "S").NoResultsReason(true) is null, "R9/R10: the reason without a results file, without load cases, none with cases");
        Check(vm.Strand7PreparationSummary.Contains("Nessun caso di risultato da importare", StringComparison.Ordinal) && vm.Strand7Warnings.Contains("Straus7FreedomCasesPreserved", StringComparison.Ordinal) &&
              !vm.Strand7PreparationSummary.Contains("rapporto di importazione", StringComparison.Ordinal), "R9: the panel says why and lists the warnings of the converter before the import");
        Check(vm.SelectedUpdateScope == 3 && vm.ImportsWithoutResults && vm.ImportCommand.CanExecute(null), "R10: «Tutto» without selectable cases can import");
        await vm.ImportCommand.ExecuteAsync(null);
        Check(commits == 1 && services.Read!.Scope == ModelUpdateScope.ModelWithoutResults && services.Read.CaseNumbers.Count == 0 &&
              vm.Status.StartsWith("Importati geometria, attributi e carichi, senza risultati", StringComparison.Ordinal), "R10: geometry, attributes and loads imported without results, with the reason");
        vm.SelectedUpdateScope = 2;
        Check(!vm.ImportCommand.CanExecute(null), "R10: «Solo risultati» without cases stays disabled");
        // Without a results file «Tutto» checks and imports the model as «Geometria + attributi + carichi».
        vm.SelectedUpdateScope = 3; vm.Strand7ResultsPath = "";
        await vm.CheckStrand7SetupCommand.ExecuteAsync(null);
        Check(vm.HasStrand7Catalog && vm.ImportCommand.CanExecute(null) && vm.Strand7PreparationSummary.Contains("nessun file dei risultati", StringComparison.Ordinal),
            "R10: «Tutto» without a results file: the catalogue is read and the import is possible, without results");
        // MIDAS: a model without static or stage cases imports with «Tutto» in the same way.
        int midasCommits = 0;
        using var midas = new ModelViewerViewModel(null, [], false, services, _ => midasCommits++, (_, _, _, _) => { });
        midas.SelectedImportSource = 1; midas.MidasApiKey = "chiave-di-prova";
        await midas.InspectMidasCommand.ExecuteAsync(null);
        Check(midas.ImportsWithoutResults && midas.ImportCommand.CanExecute(null), "R10: MIDAS without static cases: «Tutto» can import");
        await midas.ImportCommand.ExecuteAsync(null);
        Check(midasCommits == 1 && services.MidasRead!.Scope == ModelUpdateScope.ModelWithoutResults && midas.Status.StartsWith("Importati geometria", StringComparison.Ordinal),
            "R10: MIDAS without static cases imports geometry, attributes and loads, without results");
        services.Midas = new MidasModelCatalog(4, 2, ["G", "Q"], ["PLATE"], "MIDAS-SIGNATURE");
        await midas.InspectMidasCommand.ExecuteAsync(null);
        Check(!midas.ImportsWithoutResults, "R10: with static cases the import of results stays as before");

        // R2 of the review of 17/11/2026: «Aggiorna modello…» with «Tutto» from a source without result cases on a copy that has results says,
        // before the import, which results would go, and «Importa» stays disabled; «Geometria + attributi + carichi» is the explicit choice.
        var withResults = Strand7Sample.Snapshot();
        using var update = new ModelViewerViewModel(withResults, [], false, services, _ => { }, (_, _, _, _) => { });
        update.SelectedImportSource = 2;
        update.ChooseStrand7ModelCommand.Execute(null); update.ChooseStrand7ResultsCommand.Execute(null); update.ChooseStrand7ApiCommand.Execute(null);
        await update.CheckStrand7SetupCommand.ExecuteAsync(null);
        Check(update.HasStrand7Catalog && update.SelectedUpdateScope == 3 && !update.ImportsWithoutResults && !update.ImportCommand.CanExecute(null) &&
              update.HasResultsLossWarning && update.ResultsLossWarning.Contains($"{withResults.Results.Length} casi", StringComparison.Ordinal) &&
              update.ResultsLossWarning.Contains("«Geometria + attributi + carichi»", StringComparison.Ordinal) &&
              update.Strand7PreparationSummary.Contains("leggi l'avviso", StringComparison.Ordinal),
            "R2 (review): «Tutto» without result cases on a copy with results stays disabled and says first which results would go");
        update.SelectedUpdateScope = (int)ModelUpdateScope.ModelWithoutResults;
        Check(update.ImportCommand.CanExecute(null) && !update.HasResultsLossWarning, "R2 (review): the explicit «Geometria + attributi + carichi» can update the copy");
        services.Midas = new MidasModelCatalog(4, 2, [], ["PLATE", "BEAM"], "MIDAS-SIGNATURE");
        using var midasUpdate = new ModelViewerViewModel(withResults, [], false, services, _ => { }, (_, _, _, _) => { });
        midasUpdate.SelectedImportSource = 1; midasUpdate.MidasApiKey = "chiave-di-prova";
        await midasUpdate.InspectMidasCommand.ExecuteAsync(null);
        Check(!midasUpdate.ImportCommand.CanExecute(null) && midasUpdate.HasResultsLossWarning &&
              midasUpdate.ConnectionSummary.Contains("Nessun caso di risultato da importare", StringComparison.Ordinal),
            "R2 (review): MIDAS without static cases on a copy with results: the reason and the warning before the import, «Tutto» disabled");
    }
}
