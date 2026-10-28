using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ANTHEA.ModelWorkspace;

/// <summary>Straus7 source of the viewer: runs ANTHEA.Strand7Worker.exe (x64, GPC converter R3) for each operation, following
/// <see cref="Strand7WorkerProtocol"/>. This class never loads St7api.dll. A failed, cancelled or incomplete operation publishes nothing:
/// the result is read only after the worker has renamed its complete file and ended with success.</summary>
public sealed class Strand7ModelSource : IStrand7ModelSource
{
    readonly string workerPath;
    readonly IReadOnlyList<string> prefix;

    /// <param name="workerPath">Worker executable; default: next to the application.</param>
    /// <param name="prefixArguments">Arguments placed before the request path (a substitute worker in the tests).</param>
    public Strand7ModelSource(string? workerPath = null, IEnumerable<string>? prefixArguments = null)
    {
        this.workerPath = workerPath ?? Path.Combine(AppContext.BaseDirectory, Strand7WorkerProtocol.WorkerFileName);
        prefix = prefixArguments?.ToArray() ?? [];
    }

    public async Task<Strand7ModelCatalog> InspectAsync(Strand7ImportRequest request, IProgress<ModelImportProgress>? progress, CancellationToken token)
    {
        Prepare(request with { Scope = string.IsNullOrWhiteSpace(request.ResultsPath) ? ModelUpdateScope.ModelWithoutResults : ModelUpdateScope.Everything }, token);
        var catalog = await RunAsync<Strand7ModelCatalog>(Strand7WorkerOperation.Inspect, request, progress, token).ConfigureAwait(false);
        if (catalog.Cases == null || catalog.LoadCases == null || string.IsNullOrWhiteSpace(catalog.SourceSignature) || catalog.Nodes <= 0)
            throw new InvalidDataException("Catalogo Straus7 incompleto. Nessun dato è stato importato.");
        return catalog;
    }

    public async Task<ModelSnapshot> ReadAsync(Strand7ImportRequest request, IProgress<ModelImportProgress>? progress, CancellationToken token)
    {
        Prepare(request, token);
        bool results = request.Scope is ModelUpdateScope.Results or ModelUpdateScope.Everything;
        if (results && (request.CaseNumbers.Count == 0 || !request.Forces && !request.Displacements))
            throw new InvalidDataException("Seleziona almeno un caso e un tipo di risultato, oppure scegli Geometria + attributi + carichi.");
        if (string.IsNullOrWhiteSpace(request.ExpectedSignature)) throw new InvalidDataException("Controlla prima i file e i casi di Straus7.");
        var snapshot = await RunAsync<ModelSnapshot>(Strand7WorkerOperation.Read, request, progress, token).ConfigureAwait(false);
        try { snapshot.Validate(); }
        catch (InvalidDataException ex) { throw new InvalidDataException("Copia Straus7 non valida: " + ex.Message + " Nessun dato è stato importato.", ex); }
        return snapshot;
    }

    static void Prepare(Strand7ImportRequest request, CancellationToken token)
    {
        var check = Strand7ImportSetup.Check(request, token);
        if (!check.FilesReady) throw new InvalidDataException(check.Summary + " " + string.Join(" ", check.Issues));
    }

    async Task<T> RunAsync<T>(Strand7WorkerOperation operation, Strand7ImportRequest request, IProgress<ModelImportProgress>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var folder = Directory.CreateTempSubdirectory("anthea-straus7-");
        try
        {
            string output = Path.Combine(folder.FullName, "risultato.json"), requestPath = Path.Combine(folder.FullName, "richiesta.json");
            File.WriteAllText(requestPath, Strand7WorkerProtocol.Serialize(new(Strand7WorkerProtocol.Version, operation, Path.GetFullPath(request.ModelPath),
                string.IsNullOrWhiteSpace(request.ResultsPath) ? null : Path.GetFullPath(request.ResultsPath), string.IsNullOrWhiteSpace(request.ApiLibraryPath) ? null : request.ApiLibraryPath,
                request.Scope, request.CaseNumbers.ToArray(), request.Forces, request.Displacements, request.ExpectedSignature, output)), new UTF8Encoding(false));
            var start = new ProcessStartInfo(workerPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = folder.FullName };
            foreach (string argument in prefix) start.ArgumentList.Add(argument);
            start.ArgumentList.Add(requestPath);
            Process process;
            try { process = Process.Start(start) ?? throw new InvalidDataException("Processo di lettura Straus7 non avviato."); }
            catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
            { throw new InvalidDataException("Processo di lettura Straus7 non trovato (" + Path.GetFileName(workerPath) + "). Reinstalla ANTHEA.", ex); }
            using (process)
            {
                using var registration = token.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } catch (Win32Exception) { } });
                var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);
                string? error = null; bool done = false;
                while (await process.StandardOutput.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
                {
                    if (Strand7WorkerProtocol.TryProgress(line, out var step)) progress?.Report(step);
                    else if (Strand7WorkerProtocol.ErrorText(line) is { } text) error = text;
                    else if (line == Strand7WorkerProtocol.DoneLine) done = true;
                }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                string diagnostics = await errors.ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (process.ExitCode != Strand7WorkerProtocol.Success)
                    throw new InvalidDataException("Lettura Straus7 non completata (" + Strand7WorkerProtocol.ExitMeaning(process.ExitCode) + "): " +
                        (error ?? (diagnostics.Length > 0 ? diagnostics.Trim() : "nessun messaggio dal processo")) + " Nessun dato è stato importato.");
                if (!done || !File.Exists(output)) throw new InvalidDataException("Il processo Straus7 non ha consegnato un risultato completo. Nessun dato è stato importato.");
            }
            try { return Strand7WorkerProtocol.ReadResult<T>(output, ModelSnapshotFiles.MaximumPackageBytes); }
            catch (System.Text.Json.JsonException ex) { throw new InvalidDataException("Risultato del processo Straus7 non valido. Nessun dato è stato importato.", ex); }
        }
        finally
        {
            // Created by this call for this operation only.
            try { folder.Delete(true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
