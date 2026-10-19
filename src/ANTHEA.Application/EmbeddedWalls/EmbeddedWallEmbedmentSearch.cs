using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.EmbeddedWalls;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

public sealed record EmbeddedWallEmbedmentProgress(int Evaluated, int Planned);

public sealed partial class EmbeddedWallModule
{
    /// <summary>Application orchestration only: each candidate repeats the existing complete pipeline, including FEM.
    /// The GPC coordinator decides acceptance/minimality; the reports retain the exact evaluated snapshots.</summary>
    public EmbeddedWallReportData SearchEmbedment(JsonObject source, CancellationToken cancellation = default,
        IProgress<EmbeddedWallEmbedmentProgress>? progress = null)
    {
        var data = Prepare(source); var longest = ToModel(data);
        double limit = Number(data, "embedment_head_limit"); string provenance = Text(data, "embedment_source");
        if (!double.IsFinite(limit) || limit <= 0) throw new ArgumentException("Assegnare un limite positivo di spostamento in testa, in mm.");
        var families = new List<string> { "HeadDisplacement" };
        if (Text(data, "check_kind") != "Analisi") families.Add("ConcreteStrips");
        foreach (var pair in new[] { ("steel_standard", "SteelSupports"), ("anchor_standard", "Anchors"), ("tube_standard", "SteelTubes"), ("rc_pile_standard", "RcPiles") })
            if (Text(data, pair.Item1) != "Non richieste") families.Add(pair.Item2);
        if (EmbeddedWallDocument.Rows(data, "hydraulic_upl").Any() || EmbeddedWallDocument.Rows(data, "hydraulic_hyd").Any()) families.Add("HydraulicChecks");
        if (EmbeddedWallDocument.Rows(data, "global_checks").Any()) families.Add("GlobalStability");
        var definition = new EmbeddedWallEmbedmentSearch
        {
            WallTop = longest.Top, ReferenceElevation = Number(data, "embedment_reference") * SoilUnits.Metre,
            MinimumEmbedment = Number(data, "embedment_minimum") * SoilUnits.Metre, MaximumEmbedment = Number(data, "embedment_maximum") * SoilUnits.Metre,
            Increment = Number(data, "embedment_increment") * SoilUnits.Metre, Source = provenance,
            Histories = [new() { CaseId = longest.CaseId, StageIds = longest.Stages.Select(s => s.Id).ToArray(), RequiredCheckIds = families.ToArray() }]
        };
        // These are only snapshots of computations already made, including failed hydraulic evaluations.
        var reports = new Dictionary<double, EmbeddedWallReportData>();
        var search = EmbeddedWallEmbedment.Run(definition, (toe, token) =>
        {
            var cut = EmbeddedWallAnalysis.WithToe(longest, toe);
            var candidate = data.DeepClone().AsObject(); candidate["bottom"] = cut.Bottom / SoilUnits.Metre;
            var originalSegments = EmbeddedWallDocument.Rows(data, "segments").ToArray();
            candidate["segments"] = new JsonArray(cut.Segments.Select((s, i) =>
            {
                var row = originalSegments[i].DeepClone().AsObject(); row["bottom"] = s.Bottom / SoilUnits.Metre; return (JsonNode)row;
            }).ToArray());
            WallAnalysisResult? native = null;
            var result = CalculateSingle(candidate, null, token, value => native = value).NativeAs<EmbeddedWallReportData>();
            reports.Add(toe, result);
            var criteria = Criteria(result, families);
            if (native is not null) criteria.Insert(0, EmbeddedWallEmbedment.HeadDisplacement(native, limit, provenance));
            return new WallEmbedmentEvaluation<EmbeddedWallReportData>(toe, native is null ? [] : [native], criteria.AsReadOnly(), result);
        }, cancellation, new EmbedmentProgressRelay(p => progress?.Report(new(p.Evaluated, p.Planned))));
        var mapped = new EmbeddedWallEmbedmentData(search.Completed, search.Cancelled, search.MinimumCertified,
            search.ReferenceElevation / SoilUnits.Metre, search.Increment / SoilUnits.Metre, search.Source,
            Array.AsReadOnly(search.PlannedEmbedments.Select(d => d / SoilUnits.Metre).ToArray()),
            search.Best?.Embedment / SoilUnits.Metre, search.Best?.ToeElevation / SoilUnits.Metre, search.Diagnostic,
            Array.AsReadOnly(search.Candidates.Select(c => new EmbeddedWallEmbedmentCandidate(c.Embedment / SoilUnits.Metre,
                c.ToeElevation / SoilUnits.Metre, c.Outcome, c.Diagnostic,
                Array.AsReadOnly((c.Evaluation?.Criteria ?? []).Select(v => new EmbeddedWallEmbedmentCriterion(v.CaseId, v.Id, v.Outcome,
                    v.Demand, v.Limit, v.Utilization, v.Unit, v.Method, v.Diagnostic, v.StageId, v.Elevation / SoilUnits.Metre)).ToArray()),
                reports.GetValueOrDefault(c.ToeElevation))).ToArray()));
        var selected = search.Best?.Evaluation?.Payload;
        var scope = new List<string>
        {
            "Ricerca esaustiva sulla griglia assegnata: ogni quota ricalcola tutte le fasi della combinazione corrente e le verifiche richieste. Nessuna ipotesi di monotonia o fattorizzazione aggiuntiva.",
            "Il criterio di spostamento riguarda la testa, con limite e fonte assegnati. Il minimo è riferito soltanto alla griglia e ai criteri elencati; combinazioni ulteriori e verifiche non richieste restano distinte.",
            "Il modello iniziale deve coprire la parete più lunga: si tagliano i tratti senza estrapolare sezioni o terreno. Carichi o sostegni sotto il candidato e vincoli al piede impediscono la valutazione.",
            "Le tabelle e i diagrammi principali si riferiscono al candidato accettabile meno profondo, quando disponibile; dati e risultati di tutti i candidati sono conservati nello snapshot e nel CSV della ricerca."
        };
        if (selected is not null) scope.AddRange(selected.Scope);
        var root = selected ?? new EmbeddedWallReportData(longest.Name, "", longest.CaseId, Text(data, "action_basis"), "", false, "", "", [], [], []);
        return root with { Fingerprint = EmbeddedWallDocument.Fingerprint(data), InputJson = data.ToJsonString(new() { WriteIndented = true }),
            Complete = search.Completed && selected is not null, Status = search.Cancelled ? "EmbedmentCancelled" : search.MinimumCertified ? "EmbedmentMinimum" : "EmbedmentUncertified",
            Diagnostic = search.Diagnostic, Scope = scope.AsReadOnly(), EmbedmentSearch = mapped };
    }

    private sealed class EmbedmentProgressRelay(Action<WallEmbedmentSearchProgress> update) : IProgress<WallEmbedmentSearchProgress>
    { public void Report(WallEmbedmentSearchProgress value) => update(value); }

    private static List<WallEmbedmentCriterion> Criteria(EmbeddedWallReportData report, IReadOnlyList<string> requested)
    {
        var result = new List<WallEmbedmentCriterion>(); string id = report.CaseId;
        WallEmbedmentCriterion Row(EngineeringOutcome outcome, string method, string diagnostic, string? stage = null, double? z = null,
            double? demand = null, double? capacity = null, double? ratio = null, string unit = "")
            => new(id, method, outcome, demand, capacity, ratio, unit, method, diagnostic, stage, z * SoilUnits.Metre);
        void Add(string family, IEnumerable<WallEmbedmentCriterion> rows)
        { if (requested.Contains(family)) result.Add(EmbeddedWallEmbedment.Aggregate(id, family, rows)); }
        Add("ConcreteStrips", report.Checks.Select(v => Row(v.Outcome, v.Method, v.Diagnostic, v.Stage, v.Elevation, v.Demand, v.Capacity, v.Ratio, v.Unit)));
        Add("HydraulicChecks", (report.HydraulicChecks ?? []).Select(v => Row(v.Outcome, v.Method, v.Diagnostic, v.Stage, null, v.Demand, v.Capacity, v.Ratio, v.Unit)));
        Add("GlobalStability", (report.GlobalChecks ?? []).Select(v => Row(v.Outcome, v.Reference, v.CaseId + ": " + v.Diagnostic, v.Stage, null, v.Demand, v.Capacity, v.Ratio, "")));
        Add("SteelSupports", (report.SteelSupports ?? []).SelectMany(s => new[] { Row(s.Outcome, "Support " + s.Support, s.Diagnostic, s.Stage) }
            .Concat(s.Checks.Select(v => Row(v.Outcome, v.Method, v.Diagnostic, s.Stage, null, v.Demand, v.Capacity, v.Ratio, "kN")))));
        Add("Anchors", (report.AnchorChecks ?? []).Select(v => Row(v.Outcome, v.Reference, v.Diagnostic, v.Stage, null, v.Demand, v.Capacity, v.Ratio, "kN")));
        Add("SteelTubes", (report.SteelTubes ?? []).SelectMany(s => new[] { Row(s.Outcome, "Section " + s.Section, s.Diagnostic, s.Stage, s.Elevation) }
            .Concat(s.Checks.Select(v => Row(v.Outcome, v.Method, v.Diagnostic, s.Stage, s.Elevation, v.Demand, v.Capacity, v.Ratio, v.Unit)))));
        Add("RcPiles", (report.RcPiles ?? []).SelectMany(s => new[] { Row(s.Outcome, "Section " + s.Section, s.Diagnostic, s.Stage, s.Elevation) }
            .Concat(s.Checks.Select(v => Row(v.Outcome, v.Method, v.Diagnostic, s.Stage, s.Elevation, v.Demand, v.Capacity, v.Ratio, v.Unit)))
            .Concat(s.Details.Select(v => Row(v.Outcome, v.Method, v.Diagnostic, s.Stage, s.Elevation, v.Actual, v.Limit, null, v.Unit)))));
        return result;
    }
}
