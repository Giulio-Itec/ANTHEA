using System.Text.Json.Nodes;
using Anthea.Application.Scheduling;
using GPC.Checkers.Geotechnics.Piles;
namespace Anthea.Application.Modules;

public sealed record PileSegmentProposal(string SourceSignature, JsonArray Segments);

/// <summary>Document operations around the native segment engine; each proposal owns its data.</summary>
public static class PileReinforcementOperations
{
    public static JsonArray Split(JsonObject source, int index, double cut)
    {
        var snapshot = (JsonObject)source.DeepClone();
        var segments = ElasticHorizontalPile.ReadSegments(snapshot);
        if (index < 0 || index >= segments.Length) throw new ArgumentException("Tratto non disponibile.");
        var division = PileSegments.Split(segments, segments[index].Id, cut);
        PileSegments.Validate(division, segments[^1].End);
        var rows = snapshot["elastico"]!.Array("tratti");
        var next = (JsonObject)rows[index]!.DeepClone(); next["id"] = "T-" + Guid.NewGuid().ToString("N")[..6];
        rows[index]!["fine"] = cut; rows.Insert(index + 1, next);
        return (JsonArray)rows.DeepClone();
    }
    public static PileSegmentProposal Propose(JsonObject source, JsonObject result)
        => new(source.ToJsonString(), ElasticHorizontalPile.ProposeSegments((JsonObject)source.DeepClone(), (JsonObject)result.DeepClone()));
    public static void ApplyProposal(JsonObject source, PileSegmentProposal proposal)
    {
        RequireRevision(source, proposal.SourceSignature);
        var e = source["elastico"]!.AsObject();
        var previous = e["tratti"]!.DeepClone(); var next = proposal.Segments.DeepClone();
        if (e["storico_tratti"] is not JsonArray) e["storico_tratti"] = new JsonArray();
        e.Array("storico_tratti").Add(previous); e["tratti"] = next;
        e["origine_tratti"] = "Proposta applicata esplicitamente dall'utente";
    }
    internal static void RequireRevision(JsonObject source, string signature)
    {
        if (source.ToJsonString() != signature) throw new ArgumentException("Input cambiati: ripetere la ricerca.");
    }
    internal static void ApplyDesign(JsonObject source, string signature, int index, JsonObject proposal)
    {
        RequireRevision(source, signature);
        var values = ElasticHorizontalPile.ReinforcementKeys.ToDictionary(key => key,
            key => proposal[key]?.DeepClone() ?? throw new ArgumentException("Proposta incompleta: " + key));
        var target = index == 0 ? source["sezione"]!.AsObject() : source["elastico"]!.Array("tratti")[index]!.AsObject();
        foreach (var (key, value) in values) target[key] = value;
        if (index > 0) target["collegato"] = false;
        target["proposta_armatura"] = proposal.DeepClone();
    }
}

/// <summary>Owns one design-window snapshot and serializes its candidate searches.</summary>
public sealed class PileSegmentDesignSession : IDisposable
{
    private readonly JsonObject source, result;
    private readonly int index;
    private readonly string signature;
    private readonly RecalculationScheduler scheduler = new();
    private bool disposed;
    public PileSegmentDesignSession(JsonObject source, JsonObject result, int index)
    {
        this.source = (JsonObject)source.DeepClone(); this.result = (JsonObject)result.DeepClone(); this.index = index;
        signature = source.ToJsonString();
    }
    public bool Matches(JsonObject current) => !disposed && current.ToJsonString() == signature;
    public Task<ScheduledCalculation<JsonObject>> CalculateAsync(int[] counts, double[] diameters, double[] links, double[] spacings,
        IProgress<PileCalculationProgress>? progress = null, CancellationToken cancellation = default)
    {
        var ns = counts.ToArray(); var ds = diameters.ToArray(); var ts = links.ToArray(); var ss = spacings.ToArray();
        return scheduler.ScheduleAsync(token => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            if (!ElasticPileAnalysisSession.Matches(result, source)) throw new ArgumentException("Risposta FEM obsoleta rispetto ai dati comuni.");
            return ElasticHorizontalPile.DesignSegment(source, result, index, ns, ds, ts, ss, token, progress);
        }, token), cancellation: cancellation);
    }
    public void Apply(JsonObject current, JsonObject proposal)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        PileReinforcementOperations.ApplyDesign(current, signature, index, proposal);
    }
    public void Dispose() { disposed = true; scheduler.Dispose(); }
}
