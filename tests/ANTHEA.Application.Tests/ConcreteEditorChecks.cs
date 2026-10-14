using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;

internal static class ConcreteEditorChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        static string Failure(Action action) { try { action(); } catch (ArgumentException ex) { return ex.Message; } throw new InvalidOperationException("Input invalido accettato"); }
        void Independent<T>(Func<T[]> read, IEnumerable<T> expected)
        {
            Check(read().SequenceEqual(expected), "Catalogo diverso dal motore");
            var copy = read(); Array.Clear(copy);
            Check(read().SequenceEqual(expected), "Catalogo mutato dalla vista");
        }
        Independent(() => ConcreteEditor.Sets, SectionWorkspace.Sets);
        Independent(() => ConcreteEditor.SharedSleFields, SectionWorkspace.SharedSleFields);
        Independent(() => ConcreteEditor.Standards, ConcreteStandards.OrdinaryNames);
        Independent(() => ConcreteEditor.ResistanceCriteria, CheckerSection.Criteria);
        Independent(() => ConcreteEditor.Coefficients, ConcreteStandards.Coefficients);
        Independent(() => ConcreteEditor.CommonCoefficients, ConcreteCalculationSettings.CommonCoefficients);
        Independent(() => ConcreteEditor.DesignTargets, ConcreteReinforcementDesign.Targets);
        foreach (string path in Directory.GetFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*.json"))
        {
            var fixture = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if (fixture.S("module") != "str_palo") continue;
            var expected = (JsonObject)fixture["data"]!.DeepClone();
            // Assign identities before copying so normalization does not mask changes to persisted fields.
            SectionWorkspace.Prepare(expected);
            var actual = (JsonObject)expected.DeepClone();
            var nativeSettings = SectionWorkspace.Prepare(expected);
            var settings = ConcreteEditor.PrepareWorkspace(actual);
            Check(JsonNode.DeepEquals(actual, expected), "Preparazione modifica la fixture " + path);
            var input = actual["input"]!.AsObject();
            ConcreteEditor.PrepareParameters(input, settings);
            ConcreteCalculationSettings.Prepare(expected["input"]!.AsObject(), nativeSettings);
            ConcreteEditor.PrepareDesign(settings); ConcreteReinforcementDesign.Prepare(nativeSettings);
            Check(JsonNode.DeepEquals(actual, expected), "Default parametri o ricerca cambiati");
            foreach (string set in ConcreteEditor.Sets)
                Check(ConcreteEditor.Label(set) == SectionWorkspace.Label(set)
                    && ConcreteEditor.HasStressCheck(set) == SleCheckScope.Stress(set)
                    && ConcreteEditor.HasCrackCheck(set, settings) == SleCheckScope.Cracking(set, nativeSettings), "Ambito verifiche cambiato");
        }
        foreach (string name in ConcreteEditor.Standards)
        {
            Check(JsonNode.DeepEquals(ConcreteEditor.StandardDefaults(name), ConcreteStandards.Defaults(name))
                && ConcreteEditor.StandardNote(name) == ConcreteStandards.Note(name), "Coefficienti o nota alterati");
            var first = ConcreteEditor.StandardDefaults(name); first["GammaC"] = -1;
            Check(ConcreteEditor.StandardDefaults(name).D("GammaC") > 0, "Default condivisi tra editor");
        }
        foreach (double area in new[] { 12.5, 150, 1000 })
            Check(BitConverter.DoubleToInt64Bits(ConcreteEditor.ReinforcementDiameter(area)) == BitConverter.DoubleToInt64Bits(ReinforcementGeometry.EquivalentDiameter(area)), "Diametro non esatto");
        foreach (int count in new[] { 1, 7, 1000 })
            Check(BitConverter.DoubleToInt64Bits(ConcreteEditor.ReinforcementArea(12.7, count)) == BitConverter.DoubleToInt64Bits(ReinforcementGeometry.Area(12.7, count)), "Area non esatta");
        foreach (string invalid in new[] { "", "abc", "NaN", "Infinity" })
            Check(Failure(() => ConcreteEditor.Number(invalid, "N")) == Failure(() => SectionWorkspace.Number(invalid, "N")), "Rifiuto numerico cambiato");
        Check(Failure(() => ConcreteEditor.ReinforcementDiameter(-1)) == Failure(() => ReinforcementGeometry.EquivalentDiameter(-1)), "Area negativa accettata");
        Check(Failure(() => ConcreteEditor.ValidateStirrups(J.Obj(("staffe_presenti", "forse")), new()))
            == Failure(() => ConcreteCalculationSettings.ValidateStirrups(J.Obj(("staffe_presenti", "forse")), new())), "Validazione staffe cambiata");
        return checks;
    }
}
