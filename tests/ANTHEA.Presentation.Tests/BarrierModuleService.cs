using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Engine of the scheduling checks, held by barriers (as the F3.6 scheduling checks): when <see cref="Hold"/> is set, every call
/// signals its entry and waits for <see cref="Release"/> or for the cancellation of its token. No timed waits.
/// </summary>
internal sealed class BarrierModuleService : IModuleService
{
    private readonly SemaphoreSlim entered = new(0);
    private readonly SemaphoreSlim releases = new(0);
    private int running;
    private int maxRunning;
    private int calls;

    public string ModuleId => "prova";
    internal bool Hold { get; set; }
    internal Func<JsonObject, JsonObject> Values { get; set; } = data => new JsonObject { ["eco"] = data["valore"]?.DeepClone() };
    internal ConcurrentQueue<JsonObject> Inputs { get; } = new();
    internal ConcurrentQueue<int> Threads { get; } = new();
    internal int Calls => Volatile.Read(ref calls);
    internal int MaxRunning => Volatile.Read(ref maxRunning);

    public JsonObject Prepare(JsonObject source) => (JsonObject)source.DeepClone();

    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        Interlocked.Increment(ref calls);
        int now = Interlocked.Increment(ref running);
        for (int seen = Volatile.Read(ref maxRunning); now > seen; seen = Volatile.Read(ref maxRunning))
            if (Interlocked.CompareExchange(ref maxRunning, now, seen) == seen) break;
        try
        {
            Inputs.Enqueue(source);
            Threads.Enqueue(Environment.CurrentManagedThreadId);
            if (Hold)
            {
                entered.Release();
                releases.Wait(cancellation);
            }
            return new ModuleCalculation(ModuleId, source, Values(source));
        }
        finally { Interlocked.Decrement(ref running); }
    }

    /// <summary>Completes when one more call has entered the engine.</summary>
    internal Task EnteredAsync() => Expect.Within(entered.WaitAsync());

    internal void Release() => releases.Release();
}
