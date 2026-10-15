namespace Anthea.Calculations;

/// <summary>
/// Ambito di un aggancio dei motori storici o della sonda (<see cref="EngineHook"/>, decisione D3 del 15/10/2026): alla fine rimette gli
/// agganci che c'erano prima. Uso: <c>using (DurabilityLegacy.Use(DurabilityEngine.Legacy)) { … }</c>; ogni famiglia espone il proprio Use.
/// </summary>
public sealed class EngineScope : IDisposable
{
    readonly System.Collections.Immutable.ImmutableDictionary<Type, object>? previous = EngineHook.Snapshot;
    bool disposed;

    EngineScope() { }

    /// <summary>Aggancia <paramref name="hook"/> per <typeparamref name="T"/> (null: la libreria) fino alla fine dell'ambito.</summary>
    internal static EngineScope Set<T>(T? hook) where T : class
    {
        var scope = new EngineScope();
        EngineHook.Set(hook);
        return scope;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        EngineHook.Restore(previous);
    }

    /// <summary>Più ambiti insieme, chiusi in ordine inverso; i null sono ignorati.</summary>
    public static IDisposable Combine(params IDisposable?[] scopes) => new Combined(scopes.OfType<IDisposable>().ToArray());

    sealed class Combined(IDisposable[] scopes) : IDisposable
    {
        public void Dispose() { for (int i = scopes.Length - 1; i >= 0; i--) scopes[i].Dispose(); }
    }
}
