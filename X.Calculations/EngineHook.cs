using System.Collections.Immutable;

namespace Anthea.Calculations;

/// <summary>
/// Punto d'aggancio delle prove negli adattatori di libreria (decisione D3 del 15/10/2026, modo b). I motori storici e la sonda delle
/// chiamate stanno in tests/ANTHEA.Legacy, progetto che l'installer non contiene; qui restano soltanto le interfacce, dichiarate accanto
/// agli adattatori, e gli agganci del flusso corrente. Il valore è un <see cref="AsyncLocal{T}"/>: segue Task.Run e Parallel
/// (ExecutionContext) ed è nullo in produzione, quindi ogni adattatore usa la libreria e non registra nulla. Nessuna formula.
/// </summary>
internal static class EngineHook
{
    static readonly AsyncLocal<ImmutableDictionary<Type, object>?> current = new();

    /// <summary>Implementazione agganciata per <typeparamref name="T"/> nel flusso corrente; null in produzione.</summary>
    internal static T? Get<T>() where T : class
        => current.Value is { } hooks && hooks.TryGetValue(typeof(T), out var hook) ? (T)hook : null;

    /// <summary>Agganci correnti, da rimettere con <see cref="Restore"/> alla fine di un ambito.</summary>
    internal static ImmutableDictionary<Type, object>? Snapshot => current.Value;

    internal static void Restore(ImmutableDictionary<Type, object>? hooks) => current.Value = hooks;

    /// <summary>Aggancia <paramref name="hook"/> per <typeparamref name="T"/>; null lo toglie (libreria).</summary>
    internal static void Set<T>(T? hook) where T : class
        => current.Value = hook is null ? current.Value?.Remove(typeof(T)) : (current.Value ?? ImmutableDictionary<Type, object>.Empty).SetItem(typeof(T), hook);

    /// <summary>Registra adattatore, operazione e motore di una chiamata, se la sonda delle prove è agganciata.</summary>
    internal static void Record(string adapter, string operation, string engine) => Get<IAdapterObserver>()?.Record(adapter, operation, engine);

    /// <summary>Registra un risultato o un rifiuto della libreria prima della mappatura, se la sonda delle prove è agganciata.</summary>
    internal static void RecordResult(object result) => Get<IAdapterObserver>()?.RecordResult(result);

    /// <summary>Nome del motore nella sonda: «Legacy» con un motore storico agganciato, altrimenti «Library».</summary>
    internal static string EngineName(object? legacy) => legacy is null ? "Library" : "Legacy";
}

/// <summary>Sonda delle prove: adattatore, operazione e motore di ogni chiamata, risultati e rifiuti grezzi della libreria.</summary>
internal interface IAdapterObserver
{
    void Record(string adapter, string operation, string engine);
    void RecordResult(object result);
}
