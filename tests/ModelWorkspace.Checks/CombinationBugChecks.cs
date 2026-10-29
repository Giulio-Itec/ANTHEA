using ANTHEA.ModelWorkspace;

/// <summary>Bugs of the combinations of the Models module found by the refactoring of 29/10/2026, each with the check that reproduced it.</summary>
static partial class Program
{
    static async Task CombinationBugChecks()
    {
        var service = new GpcModelCombinationService();
        // COMBO-B1: an envelope with the same case twice (±Q, ±E: the usual way of writing both signs) gave two alternatives with the same
        // label «±Q=Q»: GPC refused the whole envelope (DuplicateAlternativeLabel) although the catalogue offered it as ready to calculate.
        var plusMinus = new ModelCombinationDefinition("ANTHEA/pm", "±Q", ModelCombinationKind.Envelope, "ANTHEA", "", [new("Q", false, 1), new("Q", false, -1)], true);
        var model = ModelCombinations.Save(CombinationModel(), plusMinus);
        Check(ModelCombinations.Inspect(model).Single(c => c.Definition.Id == plusMinus.Id).CanCalculate, "COMBO-B1: the catalogue offers the ±Q envelope");
        var signed = await service.CalculateAsync(model, plusMinus.Id);
        double[] q = [-4, 8, -12, 16, -20, 24, -28, 32];
        Check(signed.Results.Length == 2 && signed.Results[0].Values.All(v => v.Values.SequenceEqual(q.Select(x => -Math.Abs(x))))
            && signed.Results[1].Values.All(v => v.Values.SequenceEqual(q.Select(Math.Abs))), "COMBO-B1: ±Q envelope gives −|Q| and +|Q| on every plate component");
        var alternatives = signed.Results[1].Combination!.Alternatives!;
        Check(alternatives.Length == 2 && alternatives.Distinct().Count() == 2 && signed.Results[1].Combination!.Governing.Select(g => g.Alternative).Distinct().Count() == 2,
            "COMBO-B1: the two alternatives keep distinct labels and governants (" + string.Join(" | ", alternatives) + ")");
        var attached = ModelCombinations.Attach(model, plusMinus.Id, signed);
        Check(ModelCombinations.Inspect(attached).Single(c => c.Definition.Id == plusMinus.Id).State == "Calcolata", "COMBO-B1: the ±Q envelope is stored as calculated");

        // COMBO-B2: a linear combination of two envelopes has the product of their alternatives; over the limit of 4096 the catalogue offered
        // «Da calcolare» and the calculation failed with the untranslated «CombinationExpansionLimit».
        var wide = Enumerable.Range(1, 65).Select(i => new ModelCombinationTerm(i % 2 == 0 ? "G" : "Q", false, i)).ToArray();
        model = ModelCombinations.Save(CombinationModel(), new("ANTHEA/wide", "Inviluppo largo", ModelCombinationKind.Envelope, "ANTHEA", "", wide, true));
        model = ModelCombinations.Save(model, new("ANTHEA/product", "Prodotto", ModelCombinationKind.Linear, "ANTHEA", "",
            [new("ANTHEA/wide", true, 1, "CB"), new("ANTHEA/wide", true, 1, "CB")], true));
        var product = ModelCombinations.Inspect(model).Single(c => c.Definition.Id == "ANTHEA/product");
        Check(!product.CanCalculate && product.Message.Contains("4096", StringComparison.Ordinal), "COMBO-B2: over 4096 alternatives the catalogue says why it cannot calculate: " + product.Message);
        Check(ModelCombinations.Inspect(model).Single(c => c.Definition.Id == "ANTHEA/wide").CanCalculate, "COMBO-B2: the envelope of 65 terms alone stays available");
        try { await service.CalculateAsync(model, "ANTHEA/product"); throw new Exception("COMBO-B2: expansion over the limit accepted"); }
        catch (InvalidDataException ex) { Check(ex.Message.Contains("4096", StringComparison.Ordinal), "COMBO-B2: the calculation refuses it with the same explanation: " + ex.Message); }
    }
}
