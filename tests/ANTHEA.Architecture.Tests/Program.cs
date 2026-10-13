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
    var productionSources = Directory.EnumerateDirectories(root, "X.*")
        .Concat(Directory.EnumerateDirectories(Path.Combine(root, "src"))).SelectMany(SourceFiles);
    bool f4Inventory = args.Contains("--f4-inventory", StringComparer.Ordinal);
    checks += F4DomainRules.Run(root, output, productionSources, f4Inventory);
    if (f4Inventory) return 0;
    string[] modules = ["ANTHEA.Application", "ANTHEA.Reports", "ANTHEA.Wiki", "ANTHEA.Serialization"];
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
        if (module == "ANTHEA.Serialization") Check(!references.Any(a => a.StartsWith("ANTHEA.", StringComparison.Ordinal) || a.StartsWith("GPC", StringComparison.Ordinal)), "Serialization dipende da un motore o modulo applicativo");
        if (module == "ANTHEA.Reports") Check(!references.Contains("ANTHEA.Application"), "Reports dipende da Application");
        if (module == "ANTHEA.Wiki") Check(!references.Any(a => a.StartsWith("ANTHEA.", StringComparison.Ordinal)), "Wiki dipende da un altro modulo ANTHEA");
        foreach (string file in SourceFiles(Path.Combine(root, "src", module)))
            Check(!File.ReadAllText(file).Contains("Type.GetTypeFromProgID", StringComparison.Ordinal), "COM nello strato applicativo: " + file);
    }
    using var differences = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/refactoring/registro-differenze.json")));
    var states = differences.RootElement.GetProperty("stati").EnumerateArray().ToArray();
    Check(states.All(s => s.ValueKind == JsonValueKind.String), "Voci di differenza inserite nell’elenco degli stati");
    var entries = differences.RootElement.GetProperty("voci").EnumerateArray().ToArray();
    Check(entries.Select(e => e.GetProperty("id").GetString()).Distinct().Count() == entries.Length, "Identità duplicate nel registro");
    Check(entries.All(e => states.Any(s => s.GetString() == e.GetProperty("stato").GetString())), "Stato non dichiarato nel registro");
    Check(entries.Any(e => e.GetProperty("id").GetString() == "F3-01"), "Rinomine diagnostiche F3 non registrate fra le differenze");
    var baseline = JsonSerializer.Deserialize<ArchitectureBaseline>(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/architecture.json")))!;
    using var fixtureManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/manifest.json")));
    string manifestHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "lib/Checker/manifest.json"))));
    Check(manifestHash.Equals(fixtureManifest.RootElement.GetProperty("library_manifest_sha256").GetString(), StringComparison.OrdinalIgnoreCase), "Set GPC cambiato durante F3: il riferimento va riesaminato prima degli spostamenti");
    var desktopProject = XDocument.Load(Path.Combine(root, "X.Desktop/X.Desktop.csproj"));
    Check(desktopProject.Descendants("GpcRuntimeOnly").Any(e => e.Value == "true" && e.Attribute("Condition")?.Value == "'$(Configuration)' != 'UiTests'"), "Desktop deve distribuire GPC senza riferimenti diretti di compilazione");
    Check(!desktopProject.Descendants("ProjectReference").Any(e => (e.Attribute("Include")?.Value ?? "").Contains("X.Calculations")), "Riferimento di progetto Desktop al motore legacy");
    int gpcArgument = Array.IndexOf(args, "--gpc-snapshot");
    string runtimeSnapshot = gpcArgument < 0 ? Path.Combine(root, "lib/Checker")
        : gpcArgument + 1 < args.Length ? Path.GetFullPath(args[gpcArgument + 1]) : throw new ArgumentException("Missing explicit GPC snapshot directory.");
    using (var runtimeManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtimeSnapshot, "manifest.json"))))
        foreach (var assembly in runtimeManifest.RootElement.GetProperty("assemblies").EnumerateArray())
        {
            string file = assembly.GetProperty("file").GetString()!;
            string path = Path.Combine(root, "X.Desktop/bin/Release/net8.0-windows", file);
            Check(File.Exists(path), "Libreria runtime mancante: " + file);
            Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).Equals(assembly.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase), "Libreria runtime diversa dal set coerente: " + file);
        }
    var desktop = Metadata(Path.Combine(root, "X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll"));
    // Metadata records constructors/methods actually referenced by the built product, including target-typed new.
    // This inventory does not classify all engineering semantics: getters and data adapters remain visible.
    var accesses = desktop.Members.Where(m => m.Assembly == "ANTHEA.Calculations" || m.Assembly.StartsWith("GPC", StringComparison.Ordinal)).ToArray();
    string[] transferredCalls = [
        "Anthea.Calculations.SezioneCA::.ctor", "Anthea.Calculations.Barra::.ctor",
        "Anthea.Calculations.ConcreteCalculationSettings::UpdateAutomaticShear", "Anthea.Calculations.CheckerSection::PrepareModel",
        "Anthea.Calculations.SectionNeutralAxis::Concrete", "Anthea.Calculations.SectionNeutralAxis::Bridge",
        "GPC.Checkers.Concrete.Results.StrainPlane::GetStrain",
        "Anthea.Calculations.ConcreteSectionProperties::Calculate", "Anthea.Calculations.ConcreteCurvatureAnalysis::Calculate",
        "Anthea.Calculations.ConcreteReinforcementDesign::Optimize", "Anthea.Calculations.SectionMomentResistance::Calculate",
        "Anthea.Calculations.BridgeSection::SlabLayout", "Anthea.Calculations.BridgeSection::StiffenerPlates",
        "Anthea.Calculations.BridgeSection::Materials", "Anthea.Calculations.ConcreteMaterials::DesignValues",
        "Anthea.Calculations.BridgeSection::Calculate", "Anthea.Calculations.PaloOrizzontale::Calculate",
        "Anthea.Calculations.MicropaloOrizzontale::Calculate", "Anthea.Calculations.RetainingWall::Calculate",
        "Anthea.Calculations.ElasticHorizontalPile::DesignSegment",
        "GPC.Checkers.Geotechnics.Piles.PileSegments::Split", "GPC.Checkers.Geotechnics.Piles.PileSegments::Validate"
    ];
    string[] transferredEditors = ["CalculationHelp", "VerificationSummary", "SectionWorkspace", "ConcreteStandards",
        "ConcreteCalculationSettings", "ConcreteReinforcementDesign", "SleCheckScope", "ReinforcementGeometry", "CheckerSection", "ModuleCatalog", "ModuleDefinition", "RebarMaterial", "ElasticHorizontalPile", "HorizontalPileGroup", "PaloOrizzontale", "PileCalculationProgress", "ConcreteShearResult", "TorsionResult", "TorsionGeometry", "MomentCurvaturePoint", "MomentCurvatureResult", "PrincipalMomentResistance", "ConcreteDesignCandidate", "ConcreteDesignCheck", "ConcreteDesignMetrics", "ConcreteDesignProgress", "ConcreteDesignPerformance", "ConcreteDesignStage", "ConcreteDesignResult", "ConcreteLibraryMapping"];
    Check(!accesses.Any(m => m.Type == "Anthea.Calculations.BridgeConcept" || m.Type.StartsWith("Anthea.Calculations.BridgeConcept+", StringComparison.Ordinal)), "Contratti Bridge Design nativi nella UI");
    Check(!accesses.Any(m => m.Type == "Anthea.Calculations.RetainingWall" || m.Type.StartsWith("Anthea.Calculations.RetainingWall+", StringComparison.Ordinal)
        || m.Type.StartsWith("Anthea.Calculations.Geotechnics.", StringComparison.Ordinal)), "Contratti muri nativi nella UI");
    foreach (string name in transferredEditors)
        Check(!accesses.Any(m => m.Type == "Anthea.Calculations." + name), "Editor di calcolo ritornato nella UI: " + name);
    foreach (string call in transferredCalls)
        Check(!accesses.Any(m => m.Type + "::" + m.Name == call), "Chiamata al motore ritornata nella UI: " + call);
    Check(desktop.Members.Any(m => m.Name.StartsWith("get_", StringComparison.Ordinal)) && desktop.Members.Any(m => m.Kind == "Method" && !m.Name.StartsWith("get_", StringComparison.Ordinal)),
        "Inventario non distingue accessi ai dati e metodi");
    File.WriteAllText(Path.Combine(output, "desktop-accesses.json"), JsonSerializer.Serialize(new {
        Kind = "metadata-inventory-not-semantic-compliance", Members = accesses,
        Counts = accesses.GroupBy(m => m.Assembly).ToDictionary(g => g.Key, g => new {
            Total = g.Count(), Fields = g.Count(m => m.Kind == "Field"),
            PropertyAccessors = g.Count(m => m.Kind == "Method" && (m.Name.StartsWith("get_") || m.Name.StartsWith("set_"))),
            Constructors = g.Count(m => m.Name == ".ctor")
        })
    }, new JsonSerializerOptions { WriteIndented = true }));
    Check(!desktop.Assemblies.Contains("Materiali"), "Progetto WPF Materiali ancora separato dal Desktop");
    Check(!desktop.Assemblies.Contains("ANTHEA.Core"), "Core ancora presente nel prodotto desktop");
    var reportSources = SourceFiles(Path.Combine(root, "src/ANTHEA.Reports")).ToArray();
    Check(reportSources.Count(f => File.ReadAllText(f).Contains("ZipArchiveMode.Create")) == 1 &&
        reportSources.Where(f => File.ReadAllText(f).Contains("ZipArchiveMode.Create")).All(f => Path.GetFileName(f) == "DocxWriter.cs"), "Scrittori DOCX duplicati");
    Check(!File.ReadAllText(Path.Combine(root, "src/ANTHEA.Reports/ProjectReportComposer.cs")).Contains("ZipArchive"), "Il report progetto riapre DOCX intermedi");
    Check(!File.ReadAllText(Path.Combine(root, "X.Desktop/Wpf/ProjectReport.cs")).Contains("SheetEditor"), "Il report progetto crea editor nascosti");
    foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "X.Desktop/Wpf"), "ModuleFigureRenderer*.cs"))
    {
        string source = File.ReadAllText(file);
        Check(!Regex.IsMatch(source, @"new\s+(SheetEditor|ConcreteWorkspace|SezioneCA|BridgeWorkspace|WallWorkspace)\b"), "Il renderer costruisce un editor o un motore: " + file);
    }
    Check(!File.ReadAllText(Path.Combine(root, "X.Desktop/Wpf/BridgeWorkspace.cs")).Contains("Data[\"gamma_m0\"] ="), "Regola cambio norma nella UI");
    Check(!File.ReadAllText(Path.Combine(root, "X.Desktop/Wpf/RetainingWallWorkspace.Advanced.cs")).Contains("double.TryParse"), "Parser accelerogramma nella UI");
    Check(!File.ReadAllText(Path.Combine(root, "X.Desktop/Wpf/RetainingWallWorkspace.Results.cs")).Contains("Math.Atan2"), "Angolo sismico duplicato nella UI");
    var limits = JsonSerializer.Deserialize<ArchitectureLimits>(File.ReadAllText(Path.Combine(root, "tests/ANTHEA.Architecture.Tests/limits.json")))!;
    Check(!desktop.Assemblies.Contains("ANTHEA.Calculations"), "La UI dipende ancora direttamente dal motore legacy");
    Check(!desktop.Assemblies.Any(a => a.StartsWith("GPC", StringComparison.Ordinal)), "La UI dipende direttamente dai tipi GPC");
    var observed = desktop.Types.Where(p => p.Key == "ANTHEA.Calculations" || p.Key.StartsWith("GPC", StringComparison.Ordinal)).ToDictionary();
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
static (string[] Assemblies, Dictionary<string, string[]> Types, MemberAccess[] Members) Metadata(string path)
{
    using var stream = File.OpenRead(path); using var reader = new PEReader(stream); var metadata = reader.GetMetadataReader();
    string Assembly(EntityHandle handle) => handle.Kind switch {
        HandleKind.AssemblyReference => metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)handle).Name),
        HandleKind.TypeReference => Assembly(metadata.GetTypeReference((TypeReferenceHandle)handle).ResolutionScope), _ => ""
    };
    string TypeName(TypeReferenceHandle handle)
    {
        var type = metadata.GetTypeReference(handle); string name = metadata.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference) return TypeName((TypeReferenceHandle)type.ResolutionScope) + "+" + name;
        string ns = metadata.GetString(type.Namespace); return ns.Length == 0 ? name : ns + "." + name;
    }
    var members = metadata.MemberReferences.Select(h => metadata.GetMemberReference(h))
        .Where(m => m.Parent.Kind == HandleKind.TypeReference)
        .Select(m => new MemberAccess(Assembly(m.Parent), TypeName((TypeReferenceHandle)m.Parent), metadata.GetString(m.Name), m.GetKind().ToString()))
        .Distinct().OrderBy(m => m.Assembly, StringComparer.Ordinal).ThenBy(m => m.Type, StringComparer.Ordinal).ThenBy(m => m.Name, StringComparer.Ordinal).ToArray();
    var types = metadata.TypeReferences.Select(h => metadata.GetTypeReference(h)).Select(t => new {
        Assembly = Assembly(t.ResolutionScope), Type = metadata.GetString(t.Namespace) + "." + metadata.GetString(t.Name)
    }).GroupBy(t => t.Assembly).ToDictionary(g => g.Key, g => g.Select(t => t.Type).Order(StringComparer.Ordinal).ToArray());
    return (metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToArray(), types, members);
}
internal sealed record SourceCount(string File, int Count);
internal sealed record ArchitectureBaseline(Dictionary<string, string[]> TypeRefs, SourceCount[] AsyncVoid, SourceCount[] DispatcherTimer);

internal sealed record ArchitectureLimits(Dictionary<string, int> TypeRefs, int AsyncVoid, int DispatcherTimer);

internal sealed record MemberAccess(string Assembly, string Type, string Name, string Kind);
