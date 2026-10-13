using System.Text.Json.Nodes;
namespace Anthea.Reports;

/// <summary>Prepared project inputs. Identity references connect headings to their sheets; no document or calculation service is needed by the composer.</summary>
public sealed record ProjectReportData(JsonObject Root, JsonObject[] Sheets,
    IReadOnlyList<ProjectReportData.CommonValue> Common,
    IReadOnlyList<ProjectReportData.Conflict> Conflicts,
    IReadOnlyDictionary<JsonObject, IReadOnlyList<ProjectReportData.InputValue>> Inputs,
    IReadOnlyDictionary<JsonObject, string> Locations)
{
    public sealed record InputValue(string Group, string Label, JsonNode? Value);
    public sealed record CommonValue(JsonObject Section, string Origin, string Group, JsonObject[] Sheets, IReadOnlyList<InputValue> Values);
    public sealed record Participant(JsonObject Sheet, JsonNode? Value);
    public sealed record Conflict(string Key, string Label, IReadOnlyList<Participant> Participants);
    public sealed record SheetContent(DocxDocument? Document = null, string? Error = null, IReadOnlyList<InputValue>? Derived = null);
}
