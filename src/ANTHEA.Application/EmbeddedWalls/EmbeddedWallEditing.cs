using System.Text.Json.Nodes;

namespace Anthea.Application.EmbeddedWalls;

public static partial class EmbeddedWallDocument
{
    /// <summary>Defaults for a newly added editor row. Assigns identifiers and ownership only, never engineering values.</summary>
    public static JsonObject NewEditingRow(JsonObject data, string table, string caseId, string? stageId)
    {
        var row = NewRow(table);
        if (HistoryTables.Contains(table))
        {
            row["case"] = caseId == data["case_id"]?.ToString() ? "" : caseId;
            if (row.ContainsKey("stage") && stageId is not null) row["stage"] = stageId;
        }
        if (row.ContainsKey("id"))
        {
            var used = Rows(data, table).Select(r => r["id"]?.ToString() ?? "").ToHashSet(StringComparer.Ordinal);
            if (table == "cases") used.Add(data["case_id"]?.ToString() ?? "");
            string initial = row["id"]!.ToString(), candidate = initial;
            int suffix = 1;
            while (used.Contains(candidate))
            {
                suffix++;
                candidate = table switch { "stages" => "F" + suffix, "supports" => "T" + suffix,
                    "distributed_loads" => "Q" + suffix, "cases" => "Caso " + suffix, _ => initial + " " + suffix };
            }
            row["id"] = candidate;
        }
        return row;
    }
}
