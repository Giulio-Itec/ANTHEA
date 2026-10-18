using System.Windows;
using System.Windows.Controls;
using X.Desktop.Themes;
using Anthea.Presentation.Modules.Spectra;

namespace X.Desktop.Views.Spectra;

/// <summary>Graphics only. Engineering series and node coordinates are supplied by the ViewModel.</summary>
public sealed class SpectraPlot : ContentControl
{
    private readonly Plot plot = new() { InvertY = false, XLabel = "Periodo T [s]", YLabel = "Accelerazione Sa [g]", FollowAppearance = true, XMinimum = 0, AxisNumberFormat = "0.###" };
    public static readonly DependencyProperty CurvesProperty = DependencyProperty.Register(nameof(Curves), typeof(IReadOnlyList<SpectraCurve>), typeof(SpectraPlot), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(SpectraPlot), new PropertyMetadata("Spettri", Changed));
    public IReadOnlyList<SpectraCurve>? Curves { get => (IReadOnlyList<SpectraCurve>?)GetValue(CurvesProperty); set => SetValue(CurvesProperty, value); }
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public SpectraPlot() { Content = plot; HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch; }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SpectraPlot)d).Refresh();
    private void Refresh() { string[] colors = ["Blue", "Red", "Green", "Orange", "Magenta", "Steel", "Amber"]; plot.Series = (Curves ?? []).Select((c, i) => new Serie(c.Name, c.Points.ToList(), ThemeTokens.Brush("Series." + colors[i % colors.Length]), c.Dashed || i >= colors.Length)).ToList(); plot.Title = Caption; plot.ResetView(); }
}

public sealed class SpectraGrid : ContentControl
{
    private readonly Plot plot = new() { Title = "Nodi del reticolo e sito", InvertY = false, XLabel = "Est relativo [km]", YLabel = "Nord relativo [km]", FollowAppearance = true, FitPadding = .15 };
    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(nameof(Nodes), typeof(IReadOnlyList<SpectraMarker>), typeof(SpectraGrid), new PropertyMetadata(null, Changed));
    public IReadOnlyList<SpectraMarker>? Nodes { get => (IReadOnlyList<SpectraMarker>?)GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public SpectraGrid() { Content = plot; HorizontalContentAlignment = HorizontalAlignment.Stretch; VerticalContentAlignment = VerticalAlignment.Stretch; }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) { var self = (SpectraGrid)d; var nodes = self.Nodes ?? []; self.plot.Markers = nodes.Select(n => new PlotMarker(n.X, n.Y, n.Label, ThemeTokens.Brush("Series.Blue"))).Append(new PlotMarker(0, 0, "Sito", ThemeTokens.Brush("Series.Red"))).ToList(); self.plot.Series = nodes.Count > 1 ? [new Serie("Reticolo", nodes.Select(n => new[] { n.X, n.Y }).Append(new[] { nodes[0].X, nodes[0].Y }).ToList(), ThemeTokens.Brush("Series.Steel"))] : []; self.plot.ResetView(); }
}
