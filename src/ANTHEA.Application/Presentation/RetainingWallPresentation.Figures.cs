using Anthea.Application.Reporting;
using Anthea.Application.Walls;

namespace Anthea.Application.Presentation;

/// <summary>What a figure of the report of the wall draws.</summary>
public enum WallFigureKind
{
    /// <summary>Section, soil and reinforcement with the characteristic loads and the pressures of the combination.</summary>
    Section,
    /// <summary>The diagrams of a member (stem, toe, heel) for the combination.</summary>
    Diagrams,
    /// <summary>The reinforced section with bends and laps (cantilever).</summary>
    Reinforcement,
    /// <summary>The critical surface of the global stability.</summary>
    Global,
    /// <summary>A page of the drawing of the reinforcement and bar schedule (cantilever).</summary>
    BarSchedule
}

/// <summary>A figure of the report of the wall: kind, member, caption, size of the drawing, category and aspect ratio of the image; the page of the bar schedule.</summary>
public sealed record WallFigurePlan(WallFigureKind Kind, string Member, string Caption, double Width, double Height, string Category, double AspectRatio, int Page = 0);

/// <summary>The figures of the report of the wall (F5.9): the plan that ModuleFigureRenderer.WallFigures draws, shared with the ViewModel.</summary>
public static partial class RetainingWallPresentation
{
    private const double MemberFigureWidth = 1100, MemberFigureHeight = 480, ReinforcementFigureWidth = 760, ReinforcementFigureHeight = 560;
    private const double GlobalFigureWidth = 1100, GlobalFigureHeight = 460;
    /// <summary>The sheet of the bar schedule (the drawing) and the size of its images in the report and in the exports.</summary>
    public const double BarSheetWidth = 1120, BarSheetHeight = 790;
    public const int BarSheetPngWidth = 3360, BarSheetPngHeight = 2370;
    private const int WarningsPerPage = 8;
    /// <summary>Rows of the bar schedule on a page of the drawing (shape, Ø, number, lengths, weight).</summary>
    public const int BarRowsPerPage = 6;

    /// <summary>The aspect ratio of a page of the bar schedule.</summary>
    public static double BarSheetAspect => BarSheetWidth / BarSheetHeight;

    /// <summary>The pages of the bar schedule with the shapes, <see cref="BarRowsPerPage"/> positions a page.</summary>
    public static int BarShapePages(WallModel.BarSchedule schedule) => (int)Math.Ceiling(schedule.Bars.Count / (double)BarRowsPerPage);

    /// <summary>The pages of the drawing: the reinforced section with the totals per diameter, the bar schedule, the criteria (eight warnings a page).</summary>
    public static int BarSchedulePages(WallModel.BarSchedule schedule) => 1 + BarShapePages(schedule) + Math.Max(1, (int)Math.Ceiling(schedule.Warnings.Count / (double)WarningsPerPage));

    /// <summary>The figure of «Word globale»: the global stability with the combination shown (the export of the historic view draws it with the same size).</summary>
    public static WallFigurePlan GlobalExportFigure { get; } = new(WallFigureKind.Global, "", "Stabilità globale · superficie visualizzata", GlobalFigureWidth, GlobalFigureHeight, "globale",
        GlobalFigureWidth / GlobalFigureHeight);

    /// <summary>The caption of a page of the bar schedule.</summary>
    public static string BarScheduleCaption(int page) => "Tavola armature e distinta ferri · " + (page + 1);

    /// <summary>
    /// The figures of the report, in order: section, diagrams of the stem, of the toe and of the heel (only with a valid contact and
    /// sections of that member), the reinforced section (cantilever), the global stability when calculated, then every page of the bar
    /// schedule (when the report has one).
    /// </summary>
    public static IReadOnlyList<WallFigurePlan> Figures(WallModel.Result calculation, WallReportPresentation context)
    {
        var figures = new List<WallFigurePlan>(); var c = context.Case;
        foreach (var member in new[] { "Geometria", "Fusto", "Valle", "Monte", "Armature" })
        {
            if (member == "Armature" && calculation.Input.S("family") != "cantilever") continue;
            if (member is "Valle" or "Monte" && (!c.Contact.Valid || !c.Sections.Any(s => s.Name == member))) continue;
            double width = member == "Armature" ? ReinforcementFigureWidth : MemberFigureWidth, height = member == "Armature" ? ReinforcementFigureHeight : MemberFigureHeight;
            var kind = member == "Geometria" ? WallFigureKind.Section : member == "Armature" ? WallFigureKind.Reinforcement : WallFigureKind.Diagrams;
            string caption = member == "Geometria" ? "Sezione, terreno e armature · carichi caratteristici; spinte: " + c.Name
                : member == "Armature" ? "Sezione armata con pieghe e sovrapposizioni" : "Diagrammi " + member + " · " + c.Name;
            figures.Add(new(kind, member, caption, width, height, "muro", width / height));
        }
        if (calculation.GlobalStability is not null)
            figures.Add(new(WallFigureKind.Global, "", "Stabilità globale · superficie critica della combinazione visualizzata", GlobalFigureWidth, GlobalFigureHeight, "globale",
                GlobalFigureWidth / GlobalFigureHeight));
        if (context.Schedule is { } schedule)
            for (int page = 0; page < BarSchedulePages(schedule); page++)
                figures.Add(new(WallFigureKind.BarSchedule, "", BarScheduleCaption(page), BarSheetPngWidth, BarSheetPngHeight, "distinta", BarSheetAspect, page));
        return figures;
    }
}
