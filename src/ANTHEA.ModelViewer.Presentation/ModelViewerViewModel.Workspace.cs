using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    private IReadOnlyList<ModelTreeItem> allTree = [];
    private readonly HashSet<int> hiddenElements = [];
    private HashSet<int>? isolatedElements;
    private readonly Dictionary<(string Case, string Family, string Axes, string Component), IReadOnlyList<ResultValue>> fields = [];

    private static readonly string[] FamilyKeys = ["PLATE", "BEAM", "TRUSS", "TENSTR", "COMPTR", "CABLE"];
    public IReadOnlyList<string> FamilyLabels { get; } = FamilyKeys.Select(ElementFamilies.Label).ToArray();
    public IReadOnlyList<string> AxesLabels { get; } = ["Locali", "Principali"];
    public IReadOnlyList<string> ForceUnits { get; } = ["kN", "N", "MN"];
    public IReadOnlyList<string> LengthUnits { get; } = ["m", "mm", "cm"];
    public IReadOnlyList<string> BeamLocations { get; } = ["Lungo l’asta", "Estremità I", "1/4", "Mezzeria", "3/4", "Estremità J"];
    public bool IsBeamFamily => SelectedFamily > 0;
    public string OverlaySummary => Snapshot == null ? "Nessun modello" : $"{Snapshot.LocalFrames?.Length ?? 0:N0} terne · {Snapshot.Boundaries?.Length ?? 0:N0} assegnazioni grafiche";
    public string Family => FamilyKeys[Math.Clamp(SelectedFamily, 0, FamilyKeys.Length - 1)];
    public string Axes => SelectedAxes == 0 ? "LOCAL" : "PRINCIPAL";
    public IReadOnlyList<string> Components => ResultFields.Components(Family, Axes);
    public IReadOnlyList<ResultValue> ActiveValues { get; private set; } = [];
    public IReadOnlySet<int> SelectedElementIds { get; private set; } = new HashSet<int>();
    public IReadOnlySet<int>? VisibleElementIds { get; private set; }
    public bool HasResults => ActiveValues.Count > 0;
    public bool HasSelection => SelectedElementIds.Count > 0;
    public bool IsSectionSelected => SelectedTreeItem?.Table == "SECT" && SectionWidth > 0;
    public double SectionWidth { get; private set; }
    public double SectionHeight { get; private set; }
    public string SectionSize { get; private set; } = "";
    public string FieldTitle => IsResultsView ? $"{Family} · {SelectedComponent} · {AxesLabels[Math.Clamp(SelectedAxes, 0, 1)]}" : ModeLabels[SelectedMode];
    public string SampleLocation => SelectedFamily == 0 ? "Nodi dell’elemento · valori non mediati" : BeamLocations[Math.Clamp(SelectedBeamLocation, 0, 5)] + " · campioni importati";
    public string SelectionCount => HasSelection ? $"{SelectedElementIds.Count:N0} elementi selezionati" : "Nessuna selezione";
    public string VisibilitySummary => Snapshot == null ? "Nessun modello" : $"{(VisibleElementIds?.Count ?? Snapshot.Elements.Length):N0} / {Snapshot.Elements.Length:N0} elementi visibili";
    public string ResultKind => Snapshot?.Results.FirstOrDefault(r => r.Name == SelectedCase)?.IsEnvelope == true ? "INVILUPPO IMPORTATO" : "CASO / COMBINAZIONE";
    public string Middle => HasResults ? ((ActiveValues.Min(v => v.Value) + ActiveValues.Max(v => v.Value)) / 2).ToString("G5") : "—";

    [ObservableProperty] private int selectedPanel;
    [ObservableProperty] private int selectedFamily;
    [ObservableProperty] private int selectedAxes;
    [ObservableProperty] private string forceUnit = "kN";
    [ObservableProperty] private string lengthUnit = "m";
    [ObservableProperty] private bool showLocalAxes;
    [ObservableProperty] private bool showRestraints;
    [ObservableProperty] private bool showConstraints;
    [ObservableProperty] private bool showLinks;
    [ObservableProperty] private bool showReleases;
    [ObservableProperty] private bool showContext = true;
    [ObservableProperty] private int selectedBeamLocation;
    [ObservableProperty] private bool showInspector = true;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private IReadOnlyList<ModelTreeItem> tree = [];
    [ObservableProperty] private ModelTreeItem? selectedTreeItem;
    [ObservableProperty] private IReadOnlyList<AttributeRow> attributes = [];
    [ObservableProperty] private string inspectorTitle = "Proprietà del modello";
    [ObservableProperty] private string resultAvailability = "Importa un modello con risultati.";

    partial void OnSelectedPanelChanged(int value) { if (value == 1 && !updating) SelectedMode = 2; }
    partial void OnSelectedFamilyChanged(int value)
    {
        bool prior = updating; updating = true; ShowContext = value == 0; updating = prior;
        OnPropertyChanged(nameof(IsBeamFamily)); ResetComponent();
    }
    partial void OnSelectedAxesChanged(int value) => ResetComponent();
    partial void OnForceUnitChanged(string value) => RefreshScene();
    partial void OnLengthUnitChanged(string value) => RefreshScene();
    partial void OnShowLocalAxesChanged(bool value) => RefreshScene();
    partial void OnShowRestraintsChanged(bool value) => RefreshScene();
    partial void OnShowConstraintsChanged(bool value) => RefreshScene();
    partial void OnShowLinksChanged(bool value) => RefreshScene();
    partial void OnShowReleasesChanged(bool value) => RefreshScene();
    partial void OnShowContextChanged(bool value) => RefreshScene();
    partial void OnSelectedBeamLocationChanged(int value) { OnPropertyChanged(nameof(SampleLocation)); RefreshScene(); }
    partial void OnSearchTextChanged(string value) => Tree = string.IsNullOrWhiteSpace(value) ? allTree : ModelTreeBuilder.Filter(allTree, value.Trim());
    partial void OnSelectedTreeItemChanging(ModelTreeItem? value) { if (SelectedTreeItem != null) SelectedTreeItem.IsSelected = false; }
    partial void OnSelectedTreeItemChanged(ModelTreeItem? value)
    {
        if (value == null) return;
        value.IsSelected = true; ModelTreeBuilder.ExpandPath(Tree, value.Key);
        Attributes = value.Attributes; InspectorTitle = value.Label;
        SelectedElementIds = value.Elements.ToHashSet();
        var section = value.Table == "SECT" ? Snapshot?.Sections.FirstOrDefault(s => s.Id == value.Id) : null;
        SectionWidth = section == null ? 0 : 156 * section.Width / Math.Max(section.Width, section.Height);
        SectionHeight = section == null ? 0 : 108 * section.Height / Math.Max(section.Width, section.Height);
        // Keep the same physical scale in both directions.
        if (section != null) { double scale = Math.Min(156 / section.Width, 108 / section.Height); SectionWidth = section.Width * scale; SectionHeight = section.Height * scale; }
        SectionSize = section == null ? "" : $"{section.Width:G4} × {section.Height:G4} m · {section.Shape}";
        foreach (string property in new[] { nameof(HasSelection), nameof(SelectionCount), nameof(IsSectionSelected), nameof(SectionWidth), nameof(SectionHeight), nameof(SectionSize) }) OnPropertyChanged(property);
        IsolateCommand.NotifyCanExecuteChanged(); HideCommand.NotifyCanExecuteChanged(); ShowSelectedCommand.NotifyCanExecuteChanged();
        if (value.Table == "ELEM" && value.Id is int id) { ElementNumber = id.ToString(); Query(); }
        RefreshScene();
    }

    void ResetComponent()
    {
        bool previous = updating; updating = true;
        OnPropertyChanged(nameof(Components));
        if (!Components.Contains(SelectedComponent)) SelectedComponent = Components.FirstOrDefault() ?? "";
        updating = previous;
        OnPropertyChanged(nameof(SampleLocation)); RefreshScene();
    }

    void InitializeWorkspace()
    {
        fields.Clear(); hiddenElements.Clear(); isolatedElements = null; VisibleElementIds = null; SelectedElementIds = new HashSet<int>();
        tableState = null;
        allTree = ModelTreeBuilder.Build(Snapshot!); Tree = allTree; SearchText = "";
        displayCatalog = new ModelDisplayCatalog(Snapshot!);
        SelectedTreeItem = null;
        SectionWidth = SectionHeight = 0; SectionSize = "";
        foreach (var property in new[] { nameof(HasSelection), nameof(SelectionCount), nameof(IsSectionSelected), nameof(SectionWidth), nameof(SectionHeight), nameof(SectionSize) }) OnPropertyChanged(property);
        IsolateCommand.NotifyCanExecuteChanged(); HideCommand.NotifyCanExecuteChanged(); ShowSelectedCommand.NotifyCanExecuteChanged();
        InspectorTitle = "Proprietà del modello";
        Attributes = [new("Nome", Snapshot!.Name), new("Sorgente", Snapshot.Source), new("Nodi", Snapshot.Nodes.Length.ToString("N0")), new("Elementi", Snapshot.Elements.Length.ToString("N0")), new("Casi con risultati", Snapshot.Results.Length.ToString()), new("Geometria normalizzata", "m")];
        OnPropertyChanged(nameof(OverlaySummary));
    }

    void UpdateResultField()
    {
        var result = Snapshot!.Results.FirstOrDefault(r => r.Name == SelectedCase);
        var key = (SelectedCase ?? "", Family, Axes, SelectedComponent ?? "");
        if (!fields.TryGetValue(key, out var raw)) fields[key] = raw = ResultFields.Read(result, Family, Axes, SelectedComponent ?? "");
        double force = ForceUnit switch { "N" => 1000, "MN" => .001, _ => 1 };
        double length = LengthUnit switch { "mm" => 1000, "cm" => 100, _ => 1 };
        bool moment = SelectedComponent?.StartsWith('M') == true;
        double scale = force * (SelectedFamily == 0 ? moment ? 1 : 1 / length : moment ? length : 1);
        string unit = SelectedFamily == 0 ? moment ? $"{ForceUnit}·{LengthUnit}/{LengthUnit}" : $"{ForceUnit}/{LengthUnit}" : moment ? $"{ForceUnit}·{LengthUnit}" : ForceUnit;
        ActiveValues = raw.Where(v => (VisibleElementIds == null || VisibleElementIds.Contains(v.Element)) &&
            (SelectedFamily == 0 || SelectedBeamLocation == 0 || v.Station == (SelectedBeamLocation - 1) / 4.0)).Select(v => v with { Value = v.Value * scale }).ToArray();
        Minimum = HasResults ? ActiveValues.Min(v => v.Value).ToString("G6") : "—";
        Maximum = HasResults ? ActiveValues.Max(v => v.Value).ToString("G6") : "—";
        ResultUnit = string.IsNullOrEmpty(SelectedComponent) ? "—" : $"{SelectedComponent} [{unit}]";
        ResultAvailability = HasResults ? $"{ActiveValues.Select(v => v.Element).Distinct().Count():N0} elementi con valori · {SampleLocation.ToLowerInvariant()}"
            : SelectedFamily > 0 && SelectedAxes == 1 ? "Per le aste scegli gli assi locali. Gli assi principali delle sezioni richiedono un contratto dedicato."
            : SelectedAxes == 1 ? "Componenti principali non importate. Non si ricavano dagli estremi non simultanei di un inviluppo."
            : raw.Count > 0 ? "Nessun risultato negli elementi attualmente visibili. Usa «Mostra tutto»."
            : $"Nessun risultato {Family} disponibile per questa combinazione nella copia importata.";
        foreach (string property in new[] { nameof(HasResults), nameof(FieldTitle), nameof(ResultKind), nameof(Middle), nameof(VisibilitySummary) }) OnPropertyChanged(property);
        if (SelectedTreeItem?.Table == "ELEM" && SelectedTreeItem.Id is int selectedId)
            Attributes = SelectedTreeItem.Attributes.Concat(ActiveValues.Where(v => v.Element == selectedId)
                .Select(v => new AttributeRow(v.Station.HasValue ? $"x/L = {v.Station:G3} · {ResultUnit}" : $"Nodo {v.Node} · {ResultUnit}", v.Value.ToString("G7")))).ToArray();
        LinkCommand.NotifyCanExecuteChanged();
        if (CanQuery()) Query();
    }

    public ModelTreeItem? FindTreeItem(string key) => ModelTreeBuilder.Descendants(allTree).FirstOrDefault(n => n.Key == key);
    public void SelectElement(int id, bool additive = false)
    {
        if (FindTreeItem("ELEM:" + id) is not { } item) return;
        if (!additive) SelectedTreeItem = item;
        else
        {
            var ids = SelectedElementIds.ToHashSet(); if (!ids.Add(id)) ids.Remove(id);
            SelectedTreeItem = new() { Key = "selection", Label = "Selezione multipla", Elements = ids.Order().ToArray(), Attributes = [new("Elementi", string.Join(", ", ids.Order()))] };
        }
        ShowInspector = true;
    }
    private bool CanChangeVisibility() => HasSelection && !disposed;
    [RelayCommand(CanExecute = nameof(CanChangeVisibility))] private void Isolate() { isolatedElements = SelectedElementIds.ToHashSet(); hiddenElements.ExceptWith(isolatedElements); ApplyVisibility(); }
    [RelayCommand(CanExecute = nameof(CanChangeVisibility))] private void Hide() { hiddenElements.UnionWith(SelectedElementIds); ApplyVisibility(); }
    [RelayCommand] private void ShowAll() { hiddenElements.Clear(); isolatedElements = null; ApplyVisibility(); }
    [RelayCommand] private void ClearSelection() { SelectedTreeItem = new() { Label = "Proprietà del modello", Attributes = [new("Selezione", "Seleziona una voce nell’albero o un elemento nella scena.")] }; }
    [RelayCommand] private void SetMode(string mode) { if (int.TryParse(mode, out int value) && value is >= 0 and <= 2) SelectedMode = value; }
    void ApplyVisibility()
    {
        VisibleElementIds = Snapshot == null || hiddenElements.Count == 0 && isolatedElements == null ? null : Snapshot.Elements.Where(e => !hiddenElements.Contains(e.Id) && (isolatedElements == null || isolatedElements.Contains(e.Id))).Select(e => e.Id).ToHashSet();
        RefreshScene();
    }
}
