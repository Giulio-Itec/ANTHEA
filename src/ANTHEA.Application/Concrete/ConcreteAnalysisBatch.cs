using System.Text.Json.Nodes;
using Anthea.Application.Scheduling;

namespace Anthea.Calculations;

public sealed record ConcreteAnalysisStep(string Name, ConcreteDomainResult? Domain = null,
    Dictionary<string, StressOutcome>? Stress = null, string? Error = null);

public sealed partial class ConcreteAnalysisSession
{
    private readonly SemaphoreSlim batches = new(1, 1);
    private (string Signature, CheckerSectionModel Model)? preparedSection;

    /// <summary>Coordinates one incremental batch on owned snapshots. Callbacks use the caller's
    /// synchronization context; the numerical work runs in the background. A session serializes batches
    /// while retaining independent domain and stress caches. With a <paramref name="gate"/> (the limiter of the application,
    /// 1.16.0 D-5) the batch takes one place for its preparation and all its steps, after the batch before it and only when it has
    /// something to calculate; the steps keep their parallelism inside the place.</summary>
    public async Task CalculateBatchAsync(JsonObject sourceInput, JsonObject sourceWorkspace,
        IReadOnlyDictionary<string, JsonObject[]> sourceRows, IReadOnlySet<string> requested,
        Action<ConcreteAnalysisStep> completed, Action<IReadOnlyCollection<string>>? running = null,
        CancellationToken cancellation = default, CalculationGate? gate = null)
    {
        ArgumentNullException.ThrowIfNull(completed);
        var input = (JsonObject)sourceInput.DeepClone();
        var workspace = (JsonObject)sourceWorkspace.DeepClone();
        var rows = sourceRows.ToDictionary(p => p.Key, p => p.Value.Select(r => (JsonObject)r.DeepClone()).ToArray());
        var steps = requested.ToHashSet();
        await batches.WaitAsync(cancellation);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if (!steps.Any(s => s.StartsWith("3D:", StringComparison.Ordinal) ||
                s.StartsWith("2D:", StringComparison.Ordinal) || SectionWorkspace.Sets.Skip(2).Contains(s))) return;
            using var place = gate is null ? null : await gate(cancellation);
            cancellation.ThrowIfCancellationRequested();
            string signature = input.ToJsonString() + workspace["trefoli"]?.ToJsonString();
            CheckerSectionModel prepared;
            if (preparedSection is { } cached && cached.Signature == signature) prepared = cached.Model;
            else
            {
                running?.Invoke(["preparazione unica della sezione"]);
                prepared = await Task.Run(() => CheckerSection.PrepareModel(input, workspace), cancellation);
                cancellation.ThrowIfCancellationRequested();
                preparedSection = (signature, prepared);
            }
            var active = new HashSet<string>();
            var notifications = new object();
            async Task Step(string name, Func<ConcreteAnalysisStep> calculate)
            {
                cancellation.ThrowIfCancellationRequested();
                lock (notifications) { active.Add(name); running?.Invoke(active.ToArray()); }
                ConcreteAnalysisStep outcome;
                try { outcome = await Task.Run(calculate, cancellation); }
                catch (Exception error) when (error is not OperationCanceledException)
                { outcome = new(name, Error: error.Message); }
                finally { lock (notifications) { active.Remove(name); if (active.Count > 0 && !cancellation.IsCancellationRequested) running?.Invoke(active.ToArray()); } }
                cancellation.ThrowIfCancellationRequested();
                lock (notifications) { cancellation.ThrowIfCancellationRequested(); completed(outcome); }
            }
            var domains = from key in new[] { "SLU", "SLV" }
                from three in new[] { true, false }
                let name = (three ? "3D:" : "2D:") + key
                where steps.Contains(name)
                select Step(name, () => new(name, Domain: Domain(input, workspace,
                    workspace[three ? "dominio3d" : "dominio2d"]!.AsObject(), key, three,
                    rows.GetValueOrDefault(key) ?? [], cancellation, prepared)));
            await Task.WhenAll(domains);
            await Task.WhenAll(SectionWorkspace.Sets.Skip(2).Where(steps.Contains).Select(key =>
                Step(key, () => new(key, Stress: Stress(input, workspace, workspace["sle"]![key]!.AsObject(),
                    key, rows.GetValueOrDefault(key) ?? [], cancellation, prepared)))));
            cancellation.ThrowIfCancellationRequested();
        }
        finally { batches.Release(); }
    }
}
