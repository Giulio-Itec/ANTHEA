using System.Collections.Concurrent;

namespace Anthea.Calculations;

/// <summary>Voce della sonda degli adattatori: adattatore, operazione e motore usato.</summary>
public sealed record ServiceabilityProbeEntry(string Adapter, string Operation, string Engine);

/// <summary>
/// Sonda diagnostica degli adattatori di libreria (refactoring F2.7b, progetto F2.7 §6.2): registra adattatore, operazione e motore di ogni
/// chiamata del flusso in cui è attiva. Spostata da ConcreteServiceabilityAdapter.cs (decisione D3 del 15/10/2026): gli adattatori chiamano
/// soltanto <see cref="EngineHook.Record"/>, che in produzione non fa nulla; la sonda si aggancia lì per la durata dell'ambito. L'aggancio
/// segue <see cref="Task.Run(Action)"/> e <see cref="Parallel"/> (ExecutionContext). Il registro è thread-safe.
/// Uso: <c>using var probe = ServiceabilityProbe.Start();</c>, poi <see cref="Entries"/>; Dispose ripristina la sonda precedente.
/// </summary>
public sealed class ServiceabilityProbe : IDisposable, IAdapterObserver
{
    readonly ConcurrentQueue<ServiceabilityProbeEntry> entries = new();
    readonly ConcurrentQueue<object> results = new();
    readonly IAdapterObserver? previous;
    readonly Type? owner;

    ServiceabilityProbe(IAdapterObserver? previous, Type? owner)
    {
        this.previous = previous;
        this.owner = owner;
    }

    /// <summary>Attiva una sonda nuova per il flusso corrente.</summary>
    public static ServiceabilityProbe Start() => Start(null);

    // Le facciate diagnostiche condividono il contesto, mantenendo la proprietà del proprio scope.
    internal static ServiceabilityProbe Start(Type? owner)
    {
        var probe = new ServiceabilityProbe(EngineHook.Get<IAdapterObserver>(), owner);
        EngineHook.Set<IAdapterObserver>(probe);
        return probe;
    }

    // Stop di una facciata non deve chiudere una sonda generale o di un altro adattatore.
    internal static void Stop(Type owner)
    {
        if (EngineHook.Get<IAdapterObserver>() is ServiceabilityProbe probe && probe.owner == owner) probe.Dispose();
    }

    /// <summary>Chiamate registrate, nell'ordine di arrivo.</summary>
    public IReadOnlyList<ServiceabilityProbeEntry> Entries => entries.ToArray();

    /// <summary>Registra una chiamata se una sonda è attiva nel flusso; altrimenti non fa nulla.</summary>
    public static void Record(string adapter, string operation, Enum engine) => EngineHook.Record(adapter, operation, engine.ToString());

    /// <summary>Registra una chiamata di un adattatore con un solo motore (il nome del motore).</summary>
    public static void Record(string adapter, string operation, string engine) => EngineHook.Record(adapter, operation, engine);

    /// <summary>
    /// Risultati e rifiuti della libreria ricevuti dagli adattatori nel flusso, prima della mappatura, nell'ordine di arrivo (ciclo di prototipo di F2.7:
    /// copertura di codici, flag, argomenti, motivi ed esiti nella prova 5c).
    /// </summary>
    public IReadOnlyList<object> Results => results.ToArray();

    /// <summary>Registra un risultato o un rifiuto della libreria se una sonda è attiva nel flusso; altrimenti non fa nulla.</summary>
    public static void RecordResult(object result) => EngineHook.RecordResult(result);

    void IAdapterObserver.Record(string adapter, string operation, string engine) => entries.Enqueue(new(adapter, operation, engine));

    void IAdapterObserver.RecordResult(object result) => results.Enqueue(result);

    public void Dispose()
    {
        if (ReferenceEquals(EngineHook.Get<IAdapterObserver>(), this)) EngineHook.Set(previous);
    }
}
