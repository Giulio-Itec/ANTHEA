using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Calculation;

/// <summary>State of the calculation of a sheet, as the views show it.</summary>
public enum CalculationState
{
    /// <summary>No calculation requested since the data changed.</summary>
    Idle,
    /// <summary>Requested, waiting for the delay of the module, the previous calculation or a free place of the limiter.</summary>
    Pending,
    /// <summary>The engine is working on a copy of the data.</summary>
    Running,
    /// <summary>Results of the current data.</summary>
    Completed,
    /// <summary>The engine reported missing or invalid data («dati da completare»).</summary>
    Incomplete,
    /// <summary>The engine threw; <see cref="SheetCalculation{TResult}.Message"/> holds the text of the error.</summary>
    Failed,
    /// <summary>The caller cancelled the request (cancel command, sheet closing).</summary>
    Cancelled
}

/// <summary>Verdict of a result: missing data (empty when complete) and warnings, in the order of the engine.</summary>
public sealed record CalculationReview(string Missing, IReadOnlyList<string> Warnings)
{
    public bool IsComplete => Missing.Length == 0;
}

/// <summary>
/// Calculation scheduling of a sheet (proposal F5, section 3.1): the existing <see cref="RecalculationScheduler"/> of
/// ANTHEA.Application (latest request wins, cooperative cancellation, serial execution, publication of the current revision
/// only) around an existing calculation (<see cref="ModuleCalculationSession"/>, or another session of the application such
/// as ConcreteAnalysisSession). No calculation logic of its own: it copies the data, runs the engine on a background thread,
/// optionally through the <see cref="CalculationLimiter"/> of the application, and publishes state, result and warnings.
/// Requests and publications happen on the calling thread (the UI thread): the scheduler keeps the synchronization context.
/// </summary>
public sealed class SheetCalculation<TResult> : ObservableObject, IDisposable where TResult : class
{
    private readonly RecalculationScheduler scheduler = new();
    private readonly Func<JsonObject, CancellationToken, TResult> calculate;
    private readonly Func<TResult, CalculationReview> review;
    private readonly CalculationLimiter? limiter;
    private readonly Action? release;
    private long request;
    private bool disposed;
    private CalculationState state;
    private TResult? result;
    private string message = "";
    private IReadOnlyList<string> warnings = [];

    /// <param name="calculate">The existing calculation, called on a background thread with an owned copy of the data.</param>
    /// <param name="review">Missing data and warnings of a result, called on the calling thread at publication.</param>
    /// <param name="delay">Delay of the automatic recalculation of the module (450 ms vertical piles, 180 ms pile group…), unchanged.</param>
    /// <param name="limiter">Limit shared by the sheets of the application; null runs without limit.</param>
    /// <param name="release">Releases what the calculation owns (for example the cache of the session) at <see cref="Dispose"/>.</param>
    public SheetCalculation(Func<JsonObject, CancellationToken, TResult> calculate, Func<TResult, CalculationReview> review, TimeSpan delay,
        CalculationLimiter? limiter = null, Action? release = null)
    {
        ArgumentNullException.ThrowIfNull(calculate);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        this.calculate = calculate; this.review = review; this.limiter = limiter; this.release = release;
        Delay = delay;
    }

    public TimeSpan Delay { get; }

    public CalculationState State
    {
        get => state;
        private set { if (SetProperty(ref state, value)) OnPropertyChanged(nameof(IsBusy)); }
    }

    /// <summary>Result of the current data, null unless <see cref="State"/> is <see cref="CalculationState.Completed"/>.</summary>
    public TResult? Result
    {
        get => result;
        private set { if (SetProperty(ref result, value)) OnPropertyChanged(nameof(HasResult)); }
    }

    /// <summary>Missing data (Incomplete) or text of the error (Failed); empty otherwise.</summary>
    public string Message { get => message; private set => SetProperty(ref message, value); }

    public IReadOnlyList<string> Warnings { get => warnings; private set => SetProperty(ref warnings, value); }

    /// <summary>True while the engine runs (the Busy of the historic views).</summary>
    public bool IsBusy => State == CalculationState.Running;

    public bool HasResult => Result is not null;

    /// <summary>Raised after the publication of the current request (completed, incomplete or failed).</summary>
    public event EventHandler? Published;

    /// <summary>Automatic recalculation after an edit, with the delay of the module. True when this request was published.</summary>
    public Task<bool> ScheduleAsync(JsonObject data, CancellationToken cancellation = default) => StartAsync(data, Delay, cancellation);

    /// <summary>Calculation requested by the user (Calcola), without delay. True when this request was published.</summary>
    public Task<bool> CalculateAsync(JsonObject data, CancellationToken cancellation = default) => StartAsync(data, TimeSpan.Zero, cancellation);

    /// <summary>The data changed: cancels the running request and clears the results, as SheetEditor.Changed does.</summary>
    public void Invalidate()
    {
        if (disposed) return;
        request++;
        scheduler.Invalidate();
        Result = null; Message = ""; Warnings = []; State = CalculationState.Idle;
    }

    /// <summary>Sheet closed: cancels the running request; a late completion is never published.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        request++;
        scheduler.Dispose();
        release?.Invoke();
    }

    // Arguments and closing are checked, and the copy is taken, synchronously on the thread that owns the document: the engine
    // never sees the live JsonObject and a closed sheet refuses the request at once.
    private Task<bool> StartAsync(JsonObject data, TimeSpan delay, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(data);
        ObjectDisposedException.ThrowIf(disposed, this);
        var snapshot = (JsonObject)data.DeepClone();
        long current = ++request;
        State = CalculationState.Pending;
        return RunAsync(snapshot, current, delay, cancellation);
    }

    private async Task<bool> RunAsync(JsonObject snapshot, long current, TimeSpan delay, CancellationToken cancellation)
    {
        var completed = await scheduler.ScheduleAsync(async token =>
        {
            using var place = limiter is null ? null : await limiter.EnterAsync(token);
            if (current == request) State = CalculationState.Running;
            return await Task.Run(() => calculate(snapshot, token), token);
        }, delay, cancellation);
        bool published = completed.TryPublish(Publish);
        if (!published && current == request && !disposed) State = CalculationState.Cancelled;
        return published;
    }

    private void Publish(ScheduledCalculation<TResult> completed)
    {
        CalculationReview? verdict = null;
        Exception? error = completed.Error;
        if (error is null)
        {
            try { verdict = review(completed.Value!); }
            catch (Exception reviewError) { error = reviewError; }
        }
        if (error is not null) { Result = null; Warnings = []; Message = error.Message; State = CalculationState.Failed; }
        else if (!verdict!.IsComplete) { Result = null; Warnings = verdict.Warnings; Message = verdict.Missing; State = CalculationState.Incomplete; }
        else { Result = completed.Value; Warnings = verdict.Warnings; Message = ""; State = CalculationState.Completed; }
        Published?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Calculation of a sheet through the module services of ANTHEA.Application.</summary>
public static class SheetCalculation
{
    /// <summary>
    /// <see cref="IModuleService"/> with the per-document cache of <see cref="ModuleCalculationSession"/>, as SheetEditor uses it;
    /// the cache is cleared when the calculation is disposed.
    /// </summary>
    public static SheetCalculation<ModuleCalculation> ForModule(IModuleService service, TimeSpan delay, CalculationLimiter? limiter = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        var session = new ModuleCalculationSession(service);
        return new SheetCalculation<ModuleCalculation>(session.Calculate, Review, delay, limiter, session.Clear);
    }

    /// <summary>
    /// The «errore» of the values is missing data (as <see cref="ModuleCalculation.HasError"/>), «avvisi» are the warnings,
    /// read as SheetEditor reads them.
    /// </summary>
    public static CalculationReview Review(ModuleCalculation calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        string missing = calculation.Values["errore"]?.ToString() ?? "";
        string[] warnings = calculation.Values["avvisi"] is JsonArray list ? list.Select(v => v?.ToString() ?? "").ToArray() : [];
        return new CalculationReview(missing, warnings);
    }
}
