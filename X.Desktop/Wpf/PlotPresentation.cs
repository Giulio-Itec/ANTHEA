using Anthea.Application.Presentation;
using System.Windows.Media;
namespace X.Desktop;

internal static class PlotPresentation
{
    internal static Plot CurvaturePlot() => new()
    {
        Title = "Momento–curvatura", XLabel = "Curvatura χ [1/m]", YLabel = "Momento M [kNm]",
        AxisNumberFormat = "G4", InvertY = false, EmptyMessage = "Impostare N e direzione per calcolare la curva"
    };
    internal static void ApplyCurvature(Plot target, Anthea.Application.Concrete.ConcreteResults.CurvatureResult source)
    {
        target.Series = [new("M–χ", source.Points.Select(p => new[] { p.Curvature, p.Moment }).ToList(), Ui.Blue)];
        target.InvalidateVisual();
    }
    internal static Serie Series(PlotSeriesData source) => new(source.Name, source.Points, Ui.Brush(source.Color), source.Dashed, source.Highlighted,
        source.DashPattern switch { "Dash" => DashStyles.Dash, "Dot" => DashStyles.Dot, null => null, _ => throw new ArgumentException("Tratteggio non disponibile.") });
    internal static void Apply(Plot target, PlotData source)
    {
        target.Title = source.Title; target.XLabel = source.XLabel; target.YLabel = source.YLabel; target.Note = source.Note;
        target.XMinimum = source.XMinimum; target.Series = source.Series.Select(Series).ToList();
        target.Markers = source.Markers.Select(m => new PlotMarker(m.X, m.Y, m.Label, Ui.Brush(m.Color), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius)).ToList();
        target.InvalidateVisual();
    }
    internal static void ApplyDomainGeometry(Plot target, Anthea.Application.Concrete.DomainChartGeometry source)
    {
        target.Series = []; target.Markers = []; target.Segments = source.Segments; target.EmptyMessage = source.EmptyMessage;
        if (source.Title is not null) target.Title = source.Title;
        if (source.XLabel is not null) target.XLabel = source.XLabel;
        if (source.YLabel is not null) target.YLabel = source.YLabel;
        if (source.Note is not null) target.Note = source.Note;
        target.InvalidateVisual();
    }
    internal static void ApplyDomainActions(Plot target, Anthea.Application.Concrete.DomainChartActions source)
    {
        target.Markers = source.Markers.Select(m => new PlotMarker(m.X, m.Y, m.Label, Ui.Brush(m.Color), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius)).ToList();
        target.VerificationSegments = source.Lines; target.ToolTip = UtilizationColors.Legend; target.InvalidateVisual();
    }
}

/// <summary>
/// The colour of a curve (#RRGGBB of the plain series) as the foreground of its check box in the MVVM views (F5.6): the same brush as the
/// historic check boxes of the curves, Appearance.Series of the series colour, lightened in the dark appearances. It lives with the plain
/// series conversion of the charts, not in the views (P7): the curve colours are the colours of the drawing.
/// </summary>
internal sealed class SeriesForegroundConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is string color && color.Length > 0 ? Appearance.Series(Ui.Brush(color)) : System.Windows.DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
