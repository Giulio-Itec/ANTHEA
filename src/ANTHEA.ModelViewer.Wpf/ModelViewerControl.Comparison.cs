using ANTHEA.ModelViewer.Presentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HelixToolkit.Wpf.SharpDX;

namespace ANTHEA.ModelViewer.Wpf;

public partial class ModelViewerControl
{
    private Viewport3DX? comparisonViewport;
    private ModelSceneRenderer? comparisonRenderer;
    private ANTHEA.ModelWorkspace.ModelSnapshot? comparisonSnapshot;
    private TextBlock? comparisonTitle;
    private ModelContourLegend? comparisonLegend;
    internal static SceneOptions Options(ModelViewerViewModel state)=>new(state.ShowResults,state.IsExtruded,state.ContourValues,state.Family,state.Subdivisions,state.ShowMesh,state.ShowIsolines,state.VisibleElementIds,state.SelectedElementIds,state.ShowLocalAxes,
        state.ShowRestraints,state.ShowConstraints,state.ShowLinks,state.ShowReleases,state.ShowContext,
        new(state.ElementColors,state.SelectedSurfaceMode,state.ShowHiddenWireframe,state.ShowNodeIds,state.ShowElementIds,state.ShowPropertyIds,state.ShowNodes,state.SelectedLabelScope==1,state.LabelScale,state.LineWidth),
        state.ColorMinimum,state.ColorMaximum,state.ShowNodeAxes,state.OverlaysOnlySelection,state.SelectedNodeIds,state.SelectedLighting,state.SelectedContourPalette,state.IsolineCount,state.IsolineWidth,
        new(state.ActiveClipPlane,state.ShowOutlines,state.SurfaceOpacity,state.ReversePalette,state.ContourBands,state.AvoidLabelOverlap,[..state.ExtremeProbes,..state.PinnedProbes],state.IsBeamFamily&&state.ShowBeamDiagram,state.BeamDiagramScale,state.ActiveLoads,state.LoadGlyphScale,state.ShowDeformation&&state.CanDeform?state.Displacements:[],state.EffectiveDeformationScale*state.DeformationPhase,state.BeamDiagramAlongY,state.ShowBeamDiagramValues,state.ShowUndeformed,state.UndeformedOpacity,state.ShowUndeformedMesh,state.ActiveClipPlanes,state.ActiveClipBox,state.MeasurementAnnotations,state.PendingMeasurementNodes,state.ActiveRevisionOverlay),LineContour:state.IsDisplacementResult||state.ShowLineContour,
        ElementDirections:state.ShowElementDirections,BoundaryLabels:state.ShowBoundaryLabels,OverlayScale:state.OverlayScale);
    private void UpdateComparisonScene()
    {
        if(!viewModel.CompareEnabled||viewModel.Comparison is not {} state) {DisposeComparison();return;}
        if(comparisonViewport==null)
        {
            var panel=new Grid();panel.ColumnDefinitions.Add(new());panel.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            comparisonViewport=new Viewport3DX { ShowCoordinateSystem=false,ShowViewCube=false,IsPanEnabled=false,IsRotationEnabled=false,IsZoomEnabled=false };
            comparisonTitle=new TextBlock { Margin=new(12,8,12,8),TextWrapping=TextWrapping.Wrap,FontSize=11,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false };
            comparisonLegend=new ModelContourLegend { VerticalAlignment=VerticalAlignment.Center };
            panel.Children.Add(comparisonViewport);panel.Children.Add(comparisonTitle);Grid.SetColumn(comparisonLegend,1);panel.Children.Add(comparisonLegend);
            ComparisonHost.Content=panel;comparisonRenderer=new(comparisonViewport);
            previousCamera=renderer?.CameraState();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>{
                if(!disposed&&comparisonViewport!=null&&viewModel.Snapshot is {} model)
                    renderer?.Focus(viewModel.VisibleElementIds??model.Elements.Select(e=>e.Id).ToHashSet());
            }));
        }
        if(!ReferenceEquals(comparisonSnapshot,state.Snapshot)){comparisonRenderer!.SetModel(state.Snapshot!,state.SectionCatalog);comparisonSnapshot=state.Snapshot;}
        SyncComparisonCamera();comparisonViewport.BackgroundColor=(Color)ColorConverter.ConvertFromString(state.SceneBackground);
        comparisonViewport.MSAA=Viewport.MSAA;comparisonViewport.FXAALevel=Viewport.FXAALevel;
        comparisonRenderer!.Render(Options(state));
        comparisonTitle!.Text=state.ResultHeading;comparisonTitle.Foreground=(Brush)new BrushConverter().ConvertFromString(state.SceneInk)!;
        comparisonLegend!.DataContext=state;comparisonLegend.Visibility=state.IsResultsView?Visibility.Visible:Visibility.Collapsed;
    }
    private void DisposeComparison()
    {
        ComparisonHost.Content=null;comparisonRenderer?.Dispose();comparisonRenderer=null;comparisonViewport=null;comparisonSnapshot=null;
    }
    private void SyncComparisonCamera()
    {
        if(comparisonViewport==null||Viewport.Camera is not ProjectionCamera source)return;
        // Keep one user-controlled pose, but separate camera cores: each render host advances its own clock.
        // Sharing an animated core makes two hosts advance the same zoom/rotation animation.
        bool orthographic=source is OrthographicCamera;
        comparisonViewport.Orthographic=orthographic;
        if(comparisonViewport.Camera is not ProjectionCamera target || (target is OrthographicCamera)!=orthographic)
            comparisonViewport.Camera=target=orthographic?new OrthographicCamera():new PerspectiveCamera();
        target.Position=source.Position;target.LookDirection=source.LookDirection;target.UpDirection=source.UpDirection;
        target.NearPlaneDistance=source.NearPlaneDistance;target.FarPlaneDistance=source.FarPlaneDistance;
        if(source is OrthographicCamera a && target is OrthographicCamera b)b.Width=a.Width;
        if(source is PerspectiveCamera c && target is PerspectiveCamera d)d.FieldOfView=c.FieldOfView;
    }
    private void SaveConfiguredImage(string path)
    {
        if(!viewModel.ExportSceneOnly){ViewerImageExporter.Save(this,Viewport,path);return;}
        ViewerImageExporter.SaveScene(Viewport,comparisonViewport,viewModel,path);
    }
}
