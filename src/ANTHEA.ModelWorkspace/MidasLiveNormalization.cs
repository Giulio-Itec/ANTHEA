using System.Text.Json;
using GPC.Converter;
using GPC.Converter.CivilNx;
using GPC.Model.Analysis;
using GPC.Model.Core.Diagnostics;

namespace ANTHEA.ModelWorkspace;

/// <summary>GPC normalization of a copy read from the MIDAS API (2/11/2026): the source tables of the copy go through the GPC converter
/// (CivilNxModelProfile, the mapping of tools/ModelViewer.Prepare and of the text files) and GpcBatchNormalization adds the local frames of beams
/// and plates, the node frames, the boundary symbols and the GPC diagnostics. Plate pressures in local axes and the vertex averaging need these
/// frames. Nothing is recomputed by ANTHEA; a table set the converter cannot read leaves the copy as read, with the pending notice.</summary>
public static class MidasLiveNormalization
{
    public const string PendingCode = "MIDAS-CONVERTER-PENDING";

    public static bool Apply(ModelSnapshot snapshot, bool geometryOnly, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var tables = snapshot.SourceTables;
        if (tables == null || CivilNxModelProfile.RequiredTables.Any(t => !tables.ContainsKey(t))) return Pending(snapshot, "le tabelle UNIT, NODE ed ELEM non sono nella copia");
        // The API bodies as MIDAS sends them ({"TABLE": rows}), the same files tools/ModelViewer.Prepare reads. An empty category is
        // {"message":""} from the API (MidasReadOnlyClient keeps it as {}): the converter reads that answer as a database without records.
        var responses = CivilNxModelProfile.RequiredTables.Concat(CivilNxModelProfile.OptionalTables).Distinct().Where(tables.ContainsKey)
            .ToDictionary(t => t, t => new CivilNxResponse("db/" + t, tables[t].ValueKind == JsonValueKind.Object && !tables[t].EnumerateObject().Any()
                ? "{\"message\":\"\"}" : JsonSerializer.Serialize(new Dictionary<string, JsonElement> { [t] = tables[t] })), StringComparer.Ordinal);
        ImportBatch batch; ImportReport? mapped = null;
        try
        {
            batch = new CivilNxModelProfile().Read(new CivilNxSnapshot(responses), new AnalysisSource
            { Program = CivilNxModelProfile.Program, SolverVersion = "API", ModelRevision = MidasReadOnlyClient.Signature(tables), AnalysisId = "snapshot" }, token);
            token.ThrowIfCancellationRequested();
            // The frames are those the converter resolves while reading; the GPC model adds its diagnostics. A geometry-only reading has no
            // materials, which the model requires, so it is not built.
            if (!geometryOnly) mapped = ModelMapper.Map(batch, token);
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or KeyNotFoundException or InvalidOperationException or ArgumentException or JsonException)
        { return Pending(snapshot, "il convertitore GPC non ha letto le tabelle (" + ex.Message.TrimEnd('.') + ")"); }
        token.ThrowIfCancellationRequested();
        var messages = (snapshot.ImportMessages ?? []).Where(m => m.Code != PendingCode).ToArray();
        GpcBatchNormalization.Apply(snapshot, batch, (IEnumerable<ModelDiagnostic>?)mapped?.Diagnostics ?? batch.Diagnostics);
        // A geometry-only reading has no supports or links: their symbols stay «not acquired», as in ModelSnapshotUpdate.
        if (geometryOnly) snapshot.Boundaries = null;
        string rejected = mapped is { Model: null } ? string.Join("; ", mapped.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => (d.Message ?? d.Code).TrimEnd('.')).Distinct().Take(3)) : "";
        snapshot.ImportMessages = [.. messages, .. snapshot.ImportMessages ?? [], new("MIDAS-CONVERTER", rejected.Length > 0 ? "warning" : "info", "Geometria",
            $"Normalizzazione GPC delle tabelle MIDAS (convertitore {typeof(CivilNxModelProfile).Assembly.GetName().Version}): {snapshot.LocalFrames!.Length:N0} terne locali di aste e piastre" +
            (geometryOnly ? "." : $", {snapshot.Boundaries?.Length ?? 0:N0} simboli di vincoli, molle, link e rilasci.") + " Le tabelle originali restano nella copia." +
            (rejected.Length > 0 ? " Il modello GPC completo non è stato costruito (" + rejected + "): terne e simboli vengono dalla lettura delle tabelle." : ""))];
        return true;
    }

    static bool Pending(ModelSnapshot snapshot, string reason)
    {
        snapshot.ImportMessages = [.. (snapshot.ImportMessages ?? []).Where(m => m.Code != PendingCode), new(PendingCode, "warning", "Geometria",
            "Le tabelle originali sono conservate, ma " + reason + ": terne locali, vincoli, rilasci e link non sono acquisiti. Carichi di pressione in assi locali e media ai vertici non sono disponibili.")];
        return false;
    }
}
