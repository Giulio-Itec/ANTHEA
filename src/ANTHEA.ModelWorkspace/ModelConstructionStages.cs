using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Converter;
using GPC.Converter.CivilNx;
using GPC.Model.Analysis;
using GPC.Model.Results.Locations;
using GPC.Model.Stages;
using static ANTHEA.ModelWorkspace.MidasGpcDisplay;

namespace ANTHEA.ModelWorkspace;

public sealed record ModelConstructionPhase(int Id, string Name, double? DurationDays, bool? SavesResults, int GroupOperations)
{
    public string Description => Name + (DurationDays.HasValue ? $" · {DurationDays:G6} giorni" : " · durata non disponibile") +
        (SavesResults == false ? " · risultati non salvati" : "");
}
public sealed record ModelStageStep(string Phase, string Step);
public sealed record MidasStageReadRequest(ModelStageStep[] Steps, string[] Cases, string DisplacementReference)
{
    internal CivilNxStageSelection Selection()
    {
        if (Steps == null || Cases == null || Steps.Length > 1000 || Cases.Length > 100 || Steps.Any(s => s == null) ||
            !Enum.TryParse<CivilNxDisplacementReference>(DisplacementReference, false, out var reference) || !Enum.IsDefined(reference))
            throw new InvalidDataException("Selezione delle fasi o riferimento degli spostamenti non valido.");
        return new(Steps.Select(s => new CivilNxStageStep(s.Phase, s.Step)), Cases, reference);
    }
}

/// <summary>Presentation identity of a single source state. No superposition or activation assumption.</summary>
public sealed record ModelStageResult(string SourceCase, string Phase, string Step, string? DisplacementReference, string SourceRevision)
{
    public int Version { get; init; } = 1;
    [JsonIgnore] public string Label => $"{SourceCase} [CS · {Phase} · {Step}" +
        (DisplacementReference == null ? "]" : $" · {DisplacementReference}]");
}

public static class ModelConstructionStages
{
    public static (ModelConstructionPhase[] Phases, string[] Cases) Catalog(IReadOnlyDictionary<string, JsonElement>? tables)
    {
        if (tables == null || !tables.TryGetValue("STAG", out var stages) || !stages.EnumerateObject().Any()) return ([], []);
        var source = MidasGpcDisplay.FromTables(tables.Where(p => p.Key is "STAG" or "STCT").ToDictionary(p => p.Key, p => p.Value));
        var batch = new CivilNxModelProfile().ReadConstructionStages(source, Identity(source.Hash));
        if (batch.Stages.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count() != batch.Stages.Count)
            throw new InvalidDataException("Nomi delle fasi duplicati: la selezione dei risultati sarebbe ambigua.");
        return (batch.Stages.Select(s => new ModelConstructionPhase(s.Id, s.Name, s.Definition?.DurationDays,
            s.Definition?.SaveResults, s.Definition?.GroupChanges.Count ?? 0)).ToArray(), batch.LoadCases.Select(c => c.StageSourceCaseName).ToArray());
    }

    internal static void ValidateResults(ModelResultSet[] results)
    {
        var keys = new HashSet<(string, string, string, string?)>();
        foreach (var result in results.Where(r => r.Stage != null))
        {
            var s = result.Stage!;
            if (s.Version != 1 || string.IsNullOrWhiteSpace(s.SourceCase) || string.IsNullOrWhiteSpace(s.Phase) || string.IsNullOrWhiteSpace(s.Step) ||
                s.SourceRevision?.Length != 64 || !s.SourceRevision.All(Uri.IsHexDigit) ||
                s.DisplacementReference is not (null or "Accumulative" or "Current" or "Real") ||
                result.IsEnvelope || result.Combination != null || result.Name != s.Label ||
                result.Displacements?.Length > 0 && s.DisplacementReference == null ||
                !keys.Add((s.SourceCase, s.Phase, s.Step, s.DisplacementReference)))
                throw new InvalidDataException("Identità del risultato per fase mancante, duplicata o non compatibile.");
        }
    }

    internal static AnalysisSource Identity(string revision) => new() {
        Program = CivilNxModelProfile.Program, SolverVersion = "copia MIDAS acquisita da ANTHEA", ModelRevision = revision, AnalysisId = revision };

    /// <summary>GPC validates identities, axes, units and states. This adapter only projects supported values to the existing viewer format.</summary>
    internal static ModelResultSet[] Import(ModelSnapshot snapshot, IReadOnlyList<CivilNxResponse> tables,
        MidasStageReadRequest request, bool displacements, CancellationToken token)
    {
        var selection = request.Selection(); var source = Source(snapshot);
        var import = CivilNxGeometryReader.Import(source, Identity(source.Hash), new StageProfile(selection.SourceCases), token);
        token.ThrowIfCancellationRequested();
        if (import.Model == null || import.Status == ImportStatus.Rejected)
            throw new InvalidDataException("Il modello GPC non può associare gli stage: " + Diagnostics(import.Diagnostics));
        var model = import.Model; ValidateGeometry(snapshot, model);
        var report = CivilNxResults.ImportStages(model, source, tables, "anthea-stage", selection, cancellationToken: token);
        token.ThrowIfCancellationRequested();
        if (report.Status == ImportStatus.Rejected || report.Status == ImportStatus.Cancelled)
            throw new InvalidDataException("Risultati per fase non acquisiti: " + Diagnostics(report.Diagnostics));
        var sourceNodes = model.NodesElements.Values.ToDictionary(n => n.Id, n => int.Parse(n.Source.OriginalId, CultureInfo.InvariantCulture));
        var elements = snapshot.Elements.ToDictionary(e => e.Id);
        var samples = model.AllElements.SelectMany(owner => owner.Results.SelectMany(r => r.Results).Select(sample => (owner, sample)))
            .Where(p => p.sample is PointResultPlateForces or StationResultBeamForces or NodeResultDisplacement).ToArray();
        var output = new List<ModelResultSet>();
        foreach (var group in samples.GroupBy(p => (((StageResultCase)p.sample.Case).SourceCaseName, p.sample.State.Phase, p.sample.State.Step)))
        {
            token.ThrowIfCancellationRequested();
            var context = new ModelStageResult(group.Key.SourceCaseName, group.Key.Phase, group.Key.Step,
                displacements ? request.DisplacementReference : null, source.Hash);
            var corners = new List<CornerResult>(); var fields = new Dictionary<string, List<ResultValue>>(); var nodal = new List<ModelDisplacement>();
            foreach (var (owner, sample) in group)
            {
                int id = int.Parse(owner.Source.OriginalId, CultureInfo.InvariantCulture); var v = DisplayVector(sample);
                switch (sample)
                {
                    case PointResultPlateForces plate:
                        if (!plate.SourceNodeId.HasValue || !sourceNodes.TryGetValue(plate.SourceNodeId.Value, out var node))
                            throw new InvalidDataException("Contouring stage: il risultato plate deve essere associato a un vertice esplicito.");
                        corners.Add(new(id, node, v)); break;
                    case StationResultBeamForces beam:
                        int endpoint = beam.ParametricDistance == 0 ? elements[id].Nodes[0] : beam.ParametricDistance == 1 ? elements[id].Nodes[1] : 0;
                        var components = ResultFields.Components("BEAM", "LOCAL");
                        for (int c = 0; c < components.Count; c++)
                        {
                            if (!fields.TryGetValue(components[c], out var values)) fields[components[c]] = values = [];
                            values.Add(new(id, endpoint, v[c], beam.ParametricDistance));
                        }
                        break;
                    case NodeResultDisplacement:
                        nodal.Add(new(id, v[0], v[1], v[2]) { Rotation = new(v[3], v[4], v[5]) }); break;
                }
            }
            output.Add(new(context.Label, false, corners.ToArray()) { Stage = context,
                Fields = fields.Select(p => new ResultField("BEAM", "LOCAL", p.Key, p.Value.ToArray())).ToArray(),
                Displacements = nodal.Count == 0 ? null : nodal.ToArray() });
        }
        // Every explicitly selected state must contain at least one supported value. Missing objects are never filled with zeroes.
        foreach (var step in request.Steps) foreach (var name in request.Cases)
            if (!output.Any(r => r.Stage!.SourceCase == name && r.Stage.Phase == step.Phase && r.Stage.Step == step.Step))
                throw new InvalidDataException($"Nessun risultato visualizzabile per {name} · {step.Phase} · {step.Step}.");
        snapshot.ImportMessages = [.. snapshot.ImportMessages ?? [], .. import.Diagnostics.Concat(report.Diagnostics)
            .Where(d => d.Severity != GPC.Model.Core.Diagnostics.DiagnosticSeverity.Information)
            .Select(d => new ModelImportMessage(d.Code, "warning", d.Record ?? "Stage", d.Message))];
        return output.ToArray();
    }

    static string Diagnostics(IEnumerable<GPC.Model.Core.Diagnostics.ModelDiagnostic> diagnostics) => string.Join("; ", diagnostics.Take(6).Select(d => d.Code + ": " + d.Message));
    sealed class StageProfile(IEnumerable<string> cases) : ICivilNxGeometryProfile
    {
        readonly CivilNxModelProfile profile = new(cases);
        public string Id => profile.Id;
        public IReadOnlyDictionary<string, CapabilityStatus> Capabilities => profile.Capabilities;
        public ImportBatch Read(CivilNxSnapshot snapshot, AnalysisSource identity, CancellationToken token)
        {
            var batch = profile.Read(snapshot, identity, token);
            // Solver combinations remain in the source document; importing stage states never evaluates or expands them.
            batch.Combinations.Clear(); return batch;
        }
    }
}
