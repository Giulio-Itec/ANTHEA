using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

/// <summary>Per-document LRU cache bounded by entry count and serialized JSON size. Mutable typed engine graphs are never cached or shared.</summary>
public sealed class ModuleCalculationSession
{
    private readonly IModuleService service;
    private readonly int capacity;
    private readonly long byteLimit;
    private long bytes;
    private readonly object sync = new();
    private readonly LinkedList<Entry> recent = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> cache = new(StringComparer.Ordinal);
    private sealed record Entry(string Key, ModuleCalculation Result, long Bytes);
    public ModuleCalculationSession(IModuleService service, int capacity = 4, long byteLimit = 8 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (capacity < 0 || byteLimit < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.service = service; this.capacity = capacity; this.byteLimit = byteLimit;
    }
    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var snapshot = (JsonObject)source.DeepClone();
        string input = snapshot.ToJsonString();
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        lock (sync)
            if (cache.TryGetValue(key, out var hit))
            {
                recent.Remove(hit); recent.AddFirst(hit);
                cancellation.ThrowIfCancellationRequested();
                return hit.Value.Result.CopyJson();
            }
        var result = service.Calculate(snapshot, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (capacity == 0 || result.Native is not null || result.HasError) return result;
        var stored = result.CopyJson();
        long size = Encoding.UTF8.GetByteCount(stored.Data.ToJsonString()) + Encoding.UTF8.GetByteCount(stored.Values.ToJsonString());
        if (size > byteLimit) return result;
        lock (sync)
        {
            if (cache.Remove(key, out var previous)) { recent.Remove(previous); bytes -= previous.Value.Bytes; }
            var item = recent.AddFirst(new Entry(key, stored, size)); cache[key] = item; bytes += size;
            while (cache.Count > capacity || bytes > byteLimit)
            {
                var last = recent.Last!; recent.RemoveLast(); cache.Remove(last.Value.Key); bytes -= last.Value.Bytes;
            }
        }
        return result;
    }
    public void Clear() { lock (sync) { cache.Clear(); recent.Clear(); bytes = 0; } }
}
