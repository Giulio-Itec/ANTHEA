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
    // F5.13 (d): the views of this calculation (WallModel, BridgeDesignModel, BridgeResultView) live with it, no longer in static tables.
    private readonly ResultViews views = new();

    /// <summary>A copy (<c>with</c>) has the same components and no view: its views are built again from its own result.</summary>
    private ModuleCalculation(ModuleCalculation original)
    {
        ModuleId = original.ModuleId; Data = original.Data; Values = original.Values; Native = original.Native;
        views = new();
    }

    public bool HasError => Values.S("errore").Length != 0;
    public T NativeAs<T>() where T : class => Native as T ?? throw new InvalidOperationException("Risultato tipizzato non disponibile: " + typeof(T).Name);
    internal ModuleCalculation CopyJson() => Native is null
        ? new(ModuleId, (JsonObject)Data.DeepClone(), (JsonObject)Values.DeepClone())
        : throw new InvalidOperationException("Un risultato tipizzato non può essere condiviso nella cache JSON.");

    /// <summary>
    /// The view of type <typeparamref name="T"/> of this calculation: built by <paramref name="create"/> at the first request, then the same
    /// instance for every caller and thread, as the ConditionalWeakTable keyed by the calculation that it replaces (F5.13 d, proposal 4.6).
    /// Two calculations never share a view, even with equal components.
    /// </summary>
    public T View<T>(Func<ModuleCalculation, T> create) where T : class
    {
        ArgumentNullException.ThrowIfNull(create);
        return views.Get(this, create);
    }

    /// <summary>
    /// The views of one calculation, guarded by its own lock: a view is built outside the lock and the first one stored wins, as
    /// ConditionalWeakTable.GetValue. Equal for every instance, so that the record equality of the calculation stays that of its four components.
    /// </summary>
    private sealed class ResultViews
    {
        private readonly object gate = new();
        private readonly Dictionary<Type, object> built = [];

        internal T Get<T>(ModuleCalculation owner, Func<ModuleCalculation, T> create) where T : class
        {
            lock (gate) { if (built.TryGetValue(typeof(T), out var existing)) return (T)existing; }
            var view = create(owner);
            lock (gate)
            {
                if (built.TryGetValue(typeof(T), out var existing)) return (T)existing;
                built.Add(typeof(T), view);
                return view;
            }
        }

        public override bool Equals(object? obj) => obj is ResultViews;
        public override int GetHashCode() => 0;
    }
}
