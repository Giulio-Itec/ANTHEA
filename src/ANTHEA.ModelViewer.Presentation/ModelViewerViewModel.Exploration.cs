using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record ModelProbe(int Element, int Node, double? Station, string Caption, double X, double Y, double Z);
public sealed record GraphicPlane(double X, double Y, double Z, double Distance);

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> SelectionTools { get; } = ["Clic", "Rettangolo", "Poligono"];
    public IReadOnlyList<string> SelectionTargets { get; } = ["Tutti", "Nodi", "Aste", "Superfici"];
    public IReadOnlyList<string> ClipAxes { get; } = ["X · piano YZ", "Y · piano XZ", "Z · piano XY", "Tre nodi"];
    public IReadOnlyList<string> BandModes { get; } = ["Continuo", "8 fasce", "12 fasce", "20 fasce"];
    public ObservableCollection<ModelProbe> PinnedProbes { get; } = [];
    public ObservableCollection<ModelViewPreset> SavedViews { get; } = [];
    public event EventHandler? FocusSelectionRequested;
    public event EventHandler? PreviousCameraRequested;
    public event EventHandler? HoverChanged;
    [ObservableProperty] private int selectionTool;
    [ObservableProperty] private int selectionTarget;
    [ObservableProperty] private string selectionIds = "";
    [ObservableProperty] private string resultSelectionMinimum = "";
    [ObservableProperty] private string resultSelectionMaximum = "";
    [ObservableProperty] private string explorationMessage = "";
    [ObservableProperty] private ModelProbe? hoverProbe;
    [ObservableProperty] private bool showExtrema;
    [ObservableProperty] private bool showOutlines = true;
    [ObservableProperty] private bool avoidLabelOverlap = true;
    [ObservableProperty] private double surfaceOpacity = 1;
    [ObservableProperty] private bool clipEnabled;
    [ObservableProperty] private int clipAxis = 2;
    [ObservableProperty] private double clipPercent = 50;
    [ObservableProperty] private bool clipReverse;
    [ObservableProperty] private string clipNodes = "";
    [ObservableProperty] private bool reversePalette;
    [ObservableProperty] private int contourBands;
    [ObservableProperty] private int legendPrecision = 3;
    [ObservableProperty] private bool showLegendSettings;
    [ObservableProperty] private string viewName = "";
    [ObservableProperty] private ModelViewPreset? selectedSavedView;
    [ObservableProperty] private bool compareEnabled;
    [ObservableProperty] private string? compareCase;
    [ObservableProperty] private string compareComponent = "Myy";
    [ObservableProperty] private int compareAveraging;
    [ObservableProperty] private bool compareCommonScale = true;
    [ObservableProperty] private int exportWidth = 2400;
    [ObservableProperty] private bool exportSceneOnly = true;
    [ObservableProperty] private bool exportWhiteBackground = true;
    public IReadOnlyList<int> ExportWidths { get; } = [1600, 2400, 3840];
    public string ResultHeading => !IsResultsView ? FieldTitle : $"{SelectedCase ?? "Geometria"} · {FieldTitle} · {ContourValueLabel}";
    public string ClipDescription { get; private set; } = "";
    public GraphicPlane? ActiveClipPlane { get; private set; }
    public IReadOnlyList<ModelProbe> ExtremeProbes { get; private set; } = [];
    public IReadOnlyList<string> LegendTicks => !HasResults ? Enumerable.Repeat("—",5).ToArray() : Enumerable.Range(0, 5).Select(i => (i==0 && ContourValues.Any(v=>v.Value>ColorMaximum) ? "≥ " : i==4 && ContourValues.Any(v=>v.Value<ColorMinimum) ? "≤ " : "") + FormatLegend(ColorMaximum * (1-i/4.0) + ColorMinimum * (i/4.0))).ToArray();
    public string FormatLegend(double value) => value != 0 && (Math.Abs(value) >= 1e6 || Math.Abs(value) < Math.Pow(10, -Math.Clamp(LegendPrecision, 0, 6))) ? value.ToString("E" + Math.Clamp(LegendPrecision, 0, 6)) : value.ToString("N" + Math.Clamp(LegendPrecision, 0, 6));
    private readonly List<int[]?> visibilityHistory = [];
    private int[]? lastVisible;
    private bool restoringVisibility;
    private string viewRevision = "";

    partial void OnHoverProbeChanged(ModelProbe? value) => HoverChanged?.Invoke(this, EventArgs.Empty);
    partial void OnShowExtremaChanged(bool value) => RefreshScene();
    partial void OnShowOutlinesChanged(bool value) => RefreshScene();
    partial void OnAvoidLabelOverlapChanged(bool value) => RefreshScene();
    partial void OnSurfaceOpacityChanged(double value) => RefreshScene();
    partial void OnClipEnabledChanged(bool value) => RefreshScene();
    partial void OnClipAxisChanged(int value) => RefreshScene();
    partial void OnClipPercentChanged(double value) => RefreshScene();
    partial void OnClipReverseChanged(bool value) => RefreshScene();
    partial void OnClipNodesChanged(string value) => RefreshScene();
    partial void OnReversePaletteChanged(bool value) => RefreshScene();
    partial void OnContourBandsChanged(int value) => RefreshScene();
    partial void OnLegendPrecisionChanged(int value) => RefreshScene();
    partial void OnCompareEnabledChanged(bool value) => RefreshScene();
    partial void OnCompareCaseChanged(string? value) => RefreshScene();
    partial void OnCompareComponentChanged(string value) => RefreshScene();
    partial void OnCompareAveragingChanged(int value) => RefreshScene();
    partial void OnCompareCommonScaleChanged(bool value) => RefreshScene();
    [RelayCommand] void OpenLegendSettings() { SelectedPanel = 1; IsNavigationCollapsed = false; ShowLegendSettings = true; }
    [RelayCommand] void FocusSelection() => FocusSelectionRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] void PreviousCamera() => PreviousCameraRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] void PinProbe() { if (HoverProbe is { } p && PinnedProbes.Count < 20 && !PinnedProbes.Any(v => v.Element == p.Element && v.Node == p.Node && v.Station == p.Station)) { PinnedProbes.Add(p); RefreshScene(); } }
    [RelayCommand] void ClearProbes() { PinnedProbes.Clear(); RefreshScene(); }
    [RelayCommand] void GoToExtreme(string? kind)
    {
        if (ContourValues.Count == 0) return;
        var value = kind == "min" ? ContourValues.MinBy(v => v.Value)! : ContourValues.MaxBy(v => v.Value)!;
        SelectElement(value.Element); FocusSelection();
    }
    public bool AcceptsElement(ModelElement e) => SelectionTarget switch { 1 => false, 2 => ElementFamilies.IsLine(e.Type), 3 => e.Type == "PLATE", _ => true };
    public void SelectElements(IEnumerable<int> ids, bool additive = false, bool subtract = false)
    {
        if (Snapshot == null) return;
        var valid = Snapshot.Elements.Where(e => AcceptsElement(e) && (VisibleElementIds == null || VisibleElementIds.Contains(e.Id))).Select(e => e.Id).ToHashSet();
        var requested = ids.Where(valid.Contains).ToHashSet();
        var selected = additive || subtract ? SelectedElementIds.ToHashSet() : [];
        if (subtract) selected.ExceptWith(requested); else selected.UnionWith(requested);
        if(selected.Count==1) { SelectElement(selected.Single());return; }
        SelectedTreeItem = new() { Key = "selection", Label = "Selezione grafica", Elements = selected.Order().ToArray(), Attributes = [new("Elementi", string.Join(", ", selected.Order()))] };
    }
    public void SelectNodes(IEnumerable<int> ids, bool additive = false, bool subtract = false)
    {
        if (Snapshot == null) return;
        var valid = Snapshot.Elements.Where(e => VisibleElementIds == null || VisibleElementIds.Contains(e.Id)).SelectMany(e => e.Nodes).ToHashSet();
        var requested = ids.Where(valid.Contains).ToHashSet();
        var selected = additive || subtract ? SelectedNodeIds.ToHashSet() : [];
        if (subtract) selected.ExceptWith(requested); else selected.UnionWith(requested);
        SelectedTreeItem = new() { Key = "node-selection", Label = "Selezione nodi", Attributes = [new("Nodi", string.Join(", ", selected.Order()))] };
        SelectedNodeIds = selected; OnPropertyChanged(nameof(SelectedNodeIds)); OnPropertyChanged(nameof(SelectionCount)); RefreshScene();
    }
    [RelayCommand] void SelectByIds()
    {
        try
        {
            var ids = new HashSet<int>();
            foreach (string token in SelectionIds.Split([' ', ',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split('-');
                if (parts.Length is < 1 or > 2 || !int.TryParse(parts[0], out int first) || first <= 0) throw new FormatException();
                int last = parts.Length == 1 ? first : int.Parse(parts[1], CultureInfo.InvariantCulture);
                if (last < first || (long)last-first > 500000) throw new FormatException();
                for (long i = first; i <= last; i++) { ids.Add((int)i);if(ids.Count>500000)throw new FormatException(); }
            }
            if (SelectionTarget == 1) SelectNodes(ids); else SelectElements(ids);
            ExplorationMessage = "Selezionati gli ID presenti e visibili; esempi: 1 5 10-20.";
        }
        catch (Exception ex) when (ex is FormatException or OverflowException) { ExplorationMessage = "Usa ID positivi o intervalli crescenti, per esempio 1 5 10-20. Selezione conservata."; }
    }
    [RelayCommand] void SelectByResultRange()
    {
        bool Read(string text,out double value) => double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&double.IsFinite(value);
        if(!Read(ResultSelectionMinimum,out double low)||!Read(ResultSelectionMaximum,out double high)||low>high)
        { ExplorationMessage="Inserisci un intervallo numerico crescente. La selezione resta invariata.";return; }
        SelectionTarget=0;
        SelectElements(ContourValues.Where(v=>v.Value>=low&&v.Value<=high).Select(v=>v.Element));
        ExplorationMessage="Selezionati gli elementi con almeno un campione visualizzato nell’intervallo. Puoi isolarli o nasconderli.";
    }
    internal void TrackVisibility()
    {
        var current = VisibleElementIds?.Order().ToArray();
        if (!updating && !restoringVisibility && !(current == null && lastVisible == null) && !(current != null && lastVisible != null && current.SequenceEqual(lastVisible)))
        { visibilityHistory.Add(lastVisible); if (visibilityHistory.Count > 32) visibilityHistory.RemoveAt(0); }
        lastVisible = current;
    }
    [RelayCommand] void PreviousVisibility()
    {
        if (visibilityHistory.Count == 0) return;
        var previous = visibilityHistory[^1]; visibilityHistory.RemoveAt(visibilityHistory.Count-1); restoringVisibility = true;
        try { ClearVisibilityCriteria(); hiddenElements.Clear(); isolatedElements = previous?.ToHashSet(); ApplyVisibility(); }
        finally { restoringVisibility = false; }
    }
    void InitializeExploration()
    {
        visibilityHistory.Clear(); lastVisible = null; PinnedProbes.Clear(); HoverProbe = null;
        CompareCase = Snapshot!.Results.FirstOrDefault()?.Name; CompareComponent = "Myy";
        SavedViews.Clear(); viewRevision = Snapshot.Fingerprint();
        try { foreach (var view in services.LoadViews(viewRevision)) SavedViews.Add(view); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { ExplorationMessage = "Viste salvate non leggibili. Il modello resta disponibile."; }
        SelectedSavedView = SavedViews.FirstOrDefault();
    }
    void UpdateExploration()
    {
        ActiveClipPlane = null; ClipDescription = "Taglio grafico disattivato";
        if (ClipEnabled && Snapshot != null)
        {
            double x = ClipAxis == 0 ? 1 : 0, y = ClipAxis == 1 ? 1 : 0, z = ClipAxis == 2 ? 1 : 0;
            double distance = 0;
            bool valid = true;
            if (ClipAxis == 3)
            {
                var ids = ClipNodes.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
                var nodes = ids.Select(s => int.TryParse(s, out int id) ? Snapshot.Nodes.FirstOrDefault(n => n.Id == id) : null).ToArray();
                valid = nodes.Length == 3 && nodes.All(n => n != null);
                if (valid)
                {
                    var a=nodes[0]!; var b=nodes[1]!; var c=nodes[2]!;
                    double ux=b.X-a.X, uy=b.Y-a.Y, uz=b.Z-a.Z, vx=c.X-a.X, vy=c.Y-a.Y, vz=c.Z-a.Z;
                    x=uy*vz-uz*vy; y=uz*vx-ux*vz; z=ux*vy-uy*vx; double length=Math.Sqrt(x*x+y*y+z*z);
                    valid = double.IsFinite(length) && length > 1e-12;
                    if(valid) { x/=length; y/=length; z/=length; distance=x*a.X+y*a.Y+z*a.Z; }
                }
            }
            else
            {
                var projections=Snapshot.Nodes.Select(n=>x*n.X+y*n.Y+z*n.Z).ToArray();
                distance=projections.Min()+(projections.Max()-projections.Min())*Math.Clamp(ClipPercent,0,100)/100;
            }
            if(valid) { double sign=ClipReverse?-1:1; ActiveClipPlane=new(x*sign,y*sign,z*sign,distance*sign); ClipDescription=$"Taglio a {distance:G6} m · scala sugli elementi attivi"; }
            else ClipDescription="Indica tre ID nodali distinti e non allineati. Taglio non applicato.";
        }
        ExtremeProbes = ShowExtrema && ContourValues.Count > 0 ? new[] { ProbeFor(ContourValues.MinBy(v=>v.Value)!, "MIN"), ProbeFor(ContourValues.MaxBy(v=>v.Value)!, "MAX") } : [];
        foreach (var name in new[] { nameof(ActiveClipPlane), nameof(ClipDescription), nameof(ExtremeProbes), nameof(LegendTicks), nameof(ResultHeading) }) OnPropertyChanged(name);
    }
    public ModelProbe ProbeFor(ResultValue value, string prefix = "")
    {
        var e = Snapshot!.Elements.First(e=>e.Id==value.Element);
        var a = Snapshot.Nodes.First(n=>n.Id==(value.Node>0?value.Node:e.Nodes[0]));
        var b = value.Node>0?a:Snapshot.Nodes.First(n=>n.Id==e.Nodes[^1]); double t=value.Station??0;
        var original=ActiveValues.FirstOrDefault(v=>v.Element==value.Element && v.Node==value.Node && v.Station==value.Station);
        string place=value.Node>0?$"N {value.Node}":$"x/L {value.Station:G3}";
        string caption=$"{prefix} E {value.Element} · {place}\n{SelectedCase}\n{FormatLegend(value.Value)} · {ResultUnit}";
        if(IsAveragedContour && original!=null) caption+=$"\nImportato: {FormatLegend(original.Value)}";
        return new(value.Element,value.Node,value.Station,caption,a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
    }
    [RelayCommand] void SaveView()
    {
        if(disposed||Snapshot==null) { ExplorationMessage="Importa un modello prima di salvare la vista.";return; }
        string name=ViewName.Trim();
        if(name.Length is 0 or >80 || SavedViews.Any(v=>v.Name.Equals(name,StringComparison.OrdinalIgnoreCase))) { ExplorationMessage="Scegli un nome nuovo da 1 a 80 caratteri."; return; }
        var saved=CaptureView(name);
        try { services.SaveViews(viewRevision,[..SavedViews,saved]); SavedViews.Add(saved); SelectedSavedView=saved; ViewName=""; ExplorationMessage="Vista salvata su questo computer per la revisione corrente."; }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { ExplorationMessage="Vista non salvata: "+ex.Message; }
    }
    public ModelViewPreset CaptureView(string name)
    {
        return new ModelViewPreset { Name=name, Camera=services.CaptureCamera(), Visible=VisibleElementIds?.ToArray(), Selected=SelectedElementIds.ToArray(),
            Case=SelectedCase, Component=SelectedComponent, Family=SelectedFamily, Axes=SelectedAxes, Mode=SelectedMode, ForceUnit=ForceUnit, LengthUnit=LengthUnit,
            Averaging=SelectedContourValues, Scale=SelectedColorScale, Lower=ColorMinimum, Upper=ColorMaximum, Clip=ClipEnabled, ClipAxis=ClipAxis, ClipPercent=ClipPercent, ClipReverse=ClipReverse, ClipNodes=ClipNodes,
            Mesh=ShowMesh, Outlines=ShowOutlines, Opacity=SurfaceOpacity, Palette=SelectedContourPalette, ReversePalette=ReversePalette, Bands=ContourBands, Background=SceneBackground, SelectedNodes=SelectedNodeIds.ToArray(), Appearance=CaptureAppearance() };
    }
    [RelayCommand] void RecallView()
    {
        if(SelectedSavedView is {} view) ApplyView(view);
    }
    public void ApplyView(ModelViewPreset v)
    {
        if(Snapshot==null) return;
        updating=true;
        try
        {
            SelectedMode=v.Mode; SelectedFamily=v.Family; SelectedAxes=v.Axes; SelectedCase=Cases.Contains(v.Case??"")?v.Case:Cases.FirstOrDefault();
            SelectedComponent=Components.Contains(v.Component)?v.Component:Components.FirstOrDefault()??""; ForceUnit=v.ForceUnit; LengthUnit=v.LengthUnit; SelectedContourValues=v.Averaging;
            ClearVisibilityCriteria(); hiddenElements.Clear(); isolatedElements=v.Visible?.ToHashSet(); ApplyVisibility();
            ClipEnabled=v.Clip; ClipAxis=v.ClipAxis; ClipPercent=v.ClipPercent; ClipReverse=v.ClipReverse; ClipNodes=v.ClipNodes;
            ShowMesh=v.Mesh; ShowOutlines=v.Outlines; SurfaceOpacity=v.Opacity; SelectedContourPalette=v.Palette; ReversePalette=v.ReversePalette; ContourBands=v.Bands;
            ApplyBackgroundCommand.Execute(v.Background); SelectedColorScale=0;
            if(v.Appearance is {} appearance) ApplyAppearance(appearance);
        }
        finally { updating=false; }
        RefreshScene(); SelectedColorScale=v.Scale;
        if(v.Scale==3) { ColorScaleLower=v.Lower.ToString("R",CultureInfo.InvariantCulture); ColorScaleUpper=v.Upper.ToString("R",CultureInfo.InvariantCulture); ApplyColorScaleCommand.Execute(null); }
        SelectionTarget=0; SelectElements(v.Selected); SelectedNodeIds=v.SelectedNodes.ToHashSet(); OnPropertyChanged(nameof(SelectedNodeIds)); RefreshScene(); if(v.Camera!=null) services.RestoreCamera(v.Camera); ExplorationMessage="Vista ripristinata: "+v.Name;
    }
    [RelayCommand] void DeleteView()
    {
        if(SelectedSavedView is not {} v) return;
        try { services.SaveViews(viewRevision,SavedViews.Where(x=>x!=v).ToArray()); SavedViews.Remove(v); SelectedSavedView=SavedViews.FirstOrDefault(); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { ExplorationMessage="Vista non rimossa: "+ex.Message; }
    }
}
