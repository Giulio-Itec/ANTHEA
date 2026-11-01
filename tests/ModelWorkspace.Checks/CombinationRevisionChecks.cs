using System.Text.Json;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;

/// <summary>
/// Decision of the user of 1/11/2026 («Revisione per singola definizione»): saving or deleting a definition discards only the results of that
/// definition and of the definitions that use it (nested combinations, envelopes); a document of an older revision is calculated again once,
/// its results never shown as valid and no error at the opening.
/// </summary>
static partial class Program
{
    /// <param name="model">The reduced timing model with the user definitions and the results of «Somma utente» and «Inviluppo SLU».</param>
    static async Task DefinitionRevisionChecks(ModelSnapshot model)
    {
        const string sum = "ANTHEA/utente-somma", envelope = "ANTHEA/utente-inviluppo-slu";
        int Count(ModelSnapshot s, string id) => s.Results.Count(r => r.Combination?.DefinitionId == id);
        string State(ModelSnapshot s, string id) => ModelCombinations.Inspect(s).Single(c => c.Definition.Id == id).State;
        var definitions = ModelCombinations.Read(model);
        var sumDefinition = definitions.Single(d => d.Id == sum);
        string row = definitions.Single(d => d.Id == envelope).Terms[0].Reference;
        var rowDefinition = definitions.Single(d => d.Id == row);
        Check(Count(model, sum) == 1 && Count(model, envelope) == 2 && State(model, sum) == "Calcolata" && State(model, envelope) == "Calcolata",
            "revision per definition: both calculated definitions are current");

        var changedSum = ModelCombinations.Save(model, sumDefinition with { Terms = [.. sumDefinition.Terms.Select((t, i) => i == 0 ? t with { Factor = t.Factor + .25 } : t)] });
        Check(Count(changedSum, sum) == 0 && Count(changedSum, envelope) == 2 && State(changedSum, sum) == "Da calcolare" && State(changedSum, envelope) == "Calcolata",
            "revision per definition: saving the sum discards its result and keeps the envelope it is not part of");
        var changedRow = ModelCombinations.Save(model, rowDefinition with { Terms = [.. rowDefinition.Terms.Select((t, i) => i == 0 ? t with { Factor = t.Factor * 1.1 } : t)] });
        Check(Count(changedRow, sum) == 1 && Count(changedRow, envelope) == 0 && State(changedRow, envelope) == "Da calcolare" &&
            ModelCombinations.DefinitionRevision(changedRow, envelope) != ModelCombinations.DefinitionRevision(model, envelope) &&
            ModelCombinations.DefinitionRevision(changedRow, sum) == ModelCombinations.DefinitionRevision(model, sum),
            "revision per definition: saving a row of the envelope discards the envelope that uses it, not the sum");
        var same = ModelCombinations.Save(model, sumDefinition);
        Check(Count(same, sum) == 1 && Count(same, envelope) == 2, "revision per definition: saving a definition unchanged keeps its results");
        var removed = ModelCombinations.Remove(model, "ANTHEA/utente-annidata");
        var restored = ModelCombinations.RestoreImported(ModelCombinations.Remove(removed, "LCOM-GEN/cENV"));
        var regenerated = ModelCombinationGenerator.Apply(model, ModelCombinations.Read(model).Where(d => d.Id.StartsWith(ModelCombinationGenerator.IdPrefix, StringComparison.Ordinal)).ToArray());
        Check(new[] { removed, restored, regenerated }.All(s => Count(s, sum) == 1 && Count(s, envelope) == 2 && State(s, envelope) == "Calcolata"),
            "revision per definition: removing or restoring other definitions and generating the same rows again keep the results");
        // The calculation of a definition after a change of another one: attached with the results still valid.
        var service = new GpcModelCombinationService();
        var recalculated = ModelCombinations.Attach(changedSum, sum, await service.CalculateAsync(changedSum, sum));
        Check(Count(recalculated, sum) == 1 && Count(recalculated, envelope) == 2 && State(recalculated, sum) == "Calcolata" && State(recalculated, envelope) == "Calcolata",
            "revision per definition: calculating the changed sum keeps the envelope calculated before");

        // A document saved before 1/11/2026: the revision of the whole copy in every derived result.
        var older = ModelSnapshot.FromJson(JsonSerializer.Serialize(model));
        older.Results = [.. model.Results.Select(r => r.Combination is { } c ? r with { Combination = c with { InputRevision = new string('A', 64) } } : r)];
        var reopened = ModelSnapshot.FromJson(JsonSerializer.Serialize(older));
        var current = ModelCombinations.WithCurrentResults(reopened);
        Check(current.Results.All(r => r.Combination == null) && current.Results.Length == model.Results.Count(r => r.Combination == null) &&
            State(reopened, sum) == "Da calcolare" && State(reopened, envelope) == "Da calcolare" && ReferenceEquals(ModelCombinations.WithCurrentResults(model), model),
            "revision per definition: the results of an older document are not current, the catalogue offers them to calculate, no error");
        using var vm = new ModelViewerViewModel(reopened, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        Check(vm.Snapshot!.Results.All(r => r.Combination == null) && vm.Cases.Count == current.Results.Length && vm.Combinations.Count(c => c.State == "Calcolata") == 0,
            "revision per definition: the viewer opens an older document without showing its old results as valid");
        vm.SelectedCombination = vm.Combinations.Single(c => c.Definition.Id == sum);
        await vm.CalculateCombinationCommand.ExecuteAsync(null);
        Check(vm.Snapshot!.Results.Count(r => r.Combination?.DefinitionId == sum) == 1 && vm.Combinations.Single(c => c.Definition.Id == sum).State == "Calcolata",
            "revision per definition: an older document is calculated again once and its result is current");
    }
}
