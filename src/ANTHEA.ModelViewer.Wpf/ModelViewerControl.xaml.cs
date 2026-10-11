using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HelixToolkit.Wpf.SharpDX;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>WPF lifecycle and rendering bridge. Application state and commands live in the ViewModel.</summary>
public partial class ModelViewerControl : UserControl, IDisposable
{
    private readonly ModelViewerViewModel viewModel;
    private ModelSceneRenderer? renderer;
    private ModelSnapshot? renderedSnapshot;
    private bool disposed;
    public void SetDisplayName(string name) => viewModel.SetDisplayName(name);

    public ModelViewerControl(ModelSnapshot? snapshot, IReadOnlyList<SheetTarget> sheets, bool readOnly,
        Action<ModelSnapshot> imported, Action<string, int, string, string> linked)
    {
        InitializeComponent();
        var services = new WpfModelViewerServices(() => Window.GetWindow(this), () => renderer?.Fit(viewModel?.IncludeHiddenInFit ?? false), path => ViewerImageExporter.Save(this, Viewport, path));
        viewModel = new ModelViewerViewModel(snapshot, sheets, readOnly, services, imported, linked);
        DataContext = viewModel;
        viewModel.SceneChanged += SceneChanged;
        viewModel.ViewSettingsChanged += ViewSettingsChanged;
        viewModel.CameraPresetRequested += CameraPresetRequested;
        PreviewKeyDown += ViewerKeyDown;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (disposed) return;
        renderer ??= new ModelSceneRenderer(Viewport);
        ApplyViewSettings(); UpdateScene(); renderer.Fit();
    }
    private void ViewerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        viewModel.ClearSelectionCommand.Execute(null); viewModel.ShowViewSettings = false; e.Handled = true;
    }
    private void ViewSettingsChanged(object? sender, EventArgs e) => ApplyViewSettings();
    private void CameraPresetRequested(object? sender, string preset) => renderer?.SetCameraPreset(preset);
    private void ApplyViewSettings()
    {
        Viewport.RotationSensitivity = Math.Clamp(viewModel.RotationSensitivity, .1, 2);
        Viewport.LeftRightPanSensitivity = Viewport.UpDownPanSensitivity = Math.Clamp(viewModel.PanSensitivity, .1, 2);
        Viewport.ZoomSensitivity = Math.Clamp(viewModel.ZoomSensitivity, .1, 2);
        Viewport.CameraRotationMode = viewModel.SelectedRotationMode == 0 ? HelixToolkit.SharpDX.CameraRotationMode.Turntable : HelixToolkit.SharpDX.CameraRotationMode.Trackball;
        Viewport.IsInertiaEnabled = viewModel.CameraInertia; Viewport.RotateAroundMouseDownPoint = viewModel.RotateAroundPointer;
        Viewport.ZoomAroundMouseDownPoint = true; Viewport.Orthographic = viewModel.IsOrthographic;
        Viewport.MSAA = viewModel.SelectedAntialiasing switch { 2 => HelixToolkit.SharpDX.MSAALevel.Four, 3 => HelixToolkit.SharpDX.MSAALevel.Eight, _ => HelixToolkit.SharpDX.MSAALevel.Disable };
        Viewport.FXAALevel = viewModel.SelectedAntialiasing == 1 ? HelixToolkit.SharpDX.FXAALevel.High : HelixToolkit.SharpDX.FXAALevel.None;
        Viewport.EnableSSAO = viewModel.SelectedLighting == 2 && !viewModel.IsResultsView;
        Viewport.CoordinateSystemLabelForeground = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(viewModel.SceneInk);
    }

    private void SceneChanged(object? sender, EventArgs e) => UpdateScene();
    private void TreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ModelTreeItem item) viewModel.SelectedTreeItem = item;
    }
    private void TreeContextSelection(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right) return;
        var source = e.OriginalSource as DependencyObject;
        while (source != null && source is not TreeViewItem)
            source = source is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        if (source is TreeViewItem item && item.DataContext is ModelTreeItem modelItem)
            viewModel.SelectedTreeItem = modelItem;
    }
    private void SceneMouseDown(object? sender, RoutedEventArgs args)
    {
        Viewport.Focus();
        if (args is MouseDown3DEventArgs e && e.OriginalInputEventArgs is MouseButtonEventArgs mouse && mouse.ChangedButton == MouseButton.Left && renderer?.ElementAt(e.HitTestResult) is int id)
            viewModel.SelectElement(id, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }
    private void OnUnloaded(object sender, RoutedEventArgs e) => Dispose();

    private void UpdateScene()
    {
        if (disposed || renderer == null || viewModel.Snapshot == null) return;
        try
        {
            ApplyViewSettings();
            var palette = new System.Windows.Media.LinearGradientBrush { StartPoint = new Point(0, 1), EndPoint = new Point(0, 0) };
            for (int i = 0; i < 5; i++) { var c = Contours.Color(i / 4.0, viewModel.SelectedContourPalette); palette.GradientStops.Add(new(System.Windows.Media.Color.FromRgb((byte)Math.Round(c.Red * 255), (byte)Math.Round(c.Green * 255), (byte)Math.Round(c.Blue * 255)), i / 4.0)); }
            Resources["ModelContourPalette"] = palette;
            if (!ReferenceEquals(renderedSnapshot, viewModel.Snapshot))
            {
                renderer.SetModel(viewModel.Snapshot); renderedSnapshot = viewModel.Snapshot;
            }
            var statistics = renderer.Render(new(viewModel.SelectedMode, viewModel.ActiveValues, viewModel.Family,
                viewModel.Subdivisions, viewModel.ShowMesh, viewModel.ShowIsolines, viewModel.VisibleElementIds, viewModel.SelectedElementIds, viewModel.ShowLocalAxes,
                viewModel.ShowRestraints, viewModel.ShowConstraints, viewModel.ShowLinks, viewModel.ShowReleases, viewModel.ShowContext,
                new(viewModel.ElementColors, viewModel.SelectedSurfaceMode, viewModel.ShowHiddenWireframe, viewModel.ShowNodeIds,
                    viewModel.ShowElementIds, viewModel.ShowPropertyIds, viewModel.ShowNodes, viewModel.SelectedLabelScope == 1, viewModel.LabelScale, viewModel.LineWidth), viewModel.ColorMinimum, viewModel.ColorMaximum, viewModel.ShowNodeAxes, viewModel.OverlaysOnlySelection, viewModel.SelectedNodeIds, viewModel.SelectedLighting, viewModel.SelectedContourPalette, viewModel.IsolineCount, viewModel.IsolineWidth));
            viewModel.Status = $"Scena pronta · {statistics.Preparation.TotalMilliseconds:N0} ms di preparazione · {statistics.Triangles:N0} triangoli";
            if (viewModel.IsResultsView) viewModel.Status += $" · {statistics.MissingElements} {viewModel.Family.ToLowerInvariant()} senza risultati";
            if (viewModel.ShowLocalAxes && viewModel.Snapshot.LocalFrames == null) viewModel.Status += " · terne locali non importate";
            if (viewModel.ShowNodeAxes && viewModel.Snapshot.NodeFrames == null) viewModel.Status += " · terne nodali non importate";
        }
        catch (Exception ex) { viewModel.Status = "Vista non disponibile: " + ex.Message; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Loaded -= OnLoaded; Unloaded -= OnUnloaded; viewModel.SceneChanged -= SceneChanged;
        PreviewKeyDown -= ViewerKeyDown; viewModel.ViewSettingsChanged -= ViewSettingsChanged; viewModel.CameraPresetRequested -= CameraPresetRequested;
        viewModel.Dispose(); renderer?.Dispose(); renderer = null; renderedSnapshot = null;
    }
}
