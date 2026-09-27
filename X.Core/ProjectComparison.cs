using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>One short-lived read of common fields for a project comparison/report. Never retained across edits.</summary>
public sealed class ProjectComparison
{
    public sealed record Match(JsonObject First, JsonObject Second, ProjectSharedData.Field Source, ProjectSharedData.Field Target);
    public Dictionary<JsonObject, Dictionary<string, ProjectSharedData.Field>> Fields { get; }
    public List<Match> Matches { get; } = [];
    public List<ProjectSharedData.Difference> Differences { get; } = [];
    public ProjectComparison(JsonObject section)
    {
        Fields = ProjectSharedData.ContextSheets(section).Distinct().ToDictionary(s => s, ProjectSharedData.Fields);
        foreach (var (first, second) in ProjectSharedData.ComparisonPairs(section))
            foreach (var (a, b) in ProjectSharedData.ComparableFields(first, second, Fields[first], Fields[second]))
            {
                Matches.Add(new(first, second, a, b));
                if (!ProjectSharedData.Equal(a.Value, b.Value))
                    Differences.Add(new(first, second, a.Group, a.Key, ProjectSharedData.Text(a.Value), ProjectSharedData.Text(b.Value)));
            }
    }
}
