using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using System.Windows;
using X.Core;
namespace X.Desktop;

internal sealed partial class ModuleFigureRenderer
{
    /// <summary>The figures of the report of the wall, drawn as RetainingWallPresentation.Figures plans them (F5.9: the plan is shared with the ViewModel).</summary>
    internal static IReadOnlyList<ReportFigure> WallFigures(WallModel.Result calculation, WallReportPresentation context)
    {
        var figures = new List<ReportFigure>(); var c = context.Case;
        RetainingWallBarDrawing? sheet = null;
        foreach (var figure in RetainingWallPresentation.Figures(calculation, context))
        {
            switch (figure.Kind)
            {
                case WallFigureKind.Global:
                {
                    var view = new GlobalStabilityDrawing { Data = calculation.Input, Result = calculation.GlobalStability, Case = context.GlobalCase, Width = figure.Width, Height = figure.Height };
                    view.Measure(new Size(figure.Width, figure.Height)); view.Arrange(new Rect(0, 0, figure.Width, figure.Height)); view.UpdateLayout();
                    figures.Add(new(figure.Caption, Ui.DocumentSnapshot(view), figure.Category, AspectRatio: figure.AspectRatio));
                    break;
                }
                case WallFigureKind.BarSchedule:
                    sheet ??= new RetainingWallBarDrawing(calculation.Input, context.Schedule!);
                    figures.Add(new(figure.Caption, BarPage(sheet, figure.Page), figure.Category, AspectRatio: figure.AspectRatio));
                    break;
                default:
                {
                    var view = new RetainingWallDrawing { Data = calculation.Input, Calculation = calculation, Case = c, Width = figure.Width, Height = figure.Height,
                        Diagrams = figure.Kind == WallFigureKind.Diagrams, Mode = figure.Kind == WallFigureKind.Reinforcement ? "Armature" : "Geometria e carichi", Member = figure.Member, CombinedLoads = false };
                    view.Measure(new Size(figure.Width, figure.Height)); view.Arrange(new Rect(0, 0, figure.Width, figure.Height)); view.UpdateLayout();
                    figures.Add(new(figure.Caption, Ui.DocumentSnapshot(view), figure.Category, AspectRatio: figure.AspectRatio));
                    break;
                }
            }
        }
        return figures;
    }
    internal static IReadOnlyList<ReportRetainingWall.Figure> BarFigures(RetainingWallBarDrawing drawing)
    {
        var figures = new List<ReportRetainingWall.Figure>();
        for (int i = 0; i < drawing.PageCount; i++)
            figures.Add(new(RetainingWallPresentation.BarScheduleCaption(i), BarPage(drawing, i), RetainingWallPresentation.BarSheetAspect));
        return figures;
    }
    /// <summary>A page of the bar schedule as an image of the report; the page shown by the drawing does not change.</summary>
    private static byte[] BarPage(RetainingWallBarDrawing drawing, int page)
    {
        int oldPage = drawing.Page;
        try { drawing.Page = page; return drawing.Png(RetainingWallPresentation.BarSheetPngWidth, RetainingWallPresentation.BarSheetPngHeight); }
        finally { drawing.Page = oldPage; }
    }
}
