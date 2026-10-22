using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of <see cref="ConcreteTemplateWizardViewModel"/> (the window of the wizard, F5.10 view after S9): the preview of the historic window
/// (<see cref="ConcreteSectionViewport"/> with labels and axes) fed by the geometry and the error of the wizard at every PreviewChanged (Q3). No
/// calculation and no write of the document.
/// </summary>
public partial class ConcreteTemplateWizardView : UserControl
{
    private ConcreteTemplateWizardViewModel? wizard;

    /// <summary>The preview of the section of the wizard.</summary>
    internal ConcreteSectionViewport Preview { get; } = ConcreteSectionDrawings.WizardPreview();

    public ConcreteTemplateWizardView()
    {
        InitializeComponent();
        PreviewHost.Content = Preview;
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteTemplateWizardViewModel);
    }

    private void Attach(ConcreteTemplateWizardViewModel? next)
    {
        if (ReferenceEquals(wizard, next)) return;
        if (wizard is not null) wizard.PreviewChanged -= OnPreviewChanged;
        wizard = next;
        if (wizard is null) return;
        wizard.PreviewChanged += OnPreviewChanged;
        Draw();
    }

    private void OnPreviewChanged(object? sender, EventArgs e) => Draw();

    // UpdatePreview of the historic window.
    private void Draw()
    {
        if (wizard is null) return;
        Preview.Section = wizard.Geometry; Preview.Message = wizard.Error; Preview.InvalidateVisual();
    }
}
