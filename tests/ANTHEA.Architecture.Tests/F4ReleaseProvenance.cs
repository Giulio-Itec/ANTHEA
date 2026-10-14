using System.Security.Cryptography;
using System.Text.Json;

internal static class F4ReleaseProvenance
{
    internal static void Verify(string root, string manifestHash, string frozenF3Hash, Action<bool, string> check)
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f4/library-release.json")));
        var expected = reference.RootElement;
        check(expected.GetProperty("previous_manifest_sha256").GetString()!.Equals(frozenF3Hash, StringComparison.OrdinalIgnoreCase), "Riferimento F3 alterato durante il rilascio F4");
        check(expected.GetProperty("library_manifest_sha256").GetString()!.Equals(manifestHash, StringComparison.OrdinalIgnoreCase), "Manifest GPC diverso dal rilascio F4 registrato");
        string path = Path.Combine(root, "lib/Checker/release-set.json");
        check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(expected.GetProperty("release_set_sha256").GetString(), StringComparison.OrdinalIgnoreCase), "Provenienza completa del set GPC assente o obsoleta");
        using var release = JsonDocument.Parse(File.ReadAllText(path));
        var set = release.RootElement;
        check(!set.GetProperty("candidate").GetBoolean(), "Set candidato distribuito come rilascio");
        var suites = set.GetProperty("suites").EnumerateArray().ToArray();
        foreach (string name in new[] { "Model.Core.Tests", "UnitTest", "GPCChecker.Test.Concrete", "GPCChecker.Test.Steel", "GPCChecker.Test.CompositeBridge", "GPCChecker.Test.Geotechnics", "GPCChecker.Test.Calculation", "ShallowFoundation.Tests", "Design.Checks", "ModelChecker.Tests" })
        {
            var entries = suites.Where(s => s.GetProperty("name").GetString() == name).ToArray();
            check(entries.Length == 1 && entries[0].GetProperty("passed").GetInt32() > 0, "Suite mancante o vuota nel rilascio: " + name);
            var suite = entries[0];
            int allowed = suite.TryGetProperty("knownFailures", out var known) ? known.GetArrayLength() : 0;
            check(suite.GetProperty("failed").GetInt32() == allowed, "Fallimenti non classificati nel rilascio: " + name);
        }
        using var snapshot = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "lib/Checker/manifest.json")));
        foreach (var assembly in snapshot.RootElement.GetProperty("assemblies").EnumerateArray())
        {
            string file = assembly.GetProperty("file").GetString()!;
            var native = set.GetProperty("files").EnumerateArray().Single(f => f.GetProperty("file").GetString() == file);
            foreach (string field in new[] { "sha256", "assemblyVersion", "commit" })
                check(assembly.GetProperty(field).GetString() == native.GetProperty(field).GetString(), "Snapshot incoerente con il set completo: " + file + "/" + field);
            string repo = assembly.GetProperty("repository").GetString()!;
            var source = set.GetProperty("repositories").GetProperty(repo);
            check(assembly.GetProperty("pushed").GetBoolean() && source.GetProperty("onOriginMaster").GetBoolean()
                && source.GetProperty("commit").GetString() == assembly.GetProperty("commit").GetString(), "Sorgente non pubblicata: " + file);
        }
    }
}
