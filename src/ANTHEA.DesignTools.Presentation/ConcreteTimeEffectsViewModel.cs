using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GPC.Model.Materials;

namespace Anthea.DesignTools.Presentation;

public sealed record CementChoice(ConcreteMaterialEuropeanCommon.CementTypes Value, string Label);
public enum ConcreteTimeCurve { Creep, Modulus, ModulusFactor }
public sealed record CurveChoice(ConcreteTimeCurve Value, string Label);

/// <summary>Presentation state only. All material properties and time effects are evaluated by Model.</summary>
public sealed partial class ConcreteTimeEffectsViewModel : ObservableObject, IDataErrorInfo
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private readonly Dictionary<string, string> errors = new();
    private ConcreteTimeEffects? calculation;
    private static readonly HashSet<string> Inputs = [nameof(Fck), nameof(Humidity), nameof(NotionalSize),
        nameof(LoadingAge), nameof(Cement), nameof(StartAge), nameof(EndAge), nameof(Samples)];
    public IReadOnlyList<CementChoice> Cements { get; } = [
        new(ConcreteMaterialEuropeanCommon.CementTypes.ClassS, "S · lento"),
        new(ConcreteMaterialEuropeanCommon.CementTypes.ClassN, "N · normale"),
        new(ConcreteMaterialEuropeanCommon.CementTypes.ClassR, "R · rapido")];
    public IReadOnlyList<CurveChoice> Curves { get; } = [new(ConcreteTimeCurve.Creep, "Viscosità · φ(t, t₀)"),
        new(ConcreteTimeCurve.Modulus, "Modulo elastico · Ecm(t)"), new(ConcreteTimeCurve.ModulusFactor, "Fattore · Ecm(t) / Ecm,28")];
    [ObservableProperty] private string fck = "35";
    [ObservableProperty] private string humidity = "70";
    [ObservableProperty] private string notionalSize = "560";
    [ObservableProperty] private string loadingAge = "10";
    [ObservableProperty] private CementChoice cement = null!;
    [ObservableProperty] private string startAge = "0,5";
    [ObservableProperty] private string endAge = "10000";
    [ObservableProperty] private string samples = "160";
    [ObservableProperty] private string queryAge = "365";
    [ObservableProperty] private bool logX = true;
    [ObservableProperty] private bool logY;
    [ObservableProperty] private CurveChoice selectedCurve = null!;
    [ObservableProperty] private IReadOnlyList<ConcreteTimePoint> points = [];
    [ObservableProperty] private ConcreteTimePoint? queryPoint;
    [ObservableProperty] private ConcreteTimePoint? selectedPoint;
    [ObservableProperty] private string status = "Imposta i dati e aggiorna le curve.";
    [ObservableProperty] private string queryError = "";
    [ObservableProperty] private bool hasResults;
    public string Error => string.Join(" · ", errors.Values);
    public string this[string columnName] => errors.GetValueOrDefault(columnName, "");
    public string InfinityText => Format(calculation?.CreepInfinity, "0.000");
    public string E28Text => Format(calculation?.Ecm28Mpa, "N0") + (HasResults ? " MPa" : "");
    public string QueryPhiText => Format(QueryPoint?.CreepCoefficient, "0.000");
    public string QueryEText => Format(QueryPoint?.EcmMpa, "N0");
    public string QueryFactorText => Format(QueryPoint?.ModulusFactor, "0.000");
    public string ReferenceText => calculation is null ? "" : $"φ riferito a Ec = 1,05 Ecm,28 = {calculation.CreepReferenceModulusMpa.ToString("N0", Italian)} MPa.";
    public double? CreepInfinity => calculation?.CreepInfinity;
    public double? LoadingAgeDays => calculation?.LoadingAgeDays;
    public string GraphNote => SelectedCurve?.Value == ConcreteTimeCurve.Creep
        ? LogY ? "Scala logaritmica: φ = 0 prima del carico non è rappresentabile. La tabella conserva tutti i valori."
            : "Età t dal getto. φ = 0 fino all'applicazione del carico t₀. Rotella: zoom · doppio clic: adatta."
        : "Maturazione del modulo secante; Ecm(t) / Ecm,28 = 1 a 28 giorni. Rotella: zoom · doppio clic: adatta.";
    public const string Assumptions = "EN 1992-1-1:2004 · allegato B e §3.1.3. Calcestruzzo ordinario, 20 °C, umidità costante e viscosità lineare. Ecm(t) descrive la maturazione: non è il modulo efficace ridotto per viscosità. Ritiro e storia termica esclusi.";

    public ConcreteTimeEffectsViewModel(double? fckMpa = null)
    {
        cement = Cements[1]; selectedCurve = Curves[0];
        if (fckMpa is not null) fck = fckMpa.Value.ToString("0.###", Italian);
        Calculate();
    }
    private static string Format(double? value, string format) => value?.ToString(format, Italian) ?? "—";
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (Inputs.Contains(e.PropertyName ?? ""))
        {
            errors.Clear(); ClearResults();
            Status = "Dati modificati: aggiorna le curve per leggere i risultati.";
        }
        if (e.PropertyName is nameof(LogX) or nameof(LogY) or nameof(SelectedCurve)) OnPropertyChanged(nameof(GraphNote));
    }
    partial void OnQueryAgeChanged(string value) => ReadAge();
    partial void OnQueryPointChanged(ConcreteTimePoint? value)
    {
        OnPropertyChanged(nameof(QueryPhiText)); OnPropertyChanged(nameof(QueryEText)); OnPropertyChanged(nameof(QueryFactorText));
    }
    partial void OnSelectedPointChanged(ConcreteTimePoint? value)
    {
        if (value is not null) QueryAge = value.AgeDays.ToString("G17", Italian);
    }
    private void ClearResults()
    {
        calculation = null; HasResults = false; Points = []; QueryPoint = null; SelectedPoint = null; QueryError = "";
        NotifySummary();
    }
    private void NotifySummary()
    {
        foreach (string name in new[] { nameof(InfinityText), nameof(E28Text), nameof(ReferenceText), nameof(CreepInfinity), nameof(LoadingAgeDays), nameof(Error) }) OnPropertyChanged(name);
        ReadAgeCommand.NotifyCanExecuteChanged();
    }
    private double Parse(string text, string property, string label)
    {
        // Decimal comma or point, no implicit thousands separator or fallback to zero.
        if (double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)) return value;
        errors[property] = $"{label}: inserire un numero finito."; return double.NaN;
    }
    [RelayCommand]
    private void Calculate()
    {
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
                var candidate = new ConcreteTimeEffects(f, Cement!.Value, rh, h, t0);
                // Sampling and scale projection serve the chart; no material formula is implemented here.
                var ages = new SortedSet<double> { first, last };
                for (int i = 0; i < (int)count; i++)
                {
                    double p = i / (count - 1);
                    ages.Add(Math.Clamp(Math.Exp(Math.Log(first) * (1 - p) + Math.Log(last) * p), first, last));
                    if (last > t0)
                    {
                        double elapsed = Math.Exp(Math.Log(1e-4) * (1 - p) + Math.Log(Math.Max(1e-4, last - t0)) * p);
                        double age = t0 + elapsed;
                        if (age >= first && age <= last) ages.Add(age);
                    }
                }
                foreach (double age in new[] { t0, 28d }) if (age >= first && age <= last) ages.Add(age);
                var rows = ages.Select(candidate.AtAge).ToArray();
                if (rows.Any(p => !double.IsFinite(p.CreepCoefficient) || !double.IsFinite(p.EcmMpa) || !double.IsFinite(p.ModulusFactor)))
                    throw new ArgumentException("L'intervallo scelto non produce valori rappresentabili: ridurre le età estreme.");
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
        QueryPoint = calculation.AtAge(age);
    }
}
