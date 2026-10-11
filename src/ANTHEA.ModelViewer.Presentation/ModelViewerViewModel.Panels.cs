using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    [ObservableProperty] private bool isNavigationCollapsed;
    [ObservableProperty] private bool showNodeAxes;
    [ObservableProperty] private bool overlaysOnlySelection;
    [ObservableProperty] private bool showElementDirections;
    [ObservableProperty] private bool showBoundaryLabels;
    [ObservableProperty] private double overlayScale = 1;
    public IReadOnlySet<int> SelectedNodeIds { get; private set; } = new HashSet<int>();
    public string NodeAxesSummary => Snapshot == null ? "Nessun modello" : $"{Snapshot.NodeFrames?.Length ?? 0:N0} terne nodali importate";
    partial void OnShowNodeAxesChanged(bool value) => RefreshScene();
    partial void OnOverlaysOnlySelectionChanged(bool value) => RefreshScene();
    partial void OnShowElementDirectionsChanged(bool value) => RefreshScene();
    partial void OnShowBoundaryLabelsChanged(bool value) => RefreshScene();
    partial void OnOverlayScaleChanged(double value)
    {
        double safe = double.IsFinite(value) ? Math.Clamp(value, .25, 3) : 1;
        if (safe != value) { OverlayScale = safe; return; }
        RefreshScene();
    }
    [RelayCommand] void InspectBoundaries()
    {
        SelectedTableKind = 4; TableSearch = ""; TableOnlyVisible = true;
        TableOnlySelected = OverlaysOnlySelection; ShowTable = true;
    }
    [RelayCommand] void ToggleNavigation() => IsNavigationCollapsed = !IsNavigationCollapsed;
    [RelayCommand] void ToggleInspector() => ShowInspector = !ShowInspector;
    [RelayCommand] void OpenNavigation(string? panel)
    {
        if (int.TryParse(panel, out int index) && index is >= 0 and <= 4) SelectedPanel = index;
        IsNavigationCollapsed = false;
    }
    [RelayCommand] void DisableOverlays()
    {
        bool previous = updating; updating = true;
        ShowNodeAxes = ShowLocalAxes = ShowRestraints = ShowConstraints = ShowLinks = ShowReleases = ShowElementDirections = ShowBoundaryLabels = false;
        updating = previous; RefreshScene();
    }
    [RelayCommand(CanExecute = nameof(CanChangeVisibility))] void InspectSelection()
    {
        ShowInspector = true; ShowTable = true; SelectedTableKind = 1; TableOnlySelected = true; TableOnlyVisible = false;
    }
    void UpdateSelectedNodes(ModelTreeItem? item)
    {
        SelectedNodeIds = item?.Id is int id && item.Table is "NODE" or "SKEW" or "CONS" ? new HashSet<int> { id } :
            Snapshot?.Elements.Where(e => SelectedElementIds.Contains(e.Id)).SelectMany(e => e.Nodes).ToHashSet() ?? [];
        OnPropertyChanged(nameof(SelectedNodeIds)); InspectSelectionCommand.NotifyCanExecuteChanged();
    }
}
