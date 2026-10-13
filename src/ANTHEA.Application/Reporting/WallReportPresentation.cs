using Anthea.Application.Walls;
namespace Anthea.Application.Reporting;

public sealed record WallReportPresentation(WallModel.LoadCase Case, SlopeCaseResult? GlobalCase, WallModel.BarSchedule? Schedule)
{
    internal static WallReportPresentation ForProject(RetainingWall.Result result) => ForProject(new WallModel.Result(result));
    private static WallReportPresentation ForProject(WallModel.Result result) => Prepare(result, result.Cases[0],
        result.GlobalStability?.Cases.OrderByDescending(c => c.Critical?.Ratio ?? double.PositiveInfinity).FirstOrDefault());
    public static WallReportPresentation Prepare(WallModel.Result result, WallModel.LoadCase selected, SlopeCaseResult? global)
    {
        WallModel.BarSchedule? schedule = null;
        if (result.Input.S("family") == "cantilever")
            try { schedule = WallModel.CalculateBarSchedule(result); }
            catch (ArgumentException) { /* Report tables already record invalid schedule inputs. */ }
        return new(selected, global, schedule);
    }
}
