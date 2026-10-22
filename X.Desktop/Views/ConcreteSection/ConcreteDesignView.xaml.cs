using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of <see cref="ConcreteDesignViewModel"/> (tab 08 of the RC section, F5.10 view after S9). The code-behind does only what decision Q3 allows: the
/// section of the historic view (<see cref="ConcreteSectionViewport"/>) fed by <see cref="ConcreteDesignViewModel.Preview"/>, «Adatta», «PNG» and
/// «Espandi». No calculation and no write of the document.
/// </summary>
public partial class ConcreteDesignView : UserControl
{
    private ConcreteDesignViewModel? design;

    /// <summary>The configuration selected, drawn.</summary>
    internal ConcreteSectionViewport Preview { get; } = new();

    public ConcreteDesignView()
    {
        InitializeComponent();
        PreviewHost.Content = Preview;
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteDesignViewModel);
    }

    private void Attach(ConcreteDesignViewModel? next)
    {
        if (ReferenceEquals(design, next)) return;
        if (design is not null) { design.Preview.Changed -= OnPreviewChanged; design.Preview.PropertyChanged -= OnPreviewChanged; }
        design = next;
        if (design is null) return;
        design.Preview.Changed += OnPreviewChanged; design.Preview.PropertyChanged += OnPreviewChanged;
        Draw();
    }

    private void OnPreviewChanged(object? sender, EventArgs e) => Draw();

    private void Draw() { if (design is not null) ConcreteSectionDrawings.Apply(Preview, design.Preview); }

    private void Fit_Click(object sender, RoutedEventArgs e) => Preview.ResetView();

    private void Png_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.SavePng(this, Preview, "Configurazione selezionata");

    private void Expand_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.Expand(this, PreviewHost, Preview, "Configurazione selezionata");
}
