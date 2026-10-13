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
        if (c.ModuleId is PaloOrizzontale.Module or MicropaloOrizzontale.Module) return HorizontalFigures(c.Values);
        if (c.ModuleId == BridgeEditor.Module)
            return BridgeFigures(BridgeResultView.Read(c), c.Data["ui_mista"]!.AsObject(), request.Options);
        if (c.ModuleId == RetainingWall.Module)
            return WallFigures(c.NativeAs<RetainingWall.Result>(), (WallReportPresentation)request.Context!);
        if (c.ModuleId == BridgeDesignModel.Module)
        {
            var drawing = new BridgeDesignDrawing { Data = c.Data, Result = BridgeDesignModel.Read(c) };
            return [new("Prospetto", drawing.Png(false), "prospetto"), new("Sezione", drawing.Png(true), "sezione")];
        }
        if (c.ModuleId is not ("geo_palo_verticale" or "geo_micropalo_verticale")) throw new NotSupportedException("Figure del modulo non disponibili: " + c.ModuleId);
        bool micro = c.ModuleId == "geo_micropalo_verticale";
        var capacity = new Plot { Capacity = true, Title = micro ? "Capacità portante del micropalo" : "Capacità portante del palo",
            YLabel = micro ? "Lungo asse s [m]" : "Profondità z [m]", CapacityDepth = c.Values.D("profondita_massima"), EmptyMessage = "Nessuna curva selezionata",
            Series = VerticalPileCharts.Capacity(c.Data, c.Values, micro).Where(s => s.Visible && VerticalPileCharts.CapacityIncludes(s.Key, micro, 0)).Select(s => PlotPresentation.Series(s.Series)).ToList() };
        var reference = new Plot { InvertY = false };
        PlotPresentation.Apply(reference, VerticalPileCharts.Reference(c.Data, c.Values, micro, 0));
        return VerticalFigures(c.Data, c.Values, micro, capacity, reference, cancellation);
    }
    internal static IReadOnlyList<ReportFigure> VerticalFigures(JsonObject data, JsonObject result, bool micro, Plot capacity, Plot reference, CancellationToken cancellation = default)
    {
        var images = new List<ReportFigure> { new(capacity.Title, capacity.Png(), "grafico_capacita") };
        if (micro)
        {
            for (int i = 0; i < data.Array("stratigrafie").Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var abacus = new Plot { InvertY = false };
                PlotPresentation.Apply(abacus, VerticalPileCharts.Reference(data, result, true, i));
                images.Add(new($"Abachi Bustamante–Doix del sondaggio {i + 1}", abacus.Png(), "grafico_nq"));
            }
        }
        else images.Add(new(reference.Title, reference.Png(), "grafico_nq"));
        for (int i = 0; i < data.Array("stratigrafie").Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var profile = new StratigraphyDrawing { Data = data, Micro = micro, SelectedIndex = i, ShowAll = false };
            images.Add(new($"Profilo del sondaggio {i + 1}", profile.Png(720, micro ? 600 : 900), "stratigrafia", i));
        }
        images.Add(new("Profilo stratigrafico", new StratigraphyDrawing { Data = data, Micro = micro, ShowAll = true }.Png(), "grafico_profilo"));
        return images;
    }
}
