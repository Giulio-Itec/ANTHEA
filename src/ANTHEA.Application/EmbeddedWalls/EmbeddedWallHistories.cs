using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

public static partial class EmbeddedWallDocument
{
    /// <summary>Rows with a phase reference belong to exactly one history. Empty case means the main case.</summary>
    public static IReadOnlyList<string> HistoryTables { get; } = Array.AsReadOnly(new[] {
        "stages", "activations", "loads", "distributed_loads", "hydraulic_upl", "hydraulic_actions", "hydraulic_hyd", "global_checks", "global_actions" });
    public static IReadOnlyList<string> CaseOverrideKeys { get; } = Array.AsReadOnly(new[] {
        "action_basis", "check_kind", "steel_standard", "steel_check_kind", "anchor_standard", "anchor_situation", "anchor_a1m1",
        "tube_standard", "tube_check_kind", "rc_pile_standard", "rc_pile_check_kind", "embedment_head_limit", "embedment_source" });

    private static JsonObject NewCase()
    {
        var defaults = Create(); var row = new JsonObject { ["id"] = "Caso 2" };
        foreach (var key in CaseOverrideKeys) row[key] = defaults[key]?.DeepClone();
        return row;
    }

    /// <summary>Split complete assigned histories, preserving shared geometry/materials and input provenance.
    /// No scaling, combination of results or numerical calculation occurs here.</summary>
    public static IReadOnlyList<JsonObject> SplitHistories(JsonObject source)
    {
        var data = Snapshot(source); data.Remove("result_snapshot");
        string principal = Text(data, "case_id");
        var extra = Rows(data, "cases").ToArray();
        var ids = new[] { principal }.Concat(extra.Select(r => Text(r, "id"))).ToArray();
        if (ids.Length > 32 || ids.Any(string.IsNullOrWhiteSpace) || ids.Any(id => id != id.Trim())
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException("Assegnare al massimo 32 casi con identificativi distinti e senza spazi iniziali/finali.");
        string Owner(JsonObject row) => string.IsNullOrWhiteSpace(Text(row, "case")) ? principal : Text(row, "case");
        foreach (string table in HistoryTables)
        foreach (var row in Rows(data, table))
            if (!ids.Contains(Owner(row), StringComparer.Ordinal)) throw new ArgumentException("Caso inesistente nella tabella " + table + ": " + Owner(row));
        var histories = new List<JsonObject>();
        for (int i = 0; i < ids.Length; i++)
        {
            var copy = data.DeepClone().AsObject(); copy["cases"] = new JsonArray(); copy["case_id"] = ids[i];
            if (i > 0)
                foreach (string key in CaseOverrideKeys)
                    if (extra[i - 1][key] is { } value && !(key == "embedment_source" && string.IsNullOrWhiteSpace(Text(extra[i - 1], key))))
                        copy[key] = value.DeepClone();
            foreach (string table in HistoryTables)
                copy[table] = new JsonArray(Rows(data, table).Where(r => Owner(r) == ids[i]).Select(r => r.DeepClone()).ToArray());
            var stages = Rows(copy, "stages").Select(r => Text(r, "id")).ToArray();
            if (stages.Length == 0 || stages.Any(string.IsNullOrWhiteSpace) || stages.Distinct(StringComparer.Ordinal).Count() != stages.Length)
                throw new ArgumentException("Definire fasi con identificativi distinti per il caso " + ids[i]);
            foreach (string table in HistoryTables.Where(t => t != "stages"))
            foreach (var row in Rows(copy, table))
                if (!stages.Contains(Text(row, "stage"), StringComparer.Ordinal))
                    throw new ArgumentException("Fase inesistente nel caso " + ids[i] + ", tabella " + table + ".");
            if (ids.Length > 1 && (string.IsNullOrWhiteSpace(Text(copy, "action_basis")) || Text(copy, "action_basis") == "Da definire"))
                throw new ArgumentException("Dichiarare provenienza e coefficienti delle azioni per il caso " + ids[i]);
            histories.Add(copy);
        }
        return histories.AsReadOnly();
    }
}

public sealed partial class EmbeddedWallModule
{
    private ModuleCalculation CalculateHistories(JsonObject source, IProgress<WallAnalysisProgress>? progress,
        CancellationToken cancellation, Action<WallAnalysisResult>? capture = null)
    {
        cancellation.ThrowIfCancellationRequested();
        var data = Prepare(source); var histories = EmbeddedWallDocument.SplitHistories(data);
        if (histories.Count == 1) return CalculateSingle(histories[0], progress, cancellation, capture);
        var native = new List<WallAnalysisResult>(); var reports = new List<EmbeddedWallReportData>();
        foreach (var input in histories)
        {
            cancellation.ThrowIfCancellationRequested();
            reports.Add(CalculateSingle(input, progress, cancellation, a => { native.Add(a); capture?.Invoke(a); }, identifyCase: true).NativeAs<EmbeddedWallReportData>());
        }
        // GPC computes the envelope; missing mechanical analyses (e.g. failed seepage) stay explicitly missing.
        var envelope = native.Count == 0 ? null : Envelopes(native, cancellation);
        var missing = reports.Where(r => !native.Any(a => a.CaseId == r.CaseId)).ToArray();
        if (envelope is not null && missing.Length > 0)
            envelope = envelope with { Complete = false, Diagnostic = "Inviluppo parziale: alcuni casi non hanno una soluzione meccanica. " + envelope.Diagnostic,
                Histories = Array.AsReadOnly(envelope.Histories.Concat(missing.Select(r => new EmbeddedWallEnvelopeHistoryData(r.CaseId,
                    false, r.Hydraulics?.FirstOrDefault(h => !h.Complete)?.Stage, [], r.Diagnostic))).ToArray()) };
        bool complete = reports.All(r => r.Complete);
        string diagnostic = $"Casi convergenti {reports.Count(r => r.Complete)}/{reports.Count}. "
            + "Ogni storia è ricalcolata da uno stato iniziale indipendente; nessuna somma di risultati non lineari.";
        var root = reports[0] with { Fingerprint = EmbeddedWallDocument.Fingerprint(data), InputJson = data.ToJsonString(new() { WriteIndented = true }),
            Complete = complete, Status = complete ? "HistoriesConverged" : "HistoriesIncomplete", Diagnostic = diagnostic,
            Cases = reports.AsReadOnly(), Envelope = envelope,
            Scope = Array.AsReadOnly(new[] { diagnostic, "Geometria, materiali, sezioni e parametri del terreno condivisi. Fasi, carichi, falde e verifiche appartengono al caso dichiarato. Le azioni sono già combinate dall'utente: nessun coefficiente è applicato implicitamente." }) };
        cancellation.ThrowIfCancellationRequested();
        return new ModuleCalculation(ModuleId, data, EmbeddedWallDocument.ResultValues(root), root);
    }
}
