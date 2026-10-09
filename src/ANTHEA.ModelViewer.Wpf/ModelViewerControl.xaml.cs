using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Windows;
using System.Windows.Controls;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>WPF lifecycle and rendering bridge. Application state and commands live in the ViewModel.</summary>
public partial class ModelViewerControl : UserControl, IDisposable
{
    private readonly ModelViewerViewModel viewModel;
    private ModelSceneRenderer? renderer;
    private ModelSnapshot? renderedSnapshot;
    private bool disposed;

    public ModelViewerControl(ModelSnapshot? snapshot, IReadOnlyList<SheetTarget> sheets, bool readOnly,
        Action<ModelSnapshot> imported, Action<string, int, string, string> linked)
    {
        InitializeComponent();
        var services = new WpfModelViewerServices(() => Window.GetWindow(this), () => renderer?.Fit(), path => renderer?.SaveImage(path));
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
            var statistics = renderer.Render(new(viewModel.SelectedMode, viewModel.SelectedCase, viewModel.SelectedComponent,
                viewModel.Subdivisions, viewModel.ShowMesh, viewModel.ShowIsolines));
            viewModel.Status = $"Scena pronta · {statistics.Preparation.TotalMilliseconds:N0} ms di preparazione · {statistics.Triangles:N0} triangoli";
            if (viewModel.IsResultsView) viewModel.Status += $" · {statistics.MissingElements} piastre senza risultati";
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
