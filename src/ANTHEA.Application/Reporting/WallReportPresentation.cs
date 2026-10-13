using Anthea.Calculations.Geotechnics;
namespace Anthea.Application.Reporting;

public sealed record WallReportPresentation(RetainingWall.LoadCase Case, SlopeCaseResult? GlobalCase, RetainingWall.BarSchedule? Schedule)
{
    public static WallReportPresentation ForProject(RetainingWall.Result result) => Prepare(result, result.Cases[0],
        result.GlobalStability?.Cases.OrderByDescending(c => c.Critical?.Ratio ?? double.PositiveInfinity).FirstOrDefault());
    public static WallReportPresentation Prepare(RetainingWall.Result result, RetainingWall.LoadCase selected, SlopeCaseResult? global)
    {
        RetainingWall.BarSchedule? schedule = null;
        if (result.Input.S("family") == "cantilever")
            try { schedule = RetainingWall.CalculateBarSchedule(result); }
            catch (ArgumentException) { /* Report tables already record invalid schedule inputs. */ }
        return new(selected, global, schedule);
    }
}
