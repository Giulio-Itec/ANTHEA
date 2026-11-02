using System.Text.Json;
using ANTHEA.ModelWorkspace;

static partial class Program
{
    /// <summary>
    /// Request of the user of 2/11/2026: the combinations through the matrix of GPC (CombinationMatrix, GpcModelCombinationService.UseMatrix)
    /// give the same results as through the samples of CombinationRebuild, byte for byte in the document (values, governing states and their
    /// concomitant vectors, principal components and their governing states, labels, revisions) and the same warnings: every calculable
    /// definition of the synthetic model 24 × 12 (NTC 2018 rows, envelopes, the nested and imported definitions of the user) and a sample of
    /// the 100 × 50.
    /// </summary>
    static async Task CombinationMatrixChecks()
    {
        foreach (var (size, cases, every) in new[] { (24, 12, 1), (100, 50, 12) })
        {
            var source = ModelOfSize(size, cases);
            var model = AddUserCombinations(ModelCombinationGenerator.Apply(source.Snapshot, ModelCombinationGenerator.Generate(source.Snapshot, source.Natures)), source);
            var catalogue = ModelCombinations.Inspect(model).Where(c => c.CanCalculate).ToArray();
            var chosen = catalogue.Where((c, i) => i % every == 0 || !c.Definition.Id.StartsWith(ModelCombinationGenerator.IdPrefix, StringComparison.Ordinal)).ToArray();
            var matrix = new GpcModelCombinationService(); var rebuild = new GpcModelCombinationService { UseMatrix = false };
            int envelopes = 0;
            foreach (var combination in chosen)
            {
                var a = await matrix.CalculateAsync(model, combination.Definition.Id);
                var b = await rebuild.CalculateAsync(model, combination.Definition.Id);
                if (JsonSerializer.Serialize(a.Results) != JsonSerializer.Serialize(b.Results) || !a.Warnings.SequenceEqual(b.Warnings))
                    throw new Exception($"Matrice diversa dalla ricostruzione: {source.Label} · {combination.Definition.Name}");
                if (a.Results.Length > 1) envelopes++;
            }
            Check(chosen.Length > 0 && envelopes > 0, $"combinations {source.Label}: the matrix of GPC gives the results of the rebuild byte for byte ({chosen.Length} definitions, {envelopes} envelopes)");
        }
    }
}
