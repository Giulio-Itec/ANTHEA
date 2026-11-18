using Anthea.Application.Modules;
using Anthea.Application.Walls;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using System.Text.Json.Nodes;
using System.Windows.Threading;
namespace X.Desktop;

internal sealed partial class ModuleFigureRenderer : IFigureRenderer
{
    public async Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        await Dispatcher.Yield(DispatcherPriority.Background);
        var c = request.Calculation;
        if (c.ModuleId == Anthea.Reports.QuickSectionReport.Module)
            return new QuickSectionFigureRenderer().Render((Anthea.Application.Concrete.ConcreteQuickReportAnalysis)request.Context!, request.Options);
        if (c.ModuleId == "str_palo") return ConcreteFigures(ConcreteGraphics(c.NativeAs<Anthea.Application.Concrete.ConcreteReportAnalysis>()), request.Options);
        if (c.ModuleId is HorizontalPileEditor.Module or MicropaloOrizzontale.Module) return !request.ProjectReport && request.Options.Contains(Anthea.Reports.PileReportContents.Version) ? HorizontalPileReportFigures.Figures(c.Values, request.Options.Contains(Anthea.Reports.PileReportContents.Brief)) : HorizontalFigures(c.Values);
        if (c.ModuleId == BridgeEditor.Module)
            return BridgeFigures(BridgeResultView.Read(c), c.Data["ui_mista"]!.AsObject(), request.Options);
        if (c.ModuleId == WallModel.Module)
            return WallFigures(WallModel.Read(c), (WallReportPresentation)request.Context!);
        if (c.ModuleId == BridgeDesignModel.Module)
        {
            var drawing = new BridgeDesignDrawing { Data = c.Data, Result = BridgeDesignModel.Read(c) };
            return [new("Prospetto", drawing.Png(false), "prospetto"), new("Sezione", drawing.Png(true), "sezione")];
        }
        if (c.ModuleId is not ("geo_palo_verticale" or "geo_micropalo_verticale")) throw new NotSupportedException("Figure del modulo non disponibili: " + c.ModuleId);
        bool micro = c.ModuleId == "geo_micropalo_verticale";
        bool brief = !request.ProjectReport && request.Options.Contains(Anthea.Reports.PileReportContents.Brief);
        var capacity = new Plot { Capacity = true, Title = VerticalPilePresentation.CapacityTitle(micro),
            YLabel = VerticalPilePresentation.CapacityAxis(micro, c.Values), CapacityDepth = VerticalPilePresentation.AvailableDepth(c.Values), EmptyMessage = VerticalPilePresentation.NoCurveSelected,
            Series = VerticalPileCharts.Capacity(c.Data, c.Values, micro).Where(s => (brief || s.Visible) && VerticalPileCharts.CapacityIncludes(s.Key, micro, 0)).Select(s => PlotPresentation.Series(s.Series)).ToList() };
        var reference = new Plot { InvertY = false };
        PlotPresentation.Apply(reference, VerticalPileCharts.Reference(c.Data, c.Values, micro, 0));
        return VerticalFigures(c.Data, c.Values, micro, capacity, reference, cancellation, brief, !request.ProjectReport && request.Options.Contains(Anthea.Reports.PileReportContents.Version));
    }
    /// <summary>
    /// The figures of the report of a vertical pile sheet, drawn in the order, with the captions and the sizes of
    /// <see cref="VerticalPilePresentation.Figures"/> (F5.6: the plan is shared with the report of VerticalPileSheetViewModel).
    /// </summary>
    internal static IReadOnlyList<ReportFigure> VerticalFigures(JsonObject data, JsonObject result, bool micro, Plot capacity, Plot reference, CancellationToken cancellation = default, bool capacityOnly = false, bool reportLayout = false)
    {
        var images = new List<ReportFigure>();
        foreach (var figure in VerticalPilePresentation.Figures(data, micro, capacity.Title, reference.Title))
        {
            if (capacityOnly && figure.Kind != VerticalPileFigureKind.Capacity) continue;
            if (figure.Kind is VerticalPileFigureKind.Abacus or VerticalPileFigureKind.Profile) cancellation.ThrowIfCancellationRequested();
            byte[] png = figure.Kind switch
            {
                VerticalPileFigureKind.Capacity => CapacityImage(capacity, figure.Width, figure.Height, !micro || reportLayout || capacityOnly, capacityOnly && micro ? 430 : 520),
                VerticalPileFigureKind.Reference => micro && !reportLayout ? reference.Png(figure.Width, figure.Height) : reference.ReportPng(),
                VerticalPileFigureKind.Abacus => reportLayout ? Abacus(data, result, figure.Survey).ReportPng() : Abacus(data, result, figure.Survey).Png(figure.Width, figure.Height),
                VerticalPileFigureKind.Profile => ProfileImage(data, micro, figure, reportLayout),
                _ => new StratigraphyDrawing { Data = data, Micro = micro, ShowAll = true }.Png(figure.Width, figure.Height)
            };
            images.Add(new(figure.Caption, png, figure.Category, figure.ReportIndex));
        }
        return images;
    }

    private static byte[] CapacityImage(Plot plot, int width, int height, bool legend, int reportHeight = 520)
    {
        bool original = plot.ShowCapacityLegend;
        try { plot.ShowCapacityLegend = legend; return legend ? plot.ReportPng(640, reportHeight) : plot.Png(width, height); }
        finally { plot.ShowCapacityLegend = original; }
    }

    private static byte[] ProfileImage(JsonObject data, bool micro, VerticalPileFigurePlan figure, bool reportLayout)
    {
        var drawing = new StratigraphyDrawing { Data = data, Micro = micro, SelectedIndex = figure.Survey, ShowAll = false };
        return micro && !reportLayout ? drawing.Png(figure.Width, figure.Height) : drawing.ReportPng();
    }

    private static Plot Abacus(JsonObject data, JsonObject result, int survey)
    {
        var abacus = new Plot { InvertY = false };
        PlotPresentation.Apply(abacus, VerticalPileCharts.Reference(data, result, true, survey));
        return abacus;
    }
}
