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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (disposed) return;
        renderer ??= new ModelSceneRenderer(Viewport);
        UpdateScene(); renderer.Fit();
    }

    private void SceneChanged(object? sender, EventArgs e) => UpdateScene();
    private void TreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ModelTreeItem item) viewModel.SelectedTreeItem = item;
    }
    private void SceneMouseDown(object? sender, RoutedEventArgs args)
    {
        if (args is MouseDown3DEventArgs e && e.OriginalInputEventArgs is MouseButtonEventArgs mouse && mouse.ChangedButton == MouseButton.Left && renderer?.ElementAt(e.HitTestResult) is int id)
            viewModel.SelectElement(id, Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
    }
    private void OnUnloaded(object sender, RoutedEventArgs e) => Dispose();

    private void UpdateScene()
    {
        if (disposed || renderer == null || viewModel.Snapshot == null) return;
        try
        {
            if (!ReferenceEquals(renderedSnapshot, viewModel.Snapshot))
            {
                renderer.SetModel(viewModel.Snapshot); renderedSnapshot = viewModel.Snapshot;
            }
            var statistics = renderer.Render(new(viewModel.SelectedMode, viewModel.ActiveValues, viewModel.Family,
                viewModel.Subdivisions, viewModel.ShowMesh, viewModel.ShowIsolines, viewModel.VisibleElementIds, viewModel.SelectedElementIds, viewModel.ShowLocalAxes,
                viewModel.ShowRestraints, viewModel.ShowConstraints, viewModel.ShowLinks, viewModel.ShowReleases, viewModel.ShowContext,
                new(viewModel.ElementColors, viewModel.SelectedSurfaceMode, viewModel.ShowHiddenWireframe, viewModel.ShowNodeIds,
                    viewModel.ShowElementIds, viewModel.ShowPropertyIds, viewModel.ShowNodes, viewModel.SelectedLabelScope == 1, viewModel.LabelScale, viewModel.LineWidth)));
            viewModel.Status = $"Scena pronta · {statistics.Preparation.TotalMilliseconds:N0} ms di preparazione · {statistics.Triangles:N0} triangoli";
            if (viewModel.IsResultsView) viewModel.Status += $" · {statistics.MissingElements} {viewModel.Family.ToLowerInvariant()} senza risultati";
            if (viewModel.ShowLocalAxes && viewModel.Snapshot.LocalFrames == null) viewModel.Status += " · terne locali non importate";
        }
        catch (Exception ex) { viewModel.Status = "Vista non disponibile: " + ex.Message; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Loaded -= OnLoaded; Unloaded -= OnUnloaded; viewModel.SceneChanged -= SceneChanged;
        viewModel.Dispose(); renderer?.Dispose(); renderer = null; renderedSnapshot = null;
    }
}
