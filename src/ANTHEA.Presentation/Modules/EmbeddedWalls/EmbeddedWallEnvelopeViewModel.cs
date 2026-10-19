using System.Text;
using Anthea.Presentation.Services;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed partial class EmbeddedWallSheetViewModel
{
    private EmbeddedWallEnvelopeSeriesData? envelopeSeries;
    private EmbeddedWallEnvelopeStationData? envelopeStation;
    public IRelayCommand EnvelopeCsvCommand { get; }
    public IReadOnlyList<EmbeddedWallEnvelopeSeriesData> EnvelopeSeries => current?.Envelope?.Series ?? [];
    public EmbeddedWallEnvelopeSeriesData? SelectedEnvelopeSeries
    {
        get => envelopeSeries;
        set
        {
            if (!SetProperty(ref envelopeSeries, value)) return;
            EnvelopeMinimum = value is null ? [] : Array.AsReadOnly(value.Stations.Select(s => s.Minimum.Point).ToArray());
            EnvelopeMaximum = value is null ? [] : Array.AsReadOnly(value.Stations.Select(s => s.Maximum.Point).ToArray());
            OnPropertyChanged(nameof(EnvelopeMinimum)); OnPropertyChanged(nameof(EnvelopeMaximum)); OnPropertyChanged(nameof(EnvelopeStations));
            OnPropertyChanged(nameof(EnvelopeExtrema)); SelectedEnvelopeStation = value?.Stations.FirstOrDefault();
        }
    }
    public IReadOnlyList<EmbeddedWallPointData> EnvelopeMinimum { get; private set; } = [];
    public IReadOnlyList<EmbeddedWallPointData> EnvelopeMaximum { get; private set; } = [];
    public IReadOnlyList<EmbeddedWallEnvelopeStationData> EnvelopeStations => envelopeSeries?.Stations ?? [];
    public EmbeddedWallEnvelopeStationData? SelectedEnvelopeStation
    {
        get => envelopeStation;
        set { if (SetProperty(ref envelopeStation, value)) OnPropertyChanged(nameof(EnvelopeConcomitant)); }
    }
    public IReadOnlyList<KeyValuePair<string, EmbeddedWallEnvelopeObservationData>> EnvelopeConcomitant => envelopeStation is null ? []
        : [new("Minimo", envelopeStation.Minimum), new("Massimo", envelopeStation.Maximum)];
    public string EnvelopeStatus => current?.Envelope?.Diagnostic ?? (current is null ? "Inviluppi assenti o superati: ricalcolare le fasi." : "Inviluppi non disponibili: nessuna analisi meccanica disponibile. " + current.Diagnostic);
    public string EnvelopeExtrema => envelopeSeries is not { } s ? "" :
        $"Minimo globale: {s.Minimum.Value:G7} {s.Unit}, {s.Minimum.CaseId} / {s.Minimum.Stage}, z={s.Minimum.Point.Elevation:G7} m. "
        + $"Massimo globale: {s.Maximum.Value:G7} {s.Unit}, {s.Maximum.CaseId} / {s.Maximum.Stage}, z={s.Maximum.Point.Elevation:G7} m.";
    private void NotifyEnvelope()
    {
        OnPropertyChanged(nameof(EnvelopeSeries)); OnPropertyChanged(nameof(EnvelopeStatus));
        SelectedEnvelopeSeries = EnvelopeSeries.FirstOrDefault(s => s.Quantity == envelopeSeries?.Quantity) ?? EnvelopeSeries.FirstOrDefault();
        EnvelopeCsvCommand?.NotifyCanExecuteChanged();
    }
    private void ExportEnvelopeCsv()
    {
        var result = RequireCurrent();
        var path = Context.Services.Files.SaveFile(new([new FileFilter("Inviluppi con provenienza e azioni concomitanti", "*.csv")], "paratia-inviluppi.csv"));
        if (path is not null) File.WriteAllText(path, EmbeddedWallReport.EnvelopeCsv(result), new UTF8Encoding(true));
    }
}
