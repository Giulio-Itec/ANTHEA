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
        if (c.ModuleId == "str_palo") return ConcreteFigures(ConcreteGraphics(c.NativeAs<Anthea.Application.Concrete.ConcreteReportAnalysis>()), request.Options);
        if (c.ModuleId is HorizontalPileEditor.Module or MicropaloOrizzontale.Module) return HorizontalFigures(c.Values);
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
        var capacity = new Plot { Capacity = true, Title = VerticalPilePresentation.CapacityTitle(micro),
            YLabel = VerticalPilePresentation.CapacityAxis(micro, c.Values), CapacityDepth = VerticalPilePresentation.AvailableDepth(c.Values), EmptyMessage = VerticalPilePresentation.NoCurveSelected,
            Series = VerticalPileCharts.Capacity(c.Data, c.Values, micro).Where(s => s.Visible && VerticalPileCharts.CapacityIncludes(s.Key, micro, 0)).Select(s => PlotPresentation.Series(s.Series)).ToList() };
        var reference = new Plot { InvertY = false };
        PlotPresentation.Apply(reference, VerticalPileCharts.Reference(c.Data, c.Values, micro, 0));
        return VerticalFigures(c.Data, c.Values, micro, capacity, reference, cancellation);
    }
    /// <summary>
    /// The figures of the report of a vertical pile sheet, drawn in the order, with the captions and the sizes of
    /// <see cref="VerticalPilePresentation.Figures"/> (F5.6: the plan is shared with the report of VerticalPileSheetViewModel).
    /// </summary>
    internal static IReadOnlyList<ReportFigure> VerticalFigures(JsonObject data, JsonObject result, bool micro, Plot capacity, Plot reference, CancellationToken cancellation = default)
    {
        var images = new List<ReportFigure>();
        foreach (var figure in VerticalPilePresentation.Figures(data, micro, capacity.Title, reference.Title))
        {
            if (figure.Kind is VerticalPileFigureKind.Abacus or VerticalPileFigureKind.Profile) cancellation.ThrowIfCancellationRequested();
            byte[] png = figure.Kind switch
            {
                VerticalPileFigureKind.Capacity => capacity.Png(figure.Width, figure.Height),
                VerticalPileFigureKind.Reference => reference.Png(figure.Width, figure.Height),
                VerticalPileFigureKind.Abacus => Abacus(data, result, figure.Survey).Png(figure.Width, figure.Height),
                VerticalPileFigureKind.Profile => new StratigraphyDrawing { Data = data, Micro = micro, SelectedIndex = figure.Survey, ShowAll = false }.Png(figure.Width, figure.Height),
                _ => new StratigraphyDrawing { Data = data, Micro = micro, ShowAll = true }.Png(figure.Width, figure.Height)
            };
            images.Add(new(figure.Caption, png, figure.Category, figure.ReportIndex));
        }
        return images;
    }

    private static Plot Abacus(JsonObject data, JsonObject result, int survey)
    {
        var abacus = new Plot { InvertY = false };
        PlotPresentation.Apply(abacus, VerticalPileCharts.Reference(data, result, true, survey));
        return abacus;
    }
}
