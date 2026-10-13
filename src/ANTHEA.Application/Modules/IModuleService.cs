using System.Text.Json.Nodes;

namespace Anthea.Application.Modules;

/// <summary>Nonvisual orchestration boundary. The service owns independent snapshots and delegates every calculation to the existing engines.</summary>
public interface IModuleService
{
    string ModuleId { get; }
    JsonObject Prepare(JsonObject source);
    ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default);
}

/// <summary>A completed calculation, not a statement that engineering checks pass. Original result fields and diagnostics remain available.</summary>
public sealed record ModuleCalculation(string ModuleId, JsonObject Data, JsonObject Values, object? Native = null)
{
    public bool HasError => Values.S("errore").Length != 0;
    public T NativeAs<T>() where T : class => Native as T ?? throw new InvalidOperationException("Risultato tipizzato non disponibile: " + typeof(T).Name);
    internal ModuleCalculation CopyJson() => Native is null
        ? new(ModuleId, (JsonObject)Data.DeepClone(), (JsonObject)Values.DeepClone())
        : throw new InvalidOperationException("Un risultato tipizzato non può essere condiviso nella cache JSON.");
}
