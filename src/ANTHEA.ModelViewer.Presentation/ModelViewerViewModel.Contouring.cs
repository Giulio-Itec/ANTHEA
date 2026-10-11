using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> ContourValueModes { get; } = ["Per elemento · non mediati", "Media ai vertici"];
    [ObservableProperty] private int selectedContourValues;
    public bool CanAverageContours => SelectedFamily == 0;
    public bool IsAveragedContour => CanAverageContours && SelectedContourValues == 1;
    public string ContourValueLabel => IsAveragedContour ? "MEDIA AI VERTICI" : "VALORI IMPORTATI";
    public IReadOnlyList<ResultValue> ContourValues { get; private set; } = [];
    public string ContourAveragingSummary { get; private set; } = "";
    public string ContourAveragingHelp => "Media aritmetica dei contributi delle piastre ai nodi condivisi, a parità di proprietà e terna locale; separazione per materiale quando importato. Sono inclusi anche gli elementi nascosti: isolare non cambia i colori a scala fissa. Senza terna o con risultati incompleti non si esegue la media. Tabelle e riferimenti mantengono i dati originali.";

    private PlateContourAverager? contourAverager;
    private IReadOnlyList<ResultValue>? averagedSource;
    private AveragedContour? averagedField;

    partial void OnSelectedContourValuesChanged(int value) => RefreshScene();

    private IReadOnlyList<ResultValue> ContourField(IReadOnlyList<ResultValue> raw)
    {
        ContourAveragingSummary = "";
        if (!IsAveragedContour || raw.Count == 0) return raw;
        // Only the last smoothed field is retained. Model replacement invalidates topology and values together.
        contourAverager ??= new(Snapshot!, displayCatalog!.ForMode(2));
        if (!ReferenceEquals(averagedSource, raw))
        {
            averagedField = contourAverager.Apply(raw); averagedSource = raw;
        }
        ContourAveragingSummary = $"{averagedField!.VertexCount:N0} vertici mediati nel modello" +
            (averagedField.UnframedElements > 0 ? $" · {averagedField.UnframedElements:N0} piastre senza terna: valori originali" : "");
        return averagedField.Values;
    }
}
