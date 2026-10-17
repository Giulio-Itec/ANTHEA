using System.Text;
using Anthea.Presentation.Tests;

// Presentation without WPF (proposal F5, section 7.1). Argument: output folder (esito.txt), as the other suites of the fast stage.
try
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ANTHEA.sln"))) directory = directory.Parent;
    string root = directory?.FullName ?? throw new DirectoryNotFoundException("ANTHEA.sln non trovato.");
    string output = args.Length == 0 ? Path.Combine(root, "supporto/artefatti/refactoring/f5/presentation") : Path.GetFullPath(args[0]);
    Directory.CreateDirectory(output);
    int checks = 0;
    checks += ViewModelBaseChecks.Run();
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
    string message = $"PASS {checks} controlli di Presentation senza WPF";
    File.WriteAllText(Path.Combine(output, "esito.txt"), message, new UTF8Encoding(false));
    Console.WriteLine(message);
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
