using System.Text.Json.Nodes;

/// <summary>Compares freshly created defaults; wall action identities belong to each new document.</summary>
internal static class DefaultDataComparison
{
    public static bool Equals(string module, JsonNode? left, JsonNode? right)
    {
        if (module != "geo_muri_sostegno") return JsonNode.DeepEquals(left, right);
        return TryProjectWall(left, out var a) && TryProjectWall(right, out var b)
            && JsonNode.DeepEquals(a, b);
    }

    private static bool TryProjectWall(JsonNode? data, out JsonObject? projected)
    {
        projected = null;
        if (data is not JsonObject source || source["actions"] is not JsonArray actions) return false;
        var ids = new HashSet<Guid>();
        foreach (var action in actions)
            if (action is not JsonObject row || row["id"] is not JsonValue value
                || !value.TryGetValue<string>(out var id) || !Guid.TryParseExact(id, "N", out var guid)
                || guid == Guid.Empty || !ids.Add(guid)) return false;

        projected = (JsonObject)source.DeepClone();
        // Only this identity field is volatile. Values, order, references and other IDs stay exact.
        for (var i = 0; i < actions.Count; i++) projected["actions"]![i]!["id"] = "action-" + i;
        return true;
    }
}
