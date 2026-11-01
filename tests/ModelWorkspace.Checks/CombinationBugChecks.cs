using System.Text.Json;
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
        // 1/11/2026 (point 8, COMBO-B2-bis): over the limit the combination is calculated term by term (GPC CombinationTreeNode), no longer refused.
        Check(product.CanCalculate && product.State == "Da calcolare" && product.Message.Contains("4096", StringComparison.Ordinal) && product.Message.Contains("(4225)", StringComparison.Ordinal),
            "COMBO-B2-bis: over 4096 alternatives the catalogue offers the calculation term by term and says so: " + product.Message);
        Check(ModelCombinations.Inspect(model).Single(c => c.Definition.Id == "ANTHEA/wide").CanCalculate, "COMBO-B2: the envelope of 65 terms alone stays available");
        var products = (await service.CalculateAsync(model, "ANTHEA/product")).Results;
        // By hand: the extreme of wide + wide is twice the extreme of wide, i × G (i even) or i × Q (i odd), i = 1…65, for every plate component.
        double[] gPlate = [10, 20, 30, 40, 50, 60, 70, 80], qPlate = [-4, 8, -12, 16, -20, 24, -28, 32];
        var singles = Enumerable.Range(1, 65).Select(i => (i % 2 == 0 ? gPlate : qPlate).Select(v => i * v).ToArray()).ToArray();
        Check(products.Length == 2 && Enumerable.Range(0, 8).All(k => products[0].Values.All(v => v.Values[k] == 2 * singles.Min(s => s[k])) && products[1].Values.All(v => v.Values[k] == 2 * singles.Max(s => s[k]))),
            "COMBO-B2-bis: the extremes of the 4225 states by hand (twice the extreme of the envelope), with the GPC governing states");
        Check(products.All(r => r.Combination!.Governing.Length == 4 * 8 + 2 * 6 && r.Fields!.All(f => f.Axes != "PRINCIPAL") && r.Combination.PrincipalGoverning == null),
            "COMBO-B2-bis: complete governing states, no principal components (they need every state)");

        // COMBO-B3: a combination whose coefficients are all zero (the editor accepts 0) was offered «Da calcolare» and failed with the
        // untranslated «CombinationRejected: EmptyCombination»; the same for terms that cancel each other (Q − Q).
        model = ModelCombinations.Save(CombinationModel(), new("ANTHEA/zero", "Zero", ModelCombinationKind.Linear, "ANTHEA", "", [new("G", false, 0), new("Q", false, 0)], true));
        var zero = ModelCombinations.Inspect(model).Single(c => c.Definition.Id == "ANTHEA/zero");
        Check(zero.State == "Vuota" && !zero.CanCalculate, "COMBO-B3: all coefficients zero: the catalogue says «Vuota»: " + zero.Message);
        model = ModelCombinations.Save(model, new("ANTHEA/cancel", "Annullata", ModelCombinationKind.Linear, "ANTHEA", "", [new("Q", false, 1), new("Q", false, -1)], true));
        try { await service.CalculateAsync(model, "ANTHEA/cancel"); throw new Exception("COMBO-B3: empty combination accepted"); }
        catch (InvalidDataException ex) { Check(ex.Message.Contains("coefficienti nulli", StringComparison.Ordinal), "COMBO-B3: terms that cancel each other: explained, not EmptyCombination: " + ex.Message); }

        // COMBO-B4 (1/11/2026): an imported definition renamed as another of its table was saved; the catalogue showed two «A [LCOM-GEN]» and the
        // calculation of the second failed when attached («Il nome del risultato calcolato coincide con un risultato esistente»).
        var imported = CombinationModel();
        imported.SourceTables = new Dictionary<string, JsonElement>(imported.SourceTables!) { ["LCOM-GEN"] = JsonSerializer.Deserialize<JsonElement>(
            """{"1":{"NAME":"A","iTYPE":0,"ACTIVE":"ACTIVE","vCOMB":[{"ANAL":"ST","LCNAME":"G","FACTOR":1}]},"2":{"NAME":"B","iTYPE":0,"ACTIVE":"ACTIVE","vCOMB":[{"ANAL":"ST","LCNAME":"Q","FACTOR":1}]}}""") };
        imported.Validate();
        var second = ModelCombinations.Read(imported).Single(d => d.Name == "B");
        Reject(() => ModelCombinations.Save(imported, second with { Name = "A", Editable = true }), "COMBO-B4: an imported definition cannot take the name of another definition of its table");
        var both = ModelCombinations.Save(imported, second with { Name = "B2", Editable = true });
        foreach (var id in new[] { "LCOM-GEN/A", "LCOM-GEN/B" }) both = ModelCombinations.Attach(both, id, await service.CalculateAsync(both, id));
        Check(both.Results.Count(r => r.Combination != null) == 2, "COMBO-B4: a different name: both imported definitions are calculated and attached");
    }
}
