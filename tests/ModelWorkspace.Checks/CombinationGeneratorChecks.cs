using System.Text.Json;
using ANTHEA.ModelWorkspace;
using GPC.Model.Design.Combinations;

static partial class Program
{
    /// <summary>
    /// Census D9 a (decision of 28/10/2026): automatic combinations of the Models module on the generator of Model with the NTC 2018 profile.
    /// Expected rows written by hand from NTC 2018 §2.5.3, Tab. 2.6.I (A1: G1 1.3/1.0, Q 1.5/0) and Tab. 2.5.I (category B: ψ 0.7, 0.5, 0.3), and
    /// one combination calculated by GPC against the hand sum of the two cases of the fixture.
    /// </summary>
    static async Task CombinationGeneratorChecks(string output)
    {
        var model = CombinationModel();
        var proposal = ModelCombinationGenerator.Propose(model);
        Check(proposal.Length == 2 && proposal[0] == new ModelCaseAssignment("G", ModelCaseNature.G1) && proposal[1] == new ModelCaseAssignment("Q", ModelCaseNature.Variable),
            "generator: MIDAS type D proposed as G1, L as variable without a category");
        Reject(() => ModelCombinationGenerator.Generate(model, proposal), "generator: a variable case needs its category (ψ of Tab. 2.5.I)");
        var assignments = new[] { new ModelCaseAssignment("G", ModelCaseNature.G1), new ModelCaseAssignment("Q", ModelCaseNature.Variable, VariableActionCategory.B) };
        var rows = ModelCombinationGenerator.Generate(model, assignments);
        // Characteristic G + Q, frequent G + ψ1 Q, quasi permanent G + ψ2 Q; ULS: G 1.0 or 1.3, Q absent or 1.5 (permanent bit first).
        var expected = new (string Name, double G, double Q)[]
        {
            ("SLE rara 1", 1, 1), ("SLE frequente 1", 1, .5), ("SLE quasi permanente 1", 1, .3),
            ("SLU 1", 1, 0), ("SLU 2", 1.3, 0), ("SLU 3", 1, 1.5), ("SLU 4", 1.3, 1.5)
        };
        Check(rows.Select(r => r.Name).SequenceEqual(expected.Select(e => e.Name)), "generator: seven rows of NTC 2018 §2.5.3 in the order of the generator");
        double Factor(ModelCombinationDefinition d, string c) => d.Terms.SingleOrDefault(t => t.Reference == c)?.Factor ?? 0;
        Check(rows.Zip(expected).All(p => Factor(p.First, "G") == p.Second.G && Factor(p.First, "Q") == p.Second.Q), "generator: factors of A1 and of category B by hand");
        Check(rows.All(r => r.Id == ModelCombinationGenerator.IdPrefix + r.Name && r.Source == "ANTHEA" && r.Editable && r.Kind == ModelCombinationKind.Linear
            && r.Terms.All(t => !t.IsCombination && t.Analysis == "ST" && t.Factor != 0)), "generator: editable linear ANTHEA combinations of static cases, no zero terms");
        var applied = ModelCombinationGenerator.Apply(ModelCombinations.Save(model, new("ANTHEA/sum", "Somma", ModelCombinationKind.Linear, "ANTHEA", "", [new("G", false, 1)], true)), rows);
        Check(applied.CustomCombinations!.Length == 8 && applied.CustomCombinations[0].Id == "ANTHEA/sum", "generator: the combinations of the user are kept");
        var again = ModelCombinationGenerator.Apply(applied, rows);
        Check(again.CustomCombinations!.Length == 8, "generator: a new generation replaces the previous one");
        Reject(() => ModelCombinationGenerator.Apply(ModelCombinations.Save(model, new("ANTHEA/x", "SLU 1", ModelCombinationKind.Linear, "ANTHEA", "", [new("G", false, 1)], true)), rows),
            "generator: a name of the user is not overwritten");
        // GPC calculates the generated SLU 4 as any combination: 1.3 G + 1.5 Q on the eight plate values of the fixture.
        var service = new GpcModelCombinationService();
        var slu = await service.CalculateAsync(again, ModelCombinationGenerator.IdPrefix + "SLU 4");
        double[] g = [10, 20, 30, 40, 50, 60, 70, 80], q = [-4, 8, -12, 16, -20, 24, -28, 32];
        var sum = g.Zip(q, (a, b) => 1.3 * a + 1.5 * b).ToArray();
        Check(slu.Results.Length == 1 && slu.Results[0].Values.All(v => v.Values.Zip(sum).All(p => Math.Abs(p.First - p.Second) < 1e-8)), "generator: GPC sums the generated SLU 4 (1.3 G + 1.5 Q by hand)");
        // Seismic and accidental cases: SLV with E at +1 and −1, Q present (ψ2) or absent (census D6 a); the accidental case at 1 with ψ2 Q or Q absent.
        var seismic = CombinationModel();
        seismic.SourceTables!["STLD"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["1"] = new { NAME = "G", TYPE = "D" }, ["2"] = new { NAME = "Q", TYPE = "L" }, ["3"] = new { NAME = "E", TYPE = "E" }, ["4"] = new { NAME = "U", TYPE = "USER" }
        });
        Check(ModelCombinationGenerator.Propose(seismic).Select(a => a.Nature).SequenceEqual([ModelCaseNature.G1, ModelCaseNature.Variable, ModelCaseNature.Seismic, ModelCaseNature.Excluded]),
            "generator: MIDAS type E proposed as seismic, USER excluded");
        var full = ModelCombinationGenerator.Generate(seismic, [.. assignments, new("E", ModelCaseNature.Seismic), new("U", ModelCaseNature.Accidental)]);
        var slv = full.Where(r => r.Name.StartsWith("SLV ", StringComparison.Ordinal)).ToArray();
        Check(slv.Select(r => (Factor(r, "G"), Factor(r, "Q"), Factor(r, "E"))).SequenceEqual([(1d, .3, 1d), (1d, 0d, 1d), (1d, .3, -1d), (1d, 0d, -1d)]),
            "generator: SLV 1-4 = G + ψ2 Q ± E and G ± E (Q absent)");
        var accidental = full.Where(r => r.Name.StartsWith("Eccezionale ", StringComparison.Ordinal)).ToArray();
        Check(accidental.Select(r => (Factor(r, "G"), Factor(r, "Q"), Factor(r, "U"))).SequenceEqual([(1d, .3, 1d), (1d, 0d, 1d)]), "generator: accidental rows G + U + ψ2 Q and G + U");
        Check(full.Where(r => !r.Name.StartsWith("SLV", StringComparison.Ordinal)).All(r => Factor(r, "E") == 0) && full.Where(r => !r.Name.StartsWith("Eccezionale", StringComparison.Ordinal)).All(r => Factor(r, "U") == 0),
            "generator: seismic and accidental cases only in their rows");
        Reject(() => ModelCombinationGenerator.Generate(model, [new("G", ModelCaseNature.G1), new("X", ModelCaseNature.G1)]), "generator: unknown case refused");
        Reject(() => ModelCombinationGenerator.Generate(model, [new("G", ModelCaseNature.G1), new("G", ModelCaseNature.G2)]), "generator: repeated case refused");
        Reject(() => ModelCombinationGenerator.Generate(model, [new("G", ModelCaseNature.Excluded)]), "generator: nothing to combine refused");
        File.WriteAllText(Path.Combine(output, "combinazioni-generate.json"), JsonSerializer.Serialize(full, new JsonSerializerOptions { WriteIndented = true }));
    }
}
