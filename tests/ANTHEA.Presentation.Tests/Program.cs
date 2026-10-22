using System.Text;
using Anthea.Presentation.Tests;

// Presentation without WPF (proposal F5, section 7.1). Argument: output folder (esito.txt), as the other suites of the fast stage.
try
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ANTHEA.sln"))) directory = directory.Parent;
    string root = directory?.FullName ?? throw new DirectoryNotFoundException("ANTHEA.sln non trovato.");
    if(args.Contains("--spettri")){
        string target=args.FirstOrDefault(a=>a!="--spettri")??Path.Combine(root,"supporto/artefatti/spettri-ntc/presentation");Directory.CreateDirectory(target);
        int count=SpectraSheetChecks.Run(root,target);string spectraMessage=$"PASS {count} controlli Spettri";File.WriteAllText(Path.Combine(target,"esito.txt"),spectraMessage);Console.WriteLine(spectraMessage);return 0;
    }
    // F5.10: --sezione-tracce runs only the long traces of the RC section (decision C7, ui stage), --sezione-rapide only its checks of the fast
    // stage (to work on the module); the other arguments are positional.
    bool sectionTraces = args.Contains("--sezione-tracce"), sectionFast = args.Contains("--sezione-rapide"), embeddedWalls = args.Contains("--paratie"), versionAudit = args.Contains("--paratie-regression-audit");
    args = args.Where(a => a is not ("--sezione-tracce" or "--sezione-rapide" or "--paratie" or "--paratie-regression-audit")).ToArray();
    string output = args.Length == 0 ? Path.Combine(root, "supporto/artefatti/refactoring/f5/presentation") : Path.GetFullPath(args[0]);
    Directory.CreateDirectory(output);
    int checks = 0;
    if (versionAudit) { Console.WriteLine("PASS " + ShallowFoundationSheetChecks.AuditLibraryVersion(root, output) + " confronto versione fondazioni"); return 0; }
    if (embeddedWalls)
    {
        checks = EmbeddedWallChecks.Run(output);
        string result = $"PASS {checks} controlli paratie, applicazione e MVVM";
        File.WriteAllText(Path.Combine(output, "esito.txt"), result, new UTF8Encoding(false)); Console.WriteLine(result); return 0;
    }
    // F5.11: --sezione-mista runs only the checks of the composite bridge section, traces and F3 included (to work on the module).
    if (args.Contains("--sezione-mista"))
    {
        checks = CompositeSectionSheetChecks.Run(root, output);
        string composite = $"PASS {checks} controlli della sezione mista (F5.11) senza WPF";
        File.WriteAllText(Path.Combine(output, "esito.txt"), composite, new UTF8Encoding(false)); Console.WriteLine(composite); return 0;
    }
    // F5.12: --bridge-design runs only the checks of Bridge Design and of its price editor, traces and F3 included (to work on the module).
    if (args.Contains("--bridge-design"))
    {
        checks = BridgeDesignSheetChecks.Run(root, output) + BridgePricesChecks.Run() + BridgePriceAnalysesChecks.Run();
        string bridgeDesign = $"PASS {checks} controlli di Bridge Design (F5.12) senza WPF";
        File.WriteAllText(Path.Combine(output, "esito.txt"), bridgeDesign, new UTF8Encoding(false)); Console.WriteLine(bridgeDesign); return 0;
    }
    // F5.13: --shell runs only the checks of the sessions and ViewModels of the new shell (to work on the step).
    if (args.Contains("--shell"))
    {
        checks = ShellChecks.Run(output);
        string shell = $"PASS {checks} controlli della shell (F5.13) senza WPF";
        File.WriteAllText(Path.Combine(output, "esito.txt"), shell, new UTF8Encoding(false)); Console.WriteLine(shell); return 0;
    }
    // F5.13 (d): --paralleli runs only the exit test of the parallel calculations (proposal 4.6): the same sheets in series and in parallel.
    if (args.Contains("--paralleli"))
    {
        checks = ParallelChecks.Run(root, output);
        string parallel = $"PASS {checks} controlli dei calcoli in parallelo (F5.13 d) senza WPF";
        File.WriteAllText(Path.Combine(output, "esito.txt"), parallel, new UTF8Encoding(false)); Console.WriteLine(parallel); return 0;
    }
    if (sectionTraces || sectionFast)
    {
        checks += ConcreteSectionSheetChecks.Run(root, output, full: sectionTraces);
        // F5.10 S2 (decision C3): the editor of the actions moved from ANTHEA.Application with its check.
        if (!sectionTraces) checks += SectionActionsEditorChecks.Run();
        string traces = $"PASS {checks} controlli della sezione c.a. (F5.10" + (sectionTraces ? ", tracce lunghe" : "") + ") senza WPF";
        File.WriteAllText(Path.Combine(output, "esito.txt"), traces, new UTF8Encoding(false));
        Console.WriteLine(traces);
        return 0;
    }
    checks += ViewModelBaseChecks.Run();
    checks += EmbeddedWallChecks.Run(output);
    checks += await CommandGuardChecks.RunAsync();
    checks += await ServicesChecks.RunAsync();
    checks += SheetCalculationChecks.Run();
    checks += SynchronousSheetChecks.Run();
    checks += SheetPreviewChecks.Run();
    checks += HelpTopicsChecks.Run();
    checks += FieldSpecChecks.Run();
    checks += JsonRowChecks.Run();
    checks += FormViewModelChecks.Run();
    checks += SharedDataCoordinatorChecks.Run();
    checks += ProjectHistoryChecks.Run();
    // F5.13 (a, b): sessions and ViewModels of the new shell.
    checks += ShellChecks.Run(output);
    checks += ModuleHostChecks.Run();
    checks += PileGroupSheetChecks.Run(root, output);
    checks += MaterialSheetChecks.Run(root, output);
    checks += VerticalPileSheetChecks.Run(root, output);
    checks += ShallowFoundationSheetChecks.Run(root, output);
    checks += HorizontalPileSheetChecks.Run(root, output);
    checks += StratigraphySheetChecks.Run(root, output);
    checks += RetainingWallSheetChecks.Run(root, output);
    checks += ElasticPileSheetChecks.Run(root, output);
    checks += ConcreteSectionSheetChecks.Run(root, output, full: false);
    checks += SectionActionsEditorChecks.Run();
    checks += CompositeSectionSheetChecks.Run(root, output);
    // F5.12 (decision Q17 a): the price editor of Bridge Design moved from ANTHEA.Application with its checks.
    checks += BridgePricesChecks.Run();
    checks += BridgePriceAnalysesChecks.Run();
    checks += BridgeDesignSheetChecks.Run(root, output);
    string message = $"PASS {checks} controlli di Presentation senza WPF";
    File.WriteAllText(Path.Combine(output, "esito.txt"), message, new UTF8Encoding(false));
    Console.WriteLine(message);
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
