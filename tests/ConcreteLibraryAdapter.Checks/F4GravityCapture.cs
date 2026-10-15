using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4GravityCapture
{
    internal static void Export(string root, string reference, string output)
    {
        string path = Path.Combine(output, "gravity-f4.json");
        if (File.Exists(path)) throw new IOException("A gravity fixture is never overwritten");
        var index = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f4/reference.json")))!;
        var hashes = index["files"]!.AsArray().ToDictionary(f => f!["file"]!.GetValue<string>(), f => f!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(reference, relative));
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative], StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Frozen gravity reference changed: " + relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        var rows = new JsonArray();
        foreach (string file in Directory.GetFiles(Path.Combine(reference, "inputs/walls"), "gravity-*.json").Order(StringComparer.Ordinal))
        {
            string id = Path.GetFileName(file); var input = Read("inputs/walls/" + id); var result = Read("results/walls/" + id);
            if (result.S("status") != "ok") throw new InvalidOperationException(id + ": expected valid frozen wall");
            var material = LegacyRetainingWall.GravityMaterial(input); var native = ConcreteMaterials.Concrete(RetainingWall.MaterialSectionInput(input));
            var value = result["value"]!;
            rows.Add(new JsonObject {
                ["id"] = id, ["input"] = input, ["material"] = new JsonObject {
                    ["compression"] = material.Compression, ["tension"] = material.Tension, ["modulus"] = material.E,
                    ["shear0"] = material.Shear0, ["shearCap"] = material.ShearCap, ["gamma"] = material.Gamma,
                    ["fctk05"] = native.Fctk05, ["ecm"] = native.Ecm,
                    ["gammaC"] = RetainingWall.MaterialSectionInput(input).D("gamma_c") },
                ["checks"] = value["Structural"]!.DeepClone(),
                ["cases"] = new JsonArray(value["Cases"]!.AsArray().OfType<JsonObject>().Select(c => (JsonNode)new JsonObject {
                    ["id"] = c["Name"]!.DeepClone(), ["state"] = c["State"]!.DeepClone(),
                    ["contactValid"] = c["Contact"]!["Valid"]!.DeepClone(),
                    ["sections"] = c["Sections"]!.DeepClone(), ["curvatures"] = c["Curvatures"]!.DeepClone()
                }).ToArray())
            });
        }
        File.WriteAllText(path, rows.ToJsonString(J.Options));
        Console.WriteLine($"Captured {rows.Count} frozen gravity walls; SHA256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
    }
}
