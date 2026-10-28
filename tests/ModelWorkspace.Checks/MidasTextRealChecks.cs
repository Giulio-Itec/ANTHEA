using System.Text;
using System.Text.Json;
using ANTHEA.ModelWorkspace;
using GPC.Converter.MidasCivil;

static partial class Program
{
    /// <summary>Opt-in, real MIDAS exports (GPC_MIDAS_TEXT_SAMPLES, a folder read recursively and never written): every .mct/.mgt is read by
    /// ANTHEA and, independently, by the GPC file adapter (MidasTextFiles.Import, Windows-1252). ANTHEA must import exactly the files GPC maps
    /// and reject the others with a message; only counts and outcomes are saved, never the content of the files.</summary>
    static int MidasTextRealChecks(string directory)
    {
        if (Environment.GetEnvironmentVariable("GPC_MIDAS_TEXT_SAMPLES") is not { Length: > 0 } folder) return 0;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".mct", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".mgt", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).ToArray();
        var outcomes = new List<object>(); var mismatches = new List<string>(); int imported = 0;
        foreach (string file in files)
        {
            string name = Path.GetRelativePath(folder, file);
            var gpc = MidasTextFiles.Import(file, "check", null, Encoding.GetEncoding(1252));
            string? error = null; ModelSnapshot? snapshot = null;
            try { snapshot = ModelSnapshotFiles.Read(file); } catch (InvalidDataException ex) { error = ex.Message; }
            if ((snapshot != null) != (gpc.Model != null)) mismatches.Add($"{name}: ANTHEA {(snapshot != null ? "importa" : "rifiuta: " + error)}, GPC {gpc.Status}");
            else Check(true, $"MIDAS reale {name}: ANTHEA {(snapshot != null ? "importa" : "rifiuta")} come il convertitore GPC");
            if (snapshot != null) imported++;
            outcomes.Add(new { File = name, Program = snapshot?.Source.Split(" · ")[0], Imported = snapshot != null, Nodes = snapshot?.Nodes.Length, Elements = snapshot?.Elements.Length,
                Plates = snapshot?.Elements.Count(e => e.Type == "PLATE"), Excluded = snapshot?.ImportMessages?.Where(m => m.Code.StartsWith("MIDAS-TEXT-", StringComparison.Ordinal) && m.Severity == "Warning").Select(m => m.Code).ToArray(),
                Combinations = snapshot == null ? 0 : ModelCombinations.Read(snapshot).Length, Rejection = error, GpcStatus = gpc.Status.ToString(),
                GpcError = gpc.Diagnostics.FirstOrDefault(d => d.Severity == GPC.Model.Core.Diagnostics.DiagnosticSeverity.Error)?.Code });
        }
        File.WriteAllText(Path.Combine(directory, "midas-reali.json"), JsonSerializer.Serialize(new { Files = files.Length, Imported = imported, Outcomes = outcomes }, new JsonSerializerOptions { WriteIndented = true }));
        Check(mismatches.Count == 0, "MIDAS reali: esito di ANTHEA diverso dal convertitore GPC per " + string.Join(" | ", mismatches));
        Check(imported > 0, $"MIDAS reali: {imported} di {files.Length} file importati, gli altri rifiutati con un messaggio");
        return files.Length;
    }
}
