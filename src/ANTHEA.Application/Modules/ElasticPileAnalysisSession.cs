using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

/// <summary>
/// The calculation of the elastic response of the horizontal pile (FEM, then the reinforcement), as the sheet uses it: implemented by
/// <see cref="ElasticPileAnalysisSession"/>; the traces and the checks of the sheet (F5.8) hold it with a barrier.
/// </summary>
public interface IElasticPileAnalysis : IDisposable
{
    /// <summary>FEM responses calculated so far (the response is reused while the shared data do not change).</summary>
    int ResponseCalculations { get; }

    /// <summary>The response callback runs on the caller's context before the section checks start.</summary>
    Task<JsonObject> CalculateAsync(JsonObject source, Action<JsonObject> responseReady, IProgress<PileProgress>? progress = null, CancellationToken cancellation = default);
}

/// <summary>Owns one FEM response and the existing section resistance cache. A reinforcement edit reuses the FEM; input/output JSON never aliases the cache.</summary>
public sealed class ElasticPileAnalysisSession : IElasticPileAnalysis
{
    private readonly SemaphoreSlim serial = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly ElasticPileVerificationCache resistanceCache = new();
    private string? responseJson;
    private int responseCalculations;
    private volatile bool disposed;
    public int ResponseCalculations => Volatile.Read(ref responseCalculations);

    public static bool Matches(JsonObject? response, JsonObject source)
    {
        if (response is null) return false;
        try { return response.S("chiave_risposta") == ElasticHorizontalPile.ResponseKey(source); }
        catch (ArgumentException) { return false; }
    }

    /// <summary>The response callback runs on the caller's context before the section checks start.</summary>
    public async Task<JsonObject> CalculateAsync(JsonObject source, Action<JsonObject> responseReady,
        IProgress<PileProgress>? progress = null, CancellationToken cancellation = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(responseReady);
        var snapshot = (JsonObject)source.DeepClone();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        var token = linked.Token;
        await serial.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            var cached = responseJson;
            var response = cached is null ? null : JsonNode.Parse(cached)!.AsObject();
            if (!Matches(response, snapshot))
            {
                response = await Task.Run(() => ElasticHorizontalPile.CalculateResponse(snapshot, token), token);
                token.ThrowIfCancellationRequested();
                responseJson = response.ToJsonString();
                Interlocked.Increment(ref responseCalculations);
            }
            token.ThrowIfCancellationRequested();
            responseReady((JsonObject)response!.DeepClone());
            token.ThrowIfCancellationRequested();
            var result = await Task.Run(() => ElasticHorizontalPile.CompleteReinforcement(snapshot, response,
                resistanceCache, token, progress: PileProgress.Adapt(progress)), token);
            token.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            if (disposed) { responseJson = null; resistanceCache.Clear(); }
            serial.Release();
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; lifetime.Cancel();
        // Clear again in the active operation's finally if the engine is still returning.
        resistanceCache.Clear(); responseJson = null;
    }
}
