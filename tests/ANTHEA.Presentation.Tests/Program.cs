using System.Text;
using Anthea.Presentation.Tests;

// Presentation without WPF (proposal F5, section 7.1). Argument: output folder (esito.txt), as the other suites of the fast stage.
try
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ANTHEA.sln"))) directory = directory.Parent;
    string root = directory?.FullName ?? throw new DirectoryNotFoundException("ANTHEA.sln non trovato.");
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
    if (sectionTraces || sectionFast)
    {
        checks += ConcreteSectionSheetChecks.Run(root, output, full: sectionTraces);
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
    string message = $"PASS {checks} controlli di Presentation senza WPF";
    File.WriteAllText(Path.Combine(output, "esito.txt"), message, new UTF8Encoding(false));
    Console.WriteLine(message);
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
