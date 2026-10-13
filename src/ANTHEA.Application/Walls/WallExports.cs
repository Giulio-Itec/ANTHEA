using System.Text.Json.Nodes;
using Anthea.Reports;
using X.Core;
namespace Anthea.Application.Walls;

/// <summary>Exports reuse the completed calculation and the existing report implementation.</summary>
public static class WallExports
{
    public static IEnumerable<WallModel.Check> Envelope(IEnumerable<WallModel.Check> checks) => ReportRetainingWall.Envelope(checks.Select(c => c.Source)).Select(c => new WallModel.Check(c));
    public static string Csv(WallModel.Result result) => ReportRetainingWall.Csv(result.Source);
    public static string GlobalCsv(SlopeResult result) => ReportRetainingWall.GlobalCsv(result.Source);
    public static string BarScheduleCsv(WallModel.BarSchedule schedule) => ReportRetainingWall.BarScheduleCsv(schedule.Source);
    public static DocxDocument CreateDocument(string title, WallModel.Result result, IReadOnlyList<ReportRetainingWall.Figure>? figures = null)
        => ReportRetainingWall.CreateDocument(title, result.Source, figures);
    public static byte[] CreateGlobal(JsonObject input, SlopeResult result, ReportRetainingWall.Figure? figure = null)
        => ReportRetainingWall.CreateGlobal(input, result.Source, figure);
    public static byte[] CreateBarSchedule(JsonObject input, WallModel.BarSchedule schedule, IReadOnlyList<ReportRetainingWall.Figure>? figures = null)
        => ReportRetainingWall.CreateBarSchedule(input, schedule.Source, figures);
}
