using System.Windows.Controls;
using Anthea.Presentation.Modules.QuickSection;
using X.Desktop.Views.ConcreteSection;

namespace X.Desktop.Views.QuickSection;

public partial class ConcreteDxfImportView : UserControl
{
    private ConcreteDxfImportViewModel? model;
    internal ConcreteSectionViewport Preview { get; } = ConcreteSectionDrawings.WizardPreview();
    public ConcreteDxfImportView()
    {
        InitializeComponent(); PreviewHost.Content = Preview;
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteDxfImportViewModel);
        Loaded += (_, _) => Attach(DataContext as ConcreteDxfImportViewModel);
        Unloaded += (_, _) => Attach(null);
    }
    private void Attach(ConcreteDxfImportViewModel? next)
    {
        if (ReferenceEquals(model, next)) return;
        if (model is not null) model.PreviewChanged -= Changed;
        model = next;
        if (model is not null) { model.PreviewChanged += Changed; Draw(); }
    }
    private void Changed(object? sender, EventArgs e) => Draw();
    private void Draw()
    { if (model is not null) { Preview.Section = model.Geometry; Preview.Message = model.Error; Preview.InvalidateVisual(); } }
}
