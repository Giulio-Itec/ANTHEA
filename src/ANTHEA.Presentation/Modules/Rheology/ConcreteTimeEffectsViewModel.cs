using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Anthea.Application.Materials;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using X.Core;

namespace Anthea.Presentation.Modules.Rheology;

public sealed record CementChoice(string Value, string Label);
public enum ConcreteTimeCurve { Creep, Modulus, ModulusFactor }
public sealed record CurveChoice(ConcreteTimeCurve Value, string Label);

/// <summary>Presentation state only. All material properties and time effects are evaluated by Model.</summary>
/// <remarks>
/// Review of 2/11/2026 (finding 14): with an automatic delay the curves are calculated again by themselves after an edit, as in the Actions
/// sheets; «Aggiorna le curve» stays for an immediate update and is disabled in a read-only revision; the plot is exported through
/// <see cref="IFileDialogService"/>, so that the checks can drive it.
/// </remarks>
public sealed partial class ConcreteTimeEffectsViewModel : ViewModelBase, IDataErrorInfo
{
    /// <summary>Delay of the automatic recalculation after an edit, the one of the Actions sheets.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(500);
    // The decimal separator chosen in «Aspetto» (the current culture), as in the other sheets; no thousands separator (review of 2/11/2026).
    private static CultureInfo Culture => CultureInfo.CurrentCulture;
    private readonly Dictionary<string, string> errors = new();
    private readonly RecalculationScheduler scheduler = new();
    private readonly IFileDialogService? files;
    private readonly IMessageService? messages;
    private RheologyEvaluation? calculation;
    // The input properties, in the order of their notifications (the insertion order of the former HashSet); immutable catalogue (F5.13 d, P3).
    private static readonly ImmutableArray<string> Inputs = [nameof(Fck), nameof(Humidity), nameof(NotionalSize),
        nameof(LoadingAge), nameof(Cement), nameof(StartAge), nameof(EndAge), nameof(Samples)];
    public IReadOnlyList<CementChoice> Cements { get; } = [
        new("ClassS", "S · lento"),
        new("ClassN", "N · normale"),
        new("ClassR", "R · rapido")];
    public IReadOnlyList<CurveChoice> Curves { get; } = [new(ConcreteTimeCurve.Creep, "Viscosità · φ(t, t₀)"),
        new(ConcreteTimeCurve.Modulus, "Modulo elastico · Ecm(t)"), new(ConcreteTimeCurve.ModulusFactor, "Fattore · Ecm(t) / Ecm,28")];
    private string fck = "35";
    public string Fck { get => fck; set { if (!IsReadOnly) SetProperty(ref fck, value); } }
    private string humidity = "70";
    public string Humidity { get => humidity; set { if (!IsReadOnly) SetProperty(ref humidity, value); } }
    private string notionalSize = "560";
    public string NotionalSize { get => notionalSize; set { if (!IsReadOnly) SetProperty(ref notionalSize, value); } }
    private string loadingAge = "10";
    public string LoadingAge { get => loadingAge; set { if (!IsReadOnly) SetProperty(ref loadingAge, value); } }
    private CementChoice cement = null!;
    public CementChoice Cement { get => cement; set { if (!IsReadOnly) SetProperty(ref cement, value); } }
    private string startAge = "0,5";
    public string StartAge { get => startAge; set { if (!IsReadOnly) SetProperty(ref startAge, value); } }
    private string endAge = "10000";
    public string EndAge { get => endAge; set { if (!IsReadOnly) SetProperty(ref endAge, value); } }
    private string samples = "160";
    public string Samples { get => samples; set { if (!IsReadOnly) SetProperty(ref samples, value); } }
    [ObservableProperty] private string queryAge = "365";
    [ObservableProperty] private bool logX = true;
    [ObservableProperty] private bool logY;
    [ObservableProperty] private CurveChoice selectedCurve = null!;
    [ObservableProperty] private IReadOnlyList<ConcreteTimePointView> points = [];
    [ObservableProperty] private ConcreteTimePointView? queryPoint;
    [ObservableProperty] private ConcreteTimePointView? selectedPoint;
    [ObservableProperty] private string status = "Curve non ancora calcolate.";
    [ObservableProperty] private string queryError = "";
    [ObservableProperty] private bool hasResults;
    [ObservableProperty] private bool isReadOnly;
    /// <summary>True while an automatic recalculation waits for its delay.</summary>
    [ObservableProperty] private bool isUpdating;
    public bool CanEdit => !IsReadOnly;
    partial void OnIsReadOnlyChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); CalculateCommand.NotifyCanExecuteChanged(); }
    public string Error => string.Join(" · ", errors.Values);
    public string this[string columnName] => errors.GetValueOrDefault(columnName, "");
    public string InfinityText => Format(calculation?.CreepInfinity, "0.000");
    public string E28Text => Format(calculation?.Ecm28Mpa, "0") + (HasResults ? " MPa" : "");
    public string QueryPhiText => Format(QueryPoint?.CreepCoefficient, "0.000");
    public string QueryEText => Format(QueryPoint?.EcmMpa, "0");
    public string QueryFactorText => Format(QueryPoint?.ModulusFactor, "0.000");
    public string ReferenceText => calculation is null ? "" : $"φ riferito a Ec = {1.05.ToString(Culture)} Ecm,28 = {calculation.CreepReferenceModulusMpa.ToString("0", Culture)} MPa.";
    public double? CreepInfinity => calculation?.CreepInfinity;
    public double? LoadingAgeDays => calculation?.LoadingAgeDays;
    public string GraphNote => SelectedCurve?.Value == ConcreteTimeCurve.Creep
        ? LogY ? "Scala logaritmica: φ = 0 prima del carico non è rappresentabile. La tabella conserva tutti i valori."
            : "Età t dal getto. φ = 0 fino all'applicazione del carico t₀. Rotella: zoom · doppio clic: adatta."
        : "Maturazione del modulo secante; Ecm(t) / Ecm,28 = 1 a 28 giorni. Rotella: zoom · doppio clic: adatta.";
    /// <summary>The message of the plot without curves: waiting for the automatic recalculation, data to correct, or curves to update.</summary>
    public string PlotMessage => IsUpdating ? "Aggiornamento delle curve…"
        : errors.Count > 0 ? "Nessuna curva: correggere i dati evidenziati." : "Aggiornare le curve per leggere i risultati.";
    public const string Assumptions = RheologyDocument.Assumptions;

    /// <param name="fckMpa">Strength proposed by the RC section that opens the window; null: the default.</param>
    /// <param name="automaticDelay">Delay of the automatic recalculation after an edit (<see cref="RecalculationDelay"/>); null: only «Aggiorna le curve».</param>
    /// <param name="files">Save dialog of «Esporta grafico…»; null: no export.</param>
    /// <param name="messages">Where a failed export is reported; null: the error goes on.</param>
    public ConcreteTimeEffectsViewModel(double? fckMpa = null, TimeSpan? automaticDelay = null, IFileDialogService? files = null, IMessageService? messages = null)
    {
        if (automaticDelay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(automaticDelay));
        cement = Cements[1]; selectedCurve = Curves[0]; AutomaticDelay = automaticDelay; this.files = files; this.messages = messages;
        if (fckMpa is not null) fck = fckMpa.Value.ToString("0.###", Culture);
        Recalculate();
    }

    /// <summary>Delay of the automatic recalculation; null when the curves are updated only on request.</summary>
    public TimeSpan? AutomaticDelay { get; }

    /// <summary>The automatic recalculation started by the last edit (completed when none, or when it is superseded).</summary>
    public Task PendingCalculation { get; private set; } = Task.CompletedTask;

    private static string Format(double? value, string format) => value?.ToString(format, Culture) ?? "—";
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (Inputs.Contains(e.PropertyName ?? ""))
        {
            errors.Clear(); ClearResults();
            Status = ScheduleRecalculation() ? "Dati modificati: aggiornamento delle curve…" : "Dati modificati: aggiornare le curve per leggere i risultati.";
        }
        if (e.PropertyName is nameof(LogX) or nameof(LogY) or nameof(SelectedCurve)) OnPropertyChanged(nameof(GraphNote));
        if (e.PropertyName is nameof(IsUpdating)) OnPropertyChanged(nameof(PlotMessage));
    }
    partial void OnQueryAgeChanged(string value) => ReadAge();
    partial void OnQueryPointChanged(ConcreteTimePointView? value)
    {
        OnPropertyChanged(nameof(QueryPhiText)); OnPropertyChanged(nameof(QueryEText)); OnPropertyChanged(nameof(QueryFactorText));
    }
    partial void OnSelectedPointChanged(ConcreteTimePointView? value)
    {
        // The age as the table shows it, not the 17 digits of the double (review of 2/11/2026, finding 12): the reading is at that age.
        if (value is not null) QueryAge = value.AgeText;
    }
    private void ClearResults()
    {
        calculation = null; HasResults = false; Points = []; QueryPoint = null; SelectedPoint = null; QueryError = "";
        NotifySummary();
    }
    private void NotifySummary()
    {
        foreach (string name in new[] { nameof(InfinityText), nameof(E28Text), nameof(ReferenceText), nameof(CreepInfinity), nameof(LoadingAgeDays), nameof(Error), nameof(PlotMessage) }) OnPropertyChanged(name);
        ReadAgeCommand.NotifyCanExecuteChanged();
    }
    /// <summary>
    /// After an edit, when the view has a thread of its own (a synchronization context) and the data can change: the curves are calculated
    /// again after <see cref="AutomaticDelay"/>; a later edit, «Aggiorna le curve» or closing supersedes the request. False otherwise.
    /// </summary>
    private bool ScheduleRecalculation()
    {
        if (AutomaticDelay is not { } delay || IsReadOnly || IsDisposed || SynchronizationContext.Current is null) return false;
        IsUpdating = true;
        PendingCalculation = RecalculateLaterAsync(delay);
        return true;
    }
    private async Task RecalculateLaterAsync(TimeSpan delay)
    {
        var completion = await scheduler.ScheduleAsync(_ => Task.FromResult(true), delay);
        if (!IsDisposed) completion.TryPublish(_ => Recalculate());
    }
    private double Parse(string text, string property, string label)
    {
        // Decimal comma or point, no implicit thousands separator or fallback to zero.
        if (double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)) return value;
        errors[property] = $"{label}: inserire un numero finito."; return double.NaN;
    }
    /// <summary>«Aggiorna le curve»: refused in a read-only revision, where the data cannot change.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Calculate() => Recalculate();

    /// <summary>The curves of the current data, now (opening of the sheet, «Aggiorna le curve», automatic recalculation); a pending automatic recalculation is superseded.</summary>
    public void Recalculate()
    {
        scheduler.Invalidate(); IsUpdating = false;
        ClearResults(); errors.Clear();
        double f = Parse(Fck, nameof(Fck), "fck"), rh = Parse(Humidity, nameof(Humidity), "Umidità"),
            h = Parse(NotionalSize, nameof(NotionalSize), "h₀"), t0 = Parse(LoadingAge, nameof(LoadingAge), "Età al carico"),
            first = Parse(StartAge, nameof(StartAge), "Età iniziale"), last = Parse(EndAge, nameof(EndAge), "Età finale"),
            count = Parse(Samples, nameof(Samples), "Campionamento");
        if (double.IsFinite(first) && first <= 0) errors[nameof(StartAge)] = "Età iniziale: deve essere positiva.";
        if (double.IsFinite(last) && last <= first) errors[nameof(EndAge)] = "Età finale: deve essere maggiore dell'età iniziale.";
        if (double.IsFinite(count) && (count != Math.Truncate(count) || count < 20 || count > 1000)) errors[nameof(Samples)] = "Campionamento: numero intero fra 20 e 1000.";
        if (Cement is null) errors[nameof(Cement)] = "Selezionare la classe di cemento.";
        if (errors.Count == 0)
        {
            try
            {
                var candidate = new RheologyEvaluation(f, Cement!.Value, rh, h, t0);
                var rows = candidate.Sample(first, last, count).Select(ConcreteTimePointView.Read).ToArray();
                calculation = candidate; HasResults = true; Points = rows;
                Status = $"Curve aggiornate · {rows.Length} punti · età in giorni dal getto.";
                ReadAge();
            }
            catch (ArgumentOutOfRangeException ex)
            {
                var item = ex.ParamName switch {
                    "fckMpa" => (nameof(Fck), "fck: campo di applicazione 12–90 MPa."),
                    "relativeHumidityPercent" => (nameof(Humidity), "Umidità: campo di applicazione 40–100 %."),
                    "notionalSizeMm" => (nameof(NotionalSize), "h₀: deve essere positivo, in mm."),
                    "loadingAgeDays" => (nameof(LoadingAge), "Età al carico: almeno 0,5 giorni."),
                    _ => (nameof(Cement), "Classe di cemento non valida.") };
                errors[item.Item1] = item.Item2;
            }
            catch (ArgumentException ex) { errors[nameof(EndAge)] = ex.Message; }
        }
        if (errors.Count > 0) Status = Error;
        // Refresh field validation even when only the command changed the error dictionary.
        foreach (string name in Inputs) base.OnPropertyChanged(new PropertyChangedEventArgs(name));
        NotifySummary();
    }
    [RelayCommand(CanExecute = nameof(HasResults))]
    private void ReadAge()
    {
        QueryPoint = null; QueryError = "";
        if (calculation is null) return;
        if (!double.TryParse(QueryAge.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double age) || !double.IsFinite(age) || age <= 0)
        { QueryError = "Età di lettura: inserire un numero positivo e finito."; return; }
        QueryPoint = ConcreteTimePointView.Read(calculation.AtAge(age));
    }
    /// <summary>
    /// «Esporta grafico…» (finding 14): the file chosen through the file service, the image drawn by the view; nothing without curves, without
    /// the service or when the dialog is cancelled. Allowed also in a read-only revision: it changes nothing. A failed write is reported.
    /// </summary>
    public void ExportPlot(Func<byte[]> png)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (!HasResults || files is null) return;
        string? path = files.SaveFile(new SaveFileRequest([new FileFilter("Immagine PNG", "*.png")], "Reologia-calcestruzzo.png", "Esporta grafico"));
        if (path is null) return;
        try { Archivio.ScriviAtomico(path, png()); }
        catch (Exception ex) when (messages is not null && ex is IOException or UnauthorizedAccessException)
        { messages.ReportFailure("Esportazione non completata", ex); }
    }
    protected override void OnDisposed() => scheduler.Dispose();
}


public sealed record ConcreteTimePointView(double AgeDays, double CreepCoefficient, double EcmMpa, double ModulusFactor)
{
    // Cells of the table with the decimal separator chosen in «Aspetto»; the columns sort on the numbers.
    public string AgeText => AgeDays.ToString("0.###", CultureInfo.CurrentCulture);
    public string CreepText => CreepCoefficient.ToString("0.0000", CultureInfo.CurrentCulture);
    public string EcmText => EcmMpa.ToString("0.0", CultureInfo.CurrentCulture);
    public string ModulusFactorText => ModulusFactor.ToString("0.0000", CultureInfo.CurrentCulture);
    internal static ConcreteTimePointView Read(RheologyPoint value)
        => new(value.AgeDays, value.CreepCoefficient, value.EcmMpa, value.ModulusFactor);
}
