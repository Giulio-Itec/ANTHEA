using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of <see cref="ConcreteModelColumnViewModel"/> (tab 08 of the RC section, 1.15.0; the reduced domain in 1.16.0). The code-behind does only
/// what decision Q3 allows, as the view of the tab 07: the three charts (<see cref="PlotPresentation.ModelColumnPlot"/>) fed by the charts of the
/// ViewModel, «Adatta», «PNG» and «Espandi». No calculation and no write of the document.
/// </summary>
public partial class ConcreteModelColumnView : UserControl
{
    private ConcreteModelColumnViewModel? column;

    /// <summary>The chart M(χ) with the external moment.</summary>
    internal Plot CapacityPlot { get; } = PlotPresentation.ModelColumnPlot();
    /// <summary>The chart M − M2.</summary>
    internal Plot FirstOrderPlot { get; } = PlotPresentation.ModelColumnPlot();
    /// <summary>The reduced domain N–M1Rd,max (1.16.0).</summary>
    internal Plot DomainPlot { get; } = PlotPresentation.ModelColumnPlot();

    public ConcreteModelColumnView()
    {
        InitializeComponent();
        CapacityHost.Content = CapacityPlot; FirstOrderHost.Content = FirstOrderPlot; DomainHost.Content = DomainPlot;
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteModelColumnViewModel);
    }

    private void Attach(ConcreteModelColumnViewModel? next)
    {
        if (ReferenceEquals(column, next)) return;
        if (column is not null) { column.CapacityChart.Changed -= OnChartChanged; column.FirstOrderChart.Changed -= OnChartChanged; column.DomainChart.Changed -= OnChartChanged; }
        column = next;
        if (column is null) return;
        column.CapacityChart.Changed += OnChartChanged; column.FirstOrderChart.Changed += OnChartChanged; column.DomainChart.Changed += OnChartChanged;
        Draw();
    }

    private void OnChartChanged(object? sender, EventArgs e) => Draw();

    private void Draw()
    {
        if (column is null) return;
        ConcreteSectionDrawings.Apply(CapacityPlot, column.CapacityChart);
        ConcreteSectionDrawings.Apply(FirstOrderPlot, column.FirstOrderChart);
        ConcreteSectionDrawings.Apply(DomainPlot, column.DomainChart);
    }

    private void FitCapacity_Click(object sender, RoutedEventArgs e) => CapacityPlot.ResetView();

    private void PngCapacity_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.SavePng(this, CapacityPlot, "Colonna modello M · χ");

    private void ExpandCapacity_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.Expand(this, CapacityHost, CapacityPlot, "Colonna modello · M [kNm] · χ [1/m]");

    private void FitFirstOrder_Click(object sender, RoutedEventArgs e) => FirstOrderPlot.ResetView();

    private void PngFirstOrder_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.SavePng(this, FirstOrderPlot, "Colonna modello M − M2");

    private void ExpandFirstOrder_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.Expand(this, FirstOrderHost, FirstOrderPlot, "Colonna modello · M − M2 [kNm] · χ [1/m]");

    private void FitDomain_Click(object sender, RoutedEventArgs e) => DomainPlot.ResetView();

    private void PngDomain_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.SavePng(this, DomainPlot, "Colonna modello dominio ridotto");

    private void ExpandDomain_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.Expand(this, DomainHost, DomainPlot, "Colonna modello · dominio ridotto N–M1Rd,max");
}
