using System.Text.Json;
using ANTHEA.ModelWorkspace;

static partial class Program
{
    static void DisplacementChecks(string output)
    {
        string Fixture(string name, string unit, string[] heads, object[][] rows)
        {
            string directory = Path.Combine(output, "displacement-fixtures", name); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "displacements-global-000.json"), JsonSerializer.Serialize(new
            {
                DisplacementsGlobal = new { DIST = unit, HEAD = heads, DATA = rows }
            }));
            return directory;
        }
        string[] headers = ["Index", "Node", "Load", "DX", "DY", "DZ"];
        object[][] values = [["1", "1", "A", "3.000e+00", "-4.000e+00", "12.000e+00"], [2, 2, "A", 0, 1, 2], [3, 1, "B(max)", 8, 9, 10]];
        var nodes = new HashSet<int> { 1, 2 };
        var mm = MidasDisplacementTables.ReadDirectory(Fixture("mm", "mm", headers, values), nodes);
        Check(mm.Count == 2 && mm["A"].Length == 2 && mm["A"][0] == new ModelDisplacement(1, .003, -.004, .012), "global nodal import preserves sign, scientific precision and millimetre-to-metre normalization");
        Check(mm["A"][1] == new ModelDisplacement(2, 0, .001, .002), "displacement import accepts JSON numeric cells without locale dependence");
        var cm = MidasDisplacementTables.ReadDirectory(Fixture("cm", "CM", headers, [[1, 1, "A", 3, -4, 12]]), nodes);
        Check(cm["A"][0] == new ModelDisplacement(1, .03, -.04, .12), "centimetre displacement table is normalized to metres");
        var metres = MidasDisplacementTables.ReadDirectory(Fixture("metres", "M", headers, [[1, 1, "A", .003, -.004, .012]]), nodes);
        Check(metres["A"][0] == mm["A"][0], "metre and millimetre source tables represent identical translations");
        Reject(() => MidasDisplacementTables.ReadDirectory(Fixture("wrong-unit", "IN", headers, values), nodes), "unknown displacement units are rejected");
        Reject(() => MidasDisplacementTables.ReadDirectory(Fixture("local", "mm", ["Index", "Node", "Load", "Dx", "Dy", "Dz"], values), nodes), "local-axis displacement table cannot masquerade as a global field");
        Reject(() => MidasDisplacementTables.ReadDirectory(Fixture("ambiguous", "mm", ["Index", "Node", "Load", "DX", "DX", "DZ"], values), nodes), "ambiguous displacement headers are rejected");
        Reject(() => MidasDisplacementTables.ReadDirectory(Fixture("foreign-node", "mm", headers, [[1, 999, "A", 1, 2, 3]]), nodes), "displacements must resolve to actual model nodes");
        Reject(() => MidasDisplacementTables.ReadDirectory(Fixture("non-finite", "mm", headers, [[1, 1, "A", "NaN", 2, 3]]), nodes), "non-finite displacement component is rejected");
        Reject(() => MidasDisplacementTables.ReadDirectory(Fixture("empty-case", "mm", headers, [[1, 1, "", 1, 2, 3]]), nodes), "displacement case identity is required");
        Reject(() => MidasDisplacementTables.ReadDirectory(Fixture("duplicate", "mm", headers, [[1, 1, "A", 1, 2, 3], [2, 1, "A", 1, 2, 3]]), nodes), "duplicate nodal displacement cannot silently replace an earlier value");
        Reject(() => MidasDisplacementTables.ReadDirectory(Fixture("stage", "mm", ["Node", "Load", "Stage", "Step", "DX", "DY", "DZ"], []), nodes), "construction stages are not collapsed into a static result case");
        var sample = Sample.Model(); string fingerprint = sample.Fingerprint();
        var forces = sample.Results[0] with { Name = "A" };
        var merged = MidasDisplacementTables.Merge([forces], mm);
        Check(merged.Length == 2 && merged[0].Displacements!.Length == 2 && ReferenceEquals(merged[0].Values, forces.Values), "nodal import joins matching cases without recomputing or replacing force arrays");
        Check(merged.Single(r => r.Name == "B(max)").IsEnvelope && merged.Single(r => r.Name == "B(max)").Values.Length == 0, "displacement-only envelope retains its non-simultaneous result identity");
        Check(sample.Fingerprint() == fingerprint && forces.Displacements == null, "merging nodal tables does not mutate the prior model or force record");
        var old = JsonSerializer.Deserialize<ModelViewPreset>("{\"Name\":\"Vecchia\",\"Appearance\":{\"Deformation\":true,\"DeformationScale\":100}}")!;
        Check(old.ResultType == 0 && old.Appearance is { DeformationScaleMode: 2, DeformationPhase: 1 }, "older view recipes retain force fields, manual deformation scale and complete phase");
        foreach (var invalid in new[] { old with { ResultType = 3 }, old with { DisplacementUnit = "inch" }, old with { Appearance = old.Appearance! with { DeformationPhase = double.NaN } }, old with { Appearance = old.Appearance! with { AnimationDuration = 0 } } })
            Reject(() => ModelViewPresetStore.Validate([invalid]), "invalid displacement/animation recipe is rejected before graphics allocation");
    }
}
