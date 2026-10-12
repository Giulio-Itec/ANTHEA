using System.Text.Json.Nodes;
using Anthea.Reports;
namespace X.Core;

/// <summary>Application projection for a project report. Composition and serialization are owned by Reports.</summary>
public static class ReportProject
{
    public sealed record SheetContent(byte[]? Docx = null, string? Error = null,
        IReadOnlyList<ProjectReportPlan.InputValue>? Derived = null, DocxDocument? Document = null);

    private static IReadOnlyList<ProjectReportData.InputValue> Values(IEnumerable<ProjectReportPlan.InputValue> source)
    {
        var result = new List<ProjectReportData.InputValue>();
        foreach (var item in source)
            if (item.Value is JsonArray or JsonObject)
                foreach (var nested in ProjectReportPlan.Walk(item.Value, item.Label))
                    result.Add(new(item.Group, ProjectReportPlan.PathLabel(nested.Path), nested.Value));
            else result.Add(new(item.Group, item.Label, item.Value));
        return result;
    }
    public static DocxDocument Compose(ProjectReportPlan plan, IReadOnlyDictionary<JsonObject, SheetContent> reports,
        IReadOnlyList<string> warnings, CancellationToken cancellation = default, bool materialSheet = false)
    {
        var conflicts = plan.Conflicts.GroupBy(c => c.Key).Select(group => new ProjectReportData.Conflict(group.Key,
            ProjectReportPlan.Label(group.Key), group.SelectMany(d => new[] { d.First, d.Second }).Distinct()
                .Select(sheet => new ProjectReportData.Participant(sheet, ProjectSharedData.Fields(sheet)[group.Key].Value)).ToArray())).ToArray();
        var locations = plan.Sheets.Concat(plan.Conflicts.SelectMany(d => new[] { d.First, d.Second })).Distinct()
            .ToDictionary(sheet => sheet, ProjectSharedData.Location);
        var projection = new ProjectReportData(plan.Root, plan.Sheets,
            plan.Common.Select(v => new ProjectReportData.CommonValue(v.Section, ProjectSharedData.Location(v.Source), v.Field.Group,
                v.Sheets, Values([new(v.Field.Group, ProjectReportPlan.Label(v.Field.Key), v.Field.Value)]))).ToArray(),
            conflicts, plan.Sheets.ToDictionary(s => s, s => Values(plan.LocalInputs(s))), locations);
        var content = reports.ToDictionary(p => p.Key, p => new ProjectReportData.SheetContent(p.Value.Document ??
            (p.Value.Docx is null ? null : DocxWriter.Read(p.Value.Docx)), p.Value.Error,
            p.Value.Derived is null ? null : Values(p.Value.Derived)));
        return ProjectReportComposer.Compose(projection, content, warnings.Concat(ProjectValidation.Warnings(plan.Root)).Distinct().ToArray(), cancellation, materialSheet);
    }
    public static void Write(string path, ProjectReportPlan plan, IReadOnlyDictionary<JsonObject, SheetContent> reports,
        IReadOnlyList<string> warnings, CancellationToken cancellation = default, bool materialSheet = false)
    {
        var document = Compose(plan, reports, warnings, cancellation, materialSheet);
        var bytes = DocxWriter.Write(document);
        cancellation.ThrowIfCancellationRequested();
        DocumentFile.Write(path, bytes);
    }
}
