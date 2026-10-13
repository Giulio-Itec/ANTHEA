using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

int checks = 0;
void Check(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); checks++; }
try
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ANTHEA.sln"))) directory = directory.Parent;
    string root = directory?.FullName ?? throw new DirectoryNotFoundException("ANTHEA.sln non trovato.");
    string output = args.Length == 0 ? Path.Combine(root, "supporto/artefatti/refactoring/f3/architecture") : Path.GetFullPath(args[0]);
    Directory.CreateDirectory(output);
    string[] modules = ["ANTHEA.Application", "ANTHEA.Reports", "ANTHEA.Wiki"];
    foreach (string module in modules)
    {
        var project = XDocument.Load(Path.Combine(root, "src", module, module + ".csproj"));
        Check(project.Descendants("TargetFramework").Single().Value == "net8.0", module + ": target non portabile");
        Check(!project.Descendants("UseWPF").Any(e => e.Value.Equals("true", StringComparison.OrdinalIgnoreCase)), module + ": WPF vietato");
        var projectReferences = project.Descendants("ProjectReference").Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value)).ToArray();
        if (module == "ANTHEA.Reports") Check(!projectReferences.Contains("ANTHEA.Application"), "Reports: riferimento di progetto verso Application");
        if (module == "ANTHEA.Wiki") Check(!projectReferences.Any(n => n.StartsWith("ANTHEA.", StringComparison.Ordinal)), "Wiki: riferimento di progetto verso ANTHEA");
        string[] references = Metadata(Path.Combine(AppContext.BaseDirectory, module + ".dll")).Assemblies;
        Check(!references.Any(IsDesktop), module + ": dipendenza WPF/Office");
        if (module == "ANTHEA.Reports") Check(!references.Contains("ANTHEA.Application"), "Reports dipende da Application");
        if (module == "ANTHEA.Wiki") Check(!references.Any(a => a.StartsWith("ANTHEA.", StringComparison.Ordinal)), "Wiki dipende da un altro modulo ANTHEA");
        foreach (string file in SourceFiles(Path.Combine(root, "src", module)))
            Check(!File.ReadAllText(file).Contains("Type.GetTypeFromProgID", StringComparison.Ordinal), "COM nello strato applicativo: " + file);
    }
    var baseline = JsonSerializer.Deserialize<ArchitectureBaseline>(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/architecture.json")))!;
    using var fixtureManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/manifest.json")));
    string manifestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "lib/Checker/manifest.json"))));
    Check(manifestHash.Equals(fixtureManifest.RootElement.GetProperty("library_manifest_sha256").GetString(), StringComparison.OrdinalIgnoreCase), "Set GPC cambiato durante F3: il riferimento va riesaminato prima degli spostamenti");
    var desktop = Metadata(Path.Combine(root, "X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll"));
    Check(!desktop.Assemblies.Contains("Materiali"), "Progetto WPF Materiali ancora separato dal Desktop");
    Check(!desktop.Assemblies.Contains("ANTHEA.Core"), "Core ancora presente nel prodotto desktop");
    var reportSources = SourceFiles(Path.Combine(root, "src/ANTHEA.Reports")).ToArray();
    Check(reportSources.Count(f => File.ReadAllText(f).Contains("ZipArchiveMode.Create")) == 1 &&
        reportSources.Where(f => File.ReadAllText(f).Contains("ZipArchiveMode.Create")).All(f => Path.GetFileName(f) == "DocxWriter.cs"), "Scrittori DOCX duplicati");
    Check(!File.ReadAllText(Path.Combine(root, "src/ANTHEA.Reports/ProjectReportComposer.cs")).Contains("ZipArchive"), "Il report progetto riapre DOCX intermedi");
    var limits = JsonSerializer.Deserialize<ArchitectureLimits>(File.ReadAllText(Path.Combine(root, "tests/ANTHEA.Architecture.Tests/limits.json")))!;
    var observed = desktop.Types.Where(p => p.Key == "ANTHEA.Calculations" || p.Key.StartsWith("GPCChecker", StringComparison.Ordinal)).ToDictionary();
    foreach (var (assembly, types) in observed)
    {
        Check(baseline.TypeRefs.TryGetValue(assembly, out var allowed), "Nuova dipendenza diretta dal calcolo: " + assembly);
        Check(types.Length <= allowed!.Length && types.Length <= limits.TypeRefs.GetValueOrDefault(assembly), "TypeRef in aumento: " + assembly);
        Check(types.All(t => allowed.Contains(t, StringComparer.Ordinal)), "Nuovo tipo di calcolo usato direttamente dalla UI: " + assembly);
    }
    var sources = new[] { "X.Desktop", "X.Materiali" }.Where(d => Directory.Exists(Path.Combine(root, d)))
        .SelectMany(d => SourceFiles(Path.Combine(root, d))).ToArray();
    int asynchronousVoid = sources.Sum(f => Regex.Matches(File.ReadAllText(f), @"\basync\s+void\b").Count);
    Check(asynchronousVoid <= limits.AsyncVoid && asynchronousVoid <= baseline.AsyncVoid.Sum(f => f.Count), "async void in aumento");
    int timers = sources.Sum(f => Regex.Matches(File.ReadAllText(f), @"\bDispatcherTimer\b").Count);
    Check(timers <= limits.DispatcherTimer && timers <= baseline.DispatcherTimer.Sum(f => f.Count), "DispatcherTimer in aumento");
    // Prove del rilevatore: un riferimento proibito o un target Windows devono essere distinguibili.
    Check(IsDesktop("PresentationFramework") && IsDesktop("WindowsBase") && IsDesktop("Microsoft.Office.Interop.Word"), "Rilevatore dei riferimenti desktop");
    Check(!IsDesktop("System.Runtime") && !IsDesktop("ANTHEA.Application"), "Rilevatore troppo esteso");
    File.WriteAllText(Path.Combine(output, "counts.json"), JsonSerializer.Serialize(new {
        TypeRefs = observed.ToDictionary(p => p.Key, p => p.Value.Length), AsyncVoid = asynchronousVoid, DispatcherTimer = timers
    }, new JsonSerializerOptions { WriteIndented = true }));
    File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli di architettura");
    Console.WriteLine($"PASS {checks} controlli di architettura");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

static bool IsDesktop(string name) => name is "PresentationCore" or "PresentationFramework" or "WindowsBase" or "System.Xaml" or "System.Windows.Forms" or "WpfMath"
    || name.StartsWith("Microsoft.Office", StringComparison.Ordinal);
static IEnumerable<string> SourceFiles(string directory) => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
    .Where(f => !Path.GetRelativePath(directory, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"));
static (string[] Assemblies, Dictionary<string, string[]> Types) Metadata(string path)
{
    using var stream = File.OpenRead(path); using var reader = new PEReader(stream); var metadata = reader.GetMetadataReader();
    string Assembly(EntityHandle handle) => handle.Kind switch {
        HandleKind.AssemblyReference => metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)handle).Name),
        HandleKind.TypeReference => Assembly(metadata.GetTypeReference((TypeReferenceHandle)handle).ResolutionScope), _ => ""
    };
    var types = metadata.TypeReferences.Select(h => metadata.GetTypeReference(h)).Select(t => new {
        Assembly = Assembly(t.ResolutionScope), Type = metadata.GetString(t.Namespace) + "." + metadata.GetString(t.Name)
    }).GroupBy(t => t.Assembly).ToDictionary(g => g.Key, g => g.Select(t => t.Type).Order(StringComparer.Ordinal).ToArray());
    return (metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToArray(), types);
}
internal sealed record SourceCount(string File, int Count);
internal sealed record ArchitectureBaseline(Dictionary<string, string[]> TypeRefs, SourceCount[] AsyncVoid, SourceCount[] DispatcherTimer);

internal sealed record ArchitectureLimits(Dictionary<string, int> TypeRefs, int AsyncVoid, int DispatcherTimer);
