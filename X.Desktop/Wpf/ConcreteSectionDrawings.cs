using Anthea.Application.Reporting;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop;

/// <summary>
/// The WPF drawings of the RC section fed by the plain data of its ViewModel (F5.10, decisions Q3 and C8): the drawings stay those of the
/// historic view (ConcreteSectionViewport, Plot); these functions copy into them the properties that the historic view assigns, for the view
/// of the sheet and for the figures of its report alike.
/// </summary>
internal static class ConcreteSectionDrawings
{
    /// <summary>A section drawing (preview, stress map, section of the shear or of the design) from its data.</summary>
    internal static void Apply(ConcreteSectionViewport target, ConcreteSectionDrawingViewModel source)
    {
        target.Section = source.Section;
        target.Tendons = source.Tendons.Select(t => new TendonPoint(t.Id, t.X, t.Y, t.Area)).ToList();
        target.Message = source.Message;
        target.Contour = source.Contour;
        target.Labels = source.Labels; target.Axes = source.Axes; target.ShowNeutralAxis = source.ShowNeutralAxis;
        target.BarValues = source.BarValues; target.TendonValues = source.TendonValues; target.ConcreteValues = source.ConcreteValues;
        target.Dimensions = source.Dimensions; target.CoverDimensions = source.CoverDimensions; target.SpacingDimensions = source.SpacingDimensions;
        target.Stirrups = source.Stirrups;
        target.SelectedBar = source.SelectedBar;
        target.Stress = source.Stress;
        target.EffectiveRegion = source.EffectiveRegion;
        target.InvalidateVisual();
    }

    /// <summary>The plot of a 2D domain panel, as the historic DomainPanel builds it.</summary>
    internal static Plot DomainPlot() => new() { InvertY = false, CenteredAxes = true, Title = "Sezione del dominio", EmptyMessage = "Dominio in attesa di aggiornamento automatico" };

    /// <summary>The chart of a 2D domain from its data (ApplyDomainGeometry and ApplyDomainActions of PlotPresentation, the factors of the axes).</summary>
    internal static void Apply(Plot target, ConcreteDomain2DViewModel source)
    {
        target.Series = [];
        target.Segments = [.. source.Segments];
        target.EmptyMessage = source.EmptyMessage;
        target.Title = source.Title; target.XLabel = source.XLabel; target.YLabel = source.YLabel; target.Note = source.Note;
        target.Markers = source.Markers.Select(m => new PlotMarker(m.X, m.Y, m.Label, Ui.Brush(m.Color), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius)).ToList();
        target.VerificationSegments = [.. source.VerificationSegments];
        target.ScaleX = source.ScaleX; target.ScaleY = source.ScaleY; target.FitIncludesMarkers = source.FitIncludesMarkers;
        target.ToolTip = Anthea.Application.Presentation.UtilizationColors.Legend;
        target.InvalidateVisual();
    }
}

/// <summary>
/// The figures of ConcreteSheetViewModel (F5.10 S9) in the desktop: the drawings of the historic view built from the data of the sheet, then the
/// figures of the report as the historic view draws them (<see cref="ModuleFigureRenderer.ConcreteFigures"/>, unchanged).
/// </summary>
internal sealed class ConcreteSectionFigureRenderer : IConcreteSectionFigureRenderer
{
    public IReadOnlyList<ReportFigure> Render(ConcreteReportDrawings drawings, IReadOnlySet<string> options)
    {
        ArgumentNullException.ThrowIfNull(drawings);
        var preview = new ConcreteSectionViewport();
        ConcreteSectionDrawings.Apply(preview, drawings.Preview);
        var domains = drawings.Domains.Select(panel =>
        {
            var plot = ConcreteSectionDrawings.DomainPlot();
            if (panel.Chart is { } chart) { ConcreteSectionDrawings.Apply(plot, chart); plot.Zoom = panel.Zoom; }
            return new ConcreteDomainGraphic(panel.ThreeD, panel.Options, panel.Rows, panel.Selected, panel.Three, panel.Two, panel.Checks, plot);
        }).ToArray();
        var stresses = drawings.Stresses.ToDictionary(p => p.Key, p =>
        {
            var map = new ConcreteSectionViewport();
            ConcreteSectionDrawings.Apply(map, p.Value.Map);
            return new ConcreteStressGraphic(map, p.Value.Checks, p.Value.Rows);
        });
        Plot? curve = null;
        if (drawings.Curvature is { } curvature)
        {
            curve = PlotPresentation.CurvaturePlot();
            PlotPresentation.ApplyCurvature(curve, curvature);
        }
        return ModuleFigureRenderer.ConcreteFigures(new ConcreteReportGraphics(drawings.Data, drawings.Result, preview, domains, stresses, curve), options);
    }
}
