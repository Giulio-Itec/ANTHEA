using System.Globalization;
using System.Text;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Tests;

// Presentation without WPF (proposal F5, section 7.1). Argument: output folder (esito.txt), as the other suites of the fast stage.
try
{
    // F5.15, V-a: the checks with the decimal separator «Virgola» on every thread, as the WPF checks (AppHarness): the frozen traces, shared
    // with the ui suites, do not depend on the separator of the machine.
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = NumberSeparators.Culture(DecimalSeparatorChoice.Comma, CultureInfo.CurrentCulture);
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ANTHEA.sln"))) directory = directory.Parent;
    string root = directory?.FullName ?? throw new DirectoryNotFoundException("ANTHEA.sln non trovato.");
    if (args.Contains("--dxf"))
    {
        string target = args.First(a => a != "--dxf"); Directory.CreateDirectory(target);
        int count = Group("ConcreteDxfChecks", () => ConcreteDxfChecks.Run(root, target));
        return Finish(target, count, $"PASS {count} controlli importazione DXF");
    }
    if (args.Contains("--sezione-composta-rapida"))
    {
        string target = args.First(a => a != "--sezione-composta-rapida"); Directory.CreateDirectory(target);
        int count = Group("QuickCompositeSectionChecks", () => QuickCompositeSectionChecks.Run(root, target));
        return Finish(target, count, $"PASS {count} controlli della sezione composta rapida");
    }
    if (args.Contains("--rheology"))
    {
        string target = args.First(a => a != "--rheology"); Directory.CreateDirectory(target);
        int count = Group("RheologySheetChecks", () => RheologySheetChecks.Run(root, target));
        return Finish(target, count, $"PASS {count} controlli foglio Reologia");
    }
    // 17/11/2026: --sezione-rapida runs only the checks of the quick RC section; --sezione-rapida-esempi <cartella> writes its examples.
    if (args.Contains("--sezione-rapida-esempi"))
    {
        string target = args.First(a => a != "--sezione-rapida-esempi");
        foreach (string path in QuickSectionSheetChecks.WriteExamples(target)) Console.WriteLine(path);
        return 0;
    }
    if (args.Contains("--sezione-rapida"))
    {
        string target = args.FirstOrDefault(a => a != "--sezione-rapida") ?? Path.Combine(root, "supporto/artefatti/sezione-rapida/presentation"); Directory.CreateDirectory(target);
        int count = Group("QuickSectionSheetChecks", () => QuickSectionSheetChecks.Run(root, target));
        return Finish(target, count, $"PASS {count} controlli della verifica rapida della sezione in c.a.");
    }
    if(args.Contains("--spettri")){
        string target=args.FirstOrDefault(a=>a!="--spettri")??Path.Combine(root,"supporto/artefatti/spettri-ntc/presentation");Directory.CreateDirectory(target);
        int count=Group("SpectraSheetChecks",()=>SpectraSheetChecks.Run(root,target));return Finish(target,count,$"PASS {count} controlli Spettri");
    }
    if(args.Contains("--climate")){
        string target=args.FirstOrDefault(a=>a!="--climate")??Path.Combine(root,"supporto/artefatti/azioni-climatiche/presentation");Directory.CreateDirectory(target);
        int count=Group("ClimateSheetChecks",()=>ClimateSheetChecks.Run(root,target));return Finish(target,count,$"PASS {count} controlli azioni climatiche");
    }
    // F5.10: --sezione-tracce runs only the long traces of the RC section (decision C7, ui stage), --sezione-rapide only its checks of the fast
    // stage (to work on the module); the other arguments are positional.
    if (args.Contains("--sezione-materiali"))
    {
        string target = args.First(a => a != "--sezione-materiali"); Directory.CreateDirectory(target);
        int count = Group("ConcreteSectionSheetChecks.RunBarMaterials", () => ConcreteSectionSheetChecks.RunBarMaterials(root, target));
        return Finish(target, count, $"PASS {count} controlli materiali barre e trefoli");
    }
    bool sectionTraces = args.Contains("--sezione-tracce"), sectionFast = args.Contains("--sezione-rapide"), embeddedWalls = args.Contains("--paratie"), versionAudit = args.Contains("--paratie-regression-audit"),
        stratigraphyOnly = args.Contains("--stratigrafia");
    args = args.Where(a => a is not ("--sezione-tracce" or "--sezione-rapide" or "--paratie" or "--paratie-regression-audit" or "--stratigrafia")).ToArray();
    string output = args.Length == 0 ? Path.Combine(root, "supporto/artefatti/refactoring/f5/presentation") : Path.GetFullPath(args[0]);
    Directory.CreateDirectory(output);
    int checks = 0;
    if (versionAudit)
    {
        int audit = Group("ShallowFoundationSheetChecks.AuditLibraryVersion", () => ShallowFoundationSheetChecks.AuditLibraryVersion(root, output));
        if (Expect.Failures.Count > 0) return Finish(output, audit, "");
        Console.WriteLine("PASS " + audit + " confronto versione fondazioni"); return 0;
    }
    if (embeddedWalls)
    {
        checks = Group("UnitNumberFormatChecks", () => UnitNumberFormatChecks.Run(output)) + Group("EmbeddedWallChecks", () => EmbeddedWallChecks.Run(output));
        return Finish(output, checks, $"PASS {checks} controlli paratie, applicazione e MVVM");
    }
    // F5.11: --sezione-mista runs only the checks of the composite bridge section, traces and F3 included (to work on the module).
    if (args.Contains("--sezione-mista"))
    {
        checks = Group("CompositeSectionSheetChecks", () => CompositeSectionSheetChecks.Run(root, output));
        return Finish(output, checks, $"PASS {checks} controlli della sezione mista (F5.11) senza WPF");
    }
    // F5.12: --bridge-design runs only the checks of Bridge Design and of its price editor, traces and F3 included (to work on the module).
    if (args.Contains("--bridge-design"))
    {
        checks = Group("BridgeDesignSheetChecks", () => BridgeDesignSheetChecks.Run(root, output)) + Group("BridgePricesChecks", () => BridgePricesChecks.Run())
            + Group("BridgePriceAnalysesChecks", () => BridgePriceAnalysesChecks.Run());
        return Finish(output, checks, $"PASS {checks} controlli di Bridge Design (F5.12) senza WPF");
    }
    // 4/11/2026 (docs/sviluppo/progetto-sistema.md, step 3): --banco-progetto measures the edits of a large project in the shell.
    if (args.Contains("--banco-progetto")) return ProjectEditBench.Run(output);
    // Rilievi del 16/11/2026 (R1, R5, R6, R8): --banco-modelli measures the series, «Libera», «Annulla» and the reopening on a real model.
    if (args.Contains("--banco-modelli")) return ModelMemoryBench.Run(output, args);
    // F5.13: --shell runs only the checks of the sessions and ViewModels of the new shell (to work on the step).
    if (args.Contains("--shell"))
    {
        checks = Group("ShellChecks", () => ShellChecks.Run(output));
        return Finish(output, checks, $"PASS {checks} controlli della shell (F5.13) senza WPF");
    }
    // 17/11/2026: --stratigrafia runs only the checks of the stratigraphy sheet, traces and F3 included (to work on the module).
    if (stratigraphyOnly)
    {
        checks = StratigraphySheetChecks.Run(root, output);
        string stratigraphy = $"PASS {checks} controlli della stratigrafia senza WPF";
        File.WriteAllText(Path.Combine(output, "esito.txt"), stratigraphy, new UTF8Encoding(false)); Console.WriteLine(stratigraphy); return 0;
    }
    // F5.13 (d): --paralleli runs only the exit test of the parallel calculations (proposal 4.6): the same sheets in series and in parallel.
    if (args.Contains("--paralleli"))
    {
        checks = Group("ParallelChecks", () => ParallelChecks.Run(root, output));
        return Finish(output, checks, $"PASS {checks} controlli dei calcoli in parallelo (F5.13 d) senza WPF");
    }
    if (sectionTraces || sectionFast)
    {
        checks += Group("ConcreteSectionSheetChecks", () => ConcreteSectionSheetChecks.Run(root, output, full: sectionTraces));
        // F5.10 S2 (decision C3): the editor of the actions moved from ANTHEA.Application with its check.
        if (!sectionTraces) checks += Group("SectionActionsEditorChecks", () => SectionActionsEditorChecks.Run());
        return Finish(output, checks, $"PASS {checks} controlli della sezione c.a. (F5.10" + (sectionTraces ? ", tracce lunghe" : "") + ") senza WPF");
    }
    checks += Group("ViewModelBaseChecks.Run", () => ViewModelBaseChecks.Run());
    checks += Group("NumberSeparatorChecks.Run", () => NumberSeparatorChecks.Run());
    checks += Group("UnitNumberFormatChecks.Run", () => UnitNumberFormatChecks.Run(output));
    checks += Group("ReportTemplateMenuChecks.Run", () => ReportTemplateMenuChecks.Run(output));
    checks += Group("EmbeddedWallChecks.Run", () => EmbeddedWallChecks.Run(output));
    checks += await GroupAsync("CommandGuardChecks.RunAsync", () => CommandGuardChecks.RunAsync());
    checks += await GroupAsync("ServicesChecks.RunAsync", () => ServicesChecks.RunAsync());
    checks += Group("SheetCalculationChecks.Run", () => SheetCalculationChecks.Run());
    checks += Group("SynchronousSheetChecks.Run", () => SynchronousSheetChecks.Run());
    checks += Group("SheetPreviewChecks.Run", () => SheetPreviewChecks.Run());
    checks += Group("HelpTopicsChecks.Run", () => HelpTopicsChecks.Run());
    checks += Group("FieldSpecChecks.Run", () => FieldSpecChecks.Run());
    checks += Group("JsonRowChecks.Run", () => JsonRowChecks.Run());
    checks += Group("FormViewModelChecks.Run", () => FormViewModelChecks.Run());
    checks += Group("SharedDataCoordinatorChecks.Run", () => SharedDataCoordinatorChecks.Run());
    // 16/11/2026 (note of step 6, §1.5 point 8): the layers of a survey with another number of layers are not shared by position.
    checks += Group("SoilSubdivisionChecks.Run", () => SoilSubdivisionChecks.Run());
    checks += Group("ProjectHistoryChecks.Run", () => ProjectHistoryChecks.Run());
    // F5.13 (a, b): sessions and ViewModels of the new shell.
    checks += Group("ShellChecks.Run", () => ShellChecks.Run(output));
    checks += Group("ModuleHostChecks.Run", () => ModuleHostChecks.Run());
    checks += Group("PileGroupSheetChecks.Run", () => PileGroupSheetChecks.Run(root, output));
    checks += Group("MaterialSheetChecks.Run", () => MaterialSheetChecks.Run(root, output));
    checks += Group("RheologySheetChecks.Run", () => RheologySheetChecks.Run(root, output));
    checks += Group("VerticalPileSheetChecks.Run", () => VerticalPileSheetChecks.Run(root, output));
    checks += Group("ShallowFoundationSheetChecks.Run", () => ShallowFoundationSheetChecks.Run(root, output));
    checks += Group("HorizontalPileSheetChecks.Run", () => HorizontalPileSheetChecks.Run(root, output));
    checks += Group("HorizontalPileBromsChecks.Run", () => HorizontalPileBromsChecks.Run(output));
    checks += Group("StratigraphySheetChecks.Run", () => StratigraphySheetChecks.Run(root, output));
    checks += Group("RetainingWallSheetChecks.Run", () => RetainingWallSheetChecks.Run(root, output));
    checks += Group("ElasticPileSheetChecks.Run", () => ElasticPileSheetChecks.Run(root, output));
    checks += Group("ConcreteSectionSheetChecks.Run", () => ConcreteSectionSheetChecks.Run(root, output, full: false));
    checks += Group("SectionActionsEditorChecks.Run", () => SectionActionsEditorChecks.Run());
    checks += Group("QuickCompositeSectionChecks.Run", () => QuickCompositeSectionChecks.Run(root, output));
    checks += Group("ConcreteDxfChecks.Run", () => ConcreteDxfChecks.Run(root, output));
    checks += Group("QuickSectionSheetChecks.Run", () => QuickSectionSheetChecks.Run(root, output));
    checks += Group("CompositeSectionSheetChecks.Run", () => CompositeSectionSheetChecks.Run(root, output));
    // F5.12 (decision Q17 a): the price editor of Bridge Design moved from ANTHEA.Application with its checks.
    checks += Group("BridgePricesChecks.Run", () => BridgePricesChecks.Run());
    checks += Group("BridgePriceAnalysesChecks.Run", () => BridgePriceAnalysesChecks.Run());
    checks += Group("BridgeDesignSheetChecks.Run", () => BridgeDesignSheetChecks.Run(root, output));
    return Finish(output, checks, $"PASS {checks} controlli di Presentation senza WPF");
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    foreach (string failure in Expect.Failures) Console.Error.WriteLine(failure);
    return 1;
}

// 18/11/2026 (approved): a group that throws is recorded and the next one runs; the run fails at the end with every failure.
static int Group(string name, Func<int> run)
{
    try { return run(); }
    catch (Exception error) { Expect.Fail(name + " interrotto: " + error); return 0; }
}

static async Task<int> GroupAsync(string name, Func<Task<int>> run)
{
    try { return await run(); }
    catch (Exception error) { Expect.Fail(name + " interrotto: " + error); return 0; }
}

// esito.txt is «PASS …» only without failures; otherwise «FAIL …» and the whole list, numbered, in fallimenti.txt and on the error stream.
static int Finish(string output, int checks, string passed)
{
    var failures = Expect.Failures;
    if (failures.Count == 0)
    {
        File.WriteAllText(Path.Combine(output, "esito.txt"), passed, new UTF8Encoding(false)); Console.WriteLine(passed); return 0;
    }
    string list = string.Join(Environment.NewLine, failures.Select((failure, i) => (i + 1) + ". " + failure));
    string message = $"FAIL {failures.Count} controlli non superati, {checks} superati (elenco in fallimenti.txt)";
    File.WriteAllText(Path.Combine(output, "fallimenti.txt"), list + Environment.NewLine, new UTF8Encoding(false));
    File.WriteAllText(Path.Combine(output, "esito.txt"), message, new UTF8Encoding(false));
    Console.Error.WriteLine(message); Console.Error.WriteLine(list);
    return 1;
}
