using System.ComponentModel;
using System.Windows.Controls;
using ANTHEA.ModelViewer.Wpf;
using Anthea.Presentation.Shell.Projects;

namespace X.Desktop.Views.Shell;

/// <summary>
/// DRAFT (F5.13 c): the view of <see cref="ModelTabViewModel"/>. The code-behind creates the existing <see cref="ModelViewerControl"/> on the model,
/// the sheets and the read-only state of the ViewModel, with its import and link going to the ViewModel; it creates it again when the ViewModel
/// follows another document (undo, revisions) and gives it the name of the model.
/// </summary>
public partial class ModelTabView : UserControl
{
    private ModelTabViewModel? model;
    private ModelViewerControl? viewer;

    public ModelTabView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as ModelTabViewModel);
        Unloaded += (_, _) => Attach(null);
        Loaded += (_, _) => Attach(DataContext as ModelTabViewModel);
    }

    private void Attach(ModelTabViewModel? value)
    {
        if (ReferenceEquals(model, value)) return;
        if (model is not null) model.PropertyChanged -= OnModelChanged;
        model = value;
        if (model is not null) model.PropertyChanged += OnModelChanged;
        CreateViewer();
    }

    private void CreateViewer()
    {
        viewer = null;
        Viewer.Content = null;
        if (model is null) return;
        var current = model;
        viewer = new ModelViewerControl(current.Snapshot, current.Targets, current.IsReadOnly, current.Imported, current.Linked);
        viewer.SetDisplayName(current.Title);
        Viewer.Content = viewer;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModelTabViewModel.Snapshot) or nameof(ModelTabViewModel.IsReadOnly)) CreateViewer();
        else if (e.PropertyName == nameof(ModelTabViewModel.Title) && model is not null) viewer?.SetDisplayName(model.Title);
    }
}
