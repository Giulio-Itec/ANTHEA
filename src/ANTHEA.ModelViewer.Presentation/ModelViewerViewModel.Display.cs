using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    private ModelDisplayCatalog? displayCatalog;
    public IReadOnlyList<string> ColorModes { get; } = ["Colore uniforme", "Tipo di elemento", "Materiale", "Sezione / spessore", "Gruppo strutturale"];
    public IReadOnlyList<string> SurfaceModes { get; } = ["Superfici e bordi", "Solo superfici", "Wireframe"];
    public IReadOnlyList<string> LabelScopes { get; } = ["Oggetti visibili", "Solo selezionati"];
    public IReadOnlyDictionary<int, ElementDisplay> ElementColors { get; private set; } = new Dictionary<int, ElementDisplay>();
    [ObservableProperty] private IReadOnlyList<DisplayCategory> colorLegend = [];
    [ObservableProperty] private int selectedColorMode;
    [ObservableProperty] private int selectedSurfaceMode;
    [ObservableProperty] private int selectedLabelScope;
    [ObservableProperty] private bool showDisplayOptions;
    [ObservableProperty] private bool showHiddenWireframe;
    [ObservableProperty] private bool includeHiddenInFit;
    [ObservableProperty] private bool showNodeIds;
    [ObservableProperty] private bool showElementIds;
    [ObservableProperty] private bool showPropertyIds;
    [ObservableProperty] private bool showNodes;
    [ObservableProperty] private double labelScale = 1;
    [ObservableProperty] private double lineWidth = 1;
    public bool HasColorLegend => !IsResultsView && SelectedColorMode != 0;
    public string ColorLegendTitle => ColorModes[Math.Clamp(SelectedColorMode, 0, 4)];
    public string ColorExplanation => IsResultsView ? "Il colore rappresenta la sollecitazione. I colori degli attributi si applicano a Geometria e Volumi."
        : SelectedColorMode == 4 ? "Appartenenze multiple: colore del gruppo con meno elementi; a parità, ID minore. Tutte le appartenenze sono consultabili nell’albero."
        : "Colori stabili per gli attributi della copia importata.";
    partial void OnSelectedColorModeChanged(int value) => RefreshScene();
    partial void OnSelectedSurfaceModeChanged(int value) => RefreshScene();
    partial void OnSelectedLabelScopeChanged(int value) => RefreshScene();
    partial void OnShowHiddenWireframeChanged(bool value) => RefreshScene();
    partial void OnShowNodeIdsChanged(bool value) => RefreshScene();
    partial void OnShowElementIdsChanged(bool value) => RefreshScene();
    partial void OnShowPropertyIdsChanged(bool value) => RefreshScene();
    partial void OnShowNodesChanged(bool value) => RefreshScene();
    partial void OnLabelScaleChanged(double value) => RefreshScene();
    partial void OnLineWidthChanged(double value) => RefreshScene();
    [RelayCommand(CanExecute = nameof(CanChangeVisibility))]
    private void ShowSelected()
    {
        hiddenElements.ExceptWith(SelectedElementIds); isolatedElements?.UnionWith(SelectedElementIds); ApplyVisibility();
    }
    [RelayCommand] private void InvertVisibility()
    {
        if (Snapshot == null) return;
        var currentlyVisible = VisibleElementIds ?? Snapshot.Elements.Select(e => e.Id).ToHashSet();
        isolatedElements = null; hiddenElements.Clear(); hiddenElements.UnionWith(currentlyVisible); ApplyVisibility();
    }
    void UpdateDisplay()
    {
        ElementColors = displayCatalog?.ForMode(Math.Clamp(SelectedColorMode, 0, 4)) ?? new Dictionary<int, ElementDisplay>();
        ColorLegend = ElementColors.Where(e => VisibleElementIds == null || VisibleElementIds.Contains(e.Key)).GroupBy(e => e.Value.Key)
            .Select(g => new DisplayCategory(g.Key, g.First().Value.Label, g.First().Value.Color, g.Count())).ToArray();
        OnPropertyChanged(nameof(HasColorLegend)); OnPropertyChanged(nameof(ColorExplanation)); OnPropertyChanged(nameof(ColorLegendTitle));
    }
}
