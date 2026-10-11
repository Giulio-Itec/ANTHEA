using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Anthea.Testing.Capture;

/// <summary>
/// manifest.json of a capture: ANTHEA commit and state of the tree, SHA-256 of lib/Checker/manifest.json and of every loaded assembly
/// (Assembly.Location) checked against the library manifest, SDK and runtime, corpus with the SHA of its files, counts, fallbacks and
/// what is not covered. The manifest is excluded from the value comparison; 'compare' prints its differences as information.
/// </summary>
public static class Manifest
{
    public static JsonObject Build(CaptureOptions options, string output, JsonArray corpus, IReadOnlyDictionary<string, int> written, int errors, int fallbacks,
        DateTimeOffset started, TimeSpan elapsed)
    {
        string root = options.Root;
        var git = Git(root);
        string commit = options.Commit is { Length: > 0 } c ? c : git.Run("rev-parse HEAD");
        var status = git.Run("status --porcelain=v1 --untracked-files=no").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();

        string libManifest = Path.Combine(root, "lib", "Checker", "manifest.json");
        var expected = new Dictionary<string, (string Version, string Sha)>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(libManifest))
            foreach (var a in JsonNode.Parse(File.ReadAllText(libManifest, Encoding.UTF8))!["assemblies"] as JsonArray ?? [])
                expected[a!["file"]!.GetValue<string>()] = (a["assemblyVersion"]?.GetValue<string>() ?? "", a["sha256"]?.GetValue<string>() ?? "");

        string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        var assemblies = new JsonArray(); var libraries = new JsonArray(); bool coherent = expected.Count > 0;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.Location.Length > 0).OrderBy(a => a.GetName().Name, StringComparer.Ordinal))
        {
            string location = assembly.Location, file = Path.GetFileName(location);
            bool framework = location.StartsWith(runtimeDirectory, StringComparison.OrdinalIgnoreCase);
            string? sha = framework ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(location)));
            assemblies.Add(new JsonObject
            {
                ["nome"] = assembly.GetName().Name, ["versione"] = assembly.GetName().Version?.ToString(),
                ["origine"] = framework ? "runtime" : Path.GetRelativePath(AppContext.BaseDirectory, location).Replace('\\', '/'), ["sha256"] = sha
            });
            if (expected.TryGetValue(file, out var e))
            {
                bool same = string.Equals(e.Sha, sha, StringComparison.OrdinalIgnoreCase);
                coherent &= same;
                libraries.Add(new JsonObject { ["file"] = file, ["versione_manifest"] = e.Version, ["versione_caricata"] = assembly.GetName().Version?.ToString(), ["sha256_manifest"] = e.Sha, ["sha256_caricata"] = sha, ["coincide"] = same });
            }
        }
        var loaded = libraries.Select(l => l!["file"]!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var missing in expected.Keys.Where(k => !loaded.Contains(k)).OrderBy(k => k, StringComparer.Ordinal))
        {
            // A DLL of the snapshot not loaded by the corpus: compare the copy next to the tool.
            string local = Path.Combine(AppContext.BaseDirectory, missing);
            string? sha = File.Exists(local) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(local))) : null;
            bool same = string.Equals(expected[missing].Sha, sha, StringComparison.OrdinalIgnoreCase); coherent &= same;
            libraries.Add(new JsonObject { ["file"] = missing, ["versione_manifest"] = expected[missing].Version, ["versione_caricata"] = "non caricata", ["sha256_manifest"] = expected[missing].Sha, ["sha256_caricata"] = sha, ["coincide"] = same });
        }

        var files = new JsonArray(corpus.Select(e => e!["file"]?.GetValue<string>()).Where(f => f is not null).Distinct().OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (JsonNode)new JsonObject { ["file"] = f, ["sha256"] = Corpus.Sha256(Path.Combine(root, f!)) }).ToArray());
        var counts = new JsonObject(); foreach (var (k, v) in written.OrderBy(p => p.Key, StringComparer.Ordinal)) counts[k] = v;
        counts["errori_registrati"] = errors; counts["casi"] = corpus.Count;

        var manifest = new JsonObject
        {
            ["schema"] = 1,
            ["strumento"] = "ANTHEA.Testing",
            ["tag"] = options.Tag,
            ["creata"] = started.ToString("O", CultureInfo.InvariantCulture),
            ["durata_s"] = Math.Round(elapsed.TotalSeconds, 1),
            ["anthea"] = new JsonObject
            {
                ["commit"] = commit, ["branch"] = git.Run("rev-parse --abbrev-ref HEAD"), ["git"] = git.Executable,
                ["albero_modificato"] = status.Length > 0, ["file_modificati"] = new JsonArray(status.Take(200).Select(s => (JsonNode)s).ToArray())
            },
            ["librerie"] = new JsonObject
            {
                ["manifest_json"] = "lib/Checker/manifest.json",
                ["manifest_sha256"] = File.Exists(libManifest) ? Corpus.Sha256(libManifest) : null,
                ["coerenti_con_manifest"] = coherent,
                ["dll"] = libraries
            },
            ["assembly"] = assemblies,
            ["ambiente"] = new JsonObject
            {
                ["runtime"] = RuntimeInformation.FrameworkDescription,
                ["sdk"] = Run("dotnet", "--version", root),
                ["sistema"] = RuntimeInformation.OSDescription,
                ["architettura"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["processori"] = Environment.ProcessorCount,
                ["cultura"] = CultureInfo.CurrentCulture.Name,
                ["tiered_compilation"] = AppContext.GetData("System.Runtime.TieredCompilation")?.ToString() ?? "predefinita"
            },
            ["corpus"] = corpus.DeepClone(),
            ["file_corpus"] = files,
            ["conteggi"] = counts,
            ["ripieghi"] = new JsonObject { ["attivo"] = options.Trace, ["voci"] = fallbacks, ["file"] = "fallbacks.json" },
            ["non_coperto"] = new JsonArray(CaptureRunner.NotCovered.Select(n => (JsonNode)new JsonObject { ["voce"] = n.Item, ["motivo"] = n.Reason }).ToArray()),
            ["confronto"] = "manifest.json, log.txt e raw/ sono esclusi dal confronto dei valori (tests/ANTHEA.Testing/tolerances.json, file_esclusi)."
        };
        // Motore SLE scelto con '--motore-sle' (refactoring F2.7b, commit A4); senza l'opzione il manifest resta quello di prima.
        if (options.ServiceabilityEngine is { } engine) manifest["motore_sle"] = engine.ToString();
        return manifest;
    }

    sealed record GitTool(string? Executable, string Root)
    {
        public string Run(string arguments) => Executable is null ? "" : Manifest.Run(Executable, "-C \"" + Root + "\" " + arguments, Root);
    }

    static GitTool Git(string root)
    {
        var candidates = new List<string?> { Environment.GetEnvironmentVariable("ANTHEA_GIT") };
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => p.Length > 0).Select(p => Path.Combine(p.Trim('"'), "git.exe")));
        candidates.Add(@"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe");
        candidates.Add(@"C:\Program Files\Git\cmd\git.exe");
        return new GitTool(candidates.FirstOrDefault(c => c is { Length: > 0 } && File.Exists(c)), root);
    }

    static string Run(string file, string arguments, string directory)
    {
        try
        {
            var info = new ProcessStartInfo(file, arguments) { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var process = Process.Start(info)!;
            string text = process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd();
            process.WaitForExit(60000);
            return process.ExitCode == 0 ? text.TrimEnd() : "";
        }
        catch (Win32Exception) { return ""; }
    }
}
