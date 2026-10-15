using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;

internal static class F4DetailCapture
{
    internal static void Export(string root, string reference, string output)
    {
        string path = Path.Combine(output, "details-f4.json");
        if (File.Exists(path)) throw new IOException("A detail fixture is never overwritten");
        var hashes = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f4/reference.json")))!["files"]!.AsArray()
            .ToDictionary(f => f!["file"]!.GetValue<string>(), f => f!["sha256"]!.GetValue<string>());
        JsonObject Read(string relative)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(reference, relative));
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative], StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Frozen reference changed: " + relative);
            return JsonNode.Parse(bytes)!.AsObject();
        }
        var rows = new JsonArray();
        foreach (string scope in new[] { "walls", "schedules" })
        foreach (string file in Directory.GetFiles(Path.Combine(reference, "inputs/" + scope), "*.json").Order(StringComparer.Ordinal))
        {
            string id = scope + "/" + Path.GetFileName(file); var input = Read("inputs/" + id); var result = Read("results/" + id);
            if (result.S("status") != "ok" || input.S("family") != "cantilever") continue;
            string before = input.ToJsonString();
            var section = RetainingWall.SectionInput(input, "Fusto", input["geometry"].D("stem_base"));
            var strengths = ConcreteMaterials.DesignValues(section, SectionWorkspace.Prepare(SezioneCA.DefaultData()));
            var concrete = ConcreteMaterials.Concrete(section);
            var details = LegacyRetainingWall.CalculateReinforcementDetails(input);
            if (input.ToJsonString() != before) throw new InvalidOperationException("Legacy details changed input");
            rows.Add(new JsonObject { ["id"] = id, ["input"] = input,
                ["material"] = new JsonObject { ["fcd"] = strengths.Fcd, ["fyd"] = strengths.Fyd, ["fctk05"] = concrete.Fctk05, ["gammaC"] = section.D("gamma_c") },
                ["details"] = JsonSerializer.SerializeToNode(details),
                ["schedule"] = scope == "schedules" ? result["value"]!["schedule"]!.DeepClone() : null });
        }
        File.WriteAllText(path, rows.ToJsonString());
        Console.WriteLine($"Captured {rows.Count} legacy detail/schedule inputs; SHA256 {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
    }
}
