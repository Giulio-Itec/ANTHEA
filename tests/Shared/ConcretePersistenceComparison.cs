using System.Text.Json.Nodes;

/// <summary>Compare the existing editor contract separately from its new native archive.
/// Only input.sezione_gpc in an RC workspace is projected out; every other value and identifier remains exact.
/// The native graph, corruption handling and numerical round trips have dedicated persistence tests.</summary>
internal static class ConcretePersistenceComparison
{
    internal static bool Equivalent(JsonNode? left, JsonNode? right)
        => JsonNode.DeepEquals(EditorData(left), EditorData(right));

    internal static JsonNode? EditorData(JsonNode? node)
    {
        var copy = node?.DeepClone();
        void Visit(JsonNode? value)
        {
            if (value is JsonObject obj)
            {
                if (obj["input"] is JsonObject input &&
                    (obj["workspace_ca"] is JsonObject || obj.ContainsKey("versione_sezione")))
                    input.Remove("sezione_gpc");
                foreach (var pair in obj) Visit(pair.Value);
            }
            else if (value is JsonArray array) foreach (var item in array) Visit(item);
        }
        Visit(copy);
        return copy;
    }
}
