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
        await GeneratorInterfaceChecks(seismic, assignments);
    }

    /// <summary>
    /// Decisions of the user of 1/11/2026 for the interface of the generator: the category of a «L» case is always chosen (no default), the
    /// directional seismic cases follow the rule 100/30 of NTC 2018 §7.3.5 with the coefficients of Model, the limits are told before generating,
    /// the choices are kept in the copy (new field omitted when null). Expected SLV rows written by hand.
    /// </summary>
    static Task GeneratorInterfaceChecks(ModelSnapshot seismic, ModelCaseAssignment[] assignments)
    {
        double Factor(ModelCombinationDefinition d, string c) => d.Terms.SingleOrDefault(t => t.Reference == c)?.Factor ?? 0;
        // Two directional cases: the model of the fixture has a single E; E as X and a second seismic case as Y.
        var twoWay = ModelSnapshot.FromJson(JsonSerializer.Serialize(seismic));
        twoWay.SourceTables!["STLD"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["1"] = new { NAME = "G", TYPE = "D" }, ["2"] = new { NAME = "Q", TYPE = "L" }, ["3"] = new { NAME = "E", TYPE = "E" }, ["4"] = new { NAME = "U", TYPE = "E" }
        });
        var directional = assignments.Append(new("E", ModelCaseNature.Seismic, Direction: SeismicDirection.X)).Append(new("U", ModelCaseNature.Seismic, Direction: SeismicDirection.Y)).ToArray();
        var slv = ModelCombinationGenerator.Generate(twoWay, directional).Where(r => r.Name.StartsWith("SLV ", StringComparison.Ordinal)).ToArray();
        // For each of the 8 variants (E leads ±1 with U ±0.3, then U leads ±1 with E ±0.3), Q present (ψ2 = 0.3) then absent.
        var variants = new (double E, double U)[] { (1, .3), (1, -.3), (-1, .3), (-1, -.3), (.3, 1), (-.3, 1), (.3, -1), (-.3, -1) };
        var expected = variants.SelectMany(v => new[] { (G: 1d, Q: .3, v.E, v.U), (G: 1d, Q: 0d, v.E, v.U) }).ToArray();
        Check(slv.Length == 16 && slv.Select(r => (Factor(r, "G"), Factor(r, "Q"), Factor(r, "E"), Factor(r, "U"))).SequenceEqual(expected),
            "generator: rule 100/30 (NTC 2018 §7.3.5), 16 SLV rows = 8 variants × Q present or absent, by hand");
        var noDirection = ModelCombinationGenerator.Generate(twoWay, [.. assignments, new("E", ModelCaseNature.Seismic), new("U", ModelCaseNature.Seismic)])
            .Where(r => r.Name.StartsWith("SLV ", StringComparison.Ordinal)).ToArray();
        Check(noDirection.Length == 8 && noDirection.All(r => Math.Abs(Factor(r, "E")) + Math.Abs(Factor(r, "U")) == 1), "generator: seismic cases without direction alone with ±1, as before");
        Reject(() => ModelCombinationGenerator.Generate(twoWay, [.. assignments, new("E", ModelCaseNature.G2, Direction: SeismicDirection.X)]), "generator: a direction only for seismic cases");
        // «L» never gets a category by itself; the preview waits for it and then counts the rows.
        var proposal = ModelCombinationGenerator.Choices(twoWay);
        Check(proposal.Single(a => a.Case == "Q") is { Nature: ModelCaseNature.Variable, Category: null } && !ModelCombinationGenerator.Preview(twoWay, proposal).CanGenerate
            && ModelCombinationGenerator.Preview(twoWay, proposal).Message.Contains("categoria della Tab. 2.5.I per i carichi variabili: Q.", StringComparison.Ordinal), "generator: a «L» case waits for its category, no default");
        var preview = ModelCombinationGenerator.Preview(twoWay, directional);
        Check(preview.CanGenerate && preview.Rows == 7 + 16 && preview.Message.StartsWith("23 combinazioni (", StringComparison.Ordinal) && preview.Message.Contains("SLV 16", StringComparison.Ordinal),
            "generator: preview of the rows of each limit state: " + preview.Message);
        var many = Enumerable.Range(0, 13).Select(i => new ModelCaseAssignment("Q" + i, ModelCaseNature.Variable, VariableActionCategory.A)).ToArray();
        var wide = ModelSnapshot.FromJson(JsonSerializer.Serialize(seismic));
        wide.Results = [.. wide.Results, .. many.Select(m => wide.Results[0] with { Name = m.Case })];
        var limit = ModelCombinationGenerator.Preview(wide, [new("G", ModelCaseNature.G1), .. many]);
        Check(!limit.CanGenerate && limit.Message.Contains("14 gruppi", StringComparison.Ordinal) && limit.Message.Contains("al più 12", StringComparison.Ordinal) && limit.Message.Contains("stesso gruppo", StringComparison.Ordinal),
            "generator: over 12 groups told with the number and the remedy: " + limit.Message);
        var rows = ModelCombinationGenerator.Preview(wide, [new("G", ModelCaseNature.G1), .. many.Take(11)]);
        Check(!rows.CanGenerate && System.Text.RegularExpressions.Regex.IsMatch(rows.Message, @"^Oltre 4096 combinazioni \(\d+ righe\): riunisci"), "generator: over 4096 rows told with the number and the remedy: " + rows.Message);
        var grouped = ModelCombinationGenerator.Preview(wide, [new("G", ModelCaseNature.G1), .. many.Select((m, i) => m with { Group = i < 7 ? "a" : "b" })]);
        Check(grouped.CanGenerate, "generator: the same cases in two groups can be generated: " + grouped.Message);
        // Choices kept in the copy: omitted when null (old fingerprints unchanged), names as text, restored for a new generation.
        Check(!JsonSerializer.Serialize(seismic).Contains("GeneratorCases", StringComparison.Ordinal), "generator: no choices, no new field in the document");
        var applied = ModelCombinationGenerator.Apply(twoWay, ModelCombinationGenerator.Generate(twoWay, directional), directional);
        string json = JsonSerializer.Serialize(applied);
        var reopened = ModelSnapshot.FromJson(json);
        Check(json.Contains("\"Direction\":\"X\"", StringComparison.Ordinal) && json.Contains("\"Category\":\"B\"", StringComparison.Ordinal) && !json.Contains("\"Group\"", StringComparison.Ordinal)
            && ModelCombinationGenerator.Choices(reopened).SequenceEqual(directional), "generator: choices kept as text and restored after reopening");
        var preparation = typeof(ModelCombinations).GetMethod("PreparationRevision", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Check((string)preparation.Invoke(null, [applied])! == (string)preparation.Invoke(null, [twoWay])!, "generator: the choices are not an input of the GPC preparation");
        var size = typeof(ModelCombinations).GetMethod("SerializedSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Check((long)size.Invoke(null, [applied])! == JsonSerializer.SerializeToUtf8Bytes(applied).LongLength, "generator: the size of a copy with choices equals its serialization");
        var updated = ModelSnapshotUpdate.Apply(applied, ModelSnapshot.FromJson(JsonSerializer.Serialize(twoWay)), ModelUpdateScope.Everything);
        Check(updated.GeneratorCases?.Length == directional.Length, "generator: an update of the copy keeps the choices");
        var unknown = ModelSnapshot.FromJson(json.Replace("\"Direction\":\"X\"", "\"Direction\":\"W\"", StringComparison.Ordinal));
        Check(ModelCombinationGenerator.Choices(unknown).Single(a => a.Case == "E").Direction == SeismicDirection.None, "generator: a choice unknown to this version falls back to the proposal");
        return Task.CompletedTask;
    }
}
