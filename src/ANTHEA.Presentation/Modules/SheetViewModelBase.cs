using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Mvvm;

namespace Anthea.Presentation.Modules;

/// <summary>Where the sheets of a module calculate (proposal F5, section 3.1; decision Q8).</summary>
public enum SheetCalculationMode
{
    /// <summary>On a copy of the data, on a background thread, through the limiter of the application: the rule of the modules.</summary>
    Background,
    /// <summary>
    /// On the UI thread, as the historic view (Q8 a, F5.4: the short calculation of the pile group): «Calcola» returns with the
    /// result published and the automatic recalculation runs on the UI thread after the delay of the module.
    /// </summary>
    Synchronous,
    /// <summary>
    /// No calculation is published (F5.5, the material sheets): the values shown are a presentation of the data that the sheet
    /// computes after every edit; «Calcola» publishes nothing (the host commits the sheet) and there is no automatic
    /// recalculation, as in the historic material views. <see cref="SheetViewModelBase.CalculateModule"/> stays available.
    /// </summary>
    None
}

/// <summary>
/// Common implementation of <see cref="ISheetViewModel"/> (proposal F5, sections 3.1 and 5.1) on
/// <see cref="SheetCalculation{TResult}"/>: the existing <see cref="IModuleService"/> of the module with the cache of
/// <see cref="ModuleCalculationSession"/>, as SheetEditor uses it. No calculation logic of its own.
/// <list type="bullet">
/// <item>Edits go through <see cref="Edit"/>: refused when read-only or closed; otherwise the data are written and
/// <see cref="Changed"/> runs, the equivalent of SheetEditor.Changed: new revision, results invalidated (a running calculation
/// is never published), automatic recalculation when the module has a delay, <see cref="Modified"/>.</item>
/// <item>Only the calculation of the current revision is published (latest request wins, <see cref="SheetCalculation{TResult}"/>).</item>
/// <item>Closing (<see cref="ViewModelBase.Dispose"/>) cancels the running calculation, which is never published, and clears the cache.</item>
/// <item>With <see cref="SheetCalculationMode.Synchronous"/> (decision Q8 a) the module calculates on the UI thread, as its historic
/// view: «Calcola» returns with the result published, the automatic recalculation runs on the UI thread after the delay, and the
/// module prepares the data and reads the result in <see cref="CalculateSynchronously"/>.</item>
/// <item>With <see cref="SheetCalculationMode.None"/> (F5.5) nothing is calculated or published: no result, no automatic
/// recalculation.</item>
/// <item><see cref="PreviewData"/> is <see cref="Data"/>; a sheet that keeps its edits until Commit gives its state instead (F5.5).</item>
/// </list>
/// </summary>
public abstract class SheetViewModelBase : ViewModelBase, ISheetViewModel
{
    private readonly ModuleCalculationSession session;
    private readonly SheetCalculation<ModuleCalculation> calculation;
    private ModuleCalculation? reviewed;
    private bool readOnly;
    private long revision;

    /// <param name="moduleId">Module of the sheet; the calculation service of the context must belong to it.</param>
    /// <param name="context">Data (owned copy), host, services, calculation and limiter.</param>
    /// <param name="automaticDelay">Delay of the automatic recalculation after an edit (450 ms for the vertical piles…); null: only «Calcola».</param>
    /// <param name="mode">Where the module calculates: on a background thread (the rule), or on the UI thread (Q8 a).</param>
    protected SheetViewModelBase(string moduleId, SheetContext context, TimeSpan? automaticDelay = null, SheetCalculationMode mode = SheetCalculationMode.Background)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleId);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Calculation.ModuleId != moduleId)
            throw new ArgumentException($"Servizio di calcolo del modulo {context.Calculation.ModuleId} per un foglio del modulo {moduleId}.", nameof(context));
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == SheetCalculationMode.None && automaticDelay is not null)
            throw new ArgumentException("Un foglio senza calcolo pubblicato non ha ricalcolo automatico.", nameof(automaticDelay));
        ModuleId = moduleId; Context = context; Data = context.Data; readOnly = context.ReadOnly; AutomaticDelay = automaticDelay; CalculationMode = mode;
        Guard = new CommandGuard(context.Services.Messages);
        // As SheetCalculation.ForModule: the module service with the per-document cache of the session, cleared at closing.
        session = new ModuleCalculationSession(context.Calculation);
        calculation = new SheetCalculation<ModuleCalculation>(session.Calculate, Review, automaticDelay ?? TimeSpan.Zero, context.Limiter, session.Clear);
        calculation.PropertyChanged += OnCalculationChanged;
        calculation.Published += OnPublished;
    }

    public string ModuleId { get; }

    public JsonObject Data { get; }

    /// <summary>The data: the edits are written at once. A sheet that keeps its edits until <see cref="Commit"/> gives its state (F5.5).</summary>
    public virtual JsonObject PreviewData => Data;

    public long Revision => revision;

    /// <summary>Delay of the automatic recalculation, null when the module calculates only on request.</summary>
    public TimeSpan? AutomaticDelay { get; }

    /// <summary>Where the module calculates (decision Q8).</summary>
    public SheetCalculationMode CalculationMode { get; }

    public bool IsBusy => calculation.IsBusy;

    public JsonObject? Result => calculation.Result?.Values;

    public bool HasResults => Result is not null;

    /// <summary>State of the calculation, for the status line of the view.</summary>
    public CalculationState CalculationState => calculation.State;

    /// <summary>Missing data or text of the error of the last calculation; empty otherwise.</summary>
    public string CalculationMessage => calculation.Message;

    /// <summary>Warnings of the last result.</summary>
    public IReadOnlyList<string> Warnings => calculation.Warnings;

    public bool IsReadOnly
    {
        get => readOnly;
        set { if (SetProperty(ref readOnly, value)) OnPropertyChanged(nameof(CanEdit)); }
    }

    /// <summary>False when read-only or closed: the views disable the fields, <see cref="Edit"/> refuses.</summary>
    public bool CanEdit => !readOnly && !IsDisposed;

    /// <summary>The automatic recalculation started by the last change (completed when none); true when it was published.</summary>
    public Task<bool> PendingCalculation { get; private set; } = Task.FromResult(false);

    public event EventHandler? Modified;

    public abstract IModuleReportSource Report { get; }

    /// <summary>Context of the sheet: host, services, calculation and limiter.</summary>
    protected SheetContext Context { get; }

    /// <summary>Error policy of the commands of the sheet (Q19): failures shown through the message service of the context.</summary>
    protected CommandGuard Guard { get; }

    public void Commit()
    {
        ThrowIfDisposed();
        OnCommit();
    }

    /// <summary>
    /// Calculates the current data (the «Calcola» command, allowed also when read-only). True when this request was published.
    /// In synchronous mode the task is already completed when it is returned: the result is published on this thread. Without
    /// calculation (<see cref="SheetCalculationMode.None"/>) nothing is calculated: completed, false.
    /// </summary>
    public Task<bool> CalculateAsync(CancellationToken cancellation = default)
    {
        ThrowIfDisposed();
        return CalculationMode switch
        {
            SheetCalculationMode.Synchronous => Task.FromResult(CalculateNow()),
            SheetCalculationMode.None => Task.FromResult(false),
            _ => calculation.CalculateAsync(Data, cancellation)
        };
    }

    Task ISheetViewModel.CalculateAsync(CancellationToken cancellation) => CalculateAsync(cancellation);

    public void Reload(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ThrowIfDisposed();
        var copy = (JsonObject)data.DeepClone();
        Data.Clear();
        foreach (string key in copy.Select(p => p.Key).ToArray())
        {
            var value = copy[key];
            copy.Remove(key);
            Data[key] = value;
        }
        OnReloaded();
        NewRevision();
        OnPropertyChanged(nameof(Data));
    }

    /// <summary>
    /// Applies a user edit: refused (false) when read-only or closed; otherwise <paramref name="write"/> changes <see cref="Data"/>
    /// and <see cref="Changed"/> follows.
    /// </summary>
    protected bool Edit(Action<JsonObject> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (!CanEdit) return false;
        write(Data);
        Changed();
        return true;
    }

    /// <summary>The user changed the data (SheetEditor.Changed): new revision, results invalidated, automatic recalculation, <see cref="Modified"/>.</summary>
    protected void Changed()
    {
        ThrowIfDisposed();
        NewRevision();
        Modified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The user changed interface state saved in the data that the published results already reflect (the selected result of
    /// the pile group): new revision and <see cref="Modified"/>; the results stay and nothing is recalculated.
    /// </summary>
    protected void ChangedKeepingResults()
    {
        ThrowIfDisposed();
        revision++;
        OnPropertyChanged(nameof(Revision));
        Modified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Starts the automatic recalculation with the delay of the module (for example at opening, as the vertical piles).</summary>
    protected Task<bool> ScheduleCalculationAsync()
    {
        ThrowIfDisposed();
        if (CalculationMode == SheetCalculationMode.None)
            throw new InvalidOperationException("Ricalcolo automatico chiesto da un foglio senza calcolo pubblicato.");
        return PendingCalculation = ScheduleRecalculationAsync();
    }

    /// <summary>
    /// Synchronous mode (Q8 a): calculates the current data now, on this thread, cancelling the pending automatic recalculation;
    /// the result is published before returning. True when it was published.
    /// </summary>
    protected bool CalculateNow()
    {
        ThrowIfDisposed();
        if (CalculationMode != SheetCalculationMode.Synchronous)
            throw new InvalidOperationException("Calcolo sincrono chiesto da un foglio che calcola sullo sfondo: usare CalculateAsync.");
        return calculation.CalculateNow(CalculateSynchronously);
    }

    /// <summary>
    /// Synchronous mode (Q8 a): the calculation of the current data on the UI thread, by default the module service through the
    /// cache of the sheet. A module overrides it to prepare the data before and to read the result after, as its historic view
    /// did in the same call; an exception is published as the error of the calculation.
    /// </summary>
    protected virtual ModuleCalculation CalculateSynchronously() => CalculateModule(Data);

    /// <summary>
    /// The calculation published last (F5.7): the value of the service of the module, also when the result is incomplete, so that a
    /// module reads its typed part (<see cref="ModuleCalculation.Native"/>) in <see cref="OnCalculationPublished"/>; null after a
    /// failure or a change of the data. Nothing changes for the modules that do not read it.
    /// </summary>
    protected ModuleCalculation? PublishedCalculation { get; private set; }

    /// <summary>
    /// Cancels the pending or running calculation without clearing the published result (F5.7: the horizontal pile when the elastic tab
    /// is chosen, as its historic view): the cancelled request is never published.
    /// </summary>
    protected void CancelCalculation()
    {
        ThrowIfDisposed();
        calculation.Cancel();
    }

    /// <summary>The calculation service of the module on <paramref name="data"/>, through the per-document cache of the sheet (the session copies the data).</summary>
    protected ModuleCalculation CalculateModule(JsonObject data) => session.Calculate(data);

    /// <summary>After every publication of a calculation of the current data (completed, incomplete or failed), on the UI thread; nothing by default.</summary>
    protected virtual void OnCalculationPublished() { }

    /// <summary>Writes the drafts of the fields into <see cref="Data"/>; nothing by default.</summary>
    protected virtual void OnCommit() { }

    /// <summary>The data were replaced: discard drafts and rebuild what depends on the data; nothing by default.</summary>
    protected virtual void OnReloaded() { }

    protected override void OnDisposed()
    {
        calculation.PropertyChanged -= OnCalculationChanged;
        calculation.Published -= OnPublished;
        calculation.Dispose();
        Modified = null;
        OnPropertyChanged(nameof(CanEdit));
    }

    private void NewRevision()
    {
        revision++;
        PublishedCalculation = null;
        calculation.Invalidate();
        OnPropertyChanged(nameof(Revision));
        if (AutomaticDelay is not null) PendingCalculation = ScheduleRecalculationAsync();
    }

    private Task<bool> ScheduleRecalculationAsync() => CalculationMode == SheetCalculationMode.Synchronous
        ? calculation.ScheduleNowAsync(CalculateSynchronously) : calculation.ScheduleAsync(Data);

    // The value reviewed for the publication under way (on the calling thread, just before it): kept for PublishedCalculation.
    private CalculationReview Review(ModuleCalculation value)
    {
        reviewed = value;
        return SheetCalculation.Review(value);
    }

    private void OnPublished(object? sender, EventArgs e)
    {
        PublishedCalculation = CalculationState == CalculationState.Failed ? null : reviewed;
        reviewed = null;
        OnCalculationPublished();
    }

    private void OnCalculationChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SheetCalculation<ModuleCalculation>.State):
                OnPropertyChanged(nameof(CalculationState)); OnPropertyChanged(nameof(IsBusy)); break;
            case nameof(SheetCalculation<ModuleCalculation>.Result):
                OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults)); break;
            case nameof(SheetCalculation<ModuleCalculation>.Message):
                OnPropertyChanged(nameof(CalculationMessage)); break;
            case nameof(SheetCalculation<ModuleCalculation>.Warnings):
                OnPropertyChanged(nameof(Warnings)); break;
        }
    }
}
