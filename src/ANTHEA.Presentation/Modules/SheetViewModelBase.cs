using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Mvvm;

namespace Anthea.Presentation.Modules;

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
/// </list>
/// </summary>
public abstract class SheetViewModelBase : ViewModelBase, ISheetViewModel
{
    private readonly SheetCalculation<ModuleCalculation> calculation;
    private bool readOnly;
    private long revision;

    /// <param name="moduleId">Module of the sheet; the calculation service of the context must belong to it.</param>
    /// <param name="context">Data (owned copy), host, services, calculation and limiter.</param>
    /// <param name="automaticDelay">Delay of the automatic recalculation after an edit (450 ms for the vertical piles…); null: only «Calcola».</param>
    protected SheetViewModelBase(string moduleId, SheetContext context, TimeSpan? automaticDelay = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleId);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Calculation.ModuleId != moduleId)
            throw new ArgumentException($"Servizio di calcolo del modulo {context.Calculation.ModuleId} per un foglio del modulo {moduleId}.", nameof(context));
        ModuleId = moduleId; Context = context; Data = context.Data; readOnly = context.ReadOnly; AutomaticDelay = automaticDelay;
        Guard = new CommandGuard(context.Services.Messages);
        calculation = SheetCalculation.ForModule(context.Calculation, automaticDelay ?? TimeSpan.Zero, context.Limiter);
        calculation.PropertyChanged += OnCalculationChanged;
    }

    public string ModuleId { get; }

    public JsonObject Data { get; }

    public long Revision => revision;

    /// <summary>Delay of the automatic recalculation, null when the module calculates only on request.</summary>
    public TimeSpan? AutomaticDelay { get; }

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

    /// <summary>Calculates the current data (the «Calcola» command, allowed also when read-only). True when this request was published.</summary>
    public Task<bool> CalculateAsync(CancellationToken cancellation = default)
    {
        ThrowIfDisposed();
        return calculation.CalculateAsync(Data, cancellation);
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

    /// <summary>Starts the automatic recalculation with the delay of the module (for example at opening, as the vertical piles).</summary>
    protected Task<bool> ScheduleCalculationAsync()
    {
        ThrowIfDisposed();
        return PendingCalculation = calculation.ScheduleAsync(Data);
    }

    /// <summary>Writes the drafts of the fields into <see cref="Data"/>; nothing by default.</summary>
    protected virtual void OnCommit() { }

    /// <summary>The data were replaced: discard drafts and rebuild what depends on the data; nothing by default.</summary>
    protected virtual void OnReloaded() { }

    protected override void OnDisposed()
    {
        calculation.PropertyChanged -= OnCalculationChanged;
        calculation.Dispose();
        Modified = null;
        OnPropertyChanged(nameof(CanEdit));
    }

    private void NewRevision()
    {
        revision++;
        calculation.Invalidate();
        OnPropertyChanged(nameof(Revision));
        if (AutomaticDelay is not null) PendingCalculation = calculation.ScheduleAsync(Data);
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
