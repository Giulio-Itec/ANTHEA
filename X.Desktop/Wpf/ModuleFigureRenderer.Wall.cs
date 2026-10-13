using Anthea.Application.Walls;
using Anthea.Application.Reporting;
using System.Windows;
using X.Core;
namespace X.Desktop;

internal sealed partial class ModuleFigureRenderer
{
    internal static IReadOnlyList<ReportFigure> WallFigures(WallModel.Result calculation, WallReportPresentation context)
    {
        var figures = new List<ReportFigure>(); var c = context.Case;
        foreach (var member in new[] { "Geometria", "Fusto", "Valle", "Monte", "Armature" })
        {
            if (member == "Armature" && calculation.Input.S("family") != "cantilever") continue;
            if (member is "Valle" or "Monte" && (!c.Contact.Valid || !c.Sections.Any(s => s.Name == member))) continue;
            double figureWidth = member == "Armature" ? 760 : 1100, figureHeight = member == "Armature" ? 560 : 480;
            var view = new RetainingWallDrawing { Data = calculation.Input, Calculation = calculation, Case = c, Width = figureWidth, Height = figureHeight,
                Diagrams = member is not ("Geometria" or "Armature"), Mode = member == "Armature" ? "Armature" : "Geometria e carichi", Member = member, CombinedLoads = false };
            view.Measure(new Size(figureWidth, figureHeight)); view.Arrange(new Rect(0, 0, figureWidth, figureHeight)); view.UpdateLayout();
            figures.Add(new(member == "Geometria" ? "Sezione, terreno e armature · carichi caratteristici; spinte: " + c.Name : member == "Armature" ? "Sezione armata con pieghe e sovrapposizioni" : "Diagrammi " + member + " · " + c.Name, Ui.DocumentSnapshot(view), "muro", AspectRatio: figureWidth / figureHeight));
        }
        if (calculation.GlobalStability is { } global)
        {
            var view = new GlobalStabilityDrawing { Data = calculation.Input, Result = global, Case = context.GlobalCase, Width = 1100, Height = 460 };
            view.Measure(new Size(1100, 460)); view.Arrange(new Rect(0, 0, 1100, 460)); view.UpdateLayout();
            figures.Add(new("Stabilità globale · superficie critica della combinazione visualizzata", Ui.DocumentSnapshot(view), "globale", AspectRatio: 1100d / 460));
        }
        if (context.Schedule is { } schedule)
            figures.AddRange(BarFigures(new RetainingWallBarDrawing(calculation.Input, schedule))
                .Select(f => new ReportFigure(f.Caption, f.Png, "distinta", AspectRatio: f.AspectRatio)));
        return figures;
    }
    internal static IReadOnlyList<ReportRetainingWall.Figure> BarFigures(RetainingWallBarDrawing drawing)
    {
        int oldPage = drawing.Page; var figures = new List<ReportRetainingWall.Figure>();
        try
        {
            for (int i = 0; i < drawing.PageCount; i++)
            {
                drawing.Page = i;
                figures.Add(new("Tavola armature e distinta ferri · " + (i + 1), drawing.Png(3360, 2370), 1120d / 790));
            }
        }
        finally { drawing.Page = oldPage; }
        return figures;
    }
}
