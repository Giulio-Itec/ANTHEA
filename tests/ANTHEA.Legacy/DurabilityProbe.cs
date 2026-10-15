namespace Anthea.Calculations;

/// <summary>
/// Vista tipizzata della sonda comune degli adattatori per la durabilità (F2.9-D14), spostata da ConcreteDurabilityAdapter (Probe, StartProbe,
/// StopProbe, ProbeEntry) con la decisione D3 del 15/10/2026; conserva il contratto delle prove di durabilità.
/// </summary>
public sealed class DurabilityProbe
{
    /// <summary>Voce della sonda: adattatore, operazione, motore (forma della sonda generalizzata chiesta da F2.8 §13.2).</summary>
    public sealed record Entry(string Adapter, string Operation, DurabilityEngine Engine);

    readonly ServiceabilityProbe? scope;

    DurabilityProbe(ServiceabilityProbe scope) => this.scope = scope;

    public IReadOnlyList<Entry> Entries => scope is null ? [] : scope.Entries
        .Where(entry => entry.Adapter == nameof(ConcreteDurabilityAdapter))
        .Select(entry => Enum.TryParse<DurabilityEngine>(entry.Engine, out var engine) && Enum.IsDefined(engine)
            ? new Entry(entry.Adapter, entry.Operation, engine) : null)
        .OfType<Entry>()
        .ToArray();

    /// <summary>Avvia una sonda nel contesto corrente; <see cref="Stop"/> ripristina quella precedente.</summary>
    public static DurabilityProbe Start() => new(ServiceabilityProbe.Start(typeof(ConcreteDurabilityAdapter)));

    public static void Stop() => ServiceabilityProbe.Stop(typeof(ConcreteDurabilityAdapter));
}
