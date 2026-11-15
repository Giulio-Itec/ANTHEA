using System.Text.Json.Nodes;

namespace Anthea.Application.EmbeddedWalls;

public static partial class EmbeddedWallDocument
{
    /// <summary>Values a new phase takes from the previous phase of its case (A3 of the paratie review, sheet of 16/11/2026): ground levels
    /// and water table. Surcharges, loads, stiffness factor, supports and actions keep the defaults of a new row.</summary>
    public static IReadOnlyList<string> ContinuedStageKeys { get; } = Array.AsReadOnly(new[] { "left_ground", "right_ground", "left_water", "right_water" });

    /// <summary>Defaults for a newly added editor row. Assigns identifiers and ownership only, never engineering values, except a new phase:
    /// it continues ground levels and water table of the last phase of the same case (the row is appended, so that phase precedes it in the
    /// history) instead of restarting at level 0 without water. Without a phase in the case the defaults of the module stay.</summary>
    public static JsonObject NewEditingRow(JsonObject data, string table, string caseId, string? stageId)
    {
        var row = NewRow(table);
        if (HistoryTables.Contains(table))
        {
            row["case"] = caseId == data["case_id"]?.ToString() ? "" : caseId;
            if (row.ContainsKey("stage") && stageId is not null) row["stage"] = stageId;
        }
        if (table == "stages" && Rows(data, "stages").LastOrDefault(r => StageCase(data, r) == caseId) is { } previous)
            foreach (string key in ContinuedStageKeys) row[key] = previous[key]?.DeepClone();
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

    /// <summary>The history of a row of a history table: an empty case is the main case.</summary>
    private static string StageCase(JsonObject data, JsonObject row)
        => string.IsNullOrWhiteSpace(row["case"]?.ToString()) ? data["case_id"]?.ToString() ?? "" : row["case"]!.ToString();
}
