using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Finding = F4DomainRules.Finding;

/// <summary>
/// F5.1 rules P1–P6 and F5.3 rule P7 (docs/refactoring/f5-proposta-architettura.md, section 7.4; decision Q16 of 16/10/2026).
/// Like F4.2 they are syntax and metadata candidates, not a proof of semantics. Rules on ANTHEA.Presentation and on the
/// new views pass vacuously, with an explicit note, while the project or the folders do not exist yet.
/// </summary>
internal static class F5PresentationRules
{
    internal const string Presentation = "ANTHEA.Presentation";
    /// <summary>P1: projects without WPF. P4 and P5 apply to the same projects (section 2.1: they adopt the Presentation rules).</summary>
    internal static readonly string[] WpfFreeProjects = [Presentation, "ANTHEA.ModelViewer.Presentation", "ANTHEA.DesignTools.Presentation"];
    /// <summary>New views (section 2.1): Views/&lt;modulo&gt; and the F5.13 shell. Historical views stay under src/ANTHEA.Desktop/Wpf.</summary>
    internal static readonly string[] NewViewFolders = ["src/ANTHEA.Desktop/Views/", "src/ANTHEA.Desktop/Shell/"];
    const string BaselineFile = "tests/ANTHEA.Architecture.Tests/f5-static-baseline.json";
    internal const string AnalyzerPackage = "Microsoft.VisualStudio.Threading.Analyzers";
    const string AnalyzerConfig = "build/analyzers/ANTHEA.Presentation.globalconfig";
    /// <summary>Q16: analyzer rules that must stay errors on Presentation (the reasons are in the global config).</summary>
    static readonly string[] AnalyzerErrors = ["VSTHRD100", "VSTHRD101", "VSTHRD002", "VSTHRD110"];
    static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview, preprocessorSymbols: ["NET", "NET8_0", "NET8_0_OR_GREATER", "RELEASE"]);
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    static readonly HashSet<string> MutableCollections = ["List", "Dictionary", "HashSet", "SortedSet", "SortedDictionary", "SortedList",
        "Queue", "Stack", "LinkedList", "PriorityQueue", "ConcurrentDictionary", "ConcurrentBag", "ConcurrentQueue", "ConcurrentStack",
        "BlockingCollection", "ObservableCollection", "Collection", "KeyedCollection", "ArrayList", "Hashtable", "BitArray",
        "NameValueCollection", "StringCollection", "StringDictionary", "ConditionalWeakTable", "JsonObject", "JsonArray",
        "IList", "ICollection", "IDictionary", "ISet"];
    static readonly HashSet<string> CalculationNames = ["IModuleService", "ModuleServices", "CalculationService", "ConcreteAnalysis",
        "RecalculationScheduler", "ScheduledCalculation", "SheetCalculation", "CalculationLimiter"];
    static readonly string[] CalculationSessionSuffixes = ["AnalysisSession", "CalculationSession", "InspectionSession", "PropertySession", "DesignSession"];
    /// <summary>P7: properties that take a brush or a colour; in the new views their value is a theme token, Transparent or null.</summary>
    static readonly HashSet<string> BrushProperties = ["Background", "Foreground", "BorderBrush", "Fill", "Stroke", "Color", "CaretBrush", "SelectionBrush",
        "SelectionTextBrush", "OpacityMask", "RowBackground", "AlternatingRowBackground", "HorizontalGridLinesBrush", "VerticalGridLinesBrush"];
    /// <summary>P7: colour members of the historic palette (Appearance.cs) and of Ui.cs; Appearance.Document (light scope) is allowed.</summary>
    static readonly HashSet<string> PaletteMembers = ["Colour", "Background", "Foreground", "Outline", "Selection", "Swatch", "Series", "Dim",
        "Paper", "Surface", "Ink", "Accent", "Selected", "Calculated"];
    static readonly HashSet<string> CodeBrushes = ["SolidColorBrush", "LinearGradientBrush", "RadialGradientBrush", "Color"];
    static readonly Regex HexColour = new(@"(?<![\w#&])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})\b|^#(?:[0-9A-Fa-f]{3,4})$", RegexOptions.CultureInvariant);
    static readonly Regex HistoricResource = new(@"^(?:Appearance\.(?:background|foreground|border|selection|calculated)\.[0-9A-Fa-f]{6}|Appearance(?:Paper|Surface|Ink|Border|Selection))$",
        RegexOptions.CultureInvariant);

    internal sealed record Violation(string Rule, string File, string Text);
    internal sealed record RuleStatus(string Rule, string State, string Note);
    internal sealed record AssemblyMetadata(string[] Assemblies, string[] TypeScopes, MemberAccess[] Members);
    /// <summary>Inputs of an evaluation. Metadata is null for a fixture tree (sources and project files only).</summary>
    internal sealed record Inputs(string Root, Finding[] F4Baseline, Finding[] StaticBaseline,
        Func<string, string?>? AssemblyPath, Func<string, AssemblyMetadata>? Metadata, Func<string, bool> IsDesktop, string[] ProductAssemblies);
    internal sealed record Outcome(List<Violation> Violations, List<RuleStatus> Statuses, Finding[] StaticFindings);

    internal static int Run(string root, string output, Func<string, AssemblyMetadata> metadata, Func<string, bool> isDesktop)
    {
        int checks = SelfTest(output, metadata, isDesktop);
        // F5.14: in the product the folder of the new views must exist; P6 and P7 must not become empty in silence after a move.
        Require(Directory.Exists(Path.Combine(root, NewViewFolders[0])), "Cartella delle viste nuove assente: " + NewViewFolders[0]);
        var staticBaseline = JsonSerializer.Deserialize<F4DomainRules.Baseline>(File.ReadAllText(Path.Combine(root, BaselineFile)))
            ?? throw new InvalidOperationException("Baseline P3 assente.");
        Require(staticBaseline.Schema == 1 && staticBaseline.SourceCommit.Length == 40 && staticBaseline.Note.Length > 20, "Provenienza baseline P3 non valida.");
        Require(staticBaseline.Findings.Select(Key).Distinct().Count() == staticBaseline.Findings.Length, "Baseline P3 con duplicati.");
        var f4Baseline = JsonSerializer.Deserialize<F4DomainRules.Baseline>(File.ReadAllText(Path.Combine(root, "tests/ANTHEA.Architecture.Tests/f4-domain-baseline.json")))!;
        string desktopRelease = Path.Combine(root, "src/ANTHEA.Desktop/bin/Release/net8.0-windows");
        var products = new[] { AppContext.BaseDirectory, desktopRelease }.Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "ANTHEA*.dll"))
            .Where(f => !Path.GetFileName(f).Contains(".Tests", StringComparison.Ordinal) && !Path.GetFileName(f).Contains("Testing", StringComparison.Ordinal))
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
        Require(products.Any(f => Path.GetFileName(f) == "ANTHEA.dll"), "P4: ANTHEA.dll Release assente, metadati del prodotto non verificabili.");
        var outcome = Evaluate(new Inputs(root, f4Baseline.Findings, staticBaseline.Findings,
            name => Path.Combine(AppContext.BaseDirectory, name + ".dll"), metadata, isDesktop, products));
        var (analyzerProblems, analyzerStatus) = Analyzer(root);
        outcome.Violations.AddRange(analyzerProblems.Select(p => new Violation("Q16", "Directory.Build.targets", p)));
        outcome.Statuses.Add(analyzerStatus);
        File.WriteAllText(Path.Combine(output, "f5-static-findings.json"), JsonSerializer.Serialize(new F4DomainRules.Baseline(1, "candidate",
            "P3 syntax candidates (mutable static state) in production code. Proposal for review, never a blanket regeneration.", outcome.StaticFindings), JsonOptions));
        File.WriteAllText(Path.Combine(output, "f5-rules.json"), JsonSerializer.Serialize(new { outcome.Statuses, outcome.Violations,
            StaticFindings = outcome.StaticFindings.Sum(f => f.Count), DetectorChecks = checks }, JsonOptions));
        foreach (var status in outcome.Statuses) Console.WriteLine($"F5 {status.Rule}: {status.State}. {status.Note}");
        Require(outcome.Violations.Count == 0, "Regole F5 violate: " + string.Join("; ", outcome.Violations.Take(12).Select(v => $"{v.Rule} {v.File}: {v.Text}")));
        Console.WriteLine($"F5: P1–P7 rispettate; {outcome.StaticFindings.Sum(f => f.Count)} stati statici mutabili censiti; {checks} prove dei rilevatori");
        return checks + outcome.Statuses.Count;
    }

    internal static Outcome Evaluate(Inputs inputs)
    {
        string root = inputs.Root;
        var violations = new List<Violation>();
        var statuses = new List<RuleStatus>();
        void Fail(string rule, string file, string text) => violations.Add(new(rule, file, text));
        string Relative(string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
        string ProjectDirectory(string name) => Path.Combine(root, "src", name);
        bool presentation = File.Exists(Path.Combine(ProjectDirectory(Presentation), Presentation + ".csproj"));
        const string Missing = "ANTHEA.Presentation: progetto non ancora presente";
        var wpfFree = WpfFreeProjects.Where(p => File.Exists(Path.Combine(ProjectDirectory(p), p + ".csproj"))).ToArray();
        var production = Directory.EnumerateDirectories(root, "X.*").Concat(Directory.Exists(Path.Combine(root, "src")) ? Directory.EnumerateDirectories(Path.Combine(root, "src")) : [])
            .SelectMany(SourceFiles).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();

        // P1 — Presentation without WPF: project file, sources and built metadata.
        foreach (string project in WpfFreeProjects.Where(p => p != Presentation && !wpfFree.Contains(p)))
            Fail("P1", "src/" + project, "progetto senza WPF atteso ma assente");
        foreach (string project in wpfFree)
        {
            string directory = ProjectDirectory(project);
            foreach (string problem in ProjectProblems(XDocument.Load(Path.Combine(directory, project + ".csproj")), directory))
                Fail("P1", "src/" + project, problem);
            foreach (string file in SourceFiles(directory))
                foreach (string use in WindowsNamespaceUses(Relative(file), File.ReadAllText(file))) Fail("P1", Relative(file), use);
            if (inputs.AssemblyPath?.Invoke(project) is string assembly)
            {
                if (!File.Exists(assembly)) { Fail("P1", "src/" + project, "assembly non compilato: " + assembly); continue; }
                foreach (string reference in inputs.Metadata!(assembly).Assemblies.Where(inputs.IsDesktop)) Fail("P1", "src/" + project, "riferimento desktop nei metadati: " + reference);
            }
        }
        statuses.Add(new("P1", "attiva", (presentation ? "" : Missing + "; ") + "controllati " + string.Join(", ", wpfFree)
            + (inputs.Metadata is null ? " (solo progetti e sorgenti)" : " (progetti, sorgenti e metadati)")));

        // P2 — Presentation without calculation: TypeRef/AssemblyRef towards ANTHEA.Calculations and GPC = 0; no F4 entries.
        if (presentation)
        {
            string directory = ProjectDirectory(Presentation);
            foreach (string problem in CalculationProjectProblems(XDocument.Load(Path.Combine(directory, Presentation + ".csproj"))))
                Fail("P2", "src/" + Presentation, problem);
            if (inputs.AssemblyPath?.Invoke(Presentation) is string assembly && File.Exists(assembly))
            {
                var metadata = inputs.Metadata!(assembly);
                foreach (string reference in metadata.Assemblies.Concat(metadata.TypeScopes).Where(IsCalculation).Distinct())
                    Fail("P2", "src/" + Presentation, "dipendenza diretta dal calcolo nei metadati: " + reference);
            }
        }
        foreach (var finding in inputs.F4Baseline.Where(f => IsPresentationFile(f.File) || IsNewView(f.File)))
            Fail("P2", finding.File, "voce della baseline F4 per Presentation o per una vista nuova: " + finding.Rule + " " + finding.Owner);
        statuses.Add(new("P2", presentation ? "attiva" : "vacua", presentation
            ? "metadati e progetto di ANTHEA.Presentation; R1–R7 di F4.2 senza voci di baseline per Presentation e viste nuove"
            : Missing + "; attivo solo il divieto di voci F4 per Presentation e viste nuove"));

        // P3 — mutable static state: point baseline on the whole production code, only downwards; zero in Presentation.
        var observed = production.SelectMany(f => StaticState(Relative(f), File.ReadAllText(f))).ToArray();
        foreach (var finding in observed.Where(f => IsPresentationFile(f.File)))
            Fail("P3", finding.File, "stato statico mutabile in Presentation: " + finding.Example);
        foreach (var finding in inputs.StaticBaseline.Where(f => IsPresentationFile(f.File)))
            Fail("P3", finding.File, "voce della baseline P3 per Presentation: " + finding.Example);
        foreach (var finding in F4DomainRules.Additions(inputs.StaticBaseline, observed).Where(f => !IsPresentationFile(f.File)))
            Fail("P3", finding.File, "nuovo stato statico mutabile: " + finding.Owner + " (" + finding.Example + ")");
        var remaining = observed.ToDictionary(Key, f => f.Count, StringComparer.Ordinal);
        foreach (var finding in inputs.StaticBaseline.Where(f => remaining.GetValueOrDefault(Key(f)) < f.Count))
            Fail("P3", finding.File, "stato statico rimosso: ridurre la baseline P3 nello stesso commit (" + finding.Owner + ")");
        statuses.Add(new("P3", "attiva", $"{observed.Sum(f => f.Count)} occorrenze censite in {production.Length} file"
            + (presentation ? "; zero in ANTHEA.Presentation" : "; " + Missing)));

        // P4 — static messaging and services: Messenger.Default and Ioc.Default nowhere; Application.Current and Dispatcher not in ViewModels.
        foreach (string file in production)
        {
            string relative = Relative(file);
            bool wholeFile = wpfFree.Any(p => relative.StartsWith("src/" + p + "/", StringComparison.Ordinal));
            foreach (string use in StaticServiceUses(relative, File.ReadAllText(file), wholeFile)) Fail("P4", relative, use);
        }
        foreach (string assembly in inputs.ProductAssemblies)
            foreach (var member in inputs.Metadata!(assembly).Members.Where(IsStaticMessenger))
                Fail("P4", Path.GetFileName(assembly), "metadati: " + member.Type + "::" + member.Name);
        statuses.Add(new("P4", "attiva", "Messenger.Default/Ioc.Default in tutto il prodotto"
            + (inputs.ProductAssemblies.Length > 0 ? $" (sorgenti e {inputs.ProductAssemblies.Length} assembly)" : " (sorgenti)")
            + "; Application.Current/Dispatcher nei ViewModel: " + string.Join(", ", wpfFree) + " e classi *ViewModel" + (presentation ? "" : "; " + Missing)));

        // P5 — zero async void in Presentation (the Desktop counters are the limits.json check, only downwards).
        foreach (string project in wpfFree)
            foreach (string file in SourceFiles(ProjectDirectory(project)))
                foreach (string use in AsyncVoid(Relative(file), File.ReadAllText(file))) Fail("P5", Relative(file), use);
        statuses.Add(new("P5", "attiva", "zero async void in " + string.Join(", ", wpfFree) + (presentation ? "" : " (" + Missing + ")")
            + "; Desktop: limits.json; lambda async verso Action: VSTHRD101 sull'analyzer di Presentation"));

        // P6 — new views do not call IModuleService, calculation sessions or Task.Run.
        var viewFolders = NewViewFolders.Where(f => Directory.Exists(Path.Combine(root, f))).ToArray();
        foreach (string folder in viewFolders)
            foreach (string file in SourceFiles(Path.Combine(root, folder)))
                foreach (string use in CalculationUses(Relative(file), File.ReadAllText(file))) Fail("P6", Relative(file), use);
        statuses.Add(new("P6", viewFolders.Length > 0 ? "attiva" : "vacua", viewFolders.Length > 0 ? "controllate " + string.Join(", ", viewFolders)
            : "cartelle delle viste nuove (" + string.Join(", ", NewViewFolders) + ") non ancora presenti"));

        // P7 — theme tokens (F5.3): in the new views no hex colour, Brushes.*, Colors.*, Color.From*, brush created in code, palette of
        // Appearance or historic resource, in C# and XAML. Colours come only from {DynamicResource Token.*}; technical drawings use the
        // Drawing.* tokens (ThemeTokens.Brush). No exemption.
        foreach (string folder in viewFolders)
        {
            foreach (string file in SourceFiles(Path.Combine(root, folder)))
                foreach (string use in ColourUses(Relative(file), File.ReadAllText(file))) Fail("P7", Relative(file), use);
            foreach (string file in XamlFiles(Path.Combine(root, folder)))
                foreach (string use in XamlColourUses(Relative(file), File.ReadAllText(file))) Fail("P7", Relative(file), use);
        }
        statuses.Add(new("P7", viewFolders.Length > 0 ? "attiva" : "vacua", viewFolders.Length > 0
            ? "controllate " + string.Join(", ", viewFolders) + " (C# e XAML): colori solo da {DynamicResource Token.*}, disegni con i token Drawing.*"
            : "cartelle delle viste nuove (" + string.Join(", ", NewViewFolders) + ") non ancora presenti"));
        return new(violations, statuses, observed);
    }

    // ---------------------------------------------------------------- analyzer configuration (Q16)

    static (List<string> Problems, RuleStatus Status) Analyzer(string root)
    {
        var targets = XDocument.Load(Path.Combine(root, "Directory.Build.targets"));
        string config = Path.Combine(root, AnalyzerConfig);
        var problems = File.Exists(config)
            ? AnalyzerConfigProblems(targets, File.ReadAllText(config), File.ReadAllText(Path.Combine(root, "docs/refactoring/decisioni.md"))).ToList()
            : ["configurazione delle gravità assente: " + AnalyzerConfig];
        string version = targets.Descendants("PackageReference").FirstOrDefault(e => e.Attribute("Include")?.Value == AnalyzerPackage)?.Attribute("Version")?.Value ?? "?";
        // Only the central block: a project file that adds the package itself would bypass the "only Presentation" choice.
        var projectFiles = Directory.EnumerateDirectories(root, "X.*").Concat(new[] { "src", "tests", "tools" }.Select(d => Path.Combine(root, d)).Where(Directory.Exists))
            .SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)
                .Where(f => !Path.GetRelativePath(d, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or "node_modules")))
            .Where(f => Path.GetExtension(f) is ".csproj" or ".props" or ".targets").Append(Path.Combine(root, "Directory.Build.props"));
        foreach (string file in projectFiles.Where(f => File.ReadAllText(f).Contains(AnalyzerPackage, StringComparison.OrdinalIgnoreCase)))
            problems.Add("pacchetto dichiarato fuori dalla configurazione centrale: " + Path.GetRelativePath(root, file).Replace('\\', '/'));
        bool presentation = File.Exists(Path.Combine(root, "src", Presentation, Presentation + ".csproj"));
        string assets = Path.Combine(root, "src", Presentation, "obj", "project.assets.json");
        string note = "configurazione verificata; ";
        if (!presentation) note += "ANTHEA.Presentation: progetto non ancora presente";
        else if (!File.Exists(assets)) note += "ripristino di ANTHEA.Presentation non ancora eseguito";
        else if (!File.ReadAllText(assets).Contains("\"" + AnalyzerPackage + "/" + version + "\"", StringComparison.Ordinal)) problems.Add("pacchetto non ripristinato in ANTHEA.Presentation");
        else note += "pacchetto ripristinato in ANTHEA.Presentation";
        return (problems, new("Q16", presentation ? "attiva" : "vacua",
            $"{AnalyzerPackage} {version} solo su ANTHEA.Presentation, errori {string.Join("/", AnalyzerErrors)}, VSTHRD111 disattivata; {note}"));
    }

    internal static IEnumerable<string> AnalyzerConfigProblems(XDocument targets, string globalConfig, string decisions)
    {
        var references = targets.Descendants("PackageReference").Where(e => e.Attribute("Include")?.Value == AnalyzerPackage).ToArray();
        if (references.Length != 1) { yield return "PackageReference dell'analyzer assente o duplicato"; yield break; }
        var reference = references[0];
        string condition = (reference.Parent?.Attribute("Condition")?.Value ?? "").Replace(" ", "", StringComparison.Ordinal);
        if (condition != "'$(MSBuildProjectName)'=='" + Presentation + "'") yield return "analyzer non limitato ad ANTHEA.Presentation: " + condition;
        string version = reference.Attribute("Version")?.Value ?? "";
        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$")) yield return "versione non esatta: " + version;
        if (reference.Attribute("PrivateAssets")?.Value != "all") yield return "PrivateAssets=all mancante: il pacchetto passerebbe ai progetti che riferiscono Presentation";
        if (!reference.Parent!.Elements("EditorConfigFiles").Any(e => (e.Attribute("Include")?.Value ?? "").Replace('\\', '/').EndsWith(AnalyzerConfig, StringComparison.Ordinal)))
            yield return "configurazione delle gravità non collegata al compilatore";
        var severities = globalConfig.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => l.Split('=', 2)).Where(p => p.Length == 2)
            .GroupBy(p => p[0].Trim(), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last()[1].Trim(), StringComparer.Ordinal);
        if (severities.GetValueOrDefault("is_global") != "true") yield return "is_global = true mancante";
        foreach (string rule in AnalyzerErrors)
            if (severities.GetValueOrDefault("dotnet_diagnostic." + rule + ".severity") != "error") yield return rule + " deve essere un errore";
        if (severities.GetValueOrDefault("dotnet_diagnostic.VSTHRD111.severity") != "none") yield return "VSTHRD111 (ConfigureAwait) deve restare disattivata";
        if (!decisions.Contains("| " + AnalyzerPackage + " " + version + " |", StringComparison.Ordinal)) yield return "versione assente dalla tabella delle dipendenze di decisioni.md: " + version;
    }

    // ---------------------------------------------------------------- project files

    internal static IEnumerable<string> ProjectProblems(XDocument project, string directory)
    {
        if (project.Descendants("TargetFrameworks").Any()) yield return "più target: ammesso solo net8.0";
        var target = project.Descendants("TargetFramework").Select(e => e.Value.Trim()).ToArray();
        if (target.Length != 1 || target[0] != "net8.0") yield return "target non portabile: " + string.Join(";", target);
        foreach (string property in new[] { "UseWPF", "UseWindowsForms" })
            if (project.Descendants(property).Any(e => e.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))) yield return property + " vietato";
        foreach (var reference in project.Descendants("FrameworkReference").Select(e => e.Attribute("Include")?.Value ?? ""))
            if (reference.Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase)) yield return "FrameworkReference desktop: " + reference;
        foreach (var package in project.Descendants("PackageReference").Select(e => e.Attribute("Include")?.Value ?? ""))
            if (package.Contains("Wpf", StringComparison.OrdinalIgnoreCase) || package.Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase)) yield return "pacchetto WPF: " + package;
        foreach (var include in project.Descendants("ProjectReference").Select(e => e.Attribute("Include")?.Value ?? ""))
        {
            string path = Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar)));
            if (!File.Exists(path)) continue;
            var referenced = XDocument.Load(path);
            if (referenced.Descendants("UseWPF").Any(e => e.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                || referenced.Descendants().Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks").Any(e => e.Value.Contains("-windows", StringComparison.OrdinalIgnoreCase)))
                yield return "riferimento a un progetto WPF: " + Path.GetFileName(path);
        }
    }

    internal static IEnumerable<string> CalculationProjectProblems(XDocument project)
    {
        foreach (var include in project.Descendants("ProjectReference").Select(e => e.Attribute("Include")?.Value ?? ""))
            if (include.Contains("X.Calculations", StringComparison.OrdinalIgnoreCase) || include.Contains("ANTHEA.Legacy", StringComparison.OrdinalIgnoreCase))
                yield return "riferimento di progetto al calcolo: " + include;
        foreach (var libraries in project.Descendants("GpcLibraries").Select(e => e.Value.Trim()))
            if (libraries.Length > 0 && libraries != "none") yield return "librerie GPC dirette: " + libraries;
        foreach (var reference in project.Descendants("Reference").Select(e => e.Attribute("Include")?.Value ?? ""))
            if (IsCalculation(reference)) yield return "riferimento diretto al calcolo: " + reference;
    }

    static bool IsCalculation(string assembly) => assembly == "ANTHEA.Calculations" || assembly.StartsWith("GPC", StringComparison.Ordinal) || assembly == "DelaunayMesh";
    static bool IsStaticMessenger(MemberAccess member) => member.Name == "get_Default"
        && (member.Type is "CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger" or "CommunityToolkit.Mvvm.Messaging.StrongReferenceMessenger" or "CommunityToolkit.Mvvm.DependencyInjection.Ioc");
    static bool IsPresentationFile(string file) => file.StartsWith("src/" + Presentation + "/", StringComparison.Ordinal);
    static bool IsNewView(string file) => NewViewFolders.Any(f => file.StartsWith(f, StringComparison.Ordinal));

    // ---------------------------------------------------------------- source scanners

    static CompilationUnitSyntax Parse(string file, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, ParseOptions);
        Require(!tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error), "Sintassi non analizzabile: " + file);
        return tree.GetCompilationUnitRoot();
    }
    static string Normalized(SyntaxNode node) => string.Concat(node.DescendantTokens().Select(t => t.Text)).Replace("global::", "", StringComparison.Ordinal);
    static string Excerpt(SyntaxNode node) { string text = string.Join(" ", node.DescendantTokens().Select(t => t.Text)); return text.Length > 180 ? text[..180] : text; }
    static bool HasPrefix(string name, string prefix) => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal);
    static string Rightmost(SyntaxNode? node) => node switch
    {
        QualifiedNameSyntax q => q.Right.Identifier.ValueText, AliasQualifiedNameSyntax a => a.Name.Identifier.ValueText,
        SimpleNameSyntax s => s.Identifier.ValueText, MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
        NullableTypeSyntax n => Rightmost(n.ElementType), _ => ""
    };

    /// <summary>P1: System.Windows (and Office) in using directives, qualified names and member accesses; never in strings or comments.</summary>
    internal static IEnumerable<string> WindowsNamespaceUses(string file, string source)
    {
        var root = Parse(file, source);
        static bool Desktop(string name) => HasPrefix(name, "System.Windows") || HasPrefix(name, "Microsoft.Office");
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            if (Desktop(Normalized(directive.NamespaceOrType))) yield return "using " + Excerpt(directive.NamespaceOrType);
        foreach (var node in root.DescendantNodes().Where(n => n is QualifiedNameSyntax or MemberAccessExpressionSyntax))
        {
            if (node.Parent is QualifiedNameSyntax or MemberAccessExpressionSyntax || node.Ancestors().Any(a => a is UsingDirectiveSyntax)) continue;
            if (Desktop(Normalized(node))) yield return "nome qualificato " + Excerpt(node);
        }
    }

    /// <summary>P3: static fields not readonly, static properties with a setter, static events, static readonly mutable collections or arrays.</summary>
    internal static Finding[] StaticState(string file, string source)
    {
        var root = Parse(file, source);
        var list = new List<Finding>();
        void Add(string rule, SyntaxNode member, string name, string declaration)
        {
            string owner = string.Join(".", member.AncestorsAndSelf().OfType<BaseTypeDeclarationSyntax>().Reverse().Select(t => t.Identifier.ValueText).Append(name));
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner + "|" + declaration)));
            list.Add(new(file, rule, owner, fingerprint, 1, declaration.Length > 180 ? declaration[..180] : declaration));
        }
        static bool Static(SyntaxTokenList modifiers) => modifiers.Any(SyntaxKind.StaticKeyword) && !modifiers.Any(SyntaxKind.AbstractKeyword) && !modifiers.Any(SyntaxKind.VirtualKeyword);
        static string Declaration(SyntaxTokenList modifiers, TypeSyntax type, string name, string suffix = "") =>
            string.Join(" ", modifiers.Select(m => m.Text).Concat(type.DescendantTokens().Select(t => t.Text).Where(t => t.Length > 0)).Append(name)) + suffix;
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(f => Static(f.Modifiers)))
        {
            string? rule = !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) ? "P3-static-field" : MutableCollection(field.Declaration.Type) ? "P3-static-collection" : null;
            if (rule is null) continue;
            foreach (var variable in field.Declaration.Variables)
                Add(rule, field, variable.Identifier.ValueText, Declaration(field.Modifiers, field.Declaration.Type, variable.Identifier.ValueText));
        }
        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Where(p => Static(p.Modifiers) && p.AccessorList is not null))
        {
            var accessors = property.AccessorList!.Accessors;
            string shape = " { " + string.Join(" ", accessors.Select(a => string.Join(" ", a.Modifiers.Select(m => m.Text).Append(a.Keyword.Text)))) + " }";
            if (accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) || a.IsKind(SyntaxKind.InitAccessorDeclaration)))
                Add("P3-static-setter", property, property.Identifier.ValueText, Declaration(property.Modifiers, property.Type, property.Identifier.ValueText, shape));
            else if (accessors.All(a => a.Body is null && a.ExpressionBody is null) && MutableCollection(property.Type))
                Add("P3-static-collection", property, property.Identifier.ValueText, Declaration(property.Modifiers, property.Type, property.Identifier.ValueText, shape));
        }
        foreach (var field in root.DescendantNodes().OfType<EventFieldDeclarationSyntax>().Where(e => Static(e.Modifiers)))
            foreach (var variable in field.Declaration.Variables)
                Add("P3-static-event", field, variable.Identifier.ValueText, "event " + Declaration(field.Modifiers, field.Declaration.Type, variable.Identifier.ValueText));
        foreach (var declaration in root.DescendantNodes().OfType<EventDeclarationSyntax>().Where(e => Static(e.Modifiers)))
            Add("P3-static-event", declaration, declaration.Identifier.ValueText, "event " + Declaration(declaration.Modifiers, declaration.Type, declaration.Identifier.ValueText));
        return list.GroupBy(Key).Select(g => g.First() with { Count = g.Count() }).OrderBy(Key, StringComparer.Ordinal).ToArray();
    }

    static bool MutableCollection(TypeSyntax type) => type is ArrayTypeSyntax || type is NullableTypeSyntax { ElementType: ArrayTypeSyntax }
        || MutableCollections.Contains(Rightmost(type));

    /// <summary>P4: CommunityToolkit static messengers and Ioc anywhere; Application.Current and Dispatcher* inside ViewModels.</summary>
    internal static IEnumerable<string> StaticServiceUses(string file, string source, bool wholeFileIsViewModel)
    {
        var root = Parse(file, source);
        var aliases = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u => u.Alias is not null)
            .ToDictionary(u => u.Alias!.Name.Identifier.ValueText, u => Rightmost(u.NamespaceOrType), StringComparer.Ordinal);
        static bool Global(string type) => type.EndsWith("Messenger", StringComparison.Ordinal) || type == "Ioc";
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            if (directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) && Global(Rightmost(directive.NamespaceOrType))) yield return "using static " + Excerpt(directive.NamespaceOrType);
        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Where(m => m.Name.Identifier.ValueText == "Default"))
        {
            string receiver = Rightmost(access.Expression);
            if (Global(receiver) || aliases.TryGetValue(receiver, out var target) && Global(target)) yield return "servizio statico " + Excerpt(access);
        }
        bool InViewModel(SyntaxNode node) => wholeFileIsViewModel
            || node.Ancestors().OfType<TypeDeclarationSyntax>().Any(t => t.Identifier.ValueText.EndsWith("ViewModel", StringComparison.Ordinal));
        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            if (access.Name.Identifier.ValueText == "Current" && Rightmost(access.Expression) == "Application" && InViewModel(access)) yield return "Application.Current nel ViewModel: " + Excerpt(access);
        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
            if (name.Identifier.ValueText.StartsWith("Dispatcher", StringComparison.Ordinal) && InViewModel(name)) yield return "Dispatcher nel ViewModel: " + Excerpt(name.Parent ?? name);
    }

    /// <summary>P5: async void methods and local functions.</summary>
    internal static IEnumerable<string> AsyncVoid(string file, string source)
    {
        var root = Parse(file, source);
        static bool Void(TypeSyntax type) => type is PredefinedTypeSyntax p && p.Keyword.IsKind(SyntaxKind.VoidKeyword);
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            if (method.Modifiers.Any(SyntaxKind.AsyncKeyword) && Void(method.ReturnType)) yield return "async void " + method.Identifier.ValueText;
        foreach (var local in root.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            if (local.Modifiers.Any(SyntaxKind.AsyncKeyword) && Void(local.ReturnType)) yield return "async void " + local.Identifier.ValueText;
    }

    /// <summary>P6: calculation services and sessions, Task.Run and the other ways to start background work.</summary>
    internal static IEnumerable<string> CalculationUses(string file, string source)
    {
        var root = Parse(file, source);
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            if (directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) && Rightmost(directive.NamespaceOrType) is "Task" or "Parallel" or "ThreadPool")
                yield return "using static " + Excerpt(directive.NamespaceOrType);
        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            string text = name.Identifier.ValueText;
            if (CalculationNames.Contains(text) || CalculationSessionSuffixes.Any(s => text.EndsWith(s, StringComparison.Ordinal)))
                yield return "servizio o sessione di calcolo nella vista: " + Excerpt(name.Parent is QualifiedNameSyntax or MemberAccessExpressionSyntax ? name.Parent : name);
        }
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax member) continue;
            string method = member.Name.Identifier.ValueText, receiver = Rightmost(member.Expression);
            if (receiver == "Task" && method == "Run" || receiver == "Factory" && method == "StartNew"
                || receiver == "ThreadPool" && method.EndsWith("QueueUserWorkItem", StringComparison.Ordinal) || receiver == "Parallel")
                yield return "lavoro in background nella vista: " + Excerpt(call.Expression);
        }
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            if (Rightmost(creation.Type) == "Thread") yield return "thread nella vista: " + Excerpt(creation);
    }

    /// <summary>P7 in C#: hex colours in strings, Brushes/Colors members, Color.From*, brushes and colours created in code, colour
    /// converters, the palette members of Appearance and Ui, and the keys of the historic resources. Comments are never analysed.</summary>
    internal static IEnumerable<string> ColourUses(string file, string source)
    {
        var root = Parse(file, source);
        foreach (var token in root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.StringLiteralToken) || t.IsKind(SyntaxKind.InterpolatedStringTextToken)
            || t.IsKind(SyntaxKind.SingleLineRawStringLiteralToken) || t.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)))
        {
            string text = token.ValueText;
            if (HexColour.IsMatch(text)) yield return "colore esadecimale: " + Shorten(token.Text);
            else if (HistoricResource.IsMatch(text)) yield return "risorsa storica invece di un token: " + text;
        }
        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            string receiver = Rightmost(access.Expression), member = access.Name.Identifier.ValueText;
            if (receiver is "Brushes" or "Colors") yield return "colore predefinito: " + Excerpt(access);
            else if (receiver == "Appearance" && PaletteMembers.Contains(member)) yield return "tavolozza storica invece di un token: " + Excerpt(access);
            else if (receiver == "Ui" && member is "Brush" or "Navy" or "Blue" or "Bg" or "Muted") yield return "colore di Ui.cs invece di un token: " + Excerpt(access);
            else if (receiver == "Color" && member.StartsWith("From", StringComparison.Ordinal)) yield return "colore costruito: " + Excerpt(access);
        }
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            if (directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) && Rightmost(directive.NamespaceOrType) is "Brushes" or "Colors")
                yield return "using static " + Excerpt(directive.NamespaceOrType);
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            if (CodeBrushes.Contains(Rightmost(creation.Type))) yield return "pennello o colore creato nel codice: " + Excerpt(creation);
        foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
            if (name.Identifier.ValueText is "ColorConverter" or "BrushConverter") yield return "convertitore di colori: " + Excerpt(name.Parent ?? name);
    }

    /// <summary>P7 in XAML: hex colours in any value, literal colours on brush properties (also in Setter), brush and colour elements,
    /// x:Static of Brushes/Colors and references to the historic resources. Transparent and {x:Null} are not colours.</summary>
    internal static IEnumerable<string> XamlColourUses(string file, string source)
    {
        XDocument document;
        try { document = XDocument.Parse(source); }
        catch (System.Xml.XmlException error) { throw new InvalidOperationException("XAML non analizzabile: " + file + ": " + error.Message); }
        static string Local(string name) => name[(name.LastIndexOf('.') + 1)..];
        static bool Literal(string value) => !value.TrimStart().StartsWith('{') && value.Trim() != "Transparent";
        foreach (var element in document.Descendants())
        {
            string elementName = element.Name.LocalName;
            if (CodeBrushes.Contains(elementName) || elementName is "GradientStop" or "ImageBrush" or "DrawingBrush" or "VisualBrush")
                yield return $"pennello o colore nel XAML: <{elementName}>";
            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
            {
                string name = attribute.Name.LocalName, value = attribute.Value;
                if (HexColour.IsMatch(value)) yield return $"colore esadecimale: {elementName}.{name}=\"{Shorten(value)}\"";
                else if (Regex.IsMatch(value, @"\{\s*x:Static\s+(?:\w+:)?(?:Brushes|Colors)\.", RegexOptions.CultureInvariant)) yield return $"colore predefinito: {elementName}.{name}=\"{Shorten(value)}\"";
                else if (Regex.Match(value, @"^\{\s*(?:DynamicResource|StaticResource)\s+(?:ResourceKey\s*=\s*)?([^\s{}]+)\s*\}$") is { Success: true } reference && HistoricResource.IsMatch(reference.Groups[1].Value))
                    yield return $"risorsa storica invece di un token: {elementName}.{name}=\"{value}\"";
                else if (BrushProperties.Contains(Local(name)) && Literal(value)) yield return $"colore letterale: {elementName}.{name}=\"{Shorten(value)}\"";
            }
            if (elementName == "Setter" && element.Attribute("Property")?.Value is { } property && BrushProperties.Contains(Local(property))
                && element.Attribute("Value")?.Value is { } setterValue && Literal(setterValue) && !HexColour.IsMatch(setterValue))
                yield return $"colore letterale: Setter {property}=\"{Shorten(setterValue)}\"";
        }
    }

    static string Shorten(string text) => text.Length > 80 ? text[..80] : text;
    static string Key(Finding value) => value.File + "|" + value.Rule + "|" + value.Owner + "|" + value.Fingerprint;
    static IEnumerable<string> XamlFiles(string directory) => !Directory.Exists(directory) ? [] : Directory.EnumerateFiles(directory, "*.xaml", SearchOption.AllDirectories)
        .Where(f => !Path.GetRelativePath(directory, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"));
    static IEnumerable<string> SourceFiles(string directory) => !Directory.Exists(directory) ? [] : Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
        .Where(f => !Path.GetRelativePath(directory, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"));
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    // ---------------------------------------------------------------- detector proofs

    static int SelfTest(string output, Func<string, AssemblyMetadata> metadata, Func<string, bool> isDesktop)
    {
        int checks = 0;
        void Check(bool condition, string message) { Require(condition, "Prova del rilevatore F5: " + message); checks++; }
        const string vm = "src/ANTHEA.Presentation/Probe.cs";
        static string Class(string body, string prefix = "") => prefix + "\nnamespace Anthea.Presentation;\npublic class Probe { " + body + "\n}";

        // P1 sources.
        Check(WindowsNamespaceUses(vm, Class("", "using System.Windows;")).Any(), "P1 using System.Windows non rilevato");
        Check(WindowsNamespaceUses(vm, Class("", "using Media = System.Windows.Media;")).Any(), "P1 alias WPF non rilevato");
        Check(WindowsNamespaceUses(vm, Class("", "using static System.Windows.Visibility;")).Any(), "P1 using static WPF non rilevato");
        Check(WindowsNamespaceUses(vm, Class("System.Windows.Media.Color colour;")).Any(), "P1 tipo qualificato non rilevato");
        Check(WindowsNamespaceUses(vm, Class("object M() => global::System.Windows.Application.Current;")).Any(), "P1 global:: non rilevato");
        Check(WindowsNamespaceUses(vm, Class("object M() => System.Windows.Visibility.Collapsed;")).Any(), "P1 accesso qualificato non rilevato");
        Check(WindowsNamespaceUses(vm, Class("", "using Microsoft.Office.Interop.Word;")).Any(), "P1 Office non rilevato");
        Check(!WindowsNamespaceUses(vm, Class("// System.Windows.Media\n string S = \"System.Windows\"; System.WindowsRuntimeX? x;", "using System.Text;")).Any(), "P1 commenti, stringhe o nomi simili");
        // P1 project files and metadata.
        string probeDirectory = Path.Combine(output, "f5-rilevatori");
        if (Directory.Exists(probeDirectory)) Directory.Delete(probeDirectory, true);
        Directory.CreateDirectory(probeDirectory);
        static XDocument Project(string properties, string items = "") => XDocument.Parse($"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{properties}</PropertyGroup><ItemGroup>{items}</ItemGroup></Project>");
        Check(!ProjectProblems(Project("<TargetFramework>net8.0</TargetFramework>", "<PackageReference Include=\"CommunityToolkit.Mvvm\" Version=\"8.3.2\" />"), probeDirectory).Any(), "P1 progetto pulito segnalato");
        Check(ProjectProblems(Project("<TargetFramework>net8.0-windows</TargetFramework>"), probeDirectory).Any(), "P1 target Windows non rilevato");
        Check(ProjectProblems(Project("<TargetFrameworks>net8.0;net8.0-windows</TargetFrameworks>"), probeDirectory).Any(), "P1 più target non rilevati");
        Check(ProjectProblems(Project("<TargetFramework>net8.0</TargetFramework><UseWPF>true</UseWPF>"), probeDirectory).Any(), "P1 UseWPF non rilevato");
        Check(ProjectProblems(Project("<TargetFramework>net8.0</TargetFramework>", "<FrameworkReference Include=\"Microsoft.WindowsDesktop.App.WPF\" />"), probeDirectory).Any(), "P1 FrameworkReference non rilevato");
        Check(ProjectProblems(Project("<TargetFramework>net8.0</TargetFramework>", "<PackageReference Include=\"HelixToolkit.Wpf.SharpDX\" Version=\"3.1.2\" />"), probeDirectory).Any(), "P1 pacchetto WPF non rilevato");
        File.WriteAllText(Path.Combine(probeDirectory, "Wpf.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0-windows</TargetFramework><UseWPF>true</UseWPF></PropertyGroup></Project>");
        Check(ProjectProblems(Project("<TargetFramework>net8.0</TargetFramework>", "<ProjectReference Include=\"Wpf.csproj\" />"), probeDirectory).Any(), "P1 riferimento a progetto WPF non rilevato");
        string corlibOnly = Emit(probeDirectory, "Probe.Clean", "public class C { public object M() => new object(); }");
        string wpfProbe = Emit(probeDirectory, "Probe.Wpf", "public class C { public System.Windows.Point P; }", ("WindowsBase", "namespace System.Windows { public struct Point { } }"));
        Check(metadata(wpfProbe).Assemblies.Any(isDesktop), "P1 metadati WPF non rilevati");
        Check(!metadata(corlibOnly).Assemblies.Any(isDesktop), "P1 metadati puliti segnalati");
        // P2 project files, metadata and F4 entries.
        Check(CalculationProjectProblems(Project("<GpcLibraries>GPCModel</GpcLibraries>")).Any(), "P2 GpcLibraries non rilevato");
        Check(!CalculationProjectProblems(Project("<GpcLibraries>none</GpcLibraries>")).Any(), "P2 GpcLibraries none segnalato");
        Check(CalculationProjectProblems(Project("", "<ProjectReference Include=\"..\\..\\X.Calculations\\X.Calculations.csproj\" />")).Any(), "P2 riferimento al motore non rilevato");
        Check(!CalculationProjectProblems(Project("", "<ProjectReference Include=\"../ANTHEA.Application/ANTHEA.Application.csproj\" />")).Any(), "P2 riferimento ad Application segnalato");
        string gpcProbe = Emit(probeDirectory, "Probe.Gpc", "public class C { public GPC.Model.Beam B; public Anthea.Calculations.Sezione S; }",
            ("GPCModel", "namespace GPC.Model { public class Beam { } }"), ("ANTHEA.Calculations", "namespace Anthea.Calculations { public class Sezione { } }"));
        var gpc = metadata(gpcProbe);
        Check(gpc.Assemblies.Count(IsCalculation) == 2 && gpc.TypeScopes.Count(IsCalculation) == 2, "P2 metadati di calcolo non rilevati");
        Check(!metadata(corlibOnly).Assemblies.Any(IsCalculation), "P2 metadati puliti segnalati");
        Check(F4DomainRules.Scan(vm, Class("double M(double x) => x * .26;")).Any(f => f.Rule.StartsWith("R2", StringComparison.Ordinal)), "P2 R2 non applicata a Presentation");
        Check(F4DomainRules.Scan(vm, Class("", "using System.Windows.Media;")).Any(f => f.Rule.StartsWith("R7", StringComparison.Ordinal)), "P2 R7 non applicata a Presentation");
        // P3 static state.
        bool Has(string rule, string body) => StaticState(vm, Class(body)).Any(f => f.Rule == rule);
        Check(Has("P3-static-field", "static int counter;"), "P3 campo statico non rilevato");
        Check(Has("P3-static-field", "[System.ThreadStatic] private static volatile object? cache;"), "P3 campo volatile non rilevato");
        Check(Has("P3-static-setter", "public static int Count { get; set; }"), "P3 setter statico non rilevato");
        Check(Has("P3-static-setter", "public static string Name { get; private set; } = \"\";"), "P3 setter privato non rilevato");
        Check(Has("P3-static-event", "public static event System.EventHandler? Changed;"), "P3 evento statico non rilevato");
        Check(Has("P3-static-event", "public static event System.EventHandler Changed { add { } remove { } }"), "P3 evento con accessori non rilevato");
        Check(Has("P3-static-collection", "static readonly System.Collections.Generic.Dictionary<string, int> Map = new();"), "P3 dizionario non rilevato");
        Check(Has("P3-static-collection", "static readonly int[] Values = [1];"), "P3 array non rilevato");
        Check(Has("P3-static-collection", "static readonly IList<int> Values = new List<int>();"), "P3 interfaccia mutabile non rilevata");
        Check(Has("P3-static-collection", "public static List<int> Values { get; } = new();"), "P3 proprietà collezione non rilevata");
        Check(StaticState(vm, Class("const int A = 1; static readonly int B = 2; static readonly object Gate = new(); int instance; int[] values = [];"
            + " static readonly IReadOnlyList<int> C = [1]; static readonly System.Collections.Immutable.ImmutableArray<int> D = [];"
            + " static readonly System.Collections.Frozen.FrozenDictionary<string, int> E = null!; public static List<int> Fresh => new();"
            + " public static int Get { get { return 1; } } event System.EventHandler? Instance;")).Length == 0, "P3 dichiarazioni immutabili o di istanza segnalate");
        const string catalogue = "static readonly Dictionary<string, int> Map = new() { [\"a\"] = 1 };";
        var before = StaticState(vm.Replace("ANTHEA.Presentation", "ANTHEA.Application"), Class(catalogue));
        Check(F4DomainRules.Additions(before, StaticState(vm.Replace("ANTHEA.Presentation", "ANTHEA.Application"), Class(catalogue.Replace("1 }", "2 }")))).Length == 0, "P3 contenuto del catalogo nell'impronta");
        Check(F4DomainRules.Additions(before, StaticState(vm.Replace("ANTHEA.Presentation", "ANTHEA.Application"), Class(catalogue + " static int x;"))).Length > 0, "P3 aggiunta non rilevata");
        Check(F4DomainRules.Additions(before, StaticState("src/ANTHEA.Application/Other.cs", Class(catalogue))).Length > 0, "P3 file nuovo mascherato dalla baseline");
        Check(StaticState(vm, Class(catalogue.Replace("Dictionary<string, int>", "IReadOnlyDictionary<string, int>"))).Length == 0, "P3 catalogo reso immutabile ancora segnalato");
        // P4 static messaging and services.
        bool Uses(string body, string prefix = "", bool whole = true, string file = vm) => StaticServiceUses(file, Class(body, prefix), whole).Any();
        Check(Uses("void M() => WeakReferenceMessenger.Default.Send(new object());"), "P4 WeakReferenceMessenger.Default non rilevato");
        Check(Uses("object M() => CommunityToolkit.Mvvm.Messaging.StrongReferenceMessenger.Default;"), "P4 StrongReferenceMessenger qualificato non rilevato");
        Check(Uses("object M() => Bus.Default;", "using Bus = CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger;"), "P4 alias del messenger non rilevato");
        Check(Uses("", "using static CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger;"), "P4 using static del messenger non rilevato");
        Check(Uses("object? M() => Ioc.Default.GetService(typeof(object));", "", false, "src/ANTHEA.Desktop/App.cs"), "P4 Ioc.Default fuori dai ViewModel non rilevato");
        Check(Uses("object M() => Application.Current;"), "P4 Application.Current non rilevato");
        Check(Uses("object M() => System.Windows.Application.Current.MainWindow;"), "P4 Application.Current qualificato non rilevato");
        Check(Uses("void M(dynamic view) => view.Dispatcher.Invoke(() => { });"), "P4 Dispatcher non rilevato");
        Check(Uses("object M() => DispatcherTimer.Interval;"), "P4 DispatcherTimer non rilevato");
        Check(StaticServiceUses("src/ANTHEA.Desktop/Wpf/Probe.cs", "class ConcreteViewModel { object M() => Dispatcher.CurrentDispatcher; }", false).Any(), "P4 ViewModel nel Desktop non rilevato");
        Check(!StaticServiceUses("src/ANTHEA.Desktop/Wpf/Probe.cs", "class ConcreteView { object M() => Dispatcher.CurrentDispatcher; object A() => Application.Current; }", false).Any(), "P4 vista storica segnalata");
        Check(!Uses("object M(IMessenger messenger) => messenger; object O() => Options.Default; object S() => System.Threading.SynchronizationContext.Current!;"
            + " sealed class VerificationProgressDispatcher { } // Messenger.Default", "", true), "P4 istanze, altri Default o nomi simili segnalati");
        string messengerProbe = Emit(probeDirectory, "Probe.Messenger", "public class C { public object M() => CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default; public object I() => CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default; }",
            ("CommunityToolkit.Mvvm", "namespace CommunityToolkit.Mvvm.Messaging { public sealed class WeakReferenceMessenger { public static WeakReferenceMessenger Default { get; } = new(); } }"
                + " namespace CommunityToolkit.Mvvm.DependencyInjection { public sealed class Ioc { public static Ioc Default { get; } = new(); } }"));
        Check(metadata(messengerProbe).Members.Count(IsStaticMessenger) == 2, "P4 metadati di Messenger/Ioc.Default non rilevati");
        Check(!metadata(corlibOnly).Members.Any(IsStaticMessenger), "P4 metadati puliti segnalati");
        // P5 async void.
        Check(AsyncVoid(vm, Class("async void Load() { await System.Threading.Tasks.Task.Yield(); }")).Any(), "P5 metodo async void non rilevato");
        Check(AsyncVoid(vm, Class("void M() { async void Local() { await System.Threading.Tasks.Task.Yield(); } Local(); }")).Any(), "P5 funzione locale async void non rilevata");
        Check(!AsyncVoid(vm, Class("async System.Threading.Tasks.Task Load() { await System.Threading.Tasks.Task.Yield(); } void Sync() { } string S = \"async void\";")).Any(), "P5 metodi corretti segnalati");
        // P6 views without calculation.
        const string view = "src/ANTHEA.Desktop/Views/Probe/ProbeView.xaml.cs";
        bool Calculates(string body, string prefix = "") => CalculationUses(view, Class(body, prefix)).Any();
        Check(Calculates("Anthea.Application.Modules.IModuleService? service;"), "P6 IModuleService non rilevato");
        Check(Calculates("object M() => ModuleServices.Get(\"palo\");"), "P6 ModuleServices non rilevato");
        Check(Calculates("object M(dynamic d) => new ConcreteAnalysisSession(d);"), "P6 sessione di calcolo non rilevata");
        Check(Calculates("object M(dynamic d) => new ModuleCalculationSession(d);"), "P6 sessione del modulo non rilevata");
        Check(Calculates("void M() => System.Threading.Tasks.Task.Run(() => { });"), "P6 Task.Run non rilevato");
        Check(Calculates("void M() => Task.Factory.StartNew(() => { });"), "P6 Task.Factory.StartNew non rilevato");
        Check(Calculates("void M() => ThreadPool.QueueUserWorkItem(_ => { });"), "P6 ThreadPool non rilevato");
        Check(Calculates("void M() => Parallel.For(0, 2, _ => { });"), "P6 Parallel non rilevato");
        Check(Calculates("void M() => new Thread(() => { }).Start();"), "P6 Thread non rilevato");
        Check(Calculates("", "using static System.Threading.Tasks.Task;"), "P6 using static Task non rilevato");
        Check(!Calculates("System.Threading.Tasks.Task M(dynamic vm) => System.Threading.Tasks.Task.FromResult(vm.CalculateCommand); // Task.Run IModuleService\n string S = \"Task.Run\"; object SessionState;"), "P6 vista pulita segnalata");
        // P7 theme tokens in the new views, C#.
        bool Colours(string body, string prefix = "") => ColourUses(view, Class(body, prefix)).Any();
        Check(Colours("string C = \"#0B2A4A\";"), "P7 esadecimale non rilevato");
        Check(Colours("string C = \"#800B2A4A\";"), "P7 esadecimale con alfa non rilevato");
        Check(Colours("string C = \"#FFF\";"), "P7 esadecimale breve non rilevato");
        Check(Colours("string X = \"<Border Background='#F3F5F8'/>\";"), "P7 esadecimale in un frammento XAML non rilevato");
        Check(Colours("string C(int n) => $\"#{n}\" + $\"x #E2EFFC\";"), "P7 esadecimale interpolato non rilevato");
        Check(Colours("object B => Brushes.White;"), "P7 Brushes non rilevato");
        Check(Colours("object B => System.Windows.Media.Brushes.Firebrick;"), "P7 Brushes qualificato non rilevato");
        Check(Colours("object B => Colors.LightGray;"), "P7 Colors non rilevato");
        Check(Colours("", "using static System.Windows.Media.Brushes;"), "P7 using static Brushes non rilevato");
        Check(Colours("object C => Color.FromRgb(1, 2, 3);"), "P7 Color.FromRgb non rilevato");
        Check(Colours("object C => System.Windows.Media.Color.FromArgb(9, 1, 2, 3);"), "P7 Color.FromArgb non rilevato");
        Check(Colours("object B(dynamic c) => new SolidColorBrush(c);"), "P7 new SolidColorBrush non rilevato");
        Check(Colours("object B => new System.Windows.Media.LinearGradientBrush();"), "P7 pennello sfumato non rilevato");
        Check(Colours("object C(string s) => ColorConverter.ConvertFromString(s);"), "P7 ColorConverter non rilevato");
        Check(Colours("object C(string s) => new BrushConverter().ConvertFromString(s);"), "P7 BrushConverter non rilevato");
        Check(Colours("object B => Appearance.Background(\"x\");"), "P7 Appearance.Background non rilevato");
        Check(Colours("object B => Appearance.Paper;"), "P7 Appearance.Paper non rilevato");
        Check(Colours("object B => X.Desktop.Appearance.Colour(default, \"swatch\");"), "P7 Appearance.Colour qualificato non rilevato");
        Check(Colours("object B => Ui.Muted;"), "P7 Ui.Muted non rilevato");
        Check(Colours("object B(dynamic e) => e.FindResource(\"AppearancePaper\");"), "P7 risorsa storica nel codice non rilevata");
        Check(Colours("void M(dynamic e) => e.SetResourceReference(null, \"Appearance.background.F3F5F8\");"), "P7 chiave storica nel codice non rilevata");
        Check(!Colours("object B => ThemeTokens.Brush(\"Drawing.Ink\"); void M(dynamic e) => e.SetResourceReference(null, \"Token.Paper\");"
            + " System.IDisposable D(object r) => Appearance.Document(r); object K => SystemColors.WindowBrushKey; string T = \"Rev #12 · issue #ABCDEFG\";"
            + " // Brushes.Red \"#FF0000\" Color.FromRgb\n object myBrushes; object P => DocumentAppearance.Light(null); string S = \"AppearanceDrawingLabel\";"), "P7 codice con i token segnalato");
        // P7 theme tokens in the new views, XAML.
        const string xamlView = "src/ANTHEA.Desktop/Views/Probe/ProbeView.xaml";
        static string Xaml(string body) => "<UserControl xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" + body + "</UserControl>";
        bool XamlColours(string body) => XamlColourUses(xamlView, Xaml(body)).Any();
        Check(XamlColours("<Border Background=\"#F3F5F8\"/>"), "P7 XAML esadecimale non rilevato");
        Check(XamlColours("<Border Background=\"#FFF\"/>"), "P7 XAML esadecimale breve non rilevato");
        Check(XamlColours("<TextBlock Foreground=\"Red\"/>"), "P7 XAML colore con nome non rilevato");
        Check(XamlColours("<Border BorderBrush=\"White\"/>"), "P7 XAML bordo letterale non rilevato");
        Check(XamlColours("<Border><Border.Style><Style TargetType=\"Border\"><Setter Property=\"Background\" Value=\"White\"/></Style></Border.Style></Border>"), "P7 XAML Setter letterale non rilevato");
        Check(XamlColours("<Border><Border.Background><SolidColorBrush Color=\"{DynamicResource X}\"/></Border.Background></Border>"), "P7 XAML SolidColorBrush non rilevato");
        Check(XamlColours("<Border><Border.Background><LinearGradientBrush><GradientStop Offset=\"0\"/></LinearGradientBrush></Border.Background></Border>"), "P7 XAML sfumatura non rilevata");
        Check(XamlColours("<Rectangle Fill=\"{x:Static Brushes.Red}\"/>"), "P7 XAML x:Static Brushes non rilevato");
        Check(XamlColours("<Border Background=\"{DynamicResource Appearance.background.F3F5F8}\"/>"), "P7 XAML risorsa storica non rilevata");
        Check(XamlColours("<TextBlock Foreground=\"{StaticResource AppearanceInk}\"/>"), "P7 XAML risorsa storica statica non rilevata");
        Check(XamlColours("<TextBlock Tag=\"#0B2A4A\"/>"), "P7 XAML esadecimale in altra proprietà non rilevato");
        Check(!XamlColours("<Grid Background=\"Transparent\"><Border Background=\"{DynamicResource Token.Paper}\" BorderBrush=\"{x:Null}\"/>"
            + "<TextBlock Text=\"Rev #12\" FontSize=\"{DynamicResource Font.Body}\" Foreground=\"{DynamicResource Token.Ink}\" Style=\"{DynamicResource ProjectButton}\"/>"
            + "<!-- Background=\"#FF0000\" --><Border><Border.Style><Style TargetType=\"Border\"><Setter Property=\"Background\" Value=\"{DynamicResource Token.Hover}\"/>"
            + "<Setter Property=\"Padding\" Value=\"4\"/></Style></Border.Style></Border></Grid>"), "P7 XAML con i token segnalato");
        bool xamlError = false; try { XamlColourUses(xamlView, "<UserControl>").ToList(); } catch (InvalidOperationException) { xamlError = true; }
        Check(xamlError, "P7 XAML non analizzabile ignorato");
        // Q16 analyzer configuration.
        const string targets = "<Project><ItemGroup Condition=\"'$(MSBuildProjectName)' == 'ANTHEA.Presentation'\"><PackageReference Include=\"Microsoft.VisualStudio.Threading.Analyzers\" Version=\"18.7.23\" PrivateAssets=\"all\" />"
            + "<EditorConfigFiles Include=\"$(MSBuildThisFileDirectory)build\\analyzers\\ANTHEA.Presentation.globalconfig\" /></ItemGroup></Project>";
        const string config = "# gravità\r\nis_global = true\r\ndotnet_diagnostic.VSTHRD100.severity = error\r\ndotnet_diagnostic.VSTHRD101.severity = error\r\n"
            + "dotnet_diagnostic.VSTHRD002.severity = error\r\ndotnet_diagnostic.VSTHRD110.severity = error\r\ndotnet_diagnostic.VSTHRD111.severity = none\r\n";
        const string table = "| Microsoft.VisualStudio.Threading.Analyzers 18.7.23 | MIT | analisi | F5 |";
        bool Configured(string t, string c = config, string d = table) => !AnalyzerConfigProblems(XDocument.Parse(t), c, d).Any();
        Check(Configured(targets), "Q16 configurazione corretta segnalata");
        Check(!Configured(targets.Replace(" Condition=\"'$(MSBuildProjectName)' == 'ANTHEA.Presentation'\"", "")), "Q16 analyzer su tutti i progetti non rilevato");
        Check(!Configured(targets.Replace("'ANTHEA.Presentation'", "'ANTHEA.Desktop'")), "Q16 analyzer su un altro progetto non rilevato");
        Check(!Configured(targets.Replace("18.7.23", "18.*"), config, table.Replace("18.7.23", "18.*")), "Q16 versione flottante non rilevata");
        Check(!Configured(targets.Replace(" PrivateAssets=\"all\"", "")), "Q16 PrivateAssets mancante non rilevato");
        Check(!Configured(targets.Replace("ANTHEA.Presentation.globalconfig", "altro.globalconfig")), "Q16 configurazione scollegata non rilevata");
        Check(!Configured(targets, config.Replace("VSTHRD100.severity = error", "VSTHRD100.severity = warning")), "Q16 VSTHRD100 declassata non rilevata");
        Check(!Configured(targets, config + "dotnet_diagnostic.VSTHRD002.severity = suggestion\r\n"), "Q16 VSTHRD002 ridefinita non rilevata");
        Check(!Configured(targets, config.Replace("VSTHRD111.severity = none", "VSTHRD111.severity = warning")), "Q16 ConfigureAwait imposto non rilevato");
        Check(!Configured(targets, config.Replace("is_global = true", "")), "Q16 is_global mancante non rilevato");
        Check(!Configured(targets, config, table.Replace("18.7.23", "17.14.15")), "Q16 versione diversa dalla tabella non rilevata");
        bool syntaxError = false; try { StaticState(vm, "class {"); } catch (InvalidOperationException) { syntaxError = true; }
        Check(syntaxError, "sorgente non analizzabile ignorato");

        // Fixture trees under the test output, never in the product: a tree that breaks every rule and a tree without Presentation.
        string bad = Path.Combine(probeDirectory, "albero-violazioni");
        WriteTree(bad, includePresentation: true);
        var badOutcome = Evaluate(new Inputs(bad, [new("src/ANTHEA.Presentation/Bad.cs", "R2-numeric-literal", "Bad.M", new string('0', 64), 1, ".26")], [], null, null, isDesktop, []));
        foreach (string rule in new[] { "P1", "P2", "P3", "P4", "P5", "P6", "P7" })
            Check(badOutcome.Violations.Any(v => v.Rule == rule), rule + " non rilevata sull'albero di prova");
        Check(badOutcome.Statuses.All(s => s.State == "attiva"), "regole non attive con Presentation e viste presenti");
        string clean = Path.Combine(probeDirectory, "albero-senza-presentation");
        WriteTree(clean, includePresentation: false);
        var cleanOutcome = Evaluate(new Inputs(clean, [], [], null, null, isDesktop, []));
        Check(cleanOutcome.Violations.Count == 0, "albero pulito segnalato: " + string.Join("; ", cleanOutcome.Violations.Select(v => v.Rule + " " + v.Text)));
        Check(cleanOutcome.Statuses.Single(s => s.Rule == "P2").State == "vacua" && cleanOutcome.Statuses.Single(s => s.Rule == "P6").State == "vacua"
            && cleanOutcome.Statuses.Single(s => s.Rule == "P7").State == "vacua", "regole vacue non dichiarate");
        Check(cleanOutcome.Statuses.Where(s => s.Rule is "P1" or "P2" or "P3" or "P4" or "P5").All(s => s.Note.Contains("progetto non ancora presente", StringComparison.Ordinal)), "nota «progetto non ancora presente» mancante");
        Directory.Delete(Path.Combine(clean, "src", "ANTHEA.DesignTools.Presentation"), true);
        Check(Evaluate(new Inputs(clean, [], [], null, null, isDesktop, [])).Violations.Any(v => v.Rule == "P1"), "progetto senza WPF esistente scomparso senza segnalazione");
        return checks;
    }

    static void WriteTree(string root, bool includePresentation)
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        void Write(string relative, string text) { string path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
        const string clean = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>";
        Write("src/ANTHEA.ModelViewer.Presentation/ANTHEA.ModelViewer.Presentation.csproj", clean);
        Write("src/ANTHEA.ModelViewer.Presentation/Clean.cs", "namespace Anthea.ModelViewer; public sealed class CleanViewModel { public System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.CompletedTask; }");
        Write("src/ANTHEA.DesignTools.Presentation/ANTHEA.DesignTools.Presentation.csproj", clean);
        Write("src/ANTHEA.Desktop/Wpf/Legacy.cs", "namespace Anthea; class LegacyView { object A() => System.Windows.Application.Current; }");
        if (!includePresentation) return;
        Write("src/ANTHEA.Presentation/ANTHEA.Presentation.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0-windows</TargetFramework><UseWPF>true</UseWPF>"
            + "<GpcLibraries>GPCModel</GpcLibraries></PropertyGroup><ItemGroup><ProjectReference Include=\"../../X.Calculations/X.Calculations.csproj\" /></ItemGroup></Project>");
        Write("src/ANTHEA.Presentation/Bad.cs", "using System.Windows;\nnamespace Anthea.Presentation;\npublic class BadViewModel\n{\n    public static int Counter;\n"
            + "    public async void Load() { await System.Threading.Tasks.Task.Yield(); }\n    public object M() => CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default;\n}");
        Write("src/ANTHEA.Desktop/Views/Pile/PileView.xaml.cs", "namespace Anthea.Views;\npublic partial class PileView { void M(Anthea.Application.Modules.IModuleService s) => System.Threading.Tasks.Task.Run(() => { }); }");
        Write("src/ANTHEA.Desktop/Views/Pile/PileView.xaml", "<UserControl xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Border Background=\"#F3F5F8\"/></UserControl>");
    }

    /// <summary>Compiles a probe assembly in memory against System.Private.CoreLib and fake libraries with the given names.</summary>
    static string Emit(string directory, string name, string source, params (string Name, string Source)[] libraries)
    {
        var corlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        var references = new List<MetadataReference> { corlib };
        foreach (var library in libraries)
        {
            var compilation = CSharpCompilation.Create(library.Name, [CSharpSyntaxTree.ParseText(library.Source)], [corlib], options);
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Require(result.Success, "Libreria di prova non compilata: " + string.Join(" ", result.Diagnostics));
            references.Add(MetadataReference.CreateFromImage(stream.ToArray()));
        }
        var probe = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references, options);
        string path = Path.Combine(directory, name + ".dll");
        using var image = new MemoryStream();
        var emitted = probe.Emit(image);
        Require(emitted.Success, "Assembly di prova non compilato: " + string.Join(" ", emitted.Diagnostics));
        File.WriteAllBytes(path, image.ToArray());
        return path;
    }
}
