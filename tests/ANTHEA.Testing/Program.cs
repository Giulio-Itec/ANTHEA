using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Anthea.Testing.Capture;
using Anthea.Testing.Comparison;
using Anthea.Testing.Normalization;

// ANTHEA.Testing: the single capture, normalisation and comparison toolkit of the refactoring (docs/refactoring/piano.md, F0.4).
//   capture <out> [--root <repo>] [--tag <name>] [--commit <sha>] [--only <regex>] [--no-trace] [--culture it-IT] [--motore-sle legacy|libreria]
//   compare <a> <b> [--tolerances <file>] [--report <file.json>] [--max <n>]      exit 0 = equal within the tolerances
//   compare-dense <riferimento> <candidato> --confronto <nome> [--classificazione <file>] [--tolerances <file>] [--report <file.json>] [--max <n>]
//                                                                                 dense captures of CheckerMigration.Capture (F2.1): exit 0 = every difference admitted or classified
//   normalize <in> <out>                                                          DOCX, JSON, text, PNG of WPF tests and reports
// Exit codes: 0 success, 1 differences or failure, 2 wrong arguments.
Console.OutputEncoding = Encoding.UTF8;
if (args.Length == 0) return Usage();
var (positional, named, flags) = Parse(args[1..]);
try
{
    switch (args[0])
    {
        case "capture":
        {
            if (positional.Count != 1) return Usage();
            string output = Path.GetFullPath(positional[0]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) { Console.Error.WriteLine("Cartella di uscita non vuota: " + output); return 2; }
            Directory.CreateDirectory(output);
            bool trace = !flags.Contains("no-trace");
            // Before any calculation: JsonFallbackTrace reads the variable at its first use.
            if (trace) Environment.SetEnvironmentVariable("ANTHEA_TRACE_FALLBACKS", Path.Combine(output, "raw", "fallbacks.tsv"));
            Directory.CreateDirectory(Path.Combine(output, "raw"));
            var culture = CultureInfo.GetCultureInfo(named.GetValueOrDefault("culture", "it-IT"));
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
            string root = named.TryGetValue("root", out var r) ? Path.GetFullPath(r) : FindRoot();
            var only = named.TryGetValue("only", out var o) ? new Regex(o, RegexOptions.CultureInvariant) : null;
            // Motore SLE della sezione c.a. e dei muri (refactoring F2.7b, commit A4); senza l'opzione quello predefinito dell'adattatore.
            Anthea.Calculations.ServiceabilityEngine? serviceability = named.GetValueOrDefault("motore-sle") switch
            {
                null => null, "legacy" => Anthea.Calculations.ServiceabilityEngine.Legacy, "libreria" => Anthea.Calculations.ServiceabilityEngine.Library,
                var other => throw new ArgumentException("--motore-sle: legacy o libreria, non " + other)
            };
            return new CaptureRunner(new CaptureOptions(root, output, named.GetValueOrDefault("tag", "cattura"), named.GetValueOrDefault("commit"), only, trace, culture.Name, serviceability)).Run();
        }
        case "compare":
        {
            if (positional.Count != 2) return Usage();
            foreach (var folder in positional) if (!Directory.Exists(folder)) { Console.Error.WriteLine("Cartella non trovata: " + folder); return 2; }
            var comparer = new BaselineComparer(Tolerances.Load(named.GetValueOrDefault("tolerances")));
            var report = comparer.Compare(positional[0], positional[1]);
            if (named.TryGetValue("report", out var path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                File.WriteAllText(path, report.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new UTF8Encoding(false));
            }
            BaselineComparer.Print(report, int.Parse(named.GetValueOrDefault("max", "40"), CultureInfo.InvariantCulture));
            return comparer.NotAdmitted == 0 ? 0 : 1;
        }
        case "compare-dense":
        {
            if (positional.Count != 2 || !named.TryGetValue("confronto", out var setName)) return Usage();
            foreach (var folder in positional) if (!Directory.Exists(folder)) { Console.Error.WriteLine("Cartella non trovata: " + folder); return 2; }
            var comparer = new DenseComparer(Tolerances.Load(named.GetValueOrDefault("tolerances")), DenseClassification.Load(named.GetValueOrDefault("classificazione")), setName);
            var report = comparer.Compare(positional[0], positional[1]);
            if (named.TryGetValue("report", out var path)) DenseComparer.Write(report, path);
            DenseComparer.Print(report, int.Parse(named.GetValueOrDefault("max", "40"), CultureInfo.InvariantCulture));
            return comparer.Passed ? 0 : 1;
        }
        case "normalize":
        {
            if (positional.Count != 2) return Usage();
            var roots = new List<(string, string)> { (Path.GetFullPath(positional[0]), "<INGRESSO>") };
            try { roots.Add((FindRoot(), "<RADICE>")); } catch (DirectoryNotFoundException) { }
            roots.Add((Path.GetTempPath(), "<TEMP>"));
            return Normalizer.Run(positional[0], positional[1], new TextNormalizer(roots));
        }
        default:
            return Usage();
    }
}
catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
{
    Console.Error.WriteLine("ERRORE " + ex.GetType().Name + ": " + ex.Message);
    return 1;
}

static int Usage()
{
    Console.Error.WriteLine("""
        ANTHEA.Testing capture <out> [--root <repo>] [--tag <nome>] [--commit <sha>] [--only <regex modulo/caso>] [--no-trace] [--culture it-IT] [--motore-sle legacy|libreria]
        ANTHEA.Testing compare <a> <b> [--tolerances <file>] [--report <file.json>] [--max <n>]
        ANTHEA.Testing compare-dense <riferimento> <candidato> --confronto <nome> [--classificazione <file>] [--tolerances <file>] [--report <file.json>] [--max <n>]
        ANTHEA.Testing normalize <in> <out>
        """);
    return 2;
}

static (List<string> Positional, Dictionary<string, string> Named, HashSet<string> Flags) Parse(string[] args)
{
    var positional = new List<string>(); var named = new Dictionary<string, string>(StringComparer.Ordinal); var flags = new HashSet<string>(StringComparer.Ordinal);
    for (int i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--")) { positional.Add(args[i]); continue; }
        string name = args[i][2..];
        if (name is "no-trace") flags.Add(name);
        else if (i + 1 < args.Length) named[name] = args[++i];
        else throw new ArgumentException("Valore mancante per --" + name);
    }
    return (positional, named, flags);
}

/// <summary>The repository root: the first folder above the tool that contains ANTHEA.sln.</summary>
static string FindRoot()
{
    for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        if (File.Exists(Path.Combine(folder.FullName, "ANTHEA.sln"))) return folder.FullName;
    throw new DirectoryNotFoundException("Radice del repository (ANTHEA.sln) non trovata sopra " + AppContext.BaseDirectory);
}
