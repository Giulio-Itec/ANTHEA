using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

/// <summary>
/// 17/11/2026, set AY candidate (GPC.Converter 2.24, registro MOD-FC-AY-20261117): a Straus7 catalogue of a model with several freedom cases
/// shows each result case with its freedom case («5: G1 · FC 2»), the freedom cases of the model and the cases left out with the reason of the
/// converter in Italian; the names of the cases in the copy do not change.
/// </summary>
static partial class Program
{
    static async Task FreedomCaseCatalogChecks(string output)
    {
        const string Shared = "The linear static settings solve load case 2 under FC 1, FC 2: GPC.Model keeps the results of a load case, not of each freedom case.";
        const string OneCasePerLoadCase = "il modello GPC tiene i risultati di un caso di carico, non di ogni freedom case";
        const long JsonLimit = 1048576;
        // The reasons of Straus7LinearStaticResults.Cases (GPC.Converter 2.24) as the converter writes them.
        Check(Strand7Snapshot.ExclusionReason("No load case of the Model has the number and the name of the result case.") == "nessun caso di carico del modello ha il numero e il nome di questo caso di risultato" &&
              Strand7Snapshot.ExclusionReason("The model has no freedom case 3.") == "il modello non ha il freedom case 3" &&
              Strand7Snapshot.ExclusionReason("Freedom case 2 of the model is named 'A', not 'B'.") == "il freedom case 2 del modello si chiama «A», non «B»" &&
              Strand7Snapshot.ExclusionReason("The linear static settings of the imported model solve load case 4 under no freedom case, not under FC 2.") ==
                  "l’analisi statica lineare del modello risolve il caso di carico 4 sotto nessun freedom case, non sotto FC 2" &&
              Strand7Snapshot.ExclusionReason("The linear static settings of the imported model solve load case 4 under FC 1, FC 3, not under FC 2.") ==
                  "l’analisi statica lineare del modello risolve il caso di carico 4 sotto FC 1, FC 3, non sotto FC 2" &&
              Strand7Snapshot.ExclusionReason(Shared) == "l’analisi statica lineare risolve il caso di carico 2 sotto FC 1, FC 2: " + OneCasePerLoadCase &&
              Strand7Snapshot.ExclusionReason("The result file solves load case 2 under 1, 2 (freedom cases): GPC.Model keeps the results of a load case, not of each freedom case.") ==
                  "il file dei risultati risolve il caso di carico 2 sotto i freedom case 1, 2: " + OneCasePerLoadCase &&
              Strand7Snapshot.ExclusionReason("A reason of a later converter.") == "motivo del convertitore GPC: A reason of a later converter." &&
              Strand7Snapshot.ExclusionReason(null).Length > 0,
            "AY: the reasons of the converter for a result case left out, in Italian; another text kept after «motivo del convertitore GPC:»");

        // A catalogue like the one of the Straus7 file of the user (two freedom cases with the same name), with one load case solved under both.
        string folder = Path.Combine(output, "freedom-case-ay"); Directory.CreateDirectory(folder);
        string File(string name) { string path = Path.Combine(folder, name); System.IO.File.WriteAllBytes(path, [1, 2, 3]); return path; }
        string api = Path.Combine(folder, "St7api.dll");
        // A native x64 library in place of St7api.dll, as Strand7ImportChecks: the prerequisites of the files pass, nothing native is loaded.
        System.IO.File.Copy(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll"), api, true);
        var services = new NoResultServices { strand7Model = File("modello.st7"), strand7Results = File("modello.lsa"), strand7Api = api };
        string excluded = "2: [2: Q] [1: Freedom Case 1]";
        services.Catalog = new Strand7ModelCatalog(2563, 5238, [new(1, "1: G1") { FreedomCaseLabel = "1: G1 · FC 1" }, new(6, "5: G1") { FreedomCaseLabel = "5: G1 · FC 2" }],
            "SYNTHETICGEOMETRY/SYNTHETICRESULTS")
        {
            Beams = 96, Plates = 5142, Combinations = 31, LoadCases = ["1: G1", "2: Q", "5: G1"],
            FreedomCases = ["FC 1 «Freedom Case 1»: casi di carico 1, 2", "FC 2 «Freedom Case 1»: casi di carico 2, 5"],
            Warnings = ["Straus7FreedomCasesPreserved: vincoli e molle di più freedom case conservati.", "Casi di risultato senza un caso di carico corrispondente, non selezionabili: " + excluded + "."],
            UnmappedResultCases = [excluded], ExcludedResultCases = [new(2, excluded, Strand7Snapshot.ExclusionReason(Shared))]
        };
        using var vm = new ModelViewerViewModel(null, [], false, services, _ => { }, (_, _, _, _) => { });
        vm.SelectedImportSource = 2;
        vm.ChooseStrand7ModelCommand.Execute(null); vm.ChooseStrand7ResultsCommand.Execute(null); vm.ChooseStrand7ApiCommand.Execute(null);
        await vm.CheckStrand7SetupCommand.ExecuteAsync(null);
        Check(vm.ImportCases.Select(c => c.Label).SequenceEqual(["1: G1 · FC 1", "5: G1 · FC 2"]) && vm.ImportCases.Select(c => c.Name).SequenceEqual(["1: G1", "5: G1"]),
            "AY: the result cases show their freedom case; the names of the cases in the copy stay");
        Check(vm.Strand7PreparationSummary.Contains("Più casi di vincolo (freedom case) nel modello, con i casi di carico che l’analisi statica lineare del modello risolve sotto ciascuno: " +
                  "FC 1 «Freedom Case 1»: casi di carico 1, 2; FC 2", StringComparison.Ordinal) &&
              vm.Strand7PreparationSummary.Contains("Ogni caso di risultato è un caso di carico sotto il suo freedom case («· FC n» nell’elenco dei casi).", StringComparison.Ordinal) &&
              vm.Strand7PreparationSummary.Contains("il controllo di equilibrio lo fa GPC con queste reazioni", StringComparison.Ordinal) &&
              !vm.Strand7PreparationSummary.Contains("sono quelli del solutore", StringComparison.Ordinal) &&
              vm.Strand7PreparationSummary.Contains("(avviso Straus7FreedomCasesPreserved)", StringComparison.Ordinal) &&
              vm.Strand7PreparationSummary.Contains("Casi di risultato esclusi: 1", StringComparison.Ordinal) &&
              !vm.Strand7PreparationSummary.Contains("non associa ancora", StringComparison.Ordinal) && vm.HasStrand7ExcludedCases &&
              vm.Strand7ExcludedCases == "• " + excluded + ": l’analisi statica lineare risolve il caso di carico 2 sotto FC 1, FC 2: " + OneCasePerLoadCase + ".",
            "AY: the panel lists the freedom cases and the result cases left out, each with its reason");
        vm.ImportCases[0].IsSelected = false;
        Check(vm.ImportCommand.CanExecute(null), "AY: one selected case of a freedom case can be imported");
        await vm.ImportCommand.ExecuteAsync(null);
        // Revision P3: result case 6 is load case 5 under FC 2, so a code that sent the number of the load case would fail here.
        Check(services.Read is { } read && read.CaseNumbers.SequenceEqual([6]), "AY: the import asks the worker for the result case number of the selected case, not the load case number");
        vm.Strand7ResultsPath = "";
        Check(!vm.HasStrand7ExcludedCases && vm.Strand7ExcludedCases.Length == 0, "AY: other files clear the list of the excluded cases");

        // Without selectable cases the reason points to the list of the excluded cases instead of saying that the converter does not map them.
        var none = new Strand7ModelCatalog(1, 1, [], "S") { FreedomCases = ["FC 1 «A»: casi di carico 1"], UnmappedResultCases = ["1: [1: G] [1: A]"] };
        string? reason = none.NoResultsReason(true);
        Check(reason is not null && reason.Contains("freedom case", StringComparison.Ordinal) && reason.Contains("motivo di ogni caso escluso", StringComparison.Ordinal) &&
              !reason.Contains("non associa ancora", StringComparison.Ordinal) && none.SeveralFreedomCases, "AY: without selectable cases of several freedom cases the reason points to the excluded cases");

        // Revision P2: the summary of the freedom cases follows the catalogue, not the case of a linear solution of two freedom cases. The line of
        // the freedom cases is a fact of the model (the converter writes the freedom case data for every model with several of them).
        async Task<string> Summary(Strand7ModelCatalog catalog, string? results)
        {
            var source = new NoResultServices { strand7Model = File("modello.st7"), strand7Results = results is null ? "" : File(results), strand7Api = api, Catalog = catalog };
            using var view = new ModelViewerViewModel(null, [], false, source, _ => { }, (_, _, _, _) => { });
            view.SelectedImportSource = 2;
            view.ChooseStrand7ModelCommand.Execute(null); view.ChooseStrand7ResultsCommand.Execute(null); view.ChooseStrand7ApiCommand.Execute(null);
            await view.CheckStrand7SetupCommand.ExecuteAsync(null);
            return view.Strand7PreparationSummary;
        }
        const string FreedomLine = "Più casi di vincolo (freedom case) nel modello, con i casi di carico che l’analisi statica lineare del modello risolve sotto ciascuno: FC 1 «A»: casi di carico 1; FC 2 «B»: casi di carico 2.";
        const string Labels = "«· FC n» nell’elenco dei casi", Reactions = "il controllo di equilibrio lo fa GPC con queste reazioni", Preserved = "Straus7FreedomCasesPreserved";
        string[] twoFreedomCases = ["FC 1 «A»: casi di carico 1", "FC 2 «B»: casi di carico 2"];
        // A staged nonlinear solution of a model with a freedom case per stage (.nla): cases by phase and increment, no equilibrium check.
        string staged = await Summary(new Strand7ModelCatalog(4, 2, [new(1, Strand7Snapshot.NonlinearCaseName("1: Getto", "1")) { StageNumber = 1 }], "S/R")
        {
            NonlinearStages = true, StageResultsAvailable = true, LoadCases = ["1: G1", "2: G2"], FreedomCases = twoFreedomCases,
            Phases = [new(1, "1: Getto", null, null, 1)], Warnings = [Preserved + ": vincoli di più freedom case conservati."]
        }, "modello.nla");
        Check(staged.Contains(FreedomLine, StringComparison.Ordinal) && staged.Contains("analisi statica non lineare per fasi (.nla)", StringComparison.Ordinal) &&
              staged.Contains("il controllo di equilibrio di GPC non si fa per queste soluzioni", StringComparison.Ordinal) &&
              !staged.Contains(Labels, StringComparison.Ordinal) && !staged.Contains(Reactions, StringComparison.Ordinal) && staged.Contains("(avviso " + Preserved + ")", StringComparison.Ordinal),
            "P2: a staged nonlinear solution of several freedom cases names no «· FC n» and no equilibrium check");
        // «Controlla file e casi» without a results file: no list of the cases to speak of.
        string geometry = await Summary(new Strand7ModelCatalog(4, 2, [], "S") { LoadCases = ["1: G1", "2: G2"], FreedomCases = twoFreedomCases }, null);
        Check(geometry.Contains(FreedomLine, StringComparison.Ordinal) && geometry.Contains("Senza file dei risultati i freedom case restano dati del modello.", StringComparison.Ordinal) &&
              !geometry.Contains(Labels, StringComparison.Ordinal) && !geometry.Contains(Reactions, StringComparison.Ordinal) && !geometry.Contains(Preserved, StringComparison.Ordinal),
            "P2: without a results file the summary keeps the freedom cases of the model and says nothing of result cases, reactions or preserved restraints");
        // A model with two freedom cases solved under the first one only: «n: nome», no label; no restraint or spring, so no warning.
        string single = await Summary(new Strand7ModelCatalog(4, 2, [new(1, "1: G1"), new(2, "2: G2")], "S/R") { LoadCases = ["1: G1", "2: G2"], FreedomCases = twoFreedomCases }, "modello.lsa");
        Check(single.Contains(FreedomLine, StringComparison.Ordinal) && !single.Contains(Labels, StringComparison.Ordinal) &&
              single.Contains("I nomi dei casi del file non indicano un freedom case", StringComparison.Ordinal) && single.Contains(Reactions, StringComparison.Ordinal) &&
              !single.Contains(Preserved, StringComparison.Ordinal),
            "P2: a solution of one freedom case of a model with two has no «· FC n» sentence, and the preserved restraints only with the warning");
        // Linear static results with no selectable case: no sentence on the reactions of cases that are not imported.
        string excludedOnly = await Summary(new Strand7ModelCatalog(4, 2, [], "S/R")
        {
            LoadCases = ["1: G1", "2: G2"], FreedomCases = twoFreedomCases, UnmappedResultCases = [excluded], ExcludedResultCases = [new(2, excluded, Strand7Snapshot.ExclusionReason(Shared))]
        }, "modello.lsa");
        Check(excludedOnly.Contains(FreedomLine, StringComparison.Ordinal) && !excludedOnly.Contains(Reactions, StringComparison.Ordinal) && !excludedOnly.Contains(Labels, StringComparison.Ordinal),
            "P2: without selectable cases the summary names neither labels nor reactions");

        // The worker protocol keeps labels, excluded cases and freedom cases (the record goes from the worker process to the interface as JSON).
        string json = Path.Combine(folder, "catalogo.json");
        if (System.IO.File.Exists(json)) System.IO.File.Delete(json);
        Strand7WorkerProtocol.WriteResult(json, services.Catalog);
        var back = Strand7WorkerProtocol.ReadResult<Strand7ModelCatalog>(json, JsonLimit);
        Check(back.Cases.Select(c => c.Label).SequenceEqual(["1: G1 · FC 1", "5: G1 · FC 2"]) && back.Cases.Select(c => c.Name).SequenceEqual(["1: G1", "5: G1"]) &&
              back.ExcludedResultCases.Count == 1 && back.ExcludedResultCases[0] == services.Catalog.ExcludedResultCases[0] && back.FreedomCases.SequenceEqual(services.Catalog.FreedomCases) &&
              new Strand7ResultCase(1, "1: G").Label == "1: G", "AY: the catalogue keeps labels, excluded cases and freedom cases through the worker protocol; one freedom case keeps the name");
    }
}
