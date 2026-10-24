using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of a <see cref="ConcreteStressPanelViewModel"/> (a family of the tab 04 of the RC section, F5.10 view after S9). The code-behind does only what
/// decision Q3 allows: the stress map of the historic view (<see cref="ConcreteSectionViewport"/>) fed by <see cref="ConcreteStressPanelViewModel.Map"/>,
/// a bar clicked on the map back to the family, «Adatta», «PNG» and «Espandi». No calculation and no write of the document.
/// </summary>
public partial class ConcreteStressPanelView : UserControl
{
    private ConcreteStressPanelViewModel? panel;

    /// <summary>The stress map of the family.</summary>
    internal ConcreteSectionViewport Map { get; } = new();

    public ConcreteStressPanelView()
    {
        InitializeComponent();
        MapHost.Content = Map;
        Map.BarSelected += index => panel?.Map.ClickBar(index);
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteStressPanelViewModel);
    }

    private void Attach(ConcreteStressPanelViewModel? next)
    {
        if (ReferenceEquals(panel, next)) return;
        if (panel is not null) { panel.Map.Changed -= OnMapChanged; panel.Map.PropertyChanged -= OnMapChanged; }
        panel = next;
        if (panel is null) return;
        panel.Map.Changed += OnMapChanged; panel.Map.PropertyChanged += OnMapChanged;
        Draw();
    }

    private void OnMapChanged(object? sender, EventArgs e) => Draw();

    private void Draw() { if (panel is not null) ConcreteSectionDrawings.Apply(Map, panel.Map); }

    private void Fit_Click(object sender, RoutedEventArgs e) => Map.ResetView();

    private void Png_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.SavePng(this, Map, "Mappa tensionale della sezione");

    private void Expand_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.Expand(this, MapHost, Map, "Mappa tensionale della sezione");
}
