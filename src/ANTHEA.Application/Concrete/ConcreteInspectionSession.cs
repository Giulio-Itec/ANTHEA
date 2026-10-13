using System.Text.Json.Nodes;
using Anthea.Application.Scheduling;
namespace Anthea.Application.Concrete;

/// <summary>Serial inspection of already checked sections. Ed retains all entered components,
/// including when the domain check used a 2D projection; Rd reads the native saved limit plane.</summary>
public sealed class ConcreteInspectionSession : IDisposable
{
    private readonly RecalculationScheduler scheduler = new();
    private ConcreteResults.Check? cachedCheck;
    private Lazy<ConcreteResults.StressState>? applied;

    public Task<ScheduledCalculation<ConcreteResults.StressState>> CalculateAsync(ConcreteResults.Check check, bool limit,
        JsonObject input, JsonObject workspace, JsonObject options, string key, ConcreteResults.Action action,
        CancellationToken cancellation = default)
    {
        var sectionInput = (JsonObject)input.DeepClone(); var settings = (JsonObject)workspace.DeepClone();
        var analysisOptions = (JsonObject)options.DeepClone(); analysisOptions["modello"] = "Non lineare";
        return scheduler.ScheduleAsync(token => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(cachedCheck, check)) { cachedCheck = check; applied = null; }
            if (limit) return check.LimitState?.Value ?? throw new ArgumentException("Selezionare una combinazione calcolata.");
            applied ??= new(() => new ConcreteResults.StressState(new CheckerSection(sectionInput, settings, analysisOptions, key).Stress(action.Source, "ED")));
            return applied.Value;
        }, token), cancellation: cancellation);
    }
    public void Invalidate() => scheduler.Invalidate();
    public void Dispose() => scheduler.Dispose();
}
