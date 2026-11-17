using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using Code = GPC.Converter.Straus7.Straus7CaseExclusionReason;

/// <summary>
/// 17/11/2026, set AY candidate (GPC.Converter 2.24, registro MOD-FC-AY-20261117): a Straus7 catalogue of a model with several freedom cases
/// shows each result case with its freedom case («5: G1 · FC 2»), the freedom cases of the model and the cases left out with the reason of the
/// converter in Italian; the names of the cases in the copy do not change. Since GPC.Converter 2.25 (set AY) the Italian reason comes from the
/// code of the converter (Straus7LinearStaticCase.ReasonCode), not from its English text.
/// </summary>
static partial class Program
{
    static async Task FreedomCaseCatalogChecks(string output)
    {
        const string OneCasePerLoadCase = "il modello GPC tiene i risultati di un caso di carico, non di ogni freedom case";
        const string Shared = "l’analisi statica lineare risolve il caso di carico 2 sotto FC 1, FC 2: " + OneCasePerLoadCase;
        const long JsonLimit = 1048576;
        // GPC.Converter 2.25 (decision of the user of 17/11/2026): the Italian reason comes from the code of the converter
        // (Straus7LinearStaticCase.ReasonCode) and the numbers of the case, whatever its English text; the freedom case data of an import with
        // freedom cases 1 and 2 named «A» and 3 named «C», load case 2 solved under FC 1 and FC 2, load case 4 under FC 1 and FC 3.
        var settings = new GPC.Converter.Straus7.Straus7FreedomCaseData
        {
            FreedomCases = [new() { Number = 1, Name = "A" }, new() { Number = 2, Name = "A" }, new() { Number = 3, Name = "C" }],
            LinearCases = [new() { LoadCase = 2, FreedomCase = 1 }, new() { LoadCase = 2, FreedomCase = 2 }, new() { LoadCase = 4, FreedomCase = 1 }, new() { LoadCase = 4, FreedomCase = 3 }]
        };
        static string Reason(Code code, int? load, int? freedom, string? name = null, string? text = "Any English sentence.", GPC.Converter.Straus7.Straus7FreedomCaseData? data = null,
            int[]? listed = null) => Strand7Snapshot.ExclusionReason(code, load, freedom, name, text, data, listed ?? []);
        Check(Reason(Code.NoMatchingLoadCase, null, null) == "nessun caso di carico del modello ha il numero e il nome di questo caso di risultato" &&
              Reason(Code.FreedomCaseMissing, 1, 9, "X", data: settings) == "il modello non ha il freedom case 9" &&
              Reason(Code.FreedomCaseRenamed, 4, 3, "B", data: settings) == "il freedom case 3 del modello si chiama «C», non «B»" &&
              Reason(Code.NotSolvedUnderFreedomCase, 5, 2, "A", data: settings) == "l’analisi statica lineare del modello risolve il caso di carico 5 sotto nessun freedom case, non sotto FC 2" &&
              Reason(Code.NotSolvedUnderFreedomCase, 4, 2, "A", data: settings) == "l’analisi statica lineare del modello risolve il caso di carico 4 sotto FC 1, FC 3, non sotto FC 2" &&
              Reason(Code.SolvedUnderSeveralFreedomCases, 2, 1, "A", data: settings) == Shared &&
              Reason(Code.ListedUnderSeveralFreedomCases, 2, 2, "B", listed: [1, 2]) == "il file dei risultati risolve il caso di carico 2 sotto i freedom case 1, 2: " + OneCasePerLoadCase,
            "AY: the reasons of the converter for a result case left out, in Italian from the code and the numbers of the case, not from its English text");
        Check(Reason((Code)99, 2, 2, text: "A reason of a later converter.") == "motivo del convertitore GPC: A reason of a later converter." &&
              Reason(Code.NotSolvedUnderFreedomCase, 4, 2, text: "Without the freedom case data.") == "motivo del convertitore GPC: Without the freedom case data." &&
              Reason((Code)99, null, null, text: null).Length > 0,
            "AY: a code this version does not translate, or one without the numbers it needs, keeps the English text after «motivo del convertitore GPC:»");
        // Every code of the converter but None has its Italian text: a code added by a later GPC.Converter fails here until it is translated.
        Check(Enum.GetValues<Code>().Where(c => c != Code.None).All(c => !Reason(c, 2, 3, "B", data: settings, listed: [1, 2]).StartsWith("motivo del convertitore GPC", StringComparison.Ordinal)),
            "AY: every reason code of GPC.Converter has its Italian text");

        // A catalogue of a model with two freedom cases with the same name, with one load case solved under both (synthetic numbers, note N3).
        string folder = Path.Combine(output, "freedom-case-ay"); Directory.CreateDirectory(folder);
        string File(string name) { string path = Path.Combine(folder, name); System.IO.File.WriteAllBytes(path, [1, 2, 3]); return path; }
        string api = Path.Combine(folder, "St7api.dll");
        // A native x64 library in place of St7api.dll, as Strand7ImportChecks: the prerequisites of the files pass, nothing native is loaded.
        System.IO.File.Copy(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll"), api, true);
        var services = new NoResultServices { strand7Model = File("modello.st7"), strand7Results = File("modello.lsa"), strand7Api = api };
        string excluded = "2: [2: Q] [1: Freedom Case 1]";
        services.Catalog = new Strand7ModelCatalog(120, 210, [new(1, "1: G1") { FreedomCaseLabel = "1: G1 · FC 1" }, new(6, "5: G1") { FreedomCaseLabel = "5: G1 · FC 2" }],
            "SYNTHETICGEOMETRY/SYNTHETICRESULTS")
        {
            Beams = 30, Plates = 180, Combinations = 4, LoadCases = ["1: G1", "2: Q", "5: G1"],
            FreedomCases = ["FC 1 «Freedom Case 1»: casi di carico 1, 2", "FC 2 «Freedom Case 1»: casi di carico 2, 5"],
            Warnings = ["Straus7FreedomCasesPreserved: vincoli e molle di più freedom case conservati.", "Casi di risultato senza un caso di carico corrispondente, non selezionabili: " + excluded + "."],
            UnmappedResultCases = [excluded], ExcludedResultCases = [new(2, excluded, Shared)]
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
            LoadCases = ["1: G1", "2: G2"], FreedomCases = twoFreedomCases, UnmappedResultCases = [excluded], ExcludedResultCases = [new(2, excluded, Shared)]
        }, "modello.lsa");
        Check(excludedOnly.Contains(FreedomLine, StringComparison.Ordinal) && !excludedOnly.Contains(Reactions, StringComparison.Ordinal) && !excludedOnly.Contains(Labels, StringComparison.Ordinal),
            "P2: without selectable cases the summary names neither labels nor reactions");

        // Note N2: a reason the converter writes in another way is kept as it is, with its own full stop and not a second one.
        var later = new NoResultServices
        {
            strand7Model = File("modello.st7"), strand7Results = File("modello.lsa"), strand7Api = api,
            Catalog = new Strand7ModelCatalog(4, 2, [new(1, "1: G1")], "S/R")
            { LoadCases = ["1: G1"], UnmappedResultCases = ["2: X"], ExcludedResultCases = [new(2, "2: X", Reason((Code)99, null, null, text: "A reason of a later converter."))] }
        };
        using (var laterView = new ModelViewerViewModel(null, [], false, later, _ => { }, (_, _, _, _) => { }))
        {
            laterView.SelectedImportSource = 2;
            laterView.ChooseStrand7ModelCommand.Execute(null); laterView.ChooseStrand7ResultsCommand.Execute(null); laterView.ChooseStrand7ApiCommand.Execute(null);
            await laterView.CheckStrand7SetupCommand.ExecuteAsync(null);
            Check(laterView.Strand7ExcludedCases == "• 2: X: motivo del convertitore GPC: A reason of a later converter.", "N2: an unknown reason of the converter ends with one full stop");
        }

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
