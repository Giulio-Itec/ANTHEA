using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> ColorScaleModes { get; } = ["Automatica · elementi visibili", "Globale · caso e componente", "Simmetrica rispetto a zero", "Limiti manuali"];
    [ObservableProperty] private int selectedColorScale;
    [ObservableProperty] private string colorScaleLower = "";
    [ObservableProperty] private string colorScaleUpper = "";
    [ObservableProperty] private string colorScaleMessage = "";
    public bool IsManualColorScale => SelectedColorScale == 3;
    public double ColorMinimum { get; private set; }
    public double ColorMaximum { get; private set; }
    public string ColorMinimumLabel { get; private set; } = "—";
    public string ColorMaximumLabel { get; private set; } = "—";
    public string ColorMiddleLabel => HasResults ? (ColorMinimum / 2 + ColorMaximum / 2).ToString("G5") : "—";
    public string VisibleResultRange => HasResults ? (IsAveragedContour ? $"Grafica mediata: {ContourValues.Min(v => v.Value):G6} … {ContourValues.Max(v => v.Value):G6}\nImportati: {Minimum} … {Maximum}" : $"Valori visibili: {Minimum} … {Maximum}") : "Nessun valore visibile";
    public bool HasColorScaleMessage => ColorScaleMessage.Length > 0;
    private string? colorFieldKey, colorUnitKey;
    private double manualMinimum, manualMaximum, currentDisplayFactor = 1;
    private bool manualScaleReady;

    partial void OnSelectedColorScaleChanged(int value)
    {
        OnPropertyChanged(nameof(IsManualColorScale)); ColorScaleMessage = ""; RefreshScene();
    }
    partial void OnColorScaleMessageChanged(string value) => OnPropertyChanged(nameof(HasColorScaleMessage));
    void ResetColorScale()
    {
        colorFieldKey = colorUnitKey = null; manualScaleReady = false;
        SelectedColorScale = 0; ColorScaleMessage = "";
    }
    void UpdateColorScale(IReadOnlyList<ResultValue> rawAtStation, double factor)
    {
        string key = $"{Family}/{Axes}/{SelectedComponent}";
        if (colorFieldKey != key)
        {
            bool previous = updating; updating = true;
            if (colorFieldKey != null && IsManualColorScale) SelectedColorScale = 0;
            updating = previous; colorFieldKey = key; colorUnitKey = null; manualScaleReady = false; ColorScaleMessage = "";
        }
        currentDisplayFactor = factor;
        double low = HasResults ? ContourValues.Min(v => v.Value) : 0, high = HasResults ? ContourValues.Max(v => v.Value) : 0;
        if (IsManualColorScale && !manualScaleReady && HasResults)
        {
            double span = high == low ? Math.Max(1, Math.Abs(low) * .1) : 0;
            manualMinimum = (low - span) / factor; manualMaximum = (high + span) / factor; manualScaleReady = true;
        }
        string units = IsDisplacementResult ? DisplacementUnit : $"{ForceUnit}/{LengthUnit}";
        if (colorUnitKey != units && manualScaleReady)
        {
            ColorScaleLower = (manualMinimum * factor).ToString("G9"); ColorScaleUpper = (manualMaximum * factor).ToString("G9"); colorUnitKey = units;
        }
        switch (SelectedColorScale)
        {
            case 1 when rawAtStation.Count > 0:
                low = rawAtStation.Min(v => v.Value) * factor; high = rawAtStation.Max(v => v.Value) * factor; break;
            case 2:
                double extent = Math.Max(Math.Abs(low), Math.Abs(high)); low = -extent; high = extent; break;
            case 3 when manualScaleReady:
                low = manualMinimum * factor; high = manualMaximum * factor; break;
        }
        ColorMinimum = low; ColorMaximum = high;
        bool below = ContourValues.Any(v => v.Value < low), above = ContourValues.Any(v => v.Value > high);
        ColorMinimumLabel = HasResults ? (below ? "≤ " : "") + low.ToString("G6") : "—";
        ColorMaximumLabel = HasResults ? (above ? "≥ " : "") + high.ToString("G6") : "—";
        foreach (string property in new[] { nameof(ColorMinimum), nameof(ColorMaximum), nameof(ColorMinimumLabel), nameof(ColorMaximumLabel), nameof(ColorMiddleLabel), nameof(VisibleResultRange) }) OnPropertyChanged(property);
    }
    [RelayCommand] void ApplyColorScale()
    {
        if (disposed || !IsManualColorScale || !HasResults) return;
        static bool Read(string text, out double value) =>
            (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) || double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && double.IsFinite(value);
        if (!Read(ColorScaleLower, out double low) || !Read(ColorScaleUpper, out double high) || high <= low ||
            !double.IsFinite(high - low) || !double.IsFinite(low / currentDisplayFactor) || !double.IsFinite(high / currentDisplayFactor) ||
            ContourValues.Any(v => !float.IsFinite((float)((v.Value - low) / (high - low)))))
        {
            ColorScaleMessage = "Inserisci due numeri finiti con minimo minore del massimo. La scala applicata resta invariata."; return;
        }
        manualMinimum = low / currentDisplayFactor; manualMaximum = high / currentDisplayFactor; manualScaleReady = true;
        ColorScaleMessage = "Scala applicata. I valori esterni usano il colore estremo; tabella e dati conservano i valori originali.";
        RefreshScene();
    }
}
