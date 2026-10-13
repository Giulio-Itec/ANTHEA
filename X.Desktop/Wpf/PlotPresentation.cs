using Anthea.Application.Presentation;
using System.Windows.Media;
namespace X.Desktop;

internal static class PlotPresentation
{
    internal static Serie Series(PlotSeriesData source) => new(source.Name, source.Points, Ui.Brush(source.Color), source.Dashed, source.Highlighted,
        source.DashPattern switch { "Dash" => DashStyles.Dash, "Dot" => DashStyles.Dot, null => null, _ => throw new ArgumentException("Tratteggio non disponibile.") });
    internal static void Apply(Plot target, PlotData source)
    {
        target.Title = source.Title; target.XLabel = source.XLabel; target.YLabel = source.YLabel; target.Note = source.Note;
        target.XMinimum = source.XMinimum; target.Series = source.Series.Select(Series).ToList();
        target.Markers = source.Markers.Select(m => new PlotMarker(m.X, m.Y, m.Label, Ui.Brush(m.Color), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius)).ToList();
        target.InvalidateVisual();
    }
}
