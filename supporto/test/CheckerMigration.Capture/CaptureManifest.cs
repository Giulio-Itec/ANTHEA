using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Optional manifest of a capture (option --manifest after the mode): the command, the commit given by the caller, the assemblies loaded
// from the application folder (GPC DLLs included) with their SHA-256, and the SHA-256 of every output file. Written after the capture,
// so the output files are complete; the times are the only values that change between two identical runs.
internal static class CaptureManifest
{
    internal const string FileName = "capture-manifest.json";

    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    internal static void Write(string output, string mode, string commit, string[] args, DateTimeOffset started)
    {
        string root = Path.GetFullPath(AppContext.BaseDirectory);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && Path.GetFullPath(a.Location).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => Path.GetFileName(a.Location), StringComparer.OrdinalIgnoreCase)
            .Select(a => (JsonNode)new JsonObject
            {
                ["file"] = Path.GetFileName(a.Location), ["assemblyVersion"] = a.GetName().Version?.ToString(),
                ["fileVersion"] = FileVersionInfo.GetVersionInfo(a.Location).FileVersion, ["sha256"] = Hash(a.Location)
            }).ToArray();
        var outputs = Directory.GetFiles(output).Where(f => !string.Equals(Path.GetFileName(f), FileName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
            .Select(f => (JsonNode)new JsonObject { ["file"] = Path.GetFileName(f), ["bytes"] = new FileInfo(f).Length, ["sha256"] = Hash(f) }).ToArray();
        var finished = DateTimeOffset.Now;
        var manifest = new JsonObject
        {
            ["tool"] = "CheckerMigration.Capture", ["mode"] = mode, ["commit"] = commit,
            ["arguments"] = new JsonArray(args.Select(a => (JsonNode)a).ToArray()), ["commandLine"] = Environment.CommandLine,
            ["applicationFolder"] = root, ["runtime"] = RuntimeInformation.FrameworkDescription, ["os"] = RuntimeInformation.OSDescription,
            ["started"] = started.ToString("o"), ["finished"] = finished.ToString("o"), ["seconds"] = Math.Round((finished - started).TotalSeconds, 1),
            ["loadedAssemblies"] = new JsonArray(assemblies), ["outputs"] = new JsonArray(outputs)
        };
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        File.WriteAllText(Path.Combine(output, FileName), manifest.ToJsonString(options) + "\n", new UTF8Encoding(false));
        Console.WriteLine("Manifest -> " + Path.Combine(output, FileName));
    }
}
