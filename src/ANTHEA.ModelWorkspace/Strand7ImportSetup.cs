using System.Reflection.PortableExecutable;

namespace ANTHEA.ModelWorkspace;

public enum Strand7ImportFile { Model, Results, ApiLibrary }
public sealed record Strand7ImportRequest(string ModelPath, string? ResultsPath, string ApiLibraryPath,
    ModelUpdateScope Scope, IReadOnlyList<int> CaseNumbers, bool Forces, bool Displacements)
{
    /// <summary>Source signature of the catalog the case numbers come from; the read is refused when the files have changed since.</summary>
    public string? ExpectedSignature { get; init; }
}
public sealed record Strand7ImportPreparation(bool FilesReady, string Summary, IReadOnlyList<string> Issues);

/// <summary>Read-only Straus7 source. The implementation (<see cref="Strand7ModelSource"/>) runs the GPC converter in the separate
/// x64 worker process; WPF never owns the native API or its licence.</summary>
public interface IStrand7ModelSource
{
    Task<Strand7ModelCatalog> InspectAsync(Strand7ImportRequest request, IProgress<ModelImportProgress>? progress, CancellationToken token);
    Task<ModelSnapshot> ReadAsync(Strand7ImportRequest request, IProgress<ModelImportProgress>? progress, CancellationToken token);
}
public sealed record Strand7ResultCase(int Number, string Name)
{
    // Native stage association must be supplied by the worker; zero/null is never inferred from a case label.
    public int? StageNumber { get; init; }
}
public sealed record Strand7ModelCatalog(int Nodes, int Elements, IReadOnlyList<Strand7ResultCase> Cases, string SourceSignature)
{
    public IReadOnlyList<ModelConstructionPhase> Phases { get; init; } = [];
    public bool StageResultsAvailable { get; init; }
    /// <summary>1.16.0: the results file is a staged nonlinear static solution (.nla): its cases are stage results, not static cases.</summary>
    public bool NonlinearStages { get; init; }
    // 1.15.0: filled by the worker from the GPC import.
    public int Beams { get; init; }
    public int Plates { get; init; }
    public int Combinations { get; init; }
    public IReadOnlyList<string> LoadCases { get; init; } = [];
    public string ApiVersion { get; init; } = "";
    public IReadOnlyList<string> Warnings { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public string Summary => $"{Nodes:N0} nodi · {Beams:N0} aste · {Plates:N0} piastre · {LoadCases.Count} casi di carico · {Combinations} combinazioni LSA · " +
        (Phases.Count > 0 ? $"{Phases.Count} fasi · " : "") +
        (Cases.Count == 0 ? "nessun risultato" : NonlinearStages ? $"{Cases.Count} risultati per fase (NLA)" : $"{Cases.Count} casi di risultato" +
            (StageResultsAvailable ? " della fase attiva " + Cases.FirstOrDefault(c => c.StageNumber != null)?.StageNumber : ""));
}

/// <summary>Read-only preparation. Does not load or invoke native code, open a model, claim a licence or save files.</summary>
public static class Strand7ImportSetup
{
    /// <summary>Results files read by the Straus7 worker: linear static (.lsa) and staged nonlinear static (.nla, 1.16.0).</summary>
    public static readonly string[] ResultExtensions = [".lsa", ".nla"];

    public static string FindInstalledApi()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (string folder in new[] { "Straus7 R31", "Strand7 R31", "Straus7 R3", "Strand7 R3" })
        {
            string candidate = Path.Combine(programFiles, folder, "Bin64", "St7api.dll");
            if (File.Exists(candidate)) return candidate;
        }
        return "";
    }

    public static Strand7ImportPreparation Check(Strand7ImportRequest request, CancellationToken token = default)
    {
        var issues = new List<string>();
        if (!Enum.IsDefined(request.Scope)) issues.Add("Modalità di acquisizione non valida.");
        void FileCheck(string? path, string label, params string[] extensions)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path) || !extensions.Any(e => Path.GetExtension(path).Equals(e, StringComparison.OrdinalIgnoreCase)) || !File.Exists(path))
            { issues.Add(label + ": seleziona un file " + string.Join(" o ", extensions) + " esistente."); return; }
            try { using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); if (stream.Length == 0) issues.Add(label + ": file vuoto."); }
            catch (IOException) { issues.Add(label + ": file non leggibile o bloccato."); }
            catch (UnauthorizedAccessException) { issues.Add(label + ": accesso rifiutato."); }
        }
        FileCheck(request.ModelPath, "Modello", ".st7");
        if (request.Scope is ModelUpdateScope.Results or ModelUpdateScope.Everything) FileCheck(request.ResultsPath, "Risultati", ResultExtensions);
        FileCheck(request.ApiLibraryPath, "Libreria API R3", ".dll");
        if (!string.IsNullOrWhiteSpace(request.ApiLibraryPath) && !Path.GetFileName(request.ApiLibraryPath).Equals("St7api.dll", StringComparison.OrdinalIgnoreCase)) issues.Add("Seleziona St7api.dll dell’installazione Straus/Strand7 R3.");
        if (File.Exists(request.ApiLibraryPath))
        {
            try
            {
                using var stream = File.OpenRead(request.ApiLibraryPath);
                using var reader = new PEReader(stream);
                if (reader.PEHeaders.CoffHeader.Machine != Machine.Amd64 || reader.HasMetadata ||
                    !reader.PEHeaders.CoffHeader.Characteristics.HasFlag(Characteristics.Dll))
                    issues.Add("L’API deve essere una libreria nativa Windows x64 di Straus/Strand7 R3.");
            }
            catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException)
            { issues.Add("Il file API non è una libreria Windows leggibile."); }
        }
        token.ThrowIfCancellationRequested();
        return new(issues.Count == 0, issues.Count == 0
            ? "File pronti per il processo di lettura Straus7 (convertitore GPC R3, sola lettura); casi e dati non sono ancora stati letti."
            : "Configurazione Straus/Strand7 da completare.", issues);
    }
}
