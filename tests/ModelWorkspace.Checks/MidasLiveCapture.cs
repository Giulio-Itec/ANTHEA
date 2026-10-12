using System.Diagnostics;
using System.Text.Json;
using ANTHEA.ModelWorkspace;

static partial class Program
{
    // Explicit opt-in evidence capture; never executed by normal suites and never reads a saved credential.
    static async Task<int> CaptureLiveMidas(string output, string[] args)
    {
        try
        {
            var connection = new MidasConnection(Environment.GetEnvironmentVariable("ANTHEA_MIDAS_URL") ?? "",
                Environment.GetEnvironmentVariable("ANTHEA_MIDAS_KEY") ?? "");
            Environment.SetEnvironmentVariable("ANTHEA_MIDAS_KEY", null);
            using var source = new MidasModelSource();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(40));
            var progress = new SynchronousImportProgress();
            var watch = Stopwatch.StartNew();
            var catalog = await source.InspectAsync(connection, progress, timeout.Token);
            double inspectionSeconds = watch.Elapsed.TotalSeconds;
            var scope = args.Contains("--geometry-only") ? ModelUpdateScope.Geometry : ModelUpdateScope.Everything;
            var cases = args.Contains("--one-case") ? catalog.StaticCases.Take(1).ToArray() : catalog.StaticCases.ToArray();
            var snapshot = await source.ReadAsync(new(connection, scope, catalog.Signature, cases, true, true), progress, timeout.Token);
            snapshot = ModelSnapshotUpdate.Apply(null, snapshot, scope);
            string file = Path.Combine(output, "modello.antheamodel");
            await using (var stream = new FileStream(file, FileMode.CreateNew)) await JsonSerializer.SerializeAsync(stream, snapshot, cancellationToken: timeout.Token);
            File.WriteAllText(Path.Combine(output, "acquisizione.json"), JsonSerializer.Serialize(new
            {
                catalog.Nodes, catalog.Elements, AvailableStaticCases = catalog.StaticCases.Count,
                ImportedCases = snapshot.Results.Length, Displacements = snapshot.Results.Sum(r => r.Displacements?.Length ?? 0),
                PlateCorners = snapshot.Results.Sum(r => r.Values.Length), Scope = scope.ToString(), InspectionSeconds = inspectionSeconds,
                TotalSeconds = watch.Elapsed.TotalSeconds, Bytes = new FileInfo(file).Length, Fingerprint = snapshot.Fingerprint()
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Acquisizione reale completata: " + snapshot.Results.Length + " casi; " + watch.Elapsed.TotalSeconds.ToString("F1") + " s.");
            return 0;
        }
        catch (Exception ex)
        {
            string safe = ex is ModelSourceException source ? source.Message + " " + source.Recovery : ex.Message;
            File.WriteAllText(Path.Combine(output, "errore-acquisizione.txt"), safe); Console.Error.WriteLine(safe); return 1;
        }
    }
    sealed class SynchronousImportProgress : IProgress<ModelImportProgress>
    { public void Report(ModelImportProgress value) => Console.WriteLine(value.Message + (value.Total > 0 ? $" · {value.Completed}/{value.Total}" : "")); }
}
