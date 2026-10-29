using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Scheduling;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The shear and torsion checks of a batch (C2/S5-c, 1.16.0): the rows of the table that receive the outcomes, and the copies of the data
/// and of the rows that the checks read on a worker thread, taken on the UI thread after the automatic parameters and the invalidation.
/// </summary>
internal sealed record ConcreteShearRequest(IReadOnlyList<JsonRowViewModel> Targets, JsonObject Input, JsonObject Settings, JsonObject[] Rows);

/// <summary>The outcome of a row of the shear (ConcreteShearBatch): the checks, or the error of its data.</summary>
internal sealed record ConcreteShearOutcome(int Index, ConcreteResults.ShearBatchResult? Calculated, string? Error);

/// <summary>What the coordinator of the calculation asks to the sheet of the RC section (decision C1).</summary>
internal interface IConcreteCalculationHost
{
    /// <summary>True while the sheet is built or reloaded: nothing is queued.</summary>
    bool Initializing { get; }
    JsonObject Input { get; }
    JsonObject Settings { get; }
    /// <summary>The rows of the actions of every limit state, as the tables hold them (the session copies them).</summary>
    IReadOnlyDictionary<string, JsonObject[]> ActionRows();
    /// <summary>The status line of the sheet.</summary>
    void ShowStatus(string text);
    /// <summary>A domain of the batch: <paramref name="name"/> is «3D:SLU», «2D:SLV»…</summary>
    void PublishDomain(string name, ConcreteDomainResult result);
    /// <summary>The stresses of a family of the serviceability limit states.</summary>
    void PublishStress(string key, Dictionary<string, StressOutcome> outcomes);
    /// <summary>
    /// The shear and torsion checks, first part, on the UI thread (C2/S5-c, 1.16.0): the automatic parameters, the rows invalidated, the
    /// copies for the checks; null when the tab of the shear is not built (nothing is calculated).
    /// </summary>
    ConcreteShearRequest? PrepareShear();
    /// <summary>
    /// The shear and torsion checks, second part, on the UI thread: the outcomes of the rows in their order, then (when <paramref name="complete"/>)
    /// the summaries; without <paramref name="complete"/> the rows before the failure that stopped the checks, as the historic batch left them.
    /// </summary>
    void PublishShear(ConcreteShearRequest request, IReadOnlyList<ConcreteShearOutcome> outcomes, bool complete);
    /// <summary>After the publication of a batch: tables and summaries of the sheet, results of the current data.</summary>
    void Published();
    /// <summary>A batch stopped by invalid data: no results.</summary>
    void Blocked();
}

/// <summary>
/// The calculation of the RC section sheet (decision C1 of 17/10/2026): the orchestration of the historic view, kept as it is, over the
/// existing <see cref="RecalculationScheduler"/> and <see cref="ConcreteAnalysisSession"/>; the sheet is in mode
/// <see cref="SheetCalculationMode.None"/> of <see cref="SheetViewModelBase"/>. The coordinator
/// <list type="bullet">
/// <item>keeps the checks to calculate again (<see cref="Pending"/>), invalidated by family (3D and 2D at the ULS and SLV, a family of the
/// serviceability limit states, the shear) or all together;</item>
/// <item>queues the recalculation on one turn of the UI thread, without a delay (<see cref="Queue"/>): an edit during a calculation cancels
/// it, the new one starts when it ends;</item>
/// <item>calculates with <see cref="ConcreteAnalysisSession.CalculateBatchAsync"/> (one preparation, parallel steps), then the shear, and
/// publishes every outcome to the sheet, which gives it to its child ViewModels;</item>
/// <item>gives the outcomes of the parallel steps in the fixed order of <see cref="AllCalculations"/> (decision C6): the domains and the
/// stresses of a batch are applied together, when the last one of their phase arrives, in that order. The historic view applied them in the
/// order in which the threads ended, so that two errors together had no fixed order.</item>
/// <item>since 1.16.0 (D-5) passes through the limiter of the application as the calculations of the modules: the batch takes one place for
/// its preparation and its steps, the shear one more; meanwhile the batch is busy and waits, cancellable, with the status of the request
/// («in attesa»). <see cref="IsEngineRunning"/> says when a place is held (the Running of the other modules).</item>
/// <item>since 1.16.0 (C2/S5-c) calculates the shear on a worker thread, on a copy of the data, after the batch: the sheet prepares the rows and
/// writes the outcomes on the UI thread, in the order of the rows, as the historic view did on the UI thread. The results do not change; a change
/// of the data while the shear runs cancels it as it cancels the batch.</item>
/// </list>
/// States, status texts and errors are those of the historic view (RunAnalysis and CalculateAllAsync of ConcreteDomains.cs).
/// </summary>
public sealed class ConcreteCalculation : ObservableObject, IDisposable
{
    /// <summary>Every check of the sheet, in their fixed order.</summary>
    public static ImmutableArray<string> AllCalculations { get; } = ["3D:SLU", "2D:SLU", "3D:SLV", "2D:SLV", "SLE", "SLE_FREQ", "SLE_QP", "Taglio"];

    private readonly IConcreteCalculationHost host;
    private readonly SynchronizationContext? ui;
    private readonly CalculationLimiter limiter;
    private readonly HashSet<string> pending = new(AllCalculations);
    private readonly Dictionary<string, string> errors = new();
    private RecalculationScheduler scheduler = new();
    private bool busy, queued, progress, disposed, engine;
    private long revision;

    internal ConcreteCalculation(ConcreteAnalysisSession session, IConcreteCalculationHost host, SynchronizationContext? ui, CalculationLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        Session = session; this.host = host; this.ui = ui; this.limiter = limiter;
    }

    /// <summary>The session of the sheet: preparation and caches of the domains and of the stresses.</summary>
    internal ConcreteAnalysisSession Session { get; }

    /// <summary>True while a batch runs (the Busy of the historic view), also while it waits for a place of the limiter.</summary>
    public bool IsBusy { get => busy; private set => SetProperty(ref busy, value); }
    /// <summary>
    /// True while the batch or the shear holds a place of the limiter of the application (1.16.0, D-5): the engine works. Set after the place
    /// is taken and cleared before it is freed, on the thread of the batch.
    /// </summary>
    public bool IsEngineRunning { get => engine; private set => SetProperty(ref engine, value); }
    /// <summary>A recalculation waits for its turn of the UI thread.</summary>
    public bool IsQueued { get => queued; private set => SetProperty(ref queued, value); }
    /// <summary>The progress bar of the sheet.</summary>
    public bool IsProgressVisible { get => progress; private set => SetProperty(ref progress, value); }
    /// <summary>Version of the data for the calculation: it grows at every invalidation.</summary>
    public long Revision => revision;
    /// <summary>The checks still to calculate.</summary>
    public IReadOnlySet<string> Pending => pending;
    /// <summary>Errors of the last outcome of every check (and «Dati» when the data block the batch), in the order of the sheet.</summary>
    public IReadOnlyDictionary<string, string> Errors => errors;
    /// <summary>The same dictionary for the export of the results.</summary>
    internal Dictionary<string, string> ErrorsForExport => errors;

    /// <summary>The data changed: every check is calculated again; a running batch is cancelled.</summary>
    public void InvalidateAll() => Invalidate(AllCalculations);

    /// <summary>Some options changed: only <paramref name="affected"/> (every check when empty) are calculated again; a running batch is cancelled.</summary>
    public void Invalidate(IReadOnlyCollection<string> affected)
    {
        pending.UnionWith(affected.Count == 0 ? AllCalculations : affected);
        revision++; OnPropertyChanged(nameof(Revision));
        scheduler.Invalidate();
    }

    /// <summary>Queues the recalculation on one turn of the UI thread (no delay); nothing while the sheet is built, closed or already queued.</summary>
    public void Queue()
    {
        if (host.Initializing || disposed || IsQueued) return;
        IsQueued = true;
        // One turn of the UI thread lets the edit in progress finish (the historic view used a Background operation of the dispatcher).
        if (ui is null) _ = RunQueuedAfterYieldAsync();
        else ui.Post(_ => _ = RunQueuedAsync(), null);
    }

    /// <summary>
    /// «Calcola»: waits for the batch in progress, then calculates what is still pending (nothing when every check is current). The task
    /// ends with the batch.
    /// </summary>
    public async Task CalculateAllAsync()
    {
        while (IsBusy && !disposed) await Task.Delay(30);
        if (disposed) return;
        var requested = pending.ToHashSet();
        if (requested.Count == 0) return;
        await RunAnalysisAsync(token => WorkAsync(requested, token));
    }

    /// <summary>The sheet is closed: the running batch is cancelled and never published.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; IsQueued = false;
        scheduler.Dispose();
    }

    private async Task RunQueuedAfterYieldAsync()
    {
        await Task.Yield();
        await RunQueuedAsync();
    }

    private async Task RunQueuedAsync()
    {
        if (!IsQueued || disposed) return;
        IsQueued = false;
        if (!IsBusy) await CalculateAllAsync();
    }

    private async Task RunAnalysisAsync(Func<CancellationToken, Task> work)
    {
        if (IsBusy || disposed) return;
        IsQueued = false; long requested = revision; IsBusy = true;
        IsProgressVisible = true;
        try
        {
            var completion = await scheduler.ScheduleAsync(async token => { await work(token); return true; });
            if (disposed) return;
            if (completion.Cancelled) { host.ShowStatus("Nuove modifiche · aggiornamento in attesa…"); return; }
            completion.TryPublish(current =>
            {
                if (current.Error is not null) ExceptionDispatchInfo.Capture(current.Error).Throw();
                host.ShowStatus("Aggiornamento tabelle e riepiloghi…");
                host.Published();
                host.ShowStatus(errors.Count == 0 ? "Aggiornamento automatico completato · risultati riferiti ai dati correnti · controllare esiti e limiti di applicabilità"
                    : "Aggiornamento parziale · " + string.Join(" · ", errors.Select(e => e.Key + ": " + e.Value)));
            });
        }
        catch (Exception failure)
        {
            if (!disposed) { host.ShowStatus("Dati non validi · calcolo bloccato: " + failure.Message); errors["Dati"] = failure.Message; host.Blocked(); }
        }
        finally
        {
            IsBusy = false;
            if (!disposed) { IsProgressVisible = false; if (requested != revision) Queue(); }
        }
    }

    private async Task WorkAsync(HashSet<string> requested, CancellationToken token)
    {
        errors.Remove("Dati");
        var activeSteps = new HashSet<string>();
        async Task StepAsync(string name, Func<Task> action)
        {
            token.ThrowIfCancellationRequested();
            errors.Remove(name);
            activeSteps.Add(name);
            void Progress() => host.ShowStatus("GPC Engine · in corso: " + string.Join(" · ", activeSteps));
            try { var task = action(); Progress(); await task; }
            catch (Exception failure) when (failure is not OperationCanceledException) { errors[name] = failure.Message; }
            finally
            {
                // A modification during an await cancels the snapshot: its unfinished work stays queued, with the newly affected checks.
                if (!token.IsCancellationRequested) pending.Remove(name);
                activeSteps.Remove(name); if (activeSteps.Count > 0) Progress();
            }
        }
        if (requested.SetEquals(["Taglio"]))
        {
            await StepAsync("Taglio", () => ShearAsync(token)); return;
        }
        // The outcomes of a phase (the domains, then the stresses) are applied together in the fixed order of the checks (C6).
        var phases = new[] { AllCalculations.Take(4).Where(requested.Contains).ToArray(), AllCalculations.Skip(4).Take(3).Where(requested.Contains).ToArray() };
        var arrived = new Dictionary<string, ConcreteAnalysisStep>();
        void Apply(ConcreteAnalysisStep outcome)
        {
            errors.Remove(outcome.Name);
            if (outcome.Error is not null) errors[outcome.Name] = outcome.Error;
            else if (outcome.Domain is { } domain) host.PublishDomain(outcome.Name, domain);
            else if (outcome.Stress is { } stresses) host.PublishStress(outcome.Name, stresses);
            pending.Remove(outcome.Name);
        }
        await Session.CalculateBatchAsync(host.Input, host.Settings, host.ActionRows(), requested, outcome =>
        {
            token.ThrowIfCancellationRequested();
            arrived[outcome.Name] = outcome;
            var phase = phases.FirstOrDefault(p => p.Contains(outcome.Name)) ?? [outcome.Name];
            if (!phase.All(arrived.ContainsKey)) return;
            foreach (string name in phase) Apply(arrived[name]);
        }, running => host.ShowStatus("GPC Engine · in corso: " + string.Join(" · ", running)), token, EnterAsync);
        token.ThrowIfCancellationRequested();
        if (requested.Contains("Taglio")) await StepAsync("Taglio", () => { host.ShowStatus("Verifiche a taglio…"); return ShearAsync(token); });
    }

    // The shear and torsion (C2/S5-c, 1.16.0): the rows prepared on the UI thread, the checks of ConcreteShearBatch on a worker thread in a place of
    // the limiter, on the copies; the outcomes written on the UI thread in the order of the rows. A failure that stops the checks (the links of the
    // section, an error outside the contract of the rows) keeps the rows calculated before it and stops the step, as the historic batch; a change of
    // the data meanwhile cancels the step and nothing is written.
    private async Task ShearAsync(CancellationToken token)
    {
        if (host.PrepareShear() is not { } request) return;
        var outcomes = new List<ConcreteShearOutcome>();
        Exception? failure = null;
        using (await EnterAsync(token))
        {
            try
            {
                await Task.Run(() => ConcreteShearBatch.Calculate(request.Input, request.Settings, request.Rows,
                    (index, calculated, error) => outcomes.Add(new(index, calculated, error)), token), token);
            }
            catch (Exception error) when (error is not OperationCanceledException) { failure = error; }
        }
        token.ThrowIfCancellationRequested();
        if (disposed) return;
        host.PublishShear(request, outcomes, failure is null);
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    // A place of the limiter for the batch or the shear (D-5): IsEngineRunning while it is held, set after the place is taken and cleared before it
    // is freed. Cancelled with the batch while it waits.
    private async Task<IDisposable> EnterAsync(CancellationToken token)
    {
        var place = await limiter.EnterAsync(token);
        IsEngineRunning = true;
        return new EnginePlace(this, place);
    }

    private sealed class EnginePlace(ConcreteCalculation owner, IDisposable place) : IDisposable
    {
        private int released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) != 0) return;
            owner.IsEngineRunning = false;
            place.Dispose();
        }
    }
}
