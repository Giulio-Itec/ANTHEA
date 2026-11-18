using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using GPC.Converter.Straus7;
using GPC.Model.LoadCases;
using GPC.Model.Structure.Assignments;

/// <summary>
/// 17/11/2026, revision P3: the result cases of a Straus7 linear static solution with several freedom cases as the worker builds them
/// (Strand7Snapshot.LinearCases), on the real Straus7LinearStaticResults.Cases of GPC.Converter and without Straus7. Input: a GPC Model with
/// the load cases as the converter names them («Case 2: Q»), its freedom case data as the preserved record Straus7FreedomCaseData.Read reads
/// (same serializer as the converter), and a listing with the native names of the result cases (St7GetResultCaseName, «3: [2: Q] [2: Freedom
/// Case 1]»), numbered by freedom case and then by load case, so that a result case number differs from the number of its load case. The
/// reasons of the cases left out are written by the converter; only their Italian text is expected here.
/// </summary>
static partial class Program
{
    static async Task FreedomCaseConverterChecks(string output)
    {
        static Straus7ResultCase Listed(int number, string name) => new() { Number = number, Name = name };
        static GPC.Model.Models.Model Model(string name, params (int Number, string Name)[] loadCases)
        {
            var model = new GPC.Model.Models.Model(name);
            model.AddLoadCases(loadCases.Select(c => new LoadCaseBase(Straus7ApiConverter.CaseName(c.Number, c.Name))).ToArray());
            return model;
        }
        (int, string)[] loads = [(2, "Q"), (4, "W"), (5, "S"), (6, "T"), (7, "U"), (8, "V")];
        var model = Model("due freedom case con lo stesso nome", loads);
        Straus7LinearCasePair Pair(int load, int freedom) => new() { LoadCase = load, FreedomCase = freedom, ModelCase = Straus7ApiConverter.CaseName(load, loads.Single(l => l.Item1 == load).Item2) };
        var data = new Straus7FreedomCaseData
        {
            // Two freedom cases with the same name, told apart by the number; load case 5 solved under both; load case 8 under none.
            FreedomCases = [new() { Number = 1, Name = "Freedom Case 1" }, new() { Number = 2, Name = "Freedom Case 1" }, new() { Number = 3, Name = "Vento" }],
            LinearCases = [Pair(4, 1), Pair(7, 1), Pair(5, 1), Pair(2, 2), Pair(5, 2), Pair(6, 3)]
        };
        using (var stream = new MemoryStream())
        {
            new DataContractJsonSerializer(typeof(Straus7FreedomCaseData)).WriteObject(stream, data);
            model.PreservedSourceData.Add(new PreservedAssignment { Kind = Straus7FreedomCaseData.Kind, SourceRecord = Straus7FreedomCaseData.Record, RawData = Encoding.UTF8.GetString(stream.ToArray()) });
        }
        List<Straus7ResultCase> listed =
        [
            Listed(1, "1: [7: U] [2: Freedom Case 1]"),   // the settings solve 7 under FC 1, not under FC 2
            Listed(2, "2: [4: W] [1: Freedom Case 1]"),   // load case 4 under FC 1
            Listed(3, "3: [2: Q] [2: Freedom Case 1]"),   // load case 2 under FC 2
            Listed(4, "4: [5: S] [1: Freedom Case 1]"),   // load case 5 is solved under FC 1 and FC 2
            Listed(5, "5: [6: T] [3: Neve]"),             // freedom case 3 of the model is «Vento»
            Listed(6, "6: [4: W] [9: Freedom Case 9]"),   // no freedom case 9
            Listed(7, "7: [8: V] [1: Freedom Case 1]"),   // load case 8 is solved under no freedom case
            Listed(8, "8: Nessuno"),                      // no load case of the model
        ];
        var linear = Strand7Snapshot.LinearCases(model, listed);
        Check(linear is not null && linear.Cases.Select(c => c.Number).SequenceEqual([2, 3]) && linear.Cases.Select(c => c.Name).SequenceEqual(["4: W", "2: Q"]) &&
              linear.Cases.Select(c => c.Label).SequenceEqual(["4: W · FC 1", "2: Q · FC 2"]) && linear.Cases.All(c => c.StageNumber is null),
            "P3: the converter maps a result case to its load case under its freedom case: result case 2 is «4: W · FC 1», result case 3 «2: Q · FC 2»");
        Check(linear!.Cases.All(c => c.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) != c.Name.Split(':')[0]),
            "P3: the catalogue keeps the result case number, not the number of the load case in the name");
        var map = Straus7LinearStaticResults.CaseMap(model, listed);
        Check(map.Keys.Order().SequenceEqual(linear.Cases.Select(c => c.Number).Order()) && linear.Cases.All(c => Strand7Snapshot.CaseName(map[c.Number]) == c.Name),
            "P3: the selectable cases are those of CaseMap, the mapping of the import, with the same load cases");

        string[] reasons =
        [
            "l’analisi statica lineare del modello risolve il caso di carico 7 sotto FC 1, non sotto FC 2",
            "l’analisi statica lineare risolve il caso di carico 5 sotto FC 1, FC 2: il modello GPC tiene i risultati di un caso di carico, non di ogni freedom case",
            "il freedom case 3 del modello si chiama «Vento», non «Neve»",
            "il modello non ha il freedom case 9",
            "l’analisi statica lineare del modello risolve il caso di carico 8 sotto nessun freedom case, non sotto FC 1",
            "nessun caso di carico del modello ha il numero e il nome di questo caso di risultato",
        ];
        Check(linear.Excluded.Select(c => c.Number).SequenceEqual([1, 4, 5, 6, 7, 8]) && linear.Excluded.Select(c => c.Name).SequenceEqual(listed.Where(c => c.Number is 1 or >= 4).Select(c => c.Name)) &&
              linear.Excluded.Select(c => c.Reason).SequenceEqual(reasons),
            "P3: the cases left out keep their native name, each with the reason of the converter in Italian");
        // No reason left in English (a code of the converter that Strand7Snapshot.ExclusionReason does not translate).
        var english = new Regex(@"\b(The|the|under|not|solve[sd]?|named|has|keeps|Model)\b", RegexOptions.CultureInvariant);
        Check(linear.Excluded.All(c => !c.Reason.StartsWith("motivo del convertitore GPC", StringComparison.Ordinal) && !english.IsMatch(c.Reason)),
            "P3: every reason of the converter for these cases is translated");
        // GPC.Converter 2.25 (decision of the user of 17/11/2026): each case left out has the code of its reason, which the Italian text is
        // built from, instead of the English sentence.
        var codes = Straus7LinearStaticResults.Cases(model, listed).Where(c => c.ModelCase == null).Select(c => c.ReasonCode).ToArray();
        Check(codes.SequenceEqual([Straus7CaseExclusionReason.NotSolvedUnderFreedomCase, Straus7CaseExclusionReason.SolvedUnderSeveralFreedomCases,
                  Straus7CaseExclusionReason.FreedomCaseRenamed, Straus7CaseExclusionReason.FreedomCaseMissing, Straus7CaseExclusionReason.NotSolvedUnderFreedomCase,
                  Straus7CaseExclusionReason.NoMatchingLoadCase]) &&
              Straus7LinearStaticResults.Cases(model, listed).Where(c => c.ModelCase != null).All(c => c.ReasonCode == Straus7CaseExclusionReason.None),
            "2.25: the converter gives the code of the reason of each case left out, None for a mapped case");

        // Without the freedom case data (an older import) the listing decides: a load case under two freedom cases of the file is left out.
        var bare = Model("senza dati dei freedom case", (2, "Q"), (4, "W"));
        var file = Strand7Snapshot.LinearCases(bare, [Listed(1, "1: [2: Q] [1: A]"), Listed(2, "2: [2: Q] [2: B]"), Listed(3, "3: [4: W] [2: B]")]);
        Check(file is not null && file.Cases.Select(c => (c.Number, c.Name, c.Label)).SequenceEqual([(3, "4: W", "4: W · FC 2")]) &&
              file.Excluded.Select(c => c.Number).SequenceEqual([1, 2]) &&
              Straus7LinearStaticResults.Cases(bare, [Listed(1, "1: [2: Q] [1: A]"), Listed(2, "2: [2: Q] [2: B]"), Listed(3, "3: [4: W] [2: B]")])
                  .Where(c => c.ModelCase == null).All(c => c.ReasonCode == Straus7CaseExclusionReason.ListedUnderSeveralFreedomCases) &&
              file.Excluded.All(c => c.Reason == "il file dei risultati risolve il caso di carico 2 sotto i freedom case 1, 2: il modello GPC tiene i risultati di un caso di carico, non di ogni freedom case"),
            "P3: without freedom case data a load case solved under two freedom cases of the file is left out, with the reason in Italian");
        // A solution of one freedom case: «n: nome», numbered over the solved load cases; no label, the name of the case as before.
        var one = Strand7Snapshot.LinearCases(Model("un freedom case", (1, "G"), (2, "Q")), [Listed(1, "2: Q")]);
        Check(one is not null && one.Cases.Single() is { Number: 1, Name: "2: Q", FreedomCaseLabel: null, Label: "2: Q" } && one.Excluded.Count == 0,
            "P3: with one freedom case the case keeps its name and has no freedom case");
        Check(Strand7Snapshot.FreedomCases(model).SequenceEqual(["FC 1 «Freedom Case 1»: casi di carico 4, 5, 7", "FC 2 «Freedom Case 1»: casi di carico 2, 5", "FC 3 «Vento»: casi di carico 6"]) &&
              Strand7Snapshot.FreedomCases(bare).Count == 0,
            "P3: the freedom cases of the model with the load cases solved under each one, from the data of the converter");

        // The panel on this catalogue: the case of the list is asked to the worker by its result case number (3), not by its load case (2).
        string folder = Path.Combine(output, "freedom-case-convertitore"); Directory.CreateDirectory(folder);
        string File(string name) { string path = Path.Combine(folder, name); System.IO.File.WriteAllBytes(path, [1, 2, 3]); return path; }
        string api = Path.Combine(folder, "St7api.dll");
        System.IO.File.Copy(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "coreclr.dll"), api, true);
        var services = new NoResultServices
        {
            strand7Model = File("modello.st7"), strand7Results = File("modello.lsa"), strand7Api = api,
            Catalog = new Strand7ModelCatalog(4, 2, linear.Cases, "SYNTHETICGEOMETRY/SYNTHETICRESULTS")
            {
                LoadCases = model.LoadCases.Keys.Select(Strand7Snapshot.CaseName).ToArray(), FreedomCases = Strand7Snapshot.FreedomCases(model),
                UnmappedResultCases = linear.Excluded.Select(c => c.Name).ToArray(), ExcludedResultCases = linear.Excluded
            }
        };
        using var vm = new ModelViewerViewModel(null, [], false, services, _ => { }, (_, _, _, _) => { });
        vm.SelectedImportSource = 2;
        vm.ChooseStrand7ModelCommand.Execute(null); vm.ChooseStrand7ResultsCommand.Execute(null); vm.ChooseStrand7ApiCommand.Execute(null);
        await vm.CheckStrand7SetupCommand.ExecuteAsync(null);
        Check(vm.ImportCases.Select(c => c.Label).SequenceEqual(["4: W · FC 1", "2: Q · FC 2"]) && vm.Strand7ExcludedCases.Split('\n').Length == 6 &&
              !vm.Strand7ExcludedCases.Contains("motivo del convertitore GPC", StringComparison.Ordinal),
            "P3: the panel lists the cases of the converter with their freedom case and the six cases left out with their reason");
        foreach (var option in vm.ImportCases) option.IsSelected = option.Label == "2: Q · FC 2";
        await vm.ImportCommand.ExecuteAsync(null);
        Check(services.Read is { } read && read.CaseNumbers.SequenceEqual([3]), "P3: the import asks the worker for result case 3, the case «2: Q · FC 2»");
    }
}
