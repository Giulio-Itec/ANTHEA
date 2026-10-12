using System.Reflection.PortableExecutable;

namespace ANTHEA.ModelWorkspace;

public enum Strand7ImportFile { Model, Results, ApiLibrary }
public sealed record Strand7ImportRequest(string ModelPath, string? ResultsPath, string ApiLibraryPath,
    ModelUpdateScope Scope, IReadOnlyList<int> CaseNumbers, bool Forces, bool Displacements);
public sealed record Strand7ImportPreparation(bool FilesReady, string Summary, IReadOnlyList<string> Issues);

/// <summary>Contract for the future GPC native worker; WPF never owns the native API or its licence.</summary>
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
}

/// <summary>Read-only preparation. Does not load or invoke native code, open a model, claim a licence or save files.</summary>
public static class Strand7ImportSetup
{
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
        void FileCheck(string? path, string extension, string label)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path) || !Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            { issues.Add(label + ": seleziona un file " + extension + " esistente."); return; }
            try { using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); if (stream.Length == 0) issues.Add(label + ": file vuoto."); }
            catch (IOException) { issues.Add(label + ": file non leggibile o bloccato."); }
            catch (UnauthorizedAccessException) { issues.Add(label + ": accesso rifiutato."); }
        }
        FileCheck(request.ModelPath, ".st7", "Modello");
        if (request.Scope is ModelUpdateScope.Results or ModelUpdateScope.Everything) FileCheck(request.ResultsPath, ".lsa", "Risultati statici lineari");
        FileCheck(request.ApiLibraryPath, ".dll", "Libreria API R3");
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
            ? "File predisposti. L’importazione nativa sarà attivata collegando il convertitore GPC R3 al processo dedicato; casi e dati non sono ancora stati letti."
            : "Configurazione Straus/Strand7 da completare.", issues);
    }
}
