using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of <see cref="ConcreteCurvatureViewModel"/> (tab 07 of the RC section, F5.10 view after S9). The code-behind does only what decision Q3 allows:
/// the chart of the historic view (<see cref="PlotPresentation.CurvaturePlot"/>) fed by <see cref="ConcreteCurvatureViewModel.Chart"/>, «Adatta», «PNG»
/// and «Espandi». No calculation and no write of the document.
/// </summary>
public partial class ConcreteCurvatureView : UserControl
{
    private ConcreteCurvatureViewModel? curvature;

    /// <summary>The chart M–χ.</summary>
    internal Plot Chart { get; } = PlotPresentation.CurvaturePlot();

    public ConcreteCurvatureView()
    {
        InitializeComponent();
        ChartHost.Content = Chart;
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteCurvatureViewModel);
    }

    private void Attach(ConcreteCurvatureViewModel? next)
    {
        if (ReferenceEquals(curvature, next)) return;
        if (curvature is not null) curvature.Chart.Changed -= OnChartChanged;
        curvature = next;
        if (curvature is null) return;
        curvature.Chart.Changed += OnChartChanged;
        Draw();
    }

    private void OnChartChanged(object? sender, EventArgs e) => Draw();

    private void Draw() { if (curvature is not null) ConcreteSectionDrawings.Apply(Chart, curvature.Chart); }

    private void Fit_Click(object sender, RoutedEventArgs e) => Chart.ResetView();

    private void Png_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.SavePng(this, Chart, "M [kNm] · χ [1/m]");

    private void Expand_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.Expand(this, ChartHost, Chart, "M [kNm] · χ [1/m]");
}
