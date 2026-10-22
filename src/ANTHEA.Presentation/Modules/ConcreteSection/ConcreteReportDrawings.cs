using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Reporting;
using Anthea.Calculations;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// Draws the figures of the report of the RC section (F5.10, sub-step S9; in the desktop the WPF drawings of ModuleFigureRenderer.ConcreteFigures,
/// unchanged: reports frozen on 8/10). The sheet gives the plain data of its drawings in the moment of the report, as the historic view gives its
/// controls; the renderer builds the drawings from them and returns the figures in the order and with the captions of the report.
/// </summary>
public interface IConcreteSectionFigureRenderer
{
    /// <summary>The figures of the report for the sections <paramref name="options"/> (none without «grafici»).</summary>
    IReadOnlyList<ReportFigure> Render(ConcreteReportDrawings drawings, IReadOnlySet<string> options);
}

/// <summary>
/// A domain panel in the moment of the report (ConcreteDomainGraphic of the historic view): 3D or 2D, its options, the rows shown by its table
/// in their order, the selected row, the domains and the checks of its limit state, and for the 2D its chart with its zoom (the Plot of the
/// historic panel).
/// </summary>
public sealed record ConcreteReportDomain(bool ThreeD, JsonObject Options, IReadOnlyList<JsonObject> Rows, string? Selected,
    ConcreteResults.Domain3D? Three, ConcreteResults.Domain2D? Two, IReadOnlyDictionary<string, ConcreteResults.Check>? Checks,
    ConcreteDomain2DViewModel? Chart, double Zoom);

/// <summary>A family of the serviceability in the moment of the report (ConcreteStressGraphic): its map, its outcomes, its rows.</summary>
public sealed record ConcreteReportStress(ConcreteSectionDrawingViewModel Map, IReadOnlyDictionary<string, StressOutcome>? Checks, IReadOnlyList<JsonObject> Rows);

/// <summary>
/// What the figures of the report of the RC section are drawn from (ConcreteReportGraphics of the historic view, as plain data): the data and
/// the export of the results, the preview of the control panel, the two domain panels, the three families of the serviceability and the
/// M–χ curve published (null without a curve).
/// </summary>
public sealed record ConcreteReportDrawings(JsonObject Data, JsonObject Result, ConcreteSectionDrawingViewModel Preview, IReadOnlyList<ConcreteReportDomain> Domains,
    IReadOnlyDictionary<string, ConcreteReportStress> Stresses, ConcreteResults.CurvatureResult? Curvature);
