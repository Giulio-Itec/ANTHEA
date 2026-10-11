using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public ModelViewerViewModel? Comparison { get; private set; }
    public bool CanShareComparisonScale => SelectedComponent.StartsWith('M')==(Components.Contains(CompareComponent)?CompareComponent:SelectedComponent).StartsWith('M');
    [ObservableProperty] private bool showBeamDiagram;
    [ObservableProperty] private bool showLineContour = true;
    [ObservableProperty] private bool showBeamDiagramValues;
    [ObservableProperty] private int selectedBeamDiagramAxis;
    public IReadOnlyList<string> BeamDiagramAxes { get; } = ["Automatico", "Y locale", "Z locale"];
    public bool BeamDiagramAlongY => SelectedBeamDiagramAxis == 1 || SelectedBeamDiagramAxis == 0 && SelectedComponent is "Fy" or "Mz";
    public string LineRepresentationMessage => !IsExtruded || SelectedSurfaceMode == 2
        ? "Contouring lungo l’asse. Diagrammi fra le stazioni importate; un solo campione resta puntuale."
        : "Contouring sulle sezioni rettangolari centrate disponibili. Profili assenti o non supportati restano sull’asse; offset delle aste non importati.";
    [ObservableProperty] private double beamDiagramScale=1;
    partial void OnShowBeamDiagramChanged(bool value)=>RefreshScene();
    partial void OnShowLineContourChanged(bool value)=>RefreshScene();
    partial void OnShowBeamDiagramValuesChanged(bool value)=>RefreshScene();
    partial void OnSelectedBeamDiagramAxisChanged(int value)=>RefreshScene();
    partial void OnBeamDiagramScaleChanged(double value)=>RefreshScene();
    private void UpdateComparison()
    {
        OnPropertyChanged(nameof(CanShareComparisonScale));
        if(!CompareEnabled||Snapshot==null)return;
        if(Comparison==null||!ReferenceEquals(Comparison.Snapshot,Snapshot))
        {
            Comparison?.Dispose();Comparison=new(Snapshot,[],true,new ComparisonServices(),_=>{},(_,_,_,_)=>{});
        }
        var c=Comparison;c.updating=true;
        try
        {
            c.SelectedRepresentation=SelectedRepresentation;c.ShowResults=ShowResults;c.SelectedFamily=SelectedFamily;c.SelectedAxes=SelectedAxes;
            c.SelectedResultType=SelectedResultType;c.DisplacementUnit=DisplacementUnit;
            c.SelectedCase=Cases.Contains(CompareCase??"")?CompareCase:SelectedCase;
            c.SelectedComponent=Components.Contains(CompareComponent)?CompareComponent:SelectedComponent;
            c.ForceUnit=ForceUnit;c.LengthUnit=LengthUnit;c.SelectedBeamLocation=SelectedBeamLocation;c.SelectedContourValues=CompareAveraging;
            c.VisibleElementIds=VisibleElementIds;c.SelectedElementIds=SelectedElementIds;c.ShowMesh=ShowMesh;c.ShowContext=ShowContext;
            c.ShowOutlines=ShowOutlines;c.SurfaceOpacity=SurfaceOpacity;c.SelectedContourPalette=SelectedContourPalette;c.ReversePalette=ReversePalette;c.ContourBands=ContourBands;
            c.ShowLocalAxes=ShowLocalAxes;c.ShowNodeAxes=ShowNodeAxes;c.ShowRestraints=ShowRestraints;c.ShowConstraints=ShowConstraints;
            c.ShowLinks=ShowLinks;c.ShowReleases=ShowReleases;c.OverlaysOnlySelection=OverlaysOnlySelection;c.SelectedNodeIds=SelectedNodeIds;
            c.ShowElementDirections=ShowElementDirections;c.ShowBoundaryLabels=ShowBoundaryLabels;c.OverlayScale=OverlayScale;
            c.SelectedQuality=SelectedQuality;c.ShowIsolines=ShowIsolines;c.IsolineCount=IsolineCount;c.IsolineWidth=IsolineWidth;
            c.SelectedColorScale=SelectedColorScale==3?0:SelectedColorScale;c.ShowBeamDiagram=ShowBeamDiagram;c.BeamDiagramScale=BeamDiagramScale;
            c.ShowLineContour=ShowLineContour;c.ShowBeamDiagramValues=ShowBeamDiagramValues;c.SelectedBeamDiagramAxis=SelectedBeamDiagramAxis;
            c.SelectedSurfaceMode=SelectedSurfaceMode;
            c.SelectedColorMode=SelectedColorMode;c.categoryColors.Clear();
            foreach(var color in categoryColors)c.categoryColors[color.Key]=color.Value;
            c.ShowLoads=ShowLoads;c.SelectedLoadCase=SelectedLoadCase;c.LoadGlyphScale=LoadGlyphScale;c.ShowDeformation=ShowDeformation;c.DeformationScale=DeformationScale;c.LegendPrecision=LegendPrecision;
            c.SelectedDeformationScale=SelectedDeformationScale;c.ShowUndeformed=ShowUndeformed;c.UndeformedOpacity=UndeformedOpacity;
            c.DeformationPhase=DeformationPhase;
            c.ShowUndeformedMesh=ShowUndeformedMesh;
            c.ShowExtrema=ShowExtrema;c.SceneBackground=SceneBackground;c.AvoidLabelOverlap=AvoidLabelOverlap;
            c.ClipEnabled=ClipEnabled;c.ClipAxis=ClipAxis;c.ClipPercent=ClipPercent;c.ClipReverse=ClipReverse;c.ClipNodes=ClipNodes;
            c.ClipShape=ClipShape;c.ApplyClipBox(CaptureClipBox());
            c.ApplyMeasurements(CaptureMeasurements());
            c.ApplyRevisionOverlay(ActiveRevisionOverlay);
        }
        finally {c.updating=false;}
        c.RefreshScene();
        if(CompareCommonScale&&CanShareComparisonScale&&HasResults&&c.HasResults)
        {
            double low=SelectedColorScale==3?ColorMinimum:Math.Min(ColorMinimum,c.ColorMinimum),high=SelectedColorScale==3?ColorMaximum:Math.Max(ColorMaximum,c.ColorMaximum);
            SetLegendRange(low,high);c.SetLegendRange(low,high);
        }
        OnPropertyChanged(nameof(Comparison));
    }
    void SetLegendRange(double low,double high)
    {
        ColorMinimum=low;ColorMaximum=high;ColorMinimumLabel=FormatLegend(low);ColorMaximumLabel=FormatLegend(high);
        foreach(string p in new[]{nameof(ColorMinimumLabel),nameof(ColorMaximumLabel),nameof(ColorMiddleLabel),nameof(LegendTicks)})OnPropertyChanged(p);
    }
    sealed class ComparisonServices:IModelViewerServices
    {
        public Task<ModelSnapshot?> ImportAsync(CancellationToken cancellationToken)=>Task.FromResult<ModelSnapshot?>(null);
        public void FitView(){} public void SaveImage(){} public void ExportTable(string name,string csv){}
    }
}
